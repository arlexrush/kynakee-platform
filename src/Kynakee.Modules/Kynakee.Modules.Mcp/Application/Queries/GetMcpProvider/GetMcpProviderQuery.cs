using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Mcp.Application.Queries.GetMcpProvider;

public sealed record GetMcpProviderQuery(Guid ProviderId) : IQuery<McpProviderDetailDto>;
