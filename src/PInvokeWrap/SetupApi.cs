using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PInvokeWrap
{
    public static class SetupApi
    {
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        public const uint DI_REMOVE_DEVICE_GLOBAL = 1;

        [Flags]
        public enum DiGetClassFlags : uint
        {
            DIGCF_DEFAULT = 0x00000001,  // only valid with DIGCF_DEVICEINTERFACE
            DIGCF_PRESENT = 0x00000002,
            DIGCF_ALLCLASSES = 0x00000004,
            DIGCF_PROFILE = 0x00000008,
            DIGCF_DEVICEINTERFACE = 0x00000010,
        }

        public enum StateChangeAction : uint
        {
            Enable = 1,
            Disable = 2
        }

        [Flags]
        public enum Scopes : uint
        {
            Global = 1
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct PropertyChangeParameters
        {
            public uint size;
            public DI_FUNCTION diFunction;
            public StateChangeAction stateChange;
            public Scopes scope;
            public uint hwProfile;
        }

        [Flags]
        public enum DNFlags : uint
        {
            DN_ROOT_ENUMERATED = (0x00000001),// Was enumerated by ROOT
            DN_DRIVER_LOADED = (0x00000002),// Has Register_Device_Driver
            DN_ENUM_LOADED = (0x00000004),// Has Register_Enumerator
            DN_STARTED = (0x00000008),// Is currently configured
            DN_MANUAL = (0x00000010),// Manually installed
            DN_NEED_TO_ENUM = (0x00000020),// May need reenumeration
            DN_NOT_FIRST_TIME = (0x00000040),// Has received a config
            DN_HARDWARE_ENUM = (0x00000080),// Enum generates hardware ID
            DN_LIAR = (0x00000100),// Lied about can reconfig once
            DN_HAS_MARK = (0x00000200),// Not CM_Create_DevInst lately
            DN_HAS_PROBLEM = (0x00000400),// Need device installer
            DN_FILTERED = (0x00000800),// Is filtered
            DN_MOVED = (0x00001000),// Has been moved
            DN_DISABLEABLE = (0x00002000),// Can be disabled
            DN_REMOVABLE = (0x00004000),// Can be removed
            DN_PRIVATE_PROBLEM = (0x00008000),// Has a private problem
            DN_MF_PARENT = (0x00010000),// Multi function parent
            DN_MF_CHILD = (0x00020000),// Multi function child
            DN_WILL_BE_REMOVED = (0x00040000),// DevInst is being removed

            //
            // Windows 4 OPK2 Flags
            //
            DN_NOT_FIRST_TIMEE = 0x00080000,// S: Has received a config enumerate
            DN_STOP_FREE_RES = 0x00100000,// S: When child is stopped, free resources
            DN_REBAL_CANDIDATE = 0x00200000,// S: Don't skip during rebalance
            DN_BAD_PARTIAL = 0x00400000,// S: This devnode's log_confs do not have same resources
            DN_NT_ENUMERATOR = 0x00800000,// S: This devnode's is an NT enumerator
            DN_NT_DRIVER = 0x01000000,// S: This devnode's is an NT driver
            //
            // Windows 4.1 Flags
            //
            DN_NEEDS_LOCKING = 0x02000000,// S: Devnode need lock resume processing
            DN_ARM_WAKEUP = 0x04000000,// S: Devnode can be the wakeup device
            DN_APM_ENUMERATOR = 0x08000000,// S: APM aware enumerator
            DN_APM_DRIVER = 0x10000000,// S: APM aware driver
            DN_SILENT_INSTALL = 0x20000000,// S: Silent install
            DN_NO_SHOW_IN_DM = 0x40000000,// S: No show in device manager
            DN_BOOT_LOG_PROB = 0x80000000  // S: Had a problem during preassignment of boot log conf
        }

        public enum DI_FUNCTION : uint
        {
            DIF_SELECTDEVICE = 1,
            DIF_INSTALLDEVICE = 2,
            DIF_ASSIGNRESOURCES = 3,
            DIF_PROPERTIES = 4,
            DIF_REMOVE = 5,
            DIF_FIRSTTIMESETUP = 6,
            DIF_FOUNDDEVICE = 7,
            DIF_SELECTCLASSDRIVERS = 8,
            DIF_VALIDATECLASSDRIVERS = 9,
            DIF_INSTALLCLASSDRIVERS = 10,
            DIF_CALCDISKSPACE = 11,
            DIF_DESTROYPRIVATEDATA = 12,
            DIF_VALIDATEDRIVER = 13,
            DIF_MOVEDEVICE = 14,
            DIF_DETECT = 15,
            DIF_INSTALLWIZARD = 16,
            DIF_DESTROYWIZARDDATA = 17,
            DIF_PROPERTYCHANGE = 18,
            DIF_ENABLECLASS = 19,
            DIF_DETECTVERIFY = 20,
            DIF_INSTALLDEVICEFILES = 21,
            DIF_UNREMOVE = 22,
            DIF_SELECTBESTCOMPATDRV = 23,
            DIF_ALLOW_INSTALL = 24,
            DIF_REGISTERDEVICE = 25,
            DIF_NEWDEVICEWIZARD_PRESELECT = 26,
            DIF_NEWDEVICEWIZARD_SELECT = 27,
            DIF_NEWDEVICEWIZARD_PREANALYZE = 28,
            DIF_NEWDEVICEWIZARD_POSTANALYZE = 29,
            DIF_NEWDEVICEWIZARD_FINISHINSTALL = 30,
            DIF_UNUSED1 = 31,
            DIF_INSTALLINTERFACES = 32,
            DIF_DETECTCANCEL = 33,
            DIF_REGISTER_COINSTALLERS = 34,
            DIF_ADDPROPERTYPAGE_ADVANCED = 35,
            DIF_ADDPROPERTYPAGE_BASIC = 36,
            DIF_RESERVED1 = 37,
            DIF_TROUBLESHOOTER = 38,
            DIF_POWERMESSAGEWAKE = 39
        }

        // Originally: SPDRP_DEVICEDESC, etc..
        public enum SPDRP : uint
        {
            DEVICEDESC = 0x00000000, // DeviceDesc (R/W)
            HARDWAREID = 0x00000001, // HardwareID (R/W)
            COMPATIBLEIDS = 0x00000002, // CompatibleIDs (R/W)
            UNUSED0 = 0x00000003, // unused
            SERVICE = 0x00000004, // Service (R/W)
            UNUSED1 = 0x00000005, // unused
            UNUSED2 = 0x00000006, // unused
            CLASS = 0x00000007, // Class (R--tied to ClassGUID)
            CLASSGUID = 0x00000008, // ClassGUID (R/W)
            DRIVER = 0x00000009, // Driver (R/W)
            CONFIGFLAGS = 0x0000000A, // ConfigFlags (R/W)
            MFG = 0x0000000B, // Mfg (R/W)
            FRIENDLYNAME = 0x0000000C, // FriendlyName (R/W)
            LOCATION_INFORMATION = 0x0000000D, // LocationInformation (R/W)
            PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000E, // PhysicalDeviceObjectName (R)
            CAPABILITIES = 0x0000000F, // Capabilities (R)
            UI_NUMBER = 0x00000010, // UiNumber (R)
            UPPERFILTERS = 0x00000011, // UpperFilters (R/W)
            LOWERFILTERS = 0x00000012, // LowerFilters (R/W)
            BUSTYPEGUID = 0x00000013, // BusTypeGUID (R)
            LEGACYBUSTYPE = 0x00000014, // LegacyBusType (R)
            BUSNUMBER = 0x00000015, // BusNumber (R)
            ENUMERATOR_NAME = 0x00000016, // Enumerator Name (R)
            SECURITY = 0x00000017, // Security (R/W, binary form)
            SECURITY_SDS = 0x00000018, // Security (W, SDS form)
            DEVTYPE = 0x00000019, // Device Type (R/W)
            EXCLUSIVE = 0x0000001A, // Device is exclusive-access (R/W)
            CHARACTERISTICS = 0x0000001B, // Device Characteristics (R/W)
            ADDRESS = 0x0000001C, // Device Address (R)
            UI_NUMBER_DESC_FORMAT = 0X0000001D, // UiNumberDescFormat (R/W)
            DEVICE_POWER_DATA = 0x0000001E, // Device Power Data (R)
            REMOVAL_POLICY = 0x0000001F, // Removal Policy (R)
            REMOVAL_POLICY_HW_DEFAULT = 0x00000020, // Hardware Removal Policy (R)
            REMOVAL_POLICY_OVERRIDE = 0x00000021, // Removal Policy Override (RW)
            INSTALL_STATE = 0x00000022, // Device Install State (R)
            LOCATION_PATHS = 0x00000023, // Device Location Paths (R)
            BASE_CONTAINERID = 0x00000024  // Base ContainerID (R)
        }

        [StructLayout(LayoutKind.Sequential)]
        public class SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid classGuid;
            public uint devInst;
            public IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_CLASSINSTALL_HEADER
        {
            public uint cbSize;
            public DI_FUNCTION InstallFunction;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_REMOVEDEVICE_PARAMS
        {
            public SP_CLASSINSTALL_HEADER ClassInstallHeader;
            public uint Scope;
            public uint HwProfile;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiCallClassInstaller(
             DI_FUNCTION installFunction,
             IntPtr deviceInfoSet,
             SP_DEVINFO_DATA deviceInfoData
        );

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiCallClassInstaller(
             DI_FUNCTION installFunction,
             IntPtr deviceInfoSet,
             IntPtr deviceInfoData
        );

        public class DeviceInfoSet : IDisposable
        {
            private IntPtr devInfoSet;

            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
            private static extern IntPtr SetupDiCreateDeviceInfoList(
                ref Guid classGuid,
                IntPtr hwndParent
            );

            [DllImport("setupapi.dll", SetLastError = true)]
            private static extern bool SetupDiDestroyDeviceInfoList(
                 IntPtr deviceInfoSet
            );

            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
            private static extern IntPtr SetupDiGetClassDevs(
                ref Guid classGuid,
                [MarshalAs(UnmanagedType.LPTStr)] string enumerator,
                IntPtr hwndParent,
                DiGetClassFlags flags
            );

            // 2nd form uses an Enumerator only, with null ClassGUID
            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
            private static extern IntPtr SetupDiGetClassDevs(
                IntPtr classGuid,
                string enumerator,
                IntPtr hwndParent,
                DiGetClassFlags flags
            );

            // 3rd form, first 3 input vars are null
            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
            private static extern IntPtr SetupDiGetClassDevs(
                IntPtr classGuid,
                IntPtr enumerator,
                IntPtr hwndParent,
                DiGetClassFlags flags
            );

            public DeviceInfoSet(
                ref Guid classGuid_,
                IntPtr hwndParent_)
            {
                devInfoSet = SetupDiCreateDeviceInfoList(
                    ref classGuid_,
                    hwndParent_
                );
            }

            public DeviceInfoSet(
                ref Guid classGuid_,
                [MarshalAs(UnmanagedType.LPTStr)] string enumerator_,
                IntPtr hwndParent_,
                DiGetClassFlags flags_)
            {
                devInfoSet = SetupDiGetClassDevs(
                    ref classGuid_,
                    enumerator_,
                    hwndParent_,
                    flags_
                );
            }

            public DeviceInfoSet(
                IntPtr classGuid_,
                string enumerator_,
                IntPtr hwndParent_,
                DiGetClassFlags flags_)
            {
                devInfoSet = SetupDiGetClassDevs(
                    classGuid_,
                    enumerator_,
                    hwndParent_,
                    flags_
                );
            }

            public DeviceInfoSet(
                IntPtr classGuid_,
                IntPtr enumerator_,
                IntPtr hwndParent_,
                DiGetClassFlags flags_)
            {
                devInfoSet = SetupDiGetClassDevs(
                    classGuid_,
                    enumerator_,
                    hwndParent_,
                    flags_
                );
            }

            public void Dispose()
            {
                if (this.HandleIsValid() &&
                    !SetupDiDestroyDeviceInfoList(devInfoSet))
                {
                    throw new Exception(
                        "SetupDiDestroyDeviceInfoList() failed"
                    );
                }
            }

            public IntPtr Get() { return devInfoSet; }

            public bool HandleIsValid()
            {
                if (devInfoSet != INVALID_HANDLE_VALUE)
                {
                    return true;
                }

                return false;
            }
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInfo(
            IntPtr deviceInfoSet,
            uint memberIndex,
            SP_DEVINFO_DATA deviceInfoData
        );

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            SP_DEVINFO_DATA deviceInfoData,
            SPDRP property,
            out int propertyRegDataType,
            byte[] propertyBuffer,
            int propertyBufferSize,
            out int requiredSize
        );

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SetupDiSetClassInstallParams(
            IntPtr deviceInfoSet,
            SP_DEVINFO_DATA deviceInfoData,
            ref IntPtr classInstallParams,
            int classInstallParamsSize
        );

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SetupDiSetClassInstallParams(
            IntPtr deviceInfoSet,
            SP_DEVINFO_DATA deviceInfoData,
            ref SP_REMOVEDEVICE_PARAMS classInstallParams,
            int classInstallParamsSize
        );

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SetupDiSetClassInstallParams(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            IntPtr paramaters,
            int ClassInstallParamsSize
        );

        // Originally: SPOST_NONE, etc..
        public enum SPOST : uint
        {
            NONE = 0,
            PATH = 1,
            URL = 2,
            MAX = 3
        }

        [Flags]
        // Originally: SP_COPY_DELETESOURCE, etc..
        public enum SP_COPY : uint
        {
            DEFAULT = 0x0000000,  // just to privide a 0 value
            DELETESOURCE = 0x0000001,   // delete source file on successful copy
            REPLACEONLY = 0x0000002,   // copy only if target file already present
            NEWER = 0x0000004,   // copy only if source newer than or same as target
            NEWER_OR_SAME = NEWER,
            NOOVERWRITE = 0x0000008,   // copy only if target doesn't exist
            NODECOMP = 0x0000010,   // don't decompress source file while copying
            LANGUAGEAWARE = 0x0000020,   // don't overwrite file of different language
            SOURCE_ABSOLUTE = 0x0000040,   // SourceFile is a full source path
            SOURCEPATH_ABSOLUTE = 0x0000080,   // SourcePathRoot is the full path
            IN_USE_NEEDS_REBOOT = 0x0000100,   // System needs reboot if file in use
            FORCE_IN_USE = 0x0000200,   // Force target-in-use behavior
            NOSKIP = 0x0000400,   // Skip is disallowed for this file or section
            CABINETCONTINUATION = 0x0000800,   // Used with need media notification
            FORCE_NOOVERWRITE = 0x0001000,   // like NOOVERWRITE but no callback nofitication
            FORCE_NEWER = 0x0002000,   // like NEWER but no callback nofitication
            WARNIFSKIP = 0x0004000,   // system critical file: warn if user tries to skip
            NOBROWSE = 0x0008000,   // Browsing is disallowed for this file or section
            NEWER_ONLY = 0x0010000   // copy only if source file newer than target
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupCopyOEMInf(
            string SourceInfFileName,
            string OEMSourceMediaLocation,
            SPOST OEMSourceMediaType,
            SP_COPY CopyStyle,
            IntPtr DestinationInfFileName, // == IntPtr.Zero
            int DestinationInfFileNameSize, // == 0
            IntPtr RequiredSize, // == IntPtr.Zero
            IntPtr DestinationInfFileNameComponent // == IntPtr.Zero
        );

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SetupDiGetDeviceInstanceId(
            IntPtr DeviceInfoSet,
            SP_DEVINFO_DATA deviceInfoData,
            StringBuilder deviceInstanceId,
            int deviceInstanceIdSize,
            out int requiredSize
        );
    }
}
