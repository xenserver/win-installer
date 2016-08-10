@echo off
REM XT-bugtool generator - v1.7 by Blaine A. Anaya
REM This script collects necessary files used to identify 
REM where a PV tools installation issue has occurred
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
SET ToolVersion=1.8

CALL :I18N
IF "%1"=="" GOTO usage

:checkPrivileges
NET FILE 1>NUL 2>NUL
if '%errorlevel%' == '0' ( goto gotPrivileges ) else ( goto getPrivileges )

:getPrivileges
if '%1'=='ELEV' (echo ELEV & shift /1 & goto gotPrivileges)
echo *** This script needs to be run with administrator priviledges ***

setlocal DisableDelayedExpansion
set "batchPath=%~0"
setlocal EnableDelayedExpansion
ECHO Set UAC = CreateObject^("Shell.Application"^) > "%temp%\OEgetPrivileges.vbs"
ECHO args = "ELEV %USERNAME% " >> "%temp%\OEgetPrivileges.vbs"
ECHO For Each strArg in WScript.Arguments >> "%temp%\OEgetPrivileges.vbs"
ECHO args = args ^& strArg ^& " "  >> "%temp%\OEgetPrivileges.vbs"
ECHO Next >> "%temp%\OEgetPrivileges.vbs"
ECHO UAC.ShellExecute "!batchPath!", args, "", "runas", 1 >> "%temp%\OEgetPrivileges.vbs"
"%SystemRoot%\System32\WScript.exe" "%temp%\OEgetPrivileges.vbs" %*
exit /B

:gotPrivileges
if NOT '%1'=='ELEV' goto :noelev
set USER=%2
shift /1
shift /1
goto :cont
:noelev
set USER=%USERNAME%
:cont
setlocal & pushd .
cd /d %~dp0

set zippath=%1

SETLOCAL ENABLEDELAYEDEXPANSION

SET UTC_DATE_TIME= null
SET LOCAL_DATE_TIME= null

REM Get the UTC date-time string to use
CALL :GetUTCDate UTC_DATE_TIME

REM Get the Local date-time string to use
CALL :GetLocalDate LOCAL_DATE_TIME

GOTO :bugtool

REM Sub routine to get the current UTC date as formatted string YYY-MM-DDTHH:MM:SSZ
:GetUTCDate outString
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
:GetLocalDate outString
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
REM Set PV Tools install directory as identified in the registry
FOR /F "usebackq skip=2 tokens=1-2*" %%A IN (`REG QUERY HKLM\SOFTWARE\%REGKEY% /v Install_Dir 2^>nul`) DO (
    set XTInstallDir=%%C
	)
REM Collect PV Tools version information from registry
FOR /F "usebackq skip=2 tokens=1-3" %%A IN (`REG QUERY HKLM\SOFTWARE\%REGKEY% /v MajorVersion 2^>nul`) DO (
    set /a MajorVerReg=%%C
)

FOR /F "usebackq skip=2 tokens=1-3" %%A IN (`REG QUERY HKLM\SOFTWARE\%REGKEY% /v MinorVersion 2^>nul`) DO (
    set /a MinorVerReg=%%C
)
FOR /F "usebackq skip=2 tokens=1-3" %%A IN (`REG QUERY HKLM\SOFTWARE\%REGKEY% /v MicroVersion 2^>nul`) DO (
    set /a MicroVerReg=%%C
)
FOR /F "usebackq skip=2 tokens=1-3" %%A IN (`REG QUERY HKLM\SOFTWARE\%REGKEY% /v BuildVersion 2^>nul`) DO (
    set /a BuildVerReg=%%C
)

REM Collect important registry entries
mkdir %bugpath%\registry
reg export "HKLM\SYSTEM\CurrentControlSet\Control" "%bugpath%\registry\control.reg" /y > NUL 2>&1
reg export "HKLM\SOFTWARE\%REGCO%" "%bugpath%\registry\SW%REGCO%.reg" /y > NUL 2>&1
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
reg export "HKLM\Software\Policies\Microsoft\Windows\WindowsUpdate" "%bugpath%\registry\wupolicy.reg" /y > NUL 2>&1
reg export "HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer" "%bugpath%\registry\userexplorerpolicy.reg" /y > NUL 2>&1
reg export "HKLM\SYSTEM\Internet Communication Management\Internet Communication" "%bugpath%\registry\internetcom.reg" /y > NUL 2>&1
reg export "HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\WindowsUpdate" "%bugpath%\registry\userwupolicy.reg" /y > NUL 2>&1
REM See https://technet.microsoft.com/en-us/library/dd939844 for details
reg export "HKLM\Software\Microsoft\Windows\CurrentVersion\WindowsUpdate" "%bugpath%\registry\wu.reg" /y > NUL 2>&1

REM Check for 64 Bit Keys
reg query HKLM\Software\Wow6432node\%REGCO% > NUL 2>&1
if %ERRORLEVEL% == 0 (
reg export "HKLM\Software\Wow6432node\%REGCO%\%INSTALLNAME%" "%bugpath%\registry\%INSTALLNAME%.reg" /y > NUL 2>&1
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

:copylogs
cd %bugpath%
echo %MajorVerReg%.%MinorVerReg%.%MicroVerReg%.%BuildVerReg% > xt-reg-version.txt
echo %XTInstallDir% > xt-install-dir.txt
echo Generating MSInfo file as NFO - human readable version of data
msinfo32 /nfo msinfo.nfo
echo Generating MSInfo file as text file - script friendly version of data
msinfo32 /report msinfo.txt
if NOT %ERRORLEVEL%==0 echo "msi info failed" >> xtbugtool.log
echo Copying logfiles to bugtool...
mkdir programfiles64
mkdir programfiles
mkdir programdata
mkdir tasks
xcopy /Y /C /S c:\programdata\%COMPANY%\* programdata  > NUL 2>&1
if NOT %ERRORLEVEL%==0 echo "No programdata found" >> xtbugtool.log
xcopy /Y /C /S "c:\programdata\%GUESTLOGS%" programdata > NUL 2>&1
if NOT %ERRORLEVEL%==0 echo "No agent logs found" >> xtbugtool.log
xcopy /Y /C /S "c:\windows\system32\tasks" tasks > NUL 2>&1
if NOT %ERRORLEVEL%==0 echo "No task logs found" >> xtbugtool.log
copy "c:\Program Files (x86)\%TOOLPATH%\*.txt" programfiles64  > NUL 2>&1
copy "c:\Program Files (x86)\%TOOLPATH%\*.log" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\%TOOLPATH%\Installer\*.config" programfiles64  > NUL 2>&1
copy "C:\Program Files (x86)\%TOOLPATH%\Installer\*.install*" programfiles64  > NUL 2>&1
copy "c:\Program Files\%TOOLPATH%\*.txt" programfiles  > NUL 2>&1
copy "c:\Program Files\%TOOLPATH%\*.log" programfiles  > NUL 2>&1
copy "C:\Program Files\%TOOLPATH%\Installer\*.config" programfiles  > NUL 2>&1
copy "C:\Program Files\%TOOLPATH%\Installer\*.install*" programfiles  > NUL 2>&1
echo Capturing pnputil -e output...
pnputil.exe -e > pnputil-e.out
if NOT %ERRORLEVEL%==0 echo "pnputil failed" >> xtbugtool.log
echo Capturing state of WMI repository (will fail if not ran as administrator)...
C:\Windows\System32\wbem\winmgmt /verifyrepository > wmistate.out
if NOT %ERRORLEVEL%==0 echo "wmi failed" >> xtbugtool.log
echo Exporting System event log...
wevtutil epl System system.evtx
if NOT %ERRORLEVEL%==0 echo "system log failed" >> xtbugtool.log
echo Exporting Application event log...
wevtutil epl Application application.evtx
if NOT %ERRORLEVEL%==0 echo "application log failed" >> xtbugtool.log
cd ..
echo Finalizing process and creating ZIP file...
exit /b

:vp_setupapicopy
REM Copy setupapi on vista plus
xcopy /Y /C C:\Windows\Inf\setupapi.dev.log %bugpath% > NUL 2>&1
if NOT %ERRORLEVEL%==0 echo "No setupapi.dev.log found" >> %bugpath%\xtbugtool.log

xcopy /Y /C C:\Windows\Inf\setupapi.setup.log %bugpath% > NUL 2>&1
if NOT %ERRORLEVEL%==0 echo "No setupapi.setup.log found" >> %bugpath%\xtbugtool.log

xcopy /Y /C C:\Windows\Inf\setupapi.app.log %bugpath% > NUL 2>&1
if NOT %ERRORLEVEL%==0 echo "No setupapi.app.log found" >> %bugpath%\xtbugtool.log
exit /b

:pv_setupapicopy
REM Cop setupapi on pre-vista 
xcopy /Y /C C:\Windows\setupapi.log %bugpath% > NUL 2>&1
if NOT %ERRORLEVEL%==0 echo "No setupapi.log found" >> %bugpath%\xtbugtool.log
exit /b

:ver_10
:Run Windows 10 specific commands here.
echo Windows 10
call :copylogs
call :vp_setupapicopy
goto manifest

:ver_8
:Run Windows 8 specific commands here.
echo Windows 8
call :copylogs
call :vp_setupapicopy
goto manifest

:ver_2012
:Run Windows 2012 specific commands here.
echo Windows 2012
call :copylogs
call :vp_setupapicopy
goto manifest

:ver_7
:Run Windows 7 specific commands here.
echo Windows 7
call :copylogs
call :vp_setupapicopy
goto manifest

:ver_2008
:Run Windows Server 2008 specific commands here.
echo Windows Server 2008
call :copylogs
call :vp_setupapicopy
goto manifest

:ver_vista
:Run Windows Vista specific commands here.
echo Windows Vista
call :copylogs
call :vp_setupapicopy
goto manifest

:ver_2003
:Run Windows Server 2003 specific commands here.
call :copylogs
call :pv_setupapicopy
goto manifest

:ver_xp
:Run Windows XP specific commands here.
echo Windows XP
call :copylogs
call :pv_setupapicopy
goto manifest

:ver_2000
:Run Windows 2000 specific commands here.
echo Windows 2000
goto exit

:ver_nt
:Run Windows NT specific commands here.
echo Windows NT
goto exit

:warnthenexit
echo  Windows version undetermined.
call :copylogs
call :vp_setupapicopy
goto :manifest

:manifest
cd %bugpath%
echo ^<DataInfo^> > manifest.xml
echo ^<UTCDate^>%UTC_DATE_TIME%^</UTCDate^> >> manifest.xml
echo  ^<Date^>%LOCAL_DATE_TIME%^</Date^> >> manifest.xml
echo  ^<Product^>%TOOLSNAME%^</Product^> >> manifest.xml
echo  ^<ProductVersion^>%MajorVerReg%.%MinorVerReg%.%MicroVerReg%.%BuildVerReg%^</ProductVersion^> >> manifest.xml
echo  ^<ClientTool Name="%TOOLSNAME% bugtool generator" Version="%ToolVersion%" /^> >> manifest.xml
echo ^</DataInfo^> >> manifest.xml
cd %TEMP%
goto zipit

:zipit
REM Write a VBS script to zip the directory
echo Set objArgs = WScript.Arguments > _zipIt.vbs
echo InputFolder = objArgs(0) >> _zipIt.vbs
echo ZipFile = objArgs(1) >> _zipIt.vbs
echo CreateObject("Scripting.FileSystemObject").CreateTextFile(ZipFile, True).Write "PK" ^& Chr(5) ^& Chr(6) ^& String(18, vbNullChar) >> _zipIt.vbs
echo Set objShell = CreateObject("Shell.Application") >> _zipIt.vbs
echo Set source = objShell.NameSpace(InputFolder).Items >> _zipIt.vbs
echo objShell.NameSpace(ZipFile).CopyHere source >> _zipIt.vbs
echo wScript.Sleep 20000 >> _zipIt.vbs
echo WScript.Quit >> _zipIt.vbs
REM Delete all empty folders (via a robocopy trick)
ROBOCOPY %bugpath% %bugpath% /S /MOVE > NUL 2>&1
REM And run the script we have written
CScript  _zipIt.vbs  %bugpath%  %TEMP%\xt-bugtool-%dtstring%.zip
goto cleanup

:cleanup
move /Y xt-bugtool-%dtstring%.zip %zippath%
echo y|cacls %zippath% /G %USER%:F 
del _zipIt.vbs
rmdir /S /Q %dtstring%
goto exit

:usage
IF "%1"=="" echo "USAGE: xtbugtool.bat <Destination Path for ZIP file>"

REM Autogenerated section, do not modify
:I18N
REM I18N
SET COMPANY=Citrix
SET TOOLPATH=Citrix\XenTools
SET REGKEY=Citrix\Xentools
SET REGCO=Citrix
SET INSTALLNAME=XenToolsInstaller
SET GUESTLOGS=Citrix Systems, Inc
SET TOOLSNAME=XenTools
exit /b
REM ENDI18N

:exit
goto :EOF

:EOF
