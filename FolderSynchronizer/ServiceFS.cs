using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;
using System.Timers;
using SynchronisationProvider;


namespace FolderSynchronizer
{
    public partial class ServiceFS : ServiceBase
    {
        Provider p;
        public ServiceFS()
        {
            InitializeComponent();
        }
        protected override void OnStart(string[] args)
        {
#if DEBUG
            base.RequestAdditionalTime(600000); // 600*1000ms = 10 minutes timeout
            Debugger.Launch(); // launch and attach debugger
#endif
            p = new Provider("FolderSynchronizer.exe");
            
            p.Start();
        }
        protected override void OnStop()
        {
            p.Stop();
        }
    }
}
