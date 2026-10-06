using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Identity.Domain.Entities;

public sealed class TenantUser : BaseEntity<Guid>
{
    private TenantUser()
    {
    }

    private TenantUser(Guid id, User user, UserRole role, Guid? createdBy)
        : base(id, user.TenantId, createdBy)
    {
        UserId = user.Id;
        Role = role;
    }

    public Guid UserId { get; private set; }

    public UserRole Role { get; private set; }

    public static Result<TenantUser> Create(
        User? user,
        UserRole role,
        Guid? createdBy = null)
    {
        if (user is null || user.Id == Guid.Empty || user.TenantId == Guid.Empty)
        {
            return ResultFactory.Failure<TenantUser>(
                ApplicationError.Validation("IDENTITY_TENANT_USER_REQUIRED", "A valid user is required for tenant membership."));
        }

        if (!Enum.IsDefined(role))
        {
            return ResultFactory.Failure<TenantUser>(
                ApplicationError.Validation("IDENTITY_USER_ROLE_INVALID", "User role is invalid."));
        }

        return ResultFactory.Success(new TenantUser(Guid.NewGuid(), user, role, createdBy));
    }

    public Result ChangeRole(UserRole role, Guid? updatedBy = null)
    {
        if (!Enum.IsDefined(role))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation("IDENTITY_USER_ROLE_INVALID", "User role is invalid."));
        }

        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_TENANT_USER_DELETED", "A deleted tenant membership cannot be changed."));
        }

        if (Role == role)
        {
            return ResultFactory.Ok();
        }

        Role = role;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    public Result DeleteMembership(Guid? deletedBy = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_TENANT_USER_DELETED", "Membership is already deleted."));
        }

        Delete(deletedBy);
        return ResultFactory.Ok();
    }
}