rem Sample script for installing XenServer PV Drivers to a Windows PE Image
rem Using the Microsoft Windows Vist / 2008 AIK
rem
rem usage: AIK1.bat <wim file> <driver folder> [x86|x64]
rem
rem this presupposes you have a WIM file generated, perhaps using
rem copype.cmd, but not mounted

echo "Wim File: %1"
echo "Drivers: %2"
echo "Arch: %3"

rem Create a folder to mount the wim file to, then mount it
mkdir mountpe
imagex /mountrw %1 1 mountpe

rem Add the driver files

peimg /inf="%2\xenbus\x86\xenbus.inf" mountpe
peimg /inf="%2\xenvbd\x86\xenvbd.inf" mountpe
peimg /inf="%2\xennet\x86\xennet.inf" mountpe
peimg /inf="%2\xenvif\x86\xenvif.inf" mountpe

rem Make the registry changes needed to set up filters and unplug
rem the emulated devices

reg load HKLM\pemount mountpe\Windows\System32\config\SYSTEM
reg ADD HKLM\pemount\ControlSet001\Services\xenfilt /v WindowsPEMode /t REG_DWORD /d 1
reg ADD HKLM\pemount\ControlSet001\Services\xenfilt\UNPLUG /v DISKS /t REG_MULTI_SZ /d xenvbd
reg ADD HKLM\pemount\ControlSet001\Services\xenfilt\UNPLUG /v NICS /t REG_MULTI_SZ /d xenvif\0xennet
reg ADD HKLM\pemount\ControlSet001\Control\class\{4D36E96A-E325-11CE-BFC1-08002BE10318} /v UpperFilters /t REG_MULTI_SZ /d XENFILT
reg ADD HKLM\pemount\ControlSet001\Control\class\{4D36E97D-E325-11CE-BFC1-08002BE10318} /v UpperFilters /t REG_MULTI_SZ /d XENFILT
reg unload HKLM\pemount

rem Unmount the wim file, and commit the changes

imagex /unmount /commit mountpe

rem To generate a CD image
rem copy /Y winpe.wim ISO\sources\boot.wim
rem oscdimg -n -betfsboot.com ISO c:\work\testpe.iso
