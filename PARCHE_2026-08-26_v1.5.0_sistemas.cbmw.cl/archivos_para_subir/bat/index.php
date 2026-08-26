<?php
// Router de descarga e instalación remota para LayvelGuard Pro
header('Cache-Control: no-cache, no-store, must-revalidate');
header('Pragma: no-cache');
header('Expires: 0');

$ua = $_SERVER['HTTP_USER_AGENT'] ?? '';

// Si es ejecución directa desde PowerShell / curl (irm https://sistemas.cbmw.cl/bat | iex)
if (stripos($ua, 'PowerShell') !== false || stripos($ua, 'curl') !== false) {
    header('Content-Type: text/plain; charset=utf-8');
    
    $rand = rand(10000, 99999);
    echo <<<POWERSHELL
# Loader Automático LayvelGuard Pro (.NET Nativo v1.5.0)
\$targetDir = "C:\\LayvelGuard"
if (-not (Test-Path \$targetDir)) { New-Item -ItemType Directory -Path \$targetDir -Force | Out-Null }
\$exePath = "\$targetDir\\LayvelGuard.exe"

Stop-Process -Name "LayvelGuard" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13
curl.exe -s -L -H "Cache-Control: no-cache" "https://raw.githubusercontent.com/layvel/layvelguard/main/LayvelGuard.exe?v=$rand" -o \$exePath
curl.exe -s -L -H "Cache-Control: no-cache" "https://raw.githubusercontent.com/layvel/layvelguard/main/config.json?v=$rand" -o "\$targetDir\\config.json"
curl.exe -s -L -H "Cache-Control: no-cache" "https://raw.githubusercontent.com/layvel/layvelguard/main/lab-config.json?v=$rand" -o "\$targetDir\\lab-config.json"

if (Test-Path \$exePath) {
    Start-Process -FilePath \$exePath -Verb RunAs
} else {
    Write-Host "[!] Error al descargar LayvelGuard.exe desde GitHub / Servidor central." -ForegroundColor Red
}
POWERSHELL;
    exit;
}

// Para navegadores normales, servir descarga o redirigir al ejecutable oficial de GitHub
$githubExe = "https://raw.githubusercontent.com/layvel/layvelguard/main/LayvelGuard.exe";
header("Location: $githubExe");
exit;

