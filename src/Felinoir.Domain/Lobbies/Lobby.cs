using Felinoir.Domain.Common;

namespace Felinoir.Domain.Lobbies;

/// <summary>
/// A shareable group outing to one screening (see CONTEXT.md), and the aggregate root of its
/// <see cref="Participant"/>s. Its rules:
/// <list type="bullet">
/// <item>it can only be created for a screening that hasn't started;</item>
/// <item>participant names are unique in the lobby, ignoring case;</item>
/// <item>a device holds at most one entry; claiming an entry releases the device's other one;</item>
/// <item>it expires <see cref="Lifetime"/> after the screening starts, and is then as good as gone.</item>
/// </list>
/// Every change that alters the lobby increments <see cref="Version"/> by one, which clients poll
/// and the database uses as the concurrency token: of two changes made at once, one fails to save.
/// </summary>
public sealed class Lobby : AggregateRoot<LobbyId>
{
    /// <summary>How long after its screening starts a lobby stays reachable.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(48);

    private readonly List<Participant> _participants = [];

    private Lobby(LobbyId id, ScreeningSnapshot screening, DateTimeOffset startsAt, LobbyNote note, DateTimeOffset createdAt)
        : base(id)
    {
        Screening = screening;
        StartsAt = startsAt;
        Note = note;
        CreatedAt = createdAt;
        Version = 1;
    }

    private Lobby() : base(default) { } // EF

    public long Version { get; private set; }

    public ScreeningSnapshot Screening { get; private init; } = default!;

    /// <summary>When the screening starts.</summary>
    public DateTimeOffset StartsAt { get; private init; }

    public LobbyNote Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public IReadOnlyCollection<Participant> Participants => _participants;

    public DateTimeOffset ExpiresAt => StartsAt + Lifetime;

    /// <summary>A lobby for a screening starting at <paramref name="startsAt"/>, with the caller as its first participant.</summary>
    public static Result<Lobby> Create(
        ScreeningSnapshot screening,
        DateTimeOffset startsAt,
        ParticipantName creator,
        DeviceHash device,
        LobbyNote note,
        DateTimeOffset now)
    {
        if (startsAt <= now) return LobbyErrors.ScreeningPast;

        var lobby = new Lobby(LobbyId.New(), screening, startsAt, note, now);
        lobby._participants.Add(new Participant(ParticipantId.New(), lobby.Id, creator, device, now));
        return lobby;
    }

    public bool IsExpiredAt(DateTimeOffset now) => ExpiresAt < now;

    /// <summary>The device's own entry, if it has one.</summary>
    public Participant? EntryOf(DeviceHash device) => _participants.FirstOrDefault(p => p.IsHeldBy(device));

    public Result Join(ParticipantName name, DeviceHash device, DateTimeOffset now)
    {
        if (EntryOf(device) is not null) return LobbyErrors.AlreadyJoined;
        if (IsTaken(name, except: null)) return LobbyErrors.NameTaken;

        _participants.Add(new Participant(ParticipantId.New(), Id, name, device, now));
        return Changed();
    }

    /// <summary>Anyone may rename anyone, to any name no one else has (re-casing one's own is fine).</summary>
    public Result Rename(ParticipantId participantId, ParticipantName name)
    {
        if (Find(participantId) is not { } participant) return LobbyErrors.NotFound;
        if (IsTaken(name, except: participant)) return LobbyErrors.NameTaken;
        if (participant.Name.Value == name.Value) return Result.Success;

        participant.Name = name;
        return Changed();
    }

    /// <summary>Anyone may remove anyone; leaving is removing yourself.</summary>
    public Result Remove(ParticipantId participantId)
    {
        if (Find(participantId) is not { } participant) return LobbyErrors.NotFound;

        _participants.Remove(participant);
        return Changed();
    }

    /// <summary>
    /// Makes <paramref name="participantId"/> the device's entry: the device leaves any other entry it
    /// held, and whoever held the claimed entry loses it. Claiming one's own entry changes nothing.
    /// </summary>
    public Result Claim(ParticipantId participantId, DeviceHash device)
    {
        if (Find(participantId) is not { } participant) return LobbyErrors.NotFound;
        if (participant.IsHeldBy(device)) return Result.Success;

        if (EntryOf(device) is { } previous) previous.Device = null;
        participant.Device = device;
        return Changed();
    }

    public Result ChangeNote(LobbyNote note)
    {
        if (Note.Value == note.Value) return Result.Success;

        Note = note;
        return Changed();
    }

    private Participant? Find(ParticipantId id) => _participants.FirstOrDefault(p => p.Id == id);

    private bool IsTaken(ParticipantName name, Participant? except) =>
        _participants.Any(p => p != except && p.Name.SameAs(name));

    private Result Changed()
    {
        Version++;
        return Result.Success;
    }
}
