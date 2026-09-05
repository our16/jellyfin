namespace MediaBrowser.Providers.Plugins.Danmaku.Services
{
    /// <summary>
    /// Manages runtime configuration for the Danmaku plugin.
    /// Updated by DanmakuService when plugin config changes.
    /// </summary>
    public class DanmakuConfigManager
    {
        /// <summary>
        /// Gets or sets the Bilibili SESSDATA cookie.
        /// </summary>
        public string BilibiliSessdata { get; set; } = string.Empty;
    }
}
