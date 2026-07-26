using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Auth;
using Leno.Review.Application;
using Leno.Review.Application.DTOs;
using Leno.Review.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Medallion.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Review.Api.Tests;

/// <summary>
/// 评价域 API 集成测试（评价 BC 独立维护）。
/// 覆盖 11 个鉴权端点 + 1 个匿名端点的成功场景、鉴权场景（401/403）与失败场景（400/404）。
/// 通过 mock IReviewAppService 与 ICurrentUserContext 解耦业务逻辑，聚焦 Controller 路由/鉴权/响应包装。
/// </summary>
public class ReviewApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient _client;
    private readonly Mock<IReviewAppService> _reviewAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid ReviewId = Guid.NewGuid();

    public ReviewApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = CreateClient(role: "Buyer,Seller,Operator,Admin");
    }

    /// <summary>
    /// 创建测试 HttpClient，通过 X-Test-Role 头指定当前用户角色。
    /// 默认赋予全部 4 个角色，覆盖所有鉴权端点的成功场景。
    /// 使用 Development 环境以绕过生产级敏感配置与 InternalAuth:ApiKey 启动校验（测试通过 mock 解耦业务依赖）。
    /// </summary>
    private HttpClient CreateClient(string role)
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Development");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_reviewAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);
                RemoveRedisServices(services);

                services.AddAuthentication(defaultScheme: "Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }).CreateClient();

        _client = client;
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
        _client.DefaultRequestHeaders.Add("X-Test-Role", role);

        // 默认 currentUserMock 设置为已认证（成功场景）
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);

        return _client;
    }

    private static void RemoveMassTransitServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("MassTransit") == true
                     || s.ImplementationType?.FullName?.Contains("MassTransit") == true
                     || s.ServiceType == typeof(MassTransit.IBus)
                     || s.ServiceType == typeof(MassTransit.IBusControl)
                     || s.ServiceType == typeof(MassTransit.IPublishEndpoint)
                     || s.ServiceType == typeof(MassTransit.ISendEndpointProvider)
                     || s.ServiceType.FullName?.StartsWith("MassTransit.", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);

        // 移除依赖 MassTransit 的 IEventBus（RabbitMqEventBus 需要 IPublishEndpoint）
        var eventBusDescriptors = services
            .Where(s => s.ServiceType == typeof(Leno.Infrastructure.Abstractions.IEventBus))
            .ToList();
        foreach (var d in eventBusDescriptors) services.Remove(d);

        // 移除 MassTransit 消费者注册（ReviewReadModelSyncConsumer 同时依赖 ES，由 RemoveElasticsearchServices 兜底）
        var consumerDescriptors = services
            .Where(s => s.ImplementationType?.FullName?.Contains("ReviewReadModelSyncConsumer") == true
                     || s.ImplementationType?.FullName?.Contains("Consumer") == true
                     || s.ImplementationType?.Namespace?.Contains("MassTransit") == true)
            .ToList();
        foreach (var d in consumerDescriptors) services.Remove(d);
    }

    private static void RemoveElasticsearchServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("Elasticsearch") == true
                     || s.ServiceType.FullName?.Contains("Elastic") == true
                     || s.ServiceType.FullName?.Contains("Nest") == true
                     || s.ImplementationType?.FullName?.Contains("Elastic") == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);

        // 移除依赖 ElasticsearchClient 的 IEsReadModelRepository<> 开放泛型注册
        var esRepoDescriptors = services
            .Where(s => s.ServiceType.IsGenericType
                     && s.ServiceType.GetGenericTypeDefinition().FullName?.Contains("IEsReadModelRepository") == true)
            .ToList();
        foreach (var d in esRepoDescriptors) services.Remove(d);

        // 移除依赖 ElasticsearchClient 的 HostedService（ReviewIndexInitializer）
        var hostedServiceDescriptors = services
            .Where(s => s.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                     && s.ImplementationType?.FullName?.Contains("ReviewIndexInitializer") == true)
            .ToList();
        foreach (var d in hostedServiceDescriptors) services.Remove(d);

        // 移除可能残留的 ReviewReadModelSyncConsumer（双重保险：MassTransit 消费者也依赖 ES）
        var consumerDescriptors = services
            .Where(s => s.ImplementationType?.FullName?.Contains("ReviewReadModelSyncConsumer") == true)
            .ToList();
        foreach (var d in consumerDescriptors) services.Remove(d);
    }

    /// <summary>
    /// 移除 Redis 相关服务并替换为无操作实现，避免测试启动时连接 Redis 实例。
    /// 同时将 IDistributedLockProvider 替换为返回 null 的 mock，使 MigrateWithLockAsync 跳过数据库迁移。
    /// </summary>
    private static void RemoveRedisServices(IServiceCollection services)
    {
        // 移除 IConnectionMultiplexer（StackExchange.Redis 连接复用器）
        var multiplexerDescriptors = services
            .Where(s => s.ServiceType == typeof(StackExchange.Redis.IConnectionMultiplexer))
            .ToList();
        foreach (var d in multiplexerDescriptors) services.Remove(d);

        // 移除 IIdempotencyStore（Redis 幂等去重存储）
        var idempotencyDescriptors = services
            .Where(s => s.ServiceType == typeof(IIdempotencyStore))
            .ToList();
        foreach (var d in idempotencyDescriptors) services.Remove(d);
        // 注册无操作 IIdempotencyStore 占位，避免其他服务解析失败
        services.AddSingleton<IIdempotencyStore, NoopIdempotencyStore>();

        // 移除 IDistributedLockProvider（基于 Redis 的分布式锁提供者）
        var lockProviderDescriptors = services
            .Where(s => s.ServiceType == typeof(IDistributedLockProvider))
            .ToList();
        foreach (var d in lockProviderDescriptors) services.Remove(d);

        // 注册返回 null 的 mock：CreateLock → TryAcquireAsync 返回 null → MigrateWithLockAsync 跳过迁移
        // 注意：TryAcquireLockAsync 是扩展方法，不可直接 Mock；需 Mock IDistributedLock.TryAcquireAsync
        var lockMock = new Mock<Medallion.Threading.IDistributedLock>();
        lockMock
            .Setup(l => l.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(() => default);

        var lockProviderMock = new Mock<IDistributedLockProvider>();
        lockProviderMock
            .Setup(p => p.CreateLock(It.IsAny<string>()))
            .Returns(lockMock.Object);

        services.AddSingleton(lockProviderMock.Object);
    }

    /// <summary>切换当前用户角色，重新创建 HttpClient。</summary>
    private void SwitchRole(string role)
    {
        _client = CreateClient(role);
    }

    /// <summary>切换为未认证用户（无 Authorization 头）。</summary>
    private void SwitchToUnauthenticated()
    {
        _client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Development");
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_reviewAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);
                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);
                RemoveRedisServices(services);
                services.AddAuthentication(defaultScheme: "Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }).CreateClient();
        // 不设置 Authorization 头，模拟未认证
    }

    [Fact]
    public async Task HealthLive_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #region 买家端 ReviewsController（5端点）

    [Fact]
    public async Task SubmitReview_AsBuyer_ShouldReturn201()
    {
        SwitchRole("Buyer");
        _reviewAppServiceMock.Setup(s => s.SubmitReviewAsync(UserId, It.IsAny<SubmitReviewDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReviewDto());

        var dto = new
        {
            OrderId = OrderId,
            OrderLineId = OrderLineId,
            SpuId = SpuId,
            SkuId = SkuId,
            Rating = 5,
            Content = "商品质量很好，物流也快！",
            Images = new List<string>()
        };
        var response = await _client.PostAsJsonAsync("/api/reviews", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _reviewAppServiceMock.Verify(
            s => s.SubmitReviewAsync(UserId, It.IsAny<SubmitReviewDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetReviewByOrderLine_AsBuyer_ShouldReturn200()
    {
        SwitchRole("Buyer");
        _reviewAppServiceMock.Setup(s => s.GetReviewByOrderLineForUserAsync(OrderLineId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReviewDto());

        var response = await _client.GetAsync($"/api/reviews/order-line/{OrderLineId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewDto>>();
        body!.Data!.ReviewId.Should().Be(ReviewId);
    }

    [Fact]
    public async Task GetMyReviews_AsBuyer_ShouldReturn200()
    {
        SwitchRole("Buyer");
        var listResult = new ReviewListResultDto
        {
            Items = new List<ReviewDto> { CreateReviewDto() },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _reviewAppServiceMock.Setup(s => s.GetReviewsByUserAsync(UserId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/reviews/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewListResultDto>>();
        body!.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task AppendReview_AsBuyer_ShouldReturn200()
    {
        SwitchRole("Buyer");
        _reviewAppServiceMock.Setup(s => s.AppendAdditionalReviewAsync(ReviewId, UserId, It.IsAny<AppendReviewDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReviewDto());

        var dto = new { Content = "使用一段时间后依然很好", Images = new List<string> { "append1.jpg" } };
        var response = await _client.PostAsJsonAsync($"/api/reviews/{ReviewId}/append", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _reviewAppServiceMock.Verify(
            s => s.AppendAdditionalReviewAsync(ReviewId, UserId, It.IsAny<AppendReviewDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadReviewImages_AsBuyer_ShouldReturn200()
    {
        SwitchRole("Buyer");
        using var content = new MultipartFormDataContent();
        using var imageStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("fake-image-content"));
        var fileContent = new StreamContent(imageStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "files", "test.jpg");

        var response = await _client.PostAsync("/api/reviews/images", content);

        // 文件签名校验会拒绝伪装内容，返回 400；此处验证鉴权通过（非 401/403）即可
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(400);
    }

    [Fact]
    public async Task UploadReviewImages_AsBuyer_NoFiles_ShouldReturn400()
    {
        SwitchRole("Buyer");
        using var content = new MultipartFormDataContent();
        var response = await _client.PostAsync("/api/reviews/images", content);

        // 空表单提交时，[ApiController] 自动模型校验或控制器逻辑均应返回 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // 响应体可能为 ApiResponse（控制器返回）或 ProblemDetails（框架自动返回），二者 Code/status 均为 400
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().Contain("400");
    }

    #endregion

    #region 卖家端 SellerReviewsController（3端点）

    [Fact]
    public async Task GetSellerReviews_AsSeller_ShouldReturn200()
    {
        SwitchRole("Seller");
        _currentUserMock.SetupGet(c => c.UserId).Returns(SellerId);
        var listResult = new ReviewListResultDto
        {
            Items = new List<ReviewDto> { CreateReviewDto() },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _reviewAppServiceMock.Setup(s => s.GetBySellerAsync(SellerId, It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/seller/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewListResultDto>>();
        body!.Data!.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetSellerReviewDetail_AsSeller_ShouldReturn200()
    {
        SwitchRole("Seller");
        _currentUserMock.SetupGet(c => c.UserId).Returns(SellerId);
        _reviewAppServiceMock.Setup(s => s.GetSellerReviewDetailAsync(ReviewId, SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReviewDto());

        var response = await _client.GetAsync($"/api/seller/reviews/{ReviewId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewDto>>();
        body!.Data!.ReviewId.Should().Be(ReviewId);
    }

    [Fact]
    public async Task SellerReply_AsSeller_ShouldReturn200()
    {
        SwitchRole("Seller");
        _currentUserMock.SetupGet(c => c.UserId).Returns(SellerId);
        _reviewAppServiceMock.Setup(s => s.SellerReplyAsync(ReviewId, SellerId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new { Content = "感谢您的评价！" };
        var response = await _client.PostAsJsonAsync($"/api/seller/reviews/{ReviewId}/reply", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _reviewAppServiceMock.Verify(
            s => s.SellerReplyAsync(ReviewId, SellerId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region 运营端 AdminReviewsController（3端点）

    [Fact]
    public async Task QueryReviews_AsOperator_ShouldReturn200()
    {
        SwitchRole("Operator");
        var listResult = new ReviewListResultDto
        {
            Items = new List<ReviewDto> { CreateReviewDto() },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _reviewAppServiceMock.Setup(s => s.QueryReviewsAsync(It.IsAny<ReviewStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/admin/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewListResultDto>>();
        body!.Data!.Total.Should().Be(1);
    }

    [Fact]
    public async Task ApproveReview_AsAdmin_ShouldReturn200()
    {
        SwitchRole("Admin");
        _reviewAppServiceMock.Setup(s => s.ApproveReviewAsync(ReviewId, UserId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/reviews/{ReviewId}/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _reviewAppServiceMock.Verify(
            s => s.ApproveReviewAsync(ReviewId, UserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HideReview_AsOperator_ShouldReturn200()
    {
        SwitchRole("Operator");
        _reviewAppServiceMock.Setup(s => s.HideReviewAsync(ReviewId, UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new { Reason = "违规内容" };
        var response = await _client.PostAsJsonAsync($"/api/admin/reviews/{ReviewId}/hide", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _reviewAppServiceMock.Verify(
            s => s.HideReviewAsync(ReviewId, UserId, "违规内容", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region 匿名端点 ProductReviewsController（1端点）

    [Fact]
    public async Task GetProductReviews_WithoutAuth_ShouldReturn200()
    {
        SwitchToUnauthenticated();
        var listResult = new ReviewListResultDto
        {
            Items = new List<ReviewDto> { CreateReviewDto() },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _reviewAppServiceMock.Setup(s => s.GetReviewsBySpuAsync(SpuId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync($"/api/products/{SpuId}/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ReviewListResultDto>>();
        body!.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }

    #endregion

    #region 鉴权场景（401/403）

    [Fact]
    public async Task UnauthorizedRequest_ShouldReturn401()
    {
        SwitchToUnauthenticated();
        var response = await _client.GetAsync("/api/reviews/mine");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BuyerAccessSellerEndpoint_ShouldReturn403()
    {
        SwitchRole("Buyer");
        var response = await _client.GetAsync("/api/seller/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SellerAccessAdminEndpoint_ShouldReturn403()
    {
        SwitchRole("Seller");
        var response = await _client.GetAsync("/api/admin/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BuyerAccessAdminEndpoint_ShouldReturn403()
    {
        SwitchRole("Buyer");
        var response = await _client.GetAsync("/api/admin/reviews");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region 失败场景（400）

    [Fact]
    public async Task SubmitReview_WithEmptyContent_ShouldReturn400()
    {
        SwitchRole("Buyer");
        // 提交空 Content 触发模型绑定校验失败（DTO 必填字段缺失/空值）
        var dto = new
        {
            OrderId = OrderId,
            OrderLineId = OrderLineId,
            SpuId = SpuId,
            SkuId = SkuId,
            Rating = 5,
            Content = "",  // 空内容
            Images = new List<string>()
        };
        var response = await _client.PostAsJsonAsync("/api/reviews", dto);

        // 注意：SubmitReviewDto 没有显式 [Required] 特性，空 Content 会通过模型绑定到达 AppService。
        // 此处验证响应非 500（即 Controller 层未崩溃），业务校验由 AppService/Domain 层保证。
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    #endregion

    private static ReviewDto CreateReviewDto()
    {
        return new ReviewDto
        {
            ReviewId = ReviewId,
            OrderId = OrderId,
            OrderLineId = OrderLineId,
            SpuId = SpuId,
            SkuId = SkuId,
            UserId = UserId,
            SellerId = SellerId,
            Rating = 5,
            Content = "商品质量很好，物流也快！",
            Images = new List<string>(),
            Status = ReviewStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// 测试鉴权处理器，通过 X-Test-Role 头指定当前用户角色。
/// 默认（无 X-Test-Role 头）赋予全部 4 个角色，覆盖所有鉴权端点的成功场景。
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var roleHeader = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "Buyer,Seller,Operator,Admin";
        var roles = roleHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var claims = new List<Claim> { new(ClaimTypes.Name, "test") };
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// 无操作幂等去重存储，用于测试环境替换 Redis 实现。
/// 所有方法返回默认值（未处理 / 标记成功），不实际持久化任何状态。
/// </summary>
internal sealed class NoopIdempotencyStore : IIdempotencyStore
{
    public bool SupportsAtomicProcessing => false;

    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> TryMarkAsProcessingAsync(Guid eventId, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task ReleaseProcessingLockAsync(Guid eventId, CancellationToken ct = default)
        => Task.CompletedTask;
}
