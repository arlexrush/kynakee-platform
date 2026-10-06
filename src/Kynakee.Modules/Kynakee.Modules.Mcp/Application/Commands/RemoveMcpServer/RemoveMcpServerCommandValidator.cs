using FluentValidation;

namespace Kynakee.Modules.Mcp.Application.Commands.RemoveMcpServer;

public sealed class RemoveMcpServerCommandValidator : AbstractValidator<RemoveMcpServerCommand>
{
    public RemoveMcpServerCommandValidator()
    {
        RuleFor(command => command.ProviderId).NotEmpty();
        RuleFor(command => command.ServerId).NotEmpty();
    }
}
