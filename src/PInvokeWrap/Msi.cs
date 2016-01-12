using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PInvokeWrap
{
    public static class Msi
    {
        // **** Msi- functions, enums and constants ****

        // Maybe this should be a resource?
        public static class INSTALLPROPERTY
        {
            public const string INSTALLEDPRODUCTNAME = "InstalledProductName";
            public const string VERSIONSTRING = "VersionString";
            public const string HELPLINK = "HelpLink";
            public const string HELPTELEPHONE = "HelpTelephone";
            public const string INSTALLLOCATION = "InstallLocation";
            public const string INSTALLSOURCE = "InstallSource";
            public const string INSTALLDATE = "InstallDate";
            public const string PUBLISHER = "Publisher";
            public const string LOCALPACKAGE = "LocalPackage";
            public const string URLINFOABOUT = "URLInfoAbout";
            public const string URLUPDATEINFO = "URLUpdateInfo";
            public const string VERSIONMINOR = "VersionMinor";
            public const string VERSIONMAJOR = "VersionMajor";
        }

        [DllImport("msi.dll", SetLastError = true)]
        public static extern int MsiEnumProducts(
            int iProductIndex,
            StringBuilder lpProductBuf
        );

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        public static extern int MsiGetProductInfo(
            string product,
            string property,
            [Out] StringBuilder valueBuf,
            ref Int32 len
        );

    }
}
