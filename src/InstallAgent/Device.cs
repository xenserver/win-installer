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
                devInfoData,
                SetupApi.SetupDiGetDeviceRegistryPropertyEnum.SPDRP_HARDWAREID,
                out propertyRegDataType,
                buffer,
                BUFFER_SIZE,
                out requiredSize
            );

            return MultiByteStringSplit(buffer);
        }

        public static SetupApi.SP_DEVINFO_DATA FindInSystem(
            string hwID,
            SetupApi.DeviceInfoSet devInfoSet,
            bool strictSearch)
        // The function takes as input an initialized 'deviceInfoSet'
        // object and a hardware ID string we want to search the system
        // for. If 'strictSearch' is true, the device needs to exactly
        // match the hwID to be returned. Otherwise, the device's name
        // needs to start with the supplied hwID string. If the device
        // is found, a fully initialized 'SP_DEVINFO_DATA' object is
        // returned. If not, the function returns 'null'.
        {
            SetupApi.SP_DEVINFO_DATA devInfoData =
                new SetupApi.SP_DEVINFO_DATA();
            devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

            // Select which string comparison function
            // to use, depending on 'strictSearch'
            Func<string, string, bool> hwIDFound;
            if (strictSearch)
            {
                hwIDFound = (string _enumID, string _hwID) =>
                    _enumID.Equals(
                        _hwID,
                        StringComparison.OrdinalIgnoreCase
                    );
            }
            else
            {
                hwIDFound = (string _enumID, string _hwID) =>
                    _enumID.StartsWith(
                        _hwID,
                        StringComparison.OrdinalIgnoreCase
                    );
            }

            Trace.WriteLine(
                "Searching system for device: \'" + hwID +
                "\'; (strict search: \'" + strictSearch + "\')"
            );

            for (uint i = 0;
                 SetupApi.SetupDiEnumDeviceInfo(
                     devInfoSet.Get(),
                     i,
                     devInfoData);
                 ++i)
            {
                foreach (string id in GetHardwareIDs(devInfoSet, devInfoData))
                {
                    if (hwIDFound(id, hwID))
                    {
                        Trace.WriteLine("Device found");
                        return devInfoData;
                    }
                }
            }

            Win32Error.Set("SetupDiEnumDeviceInfo");
            if (Win32Error.GetErrorNo() == 259) // ERROR_NO_MORE_ITEMS
            {
                Trace.WriteLine("Device not found");
                return null;
            }

            Trace.WriteLine(Win32Error.GetFullErrMsg());
            throw new Exception(Win32Error.GetFullErrMsg());
        }

        public static void RemoveFromSystem(
            SetupApi.DeviceInfoSet devInfoSet,
            string hwID,
            bool strictSearch)
        {
            if (!devInfoSet.HandleIsValid())
            {
                return;
            }

            SetupApi.SP_DEVINFO_DATA devInfoData;

            devInfoData = FindInSystem(
                hwID,
                devInfoSet,
                strictSearch
            );

            if (devInfoData == null)
            {
                return;
            }

            SetupApi.SP_REMOVEDEVICE_PARAMS rparams =
                new SetupApi.SP_REMOVEDEVICE_PARAMS();

            rparams.ClassInstallHeader.cbSize =
                (uint)Marshal.SizeOf(rparams.ClassInstallHeader);

            rparams.ClassInstallHeader.InstallFunction =
                SetupApi.DI_FUNCTION.DIF_REMOVE;

            rparams.HwProfile = 0;
            rparams.Scope = SetupApi.DI_REMOVE_DEVICE_GLOBAL;
            GCHandle handle1 = GCHandle.Alloc(rparams);

            if (!SetupApi.SetupDiSetClassInstallParams(
                    devInfoSet.Get(),
                    devInfoData,
                    ref rparams,
                    Marshal.SizeOf(rparams)))
            {
                Win32Error.Set("SetupDiSetClassInstallParams");
                Trace.WriteLine(Win32Error.GetFullErrMsg());

                // TODO: write custom exception
                throw new Exception(
                    Win32Error.GetFullErrMsg()
                );
            }

            Trace.WriteLine(
                "Removing device \'" + hwID +
                "\' from system"
            );

            if (!SetupApi.SetupDiCallClassInstaller(
                    SetupApi.DI_FUNCTION.DIF_REMOVE,
                    devInfoSet.Get(),
                    devInfoData))
            {
                Win32Error.Set("SetupDiCallClassInstaller");
                Trace.WriteLine(Win32Error.GetFullErrMsg());

                // TODO: write custom exception
                throw new Exception(
                    Win32Error.GetFullErrMsg()
                );
            }

            Trace.WriteLine("Remove should have worked");
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
                         devInfoData);
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
