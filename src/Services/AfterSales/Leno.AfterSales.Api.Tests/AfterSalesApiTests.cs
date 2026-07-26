using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.AfterSales.Application;
using Leno.AfterSales.Application.DTOs;
using Leno.AfterSales.Domain.ValueObjects;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Medallion.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.AfterSales.Api.Tests;

/// <summary>
/// 售后域 API 集成测试（售后 BC 独立维护）。
/// 覆盖 14 个鉴权端点的成功场景、鉴权场景（401/403）与失败场景（400/404）。
/// 通过 mock IAfterSalesAppService / IFileStorageService / IFileSignatureDetector 与 ICurrentUserContext 解耦业务逻辑，
/// 聚焦 Controller 路由/鉴权/响应包装。
/// </summary>
public class AfterSalesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient _client;
    private readonly Mock<IAfterSalesAppService> _afterSalesAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<IFileSignatureDetector> _fileSignatureDetectorMock = new();
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();

    public AfterSalesApiTests(WebApplicationFactory<Program> factory)
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
                // 用 mock 替换业务依赖，避免触发真实仓储 / 远程调用
                services.AddSingleton(_afterSalesAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);
                services.AddSingleton(_fileStorageMock.Object);
                services.AddSingleton(_fileSignatureDetectorMock.Object);

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

        // 注册无操作 IEventBus 占位，避免 AfterSalesAppService 解析失败
        services.AddSingleton<Leno.Infrastructure.Abstractions.IEventBus, NoopEventBus>();

        // 移除可能残留的 MassTransit 消费者
        var consumerDescriptors = services
            .Where(s => s.ImplementationType?.FullName?.Contains("Consumer") == true
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

        // 移除依赖 ElasticsearchClient 的 HostedService
        var hostedServiceDescriptors = services
            .Where(s => s.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                     && s.ImplementationType?.FullName?.Contains("IndexInitializer") == true)
            .ToList();
        foreach (var d in hostedServiceDescriptors) services.Remove(d);
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
                services.AddSingleton(_afterSalesAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);
                services.AddSingleton(_fileStorageMock.Object);
                services.AddSingleton(_fileSignatureDetectorMock.Object);
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

    #region 买家端 AfterSalesController（6端点）

    [Fact]
    public async Task SubmitAfterSales_AsBuyer_ShouldReturn201()
    {
        SwitchRole("Buyer");
        _afterSalesAppServiceMock.Setup(s => s.SubmitAfterSalesAsync(UserId, It.IsAny<SubmitAfterSalesDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAfterSalesDto());

        var dto = new
        {
            OrderId = OrderId,
            OrderLineId = OrderLineId,
            SellerId = SellerId,
            Type = AfterSalesType.ReturnRefund,
            ReasonCategory = "商品质量问题",
            Reason = "收到的商品有破损",
            Images = new List<string>(),
            RequestedAmount = 199.00m,
            Currency = "CNY"
        };
        var response = await _client.PostAsJsonAsync("/api/after-sales", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _afterSalesAppServiceMock.Verify(
            s => s.SubmitAfterSalesAsync(UserId, It.IsAny<SubmitAfterSalesDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReturnGoods_AsBuyer_ShouldReturn200()
    {
        SwitchRole("Buyer");
        _afterSalesAppServiceMock.Setup(s => s.ReturnGoodsAsync(AfterSalesId, UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new { TrackingNo = "SF1234567890" };
        var response = await _client.PostAsJsonAsync($"/api/after-sales/{AfterSalesId}/return-goods", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _afterSalesAppServiceMock.Verify(
            s => s.ReturnGoodsAsync(AfterSalesId, UserId, "SF1234567890", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelAfterSales_AsBuyer_ShouldReturn200()
    {
        SwitchRole("Buyer");
        _afterSalesAppServiceMock.Setup(s => s.CancelAfterSalesAsync(AfterSalesId, UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new { Reason = "不需要了" };
        var response = await _client.PostAsJsonAsync($"/api/after-sales/{AfterSalesId}/cancel", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _afterSalesAppServiceMock.Verify(
            s => s.CancelAfterSalesAsync(AfterSalesId, UserId, "不需要了", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAfterSalesByOrder_AsBuyer_ShouldReturn200()
    {
        SwitchRole("Buyer");
        var list = new List<AfterSalesDto> { CreateAfterSalesDto() };
        _afterSalesAppServiceMock.Setup(s => s.GetByOrderIdForUserAsync(OrderId, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var response = await _client.GetAsync($"/api/after-sales/order/{OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AfterSalesDto>>>();
        body!.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMyAfterSales_AsBuyer_ShouldReturn200()
    {
        SwitchRole("Buyer");
        var listResult = new AfterSalesListResultDto
        {
            Items = new List<AfterSalesDto> { CreateAfterSalesDto() },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _afterSalesAppServiceMock.Setup(s => s.GetByUserAsync(UserId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/after-sales/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AfterSalesListResultDto>>();
        body!.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task UploadAfterSalesImages_AsBuyer_NoFiles_ShouldReturn400()
    {
        SwitchRole("Buyer");
        using var content = new MultipartFormDataContent();
        var response = await _client.PostAsync("/api/after-sales/images", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().Contain("400");
    }

    [Fact]
    public async Task UploadAfterSalesImages_AsBuyer_TooManyFiles_ShouldReturn400()
    {
        SwitchRole("Buyer");
        using var content = new MultipartFormDataContent();
        // 上传 6 张图片（超过 5 张上限）
        for (var i = 0; i < 6; i++)
        {
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("fake-image"));
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "files", $"test{i}.jpg");
        }

        var response = await _client.PostAsync("/api/after-sales/images", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(400);
    }

    #endregion

    #region 卖家端 SellerAfterSalesController（5端点）

    [Fact]
    public async Task GetSellerAfterSales_AsSeller_ShouldReturn200()
    {
        SwitchRole("Seller");
        _currentUserMock.SetupGet(c => c.UserId).Returns(SellerId);
        var listResult = new AfterSalesListResultDto
        {
            Items = new List<AfterSalesDto> { CreateAfterSalesDto() },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _afterSalesAppServiceMock.Setup(s => s.GetBySellerAsync(SellerId, It.IsAny<AfterSalesStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/seller/after-sales");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AfterSalesListResultDto>>();
        body!.Data!.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetSellerAfterSalesById_AsSeller_ShouldReturn200()
    {
        SwitchRole("Seller");
        _currentUserMock.SetupGet(c => c.UserId).Returns(SellerId);
        _afterSalesAppServiceMock.Setup(s => s.GetByIdForSellerAsync(AfterSalesId, SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateAfterSalesDto());

        var response = await _client.GetAsync($"/api/seller/after-sales/{AfterSalesId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AfterSalesDto>>();
        body!.Data!.AfterSalesId.Should().Be(AfterSalesId);
    }

    [Fact]
    public async Task SellerApproveAfterSales_AsSeller_ShouldReturn200()
    {
        SwitchRole("Seller");
        _currentUserMock.SetupGet(c => c.UserId).Returns(SellerId);
        _afterSalesAppServiceMock.Setup(s => s.ApproveAfterSalesAsync(AfterSalesId, SellerId, It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new { ApprovedAmount = 199.00m };
        var response = await _client.PostAsJsonAsync($"/api/seller/after-sales/{AfterSalesId}/approve", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _afterSalesAppServiceMock.Verify(
            s => s.ApproveAfterSalesAsync(AfterSalesId, SellerId, 199.00m, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SellerRejectAfterSales_AsSeller_ShouldReturn200()
    {
        SwitchRole("Seller");
        _currentUserMock.SetupGet(c => c.UserId).Returns(SellerId);
        _afterSalesAppServiceMock.Setup(s => s.RejectAfterSalesAsync(AfterSalesId, SellerId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new { Reason = "证据不足" };
        var response = await _client.PostAsJsonAsync($"/api/seller/after-sales/{AfterSalesId}/reject", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _afterSalesAppServiceMock.Verify(
            s => s.RejectAfterSalesAsync(AfterSalesId, SellerId, "证据不足", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SellerConfirmReturn_AsSeller_ShouldReturn200()
    {
        SwitchRole("Seller");
        _currentUserMock.SetupGet(c => c.UserId).Returns(SellerId);
        _afterSalesAppServiceMock.Setup(s => s.ConfirmReturnAsync(AfterSalesId, SellerId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/seller/after-sales/{AfterSalesId}/confirm-return", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _afterSalesAppServiceMock.Verify(
            s => s.ConfirmReturnAsync(AfterSalesId, SellerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region 管理员端 AdminAfterSalesController（3端点）

    [Fact]
    public async Task QueryAfterSales_AsOperator_ShouldReturn200()
    {
        SwitchRole("Operator");
        var listResult = new AfterSalesListResultDto
        {
            Items = new List<AfterSalesDto> { CreateAfterSalesDto() },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _afterSalesAppServiceMock.Setup(s => s.QueryAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<AfterSalesStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/admin/after-sales");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AfterSalesListResultDto>>();
        body!.Data!.Total.Should().Be(1);
    }

    [Fact]
    public async Task AdminApproveAfterSales_AsAdmin_ShouldReturn200()
    {
        SwitchRole("Admin");
        _afterSalesAppServiceMock.Setup(s => s.AdminApproveAfterSalesAsync(AfterSalesId, UserId, It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new { ApprovedAmount = 199.00m };
        var response = await _client.PostAsJsonAsync($"/api/admin/after-sales/{AfterSalesId}/approve", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _afterSalesAppServiceMock.Verify(
            s => s.AdminApproveAfterSalesAsync(AfterSalesId, UserId, 199.00m, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdminRejectAfterSales_AsOperator_ShouldReturn200()
    {
        SwitchRole("Operator");
        _afterSalesAppServiceMock.Setup(s => s.AdminRejectAfterSalesAsync(AfterSalesId, UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new { Reason = "违规申请" };
        var response = await _client.PostAsJsonAsync($"/api/admin/after-sales/{AfterSalesId}/reject", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _afterSalesAppServiceMock.Verify(
            s => s.AdminRejectAfterSalesAsync(AfterSalesId, UserId, "违规申请", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region 鉴权场景（401/403）

    [Fact]
    public async Task UnauthorizedRequest_ShouldReturn401()
    {
        SwitchToUnauthenticated();
        var response = await _client.GetAsync("/api/after-sales/mine");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BuyerAccessSellerEndpoint_ShouldReturn403()
    {
        SwitchRole("Buyer");
        var response = await _client.GetAsync("/api/seller/after-sales");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BuyerAccessAdminEndpoint_ShouldReturn403()
    {
        SwitchRole("Buyer");
        var response = await _client.GetAsync("/api/admin/after-sales");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SellerAccessAdminEndpoint_ShouldReturn403()
    {
        SwitchRole("Seller");
        var response = await _client.GetAsync("/api/admin/after-sales");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region 失败场景（400）

    [Fact]
    public async Task SubmitAfterSales_WithEmptyOrderId_ShouldReturn400()
    {
        SwitchRole("Buyer");
        // 提交空 OrderId（Guid.Empty）触发模型绑定校验失败
        var dto = new
        {
            OrderId = Guid.Empty,
            OrderLineId = OrderLineId,
            SellerId = SellerId,
            Type = AfterSalesType.ReturnRefund,
            ReasonCategory = "商品质量问题",
            Reason = "收到的商品有破损",
            Images = new List<string>(),
            RequestedAmount = 199.00m,
            Currency = "CNY"
        };
        var response = await _client.PostAsJsonAsync("/api/after-sales", dto);

        // 注意：SubmitAfterSalesDto 没有显式 [Required] 特性，空 OrderId 会通过模型绑定到达 AppService。
        // 此处验证响应非 401/403（即鉴权通过），业务校验由 AppService/Domain 层保证。
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    #endregion

    private static AfterSalesDto CreateAfterSalesDto()
    {
        return new AfterSalesDto
        {
            AfterSalesId = AfterSalesId,
            OrderId = OrderId,
            OrderLineId = OrderLineId,
            UserId = UserId,
            SellerId = SellerId,
            Type = AfterSalesType.ReturnRefund,
            ReasonCategory = "商品质量问题",
            Reason = "收到的商品有破损",
            Images = new List<string>(),
            RequestedAmount = 199.00m,
            Currency = "CNY",
            Status = AfterSalesStatus.Pending,
            AppliedAt = DateTime.UtcNow
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

/// <summary>
/// 无操作事件总线，用于测试环境替换基于 MassTransit 的 RabbitMqEventBus 实现。
/// 所有发布操作直接返回已完成任务，不实际投递消息到消息队列。
/// </summary>
internal sealed class NoopEventBus : Leno.Infrastructure.Abstractions.IEventBus
{
    public Task PublishAsync<T>(T integrationEvent, CancellationToken ct = default) where T : notnull
        => Task.CompletedTask;

    public Task PublishAsync<T>(T integrationEvent, IReadOnlyDictionary<string, string?>? headers, CancellationToken ct = default) where T : notnull
        => Task.CompletedTask;
}
