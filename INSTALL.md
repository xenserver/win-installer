To install the XenServer Windows Tools Installer Packages on a Windows Guest VM

*    Ensure .net 3.5 or higher is installed

*    copy the following files to the guest VM:

* *    citrixguestagentx64.msi
* *    citrixguestagentx86.msi
* *    citrixvssx64.msi
* *    citrixvssx86.msi
* *    citrixxendriversx64.msi
* *    citrixxendriversx86.msi
* *    installwizard.msi

*    create an empty file entitled xenlegacy.exe

*    run installwizard.msi


(Note that each of the msi files my be installed manually rather than using 
the installwizard if wished)
