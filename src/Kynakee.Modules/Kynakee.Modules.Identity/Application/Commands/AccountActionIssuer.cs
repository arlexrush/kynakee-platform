using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands;

internal sealed class AccountActionIssuer(IAccountActionRepository repository, IIdentityTokenService tokens,
    IAccountEmailJobStore emails, TimeProvider timeProvider)
{
    internal async Task<Result> IssueAsync(User user, AccountActionPurpose purpose, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var existing = await repository.ListAsync(user.TenantId, user.Id, purpose, cancellationToken).ConfigureAwait(false);
        if (existing.Any(action => action.IssuedAt > now.AddMinutes(-1))) return ResultFactory.Ok();
        foreach (var action in existing.Where(action => action.ConsumedAt is null && action.RevokedAt is null))
        {
            var revoked = action.Revoke(now);
            if (revoked.IsFailure) return revoked;
        }
        var plaintext = tokens.GenerateOpaqueToken();
        var created = AccountActionToken.Create(user.TenantId, user.Id, tokens.HashToken(plaintext), purpose, now,
            purpose == AccountActionPurpose.VerifyEmail ? now.AddHours(24) : now.AddMinutes(30));
        if (created.IsFailure) return ResultFactory.Failure(created.Error!);
        var token = created.Value!;
        repository.Add(token);
        emails.Enqueue(token, user.Email.Value, plaintext);
        return ResultFactory.Ok();
    }
}
