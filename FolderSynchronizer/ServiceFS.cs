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
        private readonly List<FolderInfo> _folderInfos = new List<FolderInfo>();

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
            ShowLog("Synchronizer started");
            GetSettings();
            CreateTasks();
        }

        protected override void OnStop()
        {
            ShowLog("Synchronizer stoped");
        }
        private void CreateTasks()
        {
            for (int i = 0; i < _folderInfos.Count; i++)
            {
                _folderInfos[i].timer = new System.Timers.Timer();
                _folderInfos[i].isLock = false;
                switch (i)
                {
                    case 0:
                        _folderInfos[i].timer.Elapsed += new System.Timers.ElapsedEventHandler(OnElapsedTime1);
                        break;
                    case 1:
                        _folderInfos[i].timer.Elapsed += new ElapsedEventHandler(OnElapsedTime2);
                        break;
                    case 2:
                        _folderInfos[i].timer.Elapsed += new ElapsedEventHandler(OnElapsedTime3);
                        break;
                }
                _folderInfos[i].timer.Interval = _folderInfos[i].Interval; //number in milisecinds  
                _folderInfos[i].timer.Start();
                ShowLog("Task for " + _folderInfos[i].LocalPath + " created");
            }
        }
        private void OnElapsedTime1(object source, ElapsedEventArgs e)
        {
            DoSynchro(0);
        }
        private void OnElapsedTime2(object source, ElapsedEventArgs e)
        {
            DoSynchro(1);
        }
        private void OnElapsedTime3(object source, ElapsedEventArgs e)
        {
            DoSynchro(2);
        }
        private void DoSynchro(int idx)
        {
            var folderInfo = _folderInfos[idx];
            if (!folderInfo.isLock)
            {
                folderInfo.isLock = true;
                var p = new Provider();
                p.SetShowLog(ShowLog);
                var targetUrl = @"\\" + folderInfo.Domain + @"\" + folderInfo.RemotePath;
                try
                {
                    p.SetTargetUserParams(folderInfo.Domain, folderInfo.DomainUser, folderInfo.Password);
                    p.Logon(targetUrl);
                    p.Synchronization(folderInfo.LocalPath, targetUrl);
                    p.Logout();
                }
                catch (Exception ex)
                {
                    ShowLog("Ошибка - " + ex.Message);
                }
                folderInfo.isLock = false;
            }
        }



        private void ShowLog(string msg)
        {
            msg = DateTime.Now.ToLongDateString() + "-" + DateTime.Now.ToLongTimeString() + " =>  " + msg;
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath))
            {
                using (StreamWriter sw = File.CreateText(filepath))
                    sw.WriteLine(msg);
            }
            else SaveMsgLog(filepath, msg);

        }


        private void SaveMsgLog(string file, string msg)
        {
            int i = 0;
            while (i < 10)
            {
                try
                {
                    using (StreamWriter sw = File.AppendText(file))
                        sw.WriteLine(msg);
                    break;
                }
                catch (Exception ex)
                {
                    i++;
                }
            }
        }



        private class FolderInfo
        {
            public string LocalPath;
            public string RemotePath;
            public string Domain;
            public string DomainUser;
            public string Password;
            public int Interval;
            public System.Timers.Timer timer;
            public bool isLock;

        }
        public void GetSettings()
        {
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = "FolderSynchronizer.config" }, ConfigurationUserLevel.None);
            if (!configuration.HasFile)
            {
                configuration = ConfigurationManager.OpenExeConfiguration($"{AppDomain.CurrentDomain.BaseDirectory}FolderSynchronizer.exe");
            }
            if (configuration.HasFile)
            {
                int idx = 1;
                string lp = "";
                while (lp != null)
                {
                    lp = ConfigurationManager.AppSettings["LocalPath" + idx.ToString()];
                    if (lp != null)
                    {
                        var nf = new FolderInfo() { LocalPath = lp };
                        nf.RemotePath = ConfigurationManager.AppSettings["RemotePath" + idx.ToString()];
                        nf.Domain = ConfigurationManager.AppSettings["Domain" + idx.ToString()];
                        nf.DomainUser = ConfigurationManager.AppSettings["DomainUser" + idx.ToString()];
                        nf.Password = ConfigurationManager.AppSettings["Password" + idx.ToString()];
                        nf.Interval = Int32.Parse(ConfigurationManager.AppSettings["Interval" + idx.ToString()]);
                        _folderInfos.Add(nf);
                    }
                    idx++;
                }

            }
        }

    }

}
