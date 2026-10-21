using Amazon.S3;
using Amazon.S3.Model;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Kynakee.Modules.Projects.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

namespace Kynakee.Modules.Projects.Infrastructure.Storage;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Registered as the Projects ITrustedMediaContentSource implementation.")]
internal sealed class S3TrustedMediaContentSource : ITrustedMediaContentSource
{
    private readonly ProjectsDbContext _dbContext;
    private readonly IAmazonS3 _client;
    private readonly ObjectStorageOptions _options;

    public S3TrustedMediaContentSource(ProjectsDbContext dbContext, IAmazonS3 client, IOptions<ObjectStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _dbContext = dbContext;
        _client = client;
        _options = options.Value;
    }

    public async Task<TrustedMediaContent?> OpenReadAsync(TrustedMediaReference reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled || reference.TenantId == Guid.Empty || reference.ProjectId == Guid.Empty ||
            !TryGetObjectKey(reference.StorageUri, out var bucket, out var objectKey) ||
            !string.Equals(bucket, _options.Bucket, StringComparison.Ordinal))
        {
            return null;
        }

        var projectId = new ProjectId(reference.ProjectId);
        var isOwnedByProject = await _dbContext.Projects.AsNoTracking()
            .Where(project => project.Id == projectId && project.TenantId == reference.TenantId)
            .Select(project => project.Capture != null && project.Capture.MediaFiles.Any(media => media.Url == reference.StorageUri))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (!isOwnedByProject)
        {
            return null;
        }

        using var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = objectKey
        }, cancellationToken).ConfigureAwait(false);
        var size = response.ContentLength;
        if (size is < 1 || size > _options.MaxObjectBytes)
        {
            throw new IOException(ObjectStorageMessages.Get("ObjectSizeInvalid"));
        }

        var declaredMimeType = NormalizeMimeType(reference.DeclaredMimeType);
        var storedMimeType = NormalizeMimeType(response.Headers.ContentType);
        if (declaredMimeType is null || storedMimeType is null ||
            !string.Equals(declaredMimeType, storedMimeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(ObjectStorageMessages.Get("ObjectMimeInvalid"));
        }

        var output = new MemoryStream(checked((int)size));
        try
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await response.ResponseStream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
            {
                if (read > size - output.Length)
                {
                    throw new IOException(ObjectStorageMessages.Get("ObjectSizeInvalid"));
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            if (output.Length != size)
            {
                throw new IOException(ObjectStorageMessages.Get("ObjectSizeInvalid"));
            }

            output.Position = 0;
            return new TrustedMediaContent(output, storedMimeType, size);
        }
        catch
        {
            await output.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static bool TryGetObjectKey(Uri storageUri, out string bucket, out string objectKey)
    {
        bucket = string.Empty;
        objectKey = string.Empty;
        if (!storageUri.IsAbsoluteUri || !string.Equals(storageUri.Scheme, "s3", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(storageUri.UserInfo) || !string.IsNullOrEmpty(storageUri.Query) ||
            !string.IsNullOrEmpty(storageUri.Fragment) || !storageUri.IsDefaultPort)
        {
            return false;
        }

        bucket = storageUri.Host;
        objectKey = Uri.UnescapeDataString(storageUri.AbsolutePath.TrimStart('/'));
        return !string.IsNullOrWhiteSpace(bucket) && !string.IsNullOrWhiteSpace(objectKey) &&
               !objectKey.Split('/').Any(segment => segment is "" or "." or "..");
    }

    private static string? NormalizeMimeType(string? mimeType) =>
        MediaTypeHeaderValue.TryParse(mimeType, out var parsed) ? parsed.MediaType : null;
}
