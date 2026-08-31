# Jellyfin Desktop 一键构建打包脚本
# 用法: .\build-desktop-package.ps1

$ErrorActionPreference = "Stop"
$Root = "F:\github\jellyfin"
$WebSource = "F:\github\jellyfin-web"
$OutputDir = "$Root\artifacts\Jellyfin-Desktop-Package"
$PackageName = "Jellyfin-Desktop-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

Write-Host "=== Jellyfin Desktop 构建打包 ===" -ForegroundColor Cyan

# 1. 清理输出目录
Write-Host "`n[1/5] 清理输出目录..." -ForegroundColor Yellow
if (Test-Path $OutputDir) { Remove-Item -Recurse -Force $OutputDir }
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# 2. 构建 Jellyfin.Server
Write-Host "`n[2/5] 构建 Jellyfin.Server..." -ForegroundColor Yellow
dotnet publish "$Root\Jellyfin.Server\Jellyfin.Server.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishTrimmed=false `
    -o "$OutputDir\server" 2>&1
if ($LASTEXITCODE -ne 0) { throw "Server 构建失败" }

# 3. 构建 Jellyfin.Desktop
Write-Host "`n[3/5] 构建 Jellyfin.Desktop..." -ForegroundColor Yellow
dotnet publish "$Root\Jellyfin.Desktop\Jellyfin.Desktop.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishTrimmed=false `
    -o "$OutputDir\desktop" 2>&1
if ($LASTEXITCODE -ne 0) { throw "Desktop 构建失败" }

# 4. 构建 Web 客户端
Write-Host "`n[4/5] 构建 Web 客户端..." -ForegroundColor Yellow
Push-Location $WebSource
try {
    if (-not (Test-Path "node_modules")) {
        Write-Host "  安装依赖..." -ForegroundColor Gray
        npm ci
    }
    npm run build:production
    if ($LASTEXITCODE -ne 0) { throw "Web 构建失败" }
}
finally { Pop-Location }

# 5. 整合打包
Write-Host "`n[5/5] 整合打包..." -ForegroundColor Yellow

# 创建 web 目录
$WebDist = "$OutputDir\jellyfin-web\dist"
New-Item -ItemType Directory -Path $WebDist -Force | Out-Null
Copy-Item -Path "$WebSource\dist\*" -Destination $WebDist -Recurse -Force

# 复制 desktop exe 到根目录（作为启动器）
Copy-Item -Path "$OutputDir\desktop\Jellyfin.Desktop.exe" -Destination $OutputDir -Force

# 复制 server 内容到根目录
Get-ChildItem "$OutputDir\server" | Where-Object { $_.Name -notin @("Jellyfin.Server.deps.json","Jellyfin.Server.runtimeconfig.json") } | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $OutputDir -Recurse -Force
}

# 复制 desktop 配置文件
Copy-Item -Path "$OutputDir\desktop\appsettings.json" -Destination $OutputDir -Force -ErrorAction SilentlyContinue

# 清理临时目录
Remove-Item -Recurse -Force "$OutputDir\server" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "$OutputDir\desktop" -ErrorAction SilentlyContinue

# 修改配置文件启用 web 客户端
$ConfigFile = "F:\github\jellyfin\artifacts\Jellyfin-Desktop-Package\appsettings.json"
Write-Host "  配置文件: $ConfigFile" -ForegroundColor Gray
if (Test-Path $ConfigFile) {
    $Config = Get-Content $ConfigFile -Raw
    $Config = $Config -replace '"ServerArguments":\s*"[^"]*"', '"ServerArguments": "--webdir ./jellyfin-web/dist"'
    Set-Content -Path $ConfigFile -Value $Config -Encoding UTF8
    Write-Host "  已配置 web 客户端" -ForegroundColor Green
} else {
    Write-Host "  配置文件不存在" -ForegroundColor Red
}

# 创建启动脚本
$BatContent = @"
@echo off
echo Starting Jellyfin Desktop...
cd /d "%~dp0"
start "" Jellyfin.Desktop.exe
"@
Set-Content -Path "$OutputDir\启动Jellyfin.bat" -Value $BatContent -Encoding Default

# 创建 zip 包
$ZipPath = "$Root\artifacts\$PackageName.zip"
Write-Host "  压缩: $ZipPath" -ForegroundColor Gray
Compress-Archive -Path "$OutputDir\*" -DestinationPath $ZipPath -Force

# 统计
$ExeSize = [math]::Round((Get-Item "$OutputDir\Jellyfin.Desktop.exe").Length / 1MB, 1)
$TotalSize = [math]::Round((Get-ChildItem $OutputDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
$ZipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)

Write-Host "`n=== 构建完成 ===" -ForegroundColor Green
Write-Host "  启动器: $OutputDir\Jellyfin.Desktop.exe"
Write-Host "  总大小: ${TotalSize} MB"
Write-Host "  压缩包: $ZipPath (${ZipSize} MB)"
Write-Host "`n运行: 启动Jellyfin.bat 或 Jellyfin.Desktop.exe" -ForegroundColor Cyan
