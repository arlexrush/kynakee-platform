using Amazon.S3;
using Amazon.S3.Model;
using Kynakee.Modules.Projects;
using Kynakee.Modules.Projects.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

if (args.Length != 3 || args[0] is not ("write" or "read" or "delete") ||
    !Uri.TryCreate(args[1], UriKind.Absolute, out var endpoint) || !endpoint.IsLoopback ||
    endpoint.Scheme != Uri.UriSchemeHttp || !string.IsNullOrEmpty(endpoint.UserInfo) ||
    endpoint.AbsolutePath != "/" || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment) ||
    !Guid.TryParseExact(args[2], "N", out var operationId))
{
    return 2;
}

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ObjectStorage:Enabled"] = "true",
    ["ObjectStorage:Endpoint"] = endpoint.AbsoluteUri,
    ["ObjectStorage:AccessKey"] = "synthetic-access",
    ["ObjectStorage:SecretKey"] = "synthetic-secret",
    ["ObjectStorage:Bucket"] = "kynakee-media-dev",
    ["ObjectStorage:Region"] = "us-east-1",
    ["ObjectStorage:UseSsl"] = "false"
}).Build();
var services = new ServiceCollection();
services.AddLogging();
services.AddProjectsModule(configuration);
var provider = services.BuildServiceProvider();
await using var providerLifetime = provider.ConfigureAwait(false);
var client = provider.GetRequiredService<IAmazonS3>();
var key = $"verification/{operationId:N}.txt";
var payload = Encoding.UTF8.GetBytes($"synthetic-storage-verification-{operationId:N}");
if (args[0] == "write")
{
    using var input = new MemoryStream(payload);
    await client.PutObjectAsync(S3UploadPolicy.CreateRequest("kynakee-media-dev", key, input,
        "text/plain", 104_857_600), timeout.Token).ConfigureAwait(false);
}
else if (args[0] == "delete")
{
    await client.DeleteObjectAsync(new DeleteObjectRequest
    {
        BucketName = "kynakee-media-dev", Key = key
    }, timeout.Token).ConfigureAwait(false);
}
else
{
    using var response = await client.GetObjectAsync(new GetObjectRequest
    {
        BucketName = "kynakee-media-dev", Key = key
    }, timeout.Token).ConfigureAwait(false);
    if (response.ContentLength != payload.Length || response.Headers.ContentType != "text/plain") return 1;
    var actual = new byte[payload.Length];
    await response.ResponseStream.ReadExactlyAsync(actual, timeout.Token).ConfigureAwait(false);
    var extra = new byte[1];
    if (await response.ResponseStream.ReadAsync(extra, timeout.Token).ConfigureAwait(false) != 0) return 1;
    if (!payload.AsSpan().SequenceEqual(actual)) return 1;
}

return 0;
