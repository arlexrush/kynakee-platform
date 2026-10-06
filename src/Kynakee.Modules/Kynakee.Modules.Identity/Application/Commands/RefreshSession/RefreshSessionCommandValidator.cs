using FluentValidation;

namespace Kynakee.Modules.Identity.Application.Commands.RefreshSession;

public sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionCommandValidator() =>
        RuleFor(command => command.RefreshToken).NotEmpty().MaximumLength(256);
}