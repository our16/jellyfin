using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration;

/// <summary>
/// AppRelease configuration.
/// </summary>
public class AppReleaseConfiguration : IEntityTypeConfiguration<AppRelease>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AppRelease> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.Channel, e.VersionCode }).IsUnique();
        builder.HasIndex(e => e.VersionCode);
        builder.HasIndex(e => e.Channel);
    }
}
