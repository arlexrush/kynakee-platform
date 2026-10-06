using Kynakee.Modules.Mcp.Domain.Enums;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Mcp.Application.Commands.ChangeMcpProviderStatus;

public sealed record ChangeMcpProviderStatusCommand(Guid ProviderId, McpProviderStatus Status) : ICommand<Guid>;
