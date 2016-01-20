using HardwareDevice;
using HelperFunctions;
using Microsoft.Win32;
using PInvokeWrap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PVDriversRemoval
{
    public static class PVDriversPurge
    // Cleans a VM of all PV drivers
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

        public static void RemovePVDriversFromFilters()
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

            Trace.WriteLine("Opened key: \'" + BASE_RK_NAME + "\'");

            string[] filterTypes = { "LowerFilters", "UpperFilters" };

            foreach (string subKeyName in baseRK.GetSubKeyNames())
            {
                using (RegistryKey tmpRK = baseRK.OpenSubKey(subKeyName, true))
                {
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

        public static void DontBootStartPVDrivers()
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

        public static void UninstallMSIs()
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
                string tmpCode = Helpers.GetMsiProductCode(msiName);

                if (!String.IsNullOrEmpty(tmpCode))
                {
                    toRemove.Add(tmpCode);
                }
            }

            foreach (string productCode in toRemove)
            {
                Helpers.UninstallMsi(productCode, TRIES);
            }
        }

        public static void UninstallDriversAndDevices()
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
                    Helpers.UninstallDriverPackages(hwId);

                    while (
                        Device.RemoveFromSystem(
                            devInfoSet,
                            hwId,
                            false)
                    );
                }
            }
        }

        public static void CleanUpXenLegacy()
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

        public static void CleanUpDriverFiles()
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

        public static void CleanUpServices()
        // Properly uninstalls PV drivers' services
        {
            List<string> services = new List<string> {
                "xeniface", "xenlite", "xennet", "xenvbd",
                "xenvif", "xennet6", "xenutil", "xenevtchn",
                "XENBUS", "xenfilt"
            };

            foreach (string service in services)
            {
                Helpers.DeleteService(service);
            }
        }
    }
}
