using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace InstallAgent
{
    // Should the class be static or not???
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
            States.CleanedUp
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
            new StateInfo("CleanedUp", 0)
        };

        [Flags]
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
            CleanedUp = 1 << 7
            // ----- PV Drivers Removal End ------
        }

        // Queries the Registry for the state of the
        // installer and initializes the static class.
        public static void Initialize()
        {
            // In .NET 3.5: Creates a new subkey or opens an
            //              existing subkey for write access
            RegistryKey installStateRK = Registry.LocalMachine.CreateSubKey(
                stateRegKey
            );

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

            using (RegistryKey installStateRK = Registry.LocalMachine.OpenSubKey(
                stateRegKey, true
            ))
            {
                installStateRK.SetValue(flagName, 1, RegistryValueKind.DWord);
                currentState |= (int)flag;
            }
        }

        public static void UnsetFlag(States flag)
        {
            int i = (int)Math.Log((double)flag, 2.0);
            string flagName = statesDefault[i].Name;

            using (RegistryKey installStateRK = Registry.LocalMachine.OpenSubKey(
                stateRegKey, true
            ))
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


/*
[Flags]
 * 
 * GotDrivers = 1 << 00, // I have determined the right drivers are installed and functioning
            DriversPlaced = 1 << 01, // I have put drivers in place so that they will install after reboot(s)
            GotOldDrivers = 1 << 02, // I have determined that an out of date (MSI) set of drivers are installed
            DriversUpdating = 1 << 03, // I have attempted to update drivers with a more recent version.  This will complete following a reboot
            NsisFree = 1 << 04, // I have determined that an old NSIS-installer of drivers and guest agent is not installed
            NsisHandlingRequired = 1 << 05, // I have initiated the uninstall of an old NSIS-installed - it will be finished post-reboot
            HWIDCorrect = 1 << 06, // The HWID is the one the new drivers expect
            GotAgent = 1 << 07, // I have determined that the correct MSI installed guest agent is present
            RebootNow = 1 << 08, // When I'm no longer able to make any state transitions, I should reboot
            NeedShutdown = 1 << 09, // When I'm ready to reboot, I should shutdown and await a restart (not reboot)
            Polling = 1 << 10, // I should wait for a few minutes - if no state transitions occur, I should reboot
            PollTimedOut = 1 << 11, // We have been in the polling state for too long
            RebootReady = 1 << 12, // I am ready to reboot.  If in active mode, the user has agreed for their machine to shut down
            PreReboot = 1 << 13, // State in which to carry out actions before we offer a shutdown / reboot
            Passive = 1 << 14, // We are in passive mode - no user interaction is required
            NoShutdown = 1 << 15, // We should not shutdown when running in passive mode
            UserRebootTriggered = 1 << 16, // A user has agreed we should reboot / shutdown (active mode only)
            Done = 1 << 17, // We have been told to exit the service
            PauseOnStart = 1 << 18, // We should pause the service thread before starting
            RebootDesired = 1 << 19, // We wish to reboot, but need a user trigger
            Installed = 1 << 20, // We believe everything to be installed and working perfectly.
            Cancelled = 1 << 21, // The User has cancelled the installation.  Service should be set to manual restart.
            Failed = 1 << 22, // The install has failed.  Service should be set to manual restart
            Rebooting = 1 << 23, // We are now running the final apps before rebooting, or rebooting
            AutoShutdown = 1 << 24, // We want all future shutdowns to happen without user intervention
            GotVssProvider = 1 << 25, // I have determined that the correct MSI installed vss provider is present
            OneFinalReboot = 1 << 26, // We have been restored after one extra reboot has been scheduled.  Don't schedule another reboot.
            NeedsReboot = 1 << 27, // We will have to reboot before we have finished, but should continue to poll for now
 * 
 * 
        public enum States { }

        [Flags]
        public enum ExitFlags : int { }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool ExitWindowsEx(ExitFlags flg, int rea);

        [DllImport("advapi32.dll", CharSet=CharSet.Auto, SetLastError=true)]
        public static extern bool InitiateSystemShutdownEx(
            string lpMachineName,
            string lpMessage,
            uint dwTimeout,
            bool bForceAppsClosed,
            bool bRebootAfterShutdown,
            ShutdownReason dwReason);

        [Flags]
        public enum ShutdownReason : uint { }

        public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

        [DllImport("kernel32")]
        private static extern bool GetVersionEx(ref OSVERSIONINFO osvi);

        struct OSVERSIONINFO { }

        public class WinVersion { }

        public string failreason = "";

        public void Fail(string reason) { }

        public static void winReboot() { }

        public static bool ExistingDriverInstalling = false;

        public const UInt32 SE_PRIVILEGE_ENABLED_BY_DEFAULT = 0x00000001;
        public const UInt32 SE_PRIVILEGE_REMOVED = 0x00000004;
        public const UInt32 SE_PRIVILEGE_USED_FOR_ACCESS = 0x80000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID { }

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES { }

        private const Int32 ANYSIZE_ARRAY = 1;
        public struct TOKEN_PRIVILEGES { }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue();

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken();

        public static void Initialize();
        
*/