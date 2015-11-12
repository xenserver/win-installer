using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;
using System.Collections;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;


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

            InstallerState.Initialize();

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

    /*
     * Responsible for:
     *   - Removing drivers from 0001 and 0002 devices + cleaning up
     *   - Installing drivers on C000 device (if nothing installed)
     *   - Updating drivers on C000 device (if drivers already present)
     */
    static class DriverHandler
    {
        private static readonly string[] driverNames =
            { "xenbus", "xeniface", "xenvif", "xenvbd", "xennet" };

        public static void SystemClean()
        {
            if (!InstallerState.GetFlag(InstallerState.States.RemovedFromFilters))
            {
                //RemovePVDriversFromFilters();
                InstallerState.SetFlag(InstallerState.States.RemovedFromFilters);
            }

            if (!InstallerState.GetFlag(InstallerState.States.BootStartDisabled))
            {
                //DontBootStartPVDrivers();
                InstallerState.SetFlag(InstallerState.States.BootStartDisabled);
            }

            if (!InstallerState.GetFlag(InstallerState.States.MSIsUninstalled))
            {
                //UninstallMSIs();
                InstallerState.SetFlag(InstallerState.States.MSIsUninstalled);
            }

            if (!InstallerState.GetFlag(InstallerState.States.XenLegacyUninstalled))
            {
                //UninstallXenLegacy();
                InstallerState.SetFlag(InstallerState.States.XenLegacyUninstalled);
            }

            if (!InstallerState.GetFlag(InstallerState.States.CleanedUp))
            {
                //CleanUpPVDrivers();
                InstallerState.SetFlag(InstallerState.States.CleanedUp);
            }
        }

        // Driver will not install on device, until next reboot
        public static bool StageToDriverStore(
            string driverRootDir,
            string driver,
            PInvoke.SetupApi.SP_COPY copyStyle =
                PInvoke.SetupApi.SP_COPY.NEWER_ONLY)
        {
            string build = WinVersion.Is64BitOS() ? @"\x64\" : @"\x86\";

            string infDir = Path.Combine(
                driverRootDir,
                driver + build
            );

            string infPath = Path.Combine(
                infDir,
                driver + ".inf"
            );

            if (!File.Exists(infPath))
            {
                throw new Exception(
                    String.Format("\'{0}\' does not exist", infPath)
                );
            }

            Trace.WriteLine(
                String.Format("Staging \'{0}\' to DriverStore", driver)
            );

            if (!PInvoke.SetupApi.SetupCopyOEMInf(
                    infPath,
                    infDir,
                    PInvoke.SetupApi.SPOST.PATH,
                    copyStyle,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                Trace.WriteLine(
                    String.Format("\'{0}\' driver staging failed: {1}",
                        driver,
                        new Win32Exception(
                            Marshal.GetLastWin32Error()
                        ).Message
                    )
                );
                return false;
            }

            Trace.WriteLine(
                String.Format(
                    "\'{0}\' driver staging success", driver
                )
            );

            return true;
        }

        private static void RemovePVDriversFromFilters()
        {
            RegistryKey baseRK = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class", true
            );

            string[] filterTypes = { "LowerFilters", "UpperFilters" };

            foreach (string subKeyName in baseRK.GetSubKeyNames())
            using (RegistryKey tmpRK = baseRK.OpenSubKey(subKeyName, true))
            foreach (string filters in filterTypes)
            {
                string[] values = (string[]) tmpRK.GetValue(filters);

                if (values != null)
                {
                    /*
                     * LINQ expression
                     * Gets all entries of "values" that
                     * are not "xenfilt" or "scsifilt"
                     */
                    values = values.Where(
                        val => !(val.Equals("xenfilt", StringComparison.OrdinalIgnoreCase) ||
                                 val.Equals("scsifilt", StringComparison.OrdinalIgnoreCase))
                    ).ToArray();

                    tmpRK.SetValue(filters, values, RegistryValueKind.MultiString);
                }
            }
        }

        private static void DontBootStartPVDrivers()
        {
            string[] xenDrivers = {
                "XENBUS", "xenfilt", "xeniface", "xenlite",
                "xennet", "xenvbd", "xenvif", "xennet6", 
                "xenutil", "xevtchn"
            };

            RegistryKey baseRK = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services", true
            );

            foreach (string driver in xenDrivers)
            using (RegistryKey tmpRK = baseRK.OpenSubKey(driver, true))
            {
                if (tmpRK != null)
                {
                    tmpRK.SetValue("Start", 3);
                }
            }

            using (RegistryKey tmpRK = baseRK.OpenSubKey(@"xenfilt\Unplug", true))
            {
                if (tmpRK != null)
                {
                    tmpRK.DeleteValue("DISKS", false);
                    tmpRK.DeleteValue("NICS", false);
                }
            }
        }

        private static void UninstallMSIs()
        {
            const int GUID_LEN = 39;
            const int BUF_LEN = 128;
            int err;
            int i = 0;
            int len;
            StringBuilder productCode = new StringBuilder(GUID_LEN, GUID_LEN);
            StringBuilder productName = new StringBuilder(BUF_LEN, BUF_LEN);
            Hashtable toRemove = new Hashtable();

            // MSIs to uninstall
            string[] msiNameList = {
            //    "Citrix XenServer Windows Guest Agent",
                "Citrix XenServer VSS Provider",
                "Citrix Xen Windows x64 PV Drivers",
                "Citrix Xen Windows x86 PV Drivers",
                "Citrix XenServer Tools Installer"
            };

            // ERROR_SUCCESS = 0
            while (PInvoke.Msi.MsiEnumProducts(i, productCode) == 0)
            {
                string tmpCode = productCode.ToString();

                len = BUF_LEN;

                // Get ProductName from Product GUID
                err = PInvoke.Msi.MsiGetProductInfo(
                    tmpCode,
                    PInvoke.Msi.INSTALLPROPERTY.INSTALLEDPRODUCTNAME,
                    productName,
                    ref len
                );

                if (err == 0)
                {
                    string tmpName = productName.ToString();

                    if (msiNameList.Contains(tmpName))
                    {
                        toRemove.Add(tmpCode, tmpName);
                    }
                }

                ++i;
            }

            foreach (DictionaryEntry product in toRemove)
            {
                Process process = new Process();
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "msiexec.exe";

                // For some unknown reason, XenServer Tools Installer
                // doesn't like the '/norestart' option and doesn't get
                // removed if it's there.
                startInfo.Arguments = "/x " + product.Key + " /qn" +
                    (product.Value.Equals(msiNameList[4]) ? "" : " /norestart");
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
                process.Close();
            }
        }

        private static void UninstallXenLegacy()
        {
            try
            {
                Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true
                ).DeleteSubKeyTree("Citrix XenTools");
            }
            catch { }

            try
            {
                HardUninstallFromReg(@"SOFTWARE\Citrix\XenTools\");
            }
            catch { }

            if (WinVersion.Is64BitOS())
            {
                try
                {
                    Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Wow6432Node\Microsoft\" +
                        @"Windows\CurrentVersion\Uninstall\",
                        true
                    ).DeleteSubKeyTree("Citrix XenTools");
                }
                catch { }

                try
                {
                    HardUninstallFromReg(@"SOFTWARE\Wow6432Node\Citrix\XenTools\");
                }
                catch { }
            }

            try
            {
                XSToolsInstallation.Device.RemoveFromSystem(
                    new string[] { @"root\xenevtchn" },
                    false
                );
            }
            catch (Exception e)
            {
                Trace.WriteLine("Remove exception: " + e.ToString());
            }
        }

        private static void HardUninstallFromReg(string key)
        {
            // TODO: Check with Ben about this
            string installdir = (string)Registry.LocalMachine.GetValue(
                key + @"Install_Dir"
            );

            if (installdir != null)
            {
                try
                {
                    Directory.Delete(installdir, true);
                }
                catch { }
            }

            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(key);
            }
            catch { }
        }

        private static void CleanUpPVDrivers(bool workaround2k8 = false)
        {
            string[] PVDrivers = {
                "xen", "xenbus", "xencrsh", "xenfilt",
                "xeniface", "xennet", "xenvbd", "xenvif",
                "xennet6", "xenutil", "xevtchn"
            };

            string[] services = {
                "XENBUS", "xenfilt", "xeniface", "xenlite",
                "xennet", "xenvbd", "xenvif", "xennet6",
                "xenutil", "xevtchn"
            };

            // On 2k8 if you're going to reinstall straight away, don't remove
            // xenbus or xenfilt - as 2k8 assumes their registry entries
            // are still in place
            string[] services2k8 = {
                "xeniface", "xenlite", "xennet", "xenvbd",
                "xenvif", "xennet6", "xenutil", "xevtchn"
            };

            string[] hwIDs = {
                // @"PCI\VEN_5853&DEV_C000&SUBSYS_C0005853&REV_01",
                @"PCI\VEN_5853&DEV_0001",
                @"PCI\VEN_5853&DEV_0002",
                // @"XENBUS\VEN_XSC000&DEV_IFACE&REV_00000001",
                @"XENBUS\VEN_XS0001&DEV_IFACE",
                @"XENBUS\VEN_XS0002&DEV_IFACE",
                // @"XENBUS\VEN_XSC000&DEV_VBD&REV_00000001",
                @"XENBUS\VEN_XS0001&DEV_VBD",
                @"XENBUS\VEN_XS0002&DEV_VBD",
                // @"XENVIF\VEN_XSC000&DEV_NET&REV_00000000",
                @"XENVIF\VEN_XS0001&DEV_NET",
                @"XENVIF\VEN_XS0002&DEV_NET",
                // @"XENBUS\VEN_XSC000&DEV_VIF&REV_00000001",
                @"XENBUS\VEN_XS0001&DEV_VIF",
                @"XENBUS\VEN_XS0002&DEV_VIF",
                @"root\xenevtchn",
                @"XENBUS\CLASS&VIF",
                @"PCI\VEN_fffd&DEV_0101",
                @"XEN\VIF",
                @"XENBUS\CLASS&IFACE",
            };

            string driverPath = Environment.GetFolderPath(
                Environment.SpecialFolder.System
            ) + @"\drivers\";

            // Remove drivers from DriverStore
            foreach (string hwID in hwIDs)
            {
                PnPRemove(hwID);
            }

            // Delete services' registry entries
            using (RegistryKey baseRK = Registry.LocalMachine.OpenSubKey(
                       @"SYSTEM\CurrentControlSet\Services", true
                   ))
            {
                string[] servicelist = workaround2k8 ? services2k8 : services;
                foreach (string service in servicelist)
                {
                    try
                    {
                        baseRK.DeleteSubKeyTree(service);
                    }
                    catch (ArgumentException) { }
                }
            }

            // Delete driver files
            foreach (string driver in PVDrivers)
            {
                File.Delete(driverPath + driver + ".sys");
            }
        }

        private static void PnPRemove(string hwID)
        {
            Trace.WriteLine("remove " + hwID);

            string infpath = Environment.GetFolderPath(
                Environment.SpecialFolder.System
            ) + @"\..\inf";
            Trace.WriteLine("inf dir = " + infpath);

            string[] oemlist = Directory.GetFiles(infpath, "oem*.inf");
            Trace.WriteLine(oemlist.ToString());

            foreach (string oemfile in oemlist)
            {
                Trace.WriteLine("Checking " + oemfile);
                string contents = File.ReadAllText(oemfile);

                if (contents.Contains(hwID))
                {
                    bool needreboot;
                    Trace.WriteLine("Uninstalling");

                    PInvoke.DIFxAll difx;

                    if (WinVersion.Is64BitOS())
                    {
                        difx = new PInvoke.DIFx64();
                    }
                    else
                    {
                        difx = new PInvoke.DIFx32();
                    }

                    difx.Uninstall(
                        oemfile,
                        (int)(PInvoke.DIFxAll.DRIVER_PACKAGE.SILENT |
                              PInvoke.DIFxAll.DRIVER_PACKAGE.FORCE |
                              PInvoke.DIFxAll.DRIVER_PACKAGE.DELETE_FILES),
                        IntPtr.Zero,
                        out needreboot
                    );

                    Trace.WriteLine("Uninstalled");
                }
            }
        }
    }
}