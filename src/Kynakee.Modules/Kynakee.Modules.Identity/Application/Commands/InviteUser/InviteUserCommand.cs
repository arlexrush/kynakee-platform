using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.InviteUser;

public sealed record InviteUserCommand(
    Guid TenantId,
    Guid InviterUserId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    UserRole Role) : ICommand<InvitationCreatedResponse>;