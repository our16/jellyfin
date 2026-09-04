using System.Text.Json.Serialization;

namespace MediaBrowser.Providers.Plugins.Danmaku.Models
{
    /// <summary>
    /// Danmaku search result DTO.
    /// </summary>
    public class DanmakuSearchResult
    {
        /// <summary>
        /// Gets or sets the source identifier.
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source video ID.
        /// </summary>
        [JsonPropertyName("sourceId")]
        public string SourceId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source CID.
        /// </summary>
        [JsonPropertyName("sourceCid")]
        public int? SourceCid { get; set; }

        /// <summary>
        /// Gets or sets the localized name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original name.
        /// </summary>
        [JsonPropertyName("nameOriginal")]
        public string? NameOriginal { get; set; }

        /// <summary>
        /// Gets or sets the category/genre.
        /// </summary>
        [JsonPropertyName("category")]
        public string? Category { get; set; }

        /// <summary>
        /// Gets or sets the year.
        /// </summary>
        [JsonPropertyName("year")]
        public string? Year { get; set; }

        /// <summary>
        /// Gets or sets the episode number.
        /// </summary>
        [JsonPropertyName("episodeNumber")]
        public int? EpisodeNumber { get; set; }

        /// <summary>
        /// Gets or sets the episode title.
        /// </summary>
        [JsonPropertyName("episodeTitle")]
        public string? EpisodeTitle { get; set; }

        /// <summary>
        /// Gets or sets the duration in milliseconds.
        /// </summary>
        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        /// <summary>
        /// Gets or sets the match score (0.0 - 1.0).
        /// </summary>
        [JsonPropertyName("matchScore")]
        public float MatchScore { get; set; }

        /// <summary>
        /// Gets or sets the matched source identifier.
        /// </summary>
        [JsonPropertyName("matchSource")]
        public string MatchSource { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the available formats.
        /// </summary>
        [JsonPropertyName("availableFormats")]
        public string[] AvailableFormats { get; set; } = new[] { "xml" };

        /// <summary>
        /// Gets or sets the thumbnail URL.
        /// </summary>
        [JsonPropertyName("thumbnailUrl")]
        public string? ThumbnailUrl { get; set; }
    }
}
