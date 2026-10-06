using FluentValidation;
using Kynakee.Modules.Identity.Domain.ValueObjects;

namespace Kynakee.Modules.Identity.Application.Commands.UpdateUserProfile;

public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Phone)
            .Must(value => value is null || PhoneNumber.Create(value).IsSuccess);
    }
}