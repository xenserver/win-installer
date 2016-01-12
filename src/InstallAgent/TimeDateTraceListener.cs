using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

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

        public static void Initialize(string name)
        // Creates a new instance of the class
        // and adds it to 'Trace.Listeners'
        {
            Directory.CreateDirectory(Application.CommonAppDataPath);

            TextWriterTraceListener tlog = new TimeDateTraceListener(
                Path.Combine(
                    Application.CommonAppDataPath,
                    name + ".log"),
                name + "Log"
            );
            Trace.Listeners.Add(tlog);
            Trace.AutoFlush = true;
        }
    }
}
