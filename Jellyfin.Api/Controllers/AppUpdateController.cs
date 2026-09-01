using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Models.AppUpdateDtos;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// App update controller.
/// </summary>
[Route("AppUpdate")]
[Authorize]
public class AppUpdateController : BaseJellyfinApiController
{
    private readonly IAppUpdateRepository _appUpdateRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUpdateController"/> class.
    /// </summary>
    /// <param name="appUpdateRepository">Instance of <see cref="IAppUpdateRepository"/>.</param>
    public AppUpdateController(IAppUpdateRepository appUpdateRepository)
    {
        _appUpdateRepository = appUpdateRepository;
    }

    /// <summary>
    /// Checks for an app update.
    /// </summary>
    /// <param name="currentVersionCode">Current client version code.</param>
    /// <param name="currentVersion">Current client version string.</param>
    /// <param name="channel">Release channel (stable, beta, alpha).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Update check result returned.</response>
    /// <returns>An <see cref="OkResult"/> containing the update info or no-update indicator.</returns>
    [HttpGet("Check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AppUpdateCheckResponse>> CheckForUpdate(
        [FromQuery, Required] int currentVersionCode,
        [FromQuery, Required] string currentVersion,
        [FromQuery] string channel = "stable",
        CancellationToken cancellationToken = default)
    {
        var release = await _appUpdateRepository.GetLatestReleaseAsync(channel, currentVersionCode, cancellationToken).ConfigureAwait(false);

        if (release is null)
        {
            return new AppUpdateCheckResponse { UpdateAvailable = false };
        }

        Dictionary<string, string>? changelog = null;
        if (!string.IsNullOrEmpty(release.Changelog))
        {
            changelog = JsonSerializer.Deserialize<Dictionary<string, string>>(release.Changelog);
        }

        return new AppUpdateCheckResponse
        {
            UpdateAvailable = true,
            AppVersion = release.VersionString,
            AppVersionCode = release.VersionCode,
            MinVersion = release.MinVersion,
            ReleaseDate = release.ReleaseDate,
            Changelog = changelog,
            DownloadUrl = release.DownloadUrl,
            DownloadSize = release.FileSize,
            Checksum = release.Checksum,
            Mandatory = release.Mandatory,
            MinServerVersion = release.MinServerVersion,
            ReleaseNotes = release.ReleaseNotesUrl
        };
    }

    /// <summary>
    /// Gets the list of available releases.
    /// </summary>
    /// <param name="channel">Optional channel filter.</param>
    /// <param name="limit">Max results (default 10).</param>
    /// <param name="offset">Pagination offset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Release list returned.</response>
    /// <returns>An <see cref="OkResult"/> containing the release list.</returns>
    [HttpGet("Releases")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<AppReleasesResponse>> GetReleases(
        [FromQuery] string? channel = null,
        [FromQuery] int limit = 10,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var releases = await _appUpdateRepository.GetReleasesAsync(channel, limit, offset, cancellationToken).ConfigureAwait(false);

        var dtos = releases.Select(r =>
        {
            Dictionary<string, string>? changelog = null;
            if (!string.IsNullOrEmpty(r.Changelog))
            {
                changelog = JsonSerializer.Deserialize<Dictionary<string, string>>(r.Changelog);
            }

            return new AppReleaseInfoDto
            {
                AppVersion = r.VersionString,
                AppVersionCode = r.VersionCode,
                ReleaseDate = r.ReleaseDate,
                Channel = r.Channel,
                Changelog = changelog,
                DownloadSize = r.FileSize,
                Mandatory = r.Mandatory
            };
        }).ToList();

        return new AppReleasesResponse { Releases = dtos };
    }

    /// <summary>
    /// Creates a new app release. Requires admin privileges.
    /// </summary>
    /// <param name="dto">The release data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Release created.</response>
    /// <response code="400">Invalid release data.</response>
    /// <returns>An <see cref="OkResult"/> containing the created release.</returns>
    [HttpPost("Releases")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AppReleaseInfoDto>> CreateRelease(
        [FromBody, Required] AppReleaseDto dto,
        CancellationToken cancellationToken)
    {
        var release = new AppRelease
        {
            VersionString = dto.VersionString,
            VersionCode = dto.VersionCode,
            Channel = dto.Channel,
            ReleaseDate = dto.ReleaseDate,
            Changelog = dto.Changelog,
            DownloadUrl = dto.DownloadUrl,
            FileSize = dto.FileSize,
            Checksum = dto.Checksum,
            Mandatory = dto.Mandatory,
            MinVersion = dto.MinVersion,
            MinServerVersion = dto.MinServerVersion,
            ReleaseNotesUrl = dto.ReleaseNotesUrl
        };

        var created = await _appUpdateRepository.CreateReleaseAsync(release, cancellationToken).ConfigureAwait(false);

        return MapToDto(created);
    }

    /// <summary>
    /// Updates an existing app release. Requires admin privileges.
    /// </summary>
    /// <param name="id">The release id.</param>
    /// <param name="dto">The release data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Release updated.</response>
    /// <response code="404">Release not found.</response>
    /// <returns>An <see cref="OkResult"/> containing the updated release.</returns>
    [HttpPut("Releases/{id:guid}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppReleaseInfoDto>> UpdateRelease(
        [FromRoute] Guid id,
        [FromBody, Required] AppReleaseDto dto,
        CancellationToken cancellationToken)
    {
        var existing = new AppRelease { Id = id };
        existing.VersionString = dto.VersionString;
        existing.VersionCode = dto.VersionCode;
        existing.Channel = dto.Channel;
        existing.ReleaseDate = dto.ReleaseDate;
        existing.Changelog = dto.Changelog;
        existing.DownloadUrl = dto.DownloadUrl;
        existing.FileSize = dto.FileSize;
        existing.Checksum = dto.Checksum;
        existing.Mandatory = dto.Mandatory;
        existing.MinVersion = dto.MinVersion;
        existing.MinServerVersion = dto.MinServerVersion;
        existing.ReleaseNotesUrl = dto.ReleaseNotesUrl;

        var updated = await _appUpdateRepository.UpdateReleaseAsync(existing, cancellationToken).ConfigureAwait(false);
        return MapToDto(updated);
    }

    /// <summary>
    /// Deletes an app release. Requires admin privileges.
    /// </summary>
    /// <param name="id">The release id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Release deleted.</response>
    /// <response code="404">Release not found.</response>
    /// <returns>An <see cref="OkResult"/>.</returns>
    [HttpDelete("Releases/{id:guid}")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRelease(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _appUpdateRepository.DeleteReleaseAsync(id, cancellationToken).ConfigureAwait(false);
        if (!deleted)
        {
            return NotFound();
        }

        return Ok();
    }

    private static AppReleaseInfoDto MapToDto(AppRelease release)
    {
        Dictionary<string, string>? changelog = null;
        if (!string.IsNullOrEmpty(release.Changelog))
        {
            changelog = JsonSerializer.Deserialize<Dictionary<string, string>>(release.Changelog);
        }

        return new AppReleaseInfoDto
        {
            AppVersion = release.VersionString,
            AppVersionCode = release.VersionCode,
            ReleaseDate = release.ReleaseDate,
            Channel = release.Channel,
            Changelog = changelog,
            DownloadSize = release.FileSize,
            Mandatory = release.Mandatory
        };
    }
}
