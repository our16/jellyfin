@echo off
REM =====================================================
REM  Jellyfin Danmaku Plugin - API Test Script
REM =====================================================
REM  使用方法:
REM    1. 先登录 Jellyfin 获取 Token
REM    2. 设置 SERVER 和 TOKEN 变量
REM    3. 运行此脚本
REM =====================================================

SET SERVER=http://localhost:8096
SET TOKEN=YOUR_TOKEN_HERE

echo ==========================================
echo  Jellyfin Danmaku Plugin - API Tests
echo ==========================================
echo.

echo [1] 测试: 获取弹幕源列表
echo GET %SERVER%/api/danmaku/sources
curl -s "%SERVER%/api/danmaku/sources" -H "Authorization: MediaBrowser Token=%TOKEN%" | python -m json.tool 2>nul || echo (需要 python 显示格式化 JSON)
echo.
echo.

echo [2] 测试: 搜索弹幕 (关键词: 超电磁炮)
echo GET %SERVER%/api/danmaku/search?keyword=超电磁炮
curl -s "%SERVER%/api/danmaku/search?keyword=%E8%B6%85%E7%94%B5%E7%A3%81%E7%82%B0" -H "Authorization: MediaBrowser Token=%TOKEN%" | python -m json.tool 2>nul
echo.
echo.

echo [3] 测试: 获取全局配置
echo GET %SERVER%/api/danmaku/config
curl -s "%SERVER%/api/danmaku/config" -H "Authorization: MediaBrowser Token=%TOKEN%" | python -m json.tool 2>nul
echo.
echo.

echo [4] 测试: 获取缓存统计
echo GET %SERVER%/api/danmaku/cache/stats
curl -s "%SERVER%/api/danmaku/cache/stats" -H "Authorization: MediaBrowser Token=%TOKEN%" | python -m json.tool 2>nul
echo.
echo.

echo [5] 测试: 弹弹play 兼容接口 - 搜索动漫
echo GET %SERVER%/api/v2/search/anime?keyword=test
curl -s "%SERVER%/api/v2/search/anime?keyword=test" -H "Authorization: MediaBrowser Token=%TOKEN%" | python -m json.tool 2>nul
echo.
echo.

echo [6] 测试: 测试 Bilibili 源连接
echo POST %SERVER%/api/danmaku/sources/bilibili/test
curl -s -X POST "%SERVER%/api/danmaku/sources/bilibili/test" -H "Authorization: MediaBrowser Token=%TOKEN%" | python -m json.tool 2>nul
echo.
echo.

echo [7] 测试: 测试 Dandanplay 源连接
echo POST %SERVER%/api/danmaku/sources/dandanplay/test
curl -s -X POST "%SERVER%/api/danmaku/sources/dandanplay/test" -H "Authorization: MediaBrowser Token=%TOKEN%" | python -m json.tool 2>nul
echo.
echo.

echo [8] 测试: 更新全局配置
echo PUT %SERVER%/api/danmaku/config
curl -s -X PUT "%SERVER%/api/danmaku/config" -H "Authorization: MediaBrowser Token=%TOKEN%" -H "Content-Type: application/json" -d "{\"enabled\":true,\"defaultEnabled\":true,\"autoMatch\":true,\"maxCacheSize\":1073741824,\"cacheExpiryDays\":30}" | python -m json.tool 2>nul
echo.
echo.

echo [9] 测试: 清理过期缓存
echo POST %SERVER%/api/danmaku/cache/cleanup
curl -s -X POST "%SERVER%/api/danmaku/cache/cleanup" -H "Authorization: MediaBrowser Token=%TOKEN%" | python -m json.tool 2>nul
echo.
echo.

echo [10] 测试: 获取弹幕 (需要有效 itemId)
echo GET %SERVER%/api/danmaku/{itemId}
echo (请替换 {itemId} 为实际的媒体项目 UUID)
echo 示例: curl -s "%SERVER%/api/danmaku/550e8400-e29b-41d4-a716-446655440000" -H "Authorization: MediaBrowser Token=%TOKEN%"
echo.
echo.

echo ==========================================
echo  所有测试完成
echo ==========================================
echo.
echo 提示:
echo   - 请确保 Jellyfin Server 已启动
echo   - 请将 TOKEN 替换为有效的访问令牌
echo   - 获取 Token: POST %SERVER%/Users/AuthenticateByName
echo.
