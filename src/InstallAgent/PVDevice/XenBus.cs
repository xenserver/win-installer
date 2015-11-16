using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace PVDevice
{
    static class XenBus
    {
        private static uint xenBusDevsPresent;

        // The actual hardware IDs for the XenBus devices might
        // be longer, but these will do just fine for checking
        // their existence in the system.
        private static readonly string[] hwIDs = {
            @"PCI\VEN_5853&DEV_0001",
            @"PCI\VEN_5853&DEV_0002",
            @"PCI\VEN_5853&DEV_C000",
        };

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
            FindDevs();
        }

        public static bool IsFunctioning()
        {
            if (xenBusDevsPresent == 0)
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

        // Returns true if either DEV_0001 or DEV_0002 is present
        public static bool IsDev000XPresent()
        {
            return (
                (xenBusDevsPresent & (uint)XenBusDevs.DEV_0001) |
                (xenBusDevsPresent & (uint)XenBusDevs.DEV_0002)
            ) != 0;
        }

        public static bool IsDevC000Present()
        {
            return (xenBusDevsPresent & (uint)XenBusDevs.DEV_C000) != 0;
        }

        private static void FindDevs()
        {
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
                        "\'devInfoSet\' is INVALID in XenBus.FindDevs()"
                    );
                }

                xenBusDevsPresent = 0;

                for (int i = 0; i < hwIDs.Length; ++i)
                {
                    PInvoke.SetupApi.SP_DEVINFO_DATA xenBusDevInfoData;

                    XSToolsInstallation.Device.FindInSystem(
                        out xenBusDevInfoData,
                        hwIDs[i],
                        devInfoSet,
                        false
                    );

                    if (xenBusDevInfoData.cbSize != 0)
                    {
                        xenBusDevsPresent |= (uint)(1 << i);
                    }
                }
            }
        }
    }
}
