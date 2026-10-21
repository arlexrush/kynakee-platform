using FluentAssertions;
using Kynakee.Modules.Projects.Domain.Entities.DataCapture;
using Kynakee.Modules.Projects.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Projects.Domain;

public class CaptureExpedientTests
{
    [Fact]
    public void CreateShouldAcceptS3BucketAndObjectKey()
    {
        var result = CaptureExpedient.Create(
            Guid.NewGuid(),
            new ProjectId(Guid.NewGuid()),
            [new CaptureMediaFile(
                new Uri("s3://kynakee-media/projects/one/photo.jpg"),
                "image/jpeg",
                "Kitchen",
                false,
                false)]);

        result.IsSuccess.Should().BeTrue();
        result.Value!.MediaFiles.Should().ContainSingle();
    }

    [Theory]
    [InlineData("https://storage.example.com/photo.jpg")]
    [InlineData("s3:///photo.jpg")]
    [InlineData("s3://kynakee-media/")]
    [InlineData("s3://kynakee-media/../photo.jpg")]
    [InlineData("s3://kynakee-media/photo.jpg?token=secret")]
    [InlineData("s3://user:password@kynakee-media/photo.jpg")]
    [InlineData("s3://kynakee-media:8333/photo.jpg")]
    [InlineData("s3://kynakee-media/photo.jpg#fragment")]
    [InlineData("s3://kynakee-media/%2e%2e/photo.jpg")]
    [InlineData("s3://kynakee-media/folder%2fphoto.jpg")]
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
                new Uri("s3://kynakee-media/photo.jpg"),
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
                new Uri("s3://kynakee-media/photo.jpg"),
                new string('a', 101),
                null,
                false,
                false)]);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("PROJ_CAPTURE_MEDIA_REFERENCE_INVALID");
    }
}
