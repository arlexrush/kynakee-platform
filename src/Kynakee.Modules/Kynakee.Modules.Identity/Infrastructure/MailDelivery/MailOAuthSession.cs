using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using System.Globalization;
using System.Resources;
using System.Security.Cryptography;
using System.Text;

namespace Kynakee.Modules.Identity.Infrastructure.MailDelivery;

/// <summary>Gestiona consentimiento delegado y renovación silenciosa del buzón de invitaciones.</summary>
public sealed class MailOAuthSession
{
    private static readonly ResourceManager Resources = new("Kynakee.Modules.Identity.Infrastructure.IdentityPersistenceResources", typeof(MailOAuthSession).Assembly);
    private static readonly string[] Scopes = ["https://outlook.office.com/SMTP.Send"];
    private readonly IPublicClientApplication _application;
    private readonly string _username;
    private readonly string _path;
    private readonly byte[] _key;
    private readonly byte[] _associatedData;

    /// <summary>Construye una sesión con cache cifrado independiente; nunca solicita consentimiento desde el worker.</summary>
    public MailOAuthSession(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("Smtp:OAuth");
        var clientId = section["ClientId"];
        var tenantId = section["TenantId"];
        _username = section["Username"] ?? string.Empty;
        _path = section["CachePath"] ?? string.Empty;
        if (!Guid.TryParse(clientId, out _) || (tenantId != "consumers" && !Guid.TryParse(tenantId, out _)) ||
            string.IsNullOrWhiteSpace(_username) || !Path.IsPathFullyQualified(_path))
            throw new InvalidOperationException(Message("MailOAuthConfigurationInvalid"));
        try { _key = Convert.FromBase64String(section["CacheKey"] ?? string.Empty); }
        catch (FormatException) { throw new InvalidOperationException(Message("MailOAuthConfigurationInvalid")); }
        if (_key.Length != 32) throw new InvalidOperationException(Message("MailOAuthConfigurationInvalid"));
        _associatedData = Encoding.UTF8.GetBytes($"Kynakee.MailOAuth.v1:{clientId}:{tenantId}:{_username.ToUpperInvariant()}");
        _application = PublicClientApplicationBuilder.Create(clientId)
            .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
            .Build();
    }

    /// <summary>Obtiene un access token sin interacción; exige consentimiento administrativo previo.</summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var cacheLock = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        ConfigureCache(cancellationToken);
        var accounts = await _application.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.SingleOrDefault(candidate => string.Equals(candidate.Username, _username, StringComparison.OrdinalIgnoreCase));
        if (account is null) throw new InvalidOperationException(Message("MailOAuthConsentRequired"));
        AuthenticationResult result;
        try
        {
            result = await _application.AcquireTokenSilent(Scopes, account).ExecuteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MsalUiRequiredException) { throw new InvalidOperationException(Message("MailOAuthConsentRequired")); }
        return result.AccessToken;
    }

    /// <summary>Autoriza el buzón con código de dispositivo; el callback solo debe mostrar instrucciones en la consola administrativa.</summary>
    public async Task AuthorizeAsync(Func<DeviceCodeResult, Task> displayCodeAsync, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(displayCodeAsync);
        using var cacheLock = await AcquireLockAsync(cancellationToken).ConfigureAwait(false);
        ConfigureCache(cancellationToken);
        var result = await _application.AcquireTokenWithDeviceCode(Scopes, displayCodeAsync).ExecuteAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(result.Account.Username, _username, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(Message("MailOAuthAccountMismatch"));
    }

    private void ConfigureCache(CancellationToken cancellationToken)
    {
        _application.UserTokenCache.SetBeforeAccessAsync(args => LoadAsync(args, cancellationToken));
        _application.UserTokenCache.SetAfterAccessAsync(args => args.HasStateChanged ? SaveAsync(args, cancellationToken) : Task.CompletedTask);
    }

    private async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false); }
        }
    }

    private async Task LoadAsync(TokenCacheNotificationArgs args, CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            args.TokenCache.DeserializeMsalV3(null, shouldClearExistingCache: true);
            return;
        }
        var encrypted = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        if (encrypted.Length < 28) throw new InvalidOperationException(Message("MailOAuthCacheInvalid"));
        var plaintext = new byte[encrypted.Length - 28];
        try
        {
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(encrypted.AsSpan(0, 12), encrypted.AsSpan(28), encrypted.AsSpan(12, 16), plaintext, _associatedData);
            args.TokenCache.DeserializeMsalV3(plaintext, shouldClearExistingCache: true);
        }
        catch (CryptographicException) { throw new InvalidOperationException(Message("MailOAuthCacheInvalid")); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private async Task SaveAsync(TokenCacheNotificationArgs args, CancellationToken cancellationToken)
    {
        if (args.Account is not null && !string.Equals(args.Account.Username, _username, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(Message("MailOAuthAccountMismatch"));
        var plaintext = args.TokenCache.SerializeMsalV3();
        var encrypted = new byte[28 + plaintext.Length];
        RandomNumberGenerator.Fill(encrypted.AsSpan(0, 12));
        try
        {
            using var aes = new AesGcm(_key, 16);
            aes.Encrypt(encrypted.AsSpan(0, 12), plaintext, encrypted.AsSpan(28), encrypted.AsSpan(12, 16), _associatedData);
            var temporaryPath = _path + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, encrypted, cancellationToken).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private static string Message(string name) => Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
