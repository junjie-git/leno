using Leno.Infrastructure.Auth;
using Moq;
using Xunit;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.Auth;

/// <summary>
/// ResourceOwnershipChecker 单元测试，验证 IDOR 越权防护逻辑。
/// 覆盖：资源所有者一致不抛异常、他人资源抛 ForbiddenAccessException、未认证抛 UnauthorizedAccessException。
/// </summary>
public class ResourceOwnershipCheckerTests
{
    [Fact]
    public async Task EnsureOwnerAsync_ResourceOwnedByCurrentUser_ShouldNotThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns(userId);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);

        var checker = new ResourceOwnershipChecker(userContext.Object);

        // Act & Assert — 资源所有者为当前用户，不应抛异常
        await FluentActions.Awaiting(() => checker.EnsureOwnerAsync(userId, "ORDER"))
            .Should().NotThrowAsync("资源所有者与当前用户一致时不应抛异常");
    }

    [Fact]
    public async Task EnsureOwnerAsync_ResourceOwnedByOther_ShouldThrowForbiddenException()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var resourceOwnerId = Guid.NewGuid();
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns(currentUserId);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);

        var checker = new ResourceOwnershipChecker(userContext.Object);

        // Act & Assert — 资源所有者为他人，应抛 ForbiddenAccessException
        var act = () => checker.EnsureOwnerAsync(resourceOwnerId, "ORDER");
        var ex = await act.Should().ThrowAsync<ForbiddenAccessException>();
        ex.Which.Message.Should().Contain("ORDER");
        ex.Which.Message.Should().NotContain(resourceOwnerId.ToString(),
            "错误消息不应暴露资源所有者的 UserId");
    }

    [Fact]
    public async Task EnsureOwnerAsync_UnauthenticatedUser_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns((Guid?)null);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(false);

        var checker = new ResourceOwnershipChecker(userContext.Object);

        // Act & Assert — 未认证用户应抛 UnauthorizedAccessException
        var act = () => checker.EnsureOwnerAsync(Guid.NewGuid(), "ORDER");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
