Get-ChildItem -Recurse -Filter *.csproj -Path F:\github\jellyfin | ForEach-Object {
    $content = Get-Content $_.FullName -Encoding utf8
    $newContent = $content -replace 'net8\.0', 'net10.0'
    if ($content -ne $newContent) {
        Set-Content $_.FullName -Value $newContent -Encoding utf8
        Write-Host "Restored: $($_.FullName)"
    }
}