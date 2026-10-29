using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class FelixCacheConfiguration : IEntityTypeConfiguration<FelixCache>
{
    public void Configure(EntityTypeBuilder<FelixCache> builder)
    {
        builder.ToTable("felix_cache");

        builder.HasKey(f => f.Id).HasName("felix_cache_pkey");
        builder.Property(f => f.Id).ValueGeneratedNever().HasDefaultValue(1);   // single row, always 1

        builder.Property(f => f.CreatedAt).HasDefaultValueSql(SqlDefaults.UtcNowText);
    }
}
