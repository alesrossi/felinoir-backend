using Felinoir.Application.Common.Results;

namespace Felinoir.UnitTests.Common;

public class ResultTests
{
    private static readonly Error Taken = Error.Conflict("name_taken");

    [Fact]
    public void A_value_converts_to_a_successful_result()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void An_error_converts_to_a_failed_result()
    {
        Result<int> result = Taken;

        Assert.False(result.IsSuccess);
        Assert.Same(Taken, result.Error);
    }

    [Fact]
    public void A_failed_result_has_no_value()
    {
        Result<int> result = Taken;

        var error = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("name_taken", error.Message);
    }

    [Fact]
    public void Match_takes_the_branch_of_the_outcome()
    {
        Assert.Equal("ok 1", Result.Ok(1).Match(v => $"ok {v}", e => e.Code));
        Assert.Equal("name_taken", Result.Fail<int>(Taken).Match(v => $"ok {v}", e => e.Code));
    }

    [Fact]
    public void A_result_without_a_value_succeeds_or_carries_an_error()
    {
        Result failed = Taken;

        Assert.True(Result.Success.IsSuccess);
        Assert.False(failed.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, failed.Error.Kind);
    }

    [Fact]
    public void Error_factories_set_the_kind()
    {
        Assert.Equal(ErrorKind.Validation, Error.Validation("x").Kind);
        Assert.Equal(ErrorKind.NotFound, Error.NotFound("x").Kind);
        Assert.Equal(ErrorKind.Conflict, Error.Conflict("x").Kind);
        Assert.Equal(ErrorKind.Unprocessable, Error.Unprocessable("x").Kind);
        Assert.Equal(ErrorKind.Unavailable, Error.Unavailable("x", "detail").Kind);
    }
}
