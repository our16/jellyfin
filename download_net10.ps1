$url = "https://dot.net/v1/dotnet-install.ps1"
$script = "F:\github\jellyfin\dotnet-install.ps1"

Write-Host "Downloading dotnet-install script..."
Invoke-WebRequest -Uri $url -OutFile $script -UseBasicParsing

Write-Host "Installing .NET 10 SDK..."
& powershell -ExecutionPolicy Bypass -File $script -Channel 10.0 -Architecture x64 -InstallDir "C:\Program Files\dotnet" -NoPath

Write-Host ".NET 10 SDK installation complete"