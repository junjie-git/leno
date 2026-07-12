using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Infrastructure.Auth;
using Leno.Product.Application;
using Leno.Product.Application.DTOs;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Product.Api.Tests;

public class ProductApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<ISPUAppService> _spuAppServiceMock = new();
    private readonly Mock<IProductSearchService> _searchServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();

    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid ShopId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();

    public ProductApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing");

            builder.ConfigureServices(services =>
            {
                // Replace external dependencies with mocks
                services.AddSingleton(_spuAppServiceMock.Object);
                services.AddSingleton(_searchServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);
                services.AddSingleton(_eventBusMock.Object);

                // Remove all MassTransit-related services
                RemoveMassTransitServices(services);

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
        var descriptorsToRemove = services
            .Where(s => s.ServiceType.FullName?.Contains("MassTransit") == true
                     || s.ImplementationType?.FullName?.Contains("MassTransit") == true
                     || s.ServiceType == typeof(MassTransit.IBus)
                     || s.ServiceType == typeof(MassTransit.IBusControl)
                     || s.ServiceType.FullName?.StartsWith("MassTransit.", StringComparison.Ordinal) == true)
            .ToList();

        foreach (var descriptor in descriptorsToRemove)
        {
            services.Remove(descriptor);
        }
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
        var response = await _client.GetAsync("/api/products");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #region ProductsController

    [Fact]
    public async Task GetById_ShouldReturnProduct()
    {
        SetupSellerAuth();
        var dto = CreateProductDto();
        _spuAppServiceMock.Setup(s => s.GetByIdAsync(ProductId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync($"/api/products/{ProductId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProductDto>>();
        body!.Data!.Id.Should().Be(ProductId);
    }

    [Fact]
    public async Task Create_ShouldCallService()
    {
        SetupSellerAuth();
        var dto = CreateProductDto();
        _spuAppServiceMock.Setup(s => s.CreateAsync(SellerId, ShopId, It.IsAny<CreateProductDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var createDto = new { Title = "Test", MainImageUrl = "https://img.example.com/1.jpg", CategoryId = Guid.NewGuid() };
        await _client.PostAsJsonAsync("/api/products", createDto);

        _spuAppServiceMock.Verify(s => s.CreateAsync(SellerId, ShopId, It.IsAny<CreateProductDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Query_ShouldReturnPagedResult()
    {
        SetupSellerAuth();
        _spuAppServiceMock.Setup(s => s.QueryProductsAsync(It.IsAny<ProductQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<ProductDto>(new List<ProductDto>(), 0, 1, 20));

        var response = await _client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitForReview_ShouldReturnOk()
    {
        SetupSellerAuth();
        _spuAppServiceMock.Setup(s => s.SubmitForReviewAsync(SellerId, ProductId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/products/{ProductId}/submit", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region SearchController

    [Fact]
    public async Task Search_ShouldReturnResults()
    {
        SetupSellerAuth();
        _searchServiceMock.Setup(s => s.SearchAsync(
                It.IsAny<string?>(), null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<ProductSearchResultDto>(new List<ProductSearchResultDto>(), 0, 1, 20));

        var response = await _client.GetAsync("/api/products/search");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region AdminProductsController

    [Fact]
    public async Task AdminApprove_ShouldReturnOk()
    {
        SetupAdminAuth();
        _spuAppServiceMock.Setup(s => s.ApproveAsync(ProductId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync($"/api/admin/products/{ProductId}/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminReject_ShouldReturnOk()
    {
        SetupAdminAuth();
        _spuAppServiceMock.Setup(s => s.RejectAsync(ProductId, It.IsAny<Guid>(), It.IsAny<ActionReasonDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var rejectDto = new { Reason = "Not good enough" };
        var response = await _client.PostAsJsonAsync($"/api/admin/products/{ProductId}/reject", rejectDto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    private void SetupSellerAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(SellerId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Seller");
        _currentUserMock.SetupGet(c => c.ShopId).Returns(ShopId);
    }

    private void SetupAdminAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        _currentUserMock.SetupGet(c => c.Role).Returns("Admin");
        _currentUserMock.SetupGet(c => c.ShopId).Returns((Guid?)null);
    }

    private static ProductDto CreateProductDto()
    {
        return new ProductDto
        {
            Id = ProductId,
            ShopId = ShopId,
            SellerId = SellerId,
            Title = "Test Product",
            MainImageUrl = "https://img.example.com/1.jpg",
            CategoryId = Guid.NewGuid(),
            Status = ProductStatus.Draft
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