using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace InstallAgent
{
    public static class InstallerState
    {
        private static int currentState;

        private static readonly string stateRegKey =
            InstallAgent.rootRegKey + @"State\";

        private static readonly int complete = (int) (
            States.NoDriverInstalling |
            States.NetworkSettingsStored |
            States.RemovedFromFilters |
            States.BootStartDisabled |
            States.MSIsUninstalled |
            States.XenLegacyUninstalled |
            States.CleanedUp |
            States.DriversStaged |
            States.XenBusInstalled |
            States.XenIfaceInstalled |
            States.XenVifInstalled |
            States.XenNetInstalled |
            States.XenVbdInstalled
        );

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
            new StateInfo("NoDriverInstalling", 1),
            new StateInfo("NetworkSettingsStored", 0),
            new StateInfo("NetworkSettingsRestored", 0),
            new StateInfo("RemovedFromFilters", 0),
            new StateInfo("BootStartDisabled", 0),
            new StateInfo("MSIsUninstalled", 0),
            new StateInfo("XenLegacyUninstalled", 0),
            new StateInfo("CleanedUp", 0),
            new StateInfo("DriversStaged", 0),
            new StateInfo("XenBusInstalled", 0),
            new StateInfo("XenIfaceInstalled", 0),
            new StateInfo("XenVifInstalled", 0),
            new StateInfo("XenNetInstalled", 0),
            new StateInfo("XenVbdInstalled", 0),
        };

        [Flags]
        // For most of the flags, setting them to 1 means either
        // the task is complete or no action required.
        public enum States
        {
            NoDriverInstalling = 1 << 0,
            NetworkSettingsStored = 1 << 1,
            NetworkSettingsRestored = 1 << 2,

            // ---- PV Drivers Removal States ----
            RemovedFromFilters = 1 << 3,
            BootStartDisabled = 1 << 4,
            MSIsUninstalled = 1 << 5,
            XenLegacyUninstalled = 1 << 6,
            CleanedUp = 1 << 7,
            // --------------- End ---------------

            DriversStaged = 1 << 8,

            // ------- PV Drivers Installed -------
            XenBusInstalled = 1 << 9,
            XenIfaceInstalled = 1 << 10,
            XenVifInstalled = 1 << 11,
            XenNetInstalled = 1 << 12,
            XenVbdInstalled = 1 << 13,
            // ---------------- End ---------------
        }

        // Static constructor: queries the Registry for the
        // state of the installer and initializes itself.
        static InstallerState()
        {
            // In .NET 3.5: Creates a new subkey or opens an
            //              existing subkey for write access
            RegistryKey installStateRK =
                Registry.LocalMachine.CreateSubKey(stateRegKey);

            if (installStateRK == null)
            {
                throw new Exception("Failed opening \'InstallAgent\' registry key.");
            }

            currentState = 0;
            
            for (int i = 0; i < statesDefault.Length; ++i)
            {
                int flag = (int) installStateRK.GetValue(
                    statesDefault[i].Name, statesDefault[i].DefaultValue
                );

                currentState |= flag << i;
            }
        }

        public static void SetFlag(States flag)
        {
            int i = (int) Math.Log((double)flag, 2.0);
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
            int i = (int)Math.Log((double)flag, 2.0);
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

        public static bool GetFlag(States flag)
        {
            return (currentState & (int) flag) != 0;
        }

        public static bool Complete()
        {
            return currentState == complete;
        }
    }
}
