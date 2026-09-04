using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.Danmaku.Models;
using MediaBrowser.Providers.Plugins.Danmaku.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MediaBrowser.Providers.Plugins.Danmaku.Api
{
    /// <summary>
    /// Danmaku search API controller.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/danmaku/search")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class DanmakuSearchController : ControllerBase
    {
        private readonly DanmakuService _danmakuService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DanmakuSearchController"/> class.
        /// </summary>
        public DanmakuSearchController(DanmakuService danmakuService)
        {
            _danmakuService = danmakuService;
        }

        /// <summary>
        /// Search for danmaku across all enabled sources.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<DanmakuSearchResult[]>> Search(
            [FromQuery] string keyword,
            [FromQuery] string? sources = null,
            [FromQuery] int limit = 20,
            CancellationToken ct = default)
        {
            var results = await _danmakuService.SearchDanmakuAsync(keyword, sources, limit, ct).ConfigureAwait(false);
            return Ok(new { totalResults = results.Length, results });
        }

        /// <summary>
        /// Search for danmaku by Jellyfin item ID.
        /// </summary>
        [HttpGet("by-item/{itemId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DanmakuSearchResult[]>> SearchByItem(
            [FromRoute] System.Guid itemId,
            [FromQuery] string? sources = null,
            [FromQuery] int limit = 10,
            CancellationToken ct = default)
        {
            var results = await _danmakuService.SearchByItemAsync(itemId, sources, limit, ct).ConfigureAwait(false);
            return Ok(new { totalResults = results.Length, results });
        }
    }
}
