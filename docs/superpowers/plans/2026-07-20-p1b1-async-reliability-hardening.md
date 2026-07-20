# P1-B.1 异步可靠性加固实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 7 项异步可靠性问题（WeChatPay fail-open / Alipay 吞异常 / async void / fire-and-forget / Redis 事务 Task 丢弃 / 对账无分页 / gRPC 无 Polly）

**Architecture:** 7 项独立修复，每项独立 Task。每 Task 遵循 TDD 红-绿-提交循环。问题 6/7（Saga 持久化）拆分到 P1-B.6 独立 spec。

**Tech Stack:** .NET 8 + xUnit + Moq + FluentAssertions + Polly + MassTransit + StackExchange.Redis + Grpc.Core

**Spec:** [docs/superpowers/specs/2026-07-20-p1b1-async-reliability-hardening-design.md](../specs/2026-07-20-p1b1-async-reliability-hardening-design.md)

---

## Task 1: WeChatPay 防重放 fail-closed

**Files:**
- Modify: `src/Services/Payment/Leno.Payment.Infrastructure/Channels/WeChatPayChannel.cs`（`ValidateNonceAsync` 方法 + `VerifySignatureAsync` 调用方 try-catch）
- Modify: `src/Services/Payment/Leno.Payment.Infrastructure.Tests/InfrastructureTests.cs`（`WeChatPayChannelTests` 新增 3 个测试）

**Spec 参考：** §2.1

- [ ] **Step 1**: 阅读现有 `WeChatPayChannel.cs` 的 `ValidateNonceAsync` 与 `VerifySignatureAsync` 实现，确认当前 fail-open 行为
- [ ] **Step 2**: 写失败测试 `ValidateNonce_RedisThrows_ShouldFailVerification`（mock Redis 抛 `RedisConnectionException`，断言 `VerifySignatureAsync` 返回 `IsValid=false`）
- [ ] **Step 3**: 运行测试确认失败（当前实现 fail-open，返回 `IsValid=true`）
- [ ] **Step 4**: 修改 `ValidateNonceAsync`：Redis catch 块改为 `throw`（fail-closed），保留 `_redis is null` 分支但升级日志为 `LogWarning`
- [ ] **Step 5**: 修改 `VerifySignatureAsync`：包 `try-catch` 在 catch 内转 `SignatureVerificationResult.Failure("验签异常")`
- [ ] **Step 6**: 运行测试确认通过
- [ ] **Step 7**: 补充测试 `ValidateNonce_RedisUnavailable_ShouldSkipAndSucceed` + `VerifySignatureAsync_ReplayAttack_ShouldFail`
- [ ] **Step 8**: 运行全部测试
- [ ] **Step 9**: `dotnet build src/Services/Payment/Leno.Payment.Infrastructure.Tests/Leno.Payment.Infrastructure.Tests.csproj` 零错误
- [ ] **Step 10**: Commit `fix(payment): WeChatPay 防重放检查改 fail-closed，Redis 故障拒绝验签`

---

## Task 2: Alipay 验签异常分类

**Files:**
- Modify: `src/Services/Payment/Leno.Payment.Infrastructure/Channels/Alipay/AlipaySignatureHelper.cs`（`VerifySign` 方法）
- Modify: `src/Services/Payment/Leno.Payment.Infrastructure/Channels/AlipayChannel.cs`（`VerifySignatureAsync` 调用方兜底 try-catch）
- Modify: `src/Services/Payment/Leno.Payment.Infrastructure.Tests/InfrastructureTests.cs`（`AlipayChannelTests` 新增 4 个测试）

**Spec 参考：** §2.2

- [ ] **Step 1**: 阅读现有 `AlipaySignatureHelper.VerifySign`（确认 `catch (Exception) { return false; }` 吞异常位置）
- [ ] **Step 2**: 写失败测试 `VerifySign_InvalidPublicKey_ShouldReturnFalseAndLogError`（传入非法 PEM，断言返回 false + mock ILogger 验证 LogError 被调用）
- [ ] **Step 3**: 运行测试确认失败（当前实现吞异常不记日志）
- [ ] **Step 4**: 修改 `VerifySign`：新增 `ILogger? logger = null` 可选参数；移除通用 `catch (Exception)`；分类 catch `ArgumentException`（LogError）/`FormatException`（LogWarning）/`CryptographicException`（LogDebug）
- [ ] **Step 5**: 修改 `AlipayChannel.VerifySignatureAsync`：调用 `VerifySign(formFields, config.ApiKey, sign, _logger)`；外层包 `try-catch` 兜底未预期异常转 `Failure`
- [ ] **Step 6**: 运行测试确认通过
- [ ] **Step 7**: 补充测试 `VerifySign_InvalidBase64Sign_ShouldReturnFalseAndLogWarning` + `VerifySign_TamperedSignature_ShouldReturnFalseAndLogDebug` + `VerifySign_UnexpectedException_ShouldBubbleToCaller`
- [ ] **Step 8**: 运行全部测试
- [ ] **Step 9**: `dotnet build` Payment.Infrastructure 项目零错误零警告新增
- [ ] **Step 10**: Commit `fix(payment): Alipay 验签异常分类，区分 PEM/Base64/RSA 错误并记日志`

---

## Task 3: ApiGateway async void OnMessage 改造

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs`（`OnMessage` 改 `async Task`，`EnsureSubscribed` 改 `EnsureSubscribedAsync`，`StartAsync` await，新增 `IAsyncDisposable`）
- Modify: `src/ApiGateway/Leno.ApiGateway.Tests/Services/CacheInvalidationSubscriberTests.cs`（13 个测试改 SubscribeAsync mock + 3 个新增测试）

**Spec 参考：** §2.3

- [ ] **Step 1**: 阅读现有 `CacheInvalidationSubscriber.cs`，确认 `OnMessage` 是 `async void`，`Subscribe` 调用方式
- [ ] **Step 2**: 写失败测试 `OnMessage_DeserializeThrows_DoesNotCrashProcess`（直接调用 `OnMessage` 传入非法 JSON，断言不抛异常）
- [ ] **Step 3**: 运行测试确认失败（`async void` 无法 await，异常会崩进程）
- [ ] **Step 4**: 修改 `OnMessage` 签名 `async void` → `async Task`，内层加 try-catch LogError
- [ ] **Step 5**: 修改 `EnsureSubscribed` → `EnsureSubscribedAsync`，改用 `SubscribeAsync(RedisChannel, Func<RedisChannel, RedisValue, Task>)`
- [ ] **Step 6**: 修改 `StartAsync`：`await EnsureSubscribedAsync()` 包 try-catch LogError
- [ ] **Step 7**: 新增 `IAsyncDisposable.DisposeAsync` 实现（`UnsubscribeAllAsync`）
- [ ] **Step 8**: 修改 13 个既有测试：`Subscribe` mock 改 `SubscribeAsync` + `Action` 改 `Func<RedisChannel, RedisValue, Task>`
- [ ] **Step 9**: 运行全部测试确认通过
- [ ] **Step 10**: 补充测试 `OnMessage_ValidMessage_DeletesKeyAndDelayedDelete` + `StartAsync_SubscribeAsyncFails_LogsButDoesNotThrow`
- [ ] **Step 11**: `dotnet build src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj` 零错误
- [ ] **Step 12**: Commit `fix(gateway): CacheInvalidationSubscriber async void 改 SubscribeAsync，避免崩溃进程`

---

## Task 4: 通知 fire-and-forget 改 await

**Files:**
- Modify: `src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs`（`_ = SendAsync` 改 `await SendAsync`，移除 SendAsync 内 try-catch）
- Modify: `src/Services/Notification/Leno.Notification.Application.Tests/NotificationEventConsumerTests.cs`（12 个测试改 await 语义 + 3 个新增测试）
- Modify: `src/Services/Notification/Leno.Notification.Api/appsettings.json`（新增 MassTransit Retry 配置节）

**Spec 参考：** §2.4

- [ ] **Step 1**: 阅读现有 `NotificationEventConsumer.Consume` 与 `SendAsync`，确认 `_ = SendAsync(...)` 位置与 try-catch 范围
- [ ] **Step 2**: 写失败测试 `Consume_SendAsyncFails_ShouldThrow`（mock `INotificationService.SendAsync` 抛异常，断言 `Consume` 抛异常而非吞掉）
- [ ] **Step 3**: 运行测试确认失败（当前实现 fire-and-forget，`Consume` 立即返回 `Task.CompletedTask`）
- [ ] **Step 4**: 修改 `Consume`：`_ = SendAsync(...)` 改 `await SendAsync(...)`，移除 `return Task.CompletedTask`
- [ ] **Step 5**: 修改 `SendAsync`：移除内层 try-catch，异常冒泡到 MassTransit
- [ ] **Step 6**: 修改 12 个既有测试：`ShouldFireAndForget` 改 `ShouldSendNotification`，断言 `await result` 后 SendAsync 被调用
- [ ] **Step 7**: 运行测试确认通过
- [ ] **Step 8**: 修改 `appsettings.json`：在 `RabbitMQ` 节点下新增 `MassTransit:Retry:{Count:3, Interval:00:00:05, Incremental:true}`
- [ ] **Step 9**: 补充测试 `Consume_OrderCreated_ShouldSendNotification` + `Consume_SendAsyncSucceeds_ShouldAckMessage`
- [ ] **Step 10**: `dotnet build` Notification 项目零错误
- [ ] **Step 11**: Commit `fix(notification): fire-and-forget 改 await，异常冒泡到 MassTransit 重试`

---

## Task 5: Redis 限流事务显式 await

**Files:**
- Modify: `src/Services/Notification/Leno.Notification.Infrastructure/Services/RedisRateLimiter.cs`（事务 Task 显式 await + `Task.WhenAll`）
- Modify: `src/Services/Notification/Leno.Notification.Application.Tests/RateLimiterTests.cs`（3 个新增测试）

**Spec 参考：** §2.5

- [ ] **Step 1**: 阅读现有 `RedisRateLimiter.AcquireAsync`，确认 4 个事务 Task 未 await 位置（removeTask/countTask/addTask/expireTask）
- [ ] **Step 2**: 写失败测试 `AcquireAsync_OverLimit_ShouldDeny`（mock 事务 countTask 返回 11，limit=10，断言 `Allowed=false`）
- [ ] **Step 3**: 运行测试确认失败（当前实现 countTask 未 await，count 永远为 0）
- [ ] **Step 4**: 修改 `AcquireAsync`：`await transaction.ExecuteAsync()` 后追加 `await Task.WhenAll(removeTask, countTask, addTask, expireTask)`，再 `await countTask` 取值
- [ ] **Step 5**: 运行测试确认通过
- [ ] **Step 6**: 补充测试 `AcquireAsync_TransactionExecuteFails_ShouldDegradeToAllow` + `AcquireAsync_SmsChannel_ShouldApplyHourAndDayLimits`
- [ ] **Step 7**: `dotnet build` Notification.Infrastructure 项目零错误
- [ ] **Step 8**: Commit `fix(notification): RedisRateLimiter 事务 Task 显式 await，消除 unobserved exception`

---

## Task 6: 对账服务分页查询

**Files:**
- Modify: `src/Services/Payment/Leno.Payment.Infrastructure/Services/ReconciliationService.cs`（`ReconcileAsync` 改分页循环，`CompareReconciliation` 重构接收字典）
- Create: `src/Services/Payment/Leno.Payment.Infrastructure.Tests/ReconciliationServiceTests.cs`（7 个测试）

**Spec 参考：** §2.6

- [ ] **Step 1**: 阅读现有 `ReconciliationService.ReconcileAsync`，确认 `QueryAsync(..., 1, 10000, ct)` 一次性查询位置
- [ ] **Step 2**: 写失败测试 `ReconcileAsync_MoreThanPageSize_ShouldQueryMultiplePages`（mock `QueryAsync` SetupSequence 返回 500+500+200+空，断言调用 4 次）
- [ ] **Step 3**: 运行测试确认失败（当前实现只调用 1 次）
- [ ] **Step 4**: 修改 `ReconcileAsync`：将一次性查询改为 `while` 循环 + PageSize=500，构建 `systemByOutTradeNo`/`systemByChannelTradeNo` 字典
- [ ] **Step 5**: 修改 `CompareReconciliation` 签名：接收 `IReadOnlyDictionary<string, PaymentOrderAggregate>` 而非 `IReadOnlyList<PaymentOrderAggregate>`
- [ ] **Step 6**: 运行测试确认通过
- [ ] **Step 7**: 补充测试 `ReconcileAsync_LessThanPageSize_ShouldQueryOnce` + `ReconcileAsync_ExactlyPageSize_ShouldQueryNextPage` + `CompareReconciliation_AmountMismatch_ShouldReportDiff` + `CompareReconciliation_ChannelOnly_ShouldReportDiff` + `CompareReconciliation_SystemOnly_ShouldReportDiff` + `CompareReconciliation_AllMatch_ShouldReportNoDiff`
- [ ] **Step 8**: `dotnet build` Payment.Infrastructure.Tests 项目零错误
- [ ] **Step 9**: Commit `fix(payment): 对账服务分页查询，PageSize=500 循环避免一次性拉取 10000 条`

---

## Task 7: gRPC 防腐层 Polly 集成

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionPollyExtensions.cs`（新增 `AddLenoGrpcAntiCorruptionPolly` 方法 + gRPC retry 策略）
- Modify: `src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs`（构造函数注入 `IServiceProvider`，`ExecuteAsync` 内嵌 Polly retry）
- Modify: `src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/GrpcAntiCorruptionClientBaseTests.cs`（5 个新增测试）
- 可能 Modify: 派生类 `Grpc*AntiCorruptionClient.cs`（仅当 `GrpcAntiCorruptionClientBase` 当前未注入 `IServiceProvider`）

**Spec 参考：** §2.7

- [ ] **Step 1**: 阅读现有 `GrpcAntiCorruptionClientBase.ExecuteAsync`，确认当前无 Polly retry；确认构造函数是否已注入 `IServiceProvider`
- [ ] **Step 2**: 阅读现有 `AntiCorruptionPollyExtensions`，确认既有 HTTP Polly 策略注册模式
- [ ] **Step 3**: 写失败测试 `ExecuteAsync_TransientFailure_RetriesAndSucceeds`（mock execute 第 1/2 次抛 `RpcException(Unavailable)`，第 3 次成功；断言调用 3 次）
- [ ] **Step 4**: 运行测试确认失败（当前无重试，第 1 次失败即抛）
- [ ] **Step 5**: 修改 `AntiCorruptionPollyExtensions`：新增 `AddLenoGrpcAntiCorruptionPolly` 方法，注册 `Policy.Handle<RpcException>(IsTransientGrpcStatus).WaitAndRetryAsync(2, exp backoff)` 为 keyed singleton `"GrpcAntiCorruptionRetry"`
- [ ] **Step 6**: 修改 `GrpcAntiCorruptionClientBase` 构造函数：注入 `IServiceProvider`（若未注入）；`ExecuteAsync` 内 `retryPolicy.ExecuteAsync(() => execute(ct))`
- [ ] **Step 7**: 若派生类构造函数需同步更新，逐一修改（最小化改动）
- [ ] **Step 8**: 在使用 `AddLenoAntiCorruption` 的 Api 服务 `Program.cs` 中追加 `AddLenoGrpcAntiCorruptionPolly` 调用
- [ ] **Step 9**: 运行测试确认通过
- [ ] **Step 10**: 补充测试 `ExecuteAsync_NonTransientFailure_DoesNotRetry` + `ExecuteAsync_AllRetriesFail_ThrowsAntiCorruptionException` + `ExecuteAsync_DomainException_DoesNotRetry` + `ExecuteAsync_CancellationToken_DoesNotRetry`
- [ ] **Step 11**: `dotnet build Leno.slnx -c Release` 零错误
- [ ] **Step 12**: Commit `feat(infrastructure): gRPC 防腐层集成 Polly retry，临时性故障自动重试 2 次`

---

## Task 8: 全量构建与验收

- [ ] **Step 1**: `dotnet build Leno.slnx -c Release` 零错误
- [ ] **Step 2**: `dotnet test Leno.slnx --filter "FullyQualifiedName!~ContainerFixture"` 全部通过（排除 Testcontainers 依赖测试）
- [ ] **Step 3**: `bash scripts/check-placeholders.sh` 通过（无新占位符）
- [ ] **Step 4**: 推送到远程 `git push origin trae/agent-LjCAhF`

---

## Self-Review

**1. Spec coverage：**
- ✅ §2.1 WeChatPay → Task 1
- ✅ §2.2 Alipay → Task 2
- ✅ §2.3 async void → Task 3
- ✅ §2.4 fire-and-forget → Task 4
- ✅ §2.5 Redis 事务 → Task 5
- ✅ §2.6 对账分页 → Task 6
- ✅ §2.7 gRPC Polly → Task 7
- ✅ §5.2 验收标准 → Task 8
- ✅ §5.3 实施顺序 → Task 1-7 顺序一致

**2. Placeholder scan：**
- ✅ 无 TBD/TODO/"implement later"
- ✅ 每个 Step 都有具体动作 + 文件路径 + Spec 章节引用
- ✅ 测试步骤引用具体测试方法名

**3. Type consistency：**
- ✅ `SignatureVerificationResult.Failure` 在 Task 1/2 一致
- ✅ `AntiCorruptionException` 在 Task 7 一致
- ✅ `ILogger?` 可选参数在 Task 2 一致
- ✅ `IServiceProvider` 注入在 Task 7 一致
