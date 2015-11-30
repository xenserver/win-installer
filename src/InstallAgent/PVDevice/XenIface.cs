using System;
using System.Diagnostics;
using System.Management;

namespace PVDevice
{
    static class XenIface
    {
        // Only 1 session can be active at any time
        private static ManagementObject _session;

        static XenIface()
        {
            _session = null;
        }

        public static bool IsFunctioning()
        {
            if (!PVDevice.IsServiceRunning("xeniface"))
            {
                return false;
            }

            // This part is currently not needed. It will be kept
            // in place for a while, just in case..
            /*try
            {
                XenIface.CreateSession();
            }
            catch
            {
                Trace.WriteLine("IFACE: CreateSession() failed");
                return false;
            }

            int noChildNodes = 0;

            try
            {
                noChildNodes = XenIface.GetNoChildNodes();
            }
            catch { }
            finally
            {
                XenIface.DestroySession();
            }

            if (noChildNodes == 0)
            {
                int noNeedToReboot =
                    (int)Application.CommonAppDataRegistry.GetValue(
                        "DriverFinalReboot",
                        0
                    );

                Trace.WriteLine("Have I rebooted? " + noNeedToReboot.ToString());
                if (noNeedToReboot == 0)
                {
                    Application.CommonAppDataRegistry.SetValue(
                        "DriverFinalReboot",
                        1
                    );

                    return false;
                }
            }*/

            Trace.WriteLine("IFACE: device installed");
            // textOut += "  XenServer Interface Device Installed\n";
            return true;
        }

        public static void CreateSession()
        {
            ManagementClass mc = new ManagementClass(
                @"root\wmi",
                "CitrixXenStoreBase",
                null
            );

            ManagementObject bse = null;

            foreach (ManagementObject obj in mc.GetInstances())
            {
                bse = obj;
                break;
            }

            if (bse == null)
            {
                throw new Exception("Xen Interface Base Not Found");
            }

            ManagementBaseObject inparam = bse.GetMethodParameters("AddSession");
            inparam["ID"] = "Citrix Xen Install Wizard";
            ManagementBaseObject outparam = bse.InvokeMethod(
                "AddSession",
                inparam,
                null
            );

            UInt32 sessionid = (UInt32)outparam["SessionId"];
            ManagementObjectSearcher objects = new ManagementObjectSearcher(
                @"root\wmi",
                "SELECT * From CitrixXenStoreSession WHERE SessionId=" +
                sessionid.ToString()
            );

            foreach (ManagementObject obj in objects.Get())
            {
                _session = obj;
                break;
            }

            if (SessionIsNull())
            {
                throw new Exception("No Session Available");
            }
        }

        public static void DestroySession()
        {
            try
            {
                _session.InvokeMethod("EndSession", null, null);
            }
            catch { }

            _session = null;
        }

        public static int GetNoChildNodes()
        {
            if (SessionIsNull())
            {
                throw new Exception("No active session"); // or
                // this.CreateSession();
            }

            int noChildNodes = 0;

            try
            {
                ManagementBaseObject inparam =
                    _session.GetMethodParameters("GetChildren");

                inparam["Pathname"] = @"device/vif";

                ManagementBaseObject outparam =
                    _session.InvokeMethod("GetChildren", inparam, null);

                noChildNodes =
                    (int)((ManagementBaseObject)(outparam["children"]))["NoOfChildNodes"];
            }
            catch
            { }

            return noChildNodes;
        }

        public static string Read(string path)
        {
            if (SessionIsNull())
            {
                throw new Exception("No active session"); // or
                // this.CreateSession();
            }

            ManagementBaseObject inparam =
                _session.GetMethodParameters("GetValue");

            inparam["Pathname"] = path;

            ManagementBaseObject outparam = _session.InvokeMethod(
                "GetValue",
                inparam,
                null
            );

            return (string)outparam["value"];
        }

        public static void Write(string path, string value)
        {
            if (SessionIsNull())
            {
                throw new Exception("No active session"); // or
                // this.CreateSession();
            }

            ManagementBaseObject inparam =
                _session.GetMethodParameters("SetValue");

            inparam["Pathname"] = path;

            inparam["value"] = value;

            _session.InvokeMethod("SetValue", inparam, null);
        }

        public static bool SessionIsNull()
        {
            return (_session == null);
        }
    }
}
