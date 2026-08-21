# ==============================================================================
# AGENTE CBMW - REDIRECCIONADOR AUTOMÁTICO A MOTOR C# .NET NATIVO (v3.1.0)
# ==============================================================================

param(
    [switch]$SilentBoot = $false
)

$targetDir = "C:\CBMW"
if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir -Force | Out-Null }
$exePath = "$targetDir\Menu_Administracion_CBMW.exe"

[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13
if (-not (Test-Path $exePath)) {
    curl.exe -s -L "https://sistemas.cbmw.cl/bat/Menu_Administracion_CBMW.exe?v=3.1.0" -o $exePath
}

if ($SilentBoot) {
    if (Test-Path $exePath) {
        Start-Process -FilePath $exePath -ArgumentList "--silent-boot" -WindowStyle Hidden
    }
    exit
}

if (Test-Path $exePath) {
    Start-Process -FilePath $exePath -Verb RunAs
} else {
    Write-Host "[!] Iniciando CBMW C# Nativo..." -ForegroundColor Green
}
