using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;

namespace Xenprep
{
    static class XenPrep
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Progress progressWindow;
            PrepareThread prepareThread;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TextWriterTraceListener tlog = new TimeDateTraceListener(Application.CommonAppDataPath + "\\Install.log", "Install");
            Trace.Listeners.Add(tlog);
            Trace.AutoFlush = true;
            
            progressWindow = new Progress();
            prepareThread = new PrepareThread(args, progressWindow);

            Thread backgroundThread = new Thread(new ThreadStart(prepareThread.Run));

            progressWindow.Show();
            backgroundThread.Start();

            Application.Run(progressWindow);
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
            base.WriteLine(DateTime.Now.ToString() +" : " + o.ToString());
        }
        public override void WriteLine(string message)
        {
            base.WriteLine(DateTime.Now.ToString() +" : " + message);
        }
    }
        

    class PrepareThread
    {
        Progress progressWindow;
        public PrepareThread(string[] args, Progress progressWindow)
        {
            this.progressWindow = progressWindow;
            this.cliargs = args;
        }
        string[] cliargs;

        public void Run()
        {
            try
            {
                

                SetProgress(0);

                SetCaption("Lock CD Drive");
                XenPrepSupport.LockCDs();
                Thread.Sleep(1000);

                SetProgress(10);

                SetCaption("Set restore point");
                XenPrepSupport.RestorePoint Restore = new XenPrepSupport.RestorePoint("Configure VM For Xenprep");
                Thread.Sleep(1000);

                SetProgress(20);

                SetCaption("Store network settings");
                XenPrepSupport.StoreNetworkSettings();
                Thread.Sleep(1000);

                SetProgress(30);

                SetCaption("Remove reliance on PV drivers");
                XenPrepSupport.RemovePVDriversFromFilters();
                XenPrepSupport.DontBootStartPVDrivers();
                Thread.Sleep(1000);

                SetProgress(40);
                Restore.End();

                SetCaption("Set restore point");
                Restore = new XenPrepSupport.RestorePoint("Xenprep VM");
                Thread.Sleep(1000);

                SetProgress(50);

                SetCaption("Remove Installer Packages");
                XenPrepSupport.UninstallMSIs();
                XenPrepSupport.UninstallXenLegacy();
                Thread.Sleep(1000);

                SetProgress(60);

                SetCaption("Clean up drivers");
                XenPrepSupport.CleanUpPVDrivers();
                Thread.Sleep(1000);

                SetProgress(70);

                SetCaption("Install new guest agent");
                XenPrepSupport.InstallGuestAgent();
                Thread.Sleep(1000);

                SetProgress(80);

                Thread.Sleep(1000);

                SetProgress(90);

                SetCaption("Unlock CD Drive");
                XenPrepSupport.UnlockCDs();
                Thread.Sleep(1000);

                SetProgress(100);
                XenPrepSupport.EjectCDs();

                XenPrepSupport.ShutDownVm();
                CloseProgressWindow();
            }
            catch(Exception e)
            {
                Trace.WriteLine("XenPrep Failed : " + e.ToString());
                progressWindow.SetRed();
                SetCaption("XenPrep Failed");
                MessageBox.Show(e.ToString());
                
            }


        }

       

        void SetCaption(string caption)
        {
            Trace.WriteLine("CAPTION: " + caption);
            progressWindow.Invoke((MethodInvoker)(() =>
            {
                progressWindow.Caption.Text = caption;
            }));
        }

        void SetProgress(int value)
        {
            Trace.WriteLine("Progress: " + value.ToString());
            progressWindow.Invoke((MethodInvoker)(() =>
            {
                progressWindow.progressBar.Value = value;
            }));
        }

        void CloseProgressWindow()
        {
            progressWindow.Invoke((MethodInvoker)(() =>
            {
                progressWindow.Close();
            }));
        }
    }

}
