using System.Text.Json.Serialization;

namespace Kynakee.Modules.AI.Contracts;

public sealed record CaptureAgentRequest(
    Guid ProjectId,
    Guid TenantId,
    IReadOnlyList<MediaFileRef> MediaFiles,
    IReadOnlyList<MeasurementRef> Measurements,
    IReadOnlyList<string> Transcriptions,
    IReadOnlyList<string>? TextInputs = null);

public sealed record MediaFileRef(
    Uri Url,
    string MimeType,
    string? Room,
    bool IsPathology,
    bool Processed,
    string? FileName = null);

public sealed record MeasurementRef(
    string Description,
    decimal Value,
    string Unit);

public sealed record CaptureAnalysisResult(
    IReadOnlyList<ExtractedWorkItem> WorkItems,
    IReadOnlyList<string> Observations,
    int TokensConsumed,
    double Confidence);

public sealed record ExtractedWorkItem(
    string CanonicalConceptId,
    string Description,
    string Unit,
    decimal Quantity,
    string Location,
    double Confidence,
    string AIStatus);

public sealed record ScopeAgentRequest(
    Guid ProjectId,
    Guid TenantId,
    IReadOnlyList<ExtractedWorkItem> CapturedWorkItems,
    IReadOnlyList<string> Observations,
    ProjectContextData? Context);

public sealed record ProjectContextData(
    string? Country,
    string? Region,
    string? Province,
    string? Municipality,
    string? Address,
    string? UrbanRegulation,
    string? ConstructionCode,
    string? CollectiveAgreement,
    decimal? SalaryOfficial1,
    decimal? SalaryLaborer,
    decimal? InflationRate,
    decimal? VatRate,
    decimal? ConstructionIndex);

public sealed record WorkItemRef(
    Guid Id,
    string CanonicalConceptId,
    string Description,
    string Unit,
    decimal Quantity,
    string? Location,
    string? Observations,
    double Confidence);

public sealed record ProductionAgentRequest(
    Guid ProjectId,
    Guid TenantId,
    IReadOnlyList<WorkItemRef> WorkItems,
    IReadOnlyList<APUTemplateRef> CandidateTemplates,
    ProjectContextData? Context);

public sealed record APUTemplateRef(
    Guid Id,
    string CanonicalConceptId,
    string OutputUnit,
    string? Description);

public sealed record APUStructureResult(
    IReadOnlyList<APUAssignmentStructure> Assignments,
    int TokensConsumed,
    double Confidence);

public sealed record APUAssignmentStructure(
    Guid WorkItemId,
    string OutputUnit,
    Guid? APUTemplateId,
    string Source,
    IReadOnlyList<APUComponentStructure> Components,
    double Confidence);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "componentType")]
[JsonDerivedType(typeof(MaterialAPUComponentStructure), "Material")]
[JsonDerivedType(typeof(LaborAPUComponentStructure), "Labor")]
[JsonDerivedType(typeof(EquipmentAPUComponentStructure), "Equipment")]
[JsonDerivedType(typeof(AuxiliaryMeansAPUComponentStructure), "AuxiliaryMeans")]
[JsonDerivedType(typeof(SubcontractAPUComponentStructure), "Subcontract")]
[JsonDerivedType(typeof(TransportAPUComponentStructure), "Transport")]
public abstract record APUComponentStructure(
    Guid? SourceComponentId,
    string Description,
    string Unit);

public sealed record MaterialAPUComponentStructure(
    Guid? SourceComponentId,
    string Description,
    string Unit,
    decimal WastePercentage,
    bool TransportIncluded,
    decimal QuantityPerApuUnit)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public sealed record LaborAPUComponentStructure(
    Guid? SourceComponentId,
    string Description,
    string Unit,
    string Trade,
    decimal CrewSize,
    decimal Productivity)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public sealed record EquipmentAPUComponentStructure(
    Guid? SourceComponentId,
    string Description,
    string Unit,
    string EquipmentCategory,
    decimal EquipmentCount,
    decimal HoursPerApuUnit)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public sealed record AuxiliaryMeansAPUComponentStructure(
    Guid? SourceComponentId,
    string Description,
    string Unit,
    decimal ApplicationPercentage)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public sealed record SubcontractAPUComponentStructure(
    Guid? SourceComponentId,
    string Description,
    string Unit,
    string ContractConditions,
    decimal QuantityPerApuUnit)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public sealed record TransportAPUComponentStructure(
    Guid? SourceComponentId,
    string Description,
    string Unit,
    decimal DistanceKm,
    decimal VehicleCapacity,
    string TransportRateBasis,
    decimal RoundTripFactor,
    decimal QuantityPerApuUnit)
    : APUComponentStructure(SourceComponentId, Description, Unit);

public sealed record PlanningAgentRequest(
    Guid ProjectId,
    Guid TenantId,
    IReadOnlyList<WorkItemRef> WorkItems,
    IReadOnlyList<APUAssignmentStructure> APUAssignments,
    ProjectContextData? Context,
    DateOnly? StartDate);

public sealed record ScheduleResult(
    IReadOnlyList<ScheduleActivityResult> Activities,
    IReadOnlyList<SchedulePrecedenceResult> Precedences,
    IReadOnlyList<Guid> CriticalPath,
    int TotalDurationDays,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<ScheduleMilestoneResult> Milestones,
    int TokensConsumed,
    double Confidence);

public sealed record ScheduleActivityResult(
    Guid Id,
    string Name,
    int DurationDays,
    IReadOnlyList<Guid> WorkItemIds);

public sealed record SchedulePrecedenceResult(
    Guid ActivityId,
    Guid PredecessorActivityId,
    string Type,
    int LagDays);

public sealed record ScheduleMilestoneResult(
    string Name,
    DateOnly Date);

public sealed record ValuationAgentRequest(
    Guid ProjectId,
    Guid TenantId,
    IReadOnlyList<WorkItemRef> WorkItems,
    IReadOnlyList<APUAssignmentStructure> APUAssignments,
    IReadOnlyList<APUComponentPriceRef> ComponentPrices,
    string Currency,
    ProjectContextData? Context);

public sealed record APUComponentPriceRef(
    Guid ComponentId,
    decimal UnitPrice,
    string Currency,
    string QuotedUnit,
    string PricingSource,
    string? ProviderName,
    bool IsFallback,
    double Confidence);

public sealed record ValuationResult(
    IReadOnlyList<ValuedComponentResult> ValuedComponents,
    IReadOnlyList<ValuedWorkItemResult> ValuedWorkItems,
    decimal DirectCost,
    decimal AuxiliaryCost,
    decimal IndirectCost,
    decimal Administration,
    decimal Quality,
    decimal SafetyHealth,
    decimal Environment,
    decimal Contingency,
    decimal Profit,
    decimal VAT,
    decimal TotalCost,
    string Currency,
    double Confidence,
    int TokensConsumed);

public sealed record ValuedComponentResult(
    Guid ComponentId,
    string ComponentType,
    string Description,
    string Unit,
    decimal QuotedUnitPrice,
    decimal ComponentSubtotal,
    string Currency,
    string PricingSource,
    string? ProviderName,
    bool IsFallback,
    double Confidence);

public sealed record ValuedWorkItemResult(
    Guid WorkItemId,
    Guid APUAssignmentId,
    decimal Quantity,
    decimal DirectUnitCost,
    decimal AuxiliaryUnitCost,
    decimal TotalUnitPrice,
    decimal TotalAmount,
    string Currency);

public sealed record OfferAgentRequest(
    Guid ProjectId,
    Guid TenantId,
    string ProjectName,
    ValuationResult Valuation,
    string? ScheduleSummary,
    string? Conditions,
    string? Warranties,
    int ValidityDays,
    string Language);

public sealed record OfferNarrativeResult(
    string Title,
    string ExecutiveSummary,
    string ScopeDescription,
    string? Conditions,
    string? Warranties,
    int ValidityDays,
    string AIActDisclaimer,
    int TokensConsumed,
    double Confidence);

public sealed record ConversationAgentRequest(
    Guid TenantId,
    string Channel,
    string UserMessage,
    string ConversationHistory,
    string? ActiveProjectPhase,
    string? ActiveProjectName);

public sealed record ConversationResponse(
    string Message,
    string? SuggestedAction,
    bool RequiresHumanInput,
    int TokensConsumed);
