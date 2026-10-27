using FluentAssertions;
using Kynakee.Modules.Identity.Application.Abstractions;
using Kynakee.Modules.Identity.Application.Commands.ChangeUserRole;
using Kynakee.Modules.Identity.Application.Commands.DeactivateTenantUser;
using Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;
using Kynakee.Modules.Identity.Application.Commands.UpdateTenantSettings;
using Kynakee.Modules.Identity.Application.Commands.UpdateUserProfile;
using Kynakee.Modules.Identity.Application.Contracts;
using Kynakee.Modules.Identity.Application.Queries.GetCurrentIdentity;
using Kynakee.Modules.Identity.Application.Queries.GetTenantProfile;
using Kynakee.Modules.Identity.Application.Queries.GetTenantUsers;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Kynakee.Modules.SharedKernel.Application;
using Kynakee.Modules.SharedKernel.Contracts;
using NSubstitute;
using Xunit;

namespace Kynakee.UnitTests.Identity.Application;

public sealed class IdentityAccessHandlerTests
{
    private readonly IIdentityRepository _repository = Substitute.For<IIdentityRepository>();
    private readonly ITenantContext _context = Substitute.For<ITenantContext>();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public IdentityAccessHandlerTests()
    {
        _context.TenantId.Returns(_tenantId);
        _context.UserId.Returns(_userId);
        _context.IsAuthenticated.Returns(true);
        SetProfiles(UserStatus.Active, TenantStatus.Active);
    }

    [Theory]
    [InlineData("CurrentIdentity")]
    [InlineData("TenantProfile")]
    [InlineData("TenantUsers")]
    [InlineData("UserProfile")]
    [InlineData("UpdateTenant")]
    [InlineData("Settings")]
    [InlineData("Role")]
    [InlineData("Deactivate")]
    public async Task IdentityHandlerInactiveActorShouldReturnForbidden(string operation)
    {
        SetProfiles(UserStatus.Inactive, TenantStatus.Active);

        var result = await ExecuteAsync(operation, _tenantId);

        GetError(result)!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Theory]
    [InlineData("CurrentIdentity")]
    [InlineData("TenantProfile")]
    [InlineData("TenantUsers")]
    [InlineData("UserProfile")]
    [InlineData("UpdateTenant")]
    [InlineData("Settings")]
    [InlineData("Role")]
    [InlineData("Deactivate")]
    public async Task IdentityHandlerSuspendedTenantShouldReturnForbidden(string operation)
    {
        SetProfiles(UserStatus.Active, TenantStatus.Suspended);

        var result = await ExecuteAsync(operation, _tenantId);

        GetError(result)!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Theory]
    [InlineData("CurrentIdentity")]
    [InlineData("TenantProfile")]
    [InlineData("TenantUsers")]
    [InlineData("UserProfile")]
    [InlineData("UpdateTenant")]
    [InlineData("Settings")]
    [InlineData("Role")]
    [InlineData("Deactivate")]
    public async Task IdentityHandlerOtherTenantShouldReturnForbidden(string operation)
    {
        var otherTenantId = Guid.NewGuid();

        var result = await ExecuteAsync(operation, otherTenantId);

        GetError(result)!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task CurrentIdentityActiveActorShouldReturnOwnProfile()
    {
        var handler = new GetCurrentIdentityQueryHandler(_repository, _context);

        var result = await handler.Handle(new GetCurrentIdentityQuery(_tenantId, _userId), CancellationToken.None);

        result.Value!.User.Id.Should().Be(_userId);
    }

    [Fact]
    public async Task CurrentIdentityFiscalAddressShouldNotBeExposed()
    {
        var profile = new IdentityTenantProfile(_tenantId, "Tenant", "tenant", TenantType.Company, "starter", TenantStatus.Active, null, null,
            new IdentityCompanySettings(0, 0, 0, 0, 0, 0), new IdentityCompanyBranding(null, null, null, null))
        {
            FiscalAddress = new IdentityFiscalAddress("GB", null, null, "London", "SW1A 1AA", "Main Street")
        };
        _repository.GetTenantProfileAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(profile);

        var result = await new GetCurrentIdentityQueryHandler(_repository, _context)
            .Handle(new GetCurrentIdentityQuery(_tenantId, _userId), CancellationToken.None);

        result.Value!.Tenant.FiscalAddress.Should().BeNull();
    }

    [Theory]
    [InlineData("Role")]
    [InlineData("Deactivate")]
    public async Task OwnerMutationPendingLockShouldNotReadAuthorization(string operation)
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _repository.AcquireTenantOwnerLockAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(signal.Task);
        var execution = ExecuteAsync(operation, _tenantId);
        var readsBeforeRelease = _repository.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "GetUserProfileAsync");
        signal.SetResult();

        await execution.ConfigureAwait(true);

        readsBeforeRelease.Should().Be(0);
    }

    [Theory]
    [InlineData("Role")]
    [InlineData("Deactivate")]
    public async Task OwnerMutationOtherTenantShouldNotAcquireLock(string operation)
    {
        await ExecuteAsync(operation, Guid.NewGuid());

        await _repository.DidNotReceive().AcquireTenantOwnerLockAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("TenantProfile")]
    [InlineData("TenantUsers")]
    [InlineData("UpdateTenant")]
    [InlineData("Settings")]
    [InlineData("Role")]
    [InlineData("Deactivate")]
    public async Task IdentityHandlerMemberRoleShouldRejectAdministration(string operation)
    {
        _repository.GetUserProfileAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns(
            new IdentityUserProfile(_userId, _tenantId, "Test", "User", "user@example.test", null, UserRole.Viewer, UserStatus.Active));

        var result = await ExecuteAsync(operation, _tenantId);

        GetError(result)!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Theory]
    [InlineData("Role")]
    [InlineData("Deactivate")]
    public async Task IdentityHandlerAdminRoleShouldRejectOwnerOperation(string operation)
    {
        _repository.GetUserProfileAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns(
            new IdentityUserProfile(_userId, _tenantId, "Test", "Admin", "admin@example.test", null, UserRole.Admin, UserStatus.Active));

        var result = await ExecuteAsync(operation, _tenantId);

        GetError(result)!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task TenantProfileActiveOwnerShouldReturnOwnTenant()
    {
        var handler = new GetTenantProfileQueryHandler(_repository, _context);

        var result = await handler.Handle(new GetTenantProfileQuery(_tenantId, _userId), CancellationToken.None);

        result.Value!.Id.Should().Be(_tenantId);
    }

    private void SetProfiles(UserStatus userStatus, TenantStatus tenantStatus)
    {
        _repository.GetUserProfileAsync(_tenantId, _userId, Arg.Any<CancellationToken>()).Returns(
            new IdentityUserProfile(_userId, _tenantId, "Test", "Owner", "owner@example.test", null, UserRole.Owner, userStatus));
        _repository.GetTenantProfileAsync(_tenantId, Arg.Any<CancellationToken>()).Returns(
            new IdentityTenantProfile(_tenantId, "Tenant", "tenant", TenantType.Company, "starter", tenantStatus, null, null,
                new IdentityCompanySettings(0, 0, 0, 0, 0, 0), new IdentityCompanyBranding(null, null, null, null)));
    }

    [Fact]
    public async Task UpdateTenantProfileWithoutFiscalAddressShouldSucceed()
    {
        var tenant = Tenant.Create("Tenant", TenantSlug.Create("tenant").Value,
            TenantType.Company, null, null, "starter").Value!;
        _context.TenantId.Returns(tenant.Id);
        _repository.GetUserProfileAsync(tenant.Id, _userId, Arg.Any<CancellationToken>()).Returns(
            new IdentityUserProfile(_userId, tenant.Id, "Test", "Owner", "owner@example.test", null, UserRole.Owner, UserStatus.Active));
        _repository.GetTenantProfileAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(
            new IdentityTenantProfile(tenant.Id, "Tenant", "tenant", TenantType.Company, "starter", TenantStatus.Active, null, null,
                new IdentityCompanySettings(0, 0, 0, 0, 0, 0), new IdentityCompanyBranding(null, null, null, null)));
        _repository.GetTenantAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);
        var handler = new UpdateTenantProfileCommandHandler(_repository, _context);
        var command = new UpdateTenantProfileCommand(tenant.Id, "Updated tenant", "tenant", null, null,
            null, null, null, null, null, null, "starter", tenant.Version);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateTenantProfileOmittedFiscalFieldsShouldPreserveExistingValues()
    {
        var tenant = CreateFiscalTenant();
        var command = FiscalCommand(tenant) with { SuppliedFiscalFields = FiscalProfileFields.None };

        var result = await new UpdateTenantProfileCommandHandler(_repository, _context).Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tenant.TaxId!.Value.Should().Be("GB123456789");
        tenant.FiscalAddress!.Street.Should().Be("Main Street");
    }

    [Fact]
    public async Task UpdateTenantProfileExplicitNullFiscalValuesShouldClearFiscalProfile()
    {
        var tenant = CreateFiscalTenant();
        var command = FiscalCommand(tenant) with { SuppliedFiscalFields = FiscalProfileFields.All };

        var result = await new UpdateTenantProfileCommandHandler(_repository, _context).Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tenant.TaxId.Should().BeNull();
        tenant.FiscalAddress.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTenantProfileExplicitNullTaxIdAloneShouldRejectIncompleteTaxIdentity()
    {
        var tenant = CreateFiscalTenant();
        var command = FiscalCommand(tenant) with { SuppliedFiscalFields = FiscalProfileFields.TaxId };

        var result = await new UpdateTenantProfileCommandHandler(_repository, _context).Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        tenant.TaxId!.Value.Should().Be("GB123456789");
    }

    [Fact]
    public async Task UpdateTenantProfileExplicitNullRegionShouldClearRegion()
    {
        var tenant = CreateFiscalTenant();
        var command = FiscalCommand(tenant) with { SuppliedFiscalFields = FiscalProfileFields.Region };

        var result = await new UpdateTenantProfileCommandHandler(_repository, _context).Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tenant.FiscalAddress!.Region.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTenantProfileExplicitNullStreetShouldRejectIncompleteAddress()
    {
        var tenant = CreateFiscalTenant();
        var command = FiscalCommand(tenant) with { SuppliedFiscalFields = FiscalProfileFields.Street };

        var result = await new UpdateTenantProfileCommandHandler(_repository, _context).Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        tenant.FiscalAddress!.Street.Should().Be("Main Street");
    }

    private Tenant CreateFiscalTenant()
    {
        var tenant = Tenant.Create("Tenant", TenantSlug.Create("tenant").Value, TenantType.Company,
            TaxId.Create("GB123456789", "GB").Value,
            Address.Create("GB", "London", null, "London", "SW1A 1AA", "Main Street").Value, "starter").Value!;
        _context.TenantId.Returns(tenant.Id);
        _repository.GetUserProfileAsync(tenant.Id, _userId, Arg.Any<CancellationToken>()).Returns(
            new IdentityUserProfile(_userId, tenant.Id, "Test", "Owner", "owner@example.test", null, UserRole.Owner, UserStatus.Active));
        _repository.GetTenantProfileAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(
            new IdentityTenantProfile(tenant.Id, "Tenant", "tenant", TenantType.Company, "starter", TenantStatus.Active, null, null,
                new IdentityCompanySettings(0, 0, 0, 0, 0, 0), new IdentityCompanyBranding(null, null, null, null)));
        _repository.GetTenantAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(tenant);
        return tenant;
    }

    private static UpdateTenantProfileCommand FiscalCommand(Tenant tenant) =>
        new(tenant.Id, "Updated tenant", "tenant", null, null, null, null, null, null, null, null, "starter", tenant.Version);

    private async Task<Kynakee.Modules.SharedKernel.Application.IResult> ExecuteAsync(string operation, Guid tenantId) => operation switch
    {
        "CurrentIdentity" => await new GetCurrentIdentityQueryHandler(_repository, _context)
            .Handle(new GetCurrentIdentityQuery(tenantId, _userId), CancellationToken.None).ConfigureAwait(false),
        "TenantProfile" => await new GetTenantProfileQueryHandler(_repository, _context)
            .Handle(new GetTenantProfileQuery(tenantId, _userId), CancellationToken.None).ConfigureAwait(false),
        "TenantUsers" => await new GetTenantUsersQueryHandler(_repository, _context)
            .Handle(new GetTenantUsersQuery(tenantId, _userId), CancellationToken.None).ConfigureAwait(false),
        "UserProfile" => await new UpdateUserProfileCommandHandler(_repository, _context)
            .Handle(new UpdateUserProfileCommand(tenantId, _userId, "Test", "Owner", null, 0), CancellationToken.None).ConfigureAwait(false),
        "UpdateTenant" => await new UpdateTenantProfileCommandHandler(_repository, _context)
            .Handle(new UpdateTenantProfileCommand(tenantId, "Tenant", "tenant", null, null, null, null, null, null, null, null, "starter", 0), CancellationToken.None).ConfigureAwait(false),
        "Settings" => await new UpdateTenantSettingsCommandHandler(_repository, _context)
            .Handle(new UpdateTenantSettingsCommand(tenantId, 0, 0, 0, 0, 0, 0, 0), CancellationToken.None).ConfigureAwait(false),
        "Role" => await new ChangeUserRoleCommandHandler(_repository, _context)
            .Handle(new ChangeUserRoleCommand(tenantId, _userId, UserRole.Admin, 0), CancellationToken.None).ConfigureAwait(false),
        "Deactivate" => await new DeactivateTenantUserCommandHandler(_repository, _context, TimeProvider.System)
            .Handle(new DeactivateTenantUserCommand(tenantId, _userId, 0), CancellationToken.None).ConfigureAwait(false),
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static ApplicationError? GetError(Kynakee.Modules.SharedKernel.Application.IResult result) => result switch
    {
        Result value => value.Error,
        Result<IdentitySessionProfile> value => value.Error,
        Result<IdentityTenantProfile> value => value.Error,
        Result<IReadOnlyList<TenantUserListItem>> value => value.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(result))
    };
}
