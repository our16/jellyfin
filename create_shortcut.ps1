$TargetPath = "F:\github\jellyfin\artifacts\Jellyfin.Desktop\Jellyfin.Desktop.exe"
$ShortcutPath = [Environment]::GetFolderPath("Desktop") + "\Jellyfin Desktop.lnk"
$WorkingDir = Split-Path $TargetPath

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = $TargetPath
$Shortcut.WorkingDirectory = $WorkingDir
$Shortcut.IconLocation = $TargetPath
$Shortcut.Description = "Jellyfin Desktop"
$Shortcut.Save()

Write-Host "Shortcut created: $ShortcutPath"