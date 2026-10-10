param([string]$SourcePath = (Join-Path $PSScriptRoot 'app.png'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourceImage = [System.Drawing.Image]::FromFile($SourcePath)
$iconSizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
try {
 foreach ($iconSize in $iconSizes) {
  $bitmap = [System.Drawing.Bitmap]::new($iconSize, $iconSize)
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  $memory = [System.IO.MemoryStream]::new()
  try {
   $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
   $graphics.DrawImage($sourceImage, 0, 0, $iconSize, $iconSize)
   $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
   $frames += ,$memory.ToArray()
  } finally { $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
 }
 $stream = [System.IO.File]::Create((Join-Path $PSScriptRoot 'app.ico'))
 $writer = [System.IO.BinaryWriter]::new($stream)
 try {
  $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$iconSizes.Count)
  $offset = 6 + 16 * $iconSizes.Count
  for ($index = 0; $index -lt $iconSizes.Count; $index++) {
   $dimension = if ($iconSizes[$index] -eq 256) { 0 } else { $iconSizes[$index] }
   $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
   $writer.Write([byte]0); $writer.Write([byte]0)
   $writer.Write([uint16]1); $writer.Write([uint16]32)
   $writer.Write([uint32]$frames[$index].Length); $writer.Write([uint32]$offset)
   $offset += $frames[$index].Length
  }
  foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
 } finally { $writer.Dispose() }
} finally { $sourceImage.Dispose() }
