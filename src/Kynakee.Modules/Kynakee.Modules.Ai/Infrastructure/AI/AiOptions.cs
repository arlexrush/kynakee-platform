using Kynakee.Modules.Ai.Domain.Enums;
using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Modules.Ai.Infrastructure.AI;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Options binding creates this type through reflection.")]
internal sealed class AiOptions
{
    public AiOptions()
    {
    }

    public const string SectionName = "AI";

    public bool Enabled { get; set; }

    public long MaxMediaBytes { get; set; } = 20 * 1024 * 1024;

    public Dictionary<string, AiProviderOptions> Providers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, AiModelOptions> Models { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, AiAgentOptions> Agents { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Options binding creates this type through reflection.")]
internal sealed class AiProviderOptions
{
    public AiProviderOptions()
    {
    }

    public AiProviderProtocol Protocol { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    public string ChatPath { get; set; } = string.Empty;

    public string EmbeddingsPath { get; set; } = string.Empty;
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Options binding creates this type through reflection.")]
internal sealed class AiModelOptions
{
    public AiModelOptions()
    {
    }

    public string Provider { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public string CostTier { get; set; } = string.Empty;

    public List<ModelCapability> Capabilities { get; set; } = [];

    public int Dimensions { get; set; }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Options binding creates this type through reflection.")]
internal sealed class AiAgentOptions
{
    public AiAgentOptions()
    {
    }

    public string ModelId { get; set; } = string.Empty;

    public string FallbackModelId { get; set; } = string.Empty;

    public string? PremiumModelId { get; set; }
}
