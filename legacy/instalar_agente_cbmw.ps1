# Script de Instalación de Tarea Programada para Agente CBMW (Carpeta C:\CBMW)
# Ejecuta el mantenimiento automáticamente al iniciar Windows en segundo plano
$ErrorActionPreference = "Continue"

$targetDir = "C:\CBMW"
if (-not (Test-Path $targetDir)) {
    New-Item -Path $targetDir -ItemType Directory -Force | Out-Null
}

$agentScript = Join-Path $targetDir "agente_cbmw_global.ps1"
$taskName = "CBMW_AgenteMantenimiento"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host " INSTALANDO AGENTE EN TAREAS PROGRAMADAS DE WINDOWS  " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

if (-not (Test-Path $agentScript)) {
    Write-Host "[!] Error: No se encuentra $agentScript" -ForegroundColor Red
    exit 1
}

$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -WindowStyle Hidden -File `"$agentScript`""
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable

try {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
    Write-Host "[+] Tarea programada '$taskName' instalada exitosamente en $targetDir." -ForegroundColor Green
    Write-Host "    Se ejecutara en segundo plano en cada inicio de Windows." -ForegroundColor Green
} catch {
    Write-Host "[!] Error registrando la tarea programada: $($_.Exception.Message)" -ForegroundColor Red
}
