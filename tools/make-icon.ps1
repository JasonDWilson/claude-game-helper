<#
.SYNOPSIS
  Renders the Claude Game Helper icon (src/ClaudeGameHelper/app.ico) from vector shapes.

.DESCRIPTION
  A dark game controller on a rounded orange tile, with a small sparkle for "AI help". Drawn on a
  256-unit canvas with WPF and rendered at every size Windows asks for. Small sizes drop the sparkle and
  the controller's button cut-outs so the silhouette stays crisp in the system tray.

.EXAMPLE
  powershell -File tools/make-icon.ps1                       # writes src/ClaudeGameHelper/app.ico
  powershell -File tools/make-icon.ps1 -PreviewDir C:\temp   # also writes one PNG per size
#>
param(
    [string] $Out = (Join-Path $PSScriptRoot '..\src\ClaudeGameHelper\app.ico'),
    [string] $PreviewDir
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase, PresentationFramework

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$orangeTop = [Windows.Media.Color]::FromRgb(0xE8, 0x8C, 0x6A)
$orangeBottom = [Windows.Media.Color]::FromRgb(0xC9, 0x5F, 0x3D)
$dark = [Windows.Media.Color]::FromRgb(0x1E, 0x1F, 0x22)
$cream = [Windows.Media.Color]::FromRgb(0xFF, 0xF4, 0xE8)

function New-Brush($color) { $b = [Windows.Media.SolidColorBrush]::new($color); $b.Freeze(); $b }

function Get-Controller([bool] $detailed) {
    # Body: a wide rounded bar plus two grips, merged into one silhouette.
    $bar = [Windows.Media.RectangleGeometry]::new([Windows.Rect]::new(36, 92, 184, 78), 39, 39)
    $leftGrip = [Windows.Media.EllipseGeometry]::new([Windows.Point]::new(74, 160), 34, 40)
    $rightGrip = [Windows.Media.EllipseGeometry]::new([Windows.Point]::new(182, 160), 34, 40)
    $body = [Windows.Media.CombinedGeometry]::new('Union', $bar, $leftGrip)
    $body = [Windows.Media.CombinedGeometry]::new('Union', $body, $rightGrip)

    if ($detailed) {
        # A D-pad on the left, two buttons on the right.
        $holes = @(
            [Windows.Media.RectangleGeometry]::new([Windows.Rect]::new(58, 123, 40, 14), 3, 3),
            [Windows.Media.RectangleGeometry]::new([Windows.Rect]::new(71, 110, 14, 40), 3, 3),
            [Windows.Media.EllipseGeometry]::new([Windows.Point]::new(168, 140), 9.5, 9.5),
            [Windows.Media.EllipseGeometry]::new([Windows.Point]::new(190, 118), 9.5, 9.5))
    }
    else {
        # Tray sizes: a chunky plus and one big button, so it still reads as a controller at 16 px.
        $holes = @(
            [Windows.Media.RectangleGeometry]::new([Windows.Rect]::new(52, 119, 52, 22)),
            [Windows.Media.RectangleGeometry]::new([Windows.Rect]::new(67, 104, 22, 52)),
            [Windows.Media.EllipseGeometry]::new([Windows.Point]::new(180, 130), 16, 16))
    }
    foreach ($hole in $holes) {
        $body = [Windows.Media.CombinedGeometry]::new('Exclude', $body, $hole)
    }
    $body
}

function Get-Sparkle([double] $cx, [double] $cy, [double] $r) {
    # Four-point star with curved sides.
    $k = $r * 0.18
    [Windows.Media.Geometry]::Parse(
        ("M {0},{1} Q {2},{3} {4},{5} Q {2},{6} {0},{7} Q {8},{6} {9},{5} Q {8},{3} {0},{1} Z" -f
            $cx, ($cy - $r), ($cx + $k), ($cy - $k), ($cx + $r), $cy, ($cy + $k), ($cy + $r), ($cx - $k), ($cx - $r)))
}

function Render([int] $size) {
    $detailed = $size -ge 32
    $visual = [Windows.Media.DrawingVisual]::new()
    $dc = $visual.RenderOpen()
    $dc.PushTransform([Windows.Media.ScaleTransform]::new($size / 256.0, $size / 256.0))

    $tileBrush = [Windows.Media.LinearGradientBrush]::new($orangeTop, $orangeBottom, 90)
    $inset = if ($size -le 24) { 4 } else { 12 }
    $tile = [Windows.Rect]::new($inset, $inset, 256 - 2 * $inset, 256 - 2 * $inset)
    $dc.DrawRoundedRectangle($tileBrush, $null, $tile, 56, 56)

    $controller = Get-Controller $detailed
    if ($size -le 24) {
        # Fill more of the tile at tray sizes so the controller reads at a glance.
        $controller = $controller.Clone()
        $controller.Transform = [Windows.Media.Transform]::Parse('1.12,0,0,1.12,-15.4,-8')
    }
    $dc.DrawGeometry((New-Brush $dark), $null, $controller)

    if ($detailed) {
        $dc.DrawGeometry((New-Brush $cream), $null, (Get-Sparkle 196 58 26))
        $dc.DrawGeometry((New-Brush $cream), $null, (Get-Sparkle 222 88 11))
    }

    $dc.Pop()
    $dc.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    $encoder.Save($stream)
    , $stream.ToArray()
}

$pngs = foreach ($s in $sizes) { , (Render $s) }

if ($PreviewDir) {
    New-Item -ItemType Directory -Force $PreviewDir | Out-Null
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        [IO.File]::WriteAllBytes((Join-Path $PreviewDir "icon-$($sizes[$i]).png"), $pngs[$i])
    }
}

# ICO container with PNG-compressed entries (supported since Windows Vista).
$fs = [IO.File]::Create((Resolve-Path -LiteralPath (Split-Path $Out) | Join-Path -ChildPath (Split-Path $Out -Leaf)))
$w = [IO.BinaryWriter]::new($fs)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
"Wrote $Out ($($sizes -join ', ') px)"
