#nullable disable

using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace MediaBrowser.Providers.Plugins.Danmaku
{
    /// <summary>
    /// Danmaku plugin class.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">application paths.</param>
        /// <param name="xmlSerializer">xml serializer.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the instance of Danmaku plugin.
        /// </summary>
        public static Plugin Instance { get; private set; }

        /// <inheritdoc/>
        public override Guid Id => new("a5b6c7d8-e9f0-4a1b-8c2d-3e4f5a6b7c8d");

        /// <inheritdoc/>
        public override string Name => "Danmaku";

        /// <inheritdoc/>
        public override string Description => "Provides danmaku (bullet comments) support from multiple sources including Bilibili, Dandanplay, and local files.";

        /// <inheritdoc/>
        public override string ConfigurationFileName => "Jellyfin.Plugin.Danmaku.xml";

        /// <summary>
        /// Return the plugin configuration page.
        /// </summary>
        /// <returns>PluginPageInfo.</returns>
        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.html"
            };
        }
    }
}
