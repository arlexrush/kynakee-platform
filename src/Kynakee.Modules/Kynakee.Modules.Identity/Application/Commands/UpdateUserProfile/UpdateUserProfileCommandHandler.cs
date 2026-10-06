using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using MediatR;

namespace Kynakee.Modules.Identity.Application.Commands.UpdateUserProfile;

public sealed class UpdateUserProfileCommandHandler : IRequestHandler<UpdateUserProfileCommand, Result>
{
    private readonly IIdentityRepository _repository;
    private readonly ITenantContext _tenantContext;

    public UpdateUserProfileCommandHandler(IIdentityRepository repository, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tenantContext);
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Result> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_tenantContext.IsAuthenticated || request.TenantId != _tenantContext.TenantId ||
            request.UserId != _tenantContext.UserId)
        {
            return ResultFactory.Failure(ApplicationError.Unauthorized(
                "IDENTITY_PROFILE_FORBIDDEN", "Users can only update their own profile."));
        }

        var user = await _repository.GetUserAsync(request.TenantId, request.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return ResultFactory.Failure(ApplicationError.NotFound(
                "IDENTITY_USER_NOT_FOUND", "The user was not found."));
        }

        var phone = request.Phone is null ? null : PhoneNumber.Create(request.Phone);
        if (phone?.IsFailure == true)
        {
            return ResultFactory.Failure(phone.Error!);
        }

        return user.UpdateProfile(request.FirstName, request.LastName, phone?.Value, request.UserId);
    }
}