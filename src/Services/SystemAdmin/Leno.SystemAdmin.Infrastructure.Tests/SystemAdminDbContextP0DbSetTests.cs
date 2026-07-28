using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Tests;

public sealed class SystemAdminDbContextP0DbSetTests
{
    [Fact]
    public void DbContext_Contains_MenusDbSet()
    {
        var options = new DbContextOptionsBuilder<SystemAdminDbContext>()
            .UseInMemoryDatabase("p0-dbset-menus")
            .Options;
        using var db = new SystemAdminDbContext(options);

        db.Menus.Should().NotBeNull();
    }

    [Fact]
    public void DbContext_Contains_LoginLogsDbSet()
    {
        var options = new DbContextOptionsBuilder<SystemAdminDbContext>()
            .UseInMemoryDatabase("p0-dbset-loginlogs")
            .Options;
        using var db = new SystemAdminDbContext(options);

        db.LoginLogs.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsMenu()
    {
        var options = new DbContextOptionsBuilder<SystemAdminDbContext>()
            .UseInMemoryDatabase("p0-dbset-persist")
            .Options;
        using var db = new SystemAdminDbContext(options);
        var menu = Menu.CreateRoot(Guid.NewGuid(), "菜单A", MenuType.Directory, "/a");

        await db.Menus.AddAsync(menu);
        await db.SaveChangesAsync();

        var loaded = await db.Menus.FirstOrDefaultAsync(m => m.Id == menu.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("菜单A");
    }
}
