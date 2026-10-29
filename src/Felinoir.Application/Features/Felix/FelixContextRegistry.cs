using System.Collections.Concurrent;

namespace Felinoir.Application.Features.Felix;

/// <summary>
/// The process-wide <see cref="FelixContext"/> of each city, with a lock per city so concurrent
/// questions rebuild a city's corpus at most once at a time, and one city rebuilding never holds
/// up another. A singleton; it holds no scoped services.
/// </summary>
public sealed class FelixContextRegistry
{
    private readonly ConcurrentDictionary<string, CityState> _cities = new();

    public CityState For(string cityId) => _cities.GetOrAdd(cityId, _ => new CityState());

    public sealed class CityState
    {
        private FelixContext? _current;

        /// <summary>Serialises rebuilding this city's context.</summary>
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public FelixContext? Current
        {
            get => Volatile.Read(ref _current);
            set => Volatile.Write(ref _current, value);
        }
    }
}
