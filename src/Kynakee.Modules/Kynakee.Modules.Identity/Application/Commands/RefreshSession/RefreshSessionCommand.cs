using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.RefreshSession;

public sealed record RefreshSessionCommand(string RefreshToken) : ICommand<IdentitySessionResponse>;