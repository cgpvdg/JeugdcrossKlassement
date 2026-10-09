Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 256,256
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(21,60,70))
$white = New-Object System.Drawing.Pen ([System.Drawing.Color]::White),12
$mint = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(57,211,180))
$graphics.DrawEllipse($white,60,32,136,136)
$graphics.DrawLine($white,128,54,128,100)
$graphics.DrawLine($white,128,100,163,121)
$graphics.FillRectangle($mint,40,204,48,24)
$graphics.FillRectangle($mint,104,180,48,48)
$graphics.FillRectangle($mint,168,212,48,16)
$graphics.DrawLine($white,108,18,148,18)
$memory = New-Object System.IO.MemoryStream
$bitmap.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png)
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'app.png'),$memory.ToArray())
$stream = [System.IO.File]::Create((Join-Path $PSScriptRoot 'app.ico'))
$writer = New-Object System.IO.BinaryWriter $stream
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$memory.Length); $writer.Write([uint32]22); $writer.Write($memory.ToArray())
$writer.Dispose(); $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $white.Dispose(); $mint.Dispose()
