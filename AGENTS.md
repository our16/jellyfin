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

### 7. B站弹幕 API 限制

**现象**: XML API (`comment.bilibili.com/{cid}.xml` 和 `api.bilibili.com/x/v1/dm/list.so`) 只返回最多 8000 条弹幕，但页面显示 9.7万

**原因**: B站 XML API 有 `maxlimit` 限制，且只返回活跃弹幕

**修复**: 使用 protobuf 分段 API `api.bilibili.com/x/v2/dm/web/seg.so?type=1&oid={cid}&segment_index={n}`，可获取全部弹幕（约 35000+ 条）

**实现**: 在 `MediaBrowser.Providers/Plugins/Danmaku/Sources/BilibiliSource.cs` 中，`GetDanmakuXmlAsync` 现在通过 `FetchProtobufDanmakuAsync` 循环获取所有分段，用 `ParseProtobufBytes` + `ParseDanmakuElem` 手动解析 protobuf wire format，最后用 `BuildXmlString` 转换为 XML 字符串

**教训**: B站弹幕 API 有多种实现，XML API 有数量限制，protobuf API 无限制但需要手动解析二进制数据

### 8. SESSDATA Cookie 大幅提升弹幕量

**现象**: 带 SESSDATA Cookie 请求 seg.so 首段返回 536KB，无 Cookie 仅 94KB（5.7 倍差距）

**原因**: B站文档"部分视频在无 Cookie: SESSDATA 时只返回部分弹幕"——Cookie 可获取完整弹幕池

**实现**: 
- `PluginConfiguration.BilibiliSessdata` 配置字段
- `DanmakuConfigManager` 单例管理运行时配置
- `BilibiliSource.FetchProtobufDanmakuAsync` 请求时带 Cookie
- 同时请求 `/x/v2/dm/web/view` 获取元数据（count, special_dms）
- 下载 BAS/代码弹幕专包（mode=8/9，seg.so 不返回）

### 9. B站 -352 风控（IP 级）

**现象**: 短时间连续请求 seg.so 后返回 `{"code":-352,"message":"-352","ttl":1}`

**原因**: IP 级滑动窗口风控，与 Cookie 无关，累积请求约 20 段后触发

**恢复**: 等待 10-15 分钟冷却

**规避策略**:
- 分批拉取：15 段/批，内部间隔 4s，批间间隔 60±5s
- 断点续传：记录最后成功段号到 `fetch-progress.txt`
- 风控后等待 30s 重试，连续 5 次失败则停止

### 10. GetCidAsync 大 CID 溢出

**现象**: `FormatException: One of the identified items was in an invalid format` 对 CID=36189962748

**原因**: B站 API 返回的 cid 是 int64，但代码用 `GetInt32()` 解析

**修复**: 改用 `GetInt64()` 再转 `int`，增加 API 错误码检查

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
| `MediaBrowser.Providers/Plugins/Danmaku/Sources/BilibiliSource.cs` | B站弹幕源（protobuf + Cookie） |
| `MediaBrowser.Providers/Plugins/Danmaku/Services/DanmakuService.cs` | 弹幕核心业务逻辑 |
| `MediaBrowser.Providers/Plugins/Danmaku/Services/DanmakuCacheManager.cs` | 缓存管理 |
| `MediaBrowser.Providers/Plugins/Danmaku/Configuration/PluginConfiguration.cs` | 插件配置（含 SESSDATA） |
| `Jellyfin.Server/Startup.cs` | DI 服务注册 |
| `test-danmaku/fetch-progress.txt` | 断点续传进度 |
| `test-danmaku/bilibili-cookie-v3.bin` | 已拉取的 protobuf 原始数据 |
