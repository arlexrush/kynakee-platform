using Kynakee.Modules.Identity.Domain.Entities;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kynakee.Modules.Identity.Infrastructure.MailDelivery;

/// <summary>Encrypts account email payloads with domain-separated authenticated context.</summary>
public sealed class AccountEmailPayloadProtector
{
    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);
    private readonly string _activeId;

    public AccountEmailPayloadProtector(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("Identity:InvitationEmail");
        _activeId = section["ActiveKeyId"] ?? throw new InvalidOperationException(AccountEmailMessages.Get("ConfigurationInvalid"));
        foreach (var entry in section.GetSection("Keys").GetChildren())
        {
            if (entry.Key.Length is < 1 or > 32 || entry.Key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
                throw new InvalidOperationException(AccountEmailMessages.Get("ConfigurationInvalid"));
            var key = Convert.FromBase64String(entry.Value ?? string.Empty);
            if (key.Length != 32) throw new InvalidOperationException(AccountEmailMessages.Get("ConfigurationInvalid"));
            _keys.Add(entry.Key, key);
        }
        if (!_keys.ContainsKey(_activeId)) throw new InvalidOperationException(AccountEmailMessages.Get("ConfigurationInvalid"));
    }

    /// <summary>Uses a distinct header and associated data from invitation ciphertext.</summary>
    public byte[] Protect(Guid tenantId, Guid tokenId, AccountEmailPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var header = Encoding.ASCII.GetBytes($"KA1:{_activeId}:");
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload);
        var result = new byte[header.Length + 28 + plaintext.Length];
        header.CopyTo(result, 0);
        RandomNumberGenerator.Fill(result.AsSpan(header.Length, 12));
        try
        {
            using var aes = new AesGcm(_keys[_activeId], 16);
            aes.Encrypt(result.AsSpan(header.Length, 12), plaintext, result.AsSpan(header.Length + 28),
                result.AsSpan(header.Length + 12, 16), AssociatedData(tenantId, tokenId, header));
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    /// <summary>Authenticates owner, record and key version before deserializing.</summary>
    public AccountEmailPayload Unprotect(Guid tenantId, Guid tokenId, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var separator = payload.Length < 4 ? -1 : Array.IndexOf(payload, (byte)':', 4);
        if (!payload.AsSpan().StartsWith("KA1:"u8) || separator is < 5 or > 36 || payload.Length < separator + 29 ||
            !_keys.TryGetValue(Encoding.ASCII.GetString(payload, 4, separator - 4), out var key))
            throw new CryptographicException(AccountEmailMessages.Get("PayloadInvalid"));
        var offset = separator + 1;
        var plaintext = new byte[payload.Length - offset - 28];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(payload.AsSpan(offset, 12), payload.AsSpan(offset + 28), payload.AsSpan(offset + 12, 16),
                plaintext, AssociatedData(tenantId, tokenId, payload.AsSpan(0, offset)));
            return JsonSerializer.Deserialize<AccountEmailPayload>(plaintext) ?? throw new JsonException();
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private static byte[] AssociatedData(Guid tenantId, Guid tokenId, ReadOnlySpan<byte> header)
    {
        var data = new byte[32 + header.Length];
        tenantId.TryWriteBytes(data);
        tokenId.TryWriteBytes(data.AsSpan(16));
        header.CopyTo(data.AsSpan(32));
        return data;
    }
}

/// <summary>Plaintext exists only during encryption and authorized delivery.</summary>
public sealed record AccountEmailPayload(string RecipientEmail, string Token, AccountActionPurpose Purpose);
