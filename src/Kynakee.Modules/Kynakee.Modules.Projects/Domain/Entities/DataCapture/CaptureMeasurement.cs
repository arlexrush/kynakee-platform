using Kynakee.Modules.Projects.Domain.ValueObjects;

namespace Kynakee.Modules.Projects.Domain.Entities.DataCapture
{
    public sealed record CaptureMeasurement(
    string Description,
    decimal Value,
    MeasurementUnit Unit);
}
