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
powershell -NoProfile -ExecutionPolicy Bypass -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; try { Invoke-WebRequest -Uri 'https://sistemas.cbmw.cl/lab/agente_cbmw_global.ps1' -OutFile 'C:\CBMW\agente_cbmw_global.ps1' -ErrorAction Stop; Invoke-WebRequest -Uri 'https://sistemas.cbmw.cl/lab/Menu_Administracion_CBMW.bat' -OutFile 'C:\CBMW\Menu_Administracion_CBMW.bat' -ErrorAction Stop; Invoke-WebRequest -Uri 'https://sistemas.cbmw.cl/lab-config.json' -OutFile 'C:\CBMW\lab-config.json' -ErrorAction Stop; Write-Host 'DESCARGA EXITOSA' -ForegroundColor Green } catch { Write-Host 'AVISO: No se pudo descargar desde el servidor remoto.' -ForegroundColor Yellow; Write-Host 'Detalle:' .Exception.Message -ForegroundColor Gray; exit 2 }"

set DOWNLOAD_STATUS=%errorlevel%

if %DOWNLOAD_STATUS% neq 0 (
    echo.
    echo [!] Verificando si existen archivos locales en %TARGET_DIR%...
    if exist "%TARGET_DIR%\Menu_Administracion_CBMW.bat" (
        echo [+] Se encontraron archivos locales previos. Se usara la copia local.
        timeout /t 3 >nul
    ) else (
        color 0C
        echo.
        echo ============================================================
        echo  [!] ATENCION: NO SE PUDO DESCARGAR DESDE SISTEMAS.CBMW.CL
        echo ============================================================
        echo  Causa posible: Aun no has subido el parche por WinSCP al servidor,
        echo  o el equipo no tiene acceso a internet en este momento.
        echo ============================================================
        echo.
        echo Presiona cualquier tecla para salir...
        pause >nul
        exit /b
    )
)

echo.
echo [2/3] Archivos preparados en %TARGET_DIR%
echo [3/3] Iniciando el Menu de Administracion...
timeout /t 2 >nul

cd /d "%TARGET_DIR%"
call "%TARGET_DIR%\Menu_Administracion_CBMW.bat" ELEVATED
pause