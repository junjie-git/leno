using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.ValueObjects;

namespace Leno.UserAuth.Domain.Tests;

public class PermissionVOTests
{
    #region Constructor

    [Fact]
    public void Create_ValidApiPermission_ShouldCreate()
    {
        var permission = new PermissionVO("api:/users");

        permission.ResourceKey.Should().Be("api:/users");
        permission.IsApiPermission.Should().BeTrue();
        permission.IsUiPermission.Should().BeFalse();
    }

    [Fact]
    public void Create_ValidUiPermission_ShouldCreate()
    {
        var permission = new PermissionVO("ui:dashboard:view");

        permission.ResourceKey.Should().Be("ui:dashboard:view");
        permission.IsApiPermission.Should().BeFalse();
        permission.IsUiPermission.Should().BeTrue();
    }

    [Fact]
    public void Create_EmptyResourceKey_ShouldThrowException()
    {
        var act = () => new PermissionVO("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WhitespaceResourceKey_ShouldThrowException()
    {
        var act = () => new PermissionVO("   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NoColon_ShouldThrowException()
    {
        var act = () => new PermissionVO("invalidformat");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithDescription_ShouldSetDescription()
    {
        var permission = new PermissionVO("api:/users") { Description = "User management API" };

        permission.Description.Should().Be("User management API");
    }

    #endregion

    #region Equality

    [Fact]
    public void Equals_SameResourceKey_ShouldBeEqual()
    {
        var p1 = new PermissionVO("api:/users");
        var p2 = new PermissionVO("api:/users");

        p1.Should().Be(p2);
    }

    [Fact]
    public void Equals_DifferentResourceKey_ShouldNotBeEqual()
    {
        var p1 = new PermissionVO("api:/users");
        var p2 = new PermissionVO("api:/orders");

        p1.Should().NotBe(p2);
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ShouldReturnResourceKey()
    {
        var permission = new PermissionVO("api:/users");

        permission.ToString().Should().Be("api:/users");
    }

    #endregion
}