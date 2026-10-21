using System.Globalization;
using System.Resources;

namespace Kynakee.Modules.Projects.Infrastructure.Storage;

internal static class ObjectStorageMessages
{
    private static readonly ResourceManager Resources = new("Kynakee.Modules.Projects.Infrastructure.Storage.ObjectStorageResources", typeof(ObjectStorageMessages).Assembly);

    internal static string Get(string name) => Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
