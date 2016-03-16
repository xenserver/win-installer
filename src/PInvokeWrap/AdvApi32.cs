using System;
using System.Runtime.InteropServices;

namespace PInvokeWrap
{
    public static class AdvApi32
    {
        private const int ANYSIZE_ARRAY = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        public struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ANYSIZE_ARRAY)]
            public LUID_AND_ATTRIBUTES[] Privileges;
        }

        public enum TOKEN_INFORMATION_CLASS
        {
            TokenUser                             = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId,
            TokenGroupsAndPrivileges,
            TokenSessionReference,
            TokenSandBoxInert,
            TokenAuditPolicy,
            TokenOrigin,
            TokenElevationType,
            TokenLinkedToken,
            TokenElevation,
            TokenHasRestrictions,
            TokenAccessInformation,
            TokenVirtualizationAllowed,
            TokenVirtualizationEnabled,
            TokenIntegrityLevel,
            TokenUIAccess,
            TokenMandatoryPolicy,
            TokenLogonSid,
            TokenIsAppContainer,
            TokenCapabilities,
            TokenAppContainerSid,
            TokenAppContainerNumber,
            TokenUserClaimAttributes,
            TokenDeviceClaimAttributes,
            TokenRestrictedUserClaimAttributes,
            TokenRestrictedDeviceClaimAttributes,
            TokenDeviceGroups,
            TokenRestrictedDeviceGroups,
            TokenSecurityAttributes,
            TokenIsRestricted,
            MaxTokenInfoClass
        }

        public struct TOKEN_USER
        {
            public SID_AND_ATTRIBUTES User;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SID_AND_ATTRIBUTES
        {
            public IntPtr Sid;
            public int Attributes;
        }

        // The place/naming of the following constants
        // may not be the best, but will do for now.
        public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
        public const string SE_LOAD_DRIVER_NAME = "SeLoadDriverPrivilege";

        [Flags]
        public enum AccessRights : uint
        {
            DELETE = 0x00010000,
            READ_CONTROL = 0x00020000,
            WRITE_DAC = 0x00040000,
            WRITE_OWNER = 0x00080000,
            SYNCHRONIZE = 0x00100000
        }

        [Flags]
        public enum StandardRights : uint
        {
            REQUIRED = AccessRights.DELETE |
                       AccessRights.READ_CONTROL |
                       AccessRights.WRITE_DAC |
                       AccessRights.WRITE_OWNER,
            READ = AccessRights.READ_CONTROL,
            WRITE = AccessRights.READ_CONTROL,
            EXECUTE = AccessRights.READ_CONTROL,
            ALL = AccessRights.DELETE |
                  AccessRights.READ_CONTROL |
                  AccessRights.WRITE_DAC |
                  AccessRights.WRITE_OWNER |
                  AccessRights.SYNCHRONIZE
        }

        [Flags]
        public enum Token : uint
        {
            ASSIGN_PRIMARY = 0x0001,
            DUPLICATE = 0x0002,
            IMPERSONATE = 0x0004,
            QUERY = 0x0008,
            QUERY_SOURCE = 0x0010,
            ADJUST_PRIVILEGES = 0x0020,
            ADJUST_GROUPS = 0x0040,
            ADJUST_DEFAULT = 0x0080,
            ADJUST_SESSIONID = 0x0100,
            //--------------------------------------------
            READ = (uint) StandardRights.READ | QUERY,
            ALL_ACCESS_P = (uint)StandardRights.REQUIRED |
                           ASSIGN_PRIMARY |
                           DUPLICATE |
                           IMPERSONATE |
                           QUERY |
                           QUERY_SOURCE |
                           ADJUST_PRIVILEGES |
                           ADJUST_GROUPS |
                           ADJUST_DEFAULT,
            ALL_ACCESS = (uint)ALL_ACCESS_P | ADJUST_SESSIONID
        }

        [Flags]
        public enum Se_Privilege : uint
        {
            ENABLED_BY_DEFAULT = 0x00000001,
            ENABLED = 0x00000002,
            REMOVED = 0x00000004,
            USED_FOR_ACCESS = 0x80000000
        }

        [Flags]
        // Originally: SHTDN_REASON_MAJOR_OTHER ...
        public enum ShtdnReason : uint
        {
            // Microsoft major reasons.
            MAJOR_OTHER = 0x00000000,
            MAJOR_NONE = 0x00000000,
            MAJOR_HARDWARE = 0x00010000,
            MAJOR_OPERATINGSYSTEM = 0x00020000,
            MAJOR_SOFTWARE = 0x00030000,
            MAJOR_APPLICATION = 0x00040000,
            MAJOR_SYSTEM = 0x00050000,
            MAJOR_POWER = 0x00060000,
            MAJOR_LEGACY_API = 0x00070000,

            // Microsoft minor reasons.
            MINOR_OTHER = 0x00000000,
            MINOR_NONE = 0x000000ff,
            MINOR_MAINTENANCE = 0x00000001,
            MINOR_INSTALLATION = 0x00000002,
            MINOR_UPGRADE = 0x00000003,
            MINOR_RECONFIG = 0x00000004,
            MINOR_HUNG = 0x00000005,
            MINOR_UNSTABLE = 0x00000006,
            SHTDN_REASON_MINOR_DISK = 0x00000007,
            MINOR_PROCESSOR = 0x00000008,
            MINOR_NETWORKCARD = 0x00000000,
            MINOR_POWER_SUPPLY = 0x0000000a,
            MINOR_CORDUNPLUGGED = 0x0000000b,
            MINOR_ENVIRONMENT = 0x0000000c,
            MINOR_HARDWARE_DRIVER = 0x0000000d,
            MINOR_OTHERDRIVER = 0x0000000e,
            MINOR_BLUESCREEN = 0x0000000F,
            MINOR_SERVICEPACK = 0x00000010,
            MINOR_HOTFIX = 0x00000011,
            MINOR_SECURITYFIX = 0x00000012,
            MINOR_SECURITY = 0x00000013,
            MINOR_NETWORK_CONNECTIVITY = 0x00000014,
            MINOR_WMI = 0x00000015,
            MINOR_SERVICEPACK_UNINSTALL = 0x00000016,
            MINOR_HOTFIX_UNINSTALL = 0x00000017,
            MINOR_SECURITYFIX_UNINSTALL = 0x00000018,
            MINOR_MMC = 0x00000019,
            MINOR_TERMSRV = 0x00000020,

            // Flags that end up in the event log code.
            FLAG_USER_DEFINED = 0x40000000,
            FLAG_PLANNED = 0x80000000,
            UNKNOWN = MINOR_NONE,
            LEGACY_API = (MAJOR_LEGACY_API | FLAG_PLANNED),

            // This mask cuts out UI flags.
            SHTDN_REASON_VALID_BIT_MASK = 0xc0ffffff
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
            [MarshalAs(UnmanagedType.Bool)]bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            uint Zero,
            IntPtr Null1,
            IntPtr Null2
        );

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool LookupPrivilegeValue(
            IntPtr Null1,
            string lpName,
            out LUID lpLuid
        );

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool OpenProcessToken(
            IntPtr ProcessHandle,
            uint DesiredAccess,
            out IntPtr TokenHandle
        );

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool InitiateSystemShutdownEx(
            string lpMachineName,
            string lpMessage,
            uint dwTimeout,
            bool bForceAppsClosed,
            bool bRebootAfterShutdown,
            ShtdnReason dwReason
        );

        // Needed in order to change a service's startup type programmatically
        // TODO: Make proper/full enums/defines
        public const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
        public const uint SERVICE_QUERY_CONFIG = 0x00000001;
        public const uint SERVICE_CHANGE_CONFIG = 0x00000002;
        public const uint DELETE = 0x00010000;
        public const uint SC_MANAGER_ALL_ACCESS = 0x000F003F;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern Boolean ChangeServiceConfig(
            IntPtr hService,
            uint nServiceType,
            uint nStartType,
            uint nErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            [In] char[] lpDependencies,
            string lpServiceStartName,
            string lpPassword,
            string lpDisplayName
        );

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr OpenService(
            IntPtr hSCManager,
            string lpServiceName,
            uint dwDesiredAccess
        );

        [DllImport(
            "advapi32.dll",
            EntryPoint = "OpenSCManagerW",
            ExactSpelling = true,
            CharSet = CharSet.Unicode,
            SetLastError = true)]
        public static extern IntPtr OpenSCManager(
            string machineName,
            string databaseName,
            uint dwAccess
        );

        [DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle")]
        public static extern int CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool DeleteService(IntPtr hService);
        // ------------- End -------------

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool GetTokenInformation(
                IntPtr                  TokenHandle,
                TOKEN_INFORMATION_CLASS TokenInformationClass,
                IntPtr                  TokenInformation,
                UInt32                  TokenInformationLength,
            out UInt32                  ReturnLength
        );

        // Using IntPtr for pSID insted of Byte[]
        [DllImport("advapi32", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool ConvertSidToStringSid(
                IntPtr pSID,
            out IntPtr ptrSid
        );
    }
}
