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
        private readonly List<FolderInfo> _folderInfos = new List<FolderInfo>();
        public Form1()
        {
            InitializeComponent();
            ShowLog("Synchronizer started");
            GetSettings();
            CreateTasks();
        }

        private void CreateTasks()
        {
            for(int i=0; i< _folderInfos.Count; i++)
            {
                _folderInfos[i].timer = new System.Timers.Timer();
                _folderInfos[i].timer.Interval = _folderInfos[i].Interval+i;
                _folderInfos[i].isLock = false;
                _folderInfos[i].timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
                _folderInfos[i].timer.Start();
                ShowLog("Task for " + _folderInfos[i].LocalPath+" created");
            }
        }
        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            var t = (System.Timers.Timer)source;
            int i1000 = (int)(t.Interval / 1000);
            int i = (int)t.Interval - i1000 * 1000;
            DoSynchro(i);

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
            msg = DateTime.Now.ToLongDateString() + "-" + DateTime.Now.ToLongTimeString()+" =>  "+msg;
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path))  Directory.CreateDirectory(path);
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


        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            ShowLog("Synchronizer stoped");
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
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap { ExeConfigFilename = "Synchronizer.config" }, ConfigurationUserLevel.None);
            if (!configuration.HasFile)
            {
                configuration = ConfigurationManager.OpenExeConfiguration($"{AppDomain.CurrentDomain.BaseDirectory}Synchronizer.exe");
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
