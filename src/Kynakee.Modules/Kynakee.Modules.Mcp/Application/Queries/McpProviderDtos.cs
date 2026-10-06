using Kynakee.Modules.Mcp.Domain.Enums;

namespace Kynakee.Modules.Mcp.Application.Queries;

public sealed record McpProviderListItemDto(
    Guid Id, string Name, McpProviderStatus Status, decimal Rating, int ServerCount);

public sealed record McpProviderPageDto(
    IReadOnlyList<McpProviderListItemDto> Items, int Offset, int PageSize, bool HasMore);

public sealed record McpServerDto(Guid Id, Uri Endpoint);

public sealed record McpProviderDetailDto(
    Guid Id, string Name, McpProviderStatus Status, decimal Rating,
    IReadOnlyList<string> Categories, IReadOnlyList<string> GeoRegions,
    IReadOnlyList<McpServerDto> Servers);
