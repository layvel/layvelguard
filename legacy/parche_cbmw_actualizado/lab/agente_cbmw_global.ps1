# ==============================================================================
# AGENTE GLOBAL DE AUDITORÍA, CONTROL Y LIMPIEZA CBMW (SISTEMA OCULTO)
# Servidor Central: https://sistemas.cbmw.cl
# ==============================================================================
param(
    [switch]$Interactive = $false,
    [string]$Action = "Auto"
)

$ErrorActionPreference = "Continue"

$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = "$env:ProgramData\CBMW-Agente"
}
$global:logFile = Join-Path $scriptDir "lab_log.txt"

function Log-Msg {
    param([string]$Text, [string]$Color = "White")
    Write-Host $Text -ForegroundColor $Color
    $stamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "[$stamp] $Text" | Out-File -FilePath $global:logFile -Append -Encoding ASCII -ErrorAction SilentlyContinue
}

function Obtener-Configuracion {
    $configUrl = "https://sistemas.cbmw.cl/lab-config.json"
    $localJson = Join-Path $scriptDir "lab-config.json"
    $config = $null

    try {
        $res = Invoke-RestMethod -Uri $configUrl -TimeoutSec 5 -ErrorAction Stop
        if ($res) {
            Log-Msg "   [+] Configuracion cargada exitosamente desde servidor: $configUrl" "Green"
            return $res
        }
    } catch {
        Log-Msg "   [-] No se pudo conectar al servidor remoto. Usando configuracion local..." "Yellow"
    }

    if (Test-Path $localJson) {
        try {
            $config = Get-Content -Path $localJson -Raw | ConvertFrom-Json
            Log-Msg "   [+] Configuracion local cargada." "Gray"
            return $config
        } catch {}
    }

    # Configuracion fallback por defecto
    return [PSCustomObject]@{
        enabled = $true
        mode = "enforce"
        script_version = "2.0.0"
        server_url = "https://sistemas.cbmw.cl"
        block_roblox_web = $true
        block_steam = $true
        clean_downloads = $true
        clean_downloads_days = 7
        clean_desktop_clutter = $true
        shortcuts = @(
            @{ name = "Plataforma DIA"; url = "https://dia.agenciaeducacion.cl/login" },
            @{ name = "Sistemas CBMW"; url = "https://sistemas.cbmw.cl" }
        )
    }
}

function Bloquear-RobloxWebEstricto {
    Log-Msg "====================================================" "Cyan"
    Log-Msg " APLICANDO BLOQUEO WEB (ROBLOX / MINECRAFT / DOH)  " "Cyan"
    Log-Msg "====================================================" "Cyan"

    # 1. Desactivar DNS Seguro / DNS-over-HTTPS en Chrome y Edge
    $browserPolicies = @(
        "HKLM:\SOFTWARE\Policies\Google\Chrome",
        "HKCU:\Software\Policies\Google\Chrome",
        "HKLM:\SOFTWARE\Policies\Microsoft\Edge",
        "HKCU:\Software\Policies\Microsoft\Edge"
    )

    foreach ($p in $browserPolicies) {
        if (-not (Test-Path $p)) { New-Item -Path $p -Force | Out-Null }
        # Desactivar DoH que salta el archivo hosts
        Set-ItemProperty -Path $p -Name "DnsOverHttpsMode" -Value "off" -Type String -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $p -Name "BuiltInDnsClientEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
        Log-Msg "   - DoH desactivado en politicas: $p" "Green"
    }

    # 2. URLBlocklist para Chrome y Edge
    $blockPatterns = @("*roblox.com*", "*roblox.es*", "*rbxcdn.com*", "*minecraft.net*", "*minecraft.com*")
    $urlBlockKeys = @(
        "HKLM:\SOFTWARE\Policies\Google\Chrome\URLBlocklist",
        "HKCU:\Software\Policies\Google\Chrome\URLBlocklist",
        "HKLM:\SOFTWARE\Policies\Microsoft\Edge\URLBlocklist",
        "HKCU:\Software\Policies\Microsoft\Edge\URLBlocklist"
    )

    foreach ($keyPath in $urlBlockKeys) {
        if (-not (Test-Path $keyPath)) { New-Item -Path $keyPath -Force | Out-Null }
        $i = 1
        foreach ($pattern in $blockPatterns) {
            Set-ItemProperty -Path $keyPath -Name "$i" -Value $pattern -Type String -Force -ErrorAction SilentlyContinue
            $i++
        }
    }

    # 3. Hosts File (IPv4 e IPv6)
    try {
        $hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
        if (Test-Path $hostsPath) {
            $hostsContent = Get-Content -Path $hostsPath -Raw -ErrorAction SilentlyContinue
            $domainsToBlock = @(
                "roblox.com", "www.roblox.com", "web.roblox.com", "api.roblox.com",
                "assetgame.roblox.com", "setup.roblox.com", "minecraft.net", "www.minecraft.net"
            )
            $newEntries = ""
            foreach ($dom in $domainsToBlock) {
                if ($hostsContent -notmatch [regex]::Escape($dom)) {
                    $newEntries += "`r`n127.0.0.1 $dom`r`n::1 $dom"
                }
            }
            if (![string]::IsNullOrWhiteSpace($newEntries)) {
                Add-Content -Path $hostsPath -Value $newEntries -ErrorAction SilentlyContinue
                Log-Msg "   - Dominios bloqueados agregados a hosts (IPv4 y IPv6)." "Green"
            }
        }
    } catch {
        Log-Msg "   [!] Advertencia modificando hosts: $($_.Exception.Message)" "Yellow"
    }

    # 4. Flush DNS
    Start-Process ipconfig -ArgumentList "/flushdns" -NoNewWindow -Wait -ErrorAction SilentlyContinue
    Log-Msg "   - Cache DNS limpiada exitosamente." "Green"
}

function Auditar-Y-EliminarProgramasProhibidos {
    param([bool]$Enforce = $true)

    Log-Msg "====================================================" "Cyan"
    Log-Msg " ESCANEANDO Y DETECTANDO SOFTWARE NO AUTORIZADO    " "Cyan"
    Log-Msg "====================================================" "Cyan"

    $detected = @()

    # Procesos activos
    $procsToKill = @(
        @{ process = "RobloxPlayerBeta*"; name = "Roblox Player" },
        @{ process = "RobloxStudio*"; name = "Roblox Studio" },
        @{ process = "steam*"; name = "Steam" },
        @{ process = "Minecraft*"; name = "Minecraft Launcher" },
        @{ process = "javaw*"; name = "Java/Minecraft" },
        @{ process = "EpicGamesLauncher*"; name = "Epic Games Store" },
        @{ process = "uTorrent*"; name = "uTorrent" },
        @{ process = "BitTorrent*"; name = "BitTorrent" }
    )

    foreach ($item in $procsToKill) {
        $p = Get-Process -Name $item.process -ErrorAction SilentlyContinue
        if ($p) {
            $detected += $item.name
            Log-Msg "   [!] ALERTA: Proceso activo detectado: $($item.name)" "Red"
            if ($Enforce) {
                $p | Stop-Process -Force -ErrorAction SilentlyContinue
                Log-Msg "       -> Proceso finalizado." "Yellow"
            }
        }
    }

    # Busqueda en AppData / Program Files
    Get-ChildItem -Path "$env:SystemDrive\Users" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $uPath = $_.FullName
        $rLocal = Join-Path $uPath "AppData\Local\Roblox"
        if (Test-Path $rLocal) {
            if ($detected -notcontains "Roblox Files") { $detected += "Roblox Files" }
            if ($Enforce) { Remove-Item -Path $rLocal -Recurse -Force -ErrorAction SilentlyContinue }
        }
        $mRoaming = Join-Path $uPath "AppData\Roaming\.minecraft"
        if (Test-Path $mRoaming) {
            if ($detected -notcontains "Minecraft Files") { $detected += "Minecraft Files" }
            if ($Enforce) { Remove-Item -Path $mRoaming -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }

    # Busqueda de Steam
    $steamPath = "$env:ProgramFiles(x86)\Steam"
    if (-not (Test-Path $steamPath)) { $steamPath = "$env:ProgramFiles\Steam" }
    if (Test-Path $steamPath) {
        if ($detected -notcontains "Steam Installed") { $detected += "Steam Installed" }
        Log-Msg "   [!] ALERTA: Steam esta instalado en: $steamPath" "Red"
    }

    return $detected
}

function Limpiar-DescargasYEscritorio {
    param([int]$Days = 7, [bool]$CleanDesktop = $true)

    Log-Msg "====================================================" "Cyan"
    Log-Msg " MANTENIMIENTO: CARPETA DESCARGAS Y ESCRITORIO      " "Cyan"
    Log-Msg "====================================================" "Cyan"

    $limitDate = (Get-Date).AddDays(-$Days)

    # Limpieza de Descargas
    Get-ChildItem -Path "$env:SystemDrive\Users" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $downloads = Join-Path $_.FullName "Downloads"
        if (Test-Path $downloads) {
            Get-ChildItem -Path $downloads -File -ErrorAction SilentlyContinue | ForEach-Object {
                if ($_.LastWriteTime -lt $limitDate -or $_.Extension -match "\.(exe|msi|zip|rar|torrent|iso|bat|ps1)$") {
                    Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
                    Log-Msg "   - Eliminado de Descargas: $($_.Name)" "Gray"
                }
            }
        }

        # Limpieza de Accesos Directos de juegos en Escritorio
        if ($CleanDesktop) {
            $desk = Join-Path $_.FullName "Desktop"
            if (Test-Path $desk) {
                Get-ChildItem -Path $desk -Filter "*.lnk" -ErrorAction SilentlyContinue | ForEach-Object {
                    if ($_.Name -match "(Roblox|Minecraft|Steam|Epic|Torrent)") {
                        Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
                        Log-Msg "   - Acceso directo no autorizado eliminado: $($_.Name)" "Yellow"
                    }
                }
            }
        }
    }
}

function Crear-AccesosDirectosInstitucionales {
    param($Shortcuts)

    Log-Msg "====================================================" "Cyan"
    Log-Msg " GENERANDO ACCESOS DIRECTOS INSTITUCIONALES         " "Cyan"
    Log-Msg "====================================================" "Cyan"

    $chromePath = "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
    if (-not (Test-Path $chromePath)) {
        $chromePath = "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
    }

    $desktops = @(
        [Environment]::GetFolderPath("CommonDesktop"),
        [Environment]::GetFolderPath("Desktop")
    ) | Select-Object -Unique

    $wsh = New-Object -ComObject WScript.Shell

    foreach ($s in $Shortcuts) {
        $name = $s.name
        $url = $s.url
        foreach ($desk in $desktops) {
            if (Test-Path $desk) {
                try {
                    $lnkPath = Join-Path $desk "$name.lnk"
                    $lnk = $wsh.CreateShortcut($lnkPath)
                    if (Test-Path $chromePath) {
                        $lnk.TargetPath = $chromePath
                        $lnk.Arguments = $url
                        $lnk.IconLocation = "$chromePath,0"
                    } else {
                        $lnk.TargetPath = $url
                    }
                    $lnk.Description = "Acceso directo CBMW - $name"
                    $lnk.Save()
                    Log-Msg "   [+] Acceso directo '$name' creado en: $desk" "Green"
                } catch {}
            }
        }
    }
}

function Enviar-ReporteServidor {
    param(
        [string]$Status,
        [array]$DetectedApps,
        [string]$ErrorMsg = ""
    )

    $reportUrl = "https://sistemas.cbmw.cl/api/lab/reporte.php"
    $ip = (Get-NetIPAddress -AddressFamily IPv4 -Type Unicast -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike "127.*" } | Select-Object -First 1).IPAddress

    $payload = @{
        hostname = $env:COMPUTERNAME
        username = $env:USERNAME
        ip = $ip
        os = (Get-CimInstance Win32_OperatingSystem).Caption
        status = $Status
        detected_apps = $DetectedApps
        error_message = $ErrorMsg
        timestamp = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    } | ConvertTo-Json

    try {
        $res = Invoke-RestMethod -Uri $reportUrl -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 5 -ErrorAction Stop
        Log-Msg "   [+] Reporte de telemetria enviado exitosamente a $reportUrl" "Green"
    } catch {
        Log-Msg "   [-] No se pudo enviar el reporte al servidor central ($reportUrl): $($_.Exception.Message)" "Gray"
    }
}

# ==============================================================================
# EJECUCIÓN PRINCIPAL CON TRAMPA DE ERRORES COMPLETA
# ==============================================================================
try {
    Log-Msg "====================================================" "Green"
    Log-Msg " INICIANDO AGENTE DE AUDITORIA Y CONTROL CBMW      " "Green"
    Log-Msg "====================================================" "Green"

    $config = Obtener-Configuracion

    if (-not $config.enabled -and $Action -ne "Force") {
        Log-Msg "[INFO] El servidor indica que el servicio esta DESACTIVADO (enabled: false)." "Yellow"
        Log-Msg "El agente finalizara sin realizar cambios." "Yellow"
        if ($Interactive) {
            Write-Host "`nPresiona ENTER para salir..." -ForegroundColor Yellow
            [void][System.Console]::ReadLine()
        }
        exit 0
    }

    # 1. Bloqueo Web
    if ($config.block_roblox_web) {
        Bloquear-RobloxWebEstricto
    }

    # 2. Software Prohibido
    $detected = Auditar-Y-EliminarProgramasProhibidos -Enforce ($config.mode -eq "enforce")

    # 3. Limpieza de Descargas y Escritorio
    if ($config.clean_downloads) {
        Limpiar-DescargasYEscritorio -Days $config.clean_downloads_days -CleanDesktop $config.clean_desktop_clutter
    }

    # 4. Accesos directos
    if ($config.shortcuts) {
        Crear-AccesosDirectosInstitucionales -Shortcuts $config.shortcuts
    }

    # 5. Enviar reporte
    Enviar-ReporteServidor -Status "OK" -DetectedApps $detected

    Log-Msg "====================================================" "Green"
    Log-Msg " MANTENIMIENTO Y AUDITORIA COMPLETADOS CON EXITO   " "Green"
    Log-Msg "====================================================" "Green"

    if ($Interactive) {
        Write-Host "`nPresione ENTER para continuar..." -ForegroundColor Green
        [void][System.Console]::ReadLine()
    }

} catch {
    $err = $_.Exception.Message
    $trace = $_.ScriptStackTrace
    Log-Msg "" "Red"
    Log-Msg "[ERROR CRITICO DETECTADO] No se pudo completar la tarea:" "Red"
    Log-Msg "Detalle: $err" "Red"
    Log-Msg "Ubicacion: $trace" "Yellow"
    Log-Msg "" "Red"

    Enviar-ReporteServidor -Status "ERROR" -DetectedApps @() -ErrorMsg "$err ($trace)"

    # SI ES INTERACTIVO, NUNCA CERRAR LA VENTANA
    Log-Msg "Se ha generado un registro completo en: $global:logFile" "Yellow"
    Write-Host "`n====================================================" -ForegroundColor Red
    Write-Host " LA VENTANA PERMANECE ABIERTA PARA REVISIÓN DE ERROR" -ForegroundColor Red
    Write-Host " Copia el texto anterior o revisa lab_log.txt para corregir el problema." -ForegroundColor Red
    Write-Host "====================================================" -ForegroundColor Red
    Write-Host "`nPresiona la tecla ENTER para salir..." -ForegroundColor Yellow
    [void][System.Console]::ReadLine()
    exit 1
}
