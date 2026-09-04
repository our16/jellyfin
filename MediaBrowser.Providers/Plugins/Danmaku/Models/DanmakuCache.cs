using System.Text.Json.Serialization;

namespace MediaBrowser.Providers.Plugins.Danmaku.Models
{
    /// <summary>
    /// Cache item entry DTO.
    /// </summary>
    public class CacheItem
    {
        /// <summary>Gets or sets the item ID.</summary>
        [JsonPropertyName("itemId")]
        public string ItemId { get; set; } = string.Empty;

        /// <summary>Gets or sets the item name.</summary>
        [JsonPropertyName("itemName")]
        public string ItemName { get; set; } = string.Empty;

        /// <summary>Gets or sets the source identifier.</summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        /// <summary>Gets or sets the source video ID.</summary>
        [JsonPropertyName("sourceId")]
        public string SourceId { get; set; } = string.Empty;

        /// <summary>Gets or sets the cached timestamp (Unix seconds).</summary>
        [JsonPropertyName("cachedAt")]
        public long CachedAt { get; set; }

        /// <summary>Gets or sets the expiration timestamp (Unix seconds).</summary>
        [JsonPropertyName("expiresAt")]
        public long ExpiresAt { get; set; }

        /// <summary>Gets or sets the file size in bytes.</summary>
        [JsonPropertyName("fileSize")]
        public long FileSize { get; set; }

        /// <summary>Gets or sets the danmaku count.</summary>
        [JsonPropertyName("danmakuCount")]
        public int DanmakuCount { get; set; }

        /// <summary>Gets or sets the format.</summary>
        [JsonPropertyName("format")]
        public string Format { get; set; } = "xml";
    }

    /// <summary>
    /// Cache statistics DTO.
    /// </summary>
    public class CacheStatsResponse
    {
        /// <summary>Gets or sets the total number of cached items.</summary>
        [JsonPropertyName("totalItems")]
        public int TotalItems { get; set; }

        /// <summary>Gets or sets the total cache size in bytes.</summary>
        [JsonPropertyName("totalSize")]
        public long TotalSize { get; set; }

        /// <summary>Gets or sets the maximum cache size in bytes.</summary>
        [JsonPropertyName("maxSize")]
        public long MaxSize { get; set; }

        /// <summary>Gets or sets the usage percentage.</summary>
        [JsonPropertyName("usagePercent")]
        public float UsagePercent { get; set; }

        /// <summary>Gets or sets the oldest cached item.</summary>
        [JsonPropertyName("oldestItem")]
        public CacheItem? OldestItem { get; set; }

        /// <summary>Gets or sets the newest cached item.</summary>
        [JsonPropertyName("newestItem")]
        public CacheItem? NewestItem { get; set; }
    }

    /// <summary>
    /// Cache cleanup result DTO.
    /// </summary>
    public class CacheCleanupResult
    {
        /// <summary>Gets or sets the number of removed items.</summary>
        [JsonPropertyName("removedCount")]
        public int RemovedCount { get; set; }

        /// <summary>Gets or sets the freed size in bytes.</summary>
        [JsonPropertyName("freedSize")]
        public long FreedSize { get; set; }

        /// <summary>Gets or sets the remaining items count.</summary>
        [JsonPropertyName("remainingItems")]
        public int RemainingItems { get; set; }
    }
}
