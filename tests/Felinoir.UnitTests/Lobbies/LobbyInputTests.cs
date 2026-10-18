using Felinoir.Application.Lobbies;

namespace Felinoir.UnitTests.Lobbies;

public class LobbyInputTests
{
    [Theory]
    [InlineData("Giulia", "Giulia")]
    [InlineData("  Marco  ", "Marco")]
    [InlineData("Anna Maria", "Anna Maria")]
    [InlineData("Zoë 🎬", "Zoë 🎬")]
    public void NormalizeName_trims_valid_names(string input, string expected) =>
        Assert.Equal(expected, LobbyInput.NormalizeName(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Gi\nulia")]
    [InlineData("Gi\tulia")]
    [InlineData("Giulia\u0007")]
    public void NormalizeName_rejects_empty_or_control_characters(string? input) =>
        Assert.Null(LobbyInput.NormalizeName(input));

    [Fact]
    public void NormalizeName_allows_40_characters_but_not_41()
    {
        Assert.NotNull(LobbyInput.NormalizeName(new string('a', 40)));
        Assert.Null(LobbyInput.NormalizeName(new string('a', 41)));
    }

    [Fact]
    public void NormalizeName_counts_code_points_like_postgres()
    {
        // 40 emoji are 80 UTF-16 units but 40 characters for char_length.
        var emoji = string.Concat(Enumerable.Repeat("🎬", 40));

        Assert.Equal(emoji, LobbyInput.NormalizeName(emoji));
    }

    [Fact]
    public void IsValidNote_allows_up_to_500_characters_including_newlines()
    {
        Assert.True(LobbyInput.IsValidNote(""));
        Assert.True(LobbyInput.IsValidNote("Ci vediamo\nall'ingresso"));
        Assert.True(LobbyInput.IsValidNote(new string('a', 500)));
        Assert.False(LobbyInput.IsValidNote(new string('a', 501)));
    }

    [Fact]
    public void HashDeviceToken_is_a_stable_sha256()
    {
        var hash = LobbyInput.HashDeviceToken("token");

        Assert.Equal(32, hash.Length);
        Assert.Equal(hash, LobbyInput.HashDeviceToken("token"));
        Assert.NotEqual(hash, LobbyInput.HashDeviceToken("other"));
    }
}
