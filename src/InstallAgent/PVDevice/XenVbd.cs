using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace PVDevice
{
    class XenVbd
    {
        public static bool IsFunctioning()
        {
            if (!PVDevice.IsServiceRunning("xenvbd"))
            {
                Trace.WriteLine("VBD: service not running");
                //textOut += "  Virtual Block Device Support Initializing\n";
                return false;
            }

            if (PVDevice.NeedsReboot("xenvbd"))
            {
                Trace.WriteLine("VBD: needs reboot");
                //textOut += "  Virtual Block Device Support Removing Emulated Devices\n";
                return false;
            }

            Trace.WriteLine("VBD: device installed");
            //textOut += "  Virtual Block Device Support Installed\n";
            return true;
        }
    }
}
