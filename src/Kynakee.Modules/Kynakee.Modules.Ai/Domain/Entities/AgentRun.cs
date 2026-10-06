using Kynakee.Modules.Ai.Domain.Enums;
using Kynakee.Modules.Ai.Domain.Resources;
using Kynakee.Modules.Ai.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Ai.Domain.Entities;

public sealed class AgentRun : BaseEntity<AgentRunId>
{
    private AgentRun()
    {
    }

    private AgentRun(
        AgentRunId id,
        Guid tenantId,
        Guid? projectId,
        AgentType agentType,
        string modelId,
        string provider,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        ProjectId = projectId;
        AgentType = agentType;
        ModelId = modelId;
        Provider = provider;
        Status = AgentRunStatus.Queued;
    }

    public Guid? ProjectId { get; private set; }

    public AgentType AgentType { get; private set; }

    public string ModelId { get; private set; } = string.Empty;

    public string Provider { get; private set; } = string.Empty;

    public int InputTokens { get; private set; }

    public int OutputTokens { get; private set; }

    public decimal CreditsCharged { get; private set; }

    public bool FallbackActivated { get; private set; }

    public string? FallbackReason { get; private set; }

    public AgentRunStatus Status { get; private set; }

    public TimeSpan Duration { get; private set; }

    public bool HumanReviewed { get; private set; }

    public DateTime? HumanReviewedAt { get; private set; }

    public Guid? HumanReviewerId { get; private set; }

    /// <summary>Creates a queued audit record before invoking an AI model.</summary>
    public static Result<AgentRun> Create(
        Guid tenantId,
        Guid? projectId,
        AgentType agentType,
        string modelId,
        string provider,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty)
        {
            return CreateFailure("AI_001", "TenantRequired");
        }

        if (projectId == Guid.Empty)
        {
            return CreateFailure("AI_002", "ProjectIdInvalid");
        }

        if (!Enum.IsDefined(agentType))
        {
            return CreateFailure("AI_003", "AgentTypeInvalid");
        }

        if (string.IsNullOrWhiteSpace(modelId) || modelId.Trim().Length > 50)
        {
            return CreateFailure("AI_004", "ModelIdInvalid");
        }

        if (string.IsNullOrWhiteSpace(provider) || provider.Trim().Length > 30)
        {
            return CreateFailure("AI_005", "ProviderInvalid");
        }

        if (createdBy == Guid.Empty)
        {
            return CreateFailure("AI_006", "CreatedByInvalid");
        }

        return ResultFactory.Success(
            new AgentRun(
                AgentRunId.New(),
                tenantId,
                projectId,
                agentType,
                modelId.Trim(),
                provider.Trim(),
                createdBy));
    }

    /// <summary>Marks a queued run as successful and stores the final model usage.</summary>
    public Result Complete(
        string modelId,
        string provider,
        int inputTokens,
        int outputTokens,
        decimal creditsCharged,
        TimeSpan duration,
        bool fallbackActivated,
        string? fallbackReason,
        Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(provider);

        if (!IsQueued())
        {
            return Conflict("AI_007", "RunNotQueued");
        }

        var validation = ValidateCompletion(
            modelId,
            provider,
            inputTokens,
            outputTokens,
            creditsCharged,
            duration,
            fallbackActivated,
            fallbackReason,
            updatedBy);

        if (validation is not null)
        {
            return validation;
        }

        ModelId = modelId.Trim();
        Provider = provider.Trim();
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CreditsCharged = creditsCharged;
        Duration = duration;
        FallbackActivated = fallbackActivated;
        FallbackReason = fallbackActivated ? fallbackReason?.Trim() : null;
        Status = fallbackActivated
            ? AgentRunStatus.FallbackUsed
            : AgentRunStatus.Success;
        RegisterUpdate(updatedBy);

        return ResultFactory.Ok();
    }

    /// <summary>Marks a queued run as failed; failed runs do not consume credits.</summary>
    public Result Fail(
        string modelId,
        string provider,
        int inputTokens,
        int outputTokens,
        TimeSpan duration,
        string? fallbackReason,
        bool fallbackActivated,
        Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(modelId);
        ArgumentNullException.ThrowIfNull(provider);

        if (!IsQueued())
        {
            return Conflict("AI_007", "RunNotQueued");
        }

        if (!HasValidModel(modelId, provider))
        {
            return Failure("AI_019", "ModelIdInvalid");
        }

        if (inputTokens < 0 ||
            outputTokens < 0 ||
            duration < TimeSpan.Zero)
        {
            return Failure("AI_008", "UsageInvalid");
        }

        if (fallbackActivated && string.IsNullOrWhiteSpace(fallbackReason))
        {
            return Failure("AI_010", "FallbackReasonRequired");
        }

        if (!fallbackActivated && !string.IsNullOrWhiteSpace(fallbackReason))
        {
            return Failure("AI_016", "FallbackReasonUnexpected");
        }

        if (!string.IsNullOrWhiteSpace(fallbackReason) &&
            fallbackReason.Trim().Length > 200)
        {
            return Failure("AI_017", "FallbackReasonTooLong");
        }

        if (updatedBy == Guid.Empty)
        {
            return Failure("AI_011", "UpdatedByInvalid");
        }

        ModelId = modelId.Trim();
        Provider = provider.Trim();
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CreditsCharged = 0;
        Duration = duration;
        FallbackActivated = fallbackActivated;
        FallbackReason = fallbackActivated ? fallbackReason?.Trim() : null;
        Status = AgentRunStatus.Failed;
        RegisterUpdate(updatedBy);

        return ResultFactory.Ok();
    }

    /// <summary>Records human review for a successful AI result.</summary>
    public Result MarkHumanReviewed(
        Guid reviewerId,
        DateTime? reviewedAt = null)
    {
        if (Status is not AgentRunStatus.Success and
            not AgentRunStatus.FallbackUsed)
        {
            return Conflict("AI_012", "RunNotReviewable");
        }

        if (HumanReviewed)
        {
            return Conflict("AI_013", "AlreadyReviewed");
        }

        if (reviewerId == Guid.Empty)
        {
            return Failure("AI_014", "ReviewerRequired");
        }

        var reviewTimestamp = reviewedAt ?? DateTime.UtcNow;
        if (reviewTimestamp.Kind != DateTimeKind.Utc)
        {
            return Failure("AI_015", "ReviewTimestampInvalid");
        }

        HumanReviewed = true;
        HumanReviewedAt = reviewTimestamp;
        HumanReviewerId = reviewerId;
        RegisterUpdate(reviewerId);

        return ResultFactory.Ok();
    }

    private bool IsQueued() => Status == AgentRunStatus.Queued;

    private static Result? ValidateCompletion(
        string modelId,
        string provider,
        int inputTokens,
        int outputTokens,
        decimal creditsCharged,
        TimeSpan duration,
        bool fallbackActivated,
        string? fallbackReason,
        Guid? updatedBy)
    {
        if (!HasValidModel(modelId, provider))
        {
            return Failure("AI_019", "ModelIdInvalid");
        }

        if (inputTokens < 0 ||
            outputTokens < 0 ||
            creditsCharged < 0 ||
            duration < TimeSpan.Zero)
        {
            return Failure("AI_008", "UsageInvalid");
        }

        if (fallbackActivated && string.IsNullOrWhiteSpace(fallbackReason))
        {
            return Failure("AI_010", "FallbackReasonRequired");
        }

        if (!fallbackActivated && !string.IsNullOrWhiteSpace(fallbackReason))
        {
            return Failure("AI_016", "FallbackReasonUnexpected");
        }

        if (!string.IsNullOrWhiteSpace(fallbackReason) &&
            fallbackReason.Trim().Length > 200)
        {
            return Failure("AI_017", "FallbackReasonTooLong");
        }

        return updatedBy == Guid.Empty
            ? Failure("AI_018", "UpdatedByInvalid")
            : null;
    }

    private static bool HasValidModel(string modelId, string provider) =>
        !string.IsNullOrWhiteSpace(modelId) &&
        modelId.Trim().Length <= 50 &&
        !string.IsNullOrWhiteSpace(provider) &&
        provider.Trim().Length <= 30;

    private static Result<AgentRun> CreateFailure(string code, string messageKey) =>
        ResultFactory.Failure<AgentRun>(
            ApplicationError.Validation(code, AiMessages.Get(messageKey)));

    private static Result Failure(string code, string messageKey) =>
        ResultFactory.Failure(
            ApplicationError.Validation(code, AiMessages.Get(messageKey)));

    private static Result Conflict(string code, string messageKey) =>
        ResultFactory.Failure(
            ApplicationError.Conflict(code, AiMessages.Get(messageKey)));
}
