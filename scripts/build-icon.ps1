$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pngPath = Join-Path $projectRoot 'src\HunterModManager.Wpf\Assets\daimao.png'
$compactPath = Join-Path $projectRoot 'src\HunterModManager.Wpf\Assets\daimao-compact.png'
$svgPath = Join-Path $projectRoot 'src\HunterModManager.Wpf\Assets\daimao.svg'
$icoPath = Join-Path $projectRoot 'src\HunterModManager.Wpf\Assets\daimao.ico'
if (-not (Test-Path -LiteralPath $pngPath -PathType Leaf)) { throw "Image missing: $pngPath" }
if (-not (Test-Path -LiteralPath $compactPath -PathType Leaf)) { throw "Image missing: $compactPath" }

Add-Type -AssemblyName System.Drawing
# Generate display-size resources during packaging rather than scaling large PNGs at startup.
$portraitDirectory = Join-Path $projectRoot 'src\HunterModManager.Wpf\Assets\portraits'
New-Item -ItemType Directory -Path $portraitDirectory -Force | Out-Null
foreach ($style in 'sticker', 'crayon', 'pixel') {
    $portraitSource = [Drawing.Bitmap]::new((Join-Path $projectRoot "src\HunterModManager.Wpf\Assets\daimao-$style.png"))
    $portrait = [Drawing.Bitmap]::new(204, 204)
    try {
        $portraitGraphics = [Drawing.Graphics]::FromImage($portrait)
        try {
            $portraitGraphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $portraitGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $portraitGraphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $portraitGraphics.DrawImage($portraitSource, [Drawing.Rectangle]::new(0, 0, 204, 204))
        } finally { $portraitGraphics.Dispose() }
        $portrait.Save((Join-Path $portraitDirectory "daimao-$style.png"), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $portrait.Dispose(); $portraitSource.Dispose() }
}
$source = [Drawing.Bitmap]::new($pngPath)
$compact = [Drawing.Bitmap]::new($compactPath)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = [Collections.Generic.List[byte[]]]::new()
try {
    if ($source.Width -ne $source.Height) { throw 'Expected a square icon image' }
    if ($compact.Width -ne $compact.Height) { throw 'Expected a square compact image' }
    # The SVG wrapper keeps the earlier SVG deliverable visually identical to the generated bitmap.
    $base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($pngPath))
    $svg = "<svg xmlns=`"http://www.w3.org/2000/svg`" xmlns:xlink=`"http://www.w3.org/1999/xlink`" viewBox=`"0 0 $($source.Width) $($source.Height)`" role=`"img`" aria-label=`"呆猫抱着 MOD.zip 文件夹`">`n  <image width=`"$($source.Width)`" height=`"$($source.Height)`" href=`"data:image/png;base64,$base64`"/>`n</svg>"
    $svg | Set-Content -LiteralPath $svgPath -Encoding utf8
    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size, $size)
        try {
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
                $frameSource = if ($size -le 48) { $compact } else { $source }
                $graphics.DrawImage($frameSource, [Drawing.Rectangle]::new(0, 0, $size, $size))
            } finally { $graphics.Dispose() }
            $stream = [IO.MemoryStream]::new()
            try {
                $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
                $images.Add($stream.ToArray())
            } finally { $stream.Dispose() }
        } finally { $bitmap.Dispose() }
    }
} finally { $source.Dispose(); $compact.Dispose() }

$output = [IO.File]::Create($icoPath)
try {
    $writer = [IO.BinaryWriter]::new($output)
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    for ($i = 0; $i -lt $images.Count; $i++) {
        $writer.Write([byte]($sizes[$i] % 256))
        $writer.Write([byte]($sizes[$i] % 256))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($bytes in $images) { $writer.Write($bytes) }
    $writer.Flush()
} finally { $output.Dispose() }
Write-Output $icoPath
