namespace Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;

internal static class EmbeddingValidation
{
    internal static bool IsValid(float[] vector) =>
        vector.Length == 768 && vector.All(float.IsFinite);
}