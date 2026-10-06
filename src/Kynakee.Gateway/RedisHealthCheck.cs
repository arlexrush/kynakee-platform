using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Kynakee.Gateway;

[SuppressMessage("Performance", "CA1812", Justification = "Created by the health check service.")]
internal sealed class RedisHealthCheck(RedisConnectionFactory redisConnectionFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await redisConnectionFactory.PingAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (RedisException)
        {
            return HealthCheckResult.Unhealthy("Redis is unavailable.");
        }
    }
}