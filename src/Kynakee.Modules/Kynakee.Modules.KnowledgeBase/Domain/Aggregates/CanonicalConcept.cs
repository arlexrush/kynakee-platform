using Kynakee.Modules.KnowledgeBase.Domain.Resources;
using Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.KnowledgeBase.Domain.Aggregates;

public sealed class CanonicalConcept : GlobalAggregateRoot<CanonicalConceptId>
{
    private readonly List<ConceptTranslation> _translations = [];
    private float[]? _embeddingVector;

    private CanonicalConcept() { }

    private CanonicalConcept(
        CanonicalConceptId id,
        string category,
        string? subcategory,
        MeasurementUnit defaultUnit,
        IEnumerable<ConceptTranslation> translations,
        Guid? ownerTenantId,
        Guid? ownerUserId)
        : base(id, ownerTenantId, ownerUserId)
    {
        Category = category;
        Subcategory = subcategory;
        DefaultUnit = defaultUnit;
        _translations.AddRange(translations);
    }

    public string Category { get; private set; } = string.Empty;

    public string? Subcategory { get; private set; }

    public MeasurementUnit DefaultUnit { get; private set; } = default!;

    public IReadOnlyList<ConceptTranslation> Translations => _translations.AsReadOnly();

    public int APUTemplateCount { get; private set; }

    public IReadOnlyList<float>? EmbeddingVector =>
        _embeddingVector is null ? null : Array.AsReadOnly(_embeddingVector);

    /// <summary>
    /// Creates a globally visible concept with at least one translation.
    /// </summary>
    public static Result<CanonicalConcept> Create(
        CanonicalConceptId id,
        string category,
        string? subcategory,
        MeasurementUnit defaultUnit,
        IEnumerable<ConceptTranslation> translations,
        Guid? ownerTenantId = null,
        Guid? ownerUserId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(defaultUnit);
        ArgumentNullException.ThrowIfNull(translations);

        if (string.IsNullOrWhiteSpace(category) || category.Trim().Length > 100)
        {
            return ResultFactory.Failure<CanonicalConcept>(
                ApplicationError.Validation("KB_CATEGORY_INVALID", KnowledgeBaseMessages.Get("CategoryInvalid")));
        }

        if (subcategory?.Length > 100)
        {
            return ResultFactory.Failure<CanonicalConcept>(
                ApplicationError.Validation("KB_SUBCATEGORY_INVALID", KnowledgeBaseMessages.Get("SubcategoryInvalid")));
        }

        if (ownerTenantId == Guid.Empty || ownerUserId == Guid.Empty)
        {
            return ResultFactory.Failure<CanonicalConcept>(
                ApplicationError.Validation("KB_OWNER_INVALID", KnowledgeBaseMessages.Get("OwnerIdInvalid")));
        }

        var items = translations.ToList();
        if (items.Count == 0 || items.Any(item => item is null))
        {
            return ResultFactory.Failure<CanonicalConcept>(
                ApplicationError.Validation("KB_TRANSLATION_REQUIRED", KnowledgeBaseMessages.Get("TranslationRequired")));
        }

        if (items.Select(item => item.LanguageCode).Distinct(StringComparer.Ordinal).Count() != items.Count)
        {
            return ResultFactory.Failure<CanonicalConcept>(
                ApplicationError.Conflict("KB_TRANSLATION_DUPLICATE", KnowledgeBaseMessages.Get("TranslationDuplicate")));
        }

        return ResultFactory.Success(new CanonicalConcept(
            id, category.Trim(), subcategory?.Trim(), defaultUnit, items, ownerTenantId, ownerUserId));
    }

    /// <summary>
    /// Adds a translation while keeping each language unique within the concept.
    /// </summary>
    public Result AddTranslation(ConceptTranslation translation, Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(translation);

        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("KB_CONCEPT_DELETED", KnowledgeBaseMessages.Get("EntityDeleted")));
        }

        if (_translations.Any(item => item.LanguageCode == translation.LanguageCode))
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("KB_TRANSLATION_DUPLICATE", KnowledgeBaseMessages.Get("TranslationDuplicate")));
        }

        _translations.Add(translation);
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    /// <summary>
    /// Records the addition of a template associated with this concept.
    /// </summary>
    public Result RegisterTemplate(Guid? updatedBy = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("KB_CONCEPT_DELETED", KnowledgeBaseMessages.Get("EntityDeleted")));
        }

        if (APUTemplateCount == int.MaxValue)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("KB_TEMPLATE_COUNT_EXCEEDED", KnowledgeBaseMessages.Get("TemplateCountExceeded")));
        }

        APUTemplateCount++;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    /// <summary>
    /// Stores a defensive copy of the concept's semantic-search vector.
    /// </summary>
    public Result UpdateEmbedding(float[] vector, Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(vector);

        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("KB_CONCEPT_DELETED", KnowledgeBaseMessages.Get("EntityDeleted")));
        }

        if (!EmbeddingValidation.IsValid(vector))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation("KB_EMBEDDING_INVALID", KnowledgeBaseMessages.Get("EmbeddingInvalid")));
        }

        _embeddingVector = (float[])vector.Clone();
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }
}