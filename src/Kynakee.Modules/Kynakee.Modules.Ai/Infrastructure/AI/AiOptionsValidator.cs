using Kynakee.Modules.Ai.Domain.Enums;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Modules.Ai.Infrastructure.AI;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The options validator is instantiated through dependency injection.")]
internal sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    public AiOptionsValidator()
    {
    }

    private static readonly (string Name, ModelCapability Capability)[] RequiredAgents =
    [
        ("CaptureAgent", ModelCapability.Chat),
        ("ScopeAgent", ModelCapability.Chat),
        ("ProductionAgent", ModelCapability.Chat),
        ("PlanningAgent", ModelCapability.Chat),
        ("ValuationAgent", ModelCapability.Chat),
        ("OfferAgent", ModelCapability.Chat),
        ("ConversationAgent", ModelCapability.Chat),
        ("EmbeddingService", ModelCapability.Embedding)
    ];

    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (options.MaxMediaBytes is < 1 or > 104_857_600)
        {
            failures.Add("AI media size limit must be between 1 byte and 100 MiB.");
        }

        ValidateProviders(options, failures);
        ValidateModels(options, failures);
        ValidateAgents(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateProviders(
        AiOptions options,
        List<string> failures)
    {
        if (options.Providers.Count == 0)
        {
            failures.Add("At least one AI provider must be configured when AI is enabled.");
            return;
        }

        foreach (var (name, provider) in options.Providers)
        {
            if (string.IsNullOrWhiteSpace(name) || provider is null)
            {
                failures.Add("AI provider names and settings are required.");
                continue;
            }

            if (!Enum.IsDefined(provider.Protocol))
            {
                failures.Add($"AI provider '{name}' has an unsupported protocol.");
            }

            if (!Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var baseUri) ||
                baseUri.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(baseUri.UserInfo) ||
                !string.IsNullOrEmpty(baseUri.Query) ||
                !string.IsNullOrEmpty(baseUri.Fragment))
            {
                failures.Add($"AI provider '{name}' must use a credential-free HTTPS base URL.");
            }

            if (string.IsNullOrWhiteSpace(provider.ApiKey))
            {
                failures.Add($"AI provider '{name}' requires an API key supplied through external configuration.");
            }

            if (provider.TimeoutSeconds is < 1 or > 300)
            {
                failures.Add($"AI provider '{name}' timeout must be between 1 and 300 seconds.");
            }

            ValidateRelativePath(provider.ChatPath, name, "chat", failures);
            ValidateRelativePath(provider.EmbeddingsPath, name, "embeddings", failures);
        }
    }

    private static void ValidateModels(
        AiOptions options,
        List<string> failures)
    {
        if (options.Models.Count == 0)
        {
            failures.Add("At least one AI model must be configured when AI is enabled.");
            return;
        }

        foreach (var (alias, model) in options.Models)
        {
            if (string.IsNullOrWhiteSpace(alias) || model is null)
            {
                failures.Add("AI model aliases and settings are required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(model.Provider) ||
                FindKey(options.Providers, model.Provider) is null)
            {
                failures.Add($"AI model '{alias}' refers to an unconfigured provider.");
            }

            if (string.IsNullOrWhiteSpace(model.ModelId) ||
                model.ModelId.Length > 100 ||
                string.IsNullOrWhiteSpace(model.CostTier))
            {
                failures.Add($"AI model '{alias}' requires a model ID and cost tier.");
            }

            if (model.Capabilities.Count == 0 ||
                model.Capabilities.Any(capability => !Enum.IsDefined(capability)))
            {
                failures.Add($"AI model '{alias}' must declare supported capabilities.");
            }

            var supportsEmbeddings = model.Capabilities.Contains(ModelCapability.Embedding);
            if ((supportsEmbeddings && model.Dimensions != 768) ||
                (!supportsEmbeddings && model.Dimensions != 0))
            {
                failures.Add($"AI model '{alias}' must configure 768 dimensions only when it supports embeddings.");
            }
        }
    }

    private static void ValidateAgents(
        AiOptions options,
        List<string> failures)
    {
        if (options.Agents.Count == 0)
        {
            failures.Add("AI agent model assignments are required when AI is enabled.");
            return;
        }

        foreach (var (agentName, requiredCapability) in RequiredAgents)
        {
            var agentKey = FindKey(options.Agents, agentName);
            if (agentKey is null || options.Agents[agentKey] is not { } agent)
            {
                failures.Add($"AI agent '{agentName}' is not configured.");
                continue;
            }

            ValidateAssignedModel(
                options,
                agentName,
                agent.ModelId,
                requiredCapability,
                "primary",
                failures);
            ValidateAssignedModel(
                options,
                agentName,
                agent.FallbackModelId,
                requiredCapability,
                "fallback",
                failures);

            if (agentName == "CaptureAgent")
            {
                ValidateAssignedModel(
                    options,
                    agentName,
                    agent.ModelId,
                    ModelCapability.Vision,
                    "primary",
                    failures);
                ValidateAssignedModel(
                    options,
                    agentName,
                    agent.FallbackModelId,
                    ModelCapability.Vision,
                    "fallback",
                    failures);
            }

            if (!string.IsNullOrWhiteSpace(agent.PremiumModelId))
            {
                ValidateAssignedModel(
                    options,
                    agentName,
                    agent.PremiumModelId,
                    requiredCapability,
                    "premium",
                    failures);

                if (agentName == "CaptureAgent")
                {
                    ValidateAssignedModel(
                        options,
                        agentName,
                        agent.PremiumModelId,
                        ModelCapability.Vision,
                        "premium",
                        failures);
                }
            }
        }
    }

    private static void ValidateAssignedModel(
        AiOptions options,
        string agentName,
        string alias,
        ModelCapability requiredCapability,
        string assignmentName,
        List<string> failures)
    {
        var modelKey = FindKey(options.Models, alias);
        if (modelKey is null || options.Models[modelKey] is not { } model)
        {
            failures.Add($"AI agent '{agentName}' references an unconfigured {assignmentName} model.");
            return;
        }

        if (!model.Capabilities.Contains(requiredCapability))
        {
            failures.Add($"AI agent '{agentName}' {assignmentName} model lacks the {requiredCapability} capability.");
        }
    }

    private static void ValidateRelativePath(
        string? path,
        string providerName,
        string pathName,
        List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out _) ||
            path.StartsWith('/') ||
            path.Contains("..", StringComparison.Ordinal))
        {
            failures.Add($"AI provider '{providerName}' {pathName} path must be a relative URI path.");
        }
    }

    private static string? FindKey<TValue>(
        IReadOnlyDictionary<string, TValue> values,
        string key) =>
        values.Keys.FirstOrDefault(candidate =>
            string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase));
}
