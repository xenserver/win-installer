using Microsoft.Win32;
using PInvokeWrap;
using State;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
        // List of all possible PV H/W devices that can be present
        // on a system. The ordering is deliberate, so that the
        // device tree(s) will be walked from children to root.
        private static readonly string[] pvHwIds = {
            @"XENVIF\VEN_XS0001&DEV_NET",
            @"XENVIF\VEN_XS0002&DEV_NET",
            @"XENVIF\DEVICE",

            @"XENBUS\VEN_XS0001&DEV_VIF",
            @"XENBUS\VEN_XS0002&DEV_VIF",
            @"XEN\VIF",
            @"XENBUS\CLASS&VIF",
            @"XENBUS\CLASS_VIF",

            @"XENBUS\VEN_XS0001&DEV_VBD",
            @"XENBUS\VEN_XS0002&DEV_VBD",
            @"XENBUS\CLASS&VBD",
            @"XENBUS\CLASS_VBD",

            @"XENBUS\VEN_XS0001&DEV_IFACE",
            @"XENBUS\VEN_XS0002&DEV_IFACE",
            @"XENBUS\CLASS&IFACE",
            @"XENBUS\CLASS_IFACE",

            @"PCI\VEN_5853&DEV_0001",
            @"PCI\VEN_5853&DEV_0002",
            @"PCI\VEN_fffd&DEV_0101",

            @"ROOT\XENEVTCHN"
        };

        public static bool BlockUntilNoDriversInstalling(uint timeout)
        // Returns true, if no drivers are installing before the timeout
        // is reached. Returns false, if timeout is reached. To block
        // until no drivers are installing pass PInvoke.CfgMgr32.INFINITE
        // 'timeout' is counted in seconds.
        {
            CfgMgr32.Wait result;

            Trace.WriteLine("Checking if drivers are currently installing");

            if (timeout != CfgMgr32.INFINITE)
            {
                Trace.WriteLine("Blocking for " + timeout + " seconds..");
                timeout *= 1000;
            }
            else
            {
                Trace.WriteLine("Blocking until no drivers are installing");
            }

            result = CfgMgr32.CMP_WaitNoPendingInstallEvents(
                timeout
            );

            if (result == CfgMgr32.Wait.OBJECT_0)
            {
                Trace.WriteLine("No drivers installing");
                return true;
            }
            else if (result == CfgMgr32.Wait.FAILED)
            {
                Win32Error.Set("CMP_WaitNoPendingInstallEvents");
                Trace.WriteLine(Win32Error.GetFullErrMsg());
                throw new Exception(Win32Error.GetFullErrMsg());
            }

            Trace.WriteLine("Timeout reached - drivers still installing");
            return false;
        }

        public static void InstallDrivers()
        {
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
                    InstallDriver(driverRootDir, driver.name);
                    Installer.SetFlag(driver.installed);
                }
            }
        }

        public static bool SystemClean()
        // Returns 'true' if all actions are completed; 'false' otherwise
        {
            if (!Installer.GetFlag(Installer.States.RemovedFromFilters))
            {
                RemovePVDriversFromFilters();
                Installer.SetFlag(Installer.States.RemovedFromFilters);
            }

            if (!Installer.GetFlag(Installer.States.BootStartDisabled))
            {
                DontBootStartPVDrivers();
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
                    UninstallDriversAndDevices();

                    if (i == TIMES - 1)
                    {
                        Installer.SetFlag(Installer.States.DrvsAndDevsUninstalled);
                    }
                }

                if (!Installer.GetFlag(Installer.States.MSIsUninstalled))
                {
                    UninstallMSIs();

                    if (i == TIMES - 1)
                    {
                        Installer.SetFlag(Installer.States.MSIsUninstalled);
                    }
                }
            }

            if (!Installer.GetFlag(Installer.States.CleanedUp))
            {
                CleanUpXenLegacy();
                CleanUpServices();
                CleanUpDriverFiles();
                Installer.SetFlag(Installer.States.CleanedUp);
            }

            return true;
        }

        public static void InstallDriver(
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

            Trace.WriteLine("Installing driver \'" + driver + "\'");

            if (!NewDev.DiInstallDriver(
                    IntPtr.Zero,
                    infPath,
                    flags,
                    out reboot))
            {
                Win32Error.Set("DiInstallDriver");
                Trace.WriteLine(Win32Error.GetFullErrMsg());
                throw new Exception(Win32Error.GetFullErrMsg());
            }

            Trace.WriteLine("Driver installed successfully");
        }

        private static void RemovePVDriversFromFilters()
        {
            const string FUNC_NAME = "RemovePVDriversFromFilters";
            const string BASE_RK_NAME =
                @"SYSTEM\CurrentControlSet\Control\Class";
            const string XENFILT = "xenfilt";
            const string SCSIFILT = "scsifilt";

            Trace.WriteLine("===> " + FUNC_NAME);

            RegistryKey baseRK = Registry.LocalMachine.OpenSubKey(
                BASE_RK_NAME, true
            );

            if (baseRK == null)
            {
                throw new Exception(
                    "Could not open registry key: \'" + BASE_RK_NAME + "\'"
                );
            }

            Trace.WriteLine("Opened key: \'" + BASE_RK_NAME + "\'");

            string[] filterTypes = { "LowerFilters", "UpperFilters" };

            foreach (string subKeyName in baseRK.GetSubKeyNames())
            {
                using (RegistryKey tmpRK = baseRK.OpenSubKey(subKeyName, true))
                {
                    if (tmpRK == null)
                    {
                        throw new Exception(
                            "Could not open registry key: \'" +
                            BASE_RK_NAME + "\\" + subKeyName + "\'"
                        );
                    }

                    foreach (string filters in filterTypes)
                    {
                        string[] values = (string[])tmpRK.GetValue(filters);

                        if (values == null ||
                            !(values.Contains(
                                  XENFILT,
                                  StringComparer.OrdinalIgnoreCase) ||
                              values.Contains(
                                  SCSIFILT,
                                  StringComparer.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        Trace.WriteLine(
                            "At \'" + subKeyName + "\\" + filters + "\'"
                        );

                        Trace.WriteLine(
                            "Before: \'" + String.Join(" ", values) + "\'"
                        );

                        // LINQ expression
                        // Gets all entries of "values" that
                        // are not "xenfilt" or "scsifilt"
                        values = values.Where(
                            val => !(val.Equals(
                                         XENFILT,
                                         StringComparison.OrdinalIgnoreCase) ||
                                     val.Equals(
                                         SCSIFILT,
                                         StringComparison.OrdinalIgnoreCase))
                        ).ToArray();

                        tmpRK.SetValue(
                            filters,
                            values,
                            RegistryValueKind.MultiString
                        );

                        Trace.WriteLine(
                            "After: \'" + String.Join(" ", values) + "\'"
                        );
                    }
                }
            }
            Trace.WriteLine("<=== " + FUNC_NAME);
        }

        private static void DontBootStartPVDrivers()
        {
            const string FUNC_NAME = "DontBootStartPVDrivers";
            const string BASE_RK_NAME =
                @"SYSTEM\CurrentControlSet\Services";
            const string START = "Start";
            const string XENFILT_UNPLUG = @"xenfilt\Unplug";
            const int MANUAL = 3;

            string[] xenServices = {
                "XENBUS", "xenfilt", "xeniface", "xenlite",
                "xennet", "xenvbd", "xenvif", "xennet6",
                "xenutil", "xenevtchn"
            };

            Trace.WriteLine("===> " + FUNC_NAME);

            RegistryKey baseRK = Registry.LocalMachine.OpenSubKey(
                BASE_RK_NAME, true
            );

            if (baseRK == null)
            {
                throw new Exception(
                    "Could not open registry key: \'" + BASE_RK_NAME + "\'"
                );
            }

            Trace.WriteLine("Opened key: \'" + BASE_RK_NAME + "\'");

            foreach (string service in xenServices)
            {
                using (RegistryKey tmpRK = baseRK.OpenSubKey(service, true))
                {
                    if (tmpRK == null || tmpRK.GetValue(START) == null)
                    {
                        continue;
                    }

                    Trace.WriteLine(service + "\\" + START + " = " + MANUAL);

                    tmpRK.SetValue(START, MANUAL);
                }
            }

            using (RegistryKey tmpRK =
                       baseRK.OpenSubKey(XENFILT_UNPLUG, true))
            {
                if (tmpRK != null)
                {
                    Trace.WriteLine("Opened subkey: \'" + XENFILT_UNPLUG + "\'");
                    Trace.WriteLine(
                        "Delete values \'DISCS\' and " +
                        "\'NICS\' (if they exist)"
                    );
                    tmpRK.DeleteValue("DISKS", false);
                    tmpRK.DeleteValue("NICS", false);
                }
            }

            Trace.WriteLine("<=== " + FUNC_NAME);
        }

        private static void UninstallMSIs()
        {
            const int TRIES = 5;
            List<string> toRemove = new List<string>();

            // MSIs to uninstall
            string[] msiNameList = {
            //    "Citrix XenServer Windows Guest Agent",
                "Citrix XenServer VSS Provider",
                "Citrix Xen Windows x64 PV Drivers",
                "Citrix Xen Windows x86 PV Drivers",
                "Citrix XenServer Tools Installer"
            };

            foreach (string msiName in msiNameList)
            {
                string tmpCode = GetMsiProductCode(msiName);

                if (!String.IsNullOrEmpty(tmpCode))
                {
                    toRemove.Add(tmpCode);
                }
            }

            foreach (string productCode in toRemove)
            {
                UninstallMsi(productCode, TRIES);
            }
        }

        private static string GetMsiProductCode(string msiName)
        // Enumerates the MSIs present in the system. If 'msiName'
        // exists, it returns its product code. If not, it returns
        // the empty string.
        {
            const int GUID_LEN = 39;
            const int BUF_LEN = 128;
            int err;
            int len;
            StringBuilder productCode = new StringBuilder(GUID_LEN, GUID_LEN);
            StringBuilder productName = new StringBuilder(BUF_LEN, BUF_LEN);

            Trace.WriteLine(
                "Checking if \'" + msiName + "\' is present in system.."
            );

            // ERROR_SUCCESS = 0
            for (int i = 0;
                 (err = Msi.MsiEnumProducts(i, productCode)) == 0;
                 ++i)
            {
                len = BUF_LEN;

                // Get ProductName from Product GUID
                err = Msi.MsiGetProductInfo(
                    productCode.ToString(),
                    Msi.INSTALLPROPERTY.INSTALLEDPRODUCTNAME,
                    productName,
                    ref len
                );

                if (err == 0)
                {
                    if (msiName.Equals(
                            productName.ToString(),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Trace.WriteLine(
                            "Product found; Code: \'" +
                            productCode.ToString() + "\'"
                        );
                        return productCode.ToString();
                    }
                }
                else
                {
                    Win32Error.Set("MsiGetProductInfo", err);
                    Trace.WriteLine(Win32Error.GetFullErrMsg());
                    throw new Win32Exception(Win32Error.GetFullErrMsg());
                }
            }

            if (err == 259) // ERROR_NO_MORE_ITEMS
            {
                Trace.WriteLine("Product not found");
                return "";
            }
            else
            {
                Win32Error.Set("MsiEnumProducts", err);
                Trace.WriteLine(Win32Error.GetFullErrMsg());
                throw new Win32Exception(Win32Error.GetFullErrMsg());
            }
        }

        private static void UninstallMsi(string msiCode, int tries = 1)
        // Uses 'msiexec.exe' to uninstall MSI with product code
        // 'msiCode'. If the exit code is none of 'ERROR_SUCCCESS',
        // the function sleeps and then retries. The amount of time
        // sleeping is doubled on every try, starting at 1 second.
        {
            int secs;

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "msiexec.exe";
            startInfo.Arguments = "/x " + msiCode + " /qn /norestart";
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.CreateNoWindow = true;

            for (int i = 0; i < tries; ++i)
            {
                Trace.WriteLine(
                    "Running: \'" + startInfo.FileName +
                    " " + startInfo.Arguments + "\'"
                );

                using (Process proc = Process.Start(startInfo))
                {
                    proc.WaitForExit();

                    switch (proc.ExitCode)
                    {
                        case 0:
                            Trace.WriteLine("ERROR_SUCCESS");
                            return;
                        case 1641:
                            Trace.WriteLine("ERROR_SUCCESS_REBOOT_INITIATED");
                            return;
                        case 3010:
                            Trace.WriteLine("ERROR_SUCCESS_REBOOT_REQUIRED");
                            return;
                        default:
                            if (i == tries - 1)
                            {
                                Trace.WriteLine(
                                    "Tries exhausted; Error: " +
                                    proc.ExitCode
                                );

                                // TODO: Create custom exceptions
                                throw new Exception();
                            }

                            secs = (int)Math.Pow(2.0, (double)i);

                            Trace.WriteLine(
                                "Msi uninstall failed; Error: " +
                                proc.ExitCode
                            );
                            Trace.WriteLine(
                                "Retrying in " +
                                secs + " seconds"
                            );

                            Thread.Sleep(secs * 1000);
                            break;
                    }
                }
            }
        }

        private static void UninstallDriversAndDevices()
        {
            using (SetupApi.DeviceInfoSet devInfoSet =
                        new SetupApi.DeviceInfoSet(
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        SetupApi.DiGetClassFlags.DIGCF_ALLCLASSES))
            {
                foreach (string hwId in pvHwIds)
                {
                    UninstallDriverPackages(hwId);

                    Device.RemoveFromSystem(
                        devInfoSet,
                        hwId,
                        false
                    );
                }
            }
        }

        private static void CleanUpXenLegacy()
        // Cleans up leftover Xen Legacy registry entries and files
        {
            const string SOFTWARE = "SOFTWARE";
            const string WOW6432NODE = @"\Wow6432Node";
            const string UNINSTALL =
                @"\Microsoft\Windows\CurrentVersion\Uninstall";
            const string CITRIX_XENTOOLS = @"\Citrix\XenTools";
            const string INSTALL_DIR = @"\Install_Dir";

            string rk1Path;
            string rk2Path;
            string rk3Path;

            if (WinVersion.Is64BitOS())
            {
                rk1Path = SOFTWARE + WOW6432NODE + UNINSTALL;
                rk3Path = SOFTWARE + WOW6432NODE + CITRIX_XENTOOLS;
            }
            else
            {
                rk1Path = SOFTWARE + UNINSTALL;
                rk3Path = SOFTWARE + CITRIX_XENTOOLS;
            }

            rk2Path = rk3Path + INSTALL_DIR;

            try
            {
                Registry.LocalMachine.OpenSubKey(
                    rk1Path,
                    true
                ).DeleteSubKeyTree("Citrix XenTools");
            }
            catch (ArgumentException) { }

            string installDir = (string)Registry.LocalMachine.GetValue(
                rk2Path
            );

            try
            {
                Directory.Delete(installDir, true);
            }
            catch (DirectoryNotFoundException) { }
            catch (ArgumentNullException) { }

            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(rk3Path);
            }
            catch (ArgumentException) { }
        }

        private static void CleanUpDriverFiles()
        // Removes left over .sys files
        {
            string[] PVDrivers = {
                "xen", "xenbus", "xencrsh", "xenfilt",
                "xeniface", "xennet", "xenvbd", "xenvif",
                "xennet6", "xenutil", "xenevtchn"
            };

            string driverPath = Environment.GetFolderPath(
                Environment.SpecialFolder.System
            ) + @"\drivers\";


            foreach (string driver in PVDrivers)
            {
                string fullPath = driverPath + driver + ".sys";

                Trace.WriteLine("Deleting \'" + fullPath + "\'");

                try
                {
                    File.Delete(fullPath);
                }
                catch (UnauthorizedAccessException)
                {
                    Trace.WriteLine(
                        "File open by another process; did not delete"
                    );
                }
            }
        }

        private static void CleanUpServices()
        // Properly uninstalls PV drivers' services
        {
            // On 2k8 if you're going to reinstall straight away,
            // don't remove 'xenbus' or 'xenfilt' - as 2k8 assumes
            // their registry entries are still in place
            List<string> services = new List<string> {
                "xeniface", "xenlite", "xennet", "xenvbd",
                "xenvif", "xennet6", "xenutil", "xenevtchn"
            };

            // OS is not Win 2k8
            if (!(WinVersion.IsServerSKU() &&
                  WinVersion.GetMajorVersion() == 6 &&
                  WinVersion.GetMinorVersion() < 2))
            {
                services.Add("XENBUS");
                services.Add("xenfilt");
            }

            foreach (string service in services)
            {
                Helpers.DeleteService(service);
            }
        }

        private static void UninstallDriverPackages(string hwId)
        // Scans all oem*.inf files present in the system
        // and uninstalls all that match 'hwId'
        {
            string infPath = Path.Combine(
                Environment.GetEnvironmentVariable("windir"),
                "inf"
            );

            Trace.WriteLine(
                "Searching drivers in system for \'" + hwId + "\'"
            );

            foreach (string oemFile in Directory.GetFiles(infPath, "oem*.inf"))
            {
                // This is currently the only way to ignore case...
                if (File.ReadAllText(oemFile).IndexOf(
                        hwId, StringComparison.OrdinalIgnoreCase) == -1)
                {
                    continue;
                }

                Trace.WriteLine(
                    "\'" + hwId + "\' matches" + "\'" + oemFile + "\';"
                );

                bool needreboot;
                Trace.WriteLine("Uninstalling...");

                int err = DIFxAPI.DriverPackageUninstall(
                    oemFile,
                    (int)(DIFxAPI.DRIVER_PACKAGE.SILENT |
                          DIFxAPI.DRIVER_PACKAGE.FORCE |
                          DIFxAPI.DRIVER_PACKAGE.DELETE_FILES),
                          // N.B.: Starting with Windows 7,
                          // 'DELETE_FILES' is ignored
                    IntPtr.Zero,
                    out needreboot
                );

                if (err != 0) // ERROR_SUCCESS
                {
                    Win32Error.Set("DriverPackageUninstall", err);
                    Trace.WriteLine(Win32Error.GetFullErrMsg());
                    throw new Exception(Win32Error.GetFullErrMsg());
                }

                Trace.WriteLine("Uninstalled");
            }
        }
    }
}
