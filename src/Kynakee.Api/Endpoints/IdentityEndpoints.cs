using System.Security.Claims;
using Kynakee.Modules.Identity.Application.Commands.ChangeUserRole;
using Kynakee.Modules.Identity.Application.Commands.DeactivateTenantUser;
using Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;
using Kynakee.Modules.Identity.Application.Commands.UpdateTenantSettings;
using Kynakee.Modules.Identity.Application.Commands.UpdateUserProfile;
using Kynakee.Modules.Identity.Application.Commands.AcceptInvitation;
using Kynakee.Modules.Identity.Application.Commands.InviteUser;
using Kynakee.Modules.Identity.Application.Commands.Login;
using Kynakee.Modules.Identity.Application.Commands.RefreshSession;
using Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;
using Kynakee.Modules.Identity.Application.Commands.RevokeSession;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Application.Queries.GetCurrentIdentity;
using Kynakee.Modules.Identity.Application.Queries.GetTenantUsers;
using Kynakee.Modules.Identity.Application.Queries.GetTenantProfile;
using Kynakee.Modules.SharedKernel.Application;
using IResult = Microsoft.AspNetCore.Http.IResult;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Kynakee.Api.Endpoints;

internal static class IdentityEndpoints
{
    internal static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var identity = endpoints.MapGroup("/api/v1/identity").WithTags("Identity");
        var auth = identity.MapGroup("/auth");
        var tenant = identity.MapGroup("").RequireAuthorization();
        auth.MapPost("/register", RegisterAsync)
            .WithName("RegisterTenantOwner")
            .WithSummary("Register a tenant and its owner")
            .WithDescription("Creates a tenant, owner account and initial authenticated session.")
            .Produces<ApiResponse<IdentitySessionResponse>>(201)
            .ProducesProblem(400)
            .ProducesProblem(422)
            .ProducesProblem(429)
            .RequireRateLimiting("identity-auth");
        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Authenticate with email and password")
            .WithDescription("Authenticates an active tenant user and issues access and refresh tokens.")
            .Produces<ApiResponse<IdentitySessionResponse>>(200)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(429)
            .RequireRateLimiting("identity-auth");
        auth.MapPost("/refresh", RefreshAsync)
            .WithName("RefreshSession")
            .WithSummary("Rotate a refresh token")
            .WithDescription("Revokes the supplied refresh token and issues a replacement session.")
            .Produces<ApiResponse<IdentitySessionResponse>>(200)
            .ProducesProblem(400)
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
            .ProducesProblem(403)
            .ProducesProblem(422);

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
            .ProducesProblem(401);
        tenant.MapPut("/tenants/me/settings", UpdateTenantSettingsAsync)
            .WithName("UpdateTenantSettings")
            .WithSummary("Update tenant construction settings")
            .WithDescription("Updates the tenant's percentage settings for authorized owners and administrators.")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403);
        tenant.MapPut("/users/{userId:guid}/role", ChangeUserRoleAsync)
            .WithName("ChangeTenantUserRole")
            .WithSummary("Change a tenant user's role")
            .WithDescription("Changes a member's role, preserving at least one active owner.")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(403)
            .ProducesProblem(422);
        tenant.MapDelete("/users/{userId:guid}", DeactivateTenantUserAsync)
            .WithName("DeactivateTenantUser")
            .WithSummary("Deactivate a tenant user")
            .WithDescription("Deactivates a tenant user and revokes active refresh tokens and invitations.")
            .Produces(204)
            .ProducesProblem(403)
            .ProducesProblem(422);
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterTenantOwnerCommand request,
        ISender sender,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken).ConfigureAwait(false);
        return MapResult(result, context, StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetCurrentIdentityAsync(
        ClaimsPrincipal principal,
        ISender sender,
        HttpContext context,
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
        return MapResult(result, context);
    }

    private static async Task<IResult> GetTenantProfileAsync(
        ClaimsPrincipal principal,
        ISender sender,
        HttpContext context,
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
        return MapResult(result, context);
    }

    private static async Task<IResult> GetTenantUsersAsync(
        ClaimsPrincipal principal,
        ISender sender,
        HttpContext context,
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
        return MapResult(result, context);
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var identifiers = GetIdentifiers(principal);
        if (identifiers is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
                new UpdateUserProfileCommand(
                    identifiers.Value.TenantId,
                    identifiers.Value.UserId,
                    request.FirstName,
                    request.LastName,
                    request.Phone),
                cancellationToken)
            .ConfigureAwait(false);
        return MapStatus(result);
    }

    private static async Task<IResult> UpdateTenantProfileAsync(
        UpdateTenantProfileRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var tenantId = ParseClaim(principal, "tenant_id");
        if (tenantId is null)
        {
            return Results.Unauthorized();
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
                    request.PlanId),
                cancellationToken)
            .ConfigureAwait(false);
        return MapStatus(result);
    }

    private static async Task<IResult> UpdateTenantSettingsAsync(
        UpdateTenantSettingsRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var tenantId = ParseClaim(principal, "tenant_id");
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
                new UpdateTenantSettingsCommand(
                    tenantId.Value,
                    request.Administration,
                    request.Profit,
                    request.Quality,
                    request.SafetyHealth,
                    request.Environment,
                    request.Contingency),
                cancellationToken)
            .ConfigureAwait(false);
        return MapStatus(result);
    }

    private static async Task<IResult> ChangeUserRoleAsync(
        Guid userId,
        ChangeUserRoleRequest request,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var tenantId = ParseClaim(principal, "tenant_id");
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
                new ChangeUserRoleCommand(tenantId.Value, userId, request.Role),
                cancellationToken)
            .ConfigureAwait(false);
        return MapStatus(result);
    }

    private static async Task<IResult> DeactivateTenantUserAsync(
        Guid userId,
        ClaimsPrincipal principal,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var tenantId = ParseClaim(principal, "tenant_id");
        if (tenantId is null)
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(
                new DeactivateTenantUserCommand(tenantId.Value, userId),
                cancellationToken)
            .ConfigureAwait(false);
        return MapStatus(result);
    }

    private static IResult MapStatus(Result result) =>
        result.IsSuccess ? Results.NoContent() : ToProblem(result.Error!, ResolveStatusCode(result.Error!));

    private static int ResolveStatusCode(ApplicationError error) => error.Type switch
    {
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status422UnprocessableEntity,
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
        var result = await sender.Send(request, cancellationToken).ConfigureAwait(false);
        return MapResult(result, context);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshSessionCommand request,
        ISender sender,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request, cancellationToken).ConfigureAwait(false);
        return MapResult(result, context);
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
        var result = await sender.Send(request, cancellationToken).ConfigureAwait(false);
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
        var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);
        return MapResult(result, context, StatusCodes.Status201Created);
    }

    private static Guid? ParseClaim(ClaimsPrincipal principal, string claimType) =>
        Guid.TryParse(principal.FindFirst(claimType)?.Value, out var value) ? value : null;

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
            ErrorType.Conflict => ToProblem(result.Error, StatusCodes.Status422UnprocessableEntity),
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
    string FiscalCountry,
    string? Region,
    string? Province,
    string? Municipality,
    string? PostalCode,
    string? Street,
    string PlanId);

internal sealed record ChangeUserRoleRequest(Kynakee.Modules.Identity.Domain.ValueObjects.UserRole Role);
#pragma warning restore CA1812
