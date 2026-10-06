using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Mcp.Application.Commands.RegisterMcpProvider;

public sealed record RegisterMcpProviderCommand(
    string Name, IReadOnlyList<string> Categories, IReadOnlyList<string> GeoRegions) : ICommand<Guid>;
