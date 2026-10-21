using Kynakee.Modules.Projects;
using Kynakee.Modules.Projects.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Amazon.S3;
using Xunit;

namespace Kynakee.UnitTests.Projects.Infrastructure;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1515", Justification = "xUnit test classes are public for test discovery.")]
public sealed class ObjectStorageConfigurationTests
{
    [Theory]
    [InlineData("http://seaweedfs:8333", "false", "us-east-1")]
    [InlineData("https://fsn1.your-objectstorage.com", "true", "fsn1")]
    public void StorageValidConfigurationShouldConstructS3Client(string endpoint, string ssl, string region)
    {
        using var provider = CreateProvider(endpoint, ssl, region);

        Assert.NotNull(provider.GetRequiredService<IAmazonS3>());
    }

    [Theory]
    [InlineData("http://seaweedfs:8333/bucket", "false", "us-east-1")]
    [InlineData("http://seaweedfs:8333", "true", "us-east-1")]
    [InlineData("https://user:password@example.test", "true", "us-east-1")]
    [InlineData("https://fsn1.your-objectstorage.com", "true", "")]
    public void StorageInvalidConfigurationShouldRejectClient(string endpoint, string ssl, string region)
    {
        using var provider = CreateProvider(endpoint, ssl, region);

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IAmazonS3>());
    }

    [Theory]
    [InlineData("http://seaweedfs:8333", "false", "us-east-1")]
    [InlineData("https://fsn1.your-objectstorage.com", "true", "fsn1")]
    public void StorageValidConfigurationShouldUseRegionalPathStyle(string endpoint, string ssl, string region)
    {
        using var provider = CreateProvider(endpoint, ssl, region);

        var configuration = (AmazonS3Config)provider.GetRequiredService<IAmazonS3>().Config;

        Assert.Equal((true, region), (configuration.ForcePathStyle, configuration.AuthenticationRegion));
    }

    private static ServiceProvider CreateProvider(string endpoint, string ssl, string region)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{ObjectStorageOptions.SectionName}:Enabled"] = "true",
            [$"{ObjectStorageOptions.SectionName}:Endpoint"] = endpoint,
            [$"{ObjectStorageOptions.SectionName}:AccessKey"] = "synthetic-access",
            [$"{ObjectStorageOptions.SectionName}:SecretKey"] = "synthetic-secret",
            [$"{ObjectStorageOptions.SectionName}:Bucket"] = "kynakee-media-test",
            [$"{ObjectStorageOptions.SectionName}:UseSsl"] = ssl,
            [$"{ObjectStorageOptions.SectionName}:Region"] = region
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProjectsModule(configuration);
        return services.BuildServiceProvider();
    }
}
