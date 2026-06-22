using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> builder)
    {
        builder.ToTable("movies");

        builder.HasKey(m => m.Id);   // SERIAL — ValueGeneratedOnAdd by convention

        // Partial unique indexes: unique only where the column is non-null.
        builder.HasIndex(m => m.Slug).IsUnique().HasFilter("slug IS NOT NULL");
        builder.HasIndex(m => m.TmdbId).IsUnique().HasFilter("tmdb_id IS NOT NULL");
        builder.HasIndex(m => m.ImdbId).IsUnique().HasFilter("imdb_id IS NOT NULL");

        // NOTE: the scraper dedup key — UNIQUE (title, COALESCE(original_title, '')) —
        // is a functional/expression index EF cannot model. It is managed outside EF
        // and intentionally not represented here so migrations never try to recreate it.
    }
}
