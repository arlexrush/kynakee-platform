using FluentValidation;
using Kynakee.Modules.Identity.Domain.ValueObjects;

namespace Kynakee.Modules.Identity.Application.Commands.InviteUser;

public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.InviterUserId).NotEmpty();
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Email).Must(value => Email.Create(value).IsSuccess);
        RuleFor(command => command.Phone)
            .Must(value => value is null || PhoneNumber.Create(value).IsSuccess);
        RuleFor(command => command.Role).IsInEnum().NotEqual(UserRole.Owner);
    }
}