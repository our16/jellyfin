# AGENTS.md - 开发备忘与踩坑记录

## 项目结构

- `F:\github\jellyfin` - 服务端 + Desktop 桌面应用
- `F:\github\jellyfin-web` - Web 前端（webpack 构建，需要 Node.js >= 24）
- 构建脚本: `build-desktop-package.ps1`
- 打包输出: `artifacts/Jellyfin-Desktop-Package/`
- Zip 包: `artifacts/Jellyfin-Desktop-Windows-x64.zip`

## 构建命令

```powershell
# 服务端
dotnet build Jellyfin.Server/Jellyfin.Server.csproj -c Release

# Desktop 桌面应用
dotnet build Jellyfin.Desktop/Jellyfin.Desktop.csproj -c Release

# Web 前端
cd F:\github\jellyfin-web
npm ci --registry https://registry.npmjs.org
npm run build:production

# 一键打包
powershell -ExecutionPolicy Bypass -File build-desktop-package.ps1
```

## 踩坑记录

### 1. Desktop 打包缺少 DLL（服务启动失败）

**现象**: 启动 Jellyfin.Desktop.exe 后页面一直显示 "Jellyfin is still starting"

**原因**: `build-desktop-package.ps1` 只复制了 `Jellyfin.Desktop.exe`，缺少 29 个依赖 DLL（`Jellyfin.Desktop.dll`、`Microsoft.Extensions.*.dll` 等），导致 exe 无法运行

**修复**: 改为复制 `Jellyfin.Desktop.*` 全部文件（exe + dll + runtimeconfig + deps.json）

**教训**: .NET Framework-dependent 发布必须包含所有程序集 DLL，不能只复制 exe

### 2. 服务无限重启循环

**现象**: 服务反复启动-退出-启动，永远停不下来

**原因**:
- `StartAsync` 成功后重置 `_restartAttempts = 0`，导致最大重启次数永远不会触发
- `StopAsync` 调用 `Kill()` 触发 `Exited` 事件，又被 `_isStopping` 逻辑漏掉（事件先于标志位设置）

**修复**:
- `_restartAttempts` 只在健康检查通过后重置（表示服务稳定运行）
- 用独立 `CancellationTokenSource` 管理重启调度
- `StopAsync` 中先取消待执行的重启，再取消订阅 `Exited` 事件
- `_isStopping` 使用 `volatile` 保证线程可见性

**教训**: 进程生命周期管理要注意事件触发顺序，Kill() 后 Exited 事件可能在 StopAsync 返回前触发

### 3. Web 前端浏览器缓存旧 Chunk

**现象**: 代码已修改但页面不生效，ChunkLoadError

**原因**: 浏览器缓存了旧的 webpack chunk hash，新构建的 chunk 文件名不同但旧 HTML 引用的文件不存在

**修复**: 硬刷新（Ctrl+Shift+R）或清除浏览器缓存 + Service Worker

**教训**: 每次重新构建 web 前端后，必须硬刷新浏览器

### 4. Dashboard 按钮不显示（Layout 问题）

**现象**: `DashboardButton.tsx` 代码正确，但页面看不到按钮

**原因**: `RootAppRouter.tsx` 根据 `layoutManager.modern` 选择路由：
```tsx
// RootAppRouter.tsx:26
...(layoutManager.modern ? MODERN_APP_ROUTES : LEGACY_APP_ROUTES)
```
如果用户 layout 是 `desktop-legacy`、`tv`、`mobile-legacy`，现代 AppToolbar 不会渲染

**修复**: 在 `RootAppLayout` 中增加固定定位的 Dashboard 按钮，仅在 legacy 布局时显示

**教训**: 修改 UI 组件时要确认目标 layout 模式，modern 和 legacy 走完全不同的路由和组件树

### 5. Dashboard 按钮不显示（IsAdministrator 问题）

**现象**: Legacy 设置页无管理台区块，按钮仍不显示

**原因**: 服务重启导致数据库状态异常，`user.Policy.IsAdministrator` 返回 `false`

**修复**: 去掉 `IsAdministrator` 检查，桌面单用户场景下任何登录用户都显示

**教训**: 桌面单用户场景不要依赖服务端权限判断，客户端直接显示即可

### 6. Web 构建需要官方 npm Registry

**现象**: `npm ci` 安装失败，缺少 `@fontsource/noto-sans-hk` 等包

**原因**: npmmirror 镜像不完整，某些包未同步

**修复**: 使用官方 registry `npm ci --registry https://registry.npmjs.org`

## 关键文件

| 文件 | 用途 |
|------|------|
| `Jellyfin.Desktop/Program.cs` | 入口点，双模式（托盘/服务） |
| `Jellyfin.Desktop/Services/ServerProcessManager.cs` | 服务进程生命周期管理 |
| `Jellyfin.Desktop/Configuration/DesktopOptions.cs` | 桌面应用配置 |
| `build-desktop-package.ps1` | 一键构建打包脚本 |
| `src/apps/modern/components/AppToolbar/DashboardButton.tsx` | 现代布局 Dashboard 按钮 |
| `src/RootAppRouter.tsx` | 根路由 + legacy 布局 Dashboard 按钮 |
| `src/apps/legacy/routes/user/settings/index.tsx` | Legacy 设置页 Dashboard 链接 |
