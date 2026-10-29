using Felinoir.Infrastructure.Persistence.ScraperTables;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class TmdbLookupCacheConfiguration : IEntityTypeConfiguration<TmdbLookupCache>
{
    public void Configure(EntityTypeBuilder<TmdbLookupCache> builder)
    {
        builder.ToTable("tmdb_lookup_cache");

        // The table's unique (scraped_title, scraped_original_title) pair is its identity.
        builder.HasKey(t => new { t.ScrapedTitle, t.ScrapedOriginalTitle })
            .HasName("tmdb_lookup_cache_pkey");

        builder.Property(t => t.ScrapedOriginalTitle).HasDefaultValue("");
        builder.Property(t => t.LookedUpAt).HasDefaultValueSql(SqlDefaults.UtcNowText);
    }
}
