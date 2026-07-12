using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class HealthAggregationResultTests
{
    #region ComputeOverallStatus

    [Fact]
    public void ComputeOverallStatus_WithEmptyModules_ShouldReturnHealthy()
    {
        var result = HealthAggregationResult.ComputeOverallStatus(new List<ModuleHealth>());

        result.Should().Be(ModuleHealthStatus.Healthy);
    }

    [Fact]
    public void ComputeOverallStatus_WithAllHealthy_ShouldReturnHealthy()
    {
        var modules = new List<ModuleHealth>
        {
            ModuleHealth.Healthy("OrderService"),
            ModuleHealth.Healthy("PaymentService"),
            ModuleHealth.Healthy("InventoryService")
        };

        var result = HealthAggregationResult.ComputeOverallStatus(modules);

        result.Should().Be(ModuleHealthStatus.Healthy);
    }

    [Fact]
    public void ComputeOverallStatus_WithOneDegraded_ShouldReturnDegraded()
    {
        var modules = new List<ModuleHealth>
        {
            ModuleHealth.Healthy("OrderService"),
            ModuleHealth.Degraded("PaymentService", "Slow response"),
            ModuleHealth.Healthy("InventoryService")
        };

        var result = HealthAggregationResult.ComputeOverallStatus(modules);

        result.Should().Be(ModuleHealthStatus.Degraded);
    }

    [Fact]
    public void ComputeOverallStatus_WithOneUnhealthy_ShouldReturnUnhealthy()
    {
        var modules = new List<ModuleHealth>
        {
            ModuleHealth.Healthy("OrderService"),
            ModuleHealth.Degraded("PaymentService", "Slow response"),
            ModuleHealth.Unhealthy("InventoryService", "Connection refused")
        };

        var result = HealthAggregationResult.ComputeOverallStatus(modules);

        result.Should().Be(ModuleHealthStatus.Unhealthy);
    }

    [Fact]
    public void ComputeOverallStatus_WithUnhealthyAndDegraded_ShouldReturnUnhealthy()
    {
        var modules = new List<ModuleHealth>
        {
            ModuleHealth.Degraded("OrderService", "Slow response"),
            ModuleHealth.Unhealthy("PaymentService", "Connection refused")
        };

        var result = HealthAggregationResult.ComputeOverallStatus(modules);

        result.Should().Be(ModuleHealthStatus.Unhealthy);
    }

    [Fact]
    public void ComputeOverallStatus_WithSingleModuleHealthy_ShouldReturnHealthy()
    {
        var modules = new List<ModuleHealth>
        {
            ModuleHealth.Healthy("OrderService")
        };

        var result = HealthAggregationResult.ComputeOverallStatus(modules);

        result.Should().Be(ModuleHealthStatus.Healthy);
    }

    [Fact]
    public void ComputeOverallStatus_WithSingleModuleUnhealthy_ShouldReturnUnhealthy()
    {
        var modules = new List<ModuleHealth>
        {
            ModuleHealth.Unhealthy("OrderService", "Timeout")
        };

        var result = HealthAggregationResult.ComputeOverallStatus(modules);

        result.Should().Be(ModuleHealthStatus.Unhealthy);
    }

    #endregion

    #region HealthAggregationResult Record

    [Fact]
    public void Result_WithDefaultConstructor_ShouldHaveEmptyModules()
    {
        var result = new HealthAggregationResult();

        result.Modules.Should().NotBeNull();
        result.Modules.Should().BeEmpty();
    }

    [Fact]
    public void Result_WithModules_ShouldSetProperties()
    {
        var modules = new List<ModuleHealth>
        {
            ModuleHealth.Healthy("OrderService"),
            ModuleHealth.Unhealthy("PaymentService", "Down")
        };

        var result = new HealthAggregationResult
        {
            OverallStatus = ModuleHealthStatus.Unhealthy,
            Modules = modules,
            AggregatedAt = DateTime.UtcNow
        };

        result.OverallStatus.Should().Be(ModuleHealthStatus.Unhealthy);
        result.Modules.Should().HaveCount(2);
        result.AggregatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion
}