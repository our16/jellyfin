using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.Danmaku.Models;

namespace MediaBrowser.Providers.Plugins.Danmaku.Services
{
    /// <summary>
    /// Interface for danmaku source adapters.
    /// </summary>
    public interface IDanmakuSource
    {
        /// <summary>
        /// Gets the source identifier.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the display name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the source type.
        /// </summary>
        string Type { get; }

        /// <summary>
        /// Gets the supported formats.
        /// </summary>
        string[] SupportedFormats { get; }

        /// <summary>
        /// Gets a value indicating whether this source is enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Gets the priority.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Search for danmaku by keyword.
        /// </summary>
        /// <param name="keyword">Search keyword.</param>
        /// <param name="limit">Max results.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Search results.</returns>
        Task<DanmakuSearchResult[]> SearchAsync(string keyword, int limit, CancellationToken ct);

        /// <summary>
        /// Get danmaku XML content for a given source ID and optional CID.
        /// </summary>
        /// <param name="sourceId">Source video ID.</param>
        /// <param name="sourceCid">Source CID (nullable).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>XML string or null if not found.</returns>
        Task<string?> GetDanmakuXmlAsync(string sourceId, int? sourceCid, CancellationToken ct);

        /// <summary>
        /// Parse danmaku XML into items.
        /// </summary>
        /// <param name="xml">XML content.</param>
        /// <returns>Parsed danmaku items.</returns>
        DanmakuItem[] ParseXml(string xml);
    }
}
