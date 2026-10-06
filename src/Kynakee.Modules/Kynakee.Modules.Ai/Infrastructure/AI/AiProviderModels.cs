namespace Kynakee.Modules.Ai.Infrastructure.AI;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Provider request messages are constructed by the AI orchestration service.")]
internal sealed record AiChatMessage(string Role, string Content);

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Provider requests are constructed by the AI orchestration service.")]
internal sealed record AiChatRequest(
    IReadOnlyList<AiChatMessage> Messages,
    bool RequireJson,
    int? MaxOutputTokens = null,
    IReadOnlyList<AiChatMedia>? Media = null);

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Provider media references are constructed by the AI orchestration service.")]
internal sealed record AiChatMedia(
    byte[]? Content,
    string MimeType,
    string? FileName = null,
    string? ProviderFileUri = null,
    string? InlineData = null);

internal sealed record AiChatCompletion(
    string Content,
    int InputTokens,
    int OutputTokens);

internal sealed record AiEmbedding(float[] Vector, int InputTokens);
