using System.Text.Json;
using FluentAssertions;
using Kynakee.Api.Endpoints;
using Kynakee.Modules.Identity.Application.Commands.UpdateTenantProfile;
using Xunit;

namespace Kynakee.UnitTests.Identity.Application;

public sealed class TenantProfileRequestTests
{
    [Fact]
    public void DeserializeOmittedFiscalFieldsShouldNotMarkFieldsAsSupplied()
    {
        var request = JsonSerializer.Deserialize<UpdateTenantProfileRequest>(
            "{\"name\":\"Tenant\",\"slug\":\"tenant\",\"planId\":\"starter\"}", JsonSerializerOptions.Web);

        request!.SuppliedFiscalFields.Should().Be(FiscalProfileFields.None);
    }

    [Fact]
    public void DeserializeExplicitNullFiscalFieldShouldMarkFieldAsSupplied()
    {
        var request = JsonSerializer.Deserialize<UpdateTenantProfileRequest>(
            "{\"name\":\"Tenant\",\"slug\":\"tenant\",\"planId\":\"starter\",\"region\":null}", JsonSerializerOptions.Web);

        request!.SuppliedFiscalFields.Should().Be(FiscalProfileFields.Region);
    }
}
