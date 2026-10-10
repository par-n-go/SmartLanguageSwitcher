Add-Type -AssemblyName System.Drawing
$asset = Join-Path $PSScriptRoot '../SmartLanguageSwitcher/Assets'
function RoundRect($g, $brush, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = [Drawing.Drawing2D.GraphicsPath]::new()
    $p.AddArc($x,$y,$r,$r,180,90); $p.AddArc($x+$w-$r,$y,$r,$r,270,90)
    $p.AddArc($x+$w-$r,$y+$h-$r,$r,$r,0,90); $p.AddArc($x,$y+$h-$r,$r,$r,90,90)
    $p.CloseFigure(); $g.FillPath($brush,$p); $p.Dispose()
}
$frames = @()
foreach ($size in @(16,20,24,32,40,48,64,128,256)) {
    $bmp = [Drawing.Bitmap]::new($size,$size)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.ScaleTransform($size/256.0,$size/256.0)
    $navy = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#25346B'))
    $cyan = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#61E7D4'))
    RoundRect $g $navy 4 4 248 248 60
    RoundRect $g ([Drawing.Brushes]::White) 35 37 126 130 24
    RoundRect $g $navy 99 93 129 133 25
    RoundRect $g $cyan 108 102 114 119 22
    $font = [Drawing.Font]::new('Segoe UI',84,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
    $format = [Drawing.StringFormat]::new(); $format.Alignment='Center'; $format.LineAlignment='Center'
    $g.DrawString('A',$font,$navy,[Drawing.RectangleF]::new(36,32,124,133),$format)
    $g.DrawString([string][char]0x05D0,$font,$navy,[Drawing.RectangleF]::new(105,93,120,127),$format)
    $stream = [IO.MemoryStream]::new(); $bmp.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@($size,$stream.ToArray())
    $stream.Dispose(); $font.Dispose(); $format.Dispose(); $cyan.Dispose(); $navy.Dispose(); $g.Dispose(); $bmp.Dispose()
}
$file = [IO.File]::Create((Join-Path $asset 'app.ico')); $writer = [IO.BinaryWriter]::new($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
    $dimension = if ($frame[0] -eq 256) { 0 } else { $frame[0] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame[1].Length); $writer.Write([uint32]$offset)
    $offset += $frame[1].Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame[1]) }
$writer.Dispose()
