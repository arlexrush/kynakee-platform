using System.Diagnostics.CodeAnalysis;

namespace Kynakee.Gateway;

[SuppressMessage("Design", "CA1515", Justification = "The integration test host uses this type to locate the Gateway assembly.")]
public sealed class GatewayEntryPointMarker
{
}
