[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$src = "F:\github\jellyfin\test-danmaku\bilibili-all-danmaku.xml"
$epDir = "F:\电影ing\花儿与少年\第二季"
$pluginDir = "C:\Users\Admin\AppData\Local\jellyfin\data\danmaku"

New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null

# Episode video files in date order
$videos = Get-ChildItem $epDir -Filter "*.mkv" | Sort-Object Name
Write-Output "Episodes found: $($videos.Count)"

# Parse danmaku
$xml = [System.IO.File]::ReadAllText($src, [System.Text.Encoding]::UTF8)
$matches = [regex]::Matches($xml, '<d p="([^"]+)">([^<]+)</d>')
Write-Output "Total danmaku: $($matches.Count)"

# Timeline: video duration 29434s / 11 episodes
$epCount = $videos.Count
$duration = 29434.0
$epLen = $duration / $epCount

$items = foreach($m in $matches) {
    $p = $m.Groups[1].Value -split ','
    [pscustomobject]@{
        Time = [double]$p[0]
        P    = $p
        Text = $m.Groups[2].Value
    }
}

for($i = 0; $i -lt $epCount; $i++) {
    $start = $i * $epLen
    $end = ($i + 1) * $epLen
    $video = $videos[$i]
    $base = [System.IO.Path]::GetFileNameWithoutExtension($video.Name)

    $epItems = $items | Where-Object { $_.Time -ge $start -and $_.Time -lt $end }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
    [void]$sb.AppendLine('<i>')
    [void]$sb.AppendLine('    <chatserver>chat.bilibili.com</chatserver>')
    [void]$sb.AppendLine("    <chatid>36189962748</chatid>")
    [void]$sb.AppendLine('    <maxlimit>' + $epItems.Count + '</maxlimit>')
    [void]$sb.AppendLine('    <state>0</state>')
    [void]$sb.AppendLine('    <real_name>0</real_name>')
    [void]$sb.AppendLine('    <source>e-r</source>')

    foreach($it in $epItems) {
        $parts = $it.P
        $parts[0] = ($it.Time - $start).ToString("F5", [System.Globalization.CultureInfo]::InvariantCulture)
        $newP = $parts -join ','
        [void]$sb.AppendLine('    <d p="' + $newP + '">' + $it.Text + '</d>')
    }
    [void]$sb.AppendLine('</i>')

    $content = $sb.ToString()
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    # 1. Sidecar next to video (standard format)
    $sidecar = Join-Path $epDir ($base + ".xml")
    [System.IO.File]::WriteAllText($sidecar, $content, $utf8NoBom)

    # 2. Plugin data dir (LocalFileSource scan path), named by date keyword
    $datePart = ($base -split '期')[0] + "期"
    $pluginFile = Join-Path $pluginDir ($datePart + ".xml")
    [System.IO.File]::WriteAllText($pluginFile, $content, $utf8NoBom)

    Write-Output ("EP{0:d2} {1}  <-  {2} danmaku, {3}KB" -f ($i+1), $datePart, $epItems.Count, [math]::Round((Get-Item $sidecar).Length/1024))
}

Write-Output "`nDone."
