using Kynakee.Modules.Projects.Domain.ValueObjects;

namespace Kynakee.Modules.Projects.Domain.Entities.Scoped
{
    public abstract record APUComponentDefinition(
    Guid SourceComponentId,
    string Description,
    MeasurementUnit Unit,    
    string? FallbackIndicator);

    public sealed record MaterialComponentDefinition(
        Guid SourceComponentId,
        string Description,
        MeasurementUnit Unit,        
        decimal WastePercentage,
        bool TransportIncluded,
        decimal QuantityPerApuUnit,
        string? FallbackIndicator = null)
        : APUComponentDefinition(
            SourceComponentId,
            Description,
            Unit,
            FallbackIndicator);

    public sealed record LaborComponentDefinition(
        Guid SourceComponentId,
        string Description,
        MeasurementUnit Unit,        
        string Trade,
        decimal CrewSize,
        decimal Productivity,
        string? FallbackIndicator = null)
        : APUComponentDefinition(
            SourceComponentId,
            Description,
            Unit,            
            FallbackIndicator);

    public sealed record EquipmentComponentDefinition(
        Guid SourceComponentId,
        string Description,
        MeasurementUnit Unit,        
        string EquipmentCategory,
        decimal EquipmentCount,
        decimal HoursPerApuUnit,
        string? FallbackIndicator = null)
        : APUComponentDefinition(
            SourceComponentId,
            Description,
            Unit,            
            FallbackIndicator);

    public sealed record TransportComponentDefinition(
        Guid SourceComponentId,
        string Description,
        MeasurementUnit Unit,        
        decimal DistanceKm,
        decimal VehicleCapacity,
        TransportRateBasis TransportRateBasis,
        decimal RoundTripFactor,
        decimal QuantityPerApuUnit,
        string? FallbackIndicator = null)
        : APUComponentDefinition(
            SourceComponentId,
            Description,
            Unit,           
            FallbackIndicator);

    public sealed record SubcontractComponentDefinition(
        Guid SourceComponentId,
        string Description,
        MeasurementUnit Unit,        
        string ContractConditions,
        decimal QuantityPerApuUnit,
        string? FallbackIndicator = null)
        : APUComponentDefinition(
            SourceComponentId,
            Description,
            Unit,            
            FallbackIndicator);

    public sealed record AuxiliaryMeansComponentDefinition(
        Guid SourceComponentId,
        string Description,
        MeasurementUnit Unit,
        decimal ApplicationPercentage,
        string? FallbackIndicator = null)
        : APUComponentDefinition(
            SourceComponentId,
            Description,
            Unit,           
            FallbackIndicator);
}
