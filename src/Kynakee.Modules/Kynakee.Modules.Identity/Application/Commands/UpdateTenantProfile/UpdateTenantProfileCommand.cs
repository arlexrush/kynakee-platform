using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;

public sealed record UpdateTenantProfileCommand(
    Guid TenantId,
    string Name,
    string Slug,
    string? TaxId,
    string? TaxCountry,
    string? FiscalCountry,
    string? Region,
    string? Province,
    string? Municipality,
    string? PostalCode,
    string? Street,
    string PlanId,
    uint ExpectedVersion) : ICommand
{
    /// <summary>Fields supplied by the caller; unspecified fiscal fields retain their current values.</summary>
    public FiscalProfileFields SuppliedFiscalFields { get; init; } = FiscalProfileFields.All;
}
