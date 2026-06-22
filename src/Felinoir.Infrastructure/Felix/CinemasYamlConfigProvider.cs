using Felinoir.Application.Common.Interfaces;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Felinoir.Infrastructure.Felix;

/// <summary>
/// Reads static cinema metadata from <c>cinemas.yml</c>. The file groups entries under
/// a top-level <c>cinemas:</c> key and uses the kebab-case <c>open-air</c> flag; the
/// hyphenated naming convention maps both, and unmatched keys (coordinates, address…)
/// are ignored. Parsed once and cached for the process lifetime.
/// </summary>
public sealed class CinemasYamlConfigProvider : ICinemaConfigProvider
{
    private readonly Lazy<IReadOnlyList<CinemaConfigEntry>> _entries;

    public CinemasYamlConfigProvider(string filePath) =>
        _entries = new Lazy<IReadOnlyList<CinemaConfigEntry>>(() => Load(filePath));

    public IReadOnlyList<CinemaConfigEntry> GetAll() => _entries.Value;

    private static IReadOnlyList<CinemaConfigEntry> Load(string filePath)
    {
        // Missing config degrades gracefully: the corpus still builds, just without
        // open-air tags and DB-sourced names only.
        if (!File.Exists(filePath)) return [];

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var file = deserializer.Deserialize<CinemasFile>(File.ReadAllText(filePath));
        if (file?.Cinemas is null) return [];

        return file.Cinemas
            .Where(c => !string.IsNullOrEmpty(c.Id))
            .Select(c => new CinemaConfigEntry(c.Id!, c.Name ?? c.Id!, c.OpenAir))
            .ToList();
    }

    // YAML shapes — deserialised via the hyphenated naming convention.
    private sealed class CinemasFile
    {
        public List<CinemaYaml>? Cinemas { get; set; }
    }

    private sealed class CinemaYaml
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public bool OpenAir { get; set; }
    }
}
