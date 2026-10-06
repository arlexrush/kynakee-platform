using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;

public sealed record UpdateTenantProfileCommand(
    Guid TenantId,
    string Name,
    string Slug,
    string? TaxId,
    string? TaxCountry,
    string FiscalCountry,
    string? Region,
    string? Province,
    string? Municipality,
    string? PostalCode,
    string? Street,
    string PlanId) : ICommand;