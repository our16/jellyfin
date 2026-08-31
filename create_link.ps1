$Target = "F:\github\jellyfin\Jellyfin.Server\bin\Release\net8.0-windows\win-x64\jellyfin.exe"
$Link = "F:\github\jellyfin\artifacts\Jellyfin.Desktop\jellyfin.exe"

if (Test-Path $Link) { Remove-Item $Link -Force }
New-Item -ItemType SymbolicLink -Path $Link -Target $Target -Force
Write-Host "Link created: $Link -> $Target"