# P1-B.1 异步可靠性加固设计

> **版本**：1.0
> **日期**：2026-07-20
> **状态**：已确认，待生成实现计划
> **前置**：P0-A 核心占位实现补齐已完成（commit `f702133`..`99b88d7` 推送至 `trae/agent-LjCAhF` 分支）

---

## 1. 范围与实施策略

### 1.1 范围决策

基于 P0-A 完成后对 9 项异步可靠性问题的调研，按修复难度与依赖关系做以下范围划分：

| # | 问题 | 处理决策 | 理由 |
|---|------|---------|------|
| 1 | async void OnMessage | ✅ 修复（本 spec） | 改 SubscribeAsync + Func handler |
| 2 | fire-and-forget SendAsync | ✅ 修复（本 spec） | 改 await + MassTransit 重试 |
| 3 | Redis 事务 Task 丢弃 | ✅ 修复（本 spec） | 显式 await 所有 Task |
| 4 | WeChatPay 防重放 fail-open | ✅ 修复（本 spec） | 对齐 T19 fail-closed |
| 5 | Alipay 验签吞异常 | ✅ 修复（本 spec） | 区分异常类型 + 记日志 |
| 6 | OrderSagaOrchestrator 无持久化 | ⚠️ 拆分到 P1-B.6 | 完整 Saga 持久化改动面大，独立子项目 |
| 7 | CancelAsync 跨服务串行无补偿 | ⚠️ 拆分到 P1-B.6 | 依赖 #6 Saga 持久化 |
| 8 | 对账无分页 | ✅ 修复（本 spec） | 分页循环 + 流式对比 |
| 9 | gRPC 无 Polly | ✅ 修复（本 spec） | 在 `GrpcAntiCorruptionClientBase.ExecuteAsync` 内嵌 Polly |

### 1.2 阶段拆分

**P1-B.1（本 spec，7 项独立修复）**
- 问题 1、2、3、4、5、8、9
- 每项独立修复，互不依赖
- 测试覆盖补齐
- 约 18 个文件改动 + 8 个测试文件新增/修改

**P1-B.6（独立 spec，Saga 持久化）**
- 问题 6 + 7
- 新增 `OrderSagaState` 聚合 + `ISagaStateRepository` + `SagaRecoveryService` 后台服务
- 改造 `OrderSagaOrchestrator` 与 `OrderAppService.CancelAsync`
- 改动面大，单独 brainstorming

### 1.3 实施顺序（P1-B.1 内部）

按风险与依赖排序：
1. **问题 4** WeChatPay fail-closed（安全优先，对齐 T19）
2. **问题 5** Alipay 验签异常分类（安全优先）
3. **问题 1** async void 改造（网关稳定性）
4. **问题 2** fire-and-forget 改 await（通知必达）
5. **问题 3** Redis 事务 await（限流准确性）
6. **问题 8** 对账分页（数据完整性）
7. **问题 9** gRPC Polly 集成（性能与稳定性）

### 1.4 不在本子项目范围

- 问题 6 Saga 持久化（→ P1-B.6）
- 问题 7 CancelAsync 补偿（→ P1-B.6）
- MassTransit 重试策略完整配置（已有 RabbitMQ 配置，仅调整使用方式）
- 外部告警联动（仅改日志，不改告警系统）
- Outbox 表新增（问题 2 用 MassTransit 重试，不引入新 Outbox）

---

## 2. 组件详细设计

### 2.1 问题 4：WeChatPay 防重放 fail-closed

**改动文件**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayChannel.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`（若构造函数变更）
- `src/Services/Payment/Leno.Payment.Infrastructure.Tests/InfrastructureTests.cs`

**改动点**：
1. `ValidateNonceAsync` Redis 异常时改为 `throw`（对齐 `WeChatPayNotifyHandler.MarkCallbackProcessedAsync` T19 模式）
2. `_redis is null` 分支保留 `return true` 但日志升级为 `LogWarning`（生产环境应配 Redis，但 P1-B.1 不强制）

**`ValidateNonceAsync` 修复后实现**：

```csharp
private async Task<bool> ValidateNonceAsync(string nonce, CancellationToken ct)
{
    if (_redis is null)
    {
        // Redis 不可用：保持兼容放行，但记警告（生产应配置 Redis）
        _logger.LogWarning("微信支付回调：Redis 未配置，跳过防重放检查 Nonce={Nonce}", nonce);
        return true;
    }

    try
    {
        var db = _redis.GetDatabase();
        var key = $"wechatpay:nonce:{nonce}";
        var ttl = TimeSpan.FromSeconds(TimestampToleranceSeconds * 2);
        return await db.StringSetAsync(key, "1", ttl, When.NotExists).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        // fail-closed：Redis 故障时拒绝验签，让微信重试回调
        _logger.LogError(ex, "微信支付回调防重放检查 Redis 故障 Nonce={Nonce}", nonce);
        throw;
    }
}
```

**`VerifySignatureAsync` 调用方改动**：

```csharp
try
{
    if (!await ValidateNonceAsync(nonce, ct).ConfigureAwait(false))
    {
        return SignatureVerificationResult.Failure("Nonce 重放");
    }
    // ... 既有验签逻辑
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    _logger.LogError(ex, "微信支付回调验签异常");
    return SignatureVerificationResult.Failure("验签异常");
}
```

**设计说明**：`VerifySignatureAsync` 当前返回 `SignatureVerificationResult`，不抛异常。改为在 catch 内转 `Failure`，让 NotifyController 返回 401，微信会重试。

**新增测试**：
- `ValidateNonce_RedisThrows_ShouldFailVerification` — mock Redis 抛异常，断言 `VerifySignatureAsync` 返回 `Failure`
- `ValidateNonce_RedisUnavailable_ShouldSkipAndSucceed` — `_redis = null` 时跳过防重放
- `VerifySignatureAsync_ReplayAttack_ShouldFail` — 同一 nonce 二次验签失败

---

### 2.2 问题 5：Alipay 验签异常分类

**改动文件**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Channels/Alipay/AlipaySignatureHelper.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayChannel.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure.Tests/InfrastructureTests.cs`

**关键设计决策**：
- `AlipaySignatureHelper` 当前是 `static` 类。改为非 static 注入 `ILogger<AlipaySignatureHelper>` 会破坏 11 处调用方，改动面过大。
- **方案：保留 static，但通过 `ILogger?` 参数传入**（可选参数），既有调用方不破坏，新调用方传 logger。

**`VerifySign` 修复后实现**：

```csharp
public static bool VerifySign(
    Dictionary<string, string> parameters,
    string publicKey,
    string? sign,
    ILogger? logger = null)
{
    if (string.IsNullOrEmpty(sign))
    {
        return false;
    }

    try
    {
        var content = BuildSignContent(parameters);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKey);
        return rsa.VerifyData(
            Encoding.UTF8.GetBytes(content),
            Convert.FromBase64String(sign),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }
    catch (ArgumentException ex)
    {
        // 公钥 PEM 格式错误：配置问题，需立即修复
        logger?.LogError(ex, "支付宝公钥 PEM 格式错误，验签失败");
        return false;
    }
    catch (FormatException ex)
    {
        // sign 非 Base64：可能是攻击或客户端异常
        logger?.LogWarning(ex, "支付宝 sign 字段非合法 Base64");
        return false;
    }
    catch (CryptographicException ex)
    {
        // RSA 验签失败：签名不匹配
        logger?.LogDebug(ex, "支付宝 RSA 验签失败（签名不匹配）");
        return false;
    }
    // 不再吞其他异常：未知异常冒泡由调用方处理
}
```

**设计权衡**：移除了通用 `catch (Exception)`。若发生未预期异常（如 OOM），将冒泡到 `AlipayChannel.VerifySignatureAsync`，由调用方 catch 转 `Failure`。这与原"吞所有异常返回 false"相比，**行为变化**：未预期异常不再静默返回 false。需在调用方加 catch 兜底。

**`AlipayChannel.VerifySignatureAsync` 调用方改动**：

```csharp
public Task<SignatureVerificationResult> VerifySignatureAsync(
    Dictionary<string, string> formFields, CancellationToken ct)
{
    // ... 既有 config 加载
    try
    {
        var verified = AlipaySignatureHelper.VerifySign(formFields, config.ApiKey, sign, _logger);
        if (!verified)
        {
            _logger.LogWarning("支付宝回调验签失败");
            return Task.FromResult(SignatureVerificationResult.Failure("签名不匹配"));
        }
        return Task.FromResult(SignatureVerificationResult.Success());
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.LogError(ex, "支付宝验签未预期异常");
        return Task.FromResult(SignatureVerificationResult.Failure("验签异常"));
    }
}
```

**新增测试**：
- `VerifySign_InvalidPublicKey_ShouldReturnFalseAndLogError` — 传入非法 PEM，断言返回 false 且记 LogError
- `VerifySign_InvalidBase64Sign_ShouldReturnFalseAndLogWarning` — 传入非 Base64 sign，断言返回 false 且记 LogWarning
- `VerifySign_TamperedSignature_ShouldReturnFalseAndLogDebug` — 传入篡改签名，断言返回 false 且记 LogDebug
- `VerifySign_UnexpectedException_ShouldBubbleToCaller` — 验证未预期异常冒泡

---

### 2.3 问题 1：async void OnMessage 改造

**改动文件**：
- `src/ApiGateway/Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs`
- `src/ApiGateway/Leno.ApiGateway.Tests/Services/CacheInvalidationSubscriberTests.cs`

**改动点**：
1. `OnMessage` 签名 `async void` → `async Task`
2. `EnsureSubscribed` → `EnsureSubscribedAsync`，改用 `SubscribeAsync(channel, Func<RedisChannel, RedisValue, Task>)`
3. `StartAsync` 内 `await EnsureSubscribedAsync()`（保留容错 try-catch）
4. `IDisposable` → `IAsyncDisposable`（保留 `IDisposable` 兼容旧调用方）

**关键改动**：

```csharp
// 原：private async void OnMessage(RedisChannel channel, RedisValue message)
// 改为：
private async Task OnMessage(RedisChannel channel, RedisValue message)
{
    try
    {
        // ... 既有逻辑不变
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.LogError(ex, "Failed to process cache invalidation message: {Message}", message);
    }
}

// 原：_subscriber.Subscribe(RedisChannel.Literal(ChannelName), OnMessage);
// 改为：
private Task EnsureSubscribedAsync()
{
    _subscriber = _redis.GetSubscriber();
    return _subscriber.SubscribeAsync(RedisChannel.Literal(ChannelName), OnMessage);
}
```

**`StartAsync` 改动**：

```csharp
public async Task StartAsync(CancellationToken cancellationToken)
{
    // ... 既有初始化
    try
    {
        await EnsureSubscribedAsync().ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Redis 订阅启动失败，将依赖重连机制");
    }
    // ... 既有事件注册
}
```

**新增 `IAsyncDisposable`**：

```csharp
public async ValueTask DisposeAsync()
{
    if (_subscriber is not null)
    {
        await _subscriber.UnsubscribeAllAsync().ConfigureAwait(false);
    }
    _stoppingCts?.Cancel();
    _stoppingCts?.Dispose();
    // ... 既有释放
}
```

**测试改动**：
- 既有 13 个测试中所有 `Subscribe(It.Is<RedisChannel>(...), It.IsAny<Action<RedisChannel, RedisValue>>(), ...)` 改为 `SubscribeAsync(It.Is<RedisChannel>(...), It.IsAny<Func<RedisChannel, RedisValue, Task>>(), ...)`
- 新增 `OnMessage_ValidMessage_DeletesKeyAndDelayedDelete`
- 新增 `OnMessage_DeserializeThrows_DoesNotCrashProcess`

---

### 2.4 问题 2：fire-and-forget 改 await

**改动文件**：
- `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs`
- `src/Services/Notification/Leno.Notification.Application.Tests/NotificationEventConsumerTests.cs`
- `src/Services/Notification/Leno.Notification.Api/appsettings.json`（MassTransit Retry 配置）

**改动点**：
1. `_ = SendAsync(...)` 改为 `await SendAsync(...)`
2. 移除 `SendAsync` 内 try-catch，让异常冒泡到 MassTransit 重试
3. MassTransit 重试策略在 `appsettings.json` 配置

**改动后代码**：

```csharp
// 原：_ = SendAsync(request, eventType, evt.EventId);
//     return Task.CompletedTask;
// 改为：
await SendAsync(request, eventType, evt.EventId);

// 原 SendAsync 内有 try-catch，移除：
private async Task SendAsync(NotificationRequest request, string eventType, Guid eventId)
{
    // 异常冒泡到 MassTransit，由重试策略 + 死信队列处理
    // IdempotencyKey 已设置，重试不会重复发送
    await _notificationService.SendAsync(request).ConfigureAwait(false);
    _logger.LogInformation("通知发送成功 EventType={EventType} EventId={EventId} TemplateCode={TemplateCode}",
        eventType, eventId, request.TemplateCode);
}
```

**MassTransit 重试配置**（`appsettings.json` 既有 `RabbitMQ` 节点扩展）：

```json
"MassTransit": {
  "Retry": {
    "Count": 3,
    "Interval": "00:00:05",
    "Incremental": true
  }
}
```

**测试改动**：
- 既有 `ShouldFireAndForget` 测试改为 `ShouldSendNotification`，断言 `await result` 后 `_notificationServiceMock.SendAsync` 被调用
- `Consume_SendAsyncFails_ShouldNotThrow` 改为 `Consume_SendAsyncFails_ShouldThrow`，断言异常冒泡
- 新增 `Consume_SendAsyncSucceeds_ShouldAckMessage`

---

### 2.5 问题 3：Redis 事务 await

**改动文件**：
- `src/Services/Notification/Leno.Notification.Infrastructure/Services/RedisRateLimiter.cs`
- `src/Services/Notification/Leno.Notification.Application.Tests/RateLimiterTests.cs`

**改动点**：显式 await 所有事务 Task，消除 unobserved exception 风险。

**改动后代码**：

```csharp
var transaction = db.CreateTransaction();

// 1. 移除窗口外的过期记录
var removeTask = transaction.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, windowStart);

// 2. 统计窗口内的记录数
var countTask = transaction.SortedSetLengthAsync(key);

// 3. 添加当前请求记录
var addTask = transaction.SortedSetAddAsync(key, now, now);

// 4. 设置过期时间
var expireTask = transaction.KeyExpireAsync(key, window + TimeSpan.FromMinutes(1));

await transaction.ExecuteAsync().ConfigureAwait(false);

// 等待所有 Task 完成，避免 unobserved exception
await Task.WhenAll(removeTask, countTask, addTask, expireTask).ConfigureAwait(false);

var count = (int)(await countTask.ConfigureAwait(false));
```

**测试新增**：
- `AcquireAsync_OverLimit_ShouldDeny` — 验证 `count > limit` 拒绝路径
- `AcquireAsync_TransactionExecuteFails_ShouldDegradeToAllow` — 验证事务执行失败时降级
- `AcquireAsync_SmsChannel_ShouldApplyHourAndDayLimits` — 验证 SMS 双重限流

---

### 2.6 问题 8：对账分页

**改动文件**：
- `src/Services/Payment/Leno.Payment.Infrastructure/Services/ReconciliationService.cs`
- `src/Services/Payment/Leno.Payment.Infrastructure.Tests/ReconciliationServiceTests.cs`（新建）

**改动点**：
1. 将一次性 `QueryAsync(..., 1, 10000, ct)` 改为分页循环
2. `CompareReconciliation` 重构为接收预构建字典

**改动后 `ReconcileAsync` 片段**：

```csharp
const int PageSize = 500;
int page = 1;
var systemByOutTradeNo = new Dictionary<string, PaymentOrderAggregate>();
var systemByChannelTradeNo = new Dictionary<string, PaymentOrderAggregate>();

while (true)
{
    var batch = await paymentRepo.QueryAsync(
        null, channel, PaymentStatus.Paid,
        billDate, billDate.AddDays(1).AddTicks(-1),
        page, PageSize, ct).ConfigureAwait(false);

    if (batch.Count == 0) break;

    foreach (var o in batch)
    {
        if (!string.IsNullOrEmpty(o.OutTradeNo))
            systemByOutTradeNo[o.OutTradeNo] = o;
        if (!string.IsNullOrEmpty(o.ChannelTradeNo))
            systemByChannelTradeNo[o.ChannelTradeNo] = o;
    }

    if (batch.Count < PageSize) break;
    page++;
}

var diffs = CompareReconciliation(billDate, channel, channelRecords, systemByOutTradeNo, systemByChannelTradeNo);
```

**`CompareReconciliation` 重构**：

```csharp
private static List<ReconciliationDiff> CompareReconciliation(
    DateTime billDate,
    PaymentChannel channel,
    IReadOnlyList<ChannelRecord> channelRecords,
    IReadOnlyDictionary<string, PaymentOrderAggregate> systemByOutTradeNo,
    IReadOnlyDictionary<string, PaymentOrderAggregate> systemByChannelTradeNo)
{
    var diffs = new List<ReconciliationDiff>();
    var matchedOutTradeNos = new HashSet<string>();

    foreach (var channelRecord in channelRecords)
    {
        if (systemByOutTradeNo.TryGetValue(channelRecord.OutTradeNo, out var sysOrder))
        {
            matchedOutTradeNos.Add(channelRecord.OutTradeNo);
            if (sysOrder.Amount != channelRecord.Amount)
            {
                diffs.Add(ReconciliationDiff.AmountMismatch(billDate, channel, channelRecord, sysOrder));
            }
        }
        else
        {
            diffs.Add(ReconciliationDiff.ChannelOnly(billDate, channel, channelRecord));
        }
    }

    // 系统有但渠道无的订单
    foreach (var kvp in systemByOutTradeNo)
    {
        if (!matchedOutTradeNos.Contains(kvp.Key))
        {
            diffs.Add(ReconciliationDiff.SystemOnly(billDate, channel, kvp.Value));
        }
    }

    return diffs;
}
```

**新增测试**（`ReconciliationServiceTests.cs`）：
- `ReconcileAsync_LessThanPageSize_ShouldQueryOnce`
- `ReconcileAsync_MoreThanPageSize_ShouldQueryMultiplePages`
- `ReconcileAsync_ExactlyPageSize_ShouldQueryNextPage`
- `CompareReconciliation_AmountMismatch_ShouldReportDiff`
- `CompareReconciliation_ChannelOnly_ShouldReportDiff`
- `CompareReconciliation_SystemOnly_ShouldReportDiff`
- `CompareReconciliation_AllMatch_ShouldReportNoDiff`

---

### 2.7 问题 9：gRPC Polly 集成

**改动文件**：
- `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionPollyExtensions.cs`
- `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs`
- `src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/GrpcAntiCorruptionClientBaseTests.cs`

**关键设计决策**：采用**方案 2**（在 `GrpcAntiCorruptionClientBase.ExecuteAsync` 内嵌 Polly），无需新增拦截器，所有派生类自动获益。

**`AntiCorruptionPollyExtensions` 新增 gRPC 策略**：

```csharp
public static IServiceCollection AddLenoGrpcAntiCorruptionPolly(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var section = configuration.GetSection(SectionName);
    var retryCount = section?.GetValue("GrpcRetryCount", 2) ?? 2;
    var timeoutSeconds = section?.GetValue("GrpcTimeoutSeconds", 5) ?? 5;

    // gRPC retry：仅对临时性故障重试
    var grpcRetryPolicy = Policy
        .Handle<RpcException>(ex => IsTransientGrpcStatus(ex.StatusCode))
        .WaitAndRetryAsync(retryCount, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)));

    services.AddKeyedSingleton("GrpcAntiCorruptionRetry", grpcRetryPolicy);
    return services;
}

private static bool IsTransientGrpcStatus(StatusCode statusCode) =>
    statusCode is StatusCode.Unavailable
        or StatusCode.DeadlineExceeded
        or StatusCode.Aborted
        or StatusCode.ResourceExhausted;
```

**`GrpcAntiCorruptionClientBase.ExecuteAsync` 改造**：

```csharp
protected async Task<T> ExecuteAsync<T>(
    string operation,
    Func<CancellationToken, Task<T>> execute,
    CancellationToken ct = default)
{
    var retryPolicy = _serviceProvider.GetRequiredKeyedService<IAsyncPolicy>("GrpcAntiCorruptionRetry");
    var sw = Stopwatch.StartNew();

    try
    {
        var result = await retryPolicy.ExecuteAsync(() => execute(ct)).ConfigureAwait(false);
        // ... 既有埋点
        return result;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
    catch (RpcException ex) when (IsUnavailable(ex.StatusCode))
    {
        // 重试耗尽后包装为 AntiCorruptionException
        throw new AntiCorruptionException($"gRPC 调用不可用 {operation}", ex);
    }
    catch (RpcException ex)
    {
        throw new AntiCorruptionException($"gRPC 调用失败 {operation}: {ex.StatusCode}", ex);
    }
    catch (DomainException) { throw; }
    catch (Exception ex)
    {
        throw new AntiCorruptionException($"防腐层调用异常 {operation}", ex);
    }
}
```

**关键约束**：
- **不重试** `InvalidArgument`/`NotFound`/`PermissionDenied`/`Unauthenticated`（业务错误）
- **重试** `Unavailable`/`DeadlineExceeded`/`Aborted`/`ResourceExhausted`（临时性故障）
- **重试次数 2 次**（保守值，避免放大下游压力）
- 与既有 `CircuitBreakerState` 共存：Polly 在 `ExecuteAsync` 内，`CircuitBreakerState` 在 `AntiCorruptionDispatcher` 外层，互不干扰

**`GrpcAntiCorruptionClientBase` 构造函数注入**：

```csharp
protected GrpcAntiCorruptionClientBase(IServiceProvider serviceProvider)
{
    _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
}
```

**派生类构造函数同步更新**（若当前未注入 `IServiceProvider`）：
- `src/Services/Order/Leno.Order.Infrastructure/AntiCorruption/GrpcProductAntiCorruptionClient.cs`
- `src/Services/Order/Leno.Order.Infrastructure/AntiCorruption/GrpcPromotionAntiCorruptionClient.cs`
- `src/Services/Order/Leno.Order.Infrastructure/AntiCorruption/GrpcPointsAntiCorruptionClient.cs`
- `src/Services/Cart/Leno.Cart.Infrastructure/AntiCorruption/Grpc*AntiCorruptionClient.cs`
- `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/Grpc*AntiCorruptionClient.cs`
- `src/Services/ReviewAfterSales/.../Grpc*AntiCorruptionClient.cs`
- `src/Services/Notification/.../Grpc*AntiCorruptionClient.cs`

**替代方案**：若 `GrpcAntiCorruptionClientBase` 已注入 `IServiceProvider`（或可通过其他方式获取 keyed service），可避免派生类改动。实施时优先检查既有构造函数，若已注入则无需派生类改动。

**新增测试**：
- `ExecuteAsync_TransientFailure_RetriesAndSucceeds` — mock 第一次抛 Unavailable，第二次成功，断言重试
- `ExecuteAsync_NonTransientFailure_DoesNotRetry` — mock 抛 InvalidArgument，断言不重试
- `ExecuteAsync_AllRetriesFail_ThrowsAntiCorruptionException` — mock 始终抛 Unavailable，断言重试 2 次后抛
- `ExecuteAsync_DomainException_DoesNotRetry` — mock 抛 DomainException，断言直接抛不重试
- `ExecuteAsync_CancellationToken_DoesNotRetry` — 验证取消不重试

---

## 3. 数据流与错误处理

### 3.1 错误处理矩阵

| 问题 | 场景 | 处理策略 | 调用方感知 |
|---|---|---|---|
| 4 WeChatPay | Redis 故障 | `ValidateNonceAsync` throw → `VerifySignatureAsync` catch 转 `Failure` → NotifyController 返回 401 | 微信重试回调，Redis 恢复后成功 |
| 4 WeChatPay | Redis 未配置 | `return true`（保留兼容）+ LogWarning | 验签继续，但无防重放保护 |
| 5 Alipay | PEM 格式错误 | `catch ArgumentException` → LogError + `return false` | 验签失败，运维从日志定位配置问题 |
| 5 Alipay | sign 非 Base64 | `catch FormatException` → LogWarning + `return false` | 验签失败，可能攻击或客户端异常 |
| 5 Alipay | RSA 验签失败 | `catch CryptographicException` → LogDebug + `return false` | 正常验签失败路径 |
| 5 Alipay | 未预期异常 | 冒泡到 `AlipayChannel.VerifySignatureAsync` catch → `Failure` | 验签异常，运维需排查 |
| 1 async void | `OnMessage` 反序列化失败 | try-catch LogError，不抛 | 单条消息处理失败不影响订阅 |
| 1 async void | `SubscribeAsync` 启动失败 | `StartAsync` catch LogError，依赖重连机制 | 网关启动后无缓存失效，但进程不崩 |
| 2 通知 | `INotificationService.SendAsync` 失败 | 异常冒泡到 MassTransit，重试 3 次 | 重试期间消息不 ACK，最终入死信队列 |
| 2 通知 | 重试耗尽 | MassTransit 转 `error` 队列 | 运维需处理死信队列 |
| 3 Redis 限流 | 事务 `ExecuteAsync` 失败 | 外层 catch 降级 `AllowedResult`（fail-open） | 限流失效，Redis 恢复后恢复 |
| 3 Redis 限流 | 单命令 Task faulted | `Task.WhenAll` 暴露异常，外层 catch 降级 | 同上 |
| 8 对账 | 分页查询中途异常 | 外层 try-catch 记日志，跳过该渠道 | 该渠道对账缺失，运维次日排查 |
| 9 gRPC | 临时性故障（Unavailable 等） | Polly 重试 2 次，指数退避（1s, 2s） | 重试成功无感知；重试耗尽抛 `AntiCorruptionException` |
| 9 gRPC | 业务错误（InvalidArgument 等） | 不重试，直接包装 `AntiCorruptionException` | 调用方按业务错误处理 |
| 9 gRPC | `DomainException` | 不重试，直接抛 | 调用方按领域异常处理 |

### 3.2 事务边界

| 操作 | 事务边界 | 一致性保证 |
|---|---|---|
| WeChatPay 验签 | 无写入（Redis SET NX 原子） | Redis 故障时 fail-closed |
| Alipay 验签 | 无写入 | 无副作用 |
| `OnMessage` 缓存失效 | 单条 Redis KeyDelete（无事务） | 双删模式缩小脏读窗口 |
| 通知发送 | MassTransit 消息 ACK 时机 | 重试期间不 ACK，ACK 后保证已发送 |
| Redis 限流 | `ITransaction.ExecuteAsync` 原子 | Redis 故障时降级允许 |
| 对账 | 分页查询 + 差异持久化 | 差异保存同事务 `SaveEntitiesAsync` |
| gRPC 防腐层 | 单次调用 | Polly 重试不引入事务，依赖下游幂等 |

### 3.3 关键数据流（通知必达为例）

```
[RabbitMQ] NotificationEvent
  └── [MassTransit] NotificationEventConsumer.Consume
        └── await SendAsync(request)            ← 不再 fire-and-forget
              ├── [INotificationService.SendAsync]
              │     ├── 幂等检查（IdempotencyKey=EventId）
              │     ├── 限流检查（RedisRateLimiter）
              │     ├── 模板渲染
              │     └── 渠道发送（SMS/Email/InApp）
              ├── 成功 → MassTransit ACK → 消息从队列移除
              └── 失败 → 异常冒泡 → MassTransit 重试（3 次，5s/10s/15s）
                          ├── 重试成功 → ACK
                          └── 重试耗尽 → 移至 error 队列 → 运维处理
```

**幂等性保证**：
- `IdempotencyKey = evt.EventId.ToString()`（既有）
- `INotificationService` 实现按 IdempotencyKey 去重（既有）
- 重试不会重复发送

### 3.4 关键数据流（gRPC Polly 重试为例）

```
[SellerShop] SellerInternalQueryService.ValidateOwnershipAsync
  └── [GrpcProductAntiCorruptionClient] GetSpuSellerIdAsync
        └── [GrpcAntiCorruptionClientBase] ExecuteAsync("get_spu_seller", ...)
              └── Polly.Retry(2, exponential backoff)
                    ├── 第 1 次：RpcException(Unavailable) → 等 1s
                    ├── 第 2 次：RpcException(Unavailable) → 等 2s
                    ├── 第 3 次：成功 → 返回 sellerId
                    └── 重试耗尽 → AntiCorruptionException → 返回 null（fail-closed）
                                                                    └── ValidateOwnership 返回 false
```

**关键约束**：
- 重试仅针对 `Unavailable`/`DeadlineExceeded`/`Aborted`/`ResourceExhausted`
- `InvalidArgument`/`NotFound`/`PermissionDenied` 不重试（业务错误）
- `DomainException` 不重试（领域异常）
- 与既有 `AntiCorruptionDispatcher.CircuitBreakerState` 共存：
  - Polly 在单次调用内重试
  - `CircuitBreakerState` 在外层判断是否降级到 HttpClient
  - 两者互不干扰

---

## 4. 测试策略

### 4.1 测试覆盖矩阵

| # | 问题 | 测试项目 | 新增测试方法 |
|---|---|---|---|
| 1 | async void | `Leno.ApiGateway.Tests` | `OnMessage_ValidMessage_DeletesKeyAndDelayedDelete`、`OnMessage_DeserializeThrows_DoesNotCrashProcess`、`StartAsync_SubscribeAsyncFails_LogsButDoesNotThrow`（既有 13 个测试改为 SubscribeAsync） |
| 2 | fire-and-forget | `Leno.Notification.Application.Tests` | `Consume_OrderCreated_ShouldSendNotification`（原 `ShouldFireAndForget` 改名）、`Consume_SendAsyncFails_ShouldThrow`（原 `ShouldNotThrow` 改语义）、`Consume_SendAsyncSucceeds_ShouldAckMessage` |
| 3 | Redis 事务 | `Leno.Notification.Application.Tests` | `AcquireAsync_OverLimit_ShouldDeny`、`AcquireAsync_TransactionExecuteFails_ShouldDegradeToAllow`、`AcquireAsync_SmsChannel_ShouldApplyHourAndDayLimits` |
| 4 | WeChatPay | `Leno.Payment.Infrastructure.Tests` | `ValidateNonce_RedisThrows_ShouldFailVerification`、`ValidateNonce_RedisUnavailable_ShouldSkipAndSucceed`、`VerifySignatureAsync_ReplayAttack_ShouldFail` |
| 5 | Alipay 验签 | `Leno.Payment.Infrastructure.Tests` | `VerifySign_InvalidPublicKey_ShouldReturnFalseAndLogError`、`VerifySign_InvalidBase64Sign_ShouldReturnFalseAndLogWarning`、`VerifySign_TamperedSignature_ShouldReturnFalseAndLogDebug`、`VerifySign_UnexpectedException_ShouldBubbleToCaller` |
| 8 | 对账分页 | `Leno.Payment.Infrastructure.Tests`（新建 `ReconciliationServiceTests.cs`） | `ReconcileAsync_LessThanPageSize_ShouldQueryOnce`、`ReconcileAsync_MoreThanPageSize_ShouldQueryMultiplePages`、`ReconcileAsync_ExactlyPageSize_ShouldQueryNextPage`、`CompareReconciliation_AmountMismatch_ShouldReportDiff`、`CompareReconciliation_ChannelOnly_ShouldReportDiff`、`CompareReconciliation_SystemOnly_ShouldReportDiff`、`CompareReconciliation_AllMatch_ShouldReportNoDiff` |
| 9 | gRPC Polly | `Leno.Infrastructure.Tests` | `ExecuteAsync_TransientFailure_RetriesAndSucceeds`、`ExecuteAsync_NonTransientFailure_DoesNotRetry`、`ExecuteAsync_AllRetriesFail_ThrowsAntiCorruptionException`、`ExecuteAsync_DomainException_DoesNotRetry`、`ExecuteAsync_CancellationToken_DoesNotRetry` |

**合计**：约 28 个新增测试方法 + 13 个既有测试改造（async void 改 SubscribeAsync）。

### 4.2 测试模式

**gRPC Polly 测试模式**（问题 9）：

```csharp
[Fact]
public async Task ExecuteAsync_TransientFailure_RetriesAndSucceeds()
{
    var callCount = 0;
    Func<CancellationToken, Task<string>> execute = ct =>
    {
        callCount++;
        if (callCount < 3)
            throw new RpcException(new Status(StatusCode.Unavailable, "gRPC down"));
        return Task.FromResult("success");
    };

    var sut = CreateServiceWithRetryPolicy(retryCount: 2);
    var result = await sut.ExecuteAsyncPublic("test", execute, CancellationToken.None);

    result.Should().Be("success");
    callCount.Should().Be(3);  // 1 次原始 + 2 次重试
}
```

**WeChatPay fail-closed 测试模式**（问题 4）：

```csharp
[Fact]
public async Task ValidateNonce_RedisThrows_ShouldFailVerification()
{
    var redis = new Mock<IConnectionMultiplexer>();
    var db = new Mock<IDatabase>();
    db.Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
        .ThrowsAsync(new RedisConnectionException("Redis down"));
    redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(db.Object);

    var sut = new WeChatPayChannel(configProvider.Object, redis.Object, Mock.Of<ILogger<WeChatPayChannel>>());
    var headers = CreateValidHeaders();
    var body = CreateValidBody();

    var result = await sut.VerifySignatureAsync(headers, body, CancellationToken.None);

    result.IsValid.Should().BeFalse();
}
```

**对账分页测试模式**（问题 8）：

```csharp
[Fact]
public async Task ReconcileAsync_MoreThanPageSize_ShouldQueryMultiplePages()
{
    // 安排：模拟 1200 条支付单，PageSize=500
    var paymentRepo = new Mock<IPaymentOrderRepository>();
    paymentRepo.SetupSequence(r => r.QueryAsync(It.IsAny<Guid?>(), It.IsAny<PaymentChannel?>(),
        It.IsAny<PaymentStatus?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
        It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(CreateOrders(500))   // 第 1 页
        .ReturnsAsync(CreateOrders(500))   // 第 2 页
        .ReturnsAsync(CreateOrders(200))   // 第 3 页
        .ReturnsAsync(new List<PaymentOrderAggregate>(0));  // 第 4 页空

    var sut = CreateReconciliationService(paymentRepo.Object);

    // 行动
    await sut.ReconcileAsync(DateTime.UtcNow.Date, CancellationToken.None);

    // 断言：查询 4 次（3 次有数据 + 1 次空）
    paymentRepo.Verify(r => r.QueryAsync(...), Times.Exactly(4));
}
```

### 4.3 测试基础设施

- **既有 helper 复用**：`TestServerCallContext`（Promotion.Api.Tests 中已有，可共享）
- **RSA 测试 helper**：`GenerateKeyPair()`（Payment.Infrastructure.Tests 中已有）
- **EF Core InMemory**：对账测试用 InMemory provider
- **Mock MassTransit**：通知测试用 `ConsumeContext<T>` mock

### 4.4 不在本子项目测试范围

- Testcontainers 集成测试（Redis/RabbitMQ 真实容器）→ P1-D
- 死信队列处理测试 → P1-D
- gRPC 真实网络故障测试 → P1-D
- Saga 崩溃恢复测试 → P1-B.6

---

## 5. 改动文件清单 + 验收标准

### 5.1 改动文件清单（按问题分组）

#### 问题 4：WeChatPay 防重放 fail-closed（3 文件）

| 文件 | 变更类型 |
|---|---|
| `src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayChannel.cs` | 修改：`ValidateNonceAsync` Redis 异常改 throw；`VerifySignatureAsync` 包 try-catch |
| `src/Services/Payment/Leno.Payment.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | 修改：`WeChatPayChannel` DI 注册同步更新（若构造函数变更） |
| `src/Services/Payment/Leno.Payment.Infrastructure.Tests/InfrastructureTests.cs` | 修改：`WeChatPayChannelTests` 补 3 个测试 |

#### 问题 5：Alipay 验签异常分类（3 文件）

| 文件 | 变更类型 |
|---|---|
| `src/Services/Payment/Leno.Payment.Infrastructure/Channels/Alipay/AlipaySignatureHelper.cs` | 修改：`VerifySign` 新增 `ILogger?` 参数；catch 分类（ArgumentException/FormatException/CryptographicException）+ 记日志；移除通用 `catch (Exception)` |
| `src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayChannel.cs` | 修改：`VerifySignatureAsync` 调用 `VerifySign` 传 logger；包 try-catch 兜底未预期异常 |
| `src/Services/Payment/Leno.Payment.Infrastructure.Tests/InfrastructureTests.cs` | 修改：`AlipayChannelTests` 补 4 个测试 |

#### 问题 1：async void OnMessage 改造（2 文件）

| 文件 | 变更类型 |
|---|---|
| `src/ApiGateway/Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs` | 修改：`OnMessage` 改 `async Task`；`EnsureSubscribed` 改 `EnsureSubscribedAsync` 用 `SubscribeAsync`；`StartAsync` await；新增 `IAsyncDisposable` |
| `src/ApiGateway/Leno.ApiGateway.Tests/Services/CacheInvalidationSubscriberTests.cs` | 修改：13 个测试改 `SubscribeAsync` mock；新增 3 个测试 |

#### 问题 2：fire-and-forget 改 await（3 文件）

| 文件 | 变更类型 |
|---|---|
| `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs` | 修改：`_ = SendAsync` 改 `await SendAsync`；移除 `SendAsync` 内 try-catch；`Consume` 返回点改 |
| `src/Services/Notification/Leno.Notification.Application.Tests/NotificationEventConsumerTests.cs` | 修改：12 个测试改 await 语义；新增 3 个测试 |
| `src/Services/Notification/Leno.Notification.Api/appsettings.json` | 修改：新增 MassTransit Retry 配置节 |

#### 问题 3：Redis 事务 await（2 文件）

| 文件 | 变更类型 |
|---|---|
| `src/Services/Notification/Leno.Notification.Infrastructure/Services/RedisRateLimiter.cs` | 修改：显式 await 所有事务 Task + `Task.WhenAll` |
| `src/Services/Notification/Leno.Notification.Application.Tests/RateLimiterTests.cs` | 修改：补 3 个测试 |

#### 问题 8：对账分页（2 文件）

| 文件 | 变更类型 |
|---|---|
| `src/Services/Payment/Leno.Payment.Infrastructure/Services/ReconciliationService.cs` | 修改：`ReconcileAsync` 改分页循环；`CompareReconciliation` 重构接收字典 |
| `src/Services/Payment/Leno.Payment.Infrastructure.Tests/ReconciliationServiceTests.cs` | 新建：7 个测试 |

#### 问题 9：gRPC Polly 集成（3 + N 文件）

| 文件 | 变更类型 |
|---|---|
| `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionPollyExtensions.cs` | 修改：新增 `AddLenoGrpcAntiCorruptionPolly` + gRPC retry 策略 |
| `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs` | 修改：构造函数注入 `IServiceProvider`；`ExecuteAsync` 内嵌 Polly retry |
| `src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/GrpcAntiCorruptionClientBaseTests.cs` | 修改：补 5 个测试 |
| 派生类 `Grpc*AntiCorruptionClient.cs`（最多 7 个文件） | 修改：构造函数同步更新（仅当 `GrpcAntiCorruptionClientBase` 当前未注入 `IServiceProvider` 时） |

**合计**：约 18 个文件改动（1 个新建 + 17 个修改）+ 约 7 个派生类可能同步改动。

### 5.2 验收标准

#### 功能验收
1. ✅ `bash scripts/check-placeholders.sh` 通过（不引入新占位符）
2. ✅ WeChatPay Redis 故障时验签失败（fail-closed），Redis 正常时验签通过
3. ✅ Alipay 验签失败时日志区分 PEM 格式错误 / sign 非 Base64 / RSA 失败
4. ✅ ApiGateway `OnMessage` 不再是 `async void`，使用 `SubscribeAsync`
5. ✅ 通知发送失败时异常冒泡到 MassTransit，不立即 ACK
6. ✅ Redis 限流事务所有 Task 被 await，无 unobserved exception
7. ✅ 对账服务支持分页查询，10000+ 支付单不漏对账
8. ✅ gRPC 临时性故障自动重试 2 次，业务错误不重试

#### 质量验收
9. ✅ `dotnet build Leno.slnx -c Release` 零错误（警告数不增加）
10. ✅ `dotnet test` 新增测试全部通过
11. ✅ 新增测试方法数 ≥ 25
12. ✅ 既有测试无回归（除预先存在的 Redis/RabbitMQ 容器依赖失败）

#### 设计约束验收
13. ✅ 不新建聚合根（问题 6/7 留 P1-B.6）
14. ✅ 不修改既有 proto（无 gRPC 接口变更）
15. ✅ `AlipaySignatureHelper` 保留 static + 可选 `ILogger?` 参数（不破坏 11 处调用方）
16. ✅ gRPC Polly 仅重试临时性故障（`Unavailable`/`DeadlineExceeded`/`Aborted`/`ResourceExhausted`）
17. ✅ gRPC Polly 与既有 `CircuitBreakerState` 共存，互不干扰
18. ✅ WeChatPay `fail-closed` 对齐 T19 已修复的 `MarkCallbackProcessedAsync` 模式

#### Git 提交规范
19. ✅ 实现完成后提交到 git 仓库，提交说明采用中文
20. ✅ 推送到远程仓库

### 5.3 实施顺序

按 §1.3 风险与依赖排序：
1. **问题 4** WeChatPay fail-closed（安全优先）
2. **问题 5** Alipay 验签异常分类（安全优先）
3. **问题 1** async void 改造（网关稳定性）
4. **问题 2** fire-and-forget 改 await（通知必达）
5. **问题 3** Redis 事务 await（限流准确性）
6. **问题 8** 对账分页（数据完整性）
7. **问题 9** gRPC Polly 集成（性能与稳定性）

每项独立 Task，TDD 红-绿-提交循环。

### 5.4 不在本子项目范围

- **P1-B.6**：Saga 持久化（问题 6 + 7）→ 独立 spec
- **P1-C**：基础设施加固（InMemoryRefreshTokenStore / Redis 库存 DB 事务）
- **P1-D**：测试覆盖率补齐（6 个测试项目空置）
- Testcontainers 集成测试
- 外部告警系统联动
- MassTransit 死信队列处理流程（仅配置重试，不实现 DLQ 处理器）

### 5.5 风险与权衡

| 风险 | 影响 | 缓解措施 |
|---|---|---|
| WeChatPay fail-closed 在 Redis 持续故障期间拒所有回调 | 微信支付通知无法到达，影响订单状态流转 | Redis 恢复后微信重试可恢复；运维需监控 Redis 健康 |
| 通知改 await 后消费速度下降 | 高峰期消息堆积 | MassTransit 并发消费配置 + 重试间隔退避 |
| Alipay 移除通用 `catch (Exception)` | 未预期异常冒泡可能破坏调用方 | 调用方 `VerifySignatureAsync` 加 try-catch 兜底 |
| gRPC Polly 重试放大下游压力 | 下游故障期间重试加重负载 | 重试次数保守（2 次）+ 指数退避（1s, 2s） |
| `GrpcAntiCorruptionClientBase` 注入 `IServiceProvider` | 派生类构造函数变更 | 实施时优先检查既有构造函数，最小化派生类改动 |
| MassTransit 重试配置改动 | 既有消费者行为变化 | 仅新增 Retry 配置节，不修改既有 RabbitMQ 连接配置 |

---

## 6. 后续子项目预告

### 6.1 P1-B.6：Saga 持久化

**范围**：
- 问题 6 `OrderSagaOrchestrator` 状态持久化
- 问题 7 `CancelAsync` 跨服务串行无补偿

**预期设计**：
- 新增 `OrderSagaState` 聚合（包含 OrderId、当前步骤、已执行步骤、已补偿步骤）
- 新增 `ISagaStateRepository` 接口与 EF Core 实现
- 新增 `SagaRecoveryService` 后台服务（30s 扫描超时 Saga）
- 改造 `OrderSagaOrchestrator`：每步骤前持久化状态，异常时标记 `Compensating`
- 改造 `OrderAppService.CancelAsync`：调用 Saga 补偿接口而非直接跨服务串行调用

### 6.2 P1-C：基础设施加固

- `InMemoryRefreshTokenStore` 替换为 Redis 实现
- Redis 库存与 DB 基线事务一致性
- CacheService `JsonSerializerOptions` 复用

### 6.3 P1-D：测试覆盖率补齐

- UserAuth.Infrastructure.Tests 完全空置补齐
- SellerShop.Infrastructure.Tests 仅 SmokeTests 补齐
- Notification.Infrastructure.Tests 仅 SmokeTests 补齐
- Testcontainers 集成测试基础设施
