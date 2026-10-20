<#
.SYNOPSIS
Draws the installer's two pictures from the app icon: the side picture of the first and last page (Dialog.bmp,
493 x 312) and the strip at the top of the other pages (Banner.bmp, 493 x 58). Run it again after the icon changes.
#>
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$icon = New-Object System.Drawing.Icon (Join-Path $root 'src\MacroStudio.App\Assets\MacroStudio.ico'), 256, 256
$accent = [System.Drawing.Color]::FromArgb(0, 103, 192)
$accentDark = [System.Drawing.Color]::FromArgb(0, 62, 125)

# First and last page: the installer writes its text on the white right part, so the picture keeps to the left 164 px.
$dialog = New-Object System.Drawing.Bitmap 493, 312
$g = [System.Drawing.Graphics]::FromImage($dialog)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'
$g.Clear([System.Drawing.Color]::White)
$panel = New-Object System.Drawing.Rectangle 0, 0, 164, 312
$g.FillRectangle((New-Object System.Drawing.Drawing2D.LinearGradientBrush $panel, $accent, $accentDark, 90.0), $panel)
$g.DrawIcon((New-Object System.Drawing.Icon $icon, 96, 96), 34, 60)
$format = New-Object System.Drawing.StringFormat
$format.Alignment = 'Center'
$g.DrawString('Macro Studio', (New-Object System.Drawing.Font 'Segoe UI Semibold', 14), [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, 172, 164, 30), $format)
$g.DrawString('Made by Ahmet Şahin', (New-Object System.Drawing.Font 'Segoe UI', 8), (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(200, 255, 255, 255))), (New-Object System.Drawing.RectangleF 0, 280, 164, 20), $format)
$g.Dispose()
$dialog.Save((Join-Path $PSScriptRoot 'Dialog.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)

# Other pages: the installer writes the page title on the left, so the icon sits on the right.
$banner = New-Object System.Drawing.Bitmap 493, 58
$g = [System.Drawing.Graphics]::FromImage($banner)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([System.Drawing.Color]::White)
$g.FillRectangle((New-Object System.Drawing.SolidBrush $accent), 0, 55, 493, 3)
$g.DrawIcon((New-Object System.Drawing.Icon $icon, 48, 48), 435, 4)
$g.Dispose()
$banner.Save((Join-Path $PSScriptRoot 'Banner.bmp'), [System.Drawing.Imaging.ImageFormat]::Bmp)
'Dialog.bmp and Banner.bmp written.'
