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

        public static void FindLast(string win32FuncName)
        {
            message =
                win32FuncName + " failed: " +
                new Win32Exception(
                    Marshal.GetLastWin32Error()
                ).Message;
        }
    }
}
