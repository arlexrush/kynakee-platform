using Kynakee.Modules.Identity.Infrastructure.MailDelivery;
using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Api.Infrastructure.Identity;

[SuppressMessage("Performance", "CA1812", Justification = "Created by the hosted service container.")]

/// <summary>
/// Worker en segundo plano que procesa trabajos de envío de correos electrónicos de invitación.
/// </summary>
internal sealed class InvitationEmailWorker(IServiceScopeFactory scopeFactory, ILogger<InvitationEmailWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogFailure = LoggerMessage.Define(LogLevel.Error,
        new EventId(6102, "InvitationEmailWorkerFailed"), "Invitation email worker failed; pending work remains durable.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            try
            {
                var worked = await scope.ServiceProvider.GetRequiredService<InvitationEmailJobProcessor>()
                    .ProcessNextAsync(stoppingToken).ConfigureAwait(false);
                var accountWorked = await scope.ServiceProvider.GetRequiredService<AccountEmailJobProcessor>()
                    .ProcessNextAsync(stoppingToken).ConfigureAwait(false);
                if (!worked && !accountWorked) await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
            catch (Npgsql.NpgsqlException exception)
            {
                using (Serilog.Context.LogContext.PushProperty("TenantId", Guid.Empty))
                using (Serilog.Context.LogContext.PushProperty("UserId", Guid.Empty))
                using (Serilog.Context.LogContext.PushProperty("CorrelationId", "invitation-email-worker"))
                {
                    LogFailure(logger, exception);
                }
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
