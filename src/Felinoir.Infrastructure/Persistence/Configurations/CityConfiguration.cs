using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities");

        builder.HasKey(c => c.Id).HasName("cities_pkey");
        builder.Property(c => c.Id).ValueGeneratedNever();   // slug, e.g. "rome"

        builder.Property(c => c.CreatedAt).HasDefaultValueSql(SqlDefaults.UtcNowText);
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql(SqlDefaults.UtcNowText);

        // Rows are reference data inserted by migrations (Rome by the Cities migration).
    }
}
