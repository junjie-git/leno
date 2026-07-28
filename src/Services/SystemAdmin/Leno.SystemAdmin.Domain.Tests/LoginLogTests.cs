using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.Tests;

public class LoginLogTests
{
    private static readonly Guid ValidLogId = Guid.NewGuid();
    private static readonly Guid ValidUserId = Guid.NewGuid();
    private const string ValidUsername = "admin";
    private const string ValidIp = "10.0.0.1";
    private const string ValidBrowser = "Chrome 120";
    private const string ValidOs = "Windows 11";
    private const string ValidUa = "Mozilla/5.0";
    private const string ValidTraceId = "trace-abc-123";
    private static readonly DateTime ValidLoginAt = DateTime.UtcNow;

    [Fact]
    public void CreateSuccess_WithValidParams_BuildsSuccessLog()
    {
        var log = LoginLog.CreateSuccess(
            ValidLogId, ValidUsername, ValidUserId, ValidIp, ValidBrowser, ValidOs,
            ValidUa, ValidTraceId, 150, ValidLoginAt);

        log.Id.Should().Be(ValidLogId);
        log.Result.Should().Be(LoginResult.Success);
        log.UserId.Should().Be(ValidUserId);
        log.FailureReason.Should().BeNull();
        log.DurationMs.Should().Be(150);
    }

    [Fact]
    public void CreateFailed_WithReason_BuildsFailedLog()
    {
        var log = LoginLog.CreateFailed(
            ValidLogId, ValidUsername, ValidIp, ValidBrowser, ValidOs,
            ValidUa, ValidTraceId, 80, "密码错误", ValidLoginAt);

        log.Result.Should().Be(LoginResult.Failed);
        log.UserId.Should().BeNull();
        log.FailureReason.Should().Be("密码错误");
    }

    [Fact]
    public void CreateSuccess_WithFailureReason_ThrowsDomainException()
    {
        var act = () => LoginLog.CreateSuccess(
            ValidLogId, ValidUsername, ValidUserId, ValidIp, ValidBrowser, ValidOs,
            ValidUa, ValidTraceId, 150, ValidLoginAt, failureReason: "不应填");

        act.Should().Throw<SystemAdminDomainException>()
            .WithErrorCode("LOGIN_SUCCESS_WITH_REASON");
    }

    [Fact]
    public void CreateFailed_WithoutFailureReason_ThrowsDomainException()
    {
        var act = () => LoginLog.CreateFailed(
            ValidLogId, ValidUsername, ValidIp, ValidBrowser, ValidOs,
            ValidUa, ValidTraceId, 80, failureReason: "", ValidLoginAt);

        act.Should().Throw<SystemAdminDomainException>()
            .WithErrorCode("LOGIN_FAILED_REASON_REQUIRED");
    }

    [Fact]
    public void CreateSuccess_UsernameEmpty_ThrowsDomainException()
    {
        var act = () => LoginLog.CreateSuccess(
            ValidLogId, "", ValidUserId, ValidIp, ValidBrowser, ValidOs,
            ValidUa, ValidTraceId, 150, ValidLoginAt);

        act.Should().Throw<SystemAdminDomainException>()
            .WithErrorCode("LOGIN_USERNAME_EMPTY");
    }

    [Fact]
    public void CreateSuccess_DurationNegative_ThrowsDomainException()
    {
        var act = () => LoginLog.CreateSuccess(
            ValidLogId, ValidUsername, ValidUserId, ValidIp, ValidBrowser, ValidOs,
            ValidUa, ValidTraceId, -1, ValidLoginAt);

        act.Should().Throw<SystemAdminDomainException>()
            .WithErrorCode("LOGIN_DURATION_NEGATIVE");
    }
}
