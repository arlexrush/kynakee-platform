namespace Kynakee.Modules.Projects.Domain.Entities.DataContext
{
    public sealed record EconomicContext(
    decimal? InflationRate,
    decimal? VatRate,
    decimal? ConstructionIndex);
}
