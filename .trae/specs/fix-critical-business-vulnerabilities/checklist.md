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
- [ ] `IPromotionAntiCorruptionService` 含 `LockCouponAsync` 接口与实现
- [ ] Promotion BC `internal/promotions/lock-coupon` 端点调用 `UserCoupon.Lock(orderId)`
- [ ] `OrderAppService.CreateOrderAsync` 下单时锁定选定券，失败回滚
- [ ] 历史数据迁移脚本存在且可执行
- [ ] 测试覆盖：下单锁定、并发锁定互斥、支付成功核销、取消释放全链路
- [ ] 同一优惠券无法被两个并发订单同时使用

### Task 4: 优惠券领取并发安全
- [ ] `UserCouponConfiguration` 含 `(UserId, CouponId)` 唯一索引
- [ ] EF Core migration 已生成
- [ ] `CouponAppService.ReceiveAsync` 捕获唯一约束冲突返回"已领取"
- [ ] 并发领取测试证明仅一个成功

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
- [ ] `OrderSagaOrchestrator` 存在并记录每组状态与补偿动作
- [ ] 第二组失败时第一组库存/积分/券/订单被补偿回滚
- [ ] 集成测试覆盖第二组失败回滚第一组场景

### Task 8: 单组下单库存/积分原子回滚
- [ ] `FreezeAsync` 失败时 `ReleaseBatchAsync` 释放已预占库存
- [ ] 测试覆盖积分冻结失败释放库存场景

### Task 9: PayAsync 事件发布原子化
- [ ] `Order` 聚合含"已发起支付"状态/标记与 `MarkPaymentInitiated`
- [ ] `PayAsync` 通过 Outbox 同事务发布 `PaymentRequestedIntegrationEvent`
- [ ] 重复发起返回"已发起支付"不重复发布
- [ ] 测试覆盖重复发起与正常发起

## P0 批次五：防腐层显式错误传播

### Task 10: 积分防腐层显式异常
- [ ] `PointsAntiCorruptionService.Freeze/Confirm/Release` 远程失败抛 `OrderDomainException`，不再吞异常
- [ ] 调用处按 spec 回滚/补偿
- [ ] 测试覆盖远程失败抛异常

### Task 11: 优惠券释放与促销计算显式失败
- [ ] `ReleaseCouponsAsync` 失败抛异常或写补偿表
- [ ] `CalculateDiscountAsync` 失败显式失败，不再按 0 优惠兜底
- [ ] 测试覆盖释放失败/计算失败

### Task 12: CartPriceService 失败处理
- [x] `CartPriceService` 失败不再静默返回空集合（改抛 `CartDomainException`）
- [x] 价格不可用标记"价格加载失败"（`PriceUnavailable=true`）并禁用结算，不展示 0 元
- [x] 测试覆盖价格加载失败

## P0 批次六：幂等性与 Outbox

### Task 13: Outbox 两阶段标记防重复发布
- [ ] `OutboxMessage` 状态枚举含 `Publishing`
- [ ] `OutboxPublisher` 两阶段：事务内置 `Publishing` 提交 → 发布 → `Processed` 提交
- [ ] 重启扫描 `Publishing` 超时消息
- [ ] 测试覆盖发布成功标记失败、发布失败重试

### Task 14: 消费者幂等强制
- [ ] `IIdempotencyStore` 接口与 `RedisIdempotencyStore` 实现存在
- [ ] `IntegrationEventConsumerBase` 幂等方法为 abstract，双基类已合并
- [ ] 全量 Consumer 子类已审计并注入 `IIdempotencyStore`
- [ ] 测试覆盖重复事件去重

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
- [ ] 防腐层远程失败记录 Prometheus 指标 `anticorruption_failure_total{service,operation}` 与结构化日志

### Task 18: 批量库存预占回滚补偿表
- [ ] 回滚失败记入补偿表，后台任务定期重试

### Task 19: 支付回调 Redis 故障降级
- [ ] `MarkCallbackProcessedAsync` Redis 故障返回 FAIL，不再 fail-open

### Task 20: 死信积压告警
- [ ] `DeadLetterMonitorBackgroundService` 每 5 分钟扫描，超阈值告警

### Task 21: 缓存失效健壮性
- [ ] `CacheInvalidationSubscriber` 监听 Redis 断连事件自动重连
- [ ] 缓存失效采用双删模式

### Task 22: Outbox 性能与可观测性
- [ ] `OutboxPublisher` 并行处理（`Parallel.ForEachAsync`）
- [ ] pending 数量超阈值告警
- [ ] 类型解析使用 FullName + `IOutboxEventTypeResolver`，兼容版本升级

## P2 批次十：收尾与文档

### Task 23: 缓存 Pattern 失效性能优化
- [ ] `InvalidatePatternAsync` 使用 `UNLINK` + 分批 SCAN

### Task 24: 测试占位收尾
- [ ] `NewFeatureTests.cs` 空文件已删除；`NewFeatureTests1-6.cs` 已重命名
- [ ] ReviewAfterSales / SellerShop / SystemAdmin 三个 `Application.Tests` 含关键测试
- [ ] CI `.github/workflows/ci.yml` 调用 `scripts/check-placeholders.sh`，违反阻止合并

### Task 25: 文档同步
- [ ] `docs/编码规范.md` 含本次安全/业务正确性约定（支付金额校验、优惠券锁流程、InternalApiKey 安全默认、Outbox 两阶段、防腐层显式错误传播、幂等强制）

## 全局验收

- [ ] 既有测试全绿（破坏性变更未引入回归）
- [ ] 每个 P0 修复含攻击场景/失败场景测试
- [ ] 破坏性变更（支付金额校验、fail-closed）灰度方案已评估
- [ ] 与既有架构方案无重复实施（BC 边界、共享内核、gRPC 等不归本 spec）
