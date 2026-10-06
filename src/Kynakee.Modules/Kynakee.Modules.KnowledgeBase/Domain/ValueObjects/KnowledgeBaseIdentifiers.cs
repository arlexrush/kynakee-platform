using Kynakee.Modules.KnowledgeBase.Domain.Resources;
using System.Text.RegularExpressions;

namespace Kynakee.Modules.KnowledgeBase.Domain.ValueObjects;

public readonly record struct APUTemplateId(Guid Value)
{
    public static APUTemplateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

public sealed record CanonicalConceptId
{
    private static readonly Regex Format = new(
        "^[A-Z0-9_]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Value { get; }

    public CanonicalConceptId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(KnowledgeBaseMessages.Get("ConceptIdRequired"), nameof(value));
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > 100 || !Format.IsMatch(normalized))
        {
            throw new ArgumentException(KnowledgeBaseMessages.Get("ConceptIdInvalid"), nameof(value));
        }

        Value = normalized;
    }

    public override string ToString() => Value;
}