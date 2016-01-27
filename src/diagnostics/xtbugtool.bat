@echo off
REM XenTools bugtool generator - v1.7 by Blaine A. Anaya
REM This script collects necessary files used to identify where a XenTools installation issue has occurred
REM and places them in a ZIP file determined at runtime.
REM Usage: xtbugtool.bat <Destination Path for ZIP file>
REM Copyright (c) Citrix Systems Inc.
REM All rights reserved.

REM Redistribution and use in source and binary forms, 
REM with or without modification, are permitted provided 
REM that the following conditions are met:

REM *   Redistributions of source code must retain the above 
    REM copyright notice, this list of conditions and the 
    REM following disclaimer.
REM *   Redistributions in binary form must reproduce the above 
    REM copyright notice, this list of conditions and the 
    REM following disclaimer in the documentation and/or other 
    REM materials provided with the distribution.

REM THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
REM CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
REM INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
REM MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
REM DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
REM CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
REM SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
REM BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
REM SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
REM INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
REM WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
REM NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
REM OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
REM SUCH DAMAGE.
SET ToolVersion=1.7
IF "%1"=="" GOTO usage
set zippath=%1

SETLOCAL ENABLEDELAYEDEXPANSION

SET UTC_DATE_TIME= null
SET LOCAL_DATE_TIME= null

REM Get the UTC date-time string to use
CALL :GetFormattedCurrentUTCDate UTC_DATE_TIME

REM Get the Local date-time string to use
CALL :GetFormattedCurrentLocalDate LOCAL_DATE_TIME

GOTO :bugtool

REM Sub routine to get the current UTC date as formatted string YYY-MM-DDTHH:MM:SSZ
:GetFormattedCurrentUTCDate outString
 FOR /F "tokens=* DELIMS=^=" %%a IN ('WMIC Path Win32_UTCTime Get Year^,Month^,Day^,Hour^,Minute^,Second /Value') DO (
  SET LINE=%%a
  FOR /f "tokens=1-2 delims=^=" %%i IN ("!LINE!") DO (
   IF "%%i" == "Year" ( SET year=%%j)
   IF "%%i" == "Month" ( SET month=%%j)
   IF "%%i" == "Day" ( SET day=%%j)
   IF "%%i" == "Hour" ( SET hour=%%j)
   IF "%%i" == "Minute" ( SET minute=%%j)
   IF "%%i" == "Second" ( SET second=%%j)
  )
 )
 
REM Prepend Zero to the number if less than Ten
 IF %month% LSS 10 SET month=0%month%
 IF %day% LSS 10 SET day=0%day%
 IF %hour% LSS 10 SET hour=0%hour%
 IF %minute% LSS 10 SET minute=0%minute%
 IF %second% LSS 10 SET second=0%second%

SET %1=%Year%-%Month%-%Day%T%Hour%:%Minute%:%Second%Z
REM END of :GetFormattedCurrentUTCDate
Exit /b 


REM Sub routine to get the current Local date as formatted string MM/DD/YYYY HH:MM:SS
:GetFormattedCurrentLocalDate outString
 FOR /F "tokens=* DELIMS=^=" %%a IN ('WMIC Path Win32_LocalTime Get Year^,Month^,Day^,Hour^,Minute^,Second /Value') DO (
  SET LINE=%%a
  FOR /f "tokens=1-2 delims=^=" %%i IN ("!LINE!") DO (
   IF "%%i" == "Year" ( SET year=%%j)
   IF "%%i" == "Month" ( SET month=%%j)
   IF "%%i" == "Day" ( SET day=%%j)
   IF "%%i" == "Hour" ( SET hour=%%j)
   IF "%%i" == "Minute" ( SET minute=%%j)
   IF "%%i" == "Second" ( SET second=%%j)
  )
 )

REM Prepend Zero to the number if less than Ten 
 IF %month% LSS 10 SET month=0%month%
 IF %day% LSS 10 SET day=0%day%
 IF %hour% LSS 10 SET hour=0%hour%
 IF %minute% LSS 10 SET minute=0%minute%
 IF %second% LSS 10 SET second=0%second%

SET %1=%month%/%day%/%year% %hour%:%minute%:%second%
SET dtstring=%year%.%month%.%day%-%hour%%minute%
REM END of :GetFormattedCurrentLocalDate
Exit /b 

:bugtool
REM Start of Bugtool Data Collection
set bugpath=%temp%\%dtstring%
mkdir %bugpath%
REM Set XenTools install directory as identified in the registry
FOR /F "usebackq skip=2 tokens=1-2*" %%A IN (`REG QUERY HKLM\SOFTWARE\Citrix\Xentools /v Install_Dir 2^>nul`) DO (
    set XTInstallDir=%%C
	)
REM Collect XenTools version information from registry
FOR /F "usebackq skip=2 tokens=1-3" %%A IN (`REG QUERY HKLM\SOFTWARE\Citrix\Xentools /v MajorVersion 2^>nul`) DO (
    set /a MajorVerReg=%%C
)

FOR /F "usebackq skip=2 tokens=1-3" %%A IN (`REG QUERY HKLM\SOFTWARE\Citrix\Xentools /v MinorVersion 2^>nul`) DO (
    set /a MinorVerReg=%%C
)
FOR /F "usebackq skip=2 tokens=1-3" %%A IN (`REG QUERY HKLM\SOFTWARE\Citrix\Xentools /v MicroVersion 2^>nul`) DO (
    set /a MicroVerReg=%%C
)
FOR /F "usebackq skip=2 tokens=1-3" %%A IN (`REG QUERY HKLM\SOFTWARE\Citrix\Xentools /v BuildVersion 2^>nul`) DO (
    set /a BuildVerReg=%%C
)

REM Collect important registry entries
mkdir %bugpath%\registry
reg export "HKLM\SYSTEM\CurrentControlSet\Control" "%bugpath%\registry\control.reg" /y > NUL 2>&1
reg export "HKLM\SOFTWARE\Citrix" "%bugpath%\registry\SWcitrix.reg" /y > NUL 2>&1
reg export "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall" "%bugpath%\registry\uninstall.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\xenvif" "%bugpath%\registry\xenvif.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\xenvbd" "%bugpath%\registry\xenvbd.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\xenSvc" "%bugpath%\registry\xensvc.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\xennet" "%bugpath%\registry\xennet.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\xenlite" "%bugpath%\registry\xenlite.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\xeniface" "%bugpath%\registry\xeniface.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\xenfilt" "%bugpath%\registry\xenfilt.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\xendisk" "%bugpath%\registry\xendisk.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\xenbus" "%bugpath%\registry\xenbus.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\xen" "%bugpath%\registry\xen.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\tcpip" "%bugpath%\registry\tcpip.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\tcpip6" "%bugpath%\registry\tcpip6.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\netbt" "%bugpath%\registry\netbt.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Services\LanmanWorkstation" "%bugpath%\registry\lanmanworkstation.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\CurrentControlSet\Enum" "%bugpath%\registry\enum.reg" /y > NUL 2>&1

REM Check for 64 Bit Keys
reg query HKLM\Software\Wow6432node\Citrix > NUL 2>&1
if %ERRORLEVEL% == 0 (
reg export "HKLM\Software\Wow6432node\Citrix\XenToolsInstaller" "%bugpath%\registry\XenToolsInstaller.reg" /y > NUL 2>&1
)
	
REM Identify Running OS then run collection commands for that version

ver | find "XP" > nul
if %ERRORLEVEL% == 0 goto ver_xp

ver | find "2000" > nul
if %ERRORLEVEL% == 0 goto ver_2000

ver | find "NT" > nul
if %ERRORLEVEL% == 0 goto ver_nt

if not exist %SystemRoot%\system32\systeminfo.exe goto warnthenexit

REM set vmosname=systeminfo |find "OS Name"
systeminfo | find "OS Name" > %bugpath%\osname.txt

FOR /F "usebackq delims=: tokens=2" %%i IN (%bugpath%\osname.txt) DO set vers=%%i

echo %vers% | find "Windows 10" > nul
if %ERRORLEVEL% == 0 goto ver_10

echo %vers% | find "Windows 8" > nul
if %ERRORLEVEL% == 0 goto ver_8

echo %vers% | find "Windows 7" > nul
if %ERRORLEVEL% == 0 goto ver_7

echo %vers% | find "2012" > nul
if %ERRORLEVEL% == 0 goto ver_2012

echo %vers% | find "Windows Server 2008" > nul
if %ERRORLEVEL% == 0 goto ver_2008

echo %vers% | find "2003" > nul
if %ERRORLEVEL% == 0 goto ver_2003

echo %vers% | find "Windows Vista" > nul
if %ERRORLEVEL% == 0 goto ver_vista

goto warnthenexit

:ver_10
:Run Windows 10 specific commands here.
echo Windows 10
cd %bugpath%
echo %MajorVerReg%.%MinorVerReg%.%MicroVerReg%.%BuildVerReg% > xt-reg-version.txt
echo %XTInstallDir% > xt-install-dir.txt
echo Generating MSInfo file as NFO - human readable version of data
msinfo32 /nfo msinfo.nfo
echo Generating MSInfo file as text file - script friendly version of data
msinfo32 /report msinfo.txt
echo Copying logfiles to bugtool...
mkdir programfiles64
mkdir programfiles
mkdir programdata
xcopy /Y /C /S c:\programdata\citrix\* programdata  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.txt" programfiles64  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.log" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.config" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.install*" programfiles64  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.txt" programfiles  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.log" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.config" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.install*" programfiles  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.dev.log  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.setup.log  > NUL 2>&1
echo Capturing pnputil -e output...
pnputil.exe -e > pnputil-e.out
echo Capturing state of WMI repository (will fail if not ran as administrator)...
C:\Windows\System32\wbem\winmgmt /verifyrepository > wmistate.out
echo Exporting System event log...
wevtutil epl System system.evtx
echo Exporting Application event log...
wevtutil epl Application application.evtx
cd ..
echo Finalizing process and creating ZIP file...
goto manifest

:ver_8
:Run Windows 8 specific commands here.
echo Windows 8
cd %bugpath%
echo %MajorVerReg%.%MinorVerReg%.%MicroVerReg%.%BuildVerReg% > xt-reg-version.txt
echo %XTInstallDir% > xt-install-dir.txt
echo Generating MSInfo file as NFO - human readable version of data
msinfo32 /nfo msinfo.nfo
echo Generating MSInfo file as text file - script friendly version of data
msinfo32 /report msinfo.txt
echo Copying logfiles to bugtool...
mkdir programfiles64
mkdir programfiles
mkdir programdata
xcopy /Y /C /S c:\programdata\citrix\* programdata  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.txt" programfiles64  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.log" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.config" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.install*" programfiles64  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.txt" programfiles  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.log" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.config" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.install*" programfiles  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.dev.log  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.setup.log  > NUL 2>&1
echo Capturing pnputil -e output...
pnputil.exe -e > pnputil-e.out
echo Capturing state of WMI repository (will fail if not ran as administrator)...
C:\Windows\System32\wbem\winmgmt /verifyrepository > wmistate.out
echo Exporting System event log...
wevtutil epl System system.evtx
echo Exporting Application event log...
wevtutil epl Application application.evtx
cd ..
echo Finalizing process and creating ZIP file...
goto manifest

:ver_2012
:Run Windows 2012 specific commands here.
echo Windows 2012
cd %bugpath%
echo %MajorVerReg%.%MinorVerReg%.%MicroVerReg%.%BuildVerReg% > xt-reg-version.txt
echo %XTInstallDir% > xt-install-dir.txt
echo Generating MSInfo file as NFO - human readable version of data
msinfo32 /nfo msinfo.nfo
echo Generating MSInfo file as text file - script friendly version of data
msinfo32 /report msinfo.txt
echo Copying logfiles to bugtool...
mkdir programfiles64
mkdir programfiles
mkdir programdata
xcopy /Y /C /S c:\programdata\citrix\* programdata  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.txt" programfiles64  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.log" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.config" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.install*" programfiles64  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.txt" programfiles  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.log" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.config" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.install*" programfiles  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.dev.log  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.setup.log  > NUL 2>&1
echo Capturing pnputil -e output...
pnputil.exe -e > pnputil-e.out
echo Capturing state of WMI repository (will fail if not ran as administrator)...
C:\Windows\System32\wbem\winmgmt /verifyrepository > wmistate.out
echo Exporting System event log...
wevtutil epl System system.evtx
echo Exporting Application event log...
wevtutil epl Application application.evtx
cd ..
echo Finalizing process and creating ZIP file...
goto manifest


:ver_7
:Run Windows 7 specific commands here.
echo Windows 7
cd %bugpath%
echo %MajorVerReg%.%MinorVerReg%.%MicroVerReg%.%BuildVerReg% > xt-reg-version.txt
echo %XTInstallDir% > xt-install-dir.txt
echo Generating MSInfo file as NFO - human readable version of data
msinfo32 /nfo msinfo.nfo
echo Generating MSInfo file as text file - script friendly version of data
msinfo32 /report msinfo.txt
echo Copying logfiles to bugtool...
mkdir programfiles64
mkdir programfiles
mkdir programdata
xcopy /Y /C /S c:\programdata\citrix\* programdata  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.txt" programfiles64  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.log" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.config" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.install*" programfiles64  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.txt" programfiles  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.log" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.config" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.install*" programfiles  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.dev.log  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.setup.log  > NUL 2>&1
echo Capturing pnputil -e output...
pnputil.exe -e > pnputil-e.out
echo Capturing state of WMI repository (will fail if not ran as administrator)...
C:\Windows\System32\wbem\winmgmt /verifyrepository > wmistate.out
echo Exporting System event log...
wevtutil epl System system.evtx
echo Exporting Application event log...
wevtutil epl Application application.evtx
cd ..
echo Finalizing process and creating ZIP file...
goto manifest


:ver_2008
:Run Windows Server 2008 specific commands here.
echo Windows Server 2008
cd %bugpath%
echo %MajorVerReg%.%MinorVerReg%.%MicroVerReg%.%BuildVerReg% > xt-reg-version.txt
echo %XTInstallDir% > xt-install-dir.txt
echo Generating MSInfo file as NFO - human readable version of data
msinfo32 /nfo msinfo.nfo
echo Generating MSInfo file as text file - script friendly version of data
msinfo32 /report msinfo.txt
echo Copying logfiles to bugtool...
mkdir programfiles64
mkdir programfiles
mkdir programdata
xcopy /Y /C /S c:\programdata\citrix\* programdata  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.txt" programfiles64  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.log" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.config" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.install*" programfiles64  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.txt" programfiles  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.log" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.config" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.install*" programfiles  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.dev.log  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.setup.log  > NUL 2>&1
echo Capturing pnputil -e output...
pnputil.exe -e > pnputil-e.out
echo Capturing state of WMI repository (will fail if not ran as administrator)...
C:\Windows\System32\wbem\winmgmt /verifyrepository > wmistate.out
echo Exporting System event log...
wevtutil epl System system.evtx
echo Exporting Application event log...
wevtutil epl Application application.evtx
cd ..
echo Finalizing process and creating ZIP file...
goto manifest

:ver_vista
:Run Windows Vista specific commands here.
echo Windows Vista
goto exit

:ver_2003
:Run Windows Server 2003 specific commands here.
echo Windows Server 2003
cd %bugpath%
echo %MajorVerReg%.%MinorVerReg%.%MicroVerReg%.%BuildVerReg% > xt-reg-version.txt
echo %XTInstallDir% > xt-install-dir.txt
echo Generating MSInfo file as NFO - human readable version of data
msinfo32 /nfo msinfo.nfo
echo Generating MSInfo file as text file - script friendly version of data
msinfo32 /report msinfo.txt
echo Copying logfiles to bugtool...
mkdir programfiles64
mkdir programfiles
mkdir programdata
xcopy /Y /C /S c:\programdata\citrix\* programdata  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.txt" programfiles64  > NUL 2>&1
copy "c:\Program Files (x86)\Citrix\XenTools\*.log" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.config" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\Citrix\XenTools\Installer\*.install*" programfiles64  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.txt" programfiles  > NUL 2>&1
copy "c:\Program Files\Citrix\XenTools\*.log" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.config" programfiles  > NUL 2>&1
copy "C:\Program Files\Citrix\XenTools\Installer\*.install*" programfiles  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.dev.log  > NUL 2>&1
xcopy /Y /C C:\Windows\Inf\setupapi.setup.log  > NUL 2>&1
echo Capturing pnputil -e output...
pnputil.exe -e > pnputil-e.out
echo Capturing state of WMI repository (will fail if not ran as administrator)...
C:\Windows\System32\wbem\winmgmt /verifyrepository > wmistate.out
echo Exporting System event log...
wevtutil epl System system.evtx
echo Exporting Application event log...
wevtutil epl Application application.evtx
cd ..
echo Finalizing process and creating ZIP file...
goto manifest

:ver_xp
:Run Windows XP specific commands here.
echo Windows XP
goto exit

:ver_2000
:Run Windows 2000 specific commands here.
echo Windows 2000
goto exit

:ver_nt
:Run Windows NT specific commands here.
echo Windows NT
goto exit

:warnthenexit
echo Machine undetermined.

:manifest
cd %bugpath%
echo ^<DataInfo^> > manifest.xml
echo ^<UTCDate^>%UTC_DATE_TIME%^</UTCDate^> >> manifest.xml
echo  ^<Date^>%LOCAL_DATE_TIME%^</Date^> >> manifest.xml
echo  ^<Product^>XenTools^</Product^> >> manifest.xml
echo  ^<ProductVersion^>%MajorVerReg%.%MinorVerReg%.%MicroVerReg%.%BuildVerReg%^</ProductVersion^> >> manifest.xml
echo  ^<ClientTool Name="XenTools bugtool generator" Version="%ToolVersion%" /^> >> manifest.xml
echo ^</DataInfo^> >> manifest.xml
cd %TEMP%
goto zipit

:zipit
echo Set objArgs = WScript.Arguments > _zipIt.vbs
echo InputFolder = objArgs(0) >> _zipIt.vbs
echo ZipFile = objArgs(1) >> _zipIt.vbs
echo CreateObject("Scripting.FileSystemObject").CreateTextFile(ZipFile, True).Write "PK" ^& Chr(5) ^& Chr(6) ^& String(18, vbNullChar) >> _zipIt.vbs
echo Set objShell = CreateObject("Shell.Application") >> _zipIt.vbs
echo Set source = objShell.NameSpace(InputFolder).Items >> _zipIt.vbs
echo objShell.NameSpace(ZipFile).CopyHere(source) >> _zipIt.vbs
echo wScript.Sleep 20000 >> _zipIt.vbs
echo WScript.Quit >> _zipIt.vbs
CScript  _zipIt.vbs  %bugpath%  %TEMP%\xt-bugtool-%dtstring%.zip
goto cleanup

:cleanup
move /Y xt-bugtool-%dtstring%.zip %zippath%
del _zipIt.vbs
rmdir /S /Q %dtstring%
goto exit

:usage
IF "%1"=="" echo "USAGE: xtbugtool.bat <Destination Path for ZIP file>"

:exit

:EOF
