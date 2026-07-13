# API 网关增强 - 阶段五：可观测性 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 Leno API 网关补齐"日志、追踪、指标"三大可观测性支柱——基于 Serilog 的结构化访问日志、基于 OpenTelemetry 的全链路追踪、基于 prometheus-net 的实时监控指标，并提供 Jaeger + Prometheus + Grafana 的本地观测栈。

**Architecture:** 网关在请求管道中按"AccessLoggingMiddleware -> Tracing（OTel 自动注入）-> YARP 代理（RequestTransform 显式透传 X-Trace-Id）"顺序串联观测能力。访问日志中间件以结构化 `AccessLogEntry` 记录 10 个标准字段，通过 Serilog 同步输出到 Console(stdout) + File。OpenTelemetry SDK 通过 AspNetCore / Http Instrumentation 自动为入口请求与出站代理请求创建 Span，并由 OTLP Exporter 推送到 Jaeger；自定义 `TracingTransform` 在 YARP 出站请求上显式补一个 `X-Trace-Id` 头供未集成 OTel 的旧服务消费。`GatewayMetricsService` 集中持有 6 个核心指标（Counter/Histogram/Gauge），通过 prometheus-net 在 `/metrics` 端点以 Prometheus 文本格式暴露，供 Prometheus 抓取与 Grafana 可视化。

**Tech Stack:** .NET 10, YARP 2.2.0, Serilog 9.0.0 (AspNetCore + Sinks.Console + Sinks.File), OpenTelemetry 1.10.0 (Extensions.Hosting + Instrumentation.AspNetCore + Instrumentation.Http + Exporter.OpenTelemetryProtocol), prometheus-net 8.2.4 (.AspNetCore), Jaeger 1.55, Prometheus v2.55, Grafana 11.2, xUnit, FluentAssertions, Moq

**Spec:** [docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md](../specs/2026-07-14-api-gateway-enhancement-design.md) 第 6 节（可观测性）

---

## 实施说明

> 本计划为 Spec 第 6 节的 Phase 5 落地。以下三点为对 Spec 字面描述的实现收敛与说明：

1. **traceparent 透传方式**：Spec 6.1 提到 "YARP 的 `RequestTransform` 将 TraceId 透传到后端服务"。实际上 OpenTelemetry .NET 的 `Instrumentation.Http` 会在 YARP 内部 `HttpClient` 发起出站请求时**自动**注入 W3C `traceparent` 头，无需手动设置。本计划的 `TracingTransform` 仍按 Spec 显式实现，但其职责收敛为：在出站请求上额外注入非标准的 `X-Trace-Id` 头（值为当前 `Activity.TraceId`），便于尚未集成 OTel SDK 的旧后端服务从简单头中获取 TraceId 用于日志关联。`traceparent` 标准头由 OTel SDK 自动处理，Transform 不重复设置以避免覆盖。
2. **userId 数据来源**：Spec 6.2 表中 `userId` 字段"验签后填充"。阶段五本身不实现 JWT 验签（属于阶段二），因此 `AccessLoggingMiddleware` 通过 `HttpContext.Items["UserId"]` 与请求头 `X-User-Id` 双通道尽力获取 UserId，两条通道都为空时记录为 `null`。待阶段二 `JwtAuthMiddleware` 落地将 UserId 写入 `HttpContext.Items["UserId"]` 后，本中间件无需改动即可自动填充。
3. **前置依赖**：本计划假设阶段一已落地（`Leno.ApiGateway.Tests` 测试项目与 `Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs` 已存在）。若阶段一未实施，请先执行阶段一 Task 2 Step 6-7 创建测试项目骨架与全局 using，再开始本计划。集成代码（Task 4）以当前 `Program.cs`（手工健康轮询版）为基线编写，如阶段一已替换 Program.cs，则将本计划新增的注册语句合并入阶段一版本的 Program.cs。

---

## 文件结构

### 新建文件

| 文件 | 职责 |
|---|---|
| `src/ApiGateway/Leno.ApiGateway/Models/AccessLogEntry.cs` | 访问日志结构化数据载体（10 个标准字段） |
| `src/ApiGateway/Leno.ApiGateway/Middleware/AccessLoggingMiddleware.cs` | 统一访问日志中间件，捕获请求元数据并经 Serilog 输出 |
| `src/ApiGateway/Leno.ApiGateway/Transforms/TracingTransform.cs` | YARP `RequestTransform`，在出站请求上注入 `X-Trace-Id` 头 |
| `src/ApiGateway/Leno.ApiGateway/Services/GatewayMetricsService.cs` | 6 个核心 Prometheus 指标的持有与记录服务 |
| `src/ApiGateway/Leno.ApiGateway/Options/ObservabilityOptions.cs` | 可观测性配置选项（OTLP / Metrics / Serilog 子节） |
| `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/AccessLoggingMiddlewareTests.cs` | 访问日志中间件单元测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Transforms/TracingTransformTests.cs` | 追踪 Transform 单元测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Services/GatewayMetricsServiceTests.cs` | 指标服务单元测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Integration/ObservabilityIntegrationTests.cs` | 端到端集成测试（/metrics 端点、访问日志、中间件管道） |
| `grafana/leno-gateway-dashboard.json` | 预置 Grafana Dashboard 模板 |
| `grafana/provisioning/datasources/prometheus.yml` | Grafana 数据源自动配置（Prometheus） |
| `grafana/provisioning/dashboards/leno.yml` | Grafana Dashboard 自动加载配置 |

### 修改文件

| 文件 | 修改内容 |
|---|---|
| `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj` | 添加 Serilog / OpenTelemetry / prometheus-net NuGet 包引用 |
| `src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs` | 追加 `AddObservability` 方法（注册 Serilog + OTel + Metrics + Transforms） |
| `src/ApiGateway/Leno.ApiGateway/Program.cs` | 调用 `AddObservability`，注册中间件管道与 `/metrics` 端点，使用 Serilog 替换默认日志 |
| `src/ApiGateway/Leno.ApiGateway/appsettings.json` | 添加 `Serilog` / `OpenTelemetry` / `Metrics` 配置节 |
| `src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json` | OTLP Endpoint 指向 `http://jaeger:4317`，Serilog File 路径调整 |
| `docker-compose.yml` | 添加 Jaeger、Prometheus、Grafana 服务及配置挂载 |

---

## Task 1: Serilog 结构化访问日志

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
- Create: `src/ApiGateway/Leno.ApiGateway/Models/AccessLogEntry.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Middleware/AccessLoggingMiddleware.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/AccessLoggingMiddlewareTests.cs`

- [ ] **Step 1: 添加 Serilog NuGet 包到网关项目**

在 `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj` 的 `<ItemGroup>`（`AspNetCore.HealthChecks.Uris` 之后、`<ProjectReference>` 之前）添加：

```xml
    <PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
```

- [ ] **Step 2: 验证包还原**

Run: `dotnet restore src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Restore completed` 无错误

- [ ] **Step 3: 创建 AccessLogEntry.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Models/AccessLogEntry.cs`：

```csharp
using System.Text.Json.Serialization;

namespace Leno.ApiGateway.Models;

/// <summary>
/// 统一访问日志结构化数据载体，对应 Spec 6.2 中定义的 10 个标准字段。
/// 经 Serilog 以 JSON 文档形式输出到 Console(stdout) 与 File。
/// </summary>
public sealed record AccessLogEntry
{
    /// <summary>请求时间（UTC，ISO 8601）。</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>分布式追踪 TraceId（关联 OpenTelemetry Span）。</summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    /// <summary>HTTP 方法（GET/POST/...）。</summary>
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    /// <summary>请求路径（不含 QueryString）。</summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>响应状态码。</summary>
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; init; }

    /// <summary>请求耗时（毫秒）。</summary>
    [JsonPropertyName("duration")]
    public long Duration { get; init; }

    /// <summary>客户端 IP（优先取 X-Forwarded-For）。</summary>
    [JsonPropertyName("clientIp")]
    public string? ClientIp { get; init; }

    /// <summary>用户 ID（来自 HttpContext.Items["UserId"] 或 X-User-Id 头）。</summary>
    [JsonPropertyName("userId")]
    public string? UserId { get; init; }

    /// <summary>目标微服务（YARP ClusterId）。</summary>
    [JsonPropertyName("targetService")]
    public string? TargetService { get; init; }

    /// <summary>客户端 User-Agent。</summary>
    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; init; }
}
```

- [ ] **Step 4: 编写 AccessLoggingMiddleware 失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/AccessLoggingMiddlewareTests.cs`：

```csharp
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;

namespace Leno.ApiGateway.Tests.Middleware;

/// <summary>
/// 轻量级测试用 Logger，捕获 <see cref="ILogger.Log{TState}"/> 调用以便断言。
/// </summary>
internal sealed class CapturingLogger<TCategory> : ILogger<TCategory>
{
    public List<(LogLevel Level, string Message, object? State)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception), state));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}

public class AccessLoggingMiddlewareTests
{
    private static DefaultHttpContext CreateContext(string method, string path, string? userIdHeader = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Headers.UserAgent = "TestAgent/1.0";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        if (userIdHeader is not null)
        {
            context.Request.Headers["X-User-Id"] = userIdHeader;
        }

        return context;
    }

    [Fact]
    public async Task InvokeAsync_LogsEntryWithExpectedFields()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        var capturedStatusCode = 200;

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = capturedStatusCode;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("POST", "/api/order/create", userIdHeader: "12345");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        logger.Entries.Should().ContainSingle();
        var (level, _, state) = logger.Entries[0];
        level.Should().Be(LogLevel.Information);
        state.Should().BeOfType<AccessLogEntry>();

        var entry = (AccessLogEntry)state!;
        entry.Method.Should().Be("POST");
        entry.Path.Should().Be("/api/order/create");
        entry.StatusCode.Should().Be(200);
        entry.Duration.Should().BeGreaterThanOrEqualTo(0);
        entry.ClientIp.Should().Be("192.168.1.100");
        entry.UserId.Should().Be("12345");
        entry.UserAgent.Should().Be("TestAgent/1.0");
        entry.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InvokeAsync_ReadsUserIdFromHttpContextItemsWhenHeaderAbsent()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        RequestDelegate next = ctx =>
        {
            ctx.Items["UserId"] = "67890";
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("GET", "/api/products/1");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = (AccessLogEntry)logger.Entries[0].State!;
        entry.UserId.Should().Be("67890");
    }

    [Fact]
    public async Task InvokeAsync_UserIdIsNullWhenNoSourceAvailable()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 404;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("GET", "/health/live");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = (AccessLogEntry)logger.Entries[0].State!;
        entry.UserId.Should().BeNull();
        entry.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task InvokeAsync_CapturesTargetServiceFromReverseProxyFeature()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();

        // 模拟 YARP 在管道中设置的 IReverseProxyFeature
        var reverseProxyFeature = new Mock<IReverseProxyFeature>();
        reverseProxyFeature.SetupGet(f => f.Cluster)
            .Returns(new ClusterState("order-api")
            {
                Model = new ClusterModel(
                    new Yarp.ReverseProxy.Configuration.ClusterConfig { ClusterId = "order-api" },
                    new Yarp.ReverseProxy.Configuration.ClusterConfig())
            });

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("POST", "/api/order/create");
        context.Features.Set(reverseProxyFeature.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = (AccessLogEntry)logger.Entries[0].State!;
        entry.TargetService.Should().Be("order-api");
    }

    [Fact]
    public async Task InvokeAsync_PropagatesExceptionFromNextAndStillLogs()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        RequestDelegate next = _ => throw new InvalidOperationException("downstream failure");

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("GET", "/api/products/1");

        // Act
        var act = async () => await middleware.InvokeAsync(context);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        // 即使下游抛异常，访问日志仍应记录（状态码默认 500）
        logger.Entries.Should().ContainSingle();
        var entry = (AccessLogEntry)logger.Entries[0].State!;
        entry.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task InvokeAsync_CapturesTraceIdFromCurrentActivity()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("GET", "/api/products/1");

        using var activity = new System.Diagnostics.Activity("test-activity")
            .SetIdFormat(System.Diagnostics.ActivityIdFormat.W3C)
            .Start();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = (AccessLogEntry)logger.Entries[0].State!;
        entry.TraceId.Should().Be(activity.TraceId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_PrefersXForwardedForOverRemoteIpAddress()
    {
        // Arrange
        var logger = new CapturingLogger<AccessLoggingMiddleware>();
        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new AccessLoggingMiddleware(next, logger);
        var context = CreateContext("GET", "/api/products/1");
        context.Request.Headers["X-Forwarded-For"] = "10.0.0.99, 192.168.1.1";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var entry = (AccessLogEntry)logger.Entries[0].State!;
        entry.ClientIp.Should().Be("10.0.0.99");
    }
}
```

- [ ] **Step 5: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "AccessLoggingMiddlewareTests"`
Expected: 编译失败 — `AccessLoggingMiddleware` 类型未定义

- [ ] **Step 6: 创建 AccessLoggingMiddleware.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Middleware/AccessLoggingMiddleware.cs`：

```csharp
using System.Diagnostics;
using Leno.ApiGateway.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Model;

namespace Leno.ApiGateway.Middleware;

/// <summary>
/// 统一访问日志中间件。
/// <para>
/// 在请求管道中包装 <c>next</c>，捕获请求进入与响应返回的元数据，
/// 构造 <see cref="AccessLogEntry"/> 后通过 Serilog 输出结构化 JSON 日志。
/// 字段符合 Spec 6.2 定义：timestamp/traceId/method/path/statusCode/duration/clientIp/userId/targetService/userAgent。
/// </para>
/// </summary>
public sealed class AccessLoggingMiddleware
{
    private const string UserIdItemsKey = "UserId";
    private const string UserIdHeader = "X-User-Id";
    private const string ForwardedForHeader = "X-Forwarded-For";

    private readonly RequestDelegate _next;
    private readonly ILogger<AccessLoggingMiddleware> _logger;

    public AccessLoggingMiddleware(RequestDelegate next, ILogger<AccessLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stopwatch = Stopwatch.StartNew();
        var timestamp = DateTimeOffset.UtcNow;
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var clientIp = ResolveClientIp(context);
        var traceId = Activity.Current?.TraceId.ToString();

        int statusCode;
        try
        {
            await _next(context);
            statusCode = context.Response.StatusCode;
        }
        catch
        {
            // 下游抛异常时仍记录访问日志（状态码记为 500），异常继续向上抛出
            stopwatch.Stop();
            LogAccess(timestamp, traceId, method, path, 500, stopwatch.ElapsedMilliseconds,
                clientIp, ResolveUserId(context), ResolveTargetService(context), userAgent);
            throw;
        }

        stopwatch.Stop();
        LogAccess(timestamp, traceId, method, path, statusCode, stopwatch.ElapsedMilliseconds,
            clientIp, ResolveUserId(context), ResolveTargetService(context), userAgent);
    }

    private void LogAccess(
        DateTimeOffset timestamp,
        string? traceId,
        string method,
        string path,
        int statusCode,
        long duration,
        string? clientIp,
        string? userId,
        string? targetService,
        string? userAgent)
    {
        var entry = new AccessLogEntry
        {
            Timestamp = timestamp,
            TraceId = traceId,
            Method = method,
            Path = path,
            StatusCode = statusCode,
            Duration = duration,
            ClientIp = clientIp,
            UserId = userId,
            TargetService = targetService,
            UserAgent = userAgent
        };

        _logger.LogInformation("{@AccessLog}", entry);
    }

    private static string? ResolveClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers[ForwardedForHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // X-Forwarded-For 可能是 "client, proxy1, proxy2" 形式，取第一个
            var first = forwardedFor.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (first.Length > 0)
            {
                return first[0];
            }
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static string? ResolveUserId(HttpContext context)
    {
        // 优先：HttpContext.Items["UserId"]（由 JwtAuthMiddleware 写入，阶段二实现）
        if (context.Items.TryGetValue(UserIdItemsKey, out var itemValue) && itemValue is string itemUserId)
        {
            return itemUserId;
        }

        // 兜底：直接读 X-User-Id 头（由 YARP UserContextTransform 注入）
        var headerValue = context.Request.Headers[UserIdHeader].FirstOrDefault();
        return string.IsNullOrWhiteSpace(headerValue) ? null : headerValue;
    }

    private static string? ResolveTargetService(HttpContext context)
    {
        // YARP 的 IReverseProxyFeature 在 YARP 管道执行后才会填充，
        // 此处容错读取——未路由到 YARP 时返回 null
        var feature = context.Features.Get<IReverseProxyFeature>();
        return feature?.Cluster?.Model?.Config?.ClusterId;
    }
}
```

- [ ] **Step 7: 运行测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "AccessLoggingMiddlewareTests"`
Expected: `Passed: 7` — 7 个测试全部通过

> **说明：** 若 `InvokeAsync_CapturesTargetServiceFromReverseProxyFeature` 因 `ClusterModel` 构造函数可见性受限失败，可改为通过 `feature.Cluster.Config.ClusterId` 读取；YARP 2.2.0 中 `ClusterState.Config` 为公开属性，类型为 `ClusterConfig`，可直接读取 `ClusterId`。若仍失败，将测试中 `ClusterModel` 构造改为只设置 `ClusterState.Config` 属性。

- [ ] **Step 8: 验证网关项目编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 9: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj src/ApiGateway/Leno.ApiGateway/Models/AccessLogEntry.cs src/ApiGateway/Leno.ApiGateway/Middleware/AccessLoggingMiddleware.cs src/ApiGateway/Leno.ApiGateway.Tests/Middleware/AccessLoggingMiddlewareTests.cs
git commit -m "feat(gateway): 添加 Serilog 结构化访问日志中间件"
```

---

## Task 2: OpenTelemetry 分布式追踪

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
- Create: `src/ApiGateway/Leno.ApiGateway/Transforms/TracingTransform.cs`
- Modify: `docker-compose.yml`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Transforms/TracingTransformTests.cs`

- [ ] **Step 1: 添加 OpenTelemetry NuGet 包到网关项目**

在 `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj` 的 Serilog 包引用之后添加：

```xml
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.10.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.10.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.10.0" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.10.0" />
```

- [ ] **Step 2: 验证包还原**

Run: `dotnet restore src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Restore completed` 无错误

- [ ] **Step 3: 编写 TracingTransform 失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Transforms/TracingTransformTests.cs`：

```csharp
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
```

- [ ] **Step 4: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "TracingTransformTests"`
Expected: 编译失败 — `TracingTransform` 类型未定义

- [ ] **Step 5: 创建 TracingTransform.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Transforms/TracingTransform.cs`：

```csharp
using System.Diagnostics;
using Yarp.ReverseProxy.Transforms;

namespace Leno.ApiGateway.Transforms;

/// <summary>
/// YARP 出站请求 Transform，在转发到后端微服务的请求上注入非标准的
/// <c>X-Trace-Id</c> 头（值为当前 <see cref="Activity.TraceId"/>）。
/// <para>
/// W3C 标准 <c>traceparent</c> 头由 OpenTelemetry 的 Http Instrumentation
/// 在 YARP 内部 HttpClient 发起出站请求时自动注入，本 Transform 不重复设置。
/// <c>X-Trace-Id</c> 仅为尚未集成 OTel SDK 的旧后端服务提供 TraceId 关联能力。
/// </para>
/// </summary>
public sealed class TracingTransform : RequestTransform
{
    private const string XTraceIdHeader = "X-Trace-Id";

    /// <summary>
    /// 在 YARP 构造出站 <see cref="HttpRequestMessage"/> 时调用，
    /// 若当前存在 <see cref="Activity"/> 且未已设置 <c>X-Trace-Id</c> 头，则注入 TraceId。
    /// </summary>
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var activity = Activity.Current;
        if (activity is null)
        {
            return ValueTask.CompletedTask;
        }

        // 不覆盖既有值（上游可能已显式设置）
        if (context.ProxyRequest.Headers.Contains(XTraceIdHeader))
        {
            return ValueTask.CompletedTask;
        }

        var traceId = activity.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
        {
            context.ProxyRequest.Headers.TryAddWithoutValidation(XTraceIdHeader, traceId);
        }

        return ValueTask.CompletedTask;
    }
}
```

- [ ] **Step 6: 运行测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "TracingTransformTests"`
Expected: `Passed: 4` — 4 个测试全部通过

- [ ] **Step 7: 在 docker-compose.yml 中添加 Jaeger 服务**

在 `docker-compose.yml` 的 `elasticsearch` 服务之后、`user-auth-api` 服务之前插入 Jaeger 服务：

```yaml
  jaeger:
    image: jaegertracing/all-in-one:1.55
    container_name: leno-jaeger
    environment:
      - COLLECTOR_OTLP_ENABLED=true
    ports:
      - "4317:4317"   # OTLP gRPC
      - "4318:4318"   # OTLP HTTP
      - "16686:16686" # Jaeger UI
    healthcheck:
      test: ["CMD-SHELL", "wget --spider -q http://localhost:14269/ || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 10
    networks:
      - leno-net
```

- [ ] **Step 8: 为 api-gateway 添加 jaeger 依赖**

在 `docker-compose.yml` 的 `api-gateway` 服务的 `depends_on` 块末尾添加：

```yaml
      jaeger:
        condition: service_healthy
```

> 注意：若阶段一已为 `api-gateway` 添加 `consul` 依赖，则将 `jaeger` 依赖追加在 `consul` 之后。

- [ ] **Step 9: 验证 docker-compose 配置**

Run: `docker compose config --quiet`
Expected: 无输出（退出码 0）

- [ ] **Step 10: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 11: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj src/ApiGateway/Leno.ApiGateway/Transforms/TracingTransform.cs src/ApiGateway/Leno.ApiGateway.Tests/Transforms/TracingTransformTests.cs docker-compose.yml
git commit -m "feat(gateway): 添加 OpenTelemetry 追踪 Transform 与 Jaeger 容器"
```

---

## Task 3: Prometheus 实时监控指标

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
- Create: `src/ApiGateway/Leno.ApiGateway/Services/GatewayMetricsService.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Services/GatewayMetricsServiceTests.cs`

- [ ] **Step 1: 添加 prometheus-net NuGet 包到网关项目**

在 `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj` 的 OpenTelemetry 包引用之后添加：

```xml
    <PackageReference Include="prometheus-net.AspNetCore" Version="8.2.4" />
```

- [ ] **Step 2: 验证包还原**

Run: `dotnet restore src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Restore completed` 无错误

- [ ] **Step 3: 编写 GatewayMetricsService 失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Services/GatewayMetricsServiceTests.cs`：

```csharp
using Leno.ApiGateway.Services;
using Prometheus;

namespace Leno.ApiGateway.Tests.Services;

public class GatewayMetricsServiceTests : IDisposable
{
    private readonly CollectorRegistry _registry;
    private readonly GatewayMetricsService _service;

    public GatewayMetricsServiceTests()
    {
        // 每个测试使用独立的 CollectorRegistry，避免全局注册冲突
        _registry = new CollectorRegistry();
        _service = new GatewayMetricsService(_registry);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void RecordRequest_IncrementsRequestsTotalCounter()
    {
        // Act
        _service.RecordRequest(route: "product", method: "GET", statusCode: 200);

        // Assert
        var counter = _registry.GetSingleValue("gateway_requests_total",
            "route", "product", "method", "GET", "status_code", "200");
        counter.Should().Be(1);
    }

    [Fact]
    public void RecordRequest_MultipleTimes_AccumulatesCount()
    {
        // Act
        _service.RecordRequest("order", "POST", 201);
        _service.RecordRequest("order", "POST", 201);
        _service.RecordRequest("order", "POST", 500);

        // Assert
        var successCount = _registry.GetSingleValue("gateway_requests_total",
            "route", "order", "method", "POST", "status_code", "201");
        successCount.Should().Be(2);

        var errorCount = _registry.GetSingleValue("gateway_requests_total",
            "route", "order", "method", "POST", "status_code", "500");
        errorCount.Should().Be(1);
    }

    [Fact]
    public void RecordRequestDuration_ObservesHistogram()
    {
        // Act
        _service.RecordRequestDuration(route: "product", method: "GET", durationMs: 125);

        // Assert — Histogram 的 _count 应为 1
        var count = _registry.GetSingleValue("gateway_request_duration_count",
            "route", "product", "method", "GET");
        count.Should().Be(1);

        var sum = _registry.GetSingleValue("gateway_request_duration_sum",
            "route", "product", "method", "GET");
        sum.Should().Be(125);
    }

    [Fact]
    public void IncrementActiveRequests_IncrementsGauge()
    {
        // Act
        _service.IncrementActiveRequests();
        _service.IncrementActiveRequests();

        // Assert
        var value = _registry.GetSingleValue("gateway_active_requests");
        value.Should().Be(2);
    }

    [Fact]
    public void DecrementActiveRequests_DecrementsGauge()
    {
        // Arrange
        _service.IncrementActiveRequests();
        _service.IncrementActiveRequests();

        // Act
        _service.DecrementActiveRequests();

        // Assert
        var value = _registry.GetSingleValue("gateway_active_requests");
        value.Should().Be(1);
    }

    [Fact]
    public void SetCircuitBreakerState_UpdatesGaugeValue()
    {
        // Act
        _service.SetCircuitBreakerState(cluster: "order", isOpen: true);

        // Assert — open=1
        var openValue = _registry.GetSingleValue("gateway_circuit_breaker_state",
            "cluster", "order");
        openValue.Should().Be(1);

        // Act — 恢复 closed
        _service.SetCircuitBreakerState("order", isOpen: false);

        // Assert — closed=0
        var closedValue = _registry.GetSingleValue("gateway_circuit_breaker_state",
            "cluster", "order");
        closedValue.Should().Be(0);
    }

    [Fact]
    public void RecordRateLimitRejection_IncrementsCounter()
    {
        // Act
        _service.RecordRateLimitRejection(route: "seckill", policy: "seckill-policy");
        _service.RecordRateLimitRejection(route: "seckill", policy: "seckill-policy");

        // Assert
        var value = _registry.GetSingleValue("gateway_rate_limit_rejected",
            "route", "seckill", "policy", "seckill-policy");
        value.Should().Be(2);
    }

    [Fact]
    public void RecordBlacklistHit_IncrementsCounter()
    {
        // Act
        _service.RecordBlacklistHit();
        _service.RecordBlacklistHit();
        _service.RecordBlacklistHit();

        // Assert
        var value = _registry.GetSingleValue("gateway_blacklist_hits");
        value.Should().Be(3);
    }

    [Fact]
    public void RecordRequest_WithNullRoute_UsesEmptyString()
    {
        // Act — 健康检查等未路由到 YARP 的请求 route 为 null
        _service.RecordRequest(route: null, method: "GET", statusCode: 200);

        // Assert
        var value = _registry.GetSingleValue("gateway_requests_total",
            "route", "", "method", "GET", "status_code", "200");
        value.Should().Be(1);
    }
}

internal static class CollectorRegistryExtensions
{
    /// <summary>
    /// 从 CollectorRegistry 中读取指定指标 + 标签组合的当前值（适用于 Counter/Gauge/Histogram 的 _count/_sum）。
    /// </summary>
    public static double GetSingleValue(
        this CollectorRegistry registry,
        string metricName,
        params string[] labelValues)
    {
        using var stream = new MemoryStream();
        registry.CollectAndExportAsText(stream);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();

        var labelPart = labelValues.Length > 0
            ? "{" + string.Join(",", Enumerable.Range(0, labelValues.Length / 2)
                .Select(i => $"{labelValues[i * 2]}=\"{labelValues[i * 2 + 1]}\"")) + "}"
            : string.Empty;

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith($"{metricName}{labelPart} "))
            {
                var valueStr = line.Substring(line.LastIndexOf(' ') + 1).Trim();
                return double.Parse(valueStr, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidOperationException(
            $"Metric {metricName}{labelPart} not found in registry output. Lines:\n{text}");
    }
}
```

- [ ] **Step 4: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "GatewayMetricsServiceTests"`
Expected: 编译失败 — `GatewayMetricsService` 类型未定义

- [ ] **Step 5: 创建 GatewayMetricsService.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Services/GatewayMetricsService.cs`：

```csharp
using Prometheus;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 网关核心 Prometheus 指标服务，集中持有 Spec 6.3 定义的 6 个指标。
/// <para>
/// 通过 <see cref="CollectorRegistry"/> 隔离指标注册，便于单元测试使用独立注册表。
/// 生产环境使用 <c>Metrics.DefaultRegistry</c>（默认构造）。
/// </para>
/// </summary>
public sealed class GatewayMetricsService
{
    private readonly Counter _requestsTotal;
    private readonly Histogram _requestDuration;
    private readonly Gauge _activeRequests;
    private readonly Gauge _circuitBreakerState;
    private readonly Counter _rateLimitRejected;
    private readonly Counter _blacklistHits;

    /// <summary>
    /// 使用默认全局注册表创建实例（生产环境使用）。
    /// </summary>
    public GatewayMetricsService() : this(Metrics.DefaultRegistry)
    {
    }

    /// <summary>
    /// 使用指定注册表创建实例（单元测试使用独立注册表）。
    /// </summary>
    public GatewayMetricsService(CollectorRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var factory = Metrics.WithCustomRegistry(registry);

        _requestsTotal = factory.CreateCounter(
            "gateway_requests_total",
            "Total number of HTTP requests processed by the gateway.",
            "route", "method", "status_code");

        _requestDuration = factory.CreateHistogram(
            "gateway_request_duration",
            "HTTP request processing duration in milliseconds.",
            "route", "method",
            new HistogramConfiguration
            {
                Buckets = new[]
                {
                    5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000
                }
            });

        _activeRequests = factory.CreateGauge(
            "gateway_active_requests",
            "Current number of in-flight requests being processed by the gateway.");

        _circuitBreakerState = factory.CreateGauge(
            "gateway_circuit_breaker_state",
            "Circuit breaker state per cluster (0=closed, 1=open).",
            "cluster");

        _rateLimitRejected = factory.CreateCounter(
            "gateway_rate_limit_rejected",
            "Number of requests rejected by rate limiting.",
            "route", "policy");

        _blacklistHits = factory.CreateCounter(
            "gateway_blacklist_hits",
            "Number of requests rejected because the JWT was on the blacklist.");
    }

    /// <summary>记录一次完整请求（响应已返回）。</summary>
    public void RecordRequest(string? route, string method, int statusCode)
    {
        _requestsTotal.WithLabels(route ?? string.Empty, method, statusCode.ToString()).Inc();
    }

    /// <summary>记录请求耗时分布。</summary>
    public void RecordRequestDuration(string? route, string method, double durationMs)
    {
        _requestDuration.WithLabels(route ?? string.Empty, method).Observe(durationMs);
    }

    /// <summary>请求进入管道时调用，活跃请求数 +1。</summary>
    public void IncrementActiveRequests()
    {
        _activeRequests.Inc();
    }

    /// <summary>请求离开管道时调用，活跃请求数 -1。</summary>
    public void DecrementActiveRequests()
    {
        _activeRequests.Dec();
    }

    /// <summary>更新指定 Cluster 的熔断器状态（0=closed, 1=open）。</summary>
    public void SetCircuitBreakerState(string cluster, bool isOpen)
    {
        _circuitBreakerState.WithLabels(cluster).Set(isOpen ? 1 : 0);
    }

    /// <summary>记录一次限流拒绝事件。</summary>
    public void RecordRateLimitRejection(string? route, string policy)
    {
        _rateLimitRejected.WithLabels(route ?? string.Empty, policy).Inc();
    }

    /// <summary>记录一次黑名单命中事件。</summary>
    public void RecordBlacklistHit()
    {
        _blacklistHits.Inc();
    }
}
```

- [ ] **Step 6: 运行测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "GatewayMetricsServiceTests"`
Expected: `Passed: 9` — 9 个测试全部通过

- [ ] **Step 7: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 8: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj src/ApiGateway/Leno.ApiGateway/Services/GatewayMetricsService.cs src/ApiGateway/Leno.ApiGateway.Tests/Services/GatewayMetricsServiceTests.cs
git commit -m "feat(gateway): 添加 Prometheus 6 核心指标服务"
```

---

## Task 4: 网关 Program.cs 集成可观测性

**Files:**
- Create: `src/ApiGateway/Leno.ApiGateway/Options/ObservabilityOptions.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/Program.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.json`
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Integration/ObservabilityIntegrationTests.cs`

- [ ] **Step 1: 创建 ObservabilityOptions.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Options/ObservabilityOptions.cs`：

```csharp
namespace Leno.ApiGateway.Options;

/// <summary>
/// 可观测性顶层配置节，对应 appsettings.json 中 <c>OpenTelemetry</c> 与 <c>Metrics</c> 节。
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>OpenTelemetry 配置节。</summary>
    public OpenTelemetryOptions OpenTelemetry { get; set; } = new();

    /// <summary>Prometheus 指标暴露配置节。</summary>
    public MetricsOptions Metrics { get; set; } = new();
}

/// <summary>
/// OpenTelemetry 追踪导出配置。
/// </summary>
public sealed class OpenTelemetryOptions
{
    /// <summary>是否启用 OTel 追踪导出。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Exporter 类型：otlp（默认）或 none。</summary>
    public string Exporter { get; set; } = "otlp";

    /// <summary>OTLP gRPC 端点（如 http://localhost:4317）。</summary>
    public string Endpoint { get; set; } = "http://localhost:4317";

    /// <summary>ServiceName 标识，用于在 Jaeger 中区分服务。</summary>
    public string ServiceName { get; set; } = "leno-api-gateway";
}

/// <summary>
/// Prometheus 指标暴露配置。
/// </summary>
public sealed class MetricsOptions
{
    /// <summary>是否启用 /metrics 端点。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>指标暴露路径。</summary>
    public string Path { get; set; } = "/metrics";
}
```

- [ ] **Step 2: 编写可观测性集成失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Integration/ObservabilityIntegrationTests.cs`：

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.ApiGateway.Tests.Integration;

public class ObservabilityIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ObservabilityIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OpenTelemetry:Enabled"] = "false", // 测试环境禁用真实 OTLP 导出
                    ["Metrics:Enabled"] = "true",
                    ["Metrics:Path"] = "/metrics"
                });
            });
        }).CreateClient();
    }

    [Fact]
    public async Task MetricsEndpoint_ReturnsOkAndPrometheusFormat()
    {
        // Act
        var response = await _client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("# HELP gateway_");
    }

    [Fact]
    public async Task HealthLiveEndpoint_RemainsAccessibleAfterObservabilityRegistration()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MetricsEndpoint_AfterRequest_RecordsRequestsTotal()
    {
        // Arrange — 先发一个请求触发计数
        await _client.GetAsync("/health/live");

        // Act
        var metricsResponse = await _client.GetAsync("/metrics");
        var content = await metricsResponse.Content.ReadAsStringAsync();

        // Assert — /health/live 请求应被记录到 gateway_requests_total
        content.Should().Contain("gateway_requests_total");
    }
}

public class ServiceCollectionExtensionsObservabilityTests
{
    [Fact]
    public void AddObservability_RegistersMetricsServiceAndTransform()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:Enabled"] = "false",
                ["OpenTelemetry:Exporter"] = "otlp",
                ["OpenTelemetry:Endpoint"] = "http://localhost:4317",
                ["OpenTelemetry:ServiceName"] = "leno-api-gateway",
                ["Metrics:Enabled"] = "true",
                ["Metrics:Path"] = "/metrics"
            })
            .Build();

        // Act
        services.AddLogging();
        services.AddObservability(config);

        // Assert
        var sp = services.BuildServiceProvider();
        sp.GetService<Leno.ApiGateway.Services.GatewayMetricsService>().Should().NotBeNull();
    }

    [Fact]
    public void AddObservability_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var config = new ConfigurationBuilder().Build();

        var act = () => services.AddObservability(config);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddObservability_NullConfig_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddObservability(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
```

- [ ] **Step 3: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "ObservabilityIntegrationTests|ServiceCollectionExtensionsObservabilityTests"`
Expected: 编译失败 — `AddObservability` 方法未定义

- [ ] **Step 4: 在 ServiceCollectionExtensions.cs 中追加 AddObservability 方法**

在 `src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs` 文件顶部 `using` 区追加：

```csharp
using Leno.ApiGateway.Middleware;
using Leno.ApiGateway.Models;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Leno.ApiGateway.Transforms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using Serilog;
using Serilog.Events;
using Yarp.ReverseProxy.Transforms;
```

> 若文件中已有部分 using（如 `Leno.ApiGateway.Options`、`Leno.ApiGateway.Services`），保留不重复添加。

在 `ServiceCollectionExtensions` 类末尾（最后一个 `}` 之前）追加以下方法：

```csharp
    /// <summary>
    /// 注册可观测性三件套：Serilog 结构化日志、OpenTelemetry 分布式追踪、prometheus-net 指标。
    /// 同时注册 <see cref="TracingTransform"/> 到 YARP Transform 管道，并暴露 <see cref="GatewayMetricsService"/> 单例。
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<OpenTelemetryOptions>(configuration.GetSection("OpenTelemetry"));
        services.Configure<MetricsOptions>(configuration.GetSection("Metrics"));

        // ===== Serilog =====
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration, sectionName: "Serilog")
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", "leno-api-gateway")
            .CreateLogger();

        services.AddSerilog(Log.Logger, dispose: true);

        // ===== OpenTelemetry =====
        var otelEnabled = configuration.GetValue("OpenTelemetry:Enabled", true);
        if (otelEnabled)
        {
            var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "leno-api-gateway";
            var exporter = configuration["OpenTelemetry:Exporter"] ?? "otlp";
            var endpoint = configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";

            var otelBuilder = services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(serviceName))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/metrics")
                                          && !ctx.Request.Path.StartsWithSegments("/health");
                    })
                    .AddHttpClientInstrumentation()
                    .AddSource("Yarp.ReverseProxy"));

            if (exporter.Equals("otlp", StringComparison.OrdinalIgnoreCase))
            {
                otelBuilder.UseOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(endpoint);
                });
            }
        }

        // ===== Prometheus Metrics =====
        services.AddSingleton<GatewayMetricsService>();

        // ===== YARP Tracing Transform =====
        services.AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"))
            .AddTransforms<TracingTransform>();

        return services;
    }

    /// <summary>
    /// 注册可观测性中间件管道。调用顺序：
    /// 1. <see cref="GatewayMetricsService.IncrementActiveRequests"/> (请求进入)
    /// 2. <see cref="AccessLoggingMiddleware"/>
    /// 3. 下游中间件 / YARP
    /// 4. <see cref="GatewayMetricsService.RecordRequest"/> + RecordRequestDuration + DecrementActiveRequests
    /// </summary>
    public static IApplicationBuilder UseObservability(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        var metricsEnabled = configuration.GetValue("Metrics:Enabled", true);
        var metricsPath = configuration["Metrics:Path"] ?? "/metrics";

        // 访问日志中间件（最早记录请求元数据）
        app.UseMiddleware<AccessLoggingMiddleware>();

        // 指标中间件（包装活跃请求数与请求耗时计数）
        if (metricsEnabled)
        {
            app.Use(async (context, next) =>
            {
                var metrics = context.RequestServices.GetRequiredService<GatewayMetricsService>();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                metrics.IncrementActiveRequests();

                try
                {
                    await next();
                }
                finally
                {
                    stopwatch.Stop();
                    var route = context.Features.Get<Yarp.ReverseProxy.Model.IReverseProxyFeature>()
                        ?.Cluster?.Model?.Config?.ClusterId;
                    metrics.RecordRequestDuration(route, context.Request.Method, stopwatch.Elapsed.TotalMilliseconds);
                    metrics.RecordRequest(route, context.Request.Method, context.Response.StatusCode);
                    metrics.DecrementActiveRequests();
                }
            });

            // /metrics 端点（在 YARP 之前注册，避免被代理）
            app.Map(metricsPath, builder => builder.UseHttpMetrics());
        }

        return app;
    }
```

> **重要说明：** 上述 `AddObservability` 中调用了 `AddReverseProxy().LoadFromConfig().AddTransforms<TracingTransform>()`。若 `Program.cs` 中原本已有 `AddReverseProxy().LoadFromConfig()` 调用，应**移除**原调用，改由 `AddObservability` 统一注册，避免重复加载。Step 6 修改 Program.cs 时会处理这一点。

- [ ] **Step 5: 验证扩展方法编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 6: 修改 Program.cs 集成可观测性**

将 `src/ApiGateway/Leno.ApiGateway/Program.cs` 的全部内容替换为：

```csharp
using Leno.ApiGateway.Extensions;
using Leno.Infrastructure.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog 替换默认日志（必须在创建 builder 后、AddObservability 前注册 host）
builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration, sectionName: "Serilog"));

// YARP 反向代理 + 可观测性（Serilog + OpenTelemetry + Prometheus + TracingTransform）
// 注意：AddObservability 内部已调用 AddReverseProxy().LoadFromConfig()，
//       不要在此处再次调用 AddReverseProxy，否则配置会重复加载。
builder.Services.AddObservability(builder.Configuration);

builder.Services.AddHttpClient("health-check");

// HealthChecksUI 仪表盘
builder.Services.AddLenoHealthChecksUI(builder.Configuration);

// 网关自身健康检查
#pragma warning disable CA1861
builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri(builder.Configuration["HealthChecks:SelfUrl"] ?? "http://localhost:5000"),
        "self",
        tags: new[] { "ready" });
#pragma warning restore CA1861

var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/health", async (IHttpClientFactory httpClientFactory, IConfiguration configuration) =>
{
    var services = configuration.GetSection("HealthChecks:Services")
        .Get<Dictionary<string, string>>()
        ?? new Dictionary<string, string>();

    if (services.Count == 0)
    {
        return Results.Ok(new { status = "Healthy" });
    }

    var httpClient = httpClientFactory.CreateClient("health-check");
    httpClient.Timeout = TimeSpan.FromSeconds(5);

    var results = new Dictionary<string, string>();
    var allHealthy = true;

    foreach (var (name, url) in services)
    {
        try
        {
            var response = await httpClient.GetAsync($"{url.TrimEnd('/')}/health/ready");
            results[name] = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy";
            if (!response.IsSuccessStatusCode)
            {
                allHealthy = false;
            }
        }
        catch (HttpRequestException)
        {
            results[name] = "Unhealthy";
            allHealthy = false;
        }
        catch (TaskCanceledException)
        {
            results[name] = "Unhealthy";
            allHealthy = false;
        }
    }

    return allHealthy
        ? Results.Ok(new { status = "Healthy", services = results })
        : Results.Json(new { status = "Unhealthy", services = results }, statusCode: 503);
});

// 可观测性中间件管道：AccessLogging -> Metrics -> (HealthChecksUI) -> YARP
app.UseObservability(builder.Configuration);

// 映射 HealthChecksUI 仪表盘
app.MapLenoHealthChecks();
app.MapLenoHealthChecksUI();

app.MapReverseProxy();

app.Run();

// 使 Program 类对 WebApplicationFactory<Program> 可见（集成测试需要）
public partial class Program { }
```

> **关键变更：**
> - 移除独立的 `builder.Services.AddReverseProxy().LoadFromConfig(...)` 调用（已由 `AddObservability` 统一接管）
> - 添加 `builder.Host.UseSerilog(...)` 替换默认日志
> - 添加 `builder.Services.AddObservability(builder.Configuration)` 注册可观测性三件套
> - 在 `MapLenoHealthChecksUI()` 之后、`MapReverseProxy()` 之前调用 `app.UseObservability(builder.Configuration)` 注册中间件
> - 新增 `public partial class Program { }` 以支持 `WebApplicationFactory<Program>` 集成测试
>
> **若阶段一已实施：** 阶段一版本的 Program.cs 已有 `AddConsulServiceDiscovery` / `AddConsulDestinationResolver` 与 `AddUrlGroup(new Uri(builder.Configuration["Consul:Url"]))`。在此情况下，保留阶段一的相关注册，仅：
> 1. 移除阶段一版本的 `AddReverseProxy().LoadFromConfig()` 调用（让 `AddObservability` 接管）
> 2. 添加 `builder.Host.UseSerilog(...)` 与 `builder.Services.AddObservability(builder.Configuration)`
> 3. 在 `app.MapLenoHealthChecksUI()` 之后调用 `app.UseObservability(builder.Configuration)`

- [ ] **Step 7: 修改 appsettings.json 添加可观测性配置节**

在 `src/ApiGateway/Leno.ApiGateway/appsettings.json` 的根级别（`"AllowedHosts": "*"` 之后、`"HealthChecks"` 之前）添加：

```json
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Yarp": "Warning",
        "Microsoft.Extensions.Diagnostics.HealthChecks": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/gateway-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext"]
  },
  "OpenTelemetry": {
    "Enabled": true,
    "Exporter": "otlp",
    "Endpoint": "http://localhost:4317",
    "ServiceName": "leno-api-gateway"
  },
  "Metrics": {
    "Enabled": true,
    "Path": "/metrics"
  },
```

> 同时将原 `"Logging"` 节保留（Serilog 会接管实际输出，但 Microsoft.Extensions.Logging 仍可在 Serilog 未启用时兜底）。

- [ ] **Step 8: 修改 appsettings.Docker.json 调整 OTLP 端点**

在 `src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json` 中（若文件不存在则创建）添加：

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "/app/logs/gateway-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
    ]
  },
  "OpenTelemetry": {
    "Enabled": true,
    "Exporter": "otlp",
    "Endpoint": "http://jaeger:4317",
    "ServiceName": "leno-api-gateway"
  },
  "Metrics": {
    "Enabled": true,
    "Path": "/metrics"
  }
}
```

- [ ] **Step 9: 运行集成测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "ObservabilityIntegrationTests|ServiceCollectionExtensionsObservabilityTests"`
Expected: `Passed: 6` — 6 个测试全部通过

> **说明：** 若 `MetricsEndpoint_AfterRequest_RecordsRequestsTotal` 不稳定（因 Serilog 在测试环境初始化时机问题），可将其改为 `[Trait("Category", "Integration")]` 并在 CI 中标记。

- [ ] **Step 10: 运行全部测试确认无回归**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj`
Expected: `Passed` — 所有测试通过（阶段一 + 阶段五）

- [ ] **Step 11: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Options/ObservabilityOptions.cs src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs src/ApiGateway/Leno.ApiGateway/Program.cs src/ApiGateway/Leno.ApiGateway/appsettings.json src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json src/ApiGateway/Leno.ApiGateway.Tests/Integration/ObservabilityIntegrationTests.cs
git commit -m "feat(gateway): Program.cs 集成 Serilog/OpenTelemetry/Prometheus 与中间件管道"
```

---

## Task 5: Grafana Dashboard 模板与监控栈

**Files:**
- Create: `grafana/leno-gateway-dashboard.json`
- Create: `grafana/provisioning/datasources/prometheus.yml`
- Create: `grafana/provisioning/dashboards/leno.yml`
- Modify: `docker-compose.yml`

- [ ] **Step 1: 创建 Grafana 数据源自动配置**

创建 `grafana/provisioning/datasources/prometheus.yml`：

```yaml
apiVersion: 1

datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
    editable: false
```

- [ ] **Step 2: 创建 Grafana Dashboard 自动加载配置**

创建 `grafana/provisioning/dashboards/leno.yml`：

```yaml
apiVersion: 1

providers:
  - name: Leno
    orgId: 1
    folder: Leno
    type: file
    disableDeletion: false
    updateIntervalSeconds: 30
    allowUiUpdates: true
    options:
      path: /var/lib/grafana/dashboards
```

- [ ] **Step 3: 创建 Grafana Dashboard JSON 模板**

创建 `grafana/leno-gateway-dashboard.json`：

```json
{
  "annotations": {
    "list": [
      {
        "builtIn": 1,
        "datasource": { "type": "grafana", "uid": "-- Grafana --" },
        "enable": true,
        "hide": true,
        "iconColor": "rgba(0, 211, 255, 1)",
        "name": "Annotations & Alerts",
        "type": "dashboard"
      }
    ]
  },
  "description": "Leno API Gateway 黄金指标仪表盘：QPS、成功率、P99 延迟、活跃请求、熔断状态、限流拒绝、黑名单命中",
  "editable": true,
  "fiscalYearStartMonth": 0,
  "graphTooltip": 0,
  "id": null,
  "links": [],
  "liveNow": false,
  "panels": [
    {
      "datasource": { "type": "prometheus", "uid": "Prometheus" },
      "fieldConfig": {
        "defaults": {
          "color": { "mode": "thresholds" },
          "thresholds": {
            "mode": "absolute",
            "steps": [
              { "color": "green", "value": null },
              { "color": "red", "value": 80 }
            ]
          },
          "unit": "reqps"
        },
        "overrides": []
      },
      "gridPos": { "h": 8, "w": 6, "x": 0, "y": 0 },
      "id": 1,
      "options": {
        "colorMode": "value",
        "graphMode": "area",
        "justifyMode": "auto",
        "orientation": "auto",
        "reduceOptions": {
          "calcs": ["lastNotNull"],
          "fields": "",
          "values": false
        },
        "textMode": "auto"
      },
      "pluginVersion": "11.2.0",
      "targets": [
        {
          "datasource": { "type": "prometheus", "uid": "Prometheus" },
          "expr": "sum(rate(gateway_requests_total[1m]))",
          "legendFormat": "QPS",
          "refId": "A"
        }
      ],
      "title": "QPS (1m rate)",
      "type": "stat"
    },
    {
      "datasource": { "type": "prometheus", "uid": "Prometheus" },
      "fieldConfig": {
        "defaults": {
          "color": { "mode": "thresholds" },
          "thresholds": {
            "mode": "absolute",
            "steps": [
              { "color": "red", "value": null },
              { "color": "yellow", "value": 0.95 },
              { "color": "green", "value": 0.99 }
            ]
          },
          "unit": "percentunit",
          "min": 0,
          "max": 1
        },
        "overrides": []
      },
      "gridPos": { "h": 8, "w": 6, "x": 6, "y": 0 },
      "id": 2,
      "options": {
        "colorMode": "value",
        "graphMode": "area",
        "justifyMode": "auto",
        "orientation": "auto",
        "reduceOptions": {
          "calcs": ["lastNotNull"],
          "fields": "",
          "values": false
        },
        "textMode": "auto"
      },
      "pluginVersion": "11.2.0",
      "targets": [
        {
          "datasource": { "type": "prometheus", "uid": "Prometheus" },
          "expr": "sum(rate(gateway_requests_total{status_code=~\"2..|3..\"}[1m])) / sum(rate(gateway_requests_total[1m]))",
          "legendFormat": "Success Rate",
          "refId": "A"
        }
      ],
      "title": "成功率 (2xx+3xx)",
      "type": "stat"
    },
    {
      "datasource": { "type": "prometheus", "uid": "Prometheus" },
      "fieldConfig": {
        "defaults": {
          "color": { "mode": "thresholds" },
          "thresholds": {
            "mode": "absolute",
            "steps": [
              { "color": "green", "value": null },
              { "color": "yellow", "value": 500 },
              { "color": "red", "value": 1000 }
            ]
          },
          "unit": "ms"
        },
        "overrides": []
      },
      "gridPos": { "h": 8, "w": 6, "x": 12, "y": 0 },
      "id": 3,
      "options": {
        "colorMode": "value",
        "graphMode": "area",
        "justifyMode": "auto",
        "orientation": "auto",
        "reduceOptions": {
          "calcs": ["lastNotNull"],
          "fields": "",
          "values": false
        },
        "textMode": "auto"
      },
      "pluginVersion": "11.2.0",
      "targets": [
        {
          "datasource": { "type": "prometheus", "uid": "Prometheus" },
          "expr": "histogram_quantile(0.99, sum(rate(gateway_request_duration_bucket[5m])) by (le))",
          "legendFormat": "P99",
          "refId": "A"
        }
      ],
      "title": "P99 延迟",
      "type": "stat"
    },
    {
      "datasource": { "type": "prometheus", "uid": "Prometheus" },
      "fieldConfig": {
        "defaults": {
          "color": { "mode": "thresholds" },
          "thresholds": {
            "mode": "absolute",
            "steps": [
              { "color": "green", "value": null },
              { "color": "yellow", "value": 100 },
              { "color": "red", "value": 500 }
            ]
          },
          "unit": "short"
        },
        "overrides": []
      },
      "gridPos": { "h": 8, "w": 6, "x": 18, "y": 0 },
      "id": 4,
      "options": {
        "colorMode": "value",
        "graphMode": "area",
        "justifyMode": "auto",
        "orientation": "auto",
        "reduceOptions": {
          "calcs": ["lastNotNull"],
          "fields": "",
          "values": false
        },
        "textMode": "auto"
      },
      "pluginVersion": "11.2.0",
      "targets": [
        {
          "datasource": { "type": "prometheus", "uid": "Prometheus" },
          "expr": "gateway_active_requests",
          "legendFormat": "Active",
          "refId": "A"
        }
      ],
      "title": "当前活跃请求数",
      "type": "stat"
    },
    {
      "datasource": { "type": "prometheus", "uid": "Prometheus" },
      "fieldConfig": {
        "defaults": {
          "color": { "mode": "palette-classic" },
          "custom": {
            "axisBorderShow": false,
            "axisCenteredZero": false,
            "axisColorMode": "text",
            "axisLabel": "",
            "axisPlacement": "auto",
            "barAlignment": 0,
            "drawStyle": "line",
            "fillOpacity": 10,
            "gradientMode": "none",
            "hideFrom": { "legend": false, "tooltip": false, "viz": false },
            "insertNulls": false,
            "lineInterpolation": "linear",
            "lineWidth": 2,
            "pointSize": 5,
            "scaleDistribution": { "type": "linear" },
            "showPoints": "never",
            "spanNulls": false,
            "stacking": { "group": "A", "mode": "none" },
            "thresholdsStyle": { "mode": "off" }
          },
          "unit": "reqps"
        },
        "overrides": []
      },
      "gridPos": { "h": 9, "w": 24, "x": 0, "y": 8 },
      "id": 5,
      "options": {
        "legend": { "calcs": ["mean", "max"], "displayMode": "table", "placement": "right", "showLegend": true },
        "tooltip": { "mode": "single", "sort": "none" }
      },
      "pluginVersion": "11.2.0",
      "targets": [
        {
          "datasource": { "type": "prometheus", "uid": "Prometheus" },
          "expr": "sum by (route) (rate(gateway_requests_total[1m]))",
          "legendFormat": "{{route}}",
          "refId": "A"
        }
      ],
      "title": "QPS by Route",
      "type": "timeseries"
    },
    {
      "datasource": { "type": "prometheus", "uid": "Prometheus" },
      "fieldConfig": {
        "defaults": {
          "color": { "mode": "palette-classic" },
          "custom": {
            "axisBorderShow": false,
            "axisCenteredZero": false,
            "axisColorMode": "text",
            "axisLabel": "",
            "axisPlacement": "auto",
            "drawStyle": "line",
            "fillOpacity": 10,
            "lineWidth": 2,
            "pointSize": 5,
            "showPoints": "never",
            "spanNulls": false
          },
          "unit": "ms"
        },
        "overrides": []
      },
      "gridPos": { "h": 9, "w": 12, "x": 0, "y": 17 },
      "id": 6,
      "options": {
        "legend": { "calcs": ["mean", "max"], "displayMode": "table", "placement": "right", "showLegend": true },
        "tooltip": { "mode": "single", "sort": "none" }
      },
      "pluginVersion": "11.2.0",
      "targets": [
        {
          "datasource": { "type": "prometheus", "uid": "Prometheus" },
          "expr": "histogram_quantile(0.99, sum by (route, le) (rate(gateway_request_duration_bucket[5m])))",
          "legendFormat": "{{route}} P99",
          "refId": "A"
        }
      ],
      "title": "P99 延迟 by Route",
      "type": "timeseries"
    },
    {
      "datasource": { "type": "prometheus", "uid": "Prometheus" },
      "fieldConfig": {
        "defaults": {
          "color": { "mode": "palette-classic" },
          "custom": {
            "axisBorderShow": false,
            "axisCenteredZero": false,
            "axisColorMode": "text",
            "axisLabel": "",
            "axisPlacement": "auto",
            "drawStyle": "bars",
            "fillOpacity": 80,
            "lineWidth": 1,
            "pointSize": 5,
            "showPoints": "never",
            "spanNulls": false,
            "stacking": { "group": "A", "mode": "normal" }
          },
          "unit": "short"
        },
        "overrides": []
      },
      "gridPos": { "h": 9, "w": 12, "x": 12, "y": 17 },
      "id": 7,
      "options": {
        "legend": { "calcs": ["sum"], "displayMode": "table", "placement": "right", "showLegend": true },
        "tooltip": { "mode": "single", "sort": "none" }
      },
      "pluginVersion": "11.2.0",
      "targets": [
        {
          "datasource": { "type": "prometheus", "uid": "Prometheus" },
          "expr": "sum by (status_code) (increase(gateway_requests_total[5m]))",
          "legendFormat": "{{status_code}}",
          "refId": "A"
        }
      ],
      "title": "状态码分布 (5m)",
      "type": "timeseries"
    },
    {
      "datasource": { "type": "prometheus", "uid": "Prometheus" },
      "fieldConfig": {
        "defaults": {
          "color": { "mode": "thresholds" },
          "thresholds": {
            "mode": "absolute",
            "steps": [
              { "color": "green", "value": null },
              { "color": "red", "value": 1 }
            ]
          },
          "unit": "short"
        },
        "overrides": []
      },
      "gridPos": { "h": 8, "w": 8, "x": 0, "y": 26 },
      "id": 8,
      "options": {
        "colorMode": "background",
        "graphMode": "none",
        "justifyMode": "auto",
        "orientation": "horizontal",
        "reduceOptions": {
          "calcs": ["lastNotNull"],
          "fields": "",
          "values": false
        },
        "textMode": "value_and_name"
      },
      "pluginVersion": "11.2.0",
      "targets": [
        {
          "datasource": { "type": "prometheus", "uid": "Prometheus" },
          "expr": "gateway_circuit_breaker_state",
          "legendFormat": "{{cluster}}",
          "refId": "A"
        }
      ],
      "title": "熔断器状态 (0=closed, 1=open)",
      "type": "stat"
    },
    {
      "datasource": { "type": "prometheus", "uid": "Prometheus" },
      "fieldConfig": {
        "defaults": {
          "color": { "mode": "thresholds" },
          "thresholds": {
            "mode": "absolute",
            "steps": [
              { "color": "green", "value": null },
              { "color": "yellow", "value": 10 },
              { "color": "red", "value": 100 }
            ]
          },
          "unit": "short"
        },
        "overrides": []
      },
      "gridPos": { "h": 8, "w": 8, "x": 8, "y": 26 },
      "id": 9,
      "options": {
        "colorMode": "value",
        "graphMode": "area",
        "justifyMode": "auto",
        "orientation": "auto",
        "reduceOptions": {
          "calcs": ["lastNotNull"],
          "fields": "",
          "values": false
        },
        "textMode": "auto"
      },
      "pluginVersion": "11.2.0",
      "targets": [
        {
          "datasource": { "type": "prometheus", "uid": "Prometheus" },
          "expr": "sum(rate(gateway_rate_limit_rejected[5m]))",
          "legendFormat": "Rejected",
          "refId": "A"
        }
      ],
      "title": "限流拒绝 (5m rate)",
      "type": "stat"
    },
    {
      "datasource": { "type": "prometheus", "uid": "Prometheus" },
      "fieldConfig": {
        "defaults": {
          "color": { "mode": "thresholds" },
          "thresholds": {
            "mode": "absolute",
            "steps": [
              { "color": "green", "value": null },
              { "color": "yellow", "value": 10 },
              { "color": "red", "value": 100 }
            ]
          },
          "unit": "short"
        },
        "overrides": []
      },
      "gridPos": { "h": 8, "w": 8, "x": 16, "y": 26 },
      "id": 10,
      "options": {
        "colorMode": "value",
        "graphMode": "area",
        "justifyMode": "auto",
        "orientation": "auto",
        "reduceOptions": {
          "calcs": ["lastNotNull"],
          "fields": "",
          "values": false
        },
        "textMode": "auto"
      },
      "pluginVersion": "11.2.0",
      "targets": [
        {
          "datasource": { "type": "prometheus", "uid": "Prometheus" },
          "expr": "sum(rate(gateway_blacklist_hits[5m]))",
          "legendFormat": "Hits",
          "refId": "A"
        }
      ],
      "title": "黑名单命中 (5m rate)",
      "type": "stat"
    }
  ],
  "refresh": "10s",
  "schemaVersion": 39,
  "tags": ["leno", "api-gateway", "observability"],
  "templating": { "list": [] },
  "time": { "from": "now-1h", "to": "now" },
  "timepicker": {},
  "timezone": "browser",
  "title": "Leno API Gateway",
  "uid": "leno-api-gateway",
  "version": 1,
  "weekStart": ""
}
```

- [ ] **Step 4: 在 docker-compose.yml 中添加 Prometheus 服务**

在 `docker-compose.yml` 的 `jaeger` 服务之后插入 Prometheus 服务：

```yaml
  prometheus:
    image: prom/prometheus:v2.55.1
    container_name: leno-prometheus
    volumes:
      - ./grafana/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheusdata:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--storage.tsdb.retention.time=7d'
    ports:
      - "9090:9090"
    healthcheck:
      test: ["CMD-SHELL", "wget --spider -q http://localhost:9090/-/healthy || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 10
    networks:
      - leno-net
```

- [ ] **Step 5: 创建 Prometheus 抓取配置**

创建 `grafana/prometheus.yml`（Prometheus 服务挂载此文件作为抓取配置）：

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: leno-api-gateway
    metrics_path: /metrics
    static_configs:
      - targets: ['api-gateway:8080']
        labels:
          service: api-gateway
```

- [ ] **Step 6: 在 docker-compose.yml 中添加 Grafana 服务**

在 `docker-compose.yml` 的 `prometheus` 服务之后插入 Grafana 服务：

```yaml
  grafana:
    image: grafana/grafana:11.2.0
    container_name: leno-grafana
    environment:
      - GF_SECURITY_ADMIN_USER=leno
      - GF_SECURITY_ADMIN_PASSWORD=Leno@Grafana2026
      - GF_USERS_ALLOW_SIGN_UP=false
    volumes:
      - ./grafana/provisioning:/etc/grafana/provisioning:ro
      - ./grafana/leno-gateway-dashboard.json:/var/lib/grafana/dashboards/leno-gateway-dashboard.json:ro
      - grafanadata:/var/lib/grafana
    ports:
      - "3000:3000"
    depends_on:
      prometheus:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "wget --spider -q http://localhost:3000/api/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 10
    networks:
      - leno-net
```

- [ ] **Step 7: 添加 volumes 声明**

在 `docker-compose.yml` 的 `volumes:` 块末尾（`esdata:` 之后）添加：

```yaml
  prometheusdata:
  grafanadata:
```

- [ ] **Step 8: 为 api-gateway 添加 prometheus 依赖**

在 `docker-compose.yml` 的 `api-gateway` 服务的 `depends_on` 块末尾添加：

```yaml
      prometheus:
        condition: service_healthy
```

> 注意：若阶段一已为 `api-gateway` 添加 `consul` 与 `jaeger` 依赖，则将 `prometheus` 依赖追加在 `jaeger` 之后。

- [ ] **Step 9: 验证 docker-compose 配置**

Run: `docker compose config --quiet`
Expected: 无输出（退出码 0）

- [ ] **Step 10: 验证目录结构**

Run: `ls -la grafana/ grafana/provisioning/datasources/ grafana/provisioning/dashboards/`
Expected: 显示以下文件：
- `grafana/leno-gateway-dashboard.json`
- `grafana/prometheus.yml`
- `grafana/provisioning/datasources/prometheus.yml`
- `grafana/provisioning/dashboards/leno.yml`

- [ ] **Step 11: 验证全量编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 12: 提交**

```bash
git add grafana/ docker-compose.yml
git commit -m "feat(observability): 添加 Grafana Dashboard 模板与 Prometheus/Grafana 容器"
```

---

## 实施后验证清单

完成所有 Task 后执行以下整体验证：

- [ ] **全量编译：** `dotnet build Leno.slnx` — 所有项目编译成功
- [ ] **全量测试：** `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj` — 所有测试通过
- [ ] **Docker 配置：** `docker compose config --quiet` — 无错误
- [ ] **NuGet 包：** 网关 csproj 包含 Serilog (3 个)、OpenTelemetry (4 个)、prometheus-net.AspNetCore 共 8 个新包
- [ ] **指标端点：** 启动网关后访问 `http://localhost:8080/metrics` 返回 Prometheus 文本格式，包含 `gateway_requests_total`、`gateway_request_duration`、`gateway_active_requests`、`gateway_circuit_breaker_state`、`gateway_rate_limit_rejected`、`gateway_blacklist_hits` 6 个指标
- [ ] **访问日志：** 启动网关发送请求后，控制台输出 JSON 结构化访问日志，包含 10 个标准字段
- [ ] **追踪链路：** 启动 Jaeger UI (`http://localhost:16686`)，发送请求后可见 `leno-api-gateway` 服务的 Span
- [ ] **Grafana Dashboard：** 访问 `http://localhost:3000`（用户名 `leno` / 密码 `Leno@Grafana2026`），在 `Leno` 文件夹下可见 `Leno API Gateway` 仪表盘，4 个 stat 面板与 4 个时间序列面板正常加载

---

## 完成后的可观测性能力总览

| 维度 | 实现方式 | 暴露形式 | 验证入口 |
|------|---------|---------|---------|
| **访问日志** | `AccessLoggingMiddleware` + Serilog | Console(stdout) + File(`logs/gateway-YYYYMMDD.log`) | `docker logs leno-api-gateway` |
| **分布式追踪** | OpenTelemetry SDK + `TracingTransform` | OTLP -> Jaeger | Jaeger UI `http://localhost:16686` |
| **监控指标** | `GatewayMetricsService` + prometheus-net | `/metrics` 端点 (Prometheus 文本格式) | `curl http://localhost:8080/metrics` |
| **可视化** | Grafana Dashboard | 8 个面板（QPS/成功率/P99/活跃/路由QPS/路由P99/状态码/熔断/限流/黑名单） | Grafana `http://localhost:3000` |
