using System.Diagnostics;
using HelperFunctions;
namespace PVDevice
{
    class XenVbd
    {
        public static bool IsFunctioning(out bool bNeedReinstall)
        {
            bNeedReinstall = false;
            if (!Helpers.IsServiceRunning("xenvbd"))
            {
                bNeedReinstall = true;
                return false;
            }

            if (PVDevice.NeedsReboot("xenvbd"))
            {
                Trace.WriteLine("VBD: needs reboot");
                return false;
            }

            Trace.WriteLine("VBD: device installed");
            return true;
        }
    }
}
