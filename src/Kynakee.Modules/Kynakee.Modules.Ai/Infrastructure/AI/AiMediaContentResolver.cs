using System.Net.Http.Headers;
using Kynakee.Modules.AI.Contracts;
using Kynakee.Modules.Ai.Domain.Services;
using Kynakee.Modules.Ai.Domain.Resources;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Kynakee.Modules.Ai.Infrastructure.AI;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Registered and invoked by AI orchestration.")]
internal sealed class AiMediaContentResolver
{
    private readonly ITrustedMediaContentSource _mediaContentSource;
    private readonly long _maxMediaBytes;

    public AiMediaContentResolver(
        ITrustedMediaContentSource mediaContentSource,
        IOptions<AiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(mediaContentSource);
        ArgumentNullException.ThrowIfNull(options);
        _mediaContentSource = mediaContentSource;
        _maxMediaBytes = options.Value.MaxMediaBytes;
    }

    public async Task<Result<IReadOnlyList<AiPreparedMedia>>> ResolveAsync(
        Guid tenantId,
        Guid projectId,
        IReadOnlyList<MediaFileRef> mediaFiles,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mediaFiles);

        if (tenantId == Guid.Empty || projectId == Guid.Empty)
        {
            return Failure("AI_053", "MediaSourceUnavailable");
        }

        var preparedMedia = new List<AiPreparedMedia>(mediaFiles.Count);
        long totalBytes = 0;
        foreach (var mediaFile in mediaFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (mediaFile.Url is null ||
                mediaFile.Url.IsAbsoluteUri &&
                (!string.IsNullOrEmpty(mediaFile.Url.UserInfo) ||
                 !string.IsNullOrEmpty(mediaFile.Url.Fragment)) ||
                AiMediaTypeClassifier.Classify(mediaFile.MimeType) is null ||
                !MediaTypeHeaderValue.TryParse(mediaFile.MimeType, out _))
            {
                return Failure("AI_041", "ChatRequestInvalid");
            }

            var reference = new TrustedMediaReference(
                tenantId,
                projectId,
                mediaFile.Url,
                mediaFile.MimeType);
            TrustedMediaContent? content;
            try
            {
                content = await _mediaContentSource
                    .OpenReadAsync(reference, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (IOException)
            {
                return Failure("AI_053", "MediaSourceUnavailable");
            }

            if (content is null)
            {
                return Failure("AI_053", "MediaSourceUnavailable");
            }

            await using (content.ConfigureAwait(false))
            {
                if (content.Length > _maxMediaBytes)
                {
                    return Failure("AI_054", "MediaTooLarge");
                }

                if (!content.Content.CanRead || content.Length == 0)
                {
                    return Failure("AI_053", "MediaSourceUnavailable");
                }

                if (!string.Equals(
                        NormalizeMimeType(content.MimeType),
                        NormalizeMimeType(mediaFile.MimeType),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Failure("AI_055", "MediaMetadataMismatch");
                }

                var bytes = await ReadLimitedAsync(
                    content.Content,
                    content.Length,
                    _maxMediaBytes - totalBytes,
                    cancellationToken)
                    .ConfigureAwait(false);
                if (bytes is null)
                {
                    return Failure("AI_054", "MediaTooLarge");
                }

                totalBytes += bytes.LongLength;
                preparedMedia.Add(new AiPreparedMedia(
                    bytes,
                    NormalizeMimeType(content.MimeType),
                    mediaFile.FileName,
                    mediaFile.Room,
                    mediaFile.IsPathology));
            }
        }

        return ResultFactory.Success<IReadOnlyList<AiPreparedMedia>>(preparedMedia);
    }

    private static async Task<byte[]?> ReadLimitedAsync(
        Stream stream,
        long declaredLength,
        long remainingCapacity,
        CancellationToken cancellationToken)
    {
        if (declaredLength > remainingCapacity)
        {
            return null;
        }

        using var output = new MemoryStream(
            checked((int)Math.Min(declaredLength, remainingCapacity)));
        var buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)
                   .ConfigureAwait(false)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > remainingCapacity)
            {
                return null;
            }

            await output.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return totalBytes == declaredLength
            ? output.ToArray()
            : null;
    }

    private static string NormalizeMimeType(string mimeType) =>
        MediaTypeHeaderValue.TryParse(mimeType, out var parsedMimeType) &&
        parsedMimeType.MediaType is { } mediaType
            ? mediaType
            : string.Empty;

    private static Result<IReadOnlyList<AiPreparedMedia>> Failure(
        string code,
        string messageKey) =>
        ResultFactory.Failure<IReadOnlyList<AiPreparedMedia>>(
            ApplicationError.Validation(code, AiMessages.Get(messageKey)));
}
