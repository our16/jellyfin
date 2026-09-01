using System;
using System.Collections.Generic;

namespace Jellyfin.Api.Models.AppUpdateDtos;

/// <summary>
/// A single release entry in the release list.
/// </summary>
public class AppReleaseInfoDto
{
    /// <summary>
    /// Gets or sets the release id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version string.
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the numeric version code.
    /// </summary>
    public int AppVersionCode { get; set; }

    /// <summary>
    /// Gets or sets the release date.
    /// </summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets the release channel.
    /// </summary>
    public string Channel { get; set; } = "stable";

    /// <summary>
    /// Gets or sets the changelog by locale.
    /// </summary>
    public Dictionary<string, string>? Changelog { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long DownloadSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the update is mandatory.
    /// </summary>
    public bool Mandatory { get; set; }
}
