using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.RevokeSession;

public sealed record RevokeSessionCommand(string RefreshToken) : ICommand;