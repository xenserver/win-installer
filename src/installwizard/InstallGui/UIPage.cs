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
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Management;
using System.Management.Instrumentation;
using System.Threading;
using Microsoft.Win32;
using System.Diagnostics;
using System.ServiceProcess;
using System.Security.Principal;

[assembly: Instrumented(@"root\citrix\xenserver\agent")]


namespace InstallGui
{
 
    public partial class UIPage : Form
    {
        public UIPage()
        {
            InitializeComponent();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {

        }
        
        class WmiInstallerService
        {

           

            ManagementObject service = null;
            public WmiInstallerService()
            {
                ManagementClass mc = new ManagementClass(@"root\citrix\xenserver\agent", "CitrixXenServerInstallStatus", null);
                ManagementObjectCollection coll = null;
                int counter = 0;
                while (counter < 10000)
                {
                    try
                    {
                        coll = mc.GetInstances();

                        while (coll.Count == 0 && counter < 10000)
                        {
                            if (coll.Count == 0)
                            {
                                Thread.Sleep(500);
                                counter += 500;
                            }
                            coll = mc.GetInstances();
                        }
                        break;
                    }
                    catch {
                        Thread.Sleep(500);
                        counter += 500; 
                    }
                }

                if (coll == null || coll.Count == 0)
                {
                    service = null;
                    return;
                }
                foreach (ManagementObject obj in coll)
                {
                    service = obj;
                }

            }

            public int Progress { get {
                if (service == null)
                    return 0;
                return (int)service["Progress"]; } }
            public int MaxProgress { get {
                if (service == null)
                    return 0;
                return (int)service["MaxProgress"]; } }

            public string Status()
            {

                if (service == null)
                {
                    return "Disconnected";
                }

                return (string)service["status"];
            }
            public string DisplayText()
            {

                if (service == null)
                {
                    return "Disconnected";
                }

                return (string)service["StatusDisplayText"];
            }
            public string FailMsg()
            {
                if (service == null)
                {
                    return "Disconnected";
                }
                return (string)service["FailReason"];
            }
        }

        void setDone(bool success, string message)
        {
            this.progressBar1.Show();
            this.AcceptButton = this.Default;
            this.CancelButton = this.Default;
            Title.Text="";
            Extra.Text = "";
            Title.Text=message;
            this.Next.Enabled = false;
            this.Back.Enabled = false;
            
            this.Default.Text = "&Done";
            this.onDone = new CitrixXenServerInstallEvent("UserDone");

            
            
            if (success)
            {
                this.progressBar1.ForeColor = Color.Green;
            }
            else
            {
                this.progressBar1.ForeColor = Color.Red;
            }
            this.progressBar1.Value = this.progressBar1.Maximum;
            this.BringToFront();
            if ((!passive) || (passivetildone))
            {
                this.Default.Enabled = true;
            }
            else
            {
                doneevent();
            }
       

        }
        void SetDone(bool success, string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { setDone(success, message); });
            }
            else
            {
                setDone(success, message);
            }
            running = false;
        }

        void setReboot()
        {
            Title.Text="";
            Extra.Text = "";
            Title.Text = "Windows must restart to continue the installation";
            Extra.Text = "Installation will automatically continue after this restart\n\n" +
                "Windows may automatically restart several times before installation is complete\n\n" +
                "Click \'Restart Now\' to restart your VM.\n\n";

            this.progressBar1.Hide();
            this.Next.Text = "&Restart Now";
            this.Back.Enabled = false;

            this.Default.Text = "Cancel";
            onNext = new CitrixXenServerInstallEvent("UserReboot");
            this.Activate();
            this.AcceptButton = this.Next;
            this.CancelButton = this.Default;
            if (!passive)
            {
                this.Next.Enabled = true;
                this.Default.Enabled = true;
            }
 
        }

        void SetReboot()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { setReboot(); });
            }
            else
            {
                setReboot();
            }
        }



        int oldprogress = 0;
        void setProgressing(string[] messages, int progress, int maxprogress)
        {
            this.AcceptButton = this.Default;
            this.CancelButton = this.Default;
            Title.Text="";
            Extra.Text = "";
            Title.Text = messages[0];
            if (messages.Length > 1)
            {
                foreach (string item in messages.Skip(1))
                {
                    Extra.Text = Extra.Text + "\n" + item + "\n";
                }
            }
            this.Next.Enabled = false;
            this.Back.Enabled = false;
            if ((!passive) || (passivetildone))
            {
                this.Default.Enabled = true;
            }
            this.Default.Text = "Cancel";
            this.progressBar1.Maximum = maxprogress;
            this.progressBar1.Increment(progress - oldprogress);
            oldprogress = progress;
        }
        void SetProgressing(string[] messages, int progress, int maxprogress)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { setProgressing(messages, progress, maxprogress); });
            }
            else
            {
                setProgressing(messages,progress, maxprogress);
            }
        }


        void disableAll()
        {
            this.progressBar1.Show();
            this.Next.Enabled = false;
            this.Back.Enabled = false;
            this.Default.Enabled = false;
            this.AcceptButton = this.Default;
            this.CancelButton = this.Default;
        }

        void DisableAll()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { disableAll(); });
            }
            else
            {
                disableAll();
            }
        }

        void setNext(string message, CitrixXenServerInstallEvent action)
        {
            this.progressBar1.Show();
            this.AcceptButton = this.Next;
            this.CancelButton = this.Default;
            Title.Text = "";
            Extra.Text = "";
            Title.Text=message;
            this.Next.Enabled = true;
            this.Next.Text = "&Next";
            this.Back.Enabled = false;
            this.Default.Enabled = true;
            this.Default.Text = "Cancel";
            onNext = action;
            
        }
        CitrixXenServerInstallEvent onNext = null;
        CitrixXenServerInstallEvent onDone = null;
        void SetNext(string message, CitrixXenServerInstallEvent action)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { setNext(message, action); });
            }
            else
            {
                setNext(message, action);
            }
        }
        void AddMessage(string add)
        {
            if (Extra.InvokeRequired)
            {
                Extra.Invoke((MethodInvoker)delegate
                {
                    Extra.Text = Extra.Text +"\n"+ add;
                });
            }
            else
            {
                Extra.Text = Extra.Text + "\n" + add;
            }
        }

        bool running = true;
        void RunServiceCommunicator()
        {
            AddMessage("Checking System\nPlease Wait");
            DisableAll();
            while (running) // Polling - FIXME with an event managed thing
            {

                WmiInstallerService service = new WmiInstallerService();
                Trace.WriteLine("STATUS EVENT : " + service.Status());
                switch (service.Status())
                {
                    case "Paused":

                        SetNext("Installation paused", new CitrixXenServerInstallEvent("UnPause"));
                        break;
                    case "Disconnected":
                        SetDone(false, "The installation of Citrix XenServer Tools has failed");
                        AddMessage("Installation Service Not Found");
                        break;
                    case "Installing":
                        SetProgressing(new string[] { "Installing Citrix XenServer Tools", service.DisplayText() }, service.Progress, service.MaxProgress);
                        break;
                    case "Success":
                        SetDone(true, "You have successfully installed the Citrix XenServer Tools.");
                        AddMessage("Click 'Done' to exit the installer");
                        break;
                    case "Failed":
                        SetDone(false, "The installation of Citrix XenServer Tools has failed");
                        AddMessage(service.FailMsg());
                        break;
                    case "RequestReboot":
                        SetReboot();
                        break;
                    case "RebootProgressing":
                        SetProgressing(new string[] { "Preparing to restart", service.DisplayText(), "Do not manually shutdown or restart the machine while this operation is in progress", "", "Windows may automatically restart several times before installation is complete" }, service.Progress, service.MaxProgress);
                        running = false;
                        break;
                    case "WaitingForDrivers":
                        SetProgressing((new string[] { "Waiting for drivers to initialize", service.DisplayText(), "Windows may automatically restart several times before installation is complete" }), service.Progress, service.MaxProgress);
                        break;
                    default:
                        AddMessage("Unknown status : " + service.Status());
                        break;
                }
                Thread.Sleep(200);
            }
        }

        event DoneEventHandler doneevent = null;

        Thread communciator;


        delegate void DoneEventHandler();
        void WaitUntilDone()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker) delegate
                {

                    this.Close();
                });
            }
            else
            {
                this.Close();
            }
        }
        class TimeDateTraceListener : TextWriterTraceListener
        {
            public TimeDateTraceListener(String file, String name)
                : base(file, name)
            {
            }

            public override void WriteLine(object o)
            {
                base.WriteLine(DateTime.Now.ToString() + " : " + o.ToString());
            }
            public override void WriteLine(string message)
            {
                base.WriteLine(DateTime.Now.ToString() + " : " + message);
            }
        }
        bool passive;
        bool passivetildone;
        private void UIPage_Load(object sender, EventArgs e)
        {

            TextWriterTraceListener tlog = new TimeDateTraceListener(Application.CommonAppDataPath + "\\Install.log", "Install");
            Trace.Listeners.Add(tlog);
            Trace.AutoFlush = true;
            string[] args = Environment.GetCommandLineArgs();
            passivetildone = false;
            passive = true;

            Title.Text="";
            Extra.Text="";

            try
            {
                ServiceController sc = new ServiceController("XenPVInstall");
                if (sc.Status != ServiceControllerStatus.Running)
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Services\XenPVInstall", "Start", 2);
                    sc.Start();
                }
            }
            catch
            {
                Trace.WriteLine("Unable to find XenPVInstall service");
                Title.Text = "Unable to find XenPVInstall service";
            }


            if (args.Length == 2)
            {
                if (args[1] == "/Active" )
                {
                    Trace.WriteLine("Active (cmd)");
                    passive = false;
                    Registry.SetValue("HKEY_CURRENT_USER\\Software\\Citrix\\XenToolsInstaller", "UIMode", "PassiveTilDone");
                }
            }
            try
            {
                string ui = (string)Registry.GetValue("HKEY_CURRENT_USER\\Software\\Citrix\\XenToolsInstaller", "UIMode", "Passive");
                if ( ui== "PassiveTilDone")
                {
                    Trace.WriteLine("Passive Til Done");
                    passivetildone = true;
                    onDone = new CitrixXenServerInstallEvent("Cancel");
                }
                else if (ui == "Active") {
                    Trace.WriteLine("Active (user)");
                    passive = false;
                    Registry.SetValue("HKEY_CURRENT_USER\\Software\\Citrix\\XenToolsInstaller", "UIMode", "PassiveTilDone");
                }
            }
            catch
            {
                passivetildone = false;
            }
            
            Trace.WriteLine("OnStart");  

            communciator = new Thread(RunServiceCommunicator);
            
            communciator.Start();
            doneevent += new DoneEventHandler(WaitUntilDone);

        }

        private void Next_Click(object sender, EventArgs e)
        {
            DisableAll();
            if (onNext != null)
            {
                onNext.SendEvent();
            }
        }

        private void Default_Click(object sender, EventArgs e)
        {
            DisableAll();
            if (onDone != null)
            {
                onDone.SendEvent();
            }
            doneevent();
        }

        private void Title_Click(object sender, EventArgs e)
        {

        }

    }


    public class CitrixXenServerInstallEvent : BaseEvent
    {
        public string status;
        public CitrixXenServerInstallEvent(string status)
        {
            this.status = status;
        }
        public void SendEvent() {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal prin = new WindowsPrincipal(id);
            if (prin.IsInRole(WindowsBuiltInRole.Administrator))
            {
                try
                {
                    this.Fire();
                }
                catch
                {
                    // It seems it is possible for us to loose permissions to fire the
                    // WMI event if the disk the installer msi is launched from is removed
                    // (say from the DVD drive)  As a workaround, we launch an external
                    // process to fire the WMI event
                    SendEventViaProcess();
                }
            }
            else
            {
                SendEventViaProcess();
            }
        }
        void SendEventViaProcess()
        {
            try
            {
                Process proc = new Process();
                ProcessStartInfo si = new ProcessStartInfo(Application.StartupPath + "\\UiEvent.exe", status);
                si.CreateNoWindow = true;
                si.ErrorDialog = false;
                //si.UseShellExecute = true;
                si.Verb = "RunAs";
                proc.StartInfo = si;
                proc.Start();
                proc.WaitForExit();
            }
            catch (Exception e)
            {
                Trace.WriteLine("Exception waiting for UI event : " + e.ToString());
            }
        }
    }
}
