using HelperFunctions;
using Microsoft.Win32;
using System;

namespace State
{
    public static class Installer
    {
        private static int currentState;

        private static readonly string stateRegKey =
            InstallAgent.InstallAgent.rootRegKeyName + @"\InstallerState";

        private static readonly int systemCleaned = (int)(
            States.RemovedFromFilters |
            States.BootStartDisabled |
            States.MSIsUninstalled |
            States.DrvsAndDevsUninstalled |
            States.CleanedUp
        );

        private static readonly int everythingInstalled = (int)(
            States.XenBusInstalled |
            States.XenIfaceInstalled |
            States.XenVifInstalled |
            States.XenNetInstalled |
            States.XenVbdInstalled |
            States.CertificatesInstalled
        );

        private static readonly int complete =
            (int) (
                States.NetworkSettingsSaved |
                States.NetworkSettingsRestored) |
            systemCleaned |
            everythingInstalled;

        private struct StateInfo
        {
            private readonly string name;
            private readonly int defaultValue;

            public StateInfo(string name, int defaultValue)
            {
                this.name = name;
                this.defaultValue = defaultValue;
            }

            public string Name { get { return name; } }
            public int DefaultValue { get { return defaultValue; } }
        };

        private static readonly StateInfo[] statesDefault = {
            new StateInfo("NetworkSettingsSaved", 0),
            new StateInfo("NetworkSettingsRestored", 0),
            new StateInfo("RemovedFromFilters", 0),
            new StateInfo("BootStartDisabled", 0),
            new StateInfo("MSIsUninstalled", 0),
            new StateInfo("DrvsAndDevsUninstalled", 0),
            new StateInfo("CleanedUp", 0),
            new StateInfo("XenBusInstalled", 0),
            new StateInfo("XenIfaceInstalled", 0),
            new StateInfo("XenVifInstalled", 0),
            new StateInfo("XenNetInstalled", 0),
            new StateInfo("XenVbdInstalled", 0),
            new StateInfo("CertificatesInstalled", 0),
            new StateInfo("ProceedWithSystemClean", 0),
        };

        [Flags]
        // For most of the flags, setting them to 1 means either
        // the task is complete or no action required.
        public enum States
        {
            NetworkSettingsSaved = 1 << 0,
            NetworkSettingsRestored = 1 << 1,

            // ---- PV Drivers Removal States ----
            RemovedFromFilters = 1 << 2,
            BootStartDisabled = 1 << 3,
            MSIsUninstalled = 1 << 4,
            DrvsAndDevsUninstalled = 1 << 5,
            CleanedUp = 1 << 6,
            // --------------- End ---------------

            // ------- PV Drivers Installed -------
            XenBusInstalled = 1 << 7,
            XenIfaceInstalled = 1 << 8,
            XenVifInstalled = 1 << 9,
            XenNetInstalled = 1 << 10,
            XenVbdInstalled = 1 << 11,
            // ---------------- End ---------------

            CertificatesInstalled = 1 << 12,
            ProceedWithSystemClean = 1 << 13,
        }

        // Static constructor: queries the Registry for the
        // state of the installer and initializes itself.
        static Installer()
        {
            currentState = 0;

            using (RegistryKey installStateRK =
                    Registry.LocalMachine.CreateSubKey(stateRegKey))
            {
                for (int i = 0; i < statesDefault.Length; ++i)
                {
                    int flag = (int)installStateRK.GetValue(
                        statesDefault[i].Name, statesDefault[i].DefaultValue
                    );

                    currentState |= flag << i;
                }
            }
        }

        public static void SetFlag(States flag)
        {
            int i = Helpers.BitIdxFromFlag((uint)flag);
            string flagName = statesDefault[i].Name;

            using (RegistryKey installStateRK =
                       Registry.LocalMachine.OpenSubKey(stateRegKey, true))
            {
                installStateRK.SetValue(flagName, 1, RegistryValueKind.DWord);
                currentState |= (int)flag;
            }
        }

        public static void UnsetFlag(States flag)
        {
            int i = Helpers.BitIdxFromFlag((uint)flag);
            string flagName = statesDefault[i].Name;

            using (RegistryKey installStateRK =
                       Registry.LocalMachine.OpenSubKey(stateRegKey, true))
            {
                installStateRK.SetValue(flagName, 0, RegistryValueKind.DWord);
                currentState &= ~(int)flag;
            }
        }

        public static void FlipFlag(States flag)
        {
            if (GetFlag(flag))
            {
                UnsetFlag(flag);
            }
            else
            {
                SetFlag(flag);
            }
        }

        public static void LogicalANDFlag(States flag, bool value)
        {
            if (GetFlag(flag) & value)
            {
                SetFlag(flag);
            }
            else
            {
                UnsetFlag(flag);
            }
        }

        public static void LogicalORFlag(States flag, bool value)
        {
            if (GetFlag(flag) | value)
            {
                SetFlag(flag);
            }
            else
            {
                UnsetFlag(flag);
            }
        }

        public static bool GetFlag(States flag)
        {
            return (currentState & (int) flag) != 0;
        }

        public static bool SystemCleaned()
        {
            // (currentState & systemCleaned) clears all other bits
            // so we can check just the ones we need
            return (currentState & systemCleaned) == systemCleaned;
        }

        public static bool EverythingInstalled()
        {
            return (currentState & everythingInstalled) == everythingInstalled;
        }

        public static bool Complete()
        {
            return currentState == complete;
        }
    }
}
