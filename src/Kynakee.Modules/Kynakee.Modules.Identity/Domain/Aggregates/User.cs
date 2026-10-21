using Kynakee.Modules.Identity.Domain.Events;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Domain;

namespace Kynakee.Modules.Identity.Domain.Aggregates;

public sealed class User : AggregateRoot<Guid>
{
    private static readonly System.Resources.ResourceManager AccountResources = new(
        "Kynakee.Modules.Identity.Domain.AccountActionResources", typeof(User).Assembly);

    private User()
    {
    }

    private User(
        Guid id,
        Guid tenantId,
        string firstName,
        string lastName,
        Email email,
        PhoneNumber? phone,
        string? passwordHash,
        UserStatus status,
        Guid? createdBy)
        : base(id, tenantId, createdBy)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Phone = phone;
        PasswordHash = passwordHash;
        Status = status;
    }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public Email Email { get; private set; } = default!;

    public PhoneNumber? Phone { get; private set; }

    public string? PasswordHash { get; private set; }

    public Guid CredentialVersion { get; private set; } = Guid.NewGuid();

    public UserStatus Status { get; private set; }

    public static Result<User> Create(
        Guid tenantId,
        string? firstName,
        string? lastName,
        Email? email,
        PhoneNumber? phone,
        string? passwordHash,
        UserStatus status = UserStatus.Active,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty)
        {
            return ResultFactory.Failure<User>(
                ApplicationError.Validation("IDENTITY_USER_TENANT_REQUIRED", "User tenant is required."));
        }

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return ResultFactory.Failure<User>(
                ApplicationError.Validation("IDENTITY_USER_NAME_REQUIRED", "First and last name are required."));
        }

        if (email is null)
        {
            return ResultFactory.Failure<User>(
                ApplicationError.Validation("IDENTITY_EMAIL_REQUIRED", "User email is required."));
        }

        if (!Enum.IsDefined(status))
        {
            return ResultFactory.Failure<User>(
                ApplicationError.Validation("IDENTITY_USER_STATUS_INVALID", "User status is invalid."));
        }

        if (status == UserStatus.Active && string.IsNullOrWhiteSpace(passwordHash))
        {
            return ResultFactory.Failure<User>(
                ApplicationError.Validation("IDENTITY_PASSWORD_HASH_REQUIRED", "An active user must have a password hash."));
        }

        if (status != UserStatus.Active && !string.IsNullOrWhiteSpace(passwordHash))
        {
            return ResultFactory.Failure<User>(
                ApplicationError.Validation("IDENTITY_PASSWORD_HASH_STATE_INVALID", "Only active users can have a password hash."));
        }

        var user = new User(
            Guid.NewGuid(),
            tenantId,
            firstName.Trim(),
            lastName.Trim(),
            email,
            phone,
            passwordHash,
            status,
            createdBy);

        if (status == UserStatus.PendingVerification)
        {
            user.AddDomainEvent(new UserInvitedDomainEvent(user.Id, tenantId, email.Value));
        }
        else if (status == UserStatus.Active)
        {
            user.AddDomainEvent(new UserRegisteredDomainEvent(user.Id, tenantId, email.Value));
        }

        return ResultFactory.Success(user);
    }

    public Result UpdateProfile(
        string? firstName,
        string? lastName,
        PhoneNumber? phone,
        Guid? updatedBy = null)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation("IDENTITY_USER_NAME_REQUIRED", "First and last name are required."));
        }

        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_USER_DELETED", "A deleted user cannot be changed."));
        }

        if (Status == UserStatus.Inactive)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_USER_INACTIVE", "An inactive user cannot be changed."));
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Phone = phone;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    public Result SetPasswordHash(string? passwordHash, Guid? updatedBy = null)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation("IDENTITY_PASSWORD_HASH_REQUIRED", "Password hash is required."));
        }

        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_USER_DELETED", "A deleted user cannot be changed."));
        }

        PasswordHash = passwordHash;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    public Result AcceptInvitation(string? passwordHash, Guid? updatedBy = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_USER_DELETED", "A deleted user cannot be activated."));
        }

        if (Status != UserStatus.PendingVerification)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_INVITATION_NOT_PENDING", "User does not have a pending invitation."));
        }

        var passwordResult = SetPasswordHash(passwordHash, updatedBy);
        if (passwordResult.IsFailure)
        {
            return passwordResult;
        }

        Status = UserStatus.Active;
        RegisterUpdate(updatedBy);
        AddDomainEvent(new UserRegisteredDomainEvent(Id, TenantId, Email.Value));
        return ResultFactory.Ok();
    }

    /// <summary>Prepares a newly created owner for mailbox verification without raising an invitation event.</summary>
    public Result PrepareRegistration()
    {
        if (IsDeleted || Status != UserStatus.Inactive || string.IsNullOrWhiteSpace(PasswordHash))
            return AccountActionUnavailable();

        Status = UserStatus.PendingVerification;
        RegisterUpdate(Id);
        return ResultFactory.Ok();
    }

    /// <summary>Activates only a pending registration whose mailbox credential was consumed by the application transaction.</summary>
    public Result ConfirmRegistration(Guid? updatedBy = null)
    {
        if (IsDeleted || Status != UserStatus.PendingVerification || string.IsNullOrWhiteSpace(PasswordHash))
            return AccountActionUnavailable();
        Status = UserStatus.Active;
        RegisterUpdate(updatedBy);
        AddDomainEvent(new UserRegisteredDomainEvent(Id, TenantId, Email.Value));
        return ResultFactory.Ok();
    }

    /// <summary>Replaces credentials only for an active account; token consumption is coordinated by the application.</summary>
    public Result RecoverPassword(string? passwordHash)
    {
        if (IsDeleted || Status != UserStatus.Active)
            return AccountActionUnavailable();
        var changed = SetPasswordHash(passwordHash, Id);
        if (changed.IsSuccess) CredentialVersion = Guid.NewGuid();
        return changed;
    }

    public Result Activate(Guid? updatedBy = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_USER_DELETED", "A deleted user cannot be activated."));
        }

        if (Status != UserStatus.Inactive)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_USER_NOT_INACTIVE", "Only an inactive user can be activated."));
        }

        if (string.IsNullOrWhiteSpace(PasswordHash))
        {
            return ResultFactory.Failure(
                ApplicationError.Validation("IDENTITY_PASSWORD_HASH_REQUIRED", "An active user must have a password hash."));
        }

        Status = UserStatus.Active;
        RegisterUpdate(updatedBy);
        AddDomainEvent(new UserRegisteredDomainEvent(Id, TenantId, Email.Value));
        return ResultFactory.Ok();
    }

    public Result Deactivate(Guid? updatedBy = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_USER_DELETED", "A deleted user cannot be changed."));
        }

        if (Status == UserStatus.Inactive)
        {
            return ResultFactory.Ok();
        }

        Status = UserStatus.Inactive;
        RegisterUpdate(updatedBy);
        return ResultFactory.Ok();
    }

    private static Result AccountActionUnavailable() => ResultFactory.Failure(ApplicationError.Unauthorized(
        "IDENTITY_ACCOUNT_TOKEN_UNAVAILABLE", AccountResources.GetString("Unavailable",
            System.Globalization.CultureInfo.CurrentUICulture) ?? "Unavailable"));

    public Result DeleteUser(Guid? deletedBy = null)
    {
        if (IsDeleted)
        {
            return ResultFactory.Failure(
                ApplicationError.Conflict("IDENTITY_USER_DELETED", "User is already deleted."));
        }

        Delete(deletedBy);
        return ResultFactory.Ok();
    }
}