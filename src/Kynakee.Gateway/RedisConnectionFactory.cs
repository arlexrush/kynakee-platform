using System.Diagnostics.CodeAnalysis;
using StackExchange.Redis;

namespace Kynakee.Gateway;

/// <summary>
/// Fábrica que crea y gestiona una única ConnectionMultiplexer de StackExchange.Redis mediante inicialización perezosa;
/// proporciona acceso a IDatabase, un método de ping asíncrono y libera la conexión al desechar.
/// </summary>
/// <remarks>La cadena de conexión se obtiene de Configuration.GetConnectionString("Redis") con valor por defecto
/// "localhost:6379". Se establece AbortOnConnectFail = false. La instancia se inicializa con
/// LazyThreadSafetyMode.ExecutionAndPublication para creación segura entre hilos; Dispose solo cierra la conexión si ya
/// se ha creado.</remarks>

[SuppressMessage("Performance", "CA1812", Justification = "Created by the Gateway dependency injection container.")]
internal sealed class RedisConnectionFactory : IDisposable
{
    private readonly Lazy<ConnectionMultiplexer> _connection; // Lazy initialization of the Redis connection

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

    public IDatabase GetDatabase() => _connection.Value.GetDatabase(); // Returns the Redis database instance

    /// <summary>
    /// Comprueba de forma asíncrona la conectividad con la base de datos.
    /// </summary>
    /// <remarks>La operación respeta el token de cancelación y puede provocar OperationCanceledException si
    /// se cancela. Realiza la comprobación contra el backend de persistencia de forma asíncrona.</remarks>
    /// <param name="cancellationToken">Token de cancelación para abortar la operación asíncrona.</param>
    /// <returns>Tarea que representa la finalización de la comprobación de conectividad.</returns>
    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        var database = GetDatabase();
        await database.PingAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Libera los recursos administrados y dispone la conexión subyacente si ésta ya se ha inicializado.
    /// </summary>
    /// <remarks>Es seguro llamar varias veces; si la conexión no se ha creado no realiza ninguna acción. No
    /// suprime la finalización del objeto.</remarks>
    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }
    }
}