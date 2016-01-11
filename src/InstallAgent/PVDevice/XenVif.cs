using Microsoft.Win32;
using PInvokeWrap;
using State;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using XSToolsInstallation;

namespace PVDevice
{
    class XenVif
    {
        public static bool IsFunctioning()
        {
            // If there are no vifs for the VM, xenbus
            // does not enumerate a device for xenvif
            if (PVDevice.IsServiceNeeded("VIF"))
            {
                if (!PVDevice.IsServiceRunning("xenvif"))
                {
                    return false;
                }

                if (!Device.ChildrenInstalled("xenvif"))
                {
                    Trace.WriteLine("VIF: children not installed");
                    return false;
                }

                if (PVDevice.NeedsReboot("xenvif"))
                {
                    Trace.WriteLine("VIF: needs reboot");
                    return false;
                }

                if (!Installer.GetFlag(
                        Installer.States.NetworkSettingsRestored))
                {
                    try
                    {
                        FixupAliases();
                        NetworkSettingsSaveRestore(false); // Restore
                        Installer.SetFlag(
                            Installer.States.NetworkSettingsRestored
                        );
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine(
                            "VIF: Restoring network config triggered " +
                            "an exception: " + e.ToString()
                        );
                    }
                }

                Trace.WriteLine("VIF: device installed");
            }
            else
            {
                Trace.WriteLine("VIF: service not needed");
            }

            return true;
        }

        public static void NetworkSettingsSaveRestore(bool save)
        {
            // Combined the 2 actions since they
            // only differ in these 3 words
            string[] action = save ?
                new string[] { "Saving", "/save", "saved" } :
                new string[] { "Restoring", "/restore", "restored" };

            string qNetExe = Path.Combine(
                InstallAgent.InstallAgent.exeDir,
                @"netsettings\QNetSettings.exe"
            );

            if (!File.Exists(qNetExe))
            {
                throw new Exception(
                    String.Format("\'{0}\' does not exist", qNetExe)
                );
            }

            Trace.WriteLine(action[0] + " network settings");

            ProcessStartInfo start = new ProcessStartInfo();
            start.Arguments = "/log " + action[1];
            start.FileName = qNetExe;
            start.WindowStyle = ProcessWindowStyle.Hidden;
            start.CreateNoWindow = true;

            using (Process proc = Process.Start(start))
            {
                proc.WaitForExit();
            }

            if (!save) // == restore
            {
                VifDisableEnable(false);
                VifDisableEnable(true);
            }

            Trace.WriteLine("Network settings " + action[2]);
        }

        public static void FixupAliases()
        {
            // If we currently have XenNet devices working
            // and we don't have any aliases listed,
            // generate aliases as per the coinstaller
            RegistryKey netKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\services\xennet\enum"
            );

            if (netKey == null)
            {
                Trace.WriteLine("No XenNet instances found");
                return;
            }

            int count = (int)netKey.GetValue("Count", -1);

            if (count == -1)
            {
                Trace.WriteLine(@"XenNet\Count doesn't exist");
                return;
            }

            for (int i = 0; i < count; ++i)
            {
                try
                {
                    BuildAlias(
                        i,
                        (string)netKey.GetValue(i.ToString())
                    );
                }
                catch (Exception e)
                {
                    Trace.WriteLine(
                        "Failed to build alias for " + i.ToString() +
                        ": " + e.ToString()
                    );
                }
            }
        }

        private static void BuildAlias(int index, string devicePath)
        {
            RegistryKey vifKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\services\xenvif",
                true
            );

            RegistryKey aliases = vifKey.CreateSubKey("aliases");

            if (aliases.GetValueNames().Contains(index.ToString()))
            {
                Trace.WriteLine(
                    "Found pre-existing alias for " + index.ToString()
                );

                return;
            }

            Trace.WriteLine(
                "Fixing up alias for " + index.ToString() +
                " with " + devicePath
            );

            aliases.SetValue(
                index.ToString(),
                @"SYSTEM\CurrentControlSet\Enum\" +
                devicePath
            );
        }

        private static void VifDisableEnable(bool enable)
        {
            using (SetupApi.DeviceInfoSet devInfoSet =
                       new SetupApi.DeviceInfoSet(
                       IntPtr.Zero,
                       "XENVIF",
                       IntPtr.Zero,
                       SetupApi.DiGetClassFlags.DIGCF_PRESENT |
                       SetupApi.DiGetClassFlags.DIGCF_ALLCLASSES))
            {
                SetupApi.SP_DEVINFO_DATA devInfoData =
                    new SetupApi.SP_DEVINFO_DATA();

                devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

                Trace.WriteLine("DevInfoData Size " + devInfoData.cbSize.ToString());

                for (uint i = 0;
                     SetupApi.SetupDiEnumDeviceInfo(
                         devInfoSet.Get(),
                         i,
                         devInfoData);
                     ++i)
                {
                    Trace.WriteLine("dev inst: " + devInfoData.devInst.ToString());
                    SetupApi.PropertyChangeParameters pcParams =
                        new SetupApi.PropertyChangeParameters();

                    pcParams.size = 8;
                    pcParams.diFunction = SetupApi.DI_FUNCTION.DIF_PROPERTYCHANGE;
                    pcParams.scope = SetupApi.Scopes.Global;

                    if (enable)
                    {
                        pcParams.stateChange = SetupApi.StateChangeAction.Enable;
                    }
                    else
                    {
                        pcParams.stateChange = SetupApi.StateChangeAction.Disable;
                    }

                    pcParams.hwProfile = 0;
                    var pinned = GCHandle.Alloc(pcParams, GCHandleType.Pinned);

                    byte[] temp = new byte[Marshal.SizeOf(pcParams)];
                    Marshal.Copy(
                        pinned.AddrOfPinnedObject(),
                        temp,
                        0,
                        Marshal.SizeOf(pcParams)
                    );

                    for (int j = 0; j < temp.Length/*Marshal.SizeOf(pcParams)*/; ++j)
                    {
                        Trace.WriteLine("[" + temp[j].ToString() + "]");
                    }

                    var pdd = GCHandle.Alloc(devInfoData, GCHandleType.Pinned);

                    Trace.WriteLine(Marshal.SizeOf(pcParams).ToString());

                    Trace.WriteLine(
                        "InstallPaarams " +
                        SetupApi.SetupDiSetClassInstallParams(
                            devInfoSet.Get(),
                            pdd.AddrOfPinnedObject(),
                            pinned.AddrOfPinnedObject(),
                            Marshal.SizeOf(pcParams)
                        ).ToString()
                    );

                    Trace.WriteLine(Marshal.GetLastWin32Error().ToString());

                    Trace.WriteLine(
                        "CallClassInstaller " +
                        SetupApi.SetupDiCallClassInstaller(
                            SetupApi.DI_FUNCTION.DIF_PROPERTYCHANGE,
                            devInfoSet.Get(),
                            pdd.AddrOfPinnedObject()
                        ).ToString() + " " +
                        Marshal.GetLastWin32Error().ToString()
                    );

                    pdd.Free();
                    pinned.Free();
                }
            }
        }
    }
}
