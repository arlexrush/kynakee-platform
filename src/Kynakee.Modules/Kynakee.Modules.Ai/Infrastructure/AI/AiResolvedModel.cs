using Kynakee.Modules.Ai.Domain.Enums;

namespace Kynakee.Modules.Ai.Infrastructure.AI;

internal sealed record AiResolvedModel(
    string Alias,
    string ProviderName,
    AiProviderProtocol Protocol,
    Uri BaseUrl,
    string ApiKey,
    string ModelId,
    string CostTier,
    IReadOnlySet<ModelCapability> Capabilities,
    int Dimensions,
    int TimeoutSeconds,
    string ChatPath,
    string EmbeddingsPath)
{
    public override string ToString() => $"{ProviderName}/{Alias}";
}
