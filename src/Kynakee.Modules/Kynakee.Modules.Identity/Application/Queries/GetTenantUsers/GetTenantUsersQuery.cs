using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Queries.GetTenantUsers;

public sealed record GetTenantUsersQuery(Guid TenantId, Guid RequestingUserId) : IQuery<IReadOnlyList<TenantUserListItem>>;