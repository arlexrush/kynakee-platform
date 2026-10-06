using System.Globalization;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Resources;
using System.Text.Json;
using Kynakee.Modules.Mcp.Contracts;
using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.Entities;
using Kynakee.Modules.Mcp.Domain.Repositories;
using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using Polly.CircuitBreaker;

namespace Kynakee.Modules.Mcp.Infrastructure.Client;

internal sealed class ProviderNetworkClient(
    IMcpProviderRepository repository,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    McpServerResilience resilience,
    McpQueryAuditWriter audit,
    ITenantContext tenantContext) : IMCPClient
{
    private static readonly ResourceManager Resources = new(
        "Kynakee.Modules.Mcp.Infrastructure.Client.McpClientResources", typeof(ProviderNetworkClient).Assembly);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<IReadOnlyList<MCPProviderSummaryDto>>> GetAvailableProvidersAsync(
        string geoRegion, string category, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(geoRegion) || string.IsNullOrWhiteSpace(category))
        {
            return ResultFactory.Failure<IReadOnlyList<MCPProviderSummaryDto>>(
                ApplicationError.Validation("MCP_QUERY_INVALID", Message("QueryInvalid")));
        }

        var providers = await repository.GetAvailableForQueryAsync(
            geoRegion, category, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
        return ResultFactory.Success<IReadOnlyList<MCPProviderSummaryDto>>(providers.Select(provider =>
            new MCPProviderSummaryDto(provider.Id.Value, provider.Name, provider.Categories.ToArray(),
                provider.GeoRegions.ToArray(), provider.Rating.Value)).ToArray());
    }

    public async Task<Result<MCPQueryResult>> QueryPriceAsync(MCPPriceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        if (!tenantContext.IsAuthenticated || tenantContext.TenantId == Guid.Empty || tenantContext.UserId == Guid.Empty)
        {
            return ResultFactory.Failure<MCPQueryResult>(ApplicationError.Unauthorized(
                "MCP_QUERY_IDENTITY_REQUIRED", Message("IdentityRequired")));
        }
        if (!IsValid(query))
        {
            return ResultFactory.Failure<MCPQueryResult>(ApplicationError.Validation(
                "MCP_QUERY_INVALID", Message("QueryInvalid")));
        }

        var providers = await repository.GetAvailableForQueryAsync(
            query.GeoRegion, query.Category, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
        if (providers.Count == 0)
        {
            return Failure("MCP_NO_PROVIDERS", "NoProviders");
        }

        Result<MCPQueryResult> last = Failure("MCP_PROVIDER_UNAVAILABLE", "ProviderUnavailable");
        foreach (var provider in providers)
        {
            var logs = new List<McpQueryLog>();
            var providerFailed = false;
            foreach (var server in provider.Servers.Where(server => !server.IsDeleted))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var startedAt = DateTime.UtcNow;
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    last = await resilience.ForServer(server.Id.Value).ExecuteAsync(
                        token => QueryServerAsync(provider, server, query, token), cancellationToken).ConfigureAwait(false);
                }
                catch (BrokenCircuitException)
                {
                    last = Failure("MCP_CIRCUIT_OPEN", "ProviderUnavailable");
                }

                cancellationToken.ThrowIfCancellationRequested();
                var log = McpQueryLog.Create(tenantContext.TenantId, query.ProjectId, provider.Id,
                    query.CanonicalConceptId, query.ComponentType, query.Quantity, query.Unit,
                    last.IsSuccess ? last.Value!.Price : null, last.IsSuccess ? last.Value!.Unit : null,
                    null, last.IsSuccess ? last.Value!.Confidence : null,
                    (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue),
                    last.IsSuccess ? McpQueryStatus.Success : last.Error!.Code == "MCP_TIMEOUT"
                        ? McpQueryStatus.Timeout : McpQueryStatus.Error,
                    0m, startedAt, tenantContext.UserId);
                if (log.IsFailure)
                {
                    return ResultFactory.Failure<MCPQueryResult>(log.Error!);
                }
                logs.Add(log.Value!);
                providerFailed |= last.IsFailure && last.Error!.Code is not ("MCP_CIRCUIT_OPEN" or "MCP_SERVER_NOT_CONFIGURED");
                if (last.IsSuccess)
                {
                    await audit.WriteAsync(provider.Id, logs, true, cancellationToken).ConfigureAwait(false);
                    return last;
                }
            }
            await audit.WriteAsync(provider.Id, logs, providerFailed ? false : null, cancellationToken).ConfigureAwait(false);
        }

        return last;
    }

    private async ValueTask<Result<MCPQueryResult>> QueryServerAsync(
        McpProvider provider, McpServer server, MCPPriceQuery query, CancellationToken cancellationToken)
    {
        var credentials = configuration.GetSection("Mcp:Servers").GetSection(server.CredentialSecretReference);
        if (!Uri.TryCreate(credentials["Endpoint"], UriKind.Absolute, out var allowedEndpoint) ||
            allowedEndpoint != server.Endpoint || server.Endpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(server.Endpoint.UserInfo) || !string.IsNullOrEmpty(server.Endpoint.Query) ||
            !string.IsNullOrEmpty(server.Endpoint.Fragment) || string.IsNullOrWhiteSpace(credentials["ApiKey"]))
        {
            return Failure("MCP_SERVER_NOT_CONFIGURED", "ServerNotConfigured");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var httpClient = httpClientFactory.CreateClient("mcp-provider");
        try
        {
            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = server.Endpoint,
                TransportMode = HttpTransportMode.StreamableHttp,
                EnableStandaloneGetStream = false,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = "Bearer " + credentials["ApiKey"]
                }
            }, httpClient, ownsHttpClient: false);
            await using var transportLifetime = transport.ConfigureAwait(false);
            var client = await McpClient.CreateAsync(transport,
                cancellationToken: timeout.Token).ConfigureAwait(false);
            await using var clientLifetime = client.ConfigureAwait(false);
            var result = await client.CallToolAsync("query_price", new Dictionary<string, object?>
            {
                ["canonicalConceptId"] = query.CanonicalConceptId,
                ["componentType"] = query.ComponentType.ToString(),
                ["category"] = query.Category,
                ["unit"] = query.Unit,
                ["quantity"] = query.Quantity,
                ["geoRegion"] = query.GeoRegion,
                ["postalCode"] = query.PostalCode,
                ["currency"] = query.Currency
            }, cancellationToken: timeout.Token).ConfigureAwait(false);
            if (result.IsError == true || result.StructuredContent is null)
            {
                return Failure("MCP_QUOTE_INVALID", "QuoteInvalid");
            }

            var quote = result.StructuredContent.Value.Deserialize<ProviderQuote>(JsonOptions);
            if (quote is null || quote.Price < 0 || quote.Confidence is < 0 or > 1 ||
                !string.Equals(quote.Unit, query.Unit, StringComparison.Ordinal) ||
                !string.Equals(quote.Currency, query.Currency, StringComparison.Ordinal) ||
                quote.Price is null || quote.Confidence is null)
            {
                return Failure("MCP_QUOTE_INVALID", "QuoteInvalid");
            }

            return ResultFactory.Success(new MCPQueryResult(quote.Price.Value, quote.Unit, quote.Currency,
                provider.Id.Value, provider.Name, server.Id.Value, quote.Confidence.Value, DateTime.UtcNow));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("MCP_TIMEOUT", "ProviderUnavailable");
        }
        catch (HttpRequestException error)
        {
            return Failure(error.StatusCode is null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
                (int?)error.StatusCode >= 500 ? "MCP_TRANSPORT_FAILURE" : "MCP_PROVIDER_REJECTED", "ProviderUnavailable");
        }
        catch (McpException)
        {
            return Failure("MCP_PROTOCOL_FAILURE", "ProviderUnavailable");
        }
        catch (IOException)
        {
            return Failure("MCP_TRANSPORT_FAILURE", "ProviderUnavailable");
        }
        catch (JsonException)
        {
            return Failure("MCP_QUOTE_INVALID", "QuoteInvalid");
        }
    }

    private static bool IsValid(MCPPriceQuery query) =>
        query.ProjectId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(query.CanonicalConceptId) && query.CanonicalConceptId.Length <= 100 &&
        Enum.IsDefined(query.ComponentType) &&
        !string.IsNullOrWhiteSpace(query.Category) && query.Category.Length <= 100 &&
        !string.IsNullOrWhiteSpace(query.Unit) && query.Unit.Length <= 20 && query.Quantity > 0 &&
        !string.IsNullOrWhiteSpace(query.GeoRegion) && query.GeoRegion.Length <= 50 &&
        !string.IsNullOrWhiteSpace(query.PostalCode) && query.PostalCode.Length <= 20 &&
        query.Currency is { Length: 3 } && query.Currency.All(character => character is >= 'A' and <= 'Z');

    private static string Message(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    private static Result<MCPQueryResult> Failure(string code, string key) =>
        ResultFactory.Failure<MCPQueryResult>(ApplicationError.MCP(code, Message(key)));

    [SuppressMessage("Performance", "CA1812", Justification = "System.Text.Json creates this wire DTO when deserializing the MCP tool response.")]
    private sealed record ProviderQuote(decimal? Price, string Unit, string Currency, decimal? Confidence);
}
