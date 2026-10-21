using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Queries.GetTenantUsers;

/// <summary>
/// Maneja la consulta GetTenantUsersQuery y devuelve una lista de TenantUserListItem del tenant solicitado, aplicando
/// las comprobaciones de autenticación y autorización.
/// </summary>
/// <remarks>Valida que el solicitante esté autenticado y que TenantId y UserId coincidan con el contexto del
/// tenant; sólo permite el acceso a usuarios con rol Owner o Admin. Recupera la membresía y los usuarios mediante
/// IIdentityRepository y retorna un Result<IReadOnlyList<TenantUserListItem>> con el resultado o un error de
/// autorización.</remarks>
public sealed class GetTenantUsersQueryHandler
    : IRequestHandler<GetTenantUsersQuery, Result<IReadOnlyList<TenantUserListItem>>>
{
    private readonly IIdentityRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetTenantUsersQueryHandler(IIdentityRepository repository, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tenantContext);
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<TenantUserListItem>>> Handle(
        GetTenantUsersQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_tenantContext.IsAuthenticated || request.TenantId != _tenantContext.TenantId ||
            request.RequestingUserId != _tenantContext.UserId)
        {
            return ResultFactory.Failure<IReadOnlyList<TenantUserListItem>>(ApplicationError.Unauthorized(
                "IDENTITY_TENANT_USERS_FORBIDDEN", "The current identity cannot access tenant users."));
        }

        if (!await IdentityAccess.HasActiveMembershipAsync(
                _repository, request.TenantId, request.RequestingUserId, cancellationToken, UserRole.Owner, UserRole.Admin)
                .ConfigureAwait(false))
        {
            return ResultFactory.Failure<IReadOnlyList<TenantUserListItem>>(ApplicationError.Unauthorized(
                "IDENTITY_TENANT_USERS_FORBIDDEN", "Only tenant owners and administrators can view tenant users."));
        }

        var users = await _repository.GetTenantUsersAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false);
        return ResultFactory.Success(users);
    }
}