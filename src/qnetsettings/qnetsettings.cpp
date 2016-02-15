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

#include "stdafx.h"
#include "qnetsettings.h"
#include <shellapi.h>
#include "Log.h"
#include "save.h"
#include "restore.h"
#include "shlobj.h"
#include <direct.h>
#include <errno.h>
#include "brandcontrol.h"

#define SAVEARG _T("/save")
#define RESTOREARG _T("/restore")
#define INITARG _T("/init")
#define HELPARG _T("/help")

#define LOGARG _T("/log")

#define InitCmd helpCmd

FILE *logptr = stderr;

int helpCmd(void) {
	Log(_T("qnetsettings.exe [ " LOGARG "] < ") SAVEARG _T(" | ") RESTOREARG _T(" | ") HELPARG _T(" >\n"));
	return 1;
}

BOOLEAN matchcmd(_TCHAR* varlength, _TCHAR* fixedlength)
{
	Log("%s %s", varlength, fixedlength);

	if (_tcscmp(varlength, fixedlength) == 0) {
		return TRUE;
	}

	return FALSE;
}

int APIENTRY 
_tWinMain(
	_In_		HINSTANCE hInstance,
	_In_opt_	HINSTANCE hPrevInstance,
    _In_		LPTSTR    lpCmdLine,
    _In_		int       nCmdShow)
{
	UNREFERENCED_PARAMETER(hPrevInstance);
	UNREFERENCED_PARAMETER(lpCmdLine);

	DWORD	i = 1;
	HRESULT err=0;

	while (i < __argc)
	{
		if ( matchcmd(__argv[i], SAVEARG) )
		{
			err = SaveCmd();
			i++;
		}
		else if ( matchcmd(__argv[i], RESTOREARG) )
		{
			err = RestoreCmd();
			i++;
		}
		else if ( matchcmd(__argv[i], INITARG) )
		{
			err = InitCmd();
			i++;
		}
		else if ( matchcmd(__argv[i], LOGARG) )
		{
			i++;
			TCHAR	Buffer[MAX_PATH];
			TCHAR	PathName[MAX_PATH];
			TCHAR	LogName[MAX_PATH];
			FILE	*outlog;
			
			err = SHGetFolderPath(NULL,CSIDL_COMMON_APPDATA, NULL,  SHGFP_TYPE_CURRENT, Buffer);
			if (err != ERROR_SUCCESS) {
				Warning("Unable to find log folder");
				continue;
			}

			err = sprintf_s(PathName, MAX_PATH, "%s\\%s", Buffer,getBrandingString(BRANDING_manufacturer));
			if (err <= 0 && errno != EEXIST) {
				Warning("Unable to generate log path name");
				continue;
			}

			err = _mkdir(PathName);
			if ((err != 0) && (errno != EEXIST)) {
				Warning("Unable to open %s programdata folder",getBrandingString(BRANDING_manufacturer));
				continue;
			}

			err = sprintf_s(PathName, MAX_PATH, "%s\\%s\\%sNetSettings", Buffer,getBrandingString(BRANDING_manufacturer),getBrandingString(BRANDING_twoCharBrand));
			if (err <= 0 ) {
				Warning("Unable to generate log path name");
				continue;
			}

			err = _mkdir(PathName);
			if ((err != 0) && (errno != EEXIST)) {
				Warning("Unable to open %s %sNetSettings ProgramData folder", getBrandingString(BRANDING_manufacturer), getBrandingString(BRANDING_twoCharBrand));
				continue;
			}

			err = sprintf_s(LogName, MAX_PATH, "%s\\NetSettings.log", PathName);
			if (err <= 0 ) {
				Warning("Unable to generate log name");
				continue;
			}

			err = fopen_s(&outlog, LogName,"a+");
			if (err != 0) {
				Warning("FOpen failed %s %d",__argv[i], err);
				continue;
			}

			fclose(logptr);
			Log("------------------------------------------------------------------------------");
			Log("  New session");
			Log("------------------------------------------------------------------------------");
			logptr = outlog;

		}
		else
		{
			err = helpCmd();
			break;
		}

		if (err > 0)
			break;
	}
	
	if (err == 0) {
		Log("Network Settings Handled Successfully");
	}

	return err;
}



