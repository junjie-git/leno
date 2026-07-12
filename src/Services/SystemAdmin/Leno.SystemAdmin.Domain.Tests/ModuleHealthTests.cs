using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class ModuleHealthTests
{
    private const string ValidModule = "OrderService";
    private static readonly List<string> ValidDependencies = new() { "PaymentService", "InventoryService" };

    #region Constructor - Happy Path

    [Fact]
    public void Constructor_WithValidParameters_ShouldSetAllProperties()
    {
        var checkedAt = DateTime.UtcNow;
        var health = new ModuleHealth(ValidModule, ModuleHealthStatus.Healthy, ValidDependencies, checkedAt, 150, null);

        health.Module.Should().Be(ValidModule);
        health.Status.Should().Be(ModuleHealthStatus.Healthy);
        health.Dependencies.Should().BeEquivalentTo(ValidDependencies);
        health.CheckedAt.Should().Be(checkedAt);
        health.ResponseTimeMs.Should().Be(150);
        health.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullDependencies_ShouldInitializeEmptyList()
    {
        var health = new ModuleHealth(ValidModule, ModuleHealthStatus.Healthy, null!, DateTime.UtcNow);

        health.Dependencies.Should().NotBeNull();
        health.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithTrimmedModuleName_ShouldTrimWhitespace()
    {
        var health = new ModuleHealth("  OrderService  ", ModuleHealthStatus.Healthy, [], DateTime.UtcNow);

        health.Module.Should().Be("OrderService");
    }

    [Fact]
    public void Constructor_WithAllStatuses_ShouldSucceed()
    {
        foreach (ModuleHealthStatus status in Enum.GetValues<ModuleHealthStatus>())
        {
            var health = new ModuleHealth($"Module-{status}", status, [], DateTime.UtcNow);
            health.Status.Should().Be(status);
        }
    }

    [Fact]
    public void Constructor_WithModuleNameAtMaxLength_ShouldSucceed()
    {
        var moduleName = new string('m', 128);

        var health = new ModuleHealth(moduleName, ModuleHealthStatus.Healthy, [], DateTime.UtcNow);

        health.Module.Should().Be(moduleName);
    }

    #endregion

    #region Constructor - Validation

    [Fact]
    public void Constructor_WithNullModule_ShouldThrowModuleNameEmpty()
    {
        var act = () => new ModuleHealth(null!, ModuleHealthStatus.Healthy, [], DateTime.UtcNow);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("MODULE_NAME_EMPTY");
    }

    [Fact]
    public void Constructor_WithEmptyModule_ShouldThrowModuleNameEmpty()
    {
        var act = () => new ModuleHealth("", ModuleHealthStatus.Healthy, [], DateTime.UtcNow);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("MODULE_NAME_EMPTY");
    }

    [Fact]
    public void Constructor_WithWhitespaceModule_ShouldThrowModuleNameEmpty()
    {
        var act = () => new ModuleHealth("   ", ModuleHealthStatus.Healthy, [], DateTime.UtcNow);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("MODULE_NAME_EMPTY");
    }

    [Fact]
    public void Constructor_WithModuleNameTooLong_ShouldThrowModuleNameLength()
    {
        var longName = new string('m', 129);

        var act = () => new ModuleHealth(longName, ModuleHealthStatus.Healthy, [], DateTime.UtcNow);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("MODULE_NAME_LENGTH");
    }

    [Fact]
    public void Constructor_WithInvalidStatus_ShouldThrowModuleStatusInvalid()
    {
        var invalidStatus = (ModuleHealthStatus)999;

        var act = () => new ModuleHealth(ValidModule, invalidStatus, [], DateTime.UtcNow);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("MODULE_STATUS_INVALID");
    }

    #endregion

    #region Factory Methods

    [Fact]
    public void Healthy_ShouldCreateHealthyModule()
    {
        var health = ModuleHealth.Healthy(ValidModule, ValidDependencies, 100);

        health.Status.Should().Be(ModuleHealthStatus.Healthy);
        health.Module.Should().Be(ValidModule);
        health.Dependencies.Should().BeEquivalentTo(ValidDependencies);
        health.ResponseTimeMs.Should().Be(100);
        health.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Healthy_WithNullDependencies_ShouldInitializeEmptyList()
    {
        var health = ModuleHealth.Healthy(ValidModule, null);

        health.Dependencies.Should().NotBeNull();
        health.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Degraded_ShouldCreateDegradedModule()
    {
        var errorMessage = "Service partially available";

        var health = ModuleHealth.Degraded(ValidModule, errorMessage, ValidDependencies, 200);

        health.Status.Should().Be(ModuleHealthStatus.Degraded);
        health.ErrorMessage.Should().Be(errorMessage);
        health.ResponseTimeMs.Should().Be(200);
    }

    [Fact]
    public void Unhealthy_ShouldCreateUnhealthyModule()
    {
        var errorMessage = "Service unavailable";

        var health = ModuleHealth.Unhealthy(ValidModule, errorMessage, ValidDependencies);

        health.Status.Should().Be(ModuleHealthStatus.Unhealthy);
        health.ErrorMessage.Should().Be(errorMessage);
        health.ResponseTimeMs.Should().Be(-1);
    }

    #endregion

    #region Record Equality

    [Fact]
    public void Records_WithSameValues_ShouldBeEqual()
    {
        var checkedAt = DateTime.UtcNow;
        var deps = new List<string> { "Dep1" };

        var health1 = new ModuleHealth(ValidModule, ModuleHealthStatus.Healthy, deps, checkedAt, 100, "err");
        var health2 = new ModuleHealth(ValidModule, ModuleHealthStatus.Healthy, deps, checkedAt, 100, "err");

        health1.Should().Be(health2);
    }

    [Fact]
    public void Records_WithDifferentStatus_ShouldNotBeEqual()
    {
        var checkedAt = DateTime.UtcNow;

        var health1 = new ModuleHealth(ValidModule, ModuleHealthStatus.Healthy, [], checkedAt);
        var health2 = new ModuleHealth(ValidModule, ModuleHealthStatus.Degraded, [], checkedAt, errorMessage: "err");

        health1.Should().NotBe(health2);
    }

    #endregion
}