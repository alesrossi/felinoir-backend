using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class MovieConfiguration : IEntityTypeConfiguration<Movie>
{
    public void Configure(EntityTypeBuilder<Movie> builder)
    {
        builder.ToTable("movies", t =>
            t.HasCheckConstraint("movies_has_matches_check",
                "has_matches IN ('FailedMatch', 'NoMatch', 'FoundMatch')"));

        builder.HasKey(m => m.Id).HasName("movies_pkey");
        builder.Property(m => m.Id).UseSerialColumn();

        builder.Property(m => m.CreatedAt).HasDefaultValueSql(SqlDefaults.UtcNowText);
        builder.Property(m => m.UpdatedAt).HasDefaultValueSql(SqlDefaults.UtcNowText);

        // Columns only the scrapers' matching and enrichment use. Shadow properties keep them
        // out of the domain Movie while EF migrations still own them.
        builder.Property<string?>("CanonicalTitle");            // canonicalizeTitle(title), filled lazily
        builder.Property<int>("TmdbMatchAttempts").HasDefaultValue(0);
        builder.Property<string?>("TmdbLastAttemptAt");
        builder.Property<string>("HasMatches").IsRequired().HasDefaultValue("FailedMatch");

        builder.HasIndex(m => m.Title).HasDatabaseName("idx_movies_title");
        builder.HasIndex(m => m.ExternalId).HasDatabaseName("idx_movies_external_id");

        // Partial unique indexes: unique only where the column is non-null.
        builder.HasIndex(m => m.Slug).IsUnique().HasFilter("slug IS NOT NULL")
            .HasDatabaseName("idx_movies_slug");
        builder.HasIndex(m => m.TmdbId).IsUnique().HasFilter("tmdb_id IS NOT NULL")
            .HasDatabaseName("idx_movies_tmdb_id");
        builder.HasIndex(m => m.ImdbId).IsUnique().HasFilter("imdb_id IS NOT NULL")
            .HasDatabaseName("idx_movies_imdb_id");

        // Trigram shortlist for the scrapers' fuzzy title dedup.
        builder.HasIndex("CanonicalTitle")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("idx_movies_canonical_trgm");

        // The scrapers' dedup key, UNIQUE (title, COALESCE(original_title, '')), named
        // idx_movies_unique, is an expression index EF can't model; it exists only in the
        // Baseline migration.
    }
}
