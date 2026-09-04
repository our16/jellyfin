using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.Danmaku.Models;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.Danmaku.Services
{
    /// <summary>
    /// Manages danmaku file caching on the local filesystem.
    /// </summary>
    public class DanmakuCacheManager : IDisposable
    {
        private readonly ILogger<DanmakuCacheManager> _logger;
        private readonly string _cacheDir;
        private readonly string _indexPath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private List<CacheItem> _index = new();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DanmakuCacheManager"/> class.
        /// </summary>
        public DanmakuCacheManager(ILogger<DanmakuCacheManager> logger, string dataPath)
        {
            _logger = logger;
            _cacheDir = Path.Combine(dataPath, "danmaku-cache");
            _indexPath = Path.Combine(_cacheDir, "cache-index.json");
            Directory.CreateDirectory(_cacheDir);
        }

        /// <summary>
        /// Get cached danmaku XML content.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>Cached XML content or null.</returns>
        public async Task<string?> GetAsync(string itemId)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var filePath = GetFilePath(itemId, "xml");
                if (File.Exists(filePath))
                {
                    UpdateAccessTime(itemId);
                    return await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                }

                var jsonPath = GetFilePath(itemId, "json");
                if (File.Exists(jsonPath))
                {
                    UpdateAccessTime(itemId);
                    return await File.ReadAllTextAsync(jsonPath).ConfigureAwait(false);
                }

                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Get the file path for a cached item.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <param name="format">The format (xml/json).</param>
        /// <returns>File path or null if not cached.</returns>
        public string? GetCachedFilePath(string itemId, string format)
        {
            var filePath = GetFilePath(itemId, format);
            return File.Exists(filePath) ? filePath : null;
        }

        /// <summary>
        /// Save danmaku content to cache.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <param name="itemName">The item name.</param>
        /// <param name="source">The source identifier.</param>
        /// <param name="sourceId">The source video ID.</param>
        /// <param name="content">The danmaku content (XML or JSON).</param>
        /// <param name="format">The format.</param>
        /// <param name="danmakuCount">Number of danmaku items.</param>
        public async Task SaveAsync(string itemId, string itemName, string source, string sourceId, string content, string format, int danmakuCount)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var filePath = GetFilePath(itemId, format);
                await File.WriteAllTextAsync(filePath, content).ConfigureAwait(false);

                var fileSize = new FileInfo(filePath).Length;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var expiryDays = Plugin.Instance?.Configuration.CacheExpiryDays ?? 30;

                // Remove existing entry
                _index.RemoveAll(e => e.ItemId == itemId);

                _index.Add(new CacheItem
                {
                    ItemId = itemId,
                    ItemName = itemName,
                    Source = source,
                    SourceId = sourceId,
                    CachedAt = now,
                    ExpiresAt = now + (long)expiryDays * 86400,
                    FileSize = fileSize,
                    DanmakuCount = danmakuCount,
                    Format = format
                });

                await SaveIndexAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Delete cached danmaku for an item.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        public async Task DeleteAsync(string itemId)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var xmlPath = GetFilePath(itemId, "xml");
                if (File.Exists(xmlPath))
                {
                    File.Delete(xmlPath);
                }

                var jsonPath = GetFilePath(itemId, "json");
                if (File.Exists(jsonPath))
                {
                    File.Delete(jsonPath);
                }

                _index.RemoveAll(e => e.ItemId == itemId);
                await SaveIndexAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Cleanup expired cache entries.
        /// </summary>
        /// <returns>Number of removed items and freed size.</returns>
        public async Task<CacheCleanupResult> CleanupAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var expired = _index.Where(e => e.ExpiresAt < now).ToList();
                var freedSize = 0L;

                foreach (var item in expired)
                {
                    var xmlPath = GetFilePath(item.ItemId, "xml");
                    if (File.Exists(xmlPath))
                    {
                        freedSize += new FileInfo(xmlPath).Length;
                        File.Delete(xmlPath);
                    }

                    var jsonPath = GetFilePath(item.ItemId, "json");
                    if (File.Exists(jsonPath))
                    {
                        freedSize += new FileInfo(jsonPath).Length;
                        File.Delete(jsonPath);
                    }
                }

                _index.RemoveAll(e => e.ExpiresAt < now);
                await SaveIndexAsync().ConfigureAwait(false);

                return new CacheCleanupResult
                {
                    RemovedCount = expired.Count,
                    FreedSize = freedSize,
                    RemainingItems = _index.Count
                };
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Clear all cached danmaku.
        /// </summary>
        public async Task ClearAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Directory.Exists(_cacheDir))
                {
                    foreach (var file in Directory.GetFiles(_cacheDir, "*.xml"))
                    {
                        File.Delete(file);
                    }

                    foreach (var file in Directory.GetFiles(_cacheDir, "*.json"))
                    {
                        File.Delete(file);
                    }
                }

                _index.Clear();
                await SaveIndexAsync().ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Get cache statistics.
        /// </summary>
        /// <param name="maxSize">Maximum cache size.</param>
        /// <returns>Cache statistics.</returns>
        public async Task<CacheStatsResponse> GetStatsAsync(long maxSize)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var totalSize = _index.Sum(e => e.FileSize);
                var oldest = _index.OrderBy(e => e.CachedAt).FirstOrDefault();
                var newest = _index.OrderByDescending(e => e.CachedAt).FirstOrDefault();

                return new CacheStatsResponse
                {
                    TotalItems = _index.Count,
                    TotalSize = totalSize,
                    MaxSize = maxSize,
                    UsagePercent = maxSize > 0 ? (float)totalSize / maxSize * 100 : 0,
                    OldestItem = oldest,
                    NewestItem = newest
                };
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Get paginated cache list.
        /// </summary>
        /// <param name="startIndex">Start index.</param>
        /// <param name="limit">Max items.</param>
        /// <param name="sortBy">Sort field (cachedAt/fileSize).</param>
        /// <param name="sortOrder">Sort order (Ascending/Descending).</param>
        /// <returns>Total count and items.</returns>
        public async Task<(int TotalCount, CacheItem[] Items)> GetListAsync(int startIndex, int limit, string sortBy, string sortOrder)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                IEnumerable<CacheItem> query = sortBy?.ToLowerInvariant() switch
                {
                    "filesize" => sortOrder == "Descending"
                        ? _index.OrderByDescending(e => e.FileSize)
                        : _index.OrderBy(e => e.FileSize),
                    _ => sortOrder == "Descending"
                        ? _index.OrderByDescending(e => e.CachedAt)
                        : _index.OrderBy(e => e.CachedAt)
                };

                var items = query.Skip(startIndex).Take(limit).ToArray();
                return (_index.Count, items);
            }
            finally
            {
                _lock.Release();
            }
        }

        private string GetFilePath(string itemId, string format)
        {
            var safeId = string.Join("_", itemId.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_cacheDir, $"{safeId}.{format}");
        }

        private void UpdateAccessTime(string itemId)
        {
            // Touch the file to update access time
            var filePath = GetFilePath(itemId, "xml");
            if (File.Exists(filePath))
            {
                File.SetLastAccessTimeUtc(filePath, DateTime.UtcNow);
            }
        }

        private async Task SaveIndexAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_index, _jsonOptions);
                await File.WriteAllTextAsync(_indexPath, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving cache index");
            }
        }

        private async Task LoadIndexAsync()
        {
            try
            {
                if (File.Exists(_indexPath))
                {
                    var json = await File.ReadAllTextAsync(_indexPath).ConfigureAwait(false);
                    _index = JsonSerializer.Deserialize<List<CacheItem>>(json, _jsonOptions) ?? new List<CacheItem>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading cache index, starting fresh");
                _index = new List<CacheItem>();
            }
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose().</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _lock.Dispose();
                }

                _disposed = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
