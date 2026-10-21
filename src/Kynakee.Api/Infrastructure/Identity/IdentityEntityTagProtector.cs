using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Kynakee.Api.Infrastructure.Identity;

internal enum IdentityEntityTagResource : byte
{
    Tenant = 1,
    User = 2,
    Membership = 3
}

internal sealed class IdentityEntityTagProtector
{
    private const string DomainSeparation = "Kynakee.Identity.EntityTag.v2";
    private readonly byte[] _key;

    public IdentityEntityTagProtector(string signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);
        _key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(DomainSeparation));
    }

    public string Create(Guid tenantId, IdentityEntityTagResource resourceType, Guid resourceId, uint version)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(Enum.IsDefined(resourceType), false, nameof(resourceType));
        ArgumentOutOfRangeException.ThrowIfEqual(resourceId, Guid.Empty);
        Span<byte> payload = stackalloc byte[20];
        resourceId.TryWriteBytes(payload);
        BinaryPrimitives.WriteUInt32BigEndian(payload[16..], version);
        Span<byte> signature = stackalloc byte[32];
        Sign(tenantId, resourceType, payload, signature);
        Span<byte> entityTag = stackalloc byte[36];
        payload.CopyTo(entityTag);
        signature[..16].CopyTo(entityTag[20..]);
        return $"\"{ToBase64Url(entityTag)}\"";
    }

    public bool TryRead(string? entityTag, Guid tenantId, IdentityEntityTagResource resourceType, Guid resourceId, out uint version)
    {
        version = default;
        if (tenantId == Guid.Empty || !Enum.IsDefined(resourceType) || resourceId == Guid.Empty || entityTag is null || entityTag.Length != 50 ||
            entityTag[0] != '"' || entityTag[^1] != '"')
        {
            return false;
        }

        Span<byte> token = stackalloc byte[36];
        if (!TryFromBase64Url(entityTag.AsSpan(1, 48), token))
        {
            return false;
        }

        var payload = token[..20];
        if (new Guid(payload[..16]) != resourceId)
        {
            return false;
        }

        Span<byte> expectedSignature = stackalloc byte[32];
        Sign(tenantId, resourceType, payload, expectedSignature);
        if (!CryptographicOperations.FixedTimeEquals(token[20..], expectedSignature[..16]))
        {
            return false;
        }

        version = BinaryPrimitives.ReadUInt32BigEndian(payload[16..]);
        return true;
    }

    private void Sign(Guid tenantId, IdentityEntityTagResource resourceType, ReadOnlySpan<byte> payload, Span<byte> signature)
    {
        Span<byte> scopedPayload = stackalloc byte[37];
        tenantId.TryWriteBytes(scopedPayload);
        scopedPayload[16] = (byte)resourceType;
        payload.CopyTo(scopedPayload[17..]);
        HMACSHA256.HashData(_key, scopedPayload, signature);
    }

    private static string ToBase64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryFromBase64Url(ReadOnlySpan<char> value, Span<byte> destination)
    {
        Span<char> paddedValue = stackalloc char[48];
        for (var index = 0; index < value.Length; index++)
        {
            paddedValue[index] = value[index] switch
            {
                '-' => '+',
                '_' => '/',
                var character when char.IsAsciiLetterOrDigit(character) => character,
                _ => '\0'
            };
            if (paddedValue[index] == '\0')
            {
                return false;
            }
        }

        return Convert.TryFromBase64Chars(paddedValue, destination, out var bytesWritten) && bytesWritten == destination.Length;
    }
}
