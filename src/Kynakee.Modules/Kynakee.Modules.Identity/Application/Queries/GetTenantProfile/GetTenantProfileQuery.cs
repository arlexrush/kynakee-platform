using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Queries.GetTenantProfile;

public sealed record GetTenantProfileQuery(Guid TenantId, Guid RequestingUserId) : IQuery<IdentityTenantProfile>;