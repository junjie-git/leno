using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Leno.ApiGateway.Bff;
using Leno.ApiGateway.Bff.Controllers;
using Leno.ApiGateway.Bff.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.ApiGateway.Tests.Bff;

/// <summary>
/// <see cref="ProductDetailBffController"/> 单元测试：覆盖 happy path、下游 5xx、超时、
/// 寻址失败与部分失败五类路径。
/// <para>
/// 使用真实 <see cref="BffForwarderService"/> + <see cref="StubHttpMessageHandler"/> 验证
/// 控制器与转发器的组合行为（而非仅 mock 掉转发器）。
/// </para>
/// </summary>
public sealed class ProductDetailBffControllerTests
{
    private const string ProductBase = "http://product-api:8080";
    private const string ReviewBase = "http://review-api:8080";

    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Get_BothDownstreamsSucceed_ReturnsOkWithAggregatedData()
    {
        var handler = new StubHttpMessageHandler(
            new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                [$"{ProductBase}/api/products/{ProductId:D}"] = _ => JsonOk(new { id = ProductId, title = "种子商品" }),
                [$"{ReviewBase}/api/products/{ProductId:D}/reviews"] = _ => JsonOk(new { total = 1, items = new[] { new { rating = 5 } } })
            });

        var controller = CreateController(handler, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));

        var result = await controller.Get(ProductId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<BffResponse<ProductDetailBffResponse>>().Subject;
        payload.Success.Should().BeTrue();
        payload.Partial.Should().BeFalse();
        payload.Errors.Should().BeEmpty();
        payload.Data.Should().NotBeNull();
        payload.Data!.Product.Should().NotBeNull();
        payload.Data.Product!.Value.GetProperty("title").GetString().Should().Be("种子商品");
        payload.Data.ReviewSummary.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_BothDownstreamsReturn5xx_Returns503Envelope()
    {
        var handler = new StubHttpMessageHandler(
            new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                [$"{ProductBase}/api/products/{ProductId:D}"] = _ => JsonStatus(HttpStatusCode.InternalServerError, "product boom"),
                [$"{ReviewBase}/api/products/{ProductId:D}/reviews"] = _ => JsonStatus(HttpStatusCode.InternalServerError, "review boom")
            });

        var controller = CreateController(handler, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));

        var result = await controller.Get(ProductId, CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        JsonSerializer.Serialize(objectResult.Value).Should().Contain("\"code\":503");
    }

    [Fact]
    public async Task Get_BothDownstreamsTimeOut_Returns503Envelope()
    {
        var slow = new Func<HttpRequestMessage, Task<HttpResponseMessage>>(async _ =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            return await JsonOk(new { slow = true });
        });

        var handler = new StubHttpMessageHandler(
            new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                [$"{ProductBase}/api/products/{ProductId:D}"] = slow,
                [$"{ReviewBase}/api/products/{ProductId:D}/reviews"] = slow
            });

        // 单请求 150ms、整体 1s：两个下游均在单请求超时内失败
        var controller = CreateController(handler, TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(1));

        var result = await controller.Get(ProductId, CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Get_AddressResolutionFails_Returns503EnvelopeWithoutCallingDownstream()
    {
        var resolver = new Mock<IBffServiceResolver>();
        resolver
            .Setup(r => r.ResolveBaseAddressAsync("product", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BffServiceUnavailableException("product", "leno-product-api", "无健康实例"));

        var handler = new StubHttpMessageHandler(new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>());
        var controller = BuildController(resolver.Object, handler, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));

        var result = await controller.Get(ProductId, CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        handler.Calls.Should().BeEmpty();
        resolver.Verify(r => r.ResolveBaseAddressAsync("review", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Get_PartialFailure_ReturnsOkWithPartialTrue()
    {
        var handler = new StubHttpMessageHandler(
            new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                [$"{ProductBase}/api/products/{ProductId:D}"] = _ => JsonOk(new { id = ProductId, title = "种子商品" }),
                [$"{ReviewBase}/api/products/{ProductId:D}/reviews"] = _ => JsonStatus(HttpStatusCode.InternalServerError, "review boom")
            });

        var controller = CreateController(handler, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));

        var result = await controller.Get(ProductId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<BffResponse<ProductDetailBffResponse>>().Subject;
        payload.Success.Should().BeFalse();
        payload.Partial.Should().BeTrue();
        payload.Errors.Should().ContainSingle();
        payload.Errors[0].Source.Should().Be("review-summary");
        payload.Errors[0].StatusCode.Should().Be(500);
        payload.Data!.Product.Should().NotBeNull();
        payload.Data.ReviewSummary.Should().BeNull();
    }

    private static ProductDetailBffController CreateController(
        StubHttpMessageHandler handler,
        TimeSpan perRequestTimeout,
        TimeSpan overallTimeout)
    {
        var resolver = new Mock<IBffServiceResolver>();
        resolver
            .Setup(r => r.ResolveBaseAddressAsync("product", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProductBase);
        resolver
            .Setup(r => r.ResolveBaseAddressAsync("review", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReviewBase);

        return BuildController(resolver.Object, handler, perRequestTimeout, overallTimeout);
    }

    private static ProductDetailBffController BuildController(
        IBffServiceResolver resolver,
        StubHttpMessageHandler handler,
        TimeSpan perRequestTimeout,
        TimeSpan overallTimeout)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(BffForwarderService.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var forwarder = new BffForwarderService(factory, overallTimeout, perRequestTimeout);

        var controller = new ProductDetailBffController(
            forwarder,
            resolver,
            NullLogger<ProductDetailBffController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private static Task<HttpResponseMessage> JsonOk(object payload)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    private static Task<HttpResponseMessage> JsonStatus(HttpStatusCode status, string message)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { message }),
            Encoding.UTF8,
            "application/json");
        return Task.FromResult(new HttpResponseMessage(status) { Content = content });
    }

    /// <summary>
    /// 基于完整 URL 路由的 HttpMessageHandler stub；未注册的 URL 返回 404。
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> _handlers;
        private readonly ConcurrentBag<HttpRequestMessage> _calls = new();

        public StubHttpMessageHandler(
            IReadOnlyDictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> handlers)
        {
            _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        }

        public IEnumerable<HttpRequestMessage> Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _calls.Add(request);
            var key = request.RequestUri?.ToString() ?? string.Empty;
            if (_handlers.TryGetValue(key, out var handler))
            {
                return handler(request);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"No stub registered for {key}")
            });
        }
    }
}
