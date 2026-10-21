using Kynakee.Modules.Projects.Infrastructure.Storage;
using Xunit;

namespace Kynakee.UnitTests.Projects.Infrastructure;

public sealed class S3UploadPolicyTests
{
    [Fact]
    public void UploadRequestKnownLengthShouldDisableChunkEncoding()
    {
        using var stream = new MemoryStream([1, 2, 3]);

        var request = S3UploadPolicy.CreateRequest("synthetic-bucket", "capture/file.txt", stream, "text/plain", 3);

        Assert.False(request.UseChunkEncoding);
    }

    [Fact]
    public void UploadRequestKnownLengthShouldRetainPayloadSigning()
    {
        using var stream = new MemoryStream([1, 2, 3]);

        var request = S3UploadPolicy.CreateRequest("synthetic-bucket", "capture/file.txt", stream, "text/plain", 3);

        Assert.Equal(false, request.DisablePayloadSigning);
    }

    [Fact]
    public void UploadRequestKnownLengthShouldSetContentLength()
    {
        using var stream = new MemoryStream([1, 2, 3]);

        var request = S3UploadPolicy.CreateRequest("synthetic-bucket", "capture/file.txt", stream, "text/plain", 3);

        Assert.Equal(3, request.Headers.ContentLength);
    }

    [Fact]
    public void UploadRequestCallerOwnedStreamShouldRemainCallerOwned()
    {
        using var stream = new MemoryStream([1, 2, 3]);

        var request = S3UploadPolicy.CreateRequest("synthetic-bucket", "capture/file.txt", stream, "text/plain", 3);

        Assert.False(request.AutoCloseStream);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(4, 3)]
    public void UploadRequestInvalidLengthShouldReject(int length, int maximum)
    {
        using var stream = new MemoryStream(new byte[length]);

        Assert.Throws<ArgumentException>(() => S3UploadPolicy.CreateRequest("synthetic-bucket", "file.txt", stream, "text/plain", maximum));
    }

    [Fact]
    public void UploadRequestAdvancedPositionShouldReject()
    {
        using var stream = new MemoryStream([1, 2, 3]);
        stream.Position = 1;

        Assert.Throws<ArgumentException>(() => S3UploadPolicy.CreateRequest("synthetic-bucket", "file.txt", stream, "text/plain", 3));
    }

    [Fact]
    public void UploadRequestNonSeekableStreamShouldReject()
    {
        using var stream = new NonSeekableStream();

        Assert.Throws<ArgumentException>(() => S3UploadPolicy.CreateRequest("synthetic-bucket", "file.txt", stream, "text/plain", 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(104857601)]
    public void UploadRequestInvalidMaximumShouldReject(long maximum)
    {
        using var stream = new MemoryStream([1]);

        Assert.Throws<ArgumentOutOfRangeException>(() => S3UploadPolicy.CreateRequest("synthetic-bucket", "file.txt", stream, "text/plain", maximum));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid mime")]
    public void UploadRequestInvalidMimeShouldReject(string mime)
    {
        using var stream = new MemoryStream([1]);

        Assert.Throws<ArgumentException>(() => S3UploadPolicy.CreateRequest("synthetic-bucket", "file.txt", stream, mime, 3));
    }

    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
