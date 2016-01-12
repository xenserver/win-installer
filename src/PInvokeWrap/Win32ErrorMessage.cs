using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PInvokeWrap
{
    public static class Win32Error
    {
        private static string message;
        private static int error;

        public static string GetFullErrMsg()
        {
            return message;
        }

        public static int GetErrorNo()
        {
            return error;
        }

        public static void Set(string win32FuncName, int err = -1)
        {
            error = (err == -1) ? Marshal.GetLastWin32Error() : err;

            message =
                win32FuncName + " - Error [" + error +
                "]: " + new Win32Exception(error).Message;
        }
    }
}
