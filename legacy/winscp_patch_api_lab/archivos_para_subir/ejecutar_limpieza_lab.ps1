# Script de Limpieza Radical de Navegadores (Reset de Fábrica de User Data)
$ErrorActionPreference = "Continue"

$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = (Get-Item -Path ".\").FullName
}

$global:logFile = Join-Path $scriptDir "lab_log.txt"
"====================================================" | Out-File -FilePath $global:logFile -Encoding ASCII -Force
"LOG DE LIMPIEZA RADICAL - $(Get-Date)" | Out-File -FilePath $global:logFile -Append -Encoding ASCII

function Log-Msg {
    param([string]$Text, [string]$Color = "White")
    Write-Host $Text -ForegroundColor $Color
    $stamp = Get-Date -Format "HH:mm:ss"
    "[$stamp] $Text" | Out-File -FilePath $global:logFile -Append -Encoding ASCII -ErrorAction SilentlyContinue
}

function Crear-AccesoDirectoChrome {
    param(
        [string]$NombreAcceso = "Plataforma DIA",
        [string]$Url = "https://dia.agenciaeducacion.cl/login"
    )
    
    $chromePath = "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
    if (-not (Test-Path $chromePath)) {
        $chromePath = "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
    }

    $desktops = @(
        [Environment]::GetFolderPath("CommonDesktop"),
        [Environment]::GetFolderPath("Desktop")
    ) | Select-Object -Unique

    $wsh = New-Object -ComObject WScript.Shell

    foreach ($desk in $desktops) {
        if (Test-Path $desk) {
            try {
                $lnkPath = Join-Path $desk "$NombreAcceso.lnk"
                $lnk = $wsh.CreateShortcut($lnkPath)
                if (Test-Path $chromePath) {
                    $lnk.TargetPath = $chromePath
                    $lnk.Arguments = $Url
                    $lnk.IconLocation = "$chromePath,0"
                } else {
                    $lnk.TargetPath = $Url
                }
                $lnk.Description = "Acceso directo a Plataforma DIA - Agencia de Educacion"
                $lnk.Save()
                Log-Msg "   - Acceso directo '$NombreAcceso' creado en: $desk" "Green"
            } catch {
                # Se ignora si falta permiso de admin para el escritorio público
            }
        }
    }
}

function Eliminar-RobloxYMinecraft {
    Log-Msg "   - Cerrando procesos de Roblox y Minecraft..." "Yellow"
    Get-Process -Name "RobloxPlayerBeta*", "RobloxStudio*", "Minecraft*", "javaw*", "MinecraftLauncher*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

    Log-Msg "   - Eliminando carpetas de Roblox y Minecraft en AppData de todos los usuarios..." "Yellow"
    Get-ChildItem -Path "$env:SystemDrive\Users" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $uPath = $_.FullName
        
        # Roblox
        $rLocal = Join-Path $uPath "AppData\Local\Roblox"
        if (Test-Path $rLocal) { Remove-Item -Path $rLocal -Recurse -Force -ErrorAction SilentlyContinue }
        
        $rLocalLow = Join-Path $uPath "AppData\LocalLow\RbxLogs"
        if (Test-Path $rLocalLow) { Remove-Item -Path $rLocalLow -Recurse -Force -ErrorAction SilentlyContinue }

        $rRoaming = Join-Path $uPath "AppData\Roaming\Roblox"
        if (Test-Path $rRoaming) { Remove-Item -Path $rRoaming -Recurse -Force -ErrorAction SilentlyContinue }

        $rStart = Join-Path $uPath "AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Roblox"
        if (Test-Path $rStart) { Remove-Item -Path $rStart -Recurse -Force -ErrorAction SilentlyContinue }

        # Minecraft
        $mRoaming = Join-Path $uPath "AppData\Roaming\.minecraft"
        if (Test-Path $mRoaming) { Remove-Item -Path $mRoaming -Recurse -Force -ErrorAction SilentlyContinue }

        $mLocalProg = Join-Path $uPath "AppData\Local\Programs\Minecraft Launcher"
        if (Test-Path $mLocalProg) { Remove-Item -Path $mLocalProg -Recurse -Force -ErrorAction SilentlyContinue }

        # Accesos directos en escritorio del usuario
        $uDesktop = Join-Path $uPath "Desktop"
        if (Test-Path $uDesktop) {
            Get-ChildItem -Path $uDesktop -Filter "*Roblox*.lnk" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
            Get-ChildItem -Path $uDesktop -Filter "*Minecraft*.lnk" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
        }
    }

    # Accesos directos en escritorio público
    $publicDesk = [Environment]::GetFolderPath("CommonDesktop")
    if (Test-Path $publicDesk) {
        Get-ChildItem -Path $publicDesk -Filter "*Roblox*.lnk" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
        Get-ChildItem -Path $publicDesk -Filter "*Minecraft*.lnk" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

function Bloquear-RobloxYMinecraftNavegador {
    Log-Msg "   - Aplicando bloqueo de URL para Roblox y Minecraft en Chrome y Edge..." "Yellow"

    $blockPatterns = @(
        "*roblox.com*",
        "*roblox.es*",
        "*rbxcdn.com*",
        "*minecraft.net*",
        "*minecraft.com*"
    )

    $policyPaths = @(
        "HKLM:\SOFTWARE\Policies\Google\Chrome\URLBlocklist",
        "HKCU:\Software\Policies\Google\Chrome\URLBlocklist",
        "HKLM:\SOFTWARE\Policies\Microsoft\Edge\URLBlocklist",
        "HKCU:\Software\Policies\Microsoft\Edge\URLBlocklist"
    )

    foreach ($keyPath in $policyPaths) {
        if (-not (Test-Path $keyPath)) { New-Item -Path $keyPath -Force | Out-Null }
        $i = 1
        foreach ($pattern in $blockPatterns) {
            Set-ItemProperty -Path $keyPath -Name "$i" -Value $pattern -Type String -Force -ErrorAction SilentlyContinue
            $i++
        }
    }

    try {
        $hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
        if (Test-Path $hostsPath) {
            $hostsContent = Get-Content -Path $hostsPath -Raw -ErrorAction SilentlyContinue
            $domainsToBlock = @(
                "roblox.com",
                "www.roblox.com",
                "web.roblox.com",
                "api.roblox.com",
                "minecraft.net",
                "www.minecraft.net"
            )
            $newEntries = ""
            foreach ($dom in $domainsToBlock) {
                if ($hostsContent -notmatch [regex]::Escape($dom)) {
                    $newEntries += "`r`n127.0.0.1 $dom"
                }
            }
            if (![string]::IsNullOrWhiteSpace($newEntries)) {
                Add-Content -Path $hostsPath -Value $newEntries -ErrorAction SilentlyContinue
            }
        }
    } catch {}
}

Log-Msg "====================================================" "Cyan"
Log-Msg "  LIMPIEZA RADICAL Y BLOQUEO DE JUEGOS EN NAVEGADOR " "Cyan"
Log-Msg "====================================================" "Cyan"
Log-Msg ""

try {
    Log-Msg "[1/6] Cerrando todos los procesos de Chrome, Edge, Roblox y Minecraft..." "Yellow"
    Get-Process -Name "chrome*", "msedge*", "GoogleUpdate*", "GoogleCrashHandler*", "RobloxPlayerBeta*", "RobloxStudio*", "Minecraft*", "javaw*", "MinecraftLauncher*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    # 2. Borrado Radical de User Data en el usuario actual
    Log-Msg "[2/6] Borrando carpeta completa 'User Data' de Chrome en AppData..." "Yellow"
    $chromeUserData = "$env:LocalAppData\Google\Chrome\User Data"
    if (Test-Path $chromeUserData) {
        Remove-Item -Path $chromeUserData -Recurse -Force -ErrorAction SilentlyContinue
        Log-Msg "Carpeta 'User Data' de Chrome eliminada por completo." "Green"
    } else {
        Log-Msg "No se encontro carpeta 'User Data' de Chrome en el usuario actual." "Gray"
    }

    $edgeUserData = "$env:LocalAppData\Microsoft\Edge\User Data"
    if (Test-Path $edgeUserData) {
        Remove-Item -Path $edgeUserData -Recurse -Force -ErrorAction SilentlyContinue
        Log-Msg "Carpeta 'User Data' de Edge eliminada por completo." "Green"
    }

    # 3. Borrado Radical en todos los demás perfiles de C:\Users\*
    Log-Msg "[3/6] Buscando y borrando 'User Data' en todos los usuarios de C:\Users..." "Yellow"
    Get-ChildItem -Path "$env:SystemDrive\Users" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $uPath = $_.FullName
        $cDir = Join-Path $uPath "AppData\Local\Google\Chrome\User Data"
        if (Test-Path $cDir) {
            Log-Msg "   - Borrando User Data en: $uPath" "Gray"
            Remove-Item -Path $cDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        $eDir = Join-Path $uPath "AppData\Local\Microsoft\Edge\User Data"
        if (Test-Path $eDir) {
            Remove-Item -Path $eDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    # 4. Eliminación de Roblox y Minecraft
    Log-Msg "[4/6] Eliminando instaladores, carpetas y accesos directos de Roblox y Minecraft..." "Yellow"
    Eliminar-RobloxYMinecraft

    # 5. Bloqueo de Roblox y Minecraft en Chrome y Edge
    Log-Msg "[5/6] Aplicando bloqueo de Roblox y Minecraft en Chrome y sistema..." "Yellow"
    Bloquear-RobloxYMinecraftNavegador

    # 6. Crear Acceso Directo de Chrome para Plataforma DIA
    Log-Msg "[6/6] Creando acceso directo de Chrome para Plataforma DIA..." "Yellow"
    Crear-AccesoDirectoChrome -NombreAcceso "Plataforma DIA" -Url "https://dia.agenciaeducacion.cl/login"

    Log-Msg ""
    Log-Msg "====================================================" "Green"
    Log-Msg " ¡LIMPIEZA Y BLOQUEO COMPLETADOS CON EXITO!        " "Green"
    Log-Msg "====================================================" "Green"
    Log-Msg "- Se eliminaron por completo las carpetas User Data de navegadores." "Green"
    Log-Msg "- Se eliminaron instaladores y carpetas AppData de Roblox y Minecraft." "Green"
    Log-Msg "- Se bloqueo roblox.com y minecraft.net en Chrome, Edge y sistema." "Green"
    Log-Msg "- Se creo el acceso directo 'Plataforma DIA' de Chrome en el Escritorio." "Green"

} catch {
    Log-Msg "" "Red"
    Log-Msg "[ERROR EN LIMPIEZA] $_" "Red"
}

Log-Msg ""
Log-Msg "Presiona la tecla ENTER para cerrar esta ventana..." "Yellow"
[void][System.Console]::ReadLine()
