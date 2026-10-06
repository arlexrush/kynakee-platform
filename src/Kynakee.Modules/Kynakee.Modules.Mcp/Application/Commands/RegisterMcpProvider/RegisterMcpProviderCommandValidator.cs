using FluentValidation;

namespace Kynakee.Modules.Mcp.Application.Commands.RegisterMcpProvider;

public sealed class RegisterMcpProviderCommandValidator : AbstractValidator<RegisterMcpProviderCommand>
{
    public RegisterMcpProviderCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Categories).NotEmpty();
        RuleForEach(command => command.Categories).NotEmpty().MaximumLength(100);
        RuleFor(command => command.GeoRegions).NotEmpty();
        RuleForEach(command => command.GeoRegions).NotEmpty().MaximumLength(50);
    }
}
