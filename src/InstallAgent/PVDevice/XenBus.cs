using PInvoke;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using XSToolsInstallation;

namespace PVDevice
{
    static class XenBus
    {
        // Is populated in the static constructor
        // If the device exists, hwIDs[i] will be the
        // device's Hardware ID string; if not it will
        // be the empty string.
        public static readonly string[] hwIDs;

        // The XenBus device we care about
        public static readonly XenBusDevs preferredXenBus;

        [Flags]
        public enum XenBusDevs : uint
        {
            DEV_0001 = 1 << 0,
            DEV_0002 = 1 << 1,
            DEV_C000 = 1 << 2
        }

        // Static constructor
        static XenBus()
        {
            hwIDs = new string[3];

            using (SetupApi.DeviceInfoSet devInfoSet =
                       new SetupApi.DeviceInfoSet(
                           IntPtr.Zero,
                           "PCI",
                           IntPtr.Zero,
                           SetupApi.DiGetClassFlags.DIGCF_ALLCLASSES |
                           SetupApi.DiGetClassFlags.DIGCF_PRESENT))
            {
                if (!devInfoSet.HandleIsValid())
                {
                    throw new Exception(
                        "XenBus static constructor: \'devInfoSet\' is INVALID"
                    );
                }

                for (int i = 0; i < hwIDs.Length; ++i)
                {
                    SetupApi.SP_DEVINFO_DATA xenBusDevInfoData;

                    Device.FindInSystem(
                        out xenBusDevInfoData,
                        @"PCI\VEN_5853&" +
                            Enum.GetName(typeof(XenBusDevs), 1 << i),
                        devInfoSet,
                        false
                    );

                    if (xenBusDevInfoData.cbSize != 0)
                    {
                        // Just get the first string returned.
                        // Should be the most explicit.
                        hwIDs[i] = Device.GetHardwareIDs(
                            devInfoSet,
                            xenBusDevInfoData
                        )[0];
                    }
                    else
                    {
                        hwIDs[i] = "";
                    }
                }

                // In descending order of preference
                if (IsPresent(XenBusDevs.DEV_C000, true))
                {
                    preferredXenBus = XenBusDevs.DEV_C000;
                }
                else if (IsPresent(XenBusDevs.DEV_0001, true))
                {
                    preferredXenBus = XenBusDevs.DEV_0001;
                }
                else if (IsPresent(XenBusDevs.DEV_0002, true))
                {
                    preferredXenBus = XenBusDevs.DEV_0002;
                }
            }
        }

        public static bool IsFunctioning()
        {
            if (!IsPresent(
                    XenBusDevs.DEV_0001 |
                    XenBusDevs.DEV_0002 |
                    XenBusDevs.DEV_C000,
                    false))
            {
                return false;
            }

            if (!PVDevice.IsServiceRunning(("xenbus")))
            {
                return false;
            }

            if (PVDevice.NeedsReboot("xenbus"))
            {
                Trace.WriteLine("BUS: needs reboot");
                return false;
            }

            Trace.WriteLine("BUS: device installed");
            return true;
        }

        // Check the existence of any combination of XenBus devices.
        // If 'strict' == true, all the devices queried need to exist
        // (bitwise AND). Else, at least one of the devices queried
        // needs to exist (bitwise OR).
        public static bool IsPresent(XenBusDevs xenBusDevQuery, bool strict)
        {
            bool result = strict ? true : false;

            for (int i = 0; i < hwIDs.Length; ++i)
            {
                if (((uint)xenBusDevQuery & (1 << i)) != 0)
                {
                    if (strict)
                    {
                        result &= !String.IsNullOrEmpty(hwIDs[i]);
                    }
                    else if (!String.IsNullOrEmpty(hwIDs[i]))
                    {
                        return true;
                    }
                }
            }

            return result;
        }

        public static string GetDeviceInstanceId(XenBusDevs xenBusDev)
        {
            const int BUFFER_SIZE = 4096;
            string xenBusDevStr;
            StringBuilder xenBusDeviceInstanceId =
                new StringBuilder(BUFFER_SIZE);

            xenBusDevStr = XenBus.hwIDs[
                Helpers.BitIdxFromFlag((uint)xenBusDev)
            ];

            using (SetupApi.DeviceInfoSet devInfoSet =
                       new SetupApi.DeviceInfoSet(
                           IntPtr.Zero,
                           "PCI",
                           IntPtr.Zero,
                           SetupApi.DiGetClassFlags.DIGCF_ALLCLASSES |
                           SetupApi.DiGetClassFlags.DIGCF_PRESENT))
            {
                SetupApi.SP_DEVINFO_DATA xenBusDevInfoData;
                int reqSize;

                Device.FindInSystem(
                    out xenBusDevInfoData,
                    xenBusDevStr,
                    devInfoSet,
                    true
                );

                if (xenBusDevInfoData.cbSize == 0)
                {
                    Trace.WriteLine(
                        String.Format(
                            "XenBus \'{0}\' not present",
                            xenBusDev.ToString()
                        )
                    );
                    return "";
                }

                if (!SetupApi.SetupDiGetDeviceInstanceId(
                        devInfoSet.Get(),
                        ref xenBusDevInfoData,
                        xenBusDeviceInstanceId,
                        BUFFER_SIZE,
                        out reqSize))
                {
                    Trace.WriteLine(
                        String.Format(
                            "SetupDiGetDeviceInstanceId() failed: {0}",
                            new Win32Exception(
                                Marshal.GetLastWin32Error()
                            ).Message
                        )
                    );
                    return "";
                }
            }

            return xenBusDeviceInstanceId.ToString();
        }

        public static int GetDevNode(XenBusDevs xenBusDev)
        {
            SetupApi.CR err;
            int xenBusNode;
            string xenBusDeviceInstanceId = GetDeviceInstanceId(xenBusDev);

            if (String.IsNullOrEmpty(xenBusDeviceInstanceId))
            {
                Trace.WriteLine("Could not retrieve XenBus Instance ID");
                return -1;
            }

            err = SetupApi.CM_Locate_DevNode(
                out xenBusNode,
                xenBusDeviceInstanceId,
                SetupApi.CM_LOCATE_DEVNODE.NORMAL
            );

            if (err != SetupApi.CR.SUCCESS)
            {
                Trace.WriteLine(
                    String.Format("CM_Locate_DevNode() error: {0}", err)
                );
                return -1;
            }

            return xenBusNode;
        }

        // Enumerates the specified XenBus device and searches
        // DriverStore for compatible drivers to install to the
        // new devices it finds.
        public static bool Enumerate(XenBusDevs xenBusDev)
        {
            SetupApi.CR err;
            int xenBusNode = GetDevNode(xenBusDev);

            if (xenBusNode == -1)
            {
                Trace.WriteLine("Could not get XenBus DevNode");
                return false;
            }

            Helpers.AcquireSystemPrivilege(
                AdvApi32.SE_LOAD_DRIVER_NAME);

            err = SetupApi.CM_Reenumerate_DevNode(
                xenBusNode,
                SetupApi.CM_REENUMERATE.SYNCHRONOUS |
                SetupApi.CM_REENUMERATE.RETRY_INSTALLATION
            );

            if (err != SetupApi.CR.SUCCESS)
            {
                Trace.WriteLine(
                    String.Format("CM_Reenumerate_DevNode() error: {0}", err)
                );
                return false;
            }

            return true;
        }

        public static bool HasChildren(XenBusDevs xenBusDev)
        {
            SetupApi.CR err;
            int xenBusNode = GetDevNode(xenBusDev);
            int xenBusChild;

            if (xenBusNode == -1)
            {
                Trace.WriteLine("Could not get XenBus DevNode");
                return false;
            }

            err = SetupApi.CM_Get_Child(
                out xenBusChild, xenBusNode, 0
            );

            if (err != SetupApi.CR.SUCCESS)
            {
                Trace.WriteLine(
                    String.Format("CM_Get_Child() error: {0}", err)
                );
                return false;
            }

            return true;
        }
    }
}
