using HardwareDevice;
using HelperFunctions;
using Microsoft.Win32;
using PInvokeWrap;
using PVDevice;
using System;
using System.Diagnostics;

namespace State
{
    public static class VM
    {
        private static readonly string regKeyName =
            InstallAgent.InstallAgent.rootRegKeyName + @"\VMState";

        private static /* readonly */ XenBus.Devs xenBusDev;
        private static /* readonly */ PVToolsVersion pvToolsVer;
        private static /* readonly */ bool othDrvInstalling;

        private static int rebootsSoFar;
        public const int REBOOTS_MAX = 5;

        public enum PVToolsVersion : int
        {
            None = 0, // system is clean; no drivers installed
            LessThanEight = 7, // system has drivers other than 8.x installed
            Eight = 8, // system has drivers 8.x installed
        }

        public static XenBus.Devs GetXenBusDevUsedOnFirstRun()
        {
            return xenBusDev;
        }

        public static PVToolsVersion GetPVToolsVersionOnFirstRun()
        {
            return pvToolsVer;
        }

        public static bool GetOtherDriverInstallingOnFirstRun()
        {
            return othDrvInstalling;
        }

        public static bool AllowedToReboot()
        {
            return rebootsSoFar < REBOOTS_MAX;
        }

        public static void IncrementRebootCount()
        {
            string regValName = "RebootsSoFar";

            using (RegistryKey rk =
                       Registry.LocalMachine.OpenSubKey(regKeyName, true))
            {
                rk.SetValue(
                    regValName,
                    (int)rk.GetValue(regValName) + 1,
                    RegistryValueKind.DWord
                );
            }

            ++rebootsSoFar;
        }

        private static void SetOtherDriverInstallingOnFirstRun(
            RegistryKey openRegKey)
        // Check if any other drivers are installing. The outcome
        // will affect the timeout before a system reboot, if
        // 'RebootType'is AUTOREBOOT.
        {
            string regValName = "OtherDriverInstallingOnFirstRun";
            int tmp = (int)openRegKey.GetValue(regValName, -1);

            if (tmp == -1)
            {
                // With a timeout of '0', the function returns instantly
                // 'true', if no drivers installing
                // 'false', if it "timed out"
                tmp = Helpers.BlockUntilNoDriversInstalling(0) ? 0 : 1;

                openRegKey.SetValue(
                    regValName,
                    tmp,
                    RegistryValueKind.DWord
                );
            }

            othDrvInstalling = (tmp == 1) ? true : false;
        }

        private static void SetXenBusDevUsedOnFirstRun(
            RegistryKey openRegKey)
        // Keeps the XenBus device that was used on
        // the Install Agent's first run
        {
            string regValName = "XenBusDevUsedOnFirstRun";

            try
            {
                xenBusDev = (XenBus.Devs)Enum.Parse(
                    typeof(XenBus.Devs),
                    (string)openRegKey.GetValue(regValName),
                    true
                );
            }
            catch (ArgumentNullException)
            // <value> does not exist; first run of Install Agent
            {
                string tmp = "";

                foreach (XenBus.Devs tmpXenBus in
                         Enum.GetValues(typeof(XenBus.Devs)))
                {
                    if (!XenBus.IsPresent(tmpXenBus, true) ||
                        !XenBus.HasChildren(tmpXenBus))
                    {
                        continue;
                    }

                    tmp = tmpXenBus.ToString();
                    xenBusDev = tmpXenBus;
                    break;
                }

                if (String.IsNullOrEmpty(tmp))
                {
                    xenBusDev = (XenBus.Devs)0;
                }

                openRegKey.SetValue(
                    regValName,
                    tmp,
                    RegistryValueKind.String
                );
            }
            catch (ArgumentException)
            // <value> exists, but is empty =>
            // no XenBus dev used =>
            // no PV drivers installed
            {
                xenBusDev = (XenBus.Devs)0;
            }
        }

        private static void SetPVToolsVersionOnFirstRun(
            RegistryKey openRegKey)
        {
            string regValName = "PVToolsVersionOnFirstRun";
            int tmp = (int)openRegKey.GetValue(regValName, -1);
            string drvVer;

            if (tmp != -1)
            {
                // We can save some indentation..
                goto SetStaticVariable;
            }

            if (xenBusDev == (XenBus.Devs)0)
            {
                tmp = 0; // None
                drvVer = "0.0.0.0";
            }
            else
            {
                using (SetupApi.DeviceInfoSet devInfoSet =
                    new SetupApi.DeviceInfoSet(
                        IntPtr.Zero,
                        "PCI",
                        IntPtr.Zero,
                        SetupApi.DiGetClassFlags.DIGCF_ALLCLASSES |
                        SetupApi.DiGetClassFlags.DIGCF_PRESENT))
                {
                    int idx = Helpers.BitIdxFromFlag((uint)xenBusDev);

                    SetupApi.SP_DEVINFO_DATA devInfoData =
                        Device.FindInSystem(
                            XenBus.hwIDs[idx],
                            devInfoSet,
                            true
                        );

                    drvVer = Device.GetDriverVersion(
                        devInfoSet, devInfoData
                    );
                }

                // Split the DriverVersion string on the '.'
                // char and parse the 1st substring (major
                // version number)
                tmp = Int32.Parse(
                    drvVer.Split(new char[] { '.' })[0]
                );

                if (tmp != 8) // Eight
                {
                    tmp = 7; // LessThanEight
                }

            }

            Trace.WriteLine(
                "XenBus driver version on first run: \'" + drvVer + "\'"
            );

            openRegKey.SetValue(
                regValName,
                tmp,
                RegistryValueKind.DWord
            );

        SetStaticVariable:
            pvToolsVer = (PVToolsVersion)Enum.Parse(
                typeof(PVToolsVersion), tmp.ToString()
            );
        }

        private static void SetRebootsSoFar(RegistryKey openRegKey)
        {
            string regValName = "RebootsSoFar";
            int tmp = (int)openRegKey.GetValue(regValName, -1);

            if (tmp == -1)
            {
                tmp = 0;

                openRegKey.SetValue(
                    regValName,
                    tmp,
                    RegistryValueKind.DWord
                );
            }

            rebootsSoFar = tmp;
        }

        static VM()
        {
            Trace.WriteLine("===> State.VM cctor");

            using (RegistryKey rk =
                       Registry.LocalMachine.CreateSubKey(regKeyName))
            {
                // These 3 registry keys will not exist on first run. Populate
                // them and only read them afterwards. This needs to be done
                // before any installer-related action takes place.
                // N.B. Ordering is important
                SetOtherDriverInstallingOnFirstRun(rk);
                SetXenBusDevUsedOnFirstRun(rk);
                SetPVToolsVersionOnFirstRun(rk);

                SetRebootsSoFar(rk);
            }

            Trace.WriteLine("<=== State.VM cctor");
        }
    }
}