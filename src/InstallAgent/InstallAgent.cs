using Microsoft.Win32;
using PInvoke;
using PVDevice;
using State;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;

namespace InstallAgent
{
    public partial class InstallAgent : ServiceBase
    {
        public static string rootRegKeyName =
            @"SOFTWARE\Citrix\InstallAgent";

        public enum RebootType
        {
            NOREBOOT,
            AUTOREBOOT,
            DEFAULT
        }

        public static /* readonly */ RebootType rebootOption;
        public static readonly string exeDir;

        private Thread installThread = null;

        static InstallAgent()
        {
            exeDir = new DirectoryInfo(
                Assembly.GetExecutingAssembly().Location
            ).Parent.FullName;

            // Just to kick off the static constructor
            // before we start messing with the VM
            VM.GetOtherDriverInstallingOnFirstRun();
        }

        public InstallAgent()
        {
            using (RegistryKey tmpRK = Registry.LocalMachine.CreateSubKey(
                           rootRegKeyName))
            {
                rebootOption = GetTrueRebootType(
                    (RebootType)Enum.Parse(
                        typeof(RebootType),
                        (string)tmpRK.GetValue("RebootOption", "DEFAULT"),
                        true
                    )
                );
            }

            InitializeComponent();
        }

        public InstallAgent(RebootType rebootOption_)
        {
            rebootOption = GetTrueRebootType(rebootOption_);
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            // Start thread - so we can do everything in the background
            Trace.WriteLine("OnStart");
            installThread = new Thread(InstallThreadHandler);
            installThread.Start();
        }

        protected override void OnStop()
        {
            installThread.Join();
        }

        public void InstallThreadHandler()
        {
            if (WinVersion.IsWOW64())
            {
                throw new Exception("WOW64: Do not do that.");
            }

            if (Installer.Complete())
            {
                Trace.WriteLine("Everything is complete!!");
                return;
            }

            //RegisterWMI();

            if (!Installer.GetFlag(Installer.States.NetworkSettingsSaved))
            {
                Trace.WriteLine("NetSettings not saved..");
                XenVif.NetworkSettingsSaveRestore(true);
                Installer.SetFlag(Installer.States.NetworkSettingsSaved);
                Trace.WriteLine("NetSettings saved!");
            }

            if (!Installer.SystemCleaned())
            {
                if (VM.GetPVToolsVersionOnFirstRun() == VM.PVToolsVersion.NotEight)
                {
                    Trace.WriteLine("PV Tools found on 0001 or 0002; xenprepping..");
                    DriverHandler.SystemClean();
                    Trace.WriteLine("xenprepping done!");
                }
                else // "XenPrepping" not needed, so just flip all relevant flags
                {
                    Installer.SetFlag(Installer.States.RemovedFromFilters);
                    Installer.SetFlag(Installer.States.BootStartDisabled);
                    Installer.SetFlag(Installer.States.MSIsUninstalled);
                    Installer.SetFlag(Installer.States.XenLegacyUninstalled);
                    Installer.SetFlag(Installer.States.CleanedUp);
                    Trace.WriteLine("xenprepping not needed; flip relevant flags");
                }
            }

            if (!Installer.EverythingInstalled())
            {
                if (!Installer.GetFlag(Installer.States.CertificatesInstalled))
                {
                    Trace.WriteLine("Installing certificates..");
                    XSToolsInstallation.Helpers.InstallCertificates(
                        Directory.GetCurrentDirectory() + @"\certs");
                    Installer.SetFlag(Installer.States.CertificatesInstalled);
                    Trace.WriteLine("Certificates installed");
                }

                string driverRootDir = Path.Combine(
                    InstallAgent.exeDir,
                    "Drivers"
                );

                var drivers = new[] {
                    new { name = "xennet", flag = Installer.States.XenNetInstalled },
                    new { name = "xenvif", flag = Installer.States.XenVifInstalled },
                    new { name = "xenvbd", flag = Installer.States.XenVbdInstalled },
                    new { name = "xeniface", flag = Installer.States.XenIfaceInstalled },
                    new { name = "xenbus", flag = Installer.States.XenBusInstalled }
                };

                foreach (var driver in drivers)
                {
                    if (!Installer.GetFlag(driver.flag))
                    {
                        if (DriverHandler.InstallDriver_2(driverRootDir, driver.name))
                        {
                            Installer.SetFlag(driver.flag);
                        }
                        else
                        {
                            // Maybe keep number of failed times?
                        }
                    }
                }
            }

            if (PVDevice.PVDevice.AllFunctioning())
            {
                VM.UnsetRebootNeeded();

                XSToolsInstallation.Helpers.ChangeServiceStartMode(
                    this.ServiceName,
                    ServiceStartMode.Manual
                );
            }
            else
            {
                if (rebootOption == RebootType.AUTOREBOOT)
                {
                    if (VM.AllowedToReboot())
                    {
                        CfgMgr32.CMP_WaitNoPendingInstallEvents(
                            GetTimeoutToReboot()
                        );

                        VM.IncrementRebootCount();
                        XSToolsInstallation.Helpers.Reboot();
                    }
                    else
                    {
                        Trace.WriteLine(
                            "VM reached maximum number of allowed reboots"
                        );
                    }
                }
                else
                {
                    VM.SetRebootNeeded();
                }
            }
        }

        private static RebootType GetTrueRebootType(RebootType rt)
        // If 'rebootOption = DEFAULT', the function returns one of the 2
        // other options, depending on the system's state on first run
        {
            if (rt != RebootType.DEFAULT)
            {
                return rt;
            }

            switch (VM.GetPVToolsVersionOnFirstRun())
            {
                case VM.PVToolsVersion.None:
                case VM.PVToolsVersion.NotEight:
                    return RebootType.AUTOREBOOT;
                case VM.PVToolsVersion.Eight:
                    return RebootType.NOREBOOT;
                default:
                    throw new Exception("Cannot get RebootType");
            }
        }

        private static uint GetTimeoutToReboot()
        {
            uint timeout;

            if (VM.GetOtherDriverInstallingOnFirstRun())
            {
                timeout = 20; // arbitrarily chosen;

                Trace.WriteLine(
                    "Something was installing before Install " +
                    "Agent's first run. Timeout: " + timeout +
                    " minutes"
                );

                timeout *= 60 * 1000; // minutes to milliseconds
            }
            else
            {
                Trace.WriteLine(
                    "Will wait until active (if any) PV Tools " +
                    "driver installations have finished"
                );
                timeout = CfgMgr32.INFINITE;
            }

            return timeout;
        }
    }
}