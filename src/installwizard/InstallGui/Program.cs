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
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;
using System.Configuration.Install;
using System.Management;
using System.Management.Instrumentation;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.IO;

namespace InstallGui
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                if (!EventLog.SourceExists(esource))
                    EventLog.CreateEventSource(esource, elog);
            }
            catch { }
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length == 2)
            {
                if (args[1]=="/i")
                {
                    try
                    {
                        EventLog.WriteEntry(esource, "InstallGui Pre-install");
                    }
                    catch { }
                    try {
                        string[] installArgs = new String[] {
                            "//logfile=",
                            "//LogToConsole=false",
                            "//ShowCallStack",
                            Assembly.GetExecutingAssembly().Location
                        };
    
                        System.Configuration.Install.
                            ManagedInstallerClass.InstallHelper(installArgs);
                    }
                    catch (Exception e) {
                        EventLog.WriteEntry(esource, "InstallGui Exception "+e.ToString());
                    }
                    try
                    {
                        EventLog.WriteEntry(esource, "InstallGui Install Helper Init");
                    }
                    catch { }
                    addcertificates(Application.StartupPath);
                    try
                    {
                        EventLog.WriteEntry(esource, "InstallGui Pre-install done");
                    }
                    catch { }



                    return;
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UIPage());
        }
        static string esource="XenServer Tools Installer Gui";
        static string elog="Application";

        static void addcertificates(string installdir)
        {
            if (File.Exists(installdir + "\\eapcitrix.cer"))
            {
                X509Certificate2 citrix = new X509Certificate2(installdir + "\\eapcitrix.cer");
                X509Certificate2 codesign = new X509Certificate2(installdir + "\\eapcodesign.cer");
                X509Certificate2 root = new X509Certificate2(installdir + "\\eaproot.cer");
                X509Store store = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(citrix);
                store.Close();
                store = new X509Store(StoreName.CertificateAuthority, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(codesign);
                store.Close();
                store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(root);
                store.Close();
                try
                {
                    EventLog.WriteEntry(esource, "InstallGui Install Helpers Added");
                }
                catch { }
            }
            else
            {
                try
                {
                    EventLog.WriteEntry(esource, "InstallGui Install Helpers Not Needed");
                }
                catch { }

            }
        }
    }
}

[System.ComponentModel.RunInstaller(true)]
public class MyInstaller :
    DefaultManagementProjectInstaller
{

    public MyInstaller()
    {
        ManagementInstaller managementInstaller =
            new ManagementInstaller();
        Installers.Add(managementInstaller);
    }


}
