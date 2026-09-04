using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using MediaBrowser.Providers.Plugins.Danmaku.Models;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.Danmaku.Sources
{
    /// <summary>
    /// Bilibili danmaku source adapter.
    /// </summary>
    public class BilibiliSource : Services.IDanmakuSource
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BilibiliSource> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="BilibiliSource"/> class.
        /// </summary>
        public BilibiliSource(IHttpClientFactory httpClientFactory, ILogger<BilibiliSource> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public string Id => "bilibili";

        /// <inheritdoc/>
        public string Name => "哔哩哔哩";

        /// <inheritdoc/>
        public string Type => "online";

        /// <inheritdoc/>
        public string[] SupportedFormats => new[] { "xml", "protobuf" };

        /// <inheritdoc/>
        public bool IsEnabled => true;

        /// <inheritdoc/>
        public int Priority => 1;

        /// <inheritdoc/>
        public async Task<DanmakuSearchResult[]> SearchAsync(string keyword, int limit, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://api.bilibili.com/x/web-interface/search/all/v2?keyword={Uri.EscapeDataString(keyword)}&page=1&pagesize={limit}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Jellyfin-Danmaku-Plugin/1.0");
                request.Headers.Add("Referer", "https://www.bilibili.com");

                var response = await client.SendAsync(request, ct).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogWarning("Bilibili search returned status {Status}", response.StatusCode);
                    return Array.Empty<DanmakuSearchResult>();
                }

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("data", out var data) ||
                    !data.TryGetProperty("result", out var results))
                {
                    return Array.Empty<DanmakuSearchResult>();
                }

                var searchResults = new List<DanmakuSearchResult>();

                foreach (var result in results.EnumerateArray())
                {
                    if (result.TryGetProperty("result_type", out var typeProp) &&
                        typeProp.GetString() != "video")
                    {
                        continue;
                    }

                    if (!result.TryGetProperty("data", out var items))
                    {
                        continue;
                    }

                    foreach (var item in items.EnumerateArray())
                    {
                        if (searchResults.Count >= limit)
                        {
                            break;
                        }

                        var bvid = item.TryGetProperty("bvid", out var bvidProp) ? bvidProp.GetString() ?? string.Empty : string.Empty;
                        var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? string.Empty : string.Empty;
                        var duration = item.TryGetProperty("duration", out var durationProp) ? durationProp.GetString() ?? "0" : "0";
                        var pic = item.TryGetProperty("pic", out var picProp) ? picProp.GetString() : null;
                        var aid = item.TryGetProperty("aid", out var aidProp) ? aidProp.GetInt64() : 0;

                        // Strip HTML tags from title
                        title = System.Text.RegularExpressions.Regex.Replace(title, "<[^>]+>", string.Empty);

                        var durationMs = ParseDuration(duration);

                        searchResults.Add(new DanmakuSearchResult
                        {
                            Source = Id,
                            SourceId = bvid,
                            Name = title,
                            Category = "Video",
                            Duration = durationMs,
                            MatchScore = 0.9f,
                            MatchSource = Id,
                            AvailableFormats = SupportedFormats,
                            ThumbnailUrl = pic?.StartsWith("//", StringComparison.Ordinal) == true ? $"https:{pic}" : pic
                        });
                    }
                }

                return searchResults.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Bilibili for keyword: {Keyword}", keyword);
                return Array.Empty<DanmakuSearchResult>();
            }
        }

        /// <inheritdoc/>
        public async Task<string?> GetDanmakuXmlAsync(string sourceId, int? sourceCid, CancellationToken ct)
        {
            try
            {
                var cid = sourceCid;
                if (!cid.HasValue)
                {
                    cid = await GetCidAsync(sourceId, ct).ConfigureAwait(false);
                    if (!cid.HasValue)
                    {
                        return null;
                    }
                }

                var client = _httpClientFactory.CreateClient();
                var url = $"https://comment.bilibili.com/{cid.Value}.xml";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Jellyfin-Danmaku-Plugin/1.0");
                request.Headers.Add("Referer", "https://www.bilibili.com");

                var response = await client.SendAsync(request, ct).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogWarning("Bilibili danmaku fetch returned status {Status} for CID {Cid}", response.StatusCode, cid.Value);
                    return null;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

                // Bilibili returns gzip-compressed XML
                string xml;
                if (bytes.Length >= 2 && bytes[0] == 0x1f && bytes[1] == 0x8b)
                {
                    using var compressedStream = new MemoryStream(bytes);
                    using var gzipStream = new System.IO.Compression.GZipStream(compressedStream, System.IO.Compression.CompressionMode.Decompress);
                    using var reader = new StreamReader(gzipStream, Encoding.UTF8);
                    xml = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                }
                else
                {
                    xml = Encoding.UTF8.GetString(bytes);
                }

                return xml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Bilibili danmaku for {SourceId}", sourceId);
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
                _logger.LogError(ex, "Error parsing Bilibili danmaku XML");
                return Array.Empty<DanmakuItem>();
            }
        }

        private async Task<int?> GetCidAsync(string bvid, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"https://api.bilibili.com/x/web-interface/view?bvid={Uri.EscapeDataString(bvid)}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Jellyfin-Danmaku-Plugin/1.0");
                request.Headers.Add("Referer", "https://www.bilibili.com");

                var response = await client.SendAsync(request, ct).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("cid", out var cidProp))
                {
                    return cidProp.GetInt32();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting CID for BVID: {Bvid}", bvid);
                return null;
            }
        }

        private static int ParseDuration(string duration)
        {
            // Duration format: "MM:SS" or "HH:MM:SS"
            var parts = duration.Split(':');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var minutes) &&
                int.TryParse(parts[1], out var seconds))
            {
                return (minutes * 60 + seconds) * 1000;
            }

            if (parts.Length == 3 &&
                int.TryParse(parts[0], out var hours) &&
                int.TryParse(parts[1], out var mins) &&
                int.TryParse(parts[2], out var secs))
            {
                return (hours * 3600 + mins * 60 + secs) * 1000;
            }

            return 0;
        }
    }
}
