# Genera los iconos de LidLock (candado cerrado = activo, candado abierto y gris = inactivo).
# Salida: assets\icons\on-N.png, assets\icons\off-N.png y assets\lidlock.ico. Los PNG se incrustan en el exe.
# Uso: powershell -ExecutionPolicy Bypass -File tools\make-icons.ps1

Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$iconDir = Join-Path $root 'assets\icons'
New-Item -ItemType Directory -Force $iconDir | Out-Null

function New-LockBitmap([int]$size, [string]$hex, [bool]$open) {
    $canvas = 512
    $bmp = New-Object System.Drawing.Bitmap $canvas, $canvas, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $color = [System.Drawing.ColorTranslator]::FromHtml($hex)
    $brush = New-Object System.Drawing.SolidBrush $color
    $pen = New-Object System.Drawing.Pen $color, 58
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    # Cuerpo del candado
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $x = 88; $y = 224; $w = 336; $h = 252; $r = 52
    $path.AddArc($x, $y, $r, $r, 180, 90)
    $path.AddArc($x + $w - $r, $y, $r, $r, 270, 90)
    $path.AddArc($x + $w - $r, $y + $h - $r, $r, $r, 0, 90)
    $path.AddArc($x, $y + $h - $r, $r, $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath($brush, $path)

    # Arco: cerrado = ambas patas al cuerpo; abierto = levantado y la pata derecha suelta
    $lift = if ($open) { 56 } else { 0 }
    $cy = 176 - $lift
    $shackle = New-Object System.Drawing.Drawing2D.GraphicsPath
    $shackle.AddLine(172, 232, 172, $cy)
    $shackle.AddArc(172, $cy - 84, 168, 168, 180, 180)
    $rightEnd = if ($open) { $cy + 40 } else { 232 }
    $shackle.AddLine(340, $cy, 340, $rightEnd)
    $g.DrawPath($pen, $shackle)

    # Ojo de cerradura recortado (transparente)
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $clear = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    $g.FillEllipse($clear, 224, 308, 64, 64)
    $g.FillRectangle($clear, 242, 350, 28, 74)
    $g.Dispose()

    $out = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $og = [System.Drawing.Graphics]::FromImage($out)
    $og.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $og.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $og.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $og.DrawImage($bmp, 0, 0, $size, $size)
    $og.Dispose(); $bmp.Dispose()
    return $out
}

function Save-Png($bitmap, [string]$path) {
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

# Iconos de bandeja
foreach ($size in 16, 20, 24, 32, 40, 48) {
    Save-Png (New-LockBitmap $size '#2DD4A8' $false) (Join-Path $iconDir "on-$size.png")
    Save-Png (New-LockBitmap $size '#8A94A3' $true) (Join-Path $iconDir "off-$size.png")
}

# Icono del exe: .ico con PNG de varios tamanos
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($size in $sizes) {
    $bmp = New-LockBitmap $size '#2DD4A8' $false
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$ico = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $ico
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$pngs[$i].Length); $writer.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $writer.Write($png) }
$writer.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $root 'assets\lidlock.ico'), $ico.ToArray())

Write-Host "Iconos generados en $iconDir y assets\lidlock.ico"
