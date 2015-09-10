using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;

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

        public static void EjectCDs()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType == DriveType.CDRom)
                {
                    // Strip the ":\" part of the drive's name
                    string tmp = drive.Name.Substring(0, drive.Name.Length - 2);

                    if (!CDTray.Eject(tmp))
                    {
                        throw new Exception(
                            String.Format("Failed to eject drive '{0}'", drive.Name)
                        );
                    }
                }
            }
        }

        public static void LockCDs()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType == DriveType.CDRom)
                {
                    // Strip the ":\" part of the drive's name
                    string tmp = drive.Name.Substring(0, drive.Name.Length - 2);

                    CDTray.Lock(tmp);
                }
            }
        }

        public class RestorePoint
        {
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
            public static extern bool SRSetRestorePointW(ref RestorePointInfo pRestorePtSpec, out STATEMGRSTATUS pSMgrStatus);
            string description;
            bool set = false;
            Int64 restoreseq;
            public RestorePoint(string description)
            {
                this.description = description;
                STATEMGRSTATUS status = new STATEMGRSTATUS();
                RestorePointInfo rpi = new RestorePointInfo();
                rpi.dwEventType = BeginSystemChange;
                rpi.dwRestorePtType = (int)RestoreType.Checkpoint;
                rpi.llSequenceNumber = 0;
                rpi.szDescription = description;
                try
                {
                    this.set = SRSetRestorePointW(ref rpi, out status);
                }
                catch
                {
                    this.set = false;
                    Trace.WriteLine("System Restore Point Not Set (System restore points don't exist on sever class versions of Windows)");
                }
                this.restoreseq = status.llSequenceNumber;
            }

            public void End()
            {
                if (this.set)
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
            if (WinVersion.is64BitOS())
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
                                   "xennet", "XenSvc", "xenvbd", "xenvif", "xennet6", 
                                   "xenutil", "xevtchn"};

            RegistryKey baseRK = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services", true);

            foreach (string driver in xenDrivers)
            {
                using (RegistryKey tmpRK = baseRK.OpenSubKey(driver, true))
                {
                    if (tmpRK != null)
                    {
                        tmpRK.SetValue("Start", 3);
                    }
                }
            }

            using (RegistryKey tmpRK = baseRK.OpenSubKey(@"xenfilt\Unplug", true))
            {
                if (tmpRK != null)
                {
                    tmpRK.DeleteValue("DISKS", false);
                    tmpRK.DeleteValue("NICS", false);
                }
            }
        }

        [DllImport("msi.dll", SetLastError = true)]
        static extern int MsiEnumProducts(int iProductIndex, StringBuilder lpProductBuf);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        static extern Int32 MsiGetProductInfo(
            string product,
            string property,
            [Out] StringBuilder valueBuf,
            ref Int32 len);

        public enum INSTALLSTATE : int
        {
            NOTUSED      = -7,  // component disabled
            BADCONFIG    = -6,  // configuration data corrupt
            INCOMPLETE   = -5,  // installation suspended or in progress
            SOURCEABSENT = -4,  // run from source, source is unavailable
            MOREDATA     = -3,  // return buffer overflow
            INVALIDARG   = -2,  // invalid function argument
            UNKNOWN      = -1,  // unrecognized product or feature
            BROKEN       =  0,  // broken
            ADVERTISED   =  1,  // advertised feature
            ABSENT       =  2,  // uninstalled (or action state absent but clients remain)
            LOCAL        =  3,  // installed on local drive
            SOURCE       =  4,  // run from source, CD or net
            DEFAULT      =  5,  // use default, local or source
        }

        public enum INSTALLLEVEL : int
        {
            DEFAULT = 0x0000,
            MINIMUM = 0x0001,
            MAXIMUM = 0xFFFF
        }

        public const string INSTALLPROPERTY_INSTALLEDPRODUCTNAME = "InstalledProductName";
        public const string INSTALLPROPERTY_VERSIONSTRING = "VersionString";
        public const string INSTALLPROPERTY_HELPLINK = "HelpLink";
        public const string INSTALLPROPERTY_HELPTELEPHONE = "HelpTelephone";
        public const string INSTALLPROPERTY_INSTALLLOCATION = "InstallLocation";
        public const string INSTALLPROPERTY_INSTALLSOURCE = "InstallSource";
        public const string INSTALLPROPERTY_INSTALLDATE = "InstallDate";
        public const string INSTALLPROPERTY_PUBLISHER = "Publisher";
        public const string INSTALLPROPERTY_LOCALPACKAGE = "LocalPackage";
        public const string INSTALLPROPERTY_URLINFOABOUT = "URLInfoAbout";
        public const string INSTALLPROPERTY_URLUPDATEINFO = "URLUpdateInfo";
        public const string INSTALLPROPERTY_VERSIONMINOR = "VersionMinor";
        public const string INSTALLPROPERTY_VERSIONMAJOR = "VersionMajor";

        [DllImport("msi.dll", SetLastError = true, CharSet=CharSet.Unicode)]
        static extern uint MsiConfigureProductEx(
            string szProduct,
            int iInstallLevel,
            INSTALLSTATE eInstallState,
            string szCommandLine);

        public static void UninstallMSIs()
        {
            const int GUID_LEN = 39;
            const int BUF_LEN = 128;
            int err;
            int i = 0;
            int len;
            StringBuilder productCode = new StringBuilder(GUID_LEN, GUID_LEN);
            StringBuilder productName = new StringBuilder(BUF_LEN, BUF_LEN);
            Hashtable toRemove = new Hashtable();

            // MSIs to uninstall
            string[] msiNameList = {
                "Citrix XenServer Windows Guest Agent",
                "Citrix XenServer VSS Provider",
                "Citrix Xen Windows x64 PV Drivers",
                "Citrix Xen Windows x86 PV Drivers",
                "Citrix XenServer Tools Installer"
            };

            while ((err = MsiEnumProducts(i, productCode)) == 0) // ERROR_SUCCESS
            {
                string tmpCode = productCode.ToString();

                len = BUF_LEN;

                // Get ProductName from Product GUID
                err = MsiGetProductInfo(
                    tmpCode,
                    INSTALLPROPERTY_INSTALLEDPRODUCTNAME,
                    productName,
                    ref len
                );

                if (err == 0)
                {
                    string tmpName = productName.ToString();

                    if (msiNameList.Contains(tmpName))
                    {
                        toRemove.Add(tmpCode, tmpName);
                    }
                }

                ++i;
            }

            foreach (DictionaryEntry product in toRemove)
            {
                Process process = new Process();
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = "msiexec.exe";

                // For some unknown reason, XenServer Tools Installer
                // doesn't like the '/norestart' option and doesn't get
                // removed if it's there.
                startInfo.Arguments = "/x " + product.Key + " /qn" +
                    (product.Value.Equals(msiNameList[4]) ? "" : " /norestart");
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
                process.Close();
            }
        }

        public class HardwareDevice
        {
            [Flags]
            public enum DiGetClassFlags : uint
            {
                DIGCF_DEFAULT = 0x00000001,  // only valid with DIGCF_DEVICEINTERFACE
                DIGCF_PRESENT = 0x00000002,
                DIGCF_ALLCLASSES = 0x00000004,
                DIGCF_PROFILE = 0x00000008,
                DIGCF_DEVICEINTERFACE = 0x00000010,
            }
            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
            static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid,
                                                     [MarshalAs(UnmanagedType.LPTStr)] string Enumerator,
                                                     IntPtr hwndParent,
                                                     uint Flags
                                                    );
            [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
            static extern IntPtr SetupDiGetClassDevs(IntPtr ClassGuid,
                                                     IntPtr Enumerator,
                                                     IntPtr hwndParent,
                                                     uint Flags
                                                    );
            [DllImport("setupapi.dll", SetLastError = true)]
            static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, 
                                                     uint MemberIndex, 
                                                     ref SP_DEVINFO_DATA DeviceInfoData);
            [StructLayout(LayoutKind.Sequential)]
            struct SP_DEVINFO_DATA
            {
                public uint cbSize;
                public Guid classGuid;
                public uint devInst;
                public IntPtr reserved;
            }
            [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            static extern bool SetupDiGetDeviceRegistryProperty(
                IntPtr deviceInfoSet,
                ref SP_DEVINFO_DATA deviceInfoData,
                uint property,
                out UInt32 propertyRegDataType,
                ushort[] propertyBuffer,
                uint propertyBufferSize,
                out UInt32 requiredSize
                );
            [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
            static extern bool SetupDiGetDeviceRegistryProperty(
                IntPtr deviceInfoSet,
                ref SP_DEVINFO_DATA deviceInfoData,
                SetupDiGetDeviceRegistryPropertyEnum property,
                IntPtr propertyRegDataType,
                ushort[] propertyBuffer,
                uint propertyBufferSize,
                IntPtr requiredSize
                );
            enum SetupDiGetDeviceRegistryPropertyEnum : uint
            {
                SPDRP_DEVICEDESC = 0x00000000, // DeviceDesc (R/W)
                SPDRP_HARDWAREID = 0x00000001, // HardwareID (R/W)
                SPDRP_COMPATIBLEIDS = 0x00000002, // CompatibleIDs (R/W)
                SPDRP_UNUSED0 = 0x00000003, // unused
                SPDRP_SERVICE = 0x00000004, // Service (R/W)
                SPDRP_UNUSED1 = 0x00000005, // unused
                SPDRP_UNUSED2 = 0x00000006, // unused
                SPDRP_CLASS = 0x00000007, // Class (R--tied to ClassGUID)
                SPDRP_CLASSGUID = 0x00000008, // ClassGUID (R/W)
                SPDRP_DRIVER = 0x00000009, // Driver (R/W)
                SPDRP_CONFIGFLAGS = 0x0000000A, // ConfigFlags (R/W)
                SPDRP_MFG = 0x0000000B, // Mfg (R/W)
                SPDRP_FRIENDLYNAME = 0x0000000C, // FriendlyName (R/W)
                SPDRP_LOCATION_INFORMATION = 0x0000000D, // LocationInformation (R/W)
                SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000E, // PhysicalDeviceObjectName (R)
                SPDRP_CAPABILITIES = 0x0000000F, // Capabilities (R)
                SPDRP_UI_NUMBER = 0x00000010, // UiNumber (R)
                SPDRP_UPPERFILTERS = 0x00000011, // UpperFilters (R/W)
                SPDRP_LOWERFILTERS = 0x00000012, // LowerFilters (R/W)
                SPDRP_BUSTYPEGUID = 0x00000013, // BusTypeGUID (R)
                SPDRP_LEGACYBUSTYPE = 0x00000014, // LegacyBusType (R)
                SPDRP_BUSNUMBER = 0x00000015, // BusNumber (R)
                SPDRP_ENUMERATOR_NAME = 0x00000016, // Enumerator Name (R)
                SPDRP_SECURITY = 0x00000017, // Security (R/W, binary form)
                SPDRP_SECURITY_SDS = 0x00000018, // Security (W, SDS form)
                SPDRP_DEVTYPE = 0x00000019, // Device Type (R/W)
                SPDRP_EXCLUSIVE = 0x0000001A, // Device is exclusive-access (R/W)
                SPDRP_CHARACTERISTICS = 0x0000001B, // Device Characteristics (R/W)
                SPDRP_ADDRESS = 0x0000001C, // Device Address (R)
                SPDRP_UI_NUMBER_DESC_FORMAT = 0X0000001D, // UiNumberDescFormat (R/W)
                SPDRP_DEVICE_POWER_DATA = 0x0000001E, // Device Power Data (R)
                SPDRP_REMOVAL_POLICY = 0x0000001F, // Removal Policy (R)
                SPDRP_REMOVAL_POLICY_HW_DEFAULT = 0x00000020, // Hardware Removal Policy (R)
                SPDRP_REMOVAL_POLICY_OVERRIDE = 0x00000021, // Removal Policy Override (RW)
                SPDRP_INSTALL_STATE = 0x00000022, // Device Install State (R)
                SPDRP_LOCATION_PATHS = 0x00000023, // Device Location Paths (R)
                SPDRP_BASE_CONTAINERID = 0x00000024  // Base ContainerID (R)
            }
            [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
            static extern bool SetupDiSetClassInstallParams(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref IntPtr ClassInstallParams, int ClassInstallParamsSize);
            [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
            static extern bool SetupDiSetClassInstallParams(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref REMOVE_PARAMS ClassInstallParams, int ClassInstallParamsSize);

            static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
            [StructLayout(LayoutKind.Sequential)]
            struct REMOVE_PARAMS
            {
                public uint cbSize;
                public uint InstallFunction;
                public uint Scope;
                public uint HwProfile;
            }
            public const uint DIF_REMOVE = 5;
            public const uint DI_REMOVE_DEVICE_GLOBAL = 1;
            [DllImport("setupapi.dll", SetLastError = true)]
            static extern bool SetupDiCallClassInstaller(
                 UInt32 InstallFunction,
                 IntPtr DeviceInfoSet,
                 ref SP_DEVINFO_DATA DeviceInfoData
            );
            public static void Remove(string HardwareId) 
            {
                IntPtr DeviceInfoSet = SetupDiGetClassDevs(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)DiGetClassFlags.DIGCF_ALLCLASSES);
                if (DeviceInfoSet == INVALID_HANDLE_VALUE) {
                    return;
                }
                uint index = 0;
                SP_DEVINFO_DATA DeviceInfoData = new SP_DEVINFO_DATA();
                const uint BUFFER_SIZE = 4096;
                ushort[] buffer = new ushort[BUFFER_SIZE];
                DeviceInfoData.cbSize = (uint) Marshal.SizeOf(DeviceInfoData);
                while (SetupDiEnumDeviceInfo(DeviceInfoSet, index, ref DeviceInfoData))
                {
                    index++;
                    SetupDiGetDeviceRegistryProperty(DeviceInfoSet,
                                                     ref DeviceInfoData,
                                                     SetupDiGetDeviceRegistryPropertyEnum.SPDRP_HARDWAREID,
                                                     IntPtr.Zero,
                                                     buffer,
                                                     BUFFER_SIZE*2,
                                                     IntPtr.Zero);
                    int start = 0;
                    int offset = 0;
                    while (start < buffer.Length)
                    {
                        while ((offset < buffer.Length) && (buffer[offset] != 0))
                        {
                            offset++;
                        }

                        if (offset < buffer.Length)
                        {
                            if (start == offset)
                                break;
                            byte[] block = new byte[(offset - start + 1) * 2];
                            Buffer.BlockCopy(buffer, (int)(start * 2), block, 0, (int)((offset - start + 1) * 2));
                            string id = System.Text.Encoding.Unicode.GetString(block, 0, (offset - start) * 2);
                            Trace.WriteLine("Examinining id " + id.ToUpper() +" vs "+HardwareId.ToUpper());
                            if (id.ToUpper().Equals(HardwareId.ToUpper()))
                            {
                                Trace.WriteLine("Trying to remove "+HardwareId.ToUpper());
                                REMOVE_PARAMS rparams = new REMOVE_PARAMS();
                                rparams.cbSize = 8; // Size of cbSide & InstallFunction
                                rparams.InstallFunction = DIF_REMOVE;
                                rparams.HwProfile = 0;
                                rparams.Scope = DI_REMOVE_DEVICE_GLOBAL;
                                GCHandle handle1 = GCHandle.Alloc(rparams);

                                if (!SetupDiSetClassInstallParams(DeviceInfoSet, ref DeviceInfoData, ref rparams, Marshal.SizeOf(rparams)))
                                {
                                    throw new Exception("Unable to set class install params");
                                }
                                if (!SetupDiCallClassInstaller(DIF_REMOVE, DeviceInfoSet, ref DeviceInfoData))
                                {
                                    throw new Exception("Unable to call class installer");
                                }
                                Trace.WriteLine("Remove should have worked");
                            }
                        }
                        else
                        {
                            break;
                        }
                        offset++;
                        start = offset;
                    }
                }
            }
        }

        static void HardUninstallFromReg(string key)
        {
            string installdir = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\"+key, "Install_Dir", "");
            if (!((installdir == null) || (installdir == "")))
            {
                try
                {
                    Directory.Delete(installdir, true);
                }
                catch
                {
                }
            }
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(key);
            }
            catch
            { }
        }

        public static void UninstallXenLegacy() {
            try
            {
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\", true).DeleteSubKeyTree("Citrix XenTools");

            }
            catch { }
            try
            {
                HardUninstallFromReg(@"SOFTWARE\Citrix\XenTools");
            }
            catch { }

            if (WinVersion.is64BitOS())
            {
                try
                {
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\", true).DeleteSubKeyTree("Citrix XenTools");
                }
                catch { }
                try 
                {
                    HardUninstallFromReg(@"SOFTWARE\Wow6432Node\Citrix\XenTools");
                }
                catch { }
            }
            try
            {
                HardwareDevice.Remove(@"root\xenevtchn");
            }
            catch(Exception e)
            {
                Trace.WriteLine("Remove exception : " + e.ToString());
            }

        }
        public static void CleanUpPVDrivers()
        {
            string[] PVDrivers = {"xen", "xenbus", "xencrsh", "xenfilt",
                                   "xeniface", "xennet", "xenvbd", "xenvif",
                                 "xennet6", "xenutil", "xevtchn"};

            string[] services = {"XENBUS", "xenfilt", "xeniface", "xenlite",
                                 "xennet", "XenSvc", "xenvbd", "xenvif", "xennet6",
                                 "xenutil", "xevtchn"};

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
                @"XENBUS\VEN_XS0002&DEV_VIF&REV_00000001",
                @"root\xenevtchn",
                @"XENBUS\CLASS&VIF",
                @"PCI\VEN_fffd&DEV_0101",
                @"PCI\VEN_5853&DEV_0001",
                @"PCI\VEN_5853&DEV_0001&SUBSYS_00015853",
                @"XEN\VIF",
                @"XENBUS\CLASS&IFACE",
            };

            string driverPath = Environment.GetFolderPath(Environment.SpecialFolder.System) + @"\drivers\";

            // Remove drivers from DriverStore
            foreach (string hwID in hwIDs)
            {
                pnpremove(hwID);
            }

            // Delete services' registry entries
            using (RegistryKey baseRK = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services", true))
            {
                foreach (string service in services)
                {
                    try
                    {
                        baseRK.DeleteSubKeyTree(service);
                    }
                    catch { }
                }
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




 

        public static void InstallGuestAgent() {
            
            
            string destname;
            try
            {
                string msitouse;
                if (WinVersion.is64BitOS())
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

        public static void UnlockCDs()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();

            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType == DriveType.CDRom)
                {
                    // Strip the ":\" part of the drive's name
                    string tmp = drive.Name.Substring(0, drive.Name.Length - 2);

                    CDTray.Unlock(tmp);
                }
            }
        }

        public class WinVersion
        {
            [DllImport("kernel32")]
            private static extern bool GetVersionEx(ref OSVERSIONINFO osvi);

            struct OSVERSIONINFO
            {
                public uint dwOSVersionInfoSize;
                public uint dwMajorVersion;
                public uint dwMinorVersion;
                public uint dwBuildNumber;
                public uint dwPlatformId;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
                public string szCSDVersion;
                public Int16 wServicePackMajor;
                public Int16 wServicePackMinor;
                public Int16 wSuiteMask;
                public Byte wProductType;
                public Byte wReserved;
            }
            OSVERSIONINFO osvi;
            public enum ProductType : uint
            {
                NT_WORKSTATION = 1,
                NT_DOMAIN_CONTROLLER = 2,
                NT_SERVER = 3
            }
            public WinVersion()
            {
                osvi = new OSVERSIONINFO();
                osvi.dwOSVersionInfoSize = (uint)Marshal.SizeOf(typeof(OSVERSIONINFO));

                GetVersionEx(ref osvi);
            }
            public uint GetPlatformId() { return osvi.dwPlatformId; }
            public uint GetServicePackMajor() { return (uint)osvi.wServicePackMajor; }
            public uint GetServicePackMinor() { return (uint)osvi.wServicePackMinor; }
            public uint GetSuite() { return (uint)osvi.wSuiteMask; }
            public uint GetProductType() { return osvi.wProductType; }
            public uint GetVersionValue()
            {
                uint vers = ((osvi.dwMajorVersion << 8) | osvi.dwMinorVersion);
                return vers;
            }
            public static bool isServerSKU()
            {
                WinVersion vers = new WinVersion();
                return (ProductType)vers.GetProductType() != ProductType.NT_WORKSTATION;
            }

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

        }

        class Privileges
        {
            public const UInt32 SE_PRIVILEGE_ENABLED_BY_DEFAULT = 0x00000001;
            public const UInt32 SE_PRIVILEGE_REMOVED = 0x00000004;
            public const UInt32 SE_PRIVILEGE_USED_FOR_ACCESS = 0x80000000;

            [StructLayout(LayoutKind.Sequential)]
            public struct LUID
            {
                public uint LowPart;
                public int HighPart;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct LUID_AND_ATTRIBUTES
            {
                public LUID Luid;
                public UInt32 Attributes;
            }

            private const Int32 ANYSIZE_ARRAY = 1;
            public struct TOKEN_PRIVILEGES
            {
                public UInt32 PrivilegeCount;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = ANYSIZE_ARRAY)]
                public LUID_AND_ATTRIBUTES[] Privileges;
            }

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool LookupPrivilegeValue(IntPtr Null1, string lpName,
                out LUID lpLuid);
            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool OpenProcessToken(IntPtr ProcessHandle,
                UInt32 DesiredAccess, out IntPtr TokenHandle);
            //Use these for DesiredAccess
            public const UInt32 STANDARD_RIGHTS_REQUIRED = 0x000F0000;
            public const UInt32 STANDARD_RIGHTS_READ = 0x00020000;
            public const UInt32 TOKEN_ASSIGN_PRIMARY = 0x0001;
            public const UInt32 TOKEN_DUPLICATE = 0x0002;
            public const UInt32 TOKEN_IMPERSONATE = 0x0004;
            public const UInt32 TOKEN_QUERY_SOURCE = 0x0010;
            public const UInt32 TOKEN_ADJUST_GROUPS = 0x0040;
            public const UInt32 TOKEN_ADJUST_DEFAULT = 0x0080;
            public const UInt32 TOKEN_ADJUST_SESSIONID = 0x0100;
            public const UInt32 TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);
            public const UInt32 TOKEN_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY |
                TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE |
                TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT |
                TOKEN_ADJUST_SESSIONID);
            private const UInt32 TOKEN_QUERY = 0x0008;
            private const UInt32 TOKEN_ADJUST_PRIVILEGES = 0x0020;
            private const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;
            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool AdjustTokenPrivileges(IntPtr TokenHandle,
                [MarshalAs(UnmanagedType.Bool)]bool DisableAllPrivileges,
                ref TOKEN_PRIVILEGES NewState,
                UInt32 Zero,
                IntPtr Null1,
                IntPtr Null2);

            public static void AcquireSystemPrivilege(string name)
            {
                TOKEN_PRIVILEGES tkp;
                IntPtr token;

                tkp.Privileges = new LUID_AND_ATTRIBUTES[1];
                LookupPrivilegeValue(IntPtr.Zero, name, out tkp.Privileges[0].Luid);
                tkp.PrivilegeCount = 1;
                tkp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle,
                    TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
                {
                    throw new Exception("OpenProcessToken");
                }
                if (!AdjustTokenPrivileges(token, false, ref tkp, 0, IntPtr.Zero,
                    IntPtr.Zero))
                {
                    throw new Exception("AdjustTokenPrivileges"); ;
                }
            }
        }

        class Shutdown
        {
            [Flags]
            public enum ExitFlags : int
            {
                EWX_LOGOFF = 0x00000000,
                EWX_POWEROFF = 0x00000008,
                EWX_REBOOT = 0x00000002,
                EWX_RESTARTAPPS = 0x00000040,
                EWX_SHUTDOWN = 0x00000001,
                EWX_FORCE = 0x00000004,
                EWX_FORCEIFHUNG = 0x00000010
            }

            [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
            internal static extern bool ExitWindowsEx(ExitFlags flg, int rea);

            [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool InitiateSystemShutdownEx(
                string lpMachineName,
                string lpMessage,
                uint dwTimeout,
                bool bForceAppsClosed,
                bool bRebootAfterShutdown,
                ShutdownReason dwReason);

            [Flags]
            public enum ShutdownReason : uint
            {
                // Microsoft major reasons.
                SHTDN_REASON_MAJOR_OTHER = 0x00000000,
                SHTDN_REASON_MAJOR_NONE = 0x00000000,
                SHTDN_REASON_MAJOR_HARDWARE = 0x00010000,
                SHTDN_REASON_MAJOR_OPERATINGSYSTEM = 0x00020000,
                SHTDN_REASON_MAJOR_SOFTWARE = 0x00030000,
                SHTDN_REASON_MAJOR_APPLICATION = 0x00040000,
                SHTDN_REASON_MAJOR_SYSTEM = 0x00050000,
                SHTDN_REASON_MAJOR_POWER = 0x00060000,
                SHTDN_REASON_MAJOR_LEGACY_API = 0x00070000,

                // Microsoft minor reasons.
                SHTDN_REASON_MINOR_OTHER = 0x00000000,
                SHTDN_REASON_MINOR_NONE = 0x000000ff,
                SHTDN_REASON_MINOR_MAINTENANCE = 0x00000001,
                SHTDN_REASON_MINOR_INSTALLATION = 0x00000002,
                SHTDN_REASON_MINOR_UPGRADE = 0x00000003,
                SHTDN_REASON_MINOR_RECONFIG = 0x00000004,
                SHTDN_REASON_MINOR_HUNG = 0x00000005,
                SHTDN_REASON_MINOR_UNSTABLE = 0x00000006,
                SHTDN_REASON_MINOR_DISK = 0x00000007,
                SHTDN_REASON_MINOR_PROCESSOR = 0x00000008,
                SHTDN_REASON_MINOR_NETWORKCARD = 0x00000000,
                SHTDN_REASON_MINOR_POWER_SUPPLY = 0x0000000a,
                SHTDN_REASON_MINOR_CORDUNPLUGGED = 0x0000000b,
                SHTDN_REASON_MINOR_ENVIRONMENT = 0x0000000c,
                SHTDN_REASON_MINOR_HARDWARE_DRIVER = 0x0000000d,
                SHTDN_REASON_MINOR_OTHERDRIVER = 0x0000000e,
                SHTDN_REASON_MINOR_BLUESCREEN = 0x0000000F,
                SHTDN_REASON_MINOR_SERVICEPACK = 0x00000010,
                SHTDN_REASON_MINOR_HOTFIX = 0x00000011,
                SHTDN_REASON_MINOR_SECURITYFIX = 0x00000012,
                SHTDN_REASON_MINOR_SECURITY = 0x00000013,
                SHTDN_REASON_MINOR_NETWORK_CONNECTIVITY = 0x00000014,
                SHTDN_REASON_MINOR_WMI = 0x00000015,
                SHTDN_REASON_MINOR_SERVICEPACK_UNINSTALL = 0x00000016,
                SHTDN_REASON_MINOR_HOTFIX_UNINSTALL = 0x00000017,
                SHTDN_REASON_MINOR_SECURITYFIX_UNINSTALL = 0x00000018,
                SHTDN_REASON_MINOR_MMC = 0x00000019,
                SHTDN_REASON_MINOR_TERMSRV = 0x00000020,

                // Flags that end up in the event log code.
                SHTDN_REASON_FLAG_USER_DEFINED = 0x40000000,
                SHTDN_REASON_FLAG_PLANNED = 0x80000000,
                SHTDN_REASON_UNKNOWN = SHTDN_REASON_MINOR_NONE,
                SHTDN_REASON_LEGACY_API = (SHTDN_REASON_MAJOR_LEGACY_API | SHTDN_REASON_FLAG_PLANNED),

                // This mask cuts out UI flags.
                SHTDN_REASON_VALID_BIT_MASK = 0xc0ffffff
            }
            public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
            public static void ShutDown()
            {
                Privileges.AcquireSystemPrivilege(SE_SHUTDOWN_NAME);
                WinVersion vers = new WinVersion();
                if (vers.GetVersionValue() >= 0x500 &&
                    vers.GetVersionValue() < 0x600)
                {
                    bool res = ExitWindowsEx(ExitFlags.EWX_SHUTDOWN |
                                             ExitFlags.EWX_FORCE, 0);
                    if (res != true)
                    {
                        throw new Exception("ExitWindowsEx Failed");
                    }
                }
                else
                {
                    bool res = InitiateSystemShutdownEx("", "", 0, true, false,
                        ShutdownReason.SHTDN_REASON_MAJOR_OTHER |
                        ShutdownReason.SHTDN_REASON_MINOR_ENVIRONMENT |
                        ShutdownReason.SHTDN_REASON_FLAG_PLANNED);
                    if (res != true)
                    {
                        throw new Exception("InitiateSystemShutdownEx Failed");
                    }
                }
            }
        }

        public static void ShutDownVm()
        {
            try
            {
                Shutdown.ShutDown();
            }
            catch
            {
                throw new Exception("Shutdown failed.  Shut down your VM for preparation to continue");
            }
        }  
    }

    /// <summary>This class is dedicated to locking and unlocking the CD ejection.
    /// Here we are importing COM classes relating to DeviceIOControl and using it
    /// to set Media removal to false.
    /// NOTE: At time of writing there are complications implementing this class</summary>
    public static class CDTray
    {
        public static bool Lock(string driveLetter)
        {
            return ManageLock(driveLetter, true);
        }

        public static bool Unlock(string driveLetter)
        {
            return ManageLock(driveLetter, false);
        }

        public static bool Eject(string driveLetter)
        {
            bool result = false;

            string fileName = string.Format(@"\\.\{0}:", driveLetter);

            IntPtr deviceHandle = _CreateFile(fileName);

            if (deviceHandle != INVALID_HANDLE_VALUE)
            {
                IntPtr null_ptr = IntPtr.Zero;
                int bytesReturned;
                NativeOverlapped overlapped = new NativeOverlapped();

                result = DeviceIoControl(
                    deviceHandle,
                    IOCTL_STORAGE_EJECT_MEDIA,
                    ref null_ptr,
                    0,
                    out null_ptr,
                    0,
                    out bytesReturned,
                    ref overlapped);
                CloseHandle(deviceHandle);
            }
            return result;
        }

        private static IntPtr _CreateFile(string fileName)
        {
            SECURITY_ATTRIBUTES securityAttributes = new SECURITY_ATTRIBUTES();

            return CreateFile(
                fileName,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                ref securityAttributes,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);
        }

        private static bool ManageLock(string driveLetter, bool lockDrive)
        {
            bool result = false;
            string fileName = string.Format(@"\\.\{0}:", driveLetter);

            IntPtr deviceHandle = _CreateFile(fileName);

            if (deviceHandle != INVALID_HANDLE_VALUE)
            {
                IntPtr outBuffer;
                int bytesReturned;
                NativeOverlapped overlapped = new NativeOverlapped();

                byte[] bytes = new byte[1] {lockDrive ? (byte) 1 : (byte) 0};

                IntPtr unmanagedPointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanagedPointer, bytes.Length);

                // Call unmanaged code
                result = DeviceIoControl(
                    deviceHandle,
                    IOCTL_STORAGE_MEDIA_REMOVAL,
                    ref unmanagedPointer,
                    1,
                    out outBuffer,
                    0,
                    out bytesReturned,
                    ref overlapped);
                CloseHandle(deviceHandle);

                Marshal.FreeHGlobal(unmanagedPointer);
            }
            return result;
        }

        // http://msdn.microsoft.com/en-us/library/aa379560(VS.85).aspx
        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            int nLength;
            IntPtr lpSecurityDescriptor;
            bool bInheritHandle;
        }

        private const int FILE_SHARE_READ = 0x00000001;
        private const int FILE_SHARE_WRITE = 0x00000002;
        private const uint GENERIC_READ = 0x80000000;
        private const int OPEN_EXISTING = 3;
        private const int FILE_ATTRIBUTE_NORMAL = 0x80;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        private const int IOCTL_STORAGE_MEDIA_REMOVAL = 0x2D4804;
        private const int IOCTL_STORAGE_EJECTION_CONTROL = 0x2D0940;
        private const int IOCTL_STORAGE_EJECT_MEDIA = 0x2D4808;

        // http://msdn.microsoft.com/en-us/library/aa363858(VS.85).aspx
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            int dwShareMode,
            ref SECURITY_ATTRIBUTES lpSecurityAttributes,
            int dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        // http://msdn.microsoft.com/en-us/library/aa363216(VS.85).aspx
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            int dwIoControlCode,
            ref IntPtr lpInBuffer,
            int nInBufferSize,
            out IntPtr lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            ref NativeOverlapped lpOverlapped);

        // http://msdn.microsoft.com/en-us/library/ms724211(VS.85).aspx
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(
            IntPtr hObject);

    }
}
