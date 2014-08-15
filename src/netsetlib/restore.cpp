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

#include "registry.h"

HRESULT restoreNetworkInterfaces(DWORD deviceIndex, HKEY DestinationKey)
{
	PTCHAR		StoreName;
    PTCHAR		DestinationName;
	BOOLEAN		Success;
	HRESULT		Error;
	HKEY		CheckKey;
	DestinationName = RegistryGetInterfaceName(DestinationKey);

	Error = ERROR_SUCCESS;

    if (DestinationName == NULL) {
		Log("Can't find data destiantion for device index %d");
		Error = ERROR_FILE_NOT_FOUND;
        goto fail1;
	}

	StoreName = RegistryGetStorageKeyOverrideName(deviceIndex);
	if (StoreName != NULL) {
		Error = RegOpenKeyEx(HKEY_LOCAL_MACHINE, StoreName, 0, KEY_READ, &CheckKey);
		if (Error == ERROR_SUCCESS) {
			RegCloseKey(CheckKey);
		}
	}
	if ((StoreName == NULL) || (Error != ERROR_SUCCESS)) {
		StoreName = RegistryGetStorageKeyName(DestinationKey, INSTALLER_KEY_MAC);
		if (StoreName == NULL) {
			Log("Can't find data store");
			goto fail2;
		}
	}

    Success &= RegistryRestoreParameters(StoreName,
									  "NetBT",
									  PARAMETERS_KEY(NetBT) "\\Interfaces\\Tcpip_",
									  DestinationName);
    Success &= RegistryRestoreParameters(StoreName,
									  "Tcpip",
									  PARAMETERS_KEY(Tcpip) "\\Interfaces\\",
									  DestinationName);
    Success &= RegistryRestoreParameters(StoreName,
									  "Tcpip6",
									  PARAMETERS_KEY(Tcpip6) "\\Interfaces\\",
									  DestinationName);

	free(StoreName);
    free(DestinationName);

    return ERROR_SUCCESS;

fail2:
	free(DestinationName);
fail1:
	Fail(Error);
	return Error;
}

HRESULT restoreStaticNetworkConfiguration(DWORD deviceIndex, HKEY DestinationKey) 
{
	HRESULT				Error;
	PTCHAR				StoreName;
	RESTORE_IPV6_DATA	restoreData;
	HKEY				CheckKey;
	Error = RegistryGetNetLuid(DestinationKey, &restoreData.NetLuid);

    if (Error !=ERROR_SUCCESS) {
		Log("Can't find NetLuid");
        goto fail1;
	}

	int storesize = sizeof(STATIC_IPV6_KEY)+sizeof(TCHAR);
	
	Error = RegCreateKeyEx(HKEY_LOCAL_MACHINE, STATIC_IPV6_KEY, 0,NULL, REG_OPTION_NON_VOLATILE,  KEY_ALL_ACCESS, NULL,  &restoreData.DestinationKey,NULL);
	if (Error != ERROR_SUCCESS) {
		Warning("Cannot open key " STATIC_IPV6_KEY);
		goto fail2;
	}

	StoreName = RegistryGetStorageKeyOverrideName(deviceIndex);
	if (StoreName != NULL) {
		Error = RegOpenKeyEx(HKEY_LOCAL_MACHINE, StoreName, 0, KEY_READ, &CheckKey);
		if (Error == ERROR_SUCCESS) {
			RegCloseKey(CheckKey);
		}
	}
	if ((StoreName == NULL) || (Error != ERROR_SUCCESS)) {
		StoreName = RegistryGetStorageKeyName(DestinationKey, INSTALLER_KEY_IPV6);
		if (StoreName == NULL) {
			Log("Can't find ipv6 store");
			goto done;
		}
	}

	Log("IPV6 Static Restore");

	Error = RegOpenKeyEx(HKEY_LOCAL_MACHINE, STATIC_IPV6_KEY, 0, KEY_READ, &CheckKey);
	if (Error == ERROR_SUCCESS) {
		RegCloseKey(CheckKey);

		Log("IPV6 Static deletion required");
		Error = RegistryIterateOverKeyValues(STATIC_IPV6_KEY, RegistryDeleteIfMatchingNetLuid, &restoreData.NetLuid);
		if (Error != ERROR_SUCCESS) {
			Warning("Removing values failed");
			goto fail3;
		}
	}

	Error = RegOpenKeyEx(HKEY_LOCAL_MACHINE, StoreName, 0, KEY_READ, &CheckKey);
	if (Error == ERROR_SUCCESS) {
		RegCloseKey(CheckKey);
		Log("IPV6 Static cloning required");
		Error = RegistryIterateOverKeyValues(StoreName, RegistryRestoreWithNewNetLuid, &restoreData);
		if (Error != ERROR_SUCCESS)
		{
			Warning("Copying new values failed");
			goto fail4;
		}
	}

    free(StoreName);
done:
	return ERROR_SUCCESS;

fail4:
fail3:
	free(StoreName);
fail2:
fail1:
	Fail(Error);
	return Error;
}

HRESULT 
removeNetSettingsOverride(
	DWORD deviceIndex
	) 
{
	PTCHAR StoreName;
	HRESULT Error;

	StoreName = RegistryGetStorageKeyOverrideName(deviceIndex);
	
	if ( StoreName == NULL )
		goto done;

	Error = RegDeleteTree(HKEY_LOCAL_MACHINE, StoreName);

	if (Error != ERROR_SUCCESS) {
		Log("Unable to delete %s %x",StoreName, Error);
		goto fail1;
	}

done:
	return ERROR_SUCCESS;

fail1:
	return Error;

}

HRESULT
restoreDevice(
	int deviceIndex
	)
{

	HKEY				DestinationKey;
	HRESULT				Error;	
	BOOLEAN				UsingOverride = false;

	Error = RegistryGetXenNetSoftwareKey(deviceIndex, &DestinationKey);
	if (Error != ERROR_SUCCESS) {
		Log("Can't find software key for device index %d (%d)", deviceIndex, Error);
		goto fail1;
	}
	if (DestinationKey == NULL) {
		Log("Can't find software key for device index %d", deviceIndex);
		goto fail1;
	}
	
	Error = restoreNetworkInterfaces(deviceIndex, DestinationKey);
	if (Error != ERROR_SUCCESS) {
		Warning("Unable to restore network interfaces for device %d", deviceIndex);
		goto fail2;
	}

	Error = restoreStaticNetworkConfiguration(deviceIndex, DestinationKey);
	if (Error != ERROR_SUCCESS) {
		Warning("Unable to restore network interfaces for device %d", deviceIndex);
		goto fail3;
	}

	Error = removeNetSettingsOverride(deviceIndex);
	if (Error != ERROR_SUCCESS)
	{
		Log("Unable to remove network settings overrides %x", Error);
	}

	RegCloseKey(DestinationKey);

	return ERROR_SUCCESS;

fail3:
fail2:
	RegCloseKey(DestinationKey);
fail1:
	Fail(Error);
	return Error;
}

HRESULT
RestoreCmd(
	void
	)
{
	HRESULT err = ERROR_SUCCESS;

	int count = RegistryGetXenNetCount();

	if (count == 0) {
		Warning("No XenVif devices found\n");
		goto done;
	}

	if (count < 0) {
		err = count;
		goto fail;
	}

	for (int i =0; i < count; i++) {
		err = restoreDevice(i);
		if ( err != 0 ) {
			Warning("Failed to restore device %d settings, due to it not being correctly installed");
		}
	}

	done:
		return err;

	fail:
		Fail(err);
		return err;
}