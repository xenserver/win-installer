using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Resources;
using System.Diagnostics;

namespace BrandSupport
{
    public class BrandingControl
    {
        private ResourceManager resources;

        public BrandingControl(string path)
        {
            Assembly sat = Assembly.LoadFile(path);
            resources = new ResourceManager("textstrings", sat);
            Trace.WriteLine("Resource manager created");
        }

        public string getString(string key)
        {
            try
            {
                string res = this.resources.GetString(key);
                Trace.WriteLine(key + ":" + res);
                return res;
            }
            catch (Exception e)
            {
                Trace.WriteLine("Unknown Branding : " + key);
                Trace.WriteLine(e.ToString());
                return "Unknown Branding " + key;
            }
        }
    }
}
