using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace InstallAgent
{
    static class SimpleParse
    {
        private enum Flags : int
        {
            CMD = 0x1,
            INSTALL = 0x2,
            REBOOT = 0x4
        }

        public static int Parse(
            string[] args,
            out bool runCMD,
            out IASInstallType installOpt,
            out IASRebootType rebootOpt)
        {
            // Assign default values
            runCMD = false;
            installOpt = IASInstallType.PASSIVE;
            rebootOpt = IASRebootType.DEFAULT;
            int userArgs = 0;

            Trace.WriteLine("===> SimpleParse.Parse()");

            for (int i = 0; i < args.Length; ++i)
            {
                string tmp = args[i];

                if (tmp.Equals("--cmd") || tmp.Equals("-c"))
                {
                    runCMD = true;
                    ArgumentCheck(ref userArgs, Flags.CMD);
                }
                else if (tmp.Equals("--install") || tmp.Equals("-i"))
                {
                    tmp = args[++i].ToLower();
                    installOpt = ParseInstallType(tmp);
                    ArgumentCheck(ref userArgs, Flags.INSTALL);
                }
                else if (tmp.Equals("--reboot") || tmp.Equals("-r"))
                {
                    tmp = args[++i].ToLower();
                    rebootOpt = ParseRebootType(tmp);
                    ArgumentCheck(ref userArgs, Flags.REBOOT);
                }
                else
                {
                    throw new ArgumentException(
                        String.Format(
                            "Unknown flag: \'{0}\'", args[i]
                        )
                    );
                }
            }

            Trace.WriteLine("<=== SimpleParse.Parse()");

            return userArgs;
        }

        private static IASInstallType ParseInstallType(string arg)
        {
            IASInstallType installOpt;

            if (arg.Equals("silent"))
            {
                installOpt = IASInstallType.SILENT;
            }
            else if (arg.Equals("passive"))
            {
                installOpt = IASInstallType.PASSIVE;
            }
            else if (arg.Equals("interactive"))
            {
                installOpt = IASInstallType.INTERACTIVE;
            }
            else
            {
                throw new ArgumentException(
                    String.Format(
                        "Unknown value for --install flag: \'{0}\'", arg
                    )
                );
            }

            return installOpt;
        }

        private static IASRebootType ParseRebootType(string arg)
        {
            IASRebootType rebootOpt;

            if (arg.Equals("noreboot"))
            {
                rebootOpt = IASRebootType.NOREBOOT;
            }
            else if (arg.Equals("autoreboot"))
            {
                rebootOpt = IASRebootType.AUTOREBOOT;
            }
            else if (arg.Equals("default"))
            {
                rebootOpt = IASRebootType.DEFAULT;
            }
            else
            {
                throw new ArgumentException(
                    String.Format(
                        "Unknown value for --reboot flag: \'{0}\'", arg
                    )
                );
            }

            return rebootOpt;
        }

        // Checks if we have already seen the current flag;
        // If no, it turns it on in 'userArgs'.
        // If yes, it raises an exception.
        private static void ArgumentCheck(ref int userArgs, Flags bit)
        {
            string cmdFlag;

            switch (bit)
            {
            case Flags.CMD:
                cmdFlag = "cmd";
                break;
            case Flags.INSTALL:
                cmdFlag = "install";
                break;
            case Flags.REBOOT:
                cmdFlag = "reboot";
                break;
            default:
                throw new Exception(
                    String.Format(
                        "Invalid argument bit: {0}",
                        Convert.ToString((int)bit, 2)
                    )
                );
            }

            if ((userArgs & (int)bit) == 0)
            {
                userArgs |= (int)bit;
            }
            else
            {
                throw new Exception(
                    String.Format(
                        "Error: Double input for flag \'{0}\'", cmdFlag
                    )
                );
            }
        }
    }
}
