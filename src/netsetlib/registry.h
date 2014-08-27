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



#ifndef _REGISTRY_H
#define _REGISTRY_H

#include "ws2tcpip.h"
#include <iphlpapi.h>
#include <Windows.h>
#include <tchar.h>
#include <strsafe.h>
#include "log.h"

#define MAXIMUM_BUFFER_SIZE 1024

#define SERVICES_KEY "SYSTEM\\CurrentControlSet\\Services"

#define SERVICE_KEY(_Driver)    \
        SERVICES_KEY ## "\\" ## #_Driver

#define PARAMETERS_KEY(_Driver) \
        SERVICE_KEY(_Driver) ## "\\Parameters"

#define ADDRESSES_KEY(_Driver)  \
        SERVICE_KEY(_Driver) ## "\\Addresses"

#define ALIASES_KEY(_Driver)    \
        SERVICE_KEY(_Driver) ## "\\Aliases"

#define UNPLUG_KEY(_Driver)     \
        SERVICE_KEY(_Driver) ## "\\Unplug"

#define CONTROL_KEY "SYSTEM\\CurrentControlSet\\Control"

#define CLASS_KEY   \
        CONTROL_KEY ## "\\Class"

#define NSI_KEY \
        CONTROL_KEY ## "\\Nsi"

#define ENUM_KEY "SYSTEM\\CurrentControlSet\\Enum"

#define SOFTWARE_KEY "SOFTWARE\\Citrix"

#define INSTALLER_KEY_MAC   \
        SOFTWARE_KEY ## "\\XenToolsNetSettings\\Mac"
#define INSTALLER_KEY_IPV6   \
        SOFTWARE_KEY ## "\\XenToolsNetSettings\\IPV6"

#define INSTALLER_KEY_OVERRIDE   \
        SOFTWARE_KEY ## "\\XenToolsNetSettings\\override"

#define STATIC_IPV6_KEY \
		NSI_KEY ## "\\{eb004a01-9b1a-11d4-9123-0050047759bc}\\10\\"

#define NETWORK_ADAPTER_CLASS_KEY \
		CLASS_KEY ## "\\{4D36E972-E325-11CE-BFC1-08002BE10318}"

extern HRESULT
RegistryGetXenNetSoftwareKey(
	int		deviceIndex,
	HKEY*	XenNetSoftwareKey
);

extern PTCHAR
RegistryGetInterfaceName( 
	HKEY SourceKey 
);

extern BOOLEAN
RegistryStoreParameters( 
	PTCHAR DestinationName,
	PTCHAR DestinationSubKeyName,
	PTCHAR SourcePrefix,
	PTCHAR SourceName
);

BOOLEAN
RegistryRestoreParameters( 
	PTCHAR StoreName,
	PTCHAR StoreSubKeyName,
	PTCHAR DevicePrefix,
	PTCHAR DeviceName
);

extern DWORD
RegistryGetXenNetCount(
	void
);

extern PTCHAR
RegistryGetStorageKeyName(
	HKEY SourceKey,
	PTCHAR StorageBaseKey
);

extern BOOLEAN
RegistryStoreIpVersion6Addresses(
    IN  PTCHAR  DestinationKeyName,
    IN  PTCHAR  SourceKeyName,
    IN  PTCHAR  DestinationValueName,
    IN  PTCHAR  SourceValueName
);

BOOLEAN
RegistryRestoreIpVersion6Addresses(
    IN  PTCHAR  StoreKeyName,
    IN  PTCHAR  DeviceKeyName,
    IN  PTCHAR  StoreValueName,
    IN  PTCHAR  DeviceValueName
);

typedef struct _RESTORE_IPV6_DATA
{
	NET_LUID	NetLuid;
	HKEY		DestinationKey;
} RESTORE_IPV6_DATA;

typedef struct _STORE_IF_MATCHING_NET_LUID {
	NET_LUID	NetLuid;
	PTCHAR		StoreKeyName;
} STORE_IF_MATCHING_NET_LUID;

typedef struct _ITERATOR_CALLBACK_DATA {
	PTCHAR	Name;
	LPBYTE	Value;
	DWORD	Type;
	DWORD	NameLength;
	DWORD	ValueLength;
	PHKEY	ParentKey;
} ITERATOR_CALLBACK_DATA;

typedef struct _SUBKEY_ITERATOR_CALLBACK_DATA {
	PTCHAR	Name;
	DWORD	NameLength;
	HKEY	Key;
} SUBKEY_ITERATOR_CALLBACK_DATA;

extern HRESULT 
RegistryStoreIfMatchingNetLuid(
	ITERATOR_CALLBACK_DATA *iteratordata, 
	void *externaldata
);

extern HRESULT
RegistryGetNetLuid(
	HKEY SourceKey,
	NET_LUID* NetLuid
);

typedef HRESULT(*ITERATOR_CALLBACK)(ITERATOR_CALLBACK_DATA *, void*);

typedef HRESULT(*SUBKEY_ITERATOR_CALLBACK)(SUBKEY_ITERATOR_CALLBACK_DATA *, void*);

extern HRESULT 
RegistryDeleteIfMatchingNetLuid(
	ITERATOR_CALLBACK_DATA	*iteratordata, 
	void					*externaldata
);

extern HRESULT 
RegistryRestoreWithNewNetLuid(
	ITERATOR_CALLBACK_DATA	*iteratordata, 
	void					*externaldata
);

extern HRESULT 
RegistryIterateOverKeyValues(
	PTCHAR				KeyName, 
	ITERATOR_CALLBACK	callback, 
	void				*data
);

extern HRESULT 
RegistryIterateOverKeySubKeys(
	PTCHAR						KeyName, 
	SUBKEY_ITERATOR_CALLBACK	callback, 
	void						*data
);

extern PTCHAR
RegistryGetStorageKeyOverrideName(
	int deviceIndex
);

#endif