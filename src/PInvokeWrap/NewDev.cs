using System;
using System.Runtime.InteropServices;

namespace PInvokeWrap
{
    public static class NewDev
    {
        // Originally: INSTALLFLAG_FORCE, ...
        public enum INSTALLFLAG
        {
            FORCE = 0x00000001,
            READONLY = 0x00000002,
            NONINTERACTIVE = 0x00000004,
            BITS = 0x00000007
        }

        [Flags]
        public enum DIIRFLAG
        {
            ZERO = 0x00000000,
            FORCE_INF = 0x00000002
        }

        [DllImport("newdev.dll", SetLastError = true)]
        public static extern bool UpdateDriverForPlugAndPlayDevices(
            IntPtr hWndParent,
            string hardwareId,
            string fullInfPath,
            INSTALLFLAG installFlags,
            out bool rebootRequired
        );

        [DllImport("newdev.dll", SetLastError = true)]
        public static extern bool DiInstallDriver(
            IntPtr hwndParent,
            string FullInfPath,
            DIIRFLAG Flags, // either ZERO or FORCE_INF
            out bool NeedReboot
        );
    }
}
