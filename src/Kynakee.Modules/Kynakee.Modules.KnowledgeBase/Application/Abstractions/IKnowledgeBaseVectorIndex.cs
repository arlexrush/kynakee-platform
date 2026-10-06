namespace Kynakee.Modules.KnowledgeBase.Application.Abstractions;

public interface IKnowledgeBaseVectorIndex
{
    Task UpsertCanonicalConceptAsync(
        string canonicalConceptId,
        float[] embedding,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<KnowledgeBaseVectorMatch>> SearchCanonicalConceptsAsync(
        float[] embedding,
        int limit,
        CancellationToken cancellationToken);

    Task RemoveCanonicalConceptAsync(
        string canonicalConceptId,
        CancellationToken cancellationToken);

    Task UpsertAPUTemplateAsync(
        Guid apuTemplateId,
        float[] embedding,
        string canonicalConceptId,
        string projectType,
        string region,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<KnowledgeBaseVectorMatch>> SearchAPUTemplatesAsync(
        float[] embedding,
        int limit,
        CancellationToken cancellationToken);

    Task RemoveAPUTemplateAsync(
        Guid apuTemplateId,
        CancellationToken cancellationToken);
}

public sealed record KnowledgeBaseVectorMatch(string EntityId, float Score);
