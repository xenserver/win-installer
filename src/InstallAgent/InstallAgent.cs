using HardwareDevice;
using HelperFunctions;
using Microsoft.Win32;
using PInvokeWrap;
using PVDevice;
using PVDriversRemoval;
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

            while (!Installer.SystemCleaned())
            // use 'while' to abuse the 'break' statement
            {
                if (VM.GetPVToolsVersionOnFirstRun() !=
                    VM.PVToolsVersion.LessThanEight)
                // System clean not needed; flip all relevant flags
                {
                    Installer.SetFlag(Installer.States.RemovedFromFilters);
                    Installer.SetFlag(Installer.States.BootStartDisabled);
                    Installer.SetFlag(Installer.States.MSIsUninstalled);
                    Installer.SetFlag(Installer.States.DrvsAndDevsUninstalled);
                    Installer.SetFlag(Installer.States.CleanedUp);
                    break;
                }

                Trace.WriteLine("PV Tools < 8.x detected; cleaning..");

                if (!PVDriversWipe())
                // Regardless of the 'rebootOption' value, the VM has to
                // reboot after the first 2 actions in PVDriversWipe(),
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

            if (!Installer.EverythingInstalled())
            {
                InstallCertificates();
                PVDriversInstall();
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
                Helpers.BlockUntilNoDriversInstalling(
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

        private static void InstallCertificates()
        // Installs security certificates if they exist
        {
            if (Installer.GetFlag(Installer.States.CertificatesInstalled))
            {
                return;
            }

            string certsDir = Path.Combine(
                InstallAgent.exeDir,
                "Certs"
            );

            if (!Directory.Exists(certsDir))
            {
                Installer.SetFlag(Installer.States.CertificatesInstalled);
                return;
            }

            string[] certNames = {
                "citrixsha1.cer",
                "citrixsha256.cer",
            };

            foreach (string certName in certNames)
            {
                Helpers.InstallCertificate(Path.Combine(certsDir, certName));
            }

            Installer.SetFlag(Installer.States.CertificatesInstalled);
        }

        private static void PVDriversInstall()
        // Installs the set of PV drivers provided
        // by the Management Agent
        {
            string build = WinVersion.Is64BitOS() ? @"\x64\" : @"\x86\";

            string driverRootDir = Path.Combine(
                InstallAgent.exeDir,
                "Drivers"
            );

            var drivers = new[] {
                new { name = "xennet",
                      installed = Installer.States.XenNetInstalled },
                new { name = "xenvif",
                      installed = Installer.States.XenVifInstalled },
                new { name = "xenvbd",
                      installed = Installer.States.XenVbdInstalled },
                new { name = "xeniface",
                      installed = Installer.States.XenIfaceInstalled },
                new { name = "xenbus",
                      installed = Installer.States.XenBusInstalled }
            };

            foreach (var driver in drivers)
            {
                if (!Installer.GetFlag(driver.installed))
                {
                    string infPath = Path.Combine(
                        driverRootDir,
                        driver.name + build + driver.name + ".inf"
                    );

                    Helpers.InstallDriver(infPath);
                    Installer.SetFlag(driver.installed);
                }
            }
        }

        private static bool PVDriversWipe()
        // Wipes the system clean of PV drivers. Has to be called
        // twice, with a VM reboot inbetween. Returns 'true' when
        // all actions are completed; 'false' otherwise
        {
            if (!Installer.GetFlag(Installer.States.RemovedFromFilters))
            {
                PVDriversPurge.RemovePVDriversFromFilters();
                Installer.SetFlag(Installer.States.RemovedFromFilters);
            }

            if (!Installer.GetFlag(Installer.States.BootStartDisabled))
            {
                PVDriversPurge.DontBootStartPVDrivers();
                Installer.SetFlag(Installer.States.BootStartDisabled);
            }

            if (!Installer.GetFlag(Installer.States.ProceedWithSystemClean))
            // Makes Install Agent stop here the first time it runs
            {
                Installer.SetFlag(Installer.States.ProceedWithSystemClean);
                return false;
            }

            // Do 2 passes to decrease the chance of
            // something left behind/not being removed
            const int TIMES = 2;

            for (int i = 0; i < TIMES; ++i)
            {
                if (!Installer.GetFlag(Installer.States.DrvsAndDevsUninstalled))
                {
                    PVDriversPurge.UninstallDriversAndDevices();

                    if (i == TIMES - 1)
                    {
                        Installer.SetFlag(Installer.States.DrvsAndDevsUninstalled);
                    }
                }

                if (!Installer.GetFlag(Installer.States.MSIsUninstalled))
                {
                    PVDriversPurge.UninstallMSIs();

                    if (i == TIMES - 1)
                    {
                        Installer.SetFlag(Installer.States.MSIsUninstalled);
                    }
                }
            }

            if (!Installer.GetFlag(Installer.States.CleanedUp))
            {
                PVDriversPurge.CleanUpXenLegacy();
                PVDriversPurge.CleanUpServices();
                PVDriversPurge.CleanUpDriverFiles();
                Installer.SetFlag(Installer.States.CleanedUp);
            }

            return true;
        }
    }
}