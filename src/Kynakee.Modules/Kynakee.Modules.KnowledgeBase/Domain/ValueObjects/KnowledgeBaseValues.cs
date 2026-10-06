using Kynakee.Modules.KnowledgeBase.Domain.Resources;
using System.Text.RegularExpressions;

namespace Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;

public sealed record ConceptTranslation
{
    private static readonly Regex LanguageFormat = new(
        "^[A-Z]{2}(?:-[A-Z]{2})?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string LanguageCode { get; }
    public string Name { get; }
    public string? Description { get; }

    public ConceptTranslation(string languageCode, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(languageCode) ||
            !LanguageFormat.IsMatch(languageCode.Trim().ToUpperInvariant()))
        {
            throw new ArgumentException(KnowledgeBaseMessages.Get("LanguageInvalid"), nameof(languageCode));
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 300)
        {
            throw new ArgumentException(KnowledgeBaseMessages.Get("TranslationNameInvalid"), nameof(name));
        }

        LanguageCode = languageCode.Trim().ToUpperInvariant();
        Name = name.Trim();
        Description = description?.Trim();
    }
}

public sealed record ProductionYield
{
    public decimal HoursPerUnit { get; }
    public string? CrewDescription { get; }

    public ProductionYield(decimal hoursPerUnit, string? crewDescription)
    {
        if (hoursPerUnit is <= 0m or > 9999.9999m ||
            decimal.Round(hoursPerUnit, 4) != hoursPerUnit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(hoursPerUnit), KnowledgeBaseMessages.Get("HoursPerUnitInvalid"));
        }

        if (crewDescription?.Length > 200)
        {
            throw new ArgumentException(KnowledgeBaseMessages.Get("CrewDescriptionInvalid"), nameof(crewDescription));
        }

        HoursPerUnit = hoursPerUnit;
        CrewDescription = crewDescription?.Trim();
    }
}

public sealed record APUTemplateComponent
{
    private APUTemplateComponent() { }

    public string Description { get; private set; } = string.Empty;
    public APUComponentType Type { get; private set; }
    public MeasurementUnit Unit { get; private set; } = default!;
    public decimal Yield { get; private set; }
    public int SortOrder { get; private set; }

    public APUTemplateComponent(
        string description,
        APUComponentType type,
        MeasurementUnit unit,
        decimal yield)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 300)
        {
            throw new ArgumentException(KnowledgeBaseMessages.Get("ComponentDescriptionInvalid"), nameof(description));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), KnowledgeBaseMessages.Get("ComponentTypeInvalid"));
        }

        ArgumentNullException.ThrowIfNull(unit);

        if (yield is <= 0m or > 9999.999999m ||
            decimal.Round(yield, 6) != yield)
        {
            throw new ArgumentOutOfRangeException(nameof(yield), KnowledgeBaseMessages.Get("ComponentYieldInvalid"));
        }

        Description = description.Trim();
        Type = type;
        Unit = unit;
        Yield = yield;
    }

    internal APUTemplateComponent WithSortOrder(int sortOrder) =>
        this with { SortOrder = sortOrder };
}