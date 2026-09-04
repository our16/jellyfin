using System.Text.Json.Serialization;

namespace MediaBrowser.Providers.Plugins.Danmaku.Models
{
    /// <summary>
    /// Single danmaku item DTO.
    /// </summary>
    public class DanmakuItem
    {
        /// <summary>
        /// Gets or sets the danmaku ID.
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the time offset in seconds from video start.
        /// </summary>
        [JsonPropertyName("time")]
        public float Time { get; set; }

        /// <summary>
        /// Gets or sets the danmaku type (1-9).
        /// 1=ScrollRL, 4=Bottom, 5=Top, 6=ScrollLR, 7=Special.
        /// </summary>
        [JsonPropertyName("type")]
        public int Type { get; set; }

        /// <summary>
        /// Gets or sets the font size (18/25/36).
        /// </summary>
        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; }

        /// <summary>
        /// Gets or sets the color as RGB888 decimal.
        /// </summary>
        [JsonPropertyName("color")]
        public int Color { get; set; }

        /// <summary>
        /// Gets or sets the send timestamp (Unix seconds).
        /// </summary>
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the pool (0=normal, 1=subtitle, 2=special).
        /// </summary>
        [JsonPropertyName("pool")]
        public int Pool { get; set; }

        /// <summary>
        /// Gets or sets the user ID hash.
        /// </summary>
        [JsonPropertyName("userIdHash")]
        public string UserIdHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the danmaku content text.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the weight (0-10).
        /// </summary>
        [JsonPropertyName("weight")]
        public int Weight { get; set; }
    }
}
