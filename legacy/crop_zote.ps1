Add-Type -AssemblyName System.Drawing

$srcPath = "$env:USERPROFILE\Downloads\Zote.jpg"
$outDir = "C:\LayvelGuard"
if (!(Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force }

$img = [System.Drawing.Image]::FromFile($srcPath)
$w = $img.Width
$h = $img.Height

Write-Host "Dimensiones originales: ${w}x${h}"

# Cropping square focused slightly above center (to keep character, cut signature at bottom)
$cropSize = [Math]::Min($w, $h)
$cropX = [int](($w - $cropSize) / 2)
# Crop slightly higher to avoid bottom signature
$cropY = [int](($h - $cropSize) * 0.25)
if ($cropY -lt 0) { $cropY = 0 }

$bmp = New-Object System.Drawing.Bitmap(256, 256)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

$srcRect = New-Object System.Drawing.Rectangle($cropX, $cropY, $cropSize, $cropSize)
$destRect = New-Object System.Drawing.Rectangle(0, 0, 256, 256)

$g.DrawImage($img, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)

# Guardar PNG en C:\LayvelGuard
$pngPath = "$outDir\layvelguard_logo.png"
$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

# Convertir a ICO nativo de Windows
$icoPath = "$outDir\layvelguard_icon.ico"
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
$icon.Save($fs)
$fs.Close()

$g.Dispose()
$bmp.Dispose()
$img.Dispose()

Write-Host "IMAGEN CORTADA Y CENTRADA EXITOSAMENTE EN $pngPath Y $icoPath"
