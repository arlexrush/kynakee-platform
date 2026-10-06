using FluentAssertions;
using Kynakee.Modules.Ai.Domain.Enums;
using Kynakee.Modules.Ai.Domain.Services;
using Xunit;

namespace Kynakee.UnitTests.Ai.Domain;

public class AiMediaTypeClassifierTests
{
    [Theory]
    [InlineData("application/pdf", AiMediaModality.Pdf)]
    [InlineData("application/pdf; charset=binary", AiMediaModality.Pdf)]
    [InlineData("image/jpeg", AiMediaModality.Image)]
    [InlineData("audio/mpeg", AiMediaModality.Audio)]
    [InlineData("video/mp4", AiMediaModality.Video)]
    public void ClassifyShouldReturnModalityForSupportedMediaType(
        string mimeType,
        AiMediaModality expectedModality)
    {
        var modality = AiMediaTypeClassifier.Classify(mimeType);

        modality.Should().Be(expectedModality);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("application/octet-stream")]
    [InlineData("not-a-mime-type")]
    public void ClassifyShouldRejectMissingOrUnsupportedMediaType(string? mimeType)
    {
        var modality = AiMediaTypeClassifier.Classify(mimeType);

        modality.Should().BeNull();
    }

    [Theory]
    [InlineData(AiMediaModality.Pdf, ModelCapability.Pdf)]
    [InlineData(AiMediaModality.Image, ModelCapability.Vision)]
    [InlineData(AiMediaModality.Audio, ModelCapability.Audio)]
    [InlineData(AiMediaModality.Video, ModelCapability.Video)]
    public void ToModelCapabilityShouldMapModality(
        AiMediaModality modality,
        ModelCapability expectedCapability)
    {
        var capability = AiMediaTypeClassifier.ToModelCapability(modality);

        capability.Should().Be(expectedCapability);
    }
}
