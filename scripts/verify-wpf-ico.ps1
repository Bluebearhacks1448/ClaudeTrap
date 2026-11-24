$ErrorActionPreference = "Stop"

function Test-RgbaIcon([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)

    if ($bytes.Length -lt 6 -or
        [BitConverter]::ToUInt16($bytes, 0) -ne 0 -or
        [BitConverter]::ToUInt16($bytes, 2) -ne 1) {
        throw "Invalid ICO directory: $Path"
    }

    $count = [BitConverter]::ToUInt16($bytes, 4)
    if ($count -lt 1 -or $bytes.Length -lt (6 + 16 * $count)) {
        throw "Invalid ICO image count or directory size: $Path"
    }

    $found = @{}
    $pngSignature = [byte[]](0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)

    for ($i = 0; $i -lt $count; $i++) {
        $entry = 6 + 16 * $i
        $width = [int]$bytes[$entry]
        $height = [int]$bytes[$entry + 1]
        if ($width -eq 0) { $width = 256 }
        if ($height -eq 0) { $height = 256 }

        $bitCount = [BitConverter]::ToUInt16($bytes, $entry + 6)
        $dataSize = [BitConverter]::ToUInt32($bytes, $entry + 8)
        $dataOffset = [BitConverter]::ToUInt32($bytes, $entry + 12)

        if ($dataSize -lt 33 -or
            [long]$dataOffset + [long]$dataSize -gt $bytes.LongLength) {
            throw "Invalid image bounds in $Path (entry $i)"
        }

        $offset = [int]$dataOffset
        for ($j = 0; $j -lt $pngSignature.Length; $j++) {
            if ($bytes[$offset + $j] -ne $pngSignature[$j]) {
                throw "ICO entry $i is not a PNG image in $Path"
            }
        }

        if ($bytes[$offset + 8] -ne 0 -or
            $bytes[$offset + 9] -ne 0 -or
            $bytes[$offset + 10] -ne 0 -or
            $bytes[$offset + 11] -ne 13 -or
            [System.Text.Encoding]::ASCII.GetString($bytes, $offset + 12, 4) -ne "IHDR") {
            throw "ICO entry $i has an invalid PNG header in $Path"
        }

        $bitDepth = [int]$bytes[$offset + 24]
        $colorType = [int]$bytes[$offset + 25]
        if ($bitCount -ne 32 -or $bitDepth -ne 8 -or $colorType -ne 6) {
            throw "ICO entry $i must be 32-bpp, 8-bit RGBA PNG in $Path (found bpp=$bitCount, depth=$bitDepth, colorType=$colorType)"
        }

        $pngWidth = [int]::Parse(
            [BitConverter]::ToString($bytes, $offset + 16, 4).Replace("-", ""),
            [System.Globalization.NumberStyles]::HexNumber
        )
        $pngHeight = [int]::Parse(
            [BitConverter]::ToString($bytes, $offset + 20, 4).Replace("-", ""),
            [System.Globalization.NumberStyles]::HexNumber
        )

        if ($pngWidth -ne $width -or $pngHeight -ne $height) {
            throw "ICO entry $i dimensions do not match its PNG payload in $Path"
        }

        $found["$($width)x$($height)"] = $true
    }

    foreach ($required in @("16x16", "32x32", "48x48")) {
        if (-not $found.ContainsKey($required)) {
            throw "Required icon size $required is missing from $Path"
        }
    }

    Write-Host "Verified RGBA ICO: $Path ($count frames)"
}

$projectRoot = Split-Path -Parent $PSScriptRoot
Test-RgbaIcon (Join-Path $projectRoot "Bloxstrap\ClaudeTrap.ico")
Test-RgbaIcon (Join-Path $projectRoot "Bloxstrap\Resources\IconBloxstrap.ico")

# Exercise the same WIC decoder that WPF uses at runtime. Structural PNG checks
# alone do not guarantee that Windows can decode every frame in an ICO.
Add-Type -AssemblyName PresentationCore

function Test-WpfBitmapDecode([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create(
            $stream,
            [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
            [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad
        )

        if ($decoder.Frames.Count -lt 1) {
            throw "WPF returned no image frames for $Path"
        }

        foreach ($frame in $decoder.Frames) {
            $stride = [int][Math]::Ceiling(($frame.PixelWidth * $frame.Format.BitsPerPixel) / 8.0)
            $pixels = [byte[]]::new($stride * $frame.PixelHeight)
            $frame.CopyPixels($pixels, $stride, 0)
        }
    }
    finally {
        $stream.Dispose()
    }

    Write-Host "WPF/WIC successfully decoded: $Path"
}

function Test-RgbaPng([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $signature = [byte[]](0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)
    if ($bytes.Length -lt 33) {
        throw "PNG is too small: $Path"
    }

    for ($i = 0; $i -lt $signature.Length; $i++) {
        if ($bytes[$i] -ne $signature[$i]) {
            throw "Invalid PNG signature: $Path"
        }
    }

    if ($bytes[24] -ne 8 -or $bytes[25] -ne 6) {
        throw "Logo PNG must use 8-bit RGBA color: $Path"
    }

    Write-Host "Verified 8-bit RGBA PNG: $Path"
}

Test-WpfBitmapDecode (Join-Path $projectRoot "Bloxstrap\ClaudeTrap.ico")
Test-WpfBitmapDecode (Join-Path $projectRoot "Bloxstrap\Resources\IconBloxstrap.ico")
$logoPng = Join-Path $projectRoot "Bloxstrap\Resources\ClaudeTrap.png"
Test-RgbaPng $logoPng
Test-WpfBitmapDecode $logoPng
