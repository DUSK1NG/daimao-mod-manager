param(
    [string]$Browser = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$svgPath = Join-Path $projectRoot 'src\HunterModManager.Wpf\Assets\daimao.svg'
$icoPath = Join-Path $projectRoot 'src\HunterModManager.Wpf\Assets\daimao.ico'
$workRoot = Join-Path $projectRoot 'artifacts\icon-build'
New-Item -ItemType Directory -Path $workRoot -Force | Out-Null

if (-not (Test-Path -LiteralPath $Browser -PathType Leaf)) { throw "Browser not found: $Browser" }
$htmlPath = Join-Path $workRoot 'preview.html'
$pngPath = Join-Path $workRoot 'preview.png'
$profilePath = Join-Path $workRoot 'browser-profile'
$svg = Get-Content -LiteralPath $svgPath -Raw
('<html><head><meta charset="utf-8"><style>html,body{margin:0;width:256px;height:256px;overflow:hidden}svg{display:block;width:256px;height:256px}</style></head><body>' + $svg + '</body></html>') |
    Set-Content -LiteralPath $htmlPath -Encoding utf8
$url = [Uri]::new($htmlPath).AbsoluteUri
if (Test-Path -LiteralPath $pngPath) { Remove-Item -LiteralPath $pngPath }
& $Browser '--headless=new' '--disable-gpu' '--hide-scrollbars' "--user-data-dir=$profilePath" "--screenshot=$pngPath" '--window-size=256,256' $url
for ($attempt = 0; $attempt -lt 50 -and -not (Test-Path -LiteralPath $pngPath -PathType Leaf); $attempt++) {
    Start-Sleep -Milliseconds 200
}
if (-not (Test-Path -LiteralPath $pngPath -PathType Leaf)) { throw 'SVG rendering failed' }

Add-Type -AssemblyName System.Drawing
$source = [Drawing.Bitmap]::new($pngPath)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = [Collections.Generic.List[byte[]]]::new()
try {
    if ($source.Width -ne 256 -or $source.Height -ne 256) { throw 'Expected a 256x256 SVG render' }
    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size, $size)
        try {
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
                $graphics.DrawImage($source, [Drawing.Rectangle]::new(0, 0, $size, $size))
            } finally { $graphics.Dispose() }
            $stream = [IO.MemoryStream]::new()
            try {
                $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
                $images.Add($stream.ToArray())
            } finally { $stream.Dispose() }
        } finally { $bitmap.Dispose() }
    }
} finally { $source.Dispose() }

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
