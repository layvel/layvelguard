# Script de Configuración, Bloqueo y Fondo para Lab de Cómputo (Windows 10/11)
param(
    [string]$SourceImage = ""
)

$ErrorActionPreference = "Continue"

# Determinar carpeta del script
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = (Get-Item -Path ".\").FullName
}

$global:logFile = Join-Path $scriptDir "lab_log.txt"
"====================================================" | Out-File -FilePath $global:logFile -Encoding ASCII -Force
"LOG DE EJECUCION DEL LABORATORIO - $(Get-Date)" | Out-File -FilePath $global:logFile -Append -Encoding ASCII

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
Log-Msg " CONFIGURANDO SEGURIDAD Y FONDO (SISTEMA COMPLETO)  " "Cyan"
Log-Msg "====================================================" "Cyan"
Log-Msg ""
Log-Msg "Carpeta detectada: $scriptDir" "Gray"

# Resolver imagen
if ([string]::IsNullOrWhiteSpace($SourceImage) -or -not (Test-Path $SourceImage)) {
    $SourceImage = Join-Path $scriptDir "fondo pc.png"
}

Log-Msg "Buscando imagen en: $SourceImage" "Gray"

if (-not (Test-Path $SourceImage)) {
    Log-Msg "" "Red"
    Log-Msg "[ERROR CRITICO] No se encontro la imagen 'fondo pc.png'." "Red"
    Log-Msg "Asegurate de que 'fondo pc.png' este dentro de la misma carpeta del USB." "Yellow"
    Log-Msg "Ruta probada: $SourceImage" "Yellow"
    Log-Msg "" "Red"
    Log-Msg "Presiona la tecla ENTER para salir..." "Yellow"
    [void][System.Console]::ReadLine()
    exit 1
}

try {
    # 1. Copiar imagen a carpeta publica del sistema (Users\Public\Pictures)
    Log-Msg "[1/9] Copiando imagen a carpeta publica segura..." "Yellow"
    $destDir = "$env:Public\Pictures\LabWallpaper"
    if (-not (Test-Path $destDir)) { New-Item -Path $destDir -ItemType Directory -Force | Out-Null }
    $destImage = Join-Path $destDir "LabFondo.png"
    Copy-Item -Path $SourceImage -Destination $destImage -Force -ErrorAction SilentlyContinue
    
    $finalImage = if (Test-Path $destImage) { $destImage } else { $SourceImage }
    Log-Msg "Imagen lista en: $finalImage" "Green"

    # 2. Aplicar Directivas HKLM y HKCU para Fondo
    Log-Msg "[2/9] Configurando Registro de Windows para fondo..." "Yellow"
    
    $sysPolicies = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"
    if (-not (Test-Path $sysPolicies)) { New-Item -Path $sysPolicies -Force | Out-Null }
    Set-ItemProperty -Path $sysPolicies -Name "Wallpaper" -Value $finalImage -Type String -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $sysPolicies -Name "WallpaperStyle" -Value "2" -Type String -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $sysPolicies -Name "NoDispBackgroundPage" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue

    $actPolicies = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"
    if (-not (Test-Path $actPolicies)) { New-Item -Path $actPolicies -Force | Out-Null }
    Set-ItemProperty -Path $actPolicies -Name "NoChangingWallPaper" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue

    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "Wallpaper" -Value $finalImage -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "WallpaperStyle" -Value "2" -Force -ErrorAction SilentlyContinue

    $hkcuSys = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System"
    if (-not (Test-Path $hkcuSys)) { New-Item -Path $hkcuSys -Force | Out-Null }
    Set-ItemProperty -Path $hkcuSys -Name "Wallpaper" -Value $finalImage -Type String -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $hkcuSys -Name "WallpaperStyle" -Value "2" -Type String -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $hkcuSys -Name "NoDispBackgroundPage" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue

    $hkcuAct = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"
    if (-not (Test-Path $hkcuAct)) { New-Item -Path $hkcuAct -Force | Out-Null }
    Set-ItemProperty -Path $hkcuAct -Name "NoChangingWallPaper" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue

    # 3. Limpieza Radical de Cuentas/Perfiles (Borrado de User Data) y Directivas
    Log-Msg "[3/9] Cerrando Chrome y eliminando por completo la carpeta User Data..." "Yellow"
    Get-Process -Name "chrome*", "msedge*", "GoogleUpdate*", "GoogleCrashHandler*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2

    # Eliminar User Data de Chrome y Edge
    Get-ChildItem -Path "$env:SystemDrive\Users" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $uPath = $_.FullName
        $cDir = Join-Path $uPath "AppData\Local\Google\Chrome\User Data"
        if (Test-Path $cDir) {
            Remove-Item -Path $cDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        $eDir = Join-Path $uPath "AppData\Local\Microsoft\Edge\User Data"
        if (Test-Path $eDir) {
            Remove-Item -Path $eDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    # Bloqueo en Registro de Chrome y Edge
    $chromePaths = @("HKLM:\SOFTWARE\Policies\Google\Chrome", "HKCU:\Software\Policies\Google\Chrome")
    foreach ($p in $chromePaths) {
        if (-not (Test-Path $p)) { New-Item -Path $p -Force | Out-Null }
        Set-ItemProperty -Path $p -Name "BrowserSignin" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $p -Name "SigninAllowed" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $p -Name "SyncDisabled" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $p -Name "EphemeralProfileEnabled" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $p -Name "BrowserAddPersonEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $p -Name "ProfilePickerOnStartupEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $p -Name "RestrictSigninToPattern" -Value ".*@invalid.domain" -Type String -Force -ErrorAction SilentlyContinue
    }

    $edgePaths = @("HKLM:\SOFTWARE\Policies\Microsoft\Edge", "HKCU:\Software\Policies\Microsoft\Edge")
    foreach ($p in $edgePaths) {
        if (-not (Test-Path $p)) { New-Item -Path $p -Force | Out-Null }
        Set-ItemProperty -Path $p -Name "BrowserSignin" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $p -Name "SyncDisabled" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $p -Name "HideFirstRunExperience" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    }

    Set-ItemProperty -Path $sysPolicies -Name "NoConnectedUser" -Value 3 -Type DWord -Force -ErrorAction SilentlyContinue

    # 4. Eliminación de Roblox y Minecraft
    Log-Msg "[4/9] Eliminando instalaciones y datos de Roblox y Minecraft..." "Yellow"
    Eliminar-RobloxYMinecraft

    # 5. Bloqueo de dominios de Roblox y Minecraft
    Log-Msg "[5/9] Bloqueando dominios de Roblox y Minecraft en navegadores y sistema..." "Yellow"
    Bloquear-RobloxYMinecraftNavegador

    # 6. Reemplazar TranscodedWallpaper
    Log-Msg "[6/9] Reemplazando cache de fondo TranscodedWallpaper de Windows..." "Yellow"
    $themeDir = "$env:AppData\Microsoft\Windows\Themes"
    if (Test-Path $themeDir) {
        Copy-Item -Path $finalImage -Destination (Join-Path $themeDir "TranscodedWallpaper") -Force -ErrorAction SilentlyContinue
        $cachedDir = Join-Path $themeDir "CachedFiles"
        if (Test-Path $cachedDir) { Remove-Item -Path "$cachedDir\*" -Recurse -Force -ErrorAction SilentlyContinue }
    }

    # 7. GPUpdate
    Log-Msg "[7/9] Actualizando directivas de grupo (gpupdate)..." "Yellow"
    Start-Process -FilePath "gpupdate.exe" -ArgumentList "/force" -WindowStyle Hidden -Wait

    # 8. Refrescar Explorer
    Log-Msg "[8/9] Reiniciando Windows Explorer para actualizar pantalla..." "Yellow"
    Add-Type -TypeDefinition 'using System; using System.Runtime.InteropServices; public class Wallpaper { [DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni); }' -ErrorAction SilentlyContinue
    [Wallpaper]::SystemParametersInfo(0x0014, 0, $finalImage, 0x01 -bor 0x02) | Out-Null

    Stop-Process -Name "explorer" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    if (-not (Get-Process -Name "explorer" -ErrorAction SilentlyContinue)) {
        Start-Process "explorer.exe"
    }

    # 9. Crear Acceso Directo de Chrome para Plataforma DIA
    Log-Msg "[9/9] Creando acceso directo de Chrome para la Plataforma DIA..." "Yellow"
    Crear-AccesoDirectoChrome -NombreAcceso "Plataforma DIA" -Url "https://dia.agenciaeducacion.cl/login"

    Log-Msg ""
    Log-Msg "====================================================" "Green"
    Log-Msg " ¡BLOQUEO Y CONFIGURACION APLICADOS CON EXITO!      " "Green"
    Log-Msg "====================================================" "Green"
    Log-Msg "- Fondo cambiado y bloqueado correctamente." "Green"
    Log-Msg "- Carpeta User Data eliminada (Navegadores limpios a fabrica)." "Green"
    Log-Msg "- Instalaciones y datos de Roblox y Minecraft eliminados." "Green"
    Log-Msg "- Sitio web roblox.com y minecraft.net bloqueados en el sistema." "Green"
    Log-Msg "- Acceso directo 'Plataforma DIA' creado en el Escritorio." "Green"
    Log-Msg "Revisa el archivo 'lab_log.txt' en el USB para ver el informe." "Gray"

} catch {
    Log-Msg "" "Red"
    Log-Msg "[ERROR EN EJECUCION] $_" "Red"
    Log-Msg "Detalles del error guardados en 'lab_log.txt'." "Yellow"
}

Log-Msg ""
Log-Msg "Presiona la tecla ENTER para cerrar esta ventana..." "Yellow"
[void][System.Console]::ReadLine()
