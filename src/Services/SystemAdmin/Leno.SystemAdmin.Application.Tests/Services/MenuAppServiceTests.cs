using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.SystemAdmin.Application.Tests.Services;

public sealed class MenuAppServiceTests
{
    private readonly Mock<IMenuRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly MenuAppService _service;

    public MenuAppServiceTests()
    {
        _unitOfWork.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _service = new MenuAppService(_repo.Object, _unitOfWork.Object, NullLogger<MenuAppService>.Instance);
    }

    [Fact]
    public async Task GetTreeAsync_ReturnsHierarchicalList()
    {
        var root = Menu.CreateRoot(Guid.NewGuid(), "系统管理", MenuType.Directory, "/system");
        var child1 = Menu.CreateChild(Guid.NewGuid(), root.Id, "菜单管理", MenuType.Menu, "/system/menus", "MenuList");
        var child2 = Menu.CreateChild(Guid.NewGuid(), root.Id, "用户管理", MenuType.Menu, "/system/users", "UserList");
        var menus = new List<Menu> { root, child1, child2 };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(menus);

        var tree = await _service.GetTreeAsync(default);

        tree.Should().HaveCount(1);
        tree[0].Name.Should().Be("系统管理");
        tree[0].Children.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_ValidDto_CallsRepoAddAsyncOnce()
    {
        _repo.Setup(r => r.GetByPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Menu?)null);
        var dto = new CreateMenuDto { Name = "测试", Type = MenuType.Directory, Path = "/test", Component = null, Icon = null };

        var result = await _service.CreateAsync(dto, Guid.NewGuid(), default);

        result.Name.Should().Be("测试");
        _repo.Verify(r => r.AddAsync(It.IsAny<Menu>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DuplicatePath_ThrowsBusinessException()
    {
        var existing = Menu.CreateRoot(Guid.NewGuid(), "已存在", MenuType.Directory, "/test");
        _repo.Setup(r => r.GetByPathAsync("/test", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var dto = new CreateMenuDto { Name = "测试", Type = MenuType.Directory, Path = "/test" };

        var act = () => _service.CreateAsync(dto, Guid.NewGuid(), default);

        await act.Should().ThrowAsync<SystemAdminDomainException>()
            .Where(e => e.ErrorCode == "MENU_PATH_DUPLICATE");
    }

    [Fact]
    public async Task UpdateAsync_MenuNotFound_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Menu?)null);
        var dto = new UpdateMenuDto { Name = "新名称" };

        var act = () => _service.UpdateAsync(id, dto, Guid.NewGuid(), default);

        await act.Should().ThrowAsync<SystemAdminDomainException>()
            .Where(e => e.ErrorCode == "MENU_NOT_FOUND");
    }

    [Fact]
    public async Task DeleteAsync_WithChildren_ThrowsBusinessException()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.CountChildrenAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var act = () => _service.DeleteAsync(id, Guid.NewGuid(), default);

        await act.Should().ThrowAsync<SystemAdminDomainException>()
            .Where(e => e.ErrorCode == "MENU_HAS_CHILDREN");
    }

    [Fact]
    public async Task DeleteAsync_NoChildren_CallsRepoDeleteAsync()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.CountChildrenAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        await _service.DeleteAsync(id, Guid.NewGuid(), default);

        _repo.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SortAsync_ReordersAllItems()
    {
        var items = new List<MenuSortItemDto>
        {
            new() { Id = Guid.NewGuid(), Sort = 1 },
            new() { Id = Guid.NewGuid(), Sort = 2 },
            new() { Id = Guid.NewGuid(), Sort = 3 }
        };
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => Menu.CreateRoot(id, "test", MenuType.Directory, "/t"));

        await _service.SortAsync(items, Guid.NewGuid(), default);

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Menu>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
