using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;

namespace SynchronisationProvider
{
    public class Provider
    {
        public string Ip = "";
        public string Port = "";
        private ShowLog _showLog;
        public string _sourceUrl;
        public string _targetUrl;
        public string _currentPath;
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

        private DeltaInfo[] DeltaList(string url, string[] scanDatas)
        {
            var delta = new List<DeltaInfo>();
            try
            {
                var manifestList = ManifestToList(url + "\\MANIFEST.SCA");
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
                var i = a.IndexOf(separator);
                var name = a.Substring(0, i);
                a = a.Substring(i + 1);
                i = a.IndexOf(separator);
                var size = a.Substring(0, i);
                a = a.Substring(i + 1);
                i = a.IndexOf(separator);
                var version = a.Substring(0, i);
                var dateCreate = a.Substring(i + 1);
                try
                {
                    ret = new ManifestFileInfo { Name = name, Size = Convert.ToInt32(size), Version = version, DateCreate = dateCreate };
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message+ " Module=ToManifestFileInfo");
                }
            }
            return ret;
        }

        
        public void SaveAsManifiest(string url, string[] scanDatas)
        {
            if (File.Exists(url + "\\MANIFEST.SCA")) File.Delete(url + "\\MANIFEST.SCA");
            File.AppendAllLines(url + "\\MANIFEST.SCA", scanDatas);
        }
        private string[] ScanUrl(string url)
        {

            var fileList = new List<string>();
            var cDir = url != "" ? url + "\\" : "";
            try
            {
                var files = GetFilesByUrl(url);
                foreach (var fileName in files)
                    if (fileName.ToUpper() != "MANIFEST.SCA")
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
