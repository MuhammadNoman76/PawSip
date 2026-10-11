# Builds a multi-size .ico from icon-<size>.png files.
# Sizes up to 64 px are stored as classic 32-bit bitmaps (best compatibility for the tray), larger ones as PNG.
param(
    [Parameter(Mandatory = $true)][string]$PngFolder,
    [Parameter(Mandatory = $true)][string]$OutFile
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$frames = @()

foreach ($s in $sizes) {
    $path = Join-Path $PngFolder "icon-$s.png"
    $pngBytes = [System.IO.File]::ReadAllBytes($path)
    if ($s -gt 64) {
        $frames += , @{ Size = $s; Bytes = $pngBytes }
        continue
    }

    $bmp = New-Object System.Drawing.Bitmap $path
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    # BITMAPINFOHEADER (height is doubled: XOR + AND masks)
    $w.Write([UInt32]40); $w.Write([Int32]$s); $w.Write([Int32]($s * 2))
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]0)
    $w.Write([UInt32]0); $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
    # XOR bitmap: bottom-up BGRA
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A)
        }
    }
    # AND mask: 1 bpp, rows padded to 32 bits (all zero - alpha channel is used)
    $rowBytes = [int]([Math]::Ceiling($s / 32.0) * 4)
    $w.Write((New-Object byte[] ($rowBytes * $s)))
    $w.Flush()
    $bmp.Dispose()
    $frames += , @{ Size = $s; Bytes = $ms.ToArray() }
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$f.Bytes.Length); $w.Write([UInt32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $w.Write([byte[]]$f.Bytes) }
$w.Flush()
[System.IO.File]::WriteAllBytes($OutFile, $out.ToArray())
Write-Host "Icon written: $OutFile ($($out.Length) bytes)"
