using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Domain.Tests;

public class RoleTests
{
    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldCreateRole()
    {
        var role = Role.Create(Guid.NewGuid(), "TestRole", "Test Description");

        role.Name.Should().Be("TestRole");
        role.Description.Should().Be("Test Description");
        role.IsBuiltIn.Should().BeFalse();
        role.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void Create_BuiltInRole_ShouldSetIsBuiltIn()
    {
        var role = Role.Create(Guid.NewGuid(), "Admin", "Admin Role", isBuiltIn: true);

        role.IsBuiltIn.Should().BeTrue();
    }

    [Fact]
    public void Create_EmptyId_ShouldThrowException()
    {
        var act = () => Role.Create(Guid.Empty, "TestRole", null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_EmptyName_ShouldThrowException()
    {
        var act = () => Role.Create(Guid.NewGuid(), "", null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_NameTooShort_ShouldThrowException()
    {
        var act = () => Role.Create(Guid.NewGuid(), "A", null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_NameTooLong_ShouldThrowException()
    {
        var longName = new string('X', 65);
        var act = () => Role.Create(Guid.NewGuid(), longName, null);

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    #region Update

    [Fact]
    public void Update_ValidParameters_ShouldUpdateNameAndDescription()
    {
        var role = Role.Create(Guid.NewGuid(), "OldName", "Old Description");

        role.Update("NewName", "New Description");

        role.Name.Should().Be("NewName");
        role.Description.Should().Be("New Description");
    }

    [Fact]
    public void Update_EmptyName_ShouldThrowException()
    {
        var role = Role.Create(Guid.NewGuid(), "TestRole", null);

        var act = () => role.Update("", null);

        act.Should().Throw<UserAuthDomainException>();
    }

    #endregion

    #region SetPermissions

    [Fact]
    public void SetPermissions_ValidPermissions_ShouldReplaceAll()
    {
        var role = Role.Create(Guid.NewGuid(), "TestRole", null);
        var permissions = new List<PermissionVO>
        {
            new("api:/users"),
            new("api:/orders"),
            new("ui:dashboard:view")
        };

        role.SetPermissions(permissions);

        role.Permissions.Should().HaveCount(3);
        role.HasPermission("api:/users").Should().BeTrue();
        role.HasPermission("api:/orders").Should().BeTrue();
        role.HasPermission("ui:dashboard:view").Should().BeTrue();
    }

    [Fact]
    public void SetPermissions_EmptyList_ShouldClearAll()
    {
        var role = Role.Create(Guid.NewGuid(), "TestRole", null);
        role.SetPermissions(new List<PermissionVO> { new("api:/users") });

        role.SetPermissions(new List<PermissionVO>());

        role.Permissions.Should().BeEmpty();
    }

    #endregion

    #region AddPermission

    [Fact]
    public void AddPermission_NewPermission_ShouldAdd()
    {
        var role = Role.Create(Guid.NewGuid(), "TestRole", null);

        role.AddPermission(new PermissionVO("api:/users"));

        role.Permissions.Should().HaveCount(1);
        role.HasPermission("api:/users").Should().BeTrue();
    }

    [Fact]
    public void AddPermission_DuplicatePermission_ShouldIgnore()
    {
        var role = Role.Create(Guid.NewGuid(), "TestRole", null);
        role.AddPermission(new PermissionVO("api:/users"));

        role.AddPermission(new PermissionVO("api:/users"));

        role.Permissions.Should().HaveCount(1);
    }

    #endregion

    #region RemovePermission

    [Fact]
    public void RemovePermission_ExistingPermission_ShouldRemove()
    {
        var role = Role.Create(Guid.NewGuid(), "TestRole", null);
        role.AddPermission(new PermissionVO("api:/users"));

        role.RemovePermission("api:/users");

        role.Permissions.Should().BeEmpty();
    }

    [Fact]
    public void RemovePermission_NonExistingPermission_ShouldNotThrow()
    {
        var role = Role.Create(Guid.NewGuid(), "TestRole", null);

        var act = () => role.RemovePermission("api:/nonexistent");

        act.Should().NotThrow();
    }

    #endregion

    #region HasPermission

    [Fact]
    public void HasPermission_ExistingPermission_ShouldReturnTrue()
    {
        var role = Role.Create(Guid.NewGuid(), "TestRole", null);
        role.AddPermission(new PermissionVO("api:/users"));

        var result = role.HasPermission("api:/users");

        result.Should().BeTrue();
    }

    [Fact]
    public void HasPermission_NonExistingPermission_ShouldReturnFalse()
    {
        var role = Role.Create(Guid.NewGuid(), "TestRole", null);

        var result = role.HasPermission("api:/users");

        result.Should().BeFalse();
    }

    #endregion
}