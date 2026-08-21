# Script de Desbloqueo Completo para Lab de Cómputo (Windows 10/11)
$ErrorActionPreference = "Continue"

$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = (Get-Item -Path ".\").FullName
}

$global:logFile = Join-Path $scriptDir "lab_log.txt"

function Log-Msg {
    param([string]$Text, [string]$Color = "White")
    Write-Host $Text -ForegroundColor $Color
    $stamp = Get-Date -Format "HH:mm:ss"
    "[$stamp] $Text" | Out-File -FilePath $global:logFile -Append -Encoding UTF8 -ErrorAction SilentlyContinue
}

Log-Msg "====================================================" "Cyan"
Log-Msg " DESBLOQUEANDO RESTRICCIONES DEL LABORATORIO        " "Cyan"
Log-Msg "====================================================" "Cyan"
Log-Msg ""

try {
    Log-Msg "[1/3] Eliminando directivas de HKLM (Sistema)..." "Yellow"
    $sysPolicies = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"
    Remove-ItemProperty -Path $sysPolicies -Name "Wallpaper" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $sysPolicies -Name "WallpaperStyle" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $sysPolicies -Name "NoDispBackgroundPage" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $sysPolicies -Name "NoConnectedUser" -Force -ErrorAction SilentlyContinue

    $actPolicies = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"
    Remove-ItemProperty -Path $actPolicies -Name "NoChangingWallPaper" -Force -ErrorAction SilentlyContinue

    Remove-Item -Path "HKLM:\SOFTWARE\Policies\Google\Chrome" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "HKLM:\SOFTWARE\Policies\Microsoft\Edge" -Recurse -Force -ErrorAction SilentlyContinue

    Log-Msg "[2/3] Eliminando directivas de HKCU (Usuario)..." "Yellow"
    $hkcuSys = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System"
    Remove-ItemProperty -Path $hkcuSys -Name "Wallpaper" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $hkcuSys -Name "WallpaperStyle" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $hkcuSys -Name "NoDispBackgroundPage" -Force -ErrorAction SilentlyContinue

    $hkcuAct = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\ActiveDesktop"
    Remove-ItemProperty -Path $hkcuAct -Name "NoChangingWallPaper" -Force -ErrorAction SilentlyContinue

    Remove-Item -Path "HKCU:\Software\Policies\Google\Chrome" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "HKCU:\Software\Policies\Microsoft\Edge" -Recurse -Force -ErrorAction SilentlyContinue

    try {
        $hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
        if (Test-Path $hostsPath) {
            $lines = Get-Content -Path $hostsPath | Where-Object { $_ -notmatch "roblox|minecraft" }
            Set-Content -Path $hostsPath -Value $lines -Force -ErrorAction SilentlyContinue
        }
    } catch {}

    Log-Msg "[3/3] Actualizando directivas y reiniciando Windows Explorer..." "Yellow"
    Start-Process -FilePath "gpupdate.exe" -ArgumentList "/force" -WindowStyle Hidden -Wait

    Stop-Process -Name "explorer" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    if (-not (Get-Process -Name "explorer" -ErrorAction SilentlyContinue)) {
        Start-Process "explorer.exe"
    }

    Log-Msg ""
    Log-Msg "====================================================" "Green"
    Log-Msg " ¡DESBLOQUEO COMPLETADO CON ÉXITO!                 " "Green"
    Log-Msg "====================================================" "Green"

} catch {
    Log-Msg "" "Red"
    Log-Msg "[ERROR EN DESBLOQUEO] $_" "Red"
}

Log-Msg ""
Log-Msg "Presiona la tecla ENTER para cerrar esta ventana..." "Yellow"
[void][System.Console]::ReadLine()
