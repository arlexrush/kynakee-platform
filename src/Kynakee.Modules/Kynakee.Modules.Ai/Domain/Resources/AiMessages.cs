using System.Globalization;
using System.Resources;

namespace Kynakee.Modules.Ai.Domain.Resources;

internal static class AiMessages
{
    private static readonly ResourceManager Resources = new(
        "Kynakee.Modules.Ai.Domain.Resources.AiMessages",
        typeof(AiMessages).Assembly);

    internal static string Get(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture)
        ?? throw new MissingManifestResourceException($"Missing AI message: {name}");
}
