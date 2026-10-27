using Kynakee.Modules.Billing.Application.Queries.GetCreditBalance;
using Kynakee.Modules.Billing.Application.Queries.GetCreditTransactions;
using Kynakee.Modules.Billing.Contracts;
using Kynakee.Modules.Billing.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Kynakee.Api.Endpoints;

/// <summary>
/// Extiende IEndpointRouteBuilder para registrar los endpoints HTTP del módulo de facturación, exponiendo operaciones
/// para consultar el saldo de créditos y el historial de transacciones del tenant autenticado.
/// </summary>
/// <remarks>Registra el grupo de rutas bajo el prefijo "/api/v1/billing" y requiere autorización. Incluye GET
/// /credits para obtener saldo de créditos y GET /credits/transactions para obtener historial paginado y filtrable por
/// tipo y fechas. Resuelve el tenant desde las claims del usuario, despacha consultas vía ISender (MediatR) y convierte
/// el resultado del dominio (Result<T>) en respuestas HTTP adecuadas mediante MapResult. Utiliza los helpers privados
/// TryGetTenant y MapResult para autorización y mapeo de errores.</remarks>
internal static class BillingEndpoints
{
    private static readonly IReadOnlyDictionary<string, string> ValidationFieldNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["endDate"] = "to"
    };
    /// <summary>
    /// Registra los endpoints HTTP de facturación para obtener el saldo de créditos y el historial de transacciones.
    /// </summary>
    /// <remarks>Agrupa las rutas bajo /api/v1/billing, requiere autorización y expone GET /credits y GET
    /// /credits/transactions con sus respuestas y códigos de error documentados.</remarks>
    /// <param name="endpoints">El IEndpointRouteBuilder al que se añaden las rutas del módulo de facturación.</param>
    /// <returns>El mismo IEndpointRouteBuilder con los endpoints de facturación añadidos para permitir encadenamiento.</returns>
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

    /// <summary>
    /// Obtiene el saldo de créditos del tenant extraído del principal y devuelve un resultado HTTP apropiado.
    /// </summary>
    /// <remarks>Extrae el TenantId del principal; si no está presente devuelve Unauthorized. En caso
    /// contrario envía GetCreditBalanceQuery y mapea el resultado a un IResult.</remarks>
    /// <param name="sender">ISender usado para enviar la consulta GetCreditBalanceQuery a través de MediatR.</param>
    /// <param name="principal">ClaimsPrincipal que contiene las reclamaciones del usuario y desde el que se extrae el TenantId.</param>
    /// <param name="context">HttpContext usado para mapear el resultado al IResult HTTP y conservar información de contexto/trazabilidad.</param>
    /// <param name="cancellationToken">CancellationToken que permite cancelar la operación asincrónica.</param>
    /// <returns>IResult que representa la respuesta HTTP con el saldo de créditos o un estado de error (por ejemplo,
    /// Unauthorized si no se encuentra el tenant).</returns>
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

    /// <summary>
    /// Obtiene las transacciones de crédito del inquilino autenticado aplicando filtros opcionales y paginación.
    /// </summary>
    /// <remarks>Extrae el TenantId del principal, convierte el parámetro de consulta al tipo de dominio
    /// correspondiente y delega la obtención a GetCreditTransactionsQuery; los valores numéricos de enum no válidos se
    /// dejan para validación y el resultado se mapea a una respuesta HTTP.</remarks>
    /// <param name="sender">Instancia ISender (MediatR) utilizada para enviar la consulta GetCreditTransactionsQuery.</param>
    /// <param name="principal">ClaimsPrincipal del usuario autenticado, de donde se extrae el TenantId; si no está presente se devuelve
    /// Unauthorized.</param>
    /// <param name="context">HttpContext usado para construir y devolver la respuesta HTTP final.</param>
    /// <param name="type">Filtro opcional por tipo de transacción de crédito (reserva, consumo, liberación, recarga).</param>
    /// <param name="from">Fecha de inicio opcional (UTC) para filtrar las transacciones.</param>
    /// <param name="endDate">Fecha de fin opcional (UTC) para filtrar las transacciones.</param>
    /// <param name="cursor">Cursor de paginación opcional para continuar la lista desde una posición previa.</param>
    /// <param name="limit">Límite opcional del número de elementos a devolver; si no se especifica, se usa 50.</param>
    /// <param name="cancellationToken">Token de cancelación para la operación asíncrona.</param>
    /// <returns>IResult HTTP que contiene la colección paginada de transacciones o un resultado de error (por ejemplo
    /// Unauthorized o errores de validación).</returns>
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

    /// <summary>
    /// Obtiene el identificador del tenant desde la claim "tenant_id" y verifica que sea un GUID distinto de
    /// Guid.Empty.
    /// </summary>
    /// <remarks>Se realiza un Guid.TryParse sobre el valor de la claim "tenant_id".</remarks>
    /// <param name="principal">ClaimsPrincipal que contiene la claim "tenant_id".</param>
    /// <param name="tenantId">Salida: GUID del tenant cuando se encuentra y es válido; Guid.Empty en caso contrario.</param>
    /// <returns>true si la claim "tenant_id" existe y representa un GUID distinto de Guid.Empty; false en caso contrario.</returns>
    private static bool TryGetTenant(ClaimsPrincipal principal, out Guid tenantId) =>
        Guid.TryParse(principal.FindFirst("tenant_id")?.Value, out tenantId) && tenantId != Guid.Empty;

    /// <summary>
    /// Convierte un Result<T> en un IResult HTTP: devuelve 200 con ApiResponse<T> en caso de éxito o ProblemDetails con
    /// código, detalle y el código de estado apropiado en caso de fallo.
    /// </summary>
    /// <remarks>Los tipos de error se mapean a códigos HTTP: Validation -> 400, Unauthorized -> 401, NotFound
    /// -> 404, Conflict -> 409, y por defecto 500. La respuesta de éxito incluye TraceIdentifier y
    /// DateTime.UtcNow.</remarks>
    /// <typeparam name="T">Tipo del valor encapsulado por el Result cuando la operación es exitosa.</typeparam>
    /// <param name="result">Resultado de la operación, con el valor en caso de éxito o la información de error en caso de fallo.</param>
    /// <param name="context">HttpContext usado para obtener el identificador de traza y generar la respuesta.</param>
    /// <returns>IResult HTTP: Results.Ok con ApiResponse<T> si result.IsSuccess; en caso contrario Results.Problem con
    /// title=result.Error.Code, detail=result.Error.Message y statusCode mapeado desde ErrorType.</returns>
    private static IResult MapResult<T>(Result<T> result, HttpContext context) =>
        result.IsSuccess
            ? Results.Ok(new ApiResponse<T>(result.Value!, context.TraceIdentifier, DateTime.UtcNow))
            : ApiProblems.FromError(
                result.Error!,
                result.Error!.Type switch
                {
                    ErrorType.Validation => StatusCodes.Status400BadRequest,
                    ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                    ErrorType.NotFound => StatusCodes.Status404NotFound,
                    ErrorType.Conflict => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status500InternalServerError
                }, ValidationFieldNames);
}
