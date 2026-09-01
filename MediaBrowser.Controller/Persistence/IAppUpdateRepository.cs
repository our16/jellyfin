using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;

namespace MediaBrowser.Controller.Persistence;

/// <summary>
/// Interface for app update repository.
/// </summary>
public interface IAppUpdateRepository
{
    /// <summary>
    /// Gets the latest release for a given channel with a version code higher than the specified value.
    /// </summary>
    /// <param name="channel">The release channel.</param>
    /// <param name="currentVersionCode">The current client version code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The latest matching release, or null if none found.</returns>
    Task<AppRelease?> GetLatestReleaseAsync(string channel, int currentVersionCode, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a list of releases, optionally filtered by channel.
    /// </summary>
    /// <param name="channel">Optional channel filter.</param>
    /// <param name="limit">Max number of results.</param>
    /// <param name="offset">Pagination offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>List of releases.</returns>
    Task<IReadOnlyList<AppRelease>> GetReleasesAsync(string? channel, int limit, int offset, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a release by version string and channel.
    /// </summary>
    /// <param name="versionString">The version string.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The release, or null if not found.</returns>
    Task<AppRelease?> GetReleaseAsync(string versionString, string channel, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new release.
    /// </summary>
    /// <param name="release">The release entity to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created release.</returns>
    Task<AppRelease> CreateReleaseAsync(AppRelease release, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing release.
    /// </summary>
    /// <param name="release">The release entity to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated release.</returns>
    Task<AppRelease> UpdateReleaseAsync(AppRelease release, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a release by id.
    /// </summary>
    /// <param name="id">The release id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteReleaseAsync(Guid id, CancellationToken cancellationToken);
}
