using FluentValidation;
using Kynakee.Modules.Identity.Domain.ValueObjects;

namespace Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;

public sealed class UpdateTenantProfileCommandValidator : AbstractValidator<UpdateTenantProfileCommand>
{
    public UpdateTenantProfileCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Slug).Must(value => TenantSlug.Create(value).IsSuccess);
        RuleFor(command => command.TaxId)
            .Must((command, value) => value is null
                ? command.TaxCountry is null
                : TaxId.Create(value, command.TaxCountry).IsSuccess);
        RuleFor(command => command.FiscalCountry)
            .NotEmpty()
            .Length(2)
            .Matches("^[A-Za-z]{2}$");
        RuleFor(command => command.PlanId).NotEmpty().MaximumLength(100);
    }
}