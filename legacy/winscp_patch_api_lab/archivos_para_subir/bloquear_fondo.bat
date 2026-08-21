@echo off
title Bloqueador de Fondo y Cuentas - Windows 10/11

:: 1. Solicitar Permisos de Administrador
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ====================================================
    echo  SOLICITANDO PERMISOS DE ADMINISTRADOR
    echo ====================================================
    echo.
    echo Este script requiere permisos de administrador.
    echo.
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

:: 2. Fijar directorio de trabajo al USB
cd /d "%~dp0"

:: 3. Ejecutar script de PowerShell pasando la ruta de la imagen
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0aplicar_bloqueo_lab.ps1" -SourceImage "%~dp0fondo pc.png"

echo.
echo ====================================================
echo  El proceso ha finalizado. Revisa la ventana superior
echo  o el archivo 'lab_log.txt' para ver el informe.
echo ====================================================
echo.
pause