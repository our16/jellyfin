using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
#pragma warning disable CA2016 // Do not forward CancellationToken to Task.Run
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Providers.Plugins.Danmaku.Models;
using MediaBrowser.Providers.Plugins.Danmaku.Sources;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.Danmaku.Services
{
    /// <summary>
    /// Core danmaku service providing business logic for danmaku operations.
    /// </summary>
    public class DanmakuService
    {
        private readonly ILogger<DanmakuService> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly DanmakuCacheManager _cacheManager;
        private readonly BilibiliSource _bilibiliSource;
        private readonly DandanplaySource _dandanplaySource;
        private readonly LocalFileSource _localSource;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object?>> _pendingTasks = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="DanmakuService"/> class.
        /// </summary>
        public DanmakuService(
            ILogger<DanmakuService> logger,
            ILibraryManager libraryManager,
            DanmakuCacheManager cacheManager,
            BilibiliSource bilibiliSource,
            DandanplaySource dandanplaySource,
            LocalFileSource localSource)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _cacheManager = cacheManager;
            _bilibiliSource = bilibiliSource;
            _dandanplaySource = dandanplaySource;
            _localSource = localSource;
        }

        private IDanmakuSource[] AllSources => new IDanmakuSource[] { _localSource, _bilibiliSource, _dandanplaySource };

        /// <summary>
        /// Get danmaku info for an item.
        /// </summary>
        public async Task<DanmakuFileInfo?> GetDanmakuInfoAsync(Guid itemId, string? mediaSourceId, CancellationToken ct)
        {
            var item = _libraryManager.GetItemById(itemId);
            if (item == null)
            {
                return null;
            }

            // Check cache first
            var cachedContent = await _cacheManager.GetAsync(itemId.ToString()).ConfigureAwait(false);
            if (cachedContent != null)
            {
                return new DanmakuFileInfo
                {
                    ItemId = itemId,
                    MediaSourceId = mediaSourceId,
                    HasDanmaku = true,
                    Source = "cached",
                    Format = "xml",
                    LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
            }

            // Try auto-match
            var config = Plugin.Instance?.Configuration;
            if (config?.AutoMatch == true)
            {
                var sources = config.AutoMatchSources ?? new[] { "bilibili", "dandanplay" };
                foreach (var sourceId in sources)
                {
                    var source = GetSource(sourceId);
                    if (source == null || !source.IsEnabled)
                    {
                        continue;
                    }

                    var searchResults = await source.SearchAsync(item.Name ?? string.Empty, 1, ct).ConfigureAwait(false);
                    if (searchResults.Length > 0)
                    {
                        var result = searchResults[0];
                        var xml = await source.GetDanmakuXmlAsync(result.SourceId, result.SourceCid, ct).ConfigureAwait(false);
                        if (xml != null)
                        {
                            var items = source.ParseXml(xml);
                            await _cacheManager.SaveAsync(
                                itemId.ToString(),
                                item.Name ?? "Unknown",
                                source.Id,
                                result.SourceId,
                                xml,
                                "xml",
                                items.Length).ConfigureAwait(false);

                            return new DanmakuFileInfo
                            {
                                ItemId = itemId,
                                MediaSourceId = mediaSourceId,
                                HasDanmaku = true,
                                DanmakuCount = items.Length,
                                Source = source.Id,
                                SourceId = result.SourceId,
                                SourceCid = result.SourceCid,
                                LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                Format = "xml",
                                MatchConfidence = result.MatchScore
                            };
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get the download URL for danmaku content.
        /// </summary>
        public async Task<DanmakuFileUrl?> GetDanmakuUrlAsync(Guid itemId, string? mediaSourceId, string format, CancellationToken ct)
        {
            var filePath = _cacheManager.GetCachedFilePath(itemId.ToString(), format);
            if (filePath != null)
            {
                return new DanmakuFileUrl
                {
                    Url = $"/Danmaku/{itemId}/danmaku.{format}",
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds(),
                    Format = format,
                    FileSize = new System.IO.FileInfo(filePath).Length
                };
            }

            // Try to fetch and cache
            var info = await GetDanmakuInfoAsync(itemId, mediaSourceId, ct).ConfigureAwait(false);
            if (info == null || !info.HasDanmaku)
            {
                return null;
            }

            filePath = _cacheManager.GetCachedFilePath(itemId.ToString(), format);
            if (filePath != null)
            {
                return new DanmakuFileUrl
                {
                    Url = $"/Danmaku/{itemId}/danmaku.{format}",
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds(),
                    Format = format,
                    FileSize = new System.IO.FileInfo(filePath).Length
                };
            }

            return null;
        }

        /// <summary>
        /// Get raw danmaku content.
        /// </summary>
        public async Task<string?> GetDanmakuRawAsync(Guid itemId, string? mediaSourceId, CancellationToken ct)
        {
            var cached = await _cacheManager.GetAsync(itemId.ToString()).ConfigureAwait(false);
            if (cached != null)
            {
                return cached;
            }

            var info = await GetDanmakuInfoAsync(itemId, mediaSourceId, ct).ConfigureAwait(false);
            if (info == null || !info.HasDanmaku)
            {
                return null;
            }

            return await _cacheManager.GetAsync(itemId.ToString()).ConfigureAwait(false);
        }

        /// <summary>
        /// Refresh danmaku for an item.
        /// </summary>
        public async Task<string> RefreshDanmakuAsync(Guid itemId, string? source, bool force, CancellationToken ct)
        {
            var taskId = Guid.NewGuid().ToString("N")[..12];
            _pendingTasks[taskId] = new TaskCompletionSource<object?>();

            _ = Task.Run(async () =>
            {
                try
                {
                    var item = _libraryManager.GetItemById(itemId);
                    if (item == null)
                    {
                        _pendingTasks[taskId].SetResult(null);
                        return;
                    }

                    if (force)
                    {
                        await _cacheManager.DeleteAsync(itemId.ToString()).ConfigureAwait(false);
                    }

                    var sources = source != null
                        ? new IDanmakuSource[] { GetSource(source)! }.Where(s => s != null).Cast<IDanmakuSource>()
                        : AllSources.Where(s => s.IsEnabled).Cast<IDanmakuSource>();

                    foreach (var s in sources)
                    {
                        if (s == null)
                        {
                            continue;
                        }

                        var searchResults = await s.SearchAsync(item.Name ?? string.Empty, 1, ct).ConfigureAwait(false);
                        if (searchResults.Length > 0)
                        {
                            var result = searchResults[0];
                            var xml = await s.GetDanmakuXmlAsync(result.SourceId, result.SourceCid, ct).ConfigureAwait(false);
                            if (xml != null)
                            {
                                var items = s.ParseXml(xml);
                                await _cacheManager.SaveAsync(
                                    itemId.ToString(),
                                    item.Name ?? "Unknown",
                                    s.Id,
                                    result.SourceId,
                                    xml,
                                    "xml",
                                    items.Length).ConfigureAwait(false);
                            }
                        }
                    }

                    _pendingTasks[taskId].SetResult(null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing danmaku for {ItemId}", itemId);
                    _pendingTasks[taskId].SetException(ex);
                }
            });

            return taskId;
        }

        /// <summary>
        /// Delete danmaku cache.
        /// </summary>
        public Task DeleteDanmakuCacheAsync(Guid itemId, CancellationToken ct)
        {
            return _cacheManager.DeleteAsync(itemId.ToString());
        }

        /// <summary>
        /// Search danmaku across all enabled sources.
        /// </summary>
        public async Task<DanmakuSearchResult[]> SearchDanmakuAsync(string keyword, string? sources, int limit, CancellationToken ct)
        {
            var enabledSources = GetSourcesFromString(sources);
            var allResults = new List<DanmakuSearchResult>();

            foreach (var source in enabledSources)
            {
                if (!source.IsEnabled)
                {
                    continue;
                }

                var results = await source.SearchAsync(keyword, limit, ct).ConfigureAwait(false);
                allResults.AddRange(results);
            }

            return allResults
                .OrderByDescending(r => r.MatchScore)
                .Take(limit)
                .ToArray();
        }

        /// <summary>
        /// Search danmaku by item ID.
        /// </summary>
        public async Task<DanmakuSearchResult[]> SearchByItemAsync(Guid itemId, string? sources, int limit, CancellationToken ct)
        {
            var item = _libraryManager.GetItemById(itemId);
            if (item == null)
            {
                return Array.Empty<DanmakuSearchResult>();
            }

            var enabledSources = GetSourcesFromString(sources);
            var allResults = new List<DanmakuSearchResult>();

            foreach (var source in enabledSources)
            {
                if (!source.IsEnabled)
                {
                    continue;
                }

                var results = await source.SearchAsync(item.Name ?? string.Empty, limit, ct).ConfigureAwait(false);
                allResults.AddRange(results);
            }

            return allResults
                .OrderByDescending(r => r.MatchScore)
                .Take(limit)
                .ToArray();
        }

        /// <summary>
        /// Get all configured sources.
        /// </summary>
        public DanmakuSource[] GetAllSources()
        {
            return AllSources.Select(s => new DanmakuSource
            {
                Id = s.Id,
                Name = s.Name,
                Type = s.Type,
                Enabled = s.IsEnabled,
                Priority = s.Priority,
                SupportedFormats = s.SupportedFormats,
                Config = new Dictionary<string, object>
                {
                    ["type"] = s.Type
                },
                Stats = new DanmakuSourceStats
                {
                    TotalRequests = 0,
                    SuccessRate = 1.0f,
                    AvgResponseTime = 0
                }
            }).OrderBy(s => s.Priority).ToArray();
        }

        /// <summary>
        /// Get a specific source by ID.
        /// </summary>
        public IDanmakuSource? GetSource(string sourceId)
        {
            return sourceId.ToLowerInvariant() switch
            {
                "bilibili" => _bilibiliSource,
                "dandanplay" => _dandanplaySource,
                "local" => _localSource,
                _ => null
            };
        }

        private IDanmakuSource[] GetSourcesFromString(string? sources)
        {
            if (string.IsNullOrEmpty(sources))
            {
                return AllSources.Where(s => s.IsEnabled).ToArray();
            }

            var ids = sources.Split(',', StringSplitOptions.RemoveEmptyEntries);
            return ids.Select(id => GetSource(id.Trim())).Where(s => s != null).ToArray()!;
        }
    }

    /// <summary>
    /// Danmaku file URL DTO.
    /// </summary>
    public class DanmakuFileUrl
    {
        /// <summary>
        /// Gets or sets the download URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the expiration timestamp.
        /// </summary>
        public long ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the format.
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file size.
        /// </summary>
        public long FileSize { get; set; }
    }
}
