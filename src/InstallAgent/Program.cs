using System;
using System.Diagnostics;
using System.ServiceProcess;

namespace InstallAgent
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            try
            {
                TimeDateTraceListener.Initialize("InstallAgent");

                if (args.Length == 0) // run as service
                {
                    // rebootOption is populated in the constructor
                    ServiceBase[] ServicesToRun;
                    ServicesToRun = new ServiceBase[]
                    {
                        new InstallAgent()
                    };
                    ServiceBase.Run(ServicesToRun);
                }
                else if (args.Length == 1) // run from command line
                {
                    InstallAgent.RebootType rebootOpt;

                    try
                    {
                        rebootOpt = (InstallAgent.RebootType)Enum.Parse(
                            typeof(InstallAgent.RebootType), args[0], true
                        );
                    }
                    catch
                    {
                        Usage();
                        return;
                    }

                    InstallAgent ias = new InstallAgent(rebootOpt);
                    ias.InstallThreadHandler();
                }
                else
                {
                    Usage();
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.ToString());
                throw;
            }
        }

        static void Usage()
        {
            Console.WriteLine(
                "Usage: InstallAgent.exe {NOREBOOT | AUTOREBOOT | DEFAULT}"
            );
        }
    }
}
