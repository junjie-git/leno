using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Leno.Cart.Infrastructure;

/// <summary>
/// EF Core 设计期工厂，避免 dotnet ef migrations add 启动完整 Program.cs（依赖 Redis 等基础设施）。
/// 仅用于生成迁移与脚本，不连接真实数据库。
/// </summary>
public sealed class CartDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CartDbContext>
{
    public CartDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=LenoCart;User Id=sa;Password=Leno@SqlServer2019;TrustServerCertificate=True;MultipleActiveResultSets=true")
            .Options;
        return new CartDbContext(options);
    }
}
