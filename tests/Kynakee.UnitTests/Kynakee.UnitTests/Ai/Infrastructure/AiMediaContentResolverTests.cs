using System.Text;
using FluentAssertions;
using Kynakee.Modules.AI.Contracts;
using Kynakee.Modules.Ai.Infrastructure.AI;
using Kynakee.Modules.SharedKernel.Contracts;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kynakee.UnitTests.Ai.Infrastructure;

public class AiMediaContentResolverTests
{
    [Fact]
    public async Task ResolveShouldReturnPreparedMediaFromTrustedSource()
    {
        var source = Substitute.For<ITrustedMediaContentSource>();
        source.OpenReadAsync(Arg.Any<TrustedMediaReference>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<TrustedMediaContent?>(new TrustedMediaContent(
                new MemoryStream(Encoding.UTF8.GetBytes("image-bytes")),
                "image/jpeg",
                11)));
        var resolver = new AiMediaContentResolver(source, CreateOptions(1024));
        var media = new MediaFileRef(
            new Uri("minio://captures/project/photo.jpg"),
            "image/jpeg",
            "kitchen",
            false,
            false,
            "photo.jpg");

        var result = await resolver.ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), [media], CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Encoding.UTF8.GetString(result.Value![0].Content).Should().Be("image-bytes");
    }

    [Fact]
    public async Task ResolveShouldRejectContentBeyondConfiguredLimit()
    {
        var source = Substitute.For<ITrustedMediaContentSource>();
        source.OpenReadAsync(Arg.Any<TrustedMediaReference>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<TrustedMediaContent?>(new TrustedMediaContent(
                new MemoryStream([1, 2, 3]),
                "application/pdf",
                3)));
        var resolver = new AiMediaContentResolver(source, CreateOptions(2));
        var media = new MediaFileRef(
            new Uri("minio://captures/project/document.pdf"),
            "application/pdf",
            null,
            false,
            false,
            "document.pdf");

        var result = await resolver.ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), [media], CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    private static IOptions<AiOptions> CreateOptions(long maxMediaBytes)
    {
        var options = new AiOptions { MaxMediaBytes = maxMediaBytes };
        return Options.Create(options);
    }
}