using System;
using System.Runtime.InteropServices;

namespace PInvokeWrap
{
    public static class WinVersion
    {
        private static OSVERSIONINFOEX osvi;
        private static bool is64BitOS;
        private static bool isWOW64;

        private enum ProductType : uint
        {
            NT_WORKSTATION = 1,
            NT_DOMAIN_CONTROLLER = 2,
            NT_SERVER = 3
        }

        private struct OSVERSIONINFOEX
        {
            public uint dwOSVersionInfoSize;
            public uint dwMajorVersion;
            public uint dwMinorVersion;
            public uint dwBuildNumber;
            public uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;
            public Int16 wServicePackMajor;
            public Int16 wServicePackMinor;
            public Int16 wSuiteMask;
            public Byte wProductType;
            public Byte wReserved;
        }

        #region Public Interface
        public static bool IsWOW64() { return isWOW64; }
        public static bool Is64BitOS() { return is64BitOS; }
        public static uint GetPlatformId() { return osvi.dwPlatformId; }
        public static uint GetServicePackMajor() { return (uint)osvi.wServicePackMajor; }
        public static uint GetServicePackMinor() { return (uint)osvi.wServicePackMinor; }
        public static uint GetSuite() { return (uint)osvi.wSuiteMask; }
        public static uint GetProductType() { return osvi.wProductType; }
        public static uint GetMajorVersion() { return osvi.dwMajorVersion; }
        public static uint GetMinorVersion() { return osvi.dwMinorVersion; }

        public static bool IsServerSKU()
        {
            return
                (ProductType) GetProductType() != ProductType.NT_WORKSTATION;
        } 
        #endregion

        // Static Constructor
        static WinVersion()
        {
            osvi = new OSVERSIONINFOEX();
            osvi.dwOSVersionInfoSize = (uint)Marshal.SizeOf(
                typeof(OSVERSIONINFOEX)
            );

            GetVersionEx(ref osvi);
            _IsWOW64();
            _Is64BitOS();
        }

        private static void _IsWOW64()
        {
            bool tmpWOW64;
            IntPtr modHandle = Kernel32.GetModuleHandle("kernel32.dll");

            if (modHandle == IntPtr.Zero)
            {
                isWOW64 = false;
            }
            else if (Kernel32.GetProcAddress(
                         modHandle, "IsWow64Process"
                     ) == IntPtr.Zero)
            {
                isWOW64 = false;
            }
            else if (IsWow64Process(
                         Kernel32.GetCurrentProcess(),
                         out tmpWOW64))
            {
                isWOW64 = tmpWOW64;
            }
            else
            {
                isWOW64 = false;
            }
        }

        private static void _Is64BitOS()
        {
            is64BitOS = IntPtr.Size == 8 ? true : isWOW64;
        }

        // These 2 functions inherently belong here
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
            IntPtr hProcess,
            out bool wow64Process
        );

        // GetVersionEx() is deprecated
        // TODO: replace
        [DllImport("kernel32")]
        private static extern bool GetVersionEx(
            ref OSVERSIONINFOEX osvi
        );
    }
}
