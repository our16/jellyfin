# Danmaku Fetch TODO

## 当前进度
- **已拉取**: 22 段 / ~86 段 (约 26%)
- **数据文件**: `bilibili-cookie-v3.bin` (1922KB, ~13500 条弹幕)
- **断点位置**: seg 22 (进度文件: `fetch-progress.txt`)
- **风控状态**: seg 23 触发 -352，需等待 10-15 分钟冷却

## 下次拉取步骤

### 1. 等待风控冷却 (10-15 分钟)

### 2. 继续拉取 (断点续传)
```powershell
# 读取进度，从 seg 23 继续
$lastSeg = [int](Get-Content "F:\github\jellyfin\test-danmaku\fetch-progress.txt" -Raw)
# 策略: 15段/批, 内部间隔 4s, 批间间隔 60±5s
```

### 3. 风控触发后
- 记录当前 seg 到 `fetch-progress.txt`
- 等待 10-15 分钟
- 重新运行脚本继续

### 4. 全部拉完后
- 解析 protobuf → XML
- 按时间拆分 11 集 (每集 2676 秒)
- 部署到 `C:\Users\Admin\AppData\Local\jellyfin\data\danmaku-cache\{itemId}.xml`
- 重启 Jellyfin 服务

## 关键参数
| 参数 | 值 |
|------|-----|
| CID | 36189962748 |
| BVID | BV1UvfEB7EX9 |
| 总时长 | 29434 秒 (8.2 小时) |
| 分段数 | ~86 段 (每段 6 分钟) |
| Cookie | 已配置在插件 settings |
| 风控限制 | ~20 段/IP/小时 |

## 文件位置
- 原始数据: `test-danmaku/bilibili-cookie-v3.bin`
- 进度文件: `test-danmaku/fetch-progress.txt`
- 缓存目录: `C:\Users\Admin\AppData\Local\jellyfin\data\danmaku-cache\`
- 插件配置: `PUT /api/danmaku/config` (bilibiliSessdata)

## 11 集 ItemId 映射
| 期数 | ItemId |
|------|--------|
| 20150425 | 2ef335bb-22cc-9727-f8df-ecc1f897e9f7 |
| 20150502 | 3e40e99c-9a56-da88-a91c-c1a9b8bcd1d1 |
| 20150509 | aed4d77f-f6fd-2288-377f-2245e9651967 |
| 20150516 | 0a1c0170-5b25-f0b0-5571-82bd1fb54e7a |
| 20150523 | b171d2ba-79df-5c33-5aa0-c4775dfc3f59 |
| 20150530 | 2671229a-cb41-24f1-e1b9-99833e4e6b16 |
| 20150606 | 1155ec29-d89a-f781-72af-a2155c848d9e |
| 20150613 | 8f4669aa-78a7-db83-40dc-2d5505b7a9cc |
| 20150620 | 28a141e3-c11a-c090-8a77-2565f7acee96 |
| 20150627 | a5cff592-6e23-8191-1328-f227e88a4982 |
| 20150704 | a09b0cee-ec71-687b-589e-2662da95d7d8 |
