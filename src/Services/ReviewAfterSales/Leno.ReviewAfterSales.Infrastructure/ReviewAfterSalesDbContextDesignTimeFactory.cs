using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Leno.ReviewAfterSales.Infrastructure;

/// <summary>
/// EF Core 设计期工厂，避免 dotnet ef migrations add 启动完整 Program.cs（依赖 Redis 等基础设施）。
/// 仅用于生成迁移与脚本，不连接真实数据库。
/// </summary>
public sealed class ReviewAfterSalesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ReviewAfterSalesDbContext>
{
    public ReviewAfterSalesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReviewAfterSalesDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=LenoReviewAfterSales;User Id=sa;Password=Leno@SqlServer2019;TrustServerCertificate=True;MultipleActiveResultSets=true")
            .Options;
        return new ReviewAfterSalesDbContext(options);
    }
}
