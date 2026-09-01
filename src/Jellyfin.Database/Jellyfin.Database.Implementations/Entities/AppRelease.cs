using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Jellyfin.Database.Implementations.Entities;

/// <summary>
/// Represents a client application release for update distribution.
/// </summary>
public class AppRelease
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the version string (e.g. "1.2.0").
    /// </summary>
    [Required]
    [MaxLength(32)]
    public string VersionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the numeric version code (MAJOR×1000000 + MINOR×10000 + PATCH×100).
    /// </summary>
    public int VersionCode { get; set; }

    /// <summary>
    /// Gets or sets the release channel (stable, beta, alpha).
    /// </summary>
    [Required]
    [MaxLength(16)]
    public string Channel { get; set; } = "stable";

    /// <summary>
    /// Gets or sets the release date.
    /// </summary>
    public DateTime ReleaseDate { get; set; }

    /// <summary>
    /// Gets or sets the changelog as JSON (e.g. {"zh-CN": "...", "en": "..."}).
    /// </summary>
    public string? Changelog { get; set; }

    /// <summary>
    /// Gets or sets the download URL for the APK file.
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
    /// Gets or sets the minimum client version required.
    /// </summary>
    [MaxLength(32)]
    public string? MinVersion { get; set; }

    /// <summary>
    /// Gets or sets the minimum server version required.
    /// </summary>
    [MaxLength(32)]
    public string? MinServerVersion { get; set; }

    /// <summary>
    /// Gets or sets the URL to the full release notes page.
    /// </summary>
    [MaxLength(2048)]
    public string? ReleaseNotesUrl { get; set; }

    /// <summary>
    /// Gets or sets the date the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
