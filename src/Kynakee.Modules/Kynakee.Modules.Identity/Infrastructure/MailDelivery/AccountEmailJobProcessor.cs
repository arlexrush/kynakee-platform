using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace Kynakee.Modules.Identity.Infrastructure.MailDelivery;

/// <summary>Claims delivery and records local outcome atomically; SMTP itself is not exactly once.</summary>
public sealed class AccountEmailJobProcessor(IdentityDbContext context, AccountEmailPayloadProtector protector,
    IAccountEmailSender sender, TimeProvider timeProvider)
{
    /// <summary>Processes at most one job, rechecking account and token state before sending.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var jobs = await context.Set<AccountEmailJob>().FromSqlInterpolated(
                $"SELECT *, xmin FROM schema_identity.account_email_jobs WHERE completed_at IS NULL AND NOT is_deleted AND (next_attempt_at <= {now} OR expires_at <= {now}) ORDER BY next_attempt_at, id LIMIT 1 FOR UPDATE SKIP LOCKED")
                .IgnoreQueryFilters().ToListAsync(cancellationToken).ConfigureAwait(false);
            var job = jobs.SingleOrDefault();
            if (job is null) return false;
            using var tenantScope = Serilog.Context.LogContext.PushProperty("TenantId", job.TenantId);
            using var userScope = Serilog.Context.LogContext.PushProperty("UserId", job.CreatedBy ?? Guid.Empty);
            using var correlationScope = Serilog.Context.LogContext.PushProperty("CorrelationId", job.Id.ToString("D"));
            var action = await context.AccountActionTokens.IgnoreQueryFilters()
                .SingleOrDefaultAsync(token => token.Id == job.TokenId && token.TenantId == job.TenantId, cancellationToken).ConfigureAwait(false);
            var user = action is null ? null : await context.Users.IgnoreQueryFilters()
                .SingleOrDefaultAsync(candidate => candidate.Id == action.UserId && candidate.TenantId == job.TenantId, cancellationToken).ConfigureAwait(false);
            var tenantActive = await context.Tenants.IgnoreQueryFilters()
                .AnyAsync(tenant => tenant.Id == job.TenantId && !tenant.IsDeleted && tenant.Status == TenantStatus.Active, cancellationToken).ConfigureAwait(false);
            var status = action?.Purpose == Domain.Entities.AccountActionPurpose.VerifyEmail ? UserStatus.PendingVerification : UserStatus.Active;
            if (action is null || user is null || user.IsDeleted || user.Status != status || !tenantActive ||
                !action.IsUsableAt(action.Purpose, now) || job.ExpiresAt <= now)
            {
                job.Complete(now, "ACTION_UNAVAILABLE");
            }
            else
            {
                try
                {
                    var payload = protector.Unprotect(job.TenantId, job.TokenId, job.EncryptedPayload);
                    if (string.IsNullOrWhiteSpace(payload.Token) || string.IsNullOrWhiteSpace(payload.RecipientEmail) ||
                        !Enum.IsDefined(payload.Purpose))
                        throw new JsonException(AccountEmailMessages.Get("PayloadInvalid"));
                    var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload.Token)));
                    if (payload.Purpose != action.Purpose || !string.Equals(payload.RecipientEmail, user.Email.Value, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(digest, action.TokenHash, StringComparison.Ordinal))
                        throw new CryptographicException(AccountEmailMessages.Get("PayloadInvalid"));
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(15));
                    await sender.SendAsync(payload.RecipientEmail, payload.Token, payload.Purpose, action.ExpiresAt, timeout.Token).ConfigureAwait(false);
                    job.Complete(timeProvider.GetUtcNow());
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { job.Retry(timeProvider.GetUtcNow(), "TRANSPORT_TIMEOUT"); }
                catch (SmtpException) { job.Retry(timeProvider.GetUtcNow(), "SMTP_FAILED"); }
                catch (InvalidOperationException) { job.Retry(timeProvider.GetUtcNow(), "CONFIGURATION_UNAVAILABLE"); }
                catch (CryptographicException) { job.Complete(timeProvider.GetUtcNow(), "PAYLOAD_UNAVAILABLE"); }
                catch (JsonException) { job.Complete(timeProvider.GetUtcNow(), "PAYLOAD_INVALID"); }
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }
}
