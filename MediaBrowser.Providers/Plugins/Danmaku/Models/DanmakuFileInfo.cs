using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MediaBrowser.Providers.Plugins.Danmaku.Models
{
    /// <summary>
    /// Danmaku file metadata DTO.
    /// </summary>
    public class DanmakuFileInfo
    {
        /// <summary>
        /// Gets or sets the item ID.
        /// </summary>
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the media source ID.
        /// </summary>
        [JsonPropertyName("mediaSourceId")]
        public string? MediaSourceId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether danmaku is available.
        /// </summary>
        [JsonPropertyName("hasDanmaku")]
        public bool HasDanmaku { get; set; }

        /// <summary>
        /// Gets or sets the danmaku count.
        /// </summary>
        [JsonPropertyName("danmakuCount")]
        public int DanmakuCount { get; set; }

        /// <summary>
        /// Gets or sets the source identifier (e.g. "bilibili", "dandanplay", "local").
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source video ID.
        /// </summary>
        [JsonPropertyName("sourceId")]
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source CID (Bilibili).
        /// </summary>
        [JsonPropertyName("sourceCid")]
        public int? SourceCid { get; set; }

        /// <summary>
        /// Gets or sets the last updated timestamp (Unix seconds).
        /// </summary>
        [JsonPropertyName("lastUpdated")]
        public long LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the format (xml/json/protobuf).
        /// </summary>
        [JsonPropertyName("format")]
        public string Format { get; set; } = "xml";

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        [JsonPropertyName("fileSize")]
        public long FileSize { get; set; }

        /// <summary>
        /// Gets or sets the video duration in milliseconds.
        /// </summary>
        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        /// <summary>
        /// Gets or sets the available languages.
        /// </summary>
        [JsonPropertyName("languages")]
        public string[] Languages { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the match confidence (0.0 - 1.0).
        /// </summary>
        [JsonPropertyName("matchConfidence")]
        public float MatchConfidence { get; set; }
    }
}
