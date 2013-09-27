/* Copyright (c) Citrix Systems Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met:
 *
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer.
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using System.Net.NetworkInformation;
using System.Security.AccessControl;
using System.IO;
using System.Runtime.InteropServices;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Security.Cryptography.X509Certificates;

namespace InstallWizard
{
    public class MsiInstaller
    {

        private enum WINDOWS_MESSAGE_CODES
        {
            ERROR_SUCCESS = 0,
            ERROR_INVALID_PARAMETER = 87,
            ERROR_NO_MORE_ITEMS = 259,
            ERROR_INSTALL_USEREXIT = 1602,
            ERROR_INSTALL_FAILURE = 1603,
            ERROR_BAD_CONFIGURATION = 1610,
            ERROR_INSTALL_IN_PROGRESS = 1618,
            ERROR_INSTALL_SOURCE_ABSENT = 1612,
            ERROR_UNKNOWN_PRODUCT = 1605,
            ERROR_FUNCTION_FAILED = 1627,
            ERROR_INVALID_HANDLE_STATE = 1609,
            ERROR_MORE_DATA = 234,
            ERROR_UNKNOWN_PROPERTY = 1608,
            ERROR_CREATE_FAILED = 1631,
            ERROR_OPEN_FAILED = 110,
            ERROR_BAD_QUERY_SYNTAX = 1615
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
        public enum INSTALLLOGATTRIBUTES // flag attributes for MsiEnableLog
        {
            INSTALLLOGATTRIBUTES_APPEND = (1 << 0),
            INSTALLLOGATTRIBUTES_FLUSHEACHLINE = (1 << 1),
        }

        [DllImport("msi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern UInt32 MsiEnableLog(INSTALLLOGMODE dwLogMode, string szLogFile, INSTALLLOGATTRIBUTES dwLogAttributes);
        [DllImport("msi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern UInt32 MsiInstallProduct([MarshalAs(UnmanagedType.LPTStr)]string packagePath, [MarshalAs(UnmanagedType.LPTStr)]string commandLine);

        delegate int InstallUIHandler(IntPtr context, INSTALLMESSAGE iMessageType, [MarshalAs(UnmanagedType.LPTStr)] string szMessage);
        [DllImport("msi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern InstallUIHandler MsiSetExternalUI(InstallUIHandler puiHandler, INSTALLLOGMODE dwMessageFilter, UIntPtr context);
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

        public enum INSTALLLEVEL {
            DEFAULT = 0,
        }

        public enum INSTALLSTATE
        {
            ABSENT = 2,
        }

        string ProductGuid;
        string UpgradeGuid;
        string pathname;

        [DllImport("msi.dll", CharSet = CharSet.Auto)]
        static extern uint MsiConfigureProduct(string szProduct, INSTALLLEVEL InstallLevel, INSTALLSTATE eInstallState);

        [DllImport("msi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern UInt32 MsiEnumRelatedProducts(string strUpgradeCode, int reserved, int iIndex, StringBuilder sbProductCode);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        static extern Int32 MsiGetProductInfo(string product, string property, [Out] StringBuilder valueBuf, ref Int32 len); 

        static uint MSIOPENPACKAGEFLAGS_IGNOREMACHINESTATE = 0x00000001;
        [DllImport("msi.dll", SetLastError = false, CharSet = CharSet.Auto)]
        static extern int MsiOpenPackageEx(string pathname, uint flags, ref IntPtr handle);

        [DllImport("msi.dll", SetLastError=true)]
        static extern int MsiGetProductProperty(IntPtr hProduct, string szProperty, StringBuilder lpValueBuf, ref int pcchValueBuf);

        [DllImport("msi.dll", ExactSpelling = true)]
        static extern uint MsiCloseHandle(IntPtr hAny);

        [DllImport("msi.dll", SetLastError = false, CharSet = CharSet.Auto)]
        static extern int MsiGetFileVersion(string szFilePath, System.Text.StringBuilder lpVersionBuf, ref int pcchVersionBuf, System.Text.StringBuilder lpLangBuf, ref int pcchLangBuf);
        public MsiInstaller(string pathname)
        {
            Trace.WriteLine("MSI PACKAGE: " + pathname);
            int size = 1024;
            StringBuilder version = new StringBuilder(size, size);
            int gsize = 1024;
            int nsize = 1024;
            StringBuilder guid = new StringBuilder(gsize, gsize);
            StringBuilder upgradecodeguid = new StringBuilder(nsize,nsize);
            IntPtr producthandle = new IntPtr() ;
            int err;
            if ((err = MsiOpenPackageEx(pathname, MSIOPENPACKAGEFLAGS_IGNOREMACHINESTATE, ref producthandle)) != 0)
            {
                Trace.WriteLine("Opening MSI package failed : " + err.ToString());
                throw new Exception("Opening MSI package failed : " + err.ToString());
            }
            MsiGetProductProperty(producthandle, "ProductVersion", version, ref size);
            MsiGetProductProperty(producthandle, "ProductCode", guid, ref gsize);
            MsiGetProductProperty(producthandle, "UpgradeCode", upgradecodeguid, ref nsize);
            MsiCloseHandle(producthandle);

            //int ret  = MsiGetFileVersion(pathname, version, ref size, lang, ref langsize);
            Match match = Regex.Match(version.ToString(), @"(?<maj>\d+)\.(?<min>\d+)\.(?<build>\d+)");
            try
            {
                
                major = int.Parse(match.Groups["maj"].Value);
                minor = int.Parse(match.Groups["min"].Value);
                build = int.Parse(match.Groups["build"].Value);
            }
            catch (Exception)
            {
                Trace.WriteLine("Version string " + version.ToString());
                Trace.WriteLine("Major "+ match.Groups["maj"].Value+" Minor "+ match.Groups["min"].Value+" Build "+match.Groups["build"].Value);
                throw;
            }
            this.ProductGuid = guid.ToString();
            this.UpgradeGuid = upgradecodeguid.ToString();
            this.pathname = pathname;
            log = new List<string>();
            
        }
        public bool installsuccess = false;
        int total = 0;
        bool forwards = true;
        int pos = 0;
        int oldpos = 0;
        int actionmove = 0;
        int starttotal = 0;

        void SetProgress()
        {
            if (pos > total)
            {
                total = pos;
            }
            oldpos = pos;
        }
       



        void parseProgress(string prog)
        {
            MatchCollection numbers = (new Regex(@"(?<index>\d): (?<value>\d+) ")).Matches(prog);
            int[] val = new int[4];
            if (numbers.Count == 0)
                return;
            foreach (Match match in numbers)
            {
                int idx = int.Parse(match.Groups["index"].Value) - 1;
                int value = int.Parse(match.Groups["value"].Value);
                val[idx] = value;

            }


            switch (val[0])
            {
                case 0:
                    pos = total;
                    starttotal = total;
                    total += val[1];
                    forwards = (val[2] == 0);
                    if (!forwards)
                    {
                        pos += val[1];
                        oldpos = 0;
                    }
                    break;
                case 1:
                    if (val[2] == 1)
                        actionmove = val[1];
                    break;

                case 2:
                    if (total == 0)
                        break;
                    if (forwards)
                        pos += val[1];
                    else
                        pos -= val[1];

                    break;
                case 3:
                    starttotal = total;
                    total += val[1];
                    break;
            }


        }
        int uiHandler(IntPtr context, INSTALLMESSAGE message, string msgstr)
        {
            try
            {
                switch (message)
                {
                    case INSTALLMESSAGE.INSTALLMESSAGE_ACTIONDATA:
                        log.Add(message.ToString() + " : " + msgstr);
                        if (forwards)
                            pos += actionmove;
                        else
                            pos -= actionmove;
                        SetProgress();
                        break;
                    case INSTALLMESSAGE.INSTALLMESSAGE_PROGRESS:
                        log.Add(message.ToString() + " : " + msgstr);
                        parseProgress(msgstr);
                        SetProgress();
                        break;
                    default:
                        log.Add(message.ToString() + " : " + msgstr);
                        break;
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine("UI Handler exception " + e.ToString());
            }
            return 0;
        }

        List<string> log;

        public void repair()
        {
            try
            {
                Trace.WriteLine("Attempting repair " + this.pathname + " " + this.ProductGuid);
                MsiInstallProduct(this.pathname, "REINSTALL=ALL REINSTALLMODE=omus REBOOT=ReallySuppress");
                Trace.WriteLine("Finished repair " + this.pathname + " " + this.ProductGuid);
            }
            catch
            {
                Trace.WriteLine("Attempted repair failed");
            }
        }
        public void uninstall()
        {
            try
            {
                StringBuilder sbbuf = new StringBuilder(39);

                int index = 0;
                while (MsiEnumRelatedProducts(UpgradeGuid, index, 0, sbbuf) == (uint)WINDOWS_MESSAGE_CODES.ERROR_SUCCESS)
                {
                    try
                    {
                        Trace.WriteLine("Found UpgradeCode " + UpgradeGuid);

                        Trace.WriteLine("Attempting uninstall " + this.pathname + " " + sbbuf.ToString());
                        MsiConfigureProduct(sbbuf.ToString(), INSTALLLEVEL.DEFAULT, INSTALLSTATE.ABSENT);
                        Trace.WriteLine("Finished uninstall " + this.pathname + " " + sbbuf.ToString());
                    }
                    catch {
                        Trace.WriteLine("Uninstall " + this.pathname + " " + sbbuf.ToString() + " failed");
                    }
                }
            }
            catch
            {
                Trace.WriteLine("Attempted uninstall failed");
            }
        }
        public static void EnsureMsiMutexAvailable(TimeSpan sp) {
            const string installerServiceMutexName = "Global\\_MSIExecute";

            try
            {
                Mutex MSIExecuteMutex = Mutex.OpenExisting(installerServiceMutexName);
                bool waitSuccess = MSIExecuteMutex.WaitOne(sp, false);

                if (!waitSuccess)
                {
                    Trace.WriteLine("Unable to obtain MSI mutex");
                    try
                    {
                        ServiceController sc = new ServiceController("msiserver");
                        if (sc.CanStop)
                        {
                            sc.Stop();
                        }
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(e.ToString());
                    }
                }
                else
                {
                    MSIExecuteMutex.ReleaseMutex();
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Trace.WriteLine("Unable to obtain MSI mutex - doesn't exist");
                // Mutex doesn't exist, do nothing
            }
        }
        public void install(string args, string logfile, InstallerState installstate)
        { 
            uint err=0;
            log.Clear();
            IntPtr a = IntPtr.Zero;
            MsiSetInternalUI(INSTALLUILEVEL.INSTALLUILEVEL_FULL, ref a);
            //MsiSetExternalUI(uiHandler, INSTALLLOGMODE.INSTALLLOGMODE_PROGRESS | INSTALLLOGMODE.INSTALLLOGMODE_ACTIONDATA, UIntPtr.Zero);
            Trace.WriteLine("Installing " + this.pathname);

            bool retry;
            int retrycount=0;
            do
            {
                retry = false;
                MsiEnableLog(INSTALLLOGMODE.INSTALLLOGMODE_VERBOSE, Application.CommonAppDataPath + "\\" + logfile, INSTALLLOGATTRIBUTES.INSTALLLOGATTRIBUTES_FLUSHEACHLINE);
                try
                {
                    // Wait to be allowed to install.  If any of this fails, give up, and try the install anyway

                    

                    EnsureMsiMutexAvailable(new TimeSpan(0,1,0));

                    err = MsiInstallProduct(this.pathname, "REBOOT=ReallySuppress ALLUSERS=1  ARPSYSTEMCOMPONENT=0" + " " + args);
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Unable to handle " + e.ToString());
                    throw;
                }



                switch (err)
                {
                    case 0:
                        break;
                    case 1603:
                        Trace.WriteLine("Detected another installer running, sleeping");
                        retry = true;
                        retrycount += 10;
                        Thread.Sleep(1000 * 10);
                        break;
                    case 1618:
                        Trace.WriteLine("Detected another installer running, sleeping");
                        retry = true;
                        retrycount += 10;
                        Thread.Sleep(1000 * 10);
                        break;
                    case 3010:
                        break;
                    case 3011:
                        break;

                    default:
                        Trace.WriteLine("MSI Install failed " + err.ToString());
                        throw new InstallerException("Installation of\n" + this.pathname + "\nfailed with error code " + err.ToString());
                }

                if (retry && retrycount > 60)
                {
                    throw new InstallerException("Installation failed, too many retries, last error :" + err.ToString());
                }

            } while (retry);
            try
            {
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + ProductGuid, true).DeleteValue("SystemComponent");
            }
            catch
            {
                Trace.WriteLine("Unable to delete SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\SystemComponent" + ProductGuid);
            }


            return;

        }

        public bool installed()
        {
            Trace.WriteLine("Looking for UpgradeCode " + UpgradeGuid);

            StringBuilder sbbuf = new StringBuilder(39);

            if (MsiEnumRelatedProducts(UpgradeGuid, 0, 0, sbbuf) == (uint)WINDOWS_MESSAGE_CODES.ERROR_SUCCESS)
            {
                Trace.WriteLine("Found UpgradeCode " + UpgradeGuid);
                return true;
            }

            return false;
        }

        public bool olderinstalled()
        {
            try
            {

                StringBuilder sbbuf = new StringBuilder(39);
                int vssize = 39;
                StringBuilder versionstring = new StringBuilder(vssize);
                int index = 0;
                while (MsiEnumRelatedProducts(UpgradeGuid, index, 0, sbbuf) == (uint)WINDOWS_MESSAGE_CODES.ERROR_SUCCESS)
                {
                    Trace.WriteLine("Found UpgradeCode " + UpgradeGuid);
                    index++;
                    if (MsiGetProductInfo(sbbuf.ToString(), "VersionString" , versionstring, ref vssize) == (uint)WINDOWS_MESSAGE_CODES.ERROR_SUCCESS)
                    {
                        Trace.WriteLine("Found.  MSI Installed version " + versionstring.ToString());

                        string installedversion = versionstring.ToString();
                        Match match = Regex.Match(installedversion.ToString(), @"(?<maj>\d+)\.(?<min>\d+)\.(?<build>\d+)");
                        int instmajor = int.Parse(match.Groups["maj"].Value);
                        int instminor = int.Parse(match.Groups["min"].Value);
                        int instbuild = int.Parse(match.Groups["build"].Value);

                        if (instmajor > major)
                            return false;
                        if (instmajor < major)
                            return true;
                        if (instminor > minor)
                            return false;
                        if (instminor < minor)
                            return true;
                        if (instbuild > build)
                            return false;
                        if (instbuild < build)
                            return true;
                        return false;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        int major;
        int minor;
        int build;
    }

    public class HwidState
    {
        string requiredhwid;
        public HwidState(string required)
        {
            requiredhwid = required;
        }
        public bool update()
        {
            ManagementObject session = XenIface.GetSession();
            long starttime = DateTime.Now.ToFileTime();
            while (true)
            {
                try
                {
                    string version;
                    if ((version = XenIface.Read(session, "attr/PVAddons/MajorVersion")) != "")
                    {
                        Trace.WriteLine("Guest agent major version is " + version);
                        break;
                    }
                }
                catch (Exception e) {
                    if ((DateTime.Now.ToFileTime() - starttime) > ((long)300 * 10000000))
                    {
                        Trace.WriteLine(e.ToString());
                    }
                }
                if ((DateTime.Now.ToFileTime() - starttime) > ((long)300 * 10000000))
                {
                    Trace.WriteLine("Unable to read guest agent version");
                    return false;
                }
                Trace.WriteLine("Waiting for Legacy Guest Agent");
                Thread.Sleep(5000);
               
            }
            XenIface.Write(session, "data/device_id", "0002");
            XenIface.Write(session, "data/updated", DateTime.Now.ToFileTime().ToString());

            session.InvokeMethod("EndSession", null, null);
            return true;
        }
        bool find(string required)
        {

            ManagementClass mc = new ManagementClass("Win32_PnpEntity");

            ManagementObjectCollection entities = mc.GetInstances();
            foreach (ManagementObject entity in entities)
            {

                object idobj = entity["CompatibleId"];
                if (idobj != null)
                {
                    String[] ids = (String[])idobj;
                    foreach (String id in ids)
                    {
                        if (id.Equals(required))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool needsupdate()
        {
            if (!find(requiredhwid))
                return true;
            return false;
        }

    }

    public class VifConfig
    {
        public bool Copied = false;
        const string SERVICES_KEY = "HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\Services\\";

        void copyregkey(RegistryKey src, RegistryKey dest)
        {
            RegistrySecurity srcac = src.GetAccessControl();
            RegistrySecurity destac = new RegistrySecurity();
            string descriptor = srcac.GetSecurityDescriptorSddlForm(AccessControlSections.Access);
            destac.SetSecurityDescriptorSddlForm(descriptor);
            dest.SetAccessControl(destac);

            string[] valuenames = src.GetValueNames();
            foreach (string valuename in valuenames)
            {
                Trace.WriteLine("Copy " + src.Name + " " + valuename + " : " + dest.Name);
                dest.SetValue(valuename, src.GetValue(valuename));
            }
            string[] subkeynames = src.GetSubKeyNames();
            foreach (string subkeyname in subkeynames)
            {
                Trace.WriteLine("DeepCopy " + src.Name + " " + subkeyname + " : " + dest.Name);
                copyregkey(src.OpenSubKey(subkeyname), dest.CreateSubKey(subkeyname));
            }

        }


        void copynic_toservice(string uuid, string device, uint luidindex, uint iftype)
        {
            RegistryKey serviceskey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Services");
            RegistryKey nics = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Citrix\\XenToolsNetSettings");
            RegistryKey devicekey = nics.CreateSubKey(device);
            RegistryKey nsikey = Registry.LocalMachine.OpenSubKey("System\\CurrentControlSet\\Control\\Nsi");
            copyregkey(serviceskey.OpenSubKey("NETBT\\Parameters\\Interfaces\\Tcpip_" + uuid), devicekey.CreateSubKey("nbt"));
            copyregkey(serviceskey.OpenSubKey("Tcpip\\Parameters\\Interfaces\\" + uuid), devicekey.CreateSubKey("tcpip"));
            copyregkey(serviceskey.OpenSubKey("Tcpip6\\Parameters\\Interfaces\\" + uuid), devicekey.CreateSubKey("tcpip6"));

            copyipv6address(nsikey.OpenSubKey("{eb004a01-9b1a-11d4-9123-0050047759bc}\\10"), luidindex, iftype, devicekey);
           
        }

        void copyipv6address(RegistryKey source, uint luidindex, uint iftype, RegistryKey dest)
        {
            if (source == null)
            {
                Trace.WriteLine("No IPv6 Config found");
                return;
            }
            //Construct a NET_LUID & convert to a hex string
            ulong prefixval = (((ulong)iftype) << 48) | (((ulong)luidindex) << 24);
            // Fix endianness to match registry entry & convert to string
            byte[] prefixbytes = BitConverter.GetBytes(prefixval);
            Array.Reverse(prefixbytes);
            string prefixstr = BitConverter.ToInt64(prefixbytes,0).ToString("x16");

            Trace.WriteLine("Looking for prefix "+prefixstr);
            string[] keys = source.GetValueNames();
            foreach (string key in keys) {
                Trace.WriteLine("Testing "+key);
                if (key.StartsWith(prefixstr)) {
                    Trace.WriteLine("Found "+key);

                    //Replace prefix with IPv6_Address____ before saving
                    string newstring="IPv6_Address____"+key.Substring(16);
                    Trace.WriteLine("Writing to " + dest.ToString()+" "+newstring);
                    dest.SetValue(newstring, source.GetValue(key));
                }
            }
            Trace.WriteLine("Copying addresses with prefix "+prefixstr+" done");
        }

        void copynic_byuuid(uuiddevice uuid)
        {
 
            copynic_toservice(uuid.uuid, uuid.device, uuid.luidindex, uuid.iftype);

            Copied = true;

        }

        class uuiddevice
        {
            public string uuid;
            public string device;
            public uint luidindex;
            public uint iftype;
        }
        List<uuiddevice> get_uuids(string service)
        {
            List<uuiddevice> uuids = new List<uuiddevice>();
            Trace.WriteLine("Get uuids for " + SERVICES_KEY + service + "\\enum" + " Count");
            object count = Registry.GetValue(SERVICES_KEY + service +"\\enum", "Count", 0);

            if (count != null)
            {
                Trace.WriteLine("We have " + count.ToString() + " nics to manage");
                int nr_emul_cards = (int)count;
                int x;
                for (x = 0; x < nr_emul_cards; x++)
                {
                    Trace.WriteLine("Nic " + x.ToString());
                    try
                    {
                        string pci_device_id = (string)Registry.GetValue(SERVICES_KEY + service + "\\enum", x.ToString(), "");
                        string driver_id = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\Enum\\" + pci_device_id, "driver", null);
                        string[] linkage = (string[])Registry.GetValue("HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\Control\\Class\\" + driver_id + "\\Linkage", "RootDevice", null);
                        uint luidindex = (UInt32)((Int32)Registry.GetValue("HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\Control\\Class\\" + driver_id, "NetLuidIndex", null));
                        uint iftype = (UInt32)((Int32)Registry.GetValue("HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\Control\\Class\\" + driver_id, "*IfType", null));
                        string uuid = linkage[0];
                        uuids.Add(new uuiddevice() { uuid = uuid, device = pci_device_id, luidindex = luidindex, iftype = iftype });
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine("Unable to find card " + x.ToString() + " : " + e.ToString());
                    }
                }
            }
            else
            {
                Trace.WriteLine("No entries found");
            }
            return uuids;
        }

        void copynics_byservice(string service, string sourcedevice="", string destinationdevice="")
        {
            List<uuiddevice> uuids = get_uuids(service);
            foreach (uuiddevice uuid in uuids) {
                if (!(destinationdevice.Equals("") && sourcedevice.Equals(""))) {
                    uuid.device = uuid.device.Replace(sourcedevice,destinationdevice);
                }
                copynic_byuuid(uuid);
            }
        }

        public void CopyNewPVWorkaround()
        {
            copynics_byservice("xennet", "XENVIF\\DEVICE&REV_02", "XEN\\VIF");
        }

        public void CopyPV()
        {
            copynics_byservice("xennet");
            copynics_byservice("xennet6");
        }

    }

    public class InstallerState
    {
        [Flags]
        public enum States
        {
            GotDrivers = 1 << 00, // I have determined the right drivers are installed and functioning
            DriversPlaced = 1 << 01, // I have put drivers in place so that they will install after reboot(s)
            GotOldDrivers = 1 << 02, // I have determined that an out of date (MSI) set of drivers are installed
            DriversUpdating = 1 << 03, // I have attempted to update drivers with a more recent version.  This will complete following a reboot
            NsisFree = 1 << 04, // I have determined that an old NSIS-installer of drivers and guest agent is not installed
            NsisUninstalling = 1 << 05, // I have initiated the uninstall of an old NSIS-installed - it will be finished post-reboot
            HWIDCorrect = 1 << 06, // The HWID is the one the new drivers expect
            GotAgent = 1 << 07, // I have determined that the correct MSI installed guest agent is present
            RebootNow = 1 << 08, // When I'm no longer able to make any state transitions, I should reboot
            NeedShutdown = 1 << 09, // When I'm ready to reboot, I should shutdown and await a restart (not reboot)
            Polling = 1 << 10, // I should wait for a few minutes - if no state transitions occur, I should reboot
            PollTimedOut = 1 << 11, // We have been in the polling state for too long
            RebootReady = 1 << 12, // I am ready to reboot.  If in active mode, the user has agreed for their machine to shut down
            PreReboot = 1 << 13, // State in which to carry out actions before we offer a shutdown / reboot
            Passive = 1 << 14, // We are in passive mode - no user interaction is required
            NoShutdown = 1 << 15, // We should not shutdown when running in passive mode
            UserRebootTriggered = 1 << 16, // A user has agreed we should reboot / shutdown (active mode only)
            Done = 1 << 17, // We have been told to exit the service
            PauseOnStart = 1<< 18, // We should pause the service thread before starting
            RebootDesired = 1<<19, // We wish to reboot, but need a user trigger
            Installed = 1<<20, // We believe everything to be installed and working perfectly.
            Cancelled = 1 << 21, // The User has cancelled the installation.  Service should be set to manual restart.
            Failed = 1<< 22, // The install has failed.  Service should be set to manual restart
            Rebooting = 1<<23, // We are now running the final apps before rebooting, or rebooting
            AutoShutdown = 1<< 24, // We want all future shutdowns to happen without user intervention
            GotVssProvider = 1 << 25, // I have determined that the correct MSI installed vss provider is present
            OneFinalReboot = 1 << 26, // We have been restored after one extra reboot has been scheduled.  Don't schedule another reboot.
            NeedsReboot = 1 << 27, // We will have to reboot before we have finished, but should continue to poll for now
        }
        public bool Unchanged = true;
        
        [Flags]
        public enum ExitFlags : int {
            EWX_LOGOFF =      0x00000000,
            EWX_POWEROFF =    0x00000008,
            EWX_REBOOT =      0x00000002,
            EWX_RESTARTAPPS = 0x00000040,
            EWX_SHUTDOWN =    0x00000001,
            EWX_FORCE =       0x00000004,
            EWX_FORCEIFHUNG = 0x00000010
        }
        
        [DllImport("user32.dll", ExactSpelling=true, SetLastError=true) ]
        internal static extern bool ExitWindowsEx( ExitFlags flg, int rea );

        [DllImport("advapi32.dll", CharSet=CharSet.Auto, SetLastError=true)]
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
            SHTDN_REASON_MAJOR_OTHER        = 0x00000000,
            SHTDN_REASON_MAJOR_NONE         = 0x00000000,
            SHTDN_REASON_MAJOR_HARDWARE         = 0x00010000,
            SHTDN_REASON_MAJOR_OPERATINGSYSTEM      = 0x00020000,
            SHTDN_REASON_MAJOR_SOFTWARE         = 0x00030000,
            SHTDN_REASON_MAJOR_APPLICATION      = 0x00040000,
            SHTDN_REASON_MAJOR_SYSTEM           = 0x00050000,
            SHTDN_REASON_MAJOR_POWER        = 0x00060000,
            SHTDN_REASON_MAJOR_LEGACY_API       = 0x00070000,
        
            // Microsoft minor reasons.
            SHTDN_REASON_MINOR_OTHER        = 0x00000000,
            SHTDN_REASON_MINOR_NONE         = 0x000000ff,
            SHTDN_REASON_MINOR_MAINTENANCE      = 0x00000001,
            SHTDN_REASON_MINOR_INSTALLATION     = 0x00000002,
            SHTDN_REASON_MINOR_UPGRADE          = 0x00000003,
            SHTDN_REASON_MINOR_RECONFIG         = 0x00000004,
            SHTDN_REASON_MINOR_HUNG         = 0x00000005,
            SHTDN_REASON_MINOR_UNSTABLE         = 0x00000006,
            SHTDN_REASON_MINOR_DISK         = 0x00000007,
            SHTDN_REASON_MINOR_PROCESSOR        = 0x00000008,
            SHTDN_REASON_MINOR_NETWORKCARD      = 0x00000000,
            SHTDN_REASON_MINOR_POWER_SUPPLY     = 0x0000000a,
            SHTDN_REASON_MINOR_CORDUNPLUGGED    = 0x0000000b,
            SHTDN_REASON_MINOR_ENVIRONMENT      = 0x0000000c,
            SHTDN_REASON_MINOR_HARDWARE_DRIVER      = 0x0000000d,
            SHTDN_REASON_MINOR_OTHERDRIVER      = 0x0000000e,
            SHTDN_REASON_MINOR_BLUESCREEN       = 0x0000000F,
            SHTDN_REASON_MINOR_SERVICEPACK      = 0x00000010,
            SHTDN_REASON_MINOR_HOTFIX           = 0x00000011,
            SHTDN_REASON_MINOR_SECURITYFIX      = 0x00000012,
            SHTDN_REASON_MINOR_SECURITY         = 0x00000013,
            SHTDN_REASON_MINOR_NETWORK_CONNECTIVITY = 0x00000014,
            SHTDN_REASON_MINOR_WMI          = 0x00000015,
            SHTDN_REASON_MINOR_SERVICEPACK_UNINSTALL = 0x00000016,
            SHTDN_REASON_MINOR_HOTFIX_UNINSTALL     = 0x00000017,
            SHTDN_REASON_MINOR_SECURITYFIX_UNINSTALL = 0x00000018,
            SHTDN_REASON_MINOR_MMC          = 0x00000019,
            SHTDN_REASON_MINOR_TERMSRV          = 0x00000020,
        
            // Flags that end up in the event log code.
            SHTDN_REASON_FLAG_USER_DEFINED      = 0x40000000,
            SHTDN_REASON_FLAG_PLANNED           = 0x80000000,
            SHTDN_REASON_UNKNOWN            = SHTDN_REASON_MINOR_NONE,
            SHTDN_REASON_LEGACY_API         = (SHTDN_REASON_MAJOR_LEGACY_API | SHTDN_REASON_FLAG_PLANNED),
        
            // This mask cuts out UI flags.
            SHTDN_REASON_VALID_BIT_MASK         = 0xc0ffffff
        }

        public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

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
        public class WinVersion
        {
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
        }

        public string failreason = "";

        public void Fail(string reason)
        {
            failreason = reason;
            Failed = true;
        }

        public static void winReboot() {
            AcquireSystemPrivilege(SE_SHUTDOWN_NAME);
            WinVersion vers = new WinVersion();
            if (vers.GetVersionValue() >= 0x500 &&
                vers.GetVersionValue() < 0x600) {
                bool res = ExitWindowsEx(ExitFlags.EWX_REBOOT|
                                         ExitFlags.EWX_FORCE,0);
            }
            else {
                bool res = InitiateSystemShutdownEx("","", 0, true, true, 
                    ShutdownReason.SHTDN_REASON_MAJOR_OTHER |
                    ShutdownReason.SHTDN_REASON_MINOR_ENVIRONMENT |
                    ShutdownReason.SHTDN_REASON_FLAG_PLANNED);
            }
        }

        
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
        
        public void DoShutdown()
        {
             Reboot();
        }

        ManualResetEvent shutdownevent = new ManualResetEvent(false);
        ManualResetEvent stateevent = new ManualResetEvent(false);
        ManualResetEvent doneevent = new ManualResetEvent(false);
        public bool WaitForDone()
        {

            if (this.Passive)
            {
                return true;
            }
            Trace.WriteLine("Waiting to be told we are done");
            int res = WaitHandle.WaitAny(new WaitHandle[] { doneevent, stateevent });

            if (res == 0)
            {
                Trace.WriteLine("got done");
                doneevent.Reset();
                return true;
            }
            Trace.WriteLine("got state event " + res.ToString());
            return false;
        }


        public bool WaitForShutdown() {

            Trace.WriteLine("Wait for shutdown : passive = " + Passive.ToString() + " noshutdown = " + NoShutdown.ToString() + " autoshutdown = " + AutoShutdown.ToString());
            if ((this.Passive && !this.NoShutdown ) || this.AutoShutdown) {
                Registry.SetValue(regpath, "AutoReboot", 1);
                this.RebootReady = true;
                return true;
            }
            Trace.WriteLine("Waiting to be told to reboot");
            int res = WaitHandle.WaitAny(new WaitHandle[]{shutdownevent, stateevent});
            Registry.SetValue(regpath, "AutoReboot", 1);
            if (res == 0)
            {
                shutdownevent.Reset();
                return true;
            }
            return false;
        }
        int longpolltimeout = 5 * 60 * 1000;
        int shortpolltimeout = 10 * 1000;
        int polltimeout = 0;
        int pollcount = 0;
        object polllock = new object();
        public void PollingReset() {
            lock(polllock) {
                Trace.WriteLine("Reset polling");
                // Start counting again from the beginning
                pollcount = 0;
                // If the timeout is non-zero then
                // a) we have already started polling
                // b) after we started polling, something has caused us to reset
                // since this implies 'something is happening' reset to a short timeout
                if (polltimeout != 0)
                {
                    polltimeout = shortpolltimeout;
                }
            }
        }

        public void Sleep(int ms)
        {
            lock (polllock) {
                if (polltimeout == 0)
                {
                    Trace.WriteLine("Setting new poll timeout");
                    polltimeout = (int)Application.UserAppDataRegistry.GetValue("PollingTimeout", shortpolltimeout);
                }
                else
                {
                    Trace.WriteLine("Using existing timeout");
                }
                if (Polling && (pollcount > polltimeout))
                {
                    // If we have timed out, we want to ensure we use longer timoouts by default
                    // on future reboots.
                    if (polltimeout != longpolltimeout)
                    {
                        Application.UserAppDataRegistry.SetValue("PollingTimeout", longpolltimeout);
                    }
                    PollTimedOut = true;
                    return;
                }
            }
            WaitHandle.WaitAny(new WaitHandle[]{shutdownevent, stateevent}, ms);
            lock(polllock) {
                pollcount += ms;
            }
        }

        private States currentstate = 0; // States.PauseOnStart; // (should be 0)
        public States state
        {
            get
            {
                return currentstate;
            }
            set
            {
                if (currentstate != value)
                {
                    currentstate = value;
                    Unchanged = false;
                }
            }
        }

        private void setstate(States flag, bool value)
        {
            state = (state & ~flag) | (value ? flag : 0);
        }
        private bool getstate(States flag)
        {
            return (flag == (state & flag));
        }


        public string DriverText = "";

        public string StateText { get {
            string text = "";

            if (GotDrivers)
            {
                text += "Drivers : Installed\n";
            }
            else
            {
                if (DriversPlaced || DriversUpdating || Polling)
                {
                    if (!(NsisUninstalling || DriversUpdating))
                    {
                        text += "Drivers : Initializing\n";
                        text += DriverText;
                    }
                    else
                    {
                        text += "Drivers : Updating\n";
                        text += DriverText;
                    }
                }
                else
                {
                    text += "Drivers : Installing\n";
                }
            }

            if (WinVersion.isServerSKU())
            {
                if (GotVssProvider)
                {
                    text += "Vss Provider : Installed\n";
                }
                else
                {
                    text += "Vss Provider : Installing\n";
                }
            }

            if (GotAgent)
            {
                text += "Guest Agent : Installed\n";
            }
            else
            {
                text += "Guest Agent : Installing\n";
            }

            return text;

        }}

        public bool GotDrivers { set { setstate(States.GotDrivers, value); } get { return getstate(States.GotDrivers); } }
        public bool DriversPlaced { set { setstate(States.DriversPlaced, value); } get { return getstate(States.DriversPlaced); } }
        public bool GotOldDrivers { set { setstate(States.GotOldDrivers, value); } get { return getstate(States.GotOldDrivers); } }
        public bool DriversUpdating { set { setstate(States.DriversUpdating, value); } get { return getstate(States.DriversUpdating); } }
        public bool NsisFree { set { setstate(States.NsisFree, value); } get { return getstate(States.NsisFree); } }
        public bool NsisUninstalling { set { setstate(States.NsisUninstalling, value); } get { return getstate(States.NsisUninstalling); } }
        public bool HWIDCorrect { set { setstate(States.HWIDCorrect, value); } get { return getstate(States.HWIDCorrect); } }
        public bool GotAgent { set { setstate(States.GotAgent, value); } get { return getstate(States.GotAgent); } }
        public bool RebootNow { set { setstate(States.RebootNow, value); } get { return getstate(States.RebootNow); } }
        public bool NeedsReboot { set { setstate(States.NeedsReboot, value); } get { return getstate(States.NeedsReboot); } }
        public bool Polling { set { setstate(States.Polling, value); } get { return getstate(States.Polling); } }
        public bool PollTimedOut { set { setstate(States.PollTimedOut, value); } get { return getstate(States.PollTimedOut); } }
        public bool RebootReady { set { setstate(States.RebootReady, value); } get { return getstate(States.RebootReady); } }
        public bool PreReboot { set { setstate(States.PreReboot, value); } get { return getstate(States.PreReboot); } }
        public bool Passive { set { setstate(States.Passive, value); } get { return getstate(States.Passive); } }
        public bool NoShutdown { set { setstate(States.NoShutdown, value); } get { return getstate(States.NoShutdown); } }
        public bool UserRebootTriggered { set { setstate(States.UserRebootTriggered, value); } get { return getstate(States.UserRebootTriggered); } }
        public bool Done { set { setstate(States.Done, value); } get { return getstate(States.Done); } }
        public bool PauseOnStart { set { setstate(States.PauseOnStart, value); } get { return getstate(States.PauseOnStart); } }
        public bool RebootDesired { set { setstate(States.RebootDesired, value); } get { return getstate(States.RebootDesired); } }
        public bool GotVssProvider { set { setstate(States.GotVssProvider, value); } get { return getstate(States.GotVssProvider); } }
        public bool OneFinalReboot { set { setstate(States.OneFinalReboot, value); } get { return getstate(States.OneFinalReboot); } }
        public bool Installed { 
            set {
                if (value == true)
                {
                    Registry.SetValue(regpath, "InstallStatus", "Installed");
                }
                setstate(States.Installed, value); 
            } 
            get { return getstate(States.Installed); } }
        public bool Cancelled { 
            set {
                if (value == true)
                {
                    Registry.SetValue(regpath, "InstallStatus", "Cancelled");
                }
                setstate(States.Cancelled, value); 
            } 
            get { return getstate(States.Cancelled); } }
        public bool Failed { 
            set {
                if (value == true)
                {
                    Registry.SetValue(regpath, "InstallStatus", "Failed");
                }
                setstate(States.Failed, value); 
            } 
            get { return getstate(States.Failed); } }
        public bool Rebooting { set { setstate(States.Rebooting, value); } get { return getstate(States.Rebooting); } }
        public bool AutoShutdown { set { setstate(States.AutoShutdown, value); } get { return getstate(States.AutoShutdown); } }
        //public bool  { set { setstate(States, value); } get { return getstate(States); } }


        public int Progress=0;
        public int MaxProgress=1;

        public void Alert(string message)
        {
            //FIXME Send an alert here...
        }
        public void Reboot()
        {
            RebootCount--;
            Registry.SetValue(regpath, "RebootCount", RebootCount);
            winReboot();
        }
        string regpath;
        public int RebootCount;
        const int MAXREBOOTS = 5;
        public bool NotRebooted = false;
        ManagementEventWatcher watcher;

   

        public InstallerState() {
            if (InstallService.is64BitOS() && (!InstallService.isWOW64()))
            {
                regpath = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Citrix\\XenToolsInstaller";
            }
            else {
                regpath = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenToolsInstaller";
            }
            Registry.SetValue(regpath, "InstallStatus", "Starting");
            try
            {
                RebootCount = (int)Registry.GetValue(regpath,"RebootCount" , MAXREBOOTS);
            }
            catch
            {
                RebootCount = MAXREBOOTS;
            }
            NotRebooted = false;
            if (RebootCount == MAXREBOOTS)
            {
                NotRebooted = true;
            }
            int uimode = 6;
            try
            {
                uimode = (int)Registry.GetValue(regpath, "Mode", 5);
            }
            catch (Exception e) {
                Trace.WriteLine("Reg value " + regpath + " Mode Not found : " + e.ToString());
            }
            
            if (uimode >= 5 || uimode == 0) {
                Trace.WriteLine("Active Install ("+uimode.ToString()+")");
                Passive = false;
                NoShutdown = true;
            }
            else {
                Trace.WriteLine("Passive Install");
                NoShutdown = false;
                Passive = true;
            }

            try
            {
                if ((int)Registry.GetValue(regpath, "NoReboot", 0) == 1)
                {
                    NoShutdown = true;
                    Registry.SetValue(regpath, "NoReboot", 0);
                }
            }
            catch { }

            try
            {
                AutoShutdown = false;
                if ((int)Registry.GetValue(regpath, "AutoReboot", 0) == 1)
                {
                    AutoShutdown = true;
                }
            }
            catch { }
            if (uimode > 2) {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run","CitrixXenAgentInstaller","\""+Application.StartupPath+"\\InstallGui.exe"+"\"");
                Trace.WriteLine("setting install agent run subkey");
            }

         

            try
            {
                Trace.WriteLine("Initializing pause registry control");
                int pause = (int)Application.CommonAppDataRegistry.GetValue("PauseOnStart", 0);
                if (pause != 0)
                {
                    PauseOnStart = true;
                }
        
                int onemorereboot = (int)Application.CommonAppDataRegistry.GetValue("OneFinalReboot", 0);
                if (onemorereboot != 0)
                {
                    OneFinalReboot = true;
                }

                Application.CommonAppDataRegistry.DeleteValue("Continue");
            }
            catch {}
            if (!Passive)
            {
                Thread watchthread = new Thread(WatcherStartThread);
                watchthread.Start();
            }
            Trace.WriteLine("state done");
        }

        void WatcherStartThread()
        {
            Trace.WriteLine("Try to create watcher");
            watcher = null;
            while (watcher == null)
            {
                try
                {

                    watcher = new ManagementEventWatcher(@"root\citrix\xenserver\agent", "SELECT * FROM CitrixXenServerInstallEvent");
                    watcher.EventArrived += new EventArrivedEventHandler(watcher_EventArrived);
                    watcher.Start();

                }
                catch (ManagementException)
                {
                    Thread.Sleep(500);
                    watcher = null;
                    Trace.WriteLine("Waiting for WMI ");
                    // We expect some management exceptions here, as we are waiting for the WMI objects to become available
                }
                catch (Exception e)
                {
                    Thread.Sleep(500);
                    Trace.WriteLine(e.ToString());
                    watcher = null;
                }
                
            }
            Trace.WriteLine("Initializing event query done");
        }

       
        void watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            Trace.WriteLine("I caught an event");
            ManagementBaseObject ev = e.NewEvent;
            Trace.WriteLine((string)ev["status"]);
            Trace.WriteLine("event done");
            switch (ev["status"].ToString())
            {
                case "UnPause":
                    PauseOnStart = false;
                    break;
                case "UserReboot":
                    SystemShutdown();
                    break;
                case "UserDone":
                    UserDone();
                    break;
                case "Cancel":
                    Cancel();
                    break;
            }

        }
        public void ServiceShutdown() {
            Trace.WriteLine("Moving into Done phase");
            Done = true;
            stateevent.Set();
        }
        public void SystemShutdown() {
            Trace.WriteLine("Moving into RebootReady phase");
            Rebooting = true;
            RebootReady = true;
            shutdownevent.Set();
        }
        public void UserDone()
        {
            Trace.WriteLine("User indicated installer is finished");
            Done = true;
            doneevent.Set();
            stateevent.Set();
        }
        public void Cancel()
        {
            Cancelled = true;
            Done = true;
            stateevent.Set();
        }

    }

    public class NSISItem
    {
        string name;
        public string path;
        public NSISItem(string name, string path)
        {
            this.name = name;
            this.path = path;
        }
        string getUninstallPath()
        {
            string res;
            if (InstallService.is64BitOS() && (!InstallService.isWOW64()))
            {
                res = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Citrix XenTools", "UninstallString", "");
            }
            else
            {
                res = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Citrix XenTools", "UninstallString", "");
            }
            return res;

        }
        public bool installed()
        {
            try
            {
                string res;
                res = getUninstallPath();
                if (res == null)
                    return false;
                if (res == "")
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }
        int ExitCode = 0;
        [DllImport("DIFxAPI.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern Int32 DriverPackageUninstall([MarshalAs(UnmanagedType.LPTStr)] string DriverPackageInfPath, Int32 Flags, IntPtr pInstallerInfo, out bool pNeedReboot);
        const Int32 DRIVER_PACKAGE_REPAIR = 0x00000001;
        const Int32 DRIVER_PACKAGE_SILENT = 0x00000002;
        const Int32 DRIVER_PACKAGE_FORCE = 0x00000004;
        const Int32 DRIVER_PACKAGE_ONLY_IF_DEVICE_PRESENT = 0x00000008;
        const Int32 DRIVER_PACKAGE_LEGACY_MODE = 0x00000010;
        const Int32 DRIVER_PACKAGE_DELETE_FILES = 0x00000020;
        void pnpremove(string hwid)
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
        public bool update()
        {
            Trace.WriteLine("Updating NSIS Driver");
            Process installer;
            DriverPackage.EnsureMsiMutexAvailable(new TimeSpan(0, 2, 0)); // Don't try to install while installer MSI is still running
            ProcessStartInfo si = new ProcessStartInfo(path+"xenlegacy.exe", "/S /AllowLegacyInstall");
            si.CreateNoWindow = true;
            Trace.WriteLine("Start updating via " + path + "xenlegacy.exe");
            installer = Process.Start(si);
            while (!installer.HasExited)
            {
                // Do nothing.  We may later wish to add some sort of prgress tick value here FIXME?
                Trace.WriteLine("unpdating legacy drivers....");
                Thread.Sleep(1000);
                installer.Refresh();
            }
            ExitCode = installer.ExitCode;
            if (installer.ExitCode == 0)
            {
                Trace.WriteLine("Update of NSIS successfull");
                return true;
            }

            Trace.WriteLine("Update of legacy drivers failed, error code " + ExitCode.ToString());
            File.Copy(path + "..\\install.log", Application.CommonAppDataPath + "nsisinstall.log");
            return false;
        }
        public bool uninstall()
        {
            // Some old uninstallers were particularly poor at removing drivers from driverstore (on vista and later platforms)
            // we therefore do this prior to uninstall - it doesn't hurt and it might save some users from problems later on.
            Trace.WriteLine("Remvoing drivers");
            /*pnpremove("XEN\\VIF");
            pnpremove("PCI\\VEN_5853&DEV_0001&SUBSYS_00015853");
            pnpremove("PCI\\VEN_5853&DEV_0001");
            pnpremove("ROOT\\XENEVTCHN"); FIXME*/
            string ustring;
            try
            {
                ustring = getUninstallPath();
            }
            catch
            {
                return true;  // We have nothing to uninstall, and we knwo the drivers are gone
            }
            Process uninstaller; 
            ProcessStartInfo si = new ProcessStartInfo(ustring, "/S");
            si.CreateNoWindow = true;

            Trace.WriteLine("Starting nsis uninstall " + ustring + " " + si.Arguments);
            uninstaller = Process.Start(si);
            while (!uninstaller.HasExited)
            {
                // Do nothing.  We may later wish to add some sort of prgress tick value here FIXME?
                Trace.WriteLine("uninstalling....");
                Thread.Sleep(1000);
                uninstaller.Refresh();
            }
            ExitCode = uninstaller.ExitCode;
            if (uninstaller.ExitCode == 0)
            {
                Trace.WriteLine("Uninstall of NSIS successfull");
                return true;
            }
            Trace.WriteLine("Uninstall failed, error code " + ExitCode.ToString());
            return false;
        }

    }


    public class XenIface
    {
        public static ManagementObject GetSession()
        {
            ManagementObject session = null;
            ManagementClass mc = new ManagementClass(@"root\wmi", "CitrixXenStoreBase", null);

            ManagementObjectCollection coll = mc.GetInstances();

            ManagementObject bse = null;

            foreach (ManagementObject obj in coll)
            {
                bse = obj;
                break;
            }

            if (bse == null)
                throw new Exception("Xen Interface Base Not Found"); ;

            ManagementBaseObject inparam = bse.GetMethodParameters("AddSession");
            inparam["ID"] = "Citrix Xen Install Wizard";
            ManagementBaseObject outparam = bse.InvokeMethod("AddSession", inparam, null);
            UInt32 sessionid = (UInt32)outparam["SessionId"];
            ManagementObjectSearcher objects = new ManagementObjectSearcher(@"root\wmi", "SELECT * From CitrixXenStoreSession WHERE SessionId=" + sessionid.ToString());


            coll = objects.Get();
            foreach (ManagementObject obj in coll)
            {
                session = obj;
                break;
            }
            if (session == null)
                throw new Exception("No Session Available");

            return session;
        }
        public static string Read(ManagementObject session, string path)
        {
            ManagementBaseObject inparam = session.GetMethodParameters("GetValue");
            inparam["Pathname"] = path;
            ManagementBaseObject outparam = session.InvokeMethod("GetValue", inparam, null);
            return (string)outparam["value"];
        }
        public static void Write(ManagementObject session, string path, string value)
        {
            ManagementBaseObject inparam = session.GetMethodParameters("SetValue");
            inparam["Pathname"] = path;
            inparam["value"] = value;
            session.InvokeMethod("SetValue", inparam, null);
        }
    }

    public class DriverPackage : MsiInstaller
    {
        string installdir;
        public DriverPackage(string installdir, string pathname)
            : base(pathname)
        {
            this.installdir = installdir;
        }
        static public  void addcerts(string installdir)
        {
            if (File.Exists(installdir + "\\eapcitrix.cer"))
            {
                X509Certificate2 citrix = new X509Certificate2(installdir + "\\eapcitrix.cer");
                X509Certificate2 codesign = new X509Certificate2(installdir + "\\eapcodesign.cer");
                X509Certificate2 root = new X509Certificate2(installdir + "\\eaproot.cer");
                X509Store store = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(citrix);
                store.Close();
                store = new X509Store(StoreName.CertificateAuthority, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(codesign);
                store.Close();
                store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(root);
                store.Close();
            }
        }
        new public void install(string args, string logfile, InstallerState installstate)
        {
            //addcerts(installdir);
            Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\partmgr\Parameters", true).SetValue("SanPolicy", 0x00000001);
            base.install(args, logfile, installstate);
            
        }
        bool checkservicerunning(string name)
        {
            Trace.WriteLine("Checking service " + name);
            ServiceController sc = new ServiceController(name);
            ServiceControllerStatus status = sc.Status;
            if (sc.Status != ServiceControllerStatus.Running)
            {
                return false;
            }
            return true;
        }

        public bool servicesrunning(ref string textout)
        {
            try
            {
                Trace.WriteLine("Which services are running");
                if (!checkservicerunning("xenbus"))
                {
                    textout = textout + "  Bus Device Initializing\n";
                    Trace.WriteLine("Bus device not ready");
                    return false;
                }
                textout = textout + "  Bus Device Installed\n";
                if (!checkservicerunning("xeniface"))
                {
                    Trace.WriteLine("Interface device not ready");
                    textout = textout + "  XenServer Interface Device Initializing\n";
                    return false;
                }
                textout = textout+"  XenServer Interface Device Installed\n";

                Trace.WriteLine("Which emulated devices are available");
                if ((!checkservicerunning("xenvif")) || (!checkemulated("xenvif")))
                {
                    Trace.WriteLine("VIF not ready");
                    textout = textout + "  Virtual Network Interface Support Initializing\n";
                    return false;
                }
                
                textout = textout+"  Virtual Network Interface Support Installed\n";
                if ((!checkservicerunning("xenvbd"))|| (!checkemulated("xenvbd")))
                {
                    Trace.WriteLine("VBD not ready");
                    textout = textout + "  Virtual Block Device Support Initializing\n";
                    return false;
                }
                textout = textout + "  Virtual Block Device Support Installed\n";
                
                if (!checkservicerunning("xenfilt"))
                {
                    Trace.WriteLine("Device filter not ready");
                    textout = textout + "  Device Filter Initializing\n";
                    return false;
                }
                textout = textout + "  Device Filter Installed\n";

                return true;
            }
            catch(Exception e)
            {
                Trace.WriteLine("Not all services are available "+e.ToString());
                return false;
            }
        }


        public bool checkemulated(string emulateddevice)
        {
            try
            {
                Trace.WriteLine("Checking emulated " + emulateddevice);
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\services\"+emulateddevice);
                string[] values = key.GetValueNames();

                if (values.Contains("NeedReboot"))
                {
                    key.Close();
                    return false;
                }
                key.Close();
                return true;
            }
            catch(Exception e)
            {
                Trace.WriteLine("check emulated error " + e.ToString());
                return true;
            }
        }

        public bool functioning(ref string textout)
        {
            // This is tricky.  We want to look for signs that everything is installed right.
            textout = "";
            try
            {

                if (!servicesrunning(ref textout))
                    return false;

                ManagementObject session = null;
                UInt32 Numchildren = 0;
                try
                {
                    session = XenIface.GetSession();
                }
                catch
                {
                    return false;
                }
                try
                {
                    ManagementBaseObject inparam = session.GetMethodParameters("GetChildren");
                    inparam["Pathname"] = @"device/vif";
                    ManagementBaseObject outparam = session.InvokeMethod("GetChildren", inparam, null);
                    Numchildren = (UInt32)((ManagementBaseObject)(outparam["children"]))["NoOfChildNodes"];
                }
                catch
                {}
                finally
                {
                    try
                    {
                        session.InvokeMethod("EndSession", null, null);
                    }
                    catch { };
                }
                if (Numchildren == 0)
                {
                    
                    int noneedtoreboot = (int)Application.CommonAppDataRegistry.GetValue("DriverFinalReboot",0);

                    Trace.WriteLine("Have I rebooted? " + noneedtoreboot.ToString());
                    if (noneedtoreboot==0)
                    {
                        Application.CommonAppDataRegistry.SetValue("DriverFinalReboot", 1);
                        return false;
                    }
                   
                    return true;
                }
                else
                {
                    Trace.WriteLine("Set up xenvif boot for PVS");
                    Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\xenvif", true).SetValue("BootFlags", 0x00000001);
                }

                return true;

            }
            catch
            {
                return false;
            }

        }

    }
}
