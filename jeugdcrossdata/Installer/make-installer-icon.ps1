Add-Type -AssemblyName System.Drawing

$pngPath = 'C:\Users\Coen\source\repos\JeugdcrossKlassement\jeugdcrossdata\Assets\logo.png'
$icoPath = 'C:\Users\Coen\source\repos\JeugdcrossKlassement\jeugdcrossdata\Installer\setup-icon.ico'

$src = [System.Drawing.Image]::FromFile($pngPath)
try {
    $bmp = New-Object System.Drawing.Bitmap 256, 256
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $scale = [Math]::Min(256.0 / $src.Width, 256.0 / $src.Height)
        $w = [int]($src.Width * $scale)
        $h = [int]($src.Height * $scale)
        $x = [int]((256 - $w) / 2)
        $y = [int]((256 - $h) / 2)
        $g.DrawImage($src, $x, $y, $w, $h)

        $pngStream = New-Object System.IO.MemoryStream
        $bmp.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $pngStream.ToArray()
        $pngStream.Dispose()

        $fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
        $bw = New-Object System.IO.BinaryWriter($fs)
        try {
            # ICONDIR
            $bw.Write([UInt16]0)      # reserved
            $bw.Write([UInt16]1)      # type = icon
            $bw.Write([UInt16]1)      # count

            # ICONDIRENTRY (16 bytes)
            $bw.Write([Byte]0)        # width 256 => 0
            $bw.Write([Byte]0)        # height 256 => 0
            $bw.Write([Byte]0)        # color count
            $bw.Write([Byte]0)        # reserved
            $bw.Write([UInt16]1)      # planes
            $bw.Write([UInt16]32)     # bit count
            $bw.Write([UInt32]$pngBytes.Length) # bytes in resource
            $bw.Write([UInt32]22)     # image offset (6 + 16)

            # image data (PNG)
            $bw.Write($pngBytes)
        }
        finally {
            $bw.Dispose()
            $fs.Dispose()
        }
    }
    finally {
        $g.Dispose()
        $bmp.Dispose()
    }
}
finally {
    $src.Dispose()
}
