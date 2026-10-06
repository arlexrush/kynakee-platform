using System.Text.Json;
using Kynakee.Modules.Projects.Domain.Entities.Scoped;
using Kynakee.Modules.Projects.Domain.ValueObjects;

namespace Kynakee.Modules.Projects.Infrastructure.Persistence.Configurations;

internal static class ValuationSnapshotJson
{
    private static readonly JsonSerializerOptions Options =
        new(JsonSerializerDefaults.Web);

    public static string SerializeComponents(
        List<ValuedComponent> components) =>
        JsonSerializer.Serialize(
            components.Select(component => new ComponentSnapshot(
                component.ComponentId.Value,
                component.ComponentType,
                component.Description,
                component.Unit.Code,
                component.QuotedUnitPrice.Amount,
                component.QuotedUnitPrice.Currency,
                component.ComponentSubtotal.Amount,
                component.ComponentSubtotal.Currency,
                component.PricingSource,
                component.ProviderName,
                component.IsFallback,
                component.Confidence.Value)),
            Options);

    public static List<ValuedComponent> DeserializeComponents(string json)
    {
        var snapshots =
            JsonSerializer.Deserialize<List<ComponentSnapshot>>(json, Options)
            ?? [];

        return snapshots.Select(snapshot =>
            ValuedComponent.Restore(
                new APUComponentId(snapshot.ComponentId),
                snapshot.ComponentType,
                snapshot.Description,
                ResolveUnit(snapshot.UnitCode),
                new Money(
                    snapshot.QuotedAmount,
                    snapshot.QuotedCurrency),
                new Money(
                    snapshot.SubtotalAmount,
                    snapshot.SubtotalCurrency),
                snapshot.PricingSource,
                snapshot.ProviderName,
                snapshot.IsFallback,
                new Confidence(snapshot.Confidence)))
            .ToList();
    }

    public static string SerializeWorkItems(
        List<ValuedWorkItem> items) =>
        JsonSerializer.Serialize(items, Options);

    public static List<ValuedWorkItem> DeserializeWorkItems(string json) =>
        JsonSerializer.Deserialize<List<ValuedWorkItem>>(json, Options)
        ?? [];

    private static MeasurementUnit ResolveUnit(string code) =>
        code switch
        {
            "M2" => MeasurementUnit.SquareMeter,
            "M3" => MeasurementUnit.CubicMeter,
            "ML" => MeasurementUnit.LinearMeter,
            "UN" => MeasurementUnit.Unit,
            "KG" => MeasurementUnit.Kilogram,
            "TN" => MeasurementUnit.Ton,
            "HR" => MeasurementUnit.Hour,
            "DY" => MeasurementUnit.Day,
            _ => ResolveCompositeUnit(code)
        };

    private static MeasurementUnit ResolveCompositeUnit(string code)
    {
        var parts = code.Split(
            '/',
            StringSplitOptions.TrimEntries);

        if (parts.Length != 2)
        {
            throw new JsonException(
                $"Unknown measurement unit '{code}'.");
        }

        return MeasurementUnit.Composite(parts[0], parts[1]);
    }

    private sealed record ComponentSnapshot(
        Guid ComponentId,
        APUComponentType ComponentType,
        string Description,
        string UnitCode,
        decimal QuotedAmount,
        string QuotedCurrency,
        decimal SubtotalAmount,
        string SubtotalCurrency,
        PricingSource PricingSource,
        string? ProviderName,
        bool IsFallback,
        decimal Confidence);
}
