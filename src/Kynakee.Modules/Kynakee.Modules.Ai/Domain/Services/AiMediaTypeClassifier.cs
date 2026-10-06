using System.Net.Http.Headers;
using Kynakee.Modules.Ai.Domain.Enums;

namespace Kynakee.Modules.Ai.Domain.Services;

public static class AiMediaTypeClassifier
{
    public static AiMediaModality? Classify(string? mimeType)
    {
        if (!MediaTypeHeaderValue.TryParse(mimeType, out var parsedMimeType) ||
            parsedMimeType.MediaType is not { } mediaType)
        {
            return null;
        }

        return mediaType.ToUpperInvariant() switch
        {
            "APPLICATION/PDF" => AiMediaModality.Pdf,
            var value when value.StartsWith("IMAGE/", StringComparison.Ordinal) =>
                AiMediaModality.Image,
            var value when value.StartsWith("AUDIO/", StringComparison.Ordinal) =>
                AiMediaModality.Audio,
            var value when value.StartsWith("VIDEO/", StringComparison.Ordinal) =>
                AiMediaModality.Video,
            _ => null
        };
    }

    public static ModelCapability ToModelCapability(AiMediaModality modality) =>
        modality switch
        {
            AiMediaModality.Pdf => ModelCapability.Pdf,
            AiMediaModality.Image => ModelCapability.Vision,
            AiMediaModality.Audio => ModelCapability.Audio,
            AiMediaModality.Video => ModelCapability.Video,
            _ => throw new ArgumentOutOfRangeException(nameof(modality), modality, null)
        };
}
