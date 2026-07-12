using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;

namespace Leno.UserAuth.Domain.Tests;

public class AuditLogTests
{
    #region Create

    [Fact]
    public void Create_ValidParameters_ShouldCreateAuditLog()
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "User",
            "user-123",
            "{\"status\":\"Active\"}",
            "{\"status\":\"Disabled\"}",
            "192.168.1.1",
            "Mozilla/5.0",
            "trace-456");

        auditLog.Action.Should().Be("UserBan");
        auditLog.ResourceType.Should().Be("User");
        auditLog.ResourceId.Should().Be("user-123");
        auditLog.BeforeSnapshot.Should().Be("{\"status\":\"Active\"}");
        auditLog.AfterSnapshot.Should().Be("{\"status\":\"Disabled\"}");
        auditLog.Ip.Should().Be("192.168.1.1");
        auditLog.UserAgent.Should().Be("Mozilla/5.0");
        auditLog.TraceId.Should().Be("trace-456");
        auditLog.OperatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_EmptyId_ShouldThrowException()
    {
        var act = () => AuditLog.Create(
            Guid.Empty,
            Guid.NewGuid(),
            "UserBan",
            "User",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_EmptyOperatorId_ShouldThrowException()
    {
        var act = () => AuditLog.Create(
            Guid.NewGuid(),
            Guid.Empty,
            "UserBan",
            "User",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_EmptyAction_ShouldThrowException()
    {
        var act = () => AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "",
            "User",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_WhitespaceAction_ShouldThrowException()
    {
        var act = () => AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "   ",
            "User",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_EmptyResourceType_ShouldThrowException()
    {
        var act = () => AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_WhitespaceResourceType_ShouldThrowException()
    {
        var act = () => AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "   ",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        act.Should().Throw<UserAuthDomainException>();
    }

    [Fact]
    public void Create_NullResourceId_ShouldSetNull()
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "User",
            null,
            null,
            null,
            null,
            null,
            null);

        auditLog.ResourceId.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyResourceId_ShouldSetNull()
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "User",
            "",
            null,
            null,
            null,
            null,
            null);

        auditLog.ResourceId.Should().BeNull();
    }

    [Fact]
    public void Create_WhitespaceResourceId_ShouldSetNull()
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "User",
            "   ",
            null,
            null,
            null,
            null,
            null);

        auditLog.ResourceId.Should().BeNull();
    }

    [Fact]
    public void Create_NullSnapshots_ShouldSetNull()
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "User",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        auditLog.BeforeSnapshot.Should().BeNull();
        auditLog.AfterSnapshot.Should().BeNull();
    }

    [Fact]
    public void Create_NullIp_ShouldSetNull()
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "User",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        auditLog.Ip.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyIp_ShouldSetNull()
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "User",
            "user-123",
            null,
            null,
            "",
            null,
            null);

        auditLog.Ip.Should().BeNull();
    }

    [Fact]
    public void Create_NullUserAgent_ShouldSetNull()
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "User",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        auditLog.UserAgent.Should().BeNull();
    }

    [Fact]
    public void Create_NullTraceId_ShouldSetNull()
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "User",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        auditLog.TraceId.Should().BeNull();
    }

    [Fact]
    public void Create_TrimmedAction_ShouldTrimWhitespace()
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  UserBan  ",
            "User",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        auditLog.Action.Should().Be("UserBan");
    }

    [Fact]
    public void Create_TrimmedResourceType_ShouldTrimWhitespace()
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UserBan",
            "  User  ",
            "user-123",
            null,
            null,
            null,
            null,
            null);

        auditLog.ResourceType.Should().Be("User");
    }

    #endregion

    #region Immutability

    [Fact]
    public void AuditLog_ShouldBeImmutable_NoPublicSetters()
    {
        // AuditLog only has private setters, so it's immutable after creation.
        // We verify that all properties are read-only from outside.
        var type = typeof(AuditLog);
        var properties = type.GetProperties();

        foreach (var prop in properties)
        {
            if (prop.Name is "Id" or "DomainEvents" or "CreatedAt" or "UpdatedAt"
                or "CreatedBy" or "UpdatedBy" or "Version")
            {
                continue; // base class properties
            }

            var setMethod = prop.GetSetMethod(nonPublic: false);
            setMethod.Should().BeNull(
                $"Property {prop.Name} should not have a public setter");
        }
    }

    [Fact]
    public void AuditLog_ShouldHaveNoUpdateOrDeleteMethods()
    {
        // AuditLog should not have any Update or Delete methods
        var type = typeof(AuditLog);
        var methods = type.GetMethods()
            .Where(m => m.DeclaringType == typeof(AuditLog))
            .Select(m => m.Name)
            .ToList();

        methods.Should().NotContain(m => m.Contains("Update", StringComparison.OrdinalIgnoreCase));
        methods.Should().NotContain(m => m.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        methods.Should().NotContain(m => m.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}