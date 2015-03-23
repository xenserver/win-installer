/* Copyright (c) Citrix Systems Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met:
 *
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer.
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using System.Net.NetworkInformation;
using System.Security.AccessControl;
using System.IO;
using System.Runtime.InteropServices;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Configuration.Install;
using System.Management.Instrumentation;
using System.Reflection;

[assembly: Instrumented(@"root\citrix\xenserver\agent")]

namespace InstallWizard
{
    class InstallerException : Exception
    {
        public string ErrorMessage;
        public InstallerException(string message)  
        {
            this.ErrorMessage = message;
        }
    }
    public partial class InstallService : ServiceBase
    {

        public InstallService()
        {
            InitializeComponent();
        }

        Thread installthread = null;
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule,
            [MarshalAs(UnmanagedType.LPStr)]string procName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);
        static public bool isWOW64()
        {
            bool flags;
            IntPtr modhandle = GetModuleHandle("kernel32.dll");
            if (modhandle == IntPtr.Zero)
                return false;
            if (GetProcAddress(modhandle, "IsWow64Process") == IntPtr.Zero)
                return false;
            if (IsWow64Process(GetCurrentProcess(), out flags))
                return flags;
            return false;
        }
        static public bool is64BitOS()
        {
            
            if (IntPtr.Size == 8)
                return true;
            return isWOW64();

        }

        void InstallThreadHandler() 
        {
            try
            {
                Trace.WriteLine("Thread");
                Assembly curAssm;
                curAssm = Assembly.GetAssembly(this.GetType());
                int registertimeout = 0;
                bool wmiregistered = false;
                while (registertimeout < 30 * 1000)
                {
                    Trace.WriteLine("Try to register WMI");
                    try
                    {
                        if (System.Management.Instrumentation.Instrumentation.IsAssemblyRegistered(curAssm))
                        {
                            //Cool; it's already registered in WMI
                            Trace.WriteLine("Already registered");
                            wmiregistered = true;
                            break;
                        }
                        else //Well then, register it
                        {
                            Trace.WriteLine("Ensure we are registered with WMI");

                            System.Management.Instrumentation.Instrumentation.RegisterAssembly(curAssm);
                            wmiregistered = true;
                            break;
                        }
                    }
                    catch (ManagementException)
                    {
                        Trace.WriteLine("Waiting for WMI to initialise");
                        Thread.Sleep(500);
                        registertimeout += 500;
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine("WMI initialisation not finished ("+e.ToString()+")");
                        Thread.Sleep(500);
                        registertimeout += 500;
                    }
                }
                if (!wmiregistered)
                {
                    Trace.WriteLine("Unable to contact WMI");
                    InstallState.Fail("Unable to contact WMI");
                }
                Trace.WriteLine("Got WMI connection");
                CitrixXenServerInstallStatus installstatusclass = new CitrixXenServerInstallStatus(InstallState);
                Trace.WriteLine("got status");
                try
                {
                    Instrumentation.Publish(installstatusclass);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.ToString());
                    throw;
                }
                Trace.WriteLine("Begin");
                while (InstallState.PauseOnStart)
                {
                    Trace.WriteLine("Paused");
                    Thread.Sleep(1000);
                    try
                    {
                        int pausestate = (int)Application.CommonAppDataRegistry.GetValue("Continue", 0);
                        if (pausestate != 0)
                        {
                            break;
                        }
                    }
                    catch { };
                };
                InstallState.MaxProgress = 100;
                InstallState.Progress = 1;
                Trace.WriteLine("Initializing Install =======================================");

            
                DriverPackage DriversMsi;
                MsiInstaller VssProvMsi;
                MsiInstaller AgentMsi;
                string installdir;
                NSISItem InstallerNSIS;
                if (is64BitOS())
                {
                    Trace.WriteLine("64 Bit Install");
                    installdir = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Citrix\\XenToolsInstaller", "Install_Dir", Application.StartupPath);
                    DriversMsi = new DriverPackage((string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Citrix\\XenToolsInstaller", "MSISourceDir", Application.StartupPath), Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Citrix\\XenToolsInstaller", "MSISourceDir", Application.StartupPath) + "citrixxendriversx64.msi");
                    VssProvMsi = new MsiInstaller(Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Citrix\\XenToolsInstaller", "MSISourceDir", Application.StartupPath) + "citrixvssx64.msi");
                    AgentMsi = new MsiInstaller(Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Citrix\\XenToolsInstaller", "MSISourceDir", Application.StartupPath) + "citrixguestagentx64.msi");

                    InstallerNSIS = new NSISItem("Citrix XenTools", (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\Citrix\\XenToolsInstaller", "MSISourceDir", Application.StartupPath));
                
                }
                else
                {
                    Trace.WriteLine("32 Bit Install");
                    installdir = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenToolsInstaller", "Install_Dir", Application.StartupPath); 
                    DriversMsi = new DriverPackage((string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenToolsInstaller", "MSISourceDir", Application.StartupPath), Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenToolsInstaller", "MSISourceDir", Application.StartupPath) + "citrixxendriversx86.msi");
                    VssProvMsi = new MsiInstaller(Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenToolsInstaller", "MSISourceDir", Application.StartupPath) + "citrixvssx86.msi");
                    AgentMsi = new MsiInstaller(Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenToolsInstaller", "MSISourceDir", Application.StartupPath) + "citrixguestagentx86.msi");

                    InstallerNSIS = new NSISItem("Citrix XenTools", (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Citrix\\XenToolsInstaller", "MSISourceDir", Application.StartupPath));
                
                }
                installdir = "\"" + installdir + "\"";

                VifConfig Vif = new VifConfig();
                Trace.WriteLine("Entering state loop");
                Dictionary<string, int[]> prog = new Dictionary<string, int[]>();

                prog["installscore"] = new int[]{300,0};
                prog["tempscore"] = new int[] { 0, 0 };
                InstallState.MaxProgress = prog["installscore"][0];

                if (InstallService.isWOW64())
                {
                    Trace.WriteLine("Install failed");
                    InstallState.Fail("PV Tools cannot be installed on systems where .Net applications are forced to run under WoW64\n\nSet HKEY_LOCAL_MACHINE\\Software\\Microsoft\\.NetFramework\\Enable64Bit to 1 and attempt the install again.");
                }

                while ((!InstallState.Failed) && (InstallState.RebootCount > 0) && (!InstallState.Done))
                {
                    InstallState.Unchanged = true;
                    prog["installscore"][1] = 0;
                    if (InstallState.GotAgent)
                        prog["installscore"][1] += 100;
                    if (InstallState.NsisFree)
                        prog["installscore"][1] += 100;
                    if (InstallState.GotDrivers)
                        prog["installscore"][1] += 100;
                    InstallState.MaxProgress = prog["installscore"][0] + prog["tempscore"][0];
                    InstallState.Progress = prog["installscore"][1] + prog["tempscore"][1];

                    if (InstallState.GotAgent &&
                         ((InstallWizard.InstallerState.WinVersion.isServerSKU() && InstallState.GotVssProvider) || !InstallWizard.InstallerState.WinVersion.isServerSKU()) && 
                         InstallState.GotDrivers &&
                         !InstallState.RebootNow &&
                         InstallState.NsisFree &&
                         !InstallState.NeedsReboot)
                    {
                        Trace.WriteLine("Everything is installed");
                        InstallState.Installed = true;
                        InstallState.Done = true;
                        continue;
                       
                    }

                    if ((!InstallState.GotDrivers) && (InstallState.NsisFree))
                    {
                        Trace.WriteLine("Check if drivers are functioning");

                        string oldtext = InstallState.DriverText;

                        if ((DriversMsi.installed() && (!DriversMsi.olderinstalled())) && DriversMsi.functioning(ref InstallState.DriverText))
                        {
                            Trace.WriteLine("Drivers functioning");
                            InstallState.GotDrivers = true;
                        }
                        else
                        {
                            if (InstallState.DriverText != oldtext)
                            {
                                InstallState.PollingReset();
                            }
                            if ((!InstallState.DriversPlaced) && ((!DriversMsi.installed()) || DriversMsi.olderinstalled()))
                            {
                                Trace.WriteLine("Driver install package not found");
                                Trace.WriteLine("Attempt driver install");
                                Trace.WriteLine(DriversMsi.ToString());

                                if (DriversMsi.olderinstalled())
                                {
                                    
                                }

                                try
                                {
                                    DriversMsi.install("INSTALLDIR=" + installdir, "driversmsi", InstallState);
                                    InstallState.DriversPlaced = true;
                                    InstallState.NeedsReboot = true;
                                    InstallState.Polling = true;
                                    Trace.WriteLine("Install success");
                                    continue;
                                }
                                catch (InstallerException e)
                                {
                                    Trace.WriteLine("Install failed");
                                    InstallState.Fail("Failed to install drivers: " + e.ErrorMessage);
                                    continue;
                                }
                            }
                            else
                            {

                                if ((!InstallState.DriversPlaced) && InstallState.NotRebooted)
                                {
                                    DriversMsi.repair();
                                    Trace.WriteLine("Repair done");
                                    InstallState.DriversPlaced = true;
                                    InstallState.NeedsReboot = true;
                                    InstallState.Polling = true;
                                    continue;
                                }
                                else
                                {
                                    Trace.WriteLine("Repair not needed");
                                }

                            }
                            if (!InstallState.RebootNow)
                            {
                                Trace.WriteLine("Wait to see if drivers initialize");
                                int noneedtoreboot = (int)Application.CommonAppDataRegistry.GetValue("DriverFinalReboot", 0);
                                if (noneedtoreboot == 0)
                                {
                                    Trace.WriteLine("Pool - I don't know if I have to reboot");
                                    InstallState.Polling = true;
                                }
                                else
                                {
                                    Trace.WriteLine("Don't poll, I have to reboot");
                                    InstallState.RebootNow = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        Trace.WriteLine("No DriverMSI work");
                    }


                    if ((!InstallState.NsisFree) && (!InstallState.NsisHandlingRequired))
                    {
                        Trace.WriteLine("Checking NSIS");
                        if (InstallerNSIS.installed() || InstallerNSIS.nsisPartiallyInstalled())
                        {
                            Trace.WriteLine("NSIS is installed, and needs to be removed");
                            if (InstallState.RebootReady)
                            {
                                Trace.WriteLine("Removing NSIS");
                                Vif.CopyPV();
                                Trace.WriteLine("Attempting NSIS Uninstall");
                                InstallState.NsisHandlingRequired = true;
                                InstallState.RebootNow = true;
                            }
                            else
                            {
                                Trace.WriteLine("We'll remove NSIS next reboot");
                                InstallState.RebootNow = true;
                            }
                        }
                        else
                        {
                            Trace.WriteLine("No NSIS based installer found");                        
                            InstallState.NsisFree = true;
                        }
                    }
                    else
                    {
                        Trace.WriteLine("No NSIS work");
                    }

                    if (DriverPackage.unknownDeviceInstalled())
                    {
                        InstallState.Fail("Unknown PV drivers installed on system.  Uninstall PV drivers and retry");
                        continue;
                    }


                    if (InstallWizard.InstallerState.WinVersion.isServerSKU())
                    {
                        if ((!InstallState.GotVssProvider) && (InstallState.NsisFree) && (!InstallState.NsisHandlingRequired))
                        {
                            Trace.WriteLine("Checking Vss Provider");
                            if (!VssProvMsi.installed())
                            {
                                Trace.WriteLine("Vss Provider not found, installing");
                                Trace.WriteLine(VssProvMsi.ToString());
                                try
                                {
                                    VssProvMsi.install("INSTALLDIR=" + installdir, "vssprov1msi", InstallState);
                                }
                                catch (InstallerException e)
                                {
                                    InstallState.Fail("Failed to install Vss  Provider : " + e.ErrorMessage);
                                    continue;
                                }

                            }
                            else if (VssProvMsi.olderinstalled())
                            {
                                Trace.WriteLine("Old Vss Provider found, updating");
                                try
                                {
                                    VssProvMsi.install("INSTALLDIR=" + installdir, "vssprov2msi", InstallState);
                                }
                                catch (InstallerException e)
                                {
                                    InstallState.Fail("Failed to install Vss Provider : " + e.ErrorMessage);
                                    continue;
                                }
                            }
                            else
                            {
                                Trace.WriteLine("Correct Vss Provider found");
                                InstallState.GotVssProvider = true;
                            }
                        }
                        else
                        {
                            Trace.WriteLine("No VSS Work");
                        }
                    }
                    else
                    {
                        Trace.WriteLine("No ServerSKU Work");
                    }

                    if ((!InstallState.GotAgent) && (InstallState.NsisFree) && (!InstallState.NsisHandlingRequired))
                    {
                        Trace.WriteLine("Checking Agent");
                        if (!AgentMsi.installed())
                        {
                            Trace.WriteLine("Agent not found, installing");
                            Trace.WriteLine(AgentMsi.ToString());
                            try
                            {
                                AgentMsi.install("INSTALLDIR=" + installdir, "agent1msi", InstallState);
                            }
                            catch (InstallerException e)
                            {
                                InstallState.Fail("Failed to install Guest Agent : " + e.ErrorMessage);
                                continue;
                            }

                        }
                        else if (AgentMsi.olderinstalled())
                        {
                            Trace.WriteLine("Old Agent found, updating");
                            try
                            {
                                AgentMsi.install("INSTALLDIR=" + installdir, "agent2msi", InstallState);
                            }
                            catch (InstallerException e)
                            {
                                InstallState.Fail("Failed to install Guest Agent : " + e.ErrorMessage);
                                continue;
                            }
                        }
                        else
                        {
                            Trace.WriteLine("Correct Agent found");
                            InstallState.GotAgent = true;
                        }
                    }
                    else
                    {
                        Trace.WriteLine("No Agent Work");
                    }

                    if (InstallState.Polling)
                    {

                        if (InstallState.PollTimedOut)
                        {
                            Trace.WriteLine("Polling timed out");
                            InstallState.RebootNow = true;
                            InstallState.NeedsReboot = false;
                            InstallState.Polling = false;
                        }
                        else
                        {
                            prog["tempscore"][1] += 1;
                            prog["tempscore"][0] += 1;
                            InstallState.Sleep(5000);

                        }
                    }
                    else
                    {
                        Trace.WriteLine("No Polling Work");
                    }

                    if ((!InstallState.RebootReady) && InstallState.RebootNow && InstallState.Unchanged)
                    {

                        // We are ready to reboot
                        Trace.WriteLine("Is anything preventing us from shutting down?");
                        setupapi.CMP_WaitNoPendingInstallEvents(0xffffffff);
                        Trace.WriteLine("Ready to reboot");
                        InstallState.Polling = false;
                        InstallState.RebootDesired = true;
                        if (InstallState.WaitForShutdown())
                        {

                            Trace.WriteLine("Shutdown occuring");
                        }
                        continue;
                    }
                    else
                    {
                        Trace.WriteLine("No rebootready work");
                    }

                    if (InstallState.RebootReady && InstallState.RebootNow && InstallState.Unchanged)
                    {
                        Trace.WriteLine("Expecting Reboot /  Shutdown");
                        InstallState.Rebooting = true;
                        if (InstallState.NsisHandlingRequired)
                        {
                           
                            // Irritatingly, the NSIS installer continues running for longer than the
                            // lifetime of the uninstall.exe process.  Since we don't want to reboot
                            // while it (or its unattached children) are still running, we rely on
                            // the NSIS uninstaller to perform its own reboot, when it is done.
                            //
                            // We want to update George or earlier installers, as they don't
                            // uninstall silently
                            if (InstallerNSIS.installed() && 
                                ((InstallerNSIS.majorVersion()>5 ) || 
                                 ((InstallerNSIS.majorVersion()==5) && (InstallerNSIS.minorVersion()>5))))
                            {
                                InstallerNSIS.uninstall();
                            }
                            else 
                            {
                                // NSIS is installed, but the install is corrupt and the uninstaller can't be located
                                InstallerNSIS.update();
  
                                //NSIS returns the same errorlevel for 'failure to install' and for 
                                // 'needs a reboot to install'.  So we have to presume the install
                                // was a success.
                            }
                            InstallState.RebootNow = false;
                            InstallState.Done = true;
                        }
                        else
                        {
                            InstallState.Done = true;
                            InstallState.RebootNow = false;
                            InstallState.DoShutdown();
                        }

                    }
                    else
                    {
                        Trace.WriteLine("No Shutdown work");
                    }
                }

                Trace.WriteLine("Exited install cycle "+InstallState.Failed+" "+InstallState.RebootCount+" "+InstallState.Done);

                if (!InstallState.Passive)
                {
                    // We want to wait until the user terminates the GUI installer before shutting down the service
                    InstallState.WaitForDone();
                }
                
                if (InstallState.Installed || InstallState.Cancelled || InstallState.Failed)
                {
                    Trace.WriteLine("Installed " + InstallState.Installed + " Cancelled " + InstallState.Cancelled + " Failed " + InstallState.Failed);

                    try
                    {
                        Registry.LocalMachine.OpenSubKey(@"Software").OpenSubKey("Microsoft").OpenSubKey("Windows").OpenSubKey("CurrentVersion").OpenSubKey("Run", true).DeleteValue("CitrixXenAgentInstaller");
                    }
                    catch (Exception e)
                    {
                        if (!InstallState.Passive)
                        {
                            Trace.WriteLine("Failed to remove install agent run subkey: " + e.ToString());
                            InstallState.Fail("Failed to remove install agent subkey");
                        }
                    }
                    Trace.WriteLine("Turn off DriverFinalReboot");
                    Application.CommonAppDataRegistry.SetValue("DriverFinalReboot", 0);
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\XenPVInstall", "Start", 3);
                    (new Thread(delegate()
                    {
                        this.Stop();
                    })).Start();

                }

            }
            catch (Exception e)
            {
                Trace.WriteLine("Install exception :" + e.ToString());
                InstallState.Fail("Failed to install due to exception" + e.ToString());
                if (!InstallState.Passive)
                {
                    // We want to wait until the user terminates the GUI installer before shutting down the service
                    
                    InstallState.WaitForDone();
                }
                try
                {
                    Registry.LocalMachine.OpenSubKey(@"Software").OpenSubKey("Microsoft").OpenSubKey("Windows").OpenSubKey("CurrentVersion").OpenSubKey("Run",true).DeleteValue("CitrixXenAgentInstaller");
                }
                catch {
                    Trace.WriteLine("Failed to remove install agent run subkey (error state)");
                    InstallState.Fail("Failed to remove install agent subkey");
                }
            }

        }

        InstallerState InstallState;

        class TimeDateTraceListener : TextWriterTraceListener
        {
            public TimeDateTraceListener(String file, String name)
                : base(file, name)
            {
            }

            public override void WriteLine(object o)
            {
                base.WriteLine(DateTime.Now.ToString() +" : " + o.ToString());
            }
            public override void WriteLine(string message)
            {
                base.WriteLine(DateTime.Now.ToString() +" : " + message);
            }
        }
        
        protected override void OnStart(string[] args)
        {
            // Start thread - so we can do everything in the background
            TextWriterTraceListener tlog = new TimeDateTraceListener(Application.CommonAppDataPath + "\\Install.log", "Install");
            Trace.Listeners.Add(tlog);
            Trace.AutoFlush = true;
            Trace.WriteLine("OnStart");  
            InstallState = new InstallerState();
            installthread = new Thread(InstallThreadHandler);
            installthread.Start();
            
        }
        protected override void OnShutdown()
        {
            Trace.WriteLine("System shutting down");
            InstallState.ServiceShutdown();
            Trace.WriteLine("Joining installer");
            installthread.Join();
            Trace.WriteLine("Stopping");
        }
        protected override void OnStop()
        {
            Trace.WriteLine("Service Stop Received");
            InstallState.ServiceShutdown();
            Trace.WriteLine("Joining installer");
            installthread.Join();
            Trace.WriteLine("Stopping");
        }
    }

    

    [InstrumentationClass(InstrumentationType.Instance)]
    public class CitrixXenServerInstallStatus {
        public Boolean Passive { get { return InstallState.Passive; } }
        public Boolean NoReboot { get { return InstallState.NoShutdown; } }
        public String FailReason { get { return InstallState.failreason; } }
        public String status
        {
            get
            {
                if (InstallState.Installed)
                {
                    Trace.WriteLine("STATUS:Success");
                    return "Success";
                }

                if (InstallState.Failed)
                {
                    Trace.WriteLine("STATUS:Failed");
                    return "Failed";
                }

                if (InstallState.PauseOnStart)
                {
                    Trace.WriteLine("STATUS:Paused");
                    return "Paused";
                }

                if (InstallState.Rebooting)
                {
                    Trace.WriteLine("STATUS:RebootProgressing");
                    return "RebootProgressing";
                }

                if (InstallState.Polling)
                {
                    Trace.WriteLine("STATUS:WaitingForDrivers");
                    return "WaitingForDrivers";
                }

                if (InstallState.RebootDesired)
                {
                    Trace.WriteLine("STATUS:RequestReboot");
                    return "RequestReboot";
                }

                Trace.WriteLine("STATUS:Installing");
                return "Installing";
            }
            
        }
        public Int32 Progress { get { return InstallState.Progress; } }
        public Int32 MaxProgress { get { return InstallState.MaxProgress; } }
        InstallerState InstallState;
        public string StatusDisplayText {
            get { return InstallState.StateText;  }
        }
        public CitrixXenServerInstallStatus(InstallerState InstallState) {
            this.InstallState = InstallState;
        }
    }

    [RunInstaller(true)]
    public class XenInstallerInstaller : DefaultManagementProjectInstaller
    {
        private ServiceProcessInstaller processInstaller;
        private ServiceInstaller serviceInstaller;
        private ManagementInstaller managementInstaller;
        public XenInstallerInstaller()
        {
            processInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();

            processInstaller.Account = ServiceAccount.LocalSystem;
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            serviceInstaller.ServiceName = "Citrix Xen Installer";
            serviceInstaller.Description = "Installs and Updates Xen Server Tools";
            serviceInstaller.ServicesDependedOn = new string[] { "Winmgmt" };

            managementInstaller = new ManagementInstaller();

            Installers.Add(serviceInstaller);
            Installers.Add(processInstaller);
            Installers.Add(managementInstaller);
        }

        private void InitializeComponent()
        {

        } 
    }
}
