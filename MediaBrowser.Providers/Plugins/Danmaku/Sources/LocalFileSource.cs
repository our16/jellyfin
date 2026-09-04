using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Providers.Plugins.Danmaku.Models;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.Danmaku.Sources
{
    /// <summary>
    /// Local file danmaku source adapter.
    /// Scans configured directories for XML/JSON danmaku files.
    /// </summary>
    public class LocalFileSource : Services.IDanmakuSource
    {
        private readonly ILogger<LocalFileSource> _logger;
        private readonly IApplicationPaths _applicationPaths;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalFileSource"/> class.
        /// </summary>
        public LocalFileSource(ILogger<LocalFileSource> logger, IApplicationPaths applicationPaths)
        {
            _logger = logger;
            _applicationPaths = applicationPaths;
        }

        /// <inheritdoc/>
        public string Id => "local";

        /// <inheritdoc/>
        public string Name => "本地文件";

        /// <inheritdoc/>
        public string Type => "local";

        /// <inheritdoc/>
        public string[] SupportedFormats => new[] { "xml", "json" };

        /// <inheritdoc/>
        public bool IsEnabled => false;

        /// <inheritdoc/>
        public int Priority => 0;

        /// <summary>
        /// Gets the configured scan paths.
        /// </summary>
        public string[] ScanPaths => new[]
        {
            Path.Combine(_applicationPaths.DataPath, "danmaku"),
            Path.Combine(_applicationPaths.CachePath, "danmaku")
        };

        /// <inheritdoc/>
        public Task<DanmakuSearchResult[]> SearchAsync(string keyword, int limit, CancellationToken ct)
        {
            var results = new List<DanmakuSearchResult>();

            foreach (var scanPath in ScanPaths)
            {
                if (!Directory.Exists(scanPath))
                {
                    continue;
                }

                var xmlFiles = Directory.GetFiles(scanPath, "*.xml");
                var jsonFiles = Directory.GetFiles(scanPath, "*.json");
                var allFiles = xmlFiles.Concat(jsonFiles);

                foreach (var file in allFiles)
                {
                    if (results.Count >= limit)
                    {
                        break;
                    }

                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        var fileInfo = new FileInfo(file);
                        results.Add(new DanmakuSearchResult
                        {
                            Source = Id,
                            SourceId = fileName,
                            Name = fileName,
                            MatchScore = 0.7f,
                            MatchSource = Id,
                            AvailableFormats = new[] { Path.GetExtension(file).TrimStart('.').ToLowerInvariant() }
                        });
                    }
                }
            }

            return Task.FromResult(results.ToArray());
        }

        /// <inheritdoc/>
        public async Task<string?> GetDanmakuXmlAsync(string sourceId, int? sourceCid, CancellationToken ct)
        {
            foreach (var scanPath in ScanPaths)
            {
                if (!Directory.Exists(scanPath))
                {
                    continue;
                }

                // Try XML first, then JSON
                var xmlPath = Path.Combine(scanPath, $"{sourceId}.xml");
                if (File.Exists(xmlPath))
                {
                    return await File.ReadAllTextAsync(xmlPath, ct).ConfigureAwait(false);
                }

                var jsonPath = Path.Combine(scanPath, $"{sourceId}.json");
                if (File.Exists(jsonPath))
                {
                    return await File.ReadAllTextAsync(jsonPath, ct).ConfigureAwait(false);
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public DanmakuItem[] ParseXml(string xml)
        {
            try
            {
                // Check if it's JSON
                if (xml.TrimStart().StartsWith('{'))
                {
                    return ParseJson(xml);
                }

                return ParseXmlInternal(xml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing local danmaku file");
                return Array.Empty<DanmakuItem>();
            }
        }

        private DanmakuItem[] ParseXmlInternal(string xml)
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

                items.Add(new DanmakuItem
                {
                    Id = long.Parse(parts[7], CultureInfo.InvariantCulture),
                    Time = float.Parse(parts[0], CultureInfo.InvariantCulture),
                    Type = int.Parse(parts[1], CultureInfo.InvariantCulture),
                    FontSize = int.Parse(parts[2], CultureInfo.InvariantCulture),
                    Color = int.Parse(parts[3], CultureInfo.InvariantCulture),
                    Timestamp = long.Parse(parts[4], CultureInfo.InvariantCulture),
                    Pool = int.Parse(parts[5], CultureInfo.InvariantCulture),
                    UserIdHash = parts[6],
                    Content = d.Value,
                    Weight = 6
                });
            }

            return items.ToArray();
        }

        private DanmakuItem[] ParseJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("danmakus", out var danmakus))
            {
                return Array.Empty<DanmakuItem>();
            }

            var items = new List<DanmakuItem>();
            foreach (var d in danmakus.EnumerateArray())
            {
                items.Add(new DanmakuItem
                {
                    Id = d.TryGetProperty("id", out var id) ? id.GetInt64() : 0,
                    Time = d.TryGetProperty("time", out var time) ? time.GetSingle() : 0,
                    Type = d.TryGetProperty("type", out var type) ? type.GetInt32() : 1,
                    FontSize = d.TryGetProperty("fontSize", out var fs) ? fs.GetInt32() : 25,
                    Color = d.TryGetProperty("color", out var color) ? color.GetInt32() : 16777215,
                    Timestamp = d.TryGetProperty("timestamp", out var ts) ? ts.GetInt64() : 0,
                    Pool = d.TryGetProperty("pool", out var pool) ? pool.GetInt32() : 0,
                    UserIdHash = d.TryGetProperty("userIdHash", out var uid) ? uid.GetString() ?? string.Empty : string.Empty,
                    Content = d.TryGetProperty("content", out var content) ? content.GetString() ?? string.Empty : string.Empty,
                    Weight = d.TryGetProperty("weight", out var weight) ? weight.GetInt32() : 6
                });
            }

            return items.ToArray();
        }
    }
}
