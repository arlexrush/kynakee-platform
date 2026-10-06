using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.DeactivateTenantUser;

public sealed record DeactivateTenantUserCommand(Guid TenantId, Guid UserId) : ICommand;