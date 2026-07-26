using System.Net;
using Leno.Identity.Application.DTOs;
using Leno.Identity.Application.Services;
using Leno.Identity.Application.Tests.OAuth;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Tests.Application;

/// <summary>
/// UserAdminAppService 单元测试（Task A2 补齐）。
/// 覆盖分页查询、详情查询、AssignRoles 跨域 HTTP 调用、Suspend/Resume 账户状态机等场景。
/// </summary>
public class UserAdminAppServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ILogger<UserAdminAppService>> _loggerMock = new();
    private readonly FakeHttpMessageHandler _httpHandler = new();
    private readonly UserAdminAppService _sut;

    public UserAdminAppServiceTests()
    {
        var httpClient = new HttpClient(_httpHandler)
        {
            BaseAddress = new Uri("http://localhost:8082/")
        };
        _sut = new UserAdminAppService(
            _userRepoMock.Object,
            _refreshTokenRepoMock.Object,
            httpClient,
            _uowMock.Object,
            _loggerMock.Object);
    }

    #region QueryUsersAsync

    [Fact]
    public async Task QueryUsersAsync_Should_Return_Paged_Result_And_Normalize_Paging()
    {
        // Arrange：page=0 / pageSize=200 应被归一化为 1 / 100
        var user1 = CreateUser(username: "alice");
        var user2 = CreateUser(username: "bob");
        var items = new List<User> { user1, user2 };
        _userRepoMock
            .Setup(r => r.QueryAsync("ali", null, 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 2));

        var query = new AdminUserQueryDto { Keyword = "ali" };

        // Act
        var result = await _sut.QueryUsersAsync(query, page: 0, pageSize: 200);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(100);
        result.Items.First().Username.Should().Be("alice");
        result.Items.First().Roles.Should().BeEmpty("Identity BC 不持久化角色");
        _userRepoMock.Verify(r => r.QueryAsync("ali", null, 1, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryUsersAsync_With_Null_Query_Should_Throw_ArgumentNullException()
    {
        var act = async () => await _sut.QueryUsersAsync(null!, page: 1, pageSize: 20);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task QueryUsersAsync_With_Empty_Result_Should_Return_Empty_Page()
    {
        _userRepoMock
            .Setup(r => r.QueryAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<User>(), 0));

        var result = await _sut.QueryUsersAsync(new AdminUserQueryDto(), page: 1, pageSize: 20);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    #endregion

    #region GetUserAsync

    [Fact]
    public async Task GetUserAsync_With_Existing_User_Should_Return_Dto()
    {
        var user = CreateUser(username: "charlie");
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _sut.GetUserAsync(user.Id);

        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.Username.Should().Be("charlie");
        result.Status.Should().Be(AccountStatus.Active);
        result.FailedLoginCount.Should().Be(0);
    }

    [Fact]
    public async Task GetUserAsync_With_Missing_User_Should_Throw_DomainException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = async () => await _sut.GetUserAsync(Guid.NewGuid());

        var ex = await act.Should().ThrowAsync<IdentityDomainException>();
        ex.Which.Message.Should().Contain("用户不存在");
    }

    [Fact]
    public async Task GetUserAsync_With_Empty_UserId_Should_Throw_ArgumentException()
    {
        var act = async () => await _sut.GetUserAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region AssignRolesAsync

    [Fact]
    public async Task AssignRolesAsync_With_Success_Response_Should_Revoke_RefreshTokens()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _httpHandler.Register("roles", HttpStatusCode.OK, "{}");

        var roleIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        await _sut.AssignRolesAsync(user.Id, roleIds);

        _refreshTokenRepoMock.Verify(
            r => r.RevokeAllByUserAsync(user.Id, "RoleAssign", It.IsAny<CancellationToken>()),
            Times.Once,
            "角色变更后应撤销该用户所有 RefreshToken");
        _httpHandler.Requests.Should().HaveCount(1);
        var request = _httpHandler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.Should().Contain($"api/admin/users/{user.Id:D}/roles");
    }

    [Fact]
    public async Task AssignRolesAsync_With_Empty_RoleIds_Should_Throw_DomainException()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var act = async () => await _sut.AssignRolesAsync(user.Id, new List<Guid>());

        var ex = await act.Should().ThrowAsync<IdentityDomainException>();
        ex.Which.Message.Should().Contain("待分配角色列表不可为空");
    }

    [Fact]
    public async Task AssignRolesAsync_With_Missing_User_Should_Throw_DomainException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = async () => await _sut.AssignRolesAsync(Guid.NewGuid(), new List<Guid> { Guid.NewGuid() });

        var ex = await act.Should().ThrowAsync<IdentityDomainException>();
        ex.Which.Message.Should().Contain("用户不存在");
    }

    [Fact]
    public async Task AssignRolesAsync_With_NonSuccess_Status_Should_Throw_DomainException()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _httpHandler.Register("roles", HttpStatusCode.BadRequest, "{\"error\":\"invalid role\"}");

        var roleIds = new List<Guid> { Guid.NewGuid() };

        var act = async () => await _sut.AssignRolesAsync(user.Id, roleIds);

        var ex = await act.Should().ThrowAsync<IdentityDomainException>();
        ex.Which.Message.Should().Contain("角色分配失败");
        _refreshTokenRepoMock.Verify(
            r => r.RevokeAllByUserAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "HTTP 调用失败时不应撤销 RefreshToken");
    }

    [Fact]
    public async Task AssignRolesAsync_With_HttpRequestException_Should_Wrap_As_DomainException()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        // 注册一个会抛 HttpRequestException 的 handler
        var throwingHandler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"));
        var httpClient = new HttpClient(throwingHandler)
        {
            BaseAddress = new Uri("http://localhost:8082/")
        };
        var sut = new UserAdminAppService(
            _userRepoMock.Object,
            _refreshTokenRepoMock.Object,
            httpClient,
            _uowMock.Object,
            _loggerMock.Object);

        var act = async () => await sut.AssignRolesAsync(user.Id, new List<Guid> { Guid.NewGuid() });

        var ex = await act.Should().ThrowAsync<IdentityDomainException>();
        ex.Which.Message.Should().Contain("角色分配服务暂时不可用");
        ex.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task AssignRolesAsync_With_Empty_UserId_Should_Throw_ArgumentException()
    {
        var act = async () => await _sut.AssignRolesAsync(Guid.Empty, new List<Guid> { Guid.NewGuid() });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region SuspendAsync

    [Fact]
    public async Task SuspendAsync_With_Active_User_Should_Lock_And_Revoke_Tokens()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new SuspendUserDto { Reason = "违规操作", DurationMinutes = 60 };

        await _sut.SuspendAsync(user.Id, request);

        user.Status.Should().Be(AccountStatus.Locked);
        user.LockedUntil.Should().NotBeNull();
        _userRepoMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokenRepoMock.Verify(
            r => r.RevokeAllByUserAsync(user.Id, "AdminSuspend", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuspendAsync_With_Disabled_User_Should_Throw_DomainException()
    {
        var user = CreateUser();
        user.Disable("历史违规");
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new SuspendUserDto { Reason = "再次违规", DurationMinutes = 30 };

        var act = async () => await _sut.SuspendAsync(user.Id, request);

        var ex = await act.Should().ThrowAsync<IdentityDomainException>();
        ex.Which.Message.Should().Contain("已禁用的账户不可锁定");
    }

    [Fact]
    public async Task SuspendAsync_With_Empty_Reason_Should_Throw_DomainException()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new SuspendUserDto { Reason = "", DurationMinutes = 30 };

        var act = async () => await _sut.SuspendAsync(user.Id, request);

        var ex = await act.Should().ThrowAsync<IdentityDomainException>();
        ex.Which.Message.Should().Contain("锁定原因不可为空");
    }

    [Fact]
    public async Task SuspendAsync_With_Invalid_Duration_Should_Throw_DomainException()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new SuspendUserDto { Reason = "违规", DurationMinutes = 0 };

        var act = async () => await _sut.SuspendAsync(user.Id, request);

        var ex = await act.Should().ThrowAsync<IdentityDomainException>();
        ex.Which.Message.Should().Contain("锁定时长须为 1-1440 分钟");
    }

    [Fact]
    public async Task SuspendAsync_With_Duration_Over_1440_Should_Throw_DomainException()
    {
        var user = CreateUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new SuspendUserDto { Reason = "违规", DurationMinutes = 2000 };

        var act = async () => await _sut.SuspendAsync(user.Id, request);

        await act.Should().ThrowAsync<IdentityDomainException>();
    }

    #endregion

    #region ResumeAsync

    [Fact]
    public async Task ResumeAsync_With_Locked_User_Should_Unlock()
    {
        var user = CreateUser();
        user.Lock("测试", TimeSpan.FromMinutes(30));
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _sut.ResumeAsync(user.Id);

        user.Status.Should().Be(AccountStatus.Active);
        user.LockedUntil.Should().BeNull();
        _userRepoMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResumeAsync_With_Disabled_User_Should_Activate()
    {
        var user = CreateUser();
        user.Disable("历史违规");
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _sut.ResumeAsync(user.Id);

        user.Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public async Task ResumeAsync_With_Active_User_Should_Skip_Update()
    {
        var user = CreateUser(); // 默认 Active
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _sut.ResumeAsync(user.Id);

        user.Status.Should().Be(AccountStatus.Active);
        _userRepoMock.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResumeAsync_With_Missing_User_Should_Throw_DomainException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = async () => await _sut.ResumeAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<IdentityDomainException>()
            .WithMessage("*用户不存在*");
    }

    #endregion

    /// <summary>创建测试用户。</summary>
    private static User CreateUser(string? username = null)
    {
        var id = Guid.NewGuid();
        return User.Create(
            id: id,
            username: username ?? "testuser_" + id.ToString("N")[..8],
            email: "test_" + id.ToString("N")[..8] + "@example.com",
            phoneNumber: null,
            passwordHash: "hashed:Pass123!",
            nickname: "TestUser");
    }

    /// <summary>测试用 HttpMessageHandler，统一抛指定异常，用于验证 HTTP 调用失败的容错。</summary>
    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception)
        {
            _exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(_exception);
        }
    }
}
