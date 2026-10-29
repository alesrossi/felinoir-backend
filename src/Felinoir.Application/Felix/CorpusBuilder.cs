namespace Felinoir.Application.Felix;

/// <summary>Loads a city's <see cref="CorpusSnapshot"/> and renders it.</summary>
public sealed class CorpusBuilder(CorpusSnapshotLoader loader) : ICorpusBuilder
{
    public async Task<string> BuildAsync(string cityId, CancellationToken ct = default) =>
        CorpusRenderer.Render(await loader.LoadAsync(cityId, ct));
}
