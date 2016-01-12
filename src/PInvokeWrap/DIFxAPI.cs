using System;
using System.Runtime.InteropServices;

namespace PInvokeWrap
{
    public static class DIFxAPI
    {
        public enum DRIVER_PACKAGE
        {
            REPAIR = 0x00000001,
            SILENT = 0x00000002,
            FORCE = 0x00000004,
            ONLY_IF_DEVICE_PRESENT = 0x00000008,
            LEGACY_MODE = 0x00000010,
            DELETE_FILES = 0x00000020,
        }

        [DllImport("DIFxAPI64.dll",
            CharSet = CharSet.Auto,
            EntryPoint = "DriverPackageUninstall")]
        private extern static int DriverPackageUninstall_64(
            [MarshalAs(UnmanagedType.LPTStr)] string driverPackageInfPath,
            int flags,
            IntPtr pInstallerInfo,
            out bool pNeedReboot
        );

        [DllImport("DIFxAPI32.dll",
            CharSet = CharSet.Auto,
            EntryPoint = "DriverPackageUninstall")]
        private extern static int DriverPackageUninstall_32(
            [MarshalAs(UnmanagedType.LPTStr)] string driverPackageInfPath,
            int flags,
            IntPtr pInstallerInfo,
            out bool pNeedReboot
        );

        public static int DriverPackageUninstall(
            [MarshalAs(UnmanagedType.LPTStr)] string driverPackageInfPath,
            int flags,
            IntPtr pInstallerInfo,
            out bool pNeedReboot)
        {
            if (WinVersion.Is64BitOS())
            {
                return DriverPackageUninstall_64(
                    driverPackageInfPath,
                    flags,
                    pInstallerInfo,
                    out pNeedReboot
                );
            }

            return DriverPackageUninstall_32(
                driverPackageInfPath,
                flags,
                pInstallerInfo,
                out pNeedReboot
            );
        }
    }
}
