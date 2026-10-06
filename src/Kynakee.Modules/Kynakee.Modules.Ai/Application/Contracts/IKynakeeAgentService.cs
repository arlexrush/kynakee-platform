using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.AI.Contracts;

/// <summary>
/// Public contract for AI operations. Other modules must use this interface instead of calling providers directly.
/// </summary>
public interface IKynakeeAgentService
{
    /// <summary>Analyzes capture media and extracts observations and candidate work items.</summary>
    Task<Result<CaptureAnalysisResult>> RunCaptureAgentAsync(
        CaptureAgentRequest request,
        CancellationToken cancellationToken);

    /// <summary>Maps captured evidence to structured work items.</summary>
    Task<Result<IReadOnlyList<ExtractedWorkItem>>> RunScopeAgentAsync(
        ScopeAgentRequest request,
        CancellationToken cancellationToken);

    /// <summary>Generates APU structures for the requested work items.</summary>
    Task<Result<APUStructureResult>> RunProductionAgentAsync(
        ProductionAgentRequest request,
        CancellationToken cancellationToken);

    /// <summary>Creates a schedule from work items and their APU structures.</summary>
    Task<Result<ScheduleResult>> RunPlanningAgentAsync(
        PlanningAgentRequest request,
        CancellationToken cancellationToken);

    /// <summary>Synthesizes valuation data from APU structures and supplied component prices.</summary>
    Task<Result<ValuationResult>> RunValuationAgentAsync(
        ValuationAgentRequest request,
        CancellationToken cancellationToken);

    /// <summary>Generates a commercial narrative from the supplied project and valuation data.</summary>
    Task<Result<OfferNarrativeResult>> RunOfferAgentAsync(
        OfferAgentRequest request,
        CancellationToken cancellationToken);

    /// <summary>Generates a response for a bot conversation.</summary>
    Task<Result<ConversationResponse>> RunConversationAgentAsync(
        ConversationAgentRequest request,
        CancellationToken cancellationToken);

    /// <summary>Generates a vector embedding for the supplied text.</summary>
    Task<Result<float[]>> GenerateEmbeddingAsync(
        Guid tenantId,
        string text,
        CancellationToken cancellationToken);
}
