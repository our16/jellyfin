using System;
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
    /// Danmaku configuration API controller.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/danmaku/config")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class DanmakuConfigController : ControllerBase
    {
        private readonly DanmakuCacheManager _cacheManager;
        private readonly DanmakuConfigManager _configManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="DanmakuConfigController"/> class.
        /// </summary>
        public DanmakuConfigController(DanmakuCacheManager cacheManager, DanmakuConfigManager configManager)
        {
            _cacheManager = cacheManager;
            _configManager = configManager;
        }

        /// <summary>
        /// Get global danmaku configuration.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<DanmakuConfigResponse> GetConfig()
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null)
            {
                return Ok(new DanmakuConfigResponse());
            }

            return Ok(new DanmakuConfigResponse
            {
                Enabled = config.Enabled,
                DefaultEnabled = config.DefaultEnabled,
                AutoMatch = config.AutoMatch,
                AutoMatchSources = config.AutoMatchSources ?? Array.Empty<string>(),
                MaxCacheSize = config.MaxCacheSize,
                CacheExpiryDays = config.CacheExpiryDays,
                MaxDanmakuCount = config.MaxDanmakuCount,
                BilibiliSessdata = config.BilibiliSessdata,
                DefaultDisplaySettings = new DanmakuDisplaySettingsResponse
                {
                    FontSize = config.DefaultDisplaySettings.FontSize,
                    Opacity = config.DefaultDisplaySettings.Opacity,
                    Speed = config.DefaultDisplaySettings.Speed,
                    Area = config.DefaultDisplaySettings.Area,
                    EnabledTypes = config.DefaultDisplaySettings.EnabledTypes,
                    BlockedColors = config.DefaultDisplaySettings.BlockedColors,
                    BlockedUsers = config.DefaultDisplaySettings.BlockedUsers,
                    BlockedWords = config.DefaultDisplaySettings.BlockedWords,
                    DensityLimit = config.DefaultDisplaySettings.DensityLimit
                },
                UpdateSettings = new UpdateSettingsResponse
                {
                    AutoUpdate = config.UpdateSettings.AutoUpdate,
                    UpdateIntervalHours = config.UpdateSettings.UpdateIntervalHours,
                    PreferProtobuf = config.UpdateSettings.PreferProtobuf
                }
            });
        }

        /// <summary>
        /// Update global danmaku configuration.
        /// </summary>
        [HttpPut]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<DanmakuConfigResponse> UpdateConfig([FromBody] DanmakuConfigResponse request)
        {
            var config = Plugin.Instance?.Configuration;
            if (config == null)
            {
                return StatusCode(500, new { errorCode = "PluginNotInitialized", message = "Plugin not initialized" });
            }

            config.Enabled = request.Enabled;
            config.DefaultEnabled = request.DefaultEnabled;
            config.AutoMatch = request.AutoMatch;
            config.AutoMatchSources = request.AutoMatchSources;
            config.MaxCacheSize = request.MaxCacheSize;
            config.CacheExpiryDays = request.CacheExpiryDays;
            config.MaxDanmakuCount = request.MaxDanmakuCount;
            config.BilibiliSessdata = request.BilibiliSessdata;
            _configManager.BilibiliSessdata = request.BilibiliSessdata;

            if (request.DefaultDisplaySettings != null)
            {
                config.DefaultDisplaySettings.FontSize = request.DefaultDisplaySettings.FontSize;
                config.DefaultDisplaySettings.Opacity = request.DefaultDisplaySettings.Opacity;
                config.DefaultDisplaySettings.Speed = request.DefaultDisplaySettings.Speed;
                config.DefaultDisplaySettings.Area = request.DefaultDisplaySettings.Area;
                config.DefaultDisplaySettings.EnabledTypes = request.DefaultDisplaySettings.EnabledTypes;
                config.DefaultDisplaySettings.BlockedColors = request.DefaultDisplaySettings.BlockedColors;
                config.DefaultDisplaySettings.BlockedUsers = request.DefaultDisplaySettings.BlockedUsers;
                config.DefaultDisplaySettings.BlockedWords = request.DefaultDisplaySettings.BlockedWords;
                config.DefaultDisplaySettings.DensityLimit = request.DefaultDisplaySettings.DensityLimit;
            }

            if (request.UpdateSettings != null)
            {
                config.UpdateSettings.AutoUpdate = request.UpdateSettings.AutoUpdate;
                config.UpdateSettings.UpdateIntervalHours = request.UpdateSettings.UpdateIntervalHours;
                config.UpdateSettings.PreferProtobuf = request.UpdateSettings.PreferProtobuf;
            }

            Plugin.Instance!.SaveConfiguration();

            return GetConfig();
        }

        /// <summary>
        /// Get user danmaku preferences.
        /// </summary>
        [HttpGet("user/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<UserDanmakuPreferencesResponse> GetUserPreferences([FromRoute] Guid userId)
        {
            // In a full implementation, this would load per-user settings
            return Ok(new UserDanmakuPreferencesResponse
            {
                UserId = userId,
                DanmakuEnabled = true,
                DisplaySettings = new DanmakuDisplaySettingsResponse()
            });
        }

        /// <summary>
        /// Update user danmaku preferences.
        /// </summary>
        [HttpPut("user/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<UserDanmakuPreferencesResponse> UpdateUserPreferences(
            [FromRoute] Guid userId,
            [FromBody] UserDanmakuPreferencesResponse request)
        {
            // In a full implementation, this would save per-user settings
            request.UserId = userId;
            return Ok(request);
        }
    }
}
