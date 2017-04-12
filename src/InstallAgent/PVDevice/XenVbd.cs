using System.Diagnostics;
using HelperFunctions;
namespace PVDevice
{
    class XenVbd
    {
        public static bool IsFunctioning()
        {
            if (!Helpers.IsServiceRunning("xenvbd"))
            {
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
