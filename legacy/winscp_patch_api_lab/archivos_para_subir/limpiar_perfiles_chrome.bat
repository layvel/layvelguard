@echo off
title Limpiador de Perfiles - Windows 10/11

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

:: 3. Ejecutar script de PowerShell de limpieza multi-usuario
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ejecutar_limpieza_lab.ps1"

echo.
pause
