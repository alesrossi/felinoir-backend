using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class FelixCacheConfiguration : IEntityTypeConfiguration<FelixCache>
{
    public void Configure(EntityTypeBuilder<FelixCache> builder)
    {
        builder.ToTable("felix_cache");

        // One row per city.
        builder.HasKey(f => f.CityId).HasName("felix_cache_pkey");
        builder.HasOne<City>()
            .WithMany()
            .HasForeignKey(f => f.CityId)
            .HasConstraintName("felix_cache_city_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(f => f.CreatedAt).HasDefaultValueSql(SqlDefaults.UtcNowText);
    }
}
