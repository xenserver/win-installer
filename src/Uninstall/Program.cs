using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using HelperFunctions;
using PVDriversRemoval;
using Microsoft.Win32;
using BrandSupport;
using System.Diagnostics;

namespace Uninstall
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 

        static bool installerInstalledDrivers()
        {
            Trace.WriteLine("Branded :" + Branding.GetString("BRANDING_installAgentRegKey"));
            using (RegistryKey rk =
                    Registry.LocalMachine.OpenSubKey(Branding.GetString("BRANDING_installAgentRegKey")))
            {
                string installDrivers = (string)rk.GetValue("InstallDrivers", "YES");
                if (installDrivers.Equals("YES"))
                {
                    Trace.WriteLine("We installed drivers");
                    return true;
                }
                Trace.WriteLine("We did not install drivers");
                return false;
            }
        }

        [STAThread]
        static void Main()
        {
            try
            {
                Trace.WriteLine("unplug");
                if (installerInstalledDrivers())
                {
                    Trace.WriteLine("disable xenbus");
                    Helpers.ChangeServiceStartMode(
                        "xenbus",
                        Helpers.ExpandedServiceStartMode.Disabled
                    );
                    Trace.WriteLine("disable xenagent");
                    Helpers.ChangeServiceStartMode(
                        "xenagent",
                        Helpers.ExpandedServiceStartMode.Disabled
                    );
                    Trace.WriteLine("remove filters");
                    PVDriversPurge.RemovePVDriversFromFilters();
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
            }
        }

    }

    public static class Branding
    {
        private static BrandingControl handle;

        static Branding()
        {
            string brandSatPath = Path.Combine(
                new DirectoryInfo(
                    System.Reflection.Assembly.GetExecutingAssembly().Location
                ).Parent.FullName , "Branding\\brandsat.dll"
            );
            handle = new BrandingControl(brandSatPath);
        }

        public static string GetString(string key)
        {
            return handle.getString(key);
        }
    }
}
