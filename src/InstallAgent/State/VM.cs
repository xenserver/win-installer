using Microsoft.Win32;
using PInvoke;
using PVDevice;
using System;
using System.Diagnostics;

namespace State
{
    public static class VM
    {
        private static readonly string regKeyName =
            InstallAgent.InstallAgent.rootRegKeyName + @"\VMState";

        private static /* readonly */ PVToolsVersion pvToolsVer;
        private static /* readonly */ bool othDrvInstalling;

        private static int rebootsSoFar;
        private static bool rebootNeeded;
        public const int REBOOTS_MAX = 5;

        public enum PVToolsVersion : int
        {
            None = 0, // system is clean; no drivers installed
            LessThanEight = 7, // system has drivers other than 8.x installed
            Eight = 8, // system has drivers 8.x installed
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

        public static bool GetRebootNeeded() { return rebootNeeded; }

        public static void SetRebootNeeded()
        {
            using (RegistryKey rk =
                       Registry.LocalMachine.OpenSubKey(regKeyName, true))
            {
                rk.SetValue(
                    "RebootNeeded",
                    1,
                    RegistryValueKind.DWord
                );
            }

            rebootNeeded = true;
        }

        public static void UnsetRebootNeeded()
        {
            using (RegistryKey rk =
                       Registry.LocalMachine.OpenSubKey(regKeyName, true))
            {
                rk.SetValue(
                    "RebootNeeded",
                    0,
                    RegistryValueKind.DWord
                );
            }

            rebootNeeded = false;
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
                CfgMgr32.Wait result =
                    CfgMgr32.CMP_WaitNoPendingInstallEvents(0);

                switch (result)
                {
                    case CfgMgr32.Wait.OBJECT_0:
                        tmp = 0;
                        break;
                    case CfgMgr32.Wait.TIMEOUT:
                        tmp = 1;
                        break;
                    case CfgMgr32.Wait.FAILED:
                        Win32ErrorMessage.SetLast(
                            "CMP_WaitNoPendingInstallEvents"
                        );

                        Trace.WriteLine(Win32ErrorMessage.GetLast());
                        throw new Exception(Win32ErrorMessage.GetLast());
                }

                openRegKey.SetValue(
                    regValName,
                    tmp,
                    RegistryValueKind.DWord
                );
            }

            othDrvInstalling = (tmp == 1) ? true : false;
        }

        private static void SetPVToolsVersionOnFirstRun(
            RegistryKey openRegKey)
        {
            string regValName = "PVToolsVersionOnFirstRun";
            int tmp = (int)openRegKey.GetValue(regValName, -1);

            if (tmp == -1)
            {
                if ((XenBus.IsPresent(XenBus.XenBusDevs.DEV_0001, true) &&
                         XenBus.HasChildren(XenBus.XenBusDevs.DEV_0001)) ||
                    (XenBus.IsPresent(XenBus.XenBusDevs.DEV_0002, true) &&
                         XenBus.HasChildren(XenBus.XenBusDevs.DEV_0002)))
                {
                    tmp = 7; // LessThanEight
                }
                else if (XenBus.IsPresent(XenBus.XenBusDevs.DEV_C000, true) &&
                         XenBus.HasChildren(XenBus.XenBusDevs.DEV_C000))
                {
                    tmp = 8; // Eight
                }
                else
                {
                    tmp = 0; // None
                }

                openRegKey.SetValue(
                    regValName,
                    tmp,
                    RegistryValueKind.DWord
                );
            }

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
            using (RegistryKey rk =
                       Registry.LocalMachine.CreateSubKey(regKeyName))
            {
                // These 2 registry keys will not exist on first run. Populate
                // them and only read them afterwards. This needs to be done
                // before any installer-related action takes place.
                SetOtherDriverInstallingOnFirstRun(rk);
                SetPVToolsVersionOnFirstRun(rk);

                SetRebootsSoFar(rk);
            }

            UnsetRebootNeeded();
        }
    }
}