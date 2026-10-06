using System.Globalization;
using System.Resources;

namespace Kynakee.Modules.KnowledgeBase.Domain.Resources;

internal static class KnowledgeBaseMessages
{
    private static readonly ResourceManager Resources = new(
        "Kynakee.Modules.KnowledgeBase.Domain.Resources.KnowledgeBaseMessages",
        typeof(KnowledgeBaseMessages).Assembly);

    internal static string Get(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture)
        ?? throw new MissingManifestResourceException($"Missing KnowledgeBase message: {name}");
}