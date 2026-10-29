using Felinoir.Application.Features.Felix;

namespace Felinoir.UnitTests.Felix;

public class FelixFilterFormatterTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    [Fact]
    public void Returns_null_when_filters_absent()
    {
        Assert.Null(FelixFilterFormatter.Format(null, Today));
    }

    [Fact]
    public void Returns_null_when_all_filters_empty()
    {
        var filters = new FelixFilters(Cinemas: [], Genres: [], Ov: false, Q: "   ");
        Assert.Null(FelixFilterFormatter.Format(filters, Today));
    }

    [Fact]
    public void Wraps_active_filters_with_header_and_footer()
    {
        var result = FelixFilterFormatter.Format(new FelixFilters(
            Cinemas: ["cinema-farnese"],
            Genres: ["Horror"]), Today);

        Assert.NotNull(result);
        Assert.StartsWith("[Note: I currently have these filters active on felinoir.it]", result);
        Assert.Contains("- Cinemas: cinema-farnese", result);
        Assert.Contains("- Genres: Horror", result);
        Assert.EndsWith("Please only include films and screenings that match ALL of these filters.", result);
    }

    [Fact]
    public void Includes_ov_line_only_when_true()
    {
        Assert.Contains("Original version", FelixFilterFormatter.Format(new FelixFilters(Ov: true), Today)!);
        Assert.Null(FelixFilterFormatter.Format(new FelixFilters(Ov: false), Today));
    }

    [Fact]
    public void Includes_search_query()
    {
        Assert.Contains("- Search: vampiri", FelixFilterFormatter.Format(new FelixFilters(Q: "vampiri"), Today)!);
    }

    [Fact]
    public void Labels_the_given_date_as_today()
    {
        var result = FelixFilterFormatter.Format(new FelixFilters(Dates: ["2026-10-30", "2020-01-01"]), Today)!;

        Assert.Contains("today (2026-10-30)", result);
        Assert.Contains("- Dates: today (2026-10-30), 2020-01-01", result);
    }
}
