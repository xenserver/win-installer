namespace PInvokeWrap
{
    public static class WinError
    // The purpose of this class is to provide the system
    // error codes that programs explicitly need to check
    // for.
    {
        public const int ERROR_SUCCESS                  = 0;
        public const int ERROR_INSUFFICIENT_BUFFER      = 122;
        public const int ERROR_NO_MORE_ITEMS            = 259;
        public const int ERROR_SUCCESS_REBOOT_INITIATED = 1641;
        public const int ERROR_SUCCESS_REBOOT_REQUIRED  = 3010;
    }
}
