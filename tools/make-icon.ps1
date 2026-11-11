# Generates src/Arrowless/Assets/app.ico (original icon: a crossed-out arrow).
# Usage: powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1 [-Preview path.png]
param(
    [string]$Out = (Join-Path $PSScriptRoot '..\src\Arrowless\Assets\app.ico'),
    [string]$Preview
)

Add-Type -AssemblyName System.Drawing

function New-Frame([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Background: rounded square with an indigo -> violet gradient
    $m = [single]($s * 0.04); $w = [single]($s * 0.92); $d = [single]($s * 0.40)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($m, $m, $d, $d, 180, 90)
    $path.AddArc($m + $w - $d, $m, $d, $d, 270, 90)
    $path.AddArc($m + $w - $d, $m + $w - $d, $d, $d, 0, 90)
    $path.AddArc($m, $m + $w - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $rect = New-Object System.Drawing.RectangleF $m, $m, $w, $w
    $c1 = [System.Drawing.Color]::FromArgb(255, 79, 70, 229)
    $c2 = [System.Drawing.Color]::FromArgb(255, 124, 58, 237)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $c1, $c2, 45.0
    $g.FillPath($bg, $path)

    function P([double]$x, [double]$y) { New-Object System.Drawing.PointF ([single]($x * $s)), ([single]($y * $s)) }

    # White arrow (pointing up and to the right)
    $white = [System.Drawing.Color]::White
    $pen = New-Object System.Drawing.Pen $white, ([single]($s * 0.11))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Flat'
    $g.DrawLine($pen, (P 0.28 0.72), (P 0.62 0.38))
    $head = [System.Drawing.PointF[]]@((P 0.76 0.24), (P 0.50 0.26), (P 0.74 0.50))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush $white), $head)

    # Coral diagonal slash with a dark outline so it stays readable at small sizes
    $dark = [System.Drawing.Color]::FromArgb(255, 49, 46, 129)
    $coral = [System.Drawing.Color]::FromArgb(255, 255, 90, 95)
    $outline = New-Object System.Drawing.Pen $dark, ([single]($s * 0.16))
    $outline.StartCap = 'Round'; $outline.EndCap = 'Round'
    $g.DrawLine($outline, (P 0.20 0.20), (P 0.80 0.80))
    $slash = New-Object System.Drawing.Pen $coral, ([single]($s * 0.09))
    $slash.StartCap = 'Round'; $slash.EndCap = 'Round'
    $g.DrawLine($slash, (P 0.20 0.20), (P 0.80 0.80))

    $g.Dispose()
    return $bmp
}

function ConvertTo-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) { $b = New-Frame $s; , (ConvertTo-PngBytes $b); $b.Dispose() }

if ($Preview) {
    $b = New-Frame 256; $b.Save($Preview, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}

New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null
$fs = [System.IO.File]::Create($Out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $bw.Write([byte]($sizes[$i] % 256)); $bw.Write([byte]($sizes[$i] % 256))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Dispose(); $fs.Dispose()
Write-Output 'Icon generated.'
