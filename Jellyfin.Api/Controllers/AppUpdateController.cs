using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Api.Models.AppUpdateDtos;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Configuration;
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
    private const int CopyBufferSize = 81920;
    private const string DefaultApkDirectory = @"F:\jellyfin_cache\apk_version";

    private readonly IAppUpdateRepository _appUpdateRepository;
    private readonly IApplicationPaths _appPaths;
    private readonly IServerConfigurationManager _serverConfigurationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUpdateController"/> class.
    /// </summary>
    /// <param name="appUpdateRepository">Instance of <see cref="IAppUpdateRepository"/>.</param>
    /// <param name="appPaths">Instance of <see cref="IApplicationPaths"/>.</param>
    /// <param name="serverConfigurationManager">Instance of <see cref="IServerConfigurationManager"/>.</param>
    public AppUpdateController(
        IAppUpdateRepository appUpdateRepository,
        IApplicationPaths appPaths,
        IServerConfigurationManager serverConfigurationManager)
    {
        _appUpdateRepository = appUpdateRepository;
        _appPaths = appPaths;
        _serverConfigurationManager = serverConfigurationManager;
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
            DownloadUrl = GetDownloadBaseUrl() + release.DownloadUrl,
            DownloadSize = release.FileSize,
            Checksum = release.Checksum,
            Mandatory = release.Mandatory,
            MinServerVersion = release.MinServerVersion,
            ReleaseNotes = release.ReleaseNotesUrl
        };
    }

    /// <summary>
    /// Downloads an APK file by version.
    /// </summary>
    /// <param name="version">Target version to download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">APK binary stream.</response>
    /// <response code="404">Version not found.</response>
    /// <returns>The APK file stream.</returns>
    [HttpGet("Download")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DownloadApk(
        [FromQuery, Required] string version,
        CancellationToken cancellationToken)
    {
        var releases = await _appUpdateRepository.GetReleasesAsync(null, 100, 0, cancellationToken).ConfigureAwait(false);
        var release = releases.FirstOrDefault(r => r.VersionString == version);
        if (release is null)
        {
            return NotFound();
        }

        var filePath = GetApkFilePath(release.VersionString, release.Channel);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        return PhysicalFile(filePath, "application/vnd.android.package-archive", $"jellyfin-androidtv-{release.VersionString}.apk");
    }

    /// <summary>
    /// Uploads an APK and creates a release. Requires admin privileges.
    /// The download URL is auto-generated as {server}/AppUpdate/Download?version={version}.
    /// </summary>
    /// <param name="versionString">Version string (e.g. "1.2.0").</param>
    /// <param name="versionCode">Numeric version code.</param>
    /// <param name="channel">Release channel.</param>
    /// <param name="releaseDate">Release date.</param>
    /// <param name="changelog">Changelog JSON.</param>
    /// <param name="mandatory">Whether the update is mandatory.</param>
    /// <param name="minVersion">Minimum client version.</param>
    /// <param name="minServerVersion">Minimum server version.</param>
    /// <param name="releaseNotesUrl">Release notes URL.</param>
    /// <param name="file">The APK file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Release created with download URL.</response>
    /// <response code="400">Invalid request.</response>
    /// <returns>The created release info.</returns>
    [HttpPost("Upload")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AppReleaseInfoDto>> UploadApk(
        [FromQuery, Required] string versionString,
        [FromQuery] int versionCode = 0,
        [FromQuery] string channel = "stable",
        [FromQuery] DateTime? releaseDate = null,
        [FromQuery] string? changelog = null,
        [FromQuery] bool mandatory = false,
        [FromQuery] string? minVersion = null,
        [FromQuery] string? minServerVersion = null,
        [FromQuery] string? releaseNotesUrl = null,
        IFormFile? file = null,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        if (!file.FileName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only .apk files are accepted.");
        }

        // Save APK to configured directory
        var apkDir = GetApkDirectory();
        Directory.CreateDirectory(apkDir);
        var filePath = GetApkFilePath(versionString, channel);

        var buffer = new byte[CopyBufferSize];
        await using (var sourceStream = file.OpenReadStream())
        await using (var destStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, FileOptions.Asynchronous))
        {
            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
        }

        // Compute SHA256 checksum
        string checksum;
        await using (var sha256Stream = System.IO.File.OpenRead(filePath))
        {
            var hash = await SHA256.HashDataAsync(sha256Stream, cancellationToken).ConfigureAwait(false);
            checksum = "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
        }

        // Construct download URL relative to server
        var downloadUrl = $"/AppUpdate/Download?version={Uri.EscapeDataString(versionString)}";

        var release = new AppRelease
        {
            VersionString = versionString,
            VersionCode = versionCode,
            Channel = channel,
            ReleaseDate = releaseDate ?? DateTime.UtcNow,
            Changelog = changelog,
            DownloadUrl = downloadUrl,
            FileSize = new FileInfo(filePath).Length,
            Checksum = checksum,
            Mandatory = mandatory,
            MinVersion = minVersion,
            MinServerVersion = minServerVersion,
            ReleaseNotesUrl = releaseNotesUrl
        };

        var created = await _appUpdateRepository.CreateReleaseAsync(release, cancellationToken).ConfigureAwait(false);
        return MapToDto(created);
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
            Dictionary<string, string>? changelogDict = null;
            if (!string.IsNullOrEmpty(r.Changelog))
            {
                changelogDict = JsonSerializer.Deserialize<Dictionary<string, string>>(r.Changelog);
            }

            return new AppReleaseInfoDto
            {
                Id = r.Id,
                AppVersion = r.VersionString,
                AppVersionCode = r.VersionCode,
                ReleaseDate = r.ReleaseDate,
                Channel = r.Channel,
                Changelog = changelogDict,
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

    private string GetDownloadBaseUrl()
    {
        var port = Request.Host.Port ?? (Request.Scheme == "https" ? 443 : 8096);
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var iface in interfaces)
            {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var props = iface.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return $"{Request.Scheme}://{addr.Address}:{port}";
                    }
                }
            }
        }
        catch
        {
            // ignored
        }

        return $"{Request.Scheme}://{Request.Host}";
    }

    private string GetApkFilePath(string versionString, string channel)
    {
        var apkDir = GetApkDirectory();
        Directory.CreateDirectory(apkDir);
        return Path.Combine(apkDir, $"jellyfin-androidtv-{channel}-{versionString}.apk");
    }

    private string GetApkDirectory()
    {
        var config = _serverConfigurationManager.Configuration;
        return string.IsNullOrEmpty(config.AppUpdateDirectory) ? DefaultApkDirectory : config.AppUpdateDirectory;
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

    /// <summary>
    /// Gets the current APK storage directory.
    /// </summary>
    /// <response code="200">APK directory returned.</response>
    /// <returns>The configured APK directory path.</returns>
    [HttpGet("Config/Directory")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> GetApkDirectoryConfig()
    {
        var config = _serverConfigurationManager.Configuration;
        return Ok(new { directory = GetApkDirectory(), isDefault = string.IsNullOrEmpty(config.AppUpdateDirectory) });
    }

    /// <summary>
    /// Sets the APK storage directory.
    /// </summary>
    /// <param name="directory">The new directory path.</param>
    /// <response code="200">Directory updated.</response>
    /// <response code="400">Invalid path.</response>
    /// <returns>Success status.</returns>
    [HttpPost("Config/Directory")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<object> SetApkDirectoryConfig([FromBody] SetApkDirectoryRequest directory)
    {
        if (string.IsNullOrWhiteSpace(directory.Directory))
        {
            return BadRequest("Directory cannot be empty.");
        }

        if (!Path.IsPathRooted(directory.Directory))
        {
            return BadRequest("Directory must be an absolute path.");
        }

        var config = _serverConfigurationManager.Configuration;
        config.AppUpdateDirectory = directory.Directory;
        _serverConfigurationManager.SaveConfiguration("server", config);

        return Ok(new { directory = directory.Directory });
    }
}
