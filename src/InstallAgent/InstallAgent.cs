using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;
using System.Collections;
using System.IO;
using System.Threading;
using System.Reflection;

namespace InstallAgent
{
    public partial class InstallAgent : ServiceBase
    {
        public static string rootRegKey =
            @"SOFTWARE\Citrix\InstallAgent\";

        public enum RebootType
        {
            NOREBOOT,
            AUTOREBOOT,
            DEFAULT
        }

        public readonly RebootType rebootOption;
        public readonly int pvToolsVer;
        public static readonly string exeDir;

        private Thread installThread = null;

        static InstallAgent()
        {
            exeDir = new DirectoryInfo(
                Assembly.GetExecutingAssembly().Location
            ).Parent.FullName;
        }

        public InstallAgent()
        {
            this.pvToolsVer = this.GetPVToolsVersionOnFirstRun();

            using (RegistryKey tmpRK = Registry.LocalMachine.CreateSubKey(
                           InstallAgent.rootRegKey))
            {
                this.rebootOption = this.GetTrueRebootType(
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
            this.pvToolsVer = this.GetPVToolsVersionOnFirstRun();
            this.rebootOption = GetTrueRebootType(rebootOption_);
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

            if (InstallerState.Complete())
            {
                Trace.WriteLine("Everything is complete!!");
                return;
            }

            //RegisterWMI();

            if (!InstallerState.GetFlag(InstallerState.States.NetworkSettingsSaved))
            {
                Trace.WriteLine("NetSettings not saved..");
                PVDevice.XenVif.NetworkSettingsSaveRestore(true);
                InstallerState.SetFlag(InstallerState.States.NetworkSettingsSaved);
                Trace.WriteLine("NetSettings saved!");
            }

            if (!InstallerState.SystemCleaned())
            {
                if (pvToolsVer == 7)
                {
                    Trace.WriteLine("PV Tools found on 0001 or 0002; xenprepping..");
                    DriverHandler.SystemClean();
                    Trace.WriteLine("xenprepping done!");
                }
                else // "XenPrepping" not needed, so just flip all relevant flags
                {
                    InstallerState.SetFlag(InstallerState.States.RemovedFromFilters);
                    InstallerState.SetFlag(InstallerState.States.BootStartDisabled);
                    InstallerState.SetFlag(InstallerState.States.MSIsUninstalled);
                    InstallerState.SetFlag(InstallerState.States.XenLegacyUninstalled);
                    InstallerState.SetFlag(InstallerState.States.CleanedUp);
                    Trace.WriteLine("xenprepping not needed; flip relevant flags");
                }
            }

            if (!InstallerState.EverythingInstalled())
            {
                if (!InstallerState.GetFlag(InstallerState.States.CertificatesInstalled))
                {
                    Trace.WriteLine("Installing certificates..");
                    XSToolsInstallation.Helpers.InstallCertificates(
                        Directory.GetCurrentDirectory() + @"\certs");
                    InstallerState.SetFlag(InstallerState.States.CertificatesInstalled);
                    Trace.WriteLine("Certificates installed");
                }

                string driverRootDir = Path.Combine(
                    InstallAgent.exeDir,
                    "Drivers"
                );

                var drivers = new[] {
                    new { name = "xennet", flag = InstallerState.States.XenNetInstalled },
                    new { name = "xenvif", flag = InstallerState.States.XenVifInstalled },
                    new { name = "xenvbd", flag = InstallerState.States.XenVbdInstalled },
                    new { name = "xeniface", flag = InstallerState.States.XenIfaceInstalled },
                    new { name = "xenbus", flag = InstallerState.States.XenBusInstalled }
                };

                foreach (var driver in drivers)
                {
                    if (!InstallerState.GetFlag(driver.flag))
                    {
                        if (DriverHandler.InstallDriver_2(driverRootDir, driver.name))
                        {
                            InstallerState.SetFlag(driver.flag);
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
                InstallerState.UnsetFlag(InstallerState.States.RebootNeeded);

                XSToolsInstallation.Helpers.ChangeServiceStartMode(
                    this.ServiceName,
                    ServiceStartMode.Manual
                );
            }
            else
            {
                if (this.rebootOption == RebootType.AUTOREBOOT)
                {
                    XSToolsInstallation.Helpers.Reboot();
                }
                else
                {
                    InstallerState.SetFlag(InstallerState.States.RebootNeeded);
                }
            }
        }

        private void SetPVToolsVersionOnFirstRun()
        // Should run only on first run of the InstallAgent.
        // Writes a registry key with the system's state;
        // '0' - system is clean; no drivers installed
        // '7' - system has drivers other than 8.x installed
        // '8' - system has drivers 8.x installed
        {
            int pvToolsVer;

            if ((PVDevice.XenBus.IsPresent(PVDevice.XenBus.XenBusDevs.DEV_0001, true) &&
                     PVDevice.XenBus.HasChildren(PVDevice.XenBus.XenBusDevs.DEV_0001)) ||
                (PVDevice.XenBus.IsPresent(PVDevice.XenBus.XenBusDevs.DEV_0002, true) &&
                     PVDevice.XenBus.HasChildren(PVDevice.XenBus.XenBusDevs.DEV_0002)))
            {
                pvToolsVer = 7;
            }
            else if (PVDevice.XenBus.IsPresent(PVDevice.XenBus.XenBusDevs.DEV_C000, true) &&
                     PVDevice.XenBus.HasChildren(PVDevice.XenBus.XenBusDevs.DEV_C000))
            {
                pvToolsVer = 8;
            }
            else
            {
                pvToolsVer = 0;
            }

            using (RegistryKey rk = Registry.LocalMachine.CreateSubKey(
                       InstallAgent.rootRegKey))
            {
                rk.SetValue(
                    "PVToolsVersionOnFirstRun",
                    pvToolsVer,
                    RegistryValueKind.DWord
                );
            }
        }

        private int GetPVToolsVersionOnFirstRun()
        {
            int pvToolsVer;

            using (RegistryKey rk = Registry.LocalMachine.CreateSubKey(
                       InstallAgent.rootRegKey))
            {
                pvToolsVer = (int)rk.GetValue("PVToolsVersionOnFirstRun", -1);

                if (pvToolsVer == -1)
                {
                    SetPVToolsVersionOnFirstRun();
                    pvToolsVer = (int)rk.GetValue("PVToolsVersionOnFirstRun");
                }
            }

            return pvToolsVer;
        }

        private RebootType GetTrueRebootType(RebootType rt)
        // If 'rebootOption = DEFAULT', the function returns one of the 2
        // other options, depending on the system's state on first run
        {
            if (rt != RebootType.DEFAULT)
            {
                return rt;
            }

            switch (this.pvToolsVer)
            {
                case 0:
                case 7:
                    return RebootType.AUTOREBOOT;
                case 8:
                    return RebootType.NOREBOOT;
                default:
                    throw new Exception("Cannot get RebootType");
            }
        }
    }
}