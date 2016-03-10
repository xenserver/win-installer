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
                win32FuncName + "() - Error [" + error +
                "]: " + new Win32Exception(error).Message;
        }

        public static void Set(int err = -1)
        {
            error = (err == -1) ? Marshal.GetLastWin32Error() : err;

            message =
                "Error [" + error + "]: " +
                new Win32Exception(error).Message;
        }

        public static void SetCR(string cmFuncName, CfgMgr32.CR configRet)
        // Tries to map CfgMgr32 error codes to Win32 error codes
        // before setting 'error' and 'message'. Both error codes
        // and messages are written.
        {
            // We return ERROR_SUCCESS if the
            // error code cannot be mapped
            int win32Err = CfgMgr32.CM_MapCrToWin32Err(
                configRet,
                WinError.ERROR_SUCCESS
            );

            message =
                cmFuncName + "() - CR_Error [" + (int)configRet + "]: " +
                configRet + " => Win32_Error [";

            if (win32Err == WinError.ERROR_SUCCESS)
            {
                message += "-]: No equivalent Win32 error code exists";
                error = (int)configRet;
            }
            else
            {
                message +=
                    win32Err + "]: " + new Win32Exception(win32Err).Message;
                error = win32Err;
            }
        }
    }
}
