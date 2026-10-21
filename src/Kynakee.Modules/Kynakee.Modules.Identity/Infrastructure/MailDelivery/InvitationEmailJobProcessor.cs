using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;

namespace Kynakee.Modules.Identity.Infrastructure.MailDelivery;

/// <summary>
/// Procesa trabajos pendientes de envío de invitaciones por correo electrónico: toma de forma exclusiva un trabajo
/// listo para ejecutar, valida la invitación, desencripta la carga, intenta el envío y marca el trabajo como completado
/// o lo reprograma para reintento según el resultado.
/// </summary>
/// <remarks>La ejecución procesa como máximo un trabajo por llamada dentro de una transacción y usa bloqueo por
/// fila (FOR UPDATE SKIP LOCKED). Aplica un tiempo de espera al transporte de envío (15 s), gestiona distintos errores
/// mapeándolos a códigos de estado (p. ej. TRANSPORT_TIMEOUT, SMTP_FAILED, CONFIGURATION_UNAVAILABLE,
/// PAYLOAD_UNAVAILABLE, PAYLOAD_INVALID) y evita borrado físico de entidades al completar o reintentar.</remarks>
/// <param name="dbContext">Contexto de EF Core del módulo de identidad para consultar y actualizar trabajos, invitaciones, usuarios y tenants.</param>
/// <param name="protector">Componente que desencripta y valida la carga cifrada de la invitación contenida en el trabajo.</param>
/// <param name="sender">Servicio encargado de enviar el correo electrónico de invitación.</param>
/// <param name="timeProvider">Proveedor de tiempo (UTC) utilizado para marcas temporales y cálculo de reintentos.</param>
public sealed class InvitationEmailJobProcessor(IdentityDbContext dbContext, InvitationEmailPayloadProtector protector,
    IInvitationEmailSender sender, TimeProvider timeProvider)
{
    /// <summary>Procesa como máximo un trabajo; el bloqueo dura hasta registrar entrega, retry o cancelación.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var jobs = await dbContext.InvitationEmailJobs.FromSqlInterpolated(
                $"SELECT *, xmin FROM schema_identity.invitation_email_jobs WHERE completed_at IS NULL AND NOT is_deleted AND (next_attempt_at <= {now} OR expires_at <= {now}) ORDER BY next_attempt_at, id LIMIT 1 FOR UPDATE SKIP LOCKED")
                .IgnoreQueryFilters().ToListAsync(cancellationToken).ConfigureAwait(false);
            var job = jobs.SingleOrDefault();
            if (job is null) return false;
            using var tenantScope = Serilog.Context.LogContext.PushProperty("TenantId", job.TenantId);
            using var userScope = Serilog.Context.LogContext.PushProperty("UserId", job.CreatedBy ?? Guid.Empty);
            using var correlationScope = Serilog.Context.LogContext.PushProperty("CorrelationId", job.Id.ToString("D"));
            var usable = await (from invitation in dbContext.UserInvitations.IgnoreQueryFilters()
                                join user in dbContext.Users.IgnoreQueryFilters() on invitation.UserId equals user.Id
                                join tenant in dbContext.Tenants.IgnoreQueryFilters() on invitation.TenantId equals tenant.Id
                                where invitation.Id == job.InvitationId && invitation.TenantId == job.TenantId && !invitation.IsDeleted
                                && invitation.AcceptedAt == null && invitation.RevokedAt == null && invitation.ExpiresAt > now
                                && user.TenantId == job.TenantId && !user.IsDeleted && user.Status == UserStatus.PendingVerification
                                && !tenant.IsDeleted && tenant.Status == TenantStatus.Active
                                select invitation.Id).AnyAsync(cancellationToken).ConfigureAwait(false);
            if (!usable || job.ExpiresAt <= now)
            {
                job.Complete(now, "INVITATION_UNAVAILABLE");
            }
            else
            {
                try
                {
                    var payload = protector.Unprotect(job.TenantId, job.InvitationId, job.EncryptedPayload);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(15));
                    await sender.SendInvitationAsync(payload.RecipientEmail, payload.TenantName, payload.InvitationToken, timeout.Token).ConfigureAwait(false);
                    job.Complete(timeProvider.GetUtcNow());
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    job.Retry(timeProvider.GetUtcNow(), "TRANSPORT_TIMEOUT");
                }
                catch (SmtpException) { job.Retry(timeProvider.GetUtcNow(), "SMTP_FAILED"); }
                catch (InvalidOperationException) { job.Retry(timeProvider.GetUtcNow(), "CONFIGURATION_UNAVAILABLE"); }
                catch (CryptographicException) { job.Retry(timeProvider.GetUtcNow(), "PAYLOAD_UNAVAILABLE"); }
                catch (JsonException) { job.Complete(timeProvider.GetUtcNow(), "PAYLOAD_INVALID"); }
            }
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }
}
