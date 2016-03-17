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
        public enum MB : long
        // MessageBox style flags; Can pick 1 from
        // each group (apart from the last one)
        {
            // Buttons
            OK                = 0x00000000L,
            OKCANCEL          = 0x00000001L,
            ABORTRETRYIGNORE  = 0x00000002L,
            YESNOCANCEL       = 0x00000003L,
            YESNO             = 0x00000004L,
            RETRYCANCEL       = 0x00000005L,
            CANCELTRYCONTINUE = 0x00000006L,
            HELP              = 0x00004000L,
            // Display Icons
            ICONSTOP        = 0x00000010L,
            ICONERROR       = 0x00000010L,
            ICONHAND        = 0x00000010L,
            ICONQUESTION    = 0x00000020L,
            ICONEXCLAMATION = 0x00000030L,
            ICONWARNING     = 0x00000030L,
            ICONINFORMATION = 0x00000040L,
            ICONASTERISK    = 0x00000040L,
            // Default Button
            DEFBUTTON1 = 0x00000000L,
            DEFBUTTON2 = 0x00000100L,
            DEFBUTTON3 = 0x00000200L,
            DEFBUTTON4 = 0x00000300L,
            // Modality
            APPLMODAL   = 0x00000000L,
            SYSTEMMODAL = 0x00001000L,
            TASKMODAL   = 0x00002000L,
            // Other options; can use more than 1
            SETFOREGROUND        = 0x00010000L,
            DEFAULT_DESKTOP_ONLY = 0x00020000L,
            TOPMOST              = 0x00040000L,
            RIGHT                = 0x00080000L,
            RTLREADING           = 0x00100000L,
            SERVICE_NOTIFICATION = 0x00200000L,
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
                ulong  SessionId,
            out IntPtr phToken
        );
    }
}
