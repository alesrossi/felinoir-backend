using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class LobbyConfiguration : IEntityTypeConfiguration<Lobby>
{
    public void Configure(EntityTypeBuilder<Lobby> builder)
    {
        builder.ToTable("lobbies", t =>
            t.HasCheckConstraint("lobbies_note_length", "char_length(note) <= 500"));

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");

        // Owned by the bump-version triggers: EF never writes it and reads it back
        // after every insert/update.
        builder.Property(l => l.Version)
            .HasDefaultValue(1L)
            .ValueGeneratedOnAddOrUpdate();

        // snake_case convention would mangle these (is3_d / is_o_v); pin them explicitly.
        builder.Property(l => l.Is3D).HasColumnName("is_3d").HasDefaultValue(false);
        builder.Property(l => l.IsOV).HasColumnName("is_ov").HasDefaultValue(false);

        builder.Property(l => l.Note).HasDefaultValue("");
        builder.Property(l => l.CreatedAt).HasDefaultValueSql("now()");

        // Used by delete_expired_lobbies().
        builder.HasIndex(l => l.StartsAt).HasDatabaseName("lobbies_starts_at_idx");

        builder.HasMany(l => l.Participants)
            .WithOne(p => p.Lobby)
            .HasForeignKey(p => p.LobbyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
