# GameServer.GUI Tests

This folder contains all tests for the GameServer.GUI application.

## Test Projects

### GameServer.Docker.Tests
Unit tests for the `GameServer.Docker` library. These tests focus on:
- Docker service operations
- Helper classes and utilities
- Constants and configurations
- Service label management
- Volume and port mapping logic

**Framework**: xUnit  
**Mocking**: Moq

### GameServer.Docker.Agent.Tests
Unit tests for the `GameServer.Docker.Agent` service. These tests focus on:
- Container operations API (stats, logs, inspect)
- Health check endpoints
- Agent configuration options
- Service instantiation and dependency injection

**Framework**: xUnit  
**Mocking**: Moq  
**Additional**: Docker.DotNet for Docker-specific testing

### GameServer.Web.Tests
Unit tests for the `GameServer.Web` Blazor application. These tests focus on:
- Blazor components (using bUnit)
- UI logic and interactions
- Service integrations
- SignalR hubs
- Pages and layouts

**Framework**: xUnit  
**Mocking**: Moq  
**UI Testing**: bUnit

### GameServer.Integration.Tests
Integration tests for the entire application. These tests focus on:
- End-to-end workflows
- API endpoints
- Database operations
- Docker Swarm interactions
- Multi-component scenarios

**Framework**: xUnit  
**Testing**: Microsoft.AspNetCore.Mvc.Testing

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Project
```bash
dotnet test tests/GameServer.Docker.Tests
dotnet test tests/GameServer.Docker.Agent.Tests
dotnet test tests/GameServer.Web.Tests
dotnet test tests/GameServer.Integration.Tests
```

### Run with Code Coverage
```bash
# Install dotnet-coverage tool (one-time)
dotnet tool install -g dotnet-coverage

# Run tests with coverage
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

### Run Tests in Watch Mode
```bash
dotnet watch test --project tests/GameServer.Docker.Tests
```

## Test Conventions

### Naming
- Test classes: `{ClassUnderTest}Tests` (e.g., `DockerServiceHelperTests`)
- Test methods: `{MethodOrScenario}_{Condition}_{ExpectedBehavior}` (e.g., `ServiceLabels_ShouldHaveCorrectManagedLabelKey`)

### Structure
All tests follow the **Arrange-Act-Assert (AAA)** pattern:
```csharp
[Fact]
public void MyTest()
{
    // Arrange - Set up test data and dependencies
    var mockService = new Mock<IMyService>();
    var sut = new SystemUnderTest(mockService.Object);
    
    // Act - Execute the method being tested
    var result = sut.DoSomething();
    
    // Assert - Verify the expected outcome
    Assert.Equal(expectedValue, result);
}
```

### Dependencies
- **DO** use mocks for external dependencies (Docker clients, databases, file systems)
- **DO** test public APIs and behaviors, not implementation details
- **DON'T** use `InternalsVisibleTo` unless absolutely necessary
- **DON'T** test private methods directly

## Key Testing Areas

### High Priority
1. **Service Label Management** - Ensure constants match documentation
2. **Docker Service Operations** - Create, update, delete, list services
3. **Node Agent Discovery** - Multi-node routing logic
4. **Volume and Port Mapping** - Immutability and persistence
5. **SignalR Hubs** - Real-time communication
6. **Blazor Components** - UI rendering and interactions

### Medium Priority
1. **Configuration Management** - Settings and extended metadata
2. **Error Handling** - Exception scenarios
3. **Performance** - Parallel operations, batch processing
4. **Security** - Authentication and authorization

### Low Priority (Consider)
1. **Logging** - Verify log messages (use mocks)
2. **UI Styling** - Visual regression testing
3. **Browser Compatibility** - Playwright/Selenium tests

## Best Practices

### Unit Tests
- Keep tests fast (< 100ms each)
- Test one behavior per test
- Use descriptive test names
- Avoid test interdependencies
- Mock external dependencies

### Integration Tests
- Use `WebApplicationFactory` for API testing
- Clean up test data after each test
- Use realistic test scenarios
- Consider using TestContainers for Docker tests

### Blazor Component Tests
- Use bUnit for component rendering
- Test user interactions (clicks, inputs)
- Verify component lifecycle
- Mock services and dependencies
- Test both happy paths and error scenarios

## Continuous Integration

Tests are automatically run on:
- Pull requests
- Commits to `main` branch
- Nightly builds

All tests must pass before merging to `main`.

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [bUnit Documentation](https://bunit.dev/)
- [Microsoft.AspNetCore.Mvc.Testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [.NET Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
