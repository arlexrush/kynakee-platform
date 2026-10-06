namespace Kynakee.Modules.Projects.Domain.Entities.DataCapture
{
    public sealed record CaptureMediaFile(
    Uri Url,
    string Type,
    string? Room,
    bool IsPathology,
    bool Processed);
}
