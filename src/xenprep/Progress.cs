using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Xenprep
{
    public partial class Progress : Form
    {
        public Progress()
        {
            InitializeComponent();
            progressBar.Maximum = 100;
            progressBar.Minimum = 0;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr w, IntPtr l);
        public void SetRed()
        {
            this.Invoke((MethodInvoker)(() =>
            {
                SendMessage(progressBar.Handle, 1040, (IntPtr)1, IntPtr.Zero);
            }));
        }

    }
}
