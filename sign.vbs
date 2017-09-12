'==========================================================================
'
' VBScript Source File -- Created with SAPIEN Technologies PrimalScript 2017
'
' NAME: MSI Packge resign and package tool
'
' AUTHOR: Ben Chalmers, Lin Liu
' DATE  : 8/29/2017
'
' COMMENT: 
'
'==========================================================================
Option Explicit

'Dim signCommand:signCommand = Wscript.Arguments(1) 'first argument is the sign command
Const Quote = """"
Const FakeQuote = "^"
Dim  SignCommand ': SignCommand = "signtool sign /a /s my /n " & Quote &"XENBUS" & Quote & " /t http://timestamp.verisign.com/scripts/timestamp.dll"
Const msiOpenDatabaseModeReadOnly = 0
Const msiOpenDatabaseModeTransact = 1

Const msiReadStreamAnsi = 2 'the ANSI bytes translated to a Unicode BSTR.

Dim installer : Set installer = Wscript.CreateObject("WindowsInstaller.Installer") 
Dim WshShell : Set WshShell = CreateObject("Wscript.Shell")
Dim fso: Set fso = CreateObject("Scripting.FileSystemObject")


Const msiViewModifyInsert         = 1
Const msiViewModifyUpdate         = 2
Const msiViewModifyAssign         = 3
Const msiViewModifyReplace        = 4
Const msiViewModifyDelete         = 6

Const wsRunDoesNotActive = 7
Const wsWaitUntilReturn = True

'Following three list will be fixed in the compile Time, 'Define in build.py
'Define the files in msi that are need to be signed
Dim signFileArr: signFileArr = Array( ">>>INSERT-FILE-LIST-IN-MSI-HERE<<<")'like "brandsat.dll","BrandSupport.dll","qnetsettings.exe"
'Dim signFileArr: signFileArr = Array( "brandsat.dll","BrandSupport.dll","NetSettings.exe","Dpriv.exe","ManagementAgentUpdater.exe","XenGuestLib.Dll","Interop.NetFwTypeLib.dll","Interop.TaskScheduler.dll","XenVss.dll","VssClient.dll","GuestAgent.exe","InstallAgent.exe","PInvokeWrap.dll","HelperFunctions.dll","HardwareDevice.dll","PVDriversRemoval.dll","Uninstall.Exe")'like "brandsat.dll","BrandSupport.dll","qnetsettings.exe"

'This array contain the binaries that need to be sign directly under current directory				         
Dim signDirectArray:signDirectArray = Array(">>>INSERT-FILE-LIST-OUT-MSI-HERE<<<") ' like "Setup.exe" _
'Dim signDirectArray:signDirectArray = Array("setup.exe") ' like "Setup.exe" _
'This array contain all the msi files needs to be signed
Dim signMsiArray:signMsiArray = Array(">>>INSERT-MSI-LIST-HERE<<<") 'like "managementagentx64.msi", "managementagentx86.msi" 
'Dim signMsiArray:signMsiArray = Array("managementagentx64.msi","managementagentx86.msi","zh-cn\managementagentx64.msi","zh-cn\managementagentx86.msi") 'like "managementagentx64.msi", "managementagentx86.msi"

'Const msiCultureFolder = ">>>INSERT-MSI-CULTURE-FOLDER-HERE<<<" 'cultured MSI folder, like zh-cn

'map files to be signed to their real filename
'filename in msi ===> filename in signFileArr
'example: oeecprlu|BrandSupport.ll ==>BrandSupport.dll
Dim signFileNameMap: Set signFileNameMap =  CreateObject("Scripting.Dictionary")


'map files to be signed to their real filename
'filenuuid in msi ===> filename in signFileArr
'example: xenagentdllx64.245FF6BE_CF5C_4DA4_ADBD_D2F9123AF86E ==>BrandSupport.dll
Dim signFileUUIDMap: Set signFileUUIDMap =  CreateObject("Scripting.Dictionary")

Const InvalidArgument = 3

Dim current_directory:current_directory = fso.GetAbsolutePathName(".\")&"\"
'Test only, init test environment
'Call RestTestEnvironment()

Dim database
Dim cabname,interCabname,cabnameNoExt:  cabname = "" :interCabname="":cabnameNoExt=""

Dim tempFolder: tempFolder = Empty

Const  finalZipFileName = "installer.zip"

'The main entry point 
Call MainEntryPoint


Function Usage()
Dim helpStr
helpStr = "sign.vbs "& Quote &"signing command"& Quote &_ 
	vbNewLine &_
	vbNewLine & "Please replace "& Quote & " with "&FakeQuote& " in your sign command" &_
	vbNewLine & "Example:" &_
	vbNewLine & "On a system where a certificate For "& Quote &"My Company Inc."& Quote & " has been installed as a personal certificate" &_
	vbNewLine &_
	vbNewLine & "sign.vbs "& Quote&"signtool sign /a /s my /n "&FakeQuote&"My Company Inc."&FakeQuote&" /t http://timestamp.verisign.com/scripts/timestamp.dll"&Quote

	WScript.Echo(helpStr)
End Function

Function ParseSignCommand
Dim argNum: argNum = WScript.Arguments.Count
	Const HelpCommand = "-h"
	If argNum <> 1 Then
		Call Usage()
		WScript.Quit InvalidArgument	
	End If
	Dim firstArg:firstArg = Wscript.Arguments(0) 'first argument is the sign command
	If StrComp(firstArg,HelpCommand,vbTextCompare) = 0 Then
		Call Usage()
		WScript.Quit InvalidArgument
	End If
	
	SignCommand=Replace(firstArg,FakeQuote,Quote)
	'WScript.Echo(SignCommand)
End Function

Function GetTempFolder
	Const WindowsFolder = 0
	Const SystemFolder = 1
	Const TemporaryFolder = 2
	
	GetTempFolder = fso.GetSpecialFolder(TemporaryFolder)
End Function 

Function MainEntryPoint()	
	' ' Parse arguments
	Call ParseSignCommand
	'Sign simple binaries
	Call SignDirectFile
	'Sign all MSI files
	Dim msifile
	For Each msifile In signMsiArray
		Call SignMSIFile(msifile)
	Next
' 	
	Call ZipResult
	
	WScript.Echo("Sign success")
End Function

Function ZipResult
	'zip current dir to installer.zip
	If fso.FileExists(finalZipFileName) Then
		Call fso.DeleteFile(finalZipFileName)
	End If 
	Dim systemTempFolder : systemTempFolder = GetTempFolder()
	dim tempFolder: tempFolder =  fso.GetTempName()
	call fso.CreateFolder(systemTempFolder &"\"& tempFolder)
	Dim tempPath:tempPath = systemTempFolder& "\"&tempFolder&"\" &  finalZipFileName
	call ArchiveFolder(tempPath,".")
	Call fso.CopyFile(tempPath,finalZipFileName,true)
	Call fso.DeleteFolder(systemTempFolder&"\"&tempFolder,true)
End Function 

Function SignFile(filepath)
	Dim command: command = SignCommand & " " & filepath
	Dim signResult:  signResult = WshShell.run(command,wsRunDoesNotActive,wsWaitUntilReturn)
	If signResult <> 0 Then
		WScript.Echo("sign file " & filepath &" with error code: "& signResult)
		Wscript.Quit signResult
	End If 
End Function 

Function SignDirectFile()
	Dim file
	For Each file In signDirectArray
		Dim filepath: filepath = current_directory & file
		Call SignFile(filepath)
	Next
End Function

Function SignMSIFile(msiFile)
	'Reset global environment
	'RestTestEnvironment
	tempFolder=fso.GetTempName
	Dim targetTempFolder: targetTempFolder = current_directory & tempFolder & "\"
	fso.CreateFolder(targetTempFolder)
	Dim msiFilename : msiFilename = fso.GetFileName(msiFile)
	Dim tempMsiPath:tempMsiPath = targetTempFolder & msiFilename
	Dim sourceMsiPath:sourceMsiPath = current_directory &  msiFile
	'Copy the MSI database to the tempfolder
	Call fso.CopyFile(sourceMsiPath, tempMsiPath)
	
	Set database = installer.OpenDatabase(tempMsiPath, msiOpenDatabaseModeTransact) 
	Call signFileUUIDMap.RemoveAll
	Call signFileNameMap.RemoveAll
	cabname = Empty:interCabname = Empty:interCabname = Empty:cabnameNoExt = Empty
	
	'Extract cat file into current folder
	'The result is stored in cabname, which is extracted from MSI file
	 Call ExportCabFile(targetTempFolder)
	
	'Get the files needs to be signed
	'The result is stored in signFileUUIDMap
	 Call GetSignFilesNameInMsi()

	'Extract cab files into temp folder
	 Dim msiCabFilePath:msiCabFilePath = targetTempFolder&cabname
	 Call ExtractCabFile(msiCabFilePath,targetTempFolder)
	
	'Sign the necessary files in temp folder
	'This function will use the global sign command
	 Call SignIncludedBinaries(targetTempFolder)
	
	'Make cab file, with signed binaries in it.
	 Call MakeCabFile(targetTempFolder,msiFilename)
	
	'Delete the unsigned cab file
	'This function will use the glboal database
	 Call DeleteOriginalCab()
	
	'Insert the signed cab file back to MSI file
	'This function will use the glboal database, from the cabname
	Call InsertCabFileBackToMSI(targetTempFolder)
	
	'Release global resource
	Set database = Nothing
	'Set installer = Nothing
	
	'Sign the final MSI file
	Dim command: command = SignCommand & " " & tempMsiPath
	'WScript.Echo(command)
	Dim signResult:  signResult = WshShell.run(command,wsRunDoesNotActive,wsWaitUntilReturn)
	If signResult <> 0 Then
		WScript.Echo("sign msi error: "& signResult)
		Wscript.Quit signResult
	End If 
	
	'Replace the original MSI
	Call fso.DeleteFile(sourceMsiPath)
	Call fso.CopyFile(tempMsiPath,sourceMsiPath)
	
	'clear the tmp folder
	Dim deletePath:deletePath= Left(targetTempFolder, len(targetTempFolder)-1)
	fso.DeleteFolder(deletePath)
	 
	'Remind user success
	'WScript.Echo("sign MSI file: " & msiFile & " success")
	
End Function

Sub ArchiveFolder (zipFile, sFolder)

    With CreateObject("Scripting.FileSystemObject")
        zipFile = .GetAbsolutePathName(zipFile)
        sFolder = .GetAbsolutePathName(sFolder)

        With .CreateTextFile(zipFile, True)
            .Write Chr(80) & Chr(75) & Chr(5) & Chr(6) & String(18, chr(0))
        End With
    End With

    With CreateObject("Shell.Application")
        .NameSpace(zipFile).CopyHere .NameSpace(sFolder).Items

        Do Until .NameSpace(zipFile).Items.Count = _
                 .NameSpace(sFolder).Items.Count
            WScript.Sleep 1000 
        Loop
    End With

End Sub

' Function RestTestEnvironment
' 	If fso.FileExists(current_directory & szMSI) Then
' 		Call fso.DeleteFile(current_directory&szMSI)
' 	End If 
' 	Call fso.CopyFile(current_directory & originMSI, current_directory&szMSI)
' End Function

'return the file needs to be signed, Empty if no need sign found 
Function FindSignFile(filenameInMSI)
	Const sep = "|"
	Dim filename:filename = ""
	Dim sepLocation
	Const START_POSITION = 1
	Dim refinedFileNameInMsi
    sepLocation = InStr(START_POSITION,filenameInMSI,sep,vbTextCompare)
    If SetLocale = 0 Then
    	refinedFileNameInMsi = filenameInMSI
    Else
    	refinedFileNameInMsi = Right(filenameInMSI,Len(filenameInMSI)-sepLocation)
    End If 
	For Each filename In signFileArr
		If StrComp (refinedFileNameInMsi,filename,vbTextCompare) = 0 Then
			FindSignFile=filename
			Exit For	
		End If 
	Next
	FindSignFile=filename
End Function

Function SetSignFileNameMap(filenameInMSI)
	SetSignFileNameMap = False
	Dim filename: filename = FindSignFile(filenameInMSI)
	'find the file in the te be signed array, must be a file need to be signed
	If filename <> "" Then
		If Not signFileNameMap.Exists(filenameInMSI) Then
			Call signFileNameMap.Add(filenameInMSI,filename)
		End If 
		SetSignFileNameMap = True
	End If 
	
End Function

Function GetSignFilesNameInMsi()
	Dim msiView, MsiRecord
	Set msiView = database.OpenView("SELECT File,FileName, FileSize FROM File"):CheckInstallError
	msiView.Execute:CheckInstallError
	Dim msiFilename,msiFileID,signFileName
	Do
		 Set MsiRecord = msiView.Fetch:CheckInstallError
		 If MsiRecord Is Nothing Then Exit Do
		 msiFileID = MsiRecord.StringData(1)
		 msiFilename =MsiRecord.StringData(2)
		 Call SetSignFileNameMap(msiFilename) 
		 If signFileNameMap.Exists(msiFilename) Then
		 	signFileName = signFileNameMap.Item(msiFilename)
		 	Call signFileUUIDMap.Add(msiFileID,signFileName)
		 End If 
	Loop
	msiView.Close()
End Function					

' Check whether last command execute successfully
Sub CheckInstallError
	Dim message, errRec
	If Err = 0 Then Exit Sub
	message = Err.Source & " " & Hex(Err) & ": " & Err.Description
	If Not installer Is Nothing Then
		Set errRec = installer.LastErrorRecord
		If Not errRec Is Nothing Then message = message & vbNewLine & errRec.FormatText
	End If
	Fail message
End Sub

' promote error meeage and exit
Sub Fail(message)
	Wscript.Echo message
	Wscript.Quit 2
End Sub

' Test only sub to dump all the singed file map
Sub DumpSignMap(someMap)
    WScript.Echo("start to dump map:")
	Dim file
	For Each file In someMap 
		WScript.Echo(file & "===>"  & someMap.Item(file))
	Next
	 WScript.Echo("end of dump map:")
End Sub

'Extract the cab file from a  MSI database
'output: the path to the exported cab file
Function ExportCabFile(tempPath)
	Dim msiView:Set msiView = database.OpenView("SELECT DiskId, LastSequence, Cabinet FROM Media ORDER BY DiskId"):CheckInstallError
	Dim msiRecord
	msiView.Execute:'CheckInstallError
	dim index: index=0
	Do
		 Set msiRecord = msiView.Fetch:CheckInstallError
		 If msiRecord Is Nothing Then Exit Do
		 interCabname = msiRecord.StringData(3)
		 Dim lencab
		 lencab = Len(interCabname)
		 cabname =  Right(interCabname, lencab-1)'trim the first #
		 index = index + 1
		 Set msiRecord = Nothing
	Loop
	'Check Cab file number
	If index > 1 Then
		WScript.Echo "Warning: more than one cab file is found, may bring conflict, total: " & index
	End If 
	Call msiView.Close()
	
	Dim selectstr
	selectstr = ("SELECT `Name`, `Data` FROM _Streams WHERE Name = '"+cabname+"'")
	Set msiView = database.OpenView(selectstr):'CheckInstallError
	msiView.Execute:'CheckInstallError

	Do
	  Set msiRecord = msiView.Fetch:CheckInstallError
	  If msiRecord Is Nothing Then Exit Do
	  Dim data 
	  Dim dataIndex: dataIndex=2 'field specified as --->SELECT `Name`, `Data` 
	  Dim dataLength: dataLength=msiRecord.DataSize(dataIndex)
	  Dim targetCabPath:targetCabPath = tempPath & "\" & cabname
	  data = msiRecord.ReadStream(dataIndex,msiRecord.DataSize(dataIndex), msiReadStreamAnsi):CheckInstallError
	  With CreateObject("Scripting.FileSystemObject")
	        With .CreateTextFile(targetCabPath, True)
	            Dim a:a=1
	            dim b:b=Mid(data,1,dataLength)
				.Write(b)
				If Err.Number <> 0 Then
	                Wscript.Echo Cstr(a)
	                Wscript.Echo CStr(b)
	                Wscript.quit(1)
	            End If 
	            .Close()
	        End With
	  End With
	Loop
	Call msiView.Close
End Function 

'Extract the cab files into temp folder
'Return the temp folder path
Function ExtractCabFile(ByVal myZipFile, ByVal myTargetDir)
	Dim intOptions, objShell, objTarget,objSource,objFolder
	' Create the required Shell objects
    Set objShell = CreateObject("Shell.Application")
    Set objFolder = objShell.NameSpace(myZipFile)
    If (objFolder is nothing) then
		Wscript.Echo("cannot find source"&myZipFile)
	end If
	
	Set objSource = objFolder.Items()
	Set objTarget = objShell.NameSpace(myTargetDir)
	If (objTarget Is nothing) Then
		Wscript.Echo("cannot find target" & myTargetDir)
	end If
	
	Dim DISPLAY_PROCESSBAR_NOT_SHOW_FILENAME:DISPLAY_PROCESSBAR_NOT_SHOW_FILENAME = 256
	Dim DOES_NOT_DISPLAY_PROCESSBAR:DOES_NOT_DISPLAY_PROCESSBAR = 4
	objTarget.CopyHere objSource, DISPLAY_PROCESSBAR_NOT_SHOW_FILENAME
	
	' Release the objects
	Set objFolder = Nothing
    Set objSource = Nothing
    Set objTarget = Nothing
    Set objShell  = Nothing 
End Function

Function SignIncludedBinaries(tempFolder)
	Dim file, filePath
	For Each file In signFileUUIDMap 
		filePath = tempFolder &file
		Call SignFile(filePath)
	Next
End Function

Function DeleteOriginalCab
	Dim msiView,msiRecord
	Set msiView = database.OpenView("SELECT `Name`,`Data` FROM _Streams WHERE `Name`= '" & cabname & "'") : CheckInstallError
	msiView.Execute : CheckInstallError
	Set msiRecord = msiView.Fetch:CheckInstallError
	If msiRecord Is Nothing Then
		Wscript.Echo "Warning, cabinet stream not found in package: " & cabname
	Else
		msiView.Modify msiViewModifyDelete, msiRecord : CheckInstallError
	End If
	'Set sumInfo = Nothing ' must release stream
	msiView.Close: CheckInstallError
	database.Commit : CheckInstallError
End Function

'Make DDF file which is then used to make the cab file
Function MakeDDFile(targetCabFolder,msiDbName)
	' Create an install session and execute actions in order to perform directory resolution
	Dim msiView, msiRecord
	Const msiUILevelNone = 2
	Dim compressType : compressType = "MSZIP"
	' FileSystemObject.CreateTextFile
	Const OverwriteIfExist = -1
	Const FailIfExist      = 0
	' FileSystemObject.CreateTextFile and FileSystemObject.OpenTextFile
	Const OpenAsASCII   = 0 
	Const OpenAsUnicode = -1
	Const msidbFileAttributesNoncompressed = &h00002000
	Dim cabSize      : cabSize      = "CDROM"

	installer.UILevel = msiUILevelNone
	Dim session : Set session = installer.OpenPackage(database,1) : If Err <> 0 Then Fail "Database: " & msiDbName & ". Invalid installer package format"
	Dim stat : stat = session.DoAction("CostInitialize") : CheckInstallError
	If stat <> 1 Then Fail "CostInitialize failed, returned " & stat

	' Join File table to Component table in order to find directories
	Dim orderBy : orderBy = "Sequence"
	Set msiView = database.OpenView("SELECT File,FileName,Directory_,Sequence,File.Attributes FROM File,Component WHERE Component_=Component ORDER BY " & orderBy) : CheckInstallError
	msiView.Execute : CheckInstallError
	
	' Create DDF file and write header properties
	Dim FileSys : Set FileSys = CreateObject("Scripting.FileSystemObject") : CheckInstallError
	Dim DotPlace : DotPlace =  InStr(1, cabname, ".", vbTextCompare)
	If DotPlace <> 0 Then cabnameNoExt = Left(cabname,DotPlace-1) Else cabnameNoExt = cabname 
	Dim targetCabFilePath:targetCabFilePath=targetCabFolder&cabnameNoExt&".DDF"
	Dim outStream : Set outStream = FileSys.CreateTextFile(targetCabFilePath , OverwriteIfExist, OpenAsASCII) : CheckInstallError
	outStream.WriteLine "; Generated from " & msiDbName & " on " & Now
	outStream.WriteLine ".Set CabinetNameTemplate=" & cabnameNoExt & "*.CAB"
	outStream.WriteLine ".Set CabinetName1=" & cabname
	outStream.WriteLine ".Set ReservePerCabinetSize=8"
	outStream.WriteLine ".Set MaxDiskSize=" & cabSize
	outStream.WriteLine ".Set CompressionType=" & compressType
	outStream.WriteLine ".Set InfFileLineFormat=(*disk#*) *file#*: *file* = *Size*"
	outStream.WriteLine ".Set InfFileName=" & cabnameNoExt & ".INF"
	outStream.WriteLine ".Set RptFileName=" & cabnameNoExt & ".RPT"
	outStream.WriteLine ".Set InfHeader="
	outStream.WriteLine ".Set InfFooter="
	outStream.WriteLine ".Set DiskDirectoryTemplate=."
	outStream.WriteLine ".Set Compress=ON"
	outStream.WriteLine ".Set Cabinet=ON"
	
	' Fetch each file and request the source path, then verify the source path
	Dim fileKey, fileName, folder, sourcePath, delim, message, attributes,sequence
	Do
		Set msiRecord = msiView.Fetch : CheckInstallError
		If msiRecord Is Nothing Then Exit Do
		fileKey    = msiRecord.StringData(1)
		fileName   = msiRecord.StringData(2)
		folder     = msiRecord.StringData(3)
		sequence   = msiRecord.IntegerData(4)
		attributes = msiRecord.IntegerData(5)
		delim = InStr(1, fileName, "|", vbTextCompare)
		fileName = Right(fileName, Len(fileName) - delim)
		If (attributes And msidbFileAttributesNoncompressed) = 0 Then
			sourcePath = session.SourcePath(folder) &"\" & fileKey
			outStream.WriteLine """" & sourcePath  & """" & " " & fileKey
			If installer.FileAttributes(sourcePath) = -1 Then message = message & vbNewLine & sourcePath
		End If
	Loop
	msiView.Close : CheckInstallError
	outStream.Close
REM Wscript.Echo "SourceDir = " & session.Property("SourceDir")
	If Not IsEmpty(message) Then Fail "The following files were not available:" & message
End Function

Function MakeCabFile(catFileFolder,msibName)
	'Delete the original cab file first
	Dim originalCabPath:originalCabPath=catFileFolder & cabname
	Call fso.DeleteFile(originalCabPath,True)'True for force
	
	Call MakeDDFile(catFileFolder,msibName)
	
	'Generate compressed file cabinet
	'Go to the temp folder, make cab and then return
	WshShell.CurrentDirectory = catFileFolder
	Dim cabStat : cabStat = WshShell.Run("MakeCab.exe /f " & catFileFolder &"\"& cabnameNoExt & ".DDF", wsRunDoesNotActive, wsWaitUntilReturn) : CheckInstallError
	WshShell.CurrentDirectory = current_directory
	If cabStat <> 0 Then Fail "MAKECAB.EXE failed, possibly could not find source files, or invalid DDF format"
End Function

'Insert the cab file which include the sign back to MSI
Function InsertCabFileBackToMSI(cabFolder)
	Dim msiView, msiRecord
	Dim cabPath:cabPath = cabFolder & cabname
	Set msiView = database.OpenView("SELECT `Name`,`Data` FROM _Streams") : CheckInstallError
	msiView.Execute : CheckInstallError
	Set msiRecord = Installer.CreateRecord(2)
	msiRecord.StringData(1) = cabname
	msiRecord.SetStream 2, cabPath : CheckInstallError
	msiView.Modify msiViewModifyAssign, msiRecord : CheckInstallError 'replace any existing stream of that name
	Const PID_LASTPRINTED = 11
	Const PID_LASTSAVE_DTM = 13
	dim sumInfo : Set sumInfo = database.SummaryInformation(3) : CheckInstallError
	sumInfo.Property(PID_LASTPRINTED) = Now
	sumInfo.Property(PID_LASTSAVE_DTM) = Now
	'sumInfo.Property(15) = (shortNames And 1) + 2
	msiView.Close()
	sumInfo.Persist:CheckInstallError
	database.Commit : CheckInstallError
End Function
