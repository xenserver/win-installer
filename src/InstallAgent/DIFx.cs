using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace PInvoke
{
    abstract class DIFxAll
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

        abstract public Int32 Uninstall(
            string driverPackageInfPath,
            Int32 flags,
            IntPtr pInstallerInfo,
            out bool pNeedReboot
        );
    }

    class DIFx32 : DIFxAll
    {
        [DllImport("DIFxAPI32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Int32 DriverPackageUninstall(
            [MarshalAs(UnmanagedType.LPTStr)] string driverPackageInfPath,
            Int32 flags,
            IntPtr pInstallerInfo,
            out bool pNeedReboot
        );

        override public Int32 Uninstall(
            string driverPackageInfPath,
            Int32 flags,
            IntPtr pInstallerInfo,
            out bool pNeedReboot)
        {
            return DriverPackageUninstall(
                driverPackageInfPath,
                flags,
                pInstallerInfo,
                out pNeedReboot
            );
        }
    }

    class DIFx64 : DIFxAll
    {
        [DllImport("DIFxAPI64.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern Int32 DriverPackageUninstall(
            [MarshalAs(UnmanagedType.LPTStr)] string driverPackageInfPath,
            Int32 flags,
            IntPtr pInstallerInfo,
            out bool pNeedReboot
        );

        override public Int32 Uninstall(
            string driverPackageInfPath,
            Int32 flags,
            IntPtr pInstallerInfo,
            out bool pNeedReboot)
        {
            return DriverPackageUninstall(
                driverPackageInfPath,
                flags,
                pInstallerInfo,
                out pNeedReboot
            );
        }
    }
}
