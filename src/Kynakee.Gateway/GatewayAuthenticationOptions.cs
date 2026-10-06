using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Gateway;

[SuppressMessage("Performance", "CA1812", Justification = "Bound and instantiated by the options framework.")]
internal sealed class GatewayAuthenticationOptions
{
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "Kynakee";

    public string Audience { get; set; } = "KynakeeClients";
}