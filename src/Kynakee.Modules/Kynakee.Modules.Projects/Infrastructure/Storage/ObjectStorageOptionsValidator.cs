using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Kynakee.Modules.Projects.Infrastructure.Storage;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The validator is instantiated through dependency injection.")]
internal sealed class ObjectStorageOptionsValidator : IValidateOptions<ObjectStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, ObjectStorageOptions options)
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
            endpoint.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
        {
            failures.Add(ObjectStorageMessages.Get("EndpointInvalid"));
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey) || string.IsNullOrWhiteSpace(options.SecretKey))
        {
            failures.Add(ObjectStorageMessages.Get("CredentialsRequired"));
        }

        if (!IsValidBucket(options.Bucket))
        {
            failures.Add(ObjectStorageMessages.Get("BucketInvalid"));
        }

        if (options.MaxObjectBytes is < 1 or > 104_857_600)
        {
            failures.Add(ObjectStorageMessages.Get("SizeInvalid"));
        }

        if (string.IsNullOrWhiteSpace(options.Region) ||
            options.Region.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            failures.Add(ObjectStorageMessages.Get("RegionInvalid"));
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidBucket(string bucket)
    {
        if (string.IsNullOrWhiteSpace(bucket) || bucket.Length is < 3 or > 63 ||
            bucket.Any(character => !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-')) ||
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
