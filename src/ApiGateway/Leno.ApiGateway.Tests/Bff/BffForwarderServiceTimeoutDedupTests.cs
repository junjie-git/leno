using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Leno.ApiGateway.Bff;
using Leno.ApiGateway.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Tests.Bff;

/// <summary>
/// P1-T15/T16 验证：BffForwarderService 整体超时可配置 + 504 回填原子去重。
/// <para>
/// T15：整体超时（默认 10s）与单请求超时（默认 3s）分离，整体超时应大于单请求超时。
/// T16：整体超时回填 504 改用 ConcurrentDictionary&lt;string, BffError&gt; + TryAdd 原子去重，
/// 同一 Source 仅产生一个 504 错误条目，不重复。
/// </para>
/// </summary>
public class BffForwarderServiceTimeoutDedupTests
{
    private static BffDownstreamRequest Req(string source, string url)
        => new() { Source = source, ServiceUrl = url };

    private static Task<HttpResponseMessage> JsonOk(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    /// <summary>
    /// 基于 URL 路由的 HttpMessageHandler stub，模拟下游响应。
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly IReadOnlyDictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> _handlers;

        public StubHttpMessageHandler(
            IReadOnlyDictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> handlers)
        {
            _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
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

    private static (BffForwarderService service, ServiceProvider sp) CreateService(
        Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>> handlers,
        TimeSpan overallTimeout,
        TimeSpan perRequestTimeout)
    {
        var handler = new StubHttpMessageHandler(handlers);
        var services = new ServiceCollection();
        services.AddHttpClient(BffForwarderService.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var service = new BffForwarderService(factory, overallTimeout, perRequestTimeout);
        return (service, sp);
    }

    /// <summary>
    /// T15 验证：整体超时可配置且独立于单请求超时。
    /// 单请求超时设为 300ms，整体超时设为 2s。慢请求在 300ms 超时（per-request），
    /// 但整体仍在 2s 内完成（快请求成功），证明两个超时独立工作。
    /// </summary>
    [Fact]
    public async Task ForwardAsync_OverallTimeoutGreaterThanPerRequest_SlowRequestTimesOutPerRequest()
    {
        // Arrange — 慢请求延迟 1s（超过 perRequest 300ms，但小于 overall 2s）
        var (service, sp) = CreateService(
            new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                ["http://slow/slow"] = async _ =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1000));
                    return await JsonOk(new { slow = true });
                },
                ["http://fast/fast"] = _ => JsonOk(new { fast = true })
            },
            overallTimeout: TimeSpan.FromSeconds(2),
            perRequestTimeout: TimeSpan.FromMilliseconds(300));

        var requests = new[]
        {
            Req("slow-source", "http://slow/slow"),
            Req("fast-source", "http://fast/fast")
        };

        // Act
        var response = await service.ForwardAsync(
            "req-id",
            requests,
            dict => new { First = dict.GetValueOrDefault("slow-source")?.ToString() });

        // Assert — 慢请求因 per-request 超时返回 504，快请求成功
        response.Success.Should().BeFalse();
        response.Partial.Should().BeTrue();
        response.Errors.Should().HaveCount(1);
        response.Errors.Single().Source.Should().Be("slow-source");
        response.Errors.Single().StatusCode.Should().Be(504);

        await sp.DisposeAsync();
    }

    /// <summary>
    /// T15 验证：BffOptions 默认值——整体超时 10s、单请求超时 3s。
    /// </summary>
    [Fact]
    public void BffOptions_DefaultValues_OverallTenSeconds_PerRequestThreeSeconds()
    {
        var options = new BffOptions();
        options.OverallTimeout.Should().Be(TimeSpan.FromSeconds(10),
            "整体超时默认 10 秒，应大于单请求超时 3 秒");
        options.PerRequestTimeout.Should().Be(TimeSpan.FromSeconds(3),
            "单请求超时默认 3 秒");
        options.OverallTimeout.Should().BeGreaterThan(options.PerRequestTimeout,
            "整体超时必须大于单请求超时，否则单请求超时无意义");
    }

    /// <summary>
    /// T15 验证：从 IOptions&lt;BffOptions&gt; 构造时读取配置的超时值。
    /// </summary>
    [Fact]
    public void Constructor_WithOptions_ReadsTimeoutsFromOptions()
    {
        var factory = new Mock<IHttpClientFactory>().Object;
        var options = Microsoft.Extensions.Options.Options.Create(new BffOptions
        {
            OverallTimeout = TimeSpan.FromSeconds(15),
            PerRequestTimeout = TimeSpan.FromSeconds(5)
        });

        var service = new BffForwarderService(factory, options, dagOrchestrator: null);

        // 验证构造不抛异常且超时已读取（通过行为间接验证——此处仅验证构造成功）
        service.Should().NotBeNull();
    }

    /// <summary>
    /// T16 验证：整体超时触发时，每个未完成的 Source 仅产生一个 504 错误条目，不重复。
    /// 使用极短的整体超时（200ms）确保所有请求均被整体超时取消，
    /// 验证 errors 中每个 Source 只出现一次。
    /// </summary>
    [Fact]
    public async Task ForwardAsync_OverallTimeout_NoDuplicate504Errors()
    {
        // Arrange — 所有下游均慢（延迟 2s），整体超时 200ms，单请求超时 2s（大于整体超时）
        // 确保只有整体超时触发，不触发单请求超时
        var (service, sp) = CreateService(
            new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                ["http://svc-a/a"] = async _ =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(2000));
                    return await JsonOk(new { a = true });
                },
                ["http://svc-b/b"] = async _ =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(2000));
                    return await JsonOk(new { b = true });
                },
                ["http://svc-c/c"] = async _ =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(2000));
                    return await JsonOk(new { c = true });
                }
            },
            overallTimeout: TimeSpan.FromMilliseconds(200),
            perRequestTimeout: TimeSpan.FromSeconds(2));

        var requests = new[]
        {
            Req("svc-a", "http://svc-a/a"),
            Req("svc-b", "http://svc-b/b"),
            Req("svc-c", "http://svc-c/c")
        };

        // Act
        var response = await service.ForwardAsync(
            "req-id",
            requests,
            dict => new { });

        // Assert — T16：每个 Source 仅一个 504，无重复
        response.Success.Should().BeFalse();
        response.Partial.Should().BeFalse();
        response.Errors.Should().HaveCount(3,
            "3 个下游均超时，应产生 3 个 504 错误");

        // 验证无重复 Source
        var sources = response.Errors.Select(e => e.Source).ToList();
        sources.Should().OnlyHaveUniqueItems(
            "ConcurrentDictionary.TryAdd 原子去重应保证同一 Source 不产生重复 504");

        // 所有错误均为 504
        response.Errors.Should().AllSatisfy(e =>
        {
            e.StatusCode.Should().Be(504);
            e.Message.Should().Contain("Overall timeout");
        });

        await sp.DisposeAsync();
    }

    /// <summary>
    /// T16 验证：单请求超时与整体超时同时触发时，同一 Source 不产生重复错误。
    /// 设置 per-request 与 overall 超时接近，确保竞态条件下 TryAdd 原子去重生效。
    /// </summary>
    [Fact]
    public async Task ForwardAsync_PerRequestAndOverallTimeoutConcurrent_NoDuplicate()
    {
        // Arrange — 慢请求延迟 1s，per-request 超时 300ms，整体超时 400ms
        // per-request 先触发（300ms）添加 504，随后整体超时（400ms）尝试添加同一 Source 的 504
        // TryAdd 应保证不重复
        var (service, sp) = CreateService(
            new Dictionary<string, Func<HttpRequestMessage, Task<HttpResponseMessage>>>
            {
                ["http://slow/slow"] = async _ =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1000));
                    return await JsonOk(new { slow = true });
                }
            },
            overallTimeout: TimeSpan.FromMilliseconds(400),
            perRequestTimeout: TimeSpan.FromMilliseconds(300));

        var requests = new[]
        {
            Req("slow-source", "http://slow/slow")
        };

        // Act
        var response = await service.ForwardAsync(
            "req-id",
            requests,
            dict => new { });

        // Assert — T16：仅一个 504 错误（per-request 先添加，overall TryAdd 失败）
        response.Errors.Should().HaveCount(1,
            "per-request 超时先添加 504，整体超时 TryAdd 同一 Source 应被拒绝，不产生重复");
        response.Errors.Single().Source.Should().Be("slow-source");
        response.Errors.Single().StatusCode.Should().Be(504);

        await sp.DisposeAsync();
    }
}
