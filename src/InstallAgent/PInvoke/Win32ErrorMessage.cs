using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PInvoke
{
    public static class Win32ErrorMessage
    {
        private static string message;

        public static string GetLast()
        {
            return message;
        }

        public static void SetLast(string win32FuncName)
        {
            int err = Marshal.GetLastWin32Error();

            message =
                win32FuncName + " failed; Error " + err +
                ": " + new Win32Exception(err).Message;
        }
    }
}
