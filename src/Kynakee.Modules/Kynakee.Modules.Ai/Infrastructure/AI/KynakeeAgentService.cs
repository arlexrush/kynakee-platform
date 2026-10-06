using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Text.Json;
using Kynakee.Modules.AI.Contracts;
using Kynakee.Modules.Ai.Domain.Enums;
using Kynakee.Modules.Ai.Domain.Entities;
using Kynakee.Modules.Ai.Domain.Resources;
using Kynakee.Modules.Ai.Domain.Services;
using Kynakee.Modules.Ai.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Ai.Infrastructure.AI;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Registered as the AI module agent service.")]
internal sealed class KynakeeAgentService : IKynakeeAgentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AiModelRegistry _modelRegistry;
    private readonly IAiProviderClient _providerClient;
    private readonly AiMediaContentResolver _mediaContentResolver;
    private readonly AiDbContext _dbContext;

    public KynakeeAgentService(
        AiModelRegistry modelRegistry,
        IAiProviderClient providerClient,
        AiMediaContentResolver mediaContentResolver,
        AiDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(modelRegistry);
        ArgumentNullException.ThrowIfNull(providerClient);
        ArgumentNullException.ThrowIfNull(mediaContentResolver);
        ArgumentNullException.ThrowIfNull(dbContext);
        _modelRegistry = modelRegistry;
        _providerClient = providerClient;
        _mediaContentResolver = mediaContentResolver;
        _dbContext = dbContext;
    }

    public async Task<Result<CaptureAnalysisResult>> RunCaptureAgentAsync(
        CaptureAgentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty || request.ProjectId == Guid.Empty ||
            request.MediaFiles is null || request.Measurements is null || request.Transcriptions is null ||
            (request.MediaFiles.Count == 0 && request.Measurements.Count == 0 &&
             request.Transcriptions.Count == 0 && request.TextInputs is not { Count: > 0 }))
        {
            return Failure<CaptureAnalysisResult>("AI_056", "CaptureRequestInvalid");
        }

        IReadOnlyList<AiPreparedMedia> preparedMedia = [];
        if (request.MediaFiles.Count > 0)
        {
            var resolution = await _mediaContentResolver.ResolveAsync(
                    request.TenantId,
                    request.ProjectId,
                    request.MediaFiles,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!resolution.IsSuccess)
            {
                return ResultFactory.Failure<CaptureAnalysisResult>(resolution.Error!);
            }

            preparedMedia = resolution.Value!;
        }

        var mimeTypes = request.MediaFiles.Select(file => file.MimeType).ToArray();
        var modelResult = _modelRegistry.ResolvePrimary(AgentType.Capture, mimeTypes);
        if (!modelResult.IsSuccess)
        {
            return ResultFactory.Failure<CaptureAnalysisResult>(modelResult.Error!);
        }

        var model = modelResult.Value!;
        IReadOnlyList<AiChatMedia> providerMedia = [];
        if (preparedMedia.Count > 0 && model.Protocol == AiProviderProtocol.OpenAiCompatible)
        {
            if (preparedMedia.Any(media =>
                    AiMediaTypeClassifier.Classify(media.MimeType) != AiMediaModality.Image))
            {
                return Failure<CaptureAnalysisResult>("AI_057", "CaptureMediaTransportPending");
            }

            providerMedia = preparedMedia.Select(media => new AiChatMedia(
                null,
                media.MimeType,
                media.FileName,
                InlineData: $"data:{media.MimeType};base64,{Convert.ToBase64String(media.Content)}")).ToArray();
        }
        else if (preparedMedia.Count > 0)
        {
            var uploadedMedia = new List<AiChatMedia>(preparedMedia.Count);
            foreach (var media in preparedMedia)
            {
                var uploadResult = await _providerClient.UploadGeminiFileAsync(
                        model,
                        media,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!uploadResult.IsSuccess)
                {
                    return ResultFactory.Failure<CaptureAnalysisResult>(uploadResult.Error!);
                }

                uploadedMedia.Add(new AiChatMedia(
                    null,
                    media.MimeType,
                    media.FileName,
                    ProviderFileUri: uploadResult.Value!.AbsoluteUri));
            }

            providerMedia = uploadedMedia;
        }

        var prompt = BuildCaptureInput(request);
        var chatRequest = new AiChatRequest(
                    [
                        new AiChatMessage("system", "Analyze the supplied construction capture data and return a JSON object conforming to the CaptureAnalysisResult contract. Do not invent data that is not present in the input."),
                        new AiChatMessage("user", prompt)
                    ],
                    RequireJson: true,
                    Media: providerMedia);
        return await CompleteChatAuditedAsync(
                AgentType.Capture,
                request.TenantId,
                request.ProjectId,
                model,
                chatRequest,
                (content, inputTokens, outputTokens) =>
                {
                    try
                    {
                        var result = JsonSerializer.Deserialize<CaptureAnalysisResult>(content, JsonOptions);
                        return IsValidCaptureResult(result)
                            ? ResultFactory.Success(result! with { TokensConsumed = inputTokens + outputTokens })
                            : Failure<CaptureAnalysisResult>("AI_058", "AgentResponseInvalid");
                    }
                    catch (JsonException)
                    {
                        return Failure<CaptureAnalysisResult>("AI_058", "AgentResponseInvalid");
                    }
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<Result<IReadOnlyList<ExtractedWorkItem>>> RunScopeAgentAsync(
        ScopeAgentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CompleteAgentAsync<ScopeAgentRequest, IReadOnlyList<ExtractedWorkItem>>(
            AgentType.Scope,
            request,
            request.TenantId,
            request.ProjectId,
            IsValidScopeRequest,
            IsValidScopeResponse,
            cancellationToken);
    }

    public Task<Result<APUStructureResult>> RunProductionAgentAsync(
        ProductionAgentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CompleteAgentAsync<ProductionAgentRequest, APUStructureResult>(
            AgentType.Production,
            request,
            request.TenantId,
            request.ProjectId,
            IsValidProductionRequest,
            IsValidProductionResponse,
            cancellationToken);
    }

    public Task<Result<ScheduleResult>> RunPlanningAgentAsync(
        PlanningAgentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CompleteAgentAsync<PlanningAgentRequest, ScheduleResult>(
            AgentType.Planning,
            request,
            request.TenantId,
            request.ProjectId,
            IsValidPlanningRequest,
            IsValidPlanningResponse,
            cancellationToken);
    }

    public Task<Result<ValuationResult>> RunValuationAgentAsync(
        ValuationAgentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CompleteAgentAsync<ValuationAgentRequest, ValuationResult>(
            AgentType.Valuation,
            request,
            request.TenantId,
            request.ProjectId,
            IsValidValuationRequest,
            IsValidValuationResponse,
            cancellationToken);
    }

    public Task<Result<OfferNarrativeResult>> RunOfferAgentAsync(
        OfferAgentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CompleteAgentAsync<OfferAgentRequest, OfferNarrativeResult>(
            AgentType.Offer,
            request,
            request.TenantId,
            request.ProjectId,
            IsValidOfferRequest,
            IsValidOfferResponse,
            cancellationToken);
    }

    public Task<Result<ConversationResponse>> RunConversationAgentAsync(
        ConversationAgentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return CompleteAgentAsync<ConversationAgentRequest, ConversationResponse>(
            AgentType.Conversation,
            request,
            request.TenantId,
            null,
            IsValidConversationRequest,
            IsValidConversationResponse,
            cancellationToken);
    }

    public async Task<Result<float[]>> GenerateEmbeddingAsync(
        Guid tenantId,
        string text,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(text))
        {
            return Failure<float[]>("AI_044", "EmbeddingTextRequired");
        }

        var modelResult = _modelRegistry.ResolvePrimary(AgentType.Embedding);
        if (!modelResult.IsSuccess)
        {
            return ResultFactory.Failure<float[]>(modelResult.Error!);
        }

        var model = modelResult.Value!;
        var runResult = AgentRun.Create(
            tenantId,
            null,
            AgentType.Embedding,
            model.ModelId,
            model.ProviderName);
        if (!runResult.IsSuccess)
        {
            return ResultFactory.Failure<float[]>(runResult.Error!);
        }

        var run = runResult.Value!;
        await _dbContext.AgentRuns.AddAsync(run, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var embeddingResult = await _providerClient.GenerateEmbeddingAsync(
                    model,
                    text,
                    cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();
            if (!embeddingResult.IsSuccess)
            {
                await FailRunAsync(run, model, stopwatch.Elapsed, cancellationToken).ConfigureAwait(false);
                return ResultFactory.Failure<float[]>(embeddingResult.Error!);
            }

            var embedding = embeddingResult.Value!;
            var completion = run.Complete(
                model.ModelId,
                model.ProviderName,
                embedding.InputTokens,
                outputTokens: 0,
                creditsCharged: 0,
                stopwatch.Elapsed,
                fallbackActivated: false,
                fallbackReason: null);
            if (!completion.IsSuccess)
            {
                return ResultFactory.Failure<float[]>(completion.Error!);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ResultFactory.Success(embedding.Vector);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            await FailRunAsync(run, model, stopwatch.Elapsed, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<Result<TResponse>> CompleteAgentAsync<TRequest, TResponse>(
        AgentType agentType,
        TRequest request,
        Guid tenantId,
        Guid? projectId,
        Func<TRequest, bool> isValidRequest,
        Func<TResponse, bool> isValidResponse,
        CancellationToken cancellationToken)
    {
        if (!isValidRequest(request))
        {
            return Failure<TResponse>("AI_041", "ChatRequestInvalid");
        }

        // Resolve the primary model configured for the requested agent type.
        var modelResult = _modelRegistry.ResolvePrimary(agentType);
        if (!modelResult.IsSuccess)
        {
            return ResultFactory.Failure<TResponse>(modelResult.Error!);
        }

        return await CompleteChatAuditedAsync(
                agentType,
                tenantId,
                projectId,
                modelResult.Value!,
                CreateJsonRequest<TRequest, TResponse>(request),
                (content, _, _) => DeserializeAndValidate(content, isValidResponse),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<T>> CompleteChatAuditedAsync<T>(
        AgentType agentType,
        Guid tenantId,
        Guid? projectId,
        AiResolvedModel model,
        AiChatRequest request,
        Func<string, int, int, Result<T>> parseResponse,
        CancellationToken cancellationToken)
    {
        var runResult = AgentRun.Create(
            tenantId,
            projectId,
            agentType,
            model.ModelId,
            model.ProviderName);
        if (!runResult.IsSuccess)
        {
            return ResultFactory.Failure<T>(runResult.Error!);
        }

        var run = runResult.Value!;
        await _dbContext.AgentRuns.AddAsync(run, cancellationToken).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var providerResult = await _providerClient
                .CompleteChatAsync(model, request, cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();

            if (!providerResult.IsSuccess)
            {
                await FailRunAsync(run, model, stopwatch.Elapsed, cancellationToken).ConfigureAwait(false);
                return ResultFactory.Failure<T>(providerResult.Error!);
            }

            var completion = providerResult.Value!;
            var parsedResult = parseResponse(
                completion.Content,
                completion.InputTokens,
                completion.OutputTokens);
            if (!parsedResult.IsSuccess)
            {
                await FailRunAsync(
                        run,
                        model,
                        stopwatch.Elapsed,
                        cancellationToken,
                        completion.InputTokens,
                        completion.OutputTokens)
                    .ConfigureAwait(false);
                return parsedResult;
            }

            var completed = run.Complete(
                model.ModelId,
                model.ProviderName,
                completion.InputTokens,
                completion.OutputTokens,
                creditsCharged: 0,
                stopwatch.Elapsed,
                fallbackActivated: false,
                fallbackReason: null);
            if (!completed.IsSuccess)
            {
                return ResultFactory.Failure<T>(completed.Error!);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return parsedResult;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            await FailRunAsync(run, model, stopwatch.Elapsed, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task FailRunAsync(
        AgentRun run,
        AiResolvedModel model,
        TimeSpan duration,
        CancellationToken cancellationToken,
        int inputTokens = 0,
        int outputTokens = 0)
    {
        var failed = run.Fail(
            model.ModelId,
            model.ProviderName,
            inputTokens,
            outputTokens,
            duration,
            fallbackReason: null,
            fallbackActivated: false);
        if (!failed.IsSuccess)
        {
            throw new InvalidOperationException(failed.Error!.Message);
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Result<T> DeserializeAndValidate<T>(
        string content,
        Func<T, bool> validator)
    {
        try
        {
            var response = JsonSerializer.Deserialize<T>(content, JsonOptions);
            return response is not null && validator(response)
                ? ResultFactory.Success(response)
                : Failure<T>("AI_058", "AgentResponseInvalid");
        }
        catch (JsonException)
        {
            return Failure<T>("AI_058", "AgentResponseInvalid");
        }
    }

    private static AiChatRequest CreateJsonRequest<TRequest, TResponse>(TRequest request) =>
        new(
            [
                new AiChatMessage(
                    "system",
                    $"Return only a JSON value matching the {typeof(TResponse).Name} contract. Use the supplied {typeof(TRequest).Name} as input. Do not add explanatory text."),
                new AiChatMessage("user", JsonSerializer.Serialize(request, JsonOptions))
            ],
            RequireJson: true);

    private static bool IsValidScopeRequest(ScopeAgentRequest request) =>
        request.TenantId != Guid.Empty && request.ProjectId != Guid.Empty &&
        request.CapturedWorkItems is not null && request.Observations is not null;

    private static bool IsValidScopeResponse(IReadOnlyList<ExtractedWorkItem> response) =>
        response.All(IsValidWorkItem);

    private static bool IsValidProductionRequest(ProductionAgentRequest request) =>
        request.TenantId != Guid.Empty && request.ProjectId != Guid.Empty &&
        request.WorkItems is not null && request.WorkItems.All(IsValidWorkItemReference) &&
        request.CandidateTemplates is not null;

    private static bool IsValidProductionResponse(APUStructureResult response) =>
        response.Assignments is not null && response.TokensConsumed >= 0 &&
        IsValidConfidence(response.Confidence) &&
        response.Assignments.All(assignment =>
            assignment is not null &&
            assignment.WorkItemId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(assignment.OutputUnit) &&
            !string.IsNullOrWhiteSpace(assignment.Source) &&
            assignment.Components is not null &&
            IsValidConfidence(assignment.Confidence) &&
            assignment.Components.All(component =>
                component is not null &&
                !string.IsNullOrWhiteSpace(component.Description) &&
                !string.IsNullOrWhiteSpace(component.Unit)));

    private static bool IsValidPlanningRequest(PlanningAgentRequest request) =>
        request.TenantId != Guid.Empty && request.ProjectId != Guid.Empty &&
        request.WorkItems is not null && request.WorkItems.All(IsValidWorkItemReference) &&
        request.APUAssignments is not null;

    private static bool IsValidPlanningResponse(ScheduleResult response) =>
        response.Activities is not null && response.Precedences is not null &&
        response.CriticalPath is not null && response.Milestones is not null &&
        response.TotalDurationDays >= 0 && response.TokensConsumed >= 0 &&
        IsValidConfidence(response.Confidence) &&
        response.Activities.All(activity =>
            activity is not null && activity.Id != Guid.Empty &&
            !string.IsNullOrWhiteSpace(activity.Name) && activity.DurationDays >= 0 &&
            activity.WorkItemIds is not null) &&
        response.Precedences.All(precedence =>
            precedence is not null && precedence.ActivityId != Guid.Empty &&
            precedence.PredecessorActivityId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(precedence.Type) && precedence.LagDays >= 0);

    private static bool IsValidValuationRequest(ValuationAgentRequest request) =>
        request.TenantId != Guid.Empty && request.ProjectId != Guid.Empty &&
        request.WorkItems is not null && request.WorkItems.All(IsValidWorkItemReference) &&
        request.APUAssignments is not null && request.ComponentPrices is not null &&
        !string.IsNullOrWhiteSpace(request.Currency);

    private static bool IsValidValuationResponse(ValuationResult response) =>
        response.ValuedComponents is not null && response.ValuedWorkItems is not null &&
        !string.IsNullOrWhiteSpace(response.Currency) && response.TokensConsumed >= 0 &&
        IsValidConfidence(response.Confidence) &&
        response.ValuedComponents.All(component =>
            component is not null && component.ComponentId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(component.ComponentType) &&
            !string.IsNullOrWhiteSpace(component.Description) &&
            !string.IsNullOrWhiteSpace(component.Unit) &&
            !string.IsNullOrWhiteSpace(component.Currency) &&
            !string.IsNullOrWhiteSpace(component.PricingSource) &&
            IsValidConfidence(component.Confidence)) &&
        response.ValuedWorkItems.All(item =>
            item is not null && item.WorkItemId != Guid.Empty &&
            item.APUAssignmentId != Guid.Empty && !string.IsNullOrWhiteSpace(item.Currency));

    private static bool IsValidOfferRequest(OfferAgentRequest request) =>
        request.TenantId != Guid.Empty && request.ProjectId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(request.ProjectName) && request.Valuation is not null &&
        request.ValidityDays > 0 && !string.IsNullOrWhiteSpace(request.Language);

    private static bool IsValidOfferResponse(OfferNarrativeResult response) =>
        !string.IsNullOrWhiteSpace(response.Title) &&
        !string.IsNullOrWhiteSpace(response.ExecutiveSummary) &&
        !string.IsNullOrWhiteSpace(response.ScopeDescription) &&
        response.ValidityDays > 0 &&
        !string.IsNullOrWhiteSpace(response.AIActDisclaimer) &&
        response.TokensConsumed >= 0 && IsValidConfidence(response.Confidence);

    private static bool IsValidConversationRequest(ConversationAgentRequest request) =>
        request.TenantId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(request.Channel) &&
        !string.IsNullOrWhiteSpace(request.UserMessage) &&
        request.ConversationHistory is not null;

    private static bool IsValidConversationResponse(ConversationResponse response) =>
        !string.IsNullOrWhiteSpace(response.Message) && response.TokensConsumed >= 0;

    private static bool IsValidWorkItem(ExtractedWorkItem item) =>
        item is not null && !string.IsNullOrWhiteSpace(item.CanonicalConceptId) &&
        !string.IsNullOrWhiteSpace(item.Description) && !string.IsNullOrWhiteSpace(item.Unit) &&
        item.Quantity >= 0 && IsValidConfidence(item.Confidence) &&
        !string.IsNullOrWhiteSpace(item.AIStatus);

    private static bool IsValidWorkItemReference(WorkItemRef item) =>
        item is not null && item.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(item.CanonicalConceptId) &&
        !string.IsNullOrWhiteSpace(item.Description) &&
        !string.IsNullOrWhiteSpace(item.Unit) && item.Quantity >= 0 &&
        IsValidConfidence(item.Confidence);

    private static bool IsValidConfidence(double confidence) =>
        double.IsFinite(confidence) && confidence is >= 0 and <= 1;

    private static string BuildCaptureInput(CaptureAgentRequest request) =>
        JsonSerializer.Serialize(new
        {
            request.TextInputs,
            request.Measurements,
            request.Transcriptions,
            Media = request.MediaFiles.Select(file => new
            {
                file.MimeType,
                file.Room,
                file.IsPathology,
                file.Processed,
                file.FileName
            })
        }, JsonOptions);

    private static bool IsValidCaptureResult(CaptureAnalysisResult? result) =>
        result is not null &&
        result.TokensConsumed >= 0 &&
        double.IsFinite(result.Confidence) && result.Confidence is >= 0 and <= 1 &&
        result.WorkItems is not null && result.Observations is not null &&
        result.WorkItems.All(item =>
            item is not null &&
            !string.IsNullOrWhiteSpace(item.CanonicalConceptId) &&
            !string.IsNullOrWhiteSpace(item.Description) &&
            !string.IsNullOrWhiteSpace(item.Unit) &&
            item.Quantity >= 0 &&
            double.IsFinite(item.Confidence) && item.Confidence is >= 0 and <= 1 &&
            !string.IsNullOrWhiteSpace(item.AIStatus));

    private static Result<T> NotImplemented<T>() =>
        Failure<T>("AI_059", "AgentOperationPending");

    private static Result<T> Failure<T>(string code, string messageKey) =>
        ResultFactory.Failure<T>(
            ApplicationError.AI(code, AiMessages.Get(messageKey)));
}
