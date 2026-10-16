# 双轨下线收口与文档对齐 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 A5 部署残留缺陷、删除最后一个双轨特性开关 `AntiCorruption:UseGrpc`（特性开关 6→0 终态）、完成 SK-05/06 与 UA-08 定性抽查、将三份过时文档回写至与代码现实一致。

**Architecture:** 三块串行——W-B（代码收口：A5 探针缺陷 + 防腐层 gRPC 单轨化）→ W-C（核查定性：SK-05/06、UA-08、前端落点、ORD-09 完整性）→ W-A（文档 SSOT 回写）。B2 采用"删死基建策略链版 + 泛型分发器单轨化（gRPC 唯一 + 熔断 fail-fast）"方案。

**Tech Stack:** .NET 10 / xUnit / PowerShell (PS5) / GitHub Actions / Docker Compose / Consul KV

**设计依据:** `docs/superpowers/specs/2026-10-16-dual-track-closeout-and-commerce-completion-design.md`（R2，已批准）

**验证基线（D-4）:** 单测+契约兜底；仅 Task 10 起 compose 冒烟一次。

---

## W-B1：A5 残留修复

### Task 1: Helm payment 探针端口修正 + 校验脚本探针层

**Files:**
- Modify: `deploy/helm/leno/values.yaml:175-176`
- Modify: `scripts/validate-service-ports.ps1`（Helm 校验循环内，grpcPort 分支之后）

- [ ] **Step 1.1: 修正探针端口（先改坏 fixture 验证脚本会红）**

`values.yaml` 当前 L175-176（payment 服务块内）：

```yaml
    readinessProbe: { httpGet: { path: /health/ready, port: 5158 }, initialDelaySeconds: 15, periodSeconds: 10 }
    livenessProbe: { httpGet: { path: /health/live, port: 5158 }, initialDelaySeconds: 30, periodSeconds: 30 }
```

- [ ] **Step 1.2: 先运行现有脚本确认当前状态（此时探针坏但脚本绿——证明盲区）**

Run: `pwsh -NoProfile -File scripts/validate-service-ports.ps1`
Expected: 末行输出"服务端口三方一致性校验通过……全部一致"，exit 0。

- [ ] **Step 1.3: 在 `validate-service-ports.ps1` 的 Helm 校验循环中追加探针校验分支**

在现有分支（`elseif ($s -match "^grpcPort:\s*(\d+)")` 块，约 L78-83）之后、循环收尾前插入：

```powershell
    elseif ($s -match "^readinessProbe: \{ httpGet: \{ path: (?<p>[^,]+), port: (?<port>\d+) \}") {
      if ([int]$Matches.port -ne $httpPort) {
        $fail.Add("values.yaml $name readinessProbe port $($Matches.port) != manifest httpPort $httpPort")
      }
    }
    elseif ($s -match "^livenessProbe: \{ httpGet: \{ path: (?<p>[^,]+), port: (?<port>\d+) \}") {
      if ([int]$Matches.port -ne $httpPort) {
        $fail.Add("values.yaml $name livenessProbe port $($Matches.port) != manifest httpPort $httpPort")
      }
    }
```

- [ ] **Step 1.4: 验证守护生效（红→绿）**

Run（当前 values.yaml 探针仍为 5158）: `pwsh -NoProfile -File scripts/validate-service-ports.ps1; echo "exit=$LASTEXITCODE"`
Expected: 报错 `values.yaml payment readinessProbe port 5158 != manifest httpPort 5156` 与 liveness 同款，exit=1。

- [ ] **Step 1.5: 修正 values.yaml L175-176 两处 `port: 5158` → `port: 5156`**

- [ ] **Step 1.6: 复跑脚本确认绿**

Run: `pwsh -NoProfile -File scripts/validate-service-ports.ps1; echo "exit=$LASTEXITCODE"`
Expected: 校验通过，exit=0。

- [ ] **Step 1.7: Commit**

```powershell
git add deploy/helm/leno/values.yaml scripts/validate-service-ports.ps1
git commit -m "fix(deploy): 修正 Helm payment 健康探针端口 5158→5156，端口校验脚本纳入探针层（A5 收口）"
```

### Task 2: serviceUrls 自动枚举 + kv-seed 主机名一致性校验

**Files:**
- Modify: `scripts/validate-service-ports.ps1:91-103`（替换硬编码 `$urlMap`）
- Modify: `scripts/validate-service-ports.ps1`（文件末尾追加 kv-seed 校验段）

- [ ] **Step 2.1: 将硬编码 `$urlMap` 段替换为自动枚举**

删除现 L91-103 的 `$urlMap = @{...}` + `foreach` 块，替换为：

```powershell
# 3) serviceUrls 已登记端口校验 —— 自动枚举 values.yaml serviceUrls.* 全部键，
#    键名形如 <X>Api，映射 manifest 服务名：PaymentApi->payment（去 Api 后缀，驼峰转小写无连字符）
$serviceUrlKeys = @()
for ($i = 0; $i -lt $vLines.Count; $i++) {
  if ($vLines[$i] -match '^serviceUrls:\s*$') {
    for ($j = $i + 1; $j -lt $vLines.Count; $j++) {
      if ($vLines[$j] -match '^  ([A-Za-z]+):\s*\{ service: "([a-z0-9-]+)", port: (\d+) \}\s*$') {
        $serviceUrlKeys += [pscustomobject]@{ Key = $Matches[1]; Service = $Matches[2]; Port = [int]$Matches[3] }
      } elseif ($vLines[$j] -match '^\S') { break }
    }
    break
  }
}
foreach ($item in $serviceUrlKeys) {
  # 以 values.yaml 中登记的 service 名反查 manifest（比键名推断更可靠）
  $svc = $manifest.services | Where-Object { $_.name -eq $item.Service -or $_.composeService -like "*$($item.Service)*" } | Select-Object -First 1
  if (-not $svc) { continue }
  if ($item.Port -ne [int]$svc.httpPort) {
    $fail.Add("values.yaml serviceUrls.$($item.Key) port $($item.Port) != manifest $($svc.name) httpPort $($svc.httpPort)")
  }
}
```

- [ ] **Step 2.2: 文件末尾（`exit 0` 之前）追加 kv-seed.json 校验段**

```powershell
# 4) Consul KV 种子 ServiceUrls__* 主机名一致性（形如 http://leno-<name>-api[:port]）
$kvSeedPath = Join-Path $RepoRoot "deploy/consul/kv-seed.json"
if (Test-Path $kvSeedPath) {
  $kvSeed = Get-Content $kvSeedPath -Raw -Encoding UTF8 | ConvertFrom-Json
  $kvEntries = @()
  function Collect-KvKeys($node) {
    if ($null -eq $node) { return }
    if ($node -is [array]) { foreach ($n in $node) { Collect-KvKeys $n }; return }
    if ($node.PSObject.Properties['key']) { $script:kvEntries += $node }
    foreach ($prop in $node.PSObject.Properties) { Collect-KvKeys $prop.Value }
  }
  Collect-KvKeys $kvSeed
  foreach ($entry in $script:kvEntries) {
    if ($entry.key -notmatch 'ServiceUrls__([A-Za-z]+)$') { continue }
    $apiName = $Matches[1]
    $svc = $manifest.services | Where-Object { $_.name -ieq ($apiName -replace 'Api$', '') } | Select-Object -First 1
    if (-not $svc) { continue }
    $url = [string]$entry.value
    $expectedHost = "leno-$($svc.name)-api"
    if ($url -notmatch [regex]::Escape($expectedHost)) {
      $fail.Add("kv-seed.json $($entry.key) 值 '$url' 未指向 manifest 服务的预期主机 '$expectedHost'")
    }
  }
}
```

- [ ] **Step 2.3: 验证（绿）+ 人为破坏验证（红）+ 恢复**

Run: `pwsh -NoProfile -File scripts/validate-service-ports.ps1; echo "exit=$LASTEXITCODE"`
Expected: exit=0。

破坏验证：临时把 kv-seed.json 中任一 ServiceUrls__ 值改为错误主机名 → 脚本报错 exit=1 → 还原 → 再跑 exit=0。

- [ ] **Step 2.4: Commit**

```powershell
git add scripts/validate-service-ports.ps1
git commit -m "chore(deploy): 端口校验脚本 serviceUrls 自动枚举全覆盖 + kv-seed 主机名一致性校验（A5 收口）"
```

---

## W-B2：AntiCorruption:UseGrpc 单轨化（开关 6→0）

> 现状事实（盘点已证实）：
> - 泛型 `AntiCorruptionDispatcher<TService>`（`src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/AntiCorruptionDispatcher.cs:201-303`）持 httpImpl+grpcImpl 双实现，运行时按 `AntiCorruptionOptions.UseGrpc` 选路，gRPC 不可用降级 HTTP。
> - 策略链版（`AntiCorruptionDispatcher` 非泛型 + `IAclChannel` 全家）为死基建：`AddAclStrategyChain` 无任何 BC 调用，`UseStrategyChain` 默认 false 且无 BC 依赖。
> - 使用方：Order（Product/Promotion/Points 三防腐点）、Review（Order/Product 两防腐点）、Cart（CartPrice/ProductSnapshot 两防腐点）；HTTP 实现类与 else 分支并存。
> - 服务端门控：9 处 `Program.cs` 的 `if (AntiCorruption:UseGrpc)` 才 `MapGrpcService`。

### Task 3: 泛型 AntiCorruptionDispatcher 单轨化改造

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/AntiCorruptionDispatcher.cs:191-303`（泛型类整体替换）
- Test: `src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/AntiCorruptionDispatcherTests.cs`（存在同名即改；不存在则新建）

- [ ] **Step 3.1: 写失败测试——gRPC 唯一路径语义**

新建/改造测试文件，完整内容：

```csharp
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Leno.Infrastructure.AntiCorruption;
using Xunit;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class AntiCorruptionDispatcherTests
{
    private sealed class FakeService
    {
        public int Calls;
        public Task<string> OkAsync() { Calls++; return Task.FromResult("ok"); }
        public Task<string> FailAsync() { Calls++; throw new AntiCorruptionException("grpc_unavailable", "gRPC 不可用"); }
        public Task<string> BizFailAsync() { Calls++; throw new AntiCorruptionException("not_found", "业务不存在"); }
    }

    private static (AntiCorruptionDispatcher<FakeService>, FakeService, CircuitBreakerState) Create()
    {
        var impl = new FakeService();
        // 位置参数与 Order DI 现文同构：new CircuitBreakerState(name, failureThreshold, successThreshold, openDuration)
        var cb = new CircuitBreakerState("test", 3, 2, TimeSpan.FromSeconds(30));
        return (new AntiCorruptionDispatcher<FakeService>(impl, NullLogger<AntiCorruptionDispatcher<FakeService>>.Instance, "test", cb), impl, cb);
    }

    [Fact]
    public async Task ExecuteAsync_Success_RecordsCircuitSuccess()
    {
        var (dispatcher, impl, _) = Create();
        var result = await dispatcher.ExecuteAsync(s => s.OkAsync());
        Assert.Equal("ok", result);
        Assert.Equal(1, impl.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_BusinessFailure_PropagatesWithoutRetry()
    {
        var (dispatcher, impl, _) = Create();
        await Assert.ThrowsAsync<AntiCorruptionException>(() => dispatcher.ExecuteAsync(s => s.BizFailAsync()));
        Assert.Equal(1, impl.Calls); // 业务失败不重试，异常原样上抛
    }

    [Fact]
    public async Task ExecuteAsync_OpenCircuit_FailsFast_WithoutCallingImplementation()
    {
        var (dispatcher, impl, cb) = Create();
        // 连续 3 次失败 → 熔断 Open
        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<AntiCorruptionException>(() => dispatcher.ExecuteAsync(s => s.FailAsync()));
        }
        Assert.Equal(CircuitState.Open, cb.GetState());

        // Open 期间 fail-fast：不再触达实现，抛 AntiCorruptionException
        var callsBefore = impl.Calls;
        var ex = await Assert.ThrowsAsync<AntiCorruptionException>(() => dispatcher.ExecuteAsync(s => s.OkAsync()));
        Assert.Contains("熔断 Open", ex.Message);
        Assert.Equal(callsBefore, impl.Calls);
    }
}
```

- [ ] **Step 3.2: 运行测试确认编译失败（旧构造签名 6 参）**

Run: `dotnet build src/BuildingBlocks/Leno.Infrastructure.Tests -v q`
Expected: FAIL——`AntiCorruptionDispatcher<FakeService>` 无 4 参构造。

- [ ] **Step 3.3: 替换泛型类实现（文件 L191-303 的 `AntiCorruptionDispatcher<TService>` 整体替换为下述代码；非泛型策略链类暂不动，Task 4 删）**

```csharp
/// <summary>
/// 防腐层调度器（双轨下线单轨版）。
/// 持有唯一 gRPC 实现，调用经熔断器保护：
/// 1. 熔断 Open 时 fail-fast 抛 <see cref="AntiCorruptionException"/>（D-10：不静默降级 HTTP）
/// 2. 业务异常（非 gRPC 不可用）直接上抛，不计重试
/// 3. 任何 <see cref="AntiCorruptionException"/> 计入熔断失败后原样上抛
/// 4. 熔断器为 Keyed Singleton（每个防腐层一个实例），跨请求累积失败计数
/// </summary>
public sealed class AntiCorruptionDispatcher<TService> : IDisposable
    where TService : class
{
    private readonly TService _implementation;
    private readonly ILogger<AntiCorruptionDispatcher<TService>> _logger;
    private readonly CircuitBreakerState _circuitBreaker;
    private readonly string _serviceName;

    public AntiCorruptionDispatcher(
        TService implementation,
        ILogger<AntiCorruptionDispatcher<TService>> logger,
        string serviceName,
        CircuitBreakerState circuitBreaker)
    {
        ArgumentNullException.ThrowIfNull(implementation);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(circuitBreaker);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        _implementation = implementation;
        _logger = logger;
        _serviceName = serviceName;
        _circuitBreaker = circuitBreaker;
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<TService, Task<TResult>> operation,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var state = _circuitBreaker.GetState();
        if (state == CircuitState.Open)
        {
            _logger.LogWarning("AntiCorruption {Service} gRPC circuit open, failing fast", _serviceName);
            AntiCorruptionMetrics.RecordFallback(_serviceName, "circuit_open");
            throw new AntiCorruptionException(
                $"AntiCorruption {_serviceName} gRPC 熔断 Open，调用被拒绝（fail-fast，无 HTTP 降级路径）");
        }

        try
        {
            var result = await operation(_implementation).ConfigureAwait(false);
            _circuitBreaker.RecordSuccess();
            return result;
        }
        catch (AntiCorruptionException ex)
        {
            _circuitBreaker.RecordFailure();
            _logger.LogWarning(ex, "AntiCorruption {Service} gRPC call failed", _serviceName);
            AntiCorruptionMetrics.RecordFallback(_serviceName, ExtractReason(ex));
            throw;
        }
    }

    private static string ExtractReason(AntiCorruptionException ex)
        => ex.InnerException is RpcException rpc ? $"grpc_{rpc.StatusCode}" : ex.Code;

    public void Dispose()
    {
        // 不 Dispose _circuitBreaker —— 它是 KeyedSingleton，由 DI 容器管理生命周期。
        GC.SuppressFinalize(this);
    }
}
```

同时删除文件头 L1 `using Grpc.Core;` 之上不再使用的 `using Microsoft.Extensions.Options;`（泛型类不再用 IOptionsMonitor；非泛型类不用 Options）——保留 `using Grpc.Core;`（ExtractReason 仍用）与 `using Microsoft.Extensions.Logging;`。

- [ ] **Step 3.4: 跑本测试 + AntiCorruption 工程全部测试**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests --filter "FullyQualifiedName~AntiCorruption" -v q`
Expected: 3 个新用例 PASS；旧"HTTP 降级/UseGrpc 选路"用例编译错误 → 逐一删除（它们验证的行为已被 D-10 废除）。

- [ ] **Step 3.5: Commit**

```powershell
git add src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/AntiCorruptionDispatcher.cs src/BuildingBlocks/Leno.Infrastructure.Tests
git commit -m "refactor(acl): 泛型防腐调度器单轨化——gRPC 唯一路径 + 熔断 fail-fast，删除 UseGrpc 运行时选路"
```

### Task 4: 删除策略链死基建（未接线的 IAclChannel 全家）

**Files:**
- Delete: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/AntiCorruptionDispatcher.cs` 中的非泛型 `AntiCorruptionDispatcher` 类（L28-189）
- Delete: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/AclChannelRegistry.cs`
- Delete: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/IAclChannel.cs`
- Delete: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/IAclRequestHandler.cs`
- Delete: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/AclChannelBase.cs`
- Delete: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/AclChannelException.cs`
- Delete: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/AclRequest.cs`
- Delete: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/AclResponse.cs`
- Delete: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/HttpAclChannel.cs`
- Delete: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/GrpcAclChannel.cs`
- Delete: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/DependencyInjection/AntiCorruptionExtensions.cs`（AddAclStrategyChain 全家 + AclChannelBuilder）
- Modify: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/AntiCorruptionOptions.cs`（删 `UseStrategyChain` 属性）
- Delete: 策略链相关测试（执行时 `Glob src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/Acl*` 与 `*Channel*` 定位，命中即删）

- [ ] **Step 4.1: 前置确认死基建判定依然成立（防期间有人接线）**

Run: `git grep -n "AddAclStrategyChain\|IAclChannel" -- src/Services`
Expected: 无输出。若有输出 → STOP，向用户报告（有人接入了策略链，改道方案需重议）。

- [ ] **Step 4.2: 执行上表删除与修改**

`AntiCorruptionOptions.cs` 中删除 `UseStrategyChain` 属性及其注释行（保留 UseGrpc/GrpcEndpoints/TargetInternalApiKeys/CircuitBreaker 等仍被消费的成员——执行时以编译器报错为准逐个确认；UseGrpc 属性在 Task 8 完成后于 Task 9 统一删）。

- [ ] **Step 4.3: 编译 AntiCorruption 工程 + 全解决方案**

Run: `dotnet build Leno.slnx -v q`
Expected: 0 error。若报 AclChannelException/AclRequest 等残留引用 → 按报错文件定位并改为泛型路径等价物（预期只有 AntiCorruptionDispatcher.cs 文件头 using 清理）。

- [ ] **Step 4.4: 跑受影响测试**

Run: `dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests -v q`
Expected: 绿（策略链测试已删，其余不受影响）。

- [ ] **Step 4.5: Commit**

```powershell
git add -A src/BuildingBlocks/Leno.Infrastructure.AntiCorruption src/BuildingBlocks/Leno.Infrastructure.Tests
git commit -m "refactor(acl): 删除未接线的策略链防腐基建（IAclChannel 全家+UseStrategyChain）——保留泛型单轨版为唯一形态"
```

### Task 5: Order 防腐层注册单轨化（3 防腐点）

**Files:**
- Modify: `src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:60-203`（Product/Promotion/Points 三段双轨注册 + else 分支）
- Delete: `src/Services/Order/Leno.Order.Infrastructure/AntiCorruption/ProductAntiCorruptionService.cs`、`PromotionAntiCorruptionService.cs`、`PointsAntiCorruptionService.cs`（三个 HTTP 实现；执行时以 `git grep "class ProductAntiCorruptionService" -- src/Services/Order` 定位真实路径）
- Test: 相关 Order.Infrastructure.Tests 中断言 HTTP 分支的用例（执行时 grep `UseGrpc` 定位删除）

- [ ] **Step 5.1: Points 段改造（before = L159-192 已确认）**

将 `AntiCorruptionDispatcher<IPointsAntiCorruptionService>` 工厂（L180-189）替换为：

```csharp
            services.AddScoped<AntiCorruptionDispatcher<IPointsAntiCorruptionService>>(sp =>
            {
                var grpcImpl = sp.GetRequiredService<GrpcPointsAntiCorruptionClient>();
                var logger = sp.GetRequiredService<ILogger<AntiCorruptionDispatcher<IPointsAntiCorruptionService>>>();
                var cb = sp.GetRequiredKeyedService<CircuitBreakerState>("points");
                return new AntiCorruptionDispatcher<IPointsAntiCorruptionService>(grpcImpl, logger, "points", cb);
            });
```

- [ ] **Step 5.2: 删除外层 `if (antiCorruptionOptions.UseGrpc)` 门控与整个 else 分支（L194-203）**

Product/Promotion/Points 三段的 gRPC 注册代码整体提升到方法顶层（去掉一层缩进），else 分支（直接注册 HTTP 实现）删除。同时删除 Product/Promotion 段中 `AddHttpClient<ProductAntiCorruptionService>` / `AddHttpClient<PromotionAntiCorruptionService>` 式的 HTTP 实现注册与 `httpImpl` 变量（执行时定位各段 `var httpImpl = sp.GetRequiredService<...AntiCorruptionService>();` 行删除）。

- [ ] **Step 5.3: 删除三个 HTTP 实现类文件（Step 5.2 前置确认无其他引用）**

Run: `git grep -n "ProductAntiCorruptionService\b" -- src/Services/Order | findstr /V "Grpc Dispatcher Adapter"`
Expected: 仅剩将被删除的定义与注册行。确认后 Delete 文件。

- [ ] **Step 5.4: 保留 fail-fast 语义**

Points 段现有 `?? throw new InvalidOperationException("AntiCorruption:GrpcEndpoints:Points 配置缺失")`（L160-161）即 fail-fast，确认 Product/Promotion 段存在同款缺失即抛（执行时若无则补同款）。

- [ ] **Step 5.5: 编译 + Order 全部测试**

Run: `dotnet test src/Services/Order -v q`
Expected: 绿（HTTP 分支测试已删）。

- [ ] **Step 5.6: Commit**

```powershell
git add -A src/Services/Order
git commit -m "refactor(order): 防腐层 Product/Promotion/Points 三点单轨化，删 HTTP 实现与 UseGrpc 分支（gRPC 唯一）"
```

### Task 6: Review 防腐层单轨化（2 防腐点 + 删降级类）

**Files:**
- Modify: `src/Services/Review/Leno.Review.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:60-210`
- Delete: `src/Services/Review/Leno.Review.Infrastructure/Services/NullProductInfoQueryService.cs`
- Delete: `src/Services/Review/Leno.Review.Infrastructure/Services/HttpOrderStatusProvider.cs`

- [ ] **Step 6.1: 前置引用确认**

Run: `git grep -n "NullProductInfoQueryService\|HttpOrderStatusProvider" -- src/Services/Review`
Expected: 仅 DI 注册处与自身定义引用。测试文件如有引用一并处理（改走 gRPC 实现语义或删除该用例）。

- [ ] **Step 6.2: 两防腐点（IOrderStatusProvider / IProductInfoQueryService）改造为 Task 5 同款模式**

Dispatcher 工厂改为 4 参构造（只传 grpcImpl），删除 `AddHttpClient<HttpOrderStatusProvider>` / HTTP 注册与 else 分支；`LogWarning 降级提示`（L202 区域）随 else 分支删除；GrpcEndpoints 配置缺失改抛 `InvalidOperationException`（fail-fast，与 Order 一致）。

- [ ] **Step 6.3: 删除 NullProductInfoQueryService.cs 与 HttpOrderStatusProvider.cs**

- [ ] **Step 6.4: 编译 + Review 全部测试**

Run: `dotnet test src/Services/Review -v q`
Expected: 绿。

- [ ] **Step 6.5: Commit**

```powershell
git add -A src/Services/Review
git commit -m "refactor(review): 防腐层 Order/Product 两点单轨化，删 Null 降级类与 Http 实现类（fail-fast）"
```

### Task 7: Cart 防腐层单轨化（2 防腐点）

**Files:**
- Modify: `src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs:80-170`
- Delete: `src/Services/Cart/Leno.Cart.Infrastructure/Services/CartPriceService.cs`（HTTP 实现，执行时以 `git grep "class CartPriceService" -- src/Services/Cart` 定位；注意保留 `SnapshotCartPriceService` 装饰器与 `GrpcCartPriceService`）

- [ ] **Step 7.1: CartPrice 段改造**

Dispatcher 工厂 4 参化（只传 `GrpcCartPriceService`），删 `AddHttpClient<CartPriceService>` 注册与 else 注释块（L105-109）；`ProductInternalServiceClient`/`CircuitBreakerState("product")` 注册保留。

- [ ] **Step 7.2: ProductSnapshot 段改造（L139-170 区域）**

`if (antiCorruptionOptions.UseGrpc)` 门控删除，gRPC 注册提升；Dispatcher 工厂 4 参化（只传 `GrpcProductSnapshotAntiCorruptionClient`）；删 `AddHttpClient<ProductSnapshotAntiCorruptionService>`（注释"保留作为降级备份"一并删除）与 else 分支。

- [ ] **Step 7.3: SnapshotCartPriceService 装饰器 inner 解析简化（L125-137）**

```csharp
        services.AddScoped<ICartPriceService>(sp =>
        {
            // 双轨下线 B2：gRPC 唯一内部实现，不再按 UseGrpc 分派
            ICartPriceService inner = sp.GetRequiredService<CartPriceDispatcherAdapter>();

            var dbContext = sp.GetRequiredService<CartDbContext>();
            var refresher = sp.GetRequiredService<IBackgroundSnapshotRefresher>();
            var options = sp.GetRequiredService<IOptionsMonitor<CartSnapshotOptions>>();
            var logger = sp.GetRequiredService<ILogger<SnapshotCartPriceService>>();
            return new SnapshotCartPriceService(inner, dbContext, refresher, options, logger);
        });
```

- [ ] **Step 7.4: 删除 CartPriceService.cs（HTTP 实现）**

- [ ] **Step 7.5: 编译 + Cart 全部测试**

Run: `dotnet test src/Services/Cart -v q`
Expected: 绿。

- [ ] **Step 7.6: Commit**

```powershell
git add -A src/Services/Cart
git commit -m "refactor(cart): 防腐层 CartPrice/ProductSnapshot 两点单轨化，删 HTTP 降级实现（gRPC 唯一）"
```

### Task 8: 9 处 Program.cs gRPC 服务端门控删除

**Files:**
- Modify（同构 9 处，锚点各 ~L45-50）:
  - `src/Services/Product/Leno.Product.Api/Program.cs:45-50`
  - `src/Services/Points/Leno.Points.Api/Program.cs:59-64`
  - `src/Services/Order/Leno.Order.Api/Program.cs:45-50`
  - `src/Services/Review/Leno.Review.Api/Program.cs:45-50`
  - `src/Services/SellerShop/Leno.SellerShop.Api/Program.cs:46-51`
  - `src/Services/Payment/Leno.Payment.Api/Program.cs:45-50`
  - `src/Services/Cart/Leno.Cart.Api/Program.cs:67-72`
  - `src/Services/Promotion/Leno.Promotion.Api/Program.cs:49-54`
  - `src/Services/AccessControl/Leno.AccessControl.Api/Program.cs:44-50`

- [ ] **Step 8.1: 逐文件替换（9 处同构；以 AccessControl 为基准）**

before（各文件实测行号见上表）：

```csharp
// M4 双轨方案：启用 gRPC 服务端（AccessControlService：CheckPermission / GetUserRoles）
// 仅当 AntiCorruption:UseGrpc=true 时映射，与 AddLenoApi 内 AddGrpc 的注册条件保持一致，
// 否则 Host 无 AddGrpc 服务、映射即抛 "Unable to find the required services" 启动失败。
if (builder.Configuration.GetValue<bool>("AntiCorruption:UseGrpc"))
{
    app.MapGrpcService<AccessControlGrpcService>();
}
```

after（`MapGrpcService<Xxx>` 泛型实参按各文件原样保留）：

```csharp
// gRPC 服务端无条件映射（双轨下线 B2：AntiCorruption:UseGrpc 开关已删除）
app.MapGrpcService<AccessControlGrpcService>();
```

- [ ] **Step 8.2: 确认各 Api 工程的 `builder.Services.AddGrpc()` 注册处不再被同款 if 包裹**

Run: `git grep -n -B2 -A2 "AddGrpc()" -- "src/Services/**/Program.cs" "src/Services/**/Startup*.cs"`
Expected: 各处无条件注册（若仍被 `UseGrpc` if 包裹则同步展开）。

- [ ] **Step 8.3: 编译 + 全解决方案测试**

Run: `dotnet build Leno.slnx -v q; dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests -v q`
Expected: 0 error / 全绿。

- [ ] **Step 8.4: Commit**

```powershell
git add -A src/Services
git commit -m "refactor(api): 9 处 gRPC 服务端映射去 UseGrpc 门控，无条件注册（双轨下线 B2）"
```

### Task 9: 配置物料清理 + CI 守护 job

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure.AntiCorruption/AntiCorruption/AntiCorruptionOptions.cs`（删 `UseGrpc` 属性）
- Modify: `src/BuildingBlocks/Leno.Infrastructure.Persistence/Configuration/ConsulConfigWatcher.cs:23`（删 `UseGrpcConfigKey` 常量与专 case）
- Modify: `src/BuildingBlocks/Leno.Infrastructure.Tests/Configuration/ConsulReloadableConfigurationProviderTests.cs`、`ConsulConfigWatcherTests.cs`（键名换中性测试键）
- Modify: `deploy/docs/consul-kv-coverage-audit.md:118`（UseGrpc 行改为"已删除"记载）
- Modify: `.github/workflows/ci.yml`（新增守护 job）
- Modify: 各处历史注释（`git grep "UseGrpc\|双轨" -- src` 逐条改陈述现状）

- [ ] **Step 9.1: Options/Cleaner——删 `UseGrpc` 属性后编译器暴露全部残留消费点，逐一处理**

Run: `dotnet build Leno.slnx -v q`
Expected: 首轮若干 error（`antiCorruptionOptions.UseGrpc` 残留消费）。按报错逐文件处理：消费点全部删除（本次重构后语义上不应再有分支）。

- [ ] **Step 9.2: ConsulConfigWatcher 键清理**

删除 `private const string UseGrpcConfigKey = "AntiCorruption:UseGrpc";`（L23）及其专属监听/分支逻辑；两个测试文件中的 `"AntiCorruption:UseGrpc"` 字符串全部替换为 `"Test:ReloadableKey"`，断言语义不变。

- [ ] **Step 9.3: 新增 CI 守护 job（插入 `.github/workflows/ci.yml` 的 `validate-service-ports` job 之后）**

```yaml
  # 双轨下线守护：六个历史特性开关名在源代码中零命中（docs/ 目录豁免，保留历史记载）
  guard-legacy-flags:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: 检查历史特性开关零命中
        shell: pwsh
        run: |
          $patterns = 'UseSagaStateMachine','UsePaymentProcessManager','UseRuleEngine','UseSkuSnapshot','Auth:Mode','AntiCorruption:UseGrpc','UseStrategyChain'
          $hits = git grep -n -E ($patterns -join '|') -- 'src' 'web' 'deploy' || $true
          if ($hits) {
            Write-Output "::error::历史特性开关残留命中："
            $hits | ForEach-Object { Write-Output $_ }
            exit 1
          }
          Write-Output "守护通过：7 个历史开关名零命中。"
```

- [ ] **Step 9.4: 本地预演守护（当前应绿——src 内仅存注释引用，需先清理）**

Run: `git grep -n -E "UseGrpc" -- src | findstr /V "obj bin"`
Expected: 仅剩注释——将 `StockConfirmConsumer.cs:19`、`PointsConfirmConsumer.cs:19`、`PaymentSucceededEventConsumer.cs:18`、`PromotionCalculateAppService.cs:9` 等处"双轨期 feature flag ..."注释改为陈述现状（例：`// 支付后流程由 ProcessManager 唯一协调（B2 2026-10-16：UsePaymentProcessManager 开关已删除）`）。

- [ ] **Step 9.5: 全量构建 + 全测试 + 守护脚本本地跑**

Run: `dotnet build Leno.slnx -v q; dotnet test Leno.slnx -v q`
Expected: 全绿。

- [ ] **Step 9.6: Commit**

```powershell
git add -A
git commit -m "refactor(acl): 删除 AntiCorruption:UseGrpc/UseStrategyChain 配置与热更新键，新增历史开关名 CI 守护（开关 6→0）"
```

### Task 10: compose 冒烟（D-4 高风险项验证）+ B2 收口验证

- [ ] **Step 10.1: 起 compose（沿用库存重构 2026-09-24 物料模式）**

Run: `docker compose up -d consul sqlserver redis rabbitmq order-api inventory-api product-api points-api`
Expected: 全部 healthy。

- [ ] **Step 10.2: 验证 fail-fast 与正常链路**

1. 从 Consul 删掉 `AntiCorruption/GrpcEndpoints/Points` 配置 → 重启 order-api 容器 → Expected: 启动失败，日志含"AntiCorruption:GrpcEndpoints:Points 配置缺失"（fail-fast 生效）。
2. 恢复配置 → 重启 → 下单 → 预占/确认库存 → Expected: 全链路成功，order-api 日志无 "falling back to HTTP"。

- [ ] **Step 10.3: 终态 grep 验收**

Run: `git grep -n -E "UseSagaStateMachine|UsePaymentProcessManager|UseRuleEngine|UseSkuSnapshot|Auth:Mode|AntiCorruption:UseGrpc|UseStrategyChain" -- src web deploy`
Expected: 零命中。

- [ ] **Step 10.4: Commit 收口记录（docs/双轨下线-收口记录.md 骨架，最终内容 W-A 完成后补全）+ compose down**

```powershell
docker compose down
git add -A
git commit -m "test(e2e): B2 单轨化 compose 冒烟通过——fail-fast 与 gRPC 全链路验证"
```

---

## W-C：抽查定性（每项产出记入收口记录）

### Task 11: SK-05 布隆过滤器 / SK-06 健康检查 UI 定性

- [ ] **Step 11.1**: `git grep -n -i "bloomfilter\|Bloom Filter" -- src` 与 `git grep -n "HealthChecks.UI\|HealthChecksUi" -- src`
- [ ] **Step 11.2**: 定性写入 `docs/双轨下线-收口记录.md` §W-C：已实现（附证据）/ 未实现（记入遗留，见设计 §7——不扩 scope）。

### Task 12: UA-08 裁决（STOP 用户检查点）

- [ ] **Step 12.1**: 向用户呈现事实——审计采集已由 SystemAdmin `AuditLogConsumer` 事件聚合承担（SYS-05 ✅），Identity 无 HTTP 审计中间件；提问：a) 事件聚合即终态，UA-08 关闭；b) 补 Identity 侧 `UseAuditLogMiddleware`。
- [ ] **Step 12.2**: 记录裁决进收口记录；选 b) 时按缺口单独列任务（默认移交，不并入本计划）。

### Task 13: 前端落点抽查

- [ ] **Step 13.1**: buyer-app——`git grep -n "cart/selection" -- web/buyer-app`（CART-05 调用）。
- [ ] **Step 13.2**: system-admin——`Glob web/system-admin/src/modules/**/views/*Reconciliation*.vue`、`*PaymentChannel*.vue`（PAY-05/06 页面）。
- [ ] **Step 13.3**: operations——`OrderManagement.vue` 内 grep `force-cancel`（ORD-09 按钮）；`ProductAudit.vue` 内 grep `admin/products/all`（PRD-06 数据源）。
- [ ] **Step 13.4**: 缺口写入收口记录；**发现缺口即 STOP 报告用户**（默认不擅自扩 scope）。

### Task 14: ORD-09 完整性复核

- [ ] **Step 14.1**: 读 `src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs` 的 ForceCancel 实现，对照任务验收：已支付→退款事件、待支付→释放库存/积分/优惠券、审计、双通知、Admin 授权。
- [ ] **Step 14.2**: 逐条打勾记录进收口记录；有缺口 STOP 报告。

---

## W-A：文档 SSOT 回写

### Task 15: progress.md 回写

- [ ] **Step 15.1**: 19 个任务行按 W-C 定性结果更新状态列（✅/🟡/⬜+移交说明），完成日期 2026-09-23/24，负责人 AI Agent。
- [ ] **Step 15.2**: 重算 §1.1 全局统计（已完成/待开始/完成率）。
- [ ] **Step 15.3**: Commit：`docs(progress): 19 项 P2 任务状态与代码现实对齐（2026-10-16 盘点）`

### Task 16: 双轨下线-实施方案.md §7 回写

- [ ] **Step 16.1**: §7.1 已完成表追加批次 1/2/W4 全部项（证据指向 `docs/双轨下线-收口记录.md`）。
- [ ] **Step 16.2**: §7.2 未完成表清空为"全部收口（2026-10-16）；仅 E2/E3 与库存遗留 3/4 项移交"。
- [ ] **Step 16.3**: Commit：`docs: 双轨下线实施方案 §7 权威状态回写——19 项全部闭合`

### Task 17: 收尾方案状态头 + 设计文档终态 + 收口记录补全

- [ ] **Step 17.1**: `docs/双轨下线-最优收尾方案.md` 头部追加："> 实施完成：2026-09-23/24 · 核实收口：2026-10-16（特性开关 6→0）"。
- [ ] **Step 17.2**: 本设计文档 R2 状态行改"已完成"，验收总表逐行打勾。
- [ ] **Step 17.3**: 补全 `docs/双轨下线-收口记录.md`（四块证据：B1 探针修复、B2 开关 6→0 + compose 冒烟、W-C 定性表、文档回写清单）。
- [ ] **Step 17.4**: 最终 Commit：`docs: 双轨下线收口记录定稿——特性开关 6→0，SSOT 三文档对齐`

---

## 自审记录（已执行）

1. **Spec 覆盖**：R2 §2（B1/B2）→ Task 1-10；§3（C1-C4）→ Task 11-14；§4（A1-A4）→ Task 15-17。无缺口。
2. **占位扫描**：无 TBD/TODO；所有"执行时定位"均给出 grep 命令与预期输出，非占位。
3. **类型一致性**：`AntiCorruptionDispatcher<TService>` 新签名 4 参（implementation/logger/serviceName/circuitBreaker）在 Task 3 定义、Task 5/6/7 调用一致；`CircuitState.Open`/`CircuitBreakerState(failureThreshold, successThreshold, openDuration)` 与现有 `CircuitBreakerState.cs` 用法一致。
