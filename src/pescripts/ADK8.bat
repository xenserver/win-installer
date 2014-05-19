rem Sample script for installing XenServer PV Drivers to a Windows PE Image
rem Using the Microsoft Windows 8 ADK 
rem
rem usage: ADK8.bat <wim file> <driver folder> [x86|x64]
rem
rem this presupposes you have a WIM file generated, perhaps using
rem copype.cmd, but not mounted

echo "Wim File: %1"
echo "Drivers: %2"
echo "Arch: %3"

rem Create a folder to mount the wim file to, then mount it
mkdir mountpe
dism /Mount-Image /ImageFile:"%1" /index:1 /MountDir:"mountpe"

rem Add the driver files

dism /add-driver /image:"mountpe" /Driver:"%2\xenbus\%3\xenbus.inf"
dism /add-driver /image:"mountpe" /Driver:"%2\xenvbd\%3\xenvbd.inf"
dism /add-driver /image:"mountpe" /Driver:"%2\xennet\%3\xennet.inf"
dism /add-driver /image:"mountpe" /Driver:"%2\xenvif\%3\xenvif.inf"

rem Make the registry changes needed to set up filters and unplug
rem the emulated devices

reg load HKLM\pemount mountpe\Windows\System32\config\SYSTEM
reg ADD HKLM\pemount\ControlSet001\Services\xenbus\Parameters /v ActiveDevice /t REG_SZ /d "PCI\VEN_5853&DEV_0002&SUBSYS_00025853&REV_02"
reg ADD HKLM\pemount\ControlSet001\Services\xenfilt /v WindowsPEMode /t REG_DWORD /d 1
reg ADD HKLM\pemount\ControlSet001\Services\xenfilt\UNPLUG /v DISKS /t REG_MULTI_SZ /d xenvbd
reg ADD HKLM\pemount\ControlSet001\Services\xenfilt\UNPLUG /v NICS /t REG_MULTI_SZ /d xenvif\0xennet
reg ADD HKLM\pemount\ControlSet001\Services\xennet /v Count /t REG_DWORD /d 1
reg ADD HKLM\pemount\ControlSet001\Services\xenvif /v Count /t REG_DWORD /d 1
reg ADD HKLM\pemount\ControlSet001\Services\xenvbd /v Count /t REG_DWORD /d 1
reg ADD HKLM\pemount\ControlSet001\Control\class\{4D36E96A-E325-11CE-BFC1-08002BE10318} /v UpperFilters /t REG_MULTI_SZ /d XENFILT
reg ADD HKLM\pemount\ControlSet001\Control\class\{4D36E97D-E325-11CE-BFC1-08002BE10318} /v UpperFilters /t REG_MULTI_SZ /d XENFILT
reg unload HKLM\pemount

rem Unmount the wim file, and commit the changes

dism /unmount-image /mountdir:mountpe /commit

rem To generate a CD Image
rem makewinpemedia /ISO . c:\work\pe8.iso


