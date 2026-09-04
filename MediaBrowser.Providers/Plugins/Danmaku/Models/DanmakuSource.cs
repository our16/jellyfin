using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MediaBrowser.Providers.Plugins.Danmaku.Models
{
    /// <summary>
    /// Danmaku source configuration DTO.
    /// </summary>
    public class DanmakuSource
    {
        /// <summary>
        /// Gets or sets the source identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source type (online/local/custom).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "online";

        /// <summary>
        /// Gets or sets a value indicating whether the source is enabled.
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the priority (lower = higher priority).
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the supported formats.
        /// </summary>
        [JsonPropertyName("supportedFormats")]
        public string[] SupportedFormats { get; set; } = new[] { "xml" };

        /// <summary>
        /// Gets or sets the source-specific configuration.
        /// </summary>
        [JsonPropertyName("config")]
        public Dictionary<string, object>? Config { get; set; }

        /// <summary>
        /// Gets or sets the source statistics.
        /// </summary>
        [JsonPropertyName("stats")]
        public DanmakuSourceStats Stats { get; set; } = new();
    }

    /// <summary>
    /// Danmaku source statistics DTO.
    /// </summary>
    public class DanmakuSourceStats
    {
        /// <summary>
        /// Gets or sets the total requests count.
        /// </summary>
        [JsonPropertyName("totalRequests")]
        public int TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets the success rate (0.0 - 1.0).
        /// </summary>
        [JsonPropertyName("successRate")]
        public float SuccessRate { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets the average response time in milliseconds.
        /// </summary>
        [JsonPropertyName("avgResponseTime")]
        public int AvgResponseTime { get; set; }

        /// <summary>
        /// Gets or sets the last error message.
        /// </summary>
        [JsonPropertyName("lastError")]
        public string? LastError { get; set; }

        /// <summary>
        /// Gets or sets the last error timestamp.
        /// </summary>
        [JsonPropertyName("lastErrorTime")]
        public long? LastErrorTime { get; set; }
    }
}
