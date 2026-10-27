namespace Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;

/// <summary>Identifies fiscal fields explicitly supplied in a profile update.</summary>
[Flags]
public enum FiscalProfileFields
{
    None = 0,
    TaxId = 1,
    TaxCountry = 2,
    FiscalCountry = 4,
    Region = 8,
    Province = 16,
    Municipality = 32,
    PostalCode = 64,
    Street = 128,
    All = TaxId | TaxCountry | FiscalCountry | Region | Province | Municipality | PostalCode | Street
}
