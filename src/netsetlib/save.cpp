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

HRESULT
saveStaticNetworkConfiguration(
	HKEY SourceKey, 
	PTCHAR statickey, 
	PTCHAR installerkey
)
{
	STORE_IF_MATCHING_NET_LUID	matchdata;
	HKEY						CheckKey;
	HRESULT						Error;
	Error = RegistryGetNetLuid(SourceKey, &matchdata.NetLuid);
    if (Error != ERROR_SUCCESS) {
		Log("Can't find NetLuid");
        goto done1;
	}

	matchdata.StoreKeyName = RegistryGetStorageKeyName(SourceKey, installerkey);

	if (matchdata.StoreKeyName == NULL)
		goto done2;

	Error = RegOpenKeyEx(HKEY_LOCAL_MACHINE, statickey, 0, KEY_READ, &CheckKey);
	if (Error == ERROR_SUCCESS) {
		RegCloseKey(CheckKey);
		Error = RegistryIterateOverKeyValues(statickey, RegistryStoreIfMatchingNetLuid, &matchdata);
		if (Error != ERROR_SUCCESS)
			goto fail1;
	}

done2:
	free(matchdata.StoreKeyName);
done1:
	return ERROR_SUCCESS;

fail1:
	free(matchdata.StoreKeyName);
	Fail(Error);
	return Error;
}

HRESULT 
saveDevice(
	SUBKEY_ITERATOR_CALLBACK_DATA	*cbargs, 
	void							*data
	) 
{
	int err = -1;

	PTCHAR						DestinationName;
    PTCHAR						SourceName;
    BOOLEAN						Success;
	HRESULT						Error;	
	STORE_IF_MATCHING_NET_LUID	matchdata;

	SourceName = RegistryGetInterfaceName(cbargs->Key);
    if (SourceName == NULL) {
		Log("Can't find data source for device index %d");
        goto done1;
	}

	DestinationName = RegistryGetStorageKeyName(cbargs->Key, INSTALLER_KEY_MAC);
    if (DestinationName == NULL) {
		Log("Can't find data destination");
        goto done2;
	}

    Success &= RegistryStoreParameters(DestinationName,
									  "NetBT",
									  PARAMETERS_KEY(NetBT) "\\Interfaces\\Tcpip_",
									  SourceName);
    Success &= RegistryStoreParameters(DestinationName,
									  "Tcpip",
									  PARAMETERS_KEY(Tcpip) "\\Interfaces\\",
									  SourceName);
    Success &= RegistryStoreParameters(DestinationName,
									  "Tcpip6",
									  PARAMETERS_KEY(Tcpip6) "\\Interfaces\\",
									  SourceName);

    Error = saveStaticNetworkConfiguration(cbargs->Key, STATIC_IPV4_KEY, INSTALLER_KEY_IPV4);
	if (Error != ERROR_SUCCESS)
		goto fail1;

    Error = saveStaticNetworkConfiguration(cbargs->Key, STATIC_IPV6_KEY, INSTALLER_KEY_IPV6);
	if (Error != ERROR_SUCCESS)
		goto fail2;

done2:
    free(SourceName);

done1:
	return ERROR_SUCCESS;

fail2:
fail1:
	free(SourceName);
	Fail(err);
	return err;
}

HRESULT 
SaveCmd(
	void
	)
{
	HRESULT err = 0;

	err = RegistryIterateOverKeySubKeys(NETWORK_ADAPTER_CLASS_KEY, saveDevice, NULL);

	return err;
}