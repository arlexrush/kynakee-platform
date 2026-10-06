using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;

public sealed record RegisterTenantOwnerCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string Phone,
    string TenantName,
    TenantType TenantType,
    string TaxId,
    string TaxCountry,
    string PlanId,
    string Channel) : ICommand<IdentitySessionResponse>;