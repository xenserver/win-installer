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
using XSToolsInstallation;

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
            installThread = new Thread(InstallThreadHandler);
            installThread.Start();
        }

        protected override void OnStop()
        {
            installThread.Join();
        }

        public void InstallThreadHandler()
        {
            try
            {
                __InstallThreadHandler();
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
                throw;
            }
        }

        private void __InstallThreadHandler()
        {
            if (WinVersion.IsWOW64())
            {
                throw new Exception("WOW64: Do not do that.");
            }

            if (!Installer.GetFlag(Installer.States.NetworkSettingsSaved))
            {
                Trace.WriteLine("NetSettings not saved..");
                XenVif.NetworkSettingsSaveRestore(true);
                Installer.SetFlag(Installer.States.NetworkSettingsSaved);
                Trace.WriteLine("NetSettings saved!");
            }

            if (!Installer.SystemCleaned())
            {
                if (VM.GetPVToolsVersionOnFirstRun() ==
                    VM.PVToolsVersion.LessThanEight)
                {
                    Trace.WriteLine("PV Tools found on 0001 or 0002; xenprepping..");
                    DriverHandler.SystemClean();
                    Trace.WriteLine("xenprepping done!");

                    // Stop after we XenPrep. Cannot install
                    // drivers without reboot, at least for now..
                    if (rebootOption == RebootType.AUTOREBOOT)
                    {
                        DriverHandler.BlockUntilNoDriversInstalling(
                                GetTimeoutToReboot()
                            );

                        Helpers.Reboot();
                    }
                    else
                    {
                        VM.SetRebootNeeded();
                    }

                    return;
                }
                else // "XenPrepping" not needed, so just flip all relevant flags
                {
                    Installer.SetFlag(Installer.States.RemovedFromFilters);
                    Installer.SetFlag(Installer.States.BootStartDisabled);
                    Installer.SetFlag(Installer.States.MSIsUninstalled);
                    Installer.SetFlag(Installer.States.DrvsAndDevsUninstalled);
                    Installer.SetFlag(Installer.States.CleanedUp);
                    Trace.WriteLine("xenprepping not needed; flip relevant flags");
                }
            }

            if (!Installer.EverythingInstalled())
            {
                if (!Installer.GetFlag(Installer.States.CertificatesInstalled))
                {
                    string certsPath = Path.Combine(
                        InstallAgent.exeDir,
                        "Certs"
                    );

                    if (Directory.Exists(certsPath))
                    {
                        Helpers.InstallCertificates(certsPath);
                    }

                    Installer.SetFlag(Installer.States.CertificatesInstalled);
                }

                DriverHandler.InstallDrivers();
            }

            if (PVDevice.PVDevice.AllFunctioning())
            {
                VM.UnsetRebootNeeded();

                Helpers.ChangeServiceStartMode(
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
                        DriverHandler.BlockUntilNoDriversInstalling(
                            GetTimeoutToReboot()
                        );

                        VM.IncrementRebootCount();
                        Helpers.Reboot();
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

            if (VM.GetPVToolsVersionOnFirstRun() == VM.PVToolsVersion.Eight)
            {
                return RebootType.NOREBOOT;
            }
            else
            {
                return RebootType.AUTOREBOOT;
            }
        }

        private static uint GetTimeoutToReboot()
        {
            uint timeout;

            if (VM.GetOtherDriverInstallingOnFirstRun())
            {
                timeout = 20; // arbitrarily chosen;

                Trace.WriteLine(
                    "Something was installing before " +
                    "Install Agent's first run."
                );

                timeout = 20 * 60; // 20 minutes to seconds
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