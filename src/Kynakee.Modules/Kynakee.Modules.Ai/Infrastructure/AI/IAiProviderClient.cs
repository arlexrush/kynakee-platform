using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Ai.Infrastructure.AI;

internal interface IAiProviderClient
{
    Task<Result<AiChatCompletion>> CompleteChatAsync(
        AiResolvedModel model,
        AiChatRequest request,
        CancellationToken cancellationToken);

    Task<Result<AiEmbedding>> GenerateEmbeddingAsync(
        AiResolvedModel model,
        string text,
        CancellationToken cancellationToken);

    Task<Result<Uri>> UploadGeminiFileAsync(
        AiResolvedModel model,
        AiPreparedMedia media,
        CancellationToken cancellationToken);
}
