using System.Net;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Volumes;
using DotNet.Testcontainers.Networks;
using Kynakee.Modules.Projects;
using Kynakee.Modules.Projects.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Amazon.S3;
using Amazon.S3.Model;
using Xunit;

namespace Kynakee.IntegrationTests.Storage;

public sealed class SeaweedFsS3Tests : IAsyncLifetime
{
    private const string Bucket = "kynakee-seaweed-test";
    private const string ObjectKey = "capture/synthetic.txt";
    private const string Payload = "synthetic-persistent-media";
    private readonly IVolume _volume = new VolumeBuilder().Build();
    private readonly INetwork _network = new NetworkBuilder().Build();

    public async ValueTask InitializeAsync()
    {
        await _volume.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await _network.CreateAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    public async ValueTask DisposeAsync()
    {
        await _volume.DisposeAsync().ConfigureAwait(true);
        await _network.DisposeAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task SeaweedFsS3UploadShouldPreserveContentAsync()
    {
        await using var container = CreateContainer();
        await container.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await UploadAsync(container).ConfigureAwait(true);

        var content = await DownloadAsync(container).ConfigureAwait(true);

        Assert.Equal(Encoding.UTF8.GetBytes(Payload), content);
    }

    [Fact]
    public async Task SeaweedFsS3UploadShouldPreserveMetadataAsync()
    {
        await using var container = CreateContainer();
        await container.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await UploadAsync(container).ConfigureAwait(true);
        var provider = CreateProvider(container);
        await using var providerLifetime = provider.ConfigureAwait(true);

        var stat = await provider.GetRequiredService<IAmazonS3>().GetObjectMetadataAsync(
            new GetObjectMetadataRequest { BucketName = Bucket, Key = ObjectKey }, TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.Equal(((long)Encoding.UTF8.GetByteCount(Payload), "text/plain"), (stat.ContentLength, stat.Headers.ContentType));
    }

    [Fact]
    public async Task SeaweedFsS3AnonymousReadShouldReturnForbiddenAsync()
    {
        await using var container = CreateContainer();
        await container.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await UploadAsync(container).ConfigureAwait(true);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        using var response = await client.GetAsync(new Uri($"{Endpoint(container)}/{Bucket}/{ObjectKey}"),
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SeaweedFsS3GracefulRestartShouldPreserveContentAsync()
    {
        await using var container = CreateContainer();
        await container.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await UploadAsync(container).ConfigureAwait(true);

        await container.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await container.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var content = await DownloadAsync(container).ConfigureAwait(true);

        Assert.Equal(Encoding.UTF8.GetBytes(Payload), content);
    }

    [Fact]
    public async Task SeaweedFsS3ContainerRecreationShouldPreserveContentAsync()
    {
        await using (var original = CreateContainer())
        {
            await original.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            await UploadAsync(original).ConfigureAwait(true);
            await original.StopAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        }

        await using var replacement = CreateContainer();
        await replacement.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var content = await DownloadAsync(replacement).ConfigureAwait(true);

        Assert.Equal(Encoding.UTF8.GetBytes(Payload), content);
    }

    [Fact]
    public async Task SeaweedFsInitializerRepeatedShouldPreserveContentAsync()
    {
        await using var container = CreateContainer();
        await container.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var initializer = CreateInitializer("synthetic-secret");
        await initializer.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var first = await initializer.ExecAsync(["/bin/sh", "/init/init-s3.sh"], TestContext.Current.CancellationToken).ConfigureAwait(true);
        Assert.Equal(0, first.ExitCode);
        await UploadObjectAsync(container).ConfigureAwait(true);

        var repeated = await initializer.ExecAsync(["/bin/sh", "/init/init-s3.sh"], TestContext.Current.CancellationToken).ConfigureAwait(true);
        Assert.Equal(0, repeated.ExitCode);
        var content = await DownloadAsync(container).ConfigureAwait(true);

        Assert.Equal(Encoding.UTF8.GetBytes(Payload), content);
    }

    [Fact]
    public async Task SeaweedFsInitializerInvalidCredentialsShouldFailWithoutCreatingBucketAsync()
    {
        await using var container = CreateContainer();
        await container.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using var initializer = CreateInitializer("invalid-synthetic-secret");
        await initializer.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var result = await initializer.ExecAsync(["/bin/sh", "/init/init-s3.sh"], TestContext.Current.CancellationToken).ConfigureAwait(true);

        Assert.NotEqual(0, result.ExitCode);
        var provider = CreateProvider(container);
        await using var providerLifetime = provider.ConfigureAwait(true);
        var buckets = await provider.GetRequiredService<IAmazonS3>().ListBucketsAsync(
            new ListBucketsRequest(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        Assert.DoesNotContain(buckets.Buckets ?? [], bucket => bucket.BucketName == Bucket);
    }

    private IContainer CreateInitializer(string secret) => new ContainerBuilder("amazon/aws-cli:2.27.49")
        .WithNetwork(_network)
        .WithResourceMapping(new FileInfo(Path.Combine(AppContext.BaseDirectory, "Storage", "init-s3.sh")), "/init/")
        .WithEnvironment("AWS_ACCESS_KEY_ID", "synthetic-access")
        .WithEnvironment("AWS_SECRET_ACCESS_KEY", secret)
        .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
        .WithEnvironment("AWS_EC2_METADATA_DISABLED", "true")
        .WithEnvironment("AWS_PAGER", "")
        .WithEnvironment("OBJECT_STORAGE_BUCKET", Bucket)
        .WithEntrypoint("/bin/sh")
        .WithCommand("-c", "while :; do sleep 1; done")
        .Build();

    private IContainer CreateContainer() => new ContainerBuilder("chrislusf/seaweedfs:3.85")
        .WithNetwork(_network)
        .WithNetworkAliases("seaweedfs")
        .WithResourceMapping(new FileInfo(Path.Combine(AppContext.BaseDirectory, "Storage", "s3.dev.json")), "/etc/seaweedfs/")
        .WithVolumeMount(_volume, "/data")
        .WithCommand("server", "-ip=127.0.0.1", "-ip.bind=0.0.0.0", "-dir=/data",
            "-master.volumeSizeLimitMB=32", "-master.volumePreallocate=false", "-volume.max=4",
            "-filer", "-s3", "-s3.config=/etc/seaweedfs/s3.dev.json")
        .WithPortBinding(8333, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
            .ForPort(8333).ForPath("/").ForStatusCode(HttpStatusCode.Forbidden)))
        .Build();

    private static async Task UploadAsync(IContainer container)
    {
        var provider = CreateProvider(container);
        await using var providerLifetime = provider.ConfigureAwait(true);
        var client = provider.GetRequiredService<IAmazonS3>();
        await client.PutBucketAsync(new PutBucketRequest { BucketName = Bucket }, TestContext.Current.CancellationToken).ConfigureAwait(true);
        await UploadObjectAsync(container).ConfigureAwait(true);
    }

    private static async Task UploadObjectAsync(IContainer container)
    {
        var provider = CreateProvider(container);
        await using var providerLifetime = provider.ConfigureAwait(true);
        var client = provider.GetRequiredService<IAmazonS3>();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Payload));
        await client.PutObjectAsync(S3UploadPolicy.CreateRequest(Bucket, ObjectKey, input, "text/plain", 1024),
            TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static async Task<byte[]> DownloadAsync(IContainer container)
    {
        // La lectura posterior al arranque no crea buckets ni repone objetos: debe usar los datos del volumen.
        var provider = CreateProvider(container);
        await using var providerLifetime = provider.ConfigureAwait(true);
        using var output = new MemoryStream();
        using var response = await provider.GetRequiredService<IAmazonS3>().GetObjectAsync(
            new GetObjectRequest { BucketName = Bucket, Key = ObjectKey },
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        await response.ResponseStream.CopyToAsync(output, TestContext.Current.CancellationToken).ConfigureAwait(true);
        return output.ToArray();
    }

    private static string Endpoint(IContainer container) => $"http://{container.Hostname}:{container.GetMappedPublicPort(8333)}";

    private static ServiceProvider CreateProvider(IContainer container)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ObjectStorage:Enabled"] = "true",
            ["ObjectStorage:Endpoint"] = Endpoint(container),
            ["ObjectStorage:Region"] = "us-east-1",
            ["ObjectStorage:AccessKey"] = "synthetic-access",
            ["ObjectStorage:SecretKey"] = "synthetic-secret",
            ["ObjectStorage:Bucket"] = Bucket,
            ["ObjectStorage:UseSsl"] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProjectsModule(configuration);
        return services.BuildServiceProvider();
    }
}
