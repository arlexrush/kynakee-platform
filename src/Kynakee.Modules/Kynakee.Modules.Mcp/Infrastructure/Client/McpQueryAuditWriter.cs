using System.Diagnostics.CodeAnalysis;
using System.Transactions;
using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.Mcp.Domain.ValueObjects;
using Kynakee.Modules.Mcp.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Mcp.Infrastructure.Client;

[SuppressMessage("Performance", "CA1812", Justification = "Created by the module dependency registration.")]
internal sealed class McpQueryAuditWriter(DbContextOptions<McpDbContext> options, ITenantContext tenantContext)
{
    internal async Task WriteAsync(
        McpProviderId providerId, IReadOnlyCollection<McpQueryLog> logs, bool? succeeded,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        cancellationToken = timeout.Token;
        // Audit must survive rollback of the command that requested the quote.
        using var ambient = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
        var context = new McpDbContext(options, tenantContext);
        await using var contextLifetime = context.ConfigureAwait(false);
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var transactionLifetime = transaction.ConfigureAwait(false);
        if (succeeded.HasValue)
        {
            // Serialize health updates across callers; aggregate xmin alone would lose competing outcomes.
            var providers = await context.Providers.FromSqlInterpolated(
                    $"SELECT p.*, p.xmin FROM schema_mcp.mcp_providers AS p WHERE id = {providerId.Value} AND NOT is_deleted FOR UPDATE")
                .IgnoreQueryFilters().ToListAsync(cancellationToken).ConfigureAwait(false);
            var provider = providers.SingleOrDefault();
            if (provider is not null)
            {
                var health = succeeded.Value ? provider.RecordSuccess() : provider.RecordFailure();
                if (health.IsFailure)
                {
                    throw new InvalidOperationException(health.Error!.Code);
                }
            }
        }

        await context.QueryLogs.AddRangeAsync(logs, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        ambient.Complete();
    }
}
