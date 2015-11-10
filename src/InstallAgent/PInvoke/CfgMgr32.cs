using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace PInvoke
{
    class CfgMgr32
    {
        public const uint INFINITE = 0xFFFFFFFF;

        // CMP_WaitNoPendingInstallEvents() return values
        // Originally: WAIT_OBJECT_0 ...
        public enum Wait : uint
        {
            OBJECT_0 = 0,
            TIMEOUT = 0x00000102,
            FAILED = 0xFFFFFFFF
        };

        [DllImport(
            "cfgmgr32.dll",
            SetLastError = true,
            EntryPoint = "CMP_WaitNoPendingInstallEvents",
            CharSet = CharSet.Auto)]
        public static extern uint CMP_WaitNoPendingInstallEvents(uint timeOut);
    }
}
