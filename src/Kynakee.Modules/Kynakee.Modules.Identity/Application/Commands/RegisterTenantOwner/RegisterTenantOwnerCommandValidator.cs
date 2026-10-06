using FluentValidation;
using Kynakee.Modules.Identity.Application.Validation;
using Kynakee.Modules.Identity.Domain.ValueObjects;

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
        RuleFor(command => command.PlanId).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Channel)
            .Must(channel => channel is "web" or "telegram" or "whatsapp");
    }
}