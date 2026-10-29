using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class ScreeningConfiguration : IEntityTypeConfiguration<Screening>
{
    public void Configure(EntityTypeBuilder<Screening> builder)
    {
        builder.ToTable("screenings");

        builder.HasKey(s => s.Id).HasName("screenings_pkey");
        builder.Property(s => s.Id).UseSerialColumn();

        // snake_case convention would mangle these (is3_d / is_o_v); pin them explicitly.
        builder.Property(s => s.Is3D).HasColumnName("is_3d").HasDefaultValue(false);
        builder.Property(s => s.IsOV).HasColumnName("is_ov").HasDefaultValue(false);

        builder.Property(s => s.CreatedAt).HasDefaultValueSql(SqlDefaults.UtcNowText);
        builder.Property(s => s.UpdatedAt).HasDefaultValueSql(SqlDefaults.UtcNowText);

        // Prevents duplicate showings; also the scraper's upsert conflict target.
        builder.HasAlternateKey(s => new { s.CinemaId, s.MovieId, s.Datetime })
            .HasName("screenings_cinema_id_movie_id_datetime_key");

        builder.HasIndex(s => s.CinemaId).HasDatabaseName("idx_screenings_cinema");
        builder.HasIndex(s => s.MovieId).HasDatabaseName("idx_screenings_movie");
        builder.HasIndex(s => s.Datetime).HasDatabaseName("idx_screenings_datetime");

        builder.HasOne(s => s.Movie)
            .WithMany(m => m.Screenings)
            .HasForeignKey(s => s.MovieId)
            .HasConstraintName("screenings_movie_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Cinema)
            .WithMany(c => c.Screenings)
            .HasForeignKey(s => s.CinemaId)
            .HasConstraintName("screenings_cinema_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
