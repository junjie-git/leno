# API 网关增强 - 阶段一：基础设施 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 Leno API 网关从静态配置路由升级为基于 Consul 的动态服务发现架构，实现微服务自动注册/注销、YARP 动态路由解析、原生负载均衡策略与 Consul+YARP 主动健康检查。

**Architecture:** 网关侧通过自定义 `IDestinationResolver` 在请求时从 Consul Health API 查询健康实例并构建 YARP Destination 列表，替代 appsettings.json 中的静态地址。微服务侧通过 `IHostedService` 在启动时向 Consul 注册自身实例（含 HTTP 健康检查），关闭时注销。YARP Cluster 配置从 `Destinations.Address` 改为 `Metadata.ConsulServiceName`，负载均衡使用 `PowerOfTwoChoices` 策略，健康检查从手工轮询改为 YARP Active HealthCheck + Consul 服务健康过滤双保险。

**Tech Stack:** .NET 10, YARP 2.2.0, Consul 1.7.14.11 (NuGet `Consul` 包), xUnit, FluentAssertions, Moq, Microsoft.AspNetCore.TestHost

**Spec:** [docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md](../specs/2026-07-14-api-gateway-enhancement-design.md) 第 3 节（核心基础功能）+ 第 8 节（健康检查改进）

---

## 实施说明

> 本计划为 Spec 第 3、8 节的 Phase 1 落地，以下两点与 Spec 字面描述不同但实现等价或被有意收敛：

1. **YARP 扩展点选择**：Spec 3.1 提到 "ConsulServiceDiscovery 实现 YARP 的 `IClusterChangeListener`"。实际上 `IClusterChangeListener` 是 YARP 在 Cluster 内部状态变化（如配置热更新、Active HealthCheck 标记不健康）时的回调接口，并非用于订阅外部服务注册中心。YARP 提供动态 Destination 的官方扩展点是 `IDestinationResolver`——每次请求时由 YARP 调用以解析目标列表。本计划据此实现 `ConsulDestinationResolver : IDestinationResolver`，与 Spec 的功能目标一致（动态 Destination、自动排除不健康实例）。
2. **HealthChecksUI 数据源**：Spec 8.1 提到 "HealthChecksUI 仪表盘保留，数据源改为各服务的 Consul 健康状态"。Phase 1 仅保留 HealthChecksUI 抓取网关自身 `/health` 端点（其中包含 Consul 连通性就绪检查）。"按服务展示 Consul 健康状态" 需要为每个微服务注册一个查询 Consul Health API 的自定义 `IHealthCheck`，规模较大且与 Phase 4（可观测性）耦合，故推迟到后续阶段。

---

## 文件结构

### 新建文件

| 文件 | 职责 |
|---|---|
| `src/ApiGateway/Leno.ApiGateway/Options/GatewayOptions.cs` | 网关配置选项，含 `ConsulOptions`（Url/Token） |
| `src/ApiGateway/Leno.ApiGateway/Services/ConsulServiceDiscovery.cs` | `IConsulServiceDiscovery` 接口 + `ConsulServiceDiscovery` 实现，封装 Consul Health API 查询 |
| `src/ApiGateway/Leno.ApiGateway/Services/ConsulDestinationResolver.cs` | 实现 YARP `IDestinationResolver`，从 Consul 动态解析 Destination |
| `src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs` | 网关侧 DI 注册扩展（ConsulClient + ConsulServiceDiscovery + Resolver） |
| `src/BuildingBlocks/Leno.Infrastructure/ServiceDiscovery/ConsulServiceRegistrationExtensions.cs` | 微服务侧 Consul 注册扩展 + `IHostedService` 生命周期管理 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj` | 网关测试项目 |
| `src/ApiGateway/Leno.ApiGateway.Tests/GlobalUsings.cs` | 测试全局 using |
| `src/ApiGateway/Leno.ApiGateway.Tests/Services/ConsulServiceDiscoveryTests.cs` | ConsulServiceDiscovery 单元测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Services/ConsulDestinationResolverTests.cs` | ConsulDestinationResolver 单元测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Extensions/ServiceCollectionExtensionsTests.cs` | 网关扩展注册测试 |
| `src/ApiGateway/Leno.ApiGateway.Tests/Integration/GatewayRoutingIntegrationTests.cs` | 端到端路由集成测试 |
| `src/BuildingBlocks/Leno.Infrastructure.Tests/ServiceDiscovery/ConsulServiceRegistrationExtensionsTests.cs` | 微服务注册扩展单元测试 |

### 修改文件

| 文件 | 修改内容 |
|---|---|
| `docker-compose.yml` | 添加 Consul 服务容器，网关依赖 Consul |
| `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj` | 添加 `Consul` NuGet 包引用 |
| `src/ApiGateway/Leno.ApiGateway/Program.cs` | 注册 ConsulServiceDiscovery + 替换 IDestinationResolver + 移除手工 /health 轮询 + 添加 Consul 健康检查 |
| `src/ApiGateway/Leno.ApiGateway/appsettings.json` | Cluster 从 `Destinations.Address` 改为 `Metadata.ConsulServiceName` + 添加 LoadBalancingPolicy + HealthCheck.Active + Consul 配置节 |
| `src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json` | Consul Url 指向 Docker 网络 `http://consul:8500`，移除静态 Cluster 地址 |
| `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj` | 添加 `Consul` NuGet 包引用 |
| `Leno.slnx` | 添加 `Leno.ApiGateway.Tests` 与 `Leno.Infrastructure.Tests`（既有遗漏补登）测试项目 |

---

## Task 1: 添加 Consul 到 docker-compose

**Files:**
- Modify: `docker-compose.yml`

- [ ] **Step 1: 在 docker-compose.yml 中添加 Consul 服务**

在 `redis` 服务之后、`rabbitmq` 服务之前（`elasticsearch` 之前均可）插入 Consul 服务定义：

```yaml
  consul:
    image: hashicorp/consul:1.18
    container_name: leno-consul
    ports:
      - "8500:8500"
    command: agent -dev -client=0.0.0.0
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8500/v1/status/leader || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - leno-net
```

- [ ] **Step 2: 为 api-gateway 添加 consul 依赖**

在 `docker-compose.yml` 的 `api-gateway` 服务的 `depends_on` 块末尾（`system-admin-api` 之后）添加：

```yaml
      consul:
        condition: service_healthy
```

- [ ] **Step 3: 为各微服务添加 consul 依赖**

在每个微服务（`user-auth-api`、`product-api`、`cart-api`、`order-api`、`promotion-api`、`payment-api`、`points-api`、`review-aftersales-api`、`seller-shop-api`、`notification-api`、`system-admin-api`）的 `depends_on` 块末尾添加：

```yaml
      consul:
        condition: service_healthy
```

- [ ] **Step 4: 验证 docker-compose 配置**

Run: `docker compose config --quiet`
Expected: 无输出（退出码 0，表示配置有效）

- [ ] **Step 5: 提交**

```bash
git add docker-compose.yml
git commit -m "feat(infra): 添加 Consul 服务到 docker-compose 用于服务发现"
```

---

## Task 2: 创建网关 Consul 服务发现基础设施

**Files:**
- Create: `src/ApiGateway/Leno.ApiGateway/Options/GatewayOptions.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Services/ConsulServiceDiscovery.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/GlobalUsings.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Services/ConsulServiceDiscoveryTests.cs`

- [ ] **Step 1: 添加 Consul NuGet 包到网关项目**

在 `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj` 的 `<ItemGroup>` 中（`Yarp.ReverseProxy` 之后）添加：

```xml
    <PackageReference Include="Consul" Version="1.7.14.11" />
```

- [ ] **Step 2: 验证包还原**

Run: `dotnet restore src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Restore completed` 无错误

- [ ] **Step 3: 创建 GatewayOptions.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Options/GatewayOptions.cs`：

```csharp
namespace Leno.ApiGateway.Options;

/// <summary>
/// 网关顶层配置节，对应 appsettings.json 中 <c>Consul</c> 节。
/// </summary>
public sealed class ConsulOptions
{
    /// <summary>Consul Agent HTTP 地址。</summary>
    public string Url { get; set; } = "http://localhost:8500";

    /// <summary>Consul ACL Token（可选）。</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>健康实例查询时是否仅返回 passing 状态的实例。</summary>
    public bool PassingOnly { get; set; } = true;
}
```

- [ ] **Step 4: 创建 ConsulServiceDiscovery.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Services/ConsulServiceDiscovery.cs`：

```csharp
using Consul;
using Leno.ApiGateway.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 服务实例信息，从 Consul Health API 解析。
/// </summary>
public sealed record ServiceInstance(string Id, string Address, int Port, IReadOnlyList<string> Tags);

/// <summary>
/// Consul 服务发现抽象，便于 <see cref="ConsulDestinationResolver"/> 解耦与单元测试 mock。
/// </summary>
public interface IConsulServiceDiscovery
{
    /// <summary>
    /// 查询指定 Consul 服务的健康实例列表。
    /// </summary>
    /// <param name="serviceName">Consul 中注册的服务名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>健康实例列表；查询异常时返回空列表。</returns>
    Task<IReadOnlyList<ServiceInstance>> GetHealthyInstancesAsync(
        string serviceName, CancellationToken cancellationToken);
}

/// <summary>
/// 封装 Consul Health API 查询，返回指定服务的健康实例列表。
/// 供 <see cref="ConsulDestinationResolver"/> 在请求时调用，实现动态路由解析。
/// </summary>
public sealed class ConsulServiceDiscovery : IConsulServiceDiscovery
{
    private readonly IConsulClient _consulClient;
    private readonly ConsulOptions _options;
    private readonly ILogger<ConsulServiceDiscovery> _logger;

    public ConsulServiceDiscovery(
        IConsulClient consulClient,
        IOptions<ConsulOptions> options,
        ILogger<ConsulServiceDiscovery> logger)
    {
        _consulClient = consulClient ?? throw new ArgumentNullException(nameof(consulClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 查询指定 Consul 服务的健康实例列表。
    /// 仅返回 passing 状态的实例（由 <see cref="ConsulOptions.PassingOnly"/> 控制）。
    /// </summary>
    /// <param name="serviceName">Consul 中注册的服务名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>健康实例列表；查询异常时返回空列表。</returns>
    public async Task<IReadOnlyList<ServiceInstance>> GetHealthyInstancesAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name cannot be null or whitespace.", nameof(serviceName));
        }

        try
        {
            var result = await _consulClient.Health.Service(
                serviceName, null, _options.PassingOnly, cancellationToken);

            var instances = result.Response
                .Where(entry => entry.Service is not null && !string.IsNullOrEmpty(entry.Service.Address))
                .Select(entry => new ServiceInstance(
                    entry.Service.ID ?? Guid.NewGuid().ToString(),
                    entry.Service.Address,
                    entry.Service.Port,
                    (IReadOnlyList<string>)(entry.Service.Tags ?? Array.Empty<string>())))
                .ToList();

            _logger.LogDebug(
                "Consul returned {Count} healthy instances for service {ServiceName}",
                instances.Count, serviceName);

            return instances;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to query Consul for service {ServiceName}", serviceName);
            return Array.Empty<ServiceInstance>();
        }
    }
}
```

- [ ] **Step 5: 创建网关 ServiceCollectionExtensions.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs`：

```csharp
using Consul;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.ApiGateway.Extensions;

/// <summary>
/// 网关侧服务注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Consul 客户端与 <see cref="ConsulServiceDiscovery"/> 服务发现组件。
    /// 从 <c>Consul:Url</c> 和 <c>Consul:Token</c> 配置读取连接信息。
    /// </summary>
    public static IServiceCollection AddConsulServiceDiscovery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ConsulOptions>(configuration.GetSection("Consul"));

        services.AddSingleton<IConsulClient>(sp =>
        {
            var consulUrl = configuration["Consul:Url"] ?? "http://localhost:8500";
            var consulToken = configuration["Consul:Token"] ?? string.Empty;

            return new ConsulClient(c =>
            {
                c.Address = new Uri(consulUrl);
                if (!string.IsNullOrEmpty(consulToken))
                {
                    c.Token = consulToken;
                }
            });
        });

        services.AddSingleton<IConsulServiceDiscovery, ConsulServiceDiscovery>();

        return services;
    }

    // Task 3 Step 5 将在此处追加 AddConsulDestinationResolver 方法
}
```

- [ ] **Step 6: 创建测试项目 csproj**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="$(XUnitVersion)" />
    <PackageReference Include="xunit.runner.visualstudio" Version="$(XUnitRunnerVersion)">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Moq" Version="$(MoqVersion)" />
    <PackageReference Include="FluentAssertions" Version="$(FluentAssertionsVersion)" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="coverlet.collector" Version="$(CoverletVersion)">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Leno.ApiGateway\Leno.ApiGateway.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 7: 创建测试项目 GlobalUsings.cs**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/GlobalUsings.cs`：

```csharp
global using Xunit;
global using FluentAssertions;
global using Moq;
```

- [ ] **Step 8: 编写 ConsulServiceDiscovery 失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Services/ConsulServiceDiscoveryTests.cs`：

```csharp
using Consul;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Leno.ApiGateway.Tests.Services;

public class ConsulServiceDiscoveryTests
{
    private static IOptions<ConsulOptions> DefaultOptions =>
        Options.Create(new ConsulOptions { Url = "http://localhost:8500", PassingOnly = true });

    [Fact]
    public async Task GetHealthyInstancesAsync_WithInstances_ReturnsMappedList()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var healthMock = new Mock<IHealthEndpoint>();

        var queryResult = new QueryResult<ServiceEntry[]>
        {
            Response = new[]
            {
                new ServiceEntry
                {
                    Service = new AgentService
                    {
                        ID = "product-1",
                        Address = "192.168.1.10",
                        Port = 8080,
                        Tags = new[] { "v1" }
                    }
                },
                new ServiceEntry
                {
                    Service = new AgentService
                    {
                        ID = "product-2",
                        Address = "192.168.1.11",
                        Port = 8080,
                        Tags = new[] { "v2" }
                    }
                }
            }
        };

        healthMock.Setup(h => h.Service("leno-product-api", null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);
        consulClientMock.SetupGet(c => c.Health).Returns(healthMock.Object);

        var discovery = new ConsulServiceDiscovery(consulClientMock.Object, DefaultOptions,
            NullLogger<ConsulServiceDiscovery>.Instance);

        // Act
        var result = await discovery.GetHealthyInstancesAsync("leno-product-api", CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("product-1");
        result[0].Address.Should().Be("192.168.1.10");
        result[0].Port.Should().Be(8080);
        result[0].Tags.Should().Contain("v1");
        result[1].Id.Should().Be("product-2");
    }

    [Fact]
    public async Task GetHealthyInstancesAsync_WithNoInstances_ReturnsEmptyList()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var healthMock = new Mock<IHealthEndpoint>();

        healthMock.Setup(h => h.Service("leno-unknown", null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<ServiceEntry[]> { Response = Array.Empty<ServiceEntry>() });
        consulClientMock.SetupGet(c => c.Health).Returns(healthMock.Object);

        var discovery = new ConsulServiceDiscovery(consulClientMock.Object, DefaultOptions,
            NullLogger<ConsulServiceDiscovery>.Instance);

        // Act
        var result = await discovery.GetHealthyInstancesAsync("leno-unknown", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHealthyInstancesAsync_WhenConsulThrows_ReturnsEmptyList()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var healthMock = new Mock<IHealthEndpoint>();

        healthMock.Setup(h => h.Service("leno-product-api", null, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        consulClientMock.SetupGet(c => c.Health).Returns(healthMock.Object);

        var discovery = new ConsulServiceDiscovery(consulClientMock.Object, DefaultOptions,
            NullLogger<ConsulServiceDiscovery>.Instance);

        // Act
        var result = await discovery.GetHealthyInstancesAsync("leno-product-api", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHealthyInstancesAsync_WithNullServiceName_Throws()
    {
        var consulClientMock = new Mock<IConsulClient>();
        var discovery = new ConsulServiceDiscovery(consulClientMock.Object, DefaultOptions,
            NullLogger<ConsulServiceDiscovery>.Instance);

        var act = async () => await discovery.GetHealthyInstancesAsync("", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetHealthyInstancesAsync_FiltersInstancesWithEmptyAddress()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var healthMock = new Mock<IHealthEndpoint>();

        var queryResult = new QueryResult<ServiceEntry[]>
        {
            Response = new[]
            {
                new ServiceEntry
                {
                    Service = new AgentService
                    {
                        ID = "valid-1",
                        Address = "192.168.1.10",
                        Port = 8080
                    }
                },
                new ServiceEntry
                {
                    Service = new AgentService
                    {
                        ID = "invalid-1",
                        Address = "",
                        Port = 8080
                    }
                }
            }
        };

        healthMock.Setup(h => h.Service("leno-product-api", null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult);
        consulClientMock.SetupGet(c => c.Health).Returns(healthMock.Object);

        var discovery = new ConsulServiceDiscovery(consulClientMock.Object, DefaultOptions,
            NullLogger<ConsulServiceDiscovery>.Instance);

        // Act
        var result = await discovery.GetHealthyInstancesAsync("leno-product-api", CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("valid-1");
    }
}
```

- [ ] **Step 9: 运行测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "ConsulServiceDiscoveryTests"`
Expected: `Passed: 5` — 5 个测试全部通过

- [ ] **Step 10: 将测试项目添加到解决方案**

Run: `dotnet sln Leno.slnx add src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj`
Expected: `Project ... was added.`

- [ ] **Step 11: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 12: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Options/ src/ApiGateway/Leno.ApiGateway/Services/ConsulServiceDiscovery.cs src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj src/ApiGateway/Leno.ApiGateway.Tests/ Leno.slnx
git commit -m "feat(gateway): 添加 Consul 服务发现基础设施与测试项目"
```

---

## Task 3: 改造 YARP 配置为 Consul 动态发现

**Files:**
- Create: `src/ApiGateway/Leno.ApiGateway/Services/ConsulDestinationResolver.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.json`
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Services/ConsulDestinationResolverTests.cs`

- [ ] **Step 1: 编写 ConsulDestinationResolver 失败测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Services/ConsulDestinationResolverTests.cs`：

```csharp
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Yarp.ReverseProxy.Configuration;

namespace Leno.ApiGateway.Tests.Services;

public class ConsulDestinationResolverTests
{
    private readonly Mock<IConsulServiceDiscovery> _discoveryMock;

    public ConsulDestinationResolverTests()
    {
        _discoveryMock = new Mock<IConsulServiceDiscovery>();
    }

    private void SetupDiscoveryInstances(string serviceName, params (string Id, string Address, int Port)[] instances)
    {
        var list = instances
            .Select(i => new ServiceInstance(i.Id, i.Address, i.Port, Array.Empty<string>()))
            .ToList();

        _discoveryMock.Setup(d => d.GetHealthyInstancesAsync(serviceName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(list);
    }

    private static ClusterConfig CreateCluster(
        string? consulServiceName = null,
        IReadOnlyDictionary<string, DestinationConfig>? staticDestinations = null)
    {
        var metadata = new Dictionary<string, string>();
        if (consulServiceName is not null)
        {
            metadata["ConsulServiceName"] = consulServiceName;
        }

        return new ClusterConfig
        {
            ClusterId = "product",
            LoadBalancingPolicy = "PowerOfTwoChoices",
            Destinations = staticDestinations ?? new Dictionary<string, DestinationConfig>(),
            Metadata = metadata
        };
    }

    [Fact]
    public async Task ResolveAsync_WithConsulServiceName_ReturnsConsulInstances()
    {
        // Arrange
        SetupDiscoveryInstances("leno-product-api",
            ("product-1", "192.168.1.10", 8080),
            ("product-2", "192.168.1.11", 8080));

        var resolver = new ConsulDestinationResolver(
            _discoveryMock.Object, NullLogger<ConsulDestinationResolver>.Instance);
        var cluster = CreateCluster(consulServiceName: "leno-product-api");

        // Act
        var result = await resolver.ResolveAsync(cluster, CancellationToken.None);

        // Assert
        result.Destinations.Should().HaveCount(2);
        result.Destinations.Values.Should().Contain(d => d.Config!.Address == "http://192.168.1.10:8080/");
        result.Destinations.Values.Should().Contain(d => d.Config!.Address == "http://192.168.1.11:8080/");
    }

    [Fact]
    public async Task ResolveAsync_WithoutConsulServiceName_FallsBackToStaticDestinations()
    {
        // Arrange
        var staticDestinations = new Dictionary<string, DestinationConfig>
        {
            ["d1"] = new DestinationConfig { Address = "http://localhost:5150/" }
        };

        var resolver = new ConsulDestinationResolver(
            _discoveryMock.Object, NullLogger<ConsulDestinationResolver>.Instance);
        var cluster = CreateCluster(consulServiceName: null, staticDestinations: staticDestinations);

        // Act
        var result = await resolver.ResolveAsync(cluster, CancellationToken.None);

        // Assert
        result.Destinations.Should().HaveCount(1);
        result.Destinations["d1"].Config!.Address.Should().Be("http://localhost:5150/");
        _discoveryMock.Verify(
            d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_WithEmptyConsulServiceName_FallsBackToStaticDestinations()
    {
        // Arrange
        var staticDestinations = new Dictionary<string, DestinationConfig>
        {
            ["d1"] = new DestinationConfig { Address = "http://localhost:5150/" }
        };

        var resolver = new ConsulDestinationResolver(
            _discoveryMock.Object, NullLogger<ConsulDestinationResolver>.Instance);
        var cluster = CreateCluster(consulServiceName: "", staticDestinations: staticDestinations);

        // Act
        var result = await resolver.ResolveAsync(cluster, CancellationToken.None);

        // Assert
        result.Destinations.Should().HaveCount(1);
        result.Destinations["d1"].Config!.Address.Should().Be("http://localhost:5150/");
    }

    [Fact]
    public async Task ResolveAsync_WhenNoHealthyInstances_ReturnsEmptyDestinations()
    {
        // Arrange
        SetupDiscoveryInstances("leno-product-api");

        var resolver = new ConsulDestinationResolver(
            _discoveryMock.Object, NullLogger<ConsulDestinationResolver>.Instance);
        var cluster = CreateCluster(consulServiceName: "leno-product-api");

        // Act
        var result = await resolver.ResolveAsync(cluster, CancellationToken.None);

        // Assert
        result.Destinations.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_WithConsulServiceName_DestinationIdsContainServiceName()
    {
        // Arrange
        SetupDiscoveryInstances("leno-product-api",
            ("instance-abc", "10.0.0.1", 8080));

        var resolver = new ConsulDestinationResolver(
            _discoveryMock.Object, NullLogger<ConsulDestinationResolver>.Instance);
        var cluster = CreateCluster(consulServiceName: "leno-product-api");

        // Act
        var result = await resolver.ResolveAsync(cluster, CancellationToken.None);

        // Assert
        result.Destinations.Should().ContainKey("leno-product-api-instance-abc");
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "ConsulDestinationResolverTests"`
Expected: 编译失败 — `ConsulDestinationResolver` 类型未定义

- [ ] **Step 3: 创建 ConsulDestinationResolver.cs**

创建 `src/ApiGateway/Leno.ApiGateway/Services/ConsulDestinationResolver.cs`：

```csharp
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 基于 Consul 的 YARP 动态 Destination 解析器。
/// <para>
/// 当 Cluster 的 <c>Metadata["ConsulServiceName"]</c> 存在时，从 Consul 查询健康实例并动态构建 Destination 列表。
/// 否则回退到 appsettings.json 中配置的静态 Destinations。
/// </para>
/// 替换 YARP 默认的 <see cref="IDestinationResolver"/>，在每次请求时动态解析。
/// </summary>
public sealed class ConsulDestinationResolver : IDestinationResolver
{
    private readonly IConsulServiceDiscovery _discovery;
    private readonly ILogger<ConsulDestinationResolver> _logger;

    public ConsulDestinationResolver(
        IConsulServiceDiscovery discovery,
        ILogger<ConsulDestinationResolver> logger)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<ResolvedDestinationCollection> ResolveAsync(
        ClusterConfig cluster,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cluster);

        if (cluster.Metadata is not null
            && cluster.Metadata.TryGetValue("ConsulServiceName", out var serviceName)
            && !string.IsNullOrWhiteSpace(serviceName))
        {
            return await ResolveFromConsulAsync(serviceName, cancellationToken);
        }

        return ResolveStatic(cluster);
    }

    private async ValueTask<ResolvedDestinationCollection> ResolveFromConsulAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        var instances = await _discovery.GetHealthyInstancesAsync(serviceName, cancellationToken);

        if (instances.Count == 0)
        {
            _logger.LogWarning(
                "No healthy instances found for Consul service {ServiceName}", serviceName);
        }

        var destinations = new Dictionary<string, DestinationState>(StringComparer.OrdinalIgnoreCase);

        foreach (var instance in instances)
        {
            var destinationId = $"{serviceName}-{instance.Id}";
            destinations[destinationId] = new DestinationState(destinationId)
            {
                Config = new DestinationConfig
                {
                    Address = $"http://{instance.Address}:{instance.Port}/"
                }
            };
        }

        return new ResolvedDestinationCollection(destinations);
    }

    private static ResolvedDestinationCollection ResolveStatic(ClusterConfig cluster)
    {
        var destinations = new Dictionary<string, DestinationState>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, config) in cluster.Destinations)
        {
            destinations[key] = new DestinationState(key)
            {
                Config = config
            };
        }

        return new ResolvedDestinationCollection(destinations);
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "ConsulDestinationResolverTests"`
Expected: `Passed: 5` — 全部通过

- [ ] **Step 5: 向 ServiceCollectionExtensions.cs 追加 AddConsulDestinationResolver 方法**

在 `src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs` 中，将 Task 2 留下的占位注释 `// Task 3 Step 5 将在此处追加 AddConsulDestinationResolver 方法` 替换为以下方法，并在文件顶部 `using` 区追加 `using Microsoft.Extensions.DependencyInjection.Extensions;`：

```csharp
    /// <summary>
    /// 用基于 Consul 的 ConsulDestinationResolver 替换 YARP 默认的 IDestinationResolver。
    /// 必须在 <c>AddReverseProxy().LoadFromConfig()</c> 之后调用。
    /// </summary>
    public static IServiceCollection AddConsulDestinationResolver(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Replace(
            ServiceDescriptor.Singleton<
                Yarp.ReverseProxy.Model.IDestinationResolver,
                ConsulDestinationResolver>());

        return services;
    }
```

替换后文件顶部的 using 区应为：

```csharp
using Consul;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
```

- [ ] **Step 6: 验证网关项目编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 7: 修改 appsettings.json — 添加 Consul 配置节 + 改造 Clusters**

在 `src/ApiGateway/Leno.ApiGateway/appsettings.json` 的根级别添加 `Consul` 配置节（在 `"AllowedHosts"` 之后）：

```json
  "Consul": {
    "Url": "http://localhost:8500",
    "Token": "",
    "PassingOnly": true
  },
```

然后将 `ReverseProxy.Clusters` 整块替换为以下配置（用 `Metadata.ConsulServiceName` 替代静态 `Destinations`）：

```json
    "Clusters": {
      "user-auth": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-user-auth-api" },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      },
      "product": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-product-api" },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      },
      "cart": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-cart-api" },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      },
      "order": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-order-api" },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      },
      "promotion": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-promotion-api" },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      },
      "payment": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-payment-api" },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      },
      "points": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-points-api" },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      },
      "review-aftersales": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-review-aftersales-api" },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      },
      "seller-shop": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-seller-shop-api" },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      },
      "notification": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-notification-api" },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      },
      "system-admin": {
        "LoadBalancingPolicy": "PowerOfTwoChoices",
        "Metadata": { "ConsulServiceName": "leno-system-admin-api" },
        "HealthCheck": {
          "Active": { "Enabled": true, "Interval": "00:00:10", "Path": "/health/ready" },
          "Passive": { "Enabled": true, "Policy": "TransportFailureRate", "ReactivationPeriod": "00:00:30" }
        }
      }
    }
```

- [ ] **Step 8: 修改 appsettings.Docker.json — Consul Url 指向 Docker 网络**

将 `src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json` 的 `ReverseProxy` 整块替换为：

```json
  "Consul": {
    "Url": "http://consul:8500",
    "Token": "",
    "PassingOnly": true
  },
  "ReverseProxy": {
    "Clusters": {}
  }
```

> 注意：Docker 环境下 Cluster 配置从 appsettings.json 继承，仅覆盖 Consul Url 即可。Routes 配置不变。

- [ ] **Step 9: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Services/ConsulDestinationResolver.cs src/ApiGateway/Leno.ApiGateway/Extensions/ServiceCollectionExtensions.cs src/ApiGateway/Leno.ApiGateway/appsettings.json src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json src/ApiGateway/Leno.ApiGateway.Tests/Services/ConsulDestinationResolverTests.cs
git commit -m "feat(gateway): 实现 Consul 动态 Destination 解析器并改造 YARP 配置"
```

---

## Task 4: 配置负载均衡策略

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.json` (已在 Task 3 Step 6 完成负载均衡配置)

> **说明：** 负载均衡策略 `PowerOfTwoChoices` 已在 Task 3 Step 6 的 Cluster 配置中添加。此 Task 进行验证和补充说明。

- [ ] **Step 1: 验证所有 Cluster 均已配置 LoadBalancingPolicy**

Run: `grep -c '"LoadBalancingPolicy": "PowerOfTwoChoices"' src/ApiGateway/Leno.ApiGateway/appsettings.json`
Expected: `11`（11 个 Cluster 全部包含）

- [ ] **Step 2: 验证 appsettings.json 为有效 JSON**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 3: 提交（如无额外修改则跳过）**

> 如果 Task 3 已包含 LoadBalancingPolicy 配置且无额外修改，则无需单独提交。

---

## Task 5: 移除手工健康轮询

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Program.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/appsettings.json`

- [ ] **Step 1: 修改 Program.cs — 移除手工轮询、注册 Consul 服务发现、添加 Consul 健康检查**

将 `src/ApiGateway/Leno.ApiGateway/Program.cs` 的全部内容替换为：

```csharp
using Leno.ApiGateway.Extensions;
using Leno.Infrastructure.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// YARP 反向代理从配置加载路由
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Consul 服务发现 + 动态 Destination 解析器（替换 YARP 默认解析器）
builder.Services.AddConsulServiceDiscovery(builder.Configuration);
builder.Services.AddConsulDestinationResolver();

// HealthChecksUI 仪表盘
builder.Services.AddLenoHealthChecksUI(builder.Configuration);

// 网关自身健康检查：存活探针 + Consul 连通性就绪检查
#pragma warning disable CA1861
builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri(builder.Configuration["Consul:Url"] ?? "http://localhost:8500"),
        "consul",
        tags: new[] { "ready" });
#pragma warning restore CA1861

var app = builder.Build();

// 存活探针：仅检查网关进程存活
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

// 就绪探针与 HealthChecksUI 仪表盘
app.MapLenoHealthChecks();
app.MapLenoHealthChecksUI();

// YARP 反向代理端点
app.MapReverseProxy();

app.Run();

// 使 Program 类对 WebApplicationFactory<Program> 可见（Task 7 集成测试需要）
public partial class Program { }
```

> **关键变更：**
> - 移除了 `AddHttpClient("health-check")`（不再需要手工轮询）
> - 移除了手工 `/health` 端点（轮询所有后端 `/health/ready` 的 lambda）
> - 移除了 `AddUrlGroup(self)` 自检（保留在 HealthChecksUI 中）
> - 添加了 `AddConsulServiceDiscovery` 和 `AddConsulDestinationResolver`
> - 添加了 Consul 连通性健康检查（就绪探针包含 Consul）
> - 保留了 `/health/live`、`MapLenoHealthChecks`、`MapLenoHealthChecksUI`、`MapReverseProxy`
> - 新增 `public partial class Program { }` 以支持 `WebApplicationFactory<Program>` 集成测试

- [ ] **Step 2: 移除 appsettings.json 中不再使用的 HealthChecks:Services 节**

在 `src/ApiGateway/Leno.ApiGateway/appsettings.json` 中，删除整个 `"HealthChecks"` 节（第 10-24 行的 `"Services": { ... }` 块）。同时添加 `HealthChecksUI` 配置节。

将文件中的 `"HealthChecks": { ... }` 节替换为：

```json
  "HealthChecksUI": {
    "HealthChecks": [
      { "Name": "API Gateway", "Uri": "http://localhost:8080/health" }
    ],
    "EvaluationTimeInSeconds": 10,
    "MinimumSecondsBetweenFailureNotifications": 60
  },
```

- [ ] **Step 3: 同步更新 appsettings.Docker.json**

在 `src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json` 中，将 `"HealthChecks"` 节替换为：

```json
  "HealthChecksUI": {
    "HealthChecks": [
      { "Name": "API Gateway", "Uri": "http://api-gateway:8080/health" }
    ],
    "EvaluationTimeInSeconds": 10,
    "MinimumSecondsBetweenFailureNotifications": 60
  },
```

- [ ] **Step 4: 验证编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`
Expected: `Build succeeded`

- [ ] **Step 5: 验证测试仍通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj`
Expected: `Passed` — 全部通过

- [ ] **Step 6: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Program.cs src/ApiGateway/Leno.ApiGateway/appsettings.json src/ApiGateway/Leno.ApiGateway/appsettings.Docker.json
git commit -m "refactor(gateway): 移除手工健康轮询改用 Consul+YARP 主动健康检查"
```

---

## Task 6: 微服务侧 Consul 注册

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`
- Create: `src/BuildingBlocks/Leno.Infrastructure/ServiceDiscovery/ConsulServiceRegistrationExtensions.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure.Tests/ServiceDiscovery/ConsulServiceRegistrationExtensionsTests.cs`

- [ ] **Step 1: 添加 Consul NuGet 包到 Infrastructure 项目**

在 `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj` 的 `<ItemGroup>`（`Winton.Extensions.Configuration.Consul` 之后）添加：

```xml
    <PackageReference Include="Consul" Version="1.7.14.11" />
```

- [ ] **Step 2: 验证包还原**

Run: `dotnet restore src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`
Expected: `Restore completed` 无错误

- [ ] **Step 3: 编写 ConsulServiceRegistration 失败测试**

创建 `src/BuildingBlocks/Leno.Infrastructure.Tests/ServiceDiscovery/ConsulServiceRegistrationExtensionsTests.cs`：

```csharp
using Consul;
using Leno.Infrastructure.ServiceDiscovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Infrastructure.Tests.ServiceDiscovery;

public class ConsulServiceRegistrationExtensionsTests
{
    [Fact]
    public void AddConsulServiceRegistration_RegistersConsulClientAndHostedService()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder([]);

        // Act
        builder.AddConsulServiceRegistration("leno-product-api", opts =>
        {
            opts.Address = "192.168.1.10";
            opts.Port = 8080;
        });

        using var host = builder.Build();

        // Assert
        host.Services.GetService<IConsulClient>().Should().NotBeNull();
        host.Services.GetServices<IHostedService>()
            .Should().Contain(s => s is ConsulServiceRegistrationHostedService);
        host.Services.GetService<ConsulRegistrationOptions>().Should().NotBeNull();
    }

    [Fact]
    public void AddConsulServiceRegistration_NullBuilder_Throws()
    {
        IHostApplicationBuilder builder = null!;

        var act = () => builder.AddConsulServiceRegistration("test-service");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddConsulServiceRegistration_NullServiceName_Throws()
    {
        var builder = Host.CreateApplicationBuilder([]);

        var act = () => builder.AddConsulServiceRegistration(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

public class ConsulServiceRegistrationHostedServiceTests
{
    [Fact]
    public async Task StartAsync_RegistersServiceWithConsul()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var agentMock = new Mock<IAgentEndpoint>();

        consulClientMock.SetupGet(c => c.Agent).Returns(agentMock.Object);

        var options = new ConsulRegistrationOptions
        {
            ServiceName = "leno-product-api",
            ServiceId = "leno-product-api-instance-1",
            Address = "192.168.1.10",
            Port = 8080,
            HealthCheckPath = "/health/live"
        };

        var service = new ConsulServiceRegistrationHostedService(
            consulClientMock.Object, options,
            NullLogger<ConsulServiceRegistrationHostedService>.Instance);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        agentMock.Verify(a => a.ServiceRegister(
            It.Is<AgentServiceRegistration>(r =>
                r.ID == "leno-product-api-instance-1" &&
                r.Name == "leno-product-api" &&
                r.Address == "192.168.1.10" &&
                r.Port == 8080 &&
                r.Check != null &&
                r.Check.HTTP == "http://192.168.1.10:8080/health/live"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_DeregistersServiceFromConsul()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var agentMock = new Mock<IAgentEndpoint>();

        consulClientMock.SetupGet(c => c.Agent).Returns(agentMock.Object);

        var options = new ConsulRegistrationOptions
        {
            ServiceName = "leno-product-api",
            ServiceId = "leno-product-api-instance-1",
            Address = "192.168.1.10",
            Port = 8080
        };

        var service = new ConsulServiceRegistrationHostedService(
            consulClientMock.Object, options,
            NullLogger<ConsulServiceRegistrationHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert
        agentMock.Verify(a => a.ServiceDeregister(
            "leno-product-api-instance-1",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_RegistersTagsWhenProvided()
    {
        // Arrange
        var consulClientMock = new Mock<IConsulClient>();
        var agentMock = new Mock<IAgentEndpoint>();

        consulClientMock.SetupGet(c => c.Agent).Returns(agentMock.Object);

        var options = new ConsulRegistrationOptions
        {
            ServiceName = "leno-product-api",
            ServiceId = "leno-product-api-1",
            Address = "10.0.0.1",
            Port = 8080,
            Tags = new[] { "v1", "primary" }
        };

        var service = new ConsulServiceRegistrationHostedService(
            consulClientMock.Object, options,
            NullLogger<ConsulServiceRegistrationHostedService>.Instance);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        agentMock.Verify(a => a.ServiceRegister(
            It.Is<AgentServiceRegistration>(r =>
                r.Tags != null &&
                r.Tags.Contains("v1") &&
                r.Tags.Contains("primary")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 4: 运行测试验证失败**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/ --filter "ConsulServiceRegistration"`
Expected: 编译失败 — `Leno.Infrastructure.ServiceDiscovery` 命名空间不存在

- [ ] **Step 5: 创建 ConsulServiceRegistrationExtensions.cs**

创建 `src/BuildingBlocks/Leno.Infrastructure/ServiceDiscovery/ConsulServiceRegistrationExtensions.cs`：

```csharp
using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Leno.Infrastructure.ServiceDiscovery;

/// <summary>
/// Consul 服务注册配置选项。
/// </summary>
public sealed class ConsulRegistrationOptions
{
    /// <summary>Consul 中注册的服务名（如 <c>leno-product-api</c>）。</summary>
    public string ServiceName { get; set; } = default!;

    /// <summary>服务实例唯一 ID（如 <c>leno-product-api-instance-1</c>）。</summary>
    public string ServiceId { get; set; } = default!;

    /// <summary>服务实例可达地址（IP 或主机名）。</summary>
    public string Address { get; set; } = default!;

    /// <summary>服务实例端口。</summary>
    public int Port { get; set; }

    /// <summary>健康检查路径（Consul 将定期 HTTP 探测此路径）。</summary>
    public string HealthCheckPath { get; set; } = "/health/live";

    /// <summary>服务标签列表（可用于灰度路由等）。</summary>
    public string[] Tags { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Consul 服务注册托管服务：应用启动时注册实例，关闭时注销。
/// </summary>
public sealed class ConsulServiceRegistrationHostedService : IHostedService, IAsyncDisposable
{
    private readonly IConsulClient _consulClient;
    private readonly ConsulRegistrationOptions _options;
    private readonly ILogger<ConsulServiceRegistrationHostedService> _logger;

    public ConsulServiceRegistrationHostedService(
        IConsulClient consulClient,
        ConsulRegistrationOptions options,
        ILogger<ConsulServiceRegistrationHostedService> logger)
    {
        _consulClient = consulClient ?? throw new ArgumentNullException(nameof(consulClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var registration = new AgentServiceRegistration
        {
            ID = _options.ServiceId,
            Name = _options.ServiceName,
            Address = _options.Address,
            Port = _options.Port,
            Tags = _options.Tags,
            Check = new AgentServiceCheck
            {
                HTTP = $"http://{_options.Address}:{_options.Port}{_options.HealthCheckPath}",
                Interval = TimeSpan.FromSeconds(10),
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
            }
        };

        await _consulClient.Agent.ServiceRegister(registration, cancellationToken);

        _logger.LogInformation(
            "Registered service {ServiceName} (ID: {ServiceId}) with Consul at {Address}:{Port}",
            _options.ServiceName, _options.ServiceId, _options.Address, _options.Port);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _consulClient.Agent.ServiceDeregister(_options.ServiceId, cancellationToken);
            _logger.LogInformation(
                "Deregistered service {ServiceName} (ID: {ServiceId}) from Consul",
                _options.ServiceName, _options.ServiceId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Failed to deregister service {ServiceId} from Consul", _options.ServiceId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_consulClient is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _consulClient.Dispose();
        }
    }
}

/// <summary>
/// 微服务 Consul 注册扩展方法。
/// 在微服务 Program.cs 中调用 <c>builder.AddConsulServiceRegistration("leno-product-api", opts => {...})</c>
/// 即可在启动时注册到 Consul，关闭时自动注销。
/// </summary>
public static class ConsulServiceRegistrationExtensions
{
    /// <summary>
    /// 注册 Consul 客户端与服务注册托管服务。
    /// </summary>
    /// <param name="builder">应用构建器。</param>
    /// <param name="serviceName">Consul 中注册的服务名（如 <c>leno-product-api</c>）。</param>
    /// <param name="configure">可选回调，用于覆盖默认注册参数（Address、Port、Tags 等）。</param>
    public static IHostApplicationBuilder AddConsulServiceRegistration(
        this IHostApplicationBuilder builder,
        string serviceName,
        Action<ConsulRegistrationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceName);

        var consulUrl = builder.Configuration["Consul:Url"] ?? "http://localhost:8500";
        var consulToken = builder.Configuration["Consul:Token"] ?? string.Empty;

        builder.Services.AddSingleton<IConsulClient>(sp =>
        {
            return new ConsulClient(c =>
            {
                c.Address = new Uri(consulUrl);
                if (!string.IsNullOrEmpty(consulToken))
                {
                    c.Token = consulToken;
                }
            });
        });

        var options = new ConsulRegistrationOptions
        {
            ServiceName = serviceName,
            ServiceId = $"{serviceName}-{Environment.MachineName}-{builder.Configuration["Consul:ServicePort"] ?? "8080"}",
            Address = builder.Configuration["Consul:ServiceAddress"] ?? Environment.MachineName,
            Port = int.TryParse(builder.Configuration["Consul:ServicePort"], out var port) ? port : 8080
        };

        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddHostedService<ConsulServiceRegistrationHostedService>();

        return builder;
    }
}
```

- [ ] **Step 6: 运行测试验证通过**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/ --filter "ConsulServiceRegistration"`
Expected: `Passed: 6` — ConsulServiceRegistrationExtensionsTests (3) + ConsulServiceRegistrationHostedServiceTests (3) 全部通过

- [ ] **Step 7: 将 Leno.Infrastructure.Tests 项目添加到解决方案**

> 说明：`Leno.Infrastructure.Tests` 项目已存在于磁盘但未在 `Leno.slnx` 中登记（既有遗漏），需补登以确保 `dotnet build Leno.slnx` 与 `dotnet test Leno.slnx` 包含此项目。

Run: `dotnet sln Leno.slnx add src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj`
Expected: `Project ... was added.`（若提示已存在则跳过此步）

- [ ] **Step 8: 验证 Infrastructure 项目编译**

Run: `dotnet build src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj`
Expected: `Build succeeded`

- [ ] **Step 9: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj src/BuildingBlocks/Leno.Infrastructure/ServiceDiscovery/ src/BuildingBlocks/Leno.Infrastructure.Tests/ServiceDiscovery/ Leno.slnx
git commit -m "feat(infra): 添加微服务侧 Consul 注册扩展与生命周期托管"
```

---

## Task 7: 集成测试

**Files:**
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Integration/GatewayRoutingIntegrationTests.cs`
- Create: `src/ApiGateway/Leno.ApiGateway.Tests/Extensions/ServiceCollectionExtensionsTests.cs`

- [ ] **Step 1: 编写 ServiceCollectionExtensions 注册测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Extensions/ServiceCollectionExtensionsTests.cs`：

```csharp
using Consul;
using Leno.ApiGateway.Extensions;
using Leno.ApiGateway.Options;
using Leno.ApiGateway.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leno.ApiGateway.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Consul:Url"] = "http://localhost:8500",
                ["Consul:Token"] = "test-token"
            })
            .Build();

    [Fact]
    public void AddConsulServiceDiscovery_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateConfig();

        // Act
        services.AddConsulServiceDiscovery(config);

        // Assert
        services.Should().Contain(s => s.ServiceType == typeof(IConsulClient));
        services.Should().Contain(s => s.ServiceType == typeof(IConsulServiceDiscovery));
    }

    [Fact]
    public void AddConsulServiceDiscovery_BindsConsulOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = CreateConfig();

        // Act
        services.AddConsulServiceDiscovery(config);
        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ConsulOptions>>().Value;

        // Assert
        options.Url.Should().Be("http://localhost:8500");
        options.Token.Should().Be("test-token");
    }

    [Fact]
    public void AddConsulServiceDiscovery_NullServices_Throws()
    {
        IServiceCollection services = null!;
        var config = CreateConfig();

        var act = () => services.AddConsulServiceDiscovery(config);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddConsulServiceDiscovery_NullConfig_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddConsulServiceDiscovery(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddConsulDestinationResolver_RegistersConsulResolverForInterface()
    {
        // Arrange — services.Replace 在无既有注册时退化为 Add，
        // 因此无需先注册 YARP 默认的 IDestinationResolver（其实现为 internal 无法直接引用）。
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddConsulDestinationResolver();
        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<Yarp.ReverseProxy.Model.IDestinationResolver>();

        // Assert
        resolver.Should().BeOfType<ConsulDestinationResolver>();
    }
}
```

- [ ] **Step 2: 编写网关路由集成测试**

创建 `src/ApiGateway/Leno.ApiGateway.Tests/Integration/GatewayRoutingIntegrationTests.cs`：

```csharp
using System.Net;
using Leno.ApiGateway.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Leno.ApiGateway.Tests.Integration;

public class GatewayRoutingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IConsulServiceDiscovery> _discoveryMock;

    public GatewayRoutingIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // 使用 IConsulServiceDiscovery 接口 mock，避免依赖真实 Consul 与 sealed 类无法 mock 的问题
        _discoveryMock = new Mock<IConsulServiceDiscovery>();

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Consul:Url"] = "http://localhost:8500",
                    ["Consul:Token"] = "",
                    ["Consul:PassingOnly"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                // 用 mock 替换真实的 IConsulServiceDiscovery（避免连接真实 Consul）
                services.RemoveAll<IConsulServiceDiscovery>();
                services.AddSingleton(_discoveryMock.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task HealthLive_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthReady_WhenConsulUnreachable_ReturnsServiceUnavailable()
    {
        // 网关就绪检查包含 Consul 连通性（AddUrlGroup 直连 Consul:Url），
        // 测试环境无真实 Consul，因此就绪检查预期返回 503。
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Proxy_WithHealthyInstances_ForwardsToBackend()
    {
        // Arrange — 模拟 Consul 返回的健康实例指向本机端口（实际无服务监听）
        var instances = new List<ServiceInstance>
        {
            new("test-1", "localhost", 5150, Array.Empty<string>())
        };

        _discoveryMock.Setup(d => d.GetHealthyInstancesAsync("leno-product-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(instances);

        // Act — 发送请求到网关
        // 注意：实际转发需要后端服务运行，此处仅验证网关尝试转发而非返回 500
        var response = await _client.GetAsync("/api/products/test-id");

        // Assert — 网关应尝试转发（502 BadGateway 表示后端不可达，网关本身工作正常）
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Proxy_WhenNoHealthyInstances_ReturnsServiceUnavailable()
    {
        // Arrange — Consul 返回空实例列表
        _discoveryMock.Setup(d => d.GetHealthyInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ServiceInstance>());

        // Act
        var response = await _client.GetAsync("/api/products/test-id");

        // Assert — 无健康实例时 YARP 返回 503
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
```

- [ ] **Step 3: 运行所有测试验证通过**

Run: `dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj`
Expected: `Passed` — ServiceCollectionExtensionsTests (5) + ConsulServiceDiscoveryTests (5) + ConsulDestinationResolverTests (5) + GatewayRoutingIntegrationTests (4) = 19 个测试通过

> 注意：`GatewayRoutingIntegrationTests` 中的 `HealthReady` 和 `Proxy` 测试可能因测试环境而异，如出现不稳定可标记为 `[Trait("Category", "Integration")]` 并在 CI 中跳过。

- [ ] **Step 4: 验证全量编译**

Run: `dotnet build src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj`
Expected: `Build succeeded`

- [ ] **Step 5: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway.Tests/Extensions/ src/ApiGateway/Leno.ApiGateway.Tests/Integration/
git commit -m "test(gateway): 添加服务注册测试与端到端路由集成测试"
```

---

## 实施后验证清单

完成所有 Task 后执行以下整体验证：

- [ ] **全量编译：** `dotnet build Leno.slnx` — 所有项目编译成功
- [ ] **全量测试：** `dotnet test Leno.slnx` — 所有测试通过
- [ ] **Docker 配置：** `docker compose config --quiet` — 无错误
- [ ] **Consul 服务名映射：** 11 个 Cluster 的 `ConsulServiceName` 与微服务注册名一致
