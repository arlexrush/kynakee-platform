using Kynakee.Modules.Identity.Domain.Aggregates;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Resources;

namespace Kynakee.Api.Infrastructure.ExceptionHandling;

[SuppressMessage(
    "Performance",
    "CA1812",
    Justification = "ASP.NET Core creates this handler through dependency injection.")]
internal sealed class IdentityUniqueConstraintExceptionHandler : IExceptionHandler
{
    private static readonly ResourceManager Resources = new(
        "Kynakee.Modules.Identity.Infrastructure.IdentityPersistenceResources",
        typeof(Tenant).Assembly);

    private static readonly Dictionary<string, (string Code, string ResourceName)> ConstraintErrors =
        new Dictionary<string, (string Code, string ResourceName)>(StringComparer.Ordinal)
        {
            ["ux_identity_users_normalized_email"] = ("IDENTITY_EMAIL_ALREADY_REGISTERED", "EmailAlreadyRegistered"),
            ["ux_identity_tenants_slug"] = ("IDENTITY_TENANT_SLUG_ALREADY_USED", "TenantSlugAlreadyUsed"),
            ["ux_identity_tenant_users_user"] = ("IDENTITY_TENANT_USER_ALREADY_EXISTS", "TenantUserAlreadyExists"),
            ["ux_identity_refresh_tokens_hash"] = ("IDENTITY_REFRESH_TOKEN_HASH_CONFLICT", "RefreshTokenHashConflict"),
            ["ux_identity_invitations_hash"] = ("IDENTITY_INVITATION_TOKEN_HASH_CONFLICT", "InvitationTokenHashConflict")
        };

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (httpContext.Response.HasStarted ||
            !httpContext.Request.Path.StartsWithSegments(
                "/api/v1/identity",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsPreconditionWrite(httpContext) && FindConcurrencyException(exception) is not null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status412PreconditionFailed;
            await Results.Problem(
                    title: "IDENTITY_PRECONDITION_FAILED",
                    detail: "The resource changed while the request was being processed. Fetch the latest version before retrying.",
                    statusCode: StatusCodes.Status412PreconditionFailed)
                .ExecuteAsync(httpContext)
                .ConfigureAwait(false);
            return true;
        }

        var postgresException = FindPostgresException(exception);
        if (postgresException?.SqlState != PostgresErrorCodes.UniqueViolation ||
            !string.Equals(postgresException.SchemaName, "schema_identity", StringComparison.Ordinal) ||
            postgresException.ConstraintName is not { } constraintName ||
            !ConstraintErrors.TryGetValue(constraintName, out var error))
        {
            return false;
        }

        var detail = Resources.GetString(error.ResourceName, CultureInfo.CurrentUICulture)
            ?? "The request conflicts with existing identity data.";
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await Results.Problem(
                title: error.Code,
                detail: detail,
                statusCode: StatusCodes.Status409Conflict)
            .ExecuteAsync(httpContext)
            .ConfigureAwait(false);
        return true;
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }

    private static DbUpdateConcurrencyException? FindConcurrencyException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException concurrencyException)
            {
                return concurrencyException;
            }
        }

        return null;
    }

    private static bool IsPreconditionWrite(HttpContext context)
    {
        if (context.Request.Headers.IfMatch.Count == 0)
        {
            return false;
        }

        var path = context.Request.Path;
        if (context.Request.Method == "PUT")
        {
            return path.Equals("/api/v1/identity/tenants/me", StringComparison.OrdinalIgnoreCase) ||
                   path.Equals("/api/v1/identity/tenants/me/settings", StringComparison.OrdinalIgnoreCase) ||
                   path.Equals("/api/v1/identity/users/me", StringComparison.OrdinalIgnoreCase) ||
                   IsUserRolePath(path);
        }

        return context.Request.Method == "DELETE" && IsUserIdPath(path);
    }

    private static bool IsUserRolePath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments is { Length: 6 } &&
               string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[1], "v1", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[2], "identity", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[3], "users", StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(segments[4], out _) &&
               string.Equals(segments[5], "role", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUserIdPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments is { Length: 5 } &&
               string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[1], "v1", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[2], "identity", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[3], "users", StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(segments[4], out _);
    }
}
