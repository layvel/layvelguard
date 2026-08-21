# -*- coding: utf-8 -*-
"""
INTERFAZ GRÁFICA MODERNA Y SERVICIO DE INICIO AUTOMÁTICO CBMW (DARK MODE)
Servidor Central: https://sistemas.cbmw.cl
"""

import sys
import os
import re
import time
import json
import urllib.request
import urllib.error
import subprocess
import ctypes
import winreg
import threading
from datetime import datetime

import tkinter as tk
from tkinter import ttk, messagebox, scrolledtext

SERVER_URL = "https://sistemas.cbmw.cl"
CONFIG_URL = f"{SERVER_URL}/lab-config.json"
REPORT_URL = f"{SERVER_URL}/api/lab/reporte.php"
LOG_FILE = r"C:\CBMW\lab_log.txt"

def is_admin():
    try:
        return ctypes.windll.shell32.IsUserAnAdmin() != 0
    except Exception:
        return False

def elevate_admin():
    if not is_admin():
        script = os.path.abspath(sys.argv[0])
        params = " ".join([f'"{a}"' for a in sys.argv[1:]])
        ctypes.windll.shell32.ShellExecuteW(
            None, "runas",
            sys.executable if script.endswith('.py') else script,
            f'"{script}" {params}' if script.endswith('.py') else params,
            None, 1
        )
        sys.exit(0)

# ==============================================================================
# LÓGICA DEL SISTEMA, TAREA PROGRAMADA Y TELEMETRÍA
# ==============================================================================

def install_startup_task():
    exe_path = r"C:\CBMW\Menu_Administracion_CBMW.exe"
    if not os.path.exists(exe_path):
        exe_path = os.path.abspath(sys.argv[0])

    cmd = [
        "schtasks", "/create", "/tn", "AgenteCBMW_Heartbeat",
        "/tr", f'"{exe_path}" --silent-boot',
        "/sc", "ONSTART", "/ru", "SYSTEM", "/rl", "HIGHEST", "/f"
    ]
    try:
        subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        
        # Registro Run de respaldo para inicio de usuario
        with winreg.CreateKey(winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Run") as k:
            winreg.SetValueEx(k, "AgenteCBMW", 0, winreg.REG_SZ, f'"{exe_path}" --silent-boot')
        return True
    except Exception:
        return False

def get_config():
    try:
        req = urllib.request.Request(f"{CONFIG_URL}?t={int(time.time())}", headers={"Cache-Control": "no-cache", "User-Agent": "CBMW-Agent/2.0"})
        with urllib.request.urlopen(req, timeout=4) as resp:
            data = json.loads(resp.read().decode('utf-8'))
            return True, data
    except Exception:
        pass
    
    local_json = r"C:\CBMW\lab-config.json"
    if os.path.exists(local_json):
        try:
            with open(local_json, "r", encoding="utf-8") as f:
                return False, json.load(f)
        except Exception:
            pass

    return False, {
        "enabled": True,
        "mode": "enforce",
        "script_version": "2.0.0",
        "server_url": SERVER_URL,
        "block_roblox_web": True,
        "block_steam": True,
        "clean_downloads": True,
        "clean_downloads_days": 7,
        "clean_desktop_clutter": True,
        "shortcuts": [
            {"name": "Plataforma DIA", "url": "https://dia.agenciaeducacion.cl/login"},
            {"name": "UMaximo", "url": "https://www.umaximo.com/"}
        ]
    }

def set_reg_dword(key, subkey, name, value):
    try:
        with winreg.CreateKey(key, subkey) as k:
            winreg.SetValueEx(k, name, 0, winreg.REG_DWORD, int(value))
    except Exception:
        pass

def set_reg_sz(key, subkey, name, value):
    try:
        with winreg.CreateKey(key, subkey) as k:
            winreg.SetValueEx(k, name, 0, winreg.REG_SZ, str(value))
    except Exception:
        pass

def delete_reg_key(key, subkey):
    try:
        winreg.DeleteKey(key, subkey)
    except Exception:
        pass

def silent_boot_worker():
    """Ejecución silenciosa al encender Windows (Sin ventana GUI)"""
    install_startup_task()
    is_online, config = get_config()

    if config.get("block_roblox_web", True):
        # Bloqueo silencioso
        policies = [
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Google\Chrome"),
            (winreg.HKEY_CURRENT_USER, r"Software\Policies\Google\Chrome"),
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Microsoft\Edge"),
            (winreg.HKEY_CURRENT_USER, r"Software\Policies\Microsoft\Edge")
        ]
        for hk, path in policies:
            set_reg_sz(hk, path, "DnsOverHttpsMode", "off")
            set_reg_dword(hk, path, "BuiltInDnsClientEnabled", 0)

        block_patterns = ["*roblox.com*", "*roblox.es*", "*rbxcdn.com*", "*minecraft.net*", "*minecraft.com*"]
        url_keys = [
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Google\Chrome\URLBlocklist"),
            (winreg.HKEY_CURRENT_USER, r"Software\Policies\Google\Chrome\URLBlocklist"),
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist"),
            (winreg.HKEY_CURRENT_USER, r"Software\Policies\Microsoft\Edge\URLBlocklist")
        ]
        for hk, path in url_keys:
            for idx, pat in enumerate(block_patterns, 1):
                set_reg_sz(hk, path, str(idx), pat)

    # Auditar y matar procesos prohibidos
    procs_to_kill = ["RobloxPlayerBeta.exe", "RobloxStudioBeta.exe", "Steam.exe", "MinecraftLauncher.exe"]
    detected = []
    try:
        output = subprocess.check_output(["tasklist", "/FO", "CSV"], text=True, errors="ignore")
        for line in output.splitlines():
            for p in procs_to_kill:
                if p.lower() in line.lower():
                    name = p.replace(".exe", "")
                    if name not in detected:
                        detected.append(name)
                    subprocess.run(["taskkill", "/F", "/IM", p], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except Exception:
        pass

    # Enviar reporte de encendido ON
    try:
        hostname = os.environ.get("COMPUTERNAME", "UNKNOWN-PC")
        username = os.environ.get("USERNAME", "SYSTEM")
        payload = {
            "hostname": hostname,
            "username": username,
            "ip": "127.0.0.1",
            "os": "Windows (Boot Auto)",
            "status": "ONLINE",
            "detected_apps": detected,
            "error_message": "",
            "timestamp": datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        }
        data = json.dumps(payload).encode('utf-8')
        req = urllib.request.Request(REPORT_URL, data=data, headers={"Content-Type": "application/json", "User-Agent": "CBMW-Agent/2.0"})
        with urllib.request.urlopen(req, timeout=5):
            pass
    except Exception:
        pass

    sys.exit(0)

# ==============================================================================
# APLICACIÓN DE INTERFAZ GRÁFICA TKINTER (DARK THEME)
# ==============================================================================

class CBMWApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Sistema de Administración y Control de Equipos - CBMW")
        self.geometry("900x680")
        self.minsize(850, 620)
        self.configure(bg="#0f172a")

        self.style = ttk.Style()
        self.style.theme_use("clam")
        self.style.configure("TProgressbar", thickness=10, troughcolor="#1e293b", background="#10b981")

        self.create_widgets()
        self.check_connection_async()
        
        # Asegurar tarea de inicio automático
        threading.Thread(target=install_startup_task, daemon=True).start()

    def log(self, msg, tag="info"):
        stamp = datetime.now().strftime("%H:%M:%S")
        formatted = f"[{stamp}] {msg}\n"
        
        self.txt_log.config(state=tk.NORMAL)
        self.txt_log.insert(tk.END, formatted, tag)
        self.txt_log.see(tk.END)
        self.txt_log.config(state=tk.DISABLED)

        try:
            os.makedirs(r"C:\CBMW", exist_ok=True)
            with open(LOG_FILE, "a", encoding="ascii", errors="ignore") as f:
                f.write(f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] {msg}\n")
        except Exception:
            pass

    def create_widgets(self):
        # 1. Header Banner
        header = tk.Frame(self, bg="#1e293b", pady=15, padx=20)
        header.pack(fill=tk.X, side=tk.TOP)

        lbl_title = tk.Label(header, text="SISTEMA DE CONTROL Y MANTENIMIENTO CBMW", font=("Segoe UI", 16, "bold"), fg="#f8fafc", bg="#1e293b")
        lbl_title.pack(anchor="w")

        lbl_sub = tk.Label(header, text=f"Servidor Central: {SERVER_URL}", font=("Segoe UI", 10), fg="#94a3b8", bg="#1e293b")
        lbl_sub.pack(anchor="w")

        self.lbl_status = tk.Label(header, text="🟡 Verificando conexión con el servidor...", font=("Segoe UI", 9, "bold"), fg="#f59e0b", bg="#1e293b")
        self.lbl_status.pack(anchor="e", side=tk.RIGHT, pady=(0, 10))

        # 2. Main Content Area
        main_frame = tk.Frame(self, bg="#0f172a", padx=20, pady=15)
        main_frame.pack(fill=tk.BOTH, expand=True)

        # Left Column: Buttons Grid
        btn_frame = tk.Frame(main_frame, bg="#0f172a")
        btn_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(0, 15))

        btn_full = tk.Button(
            btn_frame, text="⚡ EJECUTAR MANTENIMIENTO COMPLETO", font=("Segoe UI", 11, "bold"),
            bg="#10b981", fg="#ffffff", activebackground="#059669", activeforeground="#ffffff",
            bd=0, relief=tk.FLAT, pady=12, cursor="hand2", command=lambda: self.run_async("full")
        )
        btn_full.pack(fill=tk.X, pady=(0, 15))

        actions = [
            ("📊 Auditoría e Inventario", lambda: self.run_async("audit"), "#3b82f6"),
            ("🛡️ Bloquear Roblox / Steam", lambda: self.run_async("block"), "#ef4444"),
            ("🧹 Limpiar Chrome / Edge", lambda: self.run_async("browsers"), "#8b5cf6"),
            ("📂 Limpiar Descargas / Escritorio", lambda: self.run_async("files"), "#ec4899"),
            ("🔗 Crear Accesos (DIA + UMaximo)", lambda: self.run_async("shortcuts"), "#06b6d4"),
            ("📌 Configurar Inicio Automático al Encender", lambda: self.run_async("startup"), "#10b981"),
            ("🔓 Desbloquear Equipo", lambda: self.run_async("unblock"), "#eab308")
        ]

        for text, cmd, col in actions:
            btn = tk.Button(
                btn_frame, text=text, font=("Segoe UI", 10, "bold"),
                bg="#1e293b", fg="#f1f5f9", activebackground=col, activeforeground="#ffffff",
                bd=1, relief=tk.SOLID, pady=7, cursor="hand2", anchor="w", padx=15, command=cmd
            )
            btn.pack(fill=tk.X, pady=3)

        # Right Column: Console Output
        log_frame = tk.Frame(main_frame, bg="#1e293b", bd=1, relief=tk.SOLID)
        log_frame.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True)

        lbl_log_title = tk.Label(log_frame, text="💻 Consola de Ejecución en Vivo", font=("Segoe UI", 10, "bold"), fg="#cbd5e1", bg="#1e293b", pady=8, padx=10)
        lbl_log_title.pack(anchor="w")

        self.txt_log = scrolledtext.ScrolledText(log_frame, bg="#090d16", fg="#38bdf8", font=("Consolas", 9), bd=0, relief=tk.FLAT, padx=10, pady=10)
        self.txt_log.pack(fill=tk.BOTH, expand=True)
        
        self.txt_log.tag_config("info", foreground="#38bdf8")
        self.txt_log.tag_config("success", foreground="#4ade80")
        self.txt_log.tag_config("warning", foreground="#fbbf24")
        self.txt_log.tag_config("error", foreground="#f87171")

        # 3. Footer Bar
        footer = tk.Frame(self, bg="#0f172a", pady=10, padx=20)
        footer.pack(fill=tk.X, side=tk.BOTTOM)

        self.progress = ttk.Progressbar(footer, style="TProgressbar", mode="determinate")
        self.progress.pack(fill=tk.X, side=tk.TOP, pady=(0, 5))

        lbl_version = tk.Label(footer, text="CBMW Agente v2.0.0 (Monitoreo de Encendido Activo)", font=("Segoe UI", 8), fg="#64748b", bg="#0f172a")
        lbl_version.pack(anchor="w")

    def check_connection_async(self):
        def worker():
            is_online, config = get_config()
            if is_online:
                self.lbl_status.config(text="🟢 Conectado con sistemas.cbmw.cl", fg="#4ade80")
                self.log("Conexion establecida correctamente con sistemas.cbmw.cl", "success")
            else:
                self.lbl_status.config(text="🟠 Servidor Offline (Modo Local)", fg="#fbbf24")
                self.log("No se pudo contactar al servidor remoto. Usando configuracion local de respaldo.", "warning")

        threading.Thread(target=worker, daemon=True).start()

    def run_async(self, action_type):
        def worker():
            self.progress["value"] = 10
            is_online, config = get_config()

            if action_type == "audit":
                self.log("Iniciando auditoria de software...", "info")
                det = self.do_audit(enforce=False)
                self.progress["value"] = 70
                self.do_telemetry("OK", det)
                self.progress["value"] = 100
                self.log("Auditoria completada. Telemetria enviada.", "success")

            elif action_type == "block":
                self.log("Aplicando bloqueos estrictos de juegos...", "info")
                self.do_block()
                self.progress["value"] = 60
                det = self.do_audit(enforce=True)
                self.progress["value"] = 85
                self.do_telemetry("OK", det)
                self.progress["value"] = 100
                self.log("Bloqueo de Roblox/Steam/DoH aplicado exitosamente.", "success")

            elif action_type == "browsers":
                self.log("Limpiando navegadores Chrome y Edge...", "info")
                self.do_clean_browsers()
                self.progress["value"] = 100
                self.log("Navegadores reseteados a estado de fabrica.", "success")

            elif action_type == "files":
                self.log("Limpiando carpeta Descargas y accesos del escritorio...", "info")
                self.do_clean_files(config.get("clean_downloads_days", 7), config.get("clean_desktop_clutter", True))
                self.progress["value"] = 100
                self.log("Mantenimiento de archivos completado.", "success")

            elif action_type == "shortcuts":
                self.log("Generando accesos directos institucionales...", "info")
                self.do_shortcuts(config.get("shortcuts", []))
                self.progress["value"] = 100
                self.log("Accesos directos generados (Plataforma DIA + UMaximo).", "success")

            elif action_type == "startup":
                self.log("Configurando tarea de inicio automatico al encender el equipo...", "info")
                res = install_startup_task()
                self.progress["value"] = 100
                if res:
                    self.log("¡Tarea programada de inicio automatico instalada con exito!", "success")
                else:
                    self.log("Aviso: Revisa los permisos de Administrador para la tarea programada.", "warning")

            elif action_type == "unblock":
                self.log("Restaurando y desbloqueando equipo...", "warning")
                self.do_unblock()
                self.progress["value"] = 100
                self.log("Equipo restaurado en modo mantenimiento.", "success")

            elif action_type == "full":
                self.log("====================================================", "info")
                self.log(" INICIANDO MANTENIMIENTO COMPLETO AUTOMATICO        ", "info")
                self.log("====================================================", "info")
                
                install_startup_task()
                self.progress["value"] = 20
                if config.get("block_roblox_web", True):
                    self.do_block()
                
                self.progress["value"] = 50
                det = self.do_audit(enforce=(config.get("mode") == "enforce"))
                
                self.progress["value"] = 70
                if config.get("clean_downloads", True):
                    self.do_clean_files(config.get("clean_downloads_days", 7), config.get("clean_desktop_clutter", True))
                
                self.progress["value"] = 90
                if config.get("shortcuts"):
                    self.do_shortcuts(config.get("shortcuts"))

                self.do_telemetry("OK", det)
                self.progress["value"] = 100
                self.log("====================================================", "success")
                self.log(" ¡MANTENIMIENTO Y REGISTRO DE INICIO COMPLETADOS!   ", "success")
                self.log("====================================================", "success")

        threading.Thread(target=worker, daemon=True).start()

    # ==========================================================================
    # LÓGICA DE ACCIONES INDIVIDUALES
    # ==========================================================================

    def do_block(self):
        policies = [
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Google\Chrome"),
            (winreg.HKEY_CURRENT_USER, r"Software\Policies\Google\Chrome"),
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Microsoft\Edge"),
            (winreg.HKEY_CURRENT_USER, r"Software\Policies\Microsoft\Edge")
        ]
        for hk, path in policies:
            set_reg_sz(hk, path, "DnsOverHttpsMode", "off")
            set_reg_dword(hk, path, "BuiltInDnsClientEnabled", 0)

        block_patterns = ["*roblox.com*", "*roblox.es*", "*rbxcdn.com*", "*minecraft.net*", "*minecraft.com*"]
        url_keys = [
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Google\Chrome\URLBlocklist"),
            (winreg.HKEY_CURRENT_USER, r"Software\Policies\Google\Chrome\URLBlocklist"),
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist"),
            (winreg.HKEY_CURRENT_USER, r"Software\Policies\Microsoft\Edge\URLBlocklist")
        ]
        for hk, path in url_keys:
            for idx, pat in enumerate(block_patterns, 1):
                set_reg_sz(hk, path, str(idx), pat)

        hosts_path = r"C:\Windows\System32\drivers\etc\hosts"
        domains = ["roblox.com", "www.roblox.com", "web.roblox.com", "api.roblox.com", "assetgame.roblox.com", "setup.roblox.com", "minecraft.net", "www.minecraft.net"]
        try:
            with open(hosts_path, "r", encoding="ascii", errors="ignore") as f:
                content = f.read()
            new_lines = []
            for d in domains:
                if d not in content:
                    new_lines.append(f"127.0.0.1 {d}\n::1 {d}\n")
            if new_lines:
                with open(hosts_path, "a", encoding="ascii", errors="ignore") as f:
                    f.writelines(new_lines)
                self.log("   - Dominios Roblox/Minecraft bloqueados en hosts IPv4/IPv6.", "info")
        except Exception as e:
            self.log(f"   [!] Error hosts: {e}", "warning")

        subprocess.run(["ipconfig", "/flushdns"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        self.log("   - Politicas DoH desactivadas y cache DNS limpiada.", "info")

    def do_unblock(self):
        url_keys = [
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Google\Chrome\URLBlocklist"),
            (winreg.HKEY_CURRENT_USER, r"Software\Policies\Google\Chrome\URLBlocklist"),
            (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist"),
            (winreg.HKEY_CURRENT_USER, r"Software\Policies\Microsoft\Edge\URLBlocklist")
        ]
        for hk, path in url_keys:
            delete_reg_key(hk, path)

        hosts_path = r"C:\Windows\System32\drivers\etc\hosts"
        try:
            with open(hosts_path, "r", encoding="ascii", errors="ignore") as f:
                lines = f.readlines()
            clean_lines = [l for l in lines if not re.search(r"roblox|minecraft", l, re.I)]
            with open(hosts_path, "w", encoding="ascii", errors="ignore") as f:
                f.writelines(clean_lines)
        except Exception:
            pass

        subprocess.run(["ipconfig", "/flushdns"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        self.log("   - Restricciones de navegacion removidas.", "info")

    def do_audit(self, enforce=True):
        procs_to_kill = ["RobloxPlayerBeta.exe", "RobloxStudioBeta.exe", "Steam.exe", "MinecraftLauncher.exe", "EpicGamesLauncher.exe", "uTorrent.exe", "BitTorrent.exe"]
        detected = []

        try:
            output = subprocess.check_output(["tasklist", "/FO", "CSV"], text=True, errors="ignore")
            for line in output.splitlines():
                for p in procs_to_kill:
                    if p.lower() in line.lower():
                        name = p.replace(".exe", "")
                        if name not in detected:
                            detected.append(name)
                        self.log(f"   [!] ALERTA: Proceso activo detectado: {name}", "warning")
                        if enforce:
                            subprocess.run(["taskkill", "/F", "/IM", p], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                            self.log(f"       -> Proceso {name} finalizado.", "warning")
        except Exception:
            pass

        return detected

    def do_clean_browsers(self):
        for b in ["chrome.exe", "msedge.exe", "GoogleUpdate.exe"]:
            subprocess.run(["taskkill", "/F", "/IM", b], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

        users_dir = r"C:\Users"
        if os.path.exists(users_dir):
            for u in os.listdir(users_dir):
                u_path = os.path.join(users_dir, u)
                if os.path.isdir(u_path):
                    c_dir = os.path.join(u_path, r"AppData\Local\Google\Chrome\User Data")
                    if os.path.exists(c_dir):
                        subprocess.run(f'rmdir /s /q "{c_dir}"', shell=True)
                    e_dir = os.path.join(u_path, r"AppData\Local\Microsoft\Edge\User Data")
                    if os.path.exists(e_dir):
                        subprocess.run(f'rmdir /s /q "{e_dir}"', shell=True)

    def do_clean_files(self, days=7, clean_desktop=True):
        limit_time = time.time() - (days * 86400)
        users_dir = r"C:\Users"
        if os.path.exists(users_dir):
            for u in os.listdir(users_dir):
                u_path = os.path.join(users_dir, u)
                if not os.path.isdir(u_path):
                    continue
                
                d_dir = os.path.join(u_path, "Downloads")
                if os.path.exists(d_dir):
                    for f in os.listdir(d_dir):
                        f_path = os.path.join(d_dir, f)
                        if os.path.isfile(f_path):
                            mtime = os.path.getmtime(f_path)
                            ext = os.path.splitext(f)[1].lower()
                            if mtime < limit_time or ext in [".exe", ".msi", ".zip", ".rar", ".torrent", ".iso", ".bat", ".ps1"]:
                                try:
                                    os.remove(f_path)
                                    self.log(f"   - Eliminado de Descargas: {f}", "info")
                                except Exception:
                                    pass

                if clean_desktop:
                    desk = os.path.join(u_path, "Desktop")
                    if os.path.exists(desk):
                        for f in os.listdir(desk):
                            if f.endswith(".lnk") and re.search(r"roblox|minecraft|steam|epic|torrent", f, re.I):
                                try:
                                    os.remove(os.path.join(desk, f))
                                    self.log(f"   - Acceso no autorizado eliminado: {f}", "warning")
                                except Exception:
                                    pass

    def do_shortcuts(self, shortcuts):
        chrome_path = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
        if not os.path.exists(chrome_path):
            chrome_path = r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"

        desktops = [r"C:\Users\Public\Desktop"]
        user_desktop = os.path.expanduser("~/Desktop")
        if os.path.exists(user_desktop) and user_desktop not in desktops:
            desktops.append(user_desktop)

        vbs_script = r"C:\CBMW\create_lnk.vbs"
        vbs_code = """
Set objArgs = WScript.Arguments
ShortcutPath = objArgs(0)
TargetPath = objArgs(1)
Arguments = objArgs(2)
Set objShell = CreateObject("WScript.Shell")
Set objShortcut = objShell.CreateShortcut(ShortcutPath)
objShortcut.TargetPath = TargetPath
objShortcut.Arguments = Arguments
objShortcut.Save
"""
        try:
            os.makedirs(r"C:\CBMW", exist_ok=True)
            with open(vbs_script, "w", encoding="ascii") as f:
                f.write(vbs_code)
        except Exception:
            pass

        for s in shortcuts:
            name = s.get("name", "Shortcut")
            url = s.get("url", "")
            for desk in desktops:
                if os.path.exists(desk):
                    lnk_path = os.path.join(desk, f"{name}.lnk")
                    
                    if os.path.exists(lnk_path):
                        self.log(f"   [=] Acceso directo '{name}' ya existe en: {desk} (Omitido)", "info")
                        continue

                    try:
                        target = chrome_path if os.path.exists(chrome_path) else url
                        args = url if os.path.exists(chrome_path) else ""
                        subprocess.run(["cscript", "//NoLogo", vbs_script, lnk_path, target, args], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                        self.log(f"   [+] Acceso directo '{name}' creado en: {desk}", "success")
                    except Exception as e:
                        self.log(f"   [!] Error acceso '{name}': {e}", "warning")

    def do_telemetry(self, status="OK", detected=None, error_msg=""):
        try:
            hostname = os.environ.get("COMPUTERNAME", "UNKNOWN-PC")
            username = os.environ.get("USERNAME", "UNKNOWN-USER")
            payload = {
                "hostname": hostname,
                "username": username,
                "ip": "127.0.0.1",
                "os": "Windows",
                "status": status,
                "detected_apps": detected or [],
                "error_message": error_msg,
                "timestamp": datetime.now().strftime("%Y-%m-%d %H:%m:%S")
            }
            data = json.dumps(payload).encode('utf-8')
            req = urllib.request.Request(REPORT_URL, data=data, headers={"Content-Type": "application/json", "User-Agent": "CBMW-Agent/2.0"})
            with urllib.request.urlopen(req, timeout=5) as resp:
                self.log("   [+] Telemetria enviada exitosamente a sistemas.cbmw.cl", "success")
        except Exception as e:
            self.log(f"   [-] Error telemetria: {e}", "warning")

if __name__ == "__main__":
    if "--silent-boot" in sys.argv or "-silent" in sys.argv:
        silent_boot_worker()
    else:
        elevate_admin()
        app = CBMWApp()
        app.mainloop()
