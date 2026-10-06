using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Mcp.Application.Queries.ListMcpProviders;

public sealed record ListMcpProvidersQuery(
    string? GeoRegion = null, string? Category = null, McpProviderStatus? Status = null,
    int Offset = 0, int PageSize = 20) : IQuery<McpProviderPageDto>;
