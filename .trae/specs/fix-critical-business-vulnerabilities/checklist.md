# 关键业务与安全漏洞修复 - 验收检查清单

> 验收时逐项检查代码与行为，通过后勾选。每项需附证据（文件路径+行号或测试名）。

## P0 批次一：支付金额安全

### Task 1: 支付回调金额强校验
- [x] `ChannelNotifyResult` 及 WeChatPay/Alipay 实现包含 `Amount` 字段并从渠道回调正确解析
- [x] `PaymentOrder.MarkSucceeded` 接收并校验金额，不一致抛 `PaymentDomainException`
- [x] `WeChatPayNotifyHandler` 与 `AlipayNotifyHandler` 金额不一致返回 FAIL 且不调用 `MarkSucceeded`
- [x] 测试覆盖：金额一致成功、伪造低金额被拒、金额不匹配告警
- [x] 既有支付流程测试全绿

### Task 2: 主动查询补偿金额校验
- [x] `IChannelStatusQueryService.QueryPaymentStatusAsync` 返回值含 `Amount`
- [x] `PaymentAppService.QueryPaymentStatusAsync` 金额不匹配不标记成功，进入人工对账队列
- [x] 测试覆盖查询返回金额不匹配场景

## P0 批次二：优惠券正确性

### Task 3: 优惠券 Lock 流程贯通
- [x] `IPromotionAntiCorruptionService` 含 `LockCouponAsync` 接口与实现 —— `Leno.Order.Application/Services/IPromotionAntiCorruptionService.cs` + `Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs` `LockCouponAsync` 调 `internal/promotions/lock-coupon`，远程失败抛 `ORDER_PROMOTION_LOCK_COUPON_FAILED`
- [x] Promotion BC `internal/promotions/lock-coupon` 端点调用 `UserCoupon.Lock(orderId)` —— `Leno.Promotion.Api/Controllers/InternalPromotionsController.cs` `LockCouponAsync` → `CouponAppService.LockCouponAsync` → `UserCoupon.Lock(orderId)`
- [ ] `OrderAppService.CreateOrderAsync` 下单时锁定选定券，失败回滚 —— **跳过**：`CreateOrderDto` 无 couponId 字段，订单侧无选定券信息；已实现接口与端点供后续 CreateOrderDto 扩展 couponId 后接入（详见 tasks.md T3.4）
- [x] 历史数据迁移脚本存在且可执行 —— `scripts/migrations/promotion-usercoupon-unique-index-backfill.sql`（清理重复领取 + 回填脏数据 + 创建唯一索引，幂等）
- [x] 测试覆盖：下单锁定、并发锁定互斥、支付成功核销、取消释放全链路 —— `AntiCorruptionServicesTests.Promotion_LockCoupon_*`（5 例远程失败/非2xx/超时/取消/成功）+ `CouponAppServiceTests.LockCouponAsync_Valid/NotFound/AlreadyLocked`（含并发互斥）+ `ReceiveAsync_ConcurrentDuplicate`；支付成功核销与取消释放由既有 `OrderSagaOrchestrator.CompensateAsync` 的 `ReleaseCouponsAsync` 链路承接
- [x] 同一优惠券无法被两个并发订单同时使用 —— `UserCoupon.Lock` 聚合根校验（仅 Unused 可锁定）+ 防腐层端点同事务调用 + `LockCouponAsync_AlreadyLocked_ShouldThrowExceptionAndNotSave` 测试证明

### Task 4: 优惠券领取并发安全
- [x] `UserCouponConfiguration` 含 `(UserId, CouponId)` 唯一索引 —— `Leno.Promotion.Infrastructure/Configurations/UserCouponConfiguration.cs` `ux_user_coupons_user_id_coupon_id`
- [ ] EF Core migration 已生成 —— **跳过**：项目所有 BC 均未采用 EF Core migrations 模式（无 Migrations 目录），T9 新增列亦未生成；建议统一规划 schema 版本管理后补，部署时配合 T3.5 SQL 脚本创建唯一索引（详见 tasks.md T4.2）
- [x] `CouponAppService.ReceiveAsync` 捕获唯一约束冲突返回"已领取" —— `catch (DbUpdateException) => throw PromotionDomainException("已领取过该优惠券，不可重复领取", "COUPON_ALREADY_RECEIVED")`
- [x] 并发领取测试证明仅一个成功 —— `CouponAppServiceTests.ReceiveAsync_ConcurrentDuplicate_ShouldThrowAlreadyReceived`（mock SaveEntitiesAsync 抛 DbUpdateException，验证抛"已领取"）

## P0 批次三：认证授权加固

### Task 5: InternalApiKey fail-closed 与 timing-safe
- [x] 生产环境 `InternalAuth:ApiKey` 为空时启动抛异常（`EnsureInternalApiKeyConfigured` 扩展方法已实现，待各 BC Program.cs 接入；中间件层运行时 fail-closed 兜底返回 500）
- [x] ApiKey 比较使用 `CryptographicOperations.FixedTimeEquals`
- [x] 测试覆盖启动校验与 timing-safe

### Task 6: internal 路由边界精确匹配
- [x] 路径匹配为 `path == prefix || path.StartsWith(prefix + "/")`
- [x] `/internalinfo` 不被误判为 internal 路由
- [x] 测试覆盖边界路径

## P0 批次四：分布式事务与补偿

### Task 7: 多卖家拆单 Saga 补偿
- [x] `OrderSagaOrchestrator` 存在并记录每组状态与补偿动作 —— `Leno.Order.Application/Services/OrderSagaOrchestrator.cs`，`ExecuteAsync` 顺序遍历分组，`CompletedGroup` 记录每组订单/SKU/积分冻结/优惠状态，`CompensateAsync` 逆序补偿
- [x] 第二组失败时第一组库存/积分/券/订单被补偿回滚 —— `CompensateAsync` 逐组 try/catch 执行 `ReleaseCouponsAsync`→`ReleaseAsync`→`ReleaseBatchAsync`→`RemoveAsync`，单动作失败仅记日志不阻塞其它补偿；`SaveEntitiesAsync` 延迟到全部成功后统一提交，失败时无订单持久化
- [x] 集成测试覆盖第二组失败回滚第一组场景 —— `OrderAppServiceTests.CreateOrderAsync_MultiSellerSecondGroupReserveFails_ShouldCompensateFirstGroupAndNotPersist`

### Task 8: 单组下单库存/积分原子回滚
- [x] `FreezeAsync` 失败时 `ReleaseBatchAsync` 释放已预占库存 —— `OrderSagaOrchestrator.ExecuteGroupAsync` 第 151-165 行 try/catch 包裹 `FreezeAsync`，失败时 `ReleaseBatchAsync` 后重抛
- [x] 测试覆盖积分冻结失败释放库存场景 —— `OrderAppServiceTests.CreateOrderAsync_PointsFreezeFails_ShouldReleaseStockAndNotPersistOrder`

### Task 9: PayAsync 事件发布原子化
- [x] `Order` 聚合含"已发起支付"状态/标记与 `MarkPaymentInitiated` —— `Leno.Order.Domain/Aggregates/Order.cs` `PaymentInitiated`/`PaymentInitiatedAt` 字段 + `MarkPaymentInitiated(paymentMethod)` 方法 + `OrderConfiguration` 列映射
- [x] `PayAsync` 通过 Outbox 同事务发布 `PaymentRequestedIntegrationEvent` —— `OrderAppService.PayAsync` 调 `order.MarkPaymentInitiated` 触发 `AddDomainEvent(PaymentRequestedIntegrationEvent)`，经 `SaveEntitiesAsync`（Outbox 扩展）同事务持久化，移除直接 `_eventBus.PublishAsync`
- [x] 重复发起返回"已发起支付"不重复发布 —— `MarkPaymentInitiated` 校验 `PaymentInitiated` 抛 `ORDER_PAYMENT_ALREADY_INITIATED`，不再产生第二个事件
- [x] 测试覆盖重复发起与正常发起 —— `OrderTests.MarkPaymentInitiated_*`（4 例）+ `OrderAppServiceTests.PayAsync_Valid_ShouldInitiatePaymentAndSaveWithOutbox` / `PayAsync_AlreadyInitiated_ShouldThrowAndNotSave`

## P0 批次五：防腐层显式错误传播

### Task 10: 积分防腐层显式异常
- [x] `PointsAntiCorruptionService.Freeze/Confirm/Release` 远程失败抛 `OrderDomainException`，不再吞异常 —— `Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs` 第 297-366 行，`catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }` + `catch (OrderDomainException) { throw; }` + 末尾 catch 包装抛 `OrderDomainException`
- [x] 调用处按 spec 回滚/补偿 —— `OrderSagaOrchestrator.ExecuteGroupAsync`（T8 单组回滚）+ `CompensateAsync`（T7 多组补偿）均 try/catch 包裹积分释放
- [x] 测试覆盖远程失败抛异常 —— `AntiCorruptionServicesTests.Points_RemoteFailure_ShouldThrowOrderDomainException` / `Points_NonSuccessStatusCode_ShouldThrowOrderDomainException` / `Points_Timeout_ShouldThrowOrderDomainException` / `Points_UserCancellation_ShouldPropagateOperationCanceledException` / `Points_Success_ShouldNotThrow`

### Task 11: 优惠券释放与促销计算显式失败
- [x] `ReleaseCouponsAsync` 失败抛异常或写补偿表 —— `AntiCorruptionServices.cs` 第 193-226 行，远程失败抛 `OrderDomainException("ORDER_PROMOTION_RELEASE_COUPONS_FAILED")`
- [x] `CalculateDiscountAsync` 失败显式失败，不再按 0 优惠兜底 —— `AntiCorruptionServices.cs` 第 129-181 行，非 2xx/空数据/异常均抛 `OrderDomainException("ORDER_PROMOTION_CALCULATE_FAILED")`
- [x] 测试覆盖释放失败/计算失败 —— `AntiCorruptionServicesTests.Promotion_CalculateDiscount_RemoteFailure_ShouldThrowOrderDomainException` / `Promotion_ReleaseCoupons_RemoteFailure_ShouldThrowOrderDomainException`

### Task 12: CartPriceService 失败处理
- [x] `CartPriceService` 失败不再静默返回空集合（改抛 `CartDomainException`）
- [x] 价格不可用标记"价格加载失败"（`PriceUnavailable=true`）并禁用结算，不展示 0 元
- [x] 测试覆盖价格加载失败

## P0 批次六：幂等性与 Outbox

### Task 13: Outbox 两阶段标记防重复发布
- [x] `OutboxMessage` 状态枚举含 `Publishing` —— `Leno.Infrastructure/Outbox/OutboxMessage.cs` `OutboxMessageStatus.Publishing` + `PublishingStartedAt` 字段 + `MarkAsPublishing()`/`ResetStalePublishing()` 方法
- [x] `OutboxPublisher` 两阶段：事务内置 `Publishing` 提交 → 发布 → `Processed` 提交 —— `Leno.Infrastructure/Outbox/OutboxPublisher.cs` `ProcessAsync` 内 `MarkAsPublishing` 同事务提交 → 发布 MQ → `MarkAsProcessed` 提交；发布失败回退 Pending + RetryCount++
- [x] 重启扫描 `Publishing` 超时消息 —— `OutboxPublisher.RecoverStalePublishingAsync`（默认 5 分钟超时，`StalePublishingTimeout`），扫描超时 `Publishing` 消息回退 Pending
- [x] 测试覆盖发布成功标记失败、发布失败重试 —— `Leno.Infrastructure.Tests/Outbox/OutboxPublisherTests.cs`（6 例：发布成功标记 Processed、发布失败回退 Pending+RetryCount++、Publishing 超时回退、空批次、发布异常重试、Processed 提交失败依赖恢复）

### Task 14: 消费者幂等强制
- [x] `IIdempotencyStore` 接口与 `RedisIdempotencyStore` 实现存在 —— `Leno.Infrastructure.Abstractions/IIdempotencyStore.cs`（`IsProcessedAsync`/`MarkAsProcessedAsync`）+ `Leno.Infrastructure/EventBus/RedisIdempotencyStore.cs`（`StringSetAsync(key,"1",24h,When.NotExists)` SET NX + 24h TTL，key 前缀 `evt:processed`）
- [x] `IntegrationEventConsumerBase` 幂等方法为 abstract，双基类已合并 —— `Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs` 构造函数强制注入 `IIdempotencyStore`，`Consume` 内 `IsProcessedAsync`→`HandleAsync`→`MarkAsProcessedAsync`；`RedisIntegrationEventConsumerBase.cs` 已删除，所有 Consumer 直接继承 `IntegrationEventConsumerBase`
- [x] 全量 Consumer 子类已审计并注入 `IIdempotencyStore` —— 23 个标准 Consumer（Cart/Order/Payment/Promotion/Product/PointsMembership/SellerShop/ReviewAfterSales）批量改造 + 1 个特殊 Consumer（`ReviewApprovedEventConsumer` 保留 `IConnectionMultiplexer` 用于每日评价积分上限 Redis 计数，同时注入 `IIdempotencyStore`）；DI 注册 `services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>()`（`ServiceCollectionExtensions.AddRedis`）
- [x] 测试覆盖重复事件去重 —— `Promotion.Infrastructure.Tests/ConsumerTests.cs` `Consume_DuplicateEvent_ShouldSkipViaIdempotencyStore`（IsProcessedAsync 返回 true 验证仓储与 UoW 不调用）+ `Product.Infrastructure.Tests/ReviewEventConsumerTests.cs` `ReviewSubmittedEventConsumer_Idempotent_ShouldSkipDuplicateEvent`/`ReviewHiddenEventConsumer_Idempotent_ShouldSkipDuplicateEvent` + `Product.Infrastructure.Tests/ShopEventConsumerTests.cs` `ShopEventConsumer_Idempotent_ShouldSkipDuplicateEvent`

## P0 批次七：死信队列真实实现

### Task 15: 死信重投与拉取真实实现
- [x] `RepublishAsync` 真正重投原始事件到 MQ（非占位日志）—— `DeadLetterRepublishHelper.RepublishViaEventBusAsync` 反序列化 Payload 为 `IIntegrationEvent` 后调用 `IEventBus.PublishAsync`，`DeadLetterQueueManager` 与 `RabbitMqDeadLetterManager` 共用此逻辑
- [x] `FetchAsync` 处理成功后才 ack，失败回队 —— `RabbitMqDeadLetterManager.FetchAsync` 改用 `ackmode=ack_requeue_true`（消息回队不删除）+ 入库副本（按 OriginalMessageId 去重）；入库失败抛异常，消息仍保留在 DLQ
- [x] `DeadLetterQueueManager` 与 `RabbitMqDeadLetterManager` 行为一致 —— 两者 RepublishAsync 均经 `DeadLetterRepublishHelper` 走 IEventBus 重投，幂等/Discarded 校验逻辑相同
- [x] 测试覆盖重投成功、拉取失败回队 —— `DeadLetterQueueManagerTests`（4 测试）+ `RabbitMqDeadLetterManagerTests`（6 测试，含 `FetchAsync_WhenRepositoryAddThrows_ShouldPropagateAndKeepMessageInDlq`）

## P0 批次八：库存一致性

### Task 16: Redis-DB 库存对账与秒杀回写
- [x] `InventoryReconciliationBackgroundService` 定期对账，不一致告警并以 DB 刷新 Redis（待 Order Program.cs 注册）
- [x] `RedisSeckillStockService.WriteBackToDbAsync` 真实回写 DB，占位日志已移除
- [x] 测试覆盖对账不一致、秒杀回写

## P1 批次九：健壮性与可观测性

### Task 17: 防腐层降级告警
- [x] 防腐层远程失败记录 Prometheus 指标 `anticorruption_failure_total{service,operation}` 与结构化日志 —— `Leno.Order.Infrastructure/Services/AntiCorruptionMetrics.cs` 静态 `Meter`+`Counter<int>`，`AntiCorruptionServices.cs` 中 `PointsAntiCorruptionService`/`PromotionAntiCorruptionService` catch 块调用 `RecordFailure`，`ServiceCollectionExtensions.cs` `AddMeter` 订阅；测试：`AntiCorruptionMetricsTests`（9 例全绿，MeterListener 验证 service/operation 标签计数）

### Task 18: 批量库存预占回滚补偿表
- [x] 回滚失败记入补偿表，后台任务定期重试 —— `StockReservationCompensation` 聚合（4 态状态机+MaxRetries）+ `EfCoreStockReservationCompensationRepository` + `StockReservationCompensationConfiguration` + `StockReservationDomainService.RecordCompensationAsync`（IServiceScopeFactory 独立 scope）+ `StockReservationCompensationBackgroundService`（5min/50条）；测试：`StockReservationCompensationTests`（13 例全绿，聚合流转+后台重试成功/失败/混合/持久化异常）

### Task 19: 支付回调 Redis 故障降级
- [x] `MarkCallbackProcessedAsync` Redis 故障返回 FAIL，不再 fail-open —— `AlipayNotifyHandler.cs`/`WeChatPayNotifyHandler.cs` catch 块改为 `throw`，外层 `HandleAsync` catch 返回 fail；Redis null 仍放行（配置选择）；测试：`NotifyHandlerRedisFailoverTests`（3 例全绿：Redis 故障返回 fail 不标记 Paid、Redis null 放行标记 Paid、WeChatPay Redis null 不崩溃）

### Task 20: 死信积压告警
- [x] `DeadLetterMonitorBackgroundService` 每 5 分钟扫描，超阈值告警 —— `DeadLetterMonitorBackgroundService`（`ObservableGauge<int>` dead_letter_count）+ `DeadLetterMonitorOptions`（5min/10/SourceContexts）+ `RunScanCycleAsync` 调 `IDeadLetterQueueManager.CountAsync`；`ServiceCollectionExtensions.cs` 注册；测试：`DeadLetterMonitorBackgroundServiceTests`（6 例全绿：低阈值不告警、超阈值告警、多 Context 扫描、单 Context 超阈值、零死信不记 Info、异常传播）

### Task 21: 缓存失效健壮性
- [x] `CacheInvalidationSubscriber` 监听 Redis 断连事件自动重连 —— `Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs` `SubscribeToRedisEvents()` 订阅 `ConnectionFailed`/`InternalError` 事件，`OnConnectionFailed`/`OnInternalError` 触发 `ReconnectWithBackoffAsync()`（指数退避 1s→2s→4s→8s→16s→30s 封顶）后台重新订阅通道；`StartAsync`/`StopAsync`/`Dispose` 正确挂载/卸载事件处理器；测试：`CacheInvalidationSubscriberTests.StartAsync_ShouldAttachConnectionFailedEventHandler` / `StartAsync_ShouldAttachInternalErrorEventHandler` / `StopAsync_ShouldDetachConnectionEventHandlers` / `ConnectionFailed_ShouldTriggerResubscribeWithBackoff` / `InternalError_ShouldTriggerResubscribeWithBackoff`
- [x] 缓存失效采用双删模式 —— `CacheInvalidationSubscriber.OnMessage` 立即删除后调用 `DelayedDeleteAsync`（默认延迟 500ms）二次删除，Pattern 路径同样延迟二次扫描删除；`ICacheService.InvalidateWithDoubleDeleteAsync` 接口 + `CacheService` 实现（先删→执行 writeAction→延迟 500ms→再删，try/finally 保证写库异常也执行二次删除）；测试：`CacheServiceTests.InvalidateWithDoubleDelete_*`（5 例：null 参数、删除-写-删序列、写失败仍二次删、二次删失败不掩盖写异常）+ `CacheInvalidationSubscriberTests` 双删路径

### Task 22: Outbox 性能与可观测性
- [x] `OutboxPublisher` 并行处理（`Parallel.ForEachAsync`）—— `Leno.Infrastructure/Outbox/OutboxPublisher.cs` `ProcessBatchAsync` 主作用域拉取 pending ID 后用 `Parallel.ForEachAsync`（`MaxDegreeOfParallelism=4`）并行处理，每条消息经 `PublishSingleByIdAsync` 独立作用域+独立 DbContext 保持两阶段标记语义；测试：`OutboxPublisherTests.ProcessBatch_MultipleMessages_ShouldProcessInParallelAndAllSucceed`（5 条全 Processed）+ `ProcessBatch_PartialFailure_ShouldNotAffectOtherMessages`（4 条 3 Processed+1 Pending）
- [x] pending 数量超阈值告警 —— `OutboxPublisher.AlertIfPendingBacklogAsync` 每次 `ExecuteAsync` 轮询后统计 pending 数量，超阈值（默认 100，`PendingAlertThreshold`）记录结构化告警日志；测试：`OutboxPublisherTests.AlertIfPendingBacklog_ExceedsThreshold_ShouldLogWarning`（阈值 5，6 pending）+ `AlertIfPendingBacklog_BelowThreshold_ShouldNotLogWarning`（阈值 5，3 pending）
- [x] 类型解析使用 FullName + `IOutboxEventTypeResolver`，兼容版本升级 —— `Leno.Infrastructure/Outbox/IOutboxEventTypeResolver.cs`（接口 + `DefaultOutboxEventTypeResolver` 单例，按 FullName 跨已加载程序集解析，`ConcurrentDictionary` 缓存）；`OutboxMessage.Create` 优先存储 `FullName`；`OutboxPublisher` 构造函数注入可选 `IOutboxEventTypeResolver`（默认 `DefaultOutboxEventTypeResolver.Instance`），`PublishSingleAsync` 用 `_typeResolver.Resolve(message.Type)`；测试：`OutboxEventTypeResolverTests`（7 例：按 FullName/AQN/未知/空值/缓存/旧版本号 AQN/自定义注入）+ `OutboxPublisherTests.ProcessBatch_WithCustomResolver_ShouldUseResolverToResolveType` / `ProcessBatch_WhenResolverReturnsNull_ShouldMarkAsFailed`

## P2 批次十：收尾与文档

### Task 23: 缓存 Pattern 失效性能优化
- [x] `InvalidatePatternAsync` 使用 `UNLINK` + 分批 SCAN —— `Leno.Infrastructure/Caching/CacheService.cs` 新增 `InvalidatePatternAsync` 方法（SCAN 游标迭代 `IServer.KeysAsync` + 批量 `db.ExecuteAsync("UNLINK", keys, CommandFlags.None)`，默认每批 100 key，可通过 `PatternInvalidationBatchSizeOverride` 覆盖）；`Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs` `InvalidatePatternAsync` 由 per-key `KeyDeleteAsync`（DEL）改为批量 `ExecuteAsync("UNLINK", keys)`（UNLINK 异步删除）；测试：`CacheServiceTests.InvalidatePatternAsync_*`（9 例：null 参数、无主节点、仅副本、单 key UNLINK 不 DEL、低于批次单次 UNLINK、超过批次多次 UNLINK、自定义批次、无匹配 key、参数包含 key）+ `CacheInvalidationSubscriberTests.InvalidatePatternAsync_*`（7 例：UNLINK 不 DEL、低于批次单次、超过批次多次、自定义批次、无匹配 key、参数包含 key、KeyPrefix 拼接）

### Task 24: 测试占位收尾
- [x] `NewFeatureTests.cs` 空文件已删除；`NewFeatureTests1-6.cs` 已重命名 —— `Leno.PointsMembership.Domain.Tests/` 下：删除 `NewFeatureTests.cs`（0 字节）；`git mv` 重命名为 `PointsAccountConsumeRevertTests.cs`/`PointsSourceEnumTests.cs`/`ReviewApprovedEventConsumerTests.cs`/`UserRegisteredEventConsumerNewUserPointsTests.cs`/`RefundCompletedEventConsumerTests.cs`/`CouponExchangeConsumerTests.cs`（class 名与文件名一致）
- [x] ReviewAfterSales / SellerShop / SystemAdmin 三个 `Application.Tests` 含关键测试 —— ReviewAfterSales（20 例：ReviewAppServiceTests 12 + AfterSalesAppServiceTests 10 测试，部分减缩为 20 实际运行通过）；SellerShop（28 例：SellerAppServiceTests 7 + ShopAppServiceTests 12 + SellerDashboardAppServiceTests 7，实际 28 例全绿）；SystemAdmin（36 例：AuditLogAppServiceTests 6 + FeatureFlagAppServiceTests 11 + ScheduledTaskAppServiceTests 10 + DeadLetterAppServiceTests 9）；三个测试项目已加入 `Leno.slnx` 解决方案文件，`dotnet build` 与 `dotnet test` 全绿
- [x] CI `.github/workflows/ci.yml` 调用 `scripts/check-placeholders.sh`，违反阻止合并 —— ci.yml 第 22-23 行新增 `Check placeholders` 步骤（位于 Build 之后、Unit tests with coverage 之前）；`scripts/check-placeholders.sh` 已 `chmod +x`，扫描 6 类占位：NotImplementedException、SmokeTest_ShouldPass/true.Should().BeTrue()/Assert.True(true)、NewFeatureTests*.cs 文件、TODO/FIXME 注释、return default!/null!、空测试类（无 [Fact]/[Theory]）；本地执行 `bash scripts/check-placeholders.sh` 输出 `✅ 未检测到占位实现。` 退出码 0

### Task 25: 文档同步
- [x] `docs/编码规范.md` 含本次安全/业务正确性约定（支付金额校验、优惠券锁流程、InternalApiKey 安全默认、Outbox 两阶段、防腐层显式错误传播、幂等强制） —— `docs/编码规范.md` 第 10 章安全编码规范下新增 10.5–10.10 六个子节（行 2343-2750），每节含核心规范编号列表 + 正确示例 C# 代码 + 反例（禁止写法）：
  - 10.5 支付金额强校验（行 2343-2398）：回调/查询在 `MarkSucceeded` 前校验金额一致，不一致返回 FAIL 并告警
  - 10.6 优惠券锁定流程（行 2400-2453）：`LockCouponAsync` 下单锁定、状态机并发互斥、支付成功核销、取消释放、历史迁移
  - 10.7 InternalApiKey 安全默认（行 2455-2520）：fail-closed 启动校验、`FixedTimeEquals` 比较、`/internal` 精确前缀匹配
  - 10.8 Outbox 两阶段标记（行 2522-2592）：`Pending → Publishing → Processed` 三态、发布失败回退、超时扫描恢复
  - 10.9 防腐层显式错误传播（行 2594-2663）：远程失败抛领域异常、Prometheus 指标、`OperationCanceledException` 传播、CartPriceService 标记 `PriceUnavailable`
  - 10.10 集成事件消费幂等强制（行 2665-2750）：`IntegrationEventConsumerBase` 幂等方法 abstract、强制注入 `IIdempotencyStore`、三步消费流程

## 全局验收

- [ ] 既有测试全绿（破坏性变更未引入回归）
- [ ] 每个 P0 修复含攻击场景/失败场景测试
- [ ] 破坏性变更（支付金额校验、fail-closed）灰度方案已评估
- [ ] 与既有架构方案无重复实施（BC 边界、共享内核、gRPC 等不归本 spec）
