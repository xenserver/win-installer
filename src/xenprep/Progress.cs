using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

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
    }
}
