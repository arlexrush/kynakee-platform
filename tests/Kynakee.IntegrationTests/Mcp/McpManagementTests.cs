using FluentAssertions;
using FluentValidation;
using Kynakee.Modules.Identity;
using Kynakee.Modules.Identity.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.Identity.Infrastructure.Persistence;
using Kynakee.Modules.Mcp;
using Kynakee.Modules.Mcp.Application.Commands.AddMcpServer;
using Kynakee.Modules.Mcp.Application.Commands.ChangeMcpProviderStatus;
using Kynakee.Modules.Mcp.Application.Commands.RegisterMcpProvider;
using Kynakee.Modules.Mcp.Application.Commands.RemoveMcpServer;
using Kynakee.Modules.Mcp.Application.Queries.GetMcpProvider;
using Kynakee.Modules.Mcp.Application.Queries.ListMcpProviders;
using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.Mcp.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Mcp;

public sealed class McpManagementTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly TestTenantContext _tenantContext = new();
    private ServiceProvider? _services;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _postgres.StartAsync(cancellationToken).ConfigureAwait(false);
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString()
        }).Build();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<ITenantContext>(_ => _tenantContext);
        services.AddIdentityModule(configuration);
        services.AddMcpModule(configuration);
        services.AddMediatR(options => options.RegisterServicesFromAssembly(typeof(RegisterMcpProviderCommandHandler).Assembly));
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = _services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await identity.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        var mcp = scope.ServiceProvider.GetRequiredService<McpDbContext>();
        await mcp.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        await SeedIdentityAsync(UserRole.Owner).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is not null)
        {
            await _services.DisposeAsync().ConfigureAwait(false);
        }
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    [Theory]
    [InlineData(UserRole.Owner)]
    [InlineData(UserRole.Admin)]
    public async Task RegisterProviderActiveManagerShouldPersistOwnedProvider(UserRole role)
    {
        await SeedIdentityAsync(role);
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<McpDbContext>();
        var transaction = await new McpTransactionParticipant(context).BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var lifetime = transaction.ConfigureAwait(true);

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(NewRegistration(), TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        using var verification = Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<McpDbContext>().Providers.SingleAsync(TestContext.Current.CancellationToken);

        stored.Should().BeEquivalentTo(new { Id = new Kynakee.Modules.Mcp.Domain.ValueObjects.McpProviderId(result.Value),
            TenantId = _tenantContext.TenantId, CreatedBy = (Guid?)_tenantContext.UserId, Name = "Provider" });
    }

    [Theory]
    [InlineData(UserRole.Technician)]
    [InlineData(UserRole.Commercial)]
    [InlineData(UserRole.Viewer)]
    public async Task RegisterProviderNonManagerShouldReturnUnauthorized(UserRole role)
    {
        await SeedIdentityAsync(role);
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(NewRegistration(), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("IDENTITY_TENANT_MANAGEMENT_FORBIDDEN");
    }

    [Fact]
    public async Task RegisterProviderAnonymousShouldNotTrackProvider()
    {
        _tenantContext.IsAuthenticated = false;
        using var scope = Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<ISender>().Send(NewRegistration(), TestContext.Current.CancellationToken);

        scope.ServiceProvider.GetRequiredService<McpDbContext>().ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task ManagementInactiveUserShouldReturnUnauthorized()
    {
        await SeedIdentityAsync(UserRole.Owner, UserStatus.Inactive);
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ITenantManagementAuthorization>().AuthorizeAsync(TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("IDENTITY_TENANT_MANAGEMENT_FORBIDDEN");
    }

    [Fact]
    public async Task ManagementDeletedMembershipShouldReturnUnauthorized()
    {
        using (var scope = Services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var membership = await identity.TenantUsers.SingleAsync(TestContext.Current.CancellationToken);
            membership.Delete(_tenantContext.UserId);
            await identity.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var verification = Services.CreateScope();

        var result = await verification.ServiceProvider.GetRequiredService<ITenantManagementAuthorization>().AuthorizeAsync(TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("IDENTITY_TENANT_MANAGEMENT_FORBIDDEN");
    }

    [Theory]
    [InlineData(TenantStatus.Suspended)]
    [InlineData(TenantStatus.Cancelled)]
    public async Task ManagementInactiveTenantShouldReturnUnauthorized(TenantStatus status)
    {
        using (var scope = Services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var tenant = await identity.Tenants.SingleAsync(TestContext.Current.CancellationToken);
            tenant.ChangeStatus(status, _tenantContext.UserId);
            await identity.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var verification = Services.CreateScope();

        var result = await verification.ServiceProvider.GetRequiredService<ITenantManagementAuthorization>()
            .AuthorizeAsync(TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("IDENTITY_TENANT_MANAGEMENT_FORBIDDEN");
    }

    [Fact]
    public async Task ManagementCancelledRequestShouldPropagateCancellation()
    {
        using var scope = Services.CreateScope();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var authorization = scope.ServiceProvider.GetRequiredService<ITenantManagementAuthorization>();

        var action = () => authorization.AuthorizeAsync(cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AddServerOtherTenantShouldReturnNotFound()
    {
        var providerId = await RegisterAsync();
        await SeedIdentityAsync(UserRole.Owner);
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new AddMcpServerCommand(providerId, new Uri("https://provider.example.test/mcp"), "provider"), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("MCP_PROVIDER_NOT_FOUND");
    }

    [Fact]
    public async Task AddServerValidCommandShouldPersistServerAndCreator()
    {
        var providerId = await RegisterAsync();
        using var scope = Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new AddMcpServerCommand(providerId, new Uri("https://provider.example.test/mcp"), "provider"), TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<McpDbContext>().SaveChangesAsync(TestContext.Current.CancellationToken);
        using var verification = Services.CreateScope();

        var stored = await verification.ServiceProvider.GetRequiredService<McpDbContext>().Servers.SingleAsync(TestContext.Current.CancellationToken);

        stored.Should().BeEquivalentTo(new { Id = new Kynakee.Modules.Mcp.Domain.ValueObjects.McpServerId(result.Value),
            TenantId = _tenantContext.TenantId, CreatedBy = (Guid?)_tenantContext.UserId, CredentialSecretReference = "provider" });
    }

    [Fact]
    public async Task RemoveServerValidCommandShouldPersistSoftDelete()
    {
        var providerId = await RegisterAsync();
        using (var scope = Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var added = await sender.Send(new AddMcpServerCommand(providerId, new Uri("https://provider.example.test/mcp"), "provider"), TestContext.Current.CancellationToken);
            await sender.Send(new RemoveMcpServerCommand(providerId, added.Value), TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<McpDbContext>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var verification = Services.CreateScope();

        var stored = await verification.ServiceProvider.GetRequiredService<McpDbContext>().Servers.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);

        stored.Should().BeEquivalentTo(new { IsDeleted = true, UpdatedBy = (Guid?)_tenantContext.UserId });
    }

    [Fact]
    public async Task RemoveServerUnknownServerShouldReturnNotFound()
    {
        var providerId = await RegisterAsync();
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new RemoveMcpServerCommand(providerId, Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("MCP_SERVER_NOT_FOUND");
    }

    [Fact]
    public async Task ChangeProviderStatusValidCommandShouldPersistStatus()
    {
        var providerId = await RegisterAsync();
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new ChangeMcpProviderStatusCommand(providerId, McpProviderStatus.Testing), TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<McpDbContext>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var verification = Services.CreateScope();

        var stored = await verification.ServiceProvider.GetRequiredService<McpDbContext>().Providers.SingleAsync(TestContext.Current.CancellationToken);

        stored.Status.Should().Be(McpProviderStatus.Testing);
    }

    [Fact]
    public async Task RegisterProviderInvalidNameShouldReturnValidation()
    {
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new RegisterMcpProviderCommand("", ["ceramics"], ["ES-VC"]), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("MCP_PROVIDER_NAME_INVALID");
    }

    [Fact]
    public void RegisterProviderValidatorMissingCategoriesShouldRejectCommand()
    {
        using var scope = Services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<RegisterMcpProviderCommand>>();

        var result = validator.Validate(new RegisterMcpProviderCommand("Provider", [], ["ES-VC"]));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://provider.example.test/mcp")]
    [InlineData("https://provider.example.test/mcp?secret=value")]
    [InlineData("https://user:password@provider.example.test/mcp")]
    [InlineData("https://provider.example.test/mcp#fragment")]
    public void AddServerValidatorUnsafeEndpointShouldRejectCommand(string endpoint)
    {
        using var scope = Services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<AddMcpServerCommand>>();

        var result = validator.Validate(new AddMcpServerCommand(Guid.NewGuid(), new Uri(endpoint), "provider"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RemoveServerValidatorEmptyServerShouldRejectCommand()
    {
        using var scope = Services.CreateScope();

        var result = scope.ServiceProvider.GetRequiredService<IValidator<RemoveMcpServerCommand>>()
            .Validate(new RemoveMcpServerCommand(Guid.NewGuid(), Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ChangeProviderStatusValidatorUnknownStatusShouldRejectCommand()
    {
        using var scope = Services.CreateScope();

        var result = scope.ServiceProvider.GetRequiredService<IValidator<ChangeMcpProviderStatusCommand>>()
            .Validate(new ChangeMcpProviderStatusCommand(Guid.NewGuid(), (McpProviderStatus)999));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ListProvidersNormalizedFiltersShouldReturnOwnedProvider()
    {
        var providerId = await RegisterAsync();
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new ListMcpProvidersQuery(" es-vc ", " ceramics ", McpProviderStatus.Active), TestContext.Current.CancellationToken);

        result.Value!.Items.Should().ContainSingle().Which.Id.Should().Be(providerId);
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public async Task ListProvidersInvalidPaginationShouldReturnValidation(int offset, int pageSize)
    {
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new ListMcpProvidersQuery(Offset: offset, PageSize: pageSize), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("MCP_PROVIDER_FILTER_INVALID");
    }

    [Fact]
    public async Task ListProvidersFirstPageShouldReportMoreResults()
    {
        await RegisterAsync();
        await RegisterAsync();
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new ListMcpProvidersQuery(PageSize: 1), TestContext.Current.CancellationToken);

        result.Value.Should().BeEquivalentTo(new { Offset = 0, PageSize = 1, HasMore = true });
    }

    [Fact]
    public async Task ListProvidersOffsetShouldReturnNextPage()
    {
        await RegisterAsync();
        await RegisterAsync();
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new ListMcpProvidersQuery(Offset: 1, PageSize: 1), TestContext.Current.CancellationToken);

        result.Value!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task ListProvidersOtherTenantShouldReturnEmptyPage()
    {
        await RegisterAsync();
        await SeedIdentityAsync(UserRole.Owner);
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new ListMcpProvidersQuery(), TestContext.Current.CancellationToken);

        result.Value!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProviderOtherTenantShouldReturnNotFound()
    {
        var providerId = await RegisterAsync();
        await SeedIdentityAsync(UserRole.Owner);
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new GetMcpProviderQuery(providerId), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("MCP_PROVIDER_NOT_FOUND");
    }

    [Fact]
    public async Task GetProviderValidQueryShouldProjectCatalogAndLiveServers()
    {
        var providerId = await RegisterAsync();
        var endpoint = new Uri("https://provider.example.test/mcp");
        using (var scope = Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new AddMcpServerCommand(providerId, endpoint, "secret-reference"), TestContext.Current.CancellationToken);
            var deleted = await sender.Send(new AddMcpServerCommand(providerId, new Uri("https://deleted.example.test/mcp"), "deleted-secret"), TestContext.Current.CancellationToken);
            await sender.Send(new RemoveMcpServerCommand(providerId, deleted.Value), TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<McpDbContext>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var verification = Services.CreateScope();

        var result = await verification.ServiceProvider.GetRequiredService<ISender>().Send(
            new GetMcpProviderQuery(providerId), TestContext.Current.CancellationToken);

        result.Value.Should().BeEquivalentTo(new { Id = providerId, Name = "Provider",
            Categories = new List<string> { "CERAMICS" }, GeoRegions = new List<string> { "ES-VC" },
            Servers = new[] { new { Endpoint = endpoint } } });
    }

    [Fact]
    public async Task GetProviderProjectionShouldNotTrackEntities()
    {
        var providerId = await RegisterAsync();
        using var scope = Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMcpProviderQuery(providerId), TestContext.Current.CancellationToken);

        scope.ServiceProvider.GetRequiredService<McpDbContext>().ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task ListProvidersViewerShouldReturnUnauthorized()
    {
        await SeedIdentityAsync(UserRole.Viewer);
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ListMcpProvidersQuery(), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("IDENTITY_TENANT_MANAGEMENT_FORBIDDEN");
    }

    [Fact]
    public async Task GetProviderEmptyIdentifierShouldReturnValidation()
    {
        using var scope = Services.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetMcpProviderQuery(Guid.Empty), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("MCP_PROVIDER_ID_INVALID");
    }

    private ServiceProvider Services => _services ?? throw new InvalidOperationException();

    private static RegisterMcpProviderCommand NewRegistration() => new("Provider", ["ceramics"], ["ES-VC"]);

    private async Task<Guid> RegisterAsync()
    {
        using var scope = Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(NewRegistration(), TestContext.Current.CancellationToken).ConfigureAwait(false);
        await scope.ServiceProvider.GetRequiredService<McpDbContext>().SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return result.Value;
    }

    private async Task SeedIdentityAsync(UserRole role, UserStatus status = UserStatus.Active)
    {
        using var scope = Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var tenant = Tenant.Create("Provider tenant", TenantSlug.Create($"provider-{Guid.NewGuid():N}").Value!,
            TenantType.Company, null, null, "test-plan").Value!;
        var user = User.Create(tenant.Id, "Provider", "Manager", Email.Create($"{Guid.NewGuid():N}@example.test").Value!,
            null, status == UserStatus.Active ? "test-password-hash" : null, status).Value!;
        _tenantContext.TenantId = tenant.Id;
        _tenantContext.UserId = user.Id;
        identity.Tenants.Add(tenant);
        identity.Users.Add(user);
        identity.TenantUsers.Add(TenantUser.Create(user, role).Value!);
        await identity.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public bool IsAuthenticated { get; set; } = true;
    }
}
