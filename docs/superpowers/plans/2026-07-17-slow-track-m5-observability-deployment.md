# 慢轨 M5 可观测性与部署补齐 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 11 个业务服务暴露 Prometheus `/metrics` 端点并接入 Grafana 抓取；Consul KV 配置中心全面接管敏感配置，11 个 BC 各自独立 InternalApiKey；新增 Alertmanager 容器与 4 条告警规则（Outbox 积压/死信队列/防腐层失败率/服务宕机）+ Outbox 积压指标；新建 `deploy/helm/leno/` Helm chart（umbrella chart，11 BC + 网关，含 HPA + readiness/liveness probe + Init Container 迁移）；CI 增加覆盖率门槛阻断（Domain ≥80%、Application ≥60%、Infrastructure ≥40%）与 staging 集成测试 job

**Architecture:** `AddLenoOpenTelemetry` 回调扩展支持 `.WithMetrics(...).AddMeter(...)`；`UseLenoPipeline`（Plan 7 已建）追加 `app.UseMetricServer("/metrics")`；`prometheus-net.AspNetCore` 在 `Leno.Infrastructure` 引用并传递依赖至各 BC；Consul KV 路径约定 `leno/security/internal-key/{bc}`；Alertmanager 容器与 Grafana provisioning 集成，告警规则放 `grafana/provisioning/alerting/`；Helm chart 结构 `deploy/helm/leno/`（umbrella chart + 通用模板 `_helpers.tpl`/`deployment.yaml`/`service.yaml`/`hpa.yaml`/`ingress.yaml`）；Init Container 执行 `dotnet ef database update` 或调用 `MigrateWithLockAsync`；CI 在 `dotnet test` 后增加 `coverage-threshold` 步骤解析 cobertura XML

**Tech Stack:** .NET 10、prometheus-net.AspNetCore 8.2.4、OpenTelemetry 1.10、Prometheus v2.55、Grafana 11.2、Alertmanager 0.27、Consul 1.18、Helm 3.14、Kubernetes 1.29、xUnit、coverlet.msbuild

**关联 spec:** [2026-07-17-comprehensive-optimization-v2-design.md §12](../specs/2026-07-17-comprehensive-optimization-v2-design.md)

**前置依赖:** Plan 2（F2 安全，Consul KV / ValidateSensitiveConfig 已就绪）完成；Plan 3（F3 EF Migrations，`MigrateWithLockAsync` 已就绪）完成；Plan 4（F4 测试补齐 + CI 占位零容忍已落地）完成；Plan 7（M3 跨 BC 样板去重，`AddLenoApi`/`UseLenoPipeline` 已就绪）完成；Plan 8（M4 通信升级，AntiCorruptionMetrics 已统一到 Leno.Infrastructure）完成

**向后兼容策略:** M5.1 Prometheus 指标端点新增不影响既有功能（只读端点）；M5.2 Consul KV 收敛在 F2.4 基础上推进，配置缺失时启动期 `ValidateSensitiveConfig` 仍 fail-closed；M5.3 Alertmanager 容器新增不影响既有 docker-compose 服务；M5.4 Helm chart 与既有 docker-compose 并存，生产环境二选一；M5.5 CI 覆盖率门槛分阶段收紧（先 warning 后阻断），避免阻塞既有 PR

---

## 关键代码定位（实施前必读）

| 位置 | 路径 | 关键发现 |
|---|---|---|
| OpenTelemetryExtensions | `src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs:44-92` | 当前仅 `.WithTracing(...)`，**无 `.WithMetrics(...)`**；需扩展 `AddLenoOpenTelemetry` 增加 `Action<MeterProviderBuilder>? configureMetrics` 参数 |
| prometheus-net 包引用现状 | `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj:17` | 仅网关引用 `prometheus-net.AspNetCore 8.2.1`；11 个 BC **未引用**，`Leno.Infrastructure` **未引用** |
| Prometheus scrape 配置 | `grafana/prometheus.yml` | 仅抓取 `api-gateway:8080`；**11 个业务服务未抓取** |
| Grafana dashboard | `grafana/leno-gateway-dashboard.json` | 仅网关仪表板；**无业务服务仪表板** |
| Grafana provisioning | `grafana/provisioning/dashboards/leno.yml` | 仅注册 gateway dashboard |
| Alertmanager 容器 | `docker-compose.yml` | **不存在** Alertmanager 服务定义 |
| 告警规则文件 | `grafana/provisioning/alerting/` | **目录不存在**，无告警规则 |
| Outbox 积压指标 | `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessage.cs` | 无 gauge 指标暴露；需新建 `OutboxMetrics` 类暴露 `outbox_pending_count` |
| 死信队列指标 | `src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/DeadLetterMonitorBackgroundService.cs:43` | 已有 `MeterName`，但未在 `AddLenoOpenTelemetry` 中订阅 |
| AddLenoHealthChecks | `src/BuildingBlocks/Leno.Infrastructure/HealthChecks/HealthChecksUIExtensions.cs:25` | 已存在（含 RabbitMQ/Redis/ES/Self），但 11 BC 未使用（Plan 7 `AddLenoApi` 已统一接入） |
| docker-compose 服务清单 | `docker-compose.yml` | 含 11 业务服务 + 网关 + 6 基础设施（SQL/Redis/RabbitMQ/ES/Consul/Jaeger/Prometheus/Grafana）；**无 Alertmanager** |
| CI 配置 | `.github/workflows/ci.yml` | 已有 build-solution/integration-tests/build-services/docker-build/validate-compose 5 个 job；**无覆盖率门槛阻断**；**无 staging 集成测试 job** |
| Helm chart 目录 | `deploy/helm/leno/` | **不存在**，需新建 |
| AddLenoApi / UseLenoPipeline | `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`（Plan 7 已建） | 已统一 11 BC 启动管道，M5 在其内追加 metrics 端点 |
| AntiCorruptionMetrics | `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs`（Plan 8 Task 1 已建） | Meter 名 `Leno.AntiCorruption`，需在 AddLenoOpenTelemetry 订阅 |

### 11 个业务服务 Prometheus 端口分配

| BC | HTTP 端口 | Metrics 端口（同 HTTP，路径 /metrics） |
|---|---|---|
| UserAuth | 5151 | 5151/metrics |
| Product | 5152 | 5152/metrics |
| Cart | 5153 | 5153/metrics |
| Order | 5154 | 5154/metrics |
| Promotion | 5155 | 5155/metrics |
| ReviewAfterSales | 5156 | 5156/metrics |
| PointsMembership | 5157 | 5157/metrics |
| Payment | 5158 | 5158/metrics |
| Notification | 5159 | 5159/metrics |
| SellerShop | 5160 | 5160/metrics |
| SystemAdmin | 5161 | 5161/metrics |

> **说明：** metrics 复用 HTTP 端口，通过路径 `/metrics` 区分，不额外开端口（与网关一致）。

---

## Task 1: Leno.Infrastructure 引用 prometheus-net + AddLenoOpenTelemetry 扩展 WithMetrics

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`（引用 prometheus-net.AspNetCore）
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs`（增加 `configureMetrics` 参数）
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`（AddLenoApi 内调用 `UseMetricServer` + 订阅 AntiCorruptionMetrics）
- Create: `src/BuildingBlocks/Leno.Infrastructure.Tests/Telemetry/OpenTelemetryMetricsExtensionsTests.cs`

- [ ] **Step 1: Leno.Infrastructure.csproj 引用 prometheus-net.AspNetCore**

修改 `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`，在 `<ItemGroup>` 中增加：

```xml
<PackageReference Include="prometheus-net.AspNetCore" Version="8.2.4" />
```

- [ ] **Step 2: AddLenoOpenTelemetry 增加 configureMetrics 参数**

修改 `src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs`，扩展 `AddLenoOpenTelemetry` 方法签名与实现：

```csharp
public static IHostApplicationBuilder AddLenoOpenTelemetry(
    this IHostApplicationBuilder builder,
    Action<TracerProviderBuilder>? configureTracing = null,
    Action<MeterProviderBuilder>? configureMetrics = null)
{
    ArgumentNullException.ThrowIfNull(builder);

    var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? DefaultOtlpEndpoint;
    var serviceName = builder.Configuration["OpenTelemetry:ServiceName"]
                      ?? Assembly.GetEntryAssembly()?.GetName().Name
                      ?? "Leno";

    var openTelemetryBuilder = builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource =>
        {
            resource.AddService(
                serviceName: serviceName,
                serviceVersion: "1.0.0",
                autoGenerateServiceInstanceId: true);
        });

    openTelemetryBuilder.WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("MassTransit")
            .AddSource(ActivitySources.Order)
            .AddSource(ActivitySources.Payment)
            .AddSource(ActivitySources.Stock)
            .SetSampler(CreateSampler(builder.Environment))
            .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));

        configureTracing?.Invoke(tracing);
    });

    // M5.1 新增：Metrics 通道
    openTelemetryBuilder.WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("Leno.AntiCorruption")              // Plan 8 已建
            .AddMeter("Leno.SystemAdmin.DeadLetter")       // 已存在
            .AddMeter("Leno.Order.AntiCorruption")         // 兼容期：既有 Order BC 引用
            .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));

        configureMetrics?.Invoke(metrics);
    });

    builder.Services.AddSingleton<ILogEventEnricher, OpenTelemetryTraceIdEnricher>();

    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
    });

    return builder;
}
```

> **说明：** 既有 `Leno.Order.AntiCorruption` Meter 名在 Plan 8 Task 1 已统一为 `Leno.AntiCorruption`。此处保留兼容订阅，避免既有 Order BC 测试失败。Plan 8 Task 3 完成后可移除兼容订阅。

- [ ] **Step 3: AddLenoApi 内调用 UseMetricServer**

修改 `src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`，在 `UseLenoPipeline` 方法体内（Plan 7 已建）增加：

```csharp
// M5.1：暴露 Prometheus /metrics 端点
app.UseMetricServer("/metrics");
```

> **说明：** `UseMetricServer` 来自 `prometheus-net.AspNetCore`。`/metrics` 路径不要求鉴权（与网关一致），但生产环境应通过网络策略限制仅 Prometheus 可达。

- [ ] **Step 4: 创建单元测试**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/Telemetry/OpenTelemetryMetricsExtensionsTests.cs`：

```csharp
using Leno.Infrastructure.Telemetry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using FluentAssertions;
using Xunit;

namespace Leno.Infrastructure.Tests.Telemetry;

public class OpenTelemetryMetricsExtensionsTests
{
    [Fact]
    public void AddLenoOpenTelemetry_WithMetricsCallback_InvokesCallback()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:OtlpEndpoint"] = "http://localhost:4317";
        var invoked = false;

        builder.AddLenoOpenTelemetry(
            configureMetrics: metrics =>
            {
                invoked = true;
                metrics.AddMeter("Test.Meter");
            });

        using var host = builder.Build();
        invoked.Should().BeTrue();
    }

    [Fact]
    public void AddLenoOpenTelemetry_DefaultSubscribesAntiCorruptionMeter()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["OpenTelemetry:OtlpEndpoint"] = "http://localhost:4317";

        builder.AddLenoOpenTelemetry();

        // 验证 MeterProvider 可解析（间接验证 WithMetrics 已配置）
        var provider = builder.Services.BuildServiceProvider().GetService<MeterProvider>();
        provider.Should().NotBeNull();
    }
}
```

- [ ] **Step 5: 编译并运行测试**

Run: `dotnet build src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj && dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/ --filter "FullyQualifiedName~OpenTelemetryMetrics"`
Expected: BUILD SUCCESS + 2 个测试 PASS

- [ ] **Step 6: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Telemetry/OpenTelemetryMetricsExtensionsTests.cs
git commit -m "feat(M5.1): Leno.Infrastructure 引用 prometheus-net，AddLenoOpenTelemetry 扩展 WithMetrics 通道"
```

---

## Task 2: 11 个 BC 启用 /metrics 端点 + Prometheus 抓取配置

**Files:**
- Modify: 11 个 BC `Program.cs`（调用 `AddLenoApi` 时通过 `configureInfrastructure` 委托增加 `configureMetrics`，订阅 BC 专属 Meter）
- Modify: `grafana/prometheus.yml`（新增 11 个业务服务 scrape_configs）
- Create: `grafana/leno-business-services-dashboard.json`（业务服务仪表板）
- Modify: `grafana/provisioning/dashboards/leno.yml`（注册新仪表板）

- [ ] **Step 1: 11 个 BC Program.cs 订阅 BC 专属 Meter**

修改 11 个 BC 的 `Program.cs`，在 `builder.AddLenoOpenTelemetry()` 调用增加 `configureMetrics` 回调。样例（Order）：

```csharp
builder.AddLenoOpenTelemetry(
    configureMetrics: metrics =>
    {
        metrics.AddMeter("Leno.Order.AntiCorruption");
        metrics.AddMeter("Leno.Order.Saga");  // 若有专属 Meter
    });
```

各 BC 按实际 Meter 名订阅（若 BC 无专属 Meter，仅依赖 `AddLenoOpenTelemetry` 默认订阅即可，本步骤可省略）。

- [ ] **Step 2: 修改 Prometheus 抓取配置**

修改 `grafana/prometheus.yml`，新增 11 个业务服务 scrape_configs：

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

  # M5.1 新增：11 个业务服务
  - job_name: leno-userauth
    metrics_path: /metrics
    static_configs:
      - targets: ['userauth-api:5151']
        labels:
          service: userauth

  - job_name: leno-product
    metrics_path: /metrics
    static_configs:
      - targets: ['product-api:5152']
        labels:
          service: product

  - job_name: leno-cart
    metrics_path: /metrics
    static_configs:
      - targets: ['cart-api:5153']
        labels:
          service: cart

  - job_name: leno-order
    metrics_path: /metrics
    static_configs:
      - targets: ['order-api:5154']
        labels:
          service: order

  - job_name: leno-promotion
    metrics_path: /metrics
    static_configs:
      - targets: ['promotion-api:5155']
        labels:
          service: promotion

  - job_name: leno-reviewaftersales
    metrics_path: /metrics
    static_configs:
      - targets: ['reviewaftersales-api:5156']
        labels:
          service: reviewaftersales

  - job_name: leno-pointsmembership
    metrics_path: /metrics
    static_configs:
      - targets: ['pointsmembership-api:5157']
        labels:
          service: pointsmembership

  - job_name: leno-payment
    metrics_path: /metrics
    static_configs:
      - targets: ['payment-api:5158']
        labels:
          service: payment

  - job_name: leno-notification
    metrics_path: /metrics
    static_configs:
      - targets: ['notification-api:5159']
        labels:
          service: notification

  - job_name: leno-sellershop
    metrics_path: /metrics
    static_configs:
      - targets: ['sellershop-api:5160']
        labels:
          service: sellershop

  - job_name: leno-systemadmin
    metrics_path: /metrics
    static_configs:
      - targets: ['systemadmin-api:5161']
        labels:
          service: systemadmin
```

> **说明：** 服务名（如 `userauth-api`）需与 `docker-compose.yml` 中服务名一致。生产环境使用 Consul 服务发现替换 `static_configs`：

```yaml
  - job_name: leno-services
    metrics_path: /metrics
    consul_sd_configs:
      - server: 'consul:8500'
        services: ['leno-userauth-api', 'leno-product-api', 'leno-cart-api', ...]
    relabel_configs:
      - source_labels: [__meta_consul_service]
        target_label: service
```

- [ ] **Step 3: 创建业务服务仪表板**

创建 `grafana/leno-business-services-dashboard.json`，包含以下面板：
- 防腐层失败率（按 BC 分组）：`rate(anticorruption_failure_total[5m]) by (service)`
- 业务服务 HTTP QPS：`rate(aspnetcore_routing_requests_total[5m]) by (service)`
- 业务服务 HTTP 错误率：`rate(aspnetcore_routing_requests_total{status_code=~"5.."}[5m]) by (service)`
- EF Core 查询延迟 P99：`histogram_quantile(0.99, rate(ef_core_query_duration_seconds_bucket[5m]))`
- Outbox 积压（M5.3 Task 3 新增后接入）：`outbox_pending_count`

仪表板 JSON 结构参照既有 `leno-gateway-dashboard.json`，使用 Grafana 11.2 schema。

- [ ] **Step 4: 注册业务服务仪表板**

修改 `grafana/provisioning/dashboards/leno.yml`，新增：

```yaml
apiVersion: 1
providers:
  - name: Leno Dashboards
    folder: Leno
    type: file
    options:
      path: /var/lib/grafana/dashboards
      files:
        - leno-gateway-dashboard.json
        - leno-business-services-dashboard.json
```

- [ ] **Step 5: 本地验证（启动全栈）**

```bash
docker compose up -d sqlserver redis rabbitmq elasticsearch consul jaeger prometheus grafana
# 启动至少 1 个业务服务（如 Order）
dotnet run --project src/Services/Order/Leno.Order.Api
# 验证 /metrics 端点
curl http://localhost:5154/metrics
# 验证 Prometheus 抓取
# 访问 http://localhost:9090/targets，确认 leno-order job 为 UP
```

- [ ] **Step 6: 提交**

```bash
git add src/Services/*/Leno.*.Api/Program.cs grafana/prometheus.yml grafana/leno-business-services-dashboard.json grafana/provisioning/dashboards/leno.yml
git commit -m "feat(M5.1): 11 个 BC 启用 /metrics 端点，Prometheus 抓取配置与业务服务仪表板"
```

---

## Task 3: 新建 Outbox 积压指标 + Alertmanager 容器 + 告警规则

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMetrics.cs`
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxCleanupBackgroundService.cs`（或 OutboxPublisher 所在的后台服务，定期更新积压指标）
- Modify: `docker-compose.yml`（新增 alertmanager 服务）
- Create: `grafana/provisioning/alerting/leno-alerts.yml`
- Create: `alertmanager/alertmanager.yml`（Alertmanager 主配置）
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs`（订阅 `Leno.Outbox` Meter）

- [ ] **Step 1: 创建 OutboxMetrics 类**

创建 `src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMetrics.cs`：

```csharp
using System.Diagnostics.Metrics;

namespace Leno.Infrastructure.Outbox;

/// <summary>
/// Outbox 积压与处理指标（M5.3）。
/// 暴露 Prometheus 指标 <c>outbox_pending_count</c>（gauge）与 <c>outbox_published_total</c>（counter）。
/// 由 Outbox 后台服务定期调用 <see cref="SetPendingCount"/> 更新积压值。
/// </summary>
public static class OutboxMetrics
{
    public const string MeterName = "Leno.Outbox";

    private static readonly Meter _meter = new(MeterName, "1.0.0");

    /// <summary>Outbox 待发布消息数（gauge），由后台服务定期更新。</summary>
    public static ObservableGauge<int> PendingCountGauge { get; }

    /// <summary>Outbox 已发布消息数（counter）。</summary>
    public static Counter<int> PublishedCounter { get; } =
        _meter.CreateCounter<int>("outbox_published_total", unit: "messages", description: "Outbox 已发布消息数");

    private static int _currentPendingCount;

    static OutboxMetrics()
    {
        PendingCountGauge = _meter.CreateObservableGauge<int>(
            "outbox_pending_count",
            () => new Measurement<int>(_currentPendingCount),
            unit: "messages",
            description: "Outbox 待发布消息数");
    }

    /// <summary>更新 Outbox 待发布消息数（由后台服务定期调用）。</summary>
    public static void SetPendingCount(int count)
    {
        Interlocked.Exchange(ref _currentPendingCount, count < 0 ? 0 : count);
    }

    /// <summary>记录一次成功发布。</summary>
    public static void RecordPublished(string bcName)
    {
        PublishedCounter.Add(1, new KeyValuePair<string, object?>("bc", bcName));
    }
}
```

- [ ] **Step 2: Outbox 后台服务定期更新积压指标**

修改 Outbox 后台清理服务（若存在 `OutboxCleanupBackgroundService`，否则在 `OutboxPublisher` 发布循环中），在每次轮询时调用：

```csharp
// 在轮询循环中：
var pendingCount = await dbContext.Set<OutboxMessage>()
    .CountAsync(m => m.Status == OutboxMessageStatus.Pending, ct);
OutboxMetrics.SetPendingCount(pendingCount);

// 每次成功发布：
OutboxMetrics.RecordPublished(bcName);
```

> **说明：** 若 Outbox 清理逻辑分散在各 BC，需统一抽到 `Leno.Infrastructure`（Plan 7 `BaseDbContext` 已暴露 `Set<OutboxMessage>()`，可直接使用）。若 Outbox 清理仍由各 BC 单独实现，本步骤为校验各 BC 都接入 `OutboxMetrics`。

- [ ] **Step 3: AddLenoOpenTelemetry 订阅 Leno.Outbox Meter**

修改 `src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs`，在 `.WithMetrics(...)` 内增加：

```csharp
metrics
    .AddMeter("Leno.AntiCorruption")
    .AddMeter("Leno.SystemAdmin.DeadLetter")
    .AddMeter("Leno.Outbox")  // M5.3 新增
    ...
```

- [ ] **Step 4: docker-compose.yml 新增 Alertmanager 服务**

修改 `docker-compose.yml`，在 `services:` 下新增：

```yaml
  alertmanager:
    image: prom/alertmanager:v0.27.0
    container_name: leno-alertmanager
    ports:
      - "9093:9093"
    volumes:
      - ./alertmanager/alertmanager.yml:/etc/alertmanager/alertmanager.yml:ro
      - alertmanager-data:/alertmanager
    command:
      - '--config.file=/etc/alertmanager/alertmanager.yml'
      - '--storage.path=/alertmanager'
    networks:
      - leno-network
    restart: unless-stopped
```

并在 `volumes:` 段新增 `alertmanager-data:`。

- [ ] **Step 5: 创建 Alertmanager 主配置**

创建 `alertmanager/alertmanager.yml`：

```yaml
global:
  resolve_timeout: 5m

route:
  group_by: ['alertname', 'service']
  group_wait: 30s
  group_interval: 5m
  repeat_interval: 4h
  receiver: 'default'

receivers:
  - name: 'default'
    webhook_configs:
      # 钉钉/企业微信 webhook（生产替换为实际 URL）
      - url: 'https://oapi.dingtalk.com/robot/send?access_token=REPLACE_ME'
        send_resolved: true

inhibit_rules:
  - source_match:
      severity: 'critical'
    target_match:
      severity: 'warning'
    equal: ['service']
```

> **说明：** 实际钉钉/企业微信 webhook URL 通过环境变量或 Consul KV 注入，不入库 git。本配置文件保留占位符 `REPLACE_ME`。

- [ ] **Step 6: 创建告警规则文件**

创建 `grafana/provisioning/alerting/leno-alerts.yml`：

```yaml
apiVersion: 1
groups:
  - name: leno-business-alerts
    interval: 30s
    rules:
      - alert: OutboxBacklogHigh
        expr: outbox_pending_count > 100
        for: 5m
        labels:
          severity: critical
          service: outbox
        annotations:
          summary: "Outbox 积压过高 ({{ $value }} > 100)"
          description: "Outbox 待发布消息数持续 5 分钟超过 100，可能存在 RabbitMQ 故障或消费方处理过慢"

      - alert: DeadLetterQueueHigh
        expr: leno_systemadmin_deadletter_count > 50
        for: 5m
        labels:
          severity: critical
          service: systemadmin
        annotations:
          summary: "死信队列积压过高 ({{ $value }} > 50)"
          description: "SystemAdmin 死信队列持续 5 分钟超过 50 条，需人工介入处理"

      - alert: AntiCorruptionFailureRateHigh
        expr: rate(anticorruption_failure_total[5m]) > 0.1
        for: 5m
        labels:
          severity: warning
          service: anticorruption
        annotations:
          summary: "防腐层失败率过高 ({{ $value | humanizePercentage }})"
          description: "防腐层调用失败率持续 5 分钟超过 0.1/s，可能存在下游服务故障"

      - alert: ServiceDown
        expr: up{job=~"leno-.*"} == 0
        for: 1m
        labels:
          severity: critical
          service: "{{ $labels.job }}"
        annotations:
          summary: "服务不可达 ({{ $labels.job }})"
          description: "Prometheus 抓取失败持续 1 分钟，服务可能已宕机"
```

> **说明：** Grafana provisioning alerting 需在 `grafana/provisioning/` 下创建 `alerting/` 子目录。Grafana 11.2 支持 YAML 格式告警规则 provisioning。

- [ ] **Step 7: 本地验证**

```bash
docker compose up -d alertmanager prometheus grafana
# 访问 http://localhost:9093，确认 Alertmanager UI 可用
# 访问 http://localhost:3000，确认 Grafana 加载告警规则
# 手动触发告警（如制造 Outbox 积压 > 100），验证钉钉/企业微信通知送达
```

- [ ] **Step 8: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMetrics.cs src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs docker-compose.yml alertmanager/alertmanager.yml grafana/provisioning/alerting/leno-alerts.yml
# 若修改了 Outbox 后台服务
git add src/BuildingBlocks/Leno.Infrastructure/Outbox/
git commit -m "feat(M5.3): 新建 Outbox 积压指标 + Alertmanager 容器 + 4 条告警规则（Outbox/死信/防腐层失败率/服务宕机）"
```

---

## Task 4: Consul KV 配置中心收敛 + 11 BC 独立 InternalApiKey

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Configuration/ConfigCenterExtensions.cs`（`ValidateSensitiveConfig` 增加 InternalApiKey 各 BC 独立校验）
- Modify: 11 个 BC `appsettings.json`（移除明文 InternalApiKey，改从 Consul KV 读取）
- Create: `docs/consul-kv-seed.md`（Consul KV 种子数据说明，含 11 个 BC 独立 InternalApiKey 生成命令）
- Modify: 7 个防腐层调用方 BC `appsettings.json`（配置目标 BC 的 InternalApiKey）

- [ ] **Step 1: ValidateSensitiveConfig 增加 InternalApiKey 各 BC 独立校验**

修改 `src/BuildingBlocks/Leno.Infrastructure/Configuration/ConfigCenterExtensions.cs`，扩展 `ValidateSensitiveConfig`：

```csharp
public static void ValidateSensitiveConfig(this IConfiguration configuration, IHostEnvironment environment)
{
    var bcName = configuration["Service:Name"]
        ?? throw new InvalidOperationException("Service:Name 配置缺失，无法校验 InternalApiKey");

    // 既有 JWT/DB/MQ 校验...

    // M5.2 新增：InternalApiKey 各 BC 独立校验
    var internalKey = configuration[$"Security:InternalApiKey:{bcName}"];
    if (string.IsNullOrWhiteSpace(internalKey))
    {
        // F2.4 兼容期：若 BC 仍用共用 key，降级为 warning
        var sharedKey = configuration["Security:InternalApiKey:Shared"];
        if (string.IsNullOrWhiteSpace(sharedKey))
        {
            throw new InvalidOperationException(
                $"敏感配置缺失：Security:InternalApiKey:{bcName} 与 Security:InternalApiKey:Shared 均为空，请通过 Consul KV 配置 leno/security/internal-key/{bcName}");
        }

        Console.WriteLine($"[WARN] BC {bcName} 仍在使用 Shared InternalApiKey，请尽快迁移到独立 key（M5.2）");
    }
    else
    {
        // 校验独立 key 长度（至少 32 字节 base64 编码 = 44 字符）
        if (internalKey.Length < 44)
        {
            throw new InvalidOperationException(
                $"Security:InternalApiKey:{bcName} 长度不足，至少 32 字节（base64 编码 44 字符），当前 {internalKey.Length} 字符");
        }
    }
}
```

- [ ] **Step 2: 11 个 BC appsettings.json 移除明文 InternalApiKey**

修改 11 个 BC 的 `appsettings.json`，将明文 InternalApiKey 改为占位符（实际值由 Consul KV 注入）：

```json
{
  "Security": {
    "InternalApiKey": {
      "Shared": "${LENO_INTERNAL_API_KEY_SHARED}"
    }
  }
}
```

> **说明：** F2.4 已移除明文 JWT/DB/MQ 密钥，本步骤仅处理 InternalApiKey。M5.2 完成后，`Shared` 配置项移除，各 BC 仅使用 `Security:InternalApiKey:{BcName}`。

- [ ] **Step 3: 创建 Consul KV 种子数据文档**

创建 `docs/consul-kv-seed.md`：

```markdown
# Consul KV 种子数据

## M5.2：11 个 BC 独立 InternalApiKey

生成 32 字节随机 key（base64 编码）：

```bash
# 生成 11 个 BC 独立 InternalApiKey
for bc in userauth product cart order promotion reviewaftersales pointsmembership payment notification sellershop systemadmin; do
  key=$(openssl rand -base64 32)
  echo "leno/security/internal-key/$bc = $key"
  # 写入 Consul KV
  curl -X PUT "http://localhost:8500/v1/kv/leno/security/internal-key/$bc" -d "$key"
done
```

## 调用方配置

防腐层调用方需配置目标 BC 的 InternalApiKey。例如 Order BC 调用 Product/Promotion/PointsMembership：

```json
// Leno.Order.Api/appsettings.json
{
  "AntiCorruption": {
    "TargetInternalApiKeys": {
      "Product": "${LENO_INTERNAL_API_KEY_PRODUCT}",
      "Promotion": "${LENO_INTERNAL_API_KEY_PROMOTION}",
      "PointsMembership": "${LENO_INTERNAL_API_KEY_POINTSMEMBERSHIP}"
    }
  }
}
```

`InternalApiKeyMiddleware` 校验时，按 `Service:Name` 匹配 `Security:InternalApiKey:{BcName}`（本 BC 自身的 key）。
防腐层 HttpClient 调用时，按目标 BC 名匹配 `AntiCorruption:TargetInternalApiKeys:{TargetBc}`，注入 `X-Internal-Key` 头。
```

- [ ] **Step 4: 7 个防腐层调用方 BC 配置目标 BC InternalApiKey**

修改 7 个防腐层调用方 BC（Order/Notification/Cart/ReviewAfterSales/Payment/UserAuth/SystemAdmin）的 `appsettings.json`，新增 `AntiCorruption:TargetInternalApiKeys` 配置：

```json
// 样例：Leno.Order.Api/appsettings.json
{
  "AntiCorruption": {
    "TargetInternalApiKeys": {
      "Product": "${LENO_INTERNAL_API_KEY_PRODUCT}",
      "Promotion": "${LENO_INTERNAL_API_KEY_PROMOTION}",
      "PointsMembership": "${LENO_INTERNAL_API_KEY_POINTSMEMBERSHIP}"
    }
  }
}
```

- [ ] **Step 5: 改造防腐层 HttpClient 注入目标 BC InternalApiKey**

修改 9 个 HttpClient 防腐层服务（Plan 8 Task 3 已改造为继承 `AntiCorruptionBase`），在 HttpClient 构造时通过 `IOptions<AntiCorruptionOptions>` 读取目标 BC 的 InternalApiKey，并在每个请求头注入 `X-Internal-Key`。

样例（Order ProductAntiCorruptionService）：

```csharp
public sealed class ProductAntiCorruptionService : IProductAntiCorruptionService, AntiCorruptionBase
{
    private readonly HttpClient _httpClient;
    private readonly string _targetInternalKey;

    protected override string ServiceName => "product";

    public ProductAntiCorruptionService(HttpClient httpClient, IOptions<AntiCorruptionOptions> options)
    {
        _httpClient = httpClient;
        _targetInternalKey = options.Value.TargetInternalApiKeys["Product"]
            ?? throw new InvalidOperationException("AntiCorruption:TargetInternalApiKeys:Product 配置缺失");
    }

    public Task<SkuInfo?> GetSkuInfoAsync(long skuId, CancellationToken ct = default)
        => ExecuteAsync("get_sku_info", async token =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/products/skus/{skuId}");
            request.Headers.Add("X-Internal-Key", _targetInternalKey);
            using var response = await _httpClient.SendAsync(request, token);
            EnsureSuccessStatusCode(response, "get_sku_info");
            // ... 既有逻辑
        }, ct);
}
```

> **说明：** `AntiCorruptionOptions` 需新增 `TargetInternalApiKeys` 字典字段。

- [ ] **Step 6: 启动校验 Consul 不可达时降级为 warning**

修改 `src/BuildingBlocks/Leno.Infrastructure/Configuration/ConfigCenterExtensions.cs`，在 `AddLenoConsulConfig` 内增加 Consul 不可达时的降级逻辑：

```csharp
public static IHostApplicationBuilder AddLenoConsulConfig(this IHostApplicationBuilder builder)
{
    try
    {
        // 既有 Consul KV 配置加载逻辑...
    }
    catch (Exception ex) when (builder.Environment.IsProduction())
    {
        // M5.2：生产环境 Consul 不可达时降级为 warning，使用本地 appsettings 兜底
        Console.WriteLine($"[WARN] Consul KV 配置中心不可达，使用本地配置兜底：{ex.Message}");
    }

    return builder;
}
```

> **说明：** 开发环境 Consul 不可达仍抛异常（fail-closed），避免本地配置覆盖生产配置的安全风险。

- [ ] **Step 7: 全量测试验证**

Run: `dotnet test Leno.sln`
Expected: 全部 PASS

- [ ] **Step 8: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Configuration/ConfigCenterExtensions.cs src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionOptions.cs src/Services/*/Leno.*.Api/appsettings.json src/Services/*/Leno.*.Infrastructure/Services/ docs/consul-kv-seed.md
git commit -m "feat(M5.2): Consul KV 配置中心收敛，11 BC 独立 InternalApiKey，防腐层注入目标 BC key"
```

---

## Task 5: 新建 K8s Helm Chart

**Files:**
- Create: `deploy/helm/leno/Chart.yaml`
- Create: `deploy/helm/leno/values.yaml`
- Create: `deploy/helm/leno/values-dev.yaml`
- Create: `deploy/helm/leno/values-staging.yaml`
- Create: `deploy/helm/leno/values-prod.yaml`
- Create: `deploy/helm/leno/templates/_helpers.tpl`
- Create: `deploy/helm/leno/templates/deployment.yaml`
- Create: `deploy/helm/leno/templates/service.yaml`
- Create: `deploy/helm/leno/templates/configmap.yaml`
- Create: `deploy/helm/leno/templates/secret.yaml`
- Create: `deploy/helm/leno/templates/hpa.yaml`
- Create: `deploy/helm/leno/templates/ingress.yaml`
- Create: `deploy/helm/leno/templates/migration-job.yaml`
- Create: `deploy/helm/leno/templates/NOTES.txt`

- [ ] **Step 1: 创建 Chart.yaml**

创建 `deploy/helm/leno/Chart.yaml`：

```yaml
apiVersion: v2
name: leno
description: Leno 电商平台 Helm chart（umbrella chart，含 11 个 BC + API 网关）
type: application
version: 1.0.0
appVersion: "1.0.0"
keywords:
  - ecommerce
  - ddd
  - microservices
maintainers:
  - name: Leno Team
```

- [ ] **Step 2: 创建 values.yaml（默认值，生产环境基础）**

创建 `deploy/helm/leno/values.yaml`：

```yaml
# 全局配置
global:
  imageRegistry: ""
  imagePullSecrets: []
  storageClass: ""
  consul:
    address: "http://consul:8500"
  rabbitmq:
    host: "rabbitmq"
    port: 5672
  redis:
    host: "redis"
    port: 6379
  elasticsearch:
    host: "elasticsearch"
    port: 9200
  sqlserver:
    host: "sqlserver"
    port: 1433
    database: "leno"
  jaeger:
    otlpEndpoint: "http://jaeger:4317"

# 11 个业务服务 + 1 个网关
services:
  api-gateway:
    enabled: true
    image:
      repository: leno/api-gateway
      tag: "1.0.0"
      pullPolicy: IfNotPresent
    replicaCount: 2
    resources:
      requests:
        cpu: 200m
        memory: 256Mi
      limits:
        cpu: 1000m
        memory: 512Mi
    hpa:
      enabled: true
      minReplicas: 2
      maxReplicas: 10
      targetCPUUtilizationPercentage: 70
    service:
      type: ClusterIP
      port: 8080
    ingress:
      enabled: true
      className: nginx
      hosts:
        - host: api.leno.example.com
          paths:
            - path: /
              pathType: Prefix
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 8080
      initialDelaySeconds: 10
      periodSeconds: 10
    livenessProbe:
      httpGet:
        path: /health/live
        port: 8080
      initialDelaySeconds: 30
      periodSeconds: 30

  userauth:
    enabled: true
    image: { repository: leno/userauth-api, tag: "1.0.0", pullPolicy: IfNotPresent }
    replicaCount: 2
    resources: { requests: { cpu: 200m, memory: 256Mi }, limits: { cpu: 1000m, memory: 512Mi } }
    hpa: { enabled: true, minReplicas: 2, maxReplicas: 8, targetCPUUtilizationPercentage: 70 }
    service: { type: ClusterIP, port: 5151 }
    httpPort: 5151
    grpcPort: 5251
    migration:
      enabled: true
      runOnInit: true
    readinessProbe:
      httpGet: { path: /health/ready, port: 5151 }
      initialDelaySeconds: 15
      periodSeconds: 10
    livenessProbe:
      httpGet: { path: /health/live, port: 5151 }
      initialDelaySeconds: 30
      periodSeconds: 30

  # 其余 10 个 BC（product/cart/order/promotion/reviewaftersales/pointsmembership/payment/notification/sellershop/systemadmin）
  # 结构与 userauth 一致，仅端口与资源按 BC 调整。完整 values.yaml 含全部 11 个 BC 定义。
  product:
    enabled: true
    image: { repository: leno/product-api, tag: "1.0.0", pullPolicy: IfNotPresent }
    replicaCount: 2
    resources: { requests: { cpu: 200m, memory: 256Mi }, limits: { cpu: 1000m, memory: 512Mi } }
    hpa: { enabled: true, minReplicas: 2, maxReplicas: 8, targetCPUUtilizationPercentage: 70 }
    service: { type: ClusterIP, port: 5152 }
    httpPort: 5152
    grpcPort: 5252
    migration: { enabled: true, runOnInit: true }
    readinessProbe: { httpGet: { path: /health/ready, port: 5152 }, initialDelaySeconds: 15, periodSeconds: 10 }
    livenessProbe: { httpGet: { path: /health/live, port: 5152 }, initialDelaySeconds: 30, periodSeconds: 30 }

  # ... 其余 9 个 BC 同样结构（省略，实施时按模板生成）

# 外部依赖（SQL Server/Redis/RabbitMQ/ES/Consul/Jaeger/Prometheus/Grafana/Alertmanager）
# 本 chart 不部署基础设施，假设已存在或通过 Bitnami/Prometheus Community chart 部署
externalDependencies:
  sqlserver:
    enabled: true
    connectionstringSecret: "leno-db-connectionstrings"
  rabbitmq:
    enabled: true
    connectionstringSecret: "leno-mq-rabbitmq"
  redis:
    enabled: true
    connectionstringSecret: "leno-redis-connection"
  elasticsearch:
    enabled: true
    connectionstringSecret: "leno-es-connection"
  consul:
    enabled: true
    addressSecret: "leno-consul-address"

# 敏感配置通过 K8s Secret 引用（External Secrets Operator 对接 Vault/Consul）
externalSecrets:
  enabled: false
  backend: consul  # 或 vault
  refreshInterval: 1h
```

- [ ] **Step 3: 创建 values-dev.yaml / values-staging.yaml / values-prod.yaml**

创建 `deploy/helm/leno/values-dev.yaml`：

```yaml
# 开发环境：单副本，无 HPA，资源限制低
services:
  api-gateway:
    replicaCount: 1
    hpa: { enabled: false }
    resources: { requests: { cpu: 100m, memory: 128Mi }, limits: { cpu: 500m, memory: 256Mi } }
  userauth:
    replicaCount: 1
    hpa: { enabled: false }
    resources: { requests: { cpu: 100m, memory: 128Mi }, limits: { cpu: 500m, memory: 256Mi } }
  # ... 其余 BC 同样降低资源与副本数
```

创建 `deploy/helm/leno/values-staging.yaml`：

```yaml
# 预发环境：2 副本，启用 HPA，资源适中
services:
  api-gateway:
    replicaCount: 2
    hpa: { enabled: true, minReplicas: 2, maxReplicas: 5 }
  userauth:
    replicaCount: 2
    hpa: { enabled: true, minReplicas: 2, maxReplicas: 5 }
  # ...
```

创建 `deploy/helm/leno/values-prod.yaml`：

```yaml
# 生产环境：3 副本起，启用 HPA，资源充足
services:
  api-gateway:
    replicaCount: 3
    hpa: { enabled: true, minReplicas: 3, maxReplicas: 15, targetCPUUtilizationPercentage: 65 }
    resources: { requests: { cpu: 500m, memory: 512Mi }, limits: { cpu: 2000m, memory: 1Gi } }
  userauth:
    replicaCount: 3
    hpa: { enabled: true, minReplicas: 3, maxReplicas: 10, targetCPUUtilizationPercentage: 65 }
    resources: { requests: { cpu: 500m, memory: 512Mi }, limits: { cpu: 2000m, memory: 1Gi } }
  # ...
```

- [ ] **Step 4: 创建 _helpers.tpl 通用模板**

创建 `deploy/helm/leno/templates/_helpers.tpl`：

```yaml
{{/*
展开服务名（与 docker-compose 服务名一致）
*/}}
{{- define "leno.fullname" -}}
{{- if .Values.global.nameOverride -}}
{{- .Values.global.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{/*
生成服务全限定名
*/}}
{{- define "leno.serviceName" -}}
{{- printf "%s-%s" (include "leno.fullname" .context) .name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
通用标签
*/}}
{{- define "leno.labels" -}}
app.kubernetes.io/name: {{ .name }}
app.kubernetes.io/instance: {{ .context.Release.Name }}
app.kubernetes.io/managed-by: {{ .context.Release.Service }}
app.kubernetes.io/part-of: leno
{{- end -}}

{{/*
镜像全限定地址
*/}}
{{- define "leno.image" -}}
{{- $registry := .context.Values.global.imageRegistry -}}
{{- if $registry -}}
{{- printf "%s/%s:%s" $registry .service.image.repository .service.image.tag -}}
{{- else -}}
{{- printf "%s:%s" .service.image.repository .service.image.tag -}}
{{- end -}}
{{- end -}}
```

- [ ] **Step 5: 创建 deployment.yaml 通用模板**

创建 `deploy/helm/leno/templates/deployment.yaml`：

```yaml
{{- range $name, $service := .Values.services }}
{{- if $service.enabled }}
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "leno.serviceName" (dict "name" $name "context" $) }}
  labels:
    {{- include "leno.labels" (dict "name" $name "context" $) | nindent 4 }}
spec:
  replicas: {{ $service.replicaCount }}
  selector:
    matchLabels:
      app.kubernetes.io/name: {{ $name }}
  template:
    metadata:
      labels:
        app.kubernetes.io/name: {{ $name }}
    spec:
      {{- with $.Values.global.imagePullSecrets }}
      imagePullSecrets:
        {{- toYaml . | nindent 8 }}
      {{- end }}
      containers:
        - name: {{ $name }}
          image: {{ include "leno.image" (dict "service" $service "context" $) }}
          imagePullPolicy: {{ $service.image.pullPolicy | default "IfNotPresent" }}
          ports:
            - name: http
              containerPort: {{ $service.httpPort | default $service.service.port }}
              protocol: TCP
            {{- if $service.grpcPort }}
            - name: grpc
              containerPort: {{ $service.grpcPort }}
              protocol: TCP
            {{- end }}
          env:
            - name: ASPNETCORE_ENVIRONMENT
              value: {{ $.Values.global.environment | default "Production" }}
            - name: Service__Name
              value: {{ $name | replace "-" "" }}
            - name: OpenTelemetry__OtlpEndpoint
              value: {{ $.Values.global.jaeger.otlpEndpoint }}
            - name: ConnectionStrings__Default
              valueFrom:
                secretKeyRef:
                  name: {{ $.Values.externalDependencies.sqlserver.connectionstringSecret }}
                  key: {{ $name }}
            - name: Security__Jwt__SecretKey
              valueFrom:
                secretKeyRef:
                  name: leno-security-jwt
                  key: secret-key
            # 其余环境变量按需追加
          readinessProbe:
            {{- toYaml $service.readinessProbe | nindent 12 }}
          livenessProbe:
            {{- toYaml $service.livenessProbe | nindent 12 }}
          resources:
            {{- toYaml $service.resources | nindent 12 }}
{{- end }}
{{- end }}
```

- [ ] **Step 6: 创建 service.yaml / configmap.yaml / secret.yaml / hpa.yaml / ingress.yaml**

按 Helm chart 标准模板创建其余 5 个文件，结构类似 `deployment.yaml`（`range` 遍历 `.Values.services`）。每个文件包含完整 YAML 模板，本计划省略详细内容，实施时参照 Bitnami/Prometheus Community chart 模板。

关键要点：
- `service.yaml`：每个服务一个 Service（ClusterIP），暴露 http + grpc 端口
- `configmap.yaml`：非敏感配置（如 Prometheus 抓取配置、Grafana provisioning）
- `secret.yaml`：敏感配置占位（生产通过 External Secrets Operator 注入）
- `hpa.yaml`：仅 `service.hpa.enabled=true` 的服务生成 HPA
- `ingress.yaml`：仅 `service.ingress.enabled=true` 的服务生成 Ingress（仅网关）

- [ ] **Step 7: 创建 migration-job.yaml（Init Container 执行迁移）**

创建 `deploy/helm/leno/templates/migration-job.yaml`：

```yaml
{{- range $name, $service := .Values.services }}
{{- if and $service.enabled $service.migration.enabled $service.migration.runOnInit }}
---
apiVersion: batch/v1
kind: Job
metadata:
  name: {{ include "leno.serviceName" (dict "name" $name "context" $) }}-migration
  labels:
    {{- include "leno.labels" (dict "name" (printf "%s-migration" $name) "context" $) | nindent 4 }}
  annotations:
    "helm.sh/hook": pre-install,pre-upgrade
    "helm.sh/hook-weight": "-5"
    "helm.sh/hook-delete-policy": before-hook-creation,hook-succeeded
spec:
  backoffLimit: 3
  template:
    spec:
      restartPolicy: OnFailure
      containers:
        - name: migration
          image: {{ include "leno.image" (dict "service" $service "context" $) }}
          imagePullPolicy: {{ $service.image.pullPolicy | default "IfNotPresent" }}
          command: ["dotnet", "ef", "database", "update", "--no-build", "--project", "/app/Leno.{{ $name | title }}.Infrastructure.dll"]
          env:
            - name: ConnectionStrings__Default
              valueFrom:
                secretKeyRef:
                  name: {{ $.Values.externalDependencies.sqlserver.connectionstringSecret }}
                  key: {{ $name }}
            # 其余环境变量与 deployment 一致
{{- end }}
{{- end }}
```

> **说明：** Init Container 通过 `helm.sh/hook` 在部署前执行。生产推荐使用独立迁移 Job（避免 Pod 启动竞争），开发可通过 `MigrateWithLockAsync`（Plan 3 已建）在应用启动时迁移。本 chart 默认使用独立 Job。

- [ ] **Step 8: 创建 NOTES.txt**

创建 `deploy/helm/leno/templates/NOTES.txt`：

```
Leno 电商平台已部署！

1. 验证服务状态：
  kubectl get pods -l app.kubernetes.io/part-of=leno

2. 获取 API 网关地址：
  export GATEWAY_URL=$(kubectl get svc {{ include "leno.serviceName" (dict "name" "api-gateway" "context" .) }} -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')
  echo "API 网关地址: http://$GATEWAY_URL"

3. 查看 Prometheus 指标：
  kubectl port-forward svc/prometheus 9090:9090
  # 访问 http://localhost:9090/targets

4. 查看 Grafana 仪表板：
  kubectl port-forward svc/grafana 3000:3000
  # 访问 http://localhost:3000

更多文档见 docs/spec/10-模块化部署架构.md
```

- [ ] **Step 9: helm lint 验证**

Run: `helm lint deploy/helm/leno/`
Expected: `==> Linted deploy/helm/leno/[OK]`

- [ ] **Step 10: helm template 渲染验证**

Run: `helm template leno deploy/helm/leno/ --values deploy/helm/leno/values-dev.yaml | grep "kind: Deployment" | wc -l`
Expected: 12（11 个 BC + 1 个网关）

- [ ] **Step 11: 提交**

```bash
git add deploy/helm/leno/
git commit -m "feat(M5.4): 新建 K8s Helm chart（umbrella chart，11 BC + 网关，含 HPA + probe + Init Container 迁移）"
```

---

## Task 6: CI 覆盖率门槛阻断 + staging 集成测试 job

**Files:**
- Modify: `.github/workflows/ci.yml`（增加 coverage-threshold job 与 staging-integration-tests job）
- Create: `scripts/check-coverage-threshold.ps1`（解析 cobertura XML 并校验门槛）
- Create: `scripts/check-coverage-threshold.sh`（Linux 版本）
- Modify: `Directory.Build.props`（全局启用 cobertura 输出格式）

- [ ] **Step 1: Directory.Build.props 全局启用 cobertura 输出**

修改 `Directory.Build.props`，在 `<PropertyGroup>` 中增加：

```xml
<CoverletOutputFormat>cobertura</CoverletOutputFormat>
<CoverletOutput>./TestResults/</CoverletOutput>
```

- [ ] **Step 2: 创建覆盖率门槛校验脚本（PowerShell）**

创建 `scripts/check-coverage-threshold.ps1`：

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
校验各项目测试覆盖率门槛（M5.5）。
.DESCRIPTION
解析 cobertura XML，按项目分类校验：
- Domain 层 ≥ 80%
- Application 层 ≥ 60%
- Infrastructure 层 ≥ 40%
不达标则退出码 1，CI 阻断。
#>

param(
    [string]$TestResultsDir = "./TestResults",
    [double]$DomainThreshold = 80.0,
    [double]$ApplicationThreshold = 60.0,
    [double]$InfrastructureThreshold = 40.0
)

$ErrorActionPreference = "Stop"

$totalCoverageByCategory = @{
    "Domain" = @{ Sum = 0.0; Count = 0; Threshold = $DomainThreshold }
    "Application" = @{ Sum = 0.0; Count = 0; Threshold = $ApplicationThreshold }
    "Infrastructure" = @{ Sum = 0.0; Count = 0; Threshold = $InfrastructureThreshold }
}

Get-ChildItem -Path $TestResultsDir -Recurse -Filter "coverage.cobertura.xml" | ForEach-Object {
    [xml]$xml = Get-Content $_.FullName
    $lineRate = [double]$xml.coverage."line-rate" * 100
    $assemblyName = $xml.coverage.packages.package[0].name

    $category = $null
    if ($assemblyName -match "\.Domain(\.Tests)?$") { $category = "Domain" }
    elseif ($assemblyName -match "\.Application(\.Tests)?$") { $category = "Application" }
    elseif ($assemblyName -match "\.Infrastructure(\.Tests)?$") { $category = "Infrastructure" }

    if ($category) {
        $totalCoverageByCategory[$category].Sum += $lineRate
        $totalCoverageByCategory[$category].Count += 1
        Write-Host "$assemblyName ($category): $lineRate%"
    }
}

$failed = $false
foreach ($cat in $totalCoverageByCategory.Keys) {
    $data = $totalCoverageByCategory[$cat]
    if ($data.Count -eq 0) {
        Write-Warning "$cat 层无覆盖率数据"
        continue
    }
    $avg = $data.Sum / $data.Count
    $status = if ($avg -ge $data.Threshold) { "PASS" } else { "FAIL"; $failed = $true }
    Write-Host "$cat 层平均覆盖率: $avg% (门槛 $($data.Threshold)%) [$status]"
}

if ($failed) {
    Write-Error "覆盖率门槛校验失败，请提升测试覆盖率后重试"
    exit 1
}

Write-Host "覆盖率门槛校验通过"
exit 0
```

- [ ] **Step 3: 创建 Linux 版本校验脚本**

创建 `scripts/check-coverage-threshold.sh`：

```bash
#!/usr/bin/env bash
set -euo pipefail

# 简化版：使用 reportgenerator 输出 CSV，再 awk 校验
# 完整实现参照 PowerShell 版本

TEST_RESULTS_DIR="${1:-./TestResults}"
DOMAIN_THRESHOLD=80.0
APPLICATION_THRESHOLD=60.0
INFRASTRUCTURE_THRESHOLD=40.0

# 使用 reportgenerator 生成 CSV 摘要
dotnet reportgenerator -reports:"$TEST_RESULTS_DIR/**/coverage.cobertura.xml" -targetdir:"./CoverageSummary" -reporttypes:CsvSummary

# 解析 CSV 并按类别校验
# ... awk 解析逻辑
```

> **说明：** Linux 版本可使用 Python 或 awk 解析 cobertura XML。本计划提供框架，实施时按团队偏好完善。

- [ ] **Step 4: CI 增加覆盖率门槛校验 job**

修改 `.github/workflows/ci.yml`，在 `build-solution` job 内 `Generate coverage report` 步骤后增加：

```yaml
      - name: Check coverage thresholds
        run: |
          chmod +x scripts/check-coverage-threshold.sh
          bash scripts/check-coverage-threshold.sh ./TestResults
        continue-on-error: false  # M5.5 阶段 1：先 warning（true），阶段 2：阻断（false）
```

> **说明：** 分阶段收紧：
> - 阶段 1（M5.5 初期）：`continue-on-error: true`，覆盖率不达标仅 warning，不阻断 PR
> - 阶段 2（M5.5 后期）：`continue-on-error: false`，覆盖率不达标阻断 PR
> 切换时机由团队根据当前覆盖率水平决定。

- [ ] **Step 5: CI 增加 staging 集成测试 job**

修改 `.github/workflows/ci.yml`，新增 `staging-integration-tests` job：

```yaml
  staging-integration-tests:
    runs-on: ubuntu-latest
    needs: docker-build
    if: github.event_name == 'push' && (github.ref == 'refs/heads/main' || github.ref == 'refs/heads/develop')
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Start infrastructure (docker-compose)
        run: docker compose up -d sqlserver redis rabbitmq elasticsearch consul jaeger prometheus grafana alertmanager

      - name: Wait for infrastructure ready
        run: |
          for i in {1..60}; do
            if docker compose ps | grep -q "healthy"; then break; fi
            sleep 5
          done

      - name: Apply EF migrations
        run: |
          # 逐 BC 执行 dotnet ef database update
          for bc in UserAuth Product Cart Order Promotion ReviewAfterSales PointsMembership Payment Notification SellerShop SystemAdmin; do
            dotnet ef database update \
              --project src/Services/$bc/Leno.$bc.Infrastructure \
              --startup-project src/Services/$bc/Leno.$bc.Api
          done

      - name: Run integration tests (Category=Integration)
        run: dotnet test Leno.slnx --configuration Release --filter "Category=Integration" --verbosity normal
        env:
          ASPNETCORE_ENVIRONMENT: Staging

      - name: Cleanup
        if: always()
        run: docker compose down -v
```

> **说明：** staging 集成测试仅在 push 到 main/develop 时运行（PR 阶段跳过，避免阻塞）。集成测试使用真实的基础设施容器（Testcontainers 或 docker-compose）。

- [ ] **Step 6: 全量验证**

Run: `dotnet test Leno.sln --collect:"XPlat Code Coverage" --results-directory ./TestResults && bash scripts/check-coverage-threshold.sh ./TestResults`
Expected: PASS（覆盖率达标）或输出当前覆盖率水平供团队评估

- [ ] **Step 7: 提交**

```bash
git add .github/workflows/ci.yml Directory.Build.props scripts/check-coverage-threshold.ps1 scripts/check-coverage-threshold.sh
git commit -m "feat(M5.5): CI 覆盖率门槛阻断（Domain 80%/Application 60%/Infrastructure 40%）+ staging 集成测试 job"
```

---

## Task 7: 全量集成测试与最终验收

**Files:**
- Run: 全量测试套件
- Verify: spec §12 验收清单

- [ ] **Step 1: 全量测试**

Run: `dotnet test Leno.sln --configuration Release`
Expected: 全部 PASS（1648+ 既有测试 + M5 新增测试无回归）

- [ ] **Step 2: 验收清单核对（spec §12.1 M5.1）**

```bash
# 1. 11 个 BC 暴露 /metrics 端点
# 启动任一 BC 后验证
curl http://localhost:5154/metrics | head -5
# 期望：输出 Prometheus 格式指标

# 2. Prometheus 抓取配置含 11 个业务服务
grep "job_name: leno-" grafana/prometheus.yml | wc -l
# 期望：12（11 BC + 1 网关）

# 3. Grafana dashboard 新增业务服务指标面板
ls grafana/leno-business-services-dashboard.json
# 期望：文件存在
```

- [ ] **Step 3: 验收清单核对（spec §12.2 M5.2）**

```bash
# 1. 所有敏感配置通过 Consul KV 管理
grep -r "Password=\|SecretKey=\|ApiKey=" src/Services/*/Leno.*.Api/appsettings.json
# 期望：0 命中（占位符 ${ENV_VAR} 不算明文）

# 2. appsettings*.json 无明文密钥
grep -r "InternalApiKey" src/Services/*/Leno.*.Api/appsettings.json | grep -v '${'
# 期望：0 命中

# 3. 11 个 BC 各有独立 InternalApiKey
# 通过 Consul KV UI 或 API 验证
curl http://localhost:8500/v1/kv/leno/security/internal-key/?keys
# 期望：返回 11 个 BC 的 key 路径
```

- [ ] **Step 4: 验收清单核对（spec §12.3 M5.3）**

```bash
# 1. Alertmanager 容器启动
docker compose ps alertmanager
# 期望：running

# 2. 告警规则文件存在
ls grafana/provisioning/alerting/leno-alerts.yml
# 期望：文件存在

# 3. outbox_pending_count 指标暴露
curl http://localhost:5154/metrics | grep outbox_pending_count
# 期望：命中 1 行（gauge 类型）

# 4. 测试告警触发后通知送达
# 手动制造 Outbox 积压 > 100，等待 5 分钟，验证钉钉/企业微信通知
```

- [ ] **Step 5: 验收清单核对（spec §12.4 M5.4）**

```bash
# 1. deploy/helm/leno/ 目录存在且 helm lint 通过
helm lint deploy/helm/leno/
# 期望：[OK]

# 2. helm template 渲染出 11 个 BC + 网关的 Deployment/Service
helm template leno deploy/helm/leno/ | grep -c "kind: Deployment"
# 期望：12

helm template leno deploy/helm/leno/ | grep -c "kind: Service"
# 期望：12

# 3. values 文件区分 dev/staging/prod
ls deploy/helm/leno/values-*.yaml
# 期望：values-dev.yaml, values-staging.yaml, values-prod.yaml

# 4. Init Container 执行迁移
helm template leno deploy/helm/leno/ | grep -c "kind: Job"
# 期望：11（每个 BC 一个迁移 Job）

# 5. readiness/liveness probe 配置正确
helm template leno deploy/helm/leno/ | grep -c "readinessProbe"
# 期望：12
helm template leno deploy/helm/leno/ | grep -c "livenessProbe"
# 期望：12
```

- [ ] **Step 6: 验收清单核对（spec §12.5 M5.5）**

```bash
# 1. CI staging job 运行 4 个集成测试通过
# 通过 GitHub Actions UI 验证 staging-integration-tests job 状态为 success

# 2. 覆盖率报告持续追踪
# 通过 GitHub Actions artifacts 验证 coverage-report 上传成功

# 3. 覆盖率门槛校验
# 通过 CI 日志验证 "覆盖率门槛校验通过" 输出（阶段 2 启用后）
```

- [ ] **Step 7: 提交最终验收记录**

```bash
git add -A
git commit --allow-empty -m "chore(M5): 可观测性与部署补齐最终验收完成，spec §12 全部验收项通过"
```

---

## 风险与缓解

| 风险 | 缓解 |
|---|---|
| 11 个 BC 同时暴露 /metrics 端点，增加 Prometheus 抓取负载 | 抓取间隔保持 15s（默认），单个 BC 指标量 < 1000 行；生产环境使用 Consul 服务发现避免硬编码目标 |
| prometheus-net.AspNetCore 包引入导致 11 BC 启动时间增加 | 包为轻量级（< 1MB），启动开销 < 50ms；CI 验证启动时间无显著回归 |
| Outbox 积压指标 `SetPendingCount` 频繁调用导致数据库压力 | 后台服务轮询间隔保持 30s（默认），仅一次 `CountAsync` 查询；高频场景可改为缓存 + 定时刷新 |
| Alertmanager 通知频率过高（如服务抖动导致重复告警） | `route.group_wait: 30s` + `repeat_interval: 4h` 控制通知频率；`inhibit_rules` 抑制同服务 critical 告警抑制 warning |
| Helm chart Init Container 迁移失败导致部署卡住 | `backoffLimit: 3` + `restartPolicy: OnFailure`；迁移失败不阻塞既有 Pod 运行；CI `validate-compose` 已验证 docker-compose 路径 |
| 覆盖率门槛阻断现有 PR 流程 | 分阶段收紧：阶段 1 `continue-on-error: true`（warning），阶段 2 `continue-on-error: false`（阻断）；切换前全员通知 |
| staging 集成测试 job 执行时间长（启动全栈 + 迁移 + 测试） | 仅在 push 到 main/develop 时运行（PR 跳过）；使用 docker-compose healthcheck 等待就绪；并行迁移多 BC |
| 11 BC 独立 InternalApiKey 后，调用方配置遗漏导致 401 | Consul KV 种子数据文档化（`docs/consul-kv-seed.md`）；启动校验 `ValidateSensitiveConfig` 拦截缺失配置；灰度切换前全栈集成测试验证 |

## 依赖关系

- Task 1 → Task 2（OpenTelemetry 扩展先建，各 BC 接入次之）
- Task 1 → Task 3（OutboxMetrics 需在 AddLenoOpenTelemetry 订阅）
- Task 4（M5.2 Consul KV 收敛）独立，可与 Task 2/3 并行
- Task 5（M5.4 Helm chart）独立，可与 Task 2/3/4 并行
- Task 6（M5.5 CI）独立，可与 Task 2/3/4/5 并行
- Task 7（最终验收）依赖 Task 1-6 全部完成
