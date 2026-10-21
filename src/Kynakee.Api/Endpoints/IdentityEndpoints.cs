using Kynakee.Modules.Identity.Application.Commands.AcceptInvitation;
using Kynakee.Modules.Identity.Application.Commands.AccountActions;
using Kynakee.Modules.Identity.Application.Commands.ChangeUserRole;
using Kynakee.Modules.Identity.Application.Commands.DeactivateTenantUser;
using Kynakee.Modules.Identity.Application.Commands.InviteUser;
using Kynakee.Modules.Identity.Application.Commands.Login;
using Kynakee.Modules.Identity.Application.Commands.RefreshSession;
using Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;
using Kynakee.Modules.Identity.Application.Commands.RevokeSession;
using Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;
using Kynakee.Modules.Identity.Application.Commands.UpdateTenantSettings;
using Kynakee.Modules.Identity.Application.Commands.UpdateUserProfile;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Application.Queries.GetCurrentIdentity;
using Kynakee.Modules.Identity.Application.Queries.GetTenantProfile;
using Kynakee.Modules.Identity.Application.Queries.GetTenantUsers;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Api.Infrastructure.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Kynakee.Api.Endpoints;

/// <summary>
/// Registra y configura los endpoints HTTP relacionados con identidad y autenticación (registro, inicio de sesión,
/// refresco, cierre de sesión, aceptación de invitaciones, e interfaces para tenant y usuarios) en un
/// IEndpointRouteBuilder.
/// </summary>
/// <remarks>Agrupa rutas, aplica metadatos OpenAPI, políticas de autorización y limitación de tasa. Las acciones
/// delegan en MediatR mediante comandos/consultas y utilizan métodos auxiliares para convertir los resultados de
/// dominio a respuestas HTTP apropiadas.</remarks>
internal static class IdentityEndpoints
{
    /// <summary>
    /// Registra los endpoints del módulo Identity para autenticación, gestión de sesiones, invitaciones, inquilinos y
    /// usuarios.
    /// </summary>
    /// <remarks>Agrupa rutas bajo /api/v1/identity, separa /auth (registro, login, refresh, logout,
    /// aceptación de invitaciones) y rutas que requieren autorización para gestión de tenants y usuarios; aplica la
    /// política de rate limiting "identity-auth" a los endpoints de autenticación y especifica respuestas HTTP y
    /// códigos de error para cada ruta.</remarks>
    /// <param name="endpoints">Instancia de IEndpointRouteBuilder sobre la que se registran las rutas.</param>
    /// <returns>La misma instancia de IEndpointRouteBuilder con los endpoints de Identity registrados.</returns>
    internal static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var identity = endpoints.MapGroup("/api/v1/identity").WithTags("Identity"); // Agrupa todos los endpoints de identidad bajo /api/v1/identity y etiqueta para OpenAPI
        var auth = identity.MapGroup("/auth"); // Agrupa los endpoints de autenticación bajo /api/v1/identity/auth
        var tenant = identity.MapGroup("").RequireAuthorization(); // Agrupa los endpoints de gestión de tenants y usuarios que requieren autorización
        auth.MapPost("/register", RegisterAsync)
            .WithName("RegisterTenantOwner")
            .WithSummary("Register a tenant and its owner")
            .WithDescription("Accepts an owner registration pending mailbox verification. Returns a generic acknowledgement without access or refresh tokens.")
            .Produces<ApiResponse<AccountActionAcknowledgement>>(202)
            .ProducesProblem(400)
            .ProducesProblem(409)
            .ProducesProblem(429)
            .RequireRateLimiting("identity-auth");
        auth.MapPost("/email/verify", VerifyEmailAsync)
            .WithName("VerifyIdentityEmail")
            .WithSummary("Verify the owner mailbox")
            .WithDescription("Consumes a purpose-bound single-use token and activates a pending owner. Does not issue a session.")
            .Produces<ApiResponse<AccountActionAcknowledgement>>(200)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .RequireRateLimiting("identity-auth");
        auth.MapPost("/email/resend", ResendVerificationAsync)
            .WithName("ResendIdentityVerification")
            .WithSummary("Request another verification email")
            .WithDescription("Returns a generic acknowledgement without revealing account existence. Requires Idempotency-Key; applies a per-account cooldown.")
            .Produces<ApiResponse<AccountActionAcknowledgement>>(202)
            .ProducesProblem(400)
            .ProducesProblem(409)
            .ProducesProblem(429)
            .RequireRateLimiting("identity-auth");
        auth.MapPost("/password/forgot", RequestPasswordResetAsync)
            .WithName("RequestIdentityPasswordReset")
            .WithSummary("Request password recovery")
            .WithDescription("Returns a generic acknowledgement and queues recovery only for an eligible active account. Requires Idempotency-Key.")
            .Produces<ApiResponse<AccountActionAcknowledgement>>(202)
            .ProducesProblem(400)
            .ProducesProblem(409)
            .ProducesProblem(429)
            .RequireRateLimiting("identity-auth");
        auth.MapPost("/password/reset", ResetPasswordAsync)
            .WithName("ResetIdentityPassword")
            .WithSummary("Recover an active account password")
            .WithDescription("Consumes a recovery credential and replaces the password without activating disabled accounts or issuing a session.")
            .Produces<ApiResponse<AccountActionAcknowledgement>>(200)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .RequireRateLimiting("identity-auth");
        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Authenticate with email and password")
            .WithDescription("Authenticates an active tenant user and issues access and refresh tokens.")
            .Produces<ApiResponse<IdentitySessionResponse>>(200)
            .ProducesProblem(400)
            .ProducesProblem(409)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .RequireRateLimiting("identity-auth");
        auth.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshSession")
            .WithSummary("Rotate a refresh token")
            .WithDescription("Revokes the supplied refresh token and issues a replacement session.")
            .Produces<ApiResponse<IdentitySessionResponse>>(200)
            .ProducesProblem(400)
            .ProducesProblem(409)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .RequireRateLimiting("identity-auth");
        auth.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .WithSummary("Revoke a refresh token")
            .WithDescription("Revokes the provided refresh token if it exists.")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(429)
            .RequireRateLimiting("identity-auth");
        auth.MapPost("/invitations/accept", AcceptInvitationAsync)
            .WithName("AcceptInvitation")
            .WithSummary("Accept a tenant invitation")
            .WithDescription("Sets the invitee password, activates the user and issues a session.")
            .Produces<ApiResponse<IdentitySessionResponse>>(200)
            .ProducesProblem(400)
            .ProducesProblem(409)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .RequireRateLimiting("identity-auth");

        identity.MapPost("/users/invite", InviteUserAsync)
            .WithName("InviteTenantUser")
            .WithSummary("Invite a user to the current tenant")
            .WithDescription("Creates a pending user and sends a single-use 24-hour invitation after commit.")
            .RequireAuthorization()
            .Produces<ApiResponse<InvitationCreatedResponse>>(201)
            .ProducesProblem(400)
            .ProducesProblem(409)
            .ProducesProblem(401)
            .ProducesProblem(403);
        tenant.MapGet("/tenants/me", GetTenantProfileAsync)
            .WithName("GetTenantProfile")
            .WithSummary("Get current tenant profile and fiscal details")
            .WithDescription("Returns the current tenant using a database projection.")
            .Produces<ApiResponse<IdentityTenantProfile>>(200)
            .ProducesProblem(401)
            .ProducesProblem(404);
        tenant.MapPut("/tenants/me", UpdateTenantProfileAsync)
            .WithName("UpdateTenantProfile")
            .WithSummary("Update tenant profile and fiscal information")
            .WithDescription("Updates the tenant name, unique slug, tax identifier and fiscal address.")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(412)
            .ProducesProblem(428);

        tenant.MapGet("/users/me", GetCurrentIdentityAsync)
            .WithName("GetCurrentIdentity")
            .WithSummary("Get the current user's profile and tenant")
            .WithDescription("Returns the authenticated user and tenant using read projections.")
            .Produces<ApiResponse<IdentitySessionProfile>>(200)
            .ProducesProblem(401)
            .ProducesProblem(404);
        tenant.MapGet("/users", GetTenantUsersAsync)
            .WithName("GetTenantUsers")
            .WithSummary("List users in the current tenant")
            .WithDescription("Lists tenant members for tenant owners and administrators.")
            .Produces<ApiResponse<IReadOnlyList<TenantUserListItem>>>(200)
            .ProducesProblem(401)
            .ProducesProblem(403);
        tenant.MapPut("/users/me", UpdateProfileAsync)
            .WithName("UpdateCurrentIdentityProfile")
            .WithSummary("Update the current user's profile")
            .WithDescription("Updates the authenticated user's name and phone number.")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(412)
            .ProducesProblem(428);
        tenant.MapPut("/tenants/me/settings", UpdateTenantSettingsAsync)
            .WithName("UpdateTenantSettings")
            .WithSummary("Update tenant construction settings")
            .WithDescription("Updates the tenant's percentage settings for authorized owners and administrators.")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(412)
            .ProducesProblem(428);
        tenant.MapPut("/users/{userId:guid}/role", ChangeUserRoleAsync)
            .WithName("ChangeTenantUserRole")
            .WithSummary("Change a tenant user's role")
            .WithDescription("Changes a member's role, preserving at least one active owner.")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(412)
            .ProducesProblem(428);
        tenant.MapDelete("/users/{userId:guid}", DeactivateTenantUserAsync)
            .WithName("DeactivateTenantUser")
            .WithSummary("Deactivate a tenant user")
            .WithDescription("Deactivates a tenant user and revokes active refresh tokens and invitations.")
            .Produces(204)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(409)
            .ProducesProblem(412)
            .ProducesProblem(428);
        return endpoints;
    }

    /// <summary>
    /// Envía el comando para registrar el propietario del tenant y devuelve el resultado mapeado como respuesta HTTP
    /// 201.
    /// </summary>
    /// <remarks>Utiliza sender.Send para procesar el comando y MapResult para convertir el resultado en una
    /// respuesta Created (201).</remarks>
    /// <param name="request">Comando RegisterTenantOwnerCommand con los datos de registro del propietario del tenant.</param>
    /// <param name="sender">Instancia ISender (MediatR) utilizada para enviar el comando.</param>
    /// <param name="context">HttpContext para construir la respuesta HTTP y acceder al contexto de la petición.</param>
    /// <param name="cancellationToken">CancellationToken para cancelar la operación asincrónica.</param>
    /// <returns>IResult que representa un acuse HTTP 202 sin sesión.</returns>
    private static async Task<IResult> RegisterAsync(
        RegisterTenantOwnerCommand request,
        ISender sender,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var idempotencyContext = ReadIdempotencyContext(context, "identity.register", Guid.Empty, Guid.Empty); // Leer el contexto de idempotencia desde los encabezados de la solicitud

        if (idempotencyContext.Failure is not null) return idempotencyContext.Failure;
        var result = await sender.Send(
                request with { IdempotencyContext = idempotencyContext.Context },
                cancellationToken)
            .ConfigureAwait(false);
        return MapResult(result, context, StatusCodes.Status202Accepted);
    }

    /// <summary>
    /// Obtiene la identidad actual del usuario a partir de los reclamos del principal y devuelve un resultado HTTP
    /// apropiado.
    /// </summary>
    /// <remarks>Extrae identificadores del ClaimsPrincipal, envía GetCurrentIdentityQuery con TenantId y
    /// UserId y convierte el resultado en una respuesta HTTP mediante MapResult.</remarks>
    /// <param name="principal">ClaimsPrincipal que contiene los reclamos del usuario; se extraen TenantId y UserId para la consulta.</param>
    /// <param name="sender">ISender de MediatR usado para enviar la consulta GetCurrentIdentityQuery.</param>
    /// <param name="context">HttpContext actual, empleado para mapear el resultado a una respuesta HTTP.</param>
    /// <param name="cancellationToken">Token de cancelación para la operación asíncrona.</param>
    /// <returns>IResult: Unauthorized si faltan identificadores, o la respuesta mapeada del resultado de la consulta.</returns>
    private static async Task<IResult> GetCurrentIdentityAsync(
        ClaimsPrincipal principal,
        ISender sender,
        HttpContext context,
        IdentityEntityTagProtector entityTagProtector,
        CancellationToken cancellationToken)
    {
        var identifiers = GetIdentifiers(principal);
        if (identifiers is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
                new GetCurrentIdentityQuery(identifiers.Value.TenantId, identifiers.Value.UserId),
                cancellationToken)
            .ConfigureAwait(false);
        if (result.IsSuccess)
        {
            context.Response.Headers.ETag = entityTagProtector.Create(
                identifiers.Value.TenantId,
                IdentityEntityTagResource.User,
                result.Value!.User.Id,
                result.Value.User.Version);
        }

        return MapResult(result, context);
    }

    private static async Task<IResult> GetTenantProfileAsync(
        ClaimsPrincipal principal,
        ISender sender,
        HttpContext context,
        IdentityEntityTagProtector entityTagProtector,
        CancellationToken cancellationToken)
    {
        var identifiers = GetIdentifiers(principal);
        if (identifiers is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
                new GetTenantProfileQuery(identifiers.Value.TenantId, identifiers.Value.UserId),
                cancellationToken)
            .ConfigureAwait(false);
        if (result.IsSuccess)
        {
            context.Response.Headers.ETag = entityTagProtector.Create(
                identifiers.Value.TenantId,
                IdentityEntityTagResource.Tenant,
                result.Value!.Id,
                result.Value.Version);
        }

        return MapResult(result, context);
    }

    private static async Task<IResult> GetTenantUsersAsync(
        ClaimsPrincipal principal,
        ISender sender,
        HttpContext context,
        IdentityEntityTagProtector entityTagProtector,
        CancellationToken cancellationToken)
    {
        var identifiers = GetIdentifiers(principal);
        if (identifiers is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
                new GetTenantUsersQuery(identifiers.Value.TenantId, identifiers.Value.UserId),
                cancellationToken)
            .ConfigureAwait(false);
        if (result.IsSuccess)
        {
            var users = result.Value!.Select(user => user with
            {
                MembershipETag = entityTagProtector.Create(identifiers.Value.TenantId, IdentityEntityTagResource.Membership, user.UserId, user.MembershipVersion)
            }).ToArray();
            return MapResult(ResultFactory.Success<IReadOnlyList<TenantUserListItem>>(users), context);
        }

        return MapResult(result, context);
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        HttpContext context,
        IdentityEntityTagProtector entityTagProtector,
        CancellationToken cancellationToken)
    {
        var identifiers = GetIdentifiers(principal);
        if (identifiers is null)
        {
            return Results.Unauthorized();
        }

        var preconditionError = ReadIfMatch(
            context,
            identifiers.Value.TenantId,
            IdentityEntityTagResource.User,
            identifiers.Value.UserId,
            entityTagProtector,
            out var expectedVersion);
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        var result = await sender.Send(
                new UpdateUserProfileCommand(
                    identifiers.Value.TenantId,
                    identifiers.Value.UserId,
                    request.FirstName,
                    request.LastName,
                    request.Phone,
                    expectedVersion),
                cancellationToken)
            .ConfigureAwait(false);
        return MapStatus(result);
    }

    private static async Task<IResult> UpdateTenantProfileAsync(
        UpdateTenantProfileRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        HttpContext context,
        IdentityEntityTagProtector entityTagProtector,
        CancellationToken cancellationToken)
    {
        var tenantId = ParseClaim(principal, "tenant_id");
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var preconditionError = ReadIfMatch(context, tenantId.Value, IdentityEntityTagResource.Tenant, tenantId.Value, entityTagProtector, out var expectedVersion);
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        var result = await sender.Send(
                new UpdateTenantProfileCommand(
                    tenantId.Value,
                    request.Name,
                    request.Slug,
                    request.TaxId,
                    request.TaxCountry,
                    request.FiscalCountry,
                    request.Region,
                    request.Province,
                    request.Municipality,
                    request.PostalCode,
                    request.Street,
                    request.PlanId,
                    expectedVersion),
                cancellationToken)
            .ConfigureAwait(false);
        return MapStatus(result);
    }

    private static async Task<IResult> UpdateTenantSettingsAsync(
        UpdateTenantSettingsRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        HttpContext context,
        IdentityEntityTagProtector entityTagProtector,
        CancellationToken cancellationToken)
    {
        var tenantId = ParseClaim(principal, "tenant_id");
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var preconditionError = ReadIfMatch(context, tenantId.Value, IdentityEntityTagResource.Tenant, tenantId.Value, entityTagProtector, out var expectedVersion);
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        var result = await sender.Send(
                new UpdateTenantSettingsCommand(
                    tenantId.Value,
                    request.Administration,
                    request.Profit,
                    request.Quality,
                    request.SafetyHealth,
                    request.Environment,
                    request.Contingency,
                    expectedVersion),
                cancellationToken)
            .ConfigureAwait(false);
        return MapStatus(result);
    }

    private static async Task<IResult> ChangeUserRoleAsync(
        Guid userId,
        ChangeUserRoleRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        HttpContext context,
        IdentityEntityTagProtector entityTagProtector,
        CancellationToken cancellationToken)
    {
        var tenantId = ParseClaim(principal, "tenant_id");
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var preconditionError = ReadIfMatch(context, tenantId.Value, IdentityEntityTagResource.Membership, userId, entityTagProtector, out var expectedVersion);
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        var result = await sender.Send(
                new ChangeUserRoleCommand(tenantId.Value, userId, request.Role, expectedVersion),
                cancellationToken)
            .ConfigureAwait(false);
        return MapStatus(result);
    }

    private static async Task<IResult> DeactivateTenantUserAsync(
        Guid userId,
        ClaimsPrincipal principal,
        ISender sender,
        HttpContext context,
        IdentityEntityTagProtector entityTagProtector,
        CancellationToken cancellationToken)
    {
        var tenantId = ParseClaim(principal, "tenant_id");
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var preconditionError = ReadIfMatch(context, tenantId.Value, IdentityEntityTagResource.Membership, userId, entityTagProtector, out var expectedVersion);
        if (preconditionError is not null)
        {
            return preconditionError;
        }

        var result = await sender.Send(
                new DeactivateTenantUserCommand(tenantId.Value, userId, expectedVersion),
                cancellationToken)
            .ConfigureAwait(false);
        return MapStatus(result);
    }

    private static IResult MapStatus(Result result) =>
        result.IsSuccess ? Results.NoContent() : ToProblem(result.Error!, ResolveStatusCode(result.Error!));

    private static IResult? ReadIfMatch(
        HttpContext context,
        Guid tenantId,
        IdentityEntityTagResource resourceType,
        Guid resourceId,
        IdentityEntityTagProtector entityTagProtector,
        out uint expectedVersion)
    {
        var header = context.Request.Headers.IfMatch;
        if (header.Count == 0 || (header.Count == 1 && string.IsNullOrWhiteSpace(header[0])))
        {
            expectedVersion = default;
            return Results.Problem(
                title: "IDENTITY_PRECONDITION_REQUIRED",
                detail: "The If-Match header is required.",
                statusCode: StatusCodes.Status428PreconditionRequired);
        }

        if (header.Count != 1 || !entityTagProtector.TryRead(header[0]?.Trim(), tenantId, resourceType, resourceId, out expectedVersion))
        {
            expectedVersion = default;
            return Results.Problem(
                title: "IDENTITY_PRECONDITION_FAILED",
                detail: "The If-Match header is invalid or does not match this resource.",
                statusCode: StatusCodes.Status412PreconditionFailed);
        }

        return null;
    }

    private static int ResolveStatusCode(ApplicationError error) => error.Type switch
    {
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict when error.Code == "IDENTITY_PRECONDITION_FAILED" => StatusCodes.Status412PreconditionFailed,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized when error.Code.EndsWith("FORBIDDEN", StringComparison.Ordinal) =>
            StatusCodes.Status403Forbidden,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError
    };

    private static (Guid TenantId, Guid UserId)? GetIdentifiers(ClaimsPrincipal principal)
    {
        var tenantId = ParseClaim(principal, "tenant_id");
        var userId = ParseClaim(principal, ClaimTypes.NameIdentifier) ?? ParseClaim(principal, "sub");
        return tenantId is null || userId is null ? null : (tenantId.Value, userId.Value);
    }

    private static async Task<IResult> LoginAsync(
        LoginCommand request,
        ISender sender,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var idempotencyContext = ReadIdempotencyContext(context, "identity.login", Guid.Empty, Guid.Empty);
        if (idempotencyContext.Failure is not null) return idempotencyContext.Failure;
        var result = await sender.Send(
                request with { IdempotencyContext = idempotencyContext.Context },
                cancellationToken)
            .ConfigureAwait(false);
        return MapResult(result, context);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshSessionCommand request,
        ISender sender,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var idempotencyContext = ReadIdempotencyContext(context, "identity.refresh", Guid.Empty, Guid.Empty);
        if (idempotencyContext.Failure is not null) return idempotencyContext.Failure;
        var result = await sender.Send(
                request with { IdempotencyContext = idempotencyContext.Context },
                cancellationToken)
            .ConfigureAwait(false);
        return MapResult(result, context);
    }

    private static async Task<IResult> VerifyEmailAsync(
        VerifyEmailCommand request, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken).ConfigureAwait(false);
        return MapResult(result, context);
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordCommand request, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken).ConfigureAwait(false);
        return MapResult(result, context);
    }

    private static async Task<IResult> ResendVerificationAsync(
        ResendVerificationCommand request, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var idempotency = ReadIdempotencyContext(context, "identity.resend-verification", Guid.Empty, Guid.Empty);
        if (idempotency.Failure is not null) return idempotency.Failure;
        var result = await sender.Send(request with { IdempotencyContext = idempotency.Context }, cancellationToken).ConfigureAwait(false);
        return MapResult(result, context, StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> RequestPasswordResetAsync(
        RequestPasswordResetCommand request, ISender sender, HttpContext context, CancellationToken cancellationToken)
    {
        var idempotency = ReadIdempotencyContext(context, "identity.request-password-reset", Guid.Empty, Guid.Empty);
        if (idempotency.Failure is not null) return idempotency.Failure;
        var result = await sender.Send(request with { IdempotencyContext = idempotency.Context }, cancellationToken).ConfigureAwait(false);
        return MapResult(result, context, StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> LogoutAsync(
        RevokeSessionCommand request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Results.NoContent()
            : ToProblem(result.Error!, ResolveStatusCode(result.Error!));
    }

    private static async Task<IResult> AcceptInvitationAsync(
        AcceptInvitationCommand request,
        ISender sender,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var idempotencyContext = ReadIdempotencyContext(context, "identity.accept-invitation", Guid.Empty, Guid.Empty);
        if (idempotencyContext.Failure is not null) return idempotencyContext.Failure;
        var result = await sender.Send(
                request with { IdempotencyContext = idempotencyContext.Context },
                cancellationToken)
            .ConfigureAwait(false);
        return MapResult(result, context);
    }

    private static async Task<IResult> InviteUserAsync(
        InviteUserRequest request,
        ISender sender,
        ClaimsPrincipal principal,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var tenantId = ParseClaim(principal, "tenant_id");
        var userId = ParseClaim(principal, ClaimTypes.NameIdentifier) ?? ParseClaim(principal, "sub");
        if (tenantId is null || userId is null)
        {
            return Results.Unauthorized();
        }

        var command = new InviteUserCommand(
            tenantId.Value,
            userId.Value,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.Role);
        var idempotencyContext = ReadIdempotencyContext(context, "identity.invite-user", tenantId.Value, userId.Value);
        if (idempotencyContext.Failure is not null) return idempotencyContext.Failure;
        command = command with { IdempotencyContext = idempotencyContext.Context };
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return MapResult(result, context, StatusCodes.Status201Created);
    }

    private static Guid? ParseClaim(ClaimsPrincipal principal, string claimType) =>
        Guid.TryParse(principal.FindFirst(claimType)?.Value, out var value) ? value : null;

    private static (HttpIdempotencyContext? Context, IResult? Failure) ReadIdempotencyContext(
        HttpContext context,
        string operation,
        Guid tenantId,
        Guid callerId)
    {
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (!IsValidIdempotencyKey(key))
        {
            var code = string.IsNullOrWhiteSpace(key)
                ? "IDENTITY_IDEMPOTENCY_KEY_REQUIRED"
                : "IDENTITY_IDEMPOTENCY_KEY_INVALID";
            return (null, Results.Problem(
                title: code,
                detail: "A valid Idempotency-Key header is required for this operation.",
                statusCode: StatusCodes.Status400BadRequest));
        }

        return (new HttpIdempotencyContext(operation, key, tenantId, callerId), null);
    }

    private static bool IsValidIdempotencyKey(string key)
    {
        if (key.Length is < 22 or > 128)
        {
            return false;
        }

        foreach (var character in key)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')
            {
                return false;
            }
        }

        var remainder = key.Length % 4;
        if (remainder == 1)
        {
            return false;
        }

        var base64 = key.Replace('-', '+').Replace('_', '/');
        base64 += remainder switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };
        return Convert.TryFromBase64String(base64, new Span<byte>(new byte[96]), out var bytesWritten) &&
               bytesWritten >= 16;
    }

    private static Microsoft.AspNetCore.Http.IResult MapResult<T>(
        Result<T> result,
        HttpContext context,
        int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return Results.Json(
                new ApiResponse<T>(result.Value!, context.TraceIdentifier, DateTime.UtcNow),
                statusCode: successStatusCode);
        }

        return result.Error!.Type switch
        {
            ErrorType.NotFound => ToProblem(result.Error, StatusCodes.Status404NotFound),
            ErrorType.Conflict => ToProblem(result.Error, ResolveStatusCode(result.Error)),
            ErrorType.Validation => ToProblem(result.Error, StatusCodes.Status400BadRequest),
            ErrorType.Unauthorized => ToProblem(result.Error, ResolveStatusCode(result.Error)),
            _ => ToProblem(result.Error, StatusCodes.Status500InternalServerError)
        };
    }

    private static IResult ToProblem(ApplicationError error, int statusCode) =>
        Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: statusCode);
}

#pragma warning disable CA1812
internal sealed record InviteUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    Kynakee.Modules.Identity.Domain.ValueObjects.UserRole Role);

internal sealed record UpdateProfileRequest(string FirstName, string LastName, string? Phone);

internal sealed record UpdateTenantSettingsRequest(
    decimal Administration,
    decimal Profit,
    decimal Quality,
    decimal SafetyHealth,
    decimal Environment,
    decimal Contingency);

internal sealed record UpdateTenantProfileRequest(
    string Name,
    string Slug,
    string? TaxId,
    string? TaxCountry,
    string? FiscalCountry,
    string? Region,
    string? Province,
    string? Municipality,
    string? PostalCode,
    string? Street,
    string PlanId);

internal sealed record ChangeUserRoleRequest(Kynakee.Modules.Identity.Domain.ValueObjects.UserRole Role);
#pragma warning restore CA1812
