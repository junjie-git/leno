using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Tests;

public class OperatorTests
{
    private static readonly Guid ValidOperatorId = Guid.NewGuid();
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private const string ValidDisplayName = "John Doe";
    private static readonly List<string> ValidPermissions = new() { "user.read", "user.write" };

    #region Create - Happy Path

    [Fact]
    public void Create_WithValidParameters_ShouldSetAllProperties()
    {
        var op = Operator.Create(ValidOperatorId, ValidUserId, ValidDisplayName, OperatorRole.Admin, ValidPermissions);

        op.OperatorId.Should().Be(ValidOperatorId);
        op.Id.Should().Be(ValidOperatorId);
        op.UserId.Should().Be(ValidUserId);
        op.DisplayName.Should().Be(ValidDisplayName);
        op.Role.Should().Be(OperatorRole.Admin);
        op.Permissions.Should().BeEquivalentTo(ValidPermissions);
        op.Status.Should().Be(OperatorStatus.Active);
        op.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllRoles_ShouldSucceed()
    {
        foreach (OperatorRole role in Enum.GetValues<OperatorRole>())
        {
            var op = Operator.Create(Guid.NewGuid(), Guid.NewGuid(), ValidDisplayName, role, ValidPermissions);
            op.Role.Should().Be(role);
        }
    }

    [Fact]
    public void Create_ShouldTrimDisplayName()
    {
        var op = Operator.Create(ValidOperatorId, ValidUserId, "  John Doe  ", OperatorRole.Admin, ValidPermissions);

        op.DisplayName.Should().Be("John Doe");
    }

    [Fact]
    public void Create_ShouldDeduplicatePermissions()
    {
        var permissions = new List<string> { "user.read", "user.read", "user.write" };

        var op = Operator.Create(ValidOperatorId, ValidUserId, ValidDisplayName, OperatorRole.Admin, permissions);

        op.Permissions.Should().HaveCount(2);
        op.Permissions.Should().Contain("user.read");
        op.Permissions.Should().Contain("user.write");
    }

    [Fact]
    public void Create_ShouldDeduplicatePermissionsCaseInsensitive()
    {
        var permissions = new List<string> { "User.Read", "user.read" };

        var op = Operator.Create(ValidOperatorId, ValidUserId, ValidDisplayName, OperatorRole.Admin, permissions);

        op.Permissions.Should().HaveCount(1);
    }

    [Fact]
    public void Create_ShouldIgnoreWhitespacePermissions()
    {
        var permissions = new List<string> { "user.read", "   ", "user.write" };

        var op = Operator.Create(ValidOperatorId, ValidUserId, ValidDisplayName, OperatorRole.Admin, permissions);

        op.Permissions.Should().HaveCount(2);
        op.Permissions.Should().NotContain(p => string.IsNullOrWhiteSpace(p));
    }

    [Fact]
    public void Create_WithEmptyPermissionsList_ShouldSucceed()
    {
        var op = Operator.Create(ValidOperatorId, ValidUserId, ValidDisplayName, OperatorRole.Admin, new List<string>());

        op.Permissions.Should().BeEmpty();
    }

    #endregion

    #region Create - Validation

    [Fact]
    public void Create_WithEmptyOperatorId_ShouldThrowOperatorIdEmpty()
    {
        var act = () => Operator.Create(Guid.Empty, ValidUserId, ValidDisplayName, OperatorRole.Admin, ValidPermissions);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OPERATOR_ID_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowOperatorUserIdEmpty()
    {
        var act = () => Operator.Create(ValidOperatorId, Guid.Empty, ValidDisplayName, OperatorRole.Admin, ValidPermissions);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OPERATOR_USER_ID_EMPTY");
    }

    [Fact]
    public void Create_WithNullDisplayName_ShouldThrowOperatorDisplayNameEmpty()
    {
        var act = () => Operator.Create(ValidOperatorId, ValidUserId, null!, OperatorRole.Admin, ValidPermissions);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OPERATOR_DISPLAY_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithEmptyDisplayName_ShouldThrowOperatorDisplayNameEmpty()
    {
        var act = () => Operator.Create(ValidOperatorId, ValidUserId, "", OperatorRole.Admin, ValidPermissions);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OPERATOR_DISPLAY_NAME_EMPTY");
    }

    [Fact]
    public void Create_WithDisplayNameTooLong_ShouldThrowOperatorDisplayNameLength()
    {
        var longName = new string('n', 101);

        var act = () => Operator.Create(ValidOperatorId, ValidUserId, longName, OperatorRole.Admin, ValidPermissions);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OPERATOR_DISPLAY_NAME_LENGTH");
    }

    [Fact]
    public void Create_WithDisplayNameAtMaxLength_ShouldSucceed()
    {
        var name = new string('n', 100);

        var op = Operator.Create(ValidOperatorId, ValidUserId, name, OperatorRole.Admin, ValidPermissions);

        op.DisplayName.Should().Be(name);
    }

    [Fact]
    public void Create_WithInvalidRole_ShouldThrowOperatorRoleInvalid()
    {
        var invalidRole = (OperatorRole)999;

        var act = () => Operator.Create(ValidOperatorId, ValidUserId, ValidDisplayName, invalidRole, ValidPermissions);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OPERATOR_ROLE_INVALID");
    }

    [Fact]
    public void Create_WithNullPermissions_ShouldThrowArgumentNullException()
    {
        var act = () => Operator.Create(ValidOperatorId, ValidUserId, ValidDisplayName, OperatorRole.Admin, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region AssignPermissions

    [Fact]
    public void AssignPermissions_ShouldAddNewPermissions()
    {
        var op = CreateOperator();

        op.AssignPermissions(new List<string> { "user.delete", "user.export" });

        op.Permissions.Should().Contain("user.delete");
        op.Permissions.Should().Contain("user.export");
        op.Permissions.Should().HaveCount(4); // 2 original + 2 new
    }

    [Fact]
    public void AssignPermissions_ShouldNotDuplicateExisting()
    {
        var op = CreateOperator();

        op.AssignPermissions(new List<string> { "user.read", "user.write" });

        op.Permissions.Should().HaveCount(2);
    }

    [Fact]
    public void AssignPermissions_ShouldDeduplicateCaseInsensitive()
    {
        var op = CreateOperator();

        op.AssignPermissions(new List<string> { "User.Read" });

        op.Permissions.Should().HaveCount(2);
    }

    [Fact]
    public void AssignPermissions_ShouldIgnoreWhitespaceItems()
    {
        var op = CreateOperator();

        op.AssignPermissions(new List<string> { "   ", "user.delete" });

        op.Permissions.Should().Contain("user.delete");
        op.Permissions.Should().HaveCount(3);
    }

    [Fact]
    public void AssignPermissions_WithNullList_ShouldThrowArgumentNullException()
    {
        var op = CreateOperator();

        var act = () => op.AssignPermissions(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region RevokePermissions

    [Fact]
    public void RevokePermissions_ShouldRemoveMatchingPermissions()
    {
        var op = CreateOperator();

        op.RevokePermissions(new List<string> { "user.read" });

        op.Permissions.Should().NotContain("user.read");
        op.Permissions.Should().Contain("user.write");
        op.Permissions.Should().HaveCount(1);
    }

    [Fact]
    public void RevokePermissions_ShouldIgnoreNonExisting()
    {
        var op = CreateOperator();

        op.RevokePermissions(new List<string> { "user.delete" });

        op.Permissions.Should().HaveCount(2);
    }

    [Fact]
    public void RevokePermissions_ShouldMatchCaseInsensitive()
    {
        var op = CreateOperator();

        op.RevokePermissions(new List<string> { "User.Read" });

        op.Permissions.Should().NotContain("user.read");
        op.Permissions.Should().HaveCount(1);
    }

    [Fact]
    public void RevokePermissions_ShouldIgnoreWhitespaceItems()
    {
        var op = CreateOperator();

        op.RevokePermissions(new List<string> { "   ", "user.read" });

        op.Permissions.Should().NotContain("user.read");
        op.Permissions.Should().HaveCount(1);
    }

    [Fact]
    public void RevokePermissions_WithNullList_ShouldThrowArgumentNullException()
    {
        var op = CreateOperator();

        var act = () => op.RevokePermissions(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Activate / Deactivate

    [Fact]
    public void Activate_ShouldSetStatusToActive()
    {
        var op = CreateOperator();
        op.Deactivate();

        op.Activate();

        op.Status.Should().Be(OperatorStatus.Active);
    }

    [Fact]
    public void Deactivate_ShouldSetStatusToInactive()
    {
        var op = CreateOperator();

        op.Deactivate();

        op.Status.Should().Be(OperatorStatus.Inactive);
    }

    #endregion

    #region RecordLogin

    [Fact]
    public void RecordLogin_WithValidLoginAt_ShouldSetLastLoginAt()
    {
        var op = CreateOperator();
        var loginAt = DateTime.UtcNow;

        op.RecordLogin(loginAt);

        op.LastLoginAt.Should().Be(loginAt);
    }

    [Fact]
    public void RecordLogin_WithDefaultDateTime_ShouldThrowOperatorLoginAtEmpty()
    {
        var op = CreateOperator();

        var act = () => op.RecordLogin(default);

        act.Should().Throw<SystemAdminDomainException>()
            .Which.ErrorCode.Should().Be("OPERATOR_LOGIN_AT_EMPTY");
    }

    [Fact]
    public void RecordLogin_ShouldUpdateLastLoginAt()
    {
        var op = CreateOperator();
        op.RecordLogin(DateTime.UtcNow.AddDays(-1));

        var newLoginAt = DateTime.UtcNow;
        op.RecordLogin(newLoginAt);

        op.LastLoginAt.Should().Be(newLoginAt);
    }

    #endregion

    private static Operator CreateOperator()
    {
        return Operator.Create(ValidOperatorId, ValidUserId, ValidDisplayName, OperatorRole.Admin, ValidPermissions);
    }
}