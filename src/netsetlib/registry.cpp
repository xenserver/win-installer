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

#define INITGUID

#include "registry.h"
#include "ethernet.h"

static FORCEINLINE PTCHAR
__GetErrorMessage(
    IN  DWORD   Error
    )
{
    PTCHAR      Message;
    ULONG       Index;

    FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | 
                  FORMAT_MESSAGE_FROM_SYSTEM |
                  FORMAT_MESSAGE_IGNORE_INSERTS,
                  NULL,
                  Error,
                  MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
                  (LPTSTR)&Message,
                  0,
                  NULL);

    for (Index = 0; Message[Index] != '\0'; Index++) {
        if (Message[Index] == '\r' || Message[Index] == '\n') {
            Message[Index] = '\0';
            break;
        }
    }

    return Message;
}



HRESULT
RegistryGetNetLuid(
	HKEY SourceKey,
	NET_LUID* NetLuid
	)
{
	HRESULT     Error;
    DWORD       MaxValueLength;
    DWORD       ValueLength;
    LPDWORD     Value;
    DWORD       Type;

    memset(NetLuid, 0, sizeof (NetLuid));

    Error = RegQueryInfoKey(SourceKey,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
							&MaxValueLength,
                            NULL,
                            NULL);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail1;
    }

    ValueLength = MaxValueLength;

    Value = (LPDWORD)calloc(1, ValueLength);
    if (Value == NULL)
        goto fail2;

    Error = RegQueryValueEx(SourceKey,
                            "NetLuidIndex",
                            NULL,
                            &Type,
                            (LPBYTE)Value,
                            &ValueLength);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail3;
    }

    if (Type != REG_DWORD) {
        SetLastError(ERROR_BAD_FORMAT);
        goto fail4;
    }

    NetLuid->Info.NetLuidIndex = *Value;

    Error = RegQueryValueEx(SourceKey,
                            "*IfType",
                            NULL,
                            &Type,
                            (LPBYTE)Value,
                            &ValueLength);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail5;
    }

    if (Type != REG_DWORD) {
        SetLastError(ERROR_BAD_FORMAT);
        goto fail6;
    }

    NetLuid->Info.IfType = *Value;

	return ERROR_SUCCESS;

fail6:
    Log("fail6");

fail5:
    Log("fail5");

fail4:
    Log("fail4");

fail3:
    Log("fail3");

    free(Value);

fail2:
    Log("fail2");

fail1:
    Error = GetLastError();

    {
        PTCHAR  Message;

        Message = __GetErrorMessage(Error);
        Log("fail1 (%s)", Message);
        LocalFree(Message);
    }

    return Error;

}




HRESULT
getXenNetDeviceEnumKey(
	int		deviceIndex,
	HKEY*	XenNetDeviceEnumKey
	) 
{
	HRESULT		Error;
	HKEY		XenNetServiceEnumeration;
	CHAR		InBuffer[MAXIMUM_BUFFER_SIZE];
	CHAR		OutBuffer[MAXIMUM_BUFFER_SIZE];
	CHAR		EnumKeyBuffer[MAXIMUM_BUFFER_SIZE];
	DWORD		Type;
	DWORD		OutBufferLength;
	
	Error = ERROR_SUCCESS;

	Error = RegOpenKey(HKEY_LOCAL_MACHINE, SERVICE_KEY(XenNet) "\\Enum", &XenNetServiceEnumeration);
	if (Error != ERROR_SUCCESS) {
		Log("Unable to open xennet service enumeration key");
		SetLastError(Error);
		goto fail1;
	}

	sprintf_s(InBuffer, MAXIMUM_BUFFER_SIZE, "%d", deviceIndex);

	OutBufferLength = MAXIMUM_BUFFER_SIZE;

	Error = RegQueryValueEx(XenNetServiceEnumeration, InBuffer, NULL, &Type, (LPBYTE)OutBuffer, &OutBufferLength);
	if (Error != ERROR_SUCCESS) {
		Log("Unable to read key for device %d", deviceIndex);
		SetLastError(Error);
		goto fail2;
	}
	if (Type != REG_SZ) {
		Log("Key for device index %d is not a SZ", deviceIndex);
		SetLastError(ERROR_BAD_FORMAT);
		goto fail3;
	}

	sprintf_s(EnumKeyBuffer, MAXIMUM_BUFFER_SIZE, ENUM_KEY "\\%s", OutBuffer); 

	Error = RegOpenKey(HKEY_LOCAL_MACHINE, EnumKeyBuffer, XenNetDeviceEnumKey);
	if (Error != ERROR_SUCCESS) {
		Log("Unable to open key %s", EnumKeyBuffer);
		SetLastError(Error);
		goto fail4;
	}

	RegCloseKey(XenNetServiceEnumeration);

	return Error;

fail4:
fail3:
fail2:
	RegCloseKey(XenNetServiceEnumeration);
fail1:
	Fail(Error);
	return Error;
}

DWORD
RegistryGetXenNetCount(
	void
	)
{
	HRESULT		Error;
	HKEY		XenNetServiceEnumeration;
	DWORD		OutBuffer;
	DWORD		Type;
	DWORD		OutBufferLength;

	Error = ERROR_SUCCESS;

	Error = RegOpenKey(HKEY_LOCAL_MACHINE, SERVICE_KEY(XenNet) "\\Enum", &XenNetServiceEnumeration);
	if (Error != ERROR_SUCCESS) {
		Log("Unable to open xennet service enumeration key");
		SetLastError(Error);
		goto fail1;
	}

	OutBufferLength = sizeof(OutBuffer);

	Error = RegQueryValueEx(XenNetServiceEnumeration, "Count", NULL, &Type, (LPBYTE)&OutBuffer, &OutBufferLength);
	if (Error != ERROR_SUCCESS) {
		Log("Unable to read xennet enumeration count");
		SetLastError(Error);
		goto fail2;
	}
	if (Type != REG_DWORD) {
		Log("Key for xennet enumeration count is not a DWORD");
		Error = ERROR_BAD_FORMAT;
		SetLastError(ERROR_BAD_FORMAT);
		goto fail3;
	}

	return OutBuffer;

fail3:
fail2:

fail1:
	Warning("Xennet not found %d",Error);
	return -1;
}

HRESULT
RegistryGetXenNetSoftwareKey(
	int		deviceIndex,
	HKEY*	XenNetSoftwareKey
)
{
	HRESULT		Error;
	HKEY		XenNetDeviceEnumKey;
	CHAR		SoftwareKeyName[MAXIMUM_BUFFER_SIZE];
	DWORD		SoftwareKeyNameLength;
	DWORD		Type;
	CHAR		FullSoftwareKeyName[MAXIMUM_BUFFER_SIZE];

	Error = ERROR_SUCCESS;

	Error = getXenNetDeviceEnumKey(deviceIndex, &XenNetDeviceEnumKey);
	if (Error != ERROR_SUCCESS) {
		Log("Unable to open xennet device enum key");
		SetLastError(Error);
		goto fail1;
	}
	
	SoftwareKeyNameLength = MAXIMUM_BUFFER_SIZE;

	Error = RegQueryValueEx(XenNetDeviceEnumKey, "Driver", NULL, &Type, (LPBYTE)SoftwareKeyName, &SoftwareKeyNameLength);
	if (Error != ERROR_SUCCESS) {
		Log("Unable to read software key location key for device %d", deviceIndex);
		SetLastError(Error);
		goto fail2;
	}
	if (Type != REG_SZ) {
		Log("Software key location for device index %d is not a SZ", deviceIndex);
		SetLastError(ERROR_BAD_FORMAT);
		goto fail3;
	}
	
	sprintf_s(FullSoftwareKeyName, MAXIMUM_BUFFER_SIZE, CLASS_KEY "\\%s", SoftwareKeyName);
	Log("Opening key %s",FullSoftwareKeyName);

	Error = RegOpenKey(HKEY_LOCAL_MACHINE, FullSoftwareKeyName, XenNetSoftwareKey);
	if (Error !=ERROR_SUCCESS) {
		Log("Unable to open device %d software key %s", deviceIndex, FullSoftwareKeyName);
		SetLastError(Error);
		goto fail4;
	}

	return Error;
fail4:
fail3:
fail2:
	RegCloseKey(XenNetDeviceEnumKey);
fail1:
	Fail(Error);
	return Error;
}

PTCHAR
RegistryGetInterfaceName( 
	HKEY SourceKey 
) 
{
	HRESULT     Error;
    HKEY        LinkageKey;
    DWORD       MaxValueLength;
    DWORD       RootDeviceLength;
    PTCHAR      RootDevice;
    DWORD       Type;

    Error = RegOpenKeyEx(SourceKey,
                         "Linkage",
                         0,
                         KEY_READ,
                         &LinkageKey);
    if (Error != ERROR_SUCCESS) {
		Warning("Cannot find Linkage");
        SetLastError(Error);
        goto fail1;
    }

    Error = RegQueryInfoKey(LinkageKey,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            &MaxValueLength,
                            NULL,
                            NULL);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail2;
    }

    RootDeviceLength = MaxValueLength + sizeof (TCHAR);

    RootDevice = (PTCHAR) calloc(1, RootDeviceLength);
    if (RootDevice == NULL)
        goto fail2;

    Error = RegQueryValueEx(LinkageKey,
                            "RootDevice",
                            NULL,
                            &Type,
                            (LPBYTE)RootDevice,
                            &RootDeviceLength);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail3;
    }

    Error = RegQueryValueEx(LinkageKey,
                            "RootDevice",
                            NULL,
                            &Type,
                            (LPBYTE)RootDevice,
                            &RootDeviceLength);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail3;
    }

    if (Type != REG_MULTI_SZ) {
        SetLastError(ERROR_BAD_FORMAT);
        goto fail4;
    }

    Log("%s", RootDevice);

    RegCloseKey(LinkageKey);

    return RootDevice;

fail4:
    Log("fail4");

fail3:
    Log("fail3");

    free(RootDevice);

fail2:
    Log("fail2");

    RegCloseKey(LinkageKey);

fail1:
    Error = GetLastError();

    {
        PTCHAR  Message;

        Message = __GetErrorMessage(Error);
        Log("fail1 (%s)", Message);
        LocalFree(Message);
    }

    return NULL;
}

void macToString(
	ETHERNET_ADDRESS mac, 
	TCHAR string[6*2+1]
	)
{
	sprintf_s(string, 6*2+1, "%02X%02X%02X%02X%02X%02X",
		mac.Byte[0],
		mac.Byte[1],
		mac.Byte[2],
		mac.Byte[3],
		mac.Byte[4],
		mac.Byte[5]);
}

static HRESULT 
getMac(
	NET_LUID NetLuid, 
	ETHERNET_ADDRESS *Mac
	)
{
	PMIB_IF_TABLE2		Table;
	DWORD				Index;
	PMIB_IF_ROW2		Row;
	HRESULT				Error;

	Error = GetIfTable2(&Table);
	if (Error != ERROR_SUCCESS) {
		Log("Unable to read  If table");
		goto fail1;
	}

	for (Index = 0; Index < Table->NumEntries; Index++) {
		Log("getMac: Index %d, Luid %x",Index, NetLuid.Value);
		Row = &Table->Table[Index];
		Log(" check %x",Row->InterfaceLuid.Value);
		if (Row->InterfaceLuid.Value != NetLuid.Value)
			continue;
		if (Row->PhysicalAddressLength != sizeof(ETHERNET_ADDRESS)){
			Log("Address length %d",Row->PhysicalAddressLength);
			continue;
		}
		memcpy(Mac, Row->PermanentPhysicalAddress, sizeof(ETHERNET_ADDRESS));
		Log(" Found %02x%02x%02x%02x%02x%02x",
				Row->PermanentPhysicalAddress[0],
				Row->PermanentPhysicalAddress[1],
				Row->PermanentPhysicalAddress[2],
				Row->PermanentPhysicalAddress[3],
				Row->PermanentPhysicalAddress[4],
				Row->PermanentPhysicalAddress[5]);
		return ERROR_SUCCESS;
	}

	Log("Unable to find adapter for NetLuid %x", NetLuid.Value);
	Error = ERROR_FILE_NOT_FOUND;

fail1:
	Fail(Error);
	return Error;
}

PTCHAR
RegistryGetStorageKeyOverrideName(
	int deviceIndex)
{
	TCHAR number[32];
	PTCHAR StorageKeyName;
	sprintf_s(number,32,"%d",deviceIndex);
	DWORD NameLength = (DWORD)(strlen(INSTALLER_KEY_OVERRIDE)+
					strlen("\\")+
					strlen(number)+
					1);

	StorageKeyName = (PTCHAR) calloc(sizeof(TCHAR),NameLength);
	if (StorageKeyName == NULL) {
		Log("Unable to allocate memory for destination registry name");
		goto fail1;
	}

	StringCbPrintf(StorageKeyName, NameLength, "%s\\%d",INSTALLER_KEY_OVERRIDE, deviceIndex);

	return StorageKeyName;

fail1:
	Fail(-1);
	return NULL;
}

PTCHAR
RegistryGetStorageKeyName( 
	HKEY	SourceKey,
	PTCHAR	StorageBaseKeyName
)
{
	NET_LUID			NetLuid;
	HRESULT				Error;

	ETHERNET_ADDRESS	EthAddr;
	TCHAR				MacString[13];
	PTCHAR				StorageKeyName;
	DWORD				NameLength;

	Error = RegistryGetNetLuid(SourceKey, &NetLuid);
	if (Error != ERROR_SUCCESS) {
		Log("Unable to find NetLuid");
		goto fail1;
	}

	Error = getMac(NetLuid, &EthAddr);
	if (Error != ERROR_SUCCESS) {
		Log("Unable to find Mac");
		goto fail2;
	}

	macToString(EthAddr, MacString);
		
	NameLength = (DWORD)(strlen(StorageBaseKeyName)+
					strlen("\\")+
					strlen(MacString)+
					1);
		
	StorageKeyName = (PTCHAR) calloc(sizeof(TCHAR),NameLength);
	if (StorageKeyName == NULL) {
		Log("Unable to allocate memory for destination registry name");
		goto fail3;
	}


	StringCbPrintf(StorageKeyName, NameLength, "%s\\%s",StorageBaseKeyName, MacString);
	
	return StorageKeyName;

fail3:
fail2:
fail1:
	Fail(Error);
	return NULL;

}

static BOOLEAN
CopyKeyValues(
    IN  HKEY    DestinationKey,
    IN  HKEY    SourceKey
    )
{
    HRESULT     Error;
    DWORD       Values;
    DWORD       MaxNameLength;
    PTCHAR      Name;
    DWORD       MaxValueLength;
    LPBYTE      Value;
    DWORD       Index;

    Log("====>");

    Error = RegQueryInfoKey(SourceKey,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            &Values,
                            &MaxNameLength,
                            &MaxValueLength,
                            NULL,
                            NULL);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail1;
    }

    Log("%d VALUES", Values);

    if (Values == 0)
        goto done;

    MaxNameLength += sizeof (TCHAR);

    Name = (PTCHAR) malloc(MaxNameLength);
    if (Name == NULL)
        goto fail2;

    Value = (LPBYTE)malloc(MaxValueLength);
    if (Value == NULL)
        goto fail3;

    for (Index = 0; Index < Values; Index++) {
        DWORD   NameLength;
        DWORD   ValueLength;
        DWORD   Type;

        NameLength = MaxNameLength;
        memset(Name, 0, NameLength);

        ValueLength = MaxValueLength;
        memset(Value, 0, ValueLength);

        Error = RegEnumValue(SourceKey,
                             Index,
                             (LPTSTR)Name,
                             &NameLength,
                             NULL,
                             &Type,
                             Value,
                             &ValueLength);
        if (Error != ERROR_SUCCESS) {
            SetLastError(Error);
            goto fail4;
        }

        Error = RegSetValueEx(DestinationKey,
                              Name,
                              0,
                              Type,
                              Value,
                              ValueLength);
        if (Error != ERROR_SUCCESS) {
            SetLastError(Error);
            goto fail5;
        }

        Log("COPIED %s", Name);
    }

    free(Value);
    free(Name);

done:
    Log("<====");

    return TRUE;

fail5:
    Log("fail5");

fail4:
    Log("fail4");

    free(Value);

fail3:
    Log("fail3");

    free(Name);

fail2:
    Log("fail2");

fail1:
    Log("fail1");

    Error = GetLastError();

    {
        PTCHAR  Message;

        Message = __GetErrorMessage(Error);
        Log("fail1 (%s)", Message);
        LocalFree(Message);
    }

    return FALSE;

}
static BOOLEAN
CopyValues(
    IN  PTCHAR  DestinationKeyName,
    IN  PTCHAR  SourceKeyName
    )
{
    HRESULT     Error;
    HKEY        DestinationKey;
    HKEY        SourceKey;

    Log("====>");

    Log("DESTINATION: %s", DestinationKeyName);
    Log("SOURCE: %s", SourceKeyName);

    Error = RegCreateKeyEx(HKEY_LOCAL_MACHINE,
                           DestinationKeyName,
                           0,
                           NULL,
                           REG_OPTION_NON_VOLATILE,
                           KEY_ALL_ACCESS,
                           NULL,
                           &DestinationKey,
                           NULL);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail1;
    }

    Error = RegOpenKeyEx(HKEY_LOCAL_MACHINE,
                         SourceKeyName,
                         0,
                         KEY_ALL_ACCESS,
                         &SourceKey);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail2;
    }
    
    CopyKeyValues(DestinationKey, SourceKey);

    RegCloseKey(SourceKey);
    RegCloseKey(DestinationKey);

    Log("<====");

    return TRUE;

fail2:
    Log("fail2");

    RegCloseKey(DestinationKey);

fail1:
    Error = GetLastError();

    {
        PTCHAR  Message;

        Message = __GetErrorMessage(Error);
        Log("fail1 (%s)", Message);
        LocalFree(Message);
    }

    return FALSE;
}


BOOLEAN
RegistryStoreParameters( 
	PTCHAR StoreName,
	PTCHAR StoreSubKeyName,
	PTCHAR DevicePrefix,
	PTCHAR DeviceName
) 
{
    DWORD       Length;
    PTCHAR      DeviceKeyName;
    PTCHAR      StoreKeyName;
    HRESULT     Result;
    HRESULT     Error;
    BOOLEAN     Success;

    Log("====>");

    Length = (DWORD)((strlen(StoreName) + 
					  strlen("\\") +
					  strlen(StoreSubKeyName) +
                      1) * sizeof (TCHAR));

    StoreKeyName = (PTCHAR) calloc(1, Length);
    if (StoreKeyName == NULL)
        goto fail1;

    Result = StringCbPrintf(StoreKeyName,
                            Length,
                            "%s\\%s",
                            StoreName,
							StoreSubKeyName);
    if (!SUCCEEDED(Result)) {
        SetLastError(ERROR_BUFFER_OVERFLOW);
        goto fail2;
    }

    Length = (DWORD)((strlen(DevicePrefix) +
                      strlen(DeviceName) +
                      1) * sizeof (TCHAR));

    DeviceKeyName = (PTCHAR)calloc(1, Length);
    if (DeviceKeyName == NULL)
        goto fail3;

    Result = StringCbPrintf(DeviceKeyName,
                            Length,
                            "%s%s",
                            DevicePrefix,
                            DeviceName);
    if (!SUCCEEDED(Result)) {
        SetLastError(ERROR_BUFFER_OVERFLOW);
        goto fail4;
    }

    Success = CopyValues(StoreKeyName, DeviceKeyName);

    free(DeviceKeyName);
    free(StoreKeyName);

    Log("<====");

    return Success;

fail4:
    Log("fail4");

    free(DeviceKeyName);

fail3:
    Log("fail3");

fail2:
    Log("fail2");

    free(StoreKeyName);

fail1:
    Error = GetLastError();

    {
        PTCHAR  Message;

        Message = __GetErrorMessage(Error);
        Log("fail1 (%s)", Message);
        LocalFree(Message);
    }

    return FALSE;
}

BOOLEAN
RegistryRestoreParameters( 
	PTCHAR StoreName,
	PTCHAR StoreSubKeyName,
	PTCHAR DevicePrefix,
	PTCHAR DeviceName
) 
{
    DWORD       Length;
    PTCHAR      DeviceKeyName;
    PTCHAR      StoreKeyName;
    HRESULT     Result;
    HRESULT     Error;
    BOOLEAN     Success;

    Log("====>");

    Length = (DWORD)((strlen(StoreName) + 
					  strlen("\\") +
					  strlen(StoreSubKeyName) +
                      1) * sizeof (TCHAR));

    StoreKeyName = (PTCHAR) calloc(1, Length);
    if (StoreKeyName == NULL)
        goto fail1;

    Result = StringCbPrintf(StoreKeyName,
                            Length,
                            "%s\\%s",
                            StoreName,
							StoreSubKeyName);
    if (!SUCCEEDED(Result)) {
        SetLastError(ERROR_BUFFER_OVERFLOW);
        goto fail2;
    }

    Length = (DWORD)((strlen(DevicePrefix) +
                      strlen(DeviceName) +
                      1) * sizeof (TCHAR));

    DeviceKeyName = (PTCHAR)calloc(1, Length);
    if (DeviceKeyName == NULL)
        goto fail3;

    Result = StringCbPrintf(DeviceKeyName,
                            Length,
                            "%s%s",
                            DevicePrefix,
                            DeviceName);
    if (!SUCCEEDED(Result)) {
        SetLastError(ERROR_BUFFER_OVERFLOW);
        goto fail4;
    }

    Success = CopyValues(DeviceKeyName, StoreKeyName);

    free(DeviceKeyName);
    free(StoreKeyName);

    Log("<====");

    return Success;

fail4:
    Log("fail4");

    free(DeviceKeyName);

fail3:
    Log("fail3");

fail2:
    Log("fail2");

    free(StoreKeyName);

fail1:
    Error = GetLastError();

    {
        PTCHAR  Message;

        Message = __GetErrorMessage(Error);
        Log("fail1 (%s)", Message);
        LocalFree(Message);
    }

    return FALSE;
}


BOOLEAN
RegistryStoreIpVersion6Addresses(
    IN  PTCHAR  StoreKeyName,
    IN  PTCHAR  DeviceKeyName,
    IN  PTCHAR  StoreValueName,
    IN  PTCHAR  DeviceValueName
    )
{
    HKEY        StoreKey;
    HKEY        DeviceKey;
    HRESULT     Error;
    DWORD       Values;
    DWORD       MaxNameLength;
    PTCHAR      Name;
    DWORD       MaxValueLength;
    LPBYTE      Value;
    DWORD       Index;

    Log("STORE: %s\\%s", StoreKeyName, StoreValueName);
    Log("DEVICE: %s\\%s", DeviceKeyName, DeviceValueName);

    Error = RegCreateKeyEx(HKEY_LOCAL_MACHINE,
                           StoreKeyName,
                           0,
                           NULL,
                           REG_OPTION_NON_VOLATILE,
                           KEY_ALL_ACCESS,
                           NULL,
                           &StoreKey,
                           NULL);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail1;
    }

    Error = RegOpenKeyEx(HKEY_LOCAL_MACHINE,
                         DeviceKeyName,
                         0,
                         KEY_ALL_ACCESS,
                         &DeviceKey);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail2;
    }

    Error = RegQueryInfoKey(DeviceKey,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            &Values,
                            &MaxNameLength,
                            &MaxValueLength,
                            NULL,
                            NULL);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail3;
    }

	Log("Values is %d",Values);
    if (Values == 0)
        goto done;

    MaxNameLength += sizeof (TCHAR);

    Name = (PTCHAR)malloc(MaxNameLength);
    if (Name == NULL)
        goto fail4;

    Value = (LPBYTE)malloc(MaxValueLength);
    if (Value == NULL)
        goto fail5;

    for (Index = 0; Index < Values; Index++) {
        DWORD   NameLength;
        DWORD   ValueLength;
        DWORD   Type;

        NameLength = MaxNameLength;
        memset(Name, 0, NameLength);

        ValueLength = MaxValueLength;
        memset(Value, 0, ValueLength);

        Error = RegEnumValue(DeviceKey,
                             Index,
                             (LPTSTR)Name,
                             &NameLength,
                             NULL,
                             &Type,
                             Value,
                             &ValueLength);
        if (Error != ERROR_SUCCESS) {
            SetLastError(Error);
            goto fail6;
        }

        if (strncmp(Name, DeviceValueName, sizeof (ULONG64) * 2) != 0){
            Log("Ignoring %s ( %s )", Name, DeviceValueName);
            continue;
        }

 
        Error = RegSetValueEx(StoreKey,
                              Name,
                              0,
                              Type,
                              Value,
                              ValueLength);
        if (Error != ERROR_SUCCESS) {
            SetLastError(Error);
            goto fail7;
        }
    }

    free(Value);
    free(Name);

    RegCloseKey(DeviceKey);
    RegCloseKey(StoreKey);

done:

    return TRUE;

fail7:
    Log("fail7");

fail6:
    Log("fail6");

    free(Value);

fail5:
    Log("fail5");

    free(Name);

fail4:
    Log("fail4");

fail3:
    Log("fail3");

    RegCloseKey(DeviceKey);

fail2:
    Log("fail2");

    RegCloseKey(StoreKey);

fail1:
    Error = GetLastError();

    {
        PTCHAR  Message;

        Message = __GetErrorMessage(Error);
        Log("fail1 (%s)", Message);
        LocalFree(Message);
    }

    return FALSE;
}


BOOLEAN
RegistryRestoreIpVersion6Addresses(
    IN  PTCHAR  StoreKeyName,
    IN  PTCHAR  DeviceKeyName,
    IN  PTCHAR  StoreValueName,
    IN  PTCHAR  DeviceValueName
    )
{
    HKEY        StoreKey;
    HKEY        DeviceKey;
    HRESULT     Error;
    LPBYTE      Value;
	DWORD		ValueLength;
	DWORD		Type;
    Log("STORE: %s\\%s", StoreKeyName, StoreValueName);
    Log("DEVICE: %s\\%s", DeviceKeyName, DeviceValueName);

    Error = RegCreateKeyEx(HKEY_LOCAL_MACHINE,
                           StoreKeyName,
                           0,
                           NULL,
                           REG_OPTION_NON_VOLATILE,
                           KEY_ALL_ACCESS,
                           NULL,
                           &StoreKey,
                           NULL);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail1;
    }

    Error = RegOpenKeyEx(HKEY_LOCAL_MACHINE,
                         DeviceKeyName,
                         0,
                         KEY_ALL_ACCESS,
                         &DeviceKey);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
        goto fail2;
    }

	if (RegQueryValue(DeviceKey, DeviceValueName,NULL,NULL) == ERROR_SUCCESS)
	{
		RegDeleteValue(DeviceKey, DeviceValueName);
	}



	if (RegQueryValueEx(StoreKey, StoreValueName, NULL, &Type,NULL, &ValueLength) == ERROR_SUCCESS)
	{
		Value = (LPBYTE)malloc(ValueLength);
		if (Value == NULL)
			goto fail3;
		Error = RegQueryValueEx(StoreKey, StoreValueName, NULL, &Type, Value, &ValueLength);
		if (Error != ERROR_SUCCESS)
			goto fail4;
		Error = RegSetValueEx(DeviceKey, DeviceValueName, NULL, Type, Value, ValueLength);
		if (Error != ERROR_SUCCESS)
			goto fail5;
		free(Value);
	}
	RegCloseKey(StoreKey);
	RegCloseKey(DeviceKey);

	return ERROR_SUCCESS;

fail5:
fail4:
	free(Value);
fail3:
	RegCloseKey(DeviceKey);
fail2:
   RegCloseKey(StoreKey);
fail1:
    Error = GetLastError();

    {
        PTCHAR  Message;

        Message = __GetErrorMessage(Error);
        Log("fail1 (%s)", Message);
        LocalFree(Message);
    }

    return FALSE;
}
HRESULT RegistryIterateOverKeySubKeys(PTCHAR KeyName, SUBKEY_ITERATOR_CALLBACK callback, void *data)
{
	HRESULT					Error;
	HKEY					Key;
	DWORD					SubKeys;
	DWORD					MaxSubKeyLength;
	DWORD					Index;
	SUBKEY_ITERATOR_CALLBACK_DATA	cbargs;

	Error = RegOpenKeyEx(HKEY_LOCAL_MACHINE,
                         KeyName,
                         0,
                         KEY_ALL_ACCESS,
                         &Key);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
		Warning("Cannot open key %s", KeyName);
        goto fail1;
    }
    Error = RegQueryInfoKey(Key,
                            NULL,
                            NULL,
                            NULL,
                            &SubKeys,
                            &MaxSubKeyLength,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL);
    if (Error != ERROR_SUCCESS) {
		Warning("Cannot query key info");
        SetLastError(Error);
        goto fail2;
    }

	Log("SubKeys is %d",SubKeys);
    if (SubKeys == 0)
        goto done;

    MaxSubKeyLength += sizeof (TCHAR);

    cbargs.Name = (PTCHAR)malloc(MaxSubKeyLength);
    if (cbargs.Name == NULL) {
		Warning("Cannot allocated memory for name");
        goto fail3;
	}

    for (Index = 0; Index < SubKeys; Index++) {



        cbargs.NameLength = MaxSubKeyLength;
        memset(cbargs.Name, 0, cbargs.NameLength);

        Error = RegEnumKeyEx(Key,
                             Index,
                             (LPTSTR)cbargs.Name,
                             &cbargs.NameLength,
                             NULL,
                             NULL,
                             NULL,
                             NULL);
        if (Error != ERROR_SUCCESS) {
            SetLastError(Error);
			Warning("Cannot enumerate keys");
            goto fail4;
        }

		RegOpenKeyEx(Key, cbargs.Name, 0, KEY_ALL_ACCESS, &cbargs.Key);

		Error = callback(&cbargs, data);
		if (Error != ERROR_SUCCESS) {
			Warning("Callback failed");
			SetLastError(Error);
			goto fail5;
		}

		RegCloseKey(cbargs.Key);
	}

	
	free(cbargs.Name);
done:
	RegCloseKey(Key);
	return ERROR_SUCCESS;

fail5:


fail4:
	free(cbargs.Name);
fail3:
fail2:
	RegCloseKey(Key);
fail1:
	

    Error = GetLastError();

    {
        PTCHAR  Message;

        Message = __GetErrorMessage(Error);
        Log("fail1 (%s)", Message);
        LocalFree(Message);
    }
	return Error;
}


HRESULT RegistryIterateOverKeyValues(PTCHAR KeyName, ITERATOR_CALLBACK callback, void *data)
{
	HRESULT Error;
	HKEY Key;

	DWORD Values;

	DWORD MaxNameLength;
	DWORD MaxValueLength;
	DWORD Index;
	ITERATOR_CALLBACK_DATA cbargs;

	Error = RegOpenKeyEx(HKEY_LOCAL_MACHINE,
                         KeyName,
                         0,
                         KEY_ALL_ACCESS,
                         &Key);
    if (Error != ERROR_SUCCESS) {
        SetLastError(Error);
		Warning("Cannot open key %s", KeyName);
        goto fail1;
    }
	cbargs.ParentKey = &Key;
    Error = RegQueryInfoKey(Key,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            &Values,
                            &MaxNameLength,
                            &MaxValueLength,
                            NULL,
                            NULL);
    if (Error != ERROR_SUCCESS) {
		Warning("Cannot query key info");
        SetLastError(Error);
        goto fail2;
    }

	Log("Values is %d",Values);
    if (Values == 0)
        goto done;

    MaxNameLength += sizeof (TCHAR);

    cbargs.Name = (PTCHAR)malloc(MaxNameLength);
    if (cbargs.Name == NULL) {
		Warning("Cannot allocated memory for name");
        goto fail3;
	}

	cbargs.Value = (LPBYTE)malloc(MaxValueLength);
    if (cbargs.Value == NULL) {
		Warning("Cannot allocated memory for value");
        goto fail4;
	}

    for (Index = 0; Index < Values; Index++) {



        cbargs.NameLength = MaxNameLength;
        memset(cbargs.Name, 0, cbargs.NameLength);

        cbargs.ValueLength = MaxValueLength;
        memset(cbargs.Value, 0, cbargs.ValueLength);

        Error = RegEnumValue(Key,
                             Index,
                             (LPTSTR)cbargs.Name,
                             &cbargs.NameLength,
                             NULL,
                             &cbargs.Type,
                             cbargs.Value,
                             &cbargs.ValueLength);
        if (Error != ERROR_SUCCESS) {
            SetLastError(Error);
			Warning("Cannot enumerate keys");
            goto fail5;
        }
		Error = callback(&cbargs, data);
		if (Error != ERROR_SUCCESS) {
			Warning("Callback failed");
			SetLastError(Error);
			goto fail6;
		}
	}

	
	free(cbargs.Name);
	free(cbargs.Value);
done:
	RegCloseKey(Key);
	return ERROR_SUCCESS;

fail6:
fail5:
	free(cbargs.Value);
fail4:
	free(cbargs.Name);
fail3:
fail2:
	RegCloseKey(Key);
fail1:
    Error = GetLastError();

    {
        PTCHAR  Message;

        Message = __GetErrorMessage(Error);
        Log("fail1 (%s)", Message);
        LocalFree(Message);
    }
	return Error;
}

#define NETLUIDSTRINGSIZE (sizeof (ULONG64) * 2)

HRESULT
generateNetLuidString(NET_LUID NetLuid, PTCHAR Buffer)
{
	HRESULT     Error;

	Error = StringCbPrintf((STRSAFE_LPSTR)Buffer,
                           (NETLUIDSTRINGSIZE+1)*sizeof(TCHAR),
                           "%016llx",
                           _byteswap_uint64(NetLuid.Value));
	return Error;
}

BOOLEAN
nsiDataMatchesNetLuid(NET_LUID NetLuid, PTCHAR NsiValueName)  
{
	HRESULT     Error;
	TCHAR		Buffer[(NETLUIDSTRINGSIZE + 1)];

    Error = generateNetLuidString(NetLuid, Buffer);
    if (!SUCCEEDED(Error)) {
        SetLastError(ERROR_BUFFER_OVERFLOW);
        return false;
    }
        
	if (strncmp((const char *)Buffer, NsiValueName, NETLUIDSTRINGSIZE) != 0){
        return false;
    }

	return true;
}


HRESULT RegistryStoreIfMatchingNetLuid(ITERATOR_CALLBACK_DATA *iteratordata, void *externaldata)
{
	HRESULT						Error;
	HKEY						StoreKey;
	STORE_IF_MATCHING_NET_LUID	*matchdata = (STORE_IF_MATCHING_NET_LUID *)externaldata;
	
	Error = RegCreateKeyEx(HKEY_LOCAL_MACHINE, matchdata->StoreKeyName, 0, NULL, REG_OPTION_NON_VOLATILE, KEY_ALL_ACCESS, NULL, &StoreKey, NULL);
	if (Error != ERROR_SUCCESS) {
		Warning("Unable to open key %s (%d)", matchdata->StoreKeyName, Error);
		return Error;
	}
	
	if (nsiDataMatchesNetLuid(matchdata->NetLuid, iteratordata->Name)) {
		Error = RegSetValueEx(StoreKey,
                              iteratordata->Name,
                              0,
                              iteratordata->Type,
                              iteratordata->Value,
                              iteratordata->ValueLength);
        if (Error != ERROR_SUCCESS) {
            SetLastError(Error);
			Warning("Unable to write to %s", iteratordata->Name);
            return Error;
        }
	}

	return ERROR_SUCCESS;
}

HRESULT RegistryDeleteIfMatchingNetLuid(ITERATOR_CALLBACK_DATA *iteratordata, void *externaldata)
{
	HRESULT		Error;
	NET_LUID	*NetLuid = (NET_LUID *)externaldata;
	
	if (nsiDataMatchesNetLuid(*NetLuid, iteratordata->Name)) {
		Error = RegDeleteValue(*iteratordata->ParentKey, iteratordata->Name);
		if (Error != ERROR_SUCCESS) {
			Warning("Unable to delete value %s",iteratordata->Name);
		}
	}

	return ERROR_SUCCESS;
}
	
HRESULT RegistryRestoreWithNewNetLuid(ITERATOR_CALLBACK_DATA *iteratordata, void *externaldata)
{
	TCHAR				Buffer[NETLUIDSTRINGSIZE+1];
	HRESULT				Error;
	RESTORE_IPV6_DATA	*restoreData = (RESTORE_IPV6_DATA *)externaldata;

	Error = generateNetLuidString(restoreData->NetLuid, Buffer);
	if (Error) {
		Warning("Generating new Luid failed");
		goto fail1;
	}

	Log("Restoring to netluid %s", Buffer);

	memcpy(iteratordata->Name, Buffer, NETLUIDSTRINGSIZE * sizeof(TCHAR));

	Log("Restoring to key %s", iteratordata->Name);

	Error = RegSetValueEx(restoreData->DestinationKey, iteratordata->Name, 0, iteratordata->Type, iteratordata->Value, iteratordata->ValueLength);
	if (Error != ERROR_SUCCESS) {
		Warning("Unable to restore IPV6 Entry %s",iteratordata->Name);
	}

	return ERROR_SUCCESS;

fail1:
	Fail(Error);
	return Error;
}