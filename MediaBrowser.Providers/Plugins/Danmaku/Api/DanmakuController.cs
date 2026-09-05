using System;
using System.IO;
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
    /// Danmaku API controller providing core endpoints.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/danmaku")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class DanmakuController : ControllerBase
    {
        private readonly DanmakuService _danmakuService;
        private readonly DanmakuCacheManager _cacheManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="DanmakuController"/> class.
        /// </summary>
        public DanmakuController(DanmakuService danmakuService, DanmakuCacheManager cacheManager)
        {
            _danmakuService = danmakuService;
            _cacheManager = cacheManager;
        }

        /// <summary>
        /// Get danmaku info for a media item.
        /// </summary>
        [HttpGet("{itemId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DanmakuFileInfo>> GetDanmakuInfo(
            [FromRoute] Guid itemId,
            [FromQuery] string? mediaSourceId = null,
            CancellationToken ct = default)
        {
            var info = await _danmakuService.GetDanmakuInfoAsync(itemId, mediaSourceId, ct).ConfigureAwait(false);
            if (info == null)
            {
                return NotFound(new { errorCode = "DanmakuNotFound", message = "No danmaku found for this item" });
            }

            return Ok(info);
        }

        /// <summary>
        /// Get danmaku file download URL.
        /// </summary>
        [HttpGet("{itemId}/url")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> GetDanmakuUrl(
            [FromRoute] Guid itemId,
            [FromQuery] string? mediaSourceId = null,
            [FromQuery] string format = "xml",
            CancellationToken ct = default)
        {
            var url = await _danmakuService.GetDanmakuUrlAsync(itemId, mediaSourceId, format, ct).ConfigureAwait(false);
            if (url == null)
            {
                return NotFound(new { errorCode = "DanmakuNotFound", message = "No danmaku found for this item" });
            }

            return Ok(url);
        }

        /// <summary>
        /// Get raw danmaku content directly.
        /// </summary>
        [HttpGet("{itemId}/raw")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDanmakuRaw(
            [FromRoute] Guid itemId,
            [FromQuery] string? mediaSourceId = null,
            CancellationToken ct = default)
        {
            var content = await _danmakuService.GetDanmakuRawAsync(itemId, mediaSourceId, ct).ConfigureAwait(false);
            if (content == null)
            {
                return NotFound(new { errorCode = "DanmakuNotFound", message = "No danmaku found for this item" });
            }

            return Content(content, "application/xml");
        }

        /// <summary>
        /// Serve cached danmaku file for download.
        /// </summary>
        [HttpGet("{itemId}/danmaku.{format}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult ServeDanmakuFile(
            [FromRoute] Guid itemId,
            [FromRoute] string format)
        {
            var filePath = _cacheManager.GetCachedFilePath(itemId.ToString(), format);
            if (filePath == null)
            {
                return NotFound();
            }

            var contentType = format.ToLowerInvariant() switch
            {
                "json" => "application/json",
                _ => "application/xml"
            };

            return PhysicalFile(filePath, contentType);
        }

        /// <summary>
        /// Refresh danmaku for an item (async).
        /// </summary>
        [HttpPost("{itemId}/refresh")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public async Task<ActionResult> RefreshDanmaku(
            [FromRoute] Guid itemId,
            [FromBody] RefreshDanmakuRequest? request = null,
            CancellationToken ct = default)
        {
            var taskId = await _danmakuService.RefreshDanmakuAsync(
                itemId,
                request?.Source,
                request?.SourceId,
                request?.SourceCid,
                request?.Force ?? false,
                ct).ConfigureAwait(false);

            return Accepted(new
            {
                taskId,
                status = "processing",
                message = "Danmaku refresh task submitted"
            });
        }

        /// <summary>
        /// Delete danmaku cache for an item.
        /// </summary>
        [HttpDelete("{itemId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteDanmakuCache(
            [FromRoute] Guid itemId,
            [FromQuery] string? mediaSourceId = null,
            CancellationToken ct = default)
        {
            await _danmakuService.DeleteDanmakuCacheAsync(itemId, ct).ConfigureAwait(false);
            return NoContent();
        }

        /// <summary>
        /// Get async task status.
        /// </summary>
        [HttpGet("tasks/{taskId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult GetTaskStatus([FromRoute] string taskId)
        {
            // Simple task status check - in production this would use a proper task store
            return Ok(new
            {
                taskId,
                status = "completed",
                progress = 100
            });
        }
    }

    /// <summary>
    /// Request body for refreshing danmaku.
    /// </summary>
    public class RefreshDanmakuRequest
    {
        /// <summary>
        /// Gets or sets the source to refresh from.
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// Gets or sets the source ID (e.g., Bilibili BVID) to fetch danmaku for directly, bypassing search.
        /// Example: "BV1UvfEB7EX9"
        /// </summary>
        public string? SourceId { get; set; }

        /// <summary>
        /// Gets or sets the source CID (e.g., Bilibili CID) for direct fetch.
        /// If not provided, will be resolved from SourceId.
        /// </summary>
        public int? SourceCid { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to force refresh.
        /// </summary>
        public bool? Force { get; set; }
    }
}
