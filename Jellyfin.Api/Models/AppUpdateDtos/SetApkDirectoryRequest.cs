using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Api.Models.AppUpdateDtos;

/// <summary>
/// Request DTO for setting the APK storage directory.
/// </summary>
public class SetApkDirectoryRequest
{
    /// <summary>
    /// Gets or sets the directory path for APK storage.
    /// </summary>
    [Required]
    public string Directory { get; set; } = string.Empty;
}
