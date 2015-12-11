using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using Microsoft.Win32;
using System.Diagnostics;

namespace InstallAgent
{
    enum IASRebootType
    {
        NOREBOOT,
        AUTOREBOOT,
        DEFAULT
    }

    enum IASInstallType
    {
        SILENT,
        PASSIVE,
        INTERACTIVE
    }

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            bool runCMD;
            IASRebootType rebootOpt;
            IASInstallType installOpt;

            TextWriterTraceListener tlog = new TimeDateTraceListener(
                @"C:\InstallAgent.log", "Install"
            );
            Trace.Listeners.Add(tlog);
            Trace.AutoFlush = true;

            SimpleParse.Parse(
                args,
                out runCMD,
                out installOpt,
                out rebootOpt
            );

            using (RegistryKey tmpRK = Registry.LocalMachine.CreateSubKey(
                InstallAgent.rootRegKey
            ))
            {
                tmpRK.SetValue(
                    "InstallOption",
                    installOpt,
                    RegistryValueKind.DWord
                );

                tmpRK.SetValue(
                    "RebootOption",
                    rebootOpt,
                    RegistryValueKind.DWord
                );
            }

            if (runCMD)
            {
                InstallAgent ias = new InstallAgent();
                ias.InstallThreadHandler();
            }
            else // run as a service
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] 
                { 
                    new InstallAgent() 
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
