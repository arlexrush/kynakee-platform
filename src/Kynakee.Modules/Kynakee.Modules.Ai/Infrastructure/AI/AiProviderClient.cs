using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using Kynakee.Modules.Ai.Domain.Enums;
using Kynakee.Modules.Ai.Domain.Resources;
using Kynakee.Modules.Ai.Domain.Services;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Ai.Infrastructure.AI;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "The provider client is instantiated through the typed HttpClient registration.")]
internal sealed class AiProviderClient : IAiProviderClient
{
    private readonly HttpClient _httpClient;

    public AiProviderClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task<Result<AiChatCompletion>> CompleteChatAsync(
        AiResolvedModel model,
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);

        if (!model.Capabilities.Contains(ModelCapability.Chat))
        {
            return Failure<AiChatCompletion>("AI_040", "ChatCapabilityRequired");
        }

        if (request.Messages.Count == 0 ||
            request.Messages.Any(message =>
                string.IsNullOrWhiteSpace(message.Content) ||
                message.Role is not "system" and not "user" and not "assistant") ||
            request.MaxOutputTokens is <= 0 ||
            request.Media?.Any(media =>
                GetMediaCapability(media.MimeType) is null) == true)
        {
            return Failure<AiChatCompletion>("AI_041", "ChatRequestInvalid");
        }

        var mediaCapabilities = request.Media?
            .Select(media => GetMediaCapability(media.MimeType))
            .ToArray() ?? [];
        if (mediaCapabilities.Any(capability =>
                capability is null || !model.Capabilities.Contains(capability.Value)))
        {
            return Failure<AiChatCompletion>("AI_049", "VisionCapabilityRequired");
        }

        if (request.Media?.Any(media =>
                !IsSupportedByCurrentTransport(media, model.Protocol)) == true)
        {
            return Failure<AiChatCompletion>("AI_050", "MediaTransportUnsupported");
        }

        if (!TryCreateUri(model, model.ChatPath, out var uri))
        {
            return Failure<AiChatCompletion>("AI_042", "ProviderConfigurationMissing");
        }

        using var httpRequest = CreateRequest(
            model,
            HttpMethod.Post,
            uri,
            model.Protocol == AiProviderProtocol.Gemini
                ? CreateGeminiChatPayload(request)
                : CreateOpenAiChatPayload(model, request));

        return await SendJsonAsync<AiChatCompletion>(
            model,
            httpRequest,
            model.Protocol == AiProviderProtocol.Gemini
                ? response => ParseGeminiChat(response)
                : response => ParseOpenAiChat(response),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<Uri>> UploadGeminiFileAsync(
        AiResolvedModel model,
        AiPreparedMedia media,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(media);
        if (model.Protocol != AiProviderProtocol.Gemini || media.Content.Length == 0 ||
            !MediaTypeHeaderValue.TryParse(media.MimeType, out _))
        {
            return Failure<Uri>("AI_050", "MediaTransportUnsupported");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(model.TimeoutSeconds));
        try
        {
            var endpoint = new Uri(
                model.BaseUrl,
                "/upload/v1beta/files");
            using var startRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new
                {
                    file = new { display_name = GetDisplayName(media) }
                })
            };
            startRequest.Headers.TryAddWithoutValidation("x-goog-api-key", model.ApiKey);
            startRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Protocol", "resumable");
            startRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", "start");
            startRequest.Headers.TryAddWithoutValidation(
                "X-Goog-Upload-Header-Content-Length",
                media.Content.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startRequest.Headers.TryAddWithoutValidation(
                "X-Goog-Upload-Header-Content-Type",
                media.MimeType);

            using var startResponse = await _httpClient.SendAsync(
                    startRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutSource.Token)
                .ConfigureAwait(false);
            if (!startResponse.IsSuccessStatusCode ||
                !startResponse.Headers.TryGetValues("X-Goog-Upload-URL", out var uploadUrlValues) ||
                !Uri.TryCreate(uploadUrlValues.SingleOrDefault(), UriKind.Absolute, out var uploadUri) ||
                !IsTrustedGeminiUploadUri(uploadUri))
            {
                return Failure<Uri>("AI_045", "ProviderRequestFailed");
            }

            using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUri)
            {
                Content = new ByteArrayContent(media.Content)
            };
            uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(media.MimeType);
            uploadRequest.Headers.TryAddWithoutValidation("x-goog-api-key", model.ApiKey);
            uploadRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Offset", "0");
            uploadRequest.Headers.TryAddWithoutValidation("X-Goog-Upload-Command", "upload, finalize");

            using var uploadResponse = await _httpClient.SendAsync(
                    uploadRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutSource.Token)
                .ConfigureAwait(false);
            if (!uploadResponse.IsSuccessStatusCode)
            {
                return Failure<Uri>("AI_045", "ProviderRequestFailed");
            }

            using var responseStream = await uploadResponse.Content
                .ReadAsStreamAsync(timeoutSource.Token)
                .ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(
                    responseStream,
                    cancellationToken: timeoutSource.Token)
                .ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("file", out var file) ||
                !file.TryGetProperty("uri", out var uriElement) ||
                !Uri.TryCreate(uriElement.GetString(), UriKind.Absolute, out var fileUri) ||
                !IsTrustedGeminiFileUri(fileUri))
            {
                return Failure<Uri>("AI_046", "ProviderResponseInvalid");
            }

            return ResultFactory.Success(fileUri);
        }
        catch (HttpRequestException)
        {
            return Failure<Uri>("AI_045", "ProviderRequestFailed");
        }
        catch (JsonException)
        {
            return Failure<Uri>("AI_046", "ProviderResponseInvalid");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure<Uri>("AI_047", "ProviderTimeout");
        }
    }

    public async Task<Result<AiEmbedding>> GenerateEmbeddingAsync(
        AiResolvedModel model,
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(text);

        if (!model.Capabilities.Contains(ModelCapability.Embedding))
        {
            return Failure<AiEmbedding>("AI_043", "EmbeddingCapabilityRequired");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Failure<AiEmbedding>("AI_044", "EmbeddingTextRequired");
        }

        if (!TryCreateUri(model, model.EmbeddingsPath, out var uri))
        {
            return Failure<AiEmbedding>("AI_042", "ProviderConfigurationMissing");
        }

        using var httpRequest = CreateRequest(
            model,
            HttpMethod.Post,
            uri,
            model.Protocol == AiProviderProtocol.Gemini
                ? CreateGeminiEmbeddingPayload(text)
                : CreateOpenAiEmbeddingPayload(model, text));

        return await SendJsonAsync(
            model,
            httpRequest,
            response => model.Protocol == AiProviderProtocol.Gemini
                ? ParseGeminiEmbedding(response, model.Dimensions)
                : ParseOpenAiEmbedding(response, model.Dimensions),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<T>> SendJsonAsync<T>(
        AiResolvedModel model,
        HttpRequestMessage request,
        Func<JsonElement, Result<T>> parseResponse,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parseResponse);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(model.TimeoutSeconds));

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Failure<T>("AI_045", "ProviderRequestFailed");
            }

            var responseStream = await response.Content
                .ReadAsStreamAsync(timeoutSource.Token)
                .ConfigureAwait(false);
            await using var stream = responseStream.ConfigureAwait(false);
            var jsonStream = responseStream;
            using var document = await JsonDocument.ParseAsync(
                jsonStream,
                cancellationToken: timeoutSource.Token).ConfigureAwait(false);

            return parseResponse(document.RootElement);
        }
        catch (HttpRequestException)
        {
            return Failure<T>("AI_045", "ProviderRequestFailed");
        }
        catch (JsonException)
        {
            return Failure<T>("AI_046", "ProviderResponseInvalid");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure<T>("AI_047", "ProviderTimeout");
        }
    }

    private static HttpRequestMessage CreateRequest(
        AiResolvedModel model,
        HttpMethod method,
        Uri uri,
        Dictionary<string, object?> payload)
    {
        var request = new HttpRequestMessage(method, uri)
        {
            Content = JsonContent.Create(payload)
        };

        if (model.Protocol == AiProviderProtocol.Gemini)
        {
            request.Headers.TryAddWithoutValidation("x-goog-api-key", model.ApiKey);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                model.ApiKey);
        }

        return request;
    }

    private static Dictionary<string, object?> CreateOpenAiChatPayload(
        AiResolvedModel model,
        AiChatRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model.ModelId,
            ["messages"] = request.Messages.Select(message => new
            {
                role = message.Role,
                content = request.Media is { Count: > 0 } && message.Role == "user"
                    ? CreateOpenAiMultimodalContent(message.Content, request.Media)
                    : (object)message.Content
            }).ToArray()
        };

        if (request.RequireJson)
        {
            payload["response_format"] = new { type = "json_object" };
        }

        if (request.MaxOutputTokens is { } maxOutputTokens)
        {
            payload["max_tokens"] = maxOutputTokens;
        }

        return payload;
    }

    private static Dictionary<string, object?> CreateGeminiChatPayload(
        AiChatRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["contents"] = request.Messages
                .Where(message => message.Role != "system")
                .Select(message => new
                {
                    role = message.Role == "assistant" ? "model" : "user",
                    parts = CreateGeminiParts(message, request.Media)
                })
                .ToArray()
        };

        var systemInstructions = request.Messages
            .Where(message => message.Role == "system")
            .Select(message => message.Content)
            .ToArray();
        if (systemInstructions.Length > 0)
        {
            payload["systemInstruction"] = new
            {
                parts = new[] { new { text = string.Join("\n", systemInstructions) } }
            };
        }

        var generationConfig = new Dictionary<string, object?>();
        if (request.RequireJson)
        {
            generationConfig["responseMimeType"] = "application/json";
        }

        if (request.MaxOutputTokens is { } maxOutputTokens)
        {
            generationConfig["maxOutputTokens"] = maxOutputTokens;
        }

        if (generationConfig.Count > 0)
        {
            payload["generationConfig"] = generationConfig;
        }

        return payload;
    }

    private static object[] CreateOpenAiMultimodalContent(
        string text,
        IReadOnlyList<AiChatMedia> media) =>
        [
            new { type = "text", text },
            .. media.Select(item => (object)new
            {
                type = "image_url",
                image_url = new { url = item.InlineData }
            })
        ];

    private static object[] CreateGeminiParts(
        AiChatMessage message,
        IReadOnlyList<AiChatMedia>? media)
    {
        if (message.Role != "user" || media is not { Count: > 0 })
        {
            return [new { text = message.Content }];
        }

        return
        [
            new { text = message.Content },
            .. media.Select(item => item.InlineData is not null
                ? (object)new
                {
                    inlineData = new
                    {
                        mimeType = item.MimeType,
                        data = item.InlineData
                    }
                }
                : new
            {
                fileData = new
                {
                    mimeType = item.MimeType,
                    fileUri = item.ProviderFileUri
                }
            })
        ];
    }

    private static ModelCapability? GetMediaCapability(string mimeType) =>
        AiMediaTypeClassifier.Classify(mimeType) is { } modality
            ? AiMediaTypeClassifier.ToModelCapability(modality)
            : null;

    private static bool IsSupportedByCurrentTransport(
        AiChatMedia media,
        AiProviderProtocol protocol)
    {
        var capability = GetMediaCapability(media.MimeType);
        return protocol switch
        {
            AiProviderProtocol.OpenAiCompatible =>
                capability == ModelCapability.Vision &&
                !string.IsNullOrWhiteSpace(media.InlineData),
            AiProviderProtocol.Gemini =>
                capability.HasValue &&
                !string.IsNullOrWhiteSpace(media.ProviderFileUri) &&
                Uri.TryCreate(media.ProviderFileUri, UriKind.Absolute, out var fileUri) &&
                string.Equals(fileUri.Host, "generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase) &&
                fileUri.AbsolutePath.StartsWith("/v1beta/files/", StringComparison.Ordinal) &&
                string.Equals(fileUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static string GetDisplayName(AiPreparedMedia media) =>
        string.IsNullOrWhiteSpace(media.FileName)
            ? $"media-{Guid.NewGuid():N}"
            : Path.GetFileName(media.FileName);

    private static bool IsTrustedGeminiUploadUri(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(uri.Host, "generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.StartsWith("/upload/", StringComparison.Ordinal) &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Fragment);

    private static bool IsTrustedGeminiFileUri(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(uri.Host, "generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase) &&
        uri.AbsolutePath.StartsWith("/v1beta/files/", StringComparison.Ordinal) &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment);

    private static Dictionary<string, object?> CreateOpenAiEmbeddingPayload(
        AiResolvedModel model,
        string text)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model.ModelId,
            ["input"] = text
        };

        if (model.Dimensions > 0)
        {
            payload["dimensions"] = model.Dimensions;
        }

        return payload;
    }

    private static Dictionary<string, object?> CreateGeminiEmbeddingPayload(
        string text) =>
        new()
        {
            ["content"] = new
            {
                parts = new[] { new { text } }
            }
        };

    private static Result<AiChatCompletion> ParseOpenAiChat(JsonElement response)
    {
        if (!response.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0 ||
            !choices[0].TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.String)
        {
            return Failure<AiChatCompletion>("AI_046", "ProviderResponseInvalid");
        }

        return ResultFactory.Success(
            new AiChatCompletion(
                content.GetString() ?? string.Empty,
                ReadTokenCount(response, "usage", "prompt_tokens"),
                ReadTokenCount(response, "usage", "completion_tokens")));
    }

    private static Result<AiChatCompletion> ParseGeminiChat(JsonElement response)
    {
        if (!response.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0 ||
            !candidates[0].TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return Failure<AiChatCompletion>("AI_046", "ProviderResponseInvalid");
        }

        var text = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textPart) &&
                textPart.ValueKind == JsonValueKind.String)
            {
                text.Append(textPart.GetString());
            }
        }

        return text.Length == 0
            ? Failure<AiChatCompletion>("AI_046", "ProviderResponseInvalid")
            : ResultFactory.Success(
                new AiChatCompletion(
                    text.ToString(),
                    ReadTokenCount(response, "usageMetadata", "promptTokenCount"),
                    ReadTokenCount(response, "usageMetadata", "candidatesTokenCount")));
    }

    private static Result<AiEmbedding> ParseOpenAiEmbedding(
        JsonElement response,
        int expectedDimensions)
    {
        if (!response.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array ||
            data.GetArrayLength() == 0 ||
            !data[0].TryGetProperty("embedding", out var embedding) ||
            !TryReadVector(embedding, expectedDimensions, out var vector))
        {
            return Failure<AiEmbedding>("AI_048", "EmbeddingDimensionsInvalid");
        }

        return ResultFactory.Success(
            new AiEmbedding(
                vector,
                ReadTokenCount(response, "usage", "prompt_tokens")));
    }

    private static Result<AiEmbedding> ParseGeminiEmbedding(
        JsonElement response,
        int expectedDimensions)
    {
        if (!response.TryGetProperty("embedding", out var embedding) ||
            !embedding.TryGetProperty("values", out var values) ||
            !TryReadVector(values, expectedDimensions, out var vector))
        {
            return Failure<AiEmbedding>("AI_048", "EmbeddingDimensionsInvalid");
        }

        return ResultFactory.Success(new AiEmbedding(vector, 0));
    }

    private static bool TryReadVector(
        JsonElement embedding,
        int expectedDimensions,
        out float[] vector)
    {
        vector = [];
        if (embedding.ValueKind != JsonValueKind.Array ||
            embedding.GetArrayLength() != expectedDimensions)
        {
            return false;
        }

        var values = new float[expectedDimensions];
        var index = 0;
        foreach (var item in embedding.EnumerateArray())
        {
            if (!item.TryGetSingle(out var value) || !float.IsFinite(value))
            {
                return false;
            }

            values[index++] = value;
        }

        vector = values;
        return true;
    }

    private static int ReadTokenCount(
        JsonElement response,
        string usageProperty,
        string tokenProperty) =>
        response.TryGetProperty(usageProperty, out var usage) &&
        usage.TryGetProperty(tokenProperty, out var tokenCount) &&
        tokenCount.TryGetInt32(out var result) &&
        result >= 0
            ? result
            : 0;

    private static bool TryCreateUri(
        AiResolvedModel model,
        string configuredPath,
        out Uri uri)
    {
        var path = configuredPath.Replace(
            "{model}",
            Uri.EscapeDataString(model.ModelId),
            StringComparison.Ordinal);

        if (Uri.TryCreate(model.BaseUrl, path, out var candidate) &&
            candidate is not null)
        {
            uri = candidate;
            return true;
        }

        uri = model.BaseUrl;
        return false;
    }

    private static Result<T> Failure<T>(string code, string messageKey) =>
        ResultFactory.Failure<T>(
            ApplicationError.AI(code, AiMessages.Get(messageKey)));
}
