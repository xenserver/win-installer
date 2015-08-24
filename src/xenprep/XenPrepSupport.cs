using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Xenprep
{
    class XenPrepSupport
    {
        [DllImport("DIFxAPI.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern Int32 DriverPackageUninstall([MarshalAs(UnmanagedType.LPTStr)] string DriverPackageInfPath, Int32 Flags, IntPtr pInstallerInfo, out bool pNeedReboot);
        const Int32 DRIVER_PACKAGE_REPAIR = 0x00000001;
        const Int32 DRIVER_PACKAGE_SILENT = 0x00000002;
        const Int32 DRIVER_PACKAGE_FORCE = 0x00000004;
        const Int32 DRIVER_PACKAGE_ONLY_IF_DEVICE_PRESENT = 0x00000008;
        const Int32 DRIVER_PACKAGE_LEGACY_MODE = 0x00000010;
        const Int32 DRIVER_PACKAGE_DELETE_FILES = 0x00000020;
        private static void pnpremove(string hwid)
        {
            Trace.WriteLine("remove " + hwid);
            string infpath = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\..\\inf";
            Trace.WriteLine("inf dir = " + infpath);
            string[] oemlist = Directory.GetFiles(infpath, "oem*.inf");
            Trace.WriteLine(oemlist.ToString());
            foreach (string oemfile in oemlist)
            {
                Trace.WriteLine("Checking " + oemfile);
                string contents = File.ReadAllText(oemfile);
                if (contents.Contains(hwid))
                {
                    bool needreboot;
                    Trace.WriteLine("Uninstalling");
                    DriverPackageUninstall(oemfile, DRIVER_PACKAGE_SILENT | DRIVER_PACKAGE_FORCE | DRIVER_PACKAGE_DELETE_FILES, IntPtr.Zero, out needreboot);
                    Trace.WriteLine("Uninstalled");
                }
            }
        }

        public static void LockCD() {}



        public class RestorePoint : IDisposable {
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct RestorePointInfo
            {
                public int dwEventType; 	// The type of event
                public int dwRestorePtType; 	// The type of restore point
                public Int64 llSequenceNumber; 	// The sequence number of the restore point
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
                public string szDescription; 	// The description to be displayed so 
                //the user can easily identify a restore point
            } 
            [StructLayout(LayoutKind.Sequential)]
            public struct STATEMGRSTATUS
            {
                public int nStatus; 		// The status code
                public Int64 llSequenceNumber; 	// The sequence number of the restore point
            }
            public enum RestoreType : int
            {
                ApplicationInstall = 0, 	// Installing a new application
                ApplicationUninstall = 1, 	// An application has been uninstalled
                ModifySettings = 12, 		// An application has had features added or removed
                CancelledOperation = 13, 	// An application needs to delete 
                // the restore point it created
                Restore = 6, 			// System Restore
                Checkpoint = 7, 		// Checkpoint
                DeviceDriverInstall = 10, 	// Device driver has been installed
                FirstRun = 11, 		// Program used for 1<sup>st</sup> time 
                BackupRecovery = 14 		// Restoring a backup
            }
            public const Int16 BeginSystemChange = 100;
            public const Int16 EndSystemChange = 101;
            [DllImport("srclient.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SRSetRestorePointW (ref RestorePointInfo pRestorePtSpec, out STATEMGRSTATUS pSMgrStatus);
            string description;
            bool set = false;
            bool autoterminate = false;
            Int64 restoreseq;
            public RestorePoint(string description, bool autoterminate) {
                this.description = description;
                this.autoterminate = autoterminate;
                STATEMGRSTATUS status = new STATEMGRSTATUS();
                RestorePointInfo rpi = new RestorePointInfo();
                rpi.dwEventType = BeginSystemChange;
                rpi.dwRestorePtType = (int) RestoreType.Checkpoint;
                rpi.llSequenceNumber = 0;
                rpi.szDescription = description;
                this.set = SRSetRestorePointW(ref rpi, out status);
                this.restoreseq = status.llSequenceNumber;
            }

            void IDisposable.Dispose() {
                if (this.set && this.autoterminate)
                {
                    STATEMGRSTATUS status = new STATEMGRSTATUS();
                    RestorePointInfo rpi = new RestorePointInfo();
                    rpi.dwEventType = EndSystemChange;
                    rpi.dwRestorePtType = (int)RestoreType.Checkpoint;
                    rpi.llSequenceNumber = this.restoreseq;
                    rpi.szDescription = description;
                    this.set = SRSetRestorePointW(ref rpi, out status);
                }
            }
        }

        static string extractToTemp(string name, string extension)
        {
            try
            {
                Assembly assembly;
                assembly = Assembly.GetExecutingAssembly();
                byte[] buffer = new byte[16 * 1024];
                string destname;
                using (Stream stream = assembly.GetManifestResourceStream(name))
                {

                    do
                    {
                        destname = Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Path.GetRandomFileName(), "." + extension));
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
                return destname;
            }
            catch
            {
                throw new Exception("Unable to extract " + name);
            }
        }

        public static void StoreNetworkSettings() {
            
            string netsettingsexe;
            if (is64BitOS())
            {
                netsettingsexe = "Xenprep._64.qnetsettings.exe";
            }
            else
            {
                netsettingsexe = "Xenprep._32.qnetsettings.exe";
            }

            string deststring = extractToTemp(netsettingsexe, "exe");

            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.Arguments = "/log /save";
                start.FileName = deststring;
                start.WindowStyle = ProcessWindowStyle.Hidden;
                start.CreateNoWindow = true;
                using (Process proc = Process.Start(start))
                {
                    proc.WaitForExit();
                }
            }
            catch
            {
                throw new Exception("Unable to store network settings");
            }
            finally
            {
                File.Delete(deststring);
            }

        }

        public static void RemovePVDriversFromFilters()
        {
            RegistryKey baseRK = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class", true);

            foreach (string subKeyName in baseRK.GetSubKeyNames())
            {
                using (RegistryKey tmpRK = baseRK.OpenSubKey(subKeyName, true))
                {
                    foreach (string filters in new string[] {"LowerFilters", "UpperFilters"})
                    {
                        string[] values = (string[]) tmpRK.GetValue(filters);

                        if (values != null)
                        {
                            /*
                             * LINQ expression
                             * Gets all entries of "values" that
                             * are not "xenfilt" or "scsifilt"
                             */
                            values = values.Where(
                                val => !(val.Equals("xenfilt", StringComparison.OrdinalIgnoreCase) ||
                                         val.Equals("scsifilt", StringComparison.OrdinalIgnoreCase))
                            ).ToArray();

                            tmpRK.SetValue(filters, values, RegistryValueKind.MultiString);
                        }
                    }
                }
            }
        }

        public static void DontBootStartPVDrivers()
        {
            string[] xenDrivers = {"XENBUS", "xenfilt", "xeniface", "xenlite",
                                   "xennet", "XenSvc", "xenvbd", "xenvif"};

            RegistryKey baseRK = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services", true);

            foreach (string driver in xenDrivers)
            {
                if (baseRK.GetValue(driver) != null)
                {
                    using (RegistryKey tmpRK = baseRK.OpenSubKey(driver, true))
                    {
                        tmpRK.SetValue("Start", 3);
                    }
                }
            }

            if (baseRK.GetValue(@"xenfilt\Unplug") != null)
            {
                using (RegistryKey tmpRK = baseRK.OpenSubKey(@"xenfilt\Unplug", true))
                {
                    tmpRK.DeleteValue("DISKS", false);
                    tmpRK.DeleteValue("NICS", false);
                }
            }
        }

        public static void UninstallMSIs() {}
        public static void UninstallXenLegacy() {}        
        public static void CleanUpPVDrivers()
        {
            string[] PVDrivers = {"xen", "xenbus", "xencrsh", "xenfilt",
                                   "xeniface", "xennet", "xenvbd", "xenvif"};

            string[] hwIDs = {
                @"PCI\VEN_5853&DEV_C000&SUBSYS_C0005853&REV_01",
                @"PCI\VEN_5853&DEV_0002",
                @"PCI\VEN_5853&DEV_0001",
                @"XENBUS\VEN_XSC000&DEV_IFACE&REV_00000001",
                @"XENBUS\VEN_XS0001&DEV_IFACE&REV_00000001",
                @"XENBUS\VEN_XS0002&DEV_IFACE&REV_00000001",
                @"XENBUS\VEN_XSC000&DEV_VBD&REV_00000001",
                @"XENBUS\VEN_XS0001&DEV_VBD&REV_00000001",
                @"XENBUS\VEN_XS0002&DEV_VBD&REV_00000001",
                @"XENVIF\VEN_XSC000&DEV_NET&REV_00000000",
                @"XENVIF\VEN_XS0001&DEV_NET&REV_00000000",
                @"XENVIF\VEN_XS0002&DEV_NET&REV_00000000",
                @"XENBUS\VEN_XSC000&DEV_VIF&REV_00000001",
                @"XENBUS\VEN_XS0001&DEV_VIF&REV_00000001",
                @"XENBUS\VEN_XS0002&DEV_VIF&REV_00000001"
            };

            string driverPath = Environment.GetFolderPath(Environment.SpecialFolder.System) + @"\drivers\";

            // Remove drivers from DriverStore
            foreach (string hwID in hwIDs)
            {
                pnpremove(hwID);
            }

            // Delete driver files
            foreach (string driver in PVDrivers)
            {
                File.Delete(driverPath + driver + ".sys");
            }
        }

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
                destname = extractToTemp(msitouse,"msi");
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
