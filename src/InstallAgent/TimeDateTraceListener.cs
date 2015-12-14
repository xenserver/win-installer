using System;
using System.Diagnostics;

namespace InstallAgent
{
    class TimeDateTraceListener : TextWriterTraceListener
    {
        public TimeDateTraceListener(string file, string name)
            : base(file, name)
        { }

        public override void WriteLine(object o)
        {
            base.WriteLine(DateTime.Now.ToString() + ": " + o.ToString());
        }
        public override void WriteLine(string message)
        {
            base.WriteLine(DateTime.Now.ToString() + ": " + message);
        }
    }
}
