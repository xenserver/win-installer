using PInvokeWrap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace HelperFunctions
{
    public static class Helpers
    {
        public static void Reboot()
        {
            Trace.WriteLine("OK - shutting down");
            AcquireSystemPrivilege(AdvApi32.SE_SHUTDOWN_NAME);

            if (WinVersion.GetMajorVersion() >= 5 &&
                WinVersion.GetMajorVersion() < 6)
            {
                User32.ExitWindowsEx(
                    User32.ExitFlags.EWX_REBOOT |
                    User32.ExitFlags.EWX_FORCE,
                    0
                );
            }
            else
            {
                AdvApi32.InitiateSystemShutdownEx(
                    "", "", 0, true, true,
                    AdvApi32.ShtdnReason.MAJOR_OTHER |
                    AdvApi32.ShtdnReason.MINOR_ENVIRONMENT |
                    AdvApi32.ShtdnReason.FLAG_PLANNED
                );
            }
        }

        public static void AcquireSystemPrivilege(string name)
        {
            AdvApi32.TOKEN_PRIVILEGES tkp;
            IntPtr token;

            tkp.Privileges = new AdvApi32.LUID_AND_ATTRIBUTES[1];
            AdvApi32.LookupPrivilegeValue(
                IntPtr.Zero,
                name,
                out tkp.Privileges[0].Luid
            );

            tkp.PrivilegeCount = 1;

            tkp.Privileges[0].Attributes = (uint)AdvApi32.Se_Privilege.ENABLED;

            if (!AdvApi32.OpenProcessToken(
                    Process.GetCurrentProcess().Handle,
                    (uint)(AdvApi32.Token.ADJUST_PRIVILEGES |
                           AdvApi32.Token.QUERY),
                    out token))
            {
                Win32Error.Set("OpenProcessToken");
                throw new Exception(Win32Error.GetFullErrMsg());
            }

            if (!AdvApi32.AdjustTokenPrivileges(
                    token,
                    false,
                    ref tkp,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                Win32Error.Set("AdjustTokenPrivileges");
                throw new Exception(Win32Error.GetFullErrMsg());
            }
        }

        public static void InstallCertificate(string cerPath)
        {
            X509Certificate2 cert = new X509Certificate2(cerPath);

            X509Store store = new X509Store(
                StoreName.TrustedPublisher,
                StoreLocation.LocalMachine
            );

            Trace.WriteLine(
                "Installing cerificate: \'" + Path.GetFileName(cerPath) + "\'"
            );

            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
            store.Close();

            Trace.WriteLine("Certificate installed");
        }

        public static int BitIdxFromFlag(uint flag)
        {
            if (flag == 0)
            {
                throw new Exception("\'flag\' is empty");
            }
            else if ((flag & (flag - 1)) != 0)
            // If this is true, 'flag' is not a power of 2,
            // and hence, has more than one bit set
            {
                throw new Exception("\'flag\' has more than one bits set");
            }

            return (int)Math.Log((double)flag, 2.0);
        }

        public enum ExpandedServiceStartMode : uint {
            // Contains service start modes for drivers and user services
            Boot        = 0,
            System      = 1,
            Automatic   = 2,
            Manual      = 3, //User Services
            Demand      = 3, //Drivers
            Disabled    = 4,
        }

        public static bool ChangeServiceStartMode(
            string serviceName,
            ExpandedServiceStartMode mode)
        {
            Trace.WriteLine(
                "Changing Start Mode of service: \'" + serviceName + "\'"
            );

            IntPtr scManagerHandle = AdvApi32.OpenSCManager(
                null,
                null,
                AdvApi32.SC_MANAGER_ALL_ACCESS
            );

            if (scManagerHandle == IntPtr.Zero)
            {
                Trace.WriteLine("Open Service Manager Error");
                return false;
            }

            IntPtr serviceHandle = AdvApi32.OpenService(
                scManagerHandle,
                serviceName,
                AdvApi32.SERVICE_QUERY_CONFIG |
                    AdvApi32.SERVICE_CHANGE_CONFIG
            );

            if (serviceHandle == IntPtr.Zero)
            {
                Trace.WriteLine("Open Service Error");
                return false;
            }

            if (!AdvApi32.ChangeServiceConfig(
                    serviceHandle,
                    AdvApi32.SERVICE_NO_CHANGE,
                    (uint)mode,
                    AdvApi32.SERVICE_NO_CHANGE,
                    null,
                    null,
                    IntPtr.Zero,
                    null,
                    null,
                    null,
                    null))
            {
                Win32Error.Set("ChangeServiceConfig");
                Trace.WriteLine(Win32Error.GetFullErrMsg());
                return false;
            }

            AdvApi32.CloseServiceHandle(serviceHandle);
            AdvApi32.CloseServiceHandle(scManagerHandle);

            Trace.WriteLine(
                "Start Mode successfully changed to: \'" +
                mode.ToString() + "\'"
            );

            return true;
        }

        const string SERVICES_KEY =
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\";

        public static void EnsureBootStartServicesStartAtBoot()
        // This is a function which at first glance appears pointless
        // It runs through all of our services registry entries, and
        // if it finds they are boot start, it changes their start mode
        // to be boot start.
        // The Reason:  If we move xenbus to be non-boot start,
        // then reinstall the same version of xenbus, it will become boot
        // start.  But other boot start drivers hanging off it seem to become
        // forgotten by Windows.  This function reminds Windows about them.
        {
            string[] services = {
                "xenbus", "xendisk", "xenvbd", "xenvif", "xennet", "xeniface",
            };
            foreach (string service in services) {
                if (
                    (Int32)Registry.GetValue(
                        SERVICES_KEY + service, 
                        "start", 
                        4
                    )
                    == (Int32)ExpandedServiceStartMode.Boot)
                {
                    Trace.WriteLine("ensure service " + service 
                            + " is boot start");
                    ChangeServiceStartMode(
                            service, 
                            ExpandedServiceStartMode.Boot
                    );
                }
            }
        }

        public static bool DeleteService(string serviceName)
        // Marks the specified service for deletion from
        // the service control manager database
        {
            Trace.WriteLine(
                "Deleting service: \'" + serviceName + "\'"
            );

            IntPtr scManagerHandle = AdvApi32.OpenSCManager(
                null,
                null,
                AdvApi32.SC_MANAGER_ALL_ACCESS
            );

            if (scManagerHandle == IntPtr.Zero)
            {
                Win32Error.Set("OpenSCManager");
                Trace.WriteLine(Win32Error.GetFullErrMsg());
                return false;
            }

            IntPtr serviceHandle = AdvApi32.OpenService(
                scManagerHandle,
                serviceName,
                AdvApi32.DELETE
            );

            if (serviceHandle == IntPtr.Zero)
            {
                Win32Error.Set("OpenService");
                Trace.WriteLine(Win32Error.GetFullErrMsg());
                AdvApi32.CloseServiceHandle(scManagerHandle);
                return false;
            }

            bool success = AdvApi32.DeleteService(serviceHandle);

            if (success)
            {
                Trace.WriteLine("Service deleted successfully");
            }
            else
            {
                Win32Error.Set("DeleteService");
                Trace.WriteLine(Win32Error.GetFullErrMsg());
            }

            AdvApi32.CloseServiceHandle(serviceHandle);
            AdvApi32.CloseServiceHandle(scManagerHandle);

            return success;
        }

        public static bool BlockUntilNoDriversInstalling(uint timeout)
        // Returns true, if no drivers are installing before the timeout
        // is reached. Returns false, if timeout is reached. To block
        // until no drivers are installing pass PInvoke.CfgMgr32.INFINITE
        // 'timeout' is counted in seconds.
        {
            CfgMgr32.Wait result;

            Trace.WriteLine("Checking if drivers are currently installing");

            if (timeout != CfgMgr32.INFINITE)
            {
                Trace.WriteLine("Blocking for " + timeout + " seconds..");
                timeout *= 1000;
            }
            else
            {
                Trace.WriteLine("Blocking until no drivers are installing");
            }

            result = CfgMgr32.CMP_WaitNoPendingInstallEvents(
                timeout
            );

            if (result == CfgMgr32.Wait.OBJECT_0)
            {
                Trace.WriteLine("No drivers installing");
                return true;
            }
            else if (result == CfgMgr32.Wait.FAILED)
            {
                Win32Error.Set("CMP_WaitNoPendingInstallEvents");
                throw new Exception(Win32Error.GetFullErrMsg());
            }

            Trace.WriteLine("Timeout reached - drivers still installing");
            return false;
        }

        public static string GetMsiProductCode(string msiName)
        // Enumerates the MSIs present in the system. If 'msiName'
        // exists, it returns its product code. If not, it returns
        // the empty string.
        {
            const int GUID_LEN = 39;
            const int BUF_LEN = 128;
            int err;
            int len;
            StringBuilder productCode = new StringBuilder(GUID_LEN, GUID_LEN);
            StringBuilder productName = new StringBuilder(BUF_LEN, BUF_LEN);

            Trace.WriteLine(
                "Checking if \'" + msiName + "\' is present in system.."
            );

            // ERROR_SUCCESS = 0
            for (int i = 0;
                 (err = Msi.MsiEnumProducts(i, productCode)) == 0;
                 ++i)
            {
                len = BUF_LEN;

                // Get ProductName from Product GUID
                err = Msi.MsiGetProductInfo(
                    productCode.ToString(),
                    Msi.INSTALLPROPERTY.INSTALLEDPRODUCTNAME,
                    productName,
                    ref len
                );

                if (err != 0)
                {
                    Win32Error.Set("MsiGetProductInfo", err);
                    throw new Exception(Win32Error.GetFullErrMsg());
                }

                if (msiName.Equals(
                        productName.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    Trace.WriteLine(
                        "Product found; Code: \'" +
                        productCode.ToString() + "\'"
                    );
                    return productCode.ToString();
                }
            }

            if (err == 259) // ERROR_NO_MORE_ITEMS
            {
                Trace.WriteLine("Product not found");
                return "";
            }
            else
            {
                Win32Error.Set("MsiEnumProducts", err);
                throw new Exception(Win32Error.GetFullErrMsg());
            }
        }

        public static void InstallDriver(
            string infPath,
            NewDev.DIIRFLAG flags = NewDev.DIIRFLAG.ZERO)
        {
            bool reboot;

            Trace.WriteLine(
                "Installing driver: \'" + Path.GetFileName(infPath) + "\'"
            );

            if (!NewDev.DiInstallDriver(
                    IntPtr.Zero,
                    infPath,
                    flags,
                    out reboot))
            {
                Win32Error.Set("DiInstallDriver");
                throw new Exception(Win32Error.GetFullErrMsg());
            }

            Trace.WriteLine("Driver installed successfully");
        }

        public static string[] StringArrayFromMultiSz(byte[] szz)
        // Converts a double-null-terminated
        // C string to a 'string' array
        {
            List<string> strList = new List<string>();
            int strStart = 0;

            // One character is represented by 2 bytes.
            for (int i = 0; i < szz.Length; i += 2)
            {
                if (szz[i] != '\0')
                {
                    continue;
                }

                strList.Add(
                    System.Text.Encoding.Unicode.GetString(
                        szz,
                        strStart,
                        i - strStart
                    )
                );

                strStart = i + 2;

                // if the next character is also '\0', it means we
                // have reached the end of the multi-sz C string
                if (strStart < szz.Length && szz[strStart] == '\0')
                {
                    break;
                }
            }

            return strList.ToArray();
        }

        public static void UninstallMsi(
            string msiCode,
            string args = "",
            int tries = 1)
        // Uses 'msiexec.exe' to uninstall MSI with product code
        // 'msiCode'. If the exit code is none of 'ERROR_SUCCCESS',
        // the function sleeps and then retries. The amount of time
        // sleeping is doubled on every try, starting at 1 second.
        {
            if (tries < 1)
            {
                throw new Exception("tries = " + tries + " < 1");
            }

            int secs;

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "msiexec.exe";
            startInfo.Arguments = "/x " + msiCode + " " + args;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.CreateNoWindow = true;

            for (int i = 0; i < tries; ++i)
            {
                Trace.WriteLine(
                    "Running: \'" + startInfo.FileName +
                    " " + startInfo.Arguments + "\'"
                );

                using (Process proc = Process.Start(startInfo))
                {
                    proc.WaitForExit();

                    switch (proc.ExitCode)
                    {
                    case 0:
                        Trace.WriteLine("ERROR_SUCCESS");
                        return;
                    case 1641:
                        Trace.WriteLine("ERROR_SUCCESS_REBOOT_INITIATED");
                        return;
                    case 3010:
                        Trace.WriteLine("ERROR_SUCCESS_REBOOT_REQUIRED");
                        return;
                    default:
                        if (i == tries - 1)
                        {
                            throw new Exception(
                                "Tries exhausted; Error: " +
                                proc.ExitCode
                            );
                        }

                        secs = (int)Math.Pow(2.0, (double)i);

                        Trace.WriteLine(
                            "Msi uninstall failed; Error: " +
                            proc.ExitCode
                        );
                        Trace.WriteLine(
                            "Retrying in " +
                            secs + " seconds"
                        );

                        Thread.Sleep(secs * 1000);
                        break;
                    }
                }
            }
        }

        public static void UninstallDriverPackages(string hwId)
        // Scans all oem*.inf files present in the system
        // and uninstalls all that match 'hwId'
        {
            string infPath = Path.Combine(
                Environment.GetEnvironmentVariable("windir"),
                "inf"
            );

            Trace.WriteLine(
                "Searching drivers in system for \'" + hwId + "\'"
            );

            foreach (string oemFile in Directory.GetFiles(infPath, "oem*.inf"))
            {
                // This is currently the only way to ignore case...
                if (File.ReadAllText(oemFile).IndexOf(
                        hwId, StringComparison.OrdinalIgnoreCase) == -1)
                {
                    continue;
                }

                Trace.WriteLine(
                    "\'" + hwId + "\' matches" + "\'" + oemFile + "\';"
                );

                bool needreboot;
                Trace.WriteLine("Uninstalling...");

                int err = DIFxAPI.DriverPackageUninstall(
                    oemFile,
                    (int)(DIFxAPI.DRIVER_PACKAGE.SILENT |
                          DIFxAPI.DRIVER_PACKAGE.FORCE |
                          DIFxAPI.DRIVER_PACKAGE.DELETE_FILES),
                    // N.B.: Starting with Windows 7,
                    // 'DELETE_FILES' is ignored
                    IntPtr.Zero,
                    out needreboot
                );

                if (err != 0) // ERROR_SUCCESS
                {
                    Win32Error.Set("DriverPackageUninstall", err);
                    throw new Exception(Win32Error.GetFullErrMsg());
                }

                Trace.WriteLine("Uninstalled");
            }
        }
    }
}
