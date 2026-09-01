using System.Collections.Generic;

namespace Jellyfin.Api.Models.AppUpdateDtos;

/// <summary>
/// Response DTO for the Get Release List endpoint.
/// </summary>
public class AppReleasesResponse
{
    /// <summary>
    /// Gets or sets the list of releases.
    /// </summary>
    public IReadOnlyList<AppReleaseInfoDto> Releases { get; set; } = [];
}
