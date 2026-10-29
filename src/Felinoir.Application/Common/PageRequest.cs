namespace Felinoir.Application.Common;

/// <summary>
/// A page of a list: at most <see cref="Limit"/> items after skipping <see cref="Offset"/>. Only
/// <see cref="Create"/> makes one, and it clamps (it never rejects): a limit below 0 becomes 0, one
/// above the list's maximum becomes the maximum, a negative offset becomes 0. So a query can
/// never ask for an unbounded page, whoever sends it.
/// </summary>
public readonly record struct PageRequest
{
    private PageRequest(int limit, int offset)
    {
        Limit = limit;
        Offset = offset;
    }

    public int Limit { get; }
    public int Offset { get; }

    public static PageRequest Create(int? limit, int? offset, int defaultLimit, int maxLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLimit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(defaultLimit, maxLimit);

        return new(Math.Clamp(limit ?? defaultLimit, 0, maxLimit), Math.Max(offset ?? 0, 0));
    }
}
