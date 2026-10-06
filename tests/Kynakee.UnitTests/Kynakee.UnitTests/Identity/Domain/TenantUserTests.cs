using FluentAssertions;
using Kynakee.Modules.Identity.Domain.Aggregates;
using Kynakee.Modules.Identity.Domain.Entities;
using Kynakee.Modules.Identity.Domain.ValueObjects;
using Xunit;

namespace Kynakee.UnitTests.Identity.Domain;

public class TenantUserTests
{
    [Fact]
    public void CreateShouldCopyTenantAndUserIdentifiers()
    {
        var user = CreateUser();

        var membership = TenantUser.Create(user, UserRole.Owner).Value!;

        membership.TenantId.Should().Be(user.TenantId);
        membership.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void ChangeRoleShouldUpdateMembership()
    {
        var membership = TenantUser.Create(CreateUser(), UserRole.Technician).Value!;

        var result = membership.ChangeRole(UserRole.Admin);

        result.IsSuccess.Should().BeTrue();
        membership.Role.Should().Be(UserRole.Admin);
    }

    private static User CreateUser() => User.Create(
        Guid.NewGuid(),
        "Ada",
        "Lovelace",
        Email.Create("ada@example.com").Value,
        null,
        null,
        UserStatus.PendingVerification).Value!;
}