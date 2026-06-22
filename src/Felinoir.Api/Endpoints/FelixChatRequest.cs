using Felinoir.Application.Felix;

namespace Felinoir.Api.Endpoints;

/// <summary>Wire shape of a <c>POST /felix/chat</c> body. <see cref="Filters"/> is optional.</summary>
public sealed class FelixChatRequest
{
    public List<FelixChatMessage>? Messages { get; set; }
    public FelixFilters? Filters { get; set; }
}

/// <summary>A single message from the request body. <see cref="Role"/> must be "user" or "assistant".</summary>
public sealed class FelixChatMessage
{
    public string? Role { get; set; }
    public string? Content { get; set; }
}
