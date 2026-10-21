using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kynakee.Modules.Identity.Infrastructure.Persistence.DesignTime;

/// <summary>
/// Fábrica en tiempo de diseño que crea y configura un IdentityDbContext usando la cadena de conexión extraída de la
/// variable de entorno ConnectionStrings__Postgres y el ensamblado de migraciones del contexto.
/// </summary>
/// <remarks>Usada por las herramientas de EF Core para generar o aplicar migraciones. Lanza
/// InvalidOperationException si la variable de entorno ConnectionStrings__Postgres no está establecida o está
/// vacía.</remarks>
public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__Postgres to generate or apply Identity EF migrations.");
        }

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .Options;
        return new IdentityDbContext(options, new IdentityDesignTimeTenantContext());
    }
}
