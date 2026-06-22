using System.Globalization;
using Felinoir.Application.Felix;

namespace Felinoir.UnitTests.Felix;

public class FelixFilterFormatterTests
{
    [Fact]
    public void Returns_null_when_filters_absent()
    {
        Assert.Null(FelixFilterFormatter.Format(null));
    }

    [Fact]
    public void Returns_null_when_all_filters_empty()
    {
        var filters = new FelixFilters(Cinemas: [], Genres: [], Ov: false, Q: "   ");
        Assert.Null(FelixFilterFormatter.Format(filters));
    }

    [Fact]
    public void Wraps_active_filters_with_header_and_footer()
    {
        var result = FelixFilterFormatter.Format(new FelixFilters(
            Cinemas: ["cinema-farnese"],
            Genres: ["Horror"]));

        Assert.NotNull(result);
        Assert.StartsWith("[Note: I currently have these filters active on felinoir.it]", result);
        Assert.Contains("- Cinemas: cinema-farnese", result);
        Assert.Contains("- Genres: Horror", result);
        Assert.EndsWith("Please only include films and screenings that match ALL of these filters.", result);
    }

    [Fact]
    public void Includes_ov_line_only_when_true()
    {
        Assert.Contains("Original version", FelixFilterFormatter.Format(new FelixFilters(Ov: true))!);
        Assert.Null(FelixFilterFormatter.Format(new FelixFilters(Ov: false)));
    }

    [Fact]
    public void Includes_search_query()
    {
        Assert.Contains("- Search: vampiri", FelixFilterFormatter.Format(new FelixFilters(Q: "vampiri"))!);
    }

    [Fact]
    public void Labels_today_relative_to_utc()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var result = FelixFilterFormatter.Format(new FelixFilters(Dates: [today, "2020-01-01"]))!;

        Assert.Contains($"today ({today})", result);
        Assert.Contains("2020-01-01", result);
    }
}
