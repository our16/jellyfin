# Deploy updated Jellyfin server binaries (run AFTER stopping the Jellyfin server)
$bin = "F:\github\jellyfin\Jellyfin.Server\bin\Release\net10.0"
$dest = "F:\github\jellyfin\artifacts\Jellyfin.Desktop"

$files = @(
    "MediaBrowser.Providers.dll",
    "jellyfin.dll",
    "Jellyfin.Server.dll",
    "Emby.Server.Implementations.dll"
)

foreach ($f in $files) {
    $src = Join-Path $bin $f
    if (Test-Path $src) {
        try {
            Copy-Item $src (Join-Path $dest $f) -Force -ErrorAction Stop
            Write-Host "OK: $f"
        } catch {
            Write-Host "LOCKED/SKIPPED: $f - $($_.Exception.Message)"
        }
    } else {
        Write-Host "NOT FOUND in build output: $f"
    }
}
Write-Host ""
Write-Host "Deploy complete. Start the Jellyfin server now."
