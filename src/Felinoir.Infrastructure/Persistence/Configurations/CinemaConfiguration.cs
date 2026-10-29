using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class CinemaConfiguration : IEntityTypeConfiguration<Cinema>
{
    public void Configure(EntityTypeBuilder<Cinema> builder)
    {
        builder.ToTable("cinemas");

        builder.HasKey(c => c.Id).HasName("cinemas_pkey");
        builder.Property(c => c.Id).ValueGeneratedNever();   // slug, supplied by scraper

        builder.Property(c => c.Country).HasDefaultValue("IT");
        builder.Property(c => c.CreatedAt).HasDefaultValueSql(SqlDefaults.UtcNowText);
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql(SqlDefaults.UtcNowText);

        builder.Property(c => c.OpenAir).HasDefaultValue(false);

        builder.HasOne(c => c.City)
            .WithMany(c => c.Cinemas)
            .HasForeignKey(c => c.CityId)
            .HasConstraintName("cinemas_city_id_fkey")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.CityId).HasDatabaseName("idx_cinemas_city_id");
    }
}
