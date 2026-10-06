using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Queries.GetCurrentIdentity;

public sealed record GetCurrentIdentityQuery(Guid TenantId, Guid UserId) : IQuery<IdentitySessionProfile>;

public sealed record IdentitySessionProfile(IdentityUserProfile User, IdentityTenantProfile Tenant);