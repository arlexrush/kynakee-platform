using Kynakee.Modules.Ai.Domain.Enums;
using Kynakee.Modules.Ai.Domain.Resources;
using Kynakee.Modules.Ai.Domain.Services;
using Kynakee.Modules.SharedKernel.Application;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

namespace Kynakee.Modules.Ai.Infrastructure.AI;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The model registry is instantiated through dependency injection.")]
internal sealed class AiModelRegistry
{
    private readonly AiOptions _options;

    public AiModelRegistry(IOptions<AiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public Result<AiResolvedModel> ResolvePrimary(
        AgentType agentType,
        IReadOnlyCollection<string>? mediaMimeTypes = null) =>
        ResolveAgentModel(
            agentType,
            static agent => agent.ModelId,
            mediaMimeTypes);

    public Result<AiResolvedModel> ResolveFallback(
        AgentType agentType,
        IReadOnlyCollection<string>? mediaMimeTypes = null) =>
        ResolveAgentModel(
            agentType,
            static agent => agent.FallbackModelId,
            mediaMimeTypes);

    public Result<AiResolvedModel> ResolvePremium(
        AgentType agentType,
        IReadOnlyCollection<string>? mediaMimeTypes = null) =>
        ResolveAgentModel(
            agentType,
            static agent => agent.PremiumModelId,
            mediaMimeTypes);

    public Result<AiResolvedModel> ResolveModel(string alias)
    {
        if (!_options.Enabled)
        {
            return Failure("AI_030", "AiDisabled");
        }

        var modelKey = FindKey(_options.Models, alias);
        if (modelKey is null || _options.Models[modelKey] is not { } model)
        {
            return Failure("AI_031", "ModelConfigurationMissing");
        }

        var providerKey = FindKey(_options.Providers, model.Provider);
        if (providerKey is null || _options.Providers[providerKey] is not { } provider ||
            !Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var baseUrl))
        {
            return Failure("AI_032", "ProviderConfigurationMissing");
        }

        var chatPath = GetPath(
            provider.ChatPath,
            provider.Protocol,
            "chat/completions",
            "models/{model}:generateContent");
        var embeddingPath = GetPath(
            provider.EmbeddingsPath,
            provider.Protocol,
            "embeddings",
            "models/{model}:embedContent");

        return ResultFactory.Success(
            new AiResolvedModel(
                modelKey,
                providerKey,
                provider.Protocol,
                EnsureTrailingSlash(baseUrl),
                provider.ApiKey,
                model.ModelId,
                model.CostTier,
                model.Capabilities.ToHashSet(),
                model.Dimensions,
                provider.TimeoutSeconds,
                chatPath,
                embeddingPath));
    }

    private Result<AiResolvedModel> ResolveAgentModel(
        AgentType agentType,
        Func<AiAgentOptions, string?> selectModel,
        IReadOnlyCollection<string>? mediaMimeTypes)
    {
        if (!_options.Enabled)
        {
            return Failure("AI_030", "AiDisabled");
        }

        var agentName = GetAgentName(agentType);
        if (agentName is null)
        {
            return Failure("AI_033", "AgentConfigurationMissing");
        }

        var agentKey = FindKey(_options.Agents, agentName);
        if (agentKey is null || _options.Agents[agentKey] is not { } agent)
        {
            return Failure("AI_033", "AgentConfigurationMissing");
        }

        var modelAlias = selectModel(agent);
        if (string.IsNullOrWhiteSpace(modelAlias))
        {
            return Failure("AI_034", "PremiumModelNotConfigured");
        }

        var requiredCapabilities = GetRequiredCapabilities(
            agentType,
            mediaMimeTypes);
        if (!requiredCapabilities.IsSuccess)
        {
            return Failure("AI_052", "MediaTypeUnsupported");
        }

        var resolvedModel = ResolveModel(modelAlias);
        if (!resolvedModel.IsSuccess)
        {
            return resolvedModel;
        }

        return requiredCapabilities.Value!.IsSubsetOf(resolvedModel.Value!.Capabilities)
            ? resolvedModel
            : Failure("AI_051", "ModelCapabilitiesInsufficient");
    }

    private static Result<HashSet<ModelCapability>> GetRequiredCapabilities(
        AgentType agentType,
        IReadOnlyCollection<string>? mediaMimeTypes)
    {
        var requiredCapabilities = new HashSet<ModelCapability>
        {
            agentType == AgentType.Embedding
                ? ModelCapability.Embedding
                : ModelCapability.Chat
        };

        if (mediaMimeTypes is null)
        {
            return ResultFactory.Success(requiredCapabilities);
        }

        if (agentType == AgentType.Embedding && mediaMimeTypes.Count > 0)
        {
            return ResultFactory.Failure<HashSet<ModelCapability>>(
                ApplicationError.Validation(
                    "AI_052",
                    AiMessages.Get("MediaTypeUnsupported")));
        }

        foreach (var mimeType in mediaMimeTypes)
        {
            var modality = AiMediaTypeClassifier.Classify(mimeType);
            if (modality.HasValue)
            {
                requiredCapabilities.Add(
                    AiMediaTypeClassifier.ToModelCapability(modality.Value));
            }
            else
            {
                return ResultFactory.Failure<HashSet<ModelCapability>>(
                    ApplicationError.Validation(
                        "AI_052",
                        AiMessages.Get("MediaTypeUnsupported")));
            }
        }

        return ResultFactory.Success(requiredCapabilities);
    }

    private static string? GetAgentName(AgentType agentType) =>
        agentType switch
        {
            AgentType.Capture => "CaptureAgent",
            AgentType.Scope => "ScopeAgent",
            AgentType.Production => "ProductionAgent",
            AgentType.Planning => "PlanningAgent",
            AgentType.Valuation => "ValuationAgent",
            AgentType.Offer => "OfferAgent",
            AgentType.Conversation => "ConversationAgent",
            AgentType.Embedding => "EmbeddingService",
            _ => null
        };

    private static string GetPath(
        string configuredPath,
        AiProviderProtocol protocol,
        string openAiDefault,
        string geminiDefault) =>
        !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : protocol == AiProviderProtocol.Gemini
                ? geminiDefault
                : openAiDefault;

    private static Uri EnsureTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith('/')
            ? uri
            : new Uri($"{uri.AbsoluteUri}/", UriKind.Absolute);

    private static string? FindKey<TValue>(
        IReadOnlyDictionary<string, TValue> values,
        string key) =>
        values.Keys.FirstOrDefault(candidate =>
            string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase));

    private static Result<AiResolvedModel> Failure(string code, string messageKey) =>
        ResultFactory.Failure<AiResolvedModel>(
            ApplicationError.AI(code, AiMessages.Get(messageKey)));
}
