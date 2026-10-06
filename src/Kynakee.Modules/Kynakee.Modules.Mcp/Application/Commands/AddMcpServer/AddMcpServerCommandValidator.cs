using FluentValidation;

namespace Kynakee.Modules.Mcp.Application.Commands.AddMcpServer;

public sealed class AddMcpServerCommandValidator : AbstractValidator<AddMcpServerCommand>
{
    public AddMcpServerCommandValidator()
    {
        RuleFor(command => command.ProviderId).NotEmpty();
        RuleFor(command => command.Endpoint).Must(endpoint => endpoint is not null && endpoint.IsAbsoluteUri &&
            endpoint.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(endpoint.UserInfo) &&
            string.IsNullOrEmpty(endpoint.Query) && string.IsNullOrEmpty(endpoint.Fragment));
        RuleFor(command => command.CredentialSecretReference).NotEmpty().MaximumLength(200);
    }
}
