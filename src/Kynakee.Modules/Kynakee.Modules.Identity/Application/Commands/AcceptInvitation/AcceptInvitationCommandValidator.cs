using Kynakee.Modules.Identity.Application.Validation;
using FluentValidation;

namespace Kynakee.Modules.Identity.Application.Commands.AcceptInvitation;

public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(command => command.InvitationToken).NotEmpty().MaximumLength(256);
        RuleFor(command => command.Password).StrongPassword();
    }
}