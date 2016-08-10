using System;
using System.Diagnostics;
using System.Management;
using HelperFunctions;

namespace PVDevice
{
    static class XenIface
    {
        // Only 1 session can be active at any time
        private static ManagementObject _session;
        private static Boolean functioning = false;

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
            if (!functioning)
            {
                // This is a workaround to cope with xeniface being re-installed
                // as xenlite is bad at reconnecting via WMI without a kick.
                // This can be removed when the lite agent uses IOCTLS
                Trace.WriteLine("Restart xenlite service");
                Helpers.ServiceRestart("xenlite");
                functioning = true;
            }
            Trace.WriteLine("IFACE: device installed");
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
            ManagementBaseObject inparam =
                _session.GetMethodParameters("SetValue");

            inparam["Pathname"] = path;

            inparam["value"] = value;

            _session.InvokeMethod("SetValue", inparam, null);
        }
    }
}
