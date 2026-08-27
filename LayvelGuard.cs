using System;
using System.IO;
using System.Net;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.Win32;

[assembly: AssemblyTitle("LayvelGuard Pro")]
[assembly: AssemblyDescription("LayvelGuard Control & Maintenance Agent")]
[assembly: AssemblyProduct("LayvelGuard Pro")]
[assembly: AssemblyVersion("1.6.0.0")]
[assembly: AssemblyFileVersion("1.6.0.0")]

namespace LayvelGuard
{
    public struct ProhibitedAppInfo
    {
        public string Name;
        public string InstallLocation;
        public string UninstallString;
        public string KeyPath;
    }

    public class Program
    {
        public const string CURRENT_VERSION = "1.6.0";
        public const string APP_NAME = "LayvelGuard";
        public const string APP_DIR = @"C:\LayvelGuard";
        public const string INST_DIR = @"C:\LayvelGuard\Instituciones";
        public const string API_SECRET_TOKEN = "LAYVEL_SECURE_TOKEN_2026_x89a";
        
        // GitHub Repository Central URLs
        private const string GITHUB_REPO_API = "https://api.github.com/repos/layvel/layvelguard/commits/main";
        private const string LOG_FILE = @"C:\LayvelGuard\layvelguard.log";
        public const string CUSTOM_PROHIBITED_FILE = @"C:\LayvelGuard\custom_prohibited.json";

        public static List<string> LoadCustomProhibitedRules()
        {
            List<string> custom = new List<string>();
            try {
                if (File.Exists(CUSTOM_PROHIBITED_FILE))
                {
                    string json = File.ReadAllText(CUSTOM_PROHIBITED_FILE);
                    int startBracket = json.IndexOf('[');
                    int endBracket = json.LastIndexOf(']');
                    if (startBracket != -1 && endBracket > startBracket)
                    {
                        string itemsStr = json.Substring(startBracket + 1, endBracket - startBracket - 1);
                        string[] items = itemsStr.Split(new char[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string item in items)
                        {
                            string clean = item.Replace("\"", "").Replace("'", "").Trim();
                            if (!string.IsNullOrWhiteSpace(clean) && !custom.Contains(clean.ToLower()))
                            {
                                custom.Add(clean.ToLower());
                            }
                        }
                    }
                }
            } catch {}
            return custom;
        }

        public static void SaveCustomProhibitedRule(string rule)
        {
            if (string.IsNullOrWhiteSpace(rule)) return;
            try {
                List<string> existing = LoadCustomProhibitedRules();
                string cleanRule = rule.Trim().ToLower();
                if (!existing.Contains(cleanRule))
                {
                    existing.Add(cleanRule);
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("[");
                    for (int i = 0; i < existing.Count; i++)
                    {
                        sb.AppendFormat("  \"{0}\"{1}\r\n", existing[i], (i < existing.Count - 1) ? "," : "");
                    }
                    sb.AppendLine("]");
                    File.WriteAllText(CUSTOM_PROHIBITED_FILE, sb.ToString(), Encoding.UTF8);
                }
            } catch {}
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        [STAThread]
        public static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            if (!Directory.Exists(APP_DIR)) Directory.CreateDirectory(APP_DIR);
            
            AddAntivirusExclusions();
            EnsureDefaultInstitutionsExist();
            CleanInvalidAutoLogon();

            if (CheckAndUpdateSelf(false)) return;

            if (args != null && args.Length > 0 && (args[0] == "--silent-boot" || args[0] == "-silent"))
            {
                RunSilentBoot();
                return;
            }

            if (!IsAdmin())
            {
                ElevateAdmin();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public static string GetLatestCommitSha()
        {
            try {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                    wc.Headers["Cache-Control"] = "no-cache";
                    wc.Headers["Pragma"] = "no-cache";
                    string atom = wc.DownloadString("https://github.com/layvel/layvelguard/commits/main.atom");
                    int idx = atom.IndexOf("Commit/");
                    if (idx != -1 && atom.Length >= idx + 7 + 40)
                    {
                        string sha = atom.Substring(idx + 7, 40).Trim();
                        if (sha.Length == 40) return sha;
                    }
                }
            } catch {}

            try {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "LayvelGuard-Agent/" + CURRENT_VERSION;
                    string json = wc.DownloadString(GITHUB_REPO_API);
                    string sha = ExtractJsonValue(json, "sha");
                    if (!string.IsNullOrEmpty(sha) && sha.Length >= 7) return sha;
                }
            } catch {}
            return "main";
        }

        public static void EnsureDefaultInstitutionsExist()
        {
            try {
                if (!Directory.Exists(INST_DIR)) Directory.CreateDirectory(INST_DIR);

                string cbmwDir = Path.Combine(INST_DIR, "CBMW");
                string layvelDir = Path.Combine(INST_DIR, "LayvelGuard");

                if (!Directory.Exists(cbmwDir)) Directory.CreateDirectory(cbmwDir);
                if (!Directory.Exists(layvelDir)) Directory.CreateDirectory(layvelDir);

                string sha = GetLatestCommitSha();

                // 1. Restaurar/Copiar Fondo CBMW
                string cbmwFondo = Path.Combine(cbmwDir, "fondo.png");
                if (!File.Exists(cbmwFondo) || new FileInfo(cbmwFondo).Length < 1000)
                {
                    string[] sources = new string[] {
                        @"C:\Users\Profesor2\Downloads\lab\fondo pc.png",
                        @"C:\LayvelGuard\fondo_institucional.png",
                        @"C:\LayvelGuard\layvelguard_logo.png"
                    };
                    bool copied = false;
                    foreach (string s in sources)
                    {
                        if (File.Exists(s)) { try { File.Copy(s, cbmwFondo, true); copied = true; break; } catch {} }
                    }
                    if (!copied) DownloadSingleAssetFromGitHub("Instituciones/CBMW/fondo.png", cbmwFondo, sha);
                }

                // 2. Restaurar/Copiar Logo CBMW
                string cbmwLogo = Path.Combine(cbmwDir, "logo.png");
                if (!File.Exists(cbmwLogo) || new FileInfo(cbmwLogo).Length < 1000)
                {
                    string[] sources = new string[] {
                        @"C:\Users\Profesor2\Downloads\lab\logo.png",
                        @"C:\Users\Profesor2\Downloads\lab\cbmw_logo.jpg",
                        @"C:\LayvelGuard\logo_institucional.png",
                        @"C:\LayvelGuard\layvelguard_logo.png"
                    };
                    bool copied = false;
                    foreach (string s in sources)
                    {
                        if (File.Exists(s)) { try { File.Copy(s, cbmwLogo, true); copied = true; break; } catch {} }
                    }
                    if (!copied) DownloadSingleAssetFromGitHub("Instituciones/CBMW/logo.png", cbmwLogo, sha);
                }

                // 3. Restablecer LayvelGuard por defecto
                string layFondo = Path.Combine(layvelDir, "fondo.png");
                string layLogo = Path.Combine(layvelDir, "logo.png");
                string zoteSource = @"C:\LayvelGuard\layvelguard_logo.png";
                if (File.Exists(zoteSource))
                {
                    if (!File.Exists(layFondo)) try { File.Copy(zoteSource, layFondo, true); } catch {}
                    if (!File.Exists(layLogo)) try { File.Copy(zoteSource, layLogo, true); } catch {}
                }
                else
                {
                    if (!File.Exists(layFondo)) DownloadSingleAssetFromGitHub("Instituciones/LayvelGuard/fondo.png", layFondo, sha);
                    if (!File.Exists(layLogo)) DownloadSingleAssetFromGitHub("Instituciones/LayvelGuard/logo.png", layLogo, sha);
                }
            } catch {}
        }

        public static void DownloadSingleAssetFromGitHub(string gitRelPath, string localDest, string sha = "main")
        {
            try {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "LayvelGuard-Agent/" + CURRENT_VERSION;
                    wc.Headers["Cache-Control"] = "no-cache";
                    string url = string.Format("https://raw.githubusercontent.com/layvel/layvelguard/{0}/{1}", sha, gitRelPath);
                    wc.DownloadFile(url, localDest);
                }
            } catch {}
        }

        public static void SyncAllInstitutionAssetsFromGitHub(string sha = "main")
        {
            try {
                EnsureDefaultInstitutionsExist();
                string cbmwFondo = Path.Combine(INST_DIR, @"CBMW\fondo.png");
                string cbmwLogo = Path.Combine(INST_DIR, @"CBMW\logo.png");
                DownloadSingleAssetFromGitHub("Instituciones/CBMW/fondo.png", cbmwFondo, sha);
                DownloadSingleAssetFromGitHub("Instituciones/CBMW/logo.png", cbmwLogo, sha);
                DownloadSingleAssetFromGitHub("Instituciones/LayvelGuard/fondo.png", Path.Combine(INST_DIR, @"LayvelGuard\fondo.png"), sha);
                DownloadSingleAssetFromGitHub("Instituciones/LayvelGuard/logo.png", Path.Combine(INST_DIR, @"LayvelGuard\logo.png"), sha);
            } catch {}
        }

        public static void CleanInvalidAutoLogon()
        {
            try {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", true))
                {
                    if (key != null)
                    {
                        object defUser = key.GetValue("DefaultUserName");
                        if (defUser != null && (defUser.ToString().Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) || defUser.ToString().Equals("LOCAL SERVICE", StringComparison.OrdinalIgnoreCase) || defUser.ToString().Equals("NETWORK SERVICE", StringComparison.OrdinalIgnoreCase)))
                        {
                            key.DeleteValue("AutoAdminLogon", false);
                            key.DeleteValue("DefaultUserName", false);
                            key.DeleteValue("DefaultPassword", false);
                            key.DeleteValue("ForceAutoLogon", false);
                        }
                    }
                }
            } catch {}
        }

        public static bool CheckAndUpdateSelf(bool force = false)
        {
            try {
                string exePath = Application.ExecutablePath;

                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "LayvelGuard-Agent/" + CURRENT_VERSION;
                    wc.Headers["Cache-Control"] = "no-cache";
                    wc.Headers["Pragma"] = "no-cache";

                    string sha = GetLatestCommitSha();
                    string json = wc.DownloadString(string.Format("https://raw.githubusercontent.com/layvel/layvelguard/{0}/config.json", sha));
                    if (json.Contains("\"script_version\""))
                    {
                        string remoteVer = ExtractJsonValue(json, "script_version");
                        if (force || (!string.IsNullOrEmpty(remoteVer) && remoteVer != CURRENT_VERSION))
                        {
                            string targetExe = Path.Combine(APP_DIR, "LayvelGuard.exe");
                            string tempFile = Path.Combine(APP_DIR, "LayvelGuard_Update.exe");

                            wc.DownloadFile(string.Format("https://raw.githubusercontent.com/layvel/layvelguard/{0}/LayvelGuard.exe", sha), tempFile);

                            if (File.Exists(tempFile) && new FileInfo(tempFile).Length > 10000)
                            {
                                SyncAllInstitutionAssetsFromGitHub(sha);

                                string batchUpdater = Path.Combine(APP_DIR, "update_layvelguard.bat");
                                string script = string.Format(
                                    "@echo off\r\n" +
                                    "timeout /t 1 /nobreak > nul\r\n" +
                                    "copy /y \"{0}\" \"{1}\"\r\n" +
                                    "copy /y \"{0}\" \"{2}\"\r\n" +
                                    "del \"{0}\"\r\n" +
                                    "start \"\" \"{1}\"\r\n" +
                                    "del \"%~f0\"\r\n",
                                    tempFile, targetExe, exePath
                                );
                                File.WriteAllText(batchUpdater, script, Encoding.ASCII);

                                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + batchUpdater + "\"");
                                psi.CreateNoWindow = true;
                                psi.UseShellExecute = false;
                                Process.Start(psi);
                                return true;
                            }
                        }
                    }
                }
            } catch {}
            return false;
        }

        private static bool IsAdmin()
        {
            try {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            } catch { return false; }
        }

        private static void ElevateAdmin()
        {
            try {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                Process.Start(psi);
            } catch {}
        }

        public static void AddAntivirusExclusions()
        {
            try {
                string exePath = Path.Combine(APP_DIR, "LayvelGuard.exe");
                if (!File.Exists(exePath)) exePath = Application.ExecutablePath;

                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", string.Format(
                    "-Command \"Add-MpPreference -ExclusionPath '{0}' -ErrorAction SilentlyContinue; Add-MpPreference -ExclusionProcess '{1}' -ErrorAction SilentlyContinue; Add-MpPreference -ExclusionPath '{2}' -ErrorAction SilentlyContinue\"",
                    APP_DIR, Path.GetFileName(exePath), exePath
                ));
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                Process p = Process.Start(psi);
                if (p != null) p.WaitForExit();
            } catch {}
        }

        public static void RunSilentBoot()
        {
            try {
                AddAntivirusExclusions();
                CleanInvalidAutoLogon();
                InstallStartupTask();
                DoBlockGames();
                EnforceLocalAccountsOnly();
                PurgeConnectedAccountsAndIdentities();
                BlockMouseCustomization();
                EnforceInstitutionalWallpaper();
                
                string cbmwLogo = @"C:\LayvelGuard\Instituciones\CBMW\logo.png";
                if (File.Exists(cbmwLogo)) SetUserProfilePicture(cbmwLogo);

                UninstallProhibitedSoftware(null);
                EnforceDefaultApplications(null);
                
                System.Windows.Forms.Timer daemonTimer = new System.Windows.Forms.Timer();
                daemonTimer.Interval = 10000;
                daemonTimer.Tick += (s, e) => {
                    ThreadPool.QueueUserWorkItem(st => {
                        try {
                            List<string> detectedProcs = KillProhibitedProcesses();
                            List<ProhibitedAppInfo> detectedApps = ScanProhibitedSoftware();
                            List<string> detected = new List<string>();

                            if (detectedProcs != null)
                            {
                                foreach (string p in detectedProcs) if (!detected.Contains(p)) detected.Add(p);
                            }
                            if (detectedApps != null)
                            {
                                foreach (var a in detectedApps) if (!string.IsNullOrWhiteSpace(a.Name) && !detected.Contains(a.Name)) detected.Add(a.Name);
                            }

                            List<string> inv = GetSoftwareInventory();
                            SendTelemetry("ONLINE", detected, inv, null);
                        } catch {}
                    });
                };
                daemonTimer.Start();

                ThreadPool.QueueUserWorkItem(st => {
                    try {
                        List<string> detectedProcs = KillProhibitedProcesses();
                        List<ProhibitedAppInfo> detectedApps = ScanProhibitedSoftware();
                        List<string> detected = new List<string>();
                        if (detectedProcs != null) foreach (string p in detectedProcs) if (!detected.Contains(p)) detected.Add(p);
                        if (detectedApps != null) foreach (var a in detectedApps) if (!string.IsNullOrWhiteSpace(a.Name) && !detected.Contains(a.Name)) detected.Add(a.Name);
                        List<string> inv = GetSoftwareInventory();
                        SendTelemetry("ONLINE", detected, inv, null);
                    } catch {}
                });

                Application.Run();
            } catch {}
        }

        public static void SetWallpapers(string imagePath)
        {
            if (!File.Exists(imagePath)) return;

            try {
                // 1. Escritorio
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                {
                    if (key != null)
                    {
                        key.SetValue("WallpaperStyle", "2");
                        key.SetValue("TileWallpaper", "0");
                        key.SetValue("Wallpaper", imagePath);
                    }
                }
                SystemParametersInfo(0x0014, 0, imagePath, 0x01 | 0x02);

                // 2. Pantalla de Bloqueo vía WinRT API Nativa
                string psCmd = string.Format(
                    "Add-Type -AssemblyName System.Runtime.WindowsRuntime; " +
                    "$asTaskGeneric = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {{ $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' }} | Select-Object -First 1; " +
                    "Function Await($WinRtTask, $ResultType) {{ $asTask = $asTaskGeneric.MakeGenericMethod($ResultType); $netTask = $asTask.Invoke($null, @($WinRtTask)); $netTask.Wait(-1) | Out-Null; $netTask.Result }}; " +
                    "[Windows.System.UserProfile.LockScreen, Windows.System.UserProfile, ContentType = WindowsRuntime] | Out-Null; " +
                    "[Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime] | Out-Null; " +
                    "$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync('{0}')) ([Windows.Storage.StorageFile]); " +
                    "$op = [Windows.System.UserProfile.LockScreen]::SetImageFileAsync($file); " +
                    "[System.WindowsRuntimeSystemExtensions].GetMethod('AsTask', [Type[]]@([Windows.Foundation.IAsyncAction])).Invoke($null, @($op)).Wait(-1);",
                    imagePath.Replace("'", "''")
                );

                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + psCmd + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                Process p = Process.Start(psi);
                if (p != null) p.WaitForExit(8000);

                // 3. Fallback en Políticas de Registro
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Personalization"))
                {
                    if (key != null)
                    {
                        key.SetValue("LockScreenImage", imagePath, RegistryValueKind.String);
                        key.SetValue("LockScreenOverlaysDisabled", 1, RegistryValueKind.DWord);
                        key.SetValue("NoChangingLockScreen", 1, RegistryValueKind.DWord);
                    }
                }
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"))
                {
                    if (key != null)
                    {
                        key.SetValue("RotatingLockScreenEnabled", 0, RegistryValueKind.DWord);
                        key.SetValue("RotatingLockScreenOverlayEnabled", 0, RegistryValueKind.DWord);
                        key.SetValue("SubscribedContent-338387Enabled", 0, RegistryValueKind.DWord);
                    }
                }
            } catch {}
        }

        public static void SetUserProfilePicture(string imagePath)
        {
            if (!File.Exists(imagePath)) return;
            try {
                string accPictures = @"C:\ProgramData\Microsoft\User Account Pictures";
                if (Directory.Exists(accPictures))
                {
                    string[] pics = new string[] { "user.png", "user-32.png", "user-40.png", "user-48.png", "user-192.png" };
                    foreach (string p in pics)
                    {
                        try { File.Copy(imagePath, Path.Combine(accPictures, p), true); } catch {}
                    }
                }
            } catch {}
        }

        public static void DoBlockGames()
        {
            try {
                // 1. DNS Over HTTPS off & Chrome Profile/Signin Restrictions
                string[] chromeKeys = new string[] {
                    @"SOFTWARE\Policies\Google\Chrome",
                    @"Software\Policies\Google\Chrome"
                };

                foreach (string key in chromeKeys)
                {
                    RegistryKey root = key.StartsWith("Software", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine;
                    using (RegistryKey k = root.CreateSubKey(key))
                    {
                        if (k != null)
                        {
                            k.SetValue("DnsOverHttpsMode", "off", RegistryValueKind.String);
                            k.SetValue("BuiltInDnsClientEnabled", 0, RegistryValueKind.DWord);
                            k.SetValue("BrowserSignin", 0, RegistryValueKind.DWord);
                            k.SetValue("SigninAllowed", 0, RegistryValueKind.DWord);
                            k.SetValue("SyncDisabled", 1, RegistryValueKind.DWord);
                            k.SetValue("EphemeralProfileEnabled", 1, RegistryValueKind.DWord);
                            k.SetValue("BrowserAddPersonEnabled", 0, RegistryValueKind.DWord);
                            k.SetValue("ProfilePickerOnStartupEnabled", 0, RegistryValueKind.DWord);
                            k.SetValue("RestrictSigninToPattern", ".*@invalid.domain", RegistryValueKind.String);
                        }
                    }
                }

                // 2. Edge Policies
                string[] edgeKeys = new string[] {
                    @"SOFTWARE\Policies\Microsoft\Edge",
                    @"Software\Policies\Microsoft\Edge"
                };

                foreach (string key in edgeKeys)
                {
                    RegistryKey root = key.StartsWith("Software", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine;
                    using (RegistryKey k = root.CreateSubKey(key))
                    {
                        if (k != null)
                        {
                            k.SetValue("DnsOverHttpsMode", "off", RegistryValueKind.String);
                            k.SetValue("BuiltInDnsClientEnabled", 0, RegistryValueKind.DWord);
                            k.SetValue("BrowserSignin", 0, RegistryValueKind.DWord);
                            k.SetValue("SyncDisabled", 1, RegistryValueKind.DWord);
                            k.SetValue("HideFirstRunExperience", 1, RegistryValueKind.DWord);
                        }
                    }
                }

                // 3. URLBlocklist (Roblox, Minecraft, Steam, Games)
                string[] blockPatterns = new string[] {
                    "*roblox.com*", "*roblox.es*", "*rbxcdn.com*", "*minecraft.net*", "*minecraft.com*",
                    "*steampowered.com*", "*steamcommunity.com*", "*store.steampowered.com*", "*epicgames.com*"
                };

                string[] urlKeys = new string[] {
                    @"SOFTWARE\Policies\Google\Chrome\URLBlocklist",
                    @"Software\Policies\Google\Chrome\URLBlocklist",
                    @"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist",
                    @"Software\Policies\Microsoft\Edge\URLBlocklist"
                };

                foreach (string key in urlKeys)
                {
                    RegistryKey root = key.StartsWith("Software", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine;
                    using (RegistryKey k = root.CreateSubKey(key))
                    {
                        if (k != null)
                        {
                            int idx = 1;
                            foreach (string pat in blockPatterns)
                            {
                                k.SetValue(idx.ToString(), pat, RegistryValueKind.String);
                                idx++;
                            }
                        }
                    }
                }

                // 4. Hosts File (IPv4 e IPv6)
                string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                if (File.Exists(hostsPath))
                {
                    string content = File.ReadAllText(hostsPath);
                    string[] domains = new string[] {
                        "roblox.com", "www.roblox.com", "web.roblox.com", "api.roblox.com",
                        "assetgame.roblox.com", "setup.roblox.com", "minecraft.net", "www.minecraft.net",
                        "steampowered.com", "store.steampowered.com", "steamcommunity.com", "www.steamcommunity.com"
                    };

                    StringBuilder sb = new StringBuilder();
                    foreach (string d in domains)
                    {
                        if (!content.Contains(d))
                        {
                            sb.AppendLine("127.0.0.1 " + d);
                            sb.AppendLine("::1 " + d);
                        }
                    }
                    if (sb.Length > 0)
                    {
                        File.AppendAllText(hostsPath, sb.ToString());
                    }
                }

                // Flush DNS
                Process p = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns") { CreateNoWindow = true, UseShellExecute = false });
                if (p != null) p.WaitForExit();
            } catch {}
        }

        public static void UnblockEquipment()
        {
            try {
                string[] urlKeys = new string[] {
                    @"SOFTWARE\Policies\Google\Chrome\URLBlocklist",
                    @"Software\Policies\Google\Chrome\URLBlocklist",
                    @"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist",
                    @"Software\Policies\Microsoft\Edge\URLBlocklist"
                };

                foreach (string key in urlKeys)
                {
                    RegistryKey root = key.StartsWith("Software", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine;
                    try { root.DeleteSubKeyTree(key); } catch {}
                }

                // Restablecer valores de sesion en Chrome
                string[] chromeKeys = new string[] { @"SOFTWARE\Policies\Google\Chrome", @"Software\Policies\Google\Chrome" };
                foreach (string key in chromeKeys) {
                    RegistryKey root = key.StartsWith("Software", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine;
                    using (RegistryKey k = root.OpenSubKey(key, true)) {
                        if (k != null) {
                            try { k.DeleteValue("BrowserSignin", false); } catch {}
                            try { k.DeleteValue("SigninAllowed", false); } catch {}
                            try { k.DeleteValue("SyncDisabled", false); } catch {}
                            try { k.DeleteValue("EphemeralProfileEnabled", false); } catch {}
                            try { k.DeleteValue("BrowserAddPersonEnabled", false); } catch {}
                            try { k.DeleteValue("ProfilePickerOnStartupEnabled", false); } catch {}
                            try { k.DeleteValue("RestrictSigninToPattern", false); } catch {}
                        }
                    }
                }

                string hostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                if (File.Exists(hostsPath))
                {
                    string[] lines = File.ReadAllLines(hostsPath);
                    List<string> cleanLines = new List<string>();
                    foreach (string line in lines)
                    {
                        if (!line.ToLower().Contains("roblox") && !line.ToLower().Contains("minecraft") && !line.ToLower().Contains("steam"))
                        {
                            cleanLines.Add(line);
                        }
                    }
                    File.WriteAllLines(hostsPath, cleanLines.ToArray());
                }

                Process p = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns") { CreateNoWindow = true, UseShellExecute = false });
                if (p != null) p.WaitForExit();
                
                AllowMouseCustomization();
                AllowWallpaperCustomization();
                AllowMicrosoftAccounts();
            } catch {}
        }

        public static void BlockMouseCustomization(Action<string> logger = null)
        {
            try {
                if (logger != null) logger("Aplicando bloqueo de personalización y tamaño del mouse...");

                string[] policyKeys = new string[] {
                    @"SOFTWARE\Policies\Microsoft\Windows\Control Panel\Desktop",
                    @"Software\Policies\Microsoft\Windows\Control Panel\Desktop",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"
                };

                foreach (string keyPath in policyKeys)
                {
                    try {
                        RegistryKey root = keyPath.StartsWith("Software", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine;
                        using (RegistryKey key = root.CreateSubKey(keyPath))
                        {
                            if (key != null)
                            {
                                key.SetValue("NoChangingMousePointers", 1, RegistryValueKind.DWord);
                            }
                        }
                    } catch {}
                }

                try {
                    using (RegistryKey accessKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Accessibility"))
                    {
                        if (accessKey != null)
                        {
                            accessKey.SetValue("CursorSize", 1, RegistryValueKind.DWord);
                            accessKey.SetValue("CursorColor", 0, RegistryValueKind.DWord);
                        }
                    }
                } catch {}

                try {
                    using (RegistryKey cursorKey = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
                    {
                        if (cursorKey != null)
                        {
                            cursorKey.SetValue("", "Windows Default", RegistryValueKind.String);
                            cursorKey.SetValue("Scheme Source", 0, RegistryValueKind.DWord);
                            cursorKey.SetValue("CursorBaseSize", 32, RegistryValueKind.DWord);

                            cursorKey.SetValue("AppStarting", @"%SystemRoot%\cursors\aero_working.ani", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("Arrow", @"%SystemRoot%\cursors\aero_arrow.cur", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("Crosshair", "", RegistryValueKind.String);
                            cursorKey.SetValue("Hand", @"%SystemRoot%\cursors\aero_link.cur", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("Help", @"%SystemRoot%\cursors\aero_helpsel.cur", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("IBeam", "", RegistryValueKind.String);
                            cursorKey.SetValue("No", @"%SystemRoot%\cursors\aero_unavail.cur", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("NWPen", @"%SystemRoot%\cursors\aero_pen.cur", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("SizeAll", @"%SystemRoot%\cursors\aero_move.cur", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("SizeNESW", @"%SystemRoot%\cursors\aero_nesw.cur", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("SizeNS", @"%SystemRoot%\cursors\aero_ns.cur", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("SizeNWSE", @"%SystemRoot%\cursors\aero_nwse.cur", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("SizeWE", @"%SystemRoot%\cursors\aero_we.cur", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("UpArrow", @"%SystemRoot%\cursors\aero_up.cur", RegistryValueKind.ExpandString);
                            cursorKey.SetValue("Wait", @"%SystemRoot%\cursors\aero_busy.ani", RegistryValueKind.ExpandString);
                        }
                    }
                } catch {}

                try {
                    SystemParametersInfo(0x0057, 0, null, 0x01 | 0x02);
                } catch {}

                if (logger != null) logger("Bloqueo de personalización y tamaño del mouse aplicado correctamente.");
            } catch (Exception ex) {
                if (logger != null) logger("Aviso al aplicar bloqueo de mouse: " + ex.Message);
            }
        }

        public static void AllowMouseCustomization(Action<string> logger = null)
        {
            try {
                if (logger != null) logger("Removiendo bloqueo de personalización de mouse...");

                string[] policyKeys = new string[] {
                    @"SOFTWARE\Policies\Microsoft\Windows\Control Panel\Desktop",
                    @"Software\Policies\Microsoft\Windows\Control Panel\Desktop",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"
                };

                foreach (string keyPath in policyKeys)
                {
                    try {
                        RegistryKey root = keyPath.StartsWith("Software", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine;
                        using (RegistryKey key = root.OpenSubKey(keyPath, true))
                        {
                            if (key != null)
                            {
                                key.DeleteValue("NoChangingMousePointers", false);
                            }
                        }
                    } catch {}
                }

                if (logger != null) logger("Personalización de mouse permitida.");
            } catch {}
        }

        public static void EnforceLocalAccountsOnly(Action<string> logger = null)
        {
            try {
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    if (key != null)
                    {
                        key.SetValue("NoConnectedUser", 3, RegistryValueKind.DWord);
                    }
                }
                PurgeConnectedAccountsAndIdentities(logger);
            } catch {}
        }

        public static void PurgeConnectedAccountsAndIdentities(Action<string> logger = null)
        {
            try {
                if (logger != null) logger("Iniciando purga radical de cuentas Microsoft/Escuela y desvinculacion total...");

                // 1. DESVINCULAR CUENTAS MICROSOFT DE PROFILELIST (Rompe la asociación MSA a nivel de Windows)
                if (logger != null) logger("   - Desvinculando identidades MSA en HKLM ProfileList...");
                try {
                    using (RegistryKey pList = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList", true))
                    {
                        if (pList != null)
                        {
                            foreach (string sidName in pList.GetSubKeyNames())
                            {
                                try {
                                    using (RegistryKey sidKey = pList.OpenSubKey(sidName, true))
                                    {
                                        if (sidKey != null)
                                        {
                                            sidKey.DeleteValue("ConnectedIdentity", false);
                                            sidKey.DeleteValue("InternetUserName", false);
                                            sidKey.DeleteValue("InternetUID", false);
                                            sidKey.DeleteValue("InternetProviderGUID", false);
                                            sidKey.DeleteValue("InternetSid", false);
                                            sidKey.DeleteValue("UserHome", false);
                                        }
                                    }
                                } catch {}
                            }
                        }
                    }
                } catch {}

                // 2. LIMPIEZA DE IDENTITYCRL, ACCOUNTSETTINGS, WORKPLACEJOIN Y OFFICE EN REGISTRO (HKCU, HKLM, HKEY_USERS)
                if (logger != null) logger("   - Eliminando identidades registradas en IdentityCRL, AccountSettings y WorkplaceJoin...");
                string[] regSubTrees = new string[] {
                    @"Software\Microsoft\IdentityCRL",
                    @"Software\Microsoft\Windows\CurrentVersion\AccountSettings",
                    @"Software\Microsoft\Windows NT\CurrentVersion\WorkplaceJoin",
                    @"Software\Microsoft\Office\16.0\Common\Identity"
                };

                // HKCU
                foreach (string subTree in regSubTrees)
                {
                    try {
                        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(subTree, true))
                        {
                            if (k != null)
                            {
                                foreach (string sub in k.GetSubKeyNames())
                                {
                                    try { k.DeleteSubKeyTree(sub); } catch {}
                                }
                            }
                        }
                    } catch {}
                }

                // HKLM
                string[] lmidKeys = new string[] {
                    @"SOFTWARE\Microsoft\IdentityCRL",
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountSettings",
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\WorkplaceJoin"
                };
                foreach (string subTree in lmidKeys)
                {
                    try {
                        using (RegistryKey k = Registry.LocalMachine.OpenSubKey(subTree, true))
                        {
                            if (k != null)
                            {
                                foreach (string sub in k.GetSubKeyNames())
                                {
                                    try { k.DeleteSubKeyTree(sub); } catch {}
                                }
                            }
                        }
                    } catch {}
                }

                // HKEY_USERS (Todos los perfiles de usuario montados)
                try {
                    foreach (string uSid in Registry.Users.GetSubKeyNames())
                    {
                        if (uSid.StartsWith("S-1-5-21-") && !uSid.EndsWith("_Classes"))
                        {
                            foreach (string subTree in regSubTrees)
                            {
                                try {
                                    using (RegistryKey k = Registry.Users.OpenSubKey(uSid + "\\" + subTree, true))
                                    {
                                        if (k != null)
                                        {
                                            foreach (string sub in k.GetSubKeyNames())
                                            {
                                                try { k.DeleteSubKeyTree(sub); } catch {}
                                            }
                                        }
                                    }
                                } catch {}
                            }
                        }
                    }
                } catch {}

                // 3. PURGA TOTAL DE TOKENBROKER, CREDENTIALS, VAULT Y PAQUETES WAM EN C:\Users\*
                if (logger != null) logger("   - Purgando TokenBroker, Credentials, Vault y paquetes WAM en perfiles de C:\\Users...");
                try {
                    string systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
                    string usersDir = Path.Combine(systemDrive, "Users");
                    if (Directory.Exists(usersDir))
                    {
                        foreach (string userFolder in Directory.GetDirectories(usersDir))
                        {
                            string uName = Path.GetFileName(userFolder);
                            if (uName.Equals("Public", StringComparison.OrdinalIgnoreCase) ||
                                uName.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                                uName.Equals("Default User", StringComparison.OrdinalIgnoreCase) ||
                                uName.Equals("All Users", StringComparison.OrdinalIgnoreCase)) continue;

                            // TokenBroker (Limpieza completa de la carpeta)
                            string tokenDir = Path.Combine(userFolder, @"AppData\Local\Microsoft\TokenBroker");
                            if (Directory.Exists(tokenDir))
                            {
                                try {
                                    Directory.Delete(tokenDir, true);
                                    if (logger != null) logger("     * TokenBroker eliminado en: " + uName);
                                } catch {}
                            }

                            // Credentials & Vault
                            string[] credDirs = new string[] {
                                Path.Combine(userFolder, @"AppData\Local\Microsoft\Credentials"),
                                Path.Combine(userFolder, @"AppData\Roaming\Microsoft\Credentials"),
                                Path.Combine(userFolder, @"AppData\Local\Microsoft\Vault"),
                                Path.Combine(userFolder, @"AppData\Roaming\Microsoft\Vault")
                            };
                            foreach (string cd in credDirs)
                            {
                                try {
                                    if (Directory.Exists(cd)) Directory.Delete(cd, true);
                                } catch {}
                            }

                            // Paquetes UWP de autenticación y Cloud Experience
                            string packagesDir = Path.Combine(userFolder, @"AppData\Local\Packages");
                            if (Directory.Exists(packagesDir))
                            {
                                string[] patterns = new string[] {
                                    "Microsoft.AAD.BrokerPlugin_*",
                                    "Microsoft.AccountsControl_*",
                                    "Microsoft.Windows.CloudExperienceHost_*"
                                };
                                foreach (string pat in patterns)
                                {
                                    try {
                                        foreach (string pkg in Directory.GetDirectories(packagesDir, pat))
                                        {
                                            try { Directory.Delete(pkg, true); } catch {}
                                        }
                                    } catch {}
                                }
                            }
                        }
                    }
                } catch {}

                // 4. PURGA DE CREDENCIALES EN WINDOWS VAULT / CMDKEY
                if (logger != null) logger("   - Escaneando y purgando credenciales guardadas en Windows Credential Manager...");
                try {
                    ProcessStartInfo psi = new ProcessStartInfo("cmdkey", "/list");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    Process p = Process.Start(psi);
                    if (p != null)
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit();

                        string[] lines = output.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string line in lines)
                        {
                            if (line.Contains("Target:") || line.Contains("Destino:"))
                            {
                                int idx = line.IndexOf(':');
                                if (idx != -1)
                                {
                                    string target = line.Substring(idx + 1).Trim();
                                    if (target.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) != -1 ||
                                        target.IndexOf("WindowsLive", StringComparison.OrdinalIgnoreCase) != -1 ||
                                        target.IndexOf("Xbox", StringComparison.OrdinalIgnoreCase) != -1 ||
                                        target.IndexOf("Xbl", StringComparison.OrdinalIgnoreCase) != -1 ||
                                        target.IndexOf("SSO", StringComparison.OrdinalIgnoreCase) != -1 ||
                                        target.Contains("@"))
                                    {
                                        try {
                                            ProcessStartInfo delPsi = new ProcessStartInfo("cmdkey", "/delete:\"" + target + "\"");
                                            delPsi.CreateNoWindow = true;
                                            delPsi.UseShellExecute = false;
                                            Process delP = Process.Start(delPsi);
                                            if (delP != null) delP.WaitForExit();
                                            if (logger != null) logger("     * Credencial desvinculada: " + target);
                                        } catch {}
                                    }
                                }
                            }
                        }
                    }
                } catch {}

                // 5. DESACTIVAR CUENTAS LOCALES DE TIPO CORREO
                if (logger != null) logger("   - Verificando si existen cuentas locales creadas con formato de correo...");
                try {
                    ProcessStartInfo psi = new ProcessStartInfo("net", "user");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    Process p = Process.Start(psi);
                    if (p != null)
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit();

                        string[] lines = output.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string line in lines)
                        {
                            string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string user in parts)
                            {
                                if (user.Contains("@"))
                                {
                                    try {
                                        ProcessStartInfo delUser = new ProcessStartInfo("net", "user \"" + user + "\" /active:no");
                                        delUser.CreateNoWindow = true;
                                        delUser.UseShellExecute = false;
                                        Process dp = Process.Start(delUser);
                                        if (dp != null) dp.WaitForExit();
                                        if (logger != null) logger("     * Cuenta de correo local desactivada: " + user);
                                    } catch {}
                                }
                            }
                        }
                    }
                } catch {}

                // 6. RESET RADICAL DE NAVEGADORES (Chrome y Edge)
                if (logger != null) logger("   - Limpiando perfiles de Chrome y Edge para desvincular cuentas de correo en navegadores...");
                ResetBrowserUserData(logger);

                // 7. REINICIAR PROCESOS DE EXPERIENCIA SHELL Y MENÚ INICIO PARA FORZAR RECARGA
                if (logger != null) logger("   - Refrescando la caché del Menú Inicio y Shell de Windows 11...");
                try {
                    string[] procsToKill = new string[] {
                        "StartMenuExperienceHost",
                        "ShellExperienceHost",
                        "TokenBrokerHost",
                        "SearchHost",
                        "SystemSettings"
                    };
                    foreach (string procName in procsToKill)
                    {
                        try {
                            foreach (Process pr in Process.GetProcessesByName(procName))
                            {
                                try { pr.Kill(); pr.WaitForExit(1000); } catch {}
                            }
                        } catch {}
                    }
                } catch {}

                if (logger != null) logger("--> Purga radical y desvinculacion de cuentas Microsoft completada con éxito.");
            } catch (Exception ex) {
                if (logger != null) logger("Aviso en purga de cuentas de correo: " + ex.Message);
            }
        }

        public static void AllowMicrosoftAccounts(Action<string> logger = null)
        {
            try {
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    if (key != null)
                    {
                        key.DeleteValue("NoConnectedUser", false);
                    }
                }
                if (logger != null) logger("Restricción de cuentas Microsoft removida.");
            } catch {}
        }

        public const int SPI_SETDESKWALLPAPER = 20;
        public const int SPIF_UPDATEINIFILE = 0x01;
        public const int SPIF_SENDCHANGE = 0x02;

        public static void EnforceInstitutionalWallpaper(Action<string> logger = null)
        {
            try {
                if (logger != null) logger("Aplicando bloqueo rígido de Fondo de Pantalla contra aplicaciones externas...");

                // 1. Políticas ActiveDesktop (HKCU & HKLM)
                using (RegistryKey actPolicy = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"))
                {
                    if (actPolicy != null) actPolicy.SetValue("NoChangingWallPaper", 1, RegistryValueKind.DWord);
                }
                using (RegistryKey actPolicyHklm = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"))
                {
                    if (actPolicyHklm != null) actPolicyHklm.SetValue("NoChangingWallPaper", 1, RegistryValueKind.DWord);
                }

                // 2. Políticas Personalization (HKCU & HKLM)
                using (RegistryKey persPolicy = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\Personalization"))
                {
                    if (persPolicy != null) persPolicy.SetValue("NoChangingWallPaper", 1, RegistryValueKind.DWord);
                }
                using (RegistryKey persPolicyHklm = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Personalization"))
                {
                    if (persPolicyHklm != null)
                    {
                        persPolicyHklm.SetValue("NoChangingWallPaper", 1, RegistryValueKind.DWord);
                        persPolicyHklm.SetValue("NoChangingLockScreen", 1, RegistryValueKind.DWord);
                    }
                }

                // 3. Políticas Explorer (Deshabilita clic derecho 'Establecer como fondo')
                using (RegistryKey expPolicy = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                {
                    if (expPolicy != null)
                    {
                        expPolicy.SetValue("NoSetWallpaper", 1, RegistryValueKind.DWord);
                        expPolicy.SetValue("NoActiveDesktopChanges", 1, RegistryValueKind.DWord);
                        expPolicy.SetValue("NoActiveDesktop", 1, RegistryValueKind.DWord);
                    }
                }
                using (RegistryKey expPolicyHklm = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer"))
                {
                    if (expPolicyHklm != null) expPolicyHklm.SetValue("NoSetWallpaper", 1, RegistryValueKind.DWord);
                }

                // 4. Políticas System (Bloquea pestaña de fondo en configuración)
                using (RegistryKey sysPolicy = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    if (sysPolicy != null) sysPolicy.SetValue("NoDispBackgroundPage", 1, RegistryValueKind.DWord);
                }

                // 5. Cerrar aplicaciones de fondos de terceros (Lively, Wallpaper Engine, Rainmeter, etc.)
                KillProhibitedProcesses();

                // 6. Aplicar y fijar fondo institucional
                string bgPath = @"C:\LayvelGuard\Instituciones\CBMW\fondo.png";
                if (!File.Exists(bgPath)) bgPath = @"C:\LayvelGuard\Instituciones\CBMW\fondo.jpg";
                if (!File.Exists(bgPath))
                {
                    try {
                        if (Directory.Exists(@"C:\LayvelGuard\Instituciones"))
                        {
                            foreach (string sub in Directory.GetDirectories(@"C:\LayvelGuard\Instituciones"))
                            {
                                string png = Path.Combine(sub, "fondo.png");
                                string jpg = Path.Combine(sub, "fondo.jpg");
                                if (File.Exists(png)) { bgPath = png; break; }
                                if (File.Exists(jpg)) { bgPath = jpg; break; }
                            }
                        }
                    } catch {}
                }

                if (File.Exists(bgPath))
                {
                    using (RegistryKey sysPolicy = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\System"))
                    {
                        if (sysPolicy != null)
                        {
                            sysPolicy.SetValue("Wallpaper", bgPath, RegistryValueKind.String);
                            sysPolicy.SetValue("WallpaperStyle", "2", RegistryValueKind.String);
                        }
                    }
                    using (RegistryKey sysPolicyHklm = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                    {
                        if (sysPolicyHklm != null)
                        {
                            sysPolicyHklm.SetValue("Wallpaper", bgPath, RegistryValueKind.String);
                            sysPolicyHklm.SetValue("WallpaperStyle", "2", RegistryValueKind.String);
                        }
                    }

                    SetWallpapers(bgPath);
                    if (logger != null) logger("   -> Fondo institucional fijado y protegido: " + Path.GetFileName(bgPath));
                }

                if (logger != null) logger("Bloqueo rígido de Fondo de Pantalla y Apps Externas activado con éxito.");
            } catch (Exception ex) {
                if (logger != null) logger("Aviso en bloqueo de fondo: " + ex.Message);
            }
        }

        public static void AllowWallpaperCustomization(Action<string> logger = null)
        {
            try {
                if (logger != null) logger("Removiendo bloqueo rígido de Fondo de Pantalla...");

                string[] keys = new string[] {
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop",
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\System",
                    @"Software\Policies\Microsoft\Windows\Personalization"
                };

                foreach (string k in keys)
                {
                    try {
                        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(k, true))
                        {
                            if (key != null)
                            {
                                key.DeleteValue("NoChangingWallPaper", false);
                                key.DeleteValue("NoDispBackgroundPage", false);
                                key.DeleteValue("NoActiveDesktopChanges", false);
                                key.DeleteValue("NoActiveDesktop", false);
                                key.DeleteValue("NoSetWallpaper", false);
                                key.DeleteValue("Wallpaper", false);
                                key.DeleteValue("WallpaperStyle", false);
                            }
                        }
                    } catch {}

                    try {
                        string hklmPath = k.StartsWith("Software", StringComparison.OrdinalIgnoreCase) ? "SOFTWARE" + k.Substring(8) : k;
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(hklmPath, true))
                        {
                            if (key != null)
                            {
                                key.DeleteValue("NoChangingWallPaper", false);
                                key.DeleteValue("NoDispBackgroundPage", false);
                                key.DeleteValue("NoSetWallpaper", false);
                                key.DeleteValue("NoChangingLockScreen", false);
                                key.DeleteValue("Wallpaper", false);
                                key.DeleteValue("WallpaperStyle", false);
                            }
                        }
                    } catch {}
                }

                if (logger != null) logger("Personalización de fondo de pantalla permitida.");
            } catch {}
        }

        public static void InstallStartupTask(bool enable = true)
        {
            try {
                string exePath = Path.Combine(APP_DIR, "LayvelGuard.exe");
                if (!File.Exists(exePath)) exePath = Application.ExecutablePath;

                if (enable)
                {
                    ProcessStartInfo psi = new ProcessStartInfo("schtasks", string.Format("/create /tn \"LayvelGuard_Daemon\" /tr \"\\\"{0}\\\" --silent-boot\" /sc ONSTART /ru \"SYSTEM\" /rl HIGHEST /f", exePath));
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process p = Process.Start(psi);
                    if (p != null) p.WaitForExit();

                    using (RegistryKey k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        if (k != null) k.SetValue("LayvelGuard", "\"" + exePath + "\" --silent-boot", RegistryValueKind.String);
                    }
                }
                else
                {
                    ProcessStartInfo psi = new ProcessStartInfo("schtasks", "/delete /tn \"LayvelGuard_Daemon\" /f");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process p = Process.Start(psi);
                    if (p != null) p.WaitForExit();

                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (k != null) k.DeleteValue("LayvelGuard", false);
                    }
                }
            } catch {}
        }

        public static List<ProhibitedAppInfo> GetAllInstalledApplications()
        {
            List<ProhibitedAppInfo> allApps = new List<ProhibitedAppInfo>();

            string[] regKeys = new string[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            // 1. Registro HKLM y HKCU
            RegistryKey[] rootKeys = new RegistryKey[] { Registry.LocalMachine, Registry.CurrentUser };
            foreach (RegistryKey root in rootKeys)
            {
                foreach (string keyPath in regKeys)
                {
                    try {
                        using (RegistryKey key = root.OpenSubKey(keyPath))
                        {
                            if (key != null)
                            {
                                foreach (string subkeyName in key.GetSubKeyNames())
                                {
                                    using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                                    {
                                        if (subkey != null)
                                        {
                                            object displayNameObj = subkey.GetValue("DisplayName");
                                            if (displayNameObj != null)
                                            {
                                                string dispName = displayNameObj.ToString().Trim();
                                                if (string.IsNullOrWhiteSpace(dispName)) continue;
                                                if (dispName.StartsWith("KB", StringComparison.OrdinalIgnoreCase) && dispName.Length > 8) continue;

                                                ProhibitedAppInfo app = new ProhibitedAppInfo();
                                                app.Name = dispName;
                                                object unist = subkey.GetValue("QuietUninstallString") ?? subkey.GetValue("UninstallString");
                                                app.UninstallString = unist != null ? unist.ToString() : "";
                                                object instLoc = subkey.GetValue("InstallLocation");
                                                app.InstallLocation = instLoc != null ? instLoc.ToString() : "";
                                                app.KeyPath = (root == Registry.LocalMachine ? "HKLM\\" : "HKCU\\") + keyPath + "\\" + subkeyName;

                                                bool already = false;
                                                foreach (var f in allApps)
                                                {
                                                    if (f.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase)) already = true;
                                                }
                                                if (!already) allApps.Add(app);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    } catch {}
                }
            }

            // 2. Registro HKEY_USERS (Todos los perfiles de usuario del equipo)
            try {
                using (RegistryKey hkUsers = Registry.Users)
                {
                    if (hkUsers != null)
                    {
                        foreach (string sid in hkUsers.GetSubKeyNames())
                        {
                            if (sid.StartsWith("S-1-5-21-") && !sid.EndsWith("_Classes"))
                            {
                                foreach (string keyPath in regKeys)
                                {
                                    try {
                                        using (RegistryKey subKeyHive = hkUsers.OpenSubKey(sid + "\\" + keyPath))
                                        {
                                            if (subKeyHive != null)
                                            {
                                                foreach (string subkeyName in subKeyHive.GetSubKeyNames())
                                                {
                                                    using (RegistryKey subkey = subKeyHive.OpenSubKey(subkeyName))
                                                    {
                                                        if (subkey != null)
                                                        {
                                                            object displayNameObj = subkey.GetValue("DisplayName");
                                                            if (displayNameObj != null)
                                                            {
                                                                string dispName = displayNameObj.ToString().Trim();
                                                                if (string.IsNullOrWhiteSpace(dispName)) continue;

                                                                ProhibitedAppInfo app = new ProhibitedAppInfo();
                                                                app.Name = dispName;
                                                                object unist = subkey.GetValue("QuietUninstallString") ?? subkey.GetValue("UninstallString");
                                                                app.UninstallString = unist != null ? unist.ToString() : "";
                                                                object instLoc = subkey.GetValue("InstallLocation");
                                                                app.InstallLocation = instLoc != null ? instLoc.ToString() : "";
                                                                app.KeyPath = "HKEY_USERS\\" + sid + "\\" + keyPath + "\\" + subkeyName;

                                                                bool already = false;
                                                                foreach (var f in allApps)
                                                                {
                                                                    if (f.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase)) already = true;
                                                                }
                                                                if (!already) allApps.Add(app);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    } catch {}
                                }
                            }
                        }
                    }
                }
            } catch {}

            // 3. Escaneo en Carpetas AppData en C:\Users\* (osu!, Discord, Telegram, Portable Games, etc.)
            try {
                if (Directory.Exists(@"C:\Users"))
                {
                    foreach (string userDir in Directory.GetDirectories(@"C:\Users"))
                    {
                        string uName = Path.GetFileName(userDir);
                        if (uName.Equals("Public", StringComparison.OrdinalIgnoreCase) || uName.Equals("Default", StringComparison.OrdinalIgnoreCase) || uName.Equals("All Users", StringComparison.OrdinalIgnoreCase)) continue;

                        string[] appDataDirs = new string[] {
                            Path.Combine(userDir, @"AppData\Local"),
                            Path.Combine(userDir, @"AppData\Roaming"),
                            Path.Combine(userDir, @"AppData\Local\Programs")
                        };

                        foreach (string parentDir in appDataDirs)
                        {
                            if (Directory.Exists(parentDir))
                            {
                                try {
                                    foreach (string subDir in Directory.GetDirectories(parentDir))
                                    {
                                        string dirName = Path.GetFileName(subDir);
                                        if (dirName.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) || dirName.Equals("Temp", StringComparison.OrdinalIgnoreCase) || dirName.Equals("Packages", StringComparison.OrdinalIgnoreCase) || dirName.Equals("CrashDumps", StringComparison.OrdinalIgnoreCase)) continue;

                                        string[] exes = Directory.GetFiles(subDir, "*.exe", SearchOption.TopDirectoryOnly);
                                        if (exes.Length > 0)
                                        {
                                            bool already = false;
                                            foreach (var f in allApps)
                                            {
                                                if (f.Name.StartsWith(dirName, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(f.InstallLocation) && f.InstallLocation.Equals(subDir, StringComparison.OrdinalIgnoreCase))) already = true;
                                            }
                                            if (!already)
                                            {
                                                ProhibitedAppInfo app = new ProhibitedAppInfo();
                                                app.Name = string.Format("{0} (Carpeta AppData: {1})", dirName, uName);
                                                app.InstallLocation = subDir;
                                                app.UninstallString = "";
                                                allApps.Add(app);
                                            }
                                        }
                                    }
                                } catch {}
                            }
                        }
                    }
                }
            } catch {}

            // 4. Escaneo de Accesos Directos del Menú Inicio
            try {
                string[] startMenuPaths = new string[] {
                    @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs",
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs)
                };

                foreach (string smPath in startMenuPaths)
                {
                    if (Directory.Exists(smPath))
                    {
                        try {
                            foreach (string lnk in Directory.GetFiles(smPath, "*.lnk", SearchOption.AllDirectories))
                            {
                                string lnkName = Path.GetFileNameWithoutExtension(lnk);
                                if (lnkName.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase) || lnkName.StartsWith("Desinstalar", StringComparison.OrdinalIgnoreCase)) continue;

                                bool already = false;
                                foreach (var f in allApps)
                                {
                                    if (f.Name.Contains(lnkName) || lnkName.Contains(f.Name)) already = true;
                                }
                                if (!already)
                                {
                                    ProhibitedAppInfo app = new ProhibitedAppInfo();
                                    app.Name = lnkName + " (Menú Inicio)";
                                    app.InstallLocation = Path.GetDirectoryName(lnk);
                                    app.UninstallString = "";
                                    allApps.Add(app);
                                }
                            }
                        } catch {}
                    }
                }
            } catch {}

            return allApps;
        }

        public static List<ProhibitedAppInfo> ScanProhibitedSoftware()
        {
            List<ProhibitedAppInfo> found = new List<ProhibitedAppInfo>();
            List<ProhibitedAppInfo> allSystemApps = GetAllInstalledApplications();

            List<string> prohibitedKeywordsList = new List<string>() {
                // Juegos, Launchers & Emuladores
                "roblox", "steam", "minecraft", "epic games", "valorant", "league of legends", "origin", "ea app",
                "bluestacks", "ldplayer", "nox", "memu", "cheat engine", "stumble guys", "tlauncher", "lunar client", "feather client", "osu",
                // Torrents, P2P & Reproductores No Autorizados
                "utorrent", "bittorrent", "qbittorrent", "ares", "popcorn time", "stremio",
                // Navegadores No Autorizados (Solo Edge y Chrome permitidos)
                "firefox", "opera", "operagx", "brave", "vivaldi", "tor browser", "yandex", "uc browser", "waterfox", "chromium",
                // Editores y Suites Ofimáticas No Autorizadas (Solo MS Office y Nitro PDF permitidos)
                "libreoffice", "openoffice", "wps office", "wps", "onlyoffice", "freeoffice", "polaris office", "abiword", "foxit",
                // Antivirus & Limpiadores No Autorizados (Solo Windows Defender permitido)
                "avast", "avg", "avira", "kaspersky", "mcafee", "norton", "bitdefender", "panda", "eset", "sophos", "malwarebytes", "360 total security", "ccleaner",
                // Mensajería, Control Remoto y Apps de Fondo No Autorizadas
                "discord", "telegram", "whatsapp", "anydesk", "teamviewer", "parsec", "lively", "wallpaper engine", "rainmeter", "deskscapes", "bing wallpaper", "plastuer", "autodarkmode", "translucenttb", "chameleon"
            };

            List<string> customRules = LoadCustomProhibitedRules();
            foreach (string cr in customRules)
            {
                if (!prohibitedKeywordsList.Contains(cr.ToLower()))
                {
                    prohibitedKeywordsList.Add(cr.ToLower());
                }
            }

            foreach (var app in allSystemApps)
            {
                string lowerName = app.Name.ToLower();
                string lowerLoc = (app.InstallLocation ?? "").ToLower();

                bool isProhibited = false;
                foreach (string kw in prohibitedKeywordsList)
                {
                    if (lowerName.Contains(kw) || lowerLoc.Contains(kw))
                    {
                        isProhibited = true;
                        break;
                    }
                }

                if (isProhibited)
                {
                    bool already = false;
                    foreach (var f in found)
                    {
                        if (f.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase)) already = true;
                    }
                    if (!already) found.Add(app);
                }
            }

            return found;
        }

        public static void UninstallSpecificSoftware(List<ProhibitedAppInfo> selectedApps, Action<string> logger = null)
        {
            if (logger != null) logger(string.Format("--> Procesando desinstalacion de {0} elementos seleccionados...", selectedApps.Count));

            // 1. Cerrar procesos activos
            List<string> killed = KillProhibitedProcesses();
            if (logger != null && killed.Count > 0)
            {
                logger(string.Format("   - Procesos prohibidos en ejecucion cerrados: {0}", string.Join(", ", killed.ToArray())));
            }

            foreach (var app in selectedApps)
            {
                if (logger != null) logger(string.Format("-> Eliminando: {0}", app.Name));

                // Desinstalador registrado
                if (!string.IsNullOrEmpty(app.UninstallString))
                {
                    try {
                        string uncmd = app.UninstallString;
                        string exe = uncmd;
                        string args = "";

                        if (uncmd.StartsWith("\""))
                        {
                            int endQuote = uncmd.IndexOf("\"", 1);
                            if (endQuote != -1)
                            {
                                exe = uncmd.Substring(1, endQuote - 1);
                                args = uncmd.Substring(endQuote + 1).Trim();
                            }
                        }
                        else
                        {
                            int spaceIdx = uncmd.IndexOf(" ");
                            if (spaceIdx != -1)
                            {
                                exe = uncmd.Substring(0, spaceIdx);
                                args = uncmd.Substring(spaceIdx + 1).Trim();
                            }
                        }

                        if (!args.Contains("/S") && !args.Contains("/s") && !args.Contains("/quiet") && !args.Contains("/qn"))
                        {
                            if (exe.ToLower().EndsWith("msiexec.exe") || exe.ToLower().EndsWith("msiexec"))
                            {
                                args += " /quiet /norestart";
                            }
                            else
                            {
                                args += " /S /silent /quiet";
                            }
                        }

                        ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                        psi.CreateNoWindow = true;
                        psi.UseShellExecute = false;
                        Process p = Process.Start(psi);
                        if (p != null) p.WaitForExit(8000);
                        if (logger != null) logger(string.Format("   - Ejecutada desinstalacion silenciosa para {0}", app.Name));
                    } catch {}
                }

                // LIMPIEZA DE REGISTRO HUÉRFANO
                if (!string.IsNullOrEmpty(app.KeyPath))
                {
                    try {
                        if (app.KeyPath.StartsWith("HKEY_USERS\\", StringComparison.OrdinalIgnoreCase))
                        {
                            string sub = app.KeyPath.Substring("HKEY_USERS\\".Length);
                            Registry.Users.DeleteSubKeyTree(sub);
                        }
                        else
                        {
                            Registry.LocalMachine.DeleteSubKeyTree(app.KeyPath);
                            Registry.CurrentUser.DeleteSubKeyTree(app.KeyPath);
                        }
                        if (logger != null) logger(string.Format("   - Registro huérfano limpiado: {0}", app.KeyPath));
                    } catch {}
                }

                // Eliminación de carpeta física de instalación
                if (!string.IsNullOrEmpty(app.InstallLocation) && Directory.Exists(app.InstallLocation))
                {
                    try {
                        Directory.Delete(app.InstallLocation, true);
                        if (logger != null) logger(string.Format("   - Carpeta residual eliminada: {0}", app.InstallLocation));
                    } catch {}
                }
            }

            // 2. Limpieza forzada de directorios residuales en C:\Users\*
            try {
                if (Directory.Exists(@"C:\Users"))
                {
                    foreach (string userDir in Directory.GetDirectories(@"C:\Users"))
                    {
                        string uName = Path.GetFileName(userDir);
                        if (uName.Equals("Public", StringComparison.OrdinalIgnoreCase) || uName.Equals("Default", StringComparison.OrdinalIgnoreCase) || uName.Equals("All Users", StringComparison.OrdinalIgnoreCase)) continue;

                        string[] targetPaths = new string[] {
                            Path.Combine(userDir, @"AppData\Local\Roblox"),
                            Path.Combine(userDir, @"AppData\Local\Programs\Roblox"),
                            Path.Combine(userDir, @"AppData\Local\Steam"),
                            Path.Combine(userDir, @"AppData\Roaming\Steam"),
                            Path.Combine(userDir, @"AppData\Roaming\uTorrent"),
                            Path.Combine(userDir, @"AppData\Local\uTorrent"),
                            Path.Combine(userDir, @"AppData\Roaming\BitTorrent"),
                            Path.Combine(userDir, @"AppData\Roaming\.minecraft"),
                            Path.Combine(userDir, @"AppData\Local\Mozilla"),
                            Path.Combine(userDir, @"AppData\Roaming\Mozilla"),
                            Path.Combine(userDir, @"AppData\Local\Opera Software"),
                            Path.Combine(userDir, @"AppData\Roaming\Opera Software"),
                            Path.Combine(userDir, @"AppData\Local\BraveSoftware")
                        };

                        foreach (string path in targetPaths)
                        {
                            if (Directory.Exists(path))
                            {
                                try {
                                    Directory.Delete(path, true);
                                    if (logger != null) logger(string.Format("   - Limpieza de carpeta residual en {0}", path));
                                } catch {}
                            }
                        }

                        // Limpiar accesos directos no autorizados en escritorio de usuario
                        string desk = Path.Combine(userDir, "Desktop");
                        if (Directory.Exists(desk))
                        {
                            foreach (string lnk in Directory.GetFiles(desk, "*.lnk"))
                            {
                                string lnkName = Path.GetFileName(lnk).ToLower();
                                if (lnkName.Contains("roblox") || lnkName.Contains("steam") || lnkName.Contains("minecraft") ||
                                    lnkName.Contains("torrent") || lnkName.Contains("firefox") || lnkName.Contains("opera") ||
                                    lnkName.Contains("brave") || lnkName.Contains("vivaldi") || lnkName.Contains("tor browser") ||
                                    lnkName.Contains("avast") || lnkName.Contains("avg") || lnkName.Contains("ccleaner"))
                                {
                                    try {
                                        File.Delete(lnk);
                                        if (logger != null) logger(string.Format("   - Acceso directo no autorizado eliminado: {0}", Path.GetFileName(lnk)));
                                    } catch {}
                                }
                            }
                        }
                    }
                }
            } catch {}

            KillProhibitedProcesses();

            if (logger != null) logger("--> Desinstalacion, limpieza de Registro y desinfeccion finalizada con éxito.");
        }

        public static void UninstallProhibitedSoftware(Action<string> logger = null)
        {
            List<ProhibitedAppInfo> allProhibited = ScanProhibitedSoftware();
            UninstallSpecificSoftware(allProhibited, logger);
        }

        public static List<string> KillProhibitedProcesses()
        {
            List<string> detected = new List<string>();
            List<string> procs = new List<string>() {
                "RobloxPlayerBeta", "RobloxStudioBeta", "RobloxPlayerLauncher",
                "Steam", "SteamService", "steamwebhelper", "MinecraftLauncher", "EpicGamesLauncher",
                "uTorrent", "BitTorrent", "qBittorrent", "Ares", "PopcornTime", "Stremio",
                "firefox", "opera", "operagx", "brave", "vivaldi", "tor", "yandex", "ucbrowser",
                "soffice.bin", "soffice", "wps", "wpscloud", "wpscenter", "onlyoffice", "abiword", "foxitreader",
                "AvastUI", "AVGUI", "ccleaner", "mcshield", "bdagent",
                "HD-Player", "bluestacks", "dnplayer", "Nox", "MEmu", "CheatEngine", "StumbleGuys", "TLauncher",
                "Discord", "Telegram", "WhatsApp", "AnyDesk", "TeamViewer", "Parsec",
                "Lively", "LivelyUI", "livelywp", "wallpaper32", "wallpaper64", "wallpaper", "webwallpaper32", "wallpaper32_vulkan", "wallpaper64_vulkan", "Rainmeter", "DeskScapes", "BingWallpaperApp", "Plastuer", "AutoDarkModeApp", "TranslucentTB", "Chameleon"
            };

            foreach (string cr in LoadCustomProhibitedRules())
            {
                string procName = cr.Replace(".exe", "").Trim();
                if (!procs.Exists(p => p.Equals(procName, StringComparison.OrdinalIgnoreCase)))
                {
                    procs.Add(procName);
                }
            }

            foreach (string name in procs)
            {
                try {
                    Process[] running = Process.GetProcessesByName(name);
                    if (running.Length > 0)
                    {
                        detected.Add(name);
                        foreach (Process p in running)
                        {
                            try { p.Kill(); } catch {}
                        }
                    }
                } catch {}
            }
            return detected;
        }

        public static List<string> GetSoftwareInventory()
        {
            List<string> apps = new List<string>();
            try {
                List<ProhibitedAppInfo> all = GetAllInstalledApplications();
                foreach (var a in all)
                {
                    if (!string.IsNullOrWhiteSpace(a.Name) && !apps.Contains(a.Name))
                    {
                        apps.Add(a.Name);
                    }
                }
            } catch {}
            return apps;
        }

        public static void CreateShortcuts()
        {
            try {
                string chromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
                if (!File.Exists(chromePath)) chromePath = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";

                string publicDesk = @"C:\Users\Public\Desktop";
                if (!Directory.Exists(publicDesk)) Directory.CreateDirectory(publicDesk);

                CreateLnk(Path.Combine(publicDesk, "Plataforma DIA.lnk"), chromePath, "https://dia.agenciaeducacion.cl/login");
                CreateLnk(Path.Combine(publicDesk, "UMaximo.lnk"), chromePath, "https://www.umaximo.com/");
            } catch {}
        }

        public static void RemoveShortcuts()
        {
            try {
                string publicDesk = @"C:\Users\Public\Desktop";
                string diaLnk = Path.Combine(publicDesk, "Plataforma DIA.lnk");
                string umaxLnk = Path.Combine(publicDesk, "UMaximo.lnk");
                if (File.Exists(diaLnk)) File.Delete(diaLnk);
                if (File.Exists(umaxLnk)) File.Delete(umaxLnk);
            } catch {}
        }

        public static void EnforceDefaultApplications(Action<string> logger = null)
        {
            try {
                if (logger != null) logger("--> Asignando aplicaciones predeterminadas (MS Office & Google Chrome)...");

                // 1. Google Chrome como Navegador Predeterminado (HTTP, HTTPS, HTML)
                try {
                    string chromeProgId = "ChromeHTML";
                    string chromeExe = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
                    if (!File.Exists(chromeExe)) chromeExe = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";

                    if (File.Exists(chromeExe))
                    {
                        string[] protocols = new string[] { "http", "https" };
                        foreach (string proto in protocols)
                        {
                            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\" + proto + @"\UserChoice"))
                            {
                                if (k != null) k.SetValue("ProgId", chromeProgId, RegistryValueKind.String);
                            }
                        }

                        string[] webExts = new string[] { ".html", ".htm" };
                        foreach (string ext in webExts)
                        {
                            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + ext + @"\UserChoice"))
                            {
                                if (k != null) k.SetValue("ProgId", chromeProgId, RegistryValueKind.String);
                            }
                        }
                    }
                } catch {}

                // 2. Asociaciones de MS Office (.docx, .doc, .xlsx, .xls, .pptx, .ppt, .rtf, .csv)
                try {
                    Dictionary<string, string> officeAssocs = new Dictionary<string, string>() {
                        { ".docx", "Word.Document.12" },
                        { ".doc", "Word.Document.8" },
                        { ".rtf", "Word.Document.8" },
                        { ".xlsx", "Excel.Sheet.12" },
                        { ".xls", "Excel.Sheet.8" },
                        { ".csv", "Excel.CSV" },
                        { ".pptx", "PowerPoint.Show.12" },
                        { ".ppt", "PowerPoint.Show.8" }
                    };

                    foreach (KeyValuePair<string, string> kv in officeAssocs)
                    {
                        string ext = kv.Key;
                        string progId = kv.Value;
                        try {
                            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + ext + @"\UserChoice"))
                            {
                                if (k != null) k.SetValue("ProgId", progId, RegistryValueKind.String);
                            }
                        } catch {}
                    }
                } catch {}

                // 3. Fallback en nivel de sistema con assoc y ftype
                try {
                    string cmdAssoc = 
                        "assoc .docx=Word.Document.12 & assoc .doc=Word.Document.8 & assoc .rtf=Word.Document.8 & " +
                        "assoc .xlsx=Excel.Sheet.12 & assoc .xls=Excel.Sheet.8 & assoc .csv=Excel.CSV & " +
                        "assoc .pptx=PowerPoint.Show.12 & assoc .ppt=PowerPoint.Show.8 & " +
                        "assoc .html=ChromeHTML & assoc .htm=ChromeHTML";
                    
                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + cmdAssoc + "\"");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process p = Process.Start(psi);
                    if (p != null) p.WaitForExit(3000);
                } catch {}

                if (logger != null) logger("--> Aplicaciones por defecto (Office y Chrome) forzadas correctamente.");
            } catch (Exception ex) {
                if (logger != null) logger("   [!] Error al forzar apps por defecto: " + ex.Message);
            }
        }



        private static void CreateLnk(string lnkPath, string target, string args)
        {
            try {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                shortcut.TargetPath = target;
                shortcut.Arguments = args;
                shortcut.IconLocation = target + ",0";
                shortcut.Save();
            } catch {}
        }

        public static void ResetBrowserUserData(Action<string> logger = null)
        {
            try {
                if (logger != null) logger("Cerrando procesos de Chrome, Edge y servicios de actualización...");
                string[] procNames = new string[] { "chrome", "msedge", "GoogleUpdate", "GoogleCrashHandler" };
                foreach (string pName in procNames)
                {
                    try {
                        foreach (Process p in Process.GetProcessesByName(pName))
                        {
                            p.Kill();
                        }
                    } catch {}
                }
                Thread.Sleep(1500);

                if (logger != null) logger("Buscando y eliminando carpetas 'User Data' en C:\\Users...");
                string systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
                string usersDir = Path.Combine(systemDrive, "Users");

                if (Directory.Exists(usersDir))
                {
                    foreach (string userFolder in Directory.GetDirectories(usersDir))
                    {
                        string chromeUserData = Path.Combine(userFolder, @"AppData\Local\Google\Chrome\User Data");
                        if (Directory.Exists(chromeUserData))
                        {
                            try {
                                Directory.Delete(chromeUserData, true);
                                if (logger != null) logger("   - User Data de Chrome eliminado en: " + Path.GetFileName(userFolder));
                            } catch {}
                        }

                        string edgeUserData = Path.Combine(userFolder, @"AppData\Local\Microsoft\Edge\User Data");
                        if (Directory.Exists(edgeUserData))
                        {
                            try {
                                Directory.Delete(edgeUserData, true);
                                if (logger != null) logger("   - User Data de Edge eliminado en: " + Path.GetFileName(userFolder));
                            } catch {}
                        }
                    }
                }
                if (logger != null) logger("Reset Radical de perfiles de navegadores completado.");
            } catch (Exception ex) {
                if (logger != null) logger("Aviso en reset de navegadores: " + ex.Message);
            }
        }

        public static void CleanDownloadsAndDesktop(Action<string> logger = null)
        {
            try {
                if (logger != null) logger("Iniciando limpieza de Descargas y accesos del Escritorio...");
                string systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
                string usersDir = Path.Combine(systemDrive, "Users");

                string[] targetExts = new string[] { ".exe", ".msi", ".bat", ".cmd", ".ps1", ".zip", ".rar", ".7z", ".iso" };

                if (Directory.Exists(usersDir))
                {
                    foreach (string userFolder in Directory.GetDirectories(usersDir))
                    {
                        // 1. Clean Downloads
                        string downloadsDir = Path.Combine(userFolder, "Downloads");
                        if (Directory.Exists(downloadsDir))
                        {
                            try {
                                DirectoryInfo dInfo = new DirectoryInfo(downloadsDir);
                                foreach (FileInfo file in dInfo.GetFiles())
                                {
                                    string ext = file.Extension.ToLower();
                                    if (Array.Exists(targetExts, e => e == ext))
                                    {
                                        try {
                                            file.Delete();
                                            if (logger != null) logger("   - Eliminado de Descargas (" + Path.GetFileName(userFolder) + "): " + file.Name);
                                        } catch {}
                                    }
                                }
                            } catch {}
                        }

                        // 2. Clean Desktop clutter / game shortcuts
                        string desktopDir = Path.Combine(userFolder, "Desktop");
                        if (Directory.Exists(desktopDir))
                        {
                            try {
                                DirectoryInfo dInfo = new DirectoryInfo(desktopDir);
                                foreach (FileInfo file in dInfo.GetFiles("*.lnk"))
                                {
                                    string fName = file.Name.ToLower();
                                    if (fName.Contains("roblox") || fName.Contains("minecraft") || fName.Contains("steam"))
                                    {
                                        try {
                                            file.Delete();
                                            if (logger != null) logger("   - Acceso basura eliminado (" + Path.GetFileName(userFolder) + "): " + file.Name);
                                        } catch {}
                                    }
                                }
                            } catch {}
                        }
                    }
                }

                // Public Desktop
                string publicDesk = @"C:\Users\Public\Desktop";
                if (Directory.Exists(publicDesk))
                {
                    try {
                        DirectoryInfo dInfo = new DirectoryInfo(publicDesk);
                        foreach (FileInfo file in dInfo.GetFiles("*.lnk"))
                        {
                            string fName = file.Name.ToLower();
                            if (fName.Contains("roblox") || fName.Contains("minecraft") || fName.Contains("steam"))
                            {
                                try { file.Delete(); } catch {}
                            }
                        }
                    } catch {}
                }

                if (logger != null) logger("Limpieza de Descargas y Escritorio finalizada.");
            } catch (Exception ex) {
                if (logger != null) logger("Aviso en limpieza de Descargas: " + ex.Message);
            }
        }

        public static void PurgeRobloxMinecraftAppData(Action<string> logger = null)
        {
            try {
                if (logger != null) logger("Cerrando procesos y eliminando carpetas residuales de Roblox/Minecraft...");
                string[] procNames = new string[] { "RobloxPlayerBeta", "RobloxStudio", "Minecraft", "javaw", "MinecraftLauncher" };
                foreach (string pName in procNames)
                {
                    try {
                        foreach (Process p in Process.GetProcessesByName(pName)) p.Kill();
                    } catch {}
                }
                Thread.Sleep(1000);

                string systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
                string usersDir = Path.Combine(systemDrive, "Users");

                if (Directory.Exists(usersDir))
                {
                    foreach (string userFolder in Directory.GetDirectories(usersDir))
                    {
                        string[] relPaths = new string[] {
                            @"AppData\Local\Roblox",
                            @"AppData\LocalLow\RbxLogs",
                            @"AppData\Roaming\Roblox",
                            @"AppData\Roaming\.minecraft",
                            @"AppData\Local\Programs\Minecraft Launcher"
                        };

                        foreach (string rel in relPaths)
                        {
                            string target = Path.Combine(userFolder, rel);
                            if (Directory.Exists(target))
                            {
                                try {
                                    Directory.Delete(target, true);
                                    if (logger != null) logger("   - Carpetas residuales eliminadas: " + rel + " (" + Path.GetFileName(userFolder) + ")");
                                } catch {}
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                if (logger != null) logger("Aviso en purga Roblox/Minecraft: " + ex.Message);
            }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }

        public static void SendTelemetry(string status, List<string> detected, List<string> inventory, Action<string> onCommandReceived)
        {
            try {
                string localIp = "127.0.0.1";
                try {
                    IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (IPAddress ip in host.AddressList)
                    {
                        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !ip.ToString().StartsWith("127."))
                        {
                            localIp = ip.ToString();
                            break;
                        }
                    }
                } catch {}

                StringBuilder sb = new StringBuilder();
                sb.Append("{");
                sb.AppendFormat("\"api_token\":\"{0}\",", API_SECRET_TOKEN);
                sb.AppendFormat("\"hostname\":\"{0}\",", EscapeJson(Environment.MachineName));
                sb.AppendFormat("\"username\":\"{0}\",", EscapeJson(Environment.UserName));
                sb.AppendFormat("\"ip\":\"{0}\",", EscapeJson(localIp));
                sb.AppendFormat("\"status\":\"{0}\",", EscapeJson(status));
                sb.AppendFormat("\"script_version\":\"{0}\",", EscapeJson(CURRENT_VERSION));

                sb.Append("\"detected_apps\":[");
                if (detected != null)
                {
                    for (int i = 0; i < detected.Count; i++)
                    {
                        sb.AppendFormat("\"{0}\"{1}", EscapeJson(detected[i]), (i < detected.Count - 1) ? "," : "");
                    }
                }
                sb.Append("],");

                sb.Append("\"full_inventory\":[");
                if (inventory != null)
                {
                    for (int i = 0; i < inventory.Count; i++)
                    {
                        sb.AppendFormat("\"{0}\"{1}", EscapeJson(inventory[i]), (i < inventory.Count - 1) ? "," : "");
                    }
                }
                sb.Append("],");

                sb.AppendFormat("\"inventory_count\":{0}", inventory != null ? inventory.Count : 0);
                sb.Append("}");

                byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://sistemas.cbmw.cl/api/lab/reporte.php");
                req.Method = "POST";
                req.Headers["X-API-Token"] = API_SECRET_TOKEN;
                req.ContentType = "application/json";
                req.ContentLength = data.Length;
                req.Timeout = 5000;

                using (Stream stream = req.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                    {
                        string respText = sr.ReadToEnd();
                        if (respText.Contains("\"command\":\"SHUTDOWN\""))
                        {
                            if (onCommandReceived != null)
                            {
                                try { onCommandReceived("SHUTDOWN"); } catch {}
                            }

                            Thread.Sleep(2000);

                            try {
                                ProcessStartInfo psi = new ProcessStartInfo("shutdown.exe", "/s /f /t 1 /c \"Apagado remoto solicitado desde Dashboard Web\"");
                                psi.CreateNoWindow = true;
                                psi.UseShellExecute = false;
                                Process.Start(psi);

                                ProcessStartInfo psi2 = new ProcessStartInfo("powershell.exe", "-Command \"Stop-Computer -Force\"");
                                psi2.CreateNoWindow = true;
                                psi2.UseShellExecute = false;
                                Process.Start(psi2);
                            } catch {}
                        }
                        else if (respText.Contains("\"command\":\"UNINSTALL\""))
                        {
                            if (onCommandReceived != null)
                            {
                                try { onCommandReceived("UNINSTALL"); } catch {}
                            }
                            ThreadPool.QueueUserWorkItem(st => {
                                try {
                                    UninstallProhibitedSoftware(null);
                                    KillProhibitedProcesses();
                                } catch {}
                            });
                        }
                    }
                }
            } catch {}
        }

        public static string ExtractJsonValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";
            try {
                int keyIdx = json.IndexOf("\"" + key + "\"");
                if (keyIdx == -1) keyIdx = json.IndexOf("'" + key + "'");
                if (keyIdx == -1) keyIdx = json.IndexOf(key);
                if (keyIdx == -1) return "";

                int colonIdx = json.IndexOf(':', keyIdx);
                if (colonIdx == -1) return "";

                int startVal = colonIdx + 1;
                while (startVal < json.Length && (json[startVal] == ' ' || json[startVal] == '\t' || json[startVal] == '\r' || json[startVal] == '\n' || json[startVal] == '"' || json[startVal] == '\''))
                {
                    startVal++;
                }

                int endVal = startVal;
                while (endVal < json.Length && json[endVal] != '"' && json[endVal] != '\'' && json[endVal] != ',' && json[endVal] != '}' && json[endVal] != '\r' && json[endVal] != '\n')
                {
                    endVal++;
                }

                if (endVal > startVal)
                {
                    return json.Substring(startVal, endVal - startVal).Trim();
                }
            } catch {}
            return "";
        }
    }

    public class InstitutionSelectorForm : Form
    {
        private ComboBox cmbInstitutions;
        private PictureBox picPreviewFondo;
        private PictureBox picPreviewLogo;
        private Button btnApply;
        private Button btnAddNew;
        private Button btnClose;
        private Action<string> mainLogger;
        private string instRootDir = @"C:\LayvelGuard\Instituciones";

        public InstitutionSelectorForm(Action<string> logger)
        {
            this.mainLogger = logger;
            InitializeComponent();
            this.Shown += (s, e) => LoadInstitutions();
        }

        private void InitializeComponent()
        {
            this.Text = "LayvelGuard Pro - Gestor de Perfiles de Institución (Personalización)";
            this.Size = new Size(650, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(15, 23, 42);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 70;
            header.BackColor = Color.FromArgb(30, 41, 59);
            header.Padding = new Padding(15, 10, 15, 10);

            Label lblTitle = new Label();
            lblTitle.Text = "🏫 Perfiles Institucionales (Fondo de Escritorio, Bloqueo y Logo)";
            lblTitle.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(15, 12);
            lblTitle.AutoSize = true;
            header.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "Seleccione la institución para aplicar su fondo de escritorio, pantalla de bloqueo y foto de perfil.";
            lblSub.Font = new Font("Segoe UI", 8.5f);
            lblSub.ForeColor = Color.FromArgb(148, 163, 184);
            lblSub.Location = new Point(15, 38);
            lblSub.AutoSize = true;
            header.Controls.Add(lblSub);

            this.Controls.Add(header);

            Label lblSelect = new Label();
            lblSelect.Text = "Seleccionar Institución:";
            lblSelect.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblSelect.ForeColor = Color.FromArgb(241, 245, 249);
            lblSelect.Location = new Point(25, 90);
            lblSelect.AutoSize = true;
            this.Controls.Add(lblSelect);

            cmbInstitutions = new ComboBox();
            cmbInstitutions.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbInstitutions.Font = new Font("Segoe UI", 10f);
            cmbInstitutions.Location = new Point(200, 87);
            cmbInstitutions.Size = new Size(270, 28);
            cmbInstitutions.SelectedIndexChanged += (s, e) => UpdatePreviews();
            this.Controls.Add(cmbInstitutions);

            btnAddNew = new Button();
            btnAddNew.Text = "➕ Nueva...";
            btnAddNew.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnAddNew.BackColor = Color.FromArgb(59, 130, 246);
            btnAddNew.ForeColor = Color.White;
            btnAddNew.FlatStyle = FlatStyle.Flat;
            btnAddNew.FlatAppearance.BorderSize = 0;
            btnAddNew.Size = new Size(130, 28);
            btnAddNew.Location = new Point(480, 87);
            btnAddNew.Cursor = Cursors.Hand;
            btnAddNew.Click += (s, e) => AddNewInstitution();
            this.Controls.Add(btnAddNew);

            // Previews Panel
            Panel previewBox = new Panel();
            previewBox.Location = new Point(25, 130);
            previewBox.Size = new Size(585, 230);
            previewBox.BackColor = Color.FromArgb(30, 41, 59);

            Label lblPrevFondo = new Label();
            lblPrevFondo.Text = "Fondo de Pantalla / Bloqueo:";
            lblPrevFondo.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblPrevFondo.ForeColor = Color.FromArgb(148, 163, 184);
            lblPrevFondo.Location = new Point(15, 10);
            lblPrevFondo.AutoSize = true;
            previewBox.Controls.Add(lblPrevFondo);

            picPreviewFondo = new PictureBox();
            picPreviewFondo.Location = new Point(15, 35);
            picPreviewFondo.Size = new Size(340, 180);
            picPreviewFondo.SizeMode = PictureBoxSizeMode.Zoom;
            picPreviewFondo.BackColor = Color.FromArgb(15, 23, 42);
            previewBox.Controls.Add(picPreviewFondo);

            Label lblPrevLogo = new Label();
            lblPrevLogo.Text = "Foto Perfil Usuario:";
            lblPrevLogo.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblPrevLogo.ForeColor = Color.FromArgb(148, 163, 184);
            lblPrevLogo.Location = new Point(380, 10);
            lblPrevLogo.AutoSize = true;
            previewBox.Controls.Add(lblPrevLogo);

            picPreviewLogo = new PictureBox();
            picPreviewLogo.Location = new Point(380, 35);
            picPreviewLogo.Size = new Size(180, 180);
            picPreviewLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picPreviewLogo.BackColor = Color.FromArgb(15, 23, 42);
            previewBox.Controls.Add(picPreviewLogo);

            this.Controls.Add(previewBox);

            btnApply = new Button();
            btnApply.Text = "🖼️ Aplicar Perfil Institucional";
            btnApply.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnApply.BackColor = Color.FromArgb(16, 185, 129);
            btnApply.ForeColor = Color.White;
            btnApply.FlatStyle = FlatStyle.Flat;
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.Size = new Size(270, 40);
            btnApply.Location = new Point(25, 375);
            btnApply.Cursor = Cursors.Hand;
            btnApply.Click += (s, e) => ApplySelectedInstitution();
            this.Controls.Add(btnApply);

            btnClose = new Button();
            btnClose.Text = "Cerrar";
            btnClose.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnClose.BackColor = Color.FromArgb(71, 85, 105);
            btnClose.ForeColor = Color.White;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Size = new Size(120, 40);
            btnClose.Location = new Point(490, 375);
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
        }

        private void LoadInstitutions()
        {
            try {
                Program.EnsureDefaultInstitutionsExist();

                cmbInstitutions.Items.Clear();
                string[] dirs = Directory.GetDirectories(instRootDir);
                foreach (string d in dirs)
                {
                    cmbInstitutions.Items.Add(Path.GetFileName(d));
                }

                if (cmbInstitutions.Items.Count > 0)
                {
                    int cbmwIdx = cmbInstitutions.FindStringExact("CBMW");
                    cmbInstitutions.SelectedIndex = (cbmwIdx != -1) ? cbmwIdx : 0;
                }
            } catch {}
        }

        private void UpdatePreviews()
        {
            if (cmbInstitutions.SelectedItem == null) return;
            string instName = cmbInstitutions.SelectedItem.ToString();
            string instPath = Path.Combine(instRootDir, instName);

            string fondoPath = Path.Combine(instPath, "fondo.png");
            if (!File.Exists(fondoPath)) fondoPath = Path.Combine(instPath, "fondo.jpg");

            string logoPath = Path.Combine(instPath, "logo.png");
            if (!File.Exists(logoPath)) logoPath = Path.Combine(instPath, "logo.jpg");

            if (File.Exists(fondoPath))
            {
                try {
                    using (var stream = new FileStream(fondoPath, FileMode.Open, FileAccess.Read))
                    {
                        picPreviewFondo.Image = Image.FromStream(stream);
                    }
                } catch { picPreviewFondo.Image = null; }
            }
            else picPreviewFondo.Image = null;

            if (File.Exists(logoPath))
            {
                try {
                    using (var stream = new FileStream(logoPath, FileMode.Open, FileAccess.Read))
                    {
                        picPreviewLogo.Image = Image.FromStream(stream);
                    }
                } catch { picPreviewLogo.Image = null; }
            }
            else picPreviewLogo.Image = null;
        }

        private void AddNewInstitution()
        {
            string name = PromptInput("Nueva Institución", "Ingrese el nombre de la nueva institución o colegio:");
            if (string.IsNullOrWhiteSpace(name)) return;

            string newDir = Path.Combine(instRootDir, name);
            if (!Directory.Exists(newDir)) Directory.CreateDirectory(newDir);

            MessageBox.Show("Seleccione la imagen para el Fondo de Pantalla / Bloqueo (fondo.png).", "Fondo de Pantalla", MessageBoxButtons.OK, MessageBoxIcon.Information);
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Seleccionar Fondo para " + name;
                dlg.Filter = "Archivos de Imagen (*.png;*.jpg)|*.png;*.jpg";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    File.Copy(dlg.FileName, Path.Combine(newDir, "fondo.png"), true);
                }
            }

            MessageBox.Show("Seleccione la imagen para el Logo / Perfil de Usuario (logo.png).", "Foto de Perfil", MessageBoxButtons.OK, MessageBoxIcon.Information);
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Seleccionar Logo/Perfil para " + name;
                dlg.Filter = "Archivos de Imagen (*.png;*.jpg)|*.png;*.jpg";
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    File.Copy(dlg.FileName, Path.Combine(newDir, "logo.png"), true);
                }
            }

            LoadInstitutions();
            int idx = cmbInstitutions.FindStringExact(name);
            if (idx != -1) cmbInstitutions.SelectedIndex = idx;
        }

        private string PromptInput(string title, string prompt)
        {
            Form promptForm = new Form();
            promptForm.Width = 420;
            promptForm.Height = 180;
            promptForm.Text = title;
            promptForm.StartPosition = FormStartPosition.CenterParent;
            promptForm.BackColor = Color.FromArgb(15, 23, 42);
            promptForm.FormBorderStyle = FormBorderStyle.FixedDialog;

            Label lbl = new Label() { Left = 20, Top = 15, Text = prompt, AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 9.5f) };
            TextBox tb = new TextBox() { Left = 20, Top = 45, Width = 360, Font = new Font("Segoe UI", 10f) };
            Button confirm = new Button() { Text = "Aceptar", Left = 240, Width = 70, Top = 85, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button cancel = new Button() { Text = "Cancelar", Left = 315, Width = 65, Top = 85, DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(71, 85, 105), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };

            promptForm.Controls.Add(lbl);
            promptForm.Controls.Add(tb);
            promptForm.Controls.Add(confirm);
            promptForm.Controls.Add(cancel);
            promptForm.AcceptButton = confirm;

            return promptForm.ShowDialog() == DialogResult.OK ? tb.Text.Trim() : "";
        }

        private void ApplySelectedInstitution()
        {
            if (cmbInstitutions.SelectedItem == null) return;
            string instName = cmbInstitutions.SelectedItem.ToString();
            string instPath = Path.Combine(instRootDir, instName);

            string fondoPath = Path.Combine(instPath, "fondo.png");
            if (!File.Exists(fondoPath)) fondoPath = Path.Combine(instPath, "fondo.jpg");

            string logoPath = Path.Combine(instPath, "logo.png");
            if (!File.Exists(logoPath)) logoPath = Path.Combine(instPath, "logo.jpg");

            btnApply.Enabled = false;
            ThreadPool.QueueUserWorkItem(state => {
                if (File.Exists(fondoPath)) Program.SetWallpapers(fondoPath);
                if (File.Exists(logoPath)) Program.SetUserProfilePicture(logoPath);

                try {
                    this.Invoke(new Action(() => {
                        btnApply.Enabled = true;
                        if (mainLogger != null) mainLogger(string.Format("Perfil Institucional '{0}' aplicado con éxito (Fondo de escritorio, bloqueo y foto de usuario).", instName));
                        MessageBox.Show(string.Format("Perfil de '{0}' aplicado con éxito.\r\nFondo de pantalla, bloqueo y foto de perfil actualizados.", instName), "LayvelGuard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Close();
                    }));
                } catch {}
            });
        }
    }

    public class UninstallManagerForm : Form
    {
        private CheckedListBox chkListApps;
        private ComboBox cmbFilterMode;
        private Button btnUninstall, btnRescan, btnClose;
        private Label lblInfo;
        private List<ProhibitedAppInfo> scannedApps;
        private Action<string> mainLogger;

        public UninstallManagerForm(Action<string> logger)
        {
            this.mainLogger = logger;
            InitializeComponent();
            this.Shown += (s, e) => RunScan();
        }

        private void SafeInvoke(Action action)
        {
            if (this.IsDisposed || this.Disposing) return;
            try {
                if (this.InvokeRequired)
                {
                    if (this.IsHandleCreated)
                    {
                        this.Invoke(action);
                    }
                    else
                    {
                        this.HandleCreated += (s, e) => {
                            try { if (this.InvokeRequired) this.Invoke(action); else action(); } catch {}
                        };
                    }
                }
                else
                {
                    action();
                }
            } catch {}
        }

        private void InitializeComponent()
        {
            this.Text = "LayvelGuard Pro - Selector y Desinstalador de Aplicaciones (v" + Program.CURRENT_VERSION + ")";
            this.Size = new Size(720, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(15, 23, 42); // Slate 900
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 70;
            header.BackColor = Color.FromArgb(30, 41, 59);
            header.Padding = new Padding(15, 10, 15, 10);

            Label lblTitle = new Label();
            lblTitle.Text = "🔍 LayvelGuard - Escáner de Inventario 100% e Desinstalador (v" + Program.CURRENT_VERSION + ")";
            lblTitle.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(15, 12);
            lblTitle.AutoSize = true;
            header.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "Permitidos solo Chrome y Edge (Browsers) y Defender (Antivirus). Demás no autorizados vienen marcados.";
            lblSub.Font = new Font("Segoe UI", 8.5f);
            lblSub.ForeColor = Color.FromArgb(148, 163, 184);
            lblSub.Location = new Point(15, 38);
            lblSub.AutoSize = true;
            header.Controls.Add(lblSub);

            this.Controls.Add(header);

            Label lblMode = new Label();
            lblMode.Text = "Modo Vista:";
            lblMode.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblMode.ForeColor = Color.FromArgb(241, 245, 249);
            lblMode.Location = new Point(20, 85);
            lblMode.AutoSize = true;
            this.Controls.Add(lblMode);

            cmbFilterMode = new ComboBox();
            cmbFilterMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbFilterMode.Font = new Font("Segoe UI", 9.5f);
            cmbFilterMode.Location = new Point(120, 82);
            cmbFilterMode.Size = new Size(565, 28);
            cmbFilterMode.Items.Add("🚫 Aplicaciones Prohibidas Detectadas");
            cmbFilterMode.Items.Add("🌐 Todo el Inventario del PC (100% de Software Instalado)");
            cmbFilterMode.SelectedIndex = 0;
            cmbFilterMode.SelectedIndexChanged += (s, e) => RunScan();
            this.Controls.Add(cmbFilterMode);

            chkListApps = new CheckedListBox();
            chkListApps.Location = new Point(20, 118);
            chkListApps.Size = new Size(665, 300);
            chkListApps.BackColor = Color.FromArgb(9, 13, 22);
            chkListApps.ForeColor = Color.FromArgb(241, 245, 249);
            chkListApps.Font = new Font("Segoe UI", 9.5f);
            chkListApps.CheckOnClick = true;
            this.Controls.Add(chkListApps);

            lblInfo = new Label();
            lblInfo.Text = "Escaneando aplicaciones...";
            lblInfo.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblInfo.ForeColor = Color.FromArgb(56, 189, 248);
            lblInfo.Location = new Point(20, 425);
            lblInfo.AutoSize = true;
            this.Controls.Add(lblInfo);

            btnRescan = new Button();
            btnRescan.Text = "🔄 Re-Escanear";
            btnRescan.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnRescan.BackColor = Color.FromArgb(59, 130, 246);
            btnRescan.ForeColor = Color.White;
            btnRescan.FlatStyle = FlatStyle.Flat;
            btnRescan.FlatAppearance.BorderSize = 0;
            btnRescan.Size = new Size(130, 38);
            btnRescan.Location = new Point(20, 460);
            btnRescan.Cursor = Cursors.Hand;
            btnRescan.Click += (s, e) => RunScan();
            this.Controls.Add(btnRescan);

            btnUninstall = new Button();
            btnUninstall.Text = "🗑️ Desinstalar Seleccionadas";
            btnUninstall.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnUninstall.BackColor = Color.FromArgb(225, 29, 72);
            btnUninstall.ForeColor = Color.White;
            btnUninstall.FlatStyle = FlatStyle.Flat;
            btnUninstall.FlatAppearance.BorderSize = 0;
            btnUninstall.Size = new Size(230, 38);
            btnUninstall.Location = new Point(160, 460);
            btnUninstall.Cursor = Cursors.Hand;
            btnUninstall.Click += (s, e) => ExecuteSelectedUninstall();
            this.Controls.Add(btnUninstall);

            Button btnAddRule = new Button();
            btnAddRule.Text = "➕ Marcar Prohibida";
            btnAddRule.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnAddRule.BackColor = Color.FromArgb(16, 185, 129);
            btnAddRule.ForeColor = Color.White;
            btnAddRule.FlatStyle = FlatStyle.Flat;
            btnAddRule.FlatAppearance.BorderSize = 0;
            btnAddRule.Size = new Size(175, 38);
            btnAddRule.Location = new Point(400, 460);
            btnAddRule.Cursor = Cursors.Hand;
            btnAddRule.Click += (s, e) => OpenAddProhibitedDialog();
            this.Controls.Add(btnAddRule);

            btnClose = new Button();
            btnClose.Text = "Cerrar";
            btnClose.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnClose.BackColor = Color.FromArgb(71, 85, 105);
            btnClose.ForeColor = Color.White;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Size = new Size(100, 38);
            btnClose.Location = new Point(585, 460);
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
        }

        private void OpenAddProhibitedDialog()
        {
            Form dlg = new Form();
            dlg.Width = 520;
            dlg.Height = 260;
            dlg.Text = "➕ Agregar Aplicación No Autorizada / Regla de Bloqueo";
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.BackColor = Color.FromArgb(15, 23, 42);
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;

            Label lblSelect = new Label() { Left = 20, Top = 15, Text = "Seleccionar de Software Instalado en el PC:", AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            ComboBox cmbApps = new ComboBox() { Left = 20, Top = 40, Width = 460, Font = new Font("Segoe UI", 9.5f), DropDownStyle = ComboBoxStyle.DropDownList };
            
            try {
                List<string> inv = Program.GetSoftwareInventory();
                inv.Sort();
                foreach (string app in inv) cmbApps.Items.Add(app);
            } catch {}
            if (cmbApps.Items.Count > 0) cmbApps.SelectedIndex = 0;

            Label lblCustom = new Label() { Left = 20, Top = 80, Text = "O escribir palabra clave/proceso personalizado (ej. discord, stumble guys):", AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184), Font = new Font("Segoe UI", 8.5f) };
            TextBox tbCustom = new TextBox() { Left = 20, Top = 105, Width = 460, Font = new Font("Segoe UI", 9.5f) };

            if (chkListApps.SelectedItem != null)
            {
                string selText = chkListApps.SelectedItem.ToString();
                selText = selText.Replace("[🚫 PROHIBIDA]", "").Replace("[🟢 INSTALADA]", "").Trim();
                tbCustom.Text = selText;
            }

            Button btnSave = new Button() { Text = "💾 Guardar Regla", Left = 240, Width = 130, Top = 160, Height = 36, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(16, 185, 129), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Cursor = Cursors.Hand };
            Button btnCancel = new Button() { Text = "Cancelar", Left = 380, Width = 100, Top = 160, Height = 36, DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(71, 85, 105), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Cursor = Cursors.Hand };

            dlg.Controls.Add(lblSelect);
            dlg.Controls.Add(cmbApps);
            dlg.Controls.Add(lblCustom);
            dlg.Controls.Add(tbCustom);
            dlg.Controls.Add(btnSave);
            dlg.Controls.Add(btnCancel);
            dlg.AcceptButton = btnSave;

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                string ruleToAdd = tbCustom.Text.Trim();
                if (string.IsNullOrWhiteSpace(ruleToAdd) && cmbApps.SelectedItem != null)
                {
                    ruleToAdd = cmbApps.SelectedItem.ToString();
                }

                if (!string.IsNullOrWhiteSpace(ruleToAdd))
                {
                    Program.SaveCustomProhibitedRule(ruleToAdd);
                    if (mainLogger != null) mainLogger("Regla prohibida registrada y guardada localmente: " + ruleToAdd);
                    MessageBox.Show("Regla prohibida guardada con éxito:\r\n\r\n" + ruleToAdd + "\r\n\r\nGuardado en C:\\LayvelGuard\\custom_prohibited.json.", "LayvelGuard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RunScan();
                }
            }
        }

        private void RunScan()
        {
            chkListApps.Items.Clear();
            bool showAll = cmbFilterMode != null && cmbFilterMode.SelectedIndex == 1;
            lblInfo.Text = showAll ? "Escaneando 100% de aplicaciones e inventario del PC..." : "Escaneando Registro de Usuarios y Disco...";
            lblInfo.ForeColor = Color.FromArgb(56, 189, 248);

            ThreadPool.QueueUserWorkItem(state => {
                scannedApps = showAll ? Program.GetAllInstalledApplications() : Program.ScanProhibitedSoftware();
                List<ProhibitedAppInfo> prohibitedOnly = Program.ScanProhibitedSoftware();

                SafeInvoke(() => {
                    chkListApps.Items.Clear();
                    int checkedCount = 0;
                    foreach (var app in scannedApps)
                    {
                        bool isProhibited = false;
                        foreach (var p in prohibitedOnly)
                        {
                            if (p.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase)) isProhibited = true;
                        }

                        if (showAll)
                        {
                            string itemText = string.Format("{0} {1}", isProhibited ? "[🚫 PROHIBIDA]" : "[🟢 INSTALADA]", app.Name);
                            chkListApps.Items.Add(itemText, isProhibited);
                            if (isProhibited) checkedCount++;
                        }
                        else
                        {
                            chkListApps.Items.Add(app.Name, true);
                            checkedCount++;
                        }
                    }

                    if (scannedApps.Count == 0)
                    {
                        lblInfo.Text = "🟢 No se encontraron aplicaciones.";
                        lblInfo.ForeColor = Color.FromArgb(74, 222, 128);
                    }
                    else
                    {
                        lblInfo.Text = string.Format(showAll ? "🌐 Inventario total: {0} apps encontradas en el PC ({1} prohibidas)." : "⚠️ Detectadas {0} aplicaciones no autorizadas ({1} marcadas).", scannedApps.Count, checkedCount);
                        lblInfo.ForeColor = showAll ? Color.FromArgb(56, 189, 248) : Color.FromArgb(251, 191, 36);
                    }
                });
            });
        }

        private void ExecuteSelectedUninstall()
        {
            if (chkListApps.CheckedItems.Count == 0)
            {
                MessageBox.Show("Por favor seleccione al menos una aplicación para desinstalar.", "LayvelGuard", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<ProhibitedAppInfo> selectedApps = new List<ProhibitedAppInfo>();
            for (int i = 0; i < chkListApps.Items.Count; i++)
            {
                if (chkListApps.GetItemChecked(i))
                {
                    selectedApps.Add(scannedApps[i]);
                }
            }

            btnUninstall.Enabled = false;
            btnRescan.Enabled = false;
            lblInfo.Text = "Ejecutando desinstalación y limpieza de Registro...";
            lblInfo.ForeColor = Color.FromArgb(56, 189, 248);

            ThreadPool.QueueUserWorkItem(state => {
                Program.UninstallSpecificSoftware(selectedApps, (msg) => {
                    SafeInvoke(() => {
                        lblInfo.Text = msg;
                        if (mainLogger != null) mainLogger(msg);
                    });
                });

                SafeInvoke(() => {
                    btnUninstall.Enabled = true;
                    btnRescan.Enabled = true;
                    MessageBox.Show("Desinstalación y limpieza de Registro completadas con éxito.", "LayvelGuard Finalizado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RunScan();
                });
            });
        }
    }

    public class MainForm : Form
    {
        private PictureBox picLogo;
        private Label lblTitle, lblSubtitle, lblStatus;
        private TextBox txtLog;
        private ProgressBar progressBar;

        private Button btnTabActions, btnTabStatus;
        private Panel panelActions, panelStatus;

        // Status items & controls
        private Label badgeBlockWeb, badgeAccounts, badgeEmailPurge, badgeWallpaper, badgeWallpaperLock, badgeService, badgeShortcuts, badgeUninstall, badgeMouse;
        private Button btnToggleBlockWeb, btnToggleAccounts, btnToggleEmailPurge, btnToggleWallpaper, btnToggleWallpaperLock, btnToggleService, btnToggleShortcuts, btnToggleUninstall, btnToggleMouse;

        private System.Windows.Forms.Timer telemetryTimer;

        public MainForm()
        {
            InitializeComponent();
            this.Shown += (s, e) => {
                CheckConnectionAsync();
                RefreshStatusBadges();
                StartTelemetryTimer();
            };
        }

        private void SafeInvoke(Action action)
        {
            if (this.IsDisposed || this.Disposing) return;
            try {
                if (this.InvokeRequired)
                {
                    if (this.IsHandleCreated)
                    {
                        this.Invoke(action);
                    }
                    else
                    {
                        this.HandleCreated += (evSender, evArgs) => {
                            try { if (this.InvokeRequired) this.Invoke(action); else action(); } catch {}
                        };
                    }
                }
                else
                {
                    action();
                }
            } catch {}
        }

        private void StartTelemetryTimer()
        {
            telemetryTimer = new System.Windows.Forms.Timer();
            telemetryTimer.Interval = 10000;
            telemetryTimer.Tick += (s, e) => CheckRemoteCommands();
            telemetryTimer.Start();
        }

        private void CheckRemoteCommands()
        {
            ThreadPool.QueueUserWorkItem(state => {
                try {
                    List<string> detectedProcs = Program.KillProhibitedProcesses();
                    List<ProhibitedAppInfo> detectedApps = Program.ScanProhibitedSoftware();
                    List<string> detected = new List<string>();

                    if (detectedProcs != null)
                    {
                        foreach (string p in detectedProcs)
                        {
                            if (!detected.Contains(p)) detected.Add(p);
                        }
                    }
                    if (detectedApps != null)
                    {
                        foreach (var a in detectedApps)
                        {
                            if (!string.IsNullOrWhiteSpace(a.Name) && !detected.Contains(a.Name)) detected.Add(a.Name);
                        }
                    }

                    List<string> inv = Program.GetSoftwareInventory();
                    Program.SendTelemetry("ONLINE", detected, inv, (cmd) => {
                        if (cmd == "SHUTDOWN")
                        {
                            Log("====================================================");
                            Log("[!] ALERTA CRITICA: RECIBIDA ORDEN DE APAGADO REMOTO");
                            Log("    Solicitado desde Dashboard Web API");
                            Log("    Ejecutando apagado forzado de Windows en 2s...");
                            Log("====================================================");
                        }
                    });
                } catch {}
            });
        }

        private void InitializeComponent()
        {
            this.Text = "LAYVELGUARD PRO - CONTROL & MAINTENANCE AGENT (v" + Program.CURRENT_VERSION + ")";
            this.Size = new Size(1060, 750);
            this.MinimumSize = new Size(1000, 700);
            this.BackColor = Color.FromArgb(15, 23, 42); // Slate 900
            this.StartPosition = FormStartPosition.CenterScreen;

            string icoPath = @"C:\LayvelGuard\layvelguard_icon.ico";
            if (File.Exists(icoPath))
            {
                try { this.Icon = new Icon(icoPath); } catch {}
            }
            else
            {
                this.Icon = SystemIcons.Shield;
            }

            // 1. Header Superior Principal (Top Dock)
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 80;
            header.BackColor = Color.FromArgb(30, 41, 59);
            header.Padding = new Padding(15, 10, 15, 10);

            picLogo = new PictureBox();
            picLogo.Size = new Size(60, 60);
            picLogo.Location = new Point(15, 10);
            picLogo.SizeMode = PictureBoxSizeMode.Zoom;
            picLogo.BackColor = Color.Transparent;
            
            string logoPath = @"C:\LayvelGuard\layvelguard_logo.png";
            if (File.Exists(logoPath))
            {
                try { picLogo.Image = Image.FromFile(logoPath); } catch {}
            }
            header.Controls.Add(picLogo);

            lblTitle = new Label();
            lblTitle.Text = "LAYVELGUARD PRO";
            lblTitle.Font = new Font("Segoe UI", 13, FontStyle.Bold);
            lblTitle.ForeColor = Color.FromArgb(248, 250, 252);
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(85, 12);
            header.Controls.Add(lblTitle);

            lblSubtitle = new Label();
            lblSubtitle.Text = "Sistema Independiente de Control, Estatus y Mantenimiento (v" + Program.CURRENT_VERSION + ")";
            lblSubtitle.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.FromArgb(148, 163, 184);
            lblSubtitle.AutoSize = true;
            lblSubtitle.Location = new Point(85, 42);
            header.Controls.Add(lblSubtitle);

            lblStatus = new Label();
            lblStatus.Text = "[..] Conectando con GitHub...";
            lblStatus.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblStatus.ForeColor = Color.FromArgb(245, 158, 11);
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(570, 25);
            header.Controls.Add(lblStatus);

            Button btnCheckUpdate = new Button();
            btnCheckUpdate.Text = "🔄 Update GitHub";
            btnCheckUpdate.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnCheckUpdate.BackColor = Color.FromArgb(59, 130, 246);
            btnCheckUpdate.ForeColor = Color.White;
            btnCheckUpdate.FlatStyle = FlatStyle.Flat;
            btnCheckUpdate.FlatAppearance.BorderSize = 0;
            btnCheckUpdate.Size = new Size(140, 34);
            btnCheckUpdate.Location = new Point(870, 22);
            btnCheckUpdate.Cursor = Cursors.Hand;
            btnCheckUpdate.Click += (s, e) => PerformManualUpdateCheck();
            header.Controls.Add(btnCheckUpdate);

            // 2. Navigation Bar
            Panel navBar = new Panel();
            navBar.Dock = DockStyle.Top;
            navBar.Height = 42;
            navBar.BackColor = Color.FromArgb(15, 23, 42);
            navBar.Padding = new Padding(20, 3, 20, 0);

            btnTabActions = new Button();
            btnTabActions.Text = "🛠️ Acciones de Mantenimiento";
            btnTabActions.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnTabActions.ForeColor = Color.White;
            btnTabActions.BackColor = Color.FromArgb(30, 41, 59);
            btnTabActions.FlatStyle = FlatStyle.Flat;
            btnTabActions.FlatAppearance.BorderSize = 0;
            btnTabActions.Size = new Size(250, 36);
            btnTabActions.Location = new Point(20, 3);
            btnTabActions.Cursor = Cursors.Hand;
            btnTabActions.Click += (s, e) => SwitchTab(true);
            navBar.Controls.Add(btnTabActions);

            btnTabStatus = new Button();
            btnTabStatus.Text = "📊 Estatus del Equipo & Switches";
            btnTabStatus.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnTabStatus.ForeColor = Color.FromArgb(148, 163, 184);
            btnTabStatus.BackColor = Color.FromArgb(15, 23, 42);
            btnTabStatus.FlatStyle = FlatStyle.Flat;
            btnTabStatus.FlatAppearance.BorderSize = 0;
            btnTabStatus.Size = new Size(250, 36);
            btnTabStatus.Location = new Point(280, 3);
            btnTabStatus.Cursor = Cursors.Hand;
            btnTabStatus.Click += (s, e) => SwitchTab(false);
            navBar.Controls.Add(btnTabStatus);

            // 3. Tab Panel 1: Actions & Log Console
            panelActions = new Panel();
            panelActions.Dock = DockStyle.Fill;
            panelActions.BackColor = Color.FromArgb(15, 23, 42);
            panelActions.Padding = new Padding(20, 10, 20, 10);

            Panel leftPanel = new Panel();
            leftPanel.Location = new Point(20, 10);
            leftPanel.Size = new Size(365, 550);
            leftPanel.BackColor = Color.Transparent;
            leftPanel.AutoScroll = true;

            Button btnFull = CreateActionButton("[!] MANTENIMIENTO AUTOMATICO", Color.FromArgb(16, 185, 129), 0);
            btnFull.Click += (s, e) => RunAsync(DoFullMaintenance);
            leftPanel.Controls.Add(btnFull);

            Button btnStatusNav = CreateActionButton("[1] Ver Estatus y Switches On/Off", Color.FromArgb(59, 130, 246), 35);
            btnStatusNav.Click += (s, e) => SwitchTab(false);
            leftPanel.Controls.Add(btnStatusNav);

            Button btnStartup = CreateActionButton("[2] Servicio Telemetrico de Encendido", Color.FromArgb(30, 41, 59), 70);
            btnStartup.Click += (s, e) => RunAsync(() => {
                Log("Configurando servicio telemétrico silencioso de encendido...");
                Program.InstallStartupTask(true);
                RefreshStatusBadges();
                Log("Servicio telemétrico activado en segundo plano.");
            });
            leftPanel.Controls.Add(btnStartup);

            Button btnUninstallProcs = CreateActionButton("[3] Selector y Desinstalador de Apps", Color.FromArgb(30, 41, 59), 105);
            btnUninstallProcs.Click += (s, e) => OpenUninstallManager();
            leftPanel.Controls.Add(btnUninstallProcs);

            Button btnBlock = CreateActionButton("[4] Bloquear Web & Perfiles (Steam/Roblox)", Color.FromArgb(30, 41, 59), 140);
            btnBlock.Click += (s, e) => RunAsync(() => {
                Log("Aplicando directivas de bloqueo web y restricción de perfiles de navegadores...");
                Program.DoBlockGames();
                RefreshStatusBadges();
                Log("Bloqueo Web y Perfiles aplicado correctamente.");
            });
            leftPanel.Controls.Add(btnBlock);

            Button btnAccounts = CreateActionButton("[5] Bloquear Cuentas Microsoft / Escuela", Color.FromArgb(30, 41, 59), 175);
            btnAccounts.Click += (s, e) => RunAsync(() => {
                Log("Restringiendo inicio con cuentas Microsoft/Escuela...");
                Program.EnforceLocalAccountsOnly((msg) => Log(msg));
                RefreshStatusBadges();
                Log("Cuentas Microsoft bloqueadas.");
            });
            leftPanel.Controls.Add(btnAccounts);

            Button btnPurgeEmails = CreateActionButton("[11] Purgar y Desvincular Cuentas Puestas", Color.FromArgb(30, 41, 59), 210);
            btnPurgeEmails.Click += (s, e) => RunAsync(() => {
                Log("Iniciando purga radical de cuentas puestas y desvinculación total...");
                Program.PurgeConnectedAccountsAndIdentities((msg) => Log(msg));
                RefreshStatusBadges();
                Log("Purga de cuentas puestas completada.");
            });
            leftPanel.Controls.Add(btnPurgeEmails);

            Button btnWallpaper = CreateActionButton("[6] Perfiles Institucionales (Fondos / Logos)", Color.FromArgb(30, 41, 59), 245);
            btnWallpaper.Click += (s, e) => OpenInstitutionSelector();
            leftPanel.Controls.Add(btnWallpaper);

            Button btnWallpaperLock = CreateActionButton("[14] Bloquear Fondo y Apps Externas", Color.FromArgb(30, 41, 59), 280);
            btnWallpaperLock.Click += (s, e) => RunAsync(() => {
                Log("Aplicando bloqueo rígido de Fondo de Pantalla y cerrando apps externas...");
                Program.EnforceInstitutionalWallpaper((msg) => Log(msg));
                RefreshStatusBadges();
                Log("Fondo de Pantalla protegido contra cambios y apps externas.");
            });
            leftPanel.Controls.Add(btnWallpaperLock);

            Button btnShortcuts = CreateActionButton("[7] Accesos Directos Institucionales", Color.FromArgb(30, 41, 59), 315);
            btnShortcuts.Click += (s, e) => RunAsync(() => {
                Log("Creando accesos directos institucionales...");
                Program.CreateShortcuts();
                RefreshStatusBadges();
                Log("Accesos directos creados.");
            });
            leftPanel.Controls.Add(btnShortcuts);

            Button btnResetBrowsers = CreateActionButton("[8] Reset Radical Navegadores (User Data)", Color.FromArgb(30, 41, 59), 350);
            btnResetBrowsers.Click += (s, e) => RunAsync(() => {
                Log("Iniciando Reset Radical de User Data en Chrome y Edge...");
                Program.ResetBrowserUserData((msg) => Log(msg));
                Log("Reset Radical de Navegadores completado.");
            });
            leftPanel.Controls.Add(btnResetBrowsers);

            Button btnCleanFiles = CreateActionButton("[9] Limpiar Descargas y Basura Escritorio", Color.FromArgb(30, 41, 59), 385);
            btnCleanFiles.Click += (s, e) => RunAsync(() => {
                Log("Iniciando limpieza de Descargas y accesos del Escritorio...");
                Program.CleanDownloadsAndDesktop((msg) => Log(msg));
                Log("Limpieza de Descargas y Escritorio completada.");
            });
            leftPanel.Controls.Add(btnCleanFiles);

            Button btnDefaultApps = CreateActionButton("[12] Aplicaciones por Defecto (Office / Chrome)", Color.FromArgb(30, 41, 59), 420);
            btnDefaultApps.Click += (s, e) => RunAsync(() => {
                Log("Iniciando asignación de aplicaciones por defecto...");
                Program.EnforceDefaultApplications((msg) => Log(msg));
                RefreshStatusBadges();
                Log("Asignación de aplicaciones por defecto finalizada.");
            });
            leftPanel.Controls.Add(btnDefaultApps);

            Button btnMouseBlock = CreateActionButton("[13] Bloquear Personalizacion / Tamano Mouse", Color.FromArgb(30, 41, 59), 455);
            btnMouseBlock.Click += (s, e) => RunAsync(() => {
                Log("Aplicando bloqueo de personalización y tamaño del mouse...");
                Program.BlockMouseCustomization((msg) => Log(msg));
                RefreshStatusBadges();
                Log("Personalización y tamaño de mouse bloqueados.");
            });
            leftPanel.Controls.Add(btnMouseBlock);

            Button btnUnblock = CreateActionButton("[10] Desbloquear / Restaurar Equipo", Color.FromArgb(225, 29, 72), 490);
            btnUnblock.Click += (s, e) => RunAsync(() => {
                Log("Desbloqueando y restaurando configuraciones...");
                Program.UnblockEquipment();
                Program.AllowMicrosoftAccounts();
                Program.AllowMouseCustomization();
                Program.AllowWallpaperCustomization();
                Program.InstallStartupTask(false);
                RefreshStatusBadges();
                Log("Equipo desbloqueado y restaurado.");
            });
            leftPanel.Controls.Add(btnUnblock);

            panelActions.Controls.Add(leftPanel);

            // Console Box (Derecha)
            Panel rightPanel = new Panel();
            rightPanel.Location = new Point(395, 10);
            rightPanel.Size = new Size(630, 550);
            rightPanel.BackColor = Color.FromArgb(30, 41, 59);
            rightPanel.Padding = new Padding(12);

            Label lblLogTitle = new Label();
            lblLogTitle.Text = "Consola de Ejecucion LayvelGuard v" + Program.CURRENT_VERSION;
            lblLogTitle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            lblLogTitle.ForeColor = Color.FromArgb(203, 213, 225);
            lblLogTitle.Dock = DockStyle.Top;
            lblLogTitle.Height = 32;
            lblLogTitle.Padding = new Padding(5, 5, 0, 5);
            rightPanel.Controls.Add(lblLogTitle);

            Panel txtContainer = new Panel();
            txtContainer.Dock = DockStyle.Fill;
            txtContainer.Padding = new Padding(4);
            txtContainer.BackColor = Color.FromArgb(9, 13, 22);

            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.BackColor = Color.FromArgb(9, 13, 22);
            txtLog.ForeColor = Color.FromArgb(56, 189, 248);
            txtLog.Font = new Font("Consolas", 9.5f);
            txtLog.BorderStyle = BorderStyle.None;
            txtLog.Dock = DockStyle.Fill;

            txtContainer.Controls.Add(txtLog);
            rightPanel.Controls.Add(txtContainer);
            txtContainer.BringToFront();

            panelActions.Controls.Add(rightPanel);

            // 4. Tab Panel 2: Estatus del Equipo & Switches Individuales
            panelStatus = new Panel();
            panelStatus.Dock = DockStyle.Fill;
            panelStatus.BackColor = Color.FromArgb(15, 23, 42);
            panelStatus.Padding = new Padding(25);
            panelStatus.AutoScroll = true;
            panelStatus.Visible = false;

            Label lblStatusTabTitle = new Label();
            lblStatusTabTitle.Text = "Estado de Protecciones y Switches de Control Fino";
            lblStatusTabTitle.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblStatusTabTitle.ForeColor = Color.White;
            lblStatusTabTitle.Location = new Point(20, 15);
            lblStatusTabTitle.AutoSize = true;
            panelStatus.Controls.Add(lblStatusTabTitle);

            int y = 50;

            CreateStatusRow(panelStatus, "Bloqueo Web (Steam, Roblox, DoH):", y, out badgeBlockWeb, out btnToggleBlockWeb,
                (s, e) => RunAsync(() => { Program.DoBlockGames(); RefreshStatusBadges(); Log("Bloqueo Web activado."); }),
                (s, e) => RunAsync(() => { Program.UnblockEquipment(); RefreshStatusBadges(); Log("Bloqueo Web desactivado."); }));
            y += 60;

            CreateStatusRow(panelStatus, "Restriccion Cuentas Microsoft / Escuela:", y, out badgeAccounts, out btnToggleAccounts,
                (s, e) => RunAsync(() => { Program.EnforceLocalAccountsOnly((msg) => Log(msg)); RefreshStatusBadges(); Log("Cuentas Microsoft bloqueadas y desvinculadas."); }),
                (s, e) => RunAsync(() => { Program.AllowMicrosoftAccounts(); RefreshStatusBadges(); Log("Cuentas Microsoft permitidas."); }));
            y += 60;

            CreateStatusRow(panelStatus, "Purga Radical de Cuentas Puestas (MSA / Tokens):", y, out badgeEmailPurge, out btnToggleEmailPurge,
                (s, e) => RunAsync(() => { Program.PurgeConnectedAccountsAndIdentities((msg) => Log(msg)); RefreshStatusBadges(); }),
                (s, e) => RunAsync(() => { Program.PurgeConnectedAccountsAndIdentities((msg) => Log(msg)); RefreshStatusBadges(); }));
            y += 60;

            CreateStatusRow(panelStatus, "Bloqueo Fondo de Pantalla y Apps Externas (Lively/Engine):", y, out badgeWallpaperLock, out btnToggleWallpaperLock,
                (s, e) => RunAsync(() => { Program.EnforceInstitutionalWallpaper((msg) => Log(msg)); RefreshStatusBadges(); }),
                (s, e) => RunAsync(() => { Program.AllowWallpaperCustomization((msg) => Log(msg)); RefreshStatusBadges(); }));
            y += 60;

            CreateStatusRow(panelStatus, "Perfiles Institucionales (Fondos / Logos):", y, out badgeWallpaper, out btnToggleWallpaper,
                (s, e) => OpenInstitutionSelector(),
                (s, e) => Log("Gestor de Instituciones cerrado."));
            y += 60;

            CreateStatusRow(panelStatus, "Servicio Telemetrico de Encendido (Daemon):", y, out badgeService, out btnToggleService,
                (s, e) => RunAsync(() => { Program.InstallStartupTask(true); RefreshStatusBadges(); Log("Servicio telemétrico activado."); }),
                (s, e) => RunAsync(() => { Program.InstallStartupTask(false); RefreshStatusBadges(); Log("Servicio telemétrico desinstalado."); }));
            y += 60;

            CreateStatusRow(panelStatus, "Accesos Directos Institucionales:", y, out badgeShortcuts, out btnToggleShortcuts,
                (s, e) => RunAsync(() => { Program.CreateShortcuts(); RefreshStatusBadges(); Log("Accesos directos creados."); }),
                (s, e) => RunAsync(() => { Program.RemoveShortcuts(); RefreshStatusBadges(); Log("Accesos directos removidos."); }));
            y += 60;

            CreateStatusRow(panelStatus, "Selector y Desinstalador de Aplicaciones:", y, out badgeUninstall, out btnToggleUninstall,
                (s, e) => OpenUninstallManager(),
                (s, e) => Log("Selector de aplicaciones cerrado."));
            y += 60;

            CreateStatusRow(panelStatus, "Bloqueo Personalizacion y Tamaño de Mouse:", y, out badgeMouse, out btnToggleMouse,
                (s, e) => RunAsync(() => { Program.BlockMouseCustomization((msg) => Log(msg)); RefreshStatusBadges(); }),
                (s, e) => RunAsync(() => { Program.AllowMouseCustomization((msg) => Log(msg)); RefreshStatusBadges(); }));

            // 5. Footer Progress
            progressBar = new ProgressBar();
            progressBar.Dock = DockStyle.Bottom;
            progressBar.Height = 10;

            this.Controls.Add(panelActions);
            this.Controls.Add(panelStatus);
            this.Controls.Add(navBar);
            this.Controls.Add(header);
            this.Controls.Add(progressBar);
        }

        private void OpenInstitutionSelector()
        {
            InstitutionSelectorForm form = new InstitutionSelectorForm((msg) => Log(msg));
            form.ShowDialog(this);
            RefreshStatusBadges();
        }

        private void PerformManualUpdateCheck()
        {
            Log("====================================================");
            Log(" COMPROBANDO ACTUALIZACIONES EN GITHUB (layvel/layvelguard)");
            Log("====================================================");
            Log("Versión actual instalada: v" + Program.CURRENT_VERSION);

            ThreadPool.QueueUserWorkItem(state => {
                try {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = "LayvelGuard-Agent/" + Program.CURRENT_VERSION;
                        wc.Headers["Cache-Control"] = "no-cache";
                        wc.Headers["Pragma"] = "no-cache";

                        string sha = Program.GetLatestCommitSha();
                        string json = wc.DownloadString(string.Format("https://raw.githubusercontent.com/layvel/layvelguard/{0}/config.json", sha));
                        string remoteVer = Program.ExtractJsonValue(json, "script_version");

                        if (!string.IsNullOrEmpty(remoteVer))
                        {
                            if (remoteVer != Program.CURRENT_VERSION)
                            {
                                Log(string.Format("-> !NUEVA VERSION DETECTADA EN GITHUB!: v{0} (Actual: v{1})", remoteVer, Program.CURRENT_VERSION));
                                Log("-> Descargando ejecutable LayvelGuard.exe y paquetes de GitHub...");

                                bool isUpdating = Program.CheckAndUpdateSelf(true);
                                if (isUpdating)
                                {
                                    Log("-> Actualización completada desde GitHub. Reiniciando aplicación...");
                                    Thread.Sleep(1500);
                                    Application.Exit();
                                    return;
                                }
                            }
                            else
                            {
                                Log(string.Format("-> Forzando reinstalación / actualización desde GitHub (v{0})...", remoteVer));
                                bool isUpdating = Program.CheckAndUpdateSelf(true);
                                if (isUpdating)
                                {
                                    Log("-> Actualización forzada completada desde GitHub. Reiniciando...");
                                    Thread.Sleep(1500);
                                    Application.Exit();
                                    return;
                                }
                                Program.EnsureDefaultInstitutionsExist();
                            }
                        }
                        else
                        {
                            Log("-> El repositorio de GitHub aún se encuentra inicializando o no tiene config.json publicado.");
                        }
                    }
                } catch (Exception ex) {
                    Log("Aviso al consultar GitHub: " + ex.Message);
                }
            });
        }

        private void OpenUninstallManager()
        {
            UninstallManagerForm form = new UninstallManagerForm((msg) => Log(msg));
            form.ShowDialog(this);
            RefreshStatusBadges();
        }

        private void SwitchTab(bool showActions)
        {
            panelActions.Visible = showActions;
            panelStatus.Visible = !showActions;

            btnTabActions.BackColor = showActions ? Color.FromArgb(30, 41, 59) : Color.FromArgb(15, 23, 42);
            btnTabActions.ForeColor = showActions ? Color.White : Color.FromArgb(148, 163, 184);

            btnTabStatus.BackColor = !showActions ? Color.FromArgb(30, 41, 59) : Color.FromArgb(15, 23, 42);
            btnTabStatus.ForeColor = !showActions ? Color.White : Color.FromArgb(148, 163, 184);

            if (!showActions) RefreshStatusBadges();
        }

        private void CreateStatusRow(Panel parent, string title, int y, out Label badgeRef, out Button btnToggleRef, EventHandler onEnable, EventHandler onDisable)
        {
            Panel card = new Panel();
            card.Size = new Size(960, 50);
            card.Location = new Point(20, y);
            card.BackColor = Color.FromArgb(30, 41, 59);

            Label lbl = new Label();
            lbl.Text = title;
            lbl.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            lbl.ForeColor = Color.FromArgb(241, 245, 249);
            lbl.Location = new Point(15, 15);
            lbl.AutoSize = true;
            card.Controls.Add(lbl);

            Label badge = new Label();
            badge.Text = "[ VERIFICANDO ]";
            badge.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            badge.ForeColor = Color.Yellow;
            badge.Location = new Point(480, 15);
            badge.AutoSize = true;
            card.Controls.Add(badge);
            badgeRef = badge;

            Button btnToggle = new Button();
            btnToggle.Text = "Cambiar Estado";
            btnToggle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnToggle.BackColor = Color.FromArgb(16, 185, 129);
            btnToggle.ForeColor = Color.White;
            btnToggle.FlatStyle = FlatStyle.Flat;
            btnToggle.FlatAppearance.BorderSize = 0;
            btnToggle.Size = new Size(180, 32);
            btnToggle.Location = new Point(760, 9);
            btnToggle.Cursor = Cursors.Hand;
            btnToggleRef = btnToggle;

            Button targetBtn = btnToggle;

            targetBtn.Click += (s, e) => {
                if (targetBtn.Tag != null && (bool)targetBtn.Tag)
                {
                    onDisable(s, e);
                }
                else
                {
                    onEnable(s, e);
                }
            };

            card.Controls.Add(targetBtn);
            parent.Controls.Add(card);
        }

        private void RefreshStatusBadges()
        {
            SafeInvoke(() => {
                try {
                    // 1. Bloqueo Web
                    bool isWebBlocked = false;
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Google\Chrome\URLBlocklist"))
                    {
                        if (k != null) isWebBlocked = true;
                    }
                    UpdateBadge(badgeBlockWeb, btnToggleBlockWeb, isWebBlocked, "ACTIVADO", "DESACTIVADO");

                    // 2. Cuentas MS
                    bool isAccountsBlocked = false;
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                    {
                        if (k != null)
                        {
                            object val = k.GetValue("NoConnectedUser");
                            if (val != null && Convert.ToInt32(val) == 3) isAccountsBlocked = true;
                        }
                    }
                    UpdateBadge(badgeAccounts, badgeAccounts != null ? btnToggleAccounts : null, isAccountsBlocked, "ACTIVADO", "DESACTIVADO");

                    // 2b. Purga de Cuentas Puestas
                    if (badgeEmailPurge != null && btnToggleEmailPurge != null)
                    {
                        badgeEmailPurge.Text = "[ LISTO ]";
                        badgeEmailPurge.ForeColor = Color.FromArgb(74, 222, 128);
                        btnToggleEmailPurge.Text = "🔴 Purgar Cuentas Puestas";
                        btnToggleEmailPurge.BackColor = Color.FromArgb(225, 29, 72);
                        btnToggleEmailPurge.Tag = false;
                    }

                    // 3. Bloqueo de Fondo y Apps Externas
                    bool isWallpaperLocked = false;
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"))
                    {
                        if (k != null && k.GetValue("NoChangingWallPaper") != null) isWallpaperLocked = true;
                    }
                    if (!isWallpaperLocked)
                    {
                        using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"))
                        {
                            if (k != null && k.GetValue("NoChangingWallPaper") != null) isWallpaperLocked = true;
                        }
                    }
                    UpdateBadge(badgeWallpaperLock, btnToggleWallpaperLock, isWallpaperLocked, "ACTIVADO", "DESACTIVADO");

                    // 3b. Perfiles Institucionales
                    bool isWallpaperSet = Directory.Exists(@"C:\LayvelGuard\Instituciones");
                    if (badgeWallpaper != null && btnToggleWallpaper != null)
                    {
                        badgeWallpaper.Text = "[ GESTOR ACTIVO ]";
                        badgeWallpaper.ForeColor = Color.FromArgb(74, 222, 128);
                        btnToggleWallpaper.Text = "📂 Abrir Selector";
                        btnToggleWallpaper.BackColor = Color.FromArgb(59, 130, 246);
                        btnToggleWallpaper.Tag = false;
                    }

                    // 4. Servicio Telemétrico
                    bool isServiceActive = false;
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        if (k != null && k.GetValue("LayvelGuard") != null) isServiceActive = true;
                    }
                    UpdateBadge(badgeService, btnToggleService, isServiceActive, "ACTIVO EN SEGUNDO PLANO", "INACTIVO");

                    // 5. Accesos Directos
                    bool shortcutsExist = File.Exists(@"C:\Users\Public\Desktop\Plataforma DIA.lnk");
                    UpdateBadge(badgeShortcuts, btnToggleShortcuts, shortcutsExist, "INSTALADOS", "FALTAN ACCESOS");

                    // 6. Desinstalación / Filtro Procesos
                    UpdateBadge(badgeUninstall, btnToggleUninstall, true, "SELECTOR INTERACTIVO", "PAUSADO");

                    // 7. Bloqueo de Mouse
                    bool isMouseBlocked = false;
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Control Panel\Desktop"))
                    {
                        if (k != null && k.GetValue("NoChangingMousePointers") != null) isMouseBlocked = true;
                    }
                    if (!isMouseBlocked)
                    {
                        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\Control Panel\Desktop"))
                        {
                            if (k != null && k.GetValue("NoChangingMousePointers") != null) isMouseBlocked = true;
                        }
                    }
                    UpdateBadge(badgeMouse, btnToggleMouse, isMouseBlocked, "ACTIVADO", "DESACTIVADO");
                } catch {}
            });
        }

        private void UpdateBadge(Label badge, Button btnToggle, bool active, string textActive, string textInactive)
        {
            if (badge == null || btnToggle == null) return;

            if (active)
            {
                badge.Text = "[ 🟢 " + textActive + " ]";
                badge.ForeColor = Color.FromArgb(74, 222, 128);
                btnToggle.Text = "🔴 Abrir Selector";
                btnToggle.BackColor = Color.FromArgb(225, 29, 72);
                btnToggle.Tag = true;
            }
            else
            {
                badge.Text = "[ 🔴 " + textInactive + " ]";
                badge.ForeColor = Color.FromArgb(248, 113, 113);
                btnToggle.Text = "🟢 Abrir Selector";
                btnToggle.BackColor = Color.FromArgb(16, 185, 129);
                btnToggle.Tag = false;
            }
        }

        private Button CreateActionButton(string text, Color bg, int top)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.Font = new Font("Segoe UI", 8.8f, FontStyle.Bold);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Location = new Point(0, top);
            btn.Size = new Size(350, 32);
            btn.Cursor = Cursors.Hand;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(8, 0, 0, 0);
            return btn;
        }

        private void Log(string msg)
        {
            SafeInvoke(() => {
                string stamp = DateTime.Now.ToString("HH:mm:ss");
                txtLog.AppendText(string.Format("[{0}] {1}\r\n", stamp, msg));
            });
        }

        private void SetProgress(int val)
        {
            SafeInvoke(() => {
                progressBar.Value = val;
            });
        }

        private void CheckConnectionAsync()
        {
            ThreadPool.QueueUserWorkItem(state => {
                SafeInvoke(() => {
                    lblStatus.Text = "[v" + Program.CURRENT_VERSION + "] LayvelGuard Standalone (GitHub)";
                    lblStatus.ForeColor = Color.FromArgb(74, 222, 128);
                    Log("Iniciado LayvelGuard Pro v" + Program.CURRENT_VERSION + " Standalone.");
                    Log("Icono y Logo personalizado de Zote cargados correctamente.");
                });
            });
        }

        private void RunAsync(Action action)
        {
            ThreadPool.QueueUserWorkItem(state => {
                try { action(); } catch (Exception ex) { Log("Error: " + ex.Message); }
            });
        }

        private void DoFullMaintenance()
        {
            SetProgress(5);
            Log("====================================================");
            Log(" INICIANDO MANTENIMIENTO COMPLETO LAYVELGUARD v" + Program.CURRENT_VERSION);
            Log("====================================================");

            SetProgress(15);
            Log("[1/8] Escaneando y desinstalando software no autorizado...");
            Program.UninstallProhibitedSoftware((msg) => Log("      " + msg));

            SetProgress(30);
            Log("[2/8] Restringiendo y purgando inicio de sesión de Cuentas de Correo / MS...");
            Program.EnforceLocalAccountsOnly((msg) => Log("      " + msg));
            Log("      -> Políticas de Cuentas Locales y purga de correos aplicadas.");

            SetProgress(45);
            Log("[3/8] Aplicando bloqueo web y restricción de perfiles de navegación...");
            Program.DoBlockGames();
            Program.BlockMouseCustomization((msg) => Log("      " + msg));
            Log("      -> Bloqueo Web, DoH, mouse y perfiles aplicados.");

            SetProgress(55);
            Log("[4/8] Eliminando datos residuales de Roblox y Minecraft...");
            Program.PurgeRobloxMinecraftAppData((msg) => Log("      " + msg));

            SetProgress(65);
            Log("[5/8] Ejecutando limpieza de Descargas y accesos del Escritorio...");
            Program.CleanDownloadsAndDesktop((msg) => Log("      " + msg));

            SetProgress(75);
            Log("[6/8] Aplicando bloqueo rígido de Fondo de Pantalla y Perfil Institucional...");
            Program.EnsureDefaultInstitutionsExist();
            Program.EnforceInstitutionalWallpaper((msg) => Log("      " + msg));

            SetProgress(80);
            Log("[7/9] Generando accesos directos institucionales...");
            Program.CreateShortcuts();
            Log("      -> Accesos directos institucionales verificados.");

            SetProgress(90);
            Log("[8/9] Forzando aplicaciones por defecto (MS Office & Google Chrome)...");
            Program.EnforceDefaultApplications((msg) => Log("      " + msg));

            SetProgress(95);
            Log("[9/9] Registrando servicio telemétrico silencioso LayvelGuard...");
            Program.InstallStartupTask(true);
            Log("      -> Servicio telemétrico registrado al encender Windows.");

            SetProgress(100);
            RefreshStatusBadges();
            Log("====================================================");
            Log(" MANTENIMIENTO COMPLETO LAYVELGUARD FINALIZADO CON EXITO! ");
            Log("====================================================");
        }
    }
}
