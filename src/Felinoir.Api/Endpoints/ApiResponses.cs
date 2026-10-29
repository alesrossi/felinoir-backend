namespace Felinoir.Api.Endpoints;

/// <summary>Standard error envelope returned by the API: <c>{ "error": "..." }</c>.</summary>
public sealed record ErrorResponse(string Error);

/// <summary>Felix chat reply: <c>{ "reply": "..." }</c>.</summary>
public sealed record FelixReply(string Reply);
