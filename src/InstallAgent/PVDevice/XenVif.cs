using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32;
using System.Security.AccessControl;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Reflection;
using System.IO;

namespace PVDevice
{
    class XenVif
    {
        public static bool IsFunctioning()
        {
            // If there are no vifs for the VM, xenbus
            // does not enumerate a device for xenvif
            if (PVDevice.IsServiceNeeded("VIF"))
            {
                if (!PVDevice.IsServiceRunning("xenvif"))
                {
                    Trace.WriteLine("VIF: service not running");
                    //textOut += "  Virtual Network Interface Device Initializing\n";
                    return false;
                }

                if ((!XSToolsInstallation.Device.ChildrenInstalled("xenvif")))
                {
                    //textOut += "  Virtual Network Interface Children Initializing\n";
                    Trace.WriteLine("VIF: children not installed");
                    return false;
                }

                if (PVDevice.NeedsReboot("xenvif"))
                {
                    Trace.WriteLine("VIF: needs reboot");
                    //textOut += "  Virtual Network Interface Removing Emulated Devices\n";
                    return false;
                }

                try
                {
                    FixupAliases();
                    NetworkSettingsSaveRestore(false); // Restore
                }
                catch (Exception e)
                {
                    Trace.WriteLine(
                        "VIF: Restoring network config triggered " +
                        "an exception: " + e.ToString()
                    );
                }

                Trace.WriteLine("VIF: device installed");
                //textOut += "  Virtual Network Interface Support Installed\n";
            }
            else
            {
                Trace.WriteLine("VIF: service not needed");
            }

            return true;
        }

        public static void NetworkSettingsSaveRestore(bool save)
        {
            // Combined the 2 functions since they
            // only differ in these 3 words
            string[] function = save ?
                new string[] { "Saving", "/save", "saved" } :
                new string[] { "Restoring", "/restore", "restored" };

            string path = (string)Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\Software\Citrix\XenTools",
                "Driver_Install_Dir",
                ""
            );

            if (String.IsNullOrEmpty(path))
            {
                throw new Exception(
                    "Could not get full path to QNetSettings.exe"
                );
            }

            path = Path.Combine(path, @"netsettings\QNetSettings.exe");

            if (!File.Exists(path))
            {
                throw new Exception(
                    String.Format("\'{0}\' does not exist", path)
                );
            }

            Trace.WriteLine(function[0] + " network settings");

            ProcessStartInfo start = new ProcessStartInfo();
            start.Arguments = "/log " + function[1];
            start.FileName = path;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            start.CreateNoWindow = true;

            using (Process proc = Process.Start(start))
            {
                proc.WaitForExit();
            }

            VifDisableEnable(false);
            VifDisableEnable(true);

            Trace.WriteLine("Network settings " + function[2]);
        }

        public static void FixupAliases()
        {
            // If we currently have XenNet devices working
            // and we don't have any aliases listed,
            // generate aliases as per the coinstaller
            RegistryKey netKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\services\xennet\enum"
            );

            if (netKey == null)
            {
                Trace.WriteLine("No XenNet instances found");
                return;
            }

            int count = (int)netKey.GetValue("Count", -1);

            if (count == -1)
            {
                Trace.WriteLine(@"XenNet\Count doesn't exist");
                return;
            }

            for (int i = 0; i < count; ++i)
            {
                try
                {
                    BuildAlias(
                        i,
                        (string)netKey.GetValue(i.ToString())
                    );
                }
                catch (Exception e)
                {
                    Trace.WriteLine(
                        "Failed to build alias for " + i.ToString() +
                        ": " + e.ToString()
                    );
                }
            }
        }

        public static void CopyPV()
        {
            CopyNicsByService("xennet");
            CopyNicsByService("xennet6");
        }

        public static bool Copied = false;
        private const string SERVICES_KEY =
            @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\";

        private static void CopyRegKey(RegistryKey src, RegistryKey dest)
        {
            if (src == null)
            {
                return;
            }

            RegistrySecurity srcAC = src.GetAccessControl();
            RegistrySecurity destAC = new RegistrySecurity();

            string descriptor = srcAC.GetSecurityDescriptorSddlForm(
                AccessControlSections.Access
            );

            destAC.SetSecurityDescriptorSddlForm(descriptor);
            dest.SetAccessControl(destAC);

            foreach (string valueName in src.GetValueNames())
            {
                Trace.WriteLine(
                    "Copy " + src.Name + " " +
                    valueName + ": " + dest.Name
                );

                dest.SetValue(valueName, src.GetValue(valueName));
            }

            foreach (string subKeyName in src.GetSubKeyNames())
            {
                Trace.WriteLine(
                    "DeepCopy " + src.Name + " " +
                    subKeyName + ": " + dest.Name
                );

                CopyRegKey(
                    src.OpenSubKey(subKeyName),
                    dest.CreateSubKey(subKeyName)
                );
            }
        }

        private static void CopyNicToService(
            string uuid,
            string device,
            uint luidIndex,
            uint ifType)
        {
            RegistryKey serviceskey = Registry.LocalMachine.OpenSubKey(
                @"System\CurrentControlSet\Services"
            );

            RegistryKey nics = Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\Citrix\XenToolsNetSettings"
            );

            RegistryKey devicekey = nics.CreateSubKey(device);

            RegistryKey nsikey = Registry.LocalMachine.OpenSubKey(
                @"System\CurrentControlSet\Control\Nsi"
            );

            CopyRegKey(
                serviceskey.OpenSubKey(
                    @"NETBT\Parameters\Interfaces\Tcpip_" + uuid),
                devicekey.CreateSubKey("NetBt")
            );

            CopyRegKey(
                serviceskey.OpenSubKey(
                    @"Tcpip\Parameters\Interfaces\" + uuid),
                devicekey.CreateSubKey("Tcpip")
            );

            CopyRegKey(
                serviceskey.OpenSubKey(
                    @"Tcpip6\Parameters\Interfaces\" + uuid),
                devicekey.CreateSubKey("Tcpip6")
            );

            CopyIPv6Address(
                nsikey.OpenSubKey(
                    "{eb004a01-9b1a-11d4-9123-0050047759bc}\\10"),
                luidIndex,
                ifType,
                devicekey
            );
        }

        private static void CopyIPv6Address(
            RegistryKey source,
            uint luidIndex,
            uint ifType,
            RegistryKey dest)
        {
            if (source == null)
            {
                Trace.WriteLine("No IPv6 Config found");
                return;
            }

            // Construct a NET_LUID & convert to a hex string
            ulong prefixVal =
                (((ulong)ifType) << 48) | (((ulong)luidIndex) << 24);

            // Fix endianness to match registry entry & convert to string
            byte[] prefixBytes = BitConverter.GetBytes(prefixVal);
            Array.Reverse(prefixBytes);
            string prefixStr =
                BitConverter.ToInt64(prefixBytes, 0).ToString("x16");

            Trace.WriteLine("Looking for prefix: " + prefixStr);

            foreach (string key in source.GetValueNames())
            {
                Trace.WriteLine("Testing " + key);

                if (!key.StartsWith(prefixStr))
                {
                    continue;
                }

                Trace.WriteLine("Found " + key);

                //Replace prefix with IPv6_Address____ before saving
                string newstring = "IPv6_Address____" + key.Substring(16);

                Trace.WriteLine(
                    "Writing to " + dest.ToString() +
                    " " + newstring
                );

                dest.SetValue(newstring, source.GetValue(key));
            }
            Trace.WriteLine(
                "Copying addresses with prefix \'" +
                prefixStr + "\' done"
            );
        }

        private static void CopyNicByUuid(UuidDevice uuid)
        {
            CopyNicToService(
                uuid.uuid,
                uuid.device,
                uuid.luidIndex,
                uuid.ifType
            );

            Copied = true;
        }

        private class UuidDevice
        {
            public string uuid;
            public string device;
            public uint luidIndex;
            public uint ifType;
        }

        private static List<UuidDevice> GetUuids(string service)
        {
            List<UuidDevice> uuids = new List<UuidDevice>();
            Trace.WriteLine(
                "Get uuids for " + SERVICES_KEY + service + @"\enum Count");

            int count = (int)Registry.GetValue(
                SERVICES_KEY + service + @"\enum", "Count", -1
            );

            if (count == -1)
            {
                Trace.WriteLine("No entries found");
                return uuids;
            }

            Trace.WriteLine("We have " + count.ToString() + " nics to manage");

            for (int i = 0; i < count; ++i)
            {
                Trace.WriteLine("Nic " + i.ToString());
                try
                {
                    string pci_device_id = (string)Registry.GetValue(
                        SERVICES_KEY + service + @"\enum",
                        i.ToString(),
                        ""
                    );

                    string driver_id = (string)Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Enum\" +
                        pci_device_id,
                        "driver",
                        null
                    );

                    string[] linkage = (string[])Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Class\" +
                        driver_id + "\\Linkage",
                        "RootDevice",
                        null
                    );

                    // Removed (Int32)
                    uint luidIndex = (UInt32)(Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Class\" +
                        driver_id,
                        "NetLuidIndex",
                        null
                    ));

                    // Removed (Int32)
                    uint ifType = (UInt32)(Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\Class\" +
                        driver_id,
                        "*IfType",
                        null
                    ));

                    string uuid = linkage[0];

                    uuids.Add(
                        new UuidDevice() {
                            uuid = uuid,
                            device = @"override\" + i.ToString(),
                            luidIndex = luidIndex,
                            ifType = ifType
                        }
                    );
                }
                catch (Exception e)
                {
                    Trace.WriteLine(
                        "Unable to find card " + i.ToString() +
                        ": " + e.ToString()
                    );
                }
            }

            return uuids;
        }

        private static void CopyNicsByService(
            string service,
            string sourcedevice = "",
            string destinationdevice = "")
        {
            foreach (UuidDevice uuid in GetUuids(service))
            {
                if (!(destinationdevice.Equals("") &&
                      sourcedevice.Equals("")))
                {
                    uuid.device = uuid.device.Replace(
                        sourcedevice,
                        destinationdevice
                    );
                }

                CopyNicByUuid(uuid);
            }
        }

        private static void BuildAlias(int index, string devicePath)
        {
            RegistryKey vifKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\services\xenvif",
                true
            );

            RegistryKey aliases = vifKey.CreateSubKey("aliases");

            if (aliases.GetValueNames().Contains(index.ToString()))
            {
                Trace.WriteLine(
                    "Found pre-existing alias for " + index.ToString()
                );

                return;
            }

            Trace.WriteLine(
                "Fixing up alias for " + index.ToString() +
                " with " + devicePath
            );

            aliases.SetValue(
                index.ToString(),
                @"SYSTEM\CurrentControlSet\Enum\" +
                devicePath
            );
        }

        private static void VifDisableEnable(bool enable)
        {
            using (PInvoke.SetupApi.DeviceInfoSet devInfoSet =
                       new PInvoke.SetupApi.DeviceInfoSet(
                       IntPtr.Zero,
                       "XENVIF",
                       IntPtr.Zero,
                       PInvoke.SetupApi.DiGetClassFlags.DIGCF_PRESENT |
                       PInvoke.SetupApi.DiGetClassFlags.DIGCF_ALLCLASSES))
            {
                PInvoke.SetupApi.SP_DEVINFO_DATA devInfoData =
                    new PInvoke.SetupApi.SP_DEVINFO_DATA();

                devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

                Trace.WriteLine("DevInfoData Size " + devInfoData.cbSize.ToString());

                for (uint i = 0;
                     PInvoke.SetupApi.SetupDiEnumDeviceInfo(
                         devInfoSet.Get(),
                         i,
                         ref devInfoData);
                     ++i)
                {
                    Trace.WriteLine("dev inst: " + devInfoData.devInst.ToString());
                    PInvoke.SetupApi.PropertyChangeParameters pcParams =
                        new PInvoke.SetupApi.PropertyChangeParameters();

                    pcParams.size = 8;
                    pcParams.diFunction = PInvoke.SetupApi.InstallFunctions.DIF_PROPERTYCHANGE;
                    pcParams.scope = PInvoke.SetupApi.Scopes.Global;

                    if (enable)
                    {
                        pcParams.stateChange = PInvoke.SetupApi.StateChangeAction.Enable;
                    }
                    else
                    {
                        pcParams.stateChange = PInvoke.SetupApi.StateChangeAction.Disable;
                    }

                    pcParams.hwProfile = 0;
                    var pinned = GCHandle.Alloc(pcParams, GCHandleType.Pinned);

                    byte[] temp = new byte[Marshal.SizeOf(pcParams)];
                    Marshal.Copy(
                        pinned.AddrOfPinnedObject(),
                        temp,
                        0,
                        Marshal.SizeOf(pcParams)
                    );

                    for (int j = 0; j < temp.Length/*Marshal.SizeOf(pcParams)*/; ++j)
                    {
                        Trace.WriteLine("[" + temp[j].ToString() + "]");
                    }

                    var pdd = GCHandle.Alloc(devInfoData, GCHandleType.Pinned);

                    Trace.WriteLine(Marshal.SizeOf(pcParams).ToString());

                    Trace.WriteLine(
                        "InstallPaarams " +
                        PInvoke.SetupApi.SetupDiSetClassInstallParams(
                            devInfoSet.Get(),
                            pdd.AddrOfPinnedObject(),
                            pinned.AddrOfPinnedObject(),
                            Marshal.SizeOf(pcParams)
                        ).ToString()
                    );

                    Trace.WriteLine(Marshal.GetLastWin32Error().ToString());

                    Trace.WriteLine(
                        "CallClassInstaller " +
                        PInvoke.SetupApi.SetupDiCallClassInstaller(
                            PInvoke.SetupApi.InstallFunctions.DIF_PROPERTYCHANGE,
                            devInfoSet.Get(),
                            pdd.AddrOfPinnedObject()
                        ).ToString() + " " +
                        Marshal.GetLastWin32Error().ToString()
                    );

                    pdd.Free();
                    pinned.Free();
                }
            }
        }
    }
}
