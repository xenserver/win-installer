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

#include "save.h"
#include "restore.h"
#include "log.h"
#include <Windows.h>
#include <tchar.h>
#include "shlobj.h"
#include <direct.h>
#include <errno.h>

#define SAVEARG _T("/save")
#define RESTOREARG _T("/restore")
#define HELPARG _T("/help")

#define LOGARG _T("/log")

#define InitCmd helpCmd

FILE *logptr = stderr;

int helpCmd(void) {
	Log(_T("netsettings.exe [ " LOGARG "] < ") SAVEARG _T(" | ") RESTOREARG _T(" | ") HELPARG _T(" >\n"));
	return 1;
}

BOOLEAN matchcmd(_TCHAR* varlength, _TCHAR* fixedlength)
{
	if (_tcscmp(varlength, fixedlength) == 0) {
		return TRUE;
	}

	return FALSE;
}


int _tmain(int argc, _TCHAR* argv[])
{
	DWORD		i = 1;
	HRESULT		err=0;

	while (i < argc)
	{
		if ( matchcmd(argv[i], SAVEARG) )
		{
			err = SaveCmd();
			i++;
		}
		else if ( matchcmd(argv[i], RESTOREARG) )
		{
			err = RestoreCmd();
			i++;
		}
		else if ( matchcmd(argv[i], LOGARG) )
		{
			i++;
			TCHAR Buffer[MAX_PATH];
			TCHAR PathName[MAX_PATH];
			TCHAR LogName[MAX_PATH];
			FILE * outlog;
			
			err = SHGetFolderPath(NULL,CSIDL_COMMON_APPDATA, NULL,  SHGFP_TYPE_CURRENT, Buffer);
			if (err != ERROR_SUCCESS) {
				Warning("Unabale to find log folder");
				continue;
			}

			err = sprintf_s(PathName, MAX_PATH, "%s\\Citrix", Buffer);
			if (err <= 0 && errno != EEXIST) {
				Warning("Unable to generate log path name");
				continue;
			}

			err = _mkdir(PathName);
			if ((err != 0) && (errno != EEXIST)) {
				Warning("Unable to open logfile");
				continue;
			}

			err = sprintf_s(PathName, MAX_PATH, "%s\\Citrix\\XSNetSettings", Buffer);
			if (err <= 0 ) {
				Warning("Unable to generate log path name");
				continue;
			}

			err = _mkdir(PathName);
			if ((err != 0) && (errno != EEXIST)) {
				Warning("Unable to open logfile");
				continue;
			}

			err = sprintf_s(LogName, MAX_PATH, "%s\\NetSettings.log", PathName);
			if (err <= 0 ) {
				Warning("Unable to generate log name");
				continue;
			}

			err = fopen_s(&outlog, LogName,"w+");
			if (err != 0) {
				Warning("FOpen failed %s %d",argv[i], err);
				continue;
			}
			fclose(logptr);
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

