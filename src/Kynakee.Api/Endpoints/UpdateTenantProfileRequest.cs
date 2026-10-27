using Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;

namespace Kynakee.Api.Endpoints;

#pragma warning disable CA1812 // Instantiated by ASP.NET Core JSON binding.
internal sealed class UpdateTenantProfileRequest
{
    private FiscalProfileFields _suppliedFiscalFields;
    private string? _taxId, _taxCountry, _fiscalCountry, _region, _province, _municipality, _postalCode, _street;

    public string Name { get; init; } = null!;
    public string Slug { get; init; } = null!;
    public string? TaxId { get => _taxId; init { _taxId = value; _suppliedFiscalFields |= FiscalProfileFields.TaxId; } }
    public string? TaxCountry { get => _taxCountry; init { _taxCountry = value; _suppliedFiscalFields |= FiscalProfileFields.TaxCountry; } }
    public string? FiscalCountry { get => _fiscalCountry; init { _fiscalCountry = value; _suppliedFiscalFields |= FiscalProfileFields.FiscalCountry; } }
    public string? Region { get => _region; init { _region = value; _suppliedFiscalFields |= FiscalProfileFields.Region; } }
    public string? Province { get => _province; init { _province = value; _suppliedFiscalFields |= FiscalProfileFields.Province; } }
    public string? Municipality { get => _municipality; init { _municipality = value; _suppliedFiscalFields |= FiscalProfileFields.Municipality; } }
    public string? PostalCode { get => _postalCode; init { _postalCode = value; _suppliedFiscalFields |= FiscalProfileFields.PostalCode; } }
    public string? Street { get => _street; init { _street = value; _suppliedFiscalFields |= FiscalProfileFields.Street; } }
    public string PlanId { get; init; } = null!;
    internal FiscalProfileFields SuppliedFiscalFields => _suppliedFiscalFields;
}
#pragma warning restore CA1812
