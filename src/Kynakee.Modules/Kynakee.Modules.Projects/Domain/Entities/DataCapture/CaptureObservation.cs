namespace Kynakee.Modules.Projects.Domain.Entities.DataCapture
{
    public sealed record CaptureObservation(
    string Description,
    string? Room,
    string? Severity);
}
