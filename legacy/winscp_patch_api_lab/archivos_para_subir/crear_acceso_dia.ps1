# Script para crear acceso directo a Plataforma DIA en el Escritorio (Escritorio Público y de usuario)
$ErrorActionPreference = "Continue"

$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = (Get-Item -Path ".\").FullName
}

function Log-Msg {
    param([string]$Text, [string]$Color = "White")
    Write-Host $Text -ForegroundColor $Color
}

Log-Msg "====================================================" "Cyan"
Log-Msg " CREANDO ACCESO DIRECTO DE CHROME - PLATAFORMA DIA  " "Cyan"
Log-Msg "====================================================" "Cyan"
Log-Msg ""

$NombreAcceso = "Plataforma DIA"
$Url = "https://dia.agenciaeducacion.cl/login"

$chromePath = "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
if (-not (Test-Path $chromePath)) {
    $chromePath = "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
}

$desktops = @(
    [Environment]::GetFolderPath("CommonDesktop"),
    [Environment]::GetFolderPath("Desktop")
) | Select-Object -Unique

$wsh = New-Object -ComObject WScript.Shell

foreach ($desk in $desktops) {
    if (Test-Path $desk) {
        try {
            $lnkPath = Join-Path $desk "$NombreAcceso.lnk"
            $lnk = $wsh.CreateShortcut($lnkPath)
            if (Test-Path $chromePath) {
                $lnk.TargetPath = $chromePath
                $lnk.Arguments = $Url
                $lnk.IconLocation = "$chromePath,0"
            } else {
                $lnk.TargetPath = $Url
            }
            $lnk.Description = "Acceso directo a Plataforma DIA - Agencia de Educacion"
            $lnk.Save()
            Log-Msg "Acceso directo '$NombreAcceso' creado con exito en:" "Green"
            Log-Msg " -> $lnkPath" "Gray"
        } catch {
            Log-Msg " No se pudo escribir en '$desk' (requiere permisos de Administrador)." "Yellow"
        }
    }
}

Log-Msg ""
Log-Msg "====================================================" "Green"
Log-Msg " ¡PROCESO FINALIZADO CON ÉXITO!                    " "Green"
Log-Msg "====================================================" "Green"
Log-Msg ""
Log-Msg "Presiona la tecla ENTER para cerrar..." "Yellow"
[void][System.Console]::ReadLine()
