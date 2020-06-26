using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.Configuration;
using System.Timers;

namespace SynchronisationProvider
{
    public class Provider
    {

        public List<FolderInfo> _folderInfos = new List<FolderInfo>();
        static string logFile;
        public Provider(string moduleName)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            logFile = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(logFile))
                using (StreamWriter sw = File.CreateText(logFile))
            SaveToLog("Synchronizer started");
            GetFolderInfos(moduleName);
            CreateTasks();
        }
        public void Start()
        {
            for (int i = 0; i < _folderInfos.Count(); i++)
            {
                _folderInfos[i].timer.Start();
                SaveToLog("Task for " + _folderInfos[i].LocalPath + " started");
            }
        }
        public void Stop()
        {
            for (int i = 0; i < _folderInfos.Count(); i++)
                _folderInfos[i].timer.Stop();
            SaveToLog("Synchronizer stoped");
        }
        private void SaveToLog(string msg)
        {
            msg = DateTime.Now.ToLongDateString() + "-" + DateTime.Now.ToLongTimeString() + " =>  " + msg;
            SaveMsgLog(logFile, msg);
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
                catch (Exception)
                {
                    i++;
                }
            }
        }
        
        private void CreateTasks()
        {
            for (int i = 0; i < _folderInfos.Count(); i++)
            {
                _folderInfos[i].timer = new Timer();
                _folderInfos[i].timer.Interval = _folderInfos[i].Interval + i;
                _folderInfos[i].isLock = false;
                _folderInfos[i].timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
                _folderInfos[i].timer.Start();
                _folderInfos[i].FP = new FolderProvider();
                _folderInfos[i].FP.SetShowLog(SaveToLog);
                SaveToLog("Task for " + _folderInfos[i].LocalPath + " created");
            }
        }
        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            var t = (Timer)source;
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
                var targetUrl = @"\\" + folderInfo.Domain + @"\" + folderInfo.RemotePath;
                try
                {
                    folderInfo.FP.SetTargetUserParams(folderInfo.Domain, folderInfo.DomainUser, folderInfo.Password);
                    folderInfo.FP.Logon(targetUrl);
                    folderInfo.FP.Synchronization(folderInfo.LocalPath, targetUrl);
                    folderInfo.FP.Logout();
                }
                catch (Exception ex)
                {
                    SaveToLog("Ошибка - " + ex.Message);
                }
                folderInfo.isLock = false;
            }
        }

        private void GetFolderInfos(string appl)
        {
            var configuration = ConfigurationManager.OpenExeConfiguration($"{AppDomain.CurrentDomain.BaseDirectory}" + appl);
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
    public class FolderInfo
    {
        public string LocalPath;
        public string RemotePath;
        public string Domain;
        public string DomainUser;
        public string Password;
        public int Interval;
        public Timer timer;
        public bool isLock;
        public  FolderProvider FP;

    }

    public class FolderProvider
    {
        public string Ip = "";
        public string Port = "";
        public string ManifestFileName = "MANIFEST.SYN";
        private ShowLog _showLog;
        public string _sourceUrl;
        public string _targetUrl;
        private static char separator = '|';
        private WrapperImpersonationContext _targetContext;
        private readonly User _targetUser = new User();
        public CredentialsType GetCredentialsType(string url)
        {
            if (url.Substring(0, 2) != @"\\" || url.Contains(@"\\localhost") || url.Contains(@"\\127.0.0.1"))
                return CredentialsType.Local;
            return CredentialsType.Network;
        }
        public delegate void ShowLog(string message);
        class User
        {
            public string Login;
            public string Domain;
            public string Password;
        }
        public void SetTargetUserParams(string domain, string login, string password)
        {
            _targetUser.Domain = domain;
            _targetUser.Login = login;
            _targetUser.Password = password;
        }
        public void SetShowLog(ShowLog sl)
        {
            _showLog = sl;
        }

        public void Logon(string targetUrl)
        {
            try
            {
                var ct = GetCredentialsType(targetUrl);
                if (ct == CredentialsType.Network)
                {
                    _targetContext = new WrapperImpersonationContext(_targetUser.Domain, _targetUser.Login,
                        _targetUser.Password, GetCredentialsType(targetUrl));

                    _targetContext.Enter();
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + " Module=Logon");
            }
        }
        public void Logout()
        {
            if (_targetContext != null)
            {
                _targetContext.Leave();
                _targetContext.Dispose();
            }
        }
        public void Synchronization(string sUrl, string tUrl)
        {
            WriteToLog("Synchronization. SourceUrl=" + sUrl + "  TargetUrl=" + tUrl);

            try
            {
                _sourceUrl = sUrl;
                _targetUrl = tUrl;
                var scanDatas = ScanUrl(_sourceUrl);
                var delta = DeltaList(_sourceUrl, scanDatas);
                if(delta.Any()) DoneDelta(delta, _sourceUrl, _targetUrl);
                SaveAsManifiest(sUrl, scanDatas);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + " Module=Synchronization");
            }
        }

        private void DoneDelta(IEnumerable<DeltaInfo> delta, string sourceUrl, string targetUrl)
        {
            try
            {
                foreach (var fd in delta)
                {
                    if (File.Exists(targetUrl + "\\" + fd.Name)) File.Delete(targetUrl + "\\" + fd.Name);
                    if (fd.Type == DeltaType.Delete)
                    {
                        if (File.Exists(targetUrl + "\\" + fd.Name)) File.Delete(targetUrl + "\\" + fd.Name);
                        WriteToLog("Delete. " + targetUrl + "\\" + fd.Name);
                    }
                    if (fd.Type == DeltaType.Add || fd.Type == DeltaType.Update)
                    {
                        var cDir = Path.GetDirectoryName(targetUrl + "\\" + fd.Name);
                        if (cDir != "" && !Directory.Exists(cDir)) Directory.CreateDirectory(cDir);
                        File.Copy(sourceUrl + "\\" + fd.Name, targetUrl + "\\" + fd.Name, true);
                        WriteToLog("Copy. " + sourceUrl + "\\" + fd.Name + " -> " + targetUrl + "\\" + fd.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + " Module=DoneDelta");
            }
        }

        private DeltaInfo[] DeltaList(string url, string[] scanDatas)
        {
            var delta = new List<DeltaInfo>();
            try
            {
                var manifestList = ManifestToList(url + $"\\{ManifestFileName}");
                var scanList = ScanDataToList(scanDatas);
                foreach (var manData in manifestList)
                    if (!scanList.Any(a => a.Name == manData.Name)) delta.Add(new DeltaInfo { Name = manData.Name, Type = DeltaType.Delete });
                foreach (var scanData in scanList)
                {
                    var i = manifestList.FindIndex(a => a.Name == scanData.Name);
                    if (i < 0) delta.Add(new DeltaInfo { Name = scanData.Name, Type = DeltaType.Add });
                    else if (scanData.Size != manifestList[i].Size ||
                             scanData.Version != manifestList[i].Version ||
                             scanData.DateCreate != manifestList[i].DateCreate) delta.Add(new DeltaInfo { Name = scanData.Name, Type = DeltaType.Update });
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + " Module=DeltaList");
            }
            return delta.ToArray();
        }
        private static List<ManifestFileInfo> ManifestToList(string sourceManifest)
        {
            var sourceData = new List<ManifestFileInfo>();
            try
            {
                if (File.Exists(sourceManifest))
                {
                    var list = File.ReadAllLines(sourceManifest);
                    foreach (var s in list)
                    {
                        var mi = ToManifestFileInfo(s);
                        if (mi != null) sourceData.Add(mi);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + " Module=ManifestToList");
            }
            return sourceData;
        }
        private static List<ManifestFileInfo> ScanDataToList(string[] scanDatas)
        {
            var sourceData = new List<ManifestFileInfo>();
            try
            {
                foreach (var s in scanDatas)
                {
                    var mi = ToManifestFileInfo(s);
                    if (mi != null) sourceData.Add(mi);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + " Module=ScanDataToList");
            }
            return sourceData;
        }

        private static ManifestFileInfo ToManifestFileInfo(string s)
        {
            ManifestFileInfo ret = null;
            if (s.Length > 9 && s.Substring(0, 9) != "SourceUrl" &&
                                    s.Substring(0, 10) != "MainModule" &&
                                    s.Substring(0, 10) != "TargetPath" &&
                                    s.Substring(0, 10) != "ServiceUrl")
            {
                string a = s;
                try
                {
                    var i = a.IndexOf(separator);
                    var name = a.Substring(0, i);
                    a = a.Substring(i + 1);
                    i = a.IndexOf(separator);
                    var size = a.Substring(0, i);
                    a = a.Substring(i + 1);
                    i = a.IndexOf(separator);
                    var version = a.Substring(0, i);
                    var dateCreate = a.Substring(i + 1);
                    ret = new ManifestFileInfo { Name = name, Size = Convert.ToInt32(size), Version = version, DateCreate = dateCreate };
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message + " Module=ToManifestFileInfo");
                }
            }
            return ret;
        }
        public void SaveAsManifiest(string url, string[] scanDatas)
        {
            try
            {
                if (File.Exists(url + $"\\{ManifestFileName}")) File.Delete(url + $"\\{ManifestFileName}");
                File.AppendAllLines(url + $"\\{ManifestFileName}", scanDatas);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message + " Module=SaveAsManifiest");
            }
        }
        private string[] ScanUrl(string url)
        {
            var fileList = new List<string>();
            var cDir = url != "" ? url + "\\" : "";
            try
            {
                var files = GetFilesByUrl(url);
                foreach (var fileName in files)
                    if (fileName.ToUpper() != ManifestFileName)
                    {
                        var fi = new FileInfo(cDir + fileName);
                        var cVersion = "";
                        if (fi.Extension.ToUpper() == ".EXE" || fi.Extension.ToUpper() == ".DLL")
                        {
                            System.Diagnostics.FileVersionInfo myFileVersionInfo =
                                System.Diagnostics.FileVersionInfo.GetVersionInfo(fi.DirectoryName + "\\" + fi.Name);
                            cVersion = myFileVersionInfo.FileVersion;
                        }
                        var subDir = fi.DirectoryName != null && fi.DirectoryName.Length > url.Length
                            ? fi.DirectoryName.Substring(url.Length + 1)
                            : "";
                        var lSkip = subDir.Length > 8 && subDir.Substring(0, 8) == "Release_";
                        if (!lSkip)
                        {
                            if (subDir != "") subDir = subDir + "\\";
                            var dt = DateTimeToManifestStr(fi.LastWriteTime);
                            fileList.Add(subDir + fi.Name + separator + fi.Length + separator + cVersion + separator + dt);
                        }
                    }
            }
            catch(Exception ex)
            {
                throw new Exception(ex.Message + " Module=ScanUrl");
            }
            return fileList.ToArray();
        }
        private List<string> GetFilesByUrl(string url)
        {
            var ret = new List<string>();
            var fileInfos = GetFileInfosByUrl(url);
            int len = url.Length;
            foreach (var fi in fileInfos)
                if (fi.DirectoryName == null) ret.Add(fi.Name);
                else if (fi.DirectoryName.Substring(0, len) != url) ret.Add(fi.DirectoryName + '\\' + fi.Name);
                else if (fi.DirectoryName.Length == len) ret.Add(fi.Name);
                else ret.Add(fi.DirectoryName.Substring(len + 1) + '\\' + fi.Name);
            return ret;
        }
        private string DateTimeToManifestStr(DateTime date)    //yyyymmdd-hhmmss
        {
            return date.Year.ToString() + date.Month.ToString().PadLeft(2, '0') + date.Day.ToString().PadLeft(2, '0') + '-' +
                   date.Hour.ToString().PadLeft(2, '0') + date.Minute.ToString().PadLeft(2, '0') + date.Second.ToString().PadLeft(2, '0');
        }
        private void WriteToLog(string msg)
        {
            if (_showLog != null) _showLog(msg);
        }
        private List<FileInfo> GetFileInfosByUrl(string url)
        {
            var directoryInfo = new DirectoryInfo(url);
            var fileInfos = new List<FileInfo>();
            fileInfos.AddRange(directoryInfo.GetFiles());
            var directoryInfos = directoryInfo.GetDirectories();
            foreach (var di in directoryInfos)
                fileInfos.AddRange(GetFileInfosByUrl(di.FullName));
            return fileInfos;
        }
        private class ManifestFileInfo
        {
            public string Name;
            public string Version;
            public int Size;
            public string DateCreate;
        }
    }
    public class DeltaInfo
    {
        public string Name;
        public DeltaType Type;

    }
    public enum DeltaType
    {
        Add,Update,Delete
    }
    public enum CredentialsType
    {
        Local, Network
    }
    public enum UserType
    {
        Local, Source, Traget
    }
    public class WrapperImpersonationContext : IDisposable
    {
        private CredentialsType _type;
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern bool LogonUser(String lpszUsername, String lpszDomain,
        String lpszPassword, int dwLogonType, int dwLogonProvider, ref IntPtr phToken);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public extern static bool CloseHandle(IntPtr handle);

        private const int LOGON32_PROVIDER_DEFAULT = 0;
        private const int LOGON32_LOGON_INTERACTIVE = 2;

        private const int LOGON32_PROVIDER_WINNT50 = 3;
        private const int LOGON32_LOGON_NEW_CREDENTIALS = 9;

        private string _domain;
        private string _password;
        private string _username;
        private IntPtr m_Token;

        private System.Security.Principal.WindowsImpersonationContext _context = null;


        protected bool IsInContext
        {
            get { return _context != null; }
        }

        public WrapperImpersonationContext(string domain, string username, string password, CredentialsType type)
        {
            _domain = domain;
            _username = username;
            _password = password;
            _type = type;
        }

        [System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
        public void Enter()
        {
            if (this.IsInContext) return;
            m_Token = new IntPtr(0);
            try
            {
                m_Token = IntPtr.Zero;
                bool logonSuccessfull = LogonUser(
                   _username,
                   _domain,
                   _password,
                   _type == CredentialsType.Local ? LOGON32_LOGON_INTERACTIVE : LOGON32_LOGON_NEW_CREDENTIALS,
                   _type == CredentialsType.Local ? LOGON32_PROVIDER_DEFAULT : LOGON32_PROVIDER_WINNT50,
                   ref m_Token);
                if (logonSuccessfull == false)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new ApplicationException(string.Format("Could not impersonate the elevated user.  LogonUser returned error code {0}.", error));
                }
                System.Security.Principal.WindowsIdentity identity = new System.Security.Principal.WindowsIdentity(m_Token);
                _context = identity.Impersonate();
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not impersonate the elevated user. It may be invalid credentials.");
            }
        }


        [System.Security.Permissions.PermissionSetAttribute(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
        public void Leave()
        {
            if (this.IsInContext == false) return;
            _context.Undo();

            if (m_Token != IntPtr.Zero) CloseHandle(m_Token);
            _context = null;
        }

        public void Dispose()
        {
            if (_context != null)
                _context.Dispose();
        }
    }
   
}
