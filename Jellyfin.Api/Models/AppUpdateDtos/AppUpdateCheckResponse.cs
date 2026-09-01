using System;
using System.Collections.Generic;

namespace Jellyfin.Api.Models.AppUpdateDtos;

/// <summary>
/// Response DTO for the Check for Update endpoint.
/// </summary>
public class AppUpdateCheckResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether an update is available.
    /// </summary>
    public bool UpdateAvailable { get; set; }

    /// <summary>
    /// Gets or sets the new version string.
    /// </summary>
    public string? AppVersion { get; set; }

    /// <summary>
    /// Gets or sets the numeric version code.
    /// </summary>
    public int AppVersionCode { get; set; }

    /// <summary>
    /// Gets or sets the minimum client version required for force update.
    /// </summary>
    public string? MinVersion { get; set; }

    /// <summary>
    /// Gets or sets the release date.
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets the changelog by locale.
    /// </summary>
    public Dictionary<string, string>? Changelog { get; set; }

    /// <summary>
    /// Gets or sets the download URL.
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long DownloadSize { get; set; }

    /// <summary>
    /// Gets or sets the checksum string.
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the update is mandatory.
    /// </summary>
    public bool Mandatory { get; set; }

    /// <summary>
    /// Gets or sets the minimum server version.
    /// </summary>
    public string? MinServerVersion { get; set; }

    /// <summary>
    /// Gets or sets the release notes URL.
    /// </summary>
    public string? ReleaseNotes { get; set; }
}
