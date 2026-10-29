using System.Diagnostics;
using Felinoir.Application.Common;
using Felinoir.Application.Common.Behaviors;
using Felinoir.Application.Common.Interfaces;
using Felinoir.Application.Common.Results;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;
using Moq;

namespace Felinoir.UnitTests.Common;

public class PipelineBehaviorTests
{
    public sealed record RenameCommand(string Name) : ICommand<Result<string>>;

    private sealed class RenameValidator : AbstractValidator<RenameCommand>
    {
        public RenameValidator()
        {
            RuleFor(c => c.Name).NotEmpty().WithErrorCode("invalid_name").WithMessage("A name is required.");
            RuleFor(c => c.Name).MaximumLength(5).WithErrorCode("name_too_long").WithMessage("Five characters at most.");
        }
    }

    private static MessageHandlerDelegate<RenameCommand, Result<string>> Handler(
        Func<RenameCommand, Result<string>> handle, Action? onCall = null) =>
        (message, _) =>
        {
            onCall?.Invoke();
            return new ValueTask<Result<string>>(handle(message));
        };

    // ── Validation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_valid_request_reaches_its_handler()
    {
        var behavior = new ValidationBehavior<RenameCommand, Result<string>>([new RenameValidator()]);

        var result = await behavior.Handle(new RenameCommand("Ale"), Handler(c => c.Name), default);

        Assert.Equal("Ale", result.Value);
    }

    [Fact]
    public async Task An_invalid_request_fails_with_the_first_error_code_and_skips_the_handler()
    {
        var called = false;
        var behavior = new ValidationBehavior<RenameCommand, Result<string>>([new RenameValidator()]);

        var result = await behavior.Handle(new RenameCommand(""), Handler(c => c.Name, () => called = true), default);

        Assert.False(called);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal("invalid_name", result.Error.Code);
        Assert.Equal("A name is required.", result.Error.Detail);
    }

    [Fact]
    public async Task Every_failure_of_every_validator_is_in_the_detail_once()
    {
        var second = new InlineValidator<RenameCommand>
        {
            v => v.RuleFor(c => c.Name).Must(n => n.Length < 3).WithErrorCode("too_long").WithMessage("Under three."),
        };
        var behavior = new ValidationBehavior<RenameCommand, Result<string>>([new RenameValidator(), second]);

        var result = await behavior.Handle(new RenameCommand("abcdefgh"), Handler(c => c.Name), default);

        Assert.Equal("name_too_long", result.Error!.Code);
        Assert.Equal("Five characters at most. Under three.", result.Error.Detail);
    }

    [Fact]
    public async Task A_request_without_validators_reaches_its_handler()
    {
        var behavior = new ValidationBehavior<RenameCommand, Result<string>>([]);

        var result = await behavior.Handle(new RenameCommand(""), Handler(_ => "handled"), default);

        Assert.Equal("handled", result.Value);
    }

    // ── Unit of work ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_successful_command_is_committed()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var behavior = new UnitOfWorkBehavior<RenameCommand, Result<string>>(unitOfWork.Object);

        await behavior.Handle(new RenameCommand("Ale"), Handler(c => c.Name), default);

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_failed_command_commits_nothing()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var behavior = new UnitOfWorkBehavior<RenameCommand, Result<string>>(unitOfWork.Object);

        var result = await behavior.Handle(new RenameCommand("Ale"), Handler(_ => Error.Conflict("name_taken")), default);

        Assert.False(result.IsSuccess);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_command_that_throws_commits_nothing()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        var behavior = new UnitOfWorkBehavior<RenameCommand, Result<string>>(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(new RenameCommand("Ale"), Handler(_ => throw new InvalidOperationException()), default));

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Logging ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logs_the_outcome_but_never_the_payload()
    {
        var logger = new ListLogger<LoggingBehavior<RenameCommand, Result<string>>>();
        var behavior = new LoggingBehavior<RenameCommand, Result<string>>(logger, TimeProvider.System);

        await behavior.Handle(new RenameCommand("secret-name"), Handler(c => c.Name), default);
        await behavior.Handle(new RenameCommand("secret-name"), Handler(_ => Error.Conflict("name_taken")), default);

        Assert.Collection(logger.Entries,
            e =>
            {
                Assert.Equal(LogLevel.Debug, e.Level);
                Assert.StartsWith("RenameCommand handled in", e.Message);
            },
            e =>
            {
                Assert.Equal(LogLevel.Information, e.Level);
                Assert.StartsWith("RenameCommand failed with name_taken in", e.Message);
            });
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("secret-name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Traces_each_request_as_a_span_named_after_it()
    {
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FelinoirTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Add,
        };
        ActivitySource.AddActivityListener(listener);
        var behavior = new LoggingBehavior<RenameCommand, Result<string>>(
            new ListLogger<LoggingBehavior<RenameCommand, Result<string>>>(), TimeProvider.System);

        await behavior.Handle(new RenameCommand("Ale"), Handler(_ => Error.Conflict("name_taken")), default);

        var span = Assert.Single(spans, s => s.OperationName == nameof(RenameCommand));
        Assert.Equal("name_taken", span.GetTagItem("felinoir.error"));
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
