using FluentValidation;

namespace Kynakee.Modules.Mcp.Application.Commands.ChangeMcpProviderStatus;

public sealed class ChangeMcpProviderStatusCommandValidator : AbstractValidator<ChangeMcpProviderStatusCommand>
{
    public ChangeMcpProviderStatusCommandValidator()
    {
        RuleFor(command => command.ProviderId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
    }
}
