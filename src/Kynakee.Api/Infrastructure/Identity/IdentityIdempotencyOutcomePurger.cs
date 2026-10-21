using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Api.Infrastructure.Identity;

[SuppressMessage("Performance", "CA1812", Justification = "Created by the hosted service container.")]
internal sealed class IdentityIdempotencyOutcomePurger(IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
    ILogger<IdentityIdempotencyOutcomePurger> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogPurgeFailure = LoggerMessage.Define(
        LogLevel.Error, new EventId(6101, "IdentityOutcomePurgeFailed"),
        "Identity outcome purge failed; retained outcomes will be retried on the next cycle.");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            try
            {
                await new IdentityIdempotencyMaintenance(db, timeProvider).PurgeExpiredOutcomesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Npgsql.NpgsqlException exception)
            {
                using (Serilog.Context.LogContext.PushProperty("TenantId", Guid.Empty))
                using (Serilog.Context.LogContext.PushProperty("UserId", Guid.Empty))
                using (Serilog.Context.LogContext.PushProperty("CorrelationId", "identity-outcome-purge"))
                {
                    LogPurgeFailure(logger, exception);
                }
            }
        }
    }
}
