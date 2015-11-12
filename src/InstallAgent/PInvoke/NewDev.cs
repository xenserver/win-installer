using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace PInvoke
{
    class NewDev
    {
        // Originally: INSTALLFLAG_FORCE, ...
        public enum INSTALLFLAG
        {
            FORCE = 0x00000001,
            READONLY = 0x00000002,
            NONINTERACTIVE = 0x00000004,
            BITS = 0x00000007
        }

        [DllImport("newdev.dll", SetLastError = true)]
        public static extern bool UpdateDriverForPlugAndPlayDevices(
            IntPtr hWndParent,
            string hardwareId,
            string fullInfPath,
            INSTALLFLAG installFlags,
            out bool rebootRequired
        );
    }
}
