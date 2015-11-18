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
            /*
            DriverPackage DriversMsi;
            MsiInstaller VssProvMsi;
            MsiInstaller AgentMsi;
            string installdir;
            */
            if (WinVersion.IsWOW64())
            {
                // FAIL
            }

            /* NO MSI INSTALLING WILL TAKE PLACE
             * WILL NEED TO PUT DRIVERS IN DRIVER STORE
             * Populate
             *  -DriversMSI
             *  -VssMSI
             *  -AgentMSI
             *  -installdir
             */
            /*
            CreateInstallerObjects(
                is64bitOS(),
                out DriversMsi,
                out VssProvMsi,
                out AgentMsi,
                out installdir
            );*/

            //RegisterWMI();

            InstallerState.GetFlag(InstallerState.States.NoDriverInstalling);

            if (!InstallerState.GetFlag(InstallerState.States.NetworkSettingsStored))
            {
                //StoreNetworkSettings();
                InstallerState.SetFlag(InstallerState.States.NetworkSettingsStored);
            }

            // Ask if this is needed
            /*
            if (InstallerState.Complete())
            {
                this.Stop();
                return;
            }
             * */

            while (!InstallerState.Complete())
            {
                // Handles state flags internally
                DriverHandler.SystemClean();

                // AgentInstallHandler();

                // Polling(); // ??
            }

            if (!InstallerState.GetFlag(InstallerState.States.NetworkSettingsRestored))
            {
                // RestoreNetworkSettings();
                InstallerState.SetFlag(InstallerState.States.NetworkSettingsRestored);
            }
        }
    }
}