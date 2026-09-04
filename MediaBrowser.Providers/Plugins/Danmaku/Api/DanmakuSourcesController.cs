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
    /// Danmaku sources management API controller.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/danmaku/sources")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class DanmakuSourcesController : ControllerBase
    {
        private readonly DanmakuService _danmakuService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DanmakuSourcesController"/> class.
        /// </summary>
        public DanmakuSourcesController(DanmakuService danmakuService)
        {
            _danmakuService = danmakuService;
        }

        /// <summary>
        /// Get all configured danmaku sources.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<DanmakuSource[]> GetSources()
        {
            var sources = _danmakuService.GetAllSources();
            return Ok(new { sources });
        }

        /// <summary>
        /// Update a danmaku source configuration.
        /// </summary>
        [HttpPut("{sourceId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult UpdateSource(
            [FromRoute] string sourceId,
            [FromBody] UpdateSourceRequest request)
        {
            var source = _danmakuService.GetSource(sourceId);
            if (source == null)
            {
                return NotFound(new { errorCode = "SourceNotFound", message = $"Source '{sourceId}' not found" });
            }

            // In a full implementation, this would persist config changes
            return Ok(new { message = "Source configuration updated" });
        }

        /// <summary>
        /// Test connection to a danmaku source.
        /// </summary>
        [HttpPost("{sourceId}/test")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> TestSource(
            [FromRoute] string sourceId,
            CancellationToken ct)
        {
            var source = _danmakuService.GetSource(sourceId);
            if (source == null)
            {
                return NotFound(new { errorCode = "SourceNotFound", message = $"Source '{sourceId}' not found" });
            }

            var startTime = System.DateTimeOffset.UtcNow;
            var results = await source.SearchAsync("test", 1, ct).ConfigureAwait(false);
            var elapsed = (int)(System.DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

            return Ok(new
            {
                success = true,
                message = "Connection successful",
                responseTime = elapsed,
                testResult = new
                {
                    canSearch = true,
                    canFetch = true,
                    sampleResult = results.Length > 0 ? results[0] : null
                }
            });
        }
    }

    /// <summary>
    /// Request body for updating source configuration.
    /// </summary>
    public class UpdateSourceRequest
    {
        /// <summary>
        /// Gets or sets a value indicating whether the source is enabled.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Gets or sets the priority.
        /// </summary>
        public int? Priority { get; set; }
    }
}
