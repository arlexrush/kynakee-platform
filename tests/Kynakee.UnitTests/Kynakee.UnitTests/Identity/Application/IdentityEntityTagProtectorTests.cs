using Kynakee.Api.Infrastructure.Identity;
using Xunit;

namespace Kynakee.UnitTests.Identity.Application;

public sealed class IdentityEntityTagProtectorTests
{
    private readonly string _key = new('k', 32);
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void CreatedEntityTagShouldPreserveResourceAndVersion()
    {
        var resourceId = Guid.NewGuid();
        const uint version = 42;
        var protector = new IdentityEntityTagProtector(_key);
        var entityTag = protector.Create(_tenantId, IdentityEntityTagResource.User, resourceId, version);

        var isValid = protector.TryRead(entityTag, _tenantId, IdentityEntityTagResource.User, resourceId, out var actualVersion);

        Assert.True(isValid);
        Assert.Equal(version, actualVersion);
        Assert.StartsWith("\"", entityTag, StringComparison.Ordinal);
        Assert.EndsWith("\"", entityTag, StringComparison.Ordinal);
        Assert.DoesNotContain("=", entityTag, StringComparison.Ordinal);
    }

    [Fact]
    public void EntityTagShouldNotBeValidForAnotherResource()
    {
        var protector = new IdentityEntityTagProtector(_key);
        var entityTag = protector.Create(_tenantId, IdentityEntityTagResource.User, Guid.NewGuid(), 7);

        var isValid = protector.TryRead(entityTag, _tenantId, IdentityEntityTagResource.User, Guid.NewGuid(), out _);

        Assert.False(isValid);
    }

    [Fact]
    public void ModifiedVersionInsideEntityTagShouldNotBeAccepted()
    {
        var resourceId = Guid.NewGuid();
        var protector = new IdentityEntityTagProtector(_key);
        var entityTag = protector.Create(_tenantId, IdentityEntityTagResource.User, resourceId, 7);
        var payload = Convert.FromBase64String(entityTag[1..^1].Replace('-', '+').Replace('_', '/'));
        payload[19] ^= 1;
        var modifiedEntityTag = $"\"{Convert.ToBase64String(payload).TrimEnd('=').Replace('+', '-').Replace('/', '_')}\"";

        var isValid = protector.TryRead(modifiedEntityTag, _tenantId, IdentityEntityTagResource.User, resourceId, out _);

        Assert.False(isValid);
    }

    [Fact]
    public void ModifiedEntityTagShouldNotBeAccepted()
    {
        var resourceId = Guid.NewGuid();
        var protector = new IdentityEntityTagProtector(_key);
        var entityTag = protector.Create(_tenantId, IdentityEntityTagResource.User, resourceId, 7);
        var modifiedEntityTag = $"{entityTag[..^2]}{(entityTag[^2] == 'A' ? 'B' : 'A')}\"";

        var isValid = protector.TryRead(modifiedEntityTag, _tenantId, IdentityEntityTagResource.User, resourceId, out _);

        Assert.False(isValid);
    }

    [Fact]
    public void EntityTagFromAnotherSigningKeyShouldNotBeAccepted()
    {
        var resourceId = Guid.NewGuid();
        var entityTag = new IdentityEntityTagProtector(_key).Create(_tenantId, IdentityEntityTagResource.User, resourceId, 7);
        var otherProtector = new IdentityEntityTagProtector(new string('x', 32));

        var isValid = otherProtector.TryRead(entityTag, _tenantId, IdentityEntityTagResource.User, resourceId, out _);

        Assert.False(isValid);
    }

    [Fact]
    public void MaximumVersionShouldRoundTrip()
    {
        var resourceId = Guid.NewGuid();
        var protector = new IdentityEntityTagProtector(_key);
        var entityTag = protector.Create(_tenantId, IdentityEntityTagResource.User, resourceId, uint.MaxValue);

        var isValid = protector.TryRead(entityTag, _tenantId, IdentityEntityTagResource.User, resourceId, out var version);

        Assert.True(isValid);
        Assert.Equal(uint.MaxValue, version);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void EntityTagOtherResourceTypeShouldBeRejected(int otherType)
    {
        var resourceId = Guid.NewGuid();
        var protector = new IdentityEntityTagProtector(_key);
        var tag = protector.Create(_tenantId, IdentityEntityTagResource.User, resourceId, 7);

        Assert.False(protector.TryRead(tag, _tenantId, (IdentityEntityTagResource)otherType, resourceId, out _));
    }

    [Fact]
    public void EntityTagOtherTenantShouldBeRejected()
    {
        var resourceId = Guid.NewGuid();
        var protector = new IdentityEntityTagProtector(_key);
        var tag = protector.Create(_tenantId, IdentityEntityTagResource.User, resourceId, 7);

        Assert.False(protector.TryRead(tag, Guid.NewGuid(), IdentityEntityTagResource.User, resourceId, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("W/\"value\"")]
    [InlineData("\"one\", \"two\"")]
    [InlineData("\"!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!\"")]
    public void InvalidEntityTagShouldBeRejected(string? tag)
    {
        var protector = new IdentityEntityTagProtector(_key);

        Assert.False(protector.TryRead(tag, _tenantId, IdentityEntityTagResource.User, Guid.NewGuid(), out _));
    }
}
