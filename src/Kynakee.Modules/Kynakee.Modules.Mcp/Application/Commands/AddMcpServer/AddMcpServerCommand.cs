using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Mcp.Application.Commands.AddMcpServer;

public sealed record AddMcpServerCommand(
    Guid ProviderId, Uri Endpoint, string CredentialSecretReference) : ICommand<Guid>;
