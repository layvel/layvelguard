@echo off
title INSTALADOR Y EJECUTOR REMOTO CBMW
color 0A

if "%~1"=="ELEVATED" goto IS_ELEVATED

net session >nul 2>&1
if %errorlevel% equ 0 goto IS_ELEVATED

echo ============================================================
echo  SOLICITANDO PERMISOS DE ADMINISTRADOR...
echo ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -ArgumentList 'ELEVATED' -Verb RunAs"
exit /b

:IS_ELEVATED
net session >nul 2>&1
if %errorlevel% neq 0 (
    color 0C
    echo ============================================================
    echo  [!] ERROR: NO SE PUDIERON OBTENER PRIVILEGIOS DE ADMIN
    echo ============================================================
    echo  Por favor, haz clic derecho sobre el archivo .bat y selecciona:
    echo  "Ejecutar como Administrador"
    echo ============================================================
    echo.
    echo Presiona cualquier tecla para salir...
    pause >nul
    exit /b
)

cls
echo ============================================================
echo   DESCARGANDO E INSTALANDO SISTEMA AGENTE CBMW
echo ============================================================
echo  Servidor Central: https://sistemas.cbmw.cl
echo ============================================================
echo.

set "TARGET_DIR=C:\CBMW"
if not exist "%TARGET_DIR%" (
    mkdir "%TARGET_DIR%"
)

echo [1/3] Descargando componentes desde el servidor a C:\CBMW...

where curl.exe >nul 2>&1
if %errorlevel% equ 0 (
    echo [+] Usando descarga nativa ultra-rapida curl.exe...
    curl.exe -s -L -k "https://sistemas.cbmw.cl/lab/agente_cbmw_global.ps1" -o "%TARGET_DIR%\agente_cbmw_global.ps1"
    curl.exe -s -L -k "https://sistemas.cbmw.cl/lab/Menu_Administracion_CBMW.bat" -o "%TARGET_DIR%\Menu_Administracion_CBMW.bat"
    curl.exe -s -L -k "https://sistemas.cbmw.cl/lab-config.json" -o "%TARGET_DIR%\lab-config.json"
) else (
    echo [+] Usando descarga PowerShell con BasicParsing y TLS 1.2...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; try { Invoke-WebRequest -Uri 'https://sistemas.cbmw.cl/lab/agente_cbmw_global.ps1' -UseBasicParsing -OutFile '%TARGET_DIR%\agente_cbmw_global.ps1' -ErrorAction Stop; Invoke-WebRequest -Uri 'https://sistemas.cbmw.cl/lab/Menu_Administracion_CBMW.bat' -UseBasicParsing -OutFile '%TARGET_DIR%\Menu_Administracion_CBMW.bat' -ErrorAction Stop; Invoke-WebRequest -Uri 'https://sistemas.cbmw.cl/lab-config.json' -UseBasicParsing -OutFile '%TARGET_DIR%\lab-config.json' -ErrorAction Stop } catch { exit 2 }"
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -Path '%TARGET_DIR%\*' -ErrorAction SilentlyContinue"

if not exist "%TARGET_DIR%\Menu_Administracion_CBMW.bat" (
    color 0C
    echo.
    echo ============================================================
    echo  [!] ATENCION: NO SE PUDO DESCARGAR DESDE SISTEMAS.CBMW.CL
    echo ============================================================
    echo  Verifique la conexion a internet de este equipo.
    echo ============================================================
    echo.
    echo Presiona cualquier tecla para salir...
    pause >nul
    exit /b
)

echo.
echo [2/3] Archivos preparados y desbloqueados en %TARGET_DIR%
echo [3/3] Iniciando el Menu de Administracion...
timeout /t 2 >nul

cd /d "%TARGET_DIR%"
call "%TARGET_DIR%\Menu_Administracion_CBMW.bat" ELEVATED
pause