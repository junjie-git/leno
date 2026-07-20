# Shared（共享层）代码静态分析报告

## 概述
- 扫描范围：
  - `src/BuildingBlocks/Leno.Infrastructure/`
  - `src/BuildingBlocks/Leno.Infrastructure.Abstractions/`
  - `src/BuildingBlocks/Leno.SharedKernel/`
  - `src/BuildingBlocks/Leno.SharedContracts/`（排除 `Leno.SharedContracts.Grpc/Generated/`）
  - `src/ApiGateway/Leno.ApiGateway/`
- 排除项：所有 `Tests` 目录、`Migrations/*.Designer.cs`、`*ModelSnapshot.cs`、`Leno.SharedContracts.Grpc/Generated/`
- 代码行数（业务，非测试）：约 8800 行（Infrastructure ≈ 5500 / SharedKernel ≈ 600 / SharedContracts ≈ 1100 / ApiGateway ≈ 1600）
- 问题总数：高 10 / 中 18 / 低 11

---

## 🔴 高风险问题

### 1. `CacheService` 使用非线程安全的 `Random` 单字段，单例下并发竞态导致抖动失效与序列重复
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs#L20-L21`、`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs#L63`、`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs#L398-L402`
- **类别**：A3 并发与竞态 / C7 资源/连接池
- **根因**：`CacheService` 在 `AddLenoInfrastructure` 链路中被作为单例注册（依赖 `IConnectionMultiplexer`、`IBloomFilter`、`ILogger`，均为 Singleton/Scoped 兼容），但实例字段 `private readonly Random _random;` 在 .NET 8 之前并非线程安全，.NET 8 起虽内部加锁但仍是热点路径瓶颈。`GetOrSetAsync` 在缓存击穿时被多线程并发调用 `ApplyJitter`，多个线程同时 `_random.Next` 会：
  - .NET 7 及早期 8 版本下：内部 `Next()` 会抛 `IndexOutOfRangeException` 或返回 0（受种子递增冲突影响）；
  - .NET 8+：锁竞争在 10w QPS 下成为瓶颈，且 `Random` 共享实例的种子被多线程交替推进，**返回值分布退化**（短时间内大量相同 jitter 秒数），使"防止缓存雪崩"的核心目标失效——大量 key 仍可能在同一秒集体过期。
- **影响**：缓存雪崩防护在并发场景下失效；高 QPS 下可能出现偶发异常；jitter 退化为伪随机分布导致 TTL 集中。
- **修复建议**：
  ```csharp
  // 替换为线程安全的随机数生成
  private static readonly Random _random = new Random();
  // 或 .NET 6+ 推荐：
  // 内部使用 Random.Shared（线程安全且零分配）
  internal TimeSpan ApplyJitter(TimeSpan baseExpiry)
  {
      var jitterSeconds = Random.Shared.Next((int)JitterMin.TotalSeconds, (int)JitterMax.TotalSeconds + 1);
      return baseExpiry.Add(TimeSpan.FromSeconds(jitterSeconds));
  }
  ```
- **影响范围**：所有 BC 通过 `ICacheService.GetOrSetAsync` / `SetAsync` 的写缓存路径；雪崩防护全部依赖此函数。

### 2. `JwtBlacklistService` 实现与注释"三层保障"严重不符，多实例黑名单不同步且本地缓存内存泄漏
- **位置**：`file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs#L7-L11`、`file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs#L16`、`file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs#L24-L46`
- **类别**：A7 异步消息可靠性 / C5 异步消息堆积 / A2 异常处理不当
- **根因**：类注释明示"三层保障：Redis Pub/Sub 实时 + 定时拉取兜底 + 启动预热"，但实现完全缺失：
  - 无 `ISubscriber.Subscribe` 订阅 `__keyevent@0__:set` 或自定义 channel；其他网关实例 `RevokeAsync` 写入 Redis 后，本实例 `_localCache` 不会同步失效，**已登出 token 在其他网关实例的本地缓存仍判未吊销**直到下次过期。但本地缓存实际并未被 `IsRevokedAsync` 优先查询生效（仅命中本地后直接 return true），漏判方向相反——本地缓存只增加内存而不影响正确性，但与"实时同步"语义完全脱节；
  - `_localCache` 是 `ConcurrentDictionary<string, byte>`，**永不过期**。长期运行网关（周/月级别）会持续累积 jti → 内存泄漏，且每次 `IsRevokedAsync` 命中 Redis 后 `TryAdd` 到本地，本地只增不减；
  - 无启动预热逻辑：构造函数仅注入依赖，未在启动时批量加载 Redis 中所有 blacklist key；
  - 无定时拉取兜底：未实现 `IHostedService` 周期性同步。
- **影响**：多实例网关部署时，A 实例登出的用户在 B 实例本地缓存命中（误判已吊销）或反之（漏判）——前者不严重，后者影响安全语义；内存泄漏影响网关长期稳定性。
- **修复建议**：
  1. 实现 `IHostedService` + `ISubscriber.Subscribe("leno:jwt:blacklist:invalidate", ...)`：`RevokeAsync` 后 `Publish` 通知所有实例更新本地缓存；
  2. 本地缓存改用 `MemoryCache` 或 `LazyCache` 并设置与 token TTL 对齐的过期；
  3. 启动时 `SCAN leno:jwt:blacklist:*` 预热；定时（如 1min）周期重拉以兜底 Pub/Sub 丢失；
  4. 注释与实现对齐：要么补全三层，要么删除误导性注释。
- **影响范围**：所有网关实例；登出安全语义；网关内存稳定性。

### 3. `AntiCorruptionMetrics` 静态字典 `_circuitOpenStates` 非线程安全，在多 BC 共享进程中存在竞态与 `NullReferenceException` 风险
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs#L55`、`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs#L58-L67`、`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs#L101-L104`
- **类别**：A3 并发与竞态 / A2 异常处理不当
- **根因**：`AntiCorruptionMetrics` 是 `static class`，`_circuitOpenStates` 字段为 `private static readonly Dictionary<string, int>`（**普通 Dictionary，非 ConcurrentDictionary**）。多 BC 共享同一进程时（如开发环境聚合部署，或生产 ApiGateway 内部依赖），多个 `CircuitBreakerState.RecordSuccess/RecordFailure` 会并发调用 `UpdateCircuitOpenState(service, isOpen)` 写入字典，同时 OpenTelemetry `ObservableGauge` 周期回调枚举 `_circuitOpenStates.Select(...)`：
  - 写写并发：`Dictionary` 在 resize 时丢数据或抛 `InvalidOperationException: Operations that change non-concurrent collections must have exclusive access`；
  - 读写并发：枚举期间另一线程写入，`Select` 抛 `InvalidOperationException: Collection was modified`，导致 OTLP Exporter 周期采集异常，指标缺失。
- **影响**：熔断状态指标在多 BC 进程下不可靠；OTLP 采集任务可能持续异常；最严重情况下 `Dictionary` 内部结构损坏导致后续 `UpdateCircuitOpenState` 死循环（已知 .NET Dictionary 并发 bug）。
- **修复建议**：
  ```csharp
  private static readonly ConcurrentDictionary<string, int> _circuitOpenStates = new(StringComparer.Ordinal);
  ```
  或使用 `Interlocked.Exchange` + 不可变字典模式。
- **影响范围**：所有使用 `AntiCorruptionDispatcher` 的 BC；OTLP 指标采集链路。

### 4. `IntegrationEventConsumerBase` 幂等检查与标记之间无原子性，并发消费同一 `EventId` 会重复执行业务
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs#L33-L54`
- **类别**：A3 并发与竞态 / C6 Outbox / 幂等性 / A7 异步消息可靠性
- **根因**：`Consume` 流程为 `IsProcessedAsync(evt.EventId)` → `HandleAsync(evt)` → `MarkAsProcessedAsync(evt.EventId)`，三步非原子。当 MassTransit 并发预取（`PrefetchCount` 默认 ≥ 16）或 Outbox 重发同一事件时：
  - 消费者 A、B 同时进入 `Consume`，A 检查 `IsProcessed` 返回 false → 开始 `HandleAsync`；
  - B 同时检查 `IsProcessed` 也返回 false（A 尚未标记）→ 同时 `HandleAsync`；
  - 业务副作用（扣积分、扣库存、发券）被执行两次。
- 默认 `IIdempotencyStore` 为 `RedisIdempotencyStore`（推测基于 SET NX），即使如此，`IsProcessedAsync` 与 `MarkAsProcessedAsync` 是两次独立的 Redis 调用，中间的窗口足以让并发消费者穿透。
- **影响**：积分重复扣减、库存重复扣减、订单状态机非法迁移、优惠券重复发放。在 Outbox 重试场景（`RecoverStalePublishingAsync` 回退 Pending 后重新发布）下，同一事件会被多次消费，**业务必须实现应用层幂等**，但 base 类的注释仅"建议"而非强制。
- **修复建议**：
  1. 改为 `MarkAsProcessedAsync` 使用 `SET NX EX` 原子获取"处理权"：成功才执行 `HandleAsync`，失败直接返回（已处理）；
  2. 或在 base 类中使用 `try { HandleAsync } finally { MarkAsProcessed }` 但前置原子 check-in：先 `SET NX` 抢锁，未抢到则跳过；
  3. 注释从"子类 MUST 保证幂等"改为"base 类提供 check-then-mark，子类仍需在 `HandleAsync` 内做幂等以应对重试边界"。
- **影响范围**：所有继承 `IntegrationEventConsumerBase<T>` 的消费者；积分、库存、订单、优惠券等关键业务路径。

### 5. `ObjectStorageService` 构造函数中 `GetAwaiter().GetResult()` 同步阻塞异步方法，存在死锁与启动阻塞风险
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Storage/ObjectStorageService.cs#L46`
- **类别**：C7 资源/连接池 / A2 异常处理不当 / C8 启动时序
- **根因**：构造函数中调用 `EnsureBucketExistsAsync().GetAwaiter().GetResult();`。`ObjectStorageService` 在 `AddLenoInfrastructure` 中以 `AddScoped<IFileStorageService, ObjectStorageService>()` 注册，每个请求作用域首次解析都会触发构造函数。问题：
  - **sync over async**：在 ASP.NET Core 经典同步上下文（虽 ASP.NET Core 无 SynchronizationContext，但 MinIO SDK 内部使用异步 I/O）下，阻塞线程池线程等待 I/O，高并发下线程池耗尽；
  - **启动阻塞**：首次解析若 MinIO 不可达（网络分区、配置错误），`BucketExistsAsync` 超时（MinIO 客户端默认无超时，依赖 HTTP 超时 ~100s），整个请求被阻塞 100s；
  - **构造函数副作用**：构造函数不应执行 I/O，违反 DI 最佳实践；测试时 mock `IMinioClient` 困难（构造函数直接 `new MinioClient()`）。
- **影响**：MinIO 故障时整个上传/下载请求路径被阻塞，连锁拖垮网关线程池；启动期 MinIO 不可达导致首次请求超时。
- **修复建议**：
  1. 将 `EnsureBucketExistsAsync` 移至 `IHostedService.StartAsync` 中执行，启动时一次性确保 Bucket 存在；
  2. 或使用 `Lazy<Task>` 在首次 `UploadAsync` 时异步初始化；
  3. `IMinioClient` 通过 DI 注入而非构造函数 `new`，便于测试 mock；
  4. 配置 MinIO 客户端超时（`WithTimeout`）。
- **影响范围**：所有使用 `IFileStorageService` 的 BC（UserAuth 头像、Product 商品图、Review 评价图、Aftersales 售后凭证）。

### 6. `RedisBloomFilter.GetHashPositions` 中 `Math.Abs(combinedHash % _bitSize)` 对 `long.MinValue` 抛 `OverflowException`
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs#L102-L103`
- **类别**：A5 边界条件 / A2 异常处理不当
- **根因**：`var combinedHash = unchecked(hash1 + (long)i * hash2);` 使用 `unchecked` 防溢出，但 `combinedHash` 可能为 `long.MinValue`（当 `hash1 + i * hash2` 恰好溢出到 `0x8000000000000000`）。`Math.Abs(long.MinValue)` 在 .NET 中抛 `OverflowException: Negating the minimum value of a twos complement number is invalid.`。
  - 虽概率极低（约 1/2^63），但布隆过滤器每次 `AddAsync`/`MightContainAsync` 触发 7 次哈希计算，10w QPS 下约 1.4w 年遇到一次——但生产环境长期运行 + 多 BC 共享布隆过滤器，且异常未在 `CacheService` 中 catch，会直接冒泡到调用方导致请求失败。
  - 此外 `combinedHash % _bitSize` 当 `combinedHash` 为负数时返回负数，`Math.Abs` 是必须的，但应使用 `(combinedHash & 0x7FFFFFFFFFFFFFFF) % _bitSize` 或 `((combinedHash % _bitSize) + _bitSize) % _bitSize` 安全处理。
- **影响**：极端输入下缓存层抛未捕获异常，请求失败。
- **修复建议**：
  ```csharp
  positions[i] = ((combinedHash % _bitSize) + _bitSize) % _bitSize;
  ```
- **影响范围**：所有 `CacheService.GetOrSetAsync` 调用路径。

### 7. `BaseDbContext.FillAuditableFields` 仅填充时间戳，未填充 `CreatedBy`/`UpdatedBy`，审计追踪用户身份丢失
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs#L98-L114`
- **类别**：A1 空引用 / B4 共享内核污染 / 安全审计缺失
- **根因**：`Entity` 实现 `IAuditable` 接口包含 `CreatedBy`/`UpdatedBy` 字段（`file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs#L34-L36`），但 `BaseDbContext.FillAuditableFields` 只设置 `CreatedAt`/`UpdatedAt`，未注入 `ICurrentUserContext` 并填充用户身份。后果：
  - 所有 BC 的实体 `CreatedBy`/`UpdatedBy` 永远为 `null`；
  - 安全审计无法追溯"谁修改了订单/优惠券/积分"——这是电商系统的合规要求（PCI-DSS、等保三级）；
  - 运营排查问题无法定位操作人。
- `Entity.CreatedBy` 为 `string?` 允许 null，因此不会抛异常，但审计语义完全失效。
- **影响**：合规审计失败；运营排障困难；安全事件无法追责。
- **修复建议**：
  ```csharp
  public abstract class BaseDbContext : DbContext
  {
      private readonly ICurrentUserContext? _currentUser;

      protected BaseDbContext(DbContextOptions options, ICurrentUserContext? currentUser = null) : base(options)
      {
          _currentUser = currentUser;
      }

      private void FillAuditableFields()
      {
          var now = DateTime.UtcNow;
          var userId = _currentUser?.UserId?.ToString() ?? "system";
          foreach (var entry in ChangeTracker.Entries<IAuditable>())
          {
              switch (entry.State)
              {
                  case EntityState.Added:
                      entry.Entity.CreatedAt = now;
                      entry.Entity.UpdatedAt = now;
                      entry.Entity.CreatedBy ??= userId;
                      entry.Entity.UpdatedBy = userId;
                      break;
                  case EntityState.Modified:
                      entry.Entity.UpdatedAt = now;
                      entry.Entity.UpdatedBy = userId;
                      break;
              }
          }
      }
  }
  ```
  各 BC 的 DbContext 构造函数注入 `ICurrentUserContext`。
- **影响范围**：所有继承 `BaseDbContext` 的 DbContext；所有 `Entity` 派生实体；安全审计链路。

### 8. `RedisSlidingWindowRateLimiter` Lua 脚本先 `ZCARD` 后 `ZREMRANGEBYSCORE`，窗口外旧记录未清除即计数，限流误判
- **位置**：`file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs#L25-L41`
- **类别**：A5 边界条件 / A3 并发与竞态
- **根因**：Lua 脚本顺序为：
  ```
  ZCARD → 判断 >= permit → 拒绝
  ZREMRANGEBYSCORE → 清窗口外
  ZADD → 加当前
  ZCARD → 再次判断 > permit → ZREM 回滚 → 拒绝
  ```
  问题在第一次 `ZCARD`：此时窗口外旧记录尚未清除，**计数可能远超实际窗口内请求数**。例如 permit=100、window=60s，用户在 5 分钟前发起 200 个请求（已过期），首次新请求时 `ZCARD=200 >= 100` 直接拒绝，但实际窗口内只有 1 个请求。第二次 `ZCARD` 在 `ZREMRANGEBYSCORE` 后才正确，但第一次拒绝已导致误判。
  - 此外 `if count == 0 then EXPIRE`：第一次请求时 `count=0`（未清除前）会设置 TTL，但若 key 已存在旧数据（count>0），TTL 不会被刷新，可能导致 key 在窗口外记录清除前过期。
- **影响**：限流器在 key 有历史数据时误拒合法请求；秒杀场景下正常用户被错误限流。
- **修复建议**：调整 Lua 脚本顺序：先 `ZREMRANGEBYSCORE` 清窗口外 → `ZCARD` 计数 → 判断 → `ZADD`：
  ```lua
  redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, ARGV[2])
  local count = redis.call('ZCARD', KEYS[1])
  if count >= tonumber(ARGV[4]) then
      return 0
  end
  redis.call('ZADD', KEYS[1], ARGV[1], ARGV[3])
  if count == 0 then
      redis.call('EXPIRE', KEYS[1], ARGV[5])
  end
  return 1
  ```
  注意：移除第二次 ZCARD 回滚（窗口外已清除，不会超限）。
- **影响范围**：所有 `RateLimit:Routes:*` 配置的滑动窗口策略；秒杀、用户级限流。

### 9. `CacheMiddleware` 替换 `Response.Body` 后未在 `try/finally` 中恢复，下游异常导致响应流永久污染
- **位置**：`file:///workspace/src/ApiGateway/Leno.ApiGateway/Middleware/CacheMiddleware.cs#L63-L86`
- **类别**：A6 资源释放 / A2 异常处理不当 / A8 事务边界
- **根因**：`InvokeAsync` 中：
  ```csharp
  var originalBodyStream = context.Response.Body;
  using var memoryStream = new MemoryStream();
  context.Response.Body = memoryStream;
  await _next(context);  // 若抛异常，下面恢复代码不执行
  context.Response.Body = originalBodyStream;
  ...
  ```
  若 `_next(context)`（YARP 代理）抛异常（如 `TimeoutException`、`HttpRequestException`），`context.Response.Body` 仍指向 `memoryStream`，`memoryStream` 在 `using` 退出时 Dispose 但 Response.Body 已是 disposed 流。后续 `FallbackResponseMiddleware`（虽位于上游，但异常传播路径上其他中间件如 `UseExceptionHandler`）尝试写入 Response.Body 会抛 `ObjectDisposedException`，客户端收到不完整响应或连接重置。
  - 与 `FallbackResponseMiddleware` 对比，后者使用 `try/finally` 正确恢复（`file:///workspace/src/ApiGateway/Leno.ApiGateway/Middleware/FallbackResponseMiddleware.cs#L61-L69`），证明这是 `CacheMiddleware` 的疏漏。
- **影响**：下游异常时网关返回破损响应；客户端可能收到空 body 或连接重置；可观测性端 `TraceId` 丢失。
- **修复建议**：
  ```csharp
  var originalBodyStream = context.Response.Body;
  using var memoryStream = new MemoryStream();
  context.Response.Body = memoryStream;
  try
  {
      await _next(context);
  }
  finally
  {
      context.Response.Body = originalBodyStream;
  }
  memoryStream.Seek(0, SeekOrigin.Begin);
  var responseBytes = memoryStream.ToArray();
  // ... 缓存写入与回写逻辑
  ```
- **影响范围**：所有命中 `CacheMiddleware` 的 GET/HEAD 请求；下游异常时的客户端体验。

### 10. `AntiCorruptionDispatcher.Dispose` 销毁 KeyedSingleton `CircuitBreakerState`，影响其他引用同一实例的 dispatcher
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs#L105`、`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs#L102-L106`
- **类别**：A6 资源释放 / A2 异常处理不当
- **根因**：`AntiCorruptionDispatcher<TService>` 实现 `IDisposable`，`Dispose() => _circuitBreaker?.Dispose();`。但 `CircuitBreakerState` 通常注册为 Keyed Singleton（每个防腐层服务一个实例，跨请求累积失败计数），DI 容器在 dispatcher 释放时不应调用其 Dispose。
  - 若 `AntiCorruptionDispatcher` 注册为 Scoped/Transient（每个请求一个），Dispose 时会调用 `CircuitBreakerState.Dispose`，后者执行 `AntiCorruptionMetrics.UpdateCircuitOpenState(_serviceName, false)`——**误将熔断状态置为 Closed**，掩盖真实熔断状态；
  - 后续请求解析新的 dispatcher 时，`CircuitBreakerState` 已被 Dispose（虽未真正释放资源，但语义上已"清理"），其他并发的 dispatcher 引用同一实例的状态被污染。
- `CircuitBreakerState.Dispose` 的设计本意是"清理指标回调"，但作为 Singleton 不应有 Dispose 语义。
- **影响**：熔断状态指标在请求结束后被误重置；并发请求下熔断状态被污染，可能误判为 Closed 导致 gRPC 调用穿透到已熔断的下游。
- **修复建议**：
  1. `AntiCorruptionDispatcher` 不应 Dispose `_circuitBreaker`（它由 DI 容器管理生命周期）：
     ```csharp
     public void Dispose()
     {
         // CircuitBreakerState 由 DI 容器管理（Keyed Singleton），不在此释放
     }
     ```
  2. 或移除 `AntiCorruptionDispatcher : IDisposable` 实现；
  3. `CircuitBreakerState` 不应实现 `IDisposable`，指标回调由 OTLP 自动清理。
- **影响范围**：所有 `AntiCorruptionDispatcher<TService>` 使用者；熔断状态准确性。

---

## 🟡 中风险问题

### 11. `OutboxPublisher` 单次轮询周期串行执行 `RecoverStalePublishing` + `ProcessBatch` + `AlertIfPendingBacklog`，增加轮询延迟
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs#L83-L87`
- **类别**：C5 异步消息堆积 / C8 时序设计
- **根因**：`ExecuteAsync` 主循环中：
  ```csharp
  await RecoverStalePublishingAsync(stoppingToken);
  await ProcessBatchAsync(stoppingToken);
  await AlertIfPendingBacklogAsync(stoppingToken);
  ```
  三步串行。`RecoverStalePublishingAsync` 扫描 Publishing 超时消息并 SaveChanges（一次 DB 往返），`ProcessBatchAsync` 再拉取 Pending（又一次 DB 往返）并并行发布（MQ 往返），`AlertIfPendingBacklogAsync` 统计 Pending（第三次 DB 往返）。每轮 5s 轮询中，DB 往返 + MQ 发布串行，实际处理窗口被压缩。当积压严重时，Recover 修改状态后 ProcessBatch 重新加载，存在状态漂移（Recover 把 Publishing 回退为 Pending，ProcessBatch 立即拉取这些刚回退的消息，可能再次进入 Publishing）。
- **影响**：发件箱吞吐量受限；积压场景下轮询延迟放大；状态漂移可能导致同一消息被 Recover 与 ProcessBatch 同时处理（虽有 `Status != Pending` 检查，但 Recover 的 SaveChanges 与 ProcessBatch 的 Select 之间存在窗口）。
- **修复建议**：
  1. `RecoverStalePublishingAsync` 与 `AlertIfPendingBacklogAsync` 改为低频执行（如每 5 轮一次），`ProcessBatchAsync` 高频执行；
  2. 或将 Recover 改为独立 BackgroundService，与 ProcessBatch 解耦；
  3. ProcessBatch 拉取时 `WHERE Status = Pending AND Id NOT IN (SELECT Id FROM ... WHERE PublishingStartedAt < ...)` 避免与 Recover 冲突。
- **影响范围**：所有 `OutboxPublisher<TDbContext>` 实例；发件箱吞吐与积压恢复。

### 12. `OutboxPublisher.PublishSingleAsync` `MarkAsProcessed` 失败后未清理 `ChangeTracker`，可能影响下一次 `SaveChanges`
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs#L294-L310`
- **类别**：A2 异常处理不当 / A8 事务边界
- **根因**：阶段 3 `MarkAsProcessed` + `SaveChangesAsync` 失败时，catch 块仅 `LogWarning` 不抛出。但 `OutboxMessage` 实体的 `Status` 已被 `MarkAsProcessed()` 修改为 `Processed`（在内存中），`SaveChangesAsync` 失败意味着数据库中仍为 `Publishing`。同一 `DbContext` 实例（作用域内）下次 `SaveChangesAsync` 会再次尝试提交这个 `Processed` 状态——但下次是另一条消息的处理，`ChangeTracker` 中残留的 `Processed` 修改会被意外提交，导致数据库中消息状态被错误标记为 `Processed`（实际未确认）。
  - 注释承认"ChangeTracker 中残留的修改状态由下一次 SaveChangesAsync 重置"，但实际 EF Core 的 `ChangeTracker` 在 `SaveChangesAsync` 失败时**不会自动重置**实体状态，仍为 `Modified`。
  - 下次 `PublishSingleByIdAsync` 在同一作用域内（虽每条消息独立作用域，但若并行调度复用作用域则有问题）会再次 SaveChanges，残留修改被提交。
- **影响**：发件箱消息状态可能与实际发布状态不一致；下游幂等依赖被破坏。
- **修复建议**：
  ```csharp
  catch (Exception ex)
  {
      _logger.LogWarning(ex, "...");
      // 显式回滚内存状态，避免下次 SaveChanges 误提交
      context.Entry(message).CurrentValues.SetValues(new { Status = OutboxMessageStatus.Publishing });
      // 或 context.ChangeTracker.Clear();
  }
  ```
- **影响范围**：发件箱发布失败路径；消息状态一致性。

### 13. `CircuitBreakerState._openedAt` 初始值为 `DateTime.MinValue`，系统时间回拨可能误判 HalfOpen
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs#L16`、`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs#L42-L46`
- **类别**：A5 边界条件 / C8 时序设计
- **根因**：`_openedAt = DateTime.MinValue`，`GetState()` 中 `if (DateTime.UtcNow - _openedAt < _openDuration) return Open;`。问题：
  - 若 `_consecutiveFailures >= _failureThreshold` 但 `_openedAt` 仍为 `MinValue`（理论上 `RecordFailure` 中 `_consecutiveFailures >= _failureThreshold` 时会设置 `_openedAt = DateTime.UtcNow`，但若初始化时 `_failureThreshold=0`，则首次 `RecordFailure` 即触发，但 `_openedAt` 已被设置——此路径安全）；
  - 真正风险：系统时间回拨。若 `RecordFailure` 在 T1 设置 `_openedAt = T1`，系统时间回拨到 T0 < T1，则 `DateTime.UtcNow - _openedAt = T0 - T1 < 0 < _openDuration`，状态保持 Open——本应进入 HalfOpen 探测但被卡在 Open；
  - 反之，若回拨使 `DateTime.UtcNow - _openedAt > _openDuration`，会过早进入 HalfOpen。
- 此外 `DateTime.MinValue` 在序列化（如序列化 CircuitBreakerState 用于诊断）时可能引发 JSON 序列化异常（取决于序列化器）。
- **影响**：熔断器在时间回拨场景下状态不准确；NTP 校时可能触发。
- **修复建议**：使用 `Stopwatch` 计时熔断持续时间，或使用 `DateTimeOffset.UtcNow` 并在 `GetState` 中处理负值：
  ```csharp
  var elapsed = DateTime.UtcNow - _openedAt;
  if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
  if (elapsed < _openDuration) return CircuitState.Open;
  ```
- **影响范围**：所有 `CircuitBreakerState` 实例；时间回拨场景下的熔断行为。

### 14. `CircuitBreakerState.UpdateMetrics` 将 HalfOpen 状态记为 0（Closed），掩盖半开放探测状态
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs#L96-L100`、`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs#L60-L66`
- **类别**：C8 可观测性缺失
- **根因**：`UpdateMetrics` 中 `AntiCorruptionMetrics.UpdateCircuitOpenState(_serviceName, state == CircuitState.Open)`，HalfOpen 时 `isOpen=false`，与 Closed 状态在指标上无法区分。`ObservableGauge` 名为 `anticorruption_circuit_open`，值为 0/1，但 HalfOpen（半开放探测中）是熔断器的关键过渡状态，运维需要知道"当前正在探测"以判断是否需要人工介入。
- **影响**：运维无法区分"熔断器正常"与"熔断器半开放探测中"；HalfOpen 期间 gRPC 调用可能失败但指标显示熔断已恢复。
- **修复建议**：扩展指标为三态枚举（0=Closed, 1=Open, 2=HalfOpen）或新增 `anticorruption_circuit_half_open` gauge。
- **影响范围**：熔断器可观测性；运维排障。

### 15. `BffForwarderService` 整体超时与单请求超时均为 3s，下游尚未完成整体已超时
- **位置**：`file:///workspace/src/ApiGateway/Leno.ApiGateway/Bff/BffForwarderService.cs#L70-L71`、`file:///workspace/src/ApiGateway/Leno.ApiGateway/Bff/BffForwarderService.cs#L84-L85`
- **类别**：C8 时序设计 / A5 边界条件
- **根因**：`overallCts.CancelAfter(_timeout)`（3s）与 `perRequestCts.CancelAfter(_timeout)`（3s）使用同一 `_timeout` 值。当所有下游同时 3s 超时：
  - `overallCts` 先触发（因为 `Parallel.ForEachAsync` 调度有微小延迟）；
  - `perRequestCts` 通过 `CreateLinkedTokenSource(token)` 链接到 `overallCts.Token`，`overallCts` 取消后 `perRequestCts` 也立即取消；
  - 实际单请求永远无法独立超时——所有请求被整体超时统一杀掉。
  - 设计意图是"整体 3s 兜底，单请求 3s 各自超时"，但同一数值使单请求超时形同虚设。
- 此外 `perRequestCts` 在 `Parallel.ForEachAsync` 的 lambda 内 `using` 释放，但 `SendDownstreamAsync` 的 `client.SendAsync` 可能仍在飞行，`using` 释放后 `perRequestCts` 被 Dispose，`SendAsync` 收到 `OperationCanceledException` 时访问 `perRequestCts.IsCancellationRequested` 可能抛 `ObjectDisposedException`。
- **影响**：BFF 聚合端点在下游慢响应时整体 3s 超时，无法利用单请求超时做精细化控制；`ObjectDisposedException` 可能掩盖真实取消原因。
- **修复建议**：
  1. 单请求超时应小于整体超时（如单请求 2s，整体 3s）；
  2. `perRequestCts` 不在 `using` 中释放，改为在 `Parallel.ForEachAsync` 完成后统一释放；
  3. `catch (OperationCanceledException) when (perRequestCts.IsCancellationRequested && !token.IsCancellationRequested)` 改为先检查 `token.IsCancellationRequested`：
     ```csharp
     catch (OperationCanceledException) when (!token.IsCancellationRequested)
     {
         // 整体 token 未取消，必然是 perRequestCts 触发
     }
     ```
- **影响范围**：所有 `/api/bff/*` 端点；4 个 BFF 聚合控制器。

### 16. `BffForwarderService` 整体超时回填 504 时去重逻辑仅按 `Source` 名，部分下游已返回 504 会重复添加
- **位置**：`file:///workspace/src/ApiGateway/Leno.ApiGateway/Bff/BffForwarderService.cs#L138-L164`
- **类别**：A2 异常处理不当 / A5 边界条件
- **根因**：整体超时 catch 块遍历 `requests`，对 `results.ContainsKey(req.Source)` 跳过，对 `errors` 中已存在同 `Source` 的跳过，否则添加 504 `BffError`。但 `errors` 是 `ConcurrentBag<BffError>`，遍历时可能与其他线程并发添加，去重检查非原子。此外若某下游已返回 504（通过 `DownstreamFailureException` 添加了 `StatusCode=504`），整体超时后再次检查 `errors` 中 `Source` 已存在则跳过——但若该 504 是在整体超时 catch 触发瞬间被添加，可能漏检导致重复添加两个 `Source=order-detail, StatusCode=504` 的 `BffError`。
- **影响**：`BffResponse.Errors` 可能包含重复条目；调用方聚合逻辑可能重复处理。
- **修复建议**：使用 `ConcurrentDictionary<string, BffError>` 替代 `ConcurrentBag`，按 `Source` 去重；或在整体超时 catch 中先 `errors.Clear()` 再重新填充。
- **影响范围**：BFF 整体超时路径；`BffResponse.Errors` 准确性。

### 17. `CacheMiddleware.IsCacheableResponse` 仅缓存 200，201 Created/204 No Content 被错误跳过
- **位置**：`file:///workspace/src/ApiGateway/Leno.ApiGateway/Middleware/CacheMiddleware.cs#L100-L116`
- **类别**：A5 边界条件 / C8 缓存策略
- **根因**：`if (response.StatusCode != 200) return false;`。但 RESTful API 中：
  - `POST /api/resources` 返回 201 Created + Location header + body，是可缓存的（Cache-Control 允许时）；
  - `DELETE /api/resources/{id}` 返回 204 No Content，可缓存以避免重复 DELETE；
  - 虽然 `IsCacheableRequest` 限制为 GET/HEAD，但 GET 请求下游可能返回 301/302 重定向（应缓存以避免重复请求）、304 Not Modified（应直接缓存）。
- 当前实现使所有非 200 响应都不缓存，下游异常时每次都打到后端，无法起到保护作用。
- **影响**：缓存命中率降低；下游过载保护减弱。
- **修复建议**：
  ```csharp
  private static readonly HashSet<int> CacheableStatusCodes = new() { 200, 201, 204, 301, 302, 304, 404 };
  internal static bool IsCacheableResponse(HttpResponse response)
  {
      if (!CacheableStatusCodes.Contains(response.StatusCode)) return false;
      // ... Cache-Control 检查
  }
  ```
  注意 404 应缓存（防止缓存穿透，但 `CacheService` 已有布隆过滤器+空值缓存，此处可不一致）。
- **影响范围**：所有 GET/HEAD 请求的缓存策略。

### 18. `FallbackResponseMiddleware.RewriteAsFallbackAsync` 未清除 `Transfer-Encoding`/`Content-Encoding`，与重写后的 body 不一致
- **位置**：`file:///workspace/src/ApiGateway/Leno.ApiGateway/Middleware/FallbackResponseMiddleware.cs#L83-L101`
- **类别**：A2 异常处理不当 / A8 事务边界
- **根因**：重写为降级 JSON 时设置 `ContentType` 与 `ContentLength`，但若 YARP 转发时下游返回 `Transfer-Encoding: chunked` 或 `Content-Encoding: gzip`，这些 header 仍保留：
  - `Transfer-Encoding: chunked` + `ContentLength` 设置：客户端按 chunked 解析但收到的是 fixed-length body，解析错乱；
  - `Content-Encoding: gzip` + 未压缩 body：客户端尝试 gunzip 明文 JSON，抛 `InvalidDataException`。
- 注释承认"清除原始 headers 中可能与 body 不一致的字段"，但实际只设置 `ContentType` 与 `ContentLength`，未删除 `Transfer-Encoding`/`Content-Encoding`。
- **影响**：客户端收到降级响应时解析失败；降级中间件反而导致客户端报错。
- **修复建议**：
  ```csharp
  context.Response.Headers.Remove("Transfer-Encoding");
  context.Response.Headers.Remove("Content-Encoding");
  context.Response.ContentType = FallbackContentType;
  context.Response.ContentLength = FallbackBody.Length;
  ```
- **影响范围**：所有 503 降级响应路径。

### 19. `ConsulConfigWatcher` 直接修改 `IConfiguration["AntiCorruption:UseGrpc"]`，仅内存生效，不触发 `IOptionsMonitor` 重载
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs#L67-L68`
- **类别**：A2 异常处理不当 / C8 配置热更新
- **根因**：`_configuration["AntiCorruption:UseGrpc"] = newValue;` 直接写 `IConfiguration`，但：
  - `IConfiguration` 是只读视图（`Microsoft.Extensions.Configuration.IConfiguration` 索引器 set 在 `ConfigurationRoot` 上对 memory provider 有效，但其他 provider 如 JSON、环境变量是只读的）；
  - `IOptionsMonitor<AntiCorruptionOptions>` 的 `OnChange` 依赖 `IConfiguration` 的 change token，直接索引器赋值**不触发 change token**；
  - `AntiCorruptionDispatcher` 使用 `IOptionsMonitor<AntiCorruptionOptions>.CurrentValue`，期望热更新生效，但实际 `CurrentValue` 永远返回启动时绑定的值。
- 注释声称"配合 IOptionsMonitor 实现配置热更新"，但实现与注释不符。
- **影响**：Consul KV 修改 UseGrpc 后，`AntiCorruptionDispatcher` 不切换 gRPC/HTTP；运维需重启服务才生效。
- **修复建议**：
  1. 使用 `IOptionsMonitor<AntiCorruptionOptions>` + 自定义 `IOptionsChangeTokenSource<AntiCorruptionOptions>`，ConsulConfigWatcher 触发 change token：
     ```csharp
     // 自定义 IOptionsChangeTokenSource
     public class ConsulChangeTokenSource<TOptions> : IOptionsChangeTokenSource<TOptions> { ... }
     // 注册时
     services.AddSingleton<IOptionsChangeTokenSource<AntiCorruptionOptions>, ConsulChangeTokenSource<AntiCorruptionOptions>>();
     ```
  2. 或 `ConsulConfigWatcher` 直接持有 `AntiCorruptionDispatcher` 引用，调用方法更新内部状态。
- **影响范围**：所有 BC 的防腐层 gRPC/HTTP 切换；Consul KV 热更新链路。

### 20. `ServiceCollectionExtensions.AddHealthChecks` 未注册 RabbitMQ 健康检查，ready 探针可能误判
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L187-L193`
- **类别**：C8 可观测性缺失 / A2 异常处理不当
- **根因**：`AddHealthChecks` 仅注册 `RedisHealthCheck` 与 `ElasticsearchHealthCheck`（tag=ready），未注册 RabbitMQ 健康检查。RabbitMQ 是核心依赖（事件总线、Outbox 发布、消费者全部依赖），若 RabbitMQ 不可达：
  - `OutboxPublisher` 发布失败，消息积压；
  - 消费者无法消费，下游 BC 状态不一致；
  - 但 ready 探针仍返回 Healthy（Redis、ES 正常），K8s 不重启 Pod，也不阻止流量进入。
- `AddLenoFullHealthChecks`（注释中提到使用 NuGet 包）可能补充，但默认 `AddLenoInfrastructure` 路径下未注册。
- **影响**：RabbitMQ 故障时 ready 探针误判 Healthy；K8s 不切流，故障放大。
- **修复建议**：
  ```csharp
  services.AddHealthChecks()
      .AddCheck("self", ...)
      .AddCheck<RedisHealthCheck>("redis", tags: ReadyTags)
      .AddRabbitMQ(rabbitConnectionString, name: "rabbitmq", tags: ReadyTags)
      .AddCheck<ElasticsearchHealthCheck>("elasticsearch", tags: ReadyTags);
  ```
- **影响范围**：所有 BC 的 ready 探针；RabbitMQ 故障时的流量调度。

### 21. `ServiceCollectionExtensions.AddRedis` 中 `ConnectionMultiplexer.Connect` 同步阻塞，启动期 Redis 不可达即抛异常
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L97`
- **类别**：C7 资源/连接池 / A2 异常处理不当 / C8 启动时序
- **根因**：`var multiplexer = ConnectionMultiplexer.Connect(redisConfig);` 在 DI 注册阶段同步阻塞：
  - `AbortOnConnectFail` 默认 `true`（StackExchange.Redis 2.x+ 在非 Windows 下默认 `false`，但显式配置可能为 `true`），Redis 不可达时 `Connect` 抛 `RedisConnectionException`，服务启动失败；
  - 即使 `AbortOnConnectFail=false`，`Connect` 仍尝试同步建立连接，启动延迟；
  - DI 注册阶段执行 I/O 违反最佳实践（应在 `IHostedService.StartAsync` 中异步初始化）。
- **影响**：Redis 故障时服务无法启动；启动时间被 Redis 连接时间放大。
- **修复建议**：
  ```csharp
  services.AddSingleton<IConnectionMultiplexer>(sp =>
  {
      var config = sp.GetRequiredService<IConfiguration>()["Redis:Configuration"] ?? "localhost:6379";
      var options = ConfigurationOptions.Parse(config);
      options.AbortOnConnectFail = false;  // 启动期不阻塞
      return ConnectionMultiplexer.Connect(options);
  });
  ```
  或使用 `IHostedService` 异步初始化。
- **影响范围**：所有 BC 启动路径；Redis 故障时的服务可用性。

### 22. `JwtTokenGenerator` 未校验 `SecretKey` 长度，HS256 要求 ≥ 256 bits（32 字节）
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Auth/JwtTokenGenerator.cs#L84`、`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Auth/JwtTokenGenerator.cs#L131`
- **类别**：A2 异常处理不当 / 安全
- **根因**：`new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey))` 在 `SecretKey` 长度 < 32 字节时，`SymmetricSecurityKey` 构造函数不抛异常（.NET 8 起 `IdentityModel` 在 `ValidateIssuerSigningKey=true` 时校验，但构造时不校验）。HS256 算法要求密钥 ≥ 256 bits，短密钥会：
  - `BuildValidationParameters` 时 `TokenValidationParameters.IssuerSigningKey` 被接受，但 JwtBearer 中间件首次验证抛 `IDX10653: The key is too small`；
  - 启动期不报错，首次请求时 401，排障困难。
- **影响**：配置错误（如 SecretKey="leno"）时服务启动正常但所有 JWT 验证失败；运维排障困难。
- **修复建议**：
  ```csharp
  public JwtTokenGenerator(IOptions<JwtOptions> options)
  {
      _options = options.Value ?? throw new InvalidOperationException("JwtOptions 未配置");
      if (Encoding.UTF8.GetByteCount(_options.SecretKey) < 32)
      {
          throw new InvalidOperationException("Jwt:SecretKey 必须至少 32 字节（256 bits）以满足 HS256 要求");
      }
  }
  ```
- **影响范围**：所有 BC 的 JWT 签发与验证。

### 23. `JwtTokenGenerator.ClockSkew = 1 分钟`，对短 TTL token 可能放过期 token
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Auth/JwtTokenGenerator.cs#L141`
- **类别**：A5 边界条件 / 安全
- **根因**：`ClockSkew = TimeSpan.FromMinutes(1)` 允许 1 分钟时钟偏移。当 `AccessTokenExpiryMinutes=120`（默认）时影响小，但：
  - 短 TTL token（如 2FA 临时令牌 5min、密码重置令 10min）的 1min 偏移占 TTL 的 10%-20%，过期后仍可使用 1min；
  - 配合 `JwtBlacklistService` 的本地缓存永不过期，过期 token 在 1min 窗口内仍可访问。
- `ClockSkew=5min` 是 Industry default（JwtBearer 默认），1min 是收紧但不够严格。
- **影响**：过期 token 在 1min 窗口内仍可访问；安全敏感操作（改密、2FA）风险放大。
- **修复建议**：安全敏感 token（2FA、密码重置）使用 `ClockSkew=TimeSpan.Zero`；普通 token 保持 1min 或 5min。
- **影响范围**：所有 JWT 验证路径；安全敏感操作。

### 24. `EfCoreUnitOfWork.SaveChangesAsync` 不含 Outbox，与 `SaveEntitiesAsync` 行为不一致，调用方易混淆
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs#L51-L59`
- **类别**：A2 异常处理不当 / A8 事务边界 / C6 Outbox
- **根因**：`IUnitOfWork` 接口暴露两个保存方法：
  - `SaveChangesAsync` → `_context.SaveChangesAsync(ct)`，仅保存聚合变更，**不写入 Outbox**；
  - `SaveEntitiesAsync` → `SaveChangesWithOutboxAsync`，保存聚合 + 翻译领域事件为集成事件并写入 Outbox（同事务）。
  - 各 BC 应用层若误用 `SaveChangesAsync`，领域事件不会进入 Outbox，下游 BC 收不到事件。已在前序 BC 报告（如 01-userauth.md 问题 8）中发现此误用。
- 注释虽在 `SaveEntitiesAsync` 中说明"经 OutboxDbContextExtensions 保存"，但 `SaveChangesAsync` 的注释为空，调用方无感知差异。
- **影响**：领域事件丢失；下游 BC 状态不一致。已在 UserAuth、Cart 等 BC 中发现实际误用案例。
- **修复建议**：
  1. `IUnitOfWork` 移除 `SaveChangesAsync`，强制所有保存路径走 `SaveEntitiesAsync`；
  2. 或 `SaveChangesAsync` 内部调用 `SaveEntitiesAsync`（向后兼容）；
  3. 在 `SaveChangesAsync` 上添加 `[Obsolete("Use SaveEntitiesAsync to ensure domain events are persisted to outbox")]`。
- **影响范围**：所有 BC 的应用层；事件驱动链路完整性。

### 25. `CacheService.InvalidatePatternAsync` 模式扫描未加 `KeyPrefix`，可能匹配非缓存 key
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs#L328`
- **类别**：A5 边界条件 / A2 异常处理不当
- **根因**：`await foreach (var key in server.KeysAsync(pattern: pattern).WithCancellation(ct))` 直接使用调用方传入的 `pattern`，未强制拼接缓存 key 前缀。`CacheService` 的 key 由调用方决定（无统一前缀），但 Redis 实例可能同时存储：
  - 缓存 key：`user:123`、`product:456`；
  - 限流 key：`leno:ratelimit:*`；
  - JWT 黑名单：`leno:jwt:blacklist:*`；
  - 布隆过滤器：`leno:bloom`。
  - 若调用方传入 `pattern="leno:*"`，会误删 JWT 黑名单、限流计数器、布隆过滤器，导致安全/限流/穿透防护全部失效。
- 注释承认"调用方负责包含必要的 key 前缀"，但这将安全责任推给调用方，违反防御性编程。
- **影响**：误删非缓存 key 导致安全/限流功能失效。
- **修复建议**：
  1. `InvalidatePatternAsync` 强制 `pattern` 必须以特定前缀开头，否则抛 `ArgumentException`；
  2. 或自动拼接 `leno:cache:` 前缀（与 `CacheMiddleware.KeyPrefix` 对齐，但 `CacheService` 实际不使用此前缀，需统一）；
  3. 各 BC 使用 `CacheService` 时统一 key 命名规范。
- **影响范围**：所有 `InvalidatePatternAsync` 调用方；Redis 共享实例的安全性。

### 26. `Program.cs` 白名单中间件内联 lambda 未封装为独立类，违反单一职责
- **位置**：`file:///workspace/src/ApiGateway/Leno.ApiGateway/Program.cs#L132-L155`
- **类别**：代码气味 / B8 中间件滥用
- **根因**：白名单路由检查 + 未认证拦截逻辑以 `app.Use(async (context, next) => { ... })` 内联实现，包含 5 个 `StartsWith` 调用与 401 响应写入。问题：
  - 不可单元测试（无独立类）；
  - 白名单路径硬编码在 `Program.cs`，新增白名单需修改入口文件；
  - 与 `JwtBlacklistMiddleware` 风格不一致（后者是独立类）；
  - 401 响应体 `{ code = 401, message = "未认证" }` 与 `GlobalExceptionMiddleware` 的 `ApiResponse.Fail` 格式不一致，前端需处理两种错误格式。
- **影响**：可维护性差；测试困难；错误响应格式不一致。
- **修复建议**：抽取为 `WhitelistRoutingMiddleware`，白名单路径从配置 `Jwt:WhitelistPaths` 读取，401 响应使用 `ApiResponse.Fail(401, "未认证")`。
- **影响范围**：网关入口；前端错误处理。

### 27. `CacheService` 未获取互斥锁时仅单次 100ms 重试，缓存击穿防护不充分
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs#L143-L160`
- **类别**：A5 边界条件 / C7 资源/连接池
- **根因**：`GetOrSetAsync` 中 `LockTakeAsync` 失败后：
  ```csharp
  await Task.Delay(100, ct);
  var retryValue = await _database.StringGetAsync(key);
  ...
  return null;
  ```
  仅单次 100ms 等待后重试读取，若持锁线程仍未来得及写入（DB 查询耗时 > 100ms），直接返回 `null`。调用方收到 `null` 后可能：
  - 误认为数据不存在，触发业务空值处理（如返回 404）；
  - 或再次调用 `GetOrSetAsync`，形成击穿穿透。
- 正确的击穿防护应循环重试 N 次或等待锁释放事件（Redis Pub/Sub）。
- **影响**：高并发下缓存击穿防护失效；调用方收到错误的 null 值。
- **修复建议**：
  ```csharp
  const int MaxRetry = 5;
  for (var i = 0; i < MaxRetry; i++)
  {
      await Task.Delay(100 * (i + 1), ct);  // 递增退避
      var retryValue = await _database.StringGetAsync(key);
      if (retryValue.HasValue) { ... return; }
  }
  return null;  // 真正放弃
  ```
- **影响范围**：所有 `GetOrSetAsync` 调用路径；缓存击穿场景。

### 28. `RedisSlidingWindowRateLimiter` Redis 不可用时 `catch { return true; }` fail-open 无降级日志
- **位置**：`file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs#L119-L123`、`file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs#L153-L157`
- **类别**：A2 异常处理不当 / C8 可观测性缺失
- **根因**：`TryAcquireSync` 与 `TryAcquireAsync` 中 `catch { return true; }` 吞掉所有异常（除 `OperationCanceledException`）并放行。fail-open 策略本身合理（Redis 故障时不应阻断所有流量），但：
  - 无日志记录：Redis 故障时限流完全失效，但运维无感知；
  - 无指标埋点：`anticorruption_fallback_total` 等指标不记录限流降级；
  - 静默 fail-open 可能在 Redis 长时间故障期间放过恶意流量，导致后端过载。
- **影响**：Redis 故障时限流静默失效；运维无感知；后端可能被恶意流量打挂。
- **修复建议**：
  ```csharp
  catch (Exception ex)
  {
      _logger.LogWarning(ex, "Redis 限流降级放行 key={Key}", _key);
      // 可选：增加本地 fallback 计数器，超过阈值时拒绝
      return true;
  }
  ```
- **影响范围**：所有路由级与用户级限流；Redis 故障期间的安全防护。

---

## 🟢 低风险问题

### 29. `Money` 值对象使用可变属性（`private set`），违反 record 不可变性约定
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.SharedKernel/ValueObjects/Money.cs#L13-L15`
- **类别**：B2 聚合设计违规 / DDD 不可变性
- **根因**：`public sealed record Money` 中 `public decimal Amount { get; private set; }` 与 `public string Currency { get; private set; }`。`record` 的语义契约是不可变，但 `private set` 允许类内修改。当前 `Money` 的所有方法都返回新实例（`Add`/`Subtract`/`Multiply`），未实际修改自身，但 `private set` 留下了违反不可变性的后门——未来开发者可能添加 `SetAmount` 方法。
- 此外 `record` 的 `with` 表达式在 `private set` 下仍可工作（通过 init），但 `Equals`/`GetHashCode` 基于 `Amount`/`Currency`，若被修改会导致 `Dictionary<Money, T>` 键丢失。
- **影响**：潜在的可变性后门；`record` 语义不一致。
- **修复建议**：改为 `init`：
  ```csharp
  public decimal Amount { get; init; }
  public string Currency { get; init; } = default!;
  ```
- **影响范围**：`Money` 值对象的所有使用者。

### 30. `Money.Create` 中 `normalized.Length is < 3 or > 3` 等同 `!= 3`，可读性差
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.SharedKernel/ValueObjects/Money.cs#L38`
- **类别**：代码气味
- **根因**：`if (normalized.Length is < 3 or > 3)` 使用 pattern matching 表达"不等于 3"，但 `!= 3` 更直观。当前写法易误读为"小于 3 或大于 3 的某种范围"。
- **影响**：可读性差；可能引发误判。
- **修复建议**：`if (normalized.Length != 3)`。
- **影响范围**：`Money.Create` 校验逻辑。

### 31. `Entity.Id` 使用 `protected set`，允许子类任意修改 Id，违反不可变性
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs#L28`
- **类别**：B2 聚合设计违规 / DDD 不可变性
- **根因**：`public Guid Id { get; protected set; }` 允许任何 `Entity` 派生类在内部修改 Id。聚合根的 Id 应在创建时确定且永不变更，`protected set` 留下了变更后门。若子类在行为方法中误改 Id，会导致 `Equals`/`GetHashCode` 行为变化，`HashSet<Entity>` 中实体丢失。
- **修复建议**：`public Guid Id { get; init; }` 或 `public Guid Id { get; }`（仅在构造函数中设置）。
- **影响范围**：所有 `Entity` 派生类。

### 32. `Entity.GetHashCode` 返回 `Id.GetHashCode()`，与 `Guid.Empty` 实体的哈希冲突
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs#L70`
- **类别**：A5 边界条件
- **根因**：`GetHashCode() => Id.GetHashCode()`，`Equals` 中 `if (Id == Guid.Empty || other.Id == Guid.Empty) return false;`——两个 `Id=Guid.Empty` 的实体 `Equals` 返回 false 但 `GetHashCode` 相同，违反 `Equals`/`GetHashCode` 契约（相等对象必须哈希相等，但哈希相等的对象可以不等）。虽技术上合法，但 `HashSet<Entity>` 中所有 `Guid.Empty` 实体落入同一桶，性能退化为 O(n)。
- 实际场景：未持久化实体的临时 Id 可能为 `Guid.Empty`（虽构造函数 `Id == Guid.Empty ? Guid.NewGuid() : id` 防御了），但反射/反序列化可能绕过。
- **影响**：`Guid.Empty` 实体在哈希容器中性能退化。
- **修复建议**：`GetHashCode` 基于 `Id` 与 `GetType()`：
  ```csharp
  public override int GetHashCode() => HashCode.Combine(GetType(), Id);
  ```
- **影响范围**：所有 `Entity` 派生类的哈希容器使用。

### 33. `ErrorCodeMapping.GetStatusCode` 使用 `Contains` 匹配后缀，可能误匹配复合 ErrorCode
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs#L65-L71`
- **类别**：A5 边界条件 / A2 异常处理不当
- **根因**：`if (errorCode.Contains(suffix, StringComparison.Ordinal))` 使用 `Contains` 而非 `EndsWith`。后缀规则命名虽为 `_NOT_FOUND` 等，但 `Contains` 会匹配 ErrorCode 中间出现的子串。例如：
  - `USER_EXISTS_ALREADY_FOUND` 同时匹配 `_ALREADY_`（409）与 `_EXISTS`（409），结果相同侥幸正确；
  - `ORDER_FAILED_NOT_FOUND` 同时匹配 `_FAILED`（502）与 `_NOT_FOUND`（404），按数组顺序 `_NOT_FOUND` 在前返回 404，但语义可能应为 502（订单失败导致查询失败）；
  - `PAYMENT_REFUND_EXPIRED_REQUIRED` 匹配 `_EXPIRED`（401）与 `_REQUIRED`（401）与 `_FAILED`（不存在），结果 401 但语义模糊。
- 注释命名"_后缀规则"但实际是"子串匹配"，命名与实现不符。
- **影响**：复合 ErrorCode 的 HTTP 状态码可能不符合预期；前端错误处理受影响。
- **修复建议**：改为 `EndsWith`：
  ```csharp
  if (errorCode.EndsWith(suffix, StringComparison.Ordinal))
  ```
- **影响范围**：所有 `DomainException` 的 HTTP 状态码映射。

### 34. `ErrorCodeMapping` 静态 `ConcurrentDictionary` 在多 BC 共享进程时相互覆盖
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs#L11-L12`、`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L211-L237`
- **类别**：A3 并发与竞态 / B4 共享内核污染
- **根因**：`_explicit` 是 `static ConcurrentDictionary`，所有 BC 共享同一实例。`RegisterSpecialErrorCodes` 在 `AddLenoInfrastructure` 中调用，注册了 UserAuth/Cart/Seller/Address 等多个 BC 的特殊 ErrorCode。若多个 BC 在同一进程（开发环境聚合部署），后注册的 BC 不会覆盖先注册的（`Dictionary` 索引器 set 是覆盖，但 `RegisterAll` 内部用 `_explicit[errorCode] = statusCode`，确会覆盖）——若两个 BC 注册同名 ErrorCode（如 `USER_NOT_FOUND`），后注册覆盖前者，导致 HTTP 状态码错误。
- 此外 `Reset()` 仅清空 `_explicit`，不影响 `_suffixRules`，单元测试隔离不彻底。
- **影响**：多 BC 进程下 ErrorCode 注册冲突；HTTP 状态码可能错误。
- **修复建议**：
  1. `Register` 改为 `TryAdd`，冲突时抛异常；
  2. 或按 BC 命名空间隔离 ErrorCode（如 `USERAUTH_USER_NOT_FOUND`）；
  3. `Reset` 同时重置 suffix rules（虽然当前为静态只读，但测试可能需要）。
- **影响范围**：多 BC 共享进程场景；ErrorCode 注册链路。

### 35. `IntegrationEventBase.IdempotencyKey` 为非可空 `string`，反序列化无值时为 null
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs#L14`
- **类别**：A1 空引用 / B7 事件契约一致性
- **根因**：`public string IdempotencyKey { get; init; }` 非可空标注，但：
  - 无参构造函数（供 System.Text.Json 反序列化）`protected IntegrationEventBase()` 设置 `IdempotencyKey = EventId.ToString()`，但 `EventId` 也是 `init`，反序列化时 JSON 中若无 `EventId` 字段，`EventId` 为 `Guid.Empty`，`IdempotencyKey` 为 `"00000000-0000-0000-0000-000000000000"`——非 null 但语义错误；
  - 若 JSON 中 `IdempotencyKey` 字段缺失，`System.Text.Json` 不会调用无参构造函数中的赋值（反序列化直接创建对象并设置属性），`IdempotencyKey` 可能为 `null`（取决于 `JsonSerializerOptions` 的 `RespectNullableAnnotations`）；
  - .NET 8 默认不强制 nullable 标注，`null` 值会被静默接受。
- 消费者使用 `evt.IdempotencyKey` 作为 Redis key 时，`null` 会导致 `ArgumentNullException`。
- **影响**：反序列化边界场景下 `IdempotencyKey` 可能为 null；消费者空引用异常。
- **修复建议**：
  ```csharp
  public string IdempotencyKey { get; init; } = string.Empty;
  ```
  或标注 `string?` 并在使用处校验。
- **影响范围**：所有集成事件的反序列化路径。

### 36. `ObjectStorageService.ExistsAsync` 第二个 `catch` 吞掉所有异常，掩盖真实错误
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Storage/ObjectStorageService.cs#L149-L152`
- **类别**：A2 异常处理不当
- **根因**：
  ```csharp
  catch (Exception ex) when (ex is Minio.Exceptions.ObjectNotFoundException) { return false; }
  catch { return false; }
  ```
  第二个 `catch` 吞掉所有异常（网络故障、鉴权失败、Bucket 不存在等）并返回 `false`，调用方无法区分"文件不存在"与"存储故障"：
  - 鉴权失败时 `ExistsAsync=false`，调用方可能误删数据库记录；
  - 网络故障时 `ExistsAsync=false`，业务逻辑误判文件丢失；
  - 无日志记录，故障不可追溯。
- **影响**：存储故障被误判为文件不存在；业务数据不一致。
- **修复建议**：
  ```csharp
  catch (Exception ex) when (ex is Minio.Exceptions.ObjectNotFoundException) { return false; }
  catch (Exception ex)
  {
      _logger.LogWarning(ex, "MinIO ExistsAsync 失败 ObjectName={ObjectName}", objectName);
      throw;  // 或返回 true 让调用方保守处理
  }
  ```
- **影响范围**：所有 `ExistsAsync` 调用方；文件存在性判断。

### 37. `RedisBloomFilter` 每次 `AddAsync`/`MightContainAsync` 触发 14 次 SHA256 计算，性能开销大
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs#L97-L119`
- **类别**：C7 性能
- **根因**：`GetHashPositions` 中 `_hashCount=7`（默认），每次调用 `GetHash64` 两次（hash1、hash2），每次 `GetHash64` 内部 `SHA256.HashData(input)`。即每次 `AddAsync`/`MightContainAsync` 触发 2 次 SHA256（不是 14 次，因双重哈希复用 hash1/hash2）。但 SHA256 对 100 字节输入的吞吐约 1μs/次，10w QPS 下每秒 0.2s CPU 用于哈希计算，单核占用 20%。
- 更严重的是 `AddAsync` 中 `tasks.Add(_database.StringSetBitAsync(...))` 后 `await Task.WhenAll(tasks)`——7 次 Redis 网络往返（虽并行但仍受 Redis 单线程限制），高 QPS 下 Redis 连接池成为瓶颈。
- **影响**：布隆过滤器性能瓶颈；高 QPS 下 CPU 与 Redis 连接占用高。
- **修复建议**：
  1. 使用 `xxHash3` 或 `MurmurHash3` 替代 SHA256（非加密场景，性能提升 10x+）；
  2. `AddAsync`/`MightContainAsync` 改用 Lua 脚本，单次 Redis 往返完成所有位操作：
     ```lua
     -- KEYS[1] = bloom key, ARGV[1..7] = positions
     for i = 1, 7 do
         redis.call('SETBIT', KEYS[1], ARGV[i], 1)
     end
     ```
- **影响范围**：所有 `CacheService.GetOrSetAsync` 的布隆过滤器检查路径。

### 38. `RedisBloomFilter` `StringSetBitAsync` 多次网络往返，应用 pipeline 或 Lua
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs#L61-L68`、`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs#L79-L86`
- **类别**：C7 性能
- **根因**：`AddAsync` 中 7 次 `StringSetBitAsync` 通过 `Task.WhenAll` 并行，但 StackExchange.Redis 的 `Task.WhenAll` 实际通过单连接多路复用，7 次命令仍需 7 次 Redis 命令处理（Redis 单线程串行执行）。`MightContainAsync` 同理 7 次 `StringGetBitAsync`。
- 相比 Lua 脚本单次往返，网络延迟放大 7 倍。
- **影响**：布隆过滤器延迟高；Redis 连接占用大。
- **修复建议**：见问题 37 的 Lua 脚本方案。
- **影响范围**：布隆过滤器所有调用路径。

### 39. `CircuitBreakerState.GetState` 内部 `lock`，被 `RecordSuccess`/`RecordFailure` 在已持锁状态下再次调用（重入性能损耗）
- **位置**：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs#L35-L47`、`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs#L50-L87`
- **类别**：C7 性能
- **根因**：`RecordSuccess`/`RecordFailure` 内 `lock(_lock) { var state = GetState(); ... }`，而 `GetState` 自身也 `lock(_lock)`。C# `lock` 是重入 `Monitor`，不会死锁，但重入有性能损耗（每次进入计数器递增/递减 + try/finally）。在高 QPS 防腐层调用场景下，每次 `RecordSuccess`/`RecordFailure` 触发两次 `Monitor.Enter`/`Exit`。
- **影响**：高 QPS 下锁开销放大；非功能性问题但影响吞吐。
- **修复建议**：`RecordSuccess`/`RecordFailure` 内直接访问字段，不调用 `GetState`：
  ```csharp
  public void RecordSuccess()
  {
      lock (_lock)
      {
          // 直接判断状态而非调用 GetState
          var isOpen = _consecutiveFailures >= _failureThreshold
                       && DateTime.UtcNow - _openedAt < _openDuration;
          if (!isOpen && _consecutiveFailures >= _failureThreshold)
          {
              // HalfOpen
              _halfOpenSuccesses++;
              ...
          }
          ...
      }
  }
  ```
- **影响范围**：防腐层高频调用路径。

---

## BC 健康度评分

| 维度 | 评分(0-5) | 说明 |
|------|-----------|------|
| 功能正确性 | 3 | 核心功能（Outbox 两阶段标记、防腐层双轨、缓存穿透/击穿/雪崩防护、JWT 验签、限流、BFF 聚合）均已实现，但存在多处边界 bug：RedisBloomFilter `Math.Abs` 溢出（问题 6）、RedisSlidingWindowRateLimiter Lua 顺序错误（问题 8）、CacheMiddleware 异常路径未恢复 Body 流（问题 9）、OutboxPublisher ChangeTracker 未清理（问题 12）、BaseDbContext 审计字段未填充用户身份（问题 7）。这些 bug 在生产边界场景下会导致功能失效。 |
| DDD 合规 | 4 | 共享内核设计清晰（SharedKernel 仅含 Entity/AggregateRoot/ValueObjects/Abstractions），未被业务逻辑污染；防腐层模式统一抽取到 `Leno.Infrastructure.AntiCorruption/`（B3 合规）；Outbox/CQRS/EventBus 基础设施复用度高（D2 合规）。扣分点：BaseDbContext 在领域层 `Entity` 上添加 `Version` shadow property（虽是 shadow 但仍让领域层感知持久化）、`Entity.Id` `protected set` 违反不可变性（问题 31）、`Money` 值对象可变属性（问题 29）、`EfCoreUnitOfWork.SaveChangesAsync` 与 `SaveEntitiesAsync` 双保存路径导致调用方误用（问题 24，已在多个 BC 中暴露）。 |
| 性能与可靠性 | 2 | 多处关键可靠性问题：JwtBlacklistService 实现与注释不符且多实例不同步（问题 2）、CacheService Random 非线程安全（问题 1）、AntiCorruptionMetrics 静态字典竞态（问题 3）、IntegrationEventConsumerBase 幂等无原子性（问题 4）、ObjectStorageService 构造函数 sync over async（问题 5）、AntiCorruptionDispatcher.Dispose 误销毁 KeyedSingleton（问题 10）、ConsulConfigWatcher 不触发 IOptionsMonitor 重载（问题 19）、Redis 连接同步阻塞（问题 21）、限流 Redis 故障静默 fail-open（问题 28）。这些问题在生产高并发或多实例部署下会集中爆发，整体可靠性偏低。 |
| 可观测性 | 3 | OpenTelemetry 集成完整（Tracing + Metrics + Serilog TraceId 富化），AntiCorruptionMetrics/OutboxMetrics 指标设计合理，但 AntiCorruptionMetrics 字典非线程安全（问题 3）、HalfOpen 状态被掩盖（问题 14）、RabbitMQ 健康检查缺失（问题 20）、限流降级无日志（问题 28）、CircuitBreakerState Dispose 误重置指标（问题 10），关键场景下可观测性会失效。 |
| 安全性 | 3 | JWT 验签本地化、InternalKey 鉴权、敏感参数从环境变量读取（ObjectStorageService）等设计合理，但 JwtBlacklistService 多实例不同步（问题 2）使登出安全语义失效、CacheService.InvalidatePatternAsync 可能误删 JWT 黑名单 key（问题 25）、JwtTokenGenerator 未校验 SecretKey 长度（问题 22）、BaseDbContext 审计字段未填充用户身份（问题 7）使合规审计失效。 |
| **综合健康度** | **3.0** | 共享层架构设计成熟（DDD 分层清晰、防腐层统一抽取、Outbox/CQRS/EventBus 复用度高），但实现细节存在多处可靠性 bug，尤其在并发安全、资源释放、配置热更新、跨实例一致性方面问题集中。建议优先修复高风险问题 1-10，再处理中风险的 Outbox/熔断/BFF 时序问题。 |

---

## 共享层特殊检查重点结论

### B3 防腐层模式是否被各 BC 重复实现
- **结论**：合规。防腐层统一抽取到 `file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/`，包含：
  - `AntiCorruptionBase`：模板方法，统一异常捕获、指标埋点、HTTP 状态码映射；
  - `AntiCorruptionDispatcher<TService>`：双轨调度器，gRPC ↔ HTTP 降级；
  - `CircuitBreakerState`：熔断器状态机；
  - `GrpcAntiCorruptionClientBase`：gRPC 客户端基类；
  - `GrpcInternalKeyInterceptor`：gRPC 服务端鉴权拦截器；
  - `AntiCorruptionPollyExtensions`：Polly 集成。
- 各 BC（Order/Payment/Stock/Product/Promotion/PointsMembership）通过继承 `AntiCorruptionBase` 实现具体防腐层客户端，未重复实现调度/熔断/降级逻辑。
- **遗留问题**：`AntiCorruptionDispatcher.Dispose` 误销毁 KeyedSingleton（问题 10）。

### B4 共享内核是否被污染
- **结论**：基本合规，轻度污染。`Leno.SharedKernel` 仅包含：
  - `Abstractions/`：`Entity`、`AggregateRoot`、`IDomainEvent`、`IRepository`、`IUnitOfWork`、`IAuditable`、`ISoftDeletable`；
  - `ValueObjects/`：`Money`、`PageRequest`、`SpecAttribute`；
  - `Exceptions/`：`DomainException`。
- 未包含业务逻辑、领域服务、应用服务。轻度污染：
  - `BaseDbContext` 在 `Entity` 派生类型上添加 `Version` shadow property（`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs#L39-L48`），虽是 shadow property（领域层不感知），但 `Entity` 类型判断 `typeof(Entity).IsAssignableFrom(entityType.ClrType)` 让持久化层反向依赖领域抽象；
  - `IAuditable`/`ISoftDeletable` 接口在 `Entity` 中定义，持久化关注（软删除、审计）泄漏到领域层——这是 DDD 中"领域层应只关注领域语义"的争议点，可接受但属轻度污染。

### D2 重复实现是否应抽取到共享层
- **结论**：抽取充分。`Leno.Infrastructure` 已抽取：
  - `BaseDbContext`：审计字段、软删除查询过滤器、Outbox DbSet、乐观锁 shadow property；
  - `EfCoreUnitOfWork<TDbContext>`：泛型 UoW，消除各 BC 100% 同构的 UnitOfWork 副本（注释明示"抽取自各 BC 约 680 行重复代码"）；
  - `OutboxPublisher<TDbContext>`：泛型发件箱发布器；
  - `IntegrationEventConsumerBase<T>`：泛型消费者基类；
  - `ReadModelSyncConsumerBase`：读模型同步消费者基类；
  - `EsReadModelRepository<T>`：泛型 ES 仓储；
  - `GlobalExceptionMiddleware`：全局异常处理；
  - `ErrorCodeMapping`：ErrorCode → HTTP 状态码映射；
  - `CacheService`/`RedisBloomFilter`：缓存基础设施。
- **遗留问题**：`SaveChangesAsync` 与 `SaveEntitiesAsync` 双保存路径（问题 24）导致抽取不彻底，各 BC 仍可能误用前者导致事件丢失。

### ACL 模式（AntiCorruption/）设计合理性
- **结论**：设计成熟，实现有缺陷。双轨方案（gRPC 优先 + HTTP 降级）+ 熔断器 + Polly 重试 + InternalKey 鉴权 + 指标埋点，架构合理。但：
  - `CircuitBreakerState` 时间回拨风险（问题 13）；
  - `CircuitBreakerState` HalfOpen 指标掩盖（问题 14）；
  - `AntiCorruptionDispatcher.Dispose` 误销毁（问题 10）；
  - `AntiCorruptionMetrics` 字典竞态（问题 3）；
  - `ConsulConfigWatcher` 不触发 IOptionsMonitor 重载（问题 19），使 gRPC/HTTP 切换实际不生效。

### Outbox 设计合理性
- **结论**：两阶段标记 + Recover 兜底 + 并行发布 + 积压告警，设计完整。但：
  - Recover 与 ProcessBatch 串行增加延迟（问题 11）；
  - MarkAsProcessed 失败未清理 ChangeTracker（问题 12）；
  - `IntegrationEventConsumerBase` 幂等无原子性（问题 4），使 Outbox 重试场景下业务可能重复执行。

### CQRS 基础设施设计合理性
- **结论**：`Leno.Infrastructure.Abstractions/Cqrs/IQueryHandler.cs` 提供 Query 侧抽象，`EsReadModelRepository<T>` 提供读模型仓储，`ReadModelSyncConsumerBase` 提供读模型同步消费者基类。设计合理，无明显问题。

### ApiGateway 中间件设计合理性
- **结论**：中间件管道顺序合理（Observability → CORS → Authentication → JwtBlacklist → 白名单 → Authorization → Fallback → Compression → Cache → RateLimiter → Timeout → YARP），但：
  - `CacheMiddleware` 异常路径未恢复 Body 流（问题 9）；
  - `FallbackResponseMiddleware` 未清除 Transfer-Encoding/Content-Encoding（问题 18）；
  - `Program.cs` 白名单中间件内联 lambda（问题 26）；
  - `JwtBlacklistService` 实现与注释不符（问题 2）；
  - `RedisSlidingWindowRateLimiter` Lua 顺序错误（问题 8）与 fail-open 无日志（问题 28）。

### 共享事件契约稳定性（Leno.SharedContracts/Events/）
- **结论**：契约设计规范，稳定性保障机制完善：
  - `IntegrationEventBase` 提供 `EventId`/`OccurredAt`/`IdempotencyKey`/`SchemaVersion` 基类字段；
  - 所有业务事件（`OrderEvents`/`PaymentEvents`/`CouponEvents`/`PointsMembershipEvents` 等）使用 `init` 属性，构造后不可变；
  - 每个事件类有无参构造函数供 System.Text.Json 反序列化；
  - `SchemaVersion` 支持跨版本兼容（M4.2）；
  - 每个事件注释明示"消费方"与"事件契约定义在共享层，变更需所有消费方协商"。
- **遗留问题**：
  - `IdempotencyKey` 非可空 string 反序列化可能为 null（问题 35）；
  - 事件类使用 `init` 但 `IntegrationEventBase` 自身字段 `EventId`/`OccurredAt` 也是 `init`，无参构造函数中 `EventId = Guid.NewGuid()` 在反序列化时会被 JSON 值覆盖（正确行为），但若 JSON 缺失 `EventId` 字段，`init` 不会调用无参构造函数中的赋值，导致 `EventId = Guid.Empty`——消费者幂等去重失效。

### ApiGateway BFF 聚合层容错与超时设计
- **结论**：设计意图明确（整体 3s 超时 + 单请求 3s 超时 + 并行调度 + 部分失败返回 partial），但实现有缺陷：
  - 整体与单请求超时相同使单请求超时形同虚设（问题 15）；
  - 整体超时回填 504 去重不严格（问题 16）；
  - `OperationCanceledException` 条件判断不充分（问题 15）；
  - 4 个 BFF 控制器（CartCheckoutPreview/OrderDetail/ProductDetail/SellerDashboard）均硬编码下游服务 URL `http://order-api:8080`（`file:///workspace/src/ApiGateway/Leno.ApiGateway/Bff/Controllers/OrderDetailBffController.cs#L19`），未通过 Consul 服务发现动态解析——与网关整体 Consul 服务发现设计不一致，BFF 路径绕过了服务发现。
