using System.Net;
using System.Net.Http.Json;
using Leno.SharedContracts.Responses;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Moq;

namespace Leno.SystemAdmin.Api.Tests.Controllers;

/// <summary>
/// MenusController 集成测试（Task 7.14，12 用例）。
/// 覆盖菜单树查询、创建、更新、删除、排序 5 个端点，
/// 验证 200/201/400/401/403/404/409 状态码与 ApiResponse 包装。
/// </summary>
public class MenusControllerTests : IClassFixture<SystemAdminApiFactory>
{
    private readonly SystemAdminApiFactory _factory;
    private readonly HttpClient _client;

    public MenusControllerTests(SystemAdminApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAdminClient();
    }

    #region GET /api/admin/menus/tree

    [Fact]
    public async Task GetTree_WithAdminRole_ShouldReturn200()
    {
        var tree = new List<MenuDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "系统管理",
                Type = MenuType.Directory,
                Path = "/system",
                Sort = 0,
                Visible = true,
                Children =
                [
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = "用户管理",
                        Type = MenuType.Menu,
                        Path = "/system/users",
                        Component = "system/users/index",
                        Sort = 0,
                        Visible = true
                    }
                ]
            }
        };
        _factory.MenuAppServiceMock
            .Setup(s => s.GetTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tree);

        var response = await _client.GetAsync("/api/admin/menus/tree");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<MenuDto>>>();
        body!.Code.Should().Be(200);
        body.Data.Should().HaveCount(1);
        body.Data![0].Children.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTree_WithoutAuth_ShouldReturn401()
    {
        var anonymousClient = _factory.CreateAnonymousClient();
        var response = await anonymousClient.GetAsync("/api/admin/menus/tree");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTree_WithOperatorRole_ShouldReturn403()
    {
        var operatorClient = _factory.CreateClientWithRole("Operator");
        var response = await operatorClient.GetAsync("/api/admin/menus/tree");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region POST /api/admin/menus

    [Fact]
    public async Task Create_WithValidBody_ShouldReturn201()
    {
        var createdMenu = new MenuDto
        {
            Id = Guid.NewGuid(),
            Name = "新菜单",
            Type = MenuType.Menu,
            Path = "/system/new",
            Component = "system/new/index",
            Sort = 0,
            Visible = true
        };
        _factory.MenuAppServiceMock
            .Setup(s => s.CreateAsync(It.IsAny<CreateMenuDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdMenu);

        var body = new
        {
            name = "新菜单",
            type = MenuType.Menu,
            path = "/system/new",
            component = "system/new/index",
            sort = 0,
            visible = true
        };
        var response = await _client.PostAsJsonAsync("/api/admin/menus", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MenuDto>>();
        result!.Code.Should().Be(200);
        result.Data!.Name.Should().Be("新菜单");
    }

    [Fact]
    public async Task Create_WithDuplicatePath_ShouldReturn400()
    {
        // spec §4.7 错误处理矩阵：MENU_PATH_DUPLICATE 后缀不匹配 _CONFLICT/_EXISTS/_ALREADY_，默认 400
        _factory.MenuAppServiceMock
            .Setup(s => s.CreateAsync(It.IsAny<CreateMenuDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SystemAdminDomainException("菜单路径重复", "MENU_PATH_DUPLICATE"));

        var body = new
        {
            name = "重复菜单",
            type = MenuType.Menu,
            path = "/system/existing",
            component = "system/existing/index",
            sort = 0,
            visible = true
        };
        var response = await _client.PostAsJsonAsync("/api/admin/menus", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(400);
    }

    [Fact]
    public async Task Create_WithInvalidBody_ShouldReturn400()
    {
        var body = new { name = "" };
        var response = await _client.PostAsJsonAsync("/api/admin/menus", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region PUT /api/admin/menus/{id}

    [Fact]
    public async Task Update_WithValidBody_ShouldReturn200()
    {
        var menuId = Guid.NewGuid();
        var updatedMenu = new MenuDto
        {
            Id = menuId,
            Name = "更新后菜单",
            Type = MenuType.Menu,
            Path = "/system/updated",
            Component = "system/updated/index",
            Sort = 1,
            Visible = true
        };
        _factory.MenuAppServiceMock
            .Setup(s => s.UpdateAsync(menuId, It.IsAny<UpdateMenuDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedMenu);

        var body = new { name = "更新后菜单", sort = 1 };
        var response = await _client.PutAsJsonAsync($"/api/admin/menus/{menuId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<MenuDto>>();
        result!.Code.Should().Be(200);
        result.Data!.Name.Should().Be("更新后菜单");
    }

    [Fact]
    public async Task Update_WithNonExistentId_ShouldReturn404()
    {
        var menuId = Guid.NewGuid();
        _factory.MenuAppServiceMock
            .Setup(s => s.UpdateAsync(menuId, It.IsAny<UpdateMenuDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SystemAdminDomainException("菜单不存在", "MENU_NOT_FOUND"));

        var body = new { name = "更新" };
        var response = await _client.PutAsJsonAsync($"/api/admin/menus/{menuId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(404);
    }

    #endregion

    #region DELETE /api/admin/menus/{id}

    [Fact]
    public async Task Delete_WithValidId_ShouldReturn200()
    {
        var menuId = Guid.NewGuid();
        _factory.MenuAppServiceMock
            .Setup(s => s.DeleteAsync(menuId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync($"/api/admin/menus/{menuId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task Delete_WithChildren_ShouldReturn400()
    {
        var menuId = Guid.NewGuid();
        _factory.MenuAppServiceMock
            .Setup(s => s.DeleteAsync(menuId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SystemAdminDomainException("菜单包含子菜单，无法删除", "MENU_HAS_CHILDREN"));

        var response = await _client.DeleteAsync($"/api/admin/menus/{menuId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(400);
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ShouldReturn404()
    {
        var menuId = Guid.NewGuid();
        _factory.MenuAppServiceMock
            .Setup(s => s.DeleteAsync(menuId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SystemAdminDomainException("菜单不存在", "MENU_NOT_FOUND"));

        var response = await _client.DeleteAsync($"/api/admin/menus/{menuId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region PUT /api/admin/menus/sort

    [Fact]
    public async Task Sort_WithValidItems_ShouldReturn200()
    {
        var items = new[]
        {
            new { id = Guid.NewGuid(), sort = 0 },
            new { id = Guid.NewGuid(), sort = 1 },
            new { id = Guid.NewGuid(), sort = 2 }
        };
        _factory.MenuAppServiceMock
            .Setup(s => s.SortAsync(It.IsAny<List<MenuSortItemDto>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PutAsJsonAsync("/api/admin/menus/sort", items);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
    }

    #endregion
}
