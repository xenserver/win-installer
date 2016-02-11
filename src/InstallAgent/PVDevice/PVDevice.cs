using HardwareDevice;
using HelperFunctions;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;

namespace PVDevice
{
    static class PVDevice
    {
        public static bool IsServiceRunning(string name)
        {
            ServiceController sc;

            Trace.WriteLine("Checking service: \'" + name + "\'");

            try
            {
                sc = new ServiceController(name);
            }
            catch (ArgumentException e)
            {
                Trace.WriteLine(e.Message);
                return false;
            }

            try
            {
                if (sc.Status != ServiceControllerStatus.Running)
                {
                    Trace.WriteLine(
                        "Service \'" + name + "\' not running; Status: " +
                        sc.Status
                    );
                    return false;
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        public static bool IsServiceNeeded(string device)
        {
            Trace.WriteLine("Is \'" + device + "\' needed?");

            RegistryKey enumKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\enum\xenbus"
            );

            foreach (string name in enumKey.GetSubKeyNames())
            {
                // We only care about new-style VEN_XS devices
                if (name.StartsWith("VEN_XS"))
                {
                    RegistryKey subKeyDetailsKey = enumKey.OpenSubKey(name + @"\_");

                    string subKeyDevice = subKeyDetailsKey != null ?
                        (string)subKeyDetailsKey.GetValue(
                            "LocationInformation") :
                        null;

                    // LocationInformation isn't certain to be set
                    if (subKeyDevice != null && subKeyDevice.Equals(device))
                    {
                        Trace.WriteLine("Yes");
                        return true;
                    }
                }
            }

            Trace.WriteLine("No");
            return false;
        }

        public static bool NeedsReboot(string emulatedDevice)
        {
            bool reboot;

            Trace.WriteLine(emulatedDevice + ": checking if reboot needed");

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                Helpers.REGISTRY_SERVICES_KEY +
                emulatedDevice + @"\Status"))
            {
                if (key != null &&
                    key.GetValueNames().Contains("NeedReboot"))
                {
                    reboot = true;
                }
                else
                {
                    reboot = false;
                }
            }

            return reboot;
        }

        public static bool AllFunctioning()
        {
            const uint TIMEOUT = 300; // 5 minutes

            Func<bool>[] pvDevIsFunctioning = {
                XenBus.IsFunctioning,
                XenIface.IsFunctioning,
                XenVif.IsFunctioning, // <= Restores Net Settings internally
                XenVbd.IsFunctioning
            };

            bool busEnumerated = false;

            Trace.WriteLine(
                "Checking if all PV Devices are functioning properly"
            );

            for (int i = 0; i < pvDevIsFunctioning.Length; ++i)
            {
                if (!pvDevIsFunctioning[i]())
                {
                    if (!busEnumerated)
                    {
                        string xenBusHwId = XenBus.hwIDs[
                            Helpers.BitIdxFromFlag(
                                (uint)XenBus.preferredXenBus)
                        ];

                        Device.Enumerate(xenBusHwId, true);
                        Helpers.BlockUntilNoDriversInstalling(TIMEOUT);
                        busEnumerated = true;
                        --i;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
