$ErrorActionPreference = "Stop"

$installDir = Join-Path $env:LOCALAPPDATA "CrosshairStudio"
$shortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Crosshair Studio.lnk"

if (Test-Path $shortcut) { Remove-Item -LiteralPath $shortcut -Force }
Remove-Item -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CrosshairStudio" -Recurse -Force -ErrorAction SilentlyContinue
if (Test-Path $installDir) { Remove-Item -LiteralPath $installDir -Recurse -Force }
