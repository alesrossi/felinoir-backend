using System.Security.Cryptography;
using System.Text;

namespace Felinoir.Domain.Lobbies;

/// <summary>
/// A device, as stored: the SHA-256 of the anonymous token the browser sends. The token itself is
/// never kept. Two hashes are equal when their bytes are.
/// </summary>
public sealed class DeviceHash : IEquatable<DeviceHash>
{
    private readonly byte[] _bytes;

    private DeviceHash(byte[] bytes) => _bytes = bytes;

    public static DeviceHash FromToken(string token) => new(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>A hash read back from the database.</summary>
    public static DeviceHash FromStored(byte[] bytes) => new(bytes.ToArray());

    public byte[] ToArray() => _bytes.ToArray();

    public bool Equals(DeviceHash? other) => other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    public override bool Equals(object? obj) => obj is DeviceHash other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_bytes);
        return hash.ToHashCode();
    }

    public static bool operator ==(DeviceHash? left, DeviceHash? right) => Equals(left, right);

    public static bool operator !=(DeviceHash? left, DeviceHash? right) => !Equals(left, right);
}
