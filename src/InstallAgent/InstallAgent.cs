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

namespace InstallAgent
{
    public partial class InstallAgent : ServiceBase
    {
        public int InstallOption { get; set; }
        public int RebootOption { get; set; }

        public static string rootRegKey =
            @"SOFTWARE\Citrix\InstallAgent\";

        private Thread installThread = null;

        public InstallAgent()
        {
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
                if ((PVDevice.XenBus.IsPresent(PVDevice.XenBus.XenBusDevs.DEV_0001, true) &&
                         PVDevice.XenBus.HasChildren(PVDevice.XenBus.XenBusDevs.DEV_0001)) ||
                    (PVDevice.XenBus.IsPresent(PVDevice.XenBus.XenBusDevs.DEV_0002, true) &&
                         PVDevice.XenBus.HasChildren(PVDevice.XenBus.XenBusDevs.DEV_0002)))
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
                    Directory.GetCurrentDirectory(),
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

            if (!PVDevice.XenBus.IsFunctioning() ||
                !PVDevice.XenIface.IsFunctioning() ||
                !PVDevice.XenVif.IsFunctioning() || // Restores Net Settings internally
                !PVDevice.XenVbd.IsFunctioning())
            {
                XSToolsInstallation.Helpers.Reboot();
            }
        }
    }
}