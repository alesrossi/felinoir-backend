namespace Felinoir.Api.Endpoints;

/// <summary>Standard error envelope returned by the API: <c>{ "error": "..." }</c>.</summary>
public sealed record ErrorResponse(string Error);

/// <summary>Health probe payload: <c>{ "ok": true }</c>.</summary>
public sealed record HealthResponse(bool Ok);

/// <summary>Felix chat reply: <c>{ "reply": "..." }</c>.</summary>
public sealed record FelixReply(string Reply);
