using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Infrastructure;
using Leno.SystemAdmin.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Leno.SystemAdmin.Infrastructure.Tests.Repositories;

public sealed class EfCoreMenuRepositoryTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SystemAdminDbContext _db;
    private readonly EfCoreMenuRepository _repo;

    public EfCoreMenuRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<SystemAdminDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new SystemAdminDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new EfCoreMenuRepository(_db);
    }

    [Fact]
    public async Task AddAsync_PersistsMenu()
    {
        var menu = Menu.CreateRoot(Guid.NewGuid(), "用户管理", MenuType.Directory, "/user-access");

        await _repo.AddAsync(menu, default);

        var loaded = await _repo.GetByIdAsync(menu.Id, default);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("用户管理");
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid(), default);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByPathAsync_ReturnsMatchingMenu()
    {
        var menu = Menu.CreateRoot(Guid.NewGuid(), "用户列表", MenuType.Menu, "/user/list", component: "User/List/index");
        await _repo.AddAsync(menu, default);

        var loaded = await _repo.GetByPathAsync("/user/list", default);
        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(menu.Id);
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsDirectChildrenSorted()
    {
        var parent = Menu.CreateRoot(Guid.NewGuid(), "父菜单", MenuType.Directory, "/parent");
        await _repo.AddAsync(parent, default);
        var child1 = Menu.CreateChild(Guid.NewGuid(), parent.Id, "子菜单B", MenuType.Menu, "/parent/b", "Parent/B/index", sort: 2);
        var child2 = Menu.CreateChild(Guid.NewGuid(), parent.Id, "子菜单A", MenuType.Menu, "/parent/a", "Parent/A/index", sort: 1);
        await _repo.AddAsync(child1, default);
        await _repo.AddAsync(child2, default);

        var children = await _repo.GetChildrenAsync(parent.Id, default);

        children.Should().HaveCount(2);
        children[0].Name.Should().Be("子菜单A");
        children[1].Name.Should().Be("子菜单B");
    }

    [Fact]
    public async Task CountChildrenAsync_ReturnsDirectChildCount()
    {
        var parent = Menu.CreateRoot(Guid.NewGuid(), "父菜单", MenuType.Directory, "/parent2");
        await _repo.AddAsync(parent, default);
        var child = Menu.CreateChild(Guid.NewGuid(), parent.Id, "子菜单", MenuType.Menu, "/parent2/c", "Parent2/C/index");
        await _repo.AddAsync(child, default);
        var grandchild = Menu.CreateChild(Guid.NewGuid(), child.Id, "孙菜单", MenuType.Menu, "/parent2/c/g", "Parent2/C/G/index");
        await _repo.AddAsync(grandchild, default);

        var count = await _repo.CountChildrenAsync(parent.Id, default);

        count.Should().Be(1);
    }

    [Fact]
    public async Task GetByRoleAsync_FiltersByExactRoleMatch()
    {
        var adminMenu = Menu.CreateRoot(Guid.NewGuid(), "管理菜单", MenuType.Directory, "/admin",
            roles: new List<string> { "Admin" });
        var operatorMenu = Menu.CreateRoot(Guid.NewGuid(), "运营菜单", MenuType.Directory, "/op",
            roles: new List<string> { "Operator" });
        var superAdminMenu = Menu.CreateRoot(Guid.NewGuid(), "超级管理菜单", MenuType.Directory, "/super",
            roles: new List<string> { "SuperAdmin" });
        await _repo.AddAsync(adminMenu, default);
        await _repo.AddAsync(operatorMenu, default);
        await _repo.AddAsync(superAdminMenu, default);

        var result = await _repo.GetByRoleAsync("Admin", default);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("管理菜单");
    }

    [Fact]
    public async Task DeleteAsync_WithSubtree_RemovesAllDescendants()
    {
        var root = Menu.CreateRoot(Guid.NewGuid(), "根", MenuType.Directory, "/root3");
        await _repo.AddAsync(root, default);
        var child = Menu.CreateChild(Guid.NewGuid(), root.Id, "子", MenuType.Menu, "/root3/c", "Root3/C/index");
        await _repo.AddAsync(child, default);
        var grandchild = Menu.CreateChild(Guid.NewGuid(), child.Id, "孙", MenuType.Menu, "/root3/c/g", "Root3/C/G/index");
        await _repo.AddAsync(grandchild, default);

        await _repo.DeleteAsync(root.Id, default);

        var remaining = await _db.Menus.ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var menu = Menu.CreateRoot(Guid.NewGuid(), "原名", MenuType.Directory, "/rename");
        await _repo.AddAsync(menu, default);
        menu.Rename("新名");

        await _repo.UpdateAsync(menu, default);

        var loaded = await _repo.GetByIdAsync(menu.Id, default);
        loaded!.Name.Should().Be("新名");
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
