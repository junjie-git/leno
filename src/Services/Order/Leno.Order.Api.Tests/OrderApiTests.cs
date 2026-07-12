using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.Order.Application;
using Leno.Order.Application.DTOs;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Order.Api.Tests;

public class OrderApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IOrderAppService> _orderAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public OrderApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_orderAppServiceMock.Object);
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
        var response = await _client.GetAsync("/api/orders");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #region Buyer endpoints

    [Fact]
    public async Task GetOrderById_ShouldReturnOrder()
    {
        SetupBuyerAuth();
        var dto = CreateOrderDto();
        _orderAppServiceMock.Setup(s => s.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync($"/api/orders/{OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OrderDto>>();
        body!.Data!.Id.Should().Be(OrderId);
    }

    [Fact]
    public async Task ListMine_ShouldReturnPagedResult()
    {
        SetupBuyerAuth();
        _orderAppServiceMock.Setup(s => s.QueryAsync(UserId, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderListResultDto { Items = new List<OrderDto>(), Total = 0, Page = 1, PageSize = 20 });

        var response = await _client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConfirmReceipt_ShouldCallService()
    {
        SetupBuyerAuth();
        _orderAppServiceMock.Setup(s => s.ConfirmReceiptAsync(OrderId, UserId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/orders/{OrderId}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _orderAppServiceMock.Verify(s => s.ConfirmReceiptAsync(OrderId, UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_ShouldCallService()
    {
        SetupBuyerAuth();
        _orderAppServiceMock.Setup(s => s.CancelAsync(OrderId, UserId, It.IsAny<CancelOrderDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new { Reason = "Changed mind" };
        var response = await _client.PostAsJsonAsync($"/api/orders/{OrderId}/cancel", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _orderAppServiceMock.Verify(s => s.CancelAsync(OrderId, UserId, It.IsAny<CancelOrderDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Seller endpoints

    [Fact]
    public async Task Ship_ShouldCallService()
    {
        SetupSellerAuth();
        _orderAppServiceMock.Setup(s => s.ShipAsync(OrderId, UserId, It.IsAny<ShipOrderDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new { LogisticsNo = "SF123456" };
        var response = await _client.PostAsJsonAsync($"/api/seller/orders/{OrderId}/ship", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _orderAppServiceMock.Verify(s => s.ShipAsync(OrderId, UserId, It.IsAny<ShipOrderDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Admin endpoints

    [Fact]
    public async Task AdminList_ShouldReturnPagedResult()
    {
        SetupAdminAuth();
        _orderAppServiceMock.Setup(s => s.QueryAsync(null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderListResultDto { Items = new List<OrderDto>(), Total = 0, Page = 1, PageSize = 20 });

        var response = await _client.GetAsync("/api/admin/orders");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForceCancel_ShouldCallService()
    {
        SetupAdminAuth();
        _orderAppServiceMock.Setup(s => s.ForceCancelAsync(OrderId, It.IsAny<Guid>(), It.IsAny<ForceCancelOrderDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var dto = new { Reason = "Fraudulent" };
        var response = await _client.PostAsJsonAsync($"/api/admin/orders/{OrderId}/force-cancel", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _orderAppServiceMock.Verify(s => s.ForceCancelAsync(OrderId, It.IsAny<Guid>(), It.IsAny<ForceCancelOrderDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    private void SetupBuyerAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Buyer");
    }

    private void SetupSellerAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Seller");
    }

    private void SetupAdminAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        _currentUserMock.SetupGet(c => c.Role).Returns("Admin");
    }

    private static OrderDto CreateOrderDto()
    {
        return new OrderDto
        {
            Id = OrderId,
            OrderNo = "ORD-001",
            OrderType = OrderType.Normal,
            UserId = UserId,
            SellerId = Guid.NewGuid(),
            Status = OrderStatus.PendingPayment,
            ItemsAmount = 99.99m,
            TotalAmount = 109.99m,
            FreightAmount = 10m,
            CreatedAt = DateTime.UtcNow
        };
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
            new Claim(ClaimTypes.Role, "Operator")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}