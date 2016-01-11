using System;
using System.Runtime.InteropServices;

namespace PInvokeWrap
{
    public static class User32
    {
        [Flags]
        public enum ExitFlags : int
        {
            EWX_LOGOFF = 0x00000000,
            EWX_SHUTDOWN = 0x00000001,
            EWX_REBOOT = 0x00000002,
            EWX_FORCE = 0x00000004,
            EWX_POWEROFF = 0x00000008,
            EWX_FORCEIFHUNG = 0x00000010,
            EWX_RESTARTAPPS = 0x00000040
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern bool ExitWindowsEx(ExitFlags flags, int rea);
    }
}
