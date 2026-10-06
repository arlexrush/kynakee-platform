using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    string Channel) : ICommand<IdentitySessionResponse>;