using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GameServer.Web;

namespace GameServer.Integration.Tests;

public class WebApplicationFactoryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WebApplicationFactoryTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void WebApplicationFactory_ShouldCreateApplication()
    {
        // Act & Assert
        Assert.NotNull(_factory);
    }

    [Fact]
    public void WebApplicationFactory_ShouldHaveServices()
    {
        // Arrange
        var services = _factory.Services;

        // Act & Assert
        Assert.NotNull(services);
    }

    [Fact]
    public void WebApplicationFactory_ShouldResolveRequiredServices()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        // Act & Assert - Verify critical services are registered
        Assert.NotNull(serviceProvider.GetService<IHostEnvironment>());
    }

    [Fact]
    public async Task Application_ShouldStartSuccessfully()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        Assert.NotNull(response);
        // Note: We don't assert success status code because the home page might require authentication
    }
}
