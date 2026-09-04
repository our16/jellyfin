#pragma warning disable CA1819

using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace MediaBrowser.Providers.Plugins.Danmaku
{
    /// <summary>
    /// Plugin configuration class for Danmaku.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether the danmaku plugin is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether new playback should default to danmaku on.
        /// </summary>
        public bool DefaultEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to auto-match danmaku for library items.
        /// </summary>
        public bool AutoMatch { get; set; } = true;

        /// <summary>
        /// Gets or sets the sources to use for auto-matching, in priority order.
        /// </summary>
        public string[] AutoMatchSources { get; set; } = new[] { "bilibili", "dandanplay" };

        /// <summary>
        /// Gets or sets the maximum cache size in bytes. Default 1GB.
        /// </summary>
        public long MaxCacheSize { get; set; } = 1073741824;

        /// <summary>
        /// Gets or sets the number of days before cached danmaku expires.
        /// </summary>
        public int CacheExpiryDays { get; set; } = 30;

        /// <summary>
        /// Gets or sets the maximum number of danmaku items per video.
        /// </summary>
        public int MaxDanmakuCount { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the default display settings for danmaku rendering.
        /// </summary>
        public DanmakuDisplaySettings DefaultDisplaySettings { get; set; } = new();

        /// <summary>
        /// Gets or sets the update settings.
        /// </summary>
        public UpdateSettings UpdateSettings { get; set; } = new();
    }

    /// <summary>
    /// Danmaku display settings.
    /// </summary>
    public class DanmakuDisplaySettings
    {
        /// <summary>
        /// Gets or sets the font size. Supported values: 18, 25, 36.
        /// </summary>
        public int FontSize { get; set; } = 25;

        /// <summary>
        /// Gets or sets the opacity (0.0 - 1.0).
        /// </summary>
        public float Opacity { get; set; } = 0.8f;

        /// <summary>
        /// Gets or sets the speed multiplier (0.5 - 2.0).
        /// </summary>
        public float Speed { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets the display area ratio (0.0 - 1.0).
        /// </summary>
        public float Area { get; set; } = 0.7f;

        /// <summary>
        /// Gets or sets the enabled danmaku types.
        /// </summary>
        public int[] EnabledTypes { get; set; } = new[] { 1, 4, 5 };

        /// <summary>
        /// Gets or sets the blocked colors (RGB888 decimal).
        /// </summary>
        public int[] BlockedColors { get; set; } = Array.Empty<int>();

        /// <summary>
        /// Gets or sets the blocked user hashes.
        /// </summary>
        public string[] BlockedUsers { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the blocked keywords.
        /// </summary>
        public string[] BlockedWords { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the density limit (max danmaku per second).
        /// </summary>
        public int DensityLimit { get; set; } = 6;
    }

    /// <summary>
    /// Update settings.
    /// </summary>
    public class UpdateSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to auto-update danmaku.
        /// </summary>
        public bool AutoUpdate { get; set; } = true;

        /// <summary>
        /// Gets or sets the update interval in hours.
        /// </summary>
        public int UpdateIntervalHours { get; set; } = 24;

        /// <summary>
        /// Gets or sets a value indicating whether to prefer protobuf format when available.
        /// </summary>
        public bool PreferProtobuf { get; set; } = true;
    }

    /// <summary>
    /// Per-user danmaku preferences.
    /// </summary>
    public class UserDanmakuPreferences
    {
        /// <summary>
        /// Gets or sets a value indicating whether danmaku is enabled for this user.
        /// </summary>
        public bool DanmakuEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the user's display settings.
        /// </summary>
        public DanmakuDisplaySettings DisplaySettings { get; set; } = new();
    }
}
