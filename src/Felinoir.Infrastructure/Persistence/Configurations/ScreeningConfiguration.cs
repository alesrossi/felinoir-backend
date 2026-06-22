using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class ScreeningConfiguration : IEntityTypeConfiguration<Screening>
{
    public void Configure(EntityTypeBuilder<Screening> builder)
    {
        builder.ToTable("screenings");

        builder.HasKey(s => s.Id);

        // snake_case convention would mangle these (is3_d / is_o_v); pin them explicitly.
        builder.Property(s => s.Is3D).HasColumnName("is_3d");
        builder.Property(s => s.IsOV).HasColumnName("is_ov");

        // Prevents duplicate showings; also the scraper's upsert conflict target.
        builder.HasIndex(s => new { s.CinemaId, s.MovieId, s.Datetime }).IsUnique();

        builder.HasOne(s => s.Movie)
            .WithMany(m => m.Screenings)
            .HasForeignKey(s => s.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Cinema)
            .WithMany(c => c.Screenings)
            .HasForeignKey(s => s.CinemaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
