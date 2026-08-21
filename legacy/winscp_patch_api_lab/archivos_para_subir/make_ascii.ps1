$installerContent = @"
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
"@

$menuContent = @"
@echo off
title ADMINISTRACION Y CONTROL DE EQUIPOS - CBMW
color 0A

if "%~1"=="ELEVATED" goto IS_ELEVATED

net session >nul 2>&1
if %errorlevel% equ 0 goto IS_ELEVATED

echo.
echo ============================================================
echo  SOLICITANDO PERMISOS DE ADMINISTRADOR...
echo ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -ArgumentList 'ELEVATED' -Verb RunAs"
exit /b

:IS_ELEVATED
net session >nul 2>&1
if %errorlevel% neq 0 (
    color 0C
    echo.
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

:MENU
cls
echo ============================================================
echo        SISTEMA DE ADMINISTRACION Y CONTROL DE EQUIPOS - CBMW
echo ============================================================
echo  Servidor Central: https://sistemas.cbmw.cl
echo ============================================================
echo.
echo  [1] Auditoria e Inventario (Reportar estado al servidor)
echo  [2] Aplicar Bloqueos Estrictos (Roblox Web/App, Steam, Games)
echo  [3] Limpieza Radical de Navegadores (Reset Chrome y Edge)
echo  [4] Limpiar Carpeta Descargas y Archivos del Escritorio
echo  [5] Crear Accesos Directos (Plataforma DIA + UMaximo)
echo  [6] Restaurar / Desbloquear Equipo (Modo Mantenimiento)
echo  [7] Probar Conexion con Servidor (sistemas.cbmw.cl)
echo  [8] EJECUTAR MANTENIMIENTO COMPLETO AUTOMATICO
echo  [0] Salir
echo.
echo ============================================================
set /p opcion= Seleccione una opcion [0-8]: 

if "%opcion%"=="1" goto OP_AUDITORIA
if "%opcion%"=="2" goto OP_BLOQUEOS
if "%opcion%"=="3" goto OP_LIMPIEZA_BROWSER
if "%opcion%"=="4" goto OP_LIMPIEZA_ARCHIVOS
if "%opcion%"=="5" goto OP_ACCESOS
if "%opcion%"=="6" goto OP_DESBLOQUEO
if "%opcion%"=="7" goto OP_TEST_SERVIDOR
if "%opcion%"=="8" goto OP_COMPLETO
if "%opcion%"=="0" exit /b

echo.
echo Opcion invalida. Intente de nuevo.
timeout /t 2 >nul
goto MENU

:OP_AUDITORIA
cls
echo ============================================================
echo  EJECUTANDO AUDITORIA E INVENTARIO
echo ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0agente_cbmw_global.ps1" -Interactive -Action "Audit"
if %errorlevel% neq 0 goto ERROR_TRAP
goto MENU

:OP_BLOQUEOS
cls
echo ============================================================
echo  APLICANDO BLOQUEOS WEB Y PROGRAMAS PROHIBIDOS
echo ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0aplicar_bloqueo_lab.ps1"
if %errorlevel% neq 0 goto ERROR_TRAP
echo.
pause
goto MENU

:OP_LIMPIEZA_BROWSER
cls
echo ============================================================
echo  LIMPIEZA RADICAL DE NAVEGADORES (RESET CHROME / EDGE)
echo ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ejecutar_limpieza_lab.ps1"
if %errorlevel% neq 0 goto ERROR_TRAP
echo.
pause
goto MENU

:OP_LIMPIEZA_ARCHIVOS
cls
echo ============================================================
echo  LIMPIANDO CARPETA DESCARGAS Y ESCRITORIO
echo ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0agente_cbmw_global.ps1" -Interactive -Action "CleanFiles"
if %errorlevel% neq 0 goto ERROR_TRAP
goto MENU

:OP_ACCESOS
cls
echo ============================================================
echo  CREANDO ACCESOS DIRECTOS INSTITUCIONALES
echo ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0crear_acceso_dia.ps1"
if %errorlevel% neq 0 goto ERROR_TRAP
echo.
pause
goto MENU

:OP_DESBLOQUEO
cls
echo ============================================================
echo  RESTAURANDO Y DESBLOQUEANDO EQUIPO
echo ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0desbloquear_lab.ps1"
if %errorlevel% neq 0 goto ERROR_TRAP
echo.
pause
goto MENU

:OP_TEST_SERVIDOR
cls
echo ============================================================
echo  PROBANDO CONEXION CON SERVIDOR (sistemas.cbmw.cl)
echo ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { Invoke-RestMethod -Uri 'https://sistemas.cbmw.cl/lab-config.json' -TimeoutSec 5 -ErrorAction Stop; Write-Host 'CONEXION EXITOSA. Servidor sistemas.cbmw.cl respondiendo correctamente.' -ForegroundColor Green } catch { Write-Host 'ERROR CONECTANDO AL SERVIDOR:' [0].Message -ForegroundColor Red }"
echo.
pause
goto MENU

:OP_COMPLETO
cls
echo ============================================================
echo  EJECUTANDO MANTENIMIENTO Y CONTROL COMPLETO
echo ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0agente_cbmw_global.ps1" -Interactive -Action "Force"
if %errorlevel% neq 0 goto ERROR_TRAP
goto MENU

:ERROR_TRAP
color 0C
echo.
echo ============================================================
echo   [!] SE HA DETECTADO UN ERROR EN LA EJECUCION
echo ============================================================
echo  La ventana NO se cerrara para que puedas inspeccionar el problema.
echo  Revisa el archivo de registro en: %~dp0lab_log.txt
echo ============================================================
echo.
if exist "%~dp0lab_log.txt" (
    echo Ultimas lineas del registro de error:
    echo ------------------------------------------------------------
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Content -Path '%~dp0lab_log.txt' -Tail 15"
    echo ------------------------------------------------------------
)
echo.
echo Presiona cualquier tecla para volver al menu...
pause >nul
color 0A
goto MENU
"@

[System.IO.File]::WriteAllText("c:\Users\Profesor2\Downloads\lab\descargar_e_instalar_cbmw.bat", $installerContent, [System.Text.Encoding]::ASCII)
[System.IO.File]::WriteAllText("c:\Users\Profesor2\Downloads\lab\Menu_Administracion_CBMW.bat", $menuContent, [System.Text.Encoding]::ASCII)

Write-Host "Instalador ultra-compatible con curl.exe y Unblock-File generado exitosamente." -ForegroundColor Green
