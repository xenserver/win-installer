namespace PInvokeWrap
{
    public static class Winbase
    {
        public const uint INFINITE = 0xFFFFFFFF;

        // CMP_WaitNoPendingInstallEvents() return values
        // Originally: WAIT_OBJECT_0 ...
        public enum WAIT : uint
        {
            OBJECT_0 = 0,
            TIMEOUT  = 0x00000102,
            FAILED   = 0xFFFFFFFF
        };
    }
}
