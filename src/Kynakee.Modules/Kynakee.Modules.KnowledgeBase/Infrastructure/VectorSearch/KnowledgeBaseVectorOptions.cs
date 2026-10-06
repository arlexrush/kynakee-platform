namespace Kynakee.Modules.KnowledgeBase.Infrastructure.VectorSearch;

public sealed class KnowledgeBaseVectorOptions
{
    public const string SectionName = "KnowledgeBase:Qdrant";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 6334;

    public string? ApiKey { get; set; }

    public string CanonicalConceptsCollection { get; set; } = "canonical_concepts";

    public string ApuStructuresCollection { get; set; } = "apu_structures";

    public string ProjectContextsCollection { get; set; } = "project_contexts";
}
