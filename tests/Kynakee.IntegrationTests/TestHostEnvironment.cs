using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Kynakee.IntegrationTests;

internal static class TestHostEnvironment
{
    internal static IHostEnvironment Create()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Testing");
        return environment;
    }
}
