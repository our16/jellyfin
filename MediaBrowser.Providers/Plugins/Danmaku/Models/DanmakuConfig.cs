using System;
using System.Text.Json.Serialization;

namespace MediaBrowser.Providers.Plugins.Danmaku.Models
{
    /// <summary>
    /// Global danmaku configuration DTO (for API responses).
    /// </summary>
    public class DanmakuConfigResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether danmaku is globally enabled.
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether new playback defaults to danmaku on.
        /// </summary>
        [JsonPropertyName("defaultEnabled")]
        public bool DefaultEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether auto-match is enabled.
        /// </summary>
        [JsonPropertyName("autoMatch")]
        public bool AutoMatch { get; set; }

        /// <summary>
        /// Gets or sets the auto-match source list.
        /// </summary>
        [JsonPropertyName("autoMatchSources")]
        public string[] AutoMatchSources { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the max cache size in bytes.
        /// </summary>
        [JsonPropertyName("maxCacheSize")]
        public long MaxCacheSize { get; set; }

        /// <summary>
        /// Gets or sets the cache expiry in days.
        /// </summary>
        [JsonPropertyName("cacheExpiryDays")]
        public int CacheExpiryDays { get; set; }

        /// <summary>
        /// Gets or sets the max danmaku count per video.
        /// </summary>
        [JsonPropertyName("maxDanmakuCount")]
        public int MaxDanmakuCount { get; set; }

        /// <summary>
        /// Gets or sets the default display settings.
        /// </summary>
        [JsonPropertyName("defaultDisplaySettings")]
        public DanmakuDisplaySettingsResponse DefaultDisplaySettings { get; set; } = new();

        /// <summary>
        /// Gets or sets the update settings.
        /// </summary>
        [JsonPropertyName("updateSettings")]
        public UpdateSettingsResponse UpdateSettings { get; set; } = new();
    }

    /// <summary>
    /// Danmaku display settings DTO (for API responses).
    /// </summary>
    public class DanmakuDisplaySettingsResponse
    {
        /// <summary>Gets or sets the font size.</summary>
        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; } = 25;

        /// <summary>Gets or sets the opacity.</summary>
        [JsonPropertyName("opacity")]
        public float Opacity { get; set; } = 0.8f;

        /// <summary>Gets or sets the speed.</summary>
        [JsonPropertyName("speed")]
        public float Speed { get; set; } = 1.0f;

        /// <summary>Gets or sets the display area ratio.</summary>
        [JsonPropertyName("area")]
        public float Area { get; set; } = 0.7f;

        /// <summary>Gets or sets the enabled danmaku types.</summary>
        [JsonPropertyName("enabledTypes")]
        public int[] EnabledTypes { get; set; } = new[] { 1, 4, 5 };

        /// <summary>Gets or sets the blocked colors.</summary>
        [JsonPropertyName("blockedColors")]
        public int[] BlockedColors { get; set; } = Array.Empty<int>();

        /// <summary>Gets or sets the blocked user hashes.</summary>
        [JsonPropertyName("blockedUsers")]
        public string[] BlockedUsers { get; set; } = Array.Empty<string>();

        /// <summary>Gets or sets the blocked keywords.</summary>
        [JsonPropertyName("blockedWords")]
        public string[] BlockedWords { get; set; } = Array.Empty<string>();

        /// <summary>Gets or sets the density limit.</summary>
        [JsonPropertyName("densityLimit")]
        public int DensityLimit { get; set; } = 6;
    }

    /// <summary>
    /// Update settings DTO (for API responses).
    /// </summary>
    public class UpdateSettingsResponse
    {
        /// <summary>Gets or sets a value indicating whether auto update is enabled.</summary>
        [JsonPropertyName("autoUpdate")]
        public bool AutoUpdate { get; set; } = true;

        /// <summary>Gets or sets the update interval in hours.</summary>
        [JsonPropertyName("updateIntervalHours")]
        public int UpdateIntervalHours { get; set; } = 24;

        /// <summary>Gets or sets a value indicating whether to prefer protobuf format.</summary>
        [JsonPropertyName("preferProtobuf")]
        public bool PreferProtobuf { get; set; } = true;
    }

    /// <summary>
    /// User danmaku preferences DTO (for API responses).
    /// </summary>
    public class UserDanmakuPreferencesResponse
    {
        /// <summary>Gets or sets the user ID.</summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        /// <summary>Gets or sets a value indicating whether danmaku is enabled for this user.</summary>
        [JsonPropertyName("danmakuEnabled")]
        public bool DanmakuEnabled { get; set; } = true;

        /// <summary>Gets or sets the user display settings.</summary>
        [JsonPropertyName("displaySettings")]
        public DanmakuDisplaySettingsResponse DisplaySettings { get; set; } = new();
    }
}
