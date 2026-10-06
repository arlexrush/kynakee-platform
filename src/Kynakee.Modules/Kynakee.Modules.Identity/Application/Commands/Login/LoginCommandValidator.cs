using FluentValidation;
using Kynakee.Modules.Identity.Domain.ValueObjects;

namespace Kynakee.Modules.Identity.Application.Commands.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email)
            .Must(value => Email.Create(value).IsSuccess);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(256);
        RuleFor(command => command.Channel)
            .Must(channel => channel is "web" or "telegram" or "whatsapp");
    }
}