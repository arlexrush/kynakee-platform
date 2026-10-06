using FluentValidation;

namespace Kynakee.Modules.Identity.Application.Commands.UpdateTenantSettings;

public sealed class UpdateTenantSettingsCommandValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    public UpdateTenantSettingsCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Administration).InclusiveBetween(0, 100);
        RuleFor(command => command.Profit).InclusiveBetween(0, 100);
        RuleFor(command => command.Quality).InclusiveBetween(0, 100);
        RuleFor(command => command.SafetyHealth).InclusiveBetween(0, 100);
        RuleFor(command => command.Environment).InclusiveBetween(0, 100);
        RuleFor(command => command.Contingency).InclusiveBetween(0, 100);
    }
}