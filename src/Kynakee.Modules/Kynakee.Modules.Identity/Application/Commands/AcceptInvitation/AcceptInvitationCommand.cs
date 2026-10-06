using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.AcceptInvitation;

public sealed record AcceptInvitationCommand(
    string InvitationToken,
    string Password) : ICommand<IdentitySessionResponse>;