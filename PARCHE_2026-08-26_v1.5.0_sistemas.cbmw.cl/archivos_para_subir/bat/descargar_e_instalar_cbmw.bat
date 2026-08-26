@echo off
title Instalador LayvelGuard Pro (.NET 4.8)
color 0A

cls
echo ============================================================
echo   INSTALADOR REMOTO LAYVELGUARD PRO (.NET 4.8)
echo ============================================================
echo   Servidor Central: https://sistemas.cbmw.cl
echo ============================================================
echo.

set "TARGET_DIR=C:\LayvelGuard"
if not exist "%TARGET_DIR%" (
    mkdir "%TARGET_DIR%"
)

echo [1/2] Descargando LayvelGuard.exe v1.5.0 a C:\LayvelGuard...
curl.exe -s -L -H "Cache-Control: no-cache" "https://raw.githubusercontent.com/layvel/layvelguard/main/LayvelGuard.exe?v=%RANDOM%" -o "%TARGET_DIR%\LayvelGuard.exe"
curl.exe -s -L -H "Cache-Control: no-cache" "https://raw.githubusercontent.com/layvel/layvelguard/main/config.json?v=%RANDOM%" -o "%TARGET_DIR%\config.json"
curl.exe -s -L -H "Cache-Control: no-cache" "https://raw.githubusercontent.com/layvel/layvelguard/main/lab-config.json?v=%RANDOM%" -o "%TARGET_DIR%\lab-config.json"

if not exist "%TARGET_DIR%\LayvelGuard.exe" (
    echo.
    echo [!] No se pudo descargar LayvelGuard.exe desde el servidor central.
    echo     Verifique la conexion a Internet de este equipo.
    echo.
    pause
    exit /b
)

echo [2/2] Iniciando LayvelGuard Pro...
timeout /t 1 >nul

cd /d "%TARGET_DIR%"
start "" "%TARGET_DIR%\LayvelGuard.exe"
exit /b
