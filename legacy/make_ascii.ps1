$installerContent = @"
@echo off
title Sistema Agente CBMW (.NET Nativo)
color 0A

cls
echo ============================================================
echo   INSTALADOR REMOTO SISTEMA AGENTE CBMW (.NET 23 KB)
echo ============================================================
echo   Servidor Central: https://sistemas.cbmw.cl
echo ============================================================
echo.

set "TARGET_DIR=C:\CBMW"
if not exist "%TARGET_DIR%" (
    mkdir "%TARGET_DIR%"
)

echo [1/2] Descargando programa nativo .NET de 23 KB a C:\CBMW...
curl.exe -s -L "https://sistemas.cbmw.cl/bat/Menu_Administracion_CBMW.exe?v=3.0.0" -o "%TARGET_DIR%\Menu_Administracion_CBMW.exe"
curl.exe -s -L "https://sistemas.cbmw.cl/lab-config.json?v=3.0.0" -o "%TARGET_DIR%\lab-config.json"

if not exist "%TARGET_DIR%\Menu_Administracion_CBMW.exe" (
    echo.
    echo [!] No se pudo descargar el ejecutable desde el servidor central.
    echo     Verifique la conexion a Internet de este equipo.
    echo.
    pause
    exit /b
)

echo [2/2] Iniciando Aplicacion Nativa CBMW...
timeout /t 1 >nul

cd /d "%TARGET_DIR%"
start "" "%TARGET_DIR%\Menu_Administracion_CBMW.exe"
exit /b
"@

[System.IO.File]::WriteAllText("c:\Users\Profesor2\Downloads\lab\descargar_e_instalar_cbmw.bat", $installerContent, [System.Text.Encoding]::ASCII)
Write-Host "Instalador C# .NET nativo de 23 KB generado exitosamente." -ForegroundColor Green
