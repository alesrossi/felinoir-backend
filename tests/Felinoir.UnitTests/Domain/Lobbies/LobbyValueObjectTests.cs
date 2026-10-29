using Felinoir.Domain.Lobbies;

namespace Felinoir.UnitTests.Domain.Lobbies;

public class LobbyValueObjectTests
{
    [Theory]
    [InlineData("Giulia", "Giulia")]
    [InlineData("  Marco  ", "Marco")]
    [InlineData("Anna Maria", "Anna Maria")]
    [InlineData("Zoë 🎬", "Zoë 🎬")]
    public void A_name_is_trimmed(string input, string expected)
    {
        Assert.True(ParticipantName.TryCreate(input, out var name));
        Assert.Equal(expected, name.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Gi\nulia")]
    [InlineData("Gi\tulia")]
    [InlineData("Giulia\u0007")]
    public void A_name_is_never_empty_or_with_control_characters(string? input) =>
        Assert.False(ParticipantName.IsValid(input));

    [Fact]
    public void A_name_has_at_most_40_characters()
    {
        Assert.True(ParticipantName.IsValid(new string('a', 40)));
        Assert.False(ParticipantName.IsValid(new string('a', 41)));
    }

    [Fact]
    public void Name_length_counts_code_points_like_postgres()
    {
        // 40 emoji are 80 UTF-16 units but 40 characters for char_length.
        Assert.True(ParticipantName.IsValid(string.Concat(Enumerable.Repeat("🎬", 40))));
    }

    [Fact]
    public void Names_compare_ignoring_case()
    {
        ParticipantName.TryCreate("Giulia", out var a);
        ParticipantName.TryCreate("GIULIA", out var b);

        Assert.True(a.SameAs(b));
        Assert.NotEqual(a, b); // still different values: re-casing is a change
    }

    [Fact]
    public void A_note_has_at_most_500_characters_and_may_be_empty()
    {
        Assert.True(LobbyNote.IsValid(""));
        Assert.True(LobbyNote.IsValid("Ci vediamo\nall'ingresso"));
        Assert.True(LobbyNote.IsValid(new string('a', 500)));
        Assert.False(LobbyNote.IsValid(new string('a', 501)));
        Assert.False(LobbyNote.IsValid(null));
        Assert.Equal("", default(LobbyNote).Value);
    }

    [Fact]
    public void A_device_is_the_stable_sha256_of_its_token()
    {
        var hash = DeviceHash.FromToken("token");

        Assert.Equal(32, hash.ToArray().Length);
        Assert.Equal(hash, DeviceHash.FromToken("token"));
        Assert.NotEqual(hash, DeviceHash.FromToken("other"));
        Assert.Equal(hash, DeviceHash.FromStored(hash.ToArray()));
        Assert.Equal(hash.GetHashCode(), DeviceHash.FromToken("token").GetHashCode());
    }
}
