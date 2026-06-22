namespace Felinoir.Application.Felix;

/// <summary>A single turn in a Felix conversation. <see cref="Role"/> is "user" or "assistant".</summary>
public sealed record ChatMessage(string Role, string Content);
