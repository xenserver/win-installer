#!python -u

# Copyright (c) Citrix Systems Inc.
# All rights reserved.
#
# Redistribution and use in source and binary forms, 
# with or without modification, are permitted provided 
# that the following conditions are met:
#
# *   Redistributions of source code must retain the above 
#     copyright notice, this list of conditions and the 
#     following disclaimer.
# *   Redistributions in binary form must reproduce the above 
#     copyright notice, this list of conditions and the 
#     following disclaimer in the documentation and/or other 
#     materials provided with the distribution.
#
# THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
# CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
# INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
# MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
# DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
# CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
# SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
# BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
# SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
# INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
# WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
# NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
# OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
# SUCH DAMAGE.

import os, sys
import datetime
import glob
import shutil
import subprocess
import urllib.request
import tarfile
import manifestlatest
import manifestspecific
import re
import errno
import stat
import tempfile
import imp
import io

(brandingFile, brandingPath, brandingDesc) = imp.find_module("branding",["src\\branding"])
branding = imp.load_module("branding",brandingFile,brandingPath,brandingDesc)

def unpack_from_jenkins(filelist, packdir):
    if ('GIT_COMMIT' in os.environ):
        print ("Installer Build ",os.environ["GIT_COMMIT"])
    for urlkey in filelist:
        url = filelist[urlkey]
        print(url)
        tf = tarfile.open(name=url, mode='r|')
        tf.extractall(packdir)


header = "verinfo.wxi"
brandingheader = "branding"
cppheader = "branding.h"

include ="include"

signtool=os.environ['KIT']+"\\bin\\x86\\signtool.exe"
timestamp="http://timestamp.verisign.com/scripts/timestamp.dll"

#remembersignname = "Citrix Systems, Inc"

def sign(filename, signname, additionalcert=None, signstr=None):
    for i in range(1,10):
        try: 
            if signstr == None:
                if additionalcert == None:
                    callfn([signtool, "sign", "/a", "/s", "my", "/n", signname, "/t", timestamp, filename])
                else:
                    callfn([signtool, "sign", "/a", "/s", "my", "/n", signname, "/t", timestamp, "/ac", "c:\\MSCV-VSClass3.cer", filename])
            else:
                callfn(signstr+" "+filename)
        except:
            if i==9:
                raise
            continue
        break;

agenttosign = [
    'BrandSupport\\brandsat.dll',
    'BrandSupport\\BrandSupport.dll',
    'installwizard\\netsettings\\Win32\\netsettings.exe',
    'installwizard\\netsettings\\x64\\netsettings.exe',
    'installwizard\\qnetsettings\\Win32\\qnetsettings.exe',
    'installwizard\\qnetsettings\\x64\\qnetsettings.exe',
    "xenguestagent\\xendpriv\\XenDPriv.exe",
    "xenguestagent\\xenupdater\\ManagementAgentUpdater.exe",
    "xenguestagent\\xenguestagent\\XenGuestLib.Dll" ,
    'xenvss\\x64\\xenvss.dll',
    'xenvss\\x86\\xenvss.dll',
    'xenvss\\x64\\vssclient.dll', 
    'xenvss\\x86\\vssclient.dll', 
    "xenguestagent\\xenguestagent\\xenguestagent.exe",
    "InstallAgent\\InstallAgent.exe",
    "Libraries\\PInvokeWrap.dll",
    "Libraries\\HelperFunctions.dll",
    "Libraries\\HardwareDevice.dll",
    "Libraries\\PVDriversRemoval.dll",
    "Uninstall\\Uninstall.exe",
]

def sign_builds(outbuilds):
    cwd = os.getcwd()
    os.chdir(outbuilds)
    if signfiles:
        for afile in agenttosign:
            sign(afile, signname, signstr=signstr)
    os.chdir(cwd)

def signdrivers(pack, signname, arch, additionalcert, signstr=None, crosssignstr=None):

    additionalcertfiles = [
        pack+"\\xenvif\\"+arch+"\\xenvif.sys",
        pack+"\\xenvbd\\"+arch+"\\xenvbd.sys", 
        pack+"\\xenvbd\\"+arch+"\\xencrsh.sys",
        pack+"\\xenvbd\\"+arch+"\\xendisk.sys",
        pack+"\\xennet\\"+arch+"\\xennet.sys",
        pack+"\\xeniface\\"+arch+"\\xeniface.sys",
        pack+"\\xeniface\\"+arch+"\\liteagent.exe",
        pack+"\\xenbus\\"+arch+"\\xenbus.sys",
        pack+"\\xenbus\\"+arch+"\\xen.sys",
        pack+"\\xenbus\\"+arch+"\\xenfilt.sys",
    ]
    
    noadditionalcertfiles = [
        pack+"\\xenguestagent\\xenguestagent\\XenGuestAgent.exe",
        pack+"\\xenguestagent\\xenguestagent\\xenguestlib.dll", 
        pack+"\\xenguestagent\\xenguestagent\\Interop.NetFwTypeLib.dll", 
        pack+"\\xenguestagent\\xenupdater\\Interop.TaskScheduler.dll",
        pack+"\\xenguestagent\\xenupdater\\ManagementAgentUpdater.exe",
        pack+"\\xenguestagent\\xendpriv\\XenDpriv.exe",
        pack+"\\xenvif\\"+arch+"\\xenvif_coinst.dll",
        pack+"\\xenvss\\"+arch+"\\vssclient.dll", 
        pack+"\\xenvss\\"+arch+"\\vsstest.exe", 
        pack+"\\xenvss\\"+arch+"\\xenvss.dll", 
        pack+"\\xenvbd\\"+arch+"\\xenvbd_coinst.dll",
        pack+"\\xennet\\"+arch+"\\xennet_coinst.dll",
        pack+"\\xenbus\\"+arch+"\\xenbus_coinst.dll",
        pack+"\\xeniface\\"+arch+"\\xeniface_coinst.dll",
    ]


    for afile in additionalcertfiles:
        sign(afile, signname, additionalcert, signstr=crosssignstr)

    for afile in noadditionalcertfiles:
        sign(afile, signname, signstr=signstr)




def signcatfiles(pack, signname, arch, additionalcert, signstr = None):
    catfiles = [
        pack+"\\xenvif\\"+arch+"\\xenvif.cat",
        pack+"\\xenvbd\\"+arch+"\\xenvbd.cat",
        pack+"\\xennet\\"+arch+"\\xennet.cat",
        pack+"\\xeniface\\"+arch+"\\xeniface.cat",
        pack+"\\xenbus\\"+arch+"\\xenbus.cat"
    ]
    
    for afile in catfiles:
        sign(afile, signname, additionalcert, signstr=signstr)

def get_cultural_branding(culture):
    print(os.getcwd())
    cwd = os.getcwd()
    os.chdir(basedir)
    if culture==branding.cultures['default']:
        (brandingFile, brandingPath, brandingDesc) = imp.find_module("branding",["src\\branding"])
    else:
        if not os.path.isfile("src\\branding\\branding."+culture+".py"):
            print("branding file for culture "+culture+" doesn't exist")
        (brandingFile, brandingPath, brandingDesc) = imp.find_module("branding."+culture,["src\\branding"])
    module = imp.load_module("branding"+culture,brandingFile,brandingPath,brandingDesc)
    os.chdir(cwd)
    return module

def make_cs_header() :
    file = open(include+'\\VerInfo.cs', 'w')

    file.write('internal class XenVersions {\n')
    for key,value in branding.branding.items():
        file.write("public const string BRANDING_"+key+" = \""+value+"\";\n")
    file.write("}")
    file.close()


def make_wxi_header(culture):
    cbranding = get_cultural_branding(culture)
    file = io.open(include+"\\"+brandingheader+"."+culture+".wxi", mode='w', encoding="utf8")
    file.write("<?xml version='1.0' ?>\n");
    file.write("<Include xmlns = 'http://schemas.microsoft.com/wix/2006/wi'>\n")
    for key, value in cbranding.branding.items():
        file.write("<?define BRANDING_"+key+" =\t\""+value.replace("\\\\","\\")+"\"?>\n")
    for key, value in cbranding.filenames.items():
        file.write("<?define FILENAME_"+key+" =\t\""+value+"\"?>\n")
    for key, value in cbranding.resources.items():
        file.write("<?define RESOURCE_"+key+" =\t\""+value+"\"?>\n")
    
    file.write("<?define RESOURCES_Bitmaps =\t\""+cbranding.bitmaps+"\"?>\n")
    
    file.write("</Include>")
    file.close();

def make_setup_header():
    culturelist = []
    culturelist.append(branding) 
    print("make setup header")
    print(branding.cultures['others'])
    print(culturelist)

    for culture in branding.cultures['others']:
        culturelist.append(get_cultural_branding(culture))

    print(culturelist)
    

    file=io.open(include+"\\"+"setupbranding.h",mode='w', encoding="utf8" )
    keylist=[]
    filekeylist=[]
    pos=0;
    for key in branding.branding.keys():
        keylist.append(key)
        file.write("#define BRANDING_"+key+" "+str(pos)+"\n")
        pos = pos+1
    for key in branding.filenames.keys():
        filekeylist.append(key)
        file.write("#define FILENAME_"+key+" "+str(pos)+"\n")
        pos = pos+1

    for culture in culturelist:
        file.write("const TCHAR * list_"+culture.languagecode['culture']+"[] = {\n")
        for key in keylist:
            file.write("_T(\"" + culture.branding[key] + "\"),\n")
        for key in filekeylist:
            file.write("_T(\"" + culture.filenames[key] + "\"),\n")
        file.write("};\n")

    for culture in culturelist:
        file.write("const dict loc_"+culture.languagecode['culture']+" = {\n")
        file.write(culture.languagecode['language']+",\n")
        file.write(culture.languagecode['sublang']+",\n")
        file.write("list_"+culture.languagecode['culture']+"\n")
        file.write("};\n")

    file.write("const dict* dicts[]={\n")
    for culture in culturelist:
        file.write("&loc_"+culture.languagecode['culture']+",\n")
    file.write("};\n")

    file.write("const dict* loc_def = &loc_"+branding.languagecode['culture']+";\n")

    file.close()


def make_header(outbuilds):
    now = datetime.datetime.now()

    if not(os.path.lexists(include)):
        os.mkdir(include)

    file = open(include+"\\"+header, 'w')
    file.write("<?xml version='1.0' ?>\n");
    file.write("<Include xmlns = 'http://schemas.microsoft.com/wix/2006/wi'>\n")

    file.write("<?define BRANDING_MAJOR_VERSION_STR =\t\""+os.environ['MAJOR_VERSION']+"\"?>\n")
    file.write("<?define BRANDING_MINOR_VERSION_STR =\t\""+os.environ['MINOR_VERSION']+"\"?>\n")
    file.write("<?define BRANDING_MICRO_VERSION_STR =\t\""+os.environ['MICRO_VERSION']+"\"?>\n")
    file.write("<?define BRANDING_BUILD_NR_STR =\t\""+os.environ['BUILD_NUMBER']+"\"?>\n")
    file.write("<?define TOOLS_HOTFIX_NR_STR =\t\""+os.environ['TOOLS_HOTFIX_NUMBER']+"\"?>\n")
    file.write("</Include>")
    file.close();

    make_setup_header()

    make_cs_header()

    make_wxi_header(branding.cultures['default'])
    for culture in branding.cultures['others']:
        make_wxi_header(culture)

    file = open(include+"\\"+cppheader, 'w')
    file.write("#pragma once\n")
    for key, value in branding.branding.items():
        file.write("#define BRANDING_"+key+" \""+value+"\"\n")
    for key, value in branding.filenames.items():
        file.write("#define FILENAME_"+key+" \""+value+"\"\n")
    for key, value in branding.resources.items():
        file.write("#define RESOURCE_"+key+" \""+value+"\"\n")
    file.close()

    file = open("proj\\textstrings.txt",'w')
    for key, value in branding.branding.items():
        file.write("BRANDING_"+key+"="+value.replace("\\","\\\\")+"\n")
    for key, value in branding.filenames.items():
        file.write("FILENAME_"+key+"="+value.replace("\\","\\\\")+"\n")
    for key, value in branding.resources.items():
        file.write("RESOURCE_"+key+"="+value.replace("\\","\\\\")+"\n")
    file.close();

    file = open("proj\\buildsat.bat",'w')
    file.write("echo Building satellite dll\n")
    file.write("call \"%VS%\\VC\\vcvarsall.bat\" x86\n")
    file.write("set FrameworkVersion=v3.5\n")
    file.write("mkdir BrandSupport\n")
    file.write("resgen.exe proj\\textstrings.txt proj\\textstrings.resources\n")
    #file.write("al.exe proj\\branding.mod /embed:proj\\textstrings.resources /embed:"+branding.bitmaps+"\\DlgBmp.bmp /t:lib /out:proj\\brandsat.dll\n")
    file.write("\"c:\windows\Microsoft.NET\Framework\\v3.5\csc.exe\" /out:BrandSupport\\brandsat.dll /target:library /res:proj\\textstrings.resources /res:"+outbuilds+"\\"+branding.bitmaps+"\\DlgBmp.bmp src\\branding\\branding.cs \n");
    file.write("echo Built satellite dll at BrandSupport\\brandsat.dll\n")
    file.close();
    print (callfnout("proj\\buildsat.bat"))

def callfnout(cmd):
    print(cmd)

    sub = subprocess.Popen(cmd, stdout=subprocess.PIPE)
    output = sub.communicate()[0]
    ret = sub.returncode

    if ret != 0:
        raise(Exception("Error %d in : %s" % (ret, cmd)))
    print("------------------------------------------------------------")
    return output.decode('utf-8')


def callfn(cmd):
    print(cmd)
    ret = subprocess.call(cmd)
    if ret != 0:
        raise(Exception("Error %d in : %s" % (ret, cmd)))
    print("------------------------------------------------------------")

def callfnret(cmd):
    print(cmd)
    ret = subprocess.call(cmd)
    print("------------------------------------------------------------")
    return ret

def remove_readonly(func, path, execinfo):
    if (os.path.exists(path)):
        os.chmod(path, stat.S_IWRITE)
        os.unlink(path)

def make_pe(pack):
        if os.path.exists('installer\\pe'):
                shutil.rmtree('installer\\pe', onerror=remove_readonly)
        os.makedirs('installer\\pe')
        shutil.copytree(pack+"\\xenvif", "installer\\pe\\xenvif")
        shutil.copytree(pack+"\\xenvbd", "installer\\pe\\xenvbd")
        shutil.copytree(pack+"\\xennet", "installer\\pe\\xennet")
        shutil.copytree(pack+"\\xenbus", "installer\\pe\\xenbus")
        shutil.copytree("src\\pescripts", "installer\\pe\\scripts")

def make_builds(pack, outbuilds):
        shutil.copytree(pack+"\\xenvif", outbuilds+"\\xenvif")
        shutil.copytree(pack+"\\xenvbd", outbuilds+"\\xenvbd")
        shutil.copytree(pack+"\\xennet", outbuilds+"\\xennet")
        shutil.copytree(pack+"\\xenbus", outbuilds+"\\xenbus")
        shutil.copytree(pack+"\\xeniface", outbuilds+"\\xeniface")
        shutil.copytree(pack+"\\xenguestagent", outbuilds+"\\xenguestagent")
        shutil.copytree(pack+"\\xenvss", outbuilds+"\\xenvss")

def make_installer_builds(pack, outbuilds):
        shutil.copytree(pack+"\\installwizard", outbuilds+"\\installwizard")
        shutil.copytree(pack+"\\BrandSupport", outbuilds+"\\BrandSupport")
        shutil.copytree(pack+"\\InstallAgent", outbuilds+"\\InstallAgent")
        shutil.copytree(pack+"\\Libraries", outbuilds+"\\Libraries")
        shutil.copytree(pack+"\\Setup", outbuilds+"\\Setup")
        shutil.copytree(pack+"\\Uninstall", outbuilds+"\\Uninstall")



driverlist= {
        "xenbus" : 
            {"guid" : 
                {   "x64" : "eb49829c-a972-4aee-b2d7-762992f41f17",
                    "x86" : "37dad919-da89-41ee-84c2-febe29585e3f"}},
        "xenvif" :
            {"guid" : 
                {   "x64" : "c17cf278-3a60-4e2a-9cfc-db64a6847a85",
                    "x86" : "0c695c6d-5c6f-4301-b6f9-b6111864f43f"}},
        "xennet" :
            {"guid" : 
                {   "x64" : "c8b8b455-c77b-4678-a19f-fb83dce7de9d",
                    "x86" : "a2791bdf-563f-40f2-8539-b387df6c77e7"}},
        "xeniface" :
            {"guid" : 
                {   "x64" : "9556232c-44a3-480a-92a5-549262569c3f",
                    "x86" : "fd6b32f6-622a-4bb4-97f3-7f607a386a8e"}},
        "xenvbd" :
            {"guid" : 
                {   "x64" : "c813c967-2b82-4c7c-b784-49150df404f5",
                    "x86" : "bd94fe86-12e1-4e26-8117-da822892c689"}}
}

def generate_driver_wxs(pack):
    wxsfile="""
    <Wix xmlns='http://schemas.microsoft.com/wix/2006/wi'
     xmlns:difx='http://schemas.microsoft.com/wix/DifxAppExtension'>
    """
    
    for driver in driverlist:
        guid=driverlist[driver]["guid"]["x64"]
        wxsfile+="<?define "+driver+"DriverGUIDX64 = '"+guid+"' ?>\n"
        guid=driverlist[driver]["guid"]["x86"]
        wxsfile+="<?define "+driver+"DriverGUIDX86 = '"+guid+"' ?>\n"


    wxsfile += """
    <Module Id='XenServerDriversModule' Language='1033' Version='1.0.0.0'>
    <Package Id='245ff6be-cf5c-4da4-adbd-d2f9123af86e' Keywords='XenServerDriver' Description='XenServer Driver Installer'
     Comments='XenServer PV Driver' Manufacturer='Citrix'
     InstallerVersion='200' Languages='1033' 
     SummaryCodepage='1252' />
    """

    wxsfile += """
    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="Drivers" Name="Drivers">
    """
    for driver in driverlist:
        wxsfile+=driverfiles_wxs(pack, driver, driverlist[driver])

    wxsfile += """
      </Directory>
    </Directory>
    """

    wxsfile += """
    </Module>
    </Wix>
    """
    print(wxsfile)

    with open("installer\\drivergen.wxs","w") as wxs:
        wxs.write(wxsfile);



def driverfiles_wxs(pack, driver, details):
    wxsfile = ""
    wxsfile += "    <Directory Id=\""+driver+"\" Name=\""+driver+"\">\n"
    wxsfile += "      <Directory Id=\""+driver+"X86\" Name=\"x86\">\n"
    wxsfile += "        <Component Id=\""+driver+"DriverX86\" Guid=\""+details["guid"]["x86"]+"\">\n"
    wxsfile += driverarchfiles_wxs(pack, driver, "x86")
    wxsfile += "        </Component>\n"
    wxsfile += "      </Directory>\n"
    wxsfile += "      <Directory Id=\""+driver+"X64\" Name=\"x64\">\n"
    wxsfile += "        <Component Id=\""+driver+"DriverX64\" Guid=\""+details["guid"]["x64"]+"\">\n"
    wxsfile += driverarchfiles_wxs(pack, driver, "x64")
    wxsfile += "        </Component>\n"
    wxsfile += "      </Directory>\n"
    wxsfile += "    </Directory>\n"
    return wxsfile

def driverarchfiles_wxs(pack, driver, arch):
    wxsfile=""
    dlist = glob.glob(os.path.join(pack, driver, arch, "*.inf"))
    dlist+= glob.glob(os.path.join(pack, driver, arch, "*.sys"))
    dlist+= glob.glob(os.path.join(pack, driver, arch, "*.dll"))
    dlist+= glob.glob(os.path.join(pack, driver, arch, "liteagent.exe"))
    dlist+= glob.glob(os.path.join(pack, driver, arch, "*.cat"))
    for dfile in dlist:
        leaf = os.path.basename(dfile)
        leafshort=leaf.replace(".","").replace("_","")
        wxsfile +="          <File Id=\""+leafshort+arch+"\" Name=\""+leaf+"\" DiskId='1' Source=\""+dfile+"\" />\n"
    return wxsfile



signinstallers = [
    'managementx64',
    'managementx86',
    'setup'
]

def generate_signing_script():
    with open('installer\\sign.bat','w') as signfile:
        signfile.write("@if \"%~1\"==\"\" goto usage\n")
        signfile.write("@if \"%~1\"==\"/help\" goto usage\n")
        signfile.write("@set temp=%~1\n") #Remove Quotes
        signfile.write("@set temp=%temp:\"\"=\"%\n") #Convert doube quotes to single quotes
        for msi in signinstallers:
            signfile.write("%temp% "+"%~dp0\\"+branding.filenames[msi]+"\n") #dp0 is the pathname of the script
        signfile.write("@exit /B 0\n")
        signfile.write(":usage\n")
        signfile.write("@echo off\n")
        signfile.write("echo Usage:\n")
        signfile.write("echo sign.bat ^<signing command^>\n")
        signfile.write("echo. \n")
        signfile.write("echo Example:\n")
        signfile.write("echo On a system where a certificate for \"My Company Inc.\" has been installed as a personal certificate\n")
        signfile.write("echo. \n")
        signfile.write("echo sign.bat \"signtool sign /a /s my /n \"\"My Company Inc.\"\" /t http://timestamp.verisign.com/scripts/timestamp.dll\"\n")


def generate_intermediate_signing_script():
    with open('installer\\intermediatesign.bat','w') as signfile:
        signfile.write("@if \"%~1\"==\"\" goto usage\n")
        signfile.write("@if \"%~1\"==\"/help\" goto usage\n")
        signfile.write("@set temp=%~1\n") #Remove Quotes
        signfile.write("@set temp=%temp:\"\"=\"%\n") #Convert doube quotes to single quotes
        for signee in agenttosign:
            signname = signee
            if signee in branding.filenames:
                signname=branding.filenames[signee]
            signfile.write("%temp% "+"%~dp0\\builds\\"+signname+"\n") #dp0 is the pathname of the script
        signfile.write("@exit /B 0\n")
        signfile.write(":usage\n")
        signfile.write("@echo off\n")
        signfile.write("echo Usage:\n")
        signfile.write("echo intermediatesign.bat ^<signing command^>\n")
        signfile.write("echo. \n")
        signfile.write("echo Example:\n")
        signfile.write("echo On a system where a certificate for \"My Company Inc.\" has been installed as a personal certificate\n")
        signfile.write("echo. \n")
        signfile.write("echo sign.bat \"signtool sign /a /s my /n \"\"My Company Inc.\"\" /t http://timestamp.verisign.com/scripts/timestamp.dll\"\n")

def build_diagnostics(source, output):
    cwd = os.getcwd()
    print("source " + source+ " output "+output);
    outpath=os.path.join(output,"diagnostics")
    if not os.path.lexists(outpath):
        os.mkdir(outpath)
    outfile = os.path.join(outpath, "xtbugtool.bat")
    inpath=os.path.join(source,"src","diagnostics","xtbugtool.bat")
    with open(inpath,"r") as myfile:
        data=myfile.read()
    data = re.compile(r"REM I18N.*REM ENDI18N", re.MULTILINE|re.DOTALL).sub("REM I18N\n"+
    "SET COMPANY="+branding.branding['manufacturer']+"\n"+
    "SET TOOLPATH="+branding.branding['manufacturer']+"\\"+branding.branding['shortTools']+"\n"+
    "SET REGKEY=Citrix\Xentools\n"+
    "SET REGCO=Citrix\n"+
    "SET INSTALLNAME="+branding.branding['shortTools']+"Installer"+"\n"+
    "SET GUESTLOGS="+branding.branding['manufacturerLong']+"\n"+
    "SET TOOLSNAME="+branding.branding['shortTools']+"\n"+
    "\nREM ENDI18N",data)
    with open(outfile,"w") as myfile:
        myfile.write(data)



def make_installers_dir():
    if os.path.exists('installer'):
            shutil.rmtree('installer')
    os.makedirs('installer')

def make_driver_msm(pack):
    src = ".\\src\\drivers"

    wix=lambda f: os.environ['WIX']+"bin\\"+f
    callfn([wix("candle.exe"),"installer\\drivergen.wxs","-arch","x64","-darch=x64","-o", "installer\\drivergenx64.wixobj"])
    callfn([wix("light.exe"), "installer\\drivergenx64.wixobj","-darch=x64","-ext","WixUtilExtension.dll","-b",pack,"-o","installer\\drivergenx64.msm"])
    
    callfn([wix("candle.exe"),"installer\\drivergen.wxs","-arch","x86","-darch=x86","-o", "installer\\drivergenx86.wixobj"])
    callfn([wix("light.exe"), "installer\\drivergenx86.wixobj","-darch=x86","-ext","WixUtilExtension.dll","-b",pack,"-o","installer\\drivergenx86.msm"])


def make_oldmsi_installers(pack, signname):


    wix=lambda f: os.environ['WIX']+"bin\\"+f
    bitmaps = ".\\src\\bitmaps"

    
    if (all_drivers_signed) :
        use_certs='no'
    else:
        use_certs='yes'

    src = ".\\src\\agent"
    
    src = ".\\src\\drivers"
    callfn([wix("candle.exe"),src+"\\drivers.wxs","-arch","x64","-darch=x64","-ext","WixDifxAppExtension.dll", "-o", "installer\\driversx64.wixobj"])
    callfn([wix("light.exe"), "installer\\driversx64.wixobj","-darch=x64",wix("difxapp_x64.wixlib"),"-ext","WixUtilExtension.dll","-ext","WixDifxAppExtension.dll","-b",pack,"-o","installer\\driversx64.msm"])
#
    callfn([wix("candle.exe"),src+"\\drivers.wxs","-darch=x86","-ext","WixDifxAppExtension.dll", "-o", "installer\\driversx86.wixobj"])
    callfn([wix("light.exe"), "installer\\driversx86.wixobj","-darch=x86",wix("difxapp_x86.wixlib"),"-ext","WixUtilExtension.dll","-ext","WixDifxAppExtension.dll","-b",pack,"-o","installer\\driversx86.msm"])
#
    callfn([wix("candle.exe"), src+"\\citrixxendrivers.wxs", "-arch","x64", "-darch=x64", "-o", "installer\\citrixxendrivers64.wixobj", "-I"+include, "-dBitmaps="+bitmaps])
    callfn([wix("light.exe"), "installer\\citrixxendrivers64.wixobj", "-darch=x64","-b", ".\\installer", "-o", "installer\\citrixxendriversx64.msi","-b",pack, "-sw1076"])
    if signfiles:
        sign("installer\\citrixxendriversx64.msi", signname, signstr=signstr)
#
    callfn([wix("candle.exe"), src+"\\citrixxendrivers.wxs", "-darch=x86", "-o", "installer\\citrixxendrivers64.wixobj", "-I"+include, "-dBitmaps="+bitmaps])
    callfn([wix("light.exe"), "installer\\citrixxendrivers64.wixobj", "-darch=x86","-b", ".\\installer", "-o", "installer\\citrixxendriversx86.msi","-b",pack, "-sw1076"])
    if signfiles:
        sign("installer\\citrixxendriversx86.msi", signname, signstr=signstr)
#
    src = ".\\src\\vss"
#    
    callfn([wix("candle.exe"), src+"\\citrixvss.wxs", "-arch","x86", "-darch=x86", "-o", "installer\\citrixvssx86.wixobj", "-I"+include, "-dBitmaps="+bitmaps])
    callfn([wix("light.exe"), "installer\\citrixvssx86.wixobj", "-darch=x86", "-b", ".\\installer", "-o", "installer\\citrixvssx86.msi", "-b", pack, "-ext","WixUtilExtension.dll", "-cultures:en-us", "-sw1076"])
    if signfiles:
        sign("installer\\citrixvssx86.msi", signname, signstr=signstr)
#
    callfn([wix("candle.exe"), src+"\\citrixvss.wxs", "-arch","x86", "-darch=x64", "-o", "installer\\citrixvssx64.wixobj", "-I"+include, "-dBitmaps="+bitmaps])
    callfn([wix("light.exe"), "installer\\citrixvssx64.wixobj", "-darch=x64", "-b", ".\\installer", "-o", "installer\\citrixvssx64.msi", "-b", pack, "-ext","WixUtilExtension.dll", "-cultures:en-us", "-sw1076"])
    if signfiles:
        sign("installer\\citrixvssx64.msi", signname, signstr=signstr)
#
#
    src = ".\\src\\agent"

    
    callfn([wix("candle.exe"), src+"\\citrixguestagent.wxs", "-arch","x86", "-darch=x86", "-o", "installer\\citrixguestagentx86.wixobj", "-ext", "WixNetFxExtension.dll", "-I"+include, "-dBitmaps="+bitmaps])
    callfn([wix("light.exe"), "installer\\citrixguestagentx86.wixobj", "-darch=x86", "-b", ".\\installer", "-o", "installer\\citrixguestagentx86.msi", "-b", pack, "-ext", "WixNetFxExtension.dll", "-ext", "WixUiExtension", "-cultures:en-us", "-dWixUILicenseRtf="+src+"\\..\\bitmaps\\EULA_DRIVERS.rtf", "-sw1076"])
    if signfiles:
        sign("installer\\citrixguestagentx86.msi", signname, signstr=signstr)
#
    callfn([wix("candle.exe"), src+"\\citrixguestagent.wxs", "-arch","x64", "-darch=x64", "-o", "installer\\citrixguestagentx64.wixobj", "-ext", "WixNetFxExtension.dll", "-I"+include, "-dBitmaps="+bitmaps])


    callfn([wix("light.exe"), "installer\\citrixguestagentx64.wixobj", "-darch=x64", "-b", ".\\installer", "-o", "installer\\citrixguestagentx64.msi", "-b", pack, "-ext", "WixNetFxExtension.dll", "-ext", "WixUiExtension", "-cultures:en-us", "-dWixUILicenseRtf="+src+"\\..\\bitmaps\\EULA_DRIVERS.rtf", "-sw1076"])
    if signfiles:
        sign("installer\\citrixguestagentx64.msi", signname, signstr=signstr)
    src = ".\\src\\installwizard"
    bitmaps = ".\\src\\bitmaps"
    
    
    callfn([wix("candle.exe"), src+"\\installwizard.wxs",  "-o", "installer\\installwizard.wixobj", "-ext", "WixUtilExtension", "-ext", "WixUIExtension", "-I"+include, "-dBitmaps="+bitmaps, "-dusecerts="+use_certs])
    
    # We put a blank file in called XenLegacy.Exe - this doesn't get sucked
    # into the installer, but it is needed to keep light happy (XenLegacy.exe
    # will exentually be sourced from the original build tree)

    f = open("installer\\"+branding.filenames['legacy'],"w")
    f.write("DUMMY FILE")
    f.close()
    f = open("installer\\"+branding.filenames['legacyuninstallerfix'],"w")
    f.write("DUMMY FILE")
    f.close()
   
    callfn([wix("light.exe"), "installer\\installwizard.wixobj", "-b", ".\\installer", "-o", "installer\\installwizard.msi", "-b", pack, "-ext", "WixUtilExtension.dll", "-ext", "WixNetFxExtension.dll", "-ext", "WixUiExtension", "-cultures:en-us", "-dWixUILicenseRtf="+src+"\\..\\bitmaps\\EULA_DRIVERS.rtf", "-sw1076"])

    if signfiles:
        sign("installer\\installwizard.msi", signname, signstr=signstr)
    
    # Remove XenLegacy.Exe so that we don't archive the dummy file
    os.remove("installer\\"+branding.filenames['legacy'])    
    os.remove("installer\\"+branding.filenames['legacyuninstallerfix'])    


def make_mgmtagent_msi(pack,signname):


    wix=lambda f: os.environ['WIX']+"bin\\"+f
    bitmaps = ".\\src\\bitmaps"

    if (all_drivers_signed) :
        use_certs='no'
    else:
        use_certs='yes'

    cwd = os.getcwd()
    os.chdir(pack)
    print(os.getcwd())
    for arch in ["x86", "x64"]:
        src = cwd+"\\.\\src\\agent"
        culture = branding.cultures['default']
        
        callfn([wix("candle.exe"), src+"\\managementagent.wxs", "-dculture="+culture, "-arch",arch, "-darch="+arch, "-o", cwd+"\\installer\\managementagent"+arch+".wixobj", "-ext", "WixNetFxExtension.dll", "-I"+cwd+"\\"+include, "-dBitmaps="+cwd+"\\"+bitmaps, "-dusecerts="+use_certs])
        callfn([wix("light.exe"), cwd+"\\installer\\managementagent"+arch+".wixobj", "-dculture="+culture, "-darch="+arch, "-o", cwd+"\\installer\\"+branding.filenames['management'+arch], "-b", ".", "-ext", "WixNetFxExtension.dll", "-ext", "WixUiExtension", "-ext", "WixUtilExtension.dll", "-cultures:"+branding.cultures['default'], "-dWixUILicenseRtf="+branding.bitmaps+"\\EULA_DRIVERS.rtf", "-sw1076"])

    if len(branding.cultures['others']) != 0 :
        for culture in branding.cultures['others']:
            cbranding = get_cultural_branding(culture)
            os.makedirs(cwd+'installer\\'+culture)
            for arch in ["x86", "x64"]:
                callfn([wix("candle.exe"), src+"\\managementagent.wxs", "-dculture="+culture, "-arch",arch, "-darch="+arch, "-o", cwd+"\\installer\\managementagent"+arch+".wixobj", "-ext", "WixNetFxExtension.dll", "-ext", "WixUtilExtension.dll", "-I"+cwd+"\\"+include, "-dBitmaps="+cwd+"\\"+bitmaps, "-dusecerts="+use_certs])
                callfn([wix("light.exe"), cwd+"\\installer\\managementagent"+arch+".wixobj", "-dculture="+culture, "-darch="+arch, "-o", cwd+"\\installer\\"+culture+"\\"+branding.filenames['management'+arch], "-b", ".", "-ext", "WixNetFxExtension.dll", "-ext", "WixUiExtension", "-ext", "WixUtilExtension.dll", "-cultures:"+culture, "-dWixUILicenseRtf="+cbranding.bitmaps+"\\EULA_DRIVERS.rtf", "-sw1076"])
                callfn(["cscript", cwd+"\\src\\branding\\msidiff.js", cwd+"\\installer\\"+branding.filenames['management'+arch], cwd+"\\installer\\"+culture+"\\"+branding.filenames['management'+arch], cwd+"\\installer\\"+culture+arch+".mst"])
                callfn(["cscript", cwd+"\\src\\branding\\WiSubStg.vbs", cwd+"\\installer\\"+branding.filenames['management'+arch], cwd+"\\installer\\"+culture+arch+".mst",cbranding.branding["language"]])

    os.chdir(cwd)
    
    shutil.copy(pack+"\\Setup\\Setup.exe", "installer") 

    if signfiles:
        for signname in signinstallers:
            sign("installer\\"+branding.filenames[signname], signname, signstr=singlesignstr)

    # Write updates.tsv (url\tversion\tsize\tarch)
    f = open(os.sep.join(['installer','updates.tsv']),"w")
    for arch in ["x86", "x64"]:
        f.write(os.environ['UPDATE_URL']+branding.filenames['management'+arch]+"\t"+
                os.environ['MAJOR_VERSION']+"."+os.environ['MINOR_VERSION']+"."+os.environ['MICRO_VERSION']+"."+os.environ['BUILD_NUMBER']+"\t"+
                str(os.stat("installer\\"+branding.filenames['management'+arch]).st_size)+"\t"+
                arch+
                "\n")
    f.close()

def archive(filename, files, tgz=False):
    access='w'
    if tgz:
        access='w:gz'
    tar = tarfile.open(filename, access)
    for name in files :
        print('adding '+name)
        tar.add(name)
    tar.close()



def msbuild(name, platform, debug = False):
    cwd = os.getcwd()
    configuration=''
    if debug:
        configuration = 'Debug'
    else:
        configuration = 'Release'

    os.environ['CONFIGURATION'] = configuration

    os.environ['PLATFORM'] = platform

    os.environ['SOLUTION'] = name
    os.environ['TARGET'] = 'Build'

    os.chdir('proj')
    status=shell('msbuild.bat')
    os.chdir(cwd)
    if status != None:
        print("Exit status",status,status)
        sys.exit(status)

def getsrcpath(subproj,arch="",debug=False):
    configuration=''
    if debug:
        configuration = 'Debug'
    else:
        configuration = 'Release'

    if not arch == "":
        configuration = os.sep.join([configuration,arch])
    return  os.sep.join(['proj',subproj,'bin', configuration ])

def copyfiles(name, subproj, dest, arch="", debug=False):

    
    src_path = getsrcpath(subproj,arch,debug);

    if not os.path.lexists(name):
        os.mkdir(name)

    if arch=="":
        dst_path = os.sep.join([dest,name, subproj])
    else:
        dst_path = os.sep.join([dest,name, subproj,arch])

    if not os.path.lexists(dst_path):
        os.makedirs(dst_path)
    print(os.getcwd())
    for file in glob.glob(os.sep.join([src_path, '*'])):
        print("%s -> %s" % (file, dst_path))
        shutil.copy(file, dst_path)
    if not os.path.lexists(dst_path):
        print("dstpath not found")

    sys.stdout.flush()


def shell(command):
    print (command)
    sys.stdout.flush()
    pipe = os.popen(command, 'r', 1)
    for line in pipe:
        print(line.rstrip())

    return pipe.close()

def build_tar_source_files(securebuild):
	if securebuild:
		server = manifestspecific.secureserver
	else:
		server = manifestspecific.localserver
	return { k:  os.sep.join([server, v]) for k,v in 
			manifestspecific.build_tar_source_files.items() }

def record_version_details():
    if 'GIT_COMMIT' in os.environ.keys():
        f = open(os.sep.join(['installer','revision']),"w")
        f.write(os.environ['GIT_COMMIT'])
        print("Revision : "+os.environ['GIT_COMMIT'])
        f.close()

    f = open(os.sep.join(['installer','buildnumber']),"w")
    f.write(os.environ['MAJOR_VERSION']+"."+
            os.environ['MINOR_VERSION']+"."+
            os.environ['MICRO_VERSION']+"."+
            os.environ['BUILD_NUMBER'])
    f.close()
    f = open(os.sep.join(['installer','hotfixnumber']),"w")
    f.write(os.environ['MAJOR_VERSION']+"."+
            os.environ['MINOR_VERSION']+"."+
            os.environ['MICRO_VERSION']+"."+
            os.environ['TOOLS_HOTFIX_NUMBER'])
    f.close()
 
def archive_build_input(archiveSrc):
    if (archiveSrc == True):
        listfile = callfnout(['git','ls-files'])
        archive('installer\\source.tgz', listfile.splitlines(), tgz=True)
    archive('installer.tar', ['installer'])

def build_installer_apps(location, outbuilds):
    msbuild('installwizard','x64', False )
    msbuild('installwizard','Win32', False )
    msbuild('installwizard','Any CPU', False )

    if (signfiles):
        sign(os.sep.join([getsrcpath('installwizard', debug=False),"InstallWizard.exe"]), signname, signstr=signstr)
        sign(os.sep.join([getsrcpath('installgui', debug=False),"InstallGui.exe"]), signname, signstr=signstr)
        sign(os.sep.join([getsrcpath('UIEvent', debug=False),"UIEvent.exe"]), signname, signstr=signstr)
        sign(os.sep.join([getsrcpath('netsettings','x64',False),"netsettings.exe"]), signname, signstr=signstr)
        sign(os.sep.join([getsrcpath('netsettings','Win32',False),"netsettings.exe"]), signname, signstr=signstr)
        sign(os.sep.join([getsrcpath('qnetsettings','x64',False),"qnetsettings.exe"]), signname, signstr=signstr)
        sign(os.sep.join([getsrcpath('qnetsettings','Win32',False),"qnetsettings.exe"]), signname, signstr=signstr)
    copyfiles('installwizard', 'installwizard', ".", debug=False)
    copyfiles('installwizard', 'installgui', ".", debug=False)
    copyfiles('installwizard', 'UIEvent', ".", debug=False)
    copyfiles('installwizard', 'netsettings', ".",'x64', debug=False)
    copyfiles('installwizard', 'netsettings', ".",'Win32', debug=False)
    copyfiles('installwizard', 'qnetsettings', ".",'x64', debug=False)
    copyfiles('installwizard', 'qnetsettings', ".",'Win32', debug=False)

def build_xenprep():
    msbuild('xenprep','Any CPU', False)
    if signfiles:
        sign(os.sep.join([getsrcpath('xenprep', debug=False),"xenprep.exe"]), signname, signstr=signstr)
    copyfiles('xenprep', 'xenprep', location, debug=False)


def perform_autocommit():
    if ('AUTOCOMMIT' in os.environ):
        print ("AUTOCOMMIT = ",os.environ['AUTOCOMMIT'])
        if (os.environ['AUTOCOMMIT'] == "true" or os.environ['AUTOCOMMIT'] == "test"):
            repository = os.environ['AUTOREPO']
            shutil.rmtree(os.sep.join([location, 'guest-packages.hg']), True)
            callfn(['hg','clone',repository+"/guest-packages.hg",os.sep.join([location, 'guest-packages.hg'])])
            insturl = open(os.sep.join([location,'guest-packages.hg\\win-tools-iso\\installer.url']),'w')
            print (buildlocation, file=insturl, end="")
            print (buildlocation)
            insturl.close()
            commithashpath = os.sep.join([location,'guest-packages.hg\\win-tools-iso\\commithash'])
            logout = "" 

            hascommithash = os.path.isfile(commithashpath)

            if hascommithash:
                with open(commithashpath,'r') as temp:
                    hashdata = temp.read().strip()
                if (callfnret(['git','merge-base','--is-ancestor',hashdata,os.environ['GIT_COMMIT']])==1):
                    logout+="REVERTS :\n"
                    logout+=callfnout(['git','log',os.environ['GIT_COMMIT']+".."+hashdata])
                    logout+="\n\nCOMMITS :\n"
                logout+=callfnout(['git','log',hashdata+".."+os.environ['GIT_COMMIT']])
            else:
                logout+=callfnout(['git','log',"HEAD~1..HEAD"])

            with open (commithashpath,'w+t') as temp:
                print(os.environ['GIT_COMMIT'], file=temp)

            pwd = os.getcwd()
            os.chdir(os.sep.join([location, 'guest-packages.hg']))
           
            messagefilename=""

            with tempfile.NamedTemporaryFile(mode='w+t',delete=False) as message:
                print("Auto-update installer to "+buildlocation+" "+os.environ['GIT_COMMIT']+'\n\n\n'+logout+'\n')
                print("Auto-update installer to "+buildlocation+" "+os.environ['GIT_COMMIT']+'\n\n\n'+logout+'\n', file=message)
                messagefilename=message.name
    
            commit=['hg','commit','-l' ,messagefilename,'-u','jenkins@xeniface-build']
            push=['hg','push']
            add=['hg','add',os.sep.join([pwd,commithashpath])]
            print(commit)
            print(push)
            print(add)
            if (os.environ['AUTOCOMMIT'] == "true"):
                if not hascommithash:
                    callfn(add)
                callfn(commit)
                callfn(push)
            os.remove(messagefilename)
            os.chdir(pwd)
            shutil.rmtree(os.sep.join([location, 'guest-packages.hg']), True)

basedir=""

if __name__ == '__main__':

    print (sys.argv)

    basedir = os.getcwd()

    os.environ['MAJOR_VERSION'] = '6'
    os.environ['MINOR_VERSION'] = '2'
    os.environ['MICRO_VERSION'] = '50'

    os.environ['TOOLS_HOTFIX_NUMBER'] = '20000'
    # Note that the TOOLS_HOTFIX_NUMBER should be reset to 0 following a change of majror, minor or micro numbers

    if 'BUILD_NUMBER' not in os.environ.keys():
        os.environ['BUILD_NUMBER'] = '0'

    if 'UPDATE_URL' not in os.environ.keys():
        os.environ['UPDATE_URL'] = "http://fake.update.url/"



    if (len(sys.argv) < 3):
        print('.\\build.py <--local|--specific|--latest> <sourcedir> \\')
        print('  [--branch <referencebranch>] \\') 
        print('     [--sign <cert name> [ --addcert <additional certificate>]]')
        print('   | [--signcmd <full command line for signing tool>]')
        sys.exit(1)

    command = sys.argv[1]
    location = sys.argv[2]

    autocommit = False

    signfiles = False
 
    argptr = 3

    additionalcert = None
    signstr = None
    crosssignstr = None
    signname = None

    securebuild=False
    if ('AUTOCOMMIT' in os.environ):
        buildlocation = os.environ['BUILD_URL']+"artifact/installer.tar"

    archiveSrc = True;
    outbuilds = "installer\\builds"
    
    while (len(sys.argv) > argptr):
        if (sys.argv[argptr] == "--secure"):
            securebuild = True
            argptr +=1
            continue

        if (sys.argv[argptr] == "--branch"):

            reference = sys.argv[argptr+1]
            fo = urllib.request.urlopen('http://hg.uk.xensource.com/carbon/'+reference+'/branding.hg/raw-file/tip/toplevel-versions-xenserver')
            text=str(fo.read())
            m = re.search('^.*PRODUCT_MAJOR_VERSION\s*:=\s*(\d*).*$',text)
            os.environ['MAJOR_VERSION'] = m.group(1)
            m = re.search('^.*PRODUCT_MINOR_VERSION\s*:=\s*(\d*).*$',text)
            os.environ['MINOR_VERSION'] = m.group(1)
            m = re.search('^.*PRODUCT_MICRO_VERSION\s*:=\s*(\d*).*$',text)
            os.environ['MICRO_VERSION'] = m.group(1)
            rtf = open('src\\bitmaps\\EULA_DRIVERS.rtf', "w")
            print(r"{\rtf1\ansi{\fonttbl\f0\fmodern Courier;}\f0\fs10\pard", file=rtf)
            txt = urllib.request.urlopen('http://hg.uk.xensource.com/carbon/'+reference+'/docsource.hg/raw-file/tip/EULA_DRIVERS_OPEN')
            while (1):
                line = txt.readline()
                if not line:
                    break
                print(str(line, encoding='utf-8')+"\\par", file=rtf)
            print(r"}",file=rtf);
            txt.close()
            rtf.close()
            argptr += 2
            continue

        if (sys.argv[argptr] == "--sign"):
            signfiles = True
            signname = sys.argv[argptr+1]
            argptr += 2
            continue

        if (sys.argv[argptr] == "--addcert"):
            additionalcert = sys.argv[argptr+1]
            argptr += 2
            continue

        if (sys.argv[argptr] == "--signcmd"):
            signcmd = True
            signfiles = True
            signstr = sys.argv[argptr+1]
            crosssignstr = sys.argv[argptr+2]
            singlesignstr = sys.argv[argptr+3]
            additionalcert = ""
            argptr += 4
            continue

        if (sys.argv[argptr] == '--buildlocation'):
            buildlocation = sys.argv[argptr+1]
            argptr +=2
            continue

        if (sys.argv[argptr] == '--noarchive'):
             archiveSrc = False
             argptr +=1
             continue
        
        if (sys.argv[argptr] == '--binaryoutputlocation'):
            outbuilds = sys.argv[argptr+1]
            argptr +=2
            continue

    make_header(outbuilds)

    rebuild_installers_only = False

    if (command == '--local'):
        print( "Local Build")
        all_drivers_signed = False
    elif (command == '--specific'):
        print( "Specific Build")
        unpack_from_jenkins(build_tar_source_files(securebuild), location)
        all_drivers_signed = manifestspecific.all_drivers_signed
    elif (command == '--latest'):
        print ("Latest Build")
        unpack_from_jenkins(manifestlatest.latest_tar_source_files, location)
        all_drivers_signed = manifestlatest.all_drivers_signed
    elif (command == "--rebuild-msi"):
        rebuild_installers_only = True
        all_drivers_signed = True
    else:
        print("Unknown command: "+command)
        sys.exit(1)

    
    make_installers_dir()
    if not rebuild_installers_only :
        if (signfiles):
            signdrivers(location, signname, 'x86', additionalcert, signstr=signstr, crosssignstr=crosssignstr)
            signdrivers(location, signname, 'x64', additionalcert, signstr=signstr, crosssignstr=crosssignstr)
            if not all_drivers_signed:
                signcatfiles(location, signname, 'x86', additionalcert, signstr=crosssignstr)
                signcatfiles(location, signname, 'x64', additionalcert, signstr=crosssignstr)
        build_installer_apps(location,outbuilds)
        make_builds(location,outbuilds)
        build_diagnostics(".", outbuilds)
        make_installer_builds(".",outbuilds)

        make_pe(location)

        sign_builds(outbuilds)

    if rebuild_installers_only:
        make_builds(location,outbuilds)
        make_installer_builds(location,outbuilds)
        generate_signing_script()
    else:
        generate_intermediate_signing_script()
    
    generate_driver_wxs(outbuilds)
    make_driver_msm(outbuilds) 
    
    make_mgmtagent_msi(outbuilds,signname)

    record_version_details()

    archive_build_input(archiveSrc)

    perform_autocommit()

