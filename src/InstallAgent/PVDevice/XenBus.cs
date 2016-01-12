using PInvokeWrap;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using XSToolsInstallation;

namespace PVDevice
{
    public static class XenBus
    {
        // Is populated in the static constructor
        // If the device exists, hwIDs[i] will be the
        // device's Hardware ID string; if not it will
        // be the empty string.
        public static readonly string[] hwIDs;

        // The XenBus device we care about
        public static readonly Devs preferredXenBus;

        [Flags]
        public enum Devs : uint
        {
            DEV_0001 = 1 << 0,
            DEV_0002 = 1 << 1,
            DEV_C000 = 1 << 2
        }

        // Static constructor
        static XenBus()
        {
            hwIDs = new string[
                Enum.GetNames(typeof(Devs)).Length // == # of devs
            ];

            const string XENBUS_DEV_PREFIX = @"PCI\VEN_5853&";

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

                    xenBusDevInfoData = Device.FindInSystem(
                        XENBUS_DEV_PREFIX +
                            Enum.GetName(typeof(Devs), 1 << i), // == Dev name
                        devInfoSet,
                        false
                    );

                    if (xenBusDevInfoData != null)
                    {
                        // Just get the first string returned.
                        // Should be the most explicit.
                        hwIDs[i] = Device.GetDevRegPropertyMultiStr(
                            devInfoSet,
                            xenBusDevInfoData,
                            SetupApi.SPDRP.HARDWAREID
                        )[0];
                    }
                    else
                    {
                        hwIDs[i] = "";
                    }
                }

                // In descending order of preference
                if (IsPresent(Devs.DEV_C000, true))
                {
                    preferredXenBus = Devs.DEV_C000;
                }
                else if (IsPresent(Devs.DEV_0001, true))
                {
                    preferredXenBus = Devs.DEV_0001;
                }
                else if (IsPresent(Devs.DEV_0002, true))
                {
                    preferredXenBus = Devs.DEV_0002;
                }
            }
        }

        public static bool IsFunctioning()
        {
            if (!IsPresent(
                    Devs.DEV_0001 |
                    Devs.DEV_0002 |
                    Devs.DEV_C000,
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
        public static bool IsPresent(Devs xenBusDevQuery, bool strict)
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

        public static bool HasChildren(Devs xenBusDev)
        {
            SetupApi.CR err;

            string xenBusHwId = XenBus.hwIDs[
                Helpers.BitIdxFromFlag((uint)xenBusDev)
            ];

            int xenBusNode = Device.GetDevNode(xenBusHwId);
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
