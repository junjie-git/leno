using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.Promotion.Application;
using Leno.Promotion.Application.DTOs;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Promotion.Api.Tests;

public class PromotionApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IPromotionAppService> _promotionAppServiceMock = new();
    private readonly Mock<ICouponAppService> _couponAppServiceMock = new();
    private readonly Mock<ISeckillAppService> _seckillAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ActivityId = Guid.NewGuid();
    private static readonly Guid CouponId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();

    public PromotionApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_promotionAppServiceMock.Object);
                services.AddSingleton(_couponAppServiceMock.Object);
                services.AddSingleton(_seckillAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);

                services.AddAuthentication(defaultScheme: "Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }).CreateClient();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
    }

    private static void RemoveMassTransitServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("MassTransit") == true
                     || s.ImplementationType?.FullName?.Contains("MassTransit") == true
                     || s.ServiceType == typeof(MassTransit.IBus)
                     || s.ServiceType == typeof(MassTransit.IBusControl)
                     || s.ServiceType.FullName?.StartsWith("MassTransit.", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
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
    }

    #region Health

    [Fact]
    public async Task Health_Live_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturnUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/admin/promotions");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PromotionsController

    [Fact]
    public async Task CreatePromotion_ShouldReturnDto()
    {
        SetupAdminAuth();
        var dto = new PromotionActivityDto
        {
            Id = ActivityId, Name = "双11满减", Type = PromotionType.FullReduction,
            Status = PromotionStatus.Pending,
            StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(2)
        };
        _promotionAppServiceMock.Setup(s => s.CreateAsync(It.IsAny<CreatePromotionActivityDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            Name = "双11满减", Type = 0, // FullReduction
            StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(2),
            Rules = new[] { new { ThresholdAmount = 100m, DiscountAmount = 10m } }
        };
        var response = await _client.PostAsJsonAsync("/api/admin/promotions", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PromotionActivityDto>>();
        result!.Data!.Name.Should().Be("双11满减");
    }

    [Fact]
    public async Task UpdatePromotion_ShouldCallService()
    {
        SetupAdminAuth();
        var dto = new PromotionActivityDto { Id = ActivityId, Name = "更新活动" };
        _promotionAppServiceMock.Setup(s => s.UpdateAsync(ActivityId, It.IsAny<UpdatePromotionActivityDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new { Name = "更新活动", Rules = new[] { new { ThresholdAmount = 200m, DiscountAmount = 30m } } };
        var response = await _client.PutAsJsonAsync($"/api/admin/promotions/{ActivityId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _promotionAppServiceMock.Verify(s => s.UpdateAsync(ActivityId, It.IsAny<UpdatePromotionActivityDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivatePromotion_ShouldCallService()
    {
        SetupAdminAuth();
        _promotionAppServiceMock.Setup(s => s.ActivateAsync(ActivityId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/promotions/{ActivityId}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _promotionAppServiceMock.Verify(s => s.ActivateAsync(ActivityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PausePromotion_ShouldCallService()
    {
        SetupAdminAuth();
        _promotionAppServiceMock.Setup(s => s.PauseAsync(ActivityId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/promotions/{ActivityId}/pause", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _promotionAppServiceMock.Verify(s => s.PauseAsync(ActivityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClosePromotion_ShouldCallService()
    {
        SetupAdminAuth();
        _promotionAppServiceMock.Setup(s => s.CloseAsync(ActivityId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/promotions/{ActivityId}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _promotionAppServiceMock.Verify(s => s.CloseAsync(ActivityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPromotionById_ShouldReturnDto()
    {
        SetupAdminAuth();
        var dto = new PromotionActivityDto { Id = ActivityId, Name = "双11满减" };
        _promotionAppServiceMock.Setup(s => s.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync($"/api/admin/promotions/{ActivityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PromotionActivityDto>>();
        result!.Data!.Id.Should().Be(ActivityId);
    }

    [Fact]
    public async Task QueryPromotions_ShouldReturnList()
    {
        SetupAdminAuth();
        var list = new List<PromotionActivityDto> { new() { Id = ActivityId, Name = "活动1" } };
        _promotionAppServiceMock.Setup(s => s.QueryAsync(null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var response = await _client.GetAsync("/api/admin/promotions?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<PromotionActivityDto>>>();
        result!.Data!.Should().HaveCount(1);
    }

    #endregion

    #region CouponsController (Admin)

    [Fact]
    public async Task CreateCoupon_ShouldReturnDto()
    {
        SetupAdminAuth();
        var dto = new CouponDto { Id = CouponId, Name = "满100减20", Type = CouponType.FixedAmount, FaceValue = 20m, Status = CouponTemplateStatus.Enabled };
        _couponAppServiceMock.Setup(s => s.CreateAsync(It.IsAny<CreateCouponDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            Name = "满100减20", Type = 0, // FixedAmount
            FaceValue = 20m, MinSpend = 100m,
            ValidityType = 0, // FixedPeriod
            ValidFrom = DateTime.UtcNow, ValidTo = DateTime.UtcNow.AddDays(30), TotalQty = 1000
        };
        var response = await _client.PostAsJsonAsync("/api/admin/coupons", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CouponDto>>();
        result!.Data!.Name.Should().Be("满100减20");
    }

    [Fact]
    public async Task UpdateCoupon_ShouldCallService()
    {
        SetupAdminAuth();
        var dto = new CouponDto { Id = CouponId, Name = "更新券" };
        _couponAppServiceMock.Setup(s => s.UpdateAsync(CouponId, It.IsAny<UpdateCouponDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new { Name = "更新券", Type = 0, FaceValue = 30m, MinSpend = 200m, ValidityType = 0, ValidFrom = DateTime.UtcNow, ValidTo = DateTime.UtcNow.AddDays(30) };
        var response = await _client.PutAsJsonAsync($"/api/admin/coupons/{CouponId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _couponAppServiceMock.Verify(s => s.UpdateAsync(CouponId, It.IsAny<UpdateCouponDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableCoupon_ShouldCallService()
    {
        SetupAdminAuth();
        _couponAppServiceMock.Setup(s => s.EnableAsync(CouponId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/coupons/{CouponId}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _couponAppServiceMock.Verify(s => s.EnableAsync(CouponId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableCoupon_ShouldCallService()
    {
        SetupAdminAuth();
        _couponAppServiceMock.Setup(s => s.DisableAsync(CouponId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/coupons/{CouponId}/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _couponAppServiceMock.Verify(s => s.DisableAsync(CouponId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueCoupon_ShouldCallService()
    {
        SetupAdminAuth();
        _couponAppServiceMock.Setup(s => s.IssueAsync(CouponId, 50, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/coupons/{CouponId}/issue?quantity=50", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _couponAppServiceMock.Verify(s => s.IssueAsync(CouponId, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryCoupons_ShouldReturnList()
    {
        SetupAdminAuth();
        var list = new List<CouponDto> { new() { Id = CouponId, Name = "券1" } };
        _couponAppServiceMock.Setup(s => s.QueryAsync(null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var response = await _client.GetAsync("/api/admin/coupons?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CouponDto>>>();
        result!.Data!.Should().HaveCount(1);
    }

    #endregion

    #region CouponsController (Buyer)

    [Fact]
    public async Task GetAvailableCoupons_ShouldReturnList()
    {
        SetupBuyerAuth();
        var list = new List<CouponDto> { new() { Id = CouponId, Name = "可领券" } };
        _couponAppServiceMock.Setup(s => s.GetReceivableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var response = await _client.GetAsync("/api/coupons/available");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CouponDto>>>();
        result!.Data!.Should().HaveCount(1);
    }

    [Fact]
    public async Task ReceiveCoupon_ShouldReturnUserCoupon()
    {
        SetupBuyerAuth();
        var dto = new UserCouponDto { Id = Guid.NewGuid(), UserId = UserId, CouponId = CouponId, Status = CouponStatus.Unused };
        _couponAppServiceMock.Setup(s => s.ReceiveAsync(UserId, CouponId, "Manual", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.PostAsync($"/api/coupons/{CouponId}/receive?source=Manual", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserCouponDto>>();
        result!.Data!.UserId.Should().Be(UserId);
    }

    [Fact]
    public async Task GetMyCoupons_ShouldReturnList()
    {
        SetupBuyerAuth();
        var list = new List<UserCouponDto> { new() { Id = Guid.NewGuid(), UserId = UserId, Status = CouponStatus.Unused } };
        _couponAppServiceMock.Setup(s => s.GetMyCouponsAsync(UserId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var response = await _client.GetAsync("/api/coupons/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<UserCouponDto>>>();
        result!.Data!.Should().HaveCount(1);
    }

    #endregion

    #region SeckillController (Admin)

    [Fact]
    public async Task CreateSeckill_ShouldReturnDto()
    {
        SetupAdminAuth();
        var dto = new SeckillActivityDto
        {
            Id = ActivityId, SpuId = SpuId, SkuId = SkuId, SeckillPrice = 99m, OriginalPrice = 199m,
            TotalStock = 100, Status = SeckillStatus.Pending
        };
        _seckillAppServiceMock.Setup(s => s.CreateAsync(It.IsAny<CreateSeckillActivityDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new
        {
            SpuId = SpuId, SkuId = SkuId, SeckillPrice = 99m, OriginalPrice = 199m,
            TotalStock = 100, LimitPerUser = 1,
            StartTime = DateTime.UtcNow.AddHours(1), EndTime = DateTime.UtcNow.AddHours(2)
        };
        var response = await _client.PostAsJsonAsync("/api/admin/seckill/activities", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SeckillActivityDto>>();
        result!.Data!.SeckillPrice.Should().Be(99m);
    }

    [Fact]
    public async Task ActivateSeckill_ShouldCallService()
    {
        SetupAdminAuth();
        _seckillAppServiceMock.Setup(s => s.ActivateAsync(ActivityId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/seckill/activities/{ActivityId}/activate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _seckillAppServiceMock.Verify(s => s.ActivateAsync(ActivityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseSeckill_ShouldCallService()
    {
        SetupAdminAuth();
        _seckillAppServiceMock.Setup(s => s.CloseActivityWithStockWriteBackAsync(ActivityId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/seckill/activities/{ActivityId}/close", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _seckillAppServiceMock.Verify(s => s.CloseActivityWithStockWriteBackAsync(ActivityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QuerySeckill_ShouldReturnList()
    {
        SetupAdminAuth();
        var list = new List<SeckillActivityDto> { new() { Id = ActivityId, SeckillPrice = 99m } };
        _seckillAppServiceMock.Setup(s => s.QueryAsync(null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var response = await _client.GetAsync("/api/admin/seckill/activities?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SeckillActivityDto>>>();
        result!.Data!.Should().HaveCount(1);
    }

    #endregion

    #region SeckillController (Buyer)

    [Fact]
    public async Task GetActiveSeckill_ShouldReturnList()
    {
        SetupBuyerAuth();
        var list = new List<SeckillActivityDto> { new() { Id = ActivityId, SeckillPrice = 99m } };
        _seckillAppServiceMock.Setup(s => s.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);

        var response = await _client.GetAsync("/api/seckill/activities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<SeckillActivityDto>>>();
        result!.Data!.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSeckillById_ShouldReturnDto()
    {
        SetupBuyerAuth();
        var dto = new SeckillActivityDto { Id = ActivityId, SeckillPrice = 99m };
        _seckillAppServiceMock.Setup(s => s.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync($"/api/seckill/activities/{ActivityId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SeckillActivityDto>>();
        result!.Data!.Id.Should().Be(ActivityId);
    }

    [Fact]
    public async Task PlaceOrder_ShouldReturnResult()
    {
        SetupBuyerAuth();
        var resultDto = new SeckillPlaceOrderResultDto
        {
            OrderId = Guid.NewGuid(), ActivityId = ActivityId, UserId = UserId,
            SeckillPrice = 99m, Quantity = 2, PlacedAt = DateTime.UtcNow
        };
        _seckillAppServiceMock.Setup(s => s.PlaceOrderAsync(ActivityId, UserId, It.IsAny<SeckillPlaceOrderDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        var body = new { Quantity = 2 };
        var response = await _client.PostAsJsonAsync($"/api/seckill/activities/{ActivityId}/place", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SeckillPlaceOrderResultDto>>();
        result!.Data!.Quantity.Should().Be(2);
    }

    #endregion

    private void SetupAdminAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Admin");
    }

    private void SetupBuyerAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Buyer");
    }
}

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

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "test"),
            new Claim(ClaimTypes.Role, "Buyer"),
            new Claim(ClaimTypes.Role, "Seller"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Operator"),
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}