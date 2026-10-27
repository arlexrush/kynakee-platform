using Kynakee.Modules.Identity.Domain.ValueObjects;
using FluentValidation;

namespace Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;

public sealed class UpdateTenantProfileCommandValidator : AbstractValidator<UpdateTenantProfileCommand>
{
    public UpdateTenantProfileCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Slug).Must(value => TenantSlug.Create(value).IsSuccess);
        When(command => command.SuppliedFiscalFields == FiscalProfileFields.All, () =>
        {
            RuleFor(command => command.TaxId)
                .Must((command, value) => value is null
                    ? command.TaxCountry is null
                    : TaxId.Create(value, command.TaxCountry).IsSuccess);
            When(HasFiscalAddress, () =>
            {
                RuleFor(command => command.FiscalCountry).NotEmpty().Length(2).Matches("^[A-Za-z]{2}$");
                RuleFor(command => command.Municipality).NotEmpty().MaximumLength(100);
                RuleFor(command => command.PostalCode).NotEmpty().MaximumLength(20);
                RuleFor(command => command.Street).NotEmpty().MaximumLength(300);
                RuleFor(command => command.Region).Must(value => value is null || !string.IsNullOrWhiteSpace(value)).MaximumLength(100);
                RuleFor(command => command.Province).Must(value => value is null || !string.IsNullOrWhiteSpace(value)).MaximumLength(100);
            });
        });
        RuleFor(command => command.PlanId).NotEmpty().MaximumLength(100);
    }

    internal static bool HasFiscalAddress(UpdateTenantProfileCommand command) =>
        command.FiscalCountry is not null ||
        command.Region is not null ||
        command.Province is not null ||
        command.Municipality is not null ||
        command.PostalCode is not null ||
        command.Street is not null;
}