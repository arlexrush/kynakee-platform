using System.Globalization;
using System.Resources;

namespace Kynakee.Modules.Bots.Domain.Resources;

internal static class BotsMessages
{
    private static readonly ResourceManager Resources = new(
        "Kynakee.Modules.Bots.Domain.Resources.BotsMessages",
        typeof(BotsMessages).Assembly);

    internal static string Get(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture)
        ?? throw new MissingManifestResourceException(name);
}
