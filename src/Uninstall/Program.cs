using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using HelperFunctions;
using PVDriversRemoval;

namespace Uninstall
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Helpers.ChangeServiceStartMode(
                "xenbus", 
                Helpers.ExpandedServiceStartMode.Disabled
            );
            PVDriversPurge.RemovePVDriversFromFilters();
        }
    }
}
