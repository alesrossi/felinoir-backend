using Felinoir.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

public class LobbyParticipantConfiguration : IEntityTypeConfiguration<LobbyParticipant>
{
    public void Configure(EntityTypeBuilder<LobbyParticipant> builder)
    {
        builder.ToTable("lobby_participants", t =>
            t.HasCheckConstraint("lobby_participants_name_length",
                "char_length(name) between 1 and 40"));

        builder.HasKey(p => p.Id).HasName("lobby_participants_pkey");
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.JoinedAt).HasDefaultValueSql("now()");

        // Unique, with nulls allowed many times (unclaimed entries); also serves the lookup of a
        // lobby's participants. 23505 on it means "already joined".
        builder.HasIndex(p => new { p.LobbyId, p.DeviceHash })
            .IsUnique()
            .HasDatabaseName("lobby_participants_one_per_device");

        // The case-insensitive name index, lobby_participants_name_uniq on
        // (lobby_id, lower(name)), is an expression index EF can't model; it exists only
        // in the migration. 23505 on it means "name taken".
    }
}
