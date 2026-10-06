using Kynakee.Modules.Projects.Infrastructure.Persistence;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using System.Net.Http.Headers;
using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Modules.Projects.Infrastructure.Storage;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Registered as the Projects ITrustedMediaContentSource implementation.")]
internal sealed class MinioTrustedMediaContentSource : ITrustedMediaContentSource
{
    private readonly ProjectsDbContext _dbContext;
    private readonly IMinioClient _minioClient;
    private readonly MinioOptions _options;

    public MinioTrustedMediaContentSource(
        ProjectsDbContext dbContext,
        IMinioClient minioClient,
        IOptions<MinioOptions> options)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(minioClient);
        ArgumentNullException.ThrowIfNull(options);

        _dbContext = dbContext;
        _minioClient = minioClient;
        _options = options.Value;
    }

    public async Task<TrustedMediaContent?> OpenReadAsync(
        TrustedMediaReference reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled ||
            reference.TenantId == Guid.Empty ||
            reference.ProjectId == Guid.Empty ||
            !TryGetObjectKey(reference.StorageUri, out var bucket, out var objectKey) ||
            !string.Equals(bucket, _options.Bucket, StringComparison.Ordinal))
        {
            return null;
        }

        var isOwnedByProject = await _dbContext.Projects
            .AsNoTracking()
            .Where(project => project.Id.Value == reference.ProjectId &&
                              project.TenantId == reference.TenantId)
            .Select(project => project.Capture != null &&
                              project.Capture.MediaFiles.Any(media => media.Url == reference.StorageUri))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!isOwnedByProject)
        {
            return null;
        }

        var stat = await _minioClient.StatObjectAsync(
                new StatObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectKey),
                cancellationToken)
            .ConfigureAwait(false);
        if (stat.Size is < 1 || stat.Size > _options.MaxObjectBytes)
        {
            throw new IOException("The MinIO object exceeds the configured media size limit or is empty.");
        }

        var declaredMimeType = NormalizeMimeType(reference.DeclaredMimeType);
        var storedMimeType = NormalizeMimeType(stat.ContentType);
        if (declaredMimeType is null || storedMimeType is null ||
            !string.Equals(declaredMimeType, storedMimeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The MinIO object MIME type does not match its capture metadata.");
        }

        var output = new MemoryStream(checked((int)stat.Size));
        try
        {
            await _minioClient.GetObjectAsync(
                    new GetObjectArgs()
                        .WithBucket(bucket)
                        .WithObject(objectKey)
                        .WithCallbackStream(stream => stream.CopyToAsync(output, cancellationToken)),
                    cancellationToken)
                .ConfigureAwait(false);
            output.Position = 0;
            return new TrustedMediaContent(output, storedMimeType, stat.Size);
        }
        catch
        {
            await output.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static bool TryGetObjectKey(
        Uri storageUri,
        out string bucket,
        out string objectKey)
    {
        bucket = string.Empty;
        objectKey = string.Empty;
        if (!storageUri.IsAbsoluteUri ||
            !string.Equals(storageUri.Scheme, "minio", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(storageUri.UserInfo) ||
            !string.IsNullOrEmpty(storageUri.Query) ||
            !string.IsNullOrEmpty(storageUri.Fragment))
        {
            return false;
        }

        bucket = storageUri.Host;
        objectKey = Uri.UnescapeDataString(storageUri.AbsolutePath.TrimStart('/'));
        return !string.IsNullOrWhiteSpace(bucket) &&
               !string.IsNullOrWhiteSpace(objectKey) &&
               !objectKey.Split('/').Any(segment => segment is "" or "." or "..");
    }

    private static string? NormalizeMimeType(string? mimeType) =>
        MediaTypeHeaderValue.TryParse(mimeType, out var parsed) ? parsed.MediaType : null;
}
