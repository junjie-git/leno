using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.Payment.Application;
using Leno.Payment.Application.DTOs;
using Leno.Payment.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Payment.Api.Tests;

public class PaymentApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IPaymentAppService> _paymentAppServiceMock = new();
    private readonly Mock<IRefundAppService> _refundAppServiceMock = new();
    private readonly Mock<IPaymentInternalQueryService> _internalQueryServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private const string TestInternalKey = "test-internal-key-123";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid PaymentId = Guid.NewGuid();
    private static readonly Guid AfterSalesId = Guid.NewGuid();

    public PaymentApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_paymentAppServiceMock.Object);
                services.AddSingleton(_refundAppServiceMock.Object);
                services.AddSingleton(_internalQueryServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                services.Configure<InternalApiKeyOptions>(o =>
                {
                    o.ApiKey = TestInternalKey;
                    o.RoutePrefix = "internal/";
                });

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
        var response = await _client.GetAsync("/api/admin/payments");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PaymentsController - Buyer

    [Fact]
    public async Task GetPaymentResult_ShouldReturnDto()
    {
        SetupBuyerAuth();
        var dto = new PaymentOrderDto
        {
            PaymentId = PaymentId, OrderId = OrderId, UserId = UserId,
            Amount = 100m, Channel = PaymentChannel.WeChatPay, Status = PaymentStatus.Paid
        };
        _paymentAppServiceMock.Setup(s => s.GetPaymentResultAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync($"/api/payments/{OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PaymentOrderDto>>();
        result!.Data!.PaymentId.Should().Be(PaymentId);
        result.Data.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task GetPaymentResult_NotFound_ShouldReturnNullData()
    {
        SetupBuyerAuth();
        _paymentAppServiceMock.Setup(s => s.GetPaymentResultAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentOrderDto?)null);

        var response = await _client.GetAsync($"/api/payments/{OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PaymentOrderDto>>();
        result!.Data.Should().BeNull();
    }

    [Fact]
    public async Task QueryPaymentStatus_ShouldReturnChannelStatus()
    {
        SetupBuyerAuth();
        var dto = new ChannelStatusDto
        {
            PaymentId = PaymentId, IsPaid = true,
            ChannelTradeNo = "CH001", PaidAt = DateTime.UtcNow
        };
        _paymentAppServiceMock.Setup(s => s.QueryPaymentStatusAsync(PaymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync($"/api/payments/{PaymentId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<ChannelStatusDto>>();
        result!.Data!.IsPaid.Should().BeTrue();
        result.Data.ChannelTradeNo.Should().Be("CH001");
    }

    [Fact]
    public async Task GetRefundResult_ShouldReturnDto()
    {
        SetupBuyerAuth();
        var dto = new RefundOrderDto
        {
            RefundId = Guid.NewGuid(), PaymentId = PaymentId, OrderId = OrderId,
            UserId = UserId, AfterSalesId = AfterSalesId, RefundAmount = 50m,
            Channel = PaymentChannel.WeChatPay, Status = RefundStatus.Succeeded
        };
        _refundAppServiceMock.Setup(s => s.GetRefundResultAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync($"/api/refunds/{AfterSalesId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RefundOrderDto>>();
        result!.Data!.AfterSalesId.Should().Be(AfterSalesId);
        result.Data.RefundAmount.Should().Be(50m);
    }

    [Fact]
    public async Task GetRefundResult_NotFound_ShouldReturnNullData()
    {
        SetupBuyerAuth();
        _refundAppServiceMock.Setup(s => s.GetRefundResultAsync(AfterSalesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundOrderDto?)null);

        var response = await _client.GetAsync($"/api/refunds/{AfterSalesId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RefundOrderDto>>();
        result!.Data.Should().BeNull();
    }

    #endregion

    #region PaymentsController - Admin

    [Fact]
    public async Task QueryPayments_ShouldReturnPagedList()
    {
        SetupAdminAuth();
        var listResult = new PaymentListResultDto
        {
            Items =
            [
                new PaymentOrderDto { PaymentId = PaymentId, OrderId = OrderId, Amount = 100m }
            ],
            Total = 1, Page = 1, PageSize = 20
        };
        _paymentAppServiceMock.Setup(s => s.QueryPaymentsAsync(
                null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/admin/payments?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PaymentListResultDto>>();
        result!.Data!.Items.Should().HaveCount(1);
        result.Data.Total.Should().Be(1);
    }

    [Fact]
    public async Task QueryPayments_WithFilters_ShouldPassFilters()
    {
        SetupAdminAuth();
        var listResult = new PaymentListResultDto { Items = [], Total = 0, Page = 1, PageSize = 10 };
        _paymentAppServiceMock.Setup(s => s.QueryPaymentsAsync(
                UserId, PaymentChannel.WeChatPay, PaymentStatus.Paid,
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync(
            $"/api/admin/payments?userId={UserId}&channel=0&status=1&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task QueryRefunds_ShouldReturnPagedList()
    {
        SetupAdminAuth();
        var listResult = new RefundListResultDto
        {
            Items =
            [
                new RefundOrderDto { RefundId = Guid.NewGuid(), OrderId = OrderId, RefundAmount = 50m }
            ],
            Total = 1, Page = 1, PageSize = 20
        };
        _refundAppServiceMock.Setup(s => s.QueryRefundsAsync(
                null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/admin/refunds?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RefundListResultDto>>();
        result!.Data!.Items.Should().HaveCount(1);
        result.Data.Total.Should().Be(1);
    }

    [Fact]
    public async Task QueryRefunds_WithFilters_ShouldPassFilters()
    {
        SetupAdminAuth();
        var listResult = new RefundListResultDto { Items = [], Total = 0, Page = 1, PageSize = 10 };
        _refundAppServiceMock.Setup(s => s.QueryRefundsAsync(
                OrderId, RefundStatus.Succeeded, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync(
            $"/api/admin/refunds?orderId={OrderId}&status=1&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region InternalPaymentsController

    [Fact]
    public async Task GetPaymentInfo_ShouldReturnDto()
    {
        var dto = new PaymentInfoResultDto
        {
            PaymentId = PaymentId, OrderId = OrderId,
            Channel = (int)PaymentChannel.WeChatPay, Status = (int)PaymentStatus.Paid
        };
        _internalQueryServiceMock.Setup(s => s.GetPaymentInfoByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/internal/v1/payments/{OrderId}/info");
        request.Headers.Add("X-Internal-Key", TestInternalKey);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PaymentInfoResultDto>>();
        result!.Data!.PaymentId.Should().Be(PaymentId);
        result.Data.Channel.Should().Be((int)PaymentChannel.WeChatPay);
    }

    [Fact]
    public async Task GetPaymentInfo_NotFound_ShouldReturn404()
    {
        _internalQueryServiceMock.Setup(s => s.GetPaymentInfoByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentInfoResultDto?)null);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/internal/v1/payments/{OrderId}/info");
        request.Headers.Add("X-Internal-Key", TestInternalKey);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Auth Helpers

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

    #endregion
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
