namespace Kynakee.Modules.SharedKernel.Contracts;

/// <summary>Provides the owned stream and authoritative metadata for a trusted media reference.</summary>
public sealed class TrustedMediaContent : IAsyncDisposable
{
    private readonly Stream _content;

    public TrustedMediaContent(
        Stream content,
        string mimeType,
        long length)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        _content = content;
        MimeType = mimeType;
        Length = length;
    }

    public Stream Content => _content;

    public string MimeType { get; }

    public long Length { get; }

    public ValueTask DisposeAsync() => _content.DisposeAsync();
}
