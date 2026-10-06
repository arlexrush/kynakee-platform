using FluentValidation;

namespace Kynakee.Modules.Identity.Application.Commands.DeactivateTenantUser;

public sealed class DeactivateTenantUserCommandValidator : AbstractValidator<DeactivateTenantUserCommand>
{
    public DeactivateTenantUserCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
    }
}