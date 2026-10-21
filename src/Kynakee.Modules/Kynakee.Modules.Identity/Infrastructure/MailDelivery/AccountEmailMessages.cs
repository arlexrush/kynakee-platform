using System.Globalization;
using System.Resources;

namespace Kynakee.Modules.Identity.Infrastructure.MailDelivery;

internal static class AccountEmailMessages
{
    private static readonly ResourceManager Resources = new("Kynakee.Modules.Identity.Infrastructure.AccountEmailResources", typeof(AccountEmailMessages).Assembly);
    internal static string Get(string name) => Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
