The XenServer Windows Installer Packages
==========================================

The XenServer windows installer packages consists of

6 MSIs for installing 32 bit and 64 bit versions of

*    The XenServer Windows Guest Agent
*    The XenServer Windows Paravirtualized Drivers
*    The XenServer Windows Volume Shadow Copy Service Provider

An installwizard service for handling automated updates and installations of the tools 
(which internally used the correct MSIs from the list above)

A 32 bit MSI for installing and starting the installwizard service

Quick Start
===========

Prerequisites to build
----------------------

*   Visual Studio 2012 or later 
*   Python 3 or later 
*   WIX (Windows Installer XML) Version 3.5

Environment variables used in building the installer
----------------------------------------------------

BUILD\_NUMBER Build number

WIX location of the WIX binaries

VS location of visual studio

Commands to build
-----------------

To build the installer, first construct a package output directory containing a 
subdirectory for each of the other windows tools projects.  The subdirectories should be 
named

*  xenbus
*  xenguestagent
*  xeniface
*  xenvif
*  xennet
*  xenvbd
*  xenvss

Each subdirectory should have the relevent build output of it's associated component
copied inside

Then use the following commands

    git clone http://github.com/xenserver/win-installer
    cd win-installer
    .\build.py --local <build output directory>

To sign the drivers with a certificate installed on the build machine, the 
following additional arguments can be placed after the build output directory 
in the .\build.py command

    --sign <certificate name> 
        Sign with the best certificate matching <certificate name>
    
    --addcert <certificate file> 
        Add an aditional <certificate file> to the signature block

