using System.Diagnostics.CodeAnalysis;
using StackExchange.Redis;

namespace Kynakee.Gateway;

[SuppressMessage("Performance", "CA1812", Justification = "Created by the Gateway dependency injection container.")]
internal sealed class RedisConnectionFactory : IDisposable
{
    private readonly Lazy<ConnectionMultiplexer> _connection;

    public RedisConnectionFactory(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        _connection = new Lazy<ConnectionMultiplexer>(() =>
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IDatabase GetDatabase() => _connection.Value.GetDatabase();

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        var database = GetDatabase();
        await database.PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }
    }
}