using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace PVDevice
{
    static class XenBus
    {
        // Is populated in the static constructor
        // If the device exists, hwIDs[i] will be the
        // device's Hardware ID string; if not it will
        // be the empty string.
        public static readonly string[] hwIDs;

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

            using (PInvoke.SetupApi.DeviceInfoSet devInfoSet =
                       new PInvoke.SetupApi.DeviceInfoSet(
                           IntPtr.Zero,
                           "PCI",
                           IntPtr.Zero,
                           PInvoke.SetupApi.DiGetClassFlags.DIGCF_ALLCLASSES |
                           PInvoke.SetupApi.DiGetClassFlags.DIGCF_PRESENT))
            {
                if (!devInfoSet.HandleIsValid())
                {
                    throw new Exception(
                        "XenBus static constructor: \'devInfoSet\' is INVALID"
                    );
                }

                for (int i = 0; i < hwIDs.Length; ++i)
                {
                    PInvoke.SetupApi.SP_DEVINFO_DATA xenBusDevInfoData;

                    XSToolsInstallation.Device.FindInSystem(
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
                        hwIDs[i] = XSToolsInstallation.Device.GetHardwareIDs(
                            devInfoSet,
                            xenBusDevInfoData
                        )[0];
                    }
                    else
                    {
                        hwIDs[i] = "";
                    }
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
                Trace.WriteLine("BUS: service not running");
                //textOut += "  Bus Device Initializing\n";
                return false;
            }

            if (PVDevice.NeedsReboot("xenbus"))
            {
                Trace.WriteLine("BUS: needs reboot");
                //textOut += "  Bus Device Installing Filters\n";
                return false;
            }

            Trace.WriteLine("BUS: device installed");
            //textOut += "  Bus Device Installed\n";
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
    }
}
