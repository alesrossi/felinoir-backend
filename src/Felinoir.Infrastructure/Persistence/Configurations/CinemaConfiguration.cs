using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class CinemaConfiguration : IEntityTypeConfiguration<Cinema>
{
    public void Configure(EntityTypeBuilder<Cinema> builder)
    {
        builder.ToTable("cinemas");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();   // slug, supplied by scraper

        builder.Property(c => c.Country).HasDefaultValue("IT");

        builder.HasIndex(c => c.City).HasDatabaseName("idx_cinemas_city");
    }
}
