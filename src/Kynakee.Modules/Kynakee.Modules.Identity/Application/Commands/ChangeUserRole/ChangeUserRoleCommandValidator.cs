using FluentValidation;
using Kynakee.Modules.Identity.Domain.ValueObjects;

namespace Kynakee.Modules.Identity.Application.Commands.ChangeUserRole;

public sealed class ChangeUserRoleCommandValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Role).IsInEnum();
    }
}