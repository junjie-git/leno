using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SystemAdmin.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

/// <summary>
/// 验证 L-03 修复：<see cref="HttpModuleHealthProbe"/> 超时时间通过配置 <c>HealthProbe:TimeoutSeconds</c> 指定（默认 5 秒）。
/// </summary>
public sealed class HttpModuleHealthProbeTimeoutTests : IDisposable
{
    private readonly HttpMessageHandler _handler;

    public HttpModuleHealthProbeTimeoutTests()
    {
        // 默认使用返回 200 OK 的 handler
        _handler = new StubHttpMessageHandler("{}", HttpStatusCode.OK);
    }

    /// <summary>
    /// 场景：未配置超时，应默认使用 5 秒。
    /// </summary>
    [Fact]
    public void Constructor_With_No_Timeout_Config_Should_Default_To_5_Seconds()
    {
        var configuration = new ConfigurationBuilder().Build();
        var probe = CreateProbe(_handler, configuration);

        Assert.Equal(TimeSpan.FromSeconds(5), probe.ProbeTimeout);
    }

    /// <summary>
    /// 场景：配置超时为 10 秒。
    /// </summary>
    [Fact]
    public void Constructor_With_Timeout_Config_10_Should_Use_10_Seconds()
    {
        var configuration = CreateConfiguration("10");
        var probe = CreateProbe(_handler, configuration);

        Assert.Equal(TimeSpan.FromSeconds(10), probe.ProbeTimeout);
    }

    /// <summary>
    /// 场景：配置超时为 1 秒。
    /// </summary>
    [Fact]
    public void Constructor_With_Timeout_Config_1_Should_Use_1_Second()
    {
        var configuration = CreateConfiguration("1");
        var probe = CreateProbe(_handler, configuration);

        Assert.Equal(TimeSpan.FromSeconds(1), probe.ProbeTimeout);
    }

    /// <summary>
    /// 场景：配置空字符串超时，应回退到默认 5 秒。
    /// </summary>
    [Fact]
    public void Constructor_With_Empty_Timeout_Should_Default_To_5_Seconds()
    {
        var configuration = CreateConfiguration(string.Empty);
        var probe = CreateProbe(_handler, configuration);

        Assert.Equal(TimeSpan.FromSeconds(5), probe.ProbeTimeout);
    }

    /// <summary>
    /// 场景：配置非数字超时，应回退到默认 5 秒。
    /// </summary>
    [Fact]
    public void Constructor_With_NonNumeric_Timeout_Should_Default_To_5_Seconds()
    {
        var configuration = CreateConfiguration("abc");
        var probe = CreateProbe(_handler, configuration);

        Assert.Equal(TimeSpan.FromSeconds(5), probe.ProbeTimeout);
    }

    /// <summary>
    /// 场景：配置 0 秒超时（无效值），应回退到默认 5 秒。
    /// </summary>
    [Fact]
    public void Constructor_With_Zero_Timeout_Should_Default_To_5_Seconds()
    {
        var configuration = CreateConfiguration("0");
        var probe = CreateProbe(_handler, configuration);

        Assert.Equal(TimeSpan.FromSeconds(5), probe.ProbeTimeout);
    }

    /// <summary>
    /// 场景：配置负数超时（无效值），应回退到默认 5 秒。
    /// </summary>
    [Fact]
    public void Constructor_With_Negative_Timeout_Should_Default_To_5_Seconds()
    {
        var configuration = CreateConfiguration("-5");
        var probe = CreateProbe(_handler, configuration);

        Assert.Equal(TimeSpan.FromSeconds(5), probe.ProbeTimeout);
    }

    /// <summary>
    /// 场景：配置 2 秒超时，请求在 1 秒内响应。
    /// 验证：返回 Healthy 状态，响应时间小于 2 秒。
    /// </summary>
    [Fact]
    public async Task ProbeAsync_With_2_Second_Timeout_And_Fast_Response_Should_Return_Healthy()
    {
        var configuration = CreateConfiguration("2");
        var probe = CreateProbe(_handler, configuration);

        var result = await probe.ProbeAsync("http://localhost:8080/health");

        Assert.Equal(ModuleHealthStatus.Healthy, result.Status);
        Assert.True(result.ResponseTimeMs >= 0);
        Assert.True(result.ResponseTimeMs < 2000);
    }

    /// <summary>
    /// 场景：配置 1 秒超时，请求模拟延迟 2 秒。
    /// 验证：返回 Unhealthy 状态，响应时间为 -1，错误信息包含超时时长。
    /// </summary>
    [Fact]
    public async Task ProbeAsync_With_1_Second_Timeout_And_Slow_Response_Should_Return_Unhealthy()
    {
        var slowHandler = new DelayedHttpMessageHandler("{}", HttpStatusCode.OK, TimeSpan.FromSeconds(2));
        var configuration = CreateConfiguration("1");
        var probe = CreateProbe(slowHandler, configuration);

        var result = await probe.ProbeAsync("http://localhost:8080/health");

        Assert.Equal(ModuleHealthStatus.Unhealthy, result.Status);
        Assert.Equal(-1, result.ResponseTimeMs);
        Assert.Contains("1", result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("超时", result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// 场景：配置 5 秒超时（默认），请求返回非 2xx 状态码。
    /// 验证：返回 Degraded 状态。
    /// </summary>
    [Fact]
    public async Task ProbeAsync_With_Default_Timeout_And_NonSuccess_Status_Should_Return_Degraded()
    {
        var errorHandler = new StubHttpMessageHandler("error", HttpStatusCode.InternalServerError);
        var configuration = new ConfigurationBuilder().Build();
        var probe = CreateProbe(errorHandler, configuration);

        var result = await probe.ProbeAsync("http://localhost:8080/health");

        Assert.Equal(ModuleHealthStatus.Degraded, result.Status);
        Assert.Contains("500", result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    private static HttpModuleHealthProbe CreateProbe(HttpMessageHandler handler, IConfiguration configuration)
    {
        var httpClient = new HttpClient(handler);
        return new HttpModuleHealthProbe(
            httpClient,
            configuration,
            NullLogger<HttpModuleHealthProbe>.Instance);
    }

    private static IConfiguration CreateConfiguration(string timeoutSeconds)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HealthProbe:TimeoutSeconds"] = timeoutSeconds
            })
            .Build();
    }

    public void Dispose()
    {
        _handler.Dispose();
    }

    /// <summary>
    /// 简单的 <see cref="HttpMessageHandler"/> 桩，对所有请求返回固定响应。
    /// </summary>
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(string responseBody, HttpStatusCode statusCode)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// 延迟响应的 <see cref="HttpMessageHandler"/> 桩，模拟慢响应以触发超时。
    /// </summary>
    private sealed class DelayedHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;
        private readonly TimeSpan _delay;

        public DelayedHttpMessageHandler(string responseBody, HttpStatusCode statusCode, TimeSpan delay)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(_delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 模拟 HttpClient 超时行为：取消时抛出 OperationCanceledException
                throw;
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
