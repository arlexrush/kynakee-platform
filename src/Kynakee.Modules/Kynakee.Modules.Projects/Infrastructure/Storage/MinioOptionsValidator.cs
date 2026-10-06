using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Modules.Projects.Infrastructure.Storage;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The validator is instantiated through dependency injection.")]
internal sealed class MinioOptionsValidator : IValidateOptions<MinioOptions>
{
    public ValidateOptionsResult Validate(string? name, MinioOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        var expectedScheme = options.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) ||
            !string.Equals(endpoint.Scheme, expectedScheme, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
        {
            failures.Add("MinIO endpoint must be a credential-free HTTP(S) endpoint matching UseSsl.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey) ||
            string.IsNullOrWhiteSpace(options.SecretKey))
        {
            failures.Add("MinIO access and secret keys must be supplied through external configuration.");
        }

        if (!IsValidBucket(options.Bucket))
        {
            failures.Add("MinIO bucket must be a valid lowercase S3 bucket name.");
        }

        if (options.MaxObjectBytes is < 1 or > 104_857_600)
        {
            failures.Add("MinIO object size limit must be between 1 byte and 100 MiB.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidBucket(string bucket)
    {
        if (bucket.Length is < 3 or > 63 ||
            bucket.Any(character =>
                !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-')) ||
            bucket[0] is not (>= 'a' and <= 'z' or >= '0' and <= '9') ||
            bucket[^1] is not (>= 'a' and <= 'z' or >= '0' and <= '9') ||
            bucket.Contains("..", StringComparison.Ordinal) ||
            bucket.Contains(".-", StringComparison.Ordinal) ||
            bucket.Contains("-.", StringComparison.Ordinal))
        {
            return false;
        }

        return !System.Net.IPAddress.TryParse(bucket, out _);
    }
}
