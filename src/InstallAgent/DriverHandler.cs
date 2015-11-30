using Microsoft.Win32;
using PInvoke;
using PVDevice;
using State;
using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using XSToolsInstallation;

namespace InstallAgent
{
    /*
     * Responsible for:
     *   - Removing drivers from 0001 and 0002 devices + cleaning up
     *   - Installing drivers on C000 device (if nothing installed)
     *   - Updating drivers on C000 device (if drivers already present)
     */
    static class DriverHandler
    {
        private static readonly string[] driverNames = { "xenbus", "xeniface", "xenvif", "xenvbd", "xennet" };

        public static CfgMgr32.Wait DriversInstalling(uint tries)
        {
            CfgMgr32.Wait result = CfgMgr32.Wait.FAILED;

            if (tries == 0)
            {
                tries = 1;
            }
            else if (tries > 12)
            {
                tries = 12;
            }

            Trace.WriteLine("Checking if drivers are currently installing");

            for (int i = 0; i < tries; ++i)
            {
                uint secs = (uint)(1 << i);

                Trace.WriteLine("Blocking for " + secs + " seconds..");

                result = CfgMgr32.CMP_WaitNoPendingInstallEvents(
                    secs * 1000
                );

                if (result == CfgMgr32.Wait.OBJECT_0)
                {
                    Trace.WriteLine("No drivers installing");
                    break;
                }
                else if (result == CfgMgr32.Wait.FAILED)
                {
                    Trace.WriteLine(
                        "CMP_WaitNoPendingInstallEvents failed: " +
                        new Win32Exception(
                            Marshal.GetLastWin32Error()
                        ).Message
                    );
                    break;
                }
            }

            return result;
        }

        public static void SystemClean()
        {
            if (!Installer.GetFlag(Installer.States.RemovedFromFilters))
            {
                //RemovePVDriversFromFilters();
                Installer.SetFlag(Installer.States.RemovedFromFilters);
            }

            if (!Installer.GetFlag(Installer.States.BootStartDisabled))
            {
                //DontBootStartPVDrivers();
                Installer.SetFlag(Installer.States.BootStartDisabled);
            }

            if (!Installer.GetFlag(Installer.States.MSIsUninstalled))
            {
                //UninstallMSIs();
                Installer.SetFlag(Installer.States.MSIsUninstalled);
            }

            if (!Installer.GetFlag(Installer.States.XenLegacyUninstalled))
            {
                //UninstallXenLegacy();
                Installer.SetFlag(Installer.States.XenLegacyUninstalled);
            }

            if (!Installer.GetFlag(Installer.States.CleanedUp))
            {
                //CleanUpPVDrivers();
                Installer.SetFlag(Installer.States.CleanedUp);
            }
        }

        // Driver will not install on device, until next reboot
        public static bool StageToDriverStore(
            string driverRootDir,
            string driver,
            SetupApi.SP_COPY copyStyle =
                SetupApi.SP_COPY.NEWER_ONLY)
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

            if (!SetupApi.SetupCopyOEMInf(
                    infPath,
                    infDir,
                    SetupApi.SPOST.PATH,
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

        // Searches an .inf file for a hardware device ID that is
        // either a XenBus device itself or is a device enumerated
        // under one. User has to supply which of the 3 XenBus IDs
        // to look for. Function assumes there can be at most 1 such
        // string in an .inf file. If not found, an empty string is
        // returned.
        public static string GetHardwareIdFromInf(
            XenBus.XenBusDevs xenBusDev,
            string infPath)
        {
            if (!File.Exists(infPath))
            {
                throw new Exception(
                    String.Format("\'{0}\' does not exist", infPath)
                );
            }

            string xenBusDevStr;

            switch (xenBusDev)
            {
                case XenBus.XenBusDevs.DEV_0001:
                    xenBusDevStr = "0001";
                    break;
                case XenBus.XenBusDevs.DEV_0002:
                    xenBusDevStr = "0002";
                    break;
                case XenBus.XenBusDevs.DEV_C000:
                    xenBusDevStr = "C000";
                    break;
                default:
                    throw new Exception("Not a valid XenBus device");
            }

            string hwID = "";
            string suffix = WinVersion.Is64BitOS() ? "amd64" : "x86";

            string hwIDPattern =
                @"((?:PCI|XENBUS|XENVIF)\\VEN_(?:5853|XS" +
                xenBusDevStr + @")\&DEV_(?:" + xenBusDevStr +
                @"|IFACE|VIF|VBD|NET)[A-Z\d\&_]*)";

            using (System.IO.StreamReader infFile =
                       new System.IO.StreamReader(infPath))
            {
                string line;
                bool sectionFound = false;

                while ((line = infFile.ReadLine()) != null)
                {
                    if (line.Equals("[Inst.NT" + suffix + "]"))
                    {
                        sectionFound = true;
                        continue;
                    }

                    if (!sectionFound)
                    {
                        continue;
                    }

                    // When we reach the next section, break the loop
                    if (line.StartsWith("["))
                    {
                        break;
                    }

                    foreach (Match m in Regex.Matches(line, hwIDPattern))
                    {
                        hwID = m.Value;
                        break;
                    }
                }
            }

            return hwID;
        }

        public static bool InstallDriver_2(
            string driverRootDir,
            string driver,
            NewDev.DIIRFLAG flags =
                NewDev.DIIRFLAG.ZERO)
        {
            bool reboot;

            string build = WinVersion.Is64BitOS() ? @"\x64\" : @"\x86\";

            string infPath = Path.Combine(
                driverRootDir,
                driver + build + driver + ".inf"
            );

            if (!File.Exists(infPath))
            {
                throw new Exception(
                    String.Format("\'{0}\' does not exist", infPath)
                );
            }

            Trace.WriteLine(
                String.Format("Installing \'{0}\' driver...", driver)
            );

            if (!NewDev.DiInstallDriver(
                    IntPtr.Zero,
                    infPath,
                    flags,
                    out reboot))
            {
                Trace.WriteLine(
                    String.Format("Driver \'{0}\' install failed: {1}",
                        driver,
                        new Win32Exception(
                            Marshal.GetLastWin32Error()
                        ).Message
                    )
                );
                return false;
            }

            Trace.WriteLine(
                String.Format("Driver \'{0}\' installed successfully", driver)
            );
            return true;
        }

        // Full path to driver .inf files is expected to be:
        // "{driverRootDir}\{driver}\{x64|x86}\{driver}.inf"
        public static bool InstallDriver(
            XenBus.XenBusDevs xenBusDev,
            string driverRootDir,
            string driver,
            NewDev.INSTALLFLAG installFlags =
                NewDev.INSTALLFLAG.NONINTERACTIVE)
        {
            bool reboot;

            string build = WinVersion.Is64BitOS() ? @"\x64\" : @"\x86\";

            string infPath = Path.Combine(
                driverRootDir,
                driver + build + driver + ".inf"
            );

            if (!File.Exists(infPath))
            {
                throw new Exception(
                    String.Format("\'{0}\' does not exist", infPath)
                );
            }

            string hwID = GetHardwareIdFromInf(
                xenBusDev,
                infPath
            );

            if (String.IsNullOrEmpty(hwID))
            {
                throw new Exception(
                    String.Format("Hardware ID found for \'{0}\'", driver)
                );
            }

            Trace.WriteLine(
                String.Format("Installing {0} on {1}", driver, hwID)
            );

            if (!NewDev.UpdateDriverForPlugAndPlayDevices(
                    IntPtr.Zero,
                    hwID,
                    infPath,
                    installFlags,
                    out reboot))
            {
                Trace.WriteLine(
                    String.Format("Driver \'{0}\' install failed: {1}",
                        driver,
                        new Win32Exception(
                            Marshal.GetLastWin32Error()
                        ).Message
                    )
                );
                return false;
            }

            Trace.WriteLine(
                String.Format("Driver \'{0}\' install success", driver)
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
                        string[] values = (string[])tmpRK.GetValue(filters);

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
            while (Msi.MsiEnumProducts(i, productCode) == 0)
            {
                string tmpCode = productCode.ToString();

                len = BUF_LEN;

                // Get ProductName from Product GUID
                err = Msi.MsiGetProductInfo(
                    tmpCode,
                    Msi.INSTALLPROPERTY.INSTALLEDPRODUCTNAME,
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
                startInfo.Arguments =
                    "/x " + product.Key + " /qn" +
                    (product.Value.Equals(msiNameList[msiNameList.Length - 1]) ?
                        "" : " /norestart"
                    );
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
                Device.RemoveFromSystem(
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

                    DIFxAll difx;

                    if (WinVersion.Is64BitOS())
                    {
                        difx = new DIFx64();
                    }
                    else
                    {
                        difx = new DIFx32();
                    }

                    difx.Uninstall(
                        oemfile,
                        (int)(DIFxAll.DRIVER_PACKAGE.SILENT |
                              DIFxAll.DRIVER_PACKAGE.FORCE |
                              DIFxAll.DRIVER_PACKAGE.DELETE_FILES),
                        IntPtr.Zero,
                        out needreboot
                    );

                    Trace.WriteLine("Uninstalled");
                }
            }
        }
    }
}
