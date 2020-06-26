using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Timers;
using System.Windows.Forms;
using SynchronisationProvider;

namespace Synchronizer
{
    public partial class Form1 : Form
    {
        Provider p;
        public Form1()
        {
            InitializeComponent();
            p = new Provider("Synchronizer.exe");
            p.Start();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            p.Stop();
        }
    }
    
}
