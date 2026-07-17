namespace Leno.ReviewAfterSales.Infrastructure.Tests;

/// <summary>
/// 基础冒烟测试，验证项目可加载与执行；F4.3 将在此项目内补充集成测试。
/// </summary>
public class SmokeTests
{
    [Fact]
    public void ProjectAssembly_ShouldLoadSuccessfully()
    {
        var assembly = typeof(Leno.ReviewAfterSales.Infrastructure.ReviewAfterSalesDbContext).Assembly;
        assembly.FullName.Should().NotBeNull();
        assembly.GetName().Name.Should().Be("Leno.ReviewAfterSales.Infrastructure");
    }
}
