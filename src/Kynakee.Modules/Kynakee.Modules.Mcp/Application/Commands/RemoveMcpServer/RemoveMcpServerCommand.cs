using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Mcp.Application.Commands.RemoveMcpServer;

public sealed record RemoveMcpServerCommand(Guid ProviderId, Guid ServerId) : ICommand<Guid>;
