using Microsoft.Win32;
using PInvokeWrap;
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
                    Trace.WriteLine("PV Tools < 8.x detected; cleaning..");

                    if (!DriverHandler.SystemClean())
                    // Regardless of the 'rebootOption' value, the VM has to
                    // reboot after the first 2 actions in SystemClean(),
                    // before the new drivers can be installed
                    {
                        Trace.WriteLine(
                            "Prevented old drivers from being used after " +
                            "the system reboots. Install Agent will " +
                            "continue after the reboot"
                        );

                        if (rebootOption == RebootType.AUTOREBOOT)
                        {
                            TryReboot();
                        }
                        else // NOREBOOT
                        {
                            VM.SetRebootNeeded();
                        }

                        return;
                    }

                    // Enumerate the PCI Bus after
                    // cleaning the system
                    Device.Enumerate(@"ACPI\PNP0A03", true);

                    Trace.WriteLine("Old PV Tools removal complete!");
                }
                else // "XenPrepping" not needed, so just flip all relevant flags
                {
                    Installer.SetFlag(Installer.States.RemovedFromFilters);
                    Installer.SetFlag(Installer.States.BootStartDisabled);
                    Installer.SetFlag(Installer.States.MSIsUninstalled);
                    Installer.SetFlag(Installer.States.DrvsAndDevsUninstalled);
                    Installer.SetFlag(Installer.States.CleanedUp);
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
                    TryReboot();
                }
                else
                {
                    VM.SetRebootNeeded();
                }
            }
        }

        private static RebootType GetTrueRebootType(RebootType rt)
        // 'rebootOption = DEFAULT' defaults to NOREBOOT
        {
            if (rt == RebootType.DEFAULT)
            {
                return RebootType.NOREBOOT;
            }
            else
            {
                return rt;
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

        public static void TryReboot()
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
    }
}