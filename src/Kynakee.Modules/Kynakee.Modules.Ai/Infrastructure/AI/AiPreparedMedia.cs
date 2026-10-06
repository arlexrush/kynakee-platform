namespace Kynakee.Modules.Ai.Infrastructure.AI;

internal sealed record AiPreparedMedia(
    byte[] Content,
    string MimeType,
    string? FileName,
    string? Room,
    bool IsPathology);
