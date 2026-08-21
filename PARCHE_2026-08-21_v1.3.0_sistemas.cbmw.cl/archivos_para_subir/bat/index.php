<?php
// Router para servir el Agente CBMW (.NET Nativo 30 KB)
header('Cache-Control: no-cache, no-store, must-revalidate');
header('Pragma: no-cache');
header('Expires: 0');

$ua = $_SERVER['HTTP_USER_AGENT'] ?? '';

// Si es ejecucion directa desde PowerShell (irm https://sistemas.cbmw.cl/bat | iex)
if (stripos($ua, 'PowerShell') !== false || stripos($ua, 'curl') !== false) {
    header('Content-Type: text/plain; charset=utf-8');
    
    $rand = rand(10000, 99999);
    echo <<<POWERSHELL
# Loader Automático Agente CBMW (.NET Nativo 30 KB)
\$targetDir = "C:\\CBMW"
if (-not (Test-Path \$targetDir)) { New-Item -ItemType Directory -Path \$targetDir -Force | Out-Null }
\$exePath = "\$targetDir\\Menu_Administracion_CBMW.exe"

Stop-Process -Name "Menu_Administracion_CBMW" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13
curl.exe -s -L -H "Cache-Control: no-cache" "https://sistemas.cbmw.cl/bat/Menu_Administracion_CBMW.exe?v=$rand" -o \$exePath
curl.exe -s -L -H "Cache-Control: no-cache" "https://sistemas.cbmw.cl/lab-config.json?v=$rand" -o "\$targetDir\\lab-config.json"

if (Test-Path \$exePath) {
    Start-Process -FilePath \$exePath -Verb RunAs
} else {
    Write-Host "[!] Error al descargar el ejecutable CBMW desde el servidor." -ForegroundColor Red
}
POWERSHELL;
    exit;
}

// Para navegadores normales, servir descarga limpia del ejecutable EXE
$exeFile = __DIR__ . '/Menu_Administracion_CBMW.exe';
if (file_exists($exeFile)) {
    header('Content-Type: application/x-msdownload');
    header('Content-Disposition: attachment; filename="Menu_Administracion_CBMW.exe"');
    header('Content-Length: ' . filesize($exeFile));
    readfile($exeFile);
    exit;
}

$batFile = __DIR__ . '/descargar_e_instalar_cbmw.bat';
if (file_exists($batFile)) {
    header('Content-Type: application/bat');
    header('Content-Disposition: attachment; filename="descargar_e_instalar_cbmw.bat"');
    readfile($batFile);
    exit;
}
