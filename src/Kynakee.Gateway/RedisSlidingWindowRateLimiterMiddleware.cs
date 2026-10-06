using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Serilog.Context;
using StackExchange.Redis;

namespace Kynakee.Gateway;

[SuppressMessage("Performance", "CA1812", Justification = "Created by ASP.NET Core middleware activation.")]
internal sealed partial class RedisSlidingWindowRateLimiterMiddleware(
    RequestDelegate next,
    RedisConnectionFactory redisConnectionFactory,
    IOptions<RedisRateLimitOptions> options,
    ILogger<RedisSlidingWindowRateLimiterMiddleware> logger)
{
    private const string SlidingWindowScript = """
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1] - ARGV[2])
        local count = redis.call('ZCARD', KEYS[1])
        if count < tonumber(ARGV[3]) then
            redis.call('ZADD', KEYS[1], ARGV[1], ARGV[4])
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
            return {1, 0}
        end
        local oldest = redis.call('ZRANGE', KEYS[1], 0, 0, 'WITHSCORES')
        return {0, math.max(1, tonumber(oldest[2]) + tonumber(ARGV[2]) - tonumber(ARGV[1]))}
        """;

    private readonly TimeSpan _window = TimeSpan.FromSeconds(options.Value.WindowSeconds);
    private readonly int _permitLimit = options.Value.PermitLimit;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (path is "/health" or "/health/ready" or "/health/live" ||
            context.User.Identity?.IsAuthenticated != true)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var userId = context.User.FindFirst("sub")?.Value ?? "unknown";
        var correlationId = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        using var tenantProperty = LogContext.PushProperty("TenantId", tenantId);
        using var userProperty = LogContext.PushProperty("UserId", userId);
        using var correlationProperty = LogContext.PushProperty("CorrelationId", correlationId);

        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var database = redisConnectionFactory.GetDatabase();
            var result = await database.ScriptEvaluateAsync(
                SlidingWindowScript,
                [new RedisKey($"gateway:rate-limit:tenant:{tenantId}")],
                [
                    now.ToString(CultureInfo.InvariantCulture),
                    ((long)_window.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
                    _permitLimit.ToString(CultureInfo.InvariantCulture),
                    new RedisValue($"{now}:{Guid.NewGuid():N}")
                ]).ConfigureAwait(false);
            var decision = (RedisResult[])result!;

            if ((long)decision[0] == 1)
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            var retryAfterSeconds = Math.Max(1, (long)Math.Ceiling((long)decision[1] / 1000d));
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        }
        catch (RedisException exception)
        {
            LogRedisUnavailable(logger, exception, tenantId);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Redis rate limiting is unavailable for tenant {TenantId}.")]
    private static partial void LogRedisUnavailable(ILogger logger, Exception exception, string tenantId);
}