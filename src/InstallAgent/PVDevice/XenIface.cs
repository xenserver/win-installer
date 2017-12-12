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

        public static bool IsFunctioning(out bool bReinstall)
        {
            bReinstall = false;
            if (!Helpers.IsServiceRunning("xeniface"))
            {
                bReinstall = true;
                return false;
            }

            if (!functioning)
            {
                if (!Helpers.IsServiceRunning("xenagent"))
                {
                    Helpers.ChangeServiceStartMode("xenagent", Helpers.ExpandedServiceStartMode.Automatic);
                }
                Trace.WriteLine("Restart xeniface agent service");
                Helpers.ServiceRestart("xenagent");
                //For backwards compatability
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
