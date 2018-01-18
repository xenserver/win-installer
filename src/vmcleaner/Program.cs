using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PVDriversRemoval;

namespace vmcleaner
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.Out.WriteLine("Preparing to clean VM");
                Console.Out.WriteLine(" > Remove PV Drivers From Filters");
                PVDriversPurge.RemovePVDriversFromFilters();
                Console.Out.WriteLine(" > Don't Boot Start PV Drivers");
                PVDriversPurge.DontBootStartPVDrivers();
                Console.Out.WriteLine(" > Uninstall MSI Files");
                PVDriversPurge.UninstallMSIs();
                Console.Out.WriteLine(" > Uninstall Drivers and Devices");
                PVDriversPurge.UninstallDriversAndDevices();
                Console.Out.WriteLine(" > Clean up after ourselves");
                Console.Out.WriteLine("   > Legacy");
                PVDriversPurge.CleanUpXenLegacy();
                Console.Out.WriteLine("   > Services");
                PVDriversPurge.CleanUpServices();
                Console.Out.WriteLine("   > Driver Files");
                PVDriversPurge.CleanUpDriverFiles();
                Console.Out.WriteLine("Done");
                Console.Out.WriteLine("");
                Console.Out.WriteLine("");
                Console.Out.WriteLine("Please reboot the VM before continuing");
            }
            catch (Exception e)
            {
                Console.Out.WriteLine("Failed.  Please report the following message to your support engineer:");
                Console.Out.WriteLine("VMCleaner Failure : " + e.ToString());
            }
        }
    }
}
