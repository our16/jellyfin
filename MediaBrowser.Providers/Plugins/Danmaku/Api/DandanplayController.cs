using System;
using System.Globalization;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.Danmaku.Models;
using MediaBrowser.Providers.Plugins.Danmaku.Services;
using MediaBrowser.Providers.Plugins.Danmaku.Sources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.Danmaku.Api
{
    /// <summary>
    /// Dandanplay compatible API controller.
    /// Provides API endpoints compatible with the dandanplay API specification.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/v2")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class DandanplayController : ControllerBase
    {
        private readonly DandanplaySource _dandanplaySource;
        private readonly DanmakuService _danmakuService;
        private readonly ILogger<DandanplayController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DandanplayController"/> class.
        /// </summary>
        public DandanplayController(
            DandanplaySource dandanplaySource,
            DanmakuService danmakuService,
            ILogger<DandanplayController> logger)
        {
            _dandanplaySource = dandanplaySource;
            _danmakuService = danmakuService;
            _logger = logger;
        }

        /// <summary>
        /// Search for anime.
        /// </summary>
        [HttpGet("search/anime")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> SearchAnime(
            [FromQuery] string keyword,
            CancellationToken ct)
        {
            try
            {
                var results = await _dandanplaySource.SearchAsync(keyword, 20, ct).ConfigureAwait(false);

                return Ok(new
                {
                    errorCode = 0,
                    success = true,
                    errorMessage = (string?)null,
                    animes = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching anime for keyword: {Keyword}", keyword);
                return Ok(new
                {
                    errorCode = -1,
                    success = false,
                    errorMessage = ex.Message,
                    animes = Array.Empty<DanmakuSearchResult>()
                });
            }
        }

        /// <summary>
        /// Search for episodes.
        /// </summary>
        [HttpGet("search/episodes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> SearchEpisodes(
            [FromQuery] string anime,
            CancellationToken ct)
        {
            try
            {
                var results = await _dandanplaySource.SearchAsync(anime, 20, ct).ConfigureAwait(false);

                var episodes = new object[results.Length];
                for (int i = 0; i < results.Length; i++)
                {
                    episodes[i] = new
                    {
                        id = results[i].SourceId,
                        commentId = results[i].SourceCid?.ToString(CultureInfo.InvariantCulture) ?? results[i].SourceId,
                        number = results[i].EpisodeNumber ?? (i + 1),
                        title = results[i].Name,
                        site = results[i].Source
                    };
                }

                return Ok(new
                {
                    errorCode = 0,
                    success = true,
                    errorMessage = (string?)null,
                    episodes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching episodes for: {Anime}", anime);
                return Ok(new
                {
                    errorCode = -1,
                    success = false,
                    errorMessage = ex.Message,
                    episodes = Array.Empty<object>()
                });
            }
        }

        /// <summary>
        /// Get bangumi (anime) details.
        /// </summary>
        [HttpGet("bangumi/{bangumiId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetBangumi(
            [FromRoute] string bangumiId,
            CancellationToken ct)
        {
            try
            {
                var results = await _dandanplaySource.SearchAsync(bangumiId, 1, ct).ConfigureAwait(false);

                if (results.Length == 0)
                {
                    return Ok(new
                    {
                        errorCode = 0,
                        success = true,
                        errorMessage = (string?)null,
                        bangumi = (object?)null
                    });
                }

                var result = results[0];
                return Ok(new
                {
                    errorCode = 0,
                    success = true,
                    errorMessage = (string?)null,
                    bangumi = new
                    {
                        id = result.SourceId,
                        name = result.Name,
                        nameOriginal = result.NameOriginal,
                        category = result.Category,
                        year = result.Year,
                        episodes = new[]
                        {
                            new
                            {
                                id = result.SourceId,
                                commentId = result.SourceCid?.ToString(CultureInfo.InvariantCulture) ?? result.SourceId,
                                number = result.EpisodeNumber ?? 1,
                                title = result.EpisodeTitle ?? result.Name
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bangumi details for: {BangumiId}", bangumiId);
                return Ok(new
                {
                    errorCode = -1,
                    success = false,
                    errorMessage = ex.Message,
                    bangumi = (object?)null
                });
            }
        }

        /// <summary>
        /// Get danmaku comments for an episode.
        /// </summary>
        [HttpGet("comment/{episodeId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetComment(
            [FromRoute] string episodeId,
            [FromQuery] string format = "xml",
            CancellationToken ct = default)
        {
            try
            {
                var xml = await _dandanplaySource.GetDanmakuXmlAsync(episodeId, null, ct).ConfigureAwait(false);

                if (xml == null)
                {
                    return Ok(new
                    {
                        errorCode = 0,
                        success = true,
                        errorMessage = (string?)null,
                        comments = Array.Empty<object>()
                    });
                }

                if (format == "json")
                {
                    var items = _dandanplaySource.ParseXml(xml);
                    return Ok(new
                    {
                        errorCode = 0,
                        success = true,
                        errorMessage = (string?)null,
                        comments = items
                    });
                }

                return Content(xml, "application/xml");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comments for episode: {EpisodeId}", episodeId);
                return Ok(new
                {
                    errorCode = -1,
                    success = false,
                    errorMessage = ex.Message,
                    comments = Array.Empty<object>()
                });
            }
        }
    }
}
