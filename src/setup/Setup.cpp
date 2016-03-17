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

#include "Setup.h"
#include "shellapi.h"
#include "shlobj.h"
#include "shlwapi.h"
#include "stdint.h"
#include <windows.h>
#include "setupapi.h"
#include "Sddl.h"

TCHAR * _vatallocprintf(const TCHAR *format, va_list args)
{
	int chars;
	chars = _vsctprintf(format, args);
	size_t size= sizeof(TCHAR) * (chars+1);
	TCHAR *space = (TCHAR *)malloc(size);
	if (space == NULL)
		return NULL;
	_vsntprintf_s(space, chars+1, chars, format, args);
	return space;
}

TCHAR * _tallocprintf(const TCHAR *format, ...)
{
	TCHAR *space;
	va_list args;
    va_start (args, format);
	space=_vatallocprintf(format, args);
	va_end(args);
	return space;
}



#include "brandcontrol.h"

void ErrMsg(const TCHAR *format, ...)
{
	va_list args;
    va_start (args, format);

	TCHAR* space = _vatallocprintf(format, args);
	if (space == NULL)
		goto fail1;

	va_end(args);
	MessageBox(NULL, space, getBrandingString(BRANDING_setupErr), MB_OK);
	free(space);
	return;
fail1:
	va_end(args);
}


bool runProcess(TCHAR* cmdLine, DWORD* exitcode)
{
	PROCESS_INFORMATION pi;
	STARTUPINFO si;
	memset(&si, 0, sizeof(STARTUPINFO));

	OutputDebugString(cmdLine);

	if (!CreateProcess(NULL, cmdLine, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi)) {
		ErrMsg(getBrandingString(BRANDING_processFail),cmdLine, GetLastError());
		return false;
	}

	WaitForSingleObject(pi.hProcess, INFINITE);
	GetExitCodeProcess(pi.hProcess, exitcode);
	return true;
}


typedef struct {
	bool test;
	bool passive;
	bool quiet;
	bool norestart;
	bool forcerestart;
	bool legacy;
} arguments;



bool parseCommandLine(arguments* args)
{
	
	memset(args, 0, sizeof(arguments));

	// Check OS
	OSVERSIONINFO versionInfo;
	versionInfo.dwOSVersionInfoSize = sizeof(OSVERSIONINFO);

	if (GetVersionEx(&versionInfo)) {
		if (versionInfo.dwMajorVersion < 6) {
			args->legacy=true;
		}
	}

	int argCount;
	LPWSTR cli = GetCommandLineW();
	LPWSTR *szArgList = CommandLineToArgvW(cli, &argCount);
	for (int i=1; i<argCount ; i++) {
		if (!wcsncmp(szArgList[i],L"/TEST",sizeof(L"/TEST"))) {
			args->test = true;
		}
		else if (!wcsncmp(szArgList[i],L"/passive",sizeof(L"/passive"))) {
			args->passive = true;
		}
		else if (!wcsncmp(szArgList[i],L"/quiet",sizeof(L"/quiet"))) {
			args->quiet = true;
		}
		else if (!wcsncmp(szArgList[i],L"/norestart",sizeof(L"/norestart"))) {
			args->norestart = true;
		}
		else if (!wcsncmp(szArgList[i],L"/forcerestart",sizeof(L"/forcerestart"))) {
			args->forcerestart = true;
		}
		else {
			ErrMsg(getBrandingString(BRANDING_setupHelp));
			return false;
		}
	}

	return true;
}


const TCHAR* ma64 = getBrandingString(FILENAME_managementx64);
const TCHAR* ma32 = getBrandingString(FILENAME_managementx86);
const TCHAR* iw = _T("installwizard.msi");
	
const TCHAR* xenlegacy = getBrandingString(FILENAME_legacy);
const TCHAR* uninstallerfix = getBrandingString(FILENAME_legacyuninstallerfix);
const TCHAR* INSTALL_AGENT_REG_KEY = getBrandingString(BRANDING_installAgentRegKey);

TCHAR sysdir[MAX_PATH];
TCHAR workfile[MAX_PATH];
TCHAR logfile[MAX_PATH];
TCHAR* msiexec;



int getFileLocations()
{
	if (!GetSystemDirectory(sysdir, MAX_PATH)) {
		ErrMsg(getBrandingString(BRANDING_noSystemDir));
		return 0;
	}

	msiexec = _tallocprintf(_T("%s\\msiexec.exe"), sysdir);
	if ( msiexec == NULL) {
		ErrMsg(getBrandingString(BRANDING_errMSINoMem));
		return 0;
	}

	
	if (GetModuleFileName(NULL, workfile, MAX_PATH)>=MAX_PATH) {
		ErrMsg(getBrandingString(BRANDING_errFilePathNoMem));
		return 0;
	}
	PathRemoveFileSpec(workfile);


	if (SUCCEEDED(SHGetFolderPath(NULL, CSIDL_COMMON_APPDATA | CSIDL_FLAG_CREATE, NULL, 0 , logfile))) {
		PathAppend(logfile, (TCHAR *)getBrandingString(BRANDING_manufacturer));
		CreateDirectory(logfile, NULL);
		PathAppend(logfile, getBrandingString(BRANDING_setupLogDir));
		CreateDirectory(logfile, NULL);
		PathAppend(logfile,_T("Install.log"));
	}
	else {
		ErrMsg(getBrandingString(BRANDING_errNoLogPath));
		return 0;
	}
	return 1;
}

DWORD installLegacy(arguments *args) {
	DWORD exitcode;
	TCHAR* cmd = _tallocprintf(_T("%s\\%s /S"), workfile, uninstallerfix);
	runProcess(cmd, &exitcode);
	free(cmd);
	cmd = _tallocprintf(_T("%s\\%s%s"), workfile, xenlegacy, 
		(args->passive||args->quiet)?_T(" /S /AllowLegacyInstall"):_T("/AllowLegacyInstall"));
	runProcess(cmd, &exitcode);
	return exitcode;
}

const TCHAR* getInstallMsiName(arguments* args)
{
	if (!args->test) {
		BOOL wow64;
		if (IsWow64Process(GetCurrentProcess(), &wow64)) {
			if (wow64) {
				return ma64;
			}
			else {
				return ma32;
			}
		}
		else {
			return ma32;
		}
	}
	else {
		args->forcerestart = false;  //Hack because old installer doesn't like forcereboot
		return iw;
	}
}

DWORD installMsi(arguments* args)
{
	DWORD exitcode;

	const TCHAR* installname = getInstallMsiName(args);
	TCHAR* cmdline = _tallocprintf(_T("\"%s\" /i\"%s\\%s\"%s%s /liwearucmopvx+! \"%s\""),
		msiexec,
		workfile,
		installname,
		( args->passive?_T(" /passive"): (args->quiet?_T(" /quiet"):_T(""))),
		( args->norestart?_T(" /norestart"):(args->forcerestart?_T(" /forcerestart"):_T(""))),
		logfile);
	if (cmdline == NULL) {
		ErrMsg(getBrandingString(BRANDING_errCmdLineNoMem));
		return 0;
	}

	runProcess(cmdline, &exitcode);

	return exitcode;
}

BOOL WriteCurrentUserSidToRegistry(void)
{
	HANDLE      processHandle = GetCurrentProcess();
	HANDLE      tokenHandle   = NULL;
	DWORD       tokenInfLen   = 0;
	PTOKEN_USER pTokenUser    = NULL;
	LPTSTR      strSid        = NULL;
	BOOL        success       = FALSE;
	HKEY        regKey;

	if (!OpenProcessToken(processHandle, TOKEN_QUERY, &tokenHandle))
	{
		goto fail1;
	}

	/* Get buffer length */
	GetTokenInformation(
		tokenHandle,
		TokenUser,
		NULL,
		0,
		&tokenInfLen
	);

	if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
	{
		goto fail2;
	}

	pTokenUser = (PTOKEN_USER)malloc(tokenInfLen);

	if (!GetTokenInformation(
			tokenHandle,
			TokenUser,
			(LPVOID)pTokenUser,
			tokenInfLen,
			&tokenInfLen))
	{
		goto fail3;
	}

	if (!ConvertSidToStringSid(pTokenUser->User.Sid, &strSid))
	{
		goto fail3;
	}

	if (RegCreateKeyEx(
			HKEY_LOCAL_MACHINE,
			INSTALL_AGENT_REG_KEY,
			0,
			NULL,
			REG_OPTION_NON_VOLATILE,
			KEY_WRITE | KEY_WOW64_64KEY,
			NULL,
			&regKey,
			NULL) != ERROR_SUCCESS)
	{
		goto fail4;
	}

	if (RegSetValueEx(
			regKey,
			_T("InstallerInitiatorSid"),
			0,
			REG_SZ,
			(BYTE*)strSid,
			_tcslen(strSid) * sizeof(TCHAR)) != ERROR_SUCCESS)
	{
		goto fail5;
	}

	success = TRUE;

fail5:
	RegCloseKey(regKey);
fail4:
	LocalFree(strSid);
fail3:
	free(pTokenUser);
	pTokenUser = NULL;
fail2:
	CloseHandle(tokenHandle);
fail1:
	return success;
}

void waitForStatus() {
	HKEY  insAgntKey;
	HKEY  vmStateKey;
	TCHAR status[256];
	DWORD statusSize = 256;
	DWORD pvToolsOnFirstRun;
	DWORD pvToolsVerSize = sizeof(pvToolsOnFirstRun);

	TCHAR *VM_STATE_REG_KEY = _tallocprintf(
		_T("%s\\%s"),
		INSTALL_AGENT_REG_KEY,
		_T("VMState")
	);

	if (VM_STATE_REG_KEY == NULL)
	{
		return;
	}

	while (RegOpenKeyEx(
			HKEY_LOCAL_MACHINE,
			INSTALL_AGENT_REG_KEY,
			0,
			KEY_READ | KEY_WOW64_64KEY,
			&insAgntKey) != ERROR_SUCCESS)
	{
		SleepEx(1000, TRUE);
	}

	while (RegOpenKeyEx(
			HKEY_LOCAL_MACHINE,
			VM_STATE_REG_KEY,
			0,
			KEY_READ | KEY_WOW64_64KEY,
			&vmStateKey) != ERROR_SUCCESS)
	{
		SleepEx(1000, TRUE);
	}

	for(;;) {
		if (RegQueryValueEx(
				insAgntKey,
				_T("InstallStatus"),
				NULL,
				NULL,
				(LPBYTE)status,
				&statusSize) == ERROR_SUCCESS &&
		    RegQueryValueEx(
				vmStateKey,
				_T("PVToolsVersionOnFirstRun"),
				NULL,
				NULL,
				(LPBYTE)&pvToolsOnFirstRun,
				&pvToolsVerSize) == ERROR_SUCCESS)
		{
			if (_tcsicmp(status, _T("NeedsReboot")) == 0) {
				// If PV drivers other than the latest
				// (currently 8.x) are found, we should prompt
				// for reboot after removing them from
				// filters and disabling their bootstart
				if (pvToolsOnFirstRun == 7) {
					SetupPromptReboot(NULL, NULL, FALSE);
				}
				goto done;
			}
			goto needcontinue;
		}

needcontinue:
		SleepEx(2000, TRUE);
		continue;
done:
		RegCloseKey(insAgntKey);
		RegCloseKey(vmStateKey);
		free(VM_STATE_REG_KEY);
		return;
	}
}

#define dotnet4 _T("Software\\Microsoft\\NET Framework Setup\\NDP\\v4\\Client")
#define dotnet35 _T("Software\\Microsoft\\NET Framework Setup\\NDP\\v3.5")
bool checkDotNet() {
	HKEY outkey;
	
	if (RegOpenKeyEx(HKEY_LOCAL_MACHINE, dotnet4, 0, KEY_READ, &outkey)!=ERROR_SUCCESS) {
		goto check35;
	}
	if (RegQueryValueEx(outkey, _T("Install"), NULL, NULL, NULL, NULL)==ERROR_SUCCESS) {
		RegCloseKey(outkey);
		return true;
	}
	RegCloseKey(outkey);
check35:
	if (RegOpenKeyEx(HKEY_LOCAL_MACHINE, dotnet35, 0, KEY_READ, &outkey)!=ERROR_SUCCESS) {
		return false;
	}
	if (RegQueryValueEx(outkey, _T("Install"), NULL, NULL, NULL, NULL)==ERROR_SUCCESS) {
		RegCloseKey(outkey);
		return true;
	}
	RegCloseKey(outkey);
	return false;
}


int APIENTRY _tWinMain(_In_ HINSTANCE hInstance,
                     _In_opt_ HINSTANCE hPrevInstance,
                     _In_ LPTSTR    lpCmdLine,
                     _In_ int       nCmdShow)
{
	UNREFERENCED_PARAMETER(hPrevInstance);
	UNREFERENCED_PARAMETER(lpCmdLine);
	DWORD msiResult;

	// Parse command line
	// Acceptable options:
	// /TEST - test managementagent(x86/x64).msi based install
	// /quiet - silent, noninteractive install
	// /passive - passive install
	// /norestart - does not restart vm
	// /forcerestart - will always restart vm

	arguments args;

	if (!parseCommandLine(&args))
		return 0;

	// We require .net to run

	if (!checkDotNet()) {
		ErrMsg(getBrandingString(BRANDING_errDotNetNeeded));
		return 0;
	}

	//Locate Files

	if (!getFileLocations()) {
		return 0;
	}

	// Get legacy OS support out of the way

	if (args.legacy) {
		return installLegacy(&args);
	}
	
	msiResult = installMsi(&args);

	if (msiResult != ERROR_SUCCESS &&
			msiResult != ERROR_SUCCESS_REBOOT_INITIATED &&
			msiResult != ERROR_SUCCESS_REBOOT_REQUIRED) {
		ErrMsg(
			getBrandingString(BRANDING_errMSIInstallFail),
			msiResult,
			logfile
		);
		return msiResult;
	}
	
	WriteCurrentUserSidToRegistry();
	waitForStatus();
}

