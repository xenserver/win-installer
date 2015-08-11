using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;

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

            progressWindow = new Progress();
            prepareThread = new PrepareThread(args, progressWindow);

            Thread backgroundThread = new Thread(new ThreadStart(prepareThread.Run));

            progressWindow.Show();
            backgroundThread.Start();

            Application.Run(progressWindow);

       
            
            

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
                XenPrepSupport.LockCD();
                Thread.Sleep(1000);

                SetProgress(10);

                SetCaption("Set restore point");
                XenPrepSupport.SetRestorePoint();
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

                SetCaption("Set restore point");
                XenPrepSupport.SetRestorePoint();
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
                XenPrepSupport.UnlockCD();
                Thread.Sleep(1000);

                SetProgress(100);
                CloseProgressWindow();
            }
            catch(Exception e)
            {
                progressWindow.SetRed();
                
                MessageBox.Show(e.Message);
                SetCaption("XenPrep Failed");
            }


        }

       

        void SetCaption(string caption)
        {
            progressWindow.Invoke((MethodInvoker)(() =>
            {
                progressWindow.Caption.Text = caption;
            }));
        }

        void SetProgress(int value)
        {
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
