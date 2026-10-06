using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.UpdateUserProfile;

public sealed record UpdateUserProfileCommand(
    Guid TenantId,
    Guid UserId,
    string FirstName,
    string LastName,
    string? Phone) : ICommand;