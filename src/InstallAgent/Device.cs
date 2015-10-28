using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace XSToolsInstallation
{
    public static class Device
    {
        // Use to create a list of strings from the
        // "byte[] propertyBuffer" variable returned
        // by SetupDiGetDeviceRegistryProperty()
        private static List<string> MultiByteStringSplit(byte[] mbstr)
        {
            List<string> strList = new List<string>();
            int strStart = 0;

            // One character is represented by 2 bytes.
            for (int i = 0; i < mbstr.Length; i += 2)
            {
                if (mbstr[i] == '\0')
                {
                    strList.Add(
                        System.Text.Encoding.Unicode.GetString(
                            mbstr,
                            strStart,
                            i - strStart
                        )
                    );

                    strStart = i + 2;

                    if (strStart < mbstr.Length && mbstr[strStart] == '\0')
                    {
                        break;
                    }
                }
            }

            return strList;
        }

        // The function takes as input an initialized 'deviceInfoSet' object,
        // a string array with the hardware IDs we want to search the system
        // for and an 'SP_DEVINFO_DATA' array of the same size as 'hwIDs'.
        // if 'strictSearch' is true, a device needs to exactly match one
        // of the supplied hwIDs to be returned. Otherwise, the device's
        // name needs to start with one of the supplied hwIDs.
        // When the function returns, if 'hwIDs[i]' exists in the system,
        // 'devices[i]' will be populated. To test for the device's existence,
        // check 'devices[i].cbSize'; if 0, the device doesn't exist.
        // Initialized 'devices[i]' objects can be used with any function
        // that takes an 'SP_DEVINFO_DATA' object as input.
        public static int FindInSystem(
            ref PInvoke.SetupApi.SP_DEVINFO_DATA[] devices,
            string[] hwIDs,
            PInvoke.SetupApi.DeviceInfoSet devInfoSet,
            bool strictSearch)
        {
            if (devices.Length != hwIDs.Length)
            {
                throw new Exception(
                    String.Format(
                        "devices.Length != hwIDs.Length: {0} != {1}",
                        devices.Length,
                        hwIDs.Length
                    )
                );
            }

            uint propertyRegDataType = 0;
            uint requiredSize = 0;
            int j;

            // 'buffer' is 4KB  but Unicode chars are 2 bytes,
            // hence 'buffer' can hold up to 2K chars
            const uint BUFFER_SIZE = 4096;
            byte[] buffer = new byte[BUFFER_SIZE];

            PInvoke.SetupApi.SP_DEVINFO_DATA tmpDevInfoData =
                new PInvoke.SetupApi.SP_DEVINFO_DATA();
            tmpDevInfoData.cbSize = (uint)Marshal.SizeOf(tmpDevInfoData);

            // Select which string comparison function
            // to use, depending on 'strictSearch'
            Func<string, string, bool> hwIDFound;
            if (strictSearch)
            {
                hwIDFound = (string _enumID, string _hwID) =>
                    _enumID.Equals(_hwID, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                hwIDFound = (string _enumID, string _hwID) =>
                    _enumID.StartsWith(_hwID, StringComparison.OrdinalIgnoreCase);
            }

            for (uint i = 0;
                 PInvoke.SetupApi.SetupDiEnumDeviceInfo(
                     devInfoSet.Get(),
                     i,
                     ref tmpDevInfoData);
                 ++i)
            {
                // Get the device's HardwareID multistring
                PInvoke.SetupApi.SetupDiGetDeviceRegistryProperty(
                    devInfoSet.Get(),
                    ref tmpDevInfoData,
                    PInvoke.SetupApi.SetupDiGetDeviceRegistryPropertyEnum.SPDRP_HARDWAREID,
                    out propertyRegDataType,
                    buffer,
                    BUFFER_SIZE,
                    out requiredSize
                );

                foreach (string id in MultiByteStringSplit(buffer))
                {
                    // if 'id' exists in hwIDs, return its index
                    if ((j = Array.FindIndex(hwIDs, hwid => hwIDFound(id, hwid))) != -1)
                    {
                        devices[j] = tmpDevInfoData;
                        tmpDevInfoData = new PInvoke.SetupApi.SP_DEVINFO_DATA();
                        tmpDevInfoData.cbSize = (uint)Marshal.SizeOf(tmpDevInfoData);
                        break;
                    }
                }
            }

            return Marshal.GetLastWin32Error();
        }

        public static void RemoveFromSystem(string[] hwIDs, bool strictSearch)
        {
            using (PInvoke.SetupApi.DeviceInfoSet devInfoSet =
                       new PInvoke.SetupApi.DeviceInfoSet(
                       IntPtr.Zero,
                       IntPtr.Zero,
                       IntPtr.Zero,
                       (uint)PInvoke.SetupApi.DiGetClassFlags.DIGCF_ALLCLASSES))
            {
                if (!devInfoSet.HandleIsValid())
                {
                    return;
                }

                PInvoke.SetupApi.SP_DEVINFO_DATA[] devices =
                    new PInvoke.SetupApi.SP_DEVINFO_DATA[hwIDs.Length];

                FindInSystem(ref devices, hwIDs, devInfoSet, strictSearch);

                for (int i = 0; i < devices.Length; ++i)
                {
                    if (devices[i].cbSize == 0)
                    {
                        continue;
                    }

                    Trace.WriteLine("Trying to remove " + hwIDs[i]);
                    PInvoke.SetupApi.REMOVE_PARAMS rparams =
                        new PInvoke.SetupApi.REMOVE_PARAMS();
                    rparams.cbSize = 8; // Size of cbSide & InstallFunction
                    rparams.InstallFunction = PInvoke.SetupApi.DIF_REMOVE;
                    rparams.HwProfile = 0;
                    rparams.Scope = PInvoke.SetupApi.DI_REMOVE_DEVICE_GLOBAL;
                    GCHandle handle1 = GCHandle.Alloc(rparams);

                    if (!PInvoke.SetupApi.SetupDiSetClassInstallParams(
                            devInfoSet.Get(),
                            ref devices[i],
                            ref rparams,
                            Marshal.SizeOf(rparams)))
                    {
                        throw new Exception(
                            String.Format(
                                "Unable to set class install params " +
                                "for hardware device: {0}",
                                hwIDs[i]
                            )
                        );
                    }

                    if (!PInvoke.SetupApi.SetupDiCallClassInstaller(
                            PInvoke.SetupApi.DIF_REMOVE,
                            devInfoSet.Get(),
                            ref devices[i]))
                    {
                        throw new Exception(
                            String.Format(
                                "Unable to call class installer " +
                                "for hardware device: {0}",
                                hwIDs[i]
                            )
                        );
                    }

                    Trace.WriteLine("Remove should have worked");
                }
            }
        }
    }
}
