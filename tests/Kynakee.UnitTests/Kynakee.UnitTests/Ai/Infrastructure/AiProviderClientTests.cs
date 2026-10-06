using System.Net;
using System.Text;
using FluentAssertions;
using Kynakee.Modules.Ai.Domain.Enums;
using Kynakee.Modules.Ai.Infrastructure.AI;
using Xunit;

namespace Kynakee.UnitTests.Ai.Infrastructure;

public class AiProviderClientTests
{
    [Fact]
    public async Task UploadGeminiFileShouldReturnValidatedFileUri()
    {
        var uploadUri = new Uri("https://generativelanguage.googleapis.com/upload/session/1");
        var fileUri = new Uri("https://generativelanguage.googleapis.com/v1beta/files/file-1");
        using var handler = new QueueHttpMessageHandler(
            _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Headers.TryAddWithoutValidation("X-Goog-Upload-URL", uploadUri.AbsoluteUri);
                return response;
            },
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"file\":{\"uri\":\"" + fileUri.AbsoluteUri + "\"}}",
                    Encoding.UTF8,
                    "application/json")
            });
        using var httpClient = new HttpClient(handler);
        var client = new AiProviderClient(httpClient);

        var result = await client.UploadGeminiFileAsync(
            CreateGeminiModel(),
            new AiPreparedMedia([1, 2, 3], "image/jpeg", "photo.jpg", null, false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(fileUri);
        handler.Requests.Should().HaveCount(2);
        handler.Requests[1].Content.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public async Task UploadGeminiFileShouldRejectUntrustedUploadUri()
    {
        using var handler = new QueueHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.TryAddWithoutValidation(
                "X-Goog-Upload-URL",
                "https://attacker.example/upload/steal");
            return response;
        });
        using var httpClient = new HttpClient(handler);
        var client = new AiProviderClient(httpClient);

        var result = await client.UploadGeminiFileAsync(
            CreateGeminiModel(),
            new AiPreparedMedia([1], "application/pdf", "document.pdf", null, false),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task CompleteChatShouldRejectUnsupportedOpenAiMediaBeforeSendingRequest()
    {
        using var handler = new QueueHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = new AiProviderClient(httpClient);
        var model = CreateGeminiModel() with
        {
            Protocol = AiProviderProtocol.OpenAiCompatible,
            BaseUrl = new Uri("https://api.example/v1/"),
            Capabilities = new HashSet<ModelCapability> { ModelCapability.Chat, ModelCapability.Pdf }
        };
        var request = new AiChatRequest(
            [new AiChatMessage("user", "Summarize this document")],
            RequireJson: false,
            Media: [new AiChatMedia(null, "application/pdf", InlineData: "data:application/pdf;base64,AQ==")]);

        var result = await client.CompleteChatAsync(model, request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteChatShouldRejectUntrustedGeminiFileUriBeforeSendingRequest()
    {
        using var handler = new QueueHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var client = new AiProviderClient(httpClient);
        var request = new AiChatRequest(
            [new AiChatMessage("user", "Describe this image")],
            RequireJson: false,
            Media: [new AiChatMedia(
                null,
                "image/jpeg",
                ProviderFileUri: "https://attacker.example/v1beta/files/file-1")]);

        var result = await client.CompleteChatAsync(CreateGeminiModel(), request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        handler.Requests.Should().BeEmpty();
    }

    private static AiResolvedModel CreateGeminiModel() =>
        new(
            "gemini-flash",
            "Gemini",
            AiProviderProtocol.Gemini,
            new Uri("https://generativelanguage.googleapis.com/v1beta/"),
            "test-key",
            "gemini-2.0-flash",
            "low",
            new HashSet<ModelCapability> { ModelCapability.Chat, ModelCapability.Vision, ModelCapability.Pdf },
            0,
            30,
            "models/{model}:generateContent",
            "models/{model}:embedContent");

    private sealed class QueueHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new(responses);
        public List<(string? ContentType, byte[] Content)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add((request.Content?.Headers.ContentType?.MediaType, body));
            return _responses.Dequeue()(request);
        }

        public QueueHttpMessageHandler()
            : this([])
        {
        }
    }
}
