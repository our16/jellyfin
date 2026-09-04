using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using MediaBrowser.Providers.Plugins.Danmaku.Models;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.Danmaku.Sources
{
    /// <summary>
    /// Dandanplay danmaku source adapter.
    /// Compatible with the dandanplay API specification.
    /// </summary>
    public class DandanplaySource : Services.IDanmakuSource
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DandanplaySource> _logger;
        private const string BaseUrl = "https://www.dandanplay.com";

        /// <summary>
        /// Initializes a new instance of the <see cref="DandanplaySource"/> class.
        /// </summary>
        public DandanplaySource(IHttpClientFactory httpClientFactory, ILogger<DandanplaySource> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public string Id => "dandanplay";

        /// <inheritdoc/>
        public string Name => "弹弹play";

        /// <inheritdoc/>
        public string Type => "online";

        /// <inheritdoc/>
        public string[] SupportedFormats => new[] { "xml" };

        /// <inheritdoc/>
        public bool IsEnabled => true;

        /// <inheritdoc/>
        public int Priority => 2;

        /// <inheritdoc/>
        public async Task<DanmakuSearchResult[]> SearchAsync(string keyword, int limit, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{BaseUrl}/api/v2/search/anime?keyword={Uri.EscapeDataString(keyword)}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Jellyfin-Danmaku-Plugin/1.0");

                var response = await client.SendAsync(request, ct).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogWarning("Dandanplay search returned status {Status}", response.StatusCode);
                    return Array.Empty<DanmakuSearchResult>();
                }

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("animes", out var animes))
                {
                    return Array.Empty<DanmakuSearchResult>();
                }

                var searchResults = new List<DanmakuSearchResult>();

                foreach (var anime in animes.EnumerateArray())
                {
                    if (searchResults.Count >= limit)
                    {
                        break;
                    }

                    var id = anime.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
                    var name = anime.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
                    var nameOriginal = anime.TryGetProperty("nameOriginal", out var nameOrigProp) ? nameOrigProp.GetString() : null;
                    var category = anime.TryGetProperty("category", out var catProp) ? catProp.GetString() : null;
                    var year = anime.TryGetProperty("year", out var yearProp) ? yearProp.GetString() : null;
                    var episodeSize = anime.TryGetProperty("episodeSize", out var epSizeProp) ? epSizeProp.GetInt32() : 0;

                    searchResults.Add(new DanmakuSearchResult
                    {
                        Source = Id,
                        SourceId = id,
                        Name = name,
                        NameOriginal = nameOriginal,
                        Category = category,
                        Year = year,
                        EpisodeNumber = episodeSize > 0 ? 1 : null,
                        MatchScore = 0.85f,
                        MatchSource = Id,
                        AvailableFormats = SupportedFormats
                    });
                }

                return searchResults.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Dandanplay for keyword: {Keyword}", keyword);
                return Array.Empty<DanmakuSearchResult>();
            }
        }

        /// <inheritdoc/>
        public async Task<string?> GetDanmakuXmlAsync(string sourceId, int? sourceCid, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{BaseUrl}/api/v2/comment/{Uri.EscapeDataString(sourceId)}?format=xml";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Jellyfin-Danmaku-Plugin/1.0");

                var response = await client.SendAsync(request, ct).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogWarning("Dandanplay danmaku fetch returned status {Status} for ID {SourceId}", response.StatusCode, sourceId);
                    return null;
                }

                return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Dandanplay danmaku for {SourceId}", sourceId);
                return null;
            }
        }

        /// <inheritdoc/>
        public DanmakuItem[] ParseXml(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var items = new List<DanmakuItem>();

                // Dandanplay uses a slightly different XML format but similar structure
                foreach (var d in doc.Descendants("d"))
                {
                    var pAttr = d.Attribute("p")?.Value;
                    if (string.IsNullOrEmpty(pAttr))
                    {
                        continue;
                    }

                    var parts = pAttr.Split(',');
                    if (parts.Length < 8)
                    {
                        continue;
                    }

                    var time = float.Parse(parts[0], CultureInfo.InvariantCulture);
                    var type = int.Parse(parts[1], CultureInfo.InvariantCulture);
                    var fontSize = int.Parse(parts[2], CultureInfo.InvariantCulture);
                    var color = int.Parse(parts[3], CultureInfo.InvariantCulture);
                    var timestamp = long.Parse(parts[4], CultureInfo.InvariantCulture);
                    var pool = int.Parse(parts[5], CultureInfo.InvariantCulture);
                    var userIdHash = parts[6];
                    var id = long.Parse(parts[7], CultureInfo.InvariantCulture);

                    items.Add(new DanmakuItem
                    {
                        Id = id,
                        Time = time,
                        Type = type,
                        FontSize = fontSize,
                        Color = color,
                        Timestamp = timestamp,
                        Pool = pool,
                        UserIdHash = userIdHash,
                        Content = d.Value,
                        Weight = 6
                    });
                }

                return items.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Dandanplay danmaku XML");
                return Array.Empty<DanmakuItem>();
            }
        }
    }
}
