using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Microsoft.AspNetCore.Identity;
using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Modules.Identity.Infrastructure;

[SuppressMessage("Performance", "CA1812", Justification = "Created by the ASP.NET Core Identity dependency injection container.")]
internal sealed class IdentityUserStore : IUserPasswordStore<User>, IUserEmailStore<User>
{
    private readonly IIdentityRepository _repository;

    public IdentityUserStore(IIdentityRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public void Dispose()
    {
    }

    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id.ToString("D"));

    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Email.Value);

    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        return string.Equals(user.Email.Value, userName, StringComparison.OrdinalIgnoreCase)
            ? Task.CompletedTask
            : Task.FromException(new NotSupportedException("The Identity username is the immutable email address."));
    }

    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Email.Value);

    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        if (await _repository.FindUserByEmailAcrossTenantsAsync(user.Email.Value, cancellationToken)
                .ConfigureAwait(false) is not null)
        {
            return IdentityResult.Failed(new IdentityError { Code = "DuplicateEmail", Description = "Email already exists." });
        }

        _repository.AddUser(user);
        return IdentityResult.Success;
    }

    public Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(IdentityResult.Success);
    }

    public Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(IdentityResult.Failed(new IdentityError
        {
            Code = "SoftDeleteRequired",
            Description = "Identity user deletion must be performed through the domain soft-delete operation."
        }));
    }

    public Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Guid.TryParse(userId, out var id)
            ? _repository.FindUserForAuthenticationAsync(id, cancellationToken)
            : Task.FromResult<User?>(null);

    public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        _repository.FindUserByNormalizedEmailAsync(normalizedUserName, cancellationToken);

    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        var result = user.SetPasswordHash(passwordHash);
        return result.IsSuccess
            ? Task.CompletedTask
            : Task.FromException(new InvalidOperationException(result.Error!.Message));
    }

    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(!string.IsNullOrWhiteSpace(user.PasswordHash));

    public Task SetEmailAsync(User user, string? email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        return string.Equals(user.Email.Value, email, StringComparison.OrdinalIgnoreCase)
            ? Task.CompletedTask
            : Task.FromException(new NotSupportedException("Changing an identity email is not supported through UserManager."));
    }

    public Task<string?> GetEmailAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Email.Value);

    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Status == UserStatus.Active);

    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException("Email confirmation is managed by the User aggregate."));

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        _repository.FindUserByNormalizedEmailAsync(normalizedEmail, cancellationToken);

    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.Email.Value);

    public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}