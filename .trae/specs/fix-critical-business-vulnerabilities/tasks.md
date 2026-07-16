# Tasks

> 实施顺序：P0 批次（T1-T13）→ P1 批次（T14-T19）→ P2 批次（T20-T23）。每批可独立验证、独立回滚。每个任务先补测试再改实现。

## P0 批次一：支付金额安全

- [ ] Task 1: 支付回调金额强校验
  - [ ] SubTask 1.1: `ChannelNotifyResult`（及对应 WeChatPay/Alipay 实现）新增 `Amount` 字段，从渠道回调解析实付金额
  - [ ] SubTask 1.2: `PaymentOrder.MarkSucceeded` 新增 `amount` 入参，内部校验 `amount == Amount` 不一致抛 `PaymentDomainException`
  - [ ] SubTask 1.3: `WeChatPayNotifyHandler.HandlePaymentNotifyAsync` 与 `AlipayNotifyHandler.HandlePaymentNotifyAsync` 在 `MarkSucceeded` 前校验金额，不一致记录安全告警并返回 FAIL
  - [ ] SubTask 1.4: 补单元测试覆盖金额一致/不一致/伪造低金额场景
- [ ] Task 2: 主动查询补偿金额校验
  - [ ] SubTask 2.1: `IChannelStatusQueryService.QueryPaymentStatusAsync` 返回值新增 `Amount` 字段
  - [ ] SubTask 2.2: `PaymentAppService.QueryPaymentStatusAsync` 在 `MarkSucceeded` 前校验金额，不一致记录告警并进入人工对账队列
  - [ ] SubTask 2.3: 补测试覆盖查询返回金额不匹配场景

## P0 批次二：优惠券正确性

- [ ] Task 3: 优惠券 Lock 流程贯通
  - [ ] SubTask 3.1: `IPromotionAntiCorruptionService` 新增 `LockCouponAsync(userId, couponId, orderId)` 接口方法
  - [ ] SubTask 3.2: `PromotionAntiCorruptionService`（Order.Infrastructure）实现 `LockCouponAsync`，调用 Promotion BC 新增 `internal/promotions/lock-coupon` 端点
  - [ ] SubTask 3.3: Promotion BC 新增 `internal/promotions/lock-coupon` 端点，调用 `UserCoupon.Lock(orderId)`
  - [ ] SubTask 3.4: `OrderAppService.CreateOrderAsync` 在 `CalculateDiscountAsync` 后立即对选定券调用 `LockCouponAsync`，失败抛领域异常并回滚
  - [ ] SubTask 3.5: 编写历史数据迁移脚本：扫描 `Unused` 但已关联订单的券，回填 `LockedOrderId`（如有）
  - [ ] SubTask 3.6: 补测试覆盖锁定/并发锁定/支付成功核销/取消释放全链路
- [ ] Task 4: 优惠券领取并发安全
  - [ ] SubTask 4.1: `UserCouponConfiguration` 新增 `HasIndex(u => new { u.UserId, u.CouponId }).IsUnique()`
  - [ ] SubTask 4.2: 生成 EF Core migration
  - [ ] SubTask 4.3: `CouponAppService.ReceiveAsync` 捕获唯一约束冲突返回"已领取"
  - [ ] SubTask 4.4: 补并发领取测试

## P0 批次三：认证授权加固

- [ ] Task 5: InternalApiKey fail-closed 与 timing-safe
  - [ ] SubTask 5.1: `InternalApiKeyMiddleware` 改为生产环境（`!hostEnvironment.IsDevelopment()`）ApiKey 为空时启动抛异常
  - [ ] SubTask 5.2: ApiKey 比较改用 `CryptographicOperations.FixedTimeEquals`
  - [ ] SubTask 5.3: 补启动校验测试与 timing-safe 测试
- [ ] Task 6: internal 路由边界精确匹配
  - [ ] SubTask 6.1: `InternalApiKeyMiddleware` 路径匹配改为 `path == prefix || path.StartsWith(prefix + "/")`
  - [ ] SubTask 6.2: 补测试覆盖 `/internalinfo`、`/internal/foo` 等边界

## P0 批次四：分布式事务与补偿

- [ ] Task 7: 多卖家拆单 Saga 补偿
  - [ ] SubTask 7.1: 新建 `OrderSagaOrchestrator`（Order.Application），记录每组执行状态与补偿动作
  - [ ] SubTask 7.2: 任一组失败时对已成功组执行补偿：`ReleaseBatchAsync`（库存）、`ReleaseAsync`（积分）、`ReleaseCouponsAsync`（券）、取消/删除已保存订单
  - [ ] SubTask 7.3: `OrderAppService.CreateOrderAsync` 接入 Saga 编排
  - [ ] SubTask 7.4: 补集成测试覆盖第二组失败回滚第一组场景
- [ ] Task 8: 单组下单库存/积分原子回滚
  - [ ] SubTask 8.1: `CreateOrderAsync` 单组流程用 try/catch 包裹，`FreezeAsync` 失败时调用 `ReleaseBatchAsync` 释放库存再抛异常
  - [ ] SubTask 8.2: 补测试覆盖积分冻结失败释放库存场景
- [ ] Task 9: PayAsync 事件发布原子化
  - [ ] SubTask 9.1: `Order` 聚合新增 `MarkPaymentInitiated` 方法与"已发起支付"状态/标记
  - [ ] SubTask 9.2: `PayAsync` 改为聚合状态变更 + 领域事件 `PaymentRequestedIntegrationEvent`（经 Outbox 同事务发布），移除直接 `_eventBus.PublishAsync`
  - [ ] SubTask 9.3: 重复发起返回"已发起支付"不重复发布
  - [ ] SubTask 9.4: 补测试覆盖重复发起与正常发起

## P0 批次五：防腐层显式错误传播

- [ ] Task 10: 积分防腐层显式异常
  - [ ] SubTask 10.1: `PointsAntiCorruptionService.Freeze/Confirm/Release` 移除 try-catch 吞异常，远程失败抛 `OrderDomainException`
  - [ ] SubTask 10.2: `CreateOrderAsync`/`CancelOrderAsync` 调用处按 spec 处理（回滚/补偿表）
  - [ ] SubTask 10.3: 补测试覆盖远程失败抛异常
- [ ] Task 11: 优惠券释放与促销计算显式失败
  - [ ] SubTask 11.1: `PromotionAntiCorruptionService.ReleaseCouponsAsync` 失败抛异常或写"待释放券"补偿表
  - [ ] SubTask 11.2: `CalculateDiscountAsync` 失败返回显式 `TryCalcResult` 或抛异常，应用层不再按 0 优惠兜底
  - [ ] SubTask 11.3: 补测试覆盖释放失败/计算失败场景
- [ ] Task 12: CartPriceService 失败处理
  - [ ] SubTask 12.1: `CartPriceService` 失败不再返回空集合掩盖，改为抛异常或返回显式失败标记
  - [ ] SubTask 12.2: `CartAppService.BuildItemDto` 价格不可用时标记"价格加载失败"并禁用结算，不再展示 0 元
  - [ ] SubTask 12.3: 补测试覆盖价格加载失败场景

## P0 批次六：幂等性与 Outbox

- [ ] Task 13: Outbox 两阶段标记防重复发布
  - [ ] SubTask 13.1: `OutboxMessage` 状态枚举新增 `Publishing` 中间态
  - [ ] SubTask 13.2: `OutboxPublisher` 改两阶段：事务内置 `Publishing` 提交 → 发布 MQ → 置 `Processed` 提交
  - [ ] SubTask 13.3: 重启扫描 `Publishing` 超时（默认 5 分钟）消息，依赖下游幂等兜底
  - [ ] SubTask 13.4: 补测试覆盖发布成功标记失败、发布失败重试场景
- [ ] Task 14: 消费者幂等强制
  - [ ] SubTask 14.1: 新建 `IIdempotencyStore` 接口与 `RedisIdempotencyStore`（SET NX + 24h TTL）实现
  - [ ] SubTask 14.2: `IntegrationEventConsumerBase` 幂等方法改 abstract，合并 `RedisIntegrationEventConsumerBase`（既有方案主线 3.4）
  - [ ] SubTask 14.3: 全量审计所有 Consumer 子类，未注入 `IIdempotencyStore` 者补齐
  - [ ] SubTask 14.4: 补测试覆盖重复事件去重

## P0 批次七：死信队列真实实现

- [ ] Task 15: 死信重投与拉取真实实现
  - [ ] SubTask 15.1: `RabbitMqDeadLetterManager.RepublishAsync` 通过 RabbitMQ Management API 或 `_eventBus.PublishAsync` 真正重投原始事件
  - [ ] SubTask 15.2: `FetchAsync` 改为 `ack_requeue_true`，本地处理成功后才 ack；合并 `DeadLetterQueueManager` 与 `RabbitMqDeadLetterManager` 行为一致
  - [ ] SubTask 15.3: 补测试覆盖重投成功、拉取处理失败回队场景

## P0 批次八：库存一致性

- [ ] Task 16: Redis-DB 库存对账与秒杀回写
  - [ ] SubTask 16.1: 新建 `InventoryReconciliationBackgroundService`（Order.Infrastructure），定期比较 Redis 库存与 DB 聚合，不一致告警并以 DB 为准刷新 Redis
  - [ ] SubTask 16.2: `RedisSeckillStockService.WriteBackToDbAsync` 真实调用 `SeckillActivity.SyncFromRedis` 回写 DB，移除占位日志
  - [ ] SubTask 16.3: 补测试覆盖对账不一致、秒杀回写场景

## P1 批次九：健壮性与可观测性

- [ ] Task 17: 防腐层降级告警
  - [ ] SubTask 17.1: 防腐层远程失败时记录 Prometheus 指标（`anticorruption_failure_total{service,operation}`）与结构化日志
- [ ] Task 18: 批量库存预占回滚补偿表
  - [ ] SubTask 18.1: `StockReservationDomainService.ReserveBatchAsync` 回滚失败记入补偿表，后台任务定期重试
- [ ] Task 19: 支付回调 Redis 故障降级
  - [ ] SubTask 19.1: `MarkCallbackProcessedAsync` Redis 故障时返回 FAIL 让渠道重试，不再 fail-open 放行
- [ ] Task 20: 死信积压告警
  - [ ] SubTask 20.1: 新建 `DeadLetterMonitorBackgroundService`，每 5 分钟扫描死信数量，超阈值（默认 10）触发告警
- [ ] Task 21: 缓存失效健壮性
  - [ ] SubTask 21.1: `CacheInvalidationSubscriber` 监听 Redis `ConnectionFailed`/`InternalError` 事件自动重连
  - [ ] SubTask 21.2: 缓存失效改双删模式（先删 → 写库 → 延迟 500ms 再删）
- [ ] Task 22: Outbox 性能与可观测性
  - [ ] SubTask 22.1: `OutboxPublisher` 改 `Parallel.ForEachAsync` 并行处理
  - [ ] SubTask 22.2: 每次轮询统计 pending 数量，超阈值告警
  - [ ] SubTask 22.3: 类型解析改用 FullName + 自定义 `IOutboxEventTypeResolver`，兼容 BC 版本升级

## P2 批次十：收尾与文档

- [ ] Task 23: 缓存 Pattern 失效性能优化
  - [ ] SubTask 23.1: `InvalidatePatternAsync` 改 `UNLINK` + 分批 SCAN
- [ ] Task 24: 测试占位收尾
  - [ ] SubTask 24.1: 删除 `NewFeatureTests.cs` 空文件；重命名 `NewFeatureTests1-6.cs` 为具名测试文件
  - [ ] SubTask 24.2: 补 ReviewAfterSales / SellerShop / SystemAdmin 三个空 `Application.Tests` 关键测试
  - [ ] SubTask 24.3: CI（`.github/workflows/ci.yml`）集成 `scripts/check-placeholders.sh`，违反即阻止合并
- [ ] Task 25: 文档同步
  - [ ] SubTask 25.1: `docs/编码规范.md` 新增本次安全/业务正确性约定（支付金额校验、优惠券锁流程、InternalApiKey 安全默认、Outbox 两阶段、防腐层显式错误传播、幂等强制）

# Task Dependencies

- Task 3（优惠券 Lock）独立于其他，可并行
- Task 7（多卖家 Saga）依赖 Task 10/11（防腐层显式异常），补偿动作依赖防腐层抛异常语义
- Task 8（单组回滚）依赖 Task 10（积分冻结抛异常）
- Task 9（PayAsync Outbox）独立
- Task 13（Outbox 两阶段）独立，但与 Task 14（消费者幂等）协同——Outbox 重复发布由下游幂等兜底
- Task 14 依赖既有方案主线 3.4 设计（本 spec 推进落地）
- Task 15（死信）独立
- Task 16（库存对账）独立
- Task 22（Outbox 并行）依赖 Task 13（两阶段标记）完成
- P1 批次（Task 17-22）依赖对应 P0 批次完成
- P2 批次（Task 23-25）收尾，依赖 P0/P1 完成

# 并行机会

P0 批次一/二/三/六/七/八（Task 1-6、13-16）相互独立，可并行实施。批次四/五（Task 7-12）有依赖关系需顺序推进。
