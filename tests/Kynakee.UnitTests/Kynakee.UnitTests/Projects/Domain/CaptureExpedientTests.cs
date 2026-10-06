using FluentAssertions;
using Kynakee.Modules.Projects.Domain.Entities.DataCapture;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Projects.Domain;

public class CaptureExpedientTests
{
    [Fact]
    public void CreateShouldAcceptMinioBucketAndObjectKey()
    {
        var result = CaptureExpedient.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            [new CaptureMediaFile(
                new Uri("minio://kynakee-media/projects/one/photo.jpg"),
                "image/jpeg",
                "Kitchen",
                false,
                false)]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MediaFiles.Should().ContainSingle();
    }

    [Theory]
    [InlineData("https://storage.example.com/photo.jpg")]
    [InlineData("minio:///photo.jpg")]
    [InlineData("minio://kynakee-media/")]
    [InlineData("minio://kynakee-media/../photo.jpg")]
    [InlineData("minio://kynakee-media/photo.jpg?token=secret")]
    public void CreateShouldRejectInvalidStorageUri(string input)
    {
        var uri = new Uri(input, UriKind.RelativeOrAbsolute);
        var result = CaptureExpedient.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            [new CaptureMediaFile(
                uri,
                "image/jpeg",
                null,
                false,
                false)]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("PROJ_CAPTURE_MEDIA_REFERENCE_INVALID");
    }

    [Fact]
    public void CreateShouldRejectMediaWithoutDeclaredType()
    {
        var result = CaptureExpedient.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            [new CaptureMediaFile(
                new Uri("minio://kynakee-media/photo.jpg"),
                " ",
                null,
                false,
                false)]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("PROJ_CAPTURE_MEDIA_REFERENCE_INVALID");
    }

    [Fact]
    public void CreateShouldRejectMediaTypeLongerThanPersistenceLimit()
    {
        var result = CaptureExpedient.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            [new CaptureMediaFile(
                new Uri("minio://kynakee-media/photo.jpg"),
                new string('a', 101),
                null,
                false,
                false)]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("PROJ_CAPTURE_MEDIA_REFERENCE_INVALID");
    }
}
