using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class AiMatchVerdictConfiguration : IEntityTypeConfiguration<AiMatchVerdict>
{
    public void Configure(EntityTypeBuilder<AiMatchVerdict> builder)
    {
        builder.ToTable("ai_match_verdicts", t =>
        {
            t.HasCheckConstraint("ai_match_verdicts_verdict_check",
                "verdict IN ('match', 'not_a_film', 'unknown')");
            t.HasCheckConstraint("ai_match_verdicts_confidence_check",
                "confidence IN ('high', 'low')");
        });

        // The table's unique (scraped_title, scraped_original_title) pair is its identity.
        builder.HasKey(v => new { v.ScrapedTitle, v.ScrapedOriginalTitle });

        builder.Property(v => v.ScrapedOriginalTitle).HasDefaultValue("");
        builder.Property(v => v.RejectedTmdbIds).HasDefaultValueSql("'{}'");
    }
}
