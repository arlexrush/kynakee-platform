using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;

namespace Kynakee.Modules.Identity.Application.Commands.ChangeUserRole;

public sealed record ChangeUserRoleCommand(Guid TenantId, Guid UserId, UserRole Role) : ICommand;