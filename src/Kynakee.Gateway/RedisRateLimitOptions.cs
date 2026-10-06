using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Gateway;

[SuppressMessage("Performance", "CA1812", Justification = "Bound and instantiated by the options framework.")]
internal sealed class RedisRateLimitOptions
{
    public int PermitLimit { get; set; } = 100;

    public int WindowSeconds { get; set; } = 60;
}