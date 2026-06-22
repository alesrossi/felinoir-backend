using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class FelixCacheConfiguration : IEntityTypeConfiguration<FelixCache>
{
    public void Configure(EntityTypeBuilder<FelixCache> builder)
    {
        builder.ToTable("felix_cache");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();   // single row, always 1
    }
}
