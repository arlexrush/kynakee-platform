using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kynakee.Modules.Identity.Application.Contracts;

namespace Kynakee.Modules.Identity.Infrastructure.MailDelivery;

public sealed class InvitationEmailPayloadProtector
{
    private readonly Dictionary<string, byte[]> _keys = new(StringComparer.Ordinal);
    private readonly string _activeId;

    public InvitationEmailPayloadProtector(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("Identity:InvitationEmail");
        _activeId = section["ActiveKeyId"] ?? throw new InvalidOperationException(IdentityReplayMessages.EmailKeyConfigurationInvalid);
        foreach (var entry in section.GetSection("Keys").GetChildren())
        {
            if (entry.Key.Length is < 1 or > 32 || entry.Key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            {
                throw new InvalidOperationException(IdentityReplayMessages.EmailKeyConfigurationInvalid);
            }
            var key = Convert.FromBase64String(entry.Value ?? string.Empty);
            if (key.Length != 32) throw new InvalidOperationException(IdentityReplayMessages.EmailKeyConfigurationInvalid);
            _keys.Add(entry.Key, key);
        }
        if (!_keys.ContainsKey(_activeId)) throw new InvalidOperationException(IdentityReplayMessages.EmailKeyConfigurationInvalid);
    }

    /// <summary>Cifra contenido ligándolo al tenant y a la invitación.</summary>
    public byte[] Protect(Guid tenantId, Guid invitationId, InvitationEmailPayload payload)
    {
        var header = Encoding.ASCII.GetBytes($"KE1:{_activeId}:");
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload);
        var result = new byte[header.Length + 28 + plaintext.Length];
        header.CopyTo(result, 0);
        RandomNumberGenerator.Fill(result.AsSpan(header.Length, 12));
        try
        {
            using var aes = new AesGcm(_keys[_activeId], 16);
            aes.Encrypt(result.AsSpan(header.Length, 12), plaintext, result.AsSpan(header.Length + 28),
                result.AsSpan(header.Length + 12, 16), AssociatedData(tenantId, invitationId, header));
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    /// <summary>Descifra y autentica el scope y la versión de clave del trabajo.</summary>
    public InvitationEmailPayload Unprotect(Guid tenantId, Guid invitationId, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length < 4) throw new CryptographicException(IdentityReplayMessages.EmailPayloadUnavailable);
        var separator = Array.IndexOf(payload, (byte)':', 4);
        if (!payload.AsSpan().StartsWith("KE1:"u8) || separator is < 5 or > 36
            || !_keys.TryGetValue(Encoding.ASCII.GetString(payload, 4, separator - 4), out var key)
            || payload.Length < separator + 29) throw new CryptographicException(IdentityReplayMessages.EmailPayloadUnavailable);
        var offset = separator + 1;
        var plaintext = new byte[payload.Length - offset - 28];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(payload.AsSpan(offset, 12), payload.AsSpan(offset + 28), payload.AsSpan(offset + 12, 16),
                plaintext, AssociatedData(tenantId, invitationId, payload.AsSpan(0, offset)));
            return JsonSerializer.Deserialize<InvitationEmailPayload>(plaintext) ?? throw new JsonException();
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private static byte[] AssociatedData(Guid tenantId, Guid invitationId, ReadOnlySpan<byte> header)
    {
        var data = new byte[32 + header.Length];
        tenantId.TryWriteBytes(data);
        invitationId.TryWriteBytes(data.AsSpan(16));
        header.CopyTo(data.AsSpan(32));
        return data;
    }
}

public sealed record InvitationEmailPayload(string RecipientEmail, string TenantName, string InvitationToken);
