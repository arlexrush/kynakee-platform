using Kynakee.Modules.Identity.Application.Validation;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using FluentValidation;

namespace Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;

public sealed class RegisterTenantOwnerCommandValidator : AbstractValidator<RegisterTenantOwnerCommand>
{
    public RegisterTenantOwnerCommandValidator()
    {
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Email)
            .Must(value => Email.Create(value).IsSuccess);
        RuleFor(command => command.Password).StrongPassword();
        RuleFor(command => command.Phone)
            .Must(value => PhoneNumber.Create(value).IsSuccess);
        RuleFor(command => command.TenantName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.TenantType).IsInEnum();
        RuleFor(command => command.TaxId)
            .Must((command, value) => TaxId.Create(value, command.TaxCountry).IsSuccess);
        RuleFor(command => command.TaxCountry)
            .NotEmpty()
            .Length(2)
            .Matches("^[A-Za-z]{2}$");
        When(HasFiscalAddress, () =>
        {
            RuleFor(command => command.FiscalCountry)
                .NotEmpty()
                .Length(2)
                .Matches("^[A-Za-z]{2}$");
            RuleFor(command => command.FiscalMunicipality)
                .NotEmpty()
                .MaximumLength(100);
            RuleFor(command => command.FiscalPostalCode)
                .NotEmpty()
                .MaximumLength(20);
            RuleFor(command => command.FiscalStreet)
                .NotEmpty()
                .MaximumLength(300);
            RuleFor(command => command.FiscalRegion).MaximumLength(100);
            RuleFor(command => command.FiscalProvince).MaximumLength(100);
        });
        RuleFor(command => command.PlanId).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Channel)
            .Must(channel => channel is "web" or "telegram" or "whatsapp");
    }

    private static bool HasFiscalAddress(RegisterTenantOwnerCommand command) =>
        !string.IsNullOrWhiteSpace(command.FiscalCountry) ||
        !string.IsNullOrWhiteSpace(command.FiscalRegion) ||
        !string.IsNullOrWhiteSpace(command.FiscalProvince) ||
        !string.IsNullOrWhiteSpace(command.FiscalMunicipality) ||
        !string.IsNullOrWhiteSpace(command.FiscalPostalCode) ||
        !string.IsNullOrWhiteSpace(command.FiscalStreet);
}