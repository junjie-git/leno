using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Leno.Order.Infrastructure;

/// <summary>
/// EF Core 设计期工厂，避免 dotnet ef migrations add 启动完整 Program.cs（依赖 Redis 等基础设施）。
/// 仅用于生成迁移与脚本，不连接真实数据库。
/// </summary>
public sealed class OrderDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=LenoOrder;User Id=sa;Password=Leno@SqlServer2019;TrustServerCertificate=True;MultipleActiveResultSets=true")
            .Options;
        return new OrderDbContext(options);
    }
}
