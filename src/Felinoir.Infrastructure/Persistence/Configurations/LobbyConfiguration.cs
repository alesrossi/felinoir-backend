using Felinoir.Domain.Lobbies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Felinoir.Infrastructure.Persistence.Configurations;

/// <summary>The Lobby aggregate onto <c>lobbies</c> (the root and its screening snapshot) and <c>lobby_participants</c>.</summary>
public class LobbyConfiguration : IEntityTypeConfiguration<Lobby>
{
    public void Configure(EntityTypeBuilder<Lobby> builder)
    {
        builder.ToTable("lobbies", t =>
            t.HasCheckConstraint("lobbies_note_length", "char_length(note) <= 500"));

        builder.HasKey(l => l.Id).HasName("lobbies_pkey");
        // The aggregate makes its own ids, so EF never treats a set id as "already saved"; the
        // column default is only for rows written outside EF.
        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new LobbyId(value))
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();

        // Incremented by the aggregate on every change; a save whose version is stale fails, so
        // of two concurrent changes to a lobby only one lands.
        builder.Property(l => l.Version).HasDefaultValue(1L).IsConcurrencyToken();

        builder.Property(l => l.Note)
            .HasConversion(note => note.Value, value => LobbyNote.FromStored(value))
            .HasDefaultValue(LobbyNote.Empty);

        builder.Property(l => l.CreatedAt).HasDefaultValueSql("now()");

        // Used by delete_expired_lobbies().
        builder.HasIndex(l => l.StartsAt).HasDatabaseName("lobbies_starts_at_idx");

        builder.ComplexProperty(l => l.Screening, screening =>
        {
            screening.Property(s => s.ScreeningId).HasColumnName("screening_id");
            screening.Property(s => s.MovieId).HasColumnName("movie_id");
            screening.Property(s => s.MovieSlug).HasColumnName("movie_slug");
            screening.Property(s => s.MovieTitle).HasColumnName("movie_title");
            screening.Property(s => s.MovieTitleEn).HasColumnName("movie_title_en");
            screening.Property(s => s.PosterUrl).HasColumnName("poster_url");
            screening.Property(s => s.CinemaId).HasColumnName("cinema_id");
            screening.Property(s => s.CinemaName).HasColumnName("cinema_name");
            // snake_case convention would mangle these (is3_d / is_o_v); pin them explicitly.
            screening.Property(s => s.IsOV).HasColumnName("is_ov").HasDefaultValue(false);
            screening.Property(s => s.Is3D).HasColumnName("is_3d").HasDefaultValue(false);
            screening.Property(s => s.BookingUrl).HasColumnName("booking_url");
        });

        builder.HasMany(l => l.Participants)
            .WithOne()
            .HasForeignKey(p => p.LobbyId)
            .HasConstraintName("lobby_participants_lobby_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(l => l.Participants).HasField("_participants");
    }
}

public class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("lobby_participants", t =>
            t.HasCheckConstraint("lobby_participants_name_length", "char_length(name) between 1 and 40"));

        builder.HasKey(p => p.Id).HasName("lobby_participants_pkey");
        // As for the lobby: a participant added to a loaded lobby must be inserted, not updated.
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new ParticipantId(value))
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedNever();

        builder.Property(p => p.LobbyId).HasConversion(id => id.Value, value => new LobbyId(value));

        builder.Property(p => p.Name)
            .HasConversion(name => name.Value, value => ParticipantName.FromStored(value))
            .HasColumnName("name");

        builder.Property(p => p.Device)
            .HasConversion(device => device!.ToArray(), bytes => DeviceHash.FromStored(bytes)) // EF never converts null
            .HasColumnName("device_hash");

        builder.Property(p => p.JoinedAt).HasDefaultValueSql("now()");

        // Also covers the lookup of a lobby's participants.
        builder.HasIndex(p => p.LobbyId).HasDatabaseName("lobby_participants_lobby_id_idx");

        // Two rules live only in migrations, because EF can't model them:
        // - lobby_participants_name_uniq: unique (lobby_id, lower(name)), an expression index;
        // - lobby_participants_one_per_device: unique (lobby_id, device_hash), DEFERRABLE INITIALLY
        //   DEFERRED, so a claim can move a device between two entries in one save whatever order
        //   the UPDATEs run in. It is checked at commit.
        // Both back up the aggregate's own checks against concurrent requests.
    }
}
