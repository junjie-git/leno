using System.Diagnostics;
using Leno.ApiGateway.Transforms;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Transforms;

namespace Leno.ApiGateway.Tests.Transforms;

public class TracingTransformTests
{
    private static RequestTransformContext CreateContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        return new RequestTransformContext
        {
            HttpContext = httpContext,
            ProxyRequest = new HttpRequestMessage(HttpMethod.Get, "http://backend.example.com/api"),
            CancellationToken = CancellationToken.None
        };
    }

    [Fact]
    public async Task ApplyAsync_WhenActivityExists_InjectsXTraceIdHeader()
    {
        // Arrange
        var transform = new TracingTransform();
        var context = CreateContext();

        using var activity = new Activity("test-activity")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        // Act
        await transform.ApplyAsync(context);

        // Assert
        context.ProxyRequest.Headers.Contains("X-Trace-Id").Should().BeTrue();
        var traceIdValue = context.ProxyRequest.Headers.GetValues("X-Trace-Id").Single();
        traceIdValue.Should().Be(activity.TraceId.ToString());
    }

    [Fact]
    public async Task ApplyAsync_WhenNoActivity_DoesNotInjectHeader()
    {
        // Arrange — 确保当前无 Activity
        Activity.Current = null;
        var transform = new TracingTransform();
        var context = CreateContext();

        // Act
        await transform.ApplyAsync(context);

        // Assert
        context.ProxyRequest.Headers.Contains("X-Trace-Id").Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_WhenActivityExists_TraceIdIs32CharHex()
    {
        // Arrange
        var transform = new TracingTransform();
        var context = CreateContext();

        using var activity = new Activity("test-activity")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        // Act
        await transform.ApplyAsync(context);

        // Assert — W3C TraceId 为 32 位十六进制小写
        var traceIdValue = context.ProxyRequest.Headers.GetValues("X-Trace-Id").Single();
        traceIdValue.Should().HaveLength(32);
        traceIdValue.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task ApplyAsync_DoesNotOverwriteExistingXTraceIdHeader()
    {
        // Arrange — 上游已设置 X-Trace-Id（罕见但需容错）
        var transform = new TracingTransform();
        var context = CreateContext();
        context.ProxyRequest.Headers.Add("X-Trace-Id", "pre-set-value");

        using var activity = new Activity("test-activity")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        // Act
        await transform.ApplyAsync(context);

        // Assert — 不覆盖既有值
        var values = context.ProxyRequest.Headers.GetValues("X-Trace-Id").ToArray();
        values.Should().Contain("pre-set-value");
    }
}
