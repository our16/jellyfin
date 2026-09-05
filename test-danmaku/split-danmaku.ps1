$xml = [System.IO.File]::ReadAllText("F:\github\jellyfin\test-danmaku\hua-shao-2-all.xml", [System.Text.Encoding]::UTF8)
$items = [regex]::Matches($xml, '<d p="([^"]+)">([^<]+)</d>')
$times = $items | ForEach-Object { [double]($_.Groups[1].Value -split ',')[0] } | Sort-Object
$totalSec = $times[-1]
$epCount = 11
$epLen = $totalSec / $epCount

Write-Output "Total: $($items.Count) danmaku, $([math]::Round($totalSec/3600,2))h"
Write-Output "Split into $epCount episodes, ~$([math]::Round($epLen/60))min each"
Write-Output ""

$outDir = "F:\github\jellyfin\test-danmaku\hua-shao-2-episodes"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

for($i = 0; $i -lt $epCount; $i++) {
    $start = $i * $epLen
    $end = ($i + 1) * $epLen
    $epNum = ($i + 1).ToString("D2")
    
    $epItems = $items | Where-Object { 
        $t = [double]($_.Groups[1].Value -split ',')[0]
        $t -ge $start -and $t -lt $end
    }
    
    # Build XML with adjusted timestamps
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
    [void]$sb.AppendLine('<i>')
    [void]$sb.AppendLine('    <chatserver>chat.bilibili.com</chatserver>')
    [void]$sb.AppendLine("    <chatid>hua-shao-2-ep$epNum</chatid>")
    [void]$sb.AppendLine('    <maxlimit>1500</maxlimit>')
    [void]$sb.AppendLine('    <state>0</state>')
    [void]$sb.AppendLine('    <real_name>0</real_name>')
    [void]$sb.AppendLine('    <source>e-r</source>')
    
    foreach($item in $epItems) {
        $pAttr = $item.Groups[1].Value
        $content = $item.Groups[2].Value
        $parts = $pAttr -split ','
        $oldTime = [double]$parts[0]
        $newTime = $oldTime - $start
        $parts[0] = $newTime.ToString("F3")
        $newP = $parts -join ','
        [void]$sb.AppendLine("    <d p=""$newP"">$content</d>")
    }
    
    [void]$sb.AppendLine('</i>')
    
    $epFile = Join-Path $outDir "hua-shao-2-ep$epNum.xml"
    [System.IO.File]::WriteAllText($epFile, $sb.ToString(), [System.Text.Encoding]::UTF8)
    
    $sh = [math]::Floor($start / 3600)
    $sm = [math]::Floor(($start % 3600) / 60)
    $eh = [math]::Floor($end / 3600)
    $em = [math]::Floor(($end % 3600) / 60)
    $fSize = (Get-Item $epFile).Length
    Write-Output "EP${epNum}: ${sh}h${sm}m ~ ${eh}h${em}m | $($epItems.Count) danmaku | $([math]::Round($fSize/1024))KB"
}

Write-Output "`nDone! Files saved to: $outDir"
