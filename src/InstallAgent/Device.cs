using PInvoke;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XSToolsInstallation
{
    public static class Device
    {
        // Use to create a list of strings from the
        // "byte[] propertyBuffer" variable returned
        // by SetupDiGetDeviceRegistryProperty()
        private static string[] MultiByteStringSplit(byte[] mbstr)
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

            return strList.ToArray();
        }

        // Returns an array of all the Hardware ID strings
        // available for an SP_DEVINFO_DATA object
        public static string[] GetHardwareIDs(
            SetupApi.DeviceInfoSet devInfoSet,
            SetupApi.SP_DEVINFO_DATA devInfoData)
        {
            uint propertyRegDataType = 0;
            uint requiredSize = 0;

            // 'buffer' is 4KB  but Unicode chars are 2 bytes,
            // hence 'buffer' can hold up to 2K chars
            const uint BUFFER_SIZE = 4096;
            byte[] buffer = new byte[BUFFER_SIZE];

            // Get the device's HardwareID multistring
            SetupApi.SetupDiGetDeviceRegistryProperty(
                devInfoSet.Get(),
                ref devInfoData,
                SetupApi.SetupDiGetDeviceRegistryPropertyEnum.SPDRP_HARDWAREID,
                out propertyRegDataType,
                buffer,
                BUFFER_SIZE,
                out requiredSize
            );

            return MultiByteStringSplit(buffer);
        }

        // The function takes as input an initialized 'deviceInfoSet' object,
        // a hardware ID string we want to search the system for and a
        // reference to an 'SP_DEVINFO_DATA' object. If 'strictSearch' is true,
        // the device needs to exactly match the hwID to be returned. Otherwise,
        // the device's name needs to start with the supplied hwID string.
        // When the function returns, if 'hwID' exists in the system,
        // 'devInfoData' will be populated. To test for the device's existence,
        // check 'devInfoData.cbSize'; if 0, the device doesn't exist. The
        // initialized 'devInfoData' object can be used with any function that
        // takes an 'SP_DEVINFO_DATA' object as input.
        public static int FindInSystem(
            out SetupApi.SP_DEVINFO_DATA devInfoData,
            string hwID,
            SetupApi.DeviceInfoSet devInfoSet,
            bool strictSearch)
        {
            devInfoData = new SetupApi.SP_DEVINFO_DATA();
            devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

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
                 SetupApi.SetupDiEnumDeviceInfo(
                     devInfoSet.Get(),
                     i,
                     ref devInfoData);
                 ++i)
            {
                foreach (string id in GetHardwareIDs(devInfoSet, devInfoData))
                {
                    if (hwIDFound(id, hwID))
                    {
                        goto Exit; // to break out of 2 loops
                    }
                }
            }

            // Return to 0 if we don't find the device
            devInfoData.cbSize = 0;

        Exit:
            return Marshal.GetLastWin32Error();
        }

        public static void RemoveFromSystem(string[] hwIDs, bool strictSearch)
        {
            using (SetupApi.DeviceInfoSet devInfoSet =
                       new SetupApi.DeviceInfoSet(
                       IntPtr.Zero,
                       IntPtr.Zero,
                       IntPtr.Zero,
                       SetupApi.DiGetClassFlags.DIGCF_ALLCLASSES))
            {
                if (!devInfoSet.HandleIsValid())
                {
                    return;
                }

                for (int i = 0; i < hwIDs.Length; ++i)
                {
                    SetupApi.SP_DEVINFO_DATA devInfoData;

                    FindInSystem(
                        out devInfoData,
                        hwIDs[i],
                        devInfoSet,
                        strictSearch
                    );

                    if (devInfoData.cbSize == 0)
                    {
                        continue;
                    }

                    Trace.WriteLine("Trying to remove " + hwIDs[i]);
                    SetupApi.REMOVE_PARAMS rparams =
                        new SetupApi.REMOVE_PARAMS();
                    rparams.cbSize = 8; // Size of cbSide & InstallFunction
                    rparams.InstallFunction =
                        (uint)SetupApi.InstallFunctions.DIF_REMOVE;
                    rparams.HwProfile = 0;
                    rparams.Scope = SetupApi.DI_REMOVE_DEVICE_GLOBAL;
                    GCHandle handle1 = GCHandle.Alloc(rparams);

                    if (!SetupApi.SetupDiSetClassInstallParams(
                            devInfoSet.Get(),
                            ref devInfoData,
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

                    if (!SetupApi.SetupDiCallClassInstaller(
                            SetupApi.InstallFunctions.DIF_REMOVE,
                            devInfoSet.Get(),
                            ref devInfoData))
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

        public static bool ChildrenInstalled(string enumName)
        {
            UInt32 devStatus;
            UInt32 devProblemCode;

            SetupApi.SP_DEVINFO_DATA devInfoData =
                new SetupApi.SP_DEVINFO_DATA();
            devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

            using (SetupApi.DeviceInfoSet devInfoSet =
                       new SetupApi.DeviceInfoSet(
                       IntPtr.Zero,
                       enumName,
                       IntPtr.Zero,
                       SetupApi.DiGetClassFlags.DIGCF_PRESENT |
                       SetupApi.DiGetClassFlags.DIGCF_ALLCLASSES))
            {
                for (uint i = 0;
                     SetupApi.SetupDiEnumDeviceInfo(
                         devInfoSet.Get(),
                         i,
                         ref devInfoData);
                     ++i)
                {
                    SetupApi.CM_Get_DevNode_Status(
                        out devStatus,
                        out devProblemCode,
                        devInfoData.devInst,
                        0
                    );

                    if ((devStatus & (uint)SetupApi.DNFlags.DN_STARTED) == 0)
                    {
                        Trace.WriteLine(
                            enumName +
                            " child not started " +
                            devStatus.ToString()
                        );

                        return false;
                    }
                }
            }
            return true;
        }
    }
}
