using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Felinoir.IntegrationTests.Contract;

/// <summary>
/// Sends requests and writes each exchange (request line, status, contract headers, body) into
/// one text document, which a test then verifies as a snapshot. GUIDs are replaced by
/// <c>Guid_1</c>, <c>Guid_2</c>… in order of first appearance, so the same id keeps the same
/// name across a scenario. JSON bodies are re-indented; their property order and values are
/// kept, so any change to the wire shape shows up as a diff.
/// </summary>
internal sealed partial class ContractRecorder(HttpClient client)
{
    private static readonly string[] ContractHeaders = ["Content-Type", "Location"];

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly StringBuilder _text = new();

    public HttpClient Client => client;

    /// <summary>Sends the request, records the exchange and returns the parsed JSON body, if any.</summary>
    public async Task<JsonElement?> SendAsync(
        HttpMethod method, string url, object? body = null, string? deviceToken = null, string? label = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent(body);
        if (deviceToken is not null) request.Headers.Add("X-Device-Token", deviceToken);

        using var response = await client.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        _text.Append("### ").Append(method).Append(' ').Append(url);
        if (label is not null) _text.Append("   (").Append(label).Append(')');
        _text.AppendLine();
        if (body is not null) _text.AppendLine("> " + (body as string ?? JsonSerializer.Serialize(body, Compact)));
        _text.Append((int)response.StatusCode).Append(' ').AppendLine(response.StatusCode.ToString());

        foreach (var name in ContractHeaders)
        {
            if (response.Headers.TryGetValues(name, out var values)
                || response.Content.Headers.TryGetValues(name, out values))
                _text.Append(name).Append(": ").AppendLine(string.Join(", ", values));
        }

        JsonElement? json = null;
        if (raw.Length > 0)
        {
            try
            {
                json = JsonDocument.Parse(raw).RootElement.Clone();
                _text.AppendLine(JsonSerializer.Serialize(json, Indented));
            }
            catch (JsonException)
            {
                _text.AppendLine(raw);
            }
        }

        _text.AppendLine();
        return json;
    }

    public Task<JsonElement?> GetAsync(string url, string? deviceToken = null, string? label = null) =>
        SendAsync(HttpMethod.Get, url, null, deviceToken, label);

    /// <summary>Adds a free-form section, e.g. what a fake collaborator received.</summary>
    public void Note(string heading, string content)
    {
        _text.Append("--- ").AppendLine(heading);
        _text.AppendLine(content.TrimEnd());
        _text.AppendLine();
    }

    /// <summary>The recorded document with GUIDs replaced by stable placeholders.</summary>
    public string Render()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return GuidPattern().Replace(_text.ToString(), m =>
        {
            if (!names.TryGetValue(m.Value, out var name))
                names[m.Value] = name = $"Guid_{names.Count + 1}";
            return name;
        });
    }

    private static StringContent JsonContent(object body) =>
        body is string s
            ? new StringContent(s, Encoding.UTF8, "application/json")
            : new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    [GeneratedRegex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase)]
    private static partial Regex GuidPattern();
}
