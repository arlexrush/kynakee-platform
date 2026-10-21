using Kynakee.Modules.SharedKernel.Contracts;
using Kynakee.Modules.SharedKernel.Application;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Kynakee.Modules.Identity.Application.Contracts;

namespace Kynakee.Api.Infrastructure.Identity;

internal sealed class IdentityHttpIdempotencyProtector : IHttpIdempotencyProtector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _keyHashKey;
    private readonly byte[] _fingerprintKey;
    private readonly byte[] _outcomeEncryptionKey;
    private readonly Dictionary<string, byte[]> _outcomeKeys;
    private readonly string _activeKeyId;
    private readonly bool _allowLegacyOutcomes;
    private readonly byte[]? _legacyOutcomeKey;

    public IdentityHttpIdempotencyProtector(string signingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKey);
        var rootKey = Encoding.UTF8.GetBytes(signingKey);
        _keyHashKey = DeriveKey(rootKey, "Kynakee.Identity.Idempotency.Key.v1");
        _fingerprintKey = DeriveKey(rootKey, "Kynakee.Identity.Idempotency.Fingerprint.v1");
        _outcomeEncryptionKey = DeriveKey(rootKey, "Kynakee.Identity.Idempotency.Outcome.v1");
        _activeKeyId = "v1";
        _outcomeKeys = new(StringComparer.Ordinal) { [_activeKeyId] = _outcomeEncryptionKey };
        _allowLegacyOutcomes = true;
        _legacyOutcomeKey = _outcomeEncryptionKey;
    }

    public IdentityHttpIdempotencyProtector(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("Identity:Idempotency");
        var hashKey = ReadKey(section["HashKey"], allowLonger: true);
        _keyHashKey = DeriveKey(hashKey, "Kynakee.Identity.Idempotency.Key.v1");
        _fingerprintKey = DeriveKey(hashKey, "Kynakee.Identity.Idempotency.Fingerprint.v1");
        _activeKeyId = section["ActiveOutcomeKeyId"] ?? throw new InvalidOperationException(IdentityReplayMessages.KeyConfigurationInvalid);
        _outcomeKeys = new(StringComparer.Ordinal);
        foreach (var key in section.GetSection("OutcomeKeys").GetChildren())
        {
            if (key.Key.Length is < 1 or > 32 || key.Key.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            {
                throw new InvalidOperationException(IdentityReplayMessages.KeyConfigurationInvalid);
            }
            _outcomeKeys.Add(key.Key, ReadKey(key.Value));
        }
        if (!_outcomeKeys.TryGetValue(_activeKeyId, out var activeKey))
        {
            throw new InvalidOperationException(IdentityReplayMessages.KeyConfigurationInvalid);
        }
        _outcomeEncryptionKey = activeKey;
        _allowLegacyOutcomes = section.GetValue<bool>("AllowLegacyOutcomes");
        _legacyOutcomeKey = _allowLegacyOutcomes ? ReadKey(section["LegacyOutcomeKey"]) : null;
        CryptographicOperations.ZeroMemory(hashKey);
    }

    private static byte[] ReadKey(string? value, bool allowLonger = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(IdentityReplayMessages.KeyConfigurationInvalid);
        }
        var key = Convert.FromBase64String(value);
        if (key.Length < 32 || (!allowLonger && key.Length != 32))
        {
            throw new InvalidOperationException(IdentityReplayMessages.KeyConfigurationInvalid);
        }
        return key;
    }

    public IdempotencyRequest CreateRequest(
        object request,
        HttpIdempotencyContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var scope = $"{context.Operation}\n{context.TenantId:D}\n{context.CallerId:D}";
        var scopeBytes = Encoding.UTF8.GetBytes(scope);
        var keyBytes = Encoding.UTF8.GetBytes(context.Key);
        var keyHashInput = new byte[scopeBytes.Length + 1 + keyBytes.Length];
        scopeBytes.CopyTo(keyHashInput, 0);
        keyHashInput[scopeBytes.Length] = (byte)'\n';
        keyBytes.CopyTo(keyHashInput, scopeBytes.Length + 1);
        var keyHash = HMACSHA256.HashData(_keyHashKey, keyHashInput);
        var requestNode = JsonNode.Parse(JsonSerializer.SerializeToUtf8Bytes(request, request.GetType(), JsonOptions));
        requestNode?.AsObject().Remove("idempotencyContext");
        var canonicalRequest = JsonSerializer.SerializeToUtf8Bytes(requestNode, JsonOptions);
        var fingerprintInput = new byte[scopeBytes.Length + 1 + canonicalRequest.Length];
        scopeBytes.CopyTo(fingerprintInput, 0);
        fingerprintInput[scopeBytes.Length] = (byte)'\n';
        canonicalRequest.CopyTo(fingerprintInput.AsSpan(scopeBytes.Length + 1));
        var fingerprint = HMACSHA256.HashData(_fingerprintKey, fingerprintInput);
        CryptographicOperations.ZeroMemory(canonicalRequest);
        CryptographicOperations.ZeroMemory(fingerprintInput);
        CryptographicOperations.ZeroMemory(keyBytes);
        CryptographicOperations.ZeroMemory(keyHashInput);
        return new IdempotencyRequest(
            Convert.ToHexString(keyHash),
            Convert.ToHexString(fingerprint),
            context.TenantId,
            context.CallerId,
            context.Operation);
    }

    public byte[] ProtectSuccess(object response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var responseType = response.GetType();
        var resultType = responseType.IsGenericType &&
                         responseType.GetGenericTypeDefinition() == typeof(Result<>);
        var value = resultType ? responseType.GetProperty(nameof(Result<object>.Value))!.GetValue(response) : true;
        var valueType = resultType ? responseType.GetGenericArguments()[0] : typeof(bool);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(value, valueType, JsonOptions);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        var header = Encoding.ASCII.GetBytes($"KI2:{_activeKeyId}:");
        using var aes = new AesGcm(_outcomeEncryptionKey, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, header);
        var protectedOutcome = new byte[header.Length + nonce.Length + tag.Length + ciphertext.Length];
        header.CopyTo(protectedOutcome, 0);
        nonce.CopyTo(protectedOutcome, header.Length);
        tag.CopyTo(protectedOutcome, header.Length + nonce.Length);
        ciphertext.CopyTo(protectedOutcome, header.Length + nonce.Length + tag.Length);
        CryptographicOperations.ZeroMemory(plaintext);
        return protectedOutcome;
    }

    public object RestoreSuccess(byte[] protectedOutcome, Type responseType)
    {
        ArgumentNullException.ThrowIfNull(protectedOutcome);
        ArgumentNullException.ThrowIfNull(responseType);
        var offset = 0;
        var key = _outcomeEncryptionKey;
        if (protectedOutcome.AsSpan().StartsWith("KI2:"u8))
        {
            var separator = Array.IndexOf(protectedOutcome, (byte)':', 4);
            if (separator is < 5 or > 36 || !_outcomeKeys.TryGetValue(Encoding.ASCII.GetString(protectedOutcome, 4, separator - 4), out key))
            {
                throw new CryptographicException(IdentityReplayMessages.OutcomeUnavailable);
            }
            offset = separator + 1;
        }
        else if (!_allowLegacyOutcomes)
        {
            throw new CryptographicException(IdentityReplayMessages.OutcomeUnavailable);
        }
        else
        {
            key = _legacyOutcomeKey!;
        }
        if (protectedOutcome.Length - offset < 28)
        {
            throw new CryptographicException("The protected idempotency outcome is invalid.");
        }

        var plaintext = new byte[protectedOutcome.Length - offset - 28];
        using var aes = new AesGcm(key, 16);
        try
        {
            aes.Decrypt(
                protectedOutcome.AsSpan(offset, 12),
                protectedOutcome.AsSpan(offset + 28),
                protectedOutcome.AsSpan(offset + 12, 16),
                plaintext,
                protectedOutcome.AsSpan(0, offset));
            if (responseType == typeof(Result))
            {
                return ResultFactory.Ok();
            }

            if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Result<>))
            {
                throw new InvalidOperationException("Idempotent Identity commands must return Result or Result<T>.");
            }

            var resultType = responseType.GetGenericArguments()[0];
            var value = JsonSerializer.Deserialize(plaintext, resultType, JsonOptions);
            if (value is null && resultType.IsValueType && Nullable.GetUnderlyingType(resultType) is null)
            {
                throw new JsonException("The idempotency outcome was empty.");
            }

            return typeof(ResultFactory)
                .GetMethods()
                .Single(method => method.Name == nameof(ResultFactory.Success) && method.IsGenericMethodDefinition)
                .MakeGenericMethod(resultType)
                .Invoke(null, [value])!;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public object CreateKeyReuseConflict(Type responseType)
    {
        ArgumentNullException.ThrowIfNull(responseType);
        var error = ApplicationError.Conflict(
            "IDENTITY_IDEMPOTENCY_KEY_REUSED",
            "The Idempotency-Key was already used with a different request.");
        if (responseType == typeof(Result))
        {
            return ResultFactory.Failure(error);
        }

        if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Result<>))
        {
            throw new InvalidOperationException("Idempotent Identity commands must return Result or Result<T>.");
        }

        var resultType = responseType.GetGenericArguments()[0];
        return typeof(ResultFactory)
            .GetMethods()
            .Single(method => method.Name == nameof(ResultFactory.Failure) && method.IsGenericMethodDefinition)
            .MakeGenericMethod(resultType)
            .Invoke(null, [error])!;
    }

    private static byte[] DeriveKey(byte[] rootKey, string purpose) =>
        HMACSHA256.HashData(rootKey, Encoding.UTF8.GetBytes(purpose));
}
