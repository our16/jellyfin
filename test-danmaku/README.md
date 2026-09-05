# Jellyfin Danmaku Plugin - Test Guide

## 文件说明

| 文件 | 说明 |
|------|------|
| `test-anime-ep01.xml` | 标准测试弹幕 - 包含各种类型（滚动/顶部/底部/逆向/不同颜色） |
| `test-anime-ep02.xml` | 第二集测试弹幕 - 不同时间分布和颜色 |
| `test-movie.json` | JSON 格式弹幕 - 测试 JSON 解析 |
| `test-dense-danmaku.xml` | 密集弹幕测试 - 模拟高潮场景弹幕密度 |
| `test-api.bat` | API 测试脚本 (Windows) |

## 快速测试

### 1. 将测试文件放入本地弹幕目录

```powershell
# 创建本地弹幕目录
New-Item -ItemType Directory -Path "$env:APPDATA\Jellyfin\data\danmaku" -Force

# 复制测试文件
Copy-Item test-danmaku\*.xml "$env:APPDATA\Jellyfin\data\danmaku\" -Force
Copy-Item test-danmaku\*.json "$env:APPDATA\Jellyfin\data\danmaku\" -Force
```

### 2. 获取 API Token

```bash
curl -X POST http://localhost:8096/Users/AuthenticateByName \
  -H "Content-Type: application/json" \
  -d '{"Username":"admin","Pw":"your_password"}'
```

### 3. 测试各端点

```bash
# 设置 Token
TOKEN="your_token_here"

# 获取弹幕源
curl -H "Authorization: MediaBrowser Token=$TOKEN" \
  http://localhost:8096/api/danmaku/sources

# 搜索弹幕
curl -H "Authorization: MediaBrowser Token=$TOKEN" \
  "http://localhost:8096/api/danmaku/search?keyword=test"

# 测试 Bilibili 连接
curl -X POST -H "Authorization: MediaBrowser Token=$TOKEN" \
  http://localhost:8096/api/danmaku/sources/bilibili/test

# 弹弹play 兼容搜索
curl -H "Authorization: MediaBrowser Token=$TOKEN" \
  "http://localhost:8096/api/v2/search/anime?keyword=test"
```

## 弹幕类型参考

| Type | 名称 | 说明 |
|------|------|------|
| 1 | ScrollRL | 滚动弹幕 (从右到左) |
| 4 | Bottom | 底部固定弹幕 |
| 5 | Top | 顶部固定弹幕 |
| 6 | ScrollLR | 滚动弹幕 (从左到右) |
| 7 | Special | 高级弹幕 |

## 颜色参考 (RGB888 十进制)

| 颜色 | HEX | 十进制 |
|------|-----|--------|
| 白色 | #FFFFFF | 16777215 |
| 红色 | #FE0302 | 16646914 |
| 黄色 | #FFFF00 | 16776960 |
| 绿色 | #00CD00 | 52480 |
| 紫色 | #4266BE | 4351678 |
| 品红 | #CC0273 | 13369971 |
| 青色 | #89D5FF | 9022215 |
