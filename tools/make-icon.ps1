# Builds src/Arrowless/Assets/app.ico from docs/icon.png (transparent PNG, ideally 1024x1024).
# Usage: powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\docs\icon.png'),
    [string]$IcoPath = (Join-Path $PSScriptRoot '..\src\Arrowless\Assets\app.ico')
)

Add-Type -AssemblyName System.Drawing

function Resize-Image([System.Drawing.Image]$img, [int]$size) {
    # Halve repeatedly while the image is much bigger than the target (avoids aliasing), then final resize.
    $cur = New-Object System.Drawing.Bitmap $img
    while ($cur.Width / 2 -ge $size) {
        $half = New-Object System.Drawing.Bitmap ([int]($cur.Width / 2)), ([int]($cur.Height / 2)), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($half)
        $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingMode = 'SourceCopy'
        $g.DrawImage($cur, 0, 0, $half.Width, $half.Height)
        $g.Dispose(); $cur.Dispose(); $cur = $half
    }
    $final = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($final)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingMode = 'SourceCopy'
    $g.DrawImage($cur, 0, 0, $size, $size)
    $g.Dispose(); $cur.Dispose()
    return $final
}

function ConvertTo-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

$srcImg = [System.Drawing.Image]::FromFile((Resolve-Path $Source))
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) { $b = Resize-Image $srcImg $s; , (ConvertTo-PngBytes $b); $b.Dispose() }
$srcImg.Dispose()

New-Item -ItemType Directory -Force -Path (Split-Path $IcoPath) | Out-Null
$fs = [System.IO.File]::Create($IcoPath)
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
