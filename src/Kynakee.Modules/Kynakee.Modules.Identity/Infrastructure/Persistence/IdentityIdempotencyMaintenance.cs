using Microsoft.EntityFrameworkCore;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityIdempotencyMaintenance(IdentityDbContext dbContext, TimeProvider timeProvider)
{
    /// <summary>Purga resultados caducados sin borrar claves protegidas ni fingerprints.</summary>
    public Task<int> PurgeExpiredOutcomesAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE schema_identity.idempotency_records SET encrypted_outcome = '\\x'::bytea, updated_at = {now.UtcDateTime} WHERE expires_at <= {now} AND octet_length(encrypted_outcome) > 0",
            cancellationToken);
    }
}
