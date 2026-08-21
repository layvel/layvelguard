# -*- coding: utf-8 -*-
"""
AGENTE Y MENÚ DE ADMINISTRACIÓN Y CONTROL CBMW
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
from datetime import datetime, timedelta

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
        print("Solicitando permisos de Administrador...")
        script = os.path.abspath(sys.argv[0])
        params = " ".join([f'"{a}"' for a in sys.argv[1:]])
        ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable if script.endswith('.py') else script, f'"{script}" {params}' if script.endswith('.py') else params, None, 1)
        sys.exit(0)

def log_msg(text, color=None):
    stamp = datetime.now().strftime("%Y-%m-%d %H:%m:%S")
    line = f"[{stamp}] {text}"
    print(text)
    try:
        os.makedirs(r"C:\CBMW", exist_ok=True)
        with open(LOG_FILE, "a", encoding="ascii", errors="ignore") as f:
            f.write(line + "\n")
    except Exception:
        pass

def get_config():
    try:
        req = urllib.request.Request(f"{CONFIG_URL}?t={int(time.time())}", headers={"Cache-Control": "no-cache", "User-Agent": "CBMW-Agent/2.0"})
        with urllib.request.urlopen(req, timeout=5) as resp:
            data = json.loads(resp.read().decode('utf-8'))
            log_msg("   [+] Configuracion remota cargada desde el servidor central.", "green")
            return data
    except Exception as e:
        log_msg(f"   [-] No se pudo conectar al servidor remoto. ({e}) Usando config local.", "yellow")
    
    local_json = r"C:\CBMW\lab-config.json"
    if os.path.exists(local_json):
        try:
            with open(local_json, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass

    return {
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

def block_roblox_web():
    log_msg("====================================================")
    log_msg(" APLICANDO BLOQUEO WEB (ROBLOX / MINECRAFT / DOH)  ")
    log_msg("====================================================")

    # 1. Disable DoH in Chrome and Edge
    policies = [
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Google\Chrome"),
        (winreg.HKEY_CURRENT_USER, r"Software\Policies\Google\Chrome"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Policies\Microsoft\Edge"),
        (winreg.HKEY_CURRENT_USER, r"Software\Policies\Microsoft\Edge")
    ]
    for hk, path in policies:
        set_reg_sz(hk, path, "DnsOverHttpsMode", "off")
        set_reg_dword(hk, path, "BuiltInDnsClientEnabled", 0)

    # 2. URLBlocklist for Chrome and Edge
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

    # 3. Hosts File (IPv4 & IPv6)
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
            log_msg("   - Dominios agregados a hosts IPv4/IPv6.")
    except Exception as e:
        log_msg(f"   [!] Advertencia hosts: {e}")

    # 4. Flush DNS
    subprocess.run(["ipconfig", "/flushdns"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    log_msg("   - Cache DNS limpiada exitosamente.")

def unblock_equipment():
    log_msg("====================================================")
    log_msg(" DESBLOQUEANDO Y RESTAURANDO CONFIGURACION          ")
    log_msg("====================================================")

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
        log_msg("   - Entradas de Roblox/Minecraft removidas de hosts.")
    except Exception:
        pass

    subprocess.run(["ipconfig", "/flushdns"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    log_msg("   - Equipo desbloqueado exitosamente.")

def audit_and_kill(enforce=True):
    log_msg("====================================================")
    log_msg(" ESCANEANDO Y DETECTANDO SOFTWARE NO AUTORIZADO    ")
    log_msg("====================================================")

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
                    log_msg(f"   [!] ALERTA: Proceso activo detectado: {name}")
                    if enforce:
                        subprocess.run(["taskkill", "/F", "/IM", p], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                        log_msg(f"       -> Proceso {name} finalizado.")
    except Exception:
        pass

    return detected

def clean_browsers():
    log_msg("====================================================")
    log_msg(" LIMPIEZA RADICAL DE NAVEGADORES (CHROME / EDGE)    ")
    log_msg("====================================================")

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

    log_msg("   - Perfiles de navegadores limpiados a estado de fabrica.")

def clean_downloads_and_desktop(days=7, clean_desktop=True):
    log_msg("====================================================")
    log_msg(" MANTENIMIENTO: CARPETA DESCARGAS Y ESCRITORIO      ")
    log_msg("====================================================")

    limit_time = time.time() - (days * 86400)
    users_dir = r"C:\Users"
    if os.path.exists(users_dir):
        for u in os.listdir(users_dir):
            u_path = os.path.join(users_dir, u)
            if not os.path.isdir(u_path):
                continue
            
            # Downloads
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
                                log_msg(f"   - Eliminado de Descargas: {f}")
                            except Exception:
                                pass

            # Desktop Shortcuts
            if clean_desktop:
                desk = os.path.join(u_path, "Desktop")
                if os.path.exists(desk):
                    for f in os.listdir(desk):
                        if f.endswith(".lnk") and re.search(r"roblox|minecraft|steam|epic|torrent", f, re.I):
                            try:
                                os.remove(os.path.join(desk, f))
                                log_msg(f"   - Acceso no autorizado eliminado del escritorio: {f}")
                            except Exception:
                                pass

def create_shortcuts(shortcuts):
    log_msg("====================================================")
    log_msg(" GENERANDO ACCESOS DIRECTOS INSTITUCIONALES         ")
    log_msg("====================================================")

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
                
                # ANTI-DUPLICATES CHECK
                if os.path.exists(lnk_path):
                    log_msg(f"   [=] Acceso directo '{name}' ya existe en: {desk} (Omitido)")
                    continue

                try:
                    if os.path.exists(chrome_path):
                        target = chrome_path
                        args = url
                    else:
                        target = url
                        args = ""
                    
                    subprocess.run(["cscript", "//NoLogo", vbs_script, lnk_path, target, args], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                    log_msg(f"   [+] Acceso directo '{name}' creado exitosamente en: {desk}")
                except Exception as e:
                    log_msg(f"   [!] Error creando acceso '{name}': {e}")

def send_telemetry(status="OK", detected=None, error_msg=""):
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
            log_msg("   [+] Reporte de telemetria enviado exitosamente al servidor central.")
    except Exception as e:
        log_msg(f"   [-] No se pudo enviar el reporte telemétrico: {e}")

def test_server():
    print("\n============================================================")
    print(" PROBANDO CONEXION CON SERVIDOR (sistemas.cbmw.cl)")
    print("============================================================")
    try:
        req = urllib.request.Request(f"{CONFIG_URL}?t={int(time.time())}", headers={"Cache-Control": "no-cache", "User-Agent": "CBMW-Agent/2.0"})
        with urllib.request.urlopen(req, timeout=5) as resp:
            data = resp.read().decode('utf-8')
            print("\n" + data + "\n")
            print("CONEXION EXITOSA. Servidor sistemas.cbmw.cl respondiendo correctamente.")
    except Exception as e:
        print(f"\nERROR CONECTANDO AL SERVIDOR: {e}")
    input("\nPresione una tecla para continuar . . . ")

def main_menu():
    elevate_admin()
    while True:
        os.system("cls" if os.name == "nt" else "clear")
        print("============================================================")
        print("        SISTEMA DE ADMINISTRACION Y CONTROL DE EQUIPOS - CBMW")
        print("============================================================")
        print("  Servidor Central: https://sistemas.cbmw.cl")
        print("============================================================")
        print()
        print("  [1] Auditoria e Inventario (Reportar estado al servidor)")
        print("  [2] Aplicar Bloqueos Estrictos (Roblox Web/App, Steam, Games)")
        print("  [3] Limpieza Radical de Navegadores (Reset Chrome y Edge)")
        print("  [4] Limpiar Carpeta Descargas y Archivos del Escritorio")
        print("  [5] Crear Accesos Directos (Plataforma DIA + UMaximo)")
        print("  [6] Restaurar / Desbloquear Equipo (Modo Mantenimiento)")
        print("  [7] Probar Conexion con Servidor (sistemas.cbmw.cl)")
        print("  [8] EJECUTAR MANTENIMIENTO COMPLETO AUTOMATICO")
        print("  [0] Salir")
        print()
        print("============================================================")
        opcion = input(" Seleccione una opcion [0-8]: ").strip()

        config = get_config()

        if opcion == "1":
            os.system("cls")
            print("============================================================")
            print("  EJECUTANDO AUDITORIA E INVENTARIO")
            print("============================================================")
            det = audit_and_kill(enforce=False)
            send_telemetry(status="OK", detected=det)
            input("\nPresione ENTER para continuar...")

        elif opcion == "2":
            os.system("cls")
            block_roblox_web()
            det = audit_and_kill(enforce=True)
            send_telemetry(status="OK", detected=det)
            input("\nPresione ENTER para continuar...")

        elif opcion == "3":
            os.system("cls")
            clean_browsers()
            input("\nPresione ENTER para continuar...")

        elif opcion == "4":
            os.system("cls")
            clean_downloads_and_desktop(days=config.get("clean_downloads_days", 7), clean_desktop=config.get("clean_desktop_clutter", True))
            input("\nPresione ENTER para continuar...")

        elif opcion == "5":
            os.system("cls")
            create_shortcuts(config.get("shortcuts", []))
            input("\nPresione ENTER para continuar...")

        elif opcion == "6":
            os.system("cls")
            unblock_equipment()
            input("\nPresione ENTER para continuar...")

        elif opcion == "7":
            test_server()

        elif opcion == "8":
            os.system("cls")
            print("============================================================")
            print("  EJECUTANDO MANTENIMIENTO Y CONTROL COMPLETO")
            print("============================================================")
            if config.get("block_roblox_web", True):
                block_roblox_web()
            det = audit_and_kill(enforce=(config.get("mode") == "enforce"))
            if config.get("clean_downloads", True):
                clean_downloads_and_desktop(days=config.get("clean_downloads_days", 7), clean_desktop=config.get("clean_desktop_clutter", True))
            if config.get("shortcuts"):
                create_shortcuts(config.get("shortcuts"))
            send_telemetry(status="OK", detected=det)
            print("\n====================================================")
            print(" MANTENIMIENTO Y AUDITORIA COMPLETADOS CON EXITO")
            print("====================================================")
            input("\nPresione ENTER para continuar...")

        elif opcion == "0":
            sys.exit(0)

if __name__ == "__main__":
    main_menu()
