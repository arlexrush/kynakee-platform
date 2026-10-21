using Kynakee.Modules.Identity.Application.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;

namespace Kynakee.Api.Infrastructure.Identity;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Created by JWT Bearer authentication through dependency injection.")]
internal sealed class IdentityJwtBearerEvents(IIdentityAccessTokenValidator validator) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var principal = context.Principal;
        if (!Guid.TryParse(principal?.FindFirst("tenant_id")?.Value, out var tenantId) ||
            !Guid.TryParse(principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal?.FindFirst("sub")?.Value, out var userId) ||
            !Guid.TryParse(principal?.FindFirst("credential_version")?.Value, out var credentialVersion))
        {
            context.Fail("IDENTITY_ACCESS_TOKEN_UNAVAILABLE");
            return;
        }
        if (!await validator.IsCurrentAsync(tenantId, userId, credentialVersion,
            principal?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty,
            context.HttpContext.RequestAborted).ConfigureAwait(false))
            context.Fail("IDENTITY_ACCESS_TOKEN_UNAVAILABLE");
    }
}
