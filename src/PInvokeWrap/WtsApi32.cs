using System;
using System.Runtime.InteropServices;

namespace PInvokeWrap
{
    public static class WtsApi32
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct WTS_SESSION_INFO
        {
            public uint SessionID;
            [MarshalAs(UnmanagedType.LPStr, SizeConst = 256)]
            public string pWinStationName;
            public WTS_CONNECTSTATE_CLASS State;
        }

        public enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        } 

        public enum ID : uint
        {
            OK       = 1,
            CANCEL   = 2,
            ABORT    = 3,
            RETRY    = 4,
            IGNORE   = 5,
            YES      = 6,
            NO       = 7,
            TRYAGAIN = 10,
            CONTINUE = 11,
            TIMEOUT  = 32000,
            ASYNC    = 32001,
        }

        [Flags]
        public enum MB : uint
        // MessageBox style flags; Can pick 1 from
        // each group (apart from the last one)
        {
            // Buttons
            OK                = 0x00000000,
            OKCANCEL          = 0x00000001,
            ABORTRETRYIGNORE  = 0x00000002,
            YESNOCANCEL       = 0x00000003,
            YESNO             = 0x00000004,
            RETRYCANCEL       = 0x00000005,
            CANCELTRYCONTINUE = 0x00000006,
            HELP              = 0x00004000,
            // Display Icons
            ICONSTOP        = 0x00000010,
            ICONERROR       = 0x00000010,
            ICONHAND        = 0x00000010,
            ICONQUESTION    = 0x00000020,
            ICONEXCLAMATION = 0x00000030,
            ICONWARNING     = 0x00000030,
            ICONINFORMATION = 0x00000040,
            ICONASTERISK    = 0x00000040,
            // Default Button
            DEFBUTTON1 = 0x00000000,
            DEFBUTTON2 = 0x00000100,
            DEFBUTTON3 = 0x00000200,
            DEFBUTTON4 = 0x00000300,
            // Modality
            APPLMODAL   = 0x00000000,
            SYSTEMMODAL = 0x00001000,
            TASKMODAL   = 0x00002000,
            // Other options; can use more than 1
            SETFOREGROUND        = 0x00010000,
            DEFAULT_DESKTOP_ONLY = 0x00020000,
            TOPMOST              = 0x00040000,
            RIGHT                = 0x00080000,
            RTLREADING           = 0x00100000,
            SERVICE_NOTIFICATION = 0x00200000,
        }

        public static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;
        public static readonly uint   WTS_CURRENT_SESSION       = 0xFFFFFFFF;
        //                                                        ((DWORD) - 1)

        [DllImport(
            "wtsapi32.dll",
            CharSet = CharSet.Auto,
            SetLastError = true)]
        public static extern bool WTSSendMessage(
                IntPtr hServer,
                uint   SessionId,
                string pTitle,
                uint   TitleLength,
                string pMessage,
                uint   MessageLength,
                MB     Style,
                uint   Timeout,
            out ID     pResponse,
                bool   bWait
        );

        [DllImport(
            "wtsapi32.dll",
            CharSet = CharSet.Auto,
            SetLastError = true)]
        public static extern bool WTSEnumerateSessions(
                IntPtr hServer,
                uint   Reserved, // always 0
                uint   Version, // always 1
            out IntPtr ppSessionInfo, // WTS_SESSION_INFO[]
            out uint   pCount
        );

        [DllImport("wtsapi32.dll")]
        public static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSQueryUserToken(
                UInt32  SessionId,
            out IntPtr phToken
        );
    }
}
