$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "CrosshairStudio.csproj"
$output = Join-Path $PSScriptRoot "publish"
$release = Join-Path $PSScriptRoot "release"

dotnet publish $project --configuration Release --runtime win-x64 --self-contained true `
    --output $output `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

New-Item -ItemType Directory -Force -Path $release | Out-Null
$zip = Join-Path $release "CrosshairStudio-1.0.0-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $output "*") -DestinationPath $zip

$sha = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
"$sha  CrosshairStudio-1.0.0-win-x64.zip" |
    Set-Content -Encoding ascii (Join-Path $release "checksums.txt")

Write-Host "Готово: $output"
