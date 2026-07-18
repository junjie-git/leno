using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Leno.ApiGateway.Bff;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.ApiGateway.Tests.Bff;

/// <summary>
/// <see cref="BffForwarderService"/> 单元测试。
/// <para>
/// 4 个核心场景：
/// 1. 全部下游成功 → Success=true、Partial=false
/// 2. 部分下游失败 → Success=false、Partial=true、Errors 含失败明细
/// 3. 全部下游失败 → Success=false、Partial=false
/// 4. 单请求超时 → 该 Source 返回 504 错误
/// </para>
/// 使用 <see cref="StubHttpMessageHandler"/> + <c>AddHttpClient(...).ConfigurePrimaryHttpMessageHandler</c> 模拟下游响应。
/// </summary>
public class BffForwarderServiceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMilliseconds(500);

    private static (BffForwarderService service, StubHttpMessageHandler handler) CreateService(
        Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> handlers)
    {
        var handler = new StubHttpMessageHandler(handlers);
        var services = new ServiceCollection();
        services.AddHttpClient(BffForwarderService.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var service = new BffForwarderService(factory, TestTimeout);
        return (service, handler);
    }

    private static BffDownstreamRequest Req(string source, string url, string method = "GET", string? body = null)
        => new()
        {
            Source = source,
            ServiceUrl = url,
            Method = method,
            RequestBody = body
        };

    private static Task<HttpResponseMessage> JsonOk(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        });
    }

    private static Task<HttpResponseMessage> JsonStatus(HttpStatusCode status, string message)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { message }),
            Encoding.UTF8,
            "application/json");
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = content
        });
    }

    [Fact]
    public async Task ForwardAsync_AllDownstreamSucceed_ReturnsSuccessTrueAndPartialFalse()
    {
        // Arrange — 两个下游均返回 200
        var (service, _) = CreateService(new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
        {
            ["http://order-api:8080/api/orders/00000000-0000-0000-0000-000000000001"] =
                _ => JsonOk(new { orderId = "00000000-0000-0000-0000-000000000001", status = "PAID" }),
            ["http://order-api:8080/api/orders/00000000-0000-0000-0000-000000000001/logistics"] =
                _ => JsonOk(new { tracks = new[] { "created", "shipped" } })
        });

        var requests = new[]
        {
            Req("order-detail", "http://order-api:8080/api/orders/00000000-0000-0000-0000-000000000001"),
            Req("order-logistics", "http://order-api:8080/api/orders/00000000-0000-0000-0000-000000000001/logistics")
        };

        // Act
        var response = await service.ForwardAsync(
            "test-request-id",
            requests,
            dict => new AggregateResult
            {
                First = dict.GetValueOrDefault("order-detail")?.ToString(),
                Second = dict.GetValueOrDefault("order-logistics")?.ToString()
            });

        // Assert
        response.Success.Should().BeTrue();
        response.Partial.Should().BeFalse();
        response.Errors.Should().BeEmpty();
        response.Data.Should().NotBeNull();
        response.Data!.First.Should().Contain("PAID");
        response.Data.Second.Should().Contain("shipped");
    }

    [Fact]
    public async Task ForwardAsync_PartialDownstreamFails_ReturnsSuccessFalsePartialTrueWithErrors()
    {
        // Arrange — 第一个下游 500，第二个下游 200
        var (service, _) = CreateService(new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
        {
            ["http://order-api:8080/api/orders/failure"] =
                _ => JsonStatus(HttpStatusCode.InternalServerError, "order service down"),
            ["http://order-api:8080/api/orders/failure/logistics"] =
                _ => JsonOk(new { tracks = new[] { "created" } })
        });

        var requests = new[]
        {
            Req("order-detail", "http://order-api:8080/api/orders/failure"),
            Req("order-logistics", "http://order-api:8080/api/orders/failure/logistics")
        };

        // Act
        var response = await service.ForwardAsync(
            "test-request-id",
            requests,
            dict => new AggregateResult
            {
                First = dict.GetValueOrDefault("order-detail")?.ToString(),
                Second = dict.GetValueOrDefault("order-logistics")?.ToString()
            });

        // Assert
        response.Success.Should().BeFalse();
        response.Partial.Should().BeTrue();
        response.Errors.Should().HaveCount(1);
        var error = response.Errors.Single();
        error.Source.Should().Be("order-detail");
        error.StatusCode.Should().Be(500);
        error.Message.Should().Contain("order service down");
        response.Data.Should().NotBeNull();
        response.Data!.First.Should().BeNull();
        response.Data.Second.Should().Contain("created");
    }

    [Fact]
    public async Task ForwardAsync_AllDownstreamFail_ReturnsSuccessFalsePartialFalse()
    {
        // Arrange — 所有下游均返回 500
        var (service, _) = CreateService(new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
        {
            ["http://order-api:8080/api/orders/all-fail"] =
                _ => JsonStatus(HttpStatusCode.InternalServerError, "down-1"),
            ["http://order-api:8080/api/orders/all-fail/logistics"] =
                _ => JsonStatus(HttpStatusCode.ServiceUnavailable, "down-2")
        });

        var requests = new[]
        {
            Req("order-detail", "http://order-api:8080/api/orders/all-fail"),
            Req("order-logistics", "http://order-api:8080/api/orders/all-fail/logistics")
        };

        // Act
        var response = await service.ForwardAsync(
            "test-request-id",
            requests,
            dict => new AggregateResult
            {
                First = dict.GetValueOrDefault("order-detail")?.ToString(),
                Second = dict.GetValueOrDefault("order-logistics")?.ToString()
            });

        // Assert
        response.Success.Should().BeFalse();
        response.Partial.Should().BeFalse();
        response.Errors.Should().HaveCount(2);
        response.Errors.Select(e => e.Source).Should().Contain(new[] { "order-detail", "order-logistics" });
        response.Errors.Select(e => e.StatusCode).Should().Contain(new[] { 500, 503 });
        // 无任何下游成功 → results 为空 → aggregator 不被调用 → Data 为 null
        response.Data.Should().BeNull();
    }

    [Fact]
    public async Task ForwardAsync_DownstreamTimeout_Returns504Error()
    {
        // Arrange — 第一个下游延迟超过测试超时（500ms），第二个立即成功
        // 使用稳定的延迟以避免测试不稳定（>1s 远大于 500ms 超时）
        var (service, _) = CreateService(new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
        {
            ["http://slow-api:8080/slow"] = async _ =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(2000));
                return await JsonOk(new { slow = true });
            },
            ["http://fast-api:8080/fast"] = _ => JsonOk(new { fast = true })
        });

        var requests = new[]
        {
            Req("slow-source", "http://slow-api:8080/slow"),
            Req("fast-source", "http://fast-api:8080/fast")
        };

        // Act
        var response = await service.ForwardAsync(
            "test-request-id",
            requests,
            dict => new AggregateResult
            {
                First = dict.GetValueOrDefault("slow-source")?.ToString(),
                Second = dict.GetValueOrDefault("fast-source")?.ToString()
            });

        // Assert — 部分成功（fast-source 200）+ 部分超时（slow-source 504）
        response.Success.Should().BeFalse();
        response.Partial.Should().BeTrue();
        response.Errors.Should().HaveCount(1);
        var error = response.Errors.Single();
        error.Source.Should().Be("slow-source");
        error.StatusCode.Should().Be(504);
        // per-request CTS 与 overall CTS 使用同一超时，二者几乎同时触发；
        // 触发顺序取决于调度，消息为 "Request timed out" 或 "Overall timeout (...)" 之一
        error.Message.Should().ContainAny("timed out", "Overall timeout");
        response.Data.Should().NotBeNull();
        response.Data!.First.Should().BeNull();
        response.Data.Second.Should().Contain("fast");
    }

    [Fact]
    public void Constructor_NullHttpClientFactory_Throws()
    {
        var act = () => new BffForwarderService(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NonPositiveTimeout_Throws()
    {
        var factory = new Mock<IHttpClientFactory>().Object;
        var act = () => new BffForwarderService(factory, TimeSpan.Zero);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ForwardAsync_EmptyRequests_Throws()
    {
        var factory = new Mock<IHttpClientFactory>().Object;
        var service = new BffForwarderService(factory, TestTimeout);

        var act = async () => await service.ForwardAsync(
            "req-id",
            Array.Empty<BffDownstreamRequest>(),
            _ => new AggregateResult());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// 简单的聚合结果 DTO，用于测试。
    /// </summary>
    private sealed class AggregateResult
    {
        public string? First { get; set; }
        public string? Second { get; set; }
    }

    /// <summary>
    /// 基于 URL 路由的 HttpMessageHandler stub，模拟下游响应。
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
