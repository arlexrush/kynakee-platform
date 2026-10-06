using Kynakee.Modules.KnowledgeBase.Domain.Resources;
using Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.KnowledgeBase.Domain.Aggregates;

/// <summary>
/// Plantilla de APU que agrupa componentes y metadatos (concepto canónico, descripción, tipo de proyecto, región,
/// unidad y rendimiento) y expone operaciones de dominio para añadir componentes, registrar usos y actualizar su vector
/// de incrustación.
/// </summary>
/// <remarks>Las instancias se crean mediante APUTemplate.Create. Mantiene una copia defensiva del vector de
/// incrustación, un contador de uso y la confianza media agregada. Las operaciones validan el estado de eliminación
/// lógica (soft delete) y aplican reglas de validación del dominio; la plantilla contiene componentes no valorados y la
/// unidad de salida debe coincidir exactamente con la unidad del work item según la regla de construcción de
/// APU.</remarks>
public sealed class APUTemplate : GlobalAggregateRoot<APUTemplateId>
{
    private readonly List<APUTemplateComponent> _components = [];
    private float[]? _embeddingVector;

    private APUTemplate() { }

    private APUTemplate(
        APUTemplateId id,
        CanonicalConceptId canonicalConceptId,
        string description,
        ProjectType projectType,
        GeoRegion region,
        MeasurementUnit unit,
        ProductionYield productionYield,
        IEnumerable<APUTemplateComponent> components,
        APUTemplateSource source,
        Guid? ownerTenantId,
        Guid? ownerUserId)
        : base(id, ownerTenantId, ownerUserId)
    {
        CanonicalConceptId = canonicalConceptId;
        Description = description;
        ProjectType = projectType;
        Region = region;
        Unit = unit;
        ProductionYield = productionYield;
        Source = source;
        _components.AddRange(
            components.Select((component, sortOrder) => component.WithSortOrder(sortOrder)));
    }

    public CanonicalConceptId CanonicalConceptId { get; private set; } = default!;

    public string Description { get; private set; } = string.Empty;

    public ProjectType ProjectType { get; private set; }

    public GeoRegion Region { get; private set; } = default!;

    public MeasurementUnit Unit { get; private set; } = default!;

    public ProductionYield ProductionYield { get; private set; } = default!;

    public IReadOnlyList<APUTemplateComponent> Components =>
        _components.OrderBy(component => component.SortOrder).ToList().AsReadOnly();

    public int UsageCount { get; private set; }

    public Confidence? AverageConfidence { get; private set; }

    public APUTemplateSource Source { get; private set; }

    public IReadOnlyList<float>? EmbeddingVector =>
        _embeddingVector is null ? null : Array.AsReadOnly(_embeddingVector);

    /// <summary>
    /// Creates an unpriced template for the specified output unit.
    /// </summary>
    public static Result<APUTemplate> Create(
        CanonicalConceptId canonicalConceptId,
        string description,
        ProjectType projectType,
        GeoRegion region,
        MeasurementUnit unit,
        ProductionYield productionYield,
        IEnumerable<APUTemplateComponent> components,
        APUTemplateSource source,
        Guid? ownerTenantId = null,
        Guid? ownerUserId = null)
    {
        ArgumentNullException.ThrowIfNull(canonicalConceptId);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(productionYield);
        ArgumentNullException.ThrowIfNull(components);

        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 500)
        {
            return ResultFactory.Failure<APUTemplate>(
                ApplicationError.Validation("KB_TEMPLATE_DESCRIPTION_INVALID", KnowledgeBaseMessages.Get("TemplateDescriptionInvalid")));
        }

        if (!Enum.IsDefined(projectType))
        {
            return ResultFactory.Failure<APUTemplate>(
                ApplicationError.Validation("KB_PROJECT_TYPE_INVALID", KnowledgeBaseMessages.Get("ProjectTypeInvalid")));
        }

        if (!Enum.IsDefined(source))
        {
            return ResultFactory.Failure<APUTemplate>(
                ApplicationError.Validation("KB_TEMPLATE_SOURCE_INVALID", KnowledgeBaseMessages.Get("TemplateSourceInvalid")));
        }

        if (ownerTenantId == Guid.Empty || ownerUserId == Guid.Empty)
        {
            return ResultFactory.Failure<APUTemplate>(
                ApplicationError.Validation("KB_OWNER_INVALID", KnowledgeBaseMessages.Get("OwnerIdInvalid")));
        }

        var items = components.ToList();
        if (items.Count == 0 || items.Any(item => item is null))
        {
            return ResultFactory.Failure<APUTemplate>(
                ApplicationError.Validation("KB_COMPONENTS_REQUIRED", KnowledgeBaseMessages.Get("ComponentsRequired")));
        }

        return ResultFactory.Success(new APUTemplate(
            APUTemplateId.New(), canonicalConceptId, description.Trim(), projectType,
            region, unit, productionYield, items, source, ownerTenantId, ownerUserId));
    }

    /// <summary>
    /// Adds an unpriced component to the template's structure.
    /// </summary>
    public Result AddComponent(APUTemplateComponent component, Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("KB_TEMPLATE_DELETED", KnowledgeBaseMessages.Get("EntityDeleted")));
        }

        _components.Add(component.WithSortOrder(_components.Count));
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    /// <summary>
    /// Records usage and updates confidence without storing a price.
    /// </summary>
    public Result RecordUsage(Confidence confidence, Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(confidence);

        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("KB_TEMPLATE_DELETED", KnowledgeBaseMessages.Get("EntityDeleted")));
        }

        if (UsageCount == int.MaxValue)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("KB_USAGE_COUNT_EXCEEDED", KnowledgeBaseMessages.Get("UsageCountExceeded")));
        }

        var newCount = UsageCount + 1;
        AverageConfidence = new Confidence(
            decimal.Round(
                ((AverageConfidence?.Value ?? 0m) * UsageCount + confidence.Value) / newCount,
                3,
                MidpointRounding.AwayFromZero));
        UsageCount = newCount;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    /// <summary>
    /// Stores a defensive copy of the template's semantic-search vector.
    /// </summary>
    public Result UpdateEmbedding(float[] vector, Guid? updatedBy = null)
    {
        ArgumentNullException.ThrowIfNull(vector);

        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("KB_TEMPLATE_DELETED", KnowledgeBaseMessages.Get("EntityDeleted")));
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