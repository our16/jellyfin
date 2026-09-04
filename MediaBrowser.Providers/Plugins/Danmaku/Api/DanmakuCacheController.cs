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
    /// Danmaku cache management API controller.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/danmaku/cache")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class DanmakuCacheController : ControllerBase
    {
        private readonly DanmakuCacheManager _cacheManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="DanmakuCacheController"/> class.
        /// </summary>
        public DanmakuCacheController(DanmakuCacheManager cacheManager)
        {
            _cacheManager = cacheManager;
        }

        /// <summary>
        /// Get cache statistics.
        /// </summary>
        [HttpGet("stats")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<CacheStatsResponse>> GetStats(CancellationToken ct)
        {
            var maxSize = Plugin.Instance?.Configuration.MaxCacheSize ?? 1073741824;
            var stats = await _cacheManager.GetStatsAsync(maxSize).ConfigureAwait(false);
            return Ok(stats);
        }

        /// <summary>
        /// Get paginated cache list.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult> GetCacheList(
            [FromQuery] int startIndex = 0,
            [FromQuery] int limit = 50,
            [FromQuery] string sortBy = "cachedAt",
            [FromQuery] string sortOrder = "Descending",
            CancellationToken ct = default)
        {
            var (totalCount, items) = await _cacheManager.GetListAsync(
                startIndex, limit, sortBy, sortOrder).ConfigureAwait(false);

            return Ok(new
            {
                totalRecordCount = totalCount,
                items
            });
        }

        /// <summary>
        /// Cleanup expired cache entries.
        /// </summary>
        [HttpPost("cleanup")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<CacheCleanupResult>> Cleanup(CancellationToken ct)
        {
            var result = await _cacheManager.CleanupAsync().ConfigureAwait(false);
            return Ok(result);
        }

        /// <summary>
        /// Clear all cached danmaku.
        /// </summary>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ClearAll(CancellationToken ct)
        {
            await _cacheManager.ClearAsync().ConfigureAwait(false);
            return NoContent();
        }
    }
}
