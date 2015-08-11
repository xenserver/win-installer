using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Xenprep
{
    class XenPrepSupport
    {
        public static void LockCD() {}
        public static void SetRestorePoint() {}
        public static void StoreNetworkSettings() {}
        public static void RemovePVDriversFromFilters() {}
        public static void DontBootStartPVDrivers() {}
        public static void UninstallMSIs() {}
        public static void UninstallXenLegacy() {}        
        public static void CleanUpPVDrivers() {}

        public enum INSTALLMESSAGE
        {
            INSTALLMESSAGE_FATALEXIT = 0x00000000, // premature termination, possibly fatal OOM
            INSTALLMESSAGE_ERROR = 0x01000000, // formatted error message
            INSTALLMESSAGE_WARNING = 0x02000000, // formatted warning message
            INSTALLMESSAGE_USER = 0x03000000, // user request message
            INSTALLMESSAGE_INFO = 0x04000000, // informative message for log
            INSTALLMESSAGE_FILESINUSE = 0x05000000, // list of files in use that need to be replaced
            INSTALLMESSAGE_RESOLVESOURCE = 0x06000000, // request to determine a valid source location
            INSTALLMESSAGE_OUTOFDISKSPACE = 0x07000000, // insufficient disk space message
            INSTALLMESSAGE_ACTIONSTART = 0x08000000, // start of action: action name & description
            INSTALLMESSAGE_ACTIONDATA = 0x09000000, // formatted data associated with individual action item
            INSTALLMESSAGE_PROGRESS = 0x0A000000, // progress gauge info: units so far, total
            INSTALLMESSAGE_COMMONDATA = 0x0B000000, // product info for dialog: language Id, dialog caption
            INSTALLMESSAGE_INITIALIZE = 0x0C000000, // sent prior to UI initialization, no string data
            INSTALLMESSAGE_TERMINATE = 0x0D000000, // sent after UI termination, no string data
            INSTALLMESSAGE_SHOWDIALOG = 0x0E000000 // sent prior to display or authored dialog or wizard
        }
        public enum INSTALLLOGMODE  // bit flags for use with MsiEnableLog and MsiSetExternalUI
        {
            INSTALLLOGMODE_FATALEXIT = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_FATALEXIT >> 24)),
            INSTALLLOGMODE_ERROR = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_ERROR >> 24)),
            INSTALLLOGMODE_WARNING = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_WARNING >> 24)),
            INSTALLLOGMODE_USER = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_USER >> 24)),
            INSTALLLOGMODE_INFO = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_INFO >> 24)),
            INSTALLLOGMODE_RESOLVESOURCE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_RESOLVESOURCE >> 24)),
            INSTALLLOGMODE_OUTOFDISKSPACE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_OUTOFDISKSPACE >> 24)),
            INSTALLLOGMODE_ACTIONSTART = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_ACTIONSTART >> 24)),
            INSTALLLOGMODE_ACTIONDATA = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_ACTIONDATA >> 24)),
            INSTALLLOGMODE_COMMONDATA = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_COMMONDATA >> 24)),
            INSTALLLOGMODE_PROPERTYDUMP = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_PROGRESS >> 24)), // log only
            INSTALLLOGMODE_VERBOSE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_INITIALIZE >> 24)), // log only
            INSTALLLOGMODE_EXTRADEBUG = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_TERMINATE >> 24)), // log only
            INSTALLLOGMODE_LOGONLYONERROR = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_SHOWDIALOG >> 24)), // log only    
            INSTALLLOGMODE_PROGRESS = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_PROGRESS >> 24)), // external handler only
            INSTALLLOGMODE_INITIALIZE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_INITIALIZE >> 24)), // external handler only
            INSTALLLOGMODE_TERMINATE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_TERMINATE >> 24)), // external handler only
            INSTALLLOGMODE_SHOWDIALOG = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_SHOWDIALOG >> 24)), // external handler only
            INSTALLLOGMODE_FILESINUSE = (1 << (INSTALLMESSAGE.INSTALLMESSAGE_FILESINUSE >> 24)), // external handler only
        }

        public enum INSTALLUILEVEL
        {
            INSTALLUILEVEL_NOCHANGE = 0,    // UI level is unchanged
            INSTALLUILEVEL_DEFAULT = 1,    // default UI is used
            INSTALLUILEVEL_NONE = 2,    // completely silent installation
            INSTALLUILEVEL_BASIC = 3,    // simple progress and error handling
            INSTALLUILEVEL_REDUCED = 4,    // authored UI, wizard dialogs suppressed
            INSTALLUILEVEL_FULL = 5,    // authored UI with wizards, progress, errors
            INSTALLUILEVEL_ENDDIALOG = 0x80, // display success/failure dialog at end of install
            INSTALLUILEVEL_PROGRESSONLY = 0x40, // display only progress dialog
            INSTALLUILEVEL_HIDECANCEL = 0x20, // do not display the cancel button in basic UI
            INSTALLUILEVEL_SOURCERESONLY = 0x100, // force display of source resolution even if quiet
        }

        [DllImport("msi.dll", SetLastError = true)]
        static extern int MsiSetInternalUI(INSTALLUILEVEL dwUILevel, ref IntPtr phWnd);

        [DllImport("msi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern UInt32 MsiInstallProduct([MarshalAs(UnmanagedType.LPTStr)]string packagePath, [MarshalAs(UnmanagedType.LPTStr)]string commandLine);

        static uint MSIOPENPACKAGEFLAGS_IGNOREMACHINESTATE = 0x00000001;

        [DllImport("msi.dll", SetLastError = false, CharSet = CharSet.Auto)]
        static extern int MsiOpenPackageEx(string pathname, uint flags, ref IntPtr handle);

        [DllImport("msi.dll", SetLastError = true)]
        static extern int MsiGetProductProperty(IntPtr hProduct, string szProperty, StringBuilder lpValueBuf, ref int pcchValueBuf);

        [DllImport("msi.dll", ExactSpelling = true)]
        static extern uint MsiCloseHandle(IntPtr hAny);


        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule,
            [MarshalAs(UnmanagedType.LPStr)]string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);
        static public bool isWOW64()
        {
            bool flags;
            IntPtr modhandle = GetModuleHandle("kernel32.dll");
            if (modhandle == IntPtr.Zero)
                return false;
            if (GetProcAddress(modhandle, "IsWow64Process") == IntPtr.Zero)
                return false;
            if (IsWow64Process(GetCurrentProcess(), out flags))
                return flags;
            return false;
        }
        static public bool is64BitOS()
        {

            if (IntPtr.Size == 8)
                return true;
            return isWOW64();
        }


        public static void InstallGuestAgent() {
            Assembly assembly;
            assembly = Assembly.GetExecutingAssembly();
            byte[] buffer = new byte[16*1024];
            string destname;
            try
            {
                string msitouse;
                if (is64BitOS())
                {
                    msitouse = "Xenprep.citrixguestagentx64.msi";
                }
                else {
                    msitouse = "Xenprep.citrixguestagentx86.msi";
                }
                using (Stream stream = assembly.GetManifestResourceStream(msitouse))
                {

                    do
                    {
                        destname = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Path.GetRandomFileName(), ".msi"));
                    } while (File.Exists(destname));

                    using (Stream destination = File.Create(destname))
                    {
                        try
                        {
                            int read;
                            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                destination.Write(buffer, 0, read);
                            }
                        }
                        catch
                        {
                            File.Delete(destname);
                            throw;
                        }
                    }
                }
            }
            catch
            {
                throw new Exception("Unable to extract guest agent MSI");
            }

            try
            {
                uint uerr;
                string ProductGuid;
                IntPtr producthandle = new IntPtr();
                int err;
                if ((err = MsiOpenPackageEx(destname, MSIOPENPACKAGEFLAGS_IGNOREMACHINESTATE, ref producthandle)) != 0)
                {
                    throw new Exception("Unable to open guest agent MSI");
                }

                try
                {
                    
                    int gsize = 1024;
                    StringBuilder guid = new StringBuilder(gsize, gsize);
                    MsiGetProductProperty(producthandle, "ProductCode", guid, ref gsize);
                    ProductGuid = guid.ToString();
                    try
                    {
                        IntPtr a = IntPtr.Zero;
                        MsiSetInternalUI(INSTALLUILEVEL.INSTALLUILEVEL_NONE, ref a);
                    }
                    catch
                    {
                        throw new Exception("Unable to install guest agent");
                    }
                }
                finally
                {
                    MsiCloseHandle(producthandle);
                }

                if ((uerr = MsiInstallProduct(destname, "REBOOT=ReallySuppress ALLUSERS=1  ARPSYSTEMCOMPONENT=0")) != 0)
                {
                    throw new Exception("Guest agent MSI Installation failed with code " + uerr.ToString());
                }
                   
                try
                {
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + ProductGuid, true).DeleteValue("SystemComponent");
                }
                catch { }

               
            }
            finally
            {
                File.Delete(destname);
            }
        }

        public static void UnlockCD() { }
    }
}
