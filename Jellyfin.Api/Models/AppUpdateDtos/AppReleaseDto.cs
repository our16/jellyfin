using System;
using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.AppUpdateDtos;

/// <summary>
/// Request DTO for creating or updating a release (admin).
/// </summary>
public class AppReleaseDto
{
    /// <summary>
    /// Gets or sets the version string (e.g. "1.2.0").
    /// </summary>
    [Required]
    [MaxLength(32)]
    public string VersionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the numeric version code.
    /// </summary>
    public int VersionCode { get; set; }

    /// <summary>
    /// Gets or sets the release channel.
    /// </summary>
    [MaxLength(16)]
    public string Channel { get; set; } = "stable";

    /// <summary>
    /// Gets or sets the release date.
    /// </summary>
    public DateTime ReleaseDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the changelog as JSON string.
    /// </summary>
    public string? Changelog { get; set; }

    /// <summary>
    /// Gets or sets the download URL.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the checksum (format: "sha256:...").
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the update is mandatory.
    /// </summary>
    public bool Mandatory { get; set; }

    /// <summary>
    /// Gets or sets the minimum client version.
    /// </summary>
    [MaxLength(32)]
    public string? MinVersion { get; set; }

    /// <summary>
    /// Gets or sets the minimum server version.
    /// </summary>
    [MaxLength(32)]
    public string? MinServerVersion { get; set; }

    /// <summary>
    /// Gets or sets the release notes URL.
    /// </summary>
    [MaxLength(2048)]
    public string? ReleaseNotesUrl { get; set; }
}
