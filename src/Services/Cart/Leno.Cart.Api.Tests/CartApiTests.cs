using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Cart.Application;
using Leno.Cart.Application.DTOs;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Cart.Api.Tests;

public class CartApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<ICartAppService> _cartAppServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid CartId = Guid.NewGuid();

    public CartApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_cartAppServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

                // Remove MassTransit / RabbitMQ dependencies
                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);

                // Replace auth with test scheme
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
        var response = await _client.GetAsync("/api/cart");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #region GetCartAsync

    [Fact]
    public async Task GetCart_ShouldReturnCart()
    {
        SetupBuyerAuth();
        var dto = CreateCartDto();
        _cartAppServiceMock.Setup(s => s.GetCartAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CartDto>>();
        body!.Data!.Id.Should().Be(CartId);
        body.Data.UserId.Should().Be(UserId);
    }

    #endregion

    #region AddItemAsync

    [Fact]
    public async Task AddItem_ShouldCallService()
    {
        SetupBuyerAuth();
        _cartAppServiceMock.Setup(s => s.AddItemAsync(UserId, It.IsAny<AddCartItemDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCartDto());

        var dto = new { SkuId = SkuId, Quantity = 3, SellerId = Guid.NewGuid() };
        var response = await _client.PostAsJsonAsync("/api/cart/items", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _cartAppServiceMock.Verify(s => s.AddItemAsync(UserId, It.IsAny<AddCartItemDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UpdateQuantityAsync

    [Fact]
    public async Task UpdateQuantity_ShouldCallService()
    {
        SetupBuyerAuth();
        _cartAppServiceMock.Setup(s => s.UpdateQuantityAsync(UserId, SkuId, It.IsAny<UpdateCartItemQuantityDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCartDto());

        var dto = new { Quantity = 5 };
        var response = await _client.PutAsJsonAsync($"/api/cart/items/{SkuId}", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _cartAppServiceMock.Verify(s => s.UpdateQuantityAsync(UserId, SkuId, It.IsAny<UpdateCartItemQuantityDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region RemoveItemAsync

    [Fact]
    public async Task RemoveItem_ShouldCallService()
    {
        SetupBuyerAuth();
        _cartAppServiceMock.Setup(s => s.RemoveItemAsync(UserId, SkuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCartDto());

        var response = await _client.DeleteAsync($"/api/cart/items/{SkuId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _cartAppServiceMock.Verify(s => s.RemoveItemAsync(UserId, SkuId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region SelectItemsAsync

    [Fact]
    public async Task SelectItems_ShouldCallService()
    {
        SetupBuyerAuth();
        _cartAppServiceMock.Setup(s => s.SelectItemsAsync(UserId, It.IsAny<SelectCartItemsDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCartDto());

        var dto = new { SkuIds = new[] { SkuId }, Selected = true };
        var response = await _client.PostAsJsonAsync("/api/cart/items/select", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _cartAppServiceMock.Verify(s => s.SelectItemsAsync(UserId, It.IsAny<SelectCartItemsDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region PreviewCheckoutAsync

    [Fact]
    public async Task PreviewCheckout_ShouldReturnPreview()
    {
        SetupBuyerAuth();
        var preview = new CheckoutPreviewDto
        {
            Groups = new List<CheckoutGroupDto>
            {
                new()
                {
                    SellerId = Guid.NewGuid(),
                    Items = new List<CartItemDto>
                    {
                        new() { Id = Guid.NewGuid(), SkuId = SkuId, Quantity = 2, UnitPrice = 99.99m, IsSelected = true }
                    },
                    SubtotalAmount = 199.98m,
                    Currency = "CNY"
                }
            },
            TotalAmount = 199.98m,
            Currency = "CNY",
            TotalCount = 2
        };
        _cartAppServiceMock.Setup(s => s.PreviewCheckoutAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        var response = await _client.PostAsync("/api/cart/preview", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CheckoutPreviewDto>>();
        body!.Data!.TotalAmount.Should().Be(199.98m);
        body.Data.TotalCount.Should().Be(2);
    }

    #endregion

    private void SetupBuyerAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Buyer");
    }

    private static CartDto CreateCartDto()
    {
        return new CartDto
        {
            Id = CartId,
            UserId = UserId,
            Items = new List<CartItemDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SkuId = SkuId,
                    SellerId = Guid.NewGuid(),
                    Quantity = 3,
                    IsSelected = true,
                    UnitPrice = 49.99m,
                    Currency = "CNY",
                    Title = "Test Product",
                    Available = true
                }
            },
            SelectedTotalAmount = 149.97m,
            Currency = "CNY",
            TotalCount = 3
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
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}