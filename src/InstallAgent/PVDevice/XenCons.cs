using System;
using System.Diagnostics;
using System.Management;
using HelperFunctions;

namespace PVDevice
{
    static class XenCons
    {
        private static Boolean functioning = false;

        public static bool IsFunctioning()
        {
            if (!Helpers.IsServiceRunning("xencons"))
            {
                return false;
            }

            if (!functioning)
            {
                if (!Helpers.IsServiceRunning("xencons_monitor"))
                {
                    Helpers.ChangeServiceStartMode("xencons_monitor", Helpers.ExpandedServiceStartMode.Automatic);
                }
                Trace.WriteLine("Restart xencons monitor service");
                Helpers.ServiceRestart("xencons_monitor");

                functioning = true;
            }

            Trace.WriteLine("CONS: device installed");
            return true;
        }
    }
}
