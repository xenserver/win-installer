using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace PInvoke
{
    public static class HardwareDevice
    {
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public static void Remove(string HardwareId)
        {
            IntPtr DeviceInfoSet = PInvoke.SetupApi.SetupDiGetClassDevs(
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                (uint)PInvoke.SetupApi.DiGetClassFlags.DIGCF_ALLCLASSES
            );

            if (DeviceInfoSet == INVALID_HANDLE_VALUE)
            {
                return;
            }

            uint index = 0;
            PInvoke.SetupApi.SP_DEVINFO_DATA DeviceInfoData =
                new PInvoke.SetupApi.SP_DEVINFO_DATA();
            const uint BUFFER_SIZE = 4096;
            ushort[] buffer = new ushort[BUFFER_SIZE];
            DeviceInfoData.cbSize = (uint)Marshal.SizeOf(DeviceInfoData);

            while (PInvoke.SetupApi.SetupDiEnumDeviceInfo(
                DeviceInfoSet, index, ref DeviceInfoData
            ))
            {
                PInvoke.SetupApi.SetupDiGetDeviceRegistryProperty(
                    DeviceInfoSet,
                    ref DeviceInfoData,
                    PInvoke.SetupApi.SetupDiGetDeviceRegistryPropertyEnum.SPDRP_HARDWAREID,
                    IntPtr.Zero,
                    buffer,
                    BUFFER_SIZE * 2,
                    IntPtr.Zero
                );

                int start = 0;
                int offset = 0;
                while (start < buffer.Length)
                {
                    while ((offset < buffer.Length) && (buffer[offset] != 0))
                    {
                        offset++;
                    }

                    if (offset < buffer.Length)
                    {
                        if (start == offset)
                        {
                            break;
                        }

                        byte[] block = new byte[(offset - start + 1) * 2];
                        Buffer.BlockCopy(
                            buffer,
                            start * 2,
                            block,
                            0,
                            (offset - start + 1) * 2
                        );

                        string id = System.Text.Encoding.Unicode.GetString(
                            block,
                            0,
                            (offset - start) * 2
                        );

                        /*Trace.WriteLine(
                            "Examinining id " + id.ToUpper() +
                            " vs " + HardwareId.ToUpper()
                        );*/

                        if (id.ToUpper().Equals(HardwareId.ToUpper()))
                        {
                            //Trace.WriteLine("Trying to remove " + HardwareId.ToUpper());
                            PInvoke.SetupApi.REMOVE_PARAMS rparams =
                                new PInvoke.SetupApi.REMOVE_PARAMS();
                            rparams.cbSize = 8; // Size of cbSide & InstallFunction
                            rparams.InstallFunction = PInvoke.SetupApi.DIF_REMOVE;
                            rparams.HwProfile = 0;
                            rparams.Scope = PInvoke.SetupApi.DI_REMOVE_DEVICE_GLOBAL;
                            GCHandle handle1 = GCHandle.Alloc(rparams);

                            if (!PInvoke.SetupApi.SetupDiSetClassInstallParams(
                                    DeviceInfoSet,
                                    ref DeviceInfoData,
                                    ref rparams,
                                    Marshal.SizeOf(rparams)
                                ))
                            {
                                throw new Exception("Unable to set class install params");
                            }

                            if (!PInvoke.SetupApi.SetupDiCallClassInstaller(
                                    PInvoke.SetupApi.DIF_REMOVE,
                                    DeviceInfoSet,
                                    ref DeviceInfoData
                                ))
                            {
                                throw new Exception("Unable to call class installer");
                            }

                            //Trace.WriteLine("Remove should have worked");
                        }
                    }
                    else
                    {
                        break;
                    }

                    offset++;
                    start = offset;
                }
                index++;
            }
        }
    }
}
