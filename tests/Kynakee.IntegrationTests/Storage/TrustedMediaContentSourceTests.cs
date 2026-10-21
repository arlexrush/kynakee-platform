using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Kynakee.Modules.Projects;
using Kynakee.Modules.Projects.Domain.Aggregates;
using Kynakee.Modules.Projects.Domain.Entities.DataCapture;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.Projects.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testcontainers.PostgreSql;
using Xunit;

namespace Kynakee.IntegrationTests.Storage;

public sealed class TrustedMediaContentSourceTests : IClassFixture<TrustedMediaStorageFixture>
{
    private readonly TrustedMediaStorageFixture _fixture;

    public TrustedMediaContentSourceTests(TrustedMediaStorageFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task MediaOwnedReferenceShouldReturnExactContentAsync()
    {
        using var provider = _fixture.CreateProvider(Guid.NewGuid());
        using var scope = provider.CreateScope();
        var reference = await SeedAsync(scope.ServiceProvider).ConfigureAwait(true);
        await UploadAsync(scope.ServiceProvider, reference, "synthetic-media", "text/plain").ConfigureAwait(true);

        var content = await scope.ServiceProvider.GetRequiredService<ITrustedMediaContentSource>()
            .OpenReadAsync(reference, TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.NotNull(content);
        await using var lifetime = content.ConfigureAwait(true);
        using var output = new MemoryStream();
        await content.Content.CopyToAsync(output, TestContext.Current.CancellationToken).ConfigureAwait(true);
        Assert.Equal(Encoding.UTF8.GetBytes("synthetic-media"), output.ToArray());
    }

    [Theory]
    [InlineData("foreignTenant")]
    [InlineData("forgedTenant")]
    [InlineData("foreignProject")]
    [InlineData("unregisteredObject")]
    [InlineData("foreignBucket")]
    public async Task MediaUnownedReferenceShouldRejectBeforeS3Async(string scenario)
    {
        var tenant = Guid.NewGuid();
        var client = Substitute.For<IAmazonS3>();
        using var provider = _fixture.CreateProvider(tenant, client);
        using var scope = provider.CreateScope();
        var owned = await SeedAsync(scope.ServiceProvider).ConfigureAwait(true);
        var reference = scenario switch
        {
            "foreignTenant" => owned with { TenantId = Guid.NewGuid() },
            "forgedTenant" => await SeedAsync(scope.ServiceProvider, Guid.NewGuid()).ConfigureAwait(true) with { TenantId = tenant },
            "foreignProject" => owned with { ProjectId = Guid.NewGuid() },
            "unregisteredObject" => owned with { StorageUri = new Uri($"s3://{TrustedMediaStorageFixture.Bucket}/unregistered.txt") },
            "foreignBucket" => owned with { StorageUri = new Uri("s3://another-bucket/object.txt") },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        var result = await scope.ServiceProvider.GetRequiredService<ITrustedMediaContentSource>()
            .OpenReadAsync(reference, TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.Null(result);
        await client.DidNotReceive().GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public async Task MediaStoredMimeMismatchShouldRejectAsync()
    {
        using var provider = _fixture.CreateProvider(Guid.NewGuid());
        using var scope = provider.CreateScope();
        var reference = await SeedAsync(scope.ServiceProvider).ConfigureAwait(true);
        await UploadAsync(scope.ServiceProvider, reference, "synthetic-media", "application/pdf").ConfigureAwait(true);

        await Assert.ThrowsAsync<InvalidDataException>(() => scope.ServiceProvider.GetRequiredService<ITrustedMediaContentSource>()
            .OpenReadAsync(reference, TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    [Theory]
    [InlineData("", 16)]
    [InlineData("synthetic-media", 4)]
    public async Task MediaInvalidObjectSizeShouldRejectAsync(string payload, int maximum)
    {
        using var provider = _fixture.CreateProvider(Guid.NewGuid(), maximum: maximum);
        using var scope = provider.CreateScope();
        var reference = await SeedAsync(scope.ServiceProvider).ConfigureAwait(true);
        await UploadAsync(scope.ServiceProvider, reference, payload, "text/plain").ConfigureAwait(true);

        await Assert.ThrowsAsync<IOException>(() => scope.ServiceProvider.GetRequiredService<ITrustedMediaContentSource>()
            .OpenReadAsync(reference, TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(1, 2)]
    public async Task MediaInconsistentStreamLengthShouldRejectAndDisposeAsync(int actualLength, long declaredLength)
    {
        using var stream = new MemoryStream(new byte[actualLength]);
        using var handler = new InconsistentLengthHandler(stream, declaredLength);
        using var client = new AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials("synthetic-access", "synthetic-secret"),
            new AmazonS3Config
            {
                ServiceURL = "http://127.0.0.1:8333",
                AuthenticationRegion = "us-east-1",
                ForcePathStyle = true,
                MaxErrorRetry = 0,
                HttpClientFactory = new MediaHttpClientFactory(handler)
            });
        using var provider = _fixture.CreateProvider(Guid.NewGuid(), client);
        using var scope = provider.CreateScope();
        var reference = await SeedAsync(scope.ServiceProvider).ConfigureAwait(true);

        await Assert.ThrowsAsync<IOException>(() => scope.ServiceProvider.GetRequiredService<ITrustedMediaContentSource>()
            .OpenReadAsync(reference, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        Assert.False(stream.CanRead);
    }

    private sealed class MediaHttpClientFactory(HttpMessageHandler handler) : Amazon.Runtime.HttpClientFactory
    {
        public override HttpClient CreateHttpClient(Amazon.Runtime.IClientConfig clientConfig) => new(handler, false);
    }

    private sealed class InconsistentLengthHandler(Stream stream, long declaredLength) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) };
            response.Content.Headers.ContentLength = declaredLength;
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task MediaCancelledRequestShouldAvoidS3Async()
    {
        var client = Substitute.For<IAmazonS3>();
        using var provider = _fixture.CreateProvider(Guid.NewGuid(), client);
        using var scope = provider.CreateScope();
        var reference = await SeedAsync(scope.ServiceProvider).ConfigureAwait(true);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync().ConfigureAwait(true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider.GetRequiredService<ITrustedMediaContentSource>()
            .OpenReadAsync(reference, cancellation.Token)).ConfigureAwait(true);

        await client.DidNotReceive().GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    private static async Task<TrustedMediaReference> SeedAsync(IServiceProvider services, Guid? tenant = null)
    {
        var context = services.GetRequiredService<ProjectsDbContext>();
        var tenantId = tenant ?? context.CurrentTenantId;
        var uri = new Uri($"s3://{TrustedMediaStorageFixture.Bucket}/capture/{Guid.NewGuid():N}.txt");
        var created = Project.Create("Synthetic media project", new ClientInfo("Synthetic client"), new GeoLocation("ES"), ProjectChannel.Web, tenantId);
        Assert.True(created.IsSuccess);
        var project = created.Value!;
        Assert.True(project.StartCapture().IsSuccess);
        var capture = CaptureExpedient.Create(tenantId, project.Id, [new CaptureMediaFile(uri, "text/plain", null, false, false)]);
        Assert.True(capture.IsSuccess);
        Assert.True(project.CompleteCapture(capture.Value!).IsSuccess);
        context.Projects.Add(project);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        context.ChangeTracker.Clear();
        return new TrustedMediaReference(tenantId, project.Id.Value, uri, "text/plain");
    }

    private static async Task UploadAsync(IServiceProvider services, TrustedMediaReference reference, string payload, string mime)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        await services.GetRequiredService<IAmazonS3>().PutObjectAsync(new PutObjectRequest
        {
            BucketName = TrustedMediaStorageFixture.Bucket,
            Key = reference.StorageUri.AbsolutePath.TrimStart('/'),
            InputStream = stream,
            ContentType = mime,
            UseChunkEncoding = false
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1515", Justification = "The fixture must be public for xUnit class fixture injection.")]
public sealed class TrustedMediaStorageFixture : IAsyncLifetime
{
    internal const string Bucket = "kynakee-trusted-media-test";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly IContainer _storage = new ContainerBuilder("chrislusf/seaweedfs:3.85")
        .WithResourceMapping(new FileInfo(Path.Combine(AppContext.BaseDirectory, "Storage", "s3.dev.json")), "/etc/seaweedfs/")
        .WithCommand("server", "-ip=127.0.0.1", "-ip.bind=0.0.0.0", "-dir=/data",
            "-master.volumeSizeLimitMB=32", "-master.volumePreallocate=false", "-volume.max=4", "-filer", "-s3", "-s3.config=/etc/seaweedfs/s3.dev.json")
        .WithPortBinding(8333, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
            .ForPort(8333).ForPath("/").ForStatusCode(HttpStatusCode.Forbidden)))
        .Build();

    /// <summary>Creates only the ephemeral schema and bucket needed by the media tests.</summary>
    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await _storage.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var provider = CreateProvider(Guid.NewGuid());
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProjectsDbContext>().Database
            .EnsureCreatedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await provider.GetRequiredService<IAmazonS3>().PutBucketAsync(new PutBucketRequest { BucketName = Bucket },
            TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Releases only the containers owned by this fixture.</summary>
    public async ValueTask DisposeAsync()
    {
        await _storage.DisposeAsync().ConfigureAwait(true);
        await _postgres.DisposeAsync().ConfigureAwait(true);
    }

    internal ServiceProvider CreateProvider(Guid tenant, IAmazonS3? client = null, int maximum = 1024)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            ["ObjectStorage:Enabled"] = "true",
            ["ObjectStorage:Endpoint"] = $"http://{_storage.Hostname}:{_storage.GetMappedPublicPort(8333)}",
            ["ObjectStorage:AccessKey"] = "synthetic-access",
            ["ObjectStorage:SecretKey"] = "synthetic-secret",
            ["ObjectStorage:Bucket"] = Bucket,
            ["ObjectStorage:Region"] = "us-east-1",
            ["ObjectStorage:MaxObjectBytes"] = maximum.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantContext>(_ => new MediaTenantContext(tenant));
        services.AddProjectsModule(config);
        if (client is not null) services.AddSingleton(client);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class MediaTenantContext(Guid tenant) : ITenantContext
    {
        public Guid TenantId => tenant;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => true;
    }
}
