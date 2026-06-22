using Felinoir.Infrastructure.Felix;

namespace Felinoir.UnitTests.Felix;

public class CinemasYamlConfigProviderTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cinemas-{Guid.NewGuid()}.yml");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Parses_nested_cinemas_key_kebab_flag_and_ignores_extra_fields()
    {
        File.WriteAllText(_path, """
            cinemas:
              - id: cinema-adriano
                name: Cinema Adriano
                address: Piazza Cavour, Roma
                website: https://example.com
                open-air: false
                coordinates:
                  lat: 41.9
                  lng: 12.4
              - id: arena-estiva
                name: Arena Estiva
                open-air: true
            """);

        var entries = new CinemasYamlConfigProvider(_path).GetAll();

        Assert.Equal(2, entries.Count);
        var adriano = entries.Single(e => e.Id == "cinema-adriano");
        Assert.Equal("Cinema Adriano", adriano.Name);
        Assert.False(adriano.OpenAir);
        Assert.True(entries.Single(e => e.Id == "arena-estiva").OpenAir);
    }

    [Fact]
    public void Returns_empty_when_file_missing()
    {
        var provider = new CinemasYamlConfigProvider(Path.Combine(Path.GetTempPath(), "does-not-exist.yml"));

        Assert.Empty(provider.GetAll());
    }

    [Fact]
    public void Skips_entries_without_an_id()
    {
        File.WriteAllText(_path, """
            cinemas:
              - name: Nameless
                open-air: false
              - id: valid
                name: Valid
            """);

        var entries = new CinemasYamlConfigProvider(_path).GetAll();

        Assert.Single(entries);
        Assert.Equal("valid", entries[0].Id);
    }
}
