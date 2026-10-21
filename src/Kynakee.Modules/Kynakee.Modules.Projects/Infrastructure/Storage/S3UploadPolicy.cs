using Amazon.S3.Model;
using System.Net.Http.Headers;

namespace Kynakee.Modules.Projects.Infrastructure.Storage;

/// <summary>Builds single-object uploads with a known length and signed, non-chunked payload.</summary>
public static class S3UploadPolicy
{
    /// <summary>
    /// Creates a request for a readable, seekable stream positioned at its beginning.
    /// The caller owns the stream and must keep it unchanged until the awaited upload finishes.
    /// This policy does not authorize access, reserve object keys or provide multipart uploads.
    /// </summary>
    public static PutObjectRequest CreateRequest(string bucket, string key, Stream content, string mimeType, long maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumBytes, 104_857_600);
        if (!content.CanRead || !content.CanSeek || content.Position != 0)
        {
            throw new ArgumentException(ObjectStorageMessages.Get("UploadStreamInvalid"), nameof(content));
        }

        if (content.Length < 1 || content.Length > maximumBytes)
        {
            throw new ArgumentException(ObjectStorageMessages.Get("ObjectSizeInvalid"), nameof(content));
        }

        if (!MediaTypeHeaderValue.TryParse(mimeType, out var parsed))
        {
            throw new ArgumentException(ObjectStorageMessages.Get("ObjectMimeInvalid"), nameof(mimeType));
        }

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            ContentType = parsed.ToString(),
            UseChunkEncoding = false,
            DisablePayloadSigning = false,
            AutoCloseStream = false,
            AutoResetStreamPosition = false
        };
        request.Headers.ContentLength = content.Length;
        return request;
    }
}
