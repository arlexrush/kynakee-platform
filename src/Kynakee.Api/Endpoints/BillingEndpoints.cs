using System.Security.Claims;
using Kynakee.Modules.Billing.Application.Queries.GetCreditBalance;
using Kynakee.Modules.Billing.Application.Queries.GetCreditTransactions;
using Kynakee.Modules.Billing.Contracts;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Kynakee.Api.Endpoints;

internal static class BillingEndpoints
{
    internal static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var billing = endpoints.MapGroup("/api/v1/billing").WithTags("Billing").RequireAuthorization();
        billing.MapGet("/credits", GetBalanceAsync)
            .WithName("GetCreditBalance")
            .WithSummary("Get current tenant credit balance")
            .WithDescription("Returns available credits, pending reservations, plan and renewal date for the authenticated tenant.")
            .Produces<ApiResponse<CreditBalanceDto>>()
            .ProducesProblem(401)
            .ProducesProblem(404);
        billing.MapGet("/credits/transactions", GetTransactionsAsync)
            .WithName("GetCreditTransactions")
            .WithSummary("Get current tenant credit transaction history")
            .WithDescription("Returns cursor-paginated transactions filtered by type and inclusive UTC dates. The tenant is taken from authenticated claims.")
            .Produces<ApiResponse<CreditTransactionHistoryDto>>()
            .ProducesProblem(400)
            .ProducesProblem(401);
        return endpoints;
    }

    private static async Task<IResult> GetBalanceAsync(
        ISender sender,
        ClaimsPrincipal principal,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenant(principal, out var tenantId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new GetCreditBalanceQuery(tenantId), cancellationToken).ConfigureAwait(false);
        return MapResult(result, context);
    }

    private static async Task<IResult> GetTransactionsAsync(
        ISender sender,
        ClaimsPrincipal principal,
        HttpContext context,
        [FromQuery] CreditTransactionKind? type,
        [FromQuery] DateTimeOffset? from,
        [FromQuery(Name = "to")] DateTimeOffset? endDate,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenant(principal, out var tenantId))
        {
            return Results.Unauthorized();
        }

        // Undefined numeric enum values must reach the validator, not become an unfiltered query.
        var domainType = type switch
        {
            null => (CreditTransactionType?)null,
            CreditTransactionKind.Reserve => CreditTransactionType.Reservation,
            CreditTransactionKind.Consume => CreditTransactionType.Consumed,
            CreditTransactionKind.Release => CreditTransactionType.Released,
            CreditTransactionKind.Recharge => CreditTransactionType.Recharged,
            _ => (CreditTransactionType)(-1)
        };
        var result = await sender.Send(new GetCreditTransactionsQuery(
            tenantId, domainType, from?.UtcDateTime, endDate?.UtcDateTime, cursor, limit ?? 50), cancellationToken)
            .ConfigureAwait(false);
        return MapResult(result, context);
    }

    private static bool TryGetTenant(ClaimsPrincipal principal, out Guid tenantId) =>
        Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out tenantId) && tenantId != Guid.Empty;

    private static IResult MapResult<T>(Result<T> result, HttpContext context) =>
        result.IsSuccess
            ? Results.Ok(new ApiResponse<T>(result.Value!, context.TraceIdentifier, DateTime.UtcNow))
            : Results.Problem(
                title: result.Error!.Code,
                detail: result.Error.Message,
                statusCode: result.Error.Type switch
                {
                    ErrorType.Validation => StatusCodes.Status400BadRequest,
                    ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                    ErrorType.NotFound => StatusCodes.Status404NotFound,
                    ErrorType.Conflict => StatusCodes.Status422UnprocessableEntity,
                    _ => StatusCodes.Status500InternalServerError
                });
}
