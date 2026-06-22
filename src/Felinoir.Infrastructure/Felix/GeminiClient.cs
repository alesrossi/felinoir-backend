using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Felinoir.Application.Felix;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Felinoir.Infrastructure.Felix;

/// <summary>
/// Gemini REST client for Felix. Talks to <c>generativelanguage.googleapis.com/v1beta</c>
/// using <c>gemini-2.5-flash-lite</c>, and owns the system prompt and JSON payload shapes.
/// Higher-level caching/fallback logic lives in <c>FelixService</c>.
/// </summary>
public sealed class GeminiClient : IGeminiClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
    private const string Model = "gemini-2.5-flash-lite";

    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    private readonly HttpClient _http;
    private readonly ILogger<GeminiClient> _logger;
    private readonly string? _apiKey;

    public GeminiClient(HttpClient http, IConfiguration configuration, ILogger<GeminiClient> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = configuration["GEMINI_API_KEY"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<GeminiCacheRef?> CreateCacheAsync(string corpus, CancellationToken ct = default)
    {
        var body = new
        {
            model = $"models/{Model}",
            systemInstruction = new { parts = new[] { new { text = SystemPrompt() } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = corpus } } },
                new { role = "model", parts = new[] { new { text = "Capito, sono pronto ad aiutare." } } },
            },
            ttl = "86400s",
        };

        using var res = await PostAsync($"{BaseUrl}/cachedContents", body, ct);
        if (!res.IsSuccessStatusCode)
        {
            var error = await res.Content.ReadAsStringAsync(ct);
            _logger.LogError("[felix] cache creation failed: {Status} {Body}", (int)res.StatusCode, error);
            return null;
        }

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var name = doc.RootElement.GetProperty("name").GetString()!;
        var expireTime = doc.RootElement.GetProperty("expireTime").GetString()!;
        return new GeminiCacheRef(name, DateTimeOffset.Parse(expireTime, CultureInfo.InvariantCulture));
    }

    public async Task<string?> GenerateWithCacheAsync(
        IReadOnlyList<ChatMessage> messages, string cacheName, CancellationToken ct = default)
    {
        var body = new
        {
            cachedContent = cacheName,
            contents = ToGeminiContents(messages),
            generationConfig = GenerationConfig,
        };

        using var res = await PostAsync($"{BaseUrl}/models/{Model}:generateContent", body, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null; // cache expired
        await EnsureSuccessAsync(res, ct);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return ExtractText(doc);
    }

    public async Task<string> GenerateInlineAsync(
        IReadOnlyList<ChatMessage> messages, string corpus, CancellationToken ct = default)
    {
        var contents = new List<object>
        {
            new { role = "user", parts = new[] { new { text = corpus } } },
            new { role = "model", parts = new[] { new { text = "Capito, sono pronto." } } },
        };
        contents.AddRange(ToGeminiContents(messages));

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = SystemPrompt() } } },
            contents,
            generationConfig = GenerationConfig,
        };

        using var res = await PostAsync($"{BaseUrl}/models/{Model}:generateContent", body, ct);
        await EnsureSuccessAsync(res, ct);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return ExtractText(doc);
    }

    private static object GenerationConfig => new { maxOutputTokens = 700, temperature = 0.4 };

    private Task<HttpResponseMessage> PostAsync(string url, object body, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return _http.PostAsync($"{url}?key={ApiKey()}", content, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        var body = await res.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"Gemini generateContent failed: {(int)res.StatusCode} {body}");
    }

    private static object[] ToGeminiContents(IReadOnlyList<ChatMessage> messages) =>
        messages.Select(m => (object)new
        {
            role = m.Role == "assistant" ? "model" : "user",
            parts = new[] { new { text = m.Content } },
        }).ToArray();

    /// <summary>Pulls <c>candidates[0].content.parts[0].text</c>, returning "" if absent.</summary>
    private static string ExtractText(JsonDocument doc)
    {
        if (doc.RootElement.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array
            && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array
            && parts.GetArrayLength() > 0
            && parts[0].TryGetProperty("text", out var text))
        {
            return text.GetString() ?? "";
        }
        return "";
    }

    private string ApiKey() =>
        IsConfigured ? _apiKey! : throw new InvalidOperationException("GEMINI_API_KEY is not set");

    private static string SystemPrompt()
    {
        var today = DateTime.UtcNow.ToString("dddd d MMMM yyyy", Italian);
        return $"""
            You are Felix, the cinema assistant for felinoir.it, a website listing films showing in Rome, Italy.
            You have access to a complete, up-to-date list of films and their screenings in Rome.
            Always assume the user is in Rome and wants to watch a film in a Rome cinema, even if they don't say so explicitly. "I want to watch a movie about aliens" means "I want to watch a movie about aliens in Rome."
            Answer based on the provided film and screening data. You can also answer general cinema-related questions (about films, directors, genres, actors) even if they go slightly beyond the current listings — but always bring the answer back to what is available in Rome when relevant.
            Only refuse if the question has clearly nothing to do with cinema or films whatsoever.
            Reply in the same language the user writes in (Italian or English).
            When citing scores or ratings, prefer them in this order: IMDb first, then Rotten Tomatoes, then Metacritic. Only use TMDB if none of the others are available.
            Do not invent showtimes or screening data. If something is not in the data, say so clearly.
            Today is {today}.
            """;
    }
}
