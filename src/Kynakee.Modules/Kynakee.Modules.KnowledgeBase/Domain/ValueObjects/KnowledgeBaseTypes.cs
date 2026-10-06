using Kynakee.Modules.KnowledgeBase.Domain.Resources;
using System.Text.RegularExpressions;

namespace Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;

public enum ProjectType
{
    Residential,
    Commercial,
    Industrial,
    Infrastructure
}

public enum APUComponentType
{
    Material,
    Labor,
    Equipment,
    Subcontract,
    Transport
}

public enum APUTemplateSource
{
    GeneratedByAI,
    ValidatedByHuman,
    ImportedFromExternal
}

public sealed record GeoRegion
{
    private static readonly Regex Format = new(
        "^[A-Z]{2}-[A-Z0-9]{1,7}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Code { get; }

    public GeoRegion(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(KnowledgeBaseMessages.Get("RegionRequired"), nameof(code));
        }

        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length > 10 || !Format.IsMatch(normalized))
        {
            throw new ArgumentException(KnowledgeBaseMessages.Get("RegionInvalid"), nameof(code));
        }

        Code = normalized;
    }

    public override string ToString() => Code;
}

public sealed record MeasurementUnit
{
    private static readonly Regex Format = new(
        "^[A-Z0-9]+(?:/[A-Z0-9]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Code { get; }

    public MeasurementUnit(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(KnowledgeBaseMessages.Get("UnitRequired"), nameof(code));
        }

        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length > 20 || !Format.IsMatch(normalized))
        {
            throw new ArgumentException(KnowledgeBaseMessages.Get("UnitInvalid"), nameof(code));
        }

        Code = normalized;
    }

    public override string ToString() => Code;
}

public sealed record Confidence
{
    public decimal Value { get; }

    public Confidence(decimal value)
    {
        if (value is < 0m or > 1m || decimal.Round(value, 3) != value)
        {
            throw new ArgumentOutOfRangeException(nameof(value), KnowledgeBaseMessages.Get("ConfidenceInvalid"));
        }

        Value = value;
    }
}