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

        /// <summary>
        /// Gets or sets the dandanplay open API AppId.
        /// </summary>
        public string DandanplayAppId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the dandanplay open API AppSecret.
        /// </summary>
        public string DandanplayAppSecret { get; set; } = string.Empty;
    }
}
