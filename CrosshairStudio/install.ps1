$ErrorActionPreference = "Stop"

$appName = "Crosshair Studio"
$installDir = Join-Path $env:LOCALAPPDATA "CrosshairStudio"
$sourceDir = $PSScriptRoot

$publishDir = Join-Path $sourceDir "publish"
if (-not (Test-Path $publishDir)) {
    throw "Спочатку запустіть build-release.ps1."
}

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $installDir -Recurse -Force
Copy-Item -Path (Join-Path $sourceDir "uninstall.ps1") -Destination $installDir -Force

$exe = Join-Path $installDir "CrosshairStudio.exe"
$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
New-Item -ItemType Directory -Force -Path $startMenu | Out-Null

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startMenu "Crosshair Studio.lnk"))
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $installDir
$shortcut.Save()

$registryKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CrosshairStudio"
New-Item -Path $registryKey -Force | Out-Null
Set-ItemProperty -Path $registryKey -Name "DisplayName" -Value $appName
Set-ItemProperty -Path $registryKey -Name "DisplayVersion" -Value "1.0"
Set-ItemProperty -Path $registryKey -Name "Publisher" -Value "Crosshair Studio"
Set-ItemProperty -Path $registryKey -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $registryKey -Name "UninstallString" -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$installDir\uninstall.ps1`""

Write-Host "Встановлено в $installDir"
