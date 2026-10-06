using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Kynakee.Modules.Mcp;
using Kynakee.Modules.Mcp.Contracts;
using Kynakee.Modules.Mcp.Domain.Aggregates;
using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.Mcp.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Mcp;

public sealed class McpClientTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly TestTenantContext _tenant = new();
    private readonly ToolHandler _handler = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var context = CreateContext();
        await using var lifetime = context.ConfigureAwait(false);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var provider = McpProvider.Create(_tenant.TenantId, "Provider", ["construction"], ["ES-VC"]).Value
            ?? throw new InvalidOperationException();
        provider.AddServer(new Uri("https://provider.example.test/mcp"), "provider-one");
        context.Providers.Add(provider);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _handler.Dispose();
        return _postgres.DisposeAsync();
    }

    [Theory]
    [InlineData(McpComponentType.Material)]
    [InlineData(McpComponentType.Labor)]
    [InlineData(McpComponentType.Equipment)]
    [InlineData(McpComponentType.Subcontract)]
    [InlineData(McpComponentType.Transport)]
    public async Task ClientValidQueryShouldNegotiateAndReturnUnitPrice(McpComponentType componentType)
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();
        var query = Query(componentType);

        var result = await scope.ServiceProvider.GetRequiredService<IMCPClient>()
            .QueryPriceAsync(query, TestContext.Current.CancellationToken);

        result.Value.Should().BeEquivalentTo(new
        {
            Price = 7.25m,
            Unit = "kg",
            Currency = "EUR",
            ProviderName = "Provider",
            Confidence = 0.9m
        });
        _handler.Methods.Should().ContainInOrder("initialize", "notifications/initialized", "tools/call");
        JsonElement.DeepEquals(_handler.Arguments, JsonSerializer.SerializeToElement(new
        {
            canonicalConceptId = query.CanonicalConceptId,
            componentType = componentType.ToString(),
            category = query.Category,
            unit = query.Unit,
            quantity = query.Quantity,
            geoRegion = query.GeoRegion,
            postalCode = query.PostalCode,
            currency = query.Currency
        })).Should().BeTrue();
        _handler.Authorization.Should().Be("Bearer test-key");
    }

    [Theory]
    [InlineData("{\"price\":7.25,\"unit\":\"m\",\"currency\":\"EUR\",\"confidence\":0.9}")]
    [InlineData("{\"price\":7.25,\"unit\":\"kg\",\"currency\":\"USD\",\"confidence\":0.9}")]
    [InlineData("{\"price\":-1,\"unit\":\"kg\",\"currency\":\"EUR\",\"confidence\":0.9}")]
    [InlineData("{\"price\":7.25,\"unit\":\"kg\",\"currency\":\"EUR\",\"confidence\":1.1}")]
    [InlineData("{\"unit\":\"kg\",\"currency\":\"EUR\"}")]
    public async Task ClientIncompatibleQuoteShouldReturnFailureWithoutRetry(string response)
    {
        _handler.Quote = response;
        using var services = CreateServices();
        using var scope = services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMCPClient>()
            .QueryPriceAsync(Query(), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("MCP_QUOTE_INVALID");
        _handler.Methods.Count(method => method == "tools/call").Should().Be(1);
    }

    [Fact]
    public async Task ClientEndpointNotAuthorizedShouldAvoidNetwork()
    {
        using var services = CreateServices("https://other.example.test/mcp");
        using var scope = services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMCPClient>()
            .QueryPriceAsync(Query(), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("MCP_SERVER_NOT_CONFIGURED");
        _handler.Methods.Should().BeEmpty();
    }

    [Fact]
    public async Task ClientInvalidQuantityShouldAvoidNetwork()
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMCPClient>()
            .QueryPriceAsync(Query() with { Quantity = 0 }, TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("MCP_QUERY_INVALID");
        _handler.Methods.Should().BeEmpty();
    }

    [Fact]
    public async Task ClientMissingProvidersShouldReturnFailureWithoutNetwork()
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMCPClient>()
            .QueryPriceAsync(Query() with { Category = "unknown" }, TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("MCP_NO_PROVIDERS");
        _handler.Methods.Should().BeEmpty();
    }

    [Fact]
    public async Task ClientProviderSummaryShouldExcludeServerCredentials()
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMCPClient>()
            .GetAvailableProvidersAsync("ES-VC", "construction", TestContext.Current.CancellationToken);

        result.Value.Should().ContainSingle().Which.Name.Should().Be("Provider");
        JsonSerializer.Serialize(result.Value).Should().NotContain("provider-one").And.NotContain("test-key");
    }

    [Fact]
    public async Task ClientCallerCancellationShouldCancelPendingTransport()
    {
        _handler.WaitForCancellation = true;
        using var services = CreateServices();
        using var scope = services.CreateScope();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var operation = scope.ServiceProvider.GetRequiredService<IMCPClient>().QueryPriceAsync(Query(), cancellation.Token);
        await _handler.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
    }

    [Fact]
    public async Task ClientTransientFailureShouldRetryReadOnlyQuery()
    {
        _handler.FailCalls = 1;
        using var services = CreateServices();
        using var scope = services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMCPClient>()
            .QueryPriceAsync(Query(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _handler.Methods.Count(method => method == "tools/call").Should().Be(2);
    }

    [Fact]
    public async Task ClientSuccessfulQuoteShouldPersistAuditAndProviderSuccess()
    {
        var projectId = Guid.NewGuid();
        using var services = CreateServices();
        using var scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IMCPClient>()
            .QueryPriceAsync(Query() with { ProjectId = projectId }, TestContext.Current.CancellationToken);
        await using var verification = CreateContext();
        var log = await verification.QueryLogs.SingleAsync(TestContext.Current.CancellationToken);

        log.Should().BeEquivalentTo(new { ProjectId = (Guid?)projectId, TenantId = _tenant.TenantId,
            CreatedBy = (Guid?)_tenant.UserId, ResponsePrice = (decimal?)7.25m, Status = McpQueryStatus.Success });
    }

    [Fact]
    public async Task ClientSuccessfulQuoteShouldResetProviderFailures()
    {
        await using (var preparation = CreateContext())
        {
            var provider = await preparation.Providers.SingleAsync(TestContext.Current.CancellationToken);
            provider.RecordFailure();
            await preparation.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var services = CreateServices();
        using var scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IMCPClient>().QueryPriceAsync(Query(), TestContext.Current.CancellationToken);
        await using var verification = CreateContext();
        var stored = await verification.Providers.SingleAsync(TestContext.Current.CancellationToken);

        stored.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task ClientFailedQuoteOuterRollbackShouldPreserveAudit()
    {
        _handler.Quote = "{\"price\":7.25,\"unit\":\"m\",\"currency\":\"EUR\",\"confidence\":0.9}";
        using var services = CreateServices();
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<McpDbContext>();
        var transaction = await new McpTransactionParticipant(context).BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var lifetime = transaction.ConfigureAwait(true);

        await scope.ServiceProvider.GetRequiredService<IMCPClient>().QueryPriceAsync(Query(), TestContext.Current.CancellationToken);
        await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        await using var verification = CreateContext();
        var log = await verification.QueryLogs.SingleAsync(TestContext.Current.CancellationToken);

        log.Status.Should().Be(McpQueryStatus.Error);
    }

    [Fact]
    public async Task ClientThreeFailedQueriesShouldSuspendProvider()
    {
        _handler.Quote = "{\"price\":7.25,\"unit\":\"m\",\"currency\":\"EUR\",\"confidence\":0.9}";
        using var services = CreateServices();
        using var scope = services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IMCPClient>();

        await client.QueryPriceAsync(Query(), TestContext.Current.CancellationToken);
        await client.QueryPriceAsync(Query(), TestContext.Current.CancellationToken);
        await client.QueryPriceAsync(Query(), TestContext.Current.CancellationToken);
        await using var verification = CreateContext();
        var provider = await verification.Providers.SingleAsync(TestContext.Current.CancellationToken);

        provider.Should().BeEquivalentTo(new { ConsecutiveFailures = 3, Status = McpProviderStatus.Suspended });
    }

    [Fact]
    public async Task ClientUnauthorizedEndpointShouldNotPenalizeProvider()
    {
        using var services = CreateServices("https://other.example.test/mcp");
        using var scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IMCPClient>().QueryPriceAsync(Query(), TestContext.Current.CancellationToken);
        await using var verification = CreateContext();
        var provider = await verification.Providers.SingleAsync(TestContext.Current.CancellationToken);

        provider.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task ClientTransientRetryShouldPersistOneFinalAudit()
    {
        _handler.FailCalls = 1;
        using var services = CreateServices();
        using var scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IMCPClient>().QueryPriceAsync(Query(), TestContext.Current.CancellationToken);
        await using var verification = CreateContext();

        (await verification.QueryLogs.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task ClientCallerCancellationShouldNotPenalizeProvider()
    {
        _handler.WaitForCancellation = true;
        using var services = CreateServices();
        using var scope = services.CreateScope();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var operation = scope.ServiceProvider.GetRequiredService<IMCPClient>().QueryPriceAsync(Query(), cancellation.Token);
        await _handler.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        await using var verification = CreateContext();
        var provider = await verification.Providers.SingleAsync(TestContext.Current.CancellationToken);

        provider.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task ClientDifferentConsumerTenantShouldOwnAuditNotProvider()
    {
        var providerTenant = _tenant.TenantId;
        _tenant.TenantId = Guid.NewGuid();
        using var services = CreateServices();
        using var scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IMCPClient>().QueryPriceAsync(Query(), TestContext.Current.CancellationToken);
        await using var verification = CreateContext();
        var log = await verification.QueryLogs.SingleAsync(TestContext.Current.CancellationToken);

        log.TenantId.Should().Be(_tenant.TenantId).And.NotBe(providerTenant);
    }

    [Fact]
    public async Task ClientAmbientRollbackShouldPreserveAudit()
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();
        using (var ambient = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled))
        {
            await scope.ServiceProvider.GetRequiredService<IMCPClient>().QueryPriceAsync(Query(), TestContext.Current.CancellationToken);
        }
        await using var verification = CreateContext();

        (await verification.QueryLogs.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task ClientConcurrentFailuresShouldPreserveBothHealthUpdates()
    {
        _handler.Quote = "{\"price\":7.25,\"unit\":\"m\",\"currency\":\"EUR\",\"confidence\":0.9}";
        using var otherHandler = new ToolHandler { Quote = _handler.Quote };
        using var firstServices = CreateServices();
        using var secondServices = CreateServices(handler: otherHandler);
        using var firstScope = firstServices.CreateScope();
        using var secondScope = secondServices.CreateScope();

        await Task.WhenAll(
            firstScope.ServiceProvider.GetRequiredService<IMCPClient>().QueryPriceAsync(Query(), TestContext.Current.CancellationToken),
            secondScope.ServiceProvider.GetRequiredService<IMCPClient>().QueryPriceAsync(Query(), TestContext.Current.CancellationToken));
        await using var verification = CreateContext();
        var provider = await verification.Providers.SingleAsync(TestContext.Current.CancellationToken);

        provider.ConsecutiveFailures.Should().Be(2);
    }

    private ServiceProvider CreateServices(string endpoint = "https://provider.example.test/mcp", ToolHandler? handler = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            ["Mcp:Servers:provider-one:Endpoint"] = endpoint,
            ["Mcp:Servers:provider-one:ApiKey"] = "test-key"
        }).Build();
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext>(_ => _tenant);
        services.AddMcpModule(configuration);
        services.AddHttpClient("mcp-provider").ConfigurePrimaryHttpMessageHandler(() => handler ?? _handler);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private McpDbContext CreateContext() => new(
        new DbContextOptionsBuilder<McpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options, _tenant);

    private static MCPPriceQuery Query(McpComponentType type = McpComponentType.Material) =>
        new("concept-1", type, "construction", "kg", 2m, "ES-VC", "46001", "EUR");

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; set; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated => true;
    }

    private sealed class ToolHandler : HttpMessageHandler
    {
        public List<string> Methods { get; } = [];
        public JsonElement Arguments { get; private set; }
        public string? Authorization { get; private set; }
        public string Quote { get; set; } = "{\"price\":7.25,\"unit\":\"kg\",\"currency\":\"EUR\",\"confidence\":0.9}";
        public bool WaitForCancellation { get; set; }
        public int FailCalls { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Started.TrySetResult();
            if (WaitForCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }

            using var document = await JsonDocument.ParseAsync(
                await request.Content!.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            var method = root.GetProperty("method").GetString() ?? throw new InvalidOperationException();
            Methods.Add(method);
            if (method == "server/discover")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        jsonrpc = "2.0",
                        id = root.GetProperty("id").Clone(),
                        error = new { code = -32601, message = "Method not found" }
                    })
                };
            }
            if (method == "notifications/initialized")
            {
                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }

            object result;
            if (method == "initialize")
            {
                result = new
                {
                    protocolVersion = root.GetProperty("params").GetProperty("protocolVersion").GetString(),
                    capabilities = new { tools = new { } },
                    serverInfo = new { name = "test-provider", version = "1.0" }
                };
            }
            else
            {
                root.GetProperty("params").GetProperty("name").GetString().Should().Be("query_price");
                Arguments = root.GetProperty("params").GetProperty("arguments").Clone();
                if (FailCalls-- > 0)
                {
                    throw new HttpRequestException("Transient transport failure.", null, HttpStatusCode.ServiceUnavailable);
                }
                result = new { content = Array.Empty<object>(), structuredContent = JsonSerializer.Deserialize<JsonElement>(Quote) };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { jsonrpc = "2.0", id = root.GetProperty("id").Clone(), result })
            };
        }
    }
}
