using System.Diagnostics;
using System.Text.Json;
using Felinoir.Api.Observability;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Felinoir.UnitTests.Configuration;

public sealed class LogOutputsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "felinoir-logs-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => KeyValuePair.Create(v.Key, (string?)v.Value)))
            .Build();

    [Fact]
    public void With_a_file_path_every_event_is_an_ecs_json_line_with_its_trace_id()
    {
        var configuration = Configuration((LogOutputs.FilePathKey, Path.Combine(_directory, "felinoir-api-.json")));
        using var activity = new Activity("request").Start();

        using (var logger = new LoggerConfiguration().WriteToConfiguredOutputs(configuration).CreateLogger())
            logger.Information("Lobby {LobbyId} joined", "abc");

        var file = Assert.Single(Directory.GetFiles(_directory, "felinoir-api-*.json"));
        using var line = JsonDocument.Parse(Assert.Single(File.ReadAllLines(file)));
        var root = line.RootElement;
        Assert.Equal("Lobby \"abc\" joined", root.GetProperty("message").GetString());
        Assert.Equal("Information", root.GetProperty("log.level").GetString());
        Assert.Equal(activity.TraceId.ToString(), root.GetProperty("trace.id").GetString());
        Assert.True(root.TryGetProperty("@timestamp", out _));
        Assert.True(root.TryGetProperty("ecs.version", out _));
    }

    [Fact]
    public void Without_a_file_path_nothing_is_written_to_disk()
    {
        using (var logger = new LoggerConfiguration().WriteToConfiguredOutputs(Configuration()).CreateLogger())
            logger.Information("hello");

        Assert.False(Directory.Exists(_directory));
    }

    [Fact]
    public void Files_keep_at_most_a_month_and_five_gigabytes()
    {
        Assert.InRange(LogOutputs.FileSizeLimitBytes * LogOutputs.RetainedFileCountLimit, 4L << 30, 5L << 30);
        Assert.Equal(TimeSpan.FromDays(31), LogOutputs.RetainedFileTimeLimit);
    }
}
