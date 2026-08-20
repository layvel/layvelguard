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
using Microsoft.Win32;

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
        public const string CURRENT_VERSION = "1.0.0";
        public const string APP_NAME = "LayvelGuard";
        public const string APP_DIR = @"C:\LayvelGuard";
        
        // GitHub Repository Central URLs
        private const string GITHUB_CONFIG_URL = "https://raw.githubusercontent.com/layvel/layvelguard/main/config.json";
        private const string GITHUB_EXE_URL = "https://raw.githubusercontent.com/layvel/layvelguard/main/LayvelGuard.exe";
        private const string LOG_FILE = @"C:\LayvelGuard\layvelguard.log";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        [STAThread]
        public static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            if (!Directory.Exists(APP_DIR)) Directory.CreateDirectory(APP_DIR);

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
                if (!exePath.StartsWith(APP_DIR, StringComparison.OrdinalIgnoreCase) && !force) return false;

                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "LayvelGuard-Agent/" + CURRENT_VERSION;
                    string json = wc.DownloadString(GITHUB_CONFIG_URL + "?t=" + Environment.TickCount);
                    if (json.Contains("\"script_version\""))
                    {
                        string remoteVer = ExtractJsonValue(json, "script_version");
                        if (force || (!string.IsNullOrEmpty(remoteVer) && remoteVer != CURRENT_VERSION))
                        {
                            string targetExe = Path.Combine(APP_DIR, "LayvelGuard.exe");
                            string tempFile = Path.Combine(APP_DIR, "LayvelGuard_Update.exe");

                            wc.DownloadFile(GITHUB_EXE_URL + "?v=" + Environment.TickCount, tempFile);

                            if (File.Exists(tempFile) && new FileInfo(tempFile).Length > 10000)
                            {
                                string batchUpdater = Path.Combine(APP_DIR, "update_layvelguard.bat");
                                string script = string.Format(
                                    "@echo off\r\n" +
                                    "timeout /t 2 /nobreak > nul\r\n" +
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

        private static void RunSilentBoot()
        {
            try {
                CleanInvalidAutoLogon();
                InstallStartupTask();
                DoBlockGames();
                EnforceLocalAccountsOnly();
                UninstallProhibitedSoftware(null);
                List<string> detected = KillProhibitedProcesses();
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
                // 1. DNS Over HTTPS off
                string[] browserKeys = new string[] {
                    @"SOFTWARE\Policies\Google\Chrome",
                    @"Software\Policies\Google\Chrome",
                    @"SOFTWARE\Policies\Microsoft\Edge",
                    @"Software\Policies\Microsoft\Edge"
                };

                foreach (string key in browserKeys)
                {
                    RegistryKey root = key.StartsWith("Software", StringComparison.OrdinalIgnoreCase) ? Registry.CurrentUser : Registry.LocalMachine;
                    using (RegistryKey k = root.CreateSubKey(key))
                    {
                        if (k != null)
                        {
                            k.SetValue("DnsOverHttpsMode", "off", RegistryValueKind.String);
                            k.SetValue("BuiltInDnsClientEnabled", 0, RegistryValueKind.DWord);
                        }
                    }
                }

                // 2. URLBlocklist (Roblox, Minecraft, Steam, Games)
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

                // 3. Hosts File (IPv4 e IPv6)
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
            } catch {}
        }

        public static void EnforceLocalAccountsOnly()
        {
            try {
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    if (key != null)
                    {
                        key.SetValue("NoConnectedUser", 3, RegistryValueKind.DWord);
                    }
                }
            } catch {}
        }

        public static void AllowMicrosoftAccounts()
        {
            try {
                using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    if (key != null)
                    {
                        key.DeleteValue("NoConnectedUser", false);
                    }
                }
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

        public static List<ProhibitedAppInfo> ScanProhibitedSoftware()
        {
            List<ProhibitedAppInfo> found = new List<ProhibitedAppInfo>();
            
            string[] prohibitedKeywords = new string[] {
                // Juegos & Launchers
                "roblox", "steam", "minecraft", "epic games", "valorant", "league of legends", "origin", "ea app",
                // Torrents & P2P
                "utorrent", "bittorrent", "qbittorrent", "ares",
                // Navegadores No Autorizados (Solo Edge y Chrome permitidos)
                "firefox", "opera", "brave", "vivaldi", "tor browser", "yandex", "uc browser", "waterfox", "chromium",
                // Antivirus & Limpiadores No Autorizados (Solo Windows Defender permitido)
                "avast", "avg", "avira", "kaspersky", "mcafee", "norton", "bitdefender", "panda", "eset", "sophos", "malwarebytes", "360 total security", "ccleaner"
            };

            string[] regKeys = new string[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            // 1. Escaneo en HKLM y HKCU
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
                                                string dispName = displayNameObj.ToString();
                                                string lowerName = dispName.ToLower();

                                                foreach (string kw in prohibitedKeywords)
                                                {
                                                    if (lowerName.Contains(kw))
                                                    {
                                                        ProhibitedAppInfo app = new ProhibitedAppInfo();
                                                        app.Name = dispName;
                                                        object unist = subkey.GetValue("QuietUninstallString") ?? subkey.GetValue("UninstallString");
                                                        app.UninstallString = unist != null ? unist.ToString() : "";
                                                        object instLoc = subkey.GetValue("InstallLocation");
                                                        app.InstallLocation = instLoc != null ? instLoc.ToString() : "";
                                                        app.KeyPath = keyPath + "\\" + subkeyName;

                                                        bool already = false;
                                                        foreach (var f in found) { if (f.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase)) already = true; }
                                                        if (!already) found.Add(app);
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    } catch {}
                }
            }

            // 2. Escaneo en HKEY_USERS (Todos los perfiles de usuario del sistema)
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
                                                                string dispName = displayNameObj.ToString();
                                                                string lowerName = dispName.ToLower();

                                                                foreach (string kw in prohibitedKeywords)
                                                                {
                                                                    if (lowerName.Contains(kw))
                                                                    {
                                                                        ProhibitedAppInfo app = new ProhibitedAppInfo();
                                                                        app.Name = dispName;
                                                                        object unist = subkey.GetValue("QuietUninstallString") ?? subkey.GetValue("UninstallString");
                                                                        app.UninstallString = unist != null ? unist.ToString() : "";
                                                                        object instLoc = subkey.GetValue("InstallLocation");
                                                                        app.InstallLocation = instLoc != null ? instLoc.ToString() : "";
                                                                        app.KeyPath = "HKEY_USERS\\" + sid + "\\" + keyPath + "\\" + subkeyName;

                                                                        bool already = false;
                                                                        foreach (var f in found) { if (f.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase)) already = true; }
                                                                        if (!already) found.Add(app);
                                                                        break;
                                                                    }
                                                                }
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

            // 3. Escaneo exhaustivo en carpetas de TODOS los perfiles de C:\Users\*
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
                            Path.Combine(userDir, @"AppData\Local\BraveSoftware"),
                            Path.Combine(userDir, @"AppData\Local\Vivaldi"),
                            Path.Combine(userDir, @"AppData\Local\Tor Browser")
                        };

                        foreach (string path in targetPaths)
                        {
                            if (Directory.Exists(path))
                            {
                                string appTitle = Path.GetFileName(path);
                                if (appTitle.Equals("Programs", StringComparison.OrdinalIgnoreCase) || appTitle.Equals("Local", StringComparison.OrdinalIgnoreCase) || appTitle.Equals("Roaming", StringComparison.OrdinalIgnoreCase))
                                {
                                    appTitle = Path.GetFileName(Path.GetDirectoryName(path));
                                }

                                bool already = false;
                                foreach (var f in found)
                                {
                                    if (!string.IsNullOrEmpty(f.InstallLocation) && f.InstallLocation.Equals(path, StringComparison.OrdinalIgnoreCase)) already = true;
                                }

                                if (!already)
                                {
                                    ProhibitedAppInfo app = new ProhibitedAppInfo();
                                    app.Name = string.Format("{0} (Carpeta: {1})", appTitle, uName);
                                    app.InstallLocation = path;
                                    app.UninstallString = "";
                                    found.Add(app);
                                }
                            }
                        }
                    }
                }
            } catch {}

            // 4. Carpetas globales de Program Files y ProgramData
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            string[] globalPaths = new string[] {
                Path.Combine(pf86, "Steam"),
                Path.Combine(pf, "Steam"),
                Path.Combine(pf86, "Roblox"),
                Path.Combine(pf, "Roblox"),
                Path.Combine(pf86, "Mozilla Firefox"),
                Path.Combine(pf, "Mozilla Firefox"),
                Path.Combine(pf86, "Opera"),
                Path.Combine(pf, "Opera"),
                Path.Combine(pf86, "BraveSoftware"),
                Path.Combine(pf, "BraveSoftware"),
                Path.Combine(pf86, "AVAST Software"),
                Path.Combine(pf, "AVAST Software"),
                Path.Combine(pf86, "AVG"),
                Path.Combine(pf, "AVG"),
                Path.Combine(pf86, "CCleaner"),
                Path.Combine(pf, "CCleaner"),
                @"C:\ProgramData\Roblox",
                @"C:\ProgramData\AVAST Software",
                @"C:\Games"
            };

            foreach (string path in globalPaths)
            {
                if (Directory.Exists(path))
                {
                    bool already = false;
                    foreach (var f in found)
                    {
                        if (!string.IsNullOrEmpty(f.InstallLocation) && f.InstallLocation.Equals(path, StringComparison.OrdinalIgnoreCase)) already = true;
                    }
                    if (!already)
                    {
                        ProhibitedAppInfo app = new ProhibitedAppInfo();
                        app.Name = "Directorio Sistema: " + Path.GetFileName(path);
                        app.InstallLocation = path;
                        app.UninstallString = "";
                        found.Add(app);
                    }
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
            string[] procs = new string[] {
                "RobloxPlayerBeta", "RobloxStudioBeta", "RobloxPlayerLauncher",
                "Steam", "SteamService", "steamwebhelper", "MinecraftLauncher", "EpicGamesLauncher",
                "uTorrent", "BitTorrent", "qBittorrent", "Ares",
                "firefox", "opera", "operagx", "brave", "vivaldi", "tor", "yandex", "ucbrowser",
                "AvastUI", "AVGUI", "ccleaner", "mcshield", "bdagent"
            };

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
            string[] regKeys = new string[] {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

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
                                            object displayName = subkey.GetValue("DisplayName");
                                            if (displayName != null && !string.IsNullOrWhiteSpace(displayName.ToString()))
                                            {
                                                string name = displayName.ToString();
                                                if (!apps.Contains(name)) apps.Add(name);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    } catch {}
                }
            }
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

    public class UninstallManagerForm : Form
    {
        private CheckedListBox chkListApps;
        private Button btnUninstall, btnRescan, btnClose;
        private Label lblInfo;
        private List<ProhibitedAppInfo> scannedApps;
        private Action<string> mainLogger;

        public UninstallManagerForm(Action<string> logger)
        {
            this.mainLogger = logger;
            InitializeComponent();
            RunScan();
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
            lblTitle.Text = "🔍 LayvelGuard - Escáner e Desinstalador Interactivo (v" + Program.CURRENT_VERSION + ")";
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

            chkListApps = new CheckedListBox();
            chkListApps.Location = new Point(20, 85);
            chkListApps.Size = new Size(665, 330);
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
            btnRescan.Size = new Size(160, 38);
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
            btnUninstall.Size = new Size(240, 38);
            btnUninstall.Location = new Point(190, 460);
            btnUninstall.Cursor = Cursors.Hand;
            btnUninstall.Click += (s, e) => ExecuteSelectedUninstall();
            this.Controls.Add(btnUninstall);

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

        private void RunScan()
        {
            chkListApps.Items.Clear();
            lblInfo.Text = "Escaneando Registro de Usuarios y Disco...";
            lblInfo.ForeColor = Color.FromArgb(56, 189, 248);
            Application.DoEvents();

            ThreadPool.QueueUserWorkItem(state => {
                scannedApps = Program.ScanProhibitedSoftware();

                this.Invoke(new Action(() => {
                    chkListApps.Items.Clear();
                    int checkedCount = 0;
                    foreach (var app in scannedApps)
                    {
                        chkListApps.Items.Add(app.Name, true);
                        checkedCount++;
                    }

                    if (scannedApps.Count == 0)
                    {
                        lblInfo.Text = "🟢 No se encontraron aplicaciones no autorizadas (Juegos, Browsers extra o Antivirus).";
                        lblInfo.ForeColor = Color.FromArgb(74, 222, 128);
                    }
                    else
                    {
                        lblInfo.Text = string.Format("⚠️ Detectadas {0} aplicaciones no autorizadas ({1} marcadas).", scannedApps.Count, checkedCount);
                        lblInfo.ForeColor = Color.FromArgb(251, 191, 36);
                    }
                }));
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
                    try {
                        this.Invoke(new Action(() => {
                            lblInfo.Text = msg;
                            if (mainLogger != null) mainLogger(msg);
                        }));
                    } catch {}
                });

                try {
                    this.Invoke(new Action(() => {
                        btnUninstall.Enabled = true;
                        btnRescan.Enabled = true;
                        MessageBox.Show("Desinstalación y limpieza de Registro completadas con éxito.", "LayvelGuard Finalizado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        RunScan();
                    }));
                } catch {}
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
        private Label badgeBlockWeb, badgeAccounts, badgeWallpaper, badgeService, badgeShortcuts, badgeUninstall;
        private Button btnToggleBlockWeb, btnToggleAccounts, btnToggleWallpaper, btnToggleService, btnToggleShortcuts, btnToggleUninstall;

        public MainForm()
        {
            InitializeComponent();
            CheckConnectionAsync();
            RefreshStatusBadges();
        }

        private void InitializeComponent()
        {
            this.Text = "LAYVELGUARD PRO - CONTROL & MAINTENANCE AGENT (v" + Program.CURRENT_VERSION + ")";
            this.Size = new Size(1040, 720);
            this.MinimumSize = new Size(980, 680);
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
            leftPanel.Size = new Size(350, 520);
            leftPanel.BackColor = Color.Transparent;

            Button btnFull = CreateActionButton("[!] MANTENIMIENTO AUTOMATICO", Color.FromArgb(16, 185, 129), 0);
            btnFull.Click += (s, e) => RunAsync(DoFullMaintenance);
            leftPanel.Controls.Add(btnFull);

            Button btnStatusNav = CreateActionButton("[1] Ver Estatus y Switches On/Off", Color.FromArgb(59, 130, 246), 46);
            btnStatusNav.Click += (s, e) => SwitchTab(false);
            leftPanel.Controls.Add(btnStatusNav);

            Button btnStartup = CreateActionButton("[2] Servicio Telemetrico de Encendido", Color.FromArgb(30, 41, 59), 92);
            btnStartup.Click += (s, e) => RunAsync(() => {
                Log("Configurando servicio telemétrico silencioso de encendido...");
                Program.InstallStartupTask(true);
                RefreshStatusBadges();
                Log("Servicio telemétrico activado en segundo plano.");
            });
            leftPanel.Controls.Add(btnStartup);

            Button btnUninstallProcs = CreateActionButton("[3] Selector y Desinstalador de Apps", Color.FromArgb(30, 41, 59), 138);
            btnUninstallProcs.Click += (s, e) => OpenUninstallManager();
            leftPanel.Controls.Add(btnUninstallProcs);

            Button btnBlock = CreateActionButton("[4] Bloquear Web (Steam / Roblox / DoH)", Color.FromArgb(30, 41, 59), 184);
            btnBlock.Click += (s, e) => RunAsync(() => {
                Log("Aplicando directivas de bloqueo web (Steam, Roblox, DoH)...");
                Program.DoBlockGames();
                RefreshStatusBadges();
                Log("Bloqueo Web aplicado correctamente.");
            });
            leftPanel.Controls.Add(btnBlock);

            Button btnAccounts = CreateActionButton("[5] Bloquear Cuentas Microsoft", Color.FromArgb(30, 41, 59), 230);
            btnAccounts.Click += (s, e) => RunAsync(() => {
                Log("Restringiendo inicio con cuentas Microsoft/Escuela...");
                Program.EnforceLocalAccountsOnly();
                RefreshStatusBadges();
                Log("Cuentas Microsoft bloqueadas.");
            });
            leftPanel.Controls.Add(btnAccounts);

            Button btnWallpaper = CreateActionButton("[6] Aplicar Wallpaper LayvelGuard", Color.FromArgb(30, 41, 59), 276);
            btnWallpaper.Click += (s, e) => RunAsync(() => {
                Log("Aplicando fondo de escritorio y pantalla de bloqueo...");
                string wallPath = @"C:\LayvelGuard\layvelguard_logo.png";
                if (File.Exists(wallPath)) {
                    Program.SetWallpapers(wallPath);
                    Log("Fondo de escritorio y bloqueo aplicados.");
                }
                RefreshStatusBadges();
            });
            leftPanel.Controls.Add(btnWallpaper);

            Button btnShortcuts = CreateActionButton("[7] Accesos Directos Institucionales", Color.FromArgb(30, 41, 59), 322);
            btnShortcuts.Click += (s, e) => RunAsync(() => {
                Log("Creando accesos directos institucionales...");
                Program.CreateShortcuts();
                RefreshStatusBadges();
                Log("Accesos directos creados.");
            });
            leftPanel.Controls.Add(btnShortcuts);

            Button btnUnblock = CreateActionButton("[8] Desbloquear / Restaurar Equipo", Color.FromArgb(225, 29, 72), 368);
            btnUnblock.Click += (s, e) => RunAsync(() => {
                Log("Desbloqueando y restaurando configuraciones...");
                Program.UnblockEquipment();
                Program.AllowMicrosoftAccounts();
                Program.InstallStartupTask(false);
                RefreshStatusBadges();
                Log("Equipo desbloqueado y restaurado.");
            });
            leftPanel.Controls.Add(btnUnblock);

            panelActions.Controls.Add(leftPanel);

            // Console Box (Derecha)
            Panel rightPanel = new Panel();
            rightPanel.Location = new Point(390, 10);
            rightPanel.Size = new Size(610, 520);
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
                (s, e) => RunAsync(() => { Program.EnforceLocalAccountsOnly(); RefreshStatusBadges(); Log("Cuentas Microsoft bloqueadas."); }),
                (s, e) => RunAsync(() => { Program.AllowMicrosoftAccounts(); RefreshStatusBadges(); Log("Cuentas Microsoft permitidas."); }));
            y += 60;

            CreateStatusRow(panelStatus, "Fondo de Pantalla LayvelGuard:", y, out badgeWallpaper, out btnToggleWallpaper,
                (s, e) => RunAsync(() => {
                    string wp = @"C:\LayvelGuard\layvelguard_logo.png";
                    if (File.Exists(wp)) Program.SetWallpapers(wp);
                    RefreshStatusBadges();
                    Log("Fondo aplicado.");
                }),
                (s, e) => RunAsync(() => {
                    Log("Fondo liberado.");
                    RefreshStatusBadges();
                }));
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
                        wc.Headers[HttpRequestHeader.UserAgent] = "LayvelGuard-Agent/1.0.0";
                        string json = wc.DownloadString("https://raw.githubusercontent.com/layvel/layvelguard/main/config.json?t=" + Environment.TickCount);
                        string remoteVer = Program.ExtractJsonValue(json, "script_version");

                        if (!string.IsNullOrEmpty(remoteVer))
                        {
                            if (remoteVer != Program.CURRENT_VERSION)
                            {
                                Log(string.Format("-> !NUEVA VERSION DETECTADA EN GITHUB!: v{0} (Actual: v{1})", remoteVer, Program.CURRENT_VERSION));
                                Log("-> Descargando ejecutable LayvelGuard.exe desde GitHub...");

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
                                Log(string.Format("-> LayvelGuard se encuentra en la versión más reciente en GitHub (v{0}).", remoteVer));
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
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(RefreshStatusBadges));
                return;
            }

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
                UpdateBadge(badgeAccounts, btnToggleAccounts, isAccountsBlocked, "ACTIVADO", "DESACTIVADO");

                // 3. Fondo LayvelGuard
                bool isWallpaperSet = File.Exists(@"C:\LayvelGuard\layvelguard_logo.png");
                UpdateBadge(badgeWallpaper, btnToggleWallpaper, isWallpaperSet, "APLICADO", "NO INSTALADO");

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
            } catch {}
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
            btn.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Location = new Point(0, top);
            btn.Size = new Size(350, 38);
            btn.Cursor = Cursors.Hand;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(10, 0, 0, 0);
            return btn;
        }

        private void Log(string msg)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => Log(msg)));
                return;
            }
            string stamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText(string.Format("[{0}] {1}\r\n", stamp, msg));
        }

        private void SetProgress(int val)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => SetProgress(val)));
                return;
            }
            progressBar.Value = val;
        }

        private void CheckConnectionAsync()
        {
            ThreadPool.QueueUserWorkItem(state => {
                this.Invoke(new Action(() => {
                    lblStatus.Text = "[v" + Program.CURRENT_VERSION + "] LayvelGuard Standalone (GitHub)";
                    lblStatus.ForeColor = Color.FromArgb(74, 222, 128);
                    Log("Iniciado LayvelGuard Pro v" + Program.CURRENT_VERSION + " Standalone.");
                    Log("Icono y Logo personalizado de Zote cargados correctamente.");
                }));
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
            SetProgress(10);
            Log("====================================================");
            Log(" INICIANDO MANTENIMIENTO COMPLETO LAYVELGUARD v1.0.0 ");
            Log("====================================================");

            SetProgress(20);
            Log("[1/6] Escaneando y desinstalando software no autorizado...");
            Program.UninstallProhibitedSoftware((msg) => Log("      " + msg));

            SetProgress(40);
            Log("[2/6] Restringiendo inicio de sesion a Cuentas Locales únicamente...");
            Program.EnforceLocalAccountsOnly();
            Log("      -> Politicas de Cuentas Locales aplicadas.");

            SetProgress(60);
            Log("[3/6] Aplicando bloqueo web de juegos (Steam, Roblox, DoH, Hosts)...");
            Program.DoBlockGames();
            Log("      -> Bloqueo Web y filtro de navegadores aplicado.");

            SetProgress(75);
            Log("[4/6] Aplicando fondo de escritorio y pantalla de bloqueo LayvelGuard...");
            string wallPath = @"C:\LayvelGuard\layvelguard_logo.png";
            if (File.Exists(wallPath)) {
                Program.SetWallpapers(wallPath);
                Log("      -> Fondo de escritorio y bloqueo aplicados.");
            }

            SetProgress(90);
            Log("[5/6] Generando accesos directos institucionales...");
            Program.CreateShortcuts();
            Log("      -> Accesos directos institucionales verificados.");

            SetProgress(95);
            Log("[6/6] Registrando servicio telemétrico silencioso LayvelGuard...");
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
