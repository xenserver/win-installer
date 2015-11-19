using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceProcess;
using System.Diagnostics;
using Microsoft.Win32;

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

            if (enumKey == null)
            {
                Trace.WriteLine(@"Cannot find SYSTEM\CurrentControlSet\enum\xenbus");
                return false;
            }

            string[] subKeyNames = enumKey.GetSubKeyNames();

            if (subKeyNames == null)
            {
                Trace.WriteLine("No subkeys available");
            }

            foreach (string name in subKeyNames)
            {
                if (name == null)
                {
                    Trace.WriteLine("The subkey name is null");
                }

                // We only care about new-style VEN_XS devices
                if (name.StartsWith("VEN_XS"))
                {
                    RegistryKey subKeyDetailsKey = enumKey.OpenSubKey(name + @"\_");
                    string subKeyDevice = (string)subKeyDetailsKey.GetValue(
                        "LocationInformation"
                    );

                    // LocationInformation isn't certain to be set
                    if (subKeyDevice != null && subKeyDevice.Equals(device))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool NeedsReboot(string emulatedDevice)
        {
            Trace.WriteLine(emulatedDevice + ": checking if reboot needed");

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\services\" +
                emulatedDevice + @"\Status"))
            {
                if (key != null &&
                    key.GetValueNames().Contains("NeedReboot"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
