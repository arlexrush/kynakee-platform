using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;

namespace Kynakee.Modules.Identity.Application.Contracts;

public interface IIdentityTokenService
{
    string CreateAccessToken(User user, Tenant tenant, TenantUser membership);

    string GenerateOpaqueToken();

    string HashToken(string token);
}