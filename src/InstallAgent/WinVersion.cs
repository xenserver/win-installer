using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InstallAgent
{
    public static class WinVersion
    {
        public static bool isWOW64()
        {
            bool flags;
            IntPtr modhandle = PInvoke.Kernel32.GetModuleHandle("kernel32.dll");

            if (modhandle == IntPtr.Zero)
            {
                return false;
            }

            if (PInvoke.Kernel32.GetProcAddress(modhandle, "IsWow64Process") == IntPtr.Zero)
            {
                return false;
            }

            if (PInvoke.Kernel32.IsWow64Process(
                    PInvoke.Kernel32.GetCurrentProcess(),
                    out flags
            ))
            {
                return flags;
            }

            return false;
        }

        public static bool is64BitOS()
        {
            if (IntPtr.Size == 8)
            {
                return true;
            }

            return isWOW64();
        }
    }
}
