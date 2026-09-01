using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.Item;

/// <summary>
/// App update repository implementation.
/// </summary>
public class AppUpdateRepository : IAppUpdateRepository
{
    private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppUpdateRepository"/> class.
    /// </summary>
    /// <param name="dbProvider">The EFCore provider.</param>
    public AppUpdateRepository(IDbContextFactory<JellyfinDbContext> dbProvider)
    {
        _dbProvider = dbProvider;
    }

    /// <inheritdoc />
    public async Task<AppRelease?> GetLatestReleaseAsync(string channel, int currentVersionCode, CancellationToken cancellationToken)
    {
        var dbContext = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            return await dbContext.AppReleases
                .AsNoTracking()
                .Where(r => r.Channel == channel && r.VersionCode > currentVersionCode)
                .OrderByDescending(r => r.VersionCode)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppRelease>> GetReleasesAsync(string? channel, int limit, int offset, CancellationToken cancellationToken)
    {
        var dbContext = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            var query = dbContext.AppReleases.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(channel))
            {
                query = query.Where(r => r.Channel == channel);
            }

            return await query
                .OrderByDescending(r => r.VersionCode)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<AppRelease?> GetReleaseAsync(string versionString, string channel, CancellationToken cancellationToken)
    {
        var dbContext = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            return await dbContext.AppReleases
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.VersionString == versionString && r.Channel == channel, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<AppRelease> CreateReleaseAsync(AppRelease release, CancellationToken cancellationToken)
    {
        var dbContext = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            release.CreatedAt = DateTime.UtcNow;
            dbContext.AppReleases.Add(release);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return release;
        }
    }

    /// <inheritdoc />
    public async Task<AppRelease> UpdateReleaseAsync(AppRelease release, CancellationToken cancellationToken)
    {
        var dbContext = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            dbContext.AppReleases.Update(release);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return release;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        var dbContext = await _dbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (dbContext.ConfigureAwait(false))
        {
            var deleted = await dbContext.AppReleases
                .Where(r => r.Id == id)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            return deleted > 0;
        }
    }
}
