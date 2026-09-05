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
using MediaBrowser.Providers.Plugins.Danmaku.Services;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.Danmaku.Sources
{
    /// <summary>
    /// Bilibili danmaku source adapter.
    /// </summary>
    public class BilibiliSource : IDanmakuSource
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BilibiliSource> _logger;
        private readonly DanmakuConfigManager _configManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="BilibiliSource"/> class.
        /// </summary>
        public BilibiliSource(IHttpClientFactory httpClientFactory, ILogger<BilibiliSource> logger, DanmakuConfigManager configManager)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configManager = configManager;
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

                var xml = await FetchProtobufDanmakuAsync(cid.Value, ct).ConfigureAwait(false);
                return xml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Bilibili danmaku for {SourceId}", sourceId);
                return null;
            }
        }

        private async Task<string?> FetchProtobufDanmakuAsync(int cid, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var allItems = new List<DanmakuItem>();
                var sessdata = _configManager.BilibiliSessdata;
                int seg = 1;

                // Fetch metadata (count, special_dms) via view API
                long totalCount = 0;
                var specialDmUrls = new List<string>();
                try
                {
                    var viewUrl = $"https://api.bilibili.com/x/v2/dm/web/view?type=1&oid={cid}";
                    var viewReq = new HttpRequestMessage(HttpMethod.Get, viewUrl);
                    AddBilibiliHeaders(viewReq, sessdata);
                    var viewResp = await client.SendAsync(viewReq, ct).ConfigureAwait(false);
                    if (viewResp.StatusCode == HttpStatusCode.OK)
                    {
                        var viewBytes = await viewResp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                        var viewData = ParseWebViewReply(viewBytes);
                        totalCount = viewData.Count;
                        specialDmUrls.AddRange(viewData.SpecialDms);
                        _logger.LogInformation("Bilibili view API: count={Count}, specialDms={SpecialCount}, segments={Total}", totalCount, specialDmUrls.Count, viewData.TotalSegments);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to fetch Bilibili view metadata for cid {Cid}", cid);
                }

                // Fetch main danmaku segments
                while (true)
                {
                    var url = $"https://api.bilibili.com/x/v2/dm/web/seg.so?type=1&oid={cid}&segment_index={seg}";
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    AddBilibiliHeaders(request, sessdata);

                    var response = await client.SendAsync(request, ct).ConfigureAwait(false);
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        if (seg == 1)
                        {
                            _logger.LogWarning("Bilibili protobuf danmaku fetch returned {Status}", response.StatusCode);
                            return null;
                        }
                        break;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    if (bytes.Length < 10) break;

                    var items = ParseProtobufBytes(bytes);
                    allItems.AddRange(items);

                    if (items.Length == 0) break;
                    seg++;
                }

                // Fetch BAS/code danmaku from special_dms packages
                foreach (var specialUrl in specialDmUrls)
                {
                    try
                    {
                        var specialReq = new HttpRequestMessage(HttpMethod.Get, specialUrl);
                        specialReq.Headers.Add("User-Agent", "Jellyfin-Danmaku-Plugin/1.0");
                        specialReq.Headers.Add("Referer", "https://www.bilibili.com");
                        var specialResp = await client.SendAsync(specialReq, ct).ConfigureAwait(false);
                        if (specialResp.StatusCode == HttpStatusCode.OK)
                        {
                            var specialBytes = await specialResp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                            var specialItems = ParseProtobufBytes(specialBytes);
                            allItems.AddRange(specialItems);
                            _logger.LogInformation("Fetched {Count} BAS/code danmaku from special package", specialItems.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to fetch special danmaku package from {Url}", specialUrl);
                    }
                }

                _logger.LogInformation("Bilibili total danmaku for cid {Cid}: {Count} items from {Segments} segments (view API count: {ViewCount})", cid, allItems.Count, seg - 1, totalCount);

                return BuildXmlString(allItems, cid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Bilibili protobuf danmaku");
                return null;
            }
        }

        private static void AddBilibiliHeaders(HttpRequestMessage request, string? sessdata)
        {
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Referer", "https://www.bilibili.com");
            if (!string.IsNullOrEmpty(sessdata))
            {
                request.Headers.Add("Cookie", $"SESSDATA={sessdata}");
            }
        }

        private static (long Count, List<string> SpecialDms, int TotalSegments) ParseWebViewReply(byte[] data)
        {
            var result = (Count: 0L, SpecialDms: new List<string>(), TotalSegments: 0);
            int pos = 0;

            while (pos < data.Length)
            {
                int tag = ReadVarint(data, ref pos);
                int fieldNum = tag >> 3;
                int wireType = tag & 0x07;

                if (wireType == 0)
                {
                    long val = ReadVarint64(data, ref pos);
                    if (fieldNum == 8) result.Count = val; // count field
                }
                else if (wireType == 2)
                {
                    int len = ReadVarint(data, ref pos);
                    int end = pos + len;
                    if (end > data.Length) break;

                    if (fieldNum == 4) // dm_sge (DmSegConfig)
                    {
                        // Parse DmSegConfig: field 1 = pageSize, field 2 = total
                        int subPos = pos;
                        while (subPos < end)
                        {
                            int subTag = ReadVarint(data, ref subPos);
                            int subField = subTag >> 3;
                            int subWire = subTag & 0x07;
                            if (subWire == 0)
                            {
                                long subVal = ReadVarint64(data, ref subPos);
                                if (subField == 2) result.TotalSegments = (int)subVal;
                            }
                            else if (subWire == 2)
                            {
                                int sl = ReadVarint(data, ref subPos);
                                subPos += sl;
                            }
                            else break;
                        }
                    }
                    else if (fieldNum == 6) // special_dms (repeated string)
                    {
                        string url = Encoding.UTF8.GetString(data, pos, len);
                        if (url.StartsWith("http", StringComparison.Ordinal))
                        {
                            result.SpecialDms.Add(url);
                        }
                    }

                    pos = end;
                }
                else if (wireType == 1) { pos += 8; }
                else if (wireType == 5) { pos += 4; }
                else break;
            }

            return result;
        }

        private DanmakuItem[] ParseProtobufBytes(byte[] data)
        {
            var items = new List<DanmakuItem>();
            int pos = 0;

            while (pos < data.Length)
            {
                int tag = ReadVarint(data, ref pos);
                int fieldNum = tag >> 3;
                int wireType = tag & 0x07;

                if (fieldNum == 1 && wireType == 2)
                {
                    int length = ReadVarint(data, ref pos);
                    int end = pos + length;
                    if (end > data.Length) break;

                    var dm = ParseDanmakuElem(data, ref pos, end);
                    if (dm != null) items.Add(dm);
                }
                else if (wireType == 0) { ReadVarint(data, ref pos); }
                else if (wireType == 2) { int sl = ReadVarint(data, ref pos); pos += sl; }
                else if (wireType == 1) { pos += 8; }
                else if (wireType == 5) { pos += 4; }
                else break;
            }

            return items.ToArray();
        }

        private DanmakuItem? ParseDanmakuElem(byte[] data, ref int pos, int end)
        {
            long id = 0;
            int progress = 0;
            int mode = 0;
            int fontSize = 25;
            int color = 0xFFFFFF;
            string midHash = "";
            string content = "";
            int ctime = 0;

            while (pos < end)
            {
                int tag = ReadVarint(data, ref pos);
                int fieldNum = tag >> 3;
                int wireType = tag & 0x07;

                switch (fieldNum)
                {
                    case 1: id = ReadVarint64(data, ref pos); break;
                    case 2: progress = ReadVarint(data, ref pos); break;
                    case 3: mode = ReadVarint(data, ref pos); break;
                    case 4: fontSize = ReadVarint(data, ref pos); break;
                    case 5: color = ReadVarint(data, ref pos); break;
                    case 6: midHash = ReadString(data, ref pos); break;
                    case 7: content = ReadString(data, ref pos); break;
                    case 8: ctime = ReadVarint(data, ref pos); break;
                    case 9: break; // weight
                    case 10: ReadString(data, ref pos); break; // action
                    case 11: ReadVarint(data, ref pos); break; // pool
                    case 12: ReadString(data, ref pos); break; // idStr
                    default:
                        if (wireType == 0) ReadVarint(data, ref pos);
                        else if (wireType == 2) { int sl = ReadVarint(data, ref pos); pos += sl; }
                        else if (wireType == 1) pos += 8;
                        else if (wireType == 5) pos += 4;
                        break;
                }
            }

            if (string.IsNullOrEmpty(content)) return null;

            return new DanmakuItem
            {
                Id = id,
                Time = progress / 1000f,
                Type = mode,
                FontSize = fontSize,
                Color = color,
                Timestamp = ctime,
                Pool = 0,
                UserIdHash = midHash,
                Content = content,
                Weight = 6
            };
        }

        private static string BuildXmlString(List<DanmakuItem> items, int cid)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<i>");
            sb.AppendLine("  <chatserver>chat.bilibili.com</chatserver>");
            sb.AppendLine("  <chatid>" + cid + "</chatid>");
            sb.AppendLine("  <maxlimit>1500</maxlimit>");
            sb.AppendLine("  <state>0</state>");
            sb.AppendLine("  <real_name>0</real_name>");
            sb.AppendLine("  <source>e-r</source>");

            foreach (var dm in items)
            {
                sb.AppendLine("  <d p=\"" + dm.Time.ToString("F3", CultureInfo.InvariantCulture) + "," + dm.Type + "," + dm.FontSize + "," + dm.Color + "," + dm.Timestamp + ",0," + dm.UserIdHash + ",0\">" + XmlEscape(dm.Content) + "</d>");
            }
            sb.AppendLine("</i>");
            return sb.ToString();
        }

        private static string XmlEscape(string text)
        {
            return text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);
        }

        private static int ReadVarint(byte[] data, ref int pos)
        {
            int result = 0;
            int shift = 0;
            while (pos < data.Length)
            {
                byte b = data[pos++];
                result |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return result;
        }

        private static long ReadVarint64(byte[] data, ref int pos)
        {
            long result = 0;
            int shift = 0;
            while (pos < data.Length)
            {
                byte b = data[pos++];
                result |= (long)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return result;
        }

        private static string ReadString(byte[] data, ref int pos)
        {
            int len = ReadVarint(data, ref pos);
            if (len < 0 || pos + len > data.Length) return "";
            string s = Encoding.UTF8.GetString(data, pos, len);
            pos += len;
            return s;
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
