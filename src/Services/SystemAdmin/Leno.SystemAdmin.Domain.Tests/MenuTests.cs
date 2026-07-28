using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.Tests;

public class MenuTests
{
    private static readonly Guid ValidId = Guid.NewGuid();
    private const string ValidName = "用户管理";
    private const string ValidPath = "/user-access";
    private const string ValidComponent = "UserAccess/index";
    private const string ValidIcon = "TeamOutlined";

    [Fact]
    public void CreateRoot_WithValidParams_BuildsDirectoryNode()
    {
        var menu = Menu.CreateRoot(ValidId, ValidName, MenuType.Directory, ValidPath, ValidIcon);

        menu.Id.Should().Be(ValidId);
        menu.ParentId.Should().BeNull();
        menu.Name.Should().Be(ValidName);
        menu.Type.Should().Be(MenuType.Directory);
        menu.Path.Should().Be(ValidPath);
        menu.Sort.Should().Be(0);
        menu.Visible.Should().BeTrue();
        menu.Cache.Should().BeFalse();
        menu.Roles.Should().BeEmpty();
    }

    [Fact]
    public void CreateChild_WithParentId_BuildsMenuNode()
    {
        var parentId = Guid.NewGuid();
        var menu = Menu.CreateChild(ValidId, parentId, "用户列表", MenuType.Menu, "/user-access/list", "UserAccess/List/index");

        menu.ParentId.Should().Be(parentId);
        menu.Type.Should().Be(MenuType.Menu);
        menu.Component.Should().Be("UserAccess/List/index");
    }

    [Fact]
    public void CreateMenu_WithoutComponent_ThrowsDomainException()
    {
        var act = () => Menu.CreateRoot(ValidId, "用户列表", MenuType.Menu, "/user-list", component: null);

        act.Should().Throw<SystemAdminDomainException>()
            .WithErrorCode("MENU_COMPONENT_REQUIRED");
    }

    [Fact]
    public void CreateButton_WithPath_ThrowsDomainException()
    {
        var act = () => Menu.CreateRoot(ValidId, "删除按钮", MenuType.Button, path: "/delete");

        act.Should().Throw<SystemAdminDomainException>()
            .WithErrorCode("MENU_BUTTON_PATH_FORBIDDEN");
    }

    [Fact]
    public void CreateMenu_NameEmpty_ThrowsDomainException()
    {
        var act = () => Menu.CreateRoot(ValidId, "", MenuType.Directory, ValidPath);

        act.Should().Throw<SystemAdminDomainException>()
            .WithErrorCode("MENU_NAME_EMPTY");
    }

    [Fact]
    public void CreateMenu_NameTooLong_ThrowsDomainException()
    {
        var act = () => Menu.CreateRoot(ValidId, new string('a', 33), MenuType.Directory, ValidPath);

        act.Should().Throw<SystemAdminDomainException>()
            .WithErrorCode("MENU_NAME_LENGTH");
    }

    [Fact]
    public void CreateMenu_SortNegative_ThrowsDomainException()
    {
        var act = () => Menu.CreateRoot(ValidId, ValidName, MenuType.Directory, ValidPath, sort: -1);

        act.Should().Throw<SystemAdminDomainException>()
            .WithErrorCode("MENU_SORT_NEGATIVE");
    }

    [Fact]
    public void Rename_ChangesName_AndBumpsUpdatedAt()
    {
        var menu = Menu.CreateRoot(ValidId, ValidName, MenuType.Directory, ValidPath);
        var originalUpdatedAt = menu.UpdatedAt;

        menu.Rename("新菜单名");

        menu.Name.Should().Be("新菜单名");
        menu.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }

    [Fact]
    public void ChangeSort_UpdatesSortField()
    {
        var menu = Menu.CreateRoot(ValidId, ValidName, MenuType.Directory, ValidPath);

        menu.ChangeSort(5);

        menu.Sort.Should().Be(5);
    }

    [Fact]
    public void MoveTo_NewParentId_UpdatesParentId()
    {
        var menu = Menu.CreateRoot(ValidId, ValidName, MenuType.Directory, ValidPath);
        var newParent = Guid.NewGuid();

        menu.MoveTo(newParent);

        menu.ParentId.Should().Be(newParent);
    }

    [Fact]
    public void ToggleVisible_FlipsVisibleField()
    {
        var menu = Menu.CreateRoot(ValidId, ValidName, MenuType.Directory, ValidPath);
        var original = menu.Visible;

        menu.ToggleVisible();

        menu.Visible.Should().Be(!original);
    }

    [Fact]
    public void AssignRoles_SetsRolesList()
    {
        var menu = Menu.CreateRoot(ValidId, ValidName, MenuType.Directory, ValidPath);

        menu.AssignRoles(new List<string> { "Admin", "Operator" });

        menu.Roles.Should().Equal(new List<string> { "Admin", "Operator" });
    }

    [Fact]
    public void ToggleCache_FlipsCacheField()
    {
        var menu = Menu.CreateRoot(ValidId, ValidName, MenuType.Directory, ValidPath);
        var original = menu.Cache;

        menu.ToggleCache();

        menu.Cache.Should().Be(!original);
    }
}

// 临时扩展：FluentAssertions 对 SystemAdminDomainException.ErrorCode 的断言
internal static class DomainExceptionAssertionExtensions
{
    public static FluentAssertions.Specialized.ExceptionAssertions<SystemAdminDomainException> WithErrorCode(
        this FluentAssertions.Specialized.ExceptionAssertions<SystemAdminDomainException> assertions,
        string errorCode)
    {
        assertions.Which.ErrorCode.Should().Be(errorCode);
        return assertions;
    }
}
