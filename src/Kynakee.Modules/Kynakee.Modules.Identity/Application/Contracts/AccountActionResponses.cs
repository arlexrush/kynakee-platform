using System.Globalization;
using System.Resources;

namespace Kynakee.Modules.Identity.Application.Contracts;

/// <summary>Generic acknowledgement; does not disclose account existence or guarantee delivery.</summary>
public sealed record AccountActionAcknowledgement(string Message)
{
    /// <summary>Returns the same acknowledgement for eligible and unknown addresses.</summary>
    public static AccountActionAcknowledgement Pending => new(AccountActionMessages.Get("Pending"));
}

internal static class AccountActionMessages
{
    private static readonly ResourceManager Resources = new("Kynakee.Modules.Identity.Application.AccountActionResources", typeof(AccountActionMessages).Assembly);
    internal static string Get(string name) => Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
