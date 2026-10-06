using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Commands;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Kynakee.Modules.Identity.Application.Commands.RegisterTenantOwner;

public sealed class RegisterTenantOwnerCommandHandler
    : IRequestHandler<RegisterTenantOwnerCommand, Result<IdentitySessionResponse>>
{
    private readonly IIdentityRepository _repository;
    private readonly UserManager<User> _userManager;
    private readonly IIdentityTokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public RegisterTenantOwnerCommandHandler(
        IIdentityRepository repository,
        UserManager<User> userManager,
        IIdentityTokenService tokenService,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(tokenService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _userManager = userManager;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IdentitySessionResponse>> Handle(
        RegisterTenantOwnerCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = Email.Create(request.Email);
        var phone = PhoneNumber.Create(request.Phone);
        var taxId = TaxId.Create(request.TaxId, request.TaxCountry);
        var branding = CompanyBranding.Create(companyName: request.TenantName);
        var slug = await CreateAvailableTenantSlugAsync(request.TenantName, cancellationToken)
            .ConfigureAwait(false);

        if (email.IsFailure || phone.IsFailure || taxId.IsFailure || branding.IsFailure || slug.IsFailure)
        {
            var error = email.Error ?? phone.Error ?? taxId.Error ?? branding.Error ?? slug.Error!;
            return ResultFactory.Failure<IdentitySessionResponse>(error);
        }

        var tenantResult = Tenant.Create(
            request.TenantName,
            slug.Value,
            request.TenantType,
            taxId.Value,
            null,
            request.PlanId,
            branding.Value,
            CompanySettings.Default);
        if (tenantResult.IsFailure)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(tenantResult.Error!);
        }

        var tenant = tenantResult.Value!;
        var userResult = User.Create(
            tenant.Id,
            request.FirstName,
            request.LastName,
            email.Value,
            phone.Value,
            null,
            UserStatus.Inactive);
        if (userResult.IsFailure)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(userResult.Error!);
        }

        var user = userResult.Value!;
        _repository.AddTenant(tenant);
        var identityResult = await _userManager.CreateAsync(user, request.Password)
            .ConfigureAwait(false);
        if (!identityResult.Succeeded)
        {
            return MapIdentityFailure(identityResult);
        }

        var activation = user.Activate();
        if (activation.IsFailure)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(activation.Error!);
        }

        var membershipResult = TenantUser.Create(user, UserRole.Owner);
        if (membershipResult.IsFailure)
        {
            return ResultFactory.Failure<IdentitySessionResponse>(membershipResult.Error!);
        }

        var membership = membershipResult.Value!;
        _repository.AddTenantUser(membership);

        return IdentitySessionFactory.Create(
            user,
            tenant,
            membership,
            _repository,
            _tokenService,
            _timeProvider.GetUtcNow());
    }

    private async Task<Result<TenantSlug>> CreateAvailableTenantSlugAsync(
        string tenantName,
        CancellationToken cancellationToken)
    {
        var baseSlug = CreateSlugBase(tenantName);
        if (baseSlug.Length == 0)
        {
            baseSlug = "tenant";
        }

        var candidate = baseSlug;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var parsedSlug = TenantSlug.Create(candidate);
            if (parsedSlug.IsSuccess &&
                !await _repository.TenantSlugExistsAsync(parsedSlug.Value!.Value, cancellationToken)
                    .ConfigureAwait(false))
            {
                return parsedSlug;
            }

            candidate = $"{baseSlug[..Math.Min(baseSlug.Length, 30)]}-{Guid.NewGuid():N}";
        }

        return ResultFactory.Failure<TenantSlug>(
            ApplicationError.Conflict("IDENTITY_TENANT_SLUG_UNAVAILABLE", "A unique tenant slug could not be generated."));
    }

    private static string CreateSlugBase(string value)
    {
        var decomposed = value.Normalize(System.Text.NormalizationForm.FormD);
        var slug = new System.Text.StringBuilder(decomposed.Length);
        var previousWasSeparator = true;

#pragma warning disable CA1308 // Tenant slugs are defined as lowercase URL identifiers.
        foreach (var character in decomposed)
        {
            if (char.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                slug.Append('-');
                previousWasSeparator = true;
            }
        }
#pragma warning restore CA1308

        return slug.ToString().Trim('-');
    }

    private static Result<IdentitySessionResponse> MapIdentityFailure(IdentityResult identityResult)
    {
        if (identityResult.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            return ResultFactory.Failure<IdentitySessionResponse>(
                ApplicationError.Conflict("IDENTITY_EMAIL_ALREADY_REGISTERED", "An account with this email already exists."));
        }

        return ResultFactory.Failure<IdentitySessionResponse>(
            ApplicationError.Validation("IDENTITY_USER_CREATION_FAILED", "Identity rejected the user registration."));
    }
}