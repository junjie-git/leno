---
status: partially_superseded
partially_superseded_by:
  - docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md
partially_superseded_date: 2026-07-19
partially_superseded_reason: |
  本 spec 中鉴权集中化部分已被 V2 spec 的 F1.4 后续任务取代。
  以下章节仍有效：
  - 输入校验
  - SQL 注入防护
  - XSS 防护
  - CSRF 防护
---

# 关键业务与安全漏洞修复 Spec

**Change-ID**：`fix-critical-business-vulnerabilities`
**创建日期**：2026-07-17
**方案定位**：独立的安全/业务正确性修复 spec，与既有 `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md`（架构优化主线）并行。本 spec 聚焦架构方案**未覆盖**的业务逻辑漏洞、数据一致性风险、并发安全缺陷与认证授权问题；架构重构项（BC 边界、共享内核、样板去重、gRPC 迁移等）仍归既有方案治理，本 spec 仅在交叉点引用。

---

## Why

对 `/workspace` 仓库全面分析后发现：既有优化方案主要解决架构合规与代码质量问题，但**遗漏了一类更严重的问题——业务正确性与安全漏洞**。这些问题即使架构完美也会导致资金损失、数据错乱、越权访问：

- 支付回调不校验金额，0.01 元可购买 1000 元商品
- 优惠券 `Lock` 流程断裂，同一张券可被无限重复使用且永不核销
- `InternalApiKey` 未配置时 fail-open，生产环境忘配置则所有 internal 端点完全开放
- 多卖家拆单无补偿事务，第二组失败时第一组的库存/积分不会被释放
- 防腐层静默吞异常，积分冻结失败仍创建订单，导致积分账户透支
- Outbox 发布成功但 SaveChanges 失败导致重复发布，发件箱模式失效
- 死信"重投"是占位实现，运营点重投实际消息未重投

这些问题中有 17 项为 P0 级（阻塞生产/资金损失/安全漏洞），必须优先修复。本 spec 全量规划 P0+P1+P2，分批交付，每批可独立验证、独立回滚。

---

## What Changes

### P0 — 业务正确性与安全（阻塞生产）

- **BREAKING（行为变更）**：支付回调与主动查询补偿增加金额校验，金额不一致一律拒绝并返回 FAIL，不再标记订单成功
- **BREAKING**：`InternalApiKey` 未配置时，生产环境启动直接抛异常阻止启动（不再 fail-open）；ApiKey 比较改为 timing-safe；internal 路由匹配增加边界检查
- **新增**：优惠券 `Lock` 流程贯通——`IPromotionAntiCorruptionService` 新增 `LockCouponAsync`，下单时锁定选定券，支付成功后核销
- **新增**：优惠券领取增加数据库唯一约束 `(UserId, CouponId)` 防并发重复领取
- **新增**：多卖家拆单 Saga 补偿——任一组失败时对已成功组执行补偿（释放库存、释放积分、取消订单）
- **新增**：单组下单积分冻结失败时回滚已预占库存
- **BREAKING**：`OrderAppService.PayAsync` 改为通过聚合领域事件 + Outbox 发布 `PaymentRequestedIntegrationEvent`，与订单状态变更同事务，新增"已发起支付"防重复发起
- **BREAKING**：防腐层 `PointsAntiCorruptionService.Freeze/Confirm/Release`、`PromotionAntiCorruptionService.ReleaseCoupons`、`CalculateDiscount` 失败由"静默吞异常返回默认值"改为"显式抛领域异常"
- **新增**：Outbox 两阶段标记（`Pending → Publishing → Processed`）防重复发布；重启扫描 `Publishing` 超时消息依赖下游幂等兜底
- **BREAKING**：`IntegrationEventConsumerBase` 幂等方法改为 abstract，强制全量 Consumer 注入 `IIdempotencyStore`（既有方案主线 3.4 已规划，本 spec 推进落地并审计全部子类）
- **新增**：死信重投真实实现（通过 RabbitMQ Management API 或 `_eventBus.PublishAsync` 重新发布原始事件）；死信拉取改为处理成功后才 ack
- **新增**：Redis 库存与 DB 库存定期对账任务；秒杀库存 `WriteBackToDbAsync` 真实回写 DB（移除占位日志）

### P1 — 健壮性与可观测性

- 防腐层降级时记录告警（Prometheus 指标 + 日志）
- 批量库存预占回滚失败记入补偿表，后台任务重试
- 支付回调 Redis 故障时返回 FAIL 让渠道重试，由聚合状态机兜底幂等
- 死信积压告警 `BackgroundService`（每 5 分钟扫描，超阈值触发告警）
- 缓存失效订阅断线自动重连；缓存失效改双删模式（先删→写库→延迟再删）
- Outbox 并行处理（`Parallel.ForEachAsync`）+ 积压告警；类型解析改用 FullName + 自定义类型解析器兼容版本升级

### P2 — 收尾与文档

- 缓存 Pattern 失效改用 `UNLINK` + 分批 SCAN
- 清理 `NewFeatureTests.cs` 空文件、重命名 `NewFeatureTests1-6.cs`
- 补 3 个空 `Application.Tests`（ReviewAfterSales / SellerShop / SystemAdmin）
- CI 集成 `scripts/check-placeholders.sh` 占位扫描步骤
- 编码规范同步本次安全/业务正确性约定

---

## Impact

- **Affected specs（既有）**：
  - `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md` — 主线 3.4（`IIdempotencyStore`）由本 spec 推进落地；主线 4.7（防腐层静默兜底）由本 spec 细化并实施
  - `.trae/specs/replace-placeholder-implementations/spec.md` — 死信重投占位、秒杀回写占位属同类占位，本 spec 接续处理
  - `docs/spec/04-订单与交易域.md`、`docs/spec/08-支付集成域.md`、`docs/spec/05-促销域.md` — 业务流程契约变更需同步
- **Affected code（关键文件）**：
  - 支付：`src/Services/Payment/Leno.Payment.Infrastructure/Notify/{WeChatPayNotifyHandler,AlipayNotifyHandler}.cs`、`src/Services/Payment/Leno.Payment.Application/Services/PaymentAppService.cs`、`src/Services/Payment/Leno.Payment.Domain/Aggregates/PaymentOrder.cs`
  - 优惠券：`src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs`、`src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs`、`src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs`、`src/Services/Promotion/Leno.Promotion.Infrastructure/Repositories/EfCoreUserCouponRepository.cs`、`src/Services/Promotion/Leno.Promotion.Infrastructure/Configurations/UserCouponConfiguration.cs`
  - 鉴权：`src/BuildingBlocks/Leno.Infrastructure/Middleware/InternalApiKeyMiddleware.cs`、`src/BuildingBlocks/Leno.Infrastructure/Auth/InternalApiKeyOptions.cs`
  - 事务/库存：`src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs`、`src/Services/Order/Leno.Order.Infrastructure/Services/{StockReservationDomainService,AntiCorruptionServices}.cs`、`src/Services/Order/Leno.Order.Infrastructure/Repositories/RedisInventoryRepository.cs`、`src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs`
  - 幂等/Outbox：`src/BuildingBlocks/Leno.Infrastructure/EventBus/{IntegrationEventConsumerBase,RedisIntegrationEventConsumerBase}.cs`、`src/BuildingBlocks/Leno.Infrastructure/Outbox/{OutboxPublisher,OutboxMessage}.cs`
  - 死信：`src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/{RabbitMqDeadLetterManager,DeadLetterQueueManager}.cs`
  - 缓存：`src/ApiGateway/Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs`
  - 测试/CI：`src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/NewFeatureTests*.cs`、`.github/workflows/ci.yml`

---

## ADDED Requirements

### Requirement: 支付金额强校验

支付渠道回调与主动查询补偿在标记订单成功前，系统 SHALL 校验回调/查询返回的实付金额等于 `PaymentOrder.Amount`，不一致一律拒绝并返回 FAIL，不得标记订单成功。

#### Scenario: 回调金额一致
- **WHEN** 渠道回调到达，验签通过且回调金额 == 订单金额
- **THEN** 调用 `MarkSucceeded` 标记订单成功，返回 SUCCESS

#### Scenario: 回调金额不一致（伪造低金额）
- **WHEN** 回调金额 < 订单金额（如 0.01 元 vs 1000 元）
- **THEN** 记录安全告警日志，订单保持原状态，返回 FAIL，不调用 `MarkSucceeded`

#### Scenario: 主动查询补偿金额不一致
- **WHEN** `QueryPaymentStatusAsync` 查询渠道返回已支付但金额不匹配
- **THEN** 不标记订单成功，记录告警，进入人工对账队列

### Requirement: 优惠券锁定流程贯通

下单选中优惠券时，系统 SHALL 在订单创建事务内调用 `LockCouponAsync(userId, couponId, orderId)` 将券标记为 `Locked`，确保同一张券不会被并发订单重复使用；支付成功后由 `OrderPaidEventConsumer` 核销（`Used`）；订单取消时释放（`Unused`）。

#### Scenario: 下单锁定优惠券
- **WHEN** 用户下单且选用优惠券
- **THEN** 调用 `LockCouponAsync` 成功后订单才创建成功；券状态由 `Unused` → `Locked`，记录 `LockedOrderId`

#### Scenario: 并发订单使用同一券
- **WHEN** 两个并发订单尝试锁定同一张 `Unused` 券
- **THEN** 仅一个成功（基于聚合状态机 + 乐观锁），另一个抛领域异常"优惠券已被占用"

#### Scenario: 支付成功核销
- **WHEN** 订单支付成功事件到达
- **THEN** `OrderPaidEventConsumer` 查 `GetByLockedOrderIdAsync(orderId)` 命中券并标记 `Used`

### Requirement: 优惠券领取并发安全

系统 SHALL 在数据库层对 `(UserId, CouponId)` 建立唯一约束，防止并发领取导致重复发券。

#### Scenario: 并发领取同一券
- **WHEN** 两个并发请求为同一用户领取同一券模板
- **THEN** 数据库唯一约束拒绝其一，应用层捕获唯一约束冲突返回"已领取"

### Requirement: InternalApiKey 安全默认

系统 SHALL 在生产环境（非 Development）启动时校验 `InternalAuth:ApiKey` 非空，为空直接抛异常阻止启动；ApiKey 比较使用 timing-safe 常数时间比较；internal 路由匹配精确到 `/internal` 或 `/internal/` 前缀。

#### Scenario: 生产环境未配置 ApiKey
- **WHEN** 生产环境启动且 `InternalAuth:ApiKey` 为空
- **THEN** 抛出启动异常，服务不启动，日志明确提示配置缺失

#### Scenario: 路径边界精确匹配
- **WHEN** 请求路径为 `/internalinfo`（非 internal 端点）
- **THEN** 不被识别为 internal 路由，走正常鉴权链路

### Requirement: 多卖家拆单 Saga 补偿

多卖家拆单创建订单时，系统 SHALL 以 Saga 方式编排各组操作；任一组失败时对已成功组执行补偿（释放库存、释放积分、取消已保存订单），保证最终一致。

#### Scenario: 第二组失败回滚第一组
- **WHEN** 卖家 A 组成功（库存预占 + 积分冻结 + 订单保存），卖家 B 组库存预占失败
- **THEN** 对卖家 A 组执行补偿：释放库存、释放积分、取消/删除订单，整体抛异常让用户重试

#### Scenario: 全部成功
- **WHEN** 所有组成功
- **THEN** 全部订单创建成功，库存预占、积分冻结、优惠券锁定状态正确

### Requirement: 单组下单原子回滚

单组下单中，库存预占成功但积分冻结失败时，系统 SHALL 释放已预占库存后再抛异常，避免库存被无效占用。

#### Scenario: 积分冻结失败释放库存
- **WHEN** 库存预占成功，`FreezeAsync` 抛异常
- **THEN** 调用 `ReleaseBatchAsync` 释放已预占库存，再向上抛异常

### Requirement: 支付发起事件原子化

`OrderAppService.PayAsync` SHALL 通过聚合领域事件 + Outbox 模式发布 `PaymentRequestedIntegrationEvent`，与订单状态变更同事务；订单新增"已发起支付"标记防止重复发起。

#### Scenario: 重复点击支付
- **WHEN** 订单已标记"已发起支付"，用户再次点击支付
- **THEN** 返回"支付已发起，请勿重复操作"，不重复发布事件

### Requirement: 防腐层显式错误传播

防腐层服务（积分冻结/确认/释放、优惠券释放、促销计算）SHALL 在远程调用失败时显式抛领域异常或返回显式失败结果，不得静默吞异常返回默认值掩盖数据不一致。

#### Scenario: 积分冻结失败阻止下单
- **WHEN** `FreezeAsync` 远程调用失败
- **THEN** 抛出领域异常，`CreateOrderAsync` 捕获并回滚库存预占，订单不创建

#### Scenario: 优惠券释放失败可重试
- **WHEN** 订单取消时 `ReleaseCouponsAsync` 失败
- **THEN** 抛异常或写入"待释放券"补偿表，由后台任务重试，券最终被释放

### Requirement: Outbox 两阶段标记防重复发布

Outbox 发布 SHALL 采用两阶段标记：事务中将消息置 `Publishing` 并提交 → 发布到 MQ → 置 `Processed` 并提交。重启时扫描 `Publishing` 超时消息，依赖下游幂等兜底，不再重复发布已确认发布的消息。

#### Scenario: 发布成功但标记失败
- **WHEN** MQ 发布成功，`Processed` 标记保存失败
- **THEN** 重启后扫描 `Publishing` 超时消息，由下游消费者幂等性保证不重复执行业务

#### Scenario: 发布失败
- **WHEN** MQ 发布失败
- **THEN** 消息保持 `Publishing`（或回退 `Pending`），下次轮询重试

### Requirement: 消费者幂等强制

`IntegrationEventConsumerBase` 的幂等方法 SHALL 为 abstract，所有 Consumer 子类 SHALL 注入 `IIdempotencyStore`（默认 `RedisIdempotencyStore`，基于 SET NX + 24h TTL）实现幂等去重；全量审计现有 Consumer，未实现幂等者补齐。

#### Scenario: 重复事件被去重
- **WHEN** 同一 `EventId` 的事件被重复投递
- **THEN** 仅第一次执行业务逻辑，后续 `IsProcessedAsync` 返回 true 直接跳过

### Requirement: 死信重投真实实现

死信"重投"操作 SHALL 通过 RabbitMQ Management API 或 `_eventBus.PublishAsync` 真正重新发布原始事件到 MQ，不得是仅记日志的占位实现；死信拉取 SHALL 在本地处理成功后才 ack，处理失败回队。

#### Scenario: 重投成功
- **WHEN** 运营点击重投
- **THEN** 原始事件重新发布到目标 exchange，死信记录状态更新为"已重投"

#### Scenario: 拉取后处理失败
- **WHEN** 从 DLQ 拉取消息后本地处理失败
- **THEN** 消息回队（不丢失），可再次拉取

### Requirement: 库存 Redis-DB 对账

系统 SHALL 提供定期对账任务，比较 Redis 库存基线与 DB `StockReservation` 聚合，不一致时记录告警并触发修复；秒杀库存 SHALL 真实回写 DB（移除占位日志实现）。

#### Scenario: Redis 与 DB 不一致
- **WHEN** 对账任务发现 Redis 库存 ≠ DB 可用库存
- **THEN** 记录告警，触发基线重建（以 DB 为准刷新 Redis）

#### Scenario: 秒杀回写
- **WHEN** 秒杀活动扣减 Redis 库存
- **THEN** 差异真实写回 `SeckillActivity` 聚合并持久化，不再仅记日志

---

## MODIFIED Requirements

### Requirement: 防腐层降级可观测（P1）

防腐层在远程调用失败时，除显式抛异常外，SHALL 记录 Prometheus 告警指标与结构化日志，便于监控发现下游故障。

### Requirement: 支付回调 Redis 故障降级（P1）

支付回调幂等标记在 Redis 故障时 SHALL 返回 FAIL 让渠道重试，不得 fail-open 放行；由 `PaymentOrder` 聚合状态机兜底幂等。

### Requirement: 死信积压告警（P1）

系统 SHALL 提供 `DeadLetterMonitorBackgroundService`，定期扫描死信数量，超阈值触发告警。

### Requirement: 缓存失效健壮性（P1）

`CacheInvalidationSubscriber` SHALL 监听 Redis 连接断开事件自动重连；缓存失效 SHALL 采用双删模式（先删→写库→延迟 500ms 再删）缩小脏读窗口。

### Requirement: Outbox 性能与可观测性（P1）

Outbox 处理 SHALL 支持并行（`Parallel.ForEachAsync`）；每次轮询后统计 pending 数量，超阈值记录告警；类型解析 SHALL 使用 FullName + 自定义类型解析器，兼容 BC 版本升级。

### Requirement: 缓存 Pattern 失效性能（P2）

`InvalidatePatternAsync` SHALL 使用 `UNLINK` 异步删除 + 分批 SCAN，避免阻塞 Redis。

### Requirement: 测试占位收尾（P2）

清理 `NewFeatureTests.cs` 空文件；重命名 `NewFeatureTests1-6.cs` 为具名测试文件；补 ReviewAfterSales / SellerShop / SystemAdmin 三个空 `Application.Tests` 的关键测试；CI 集成 `scripts/check-placeholders.sh`。

### Requirement: 文档同步（P2）

`docs/编码规范.md` 同步本次安全/业务正确性约定（支付金额校验、优惠券锁流程、InternalApiKey 安全默认、Outbox 两阶段、防腐层显式错误传播、幂等强制）。

---

## 设计原则

1. **不破坏既有功能** — 所有修复保持现有测试通过，破坏性变更（支付金额校验、fail-open 修复）需评估对既有流程影响
2. **测试先行** — 每个 P0 修复先补对应单元/集成测试（含攻击场景），修复后验证
3. **分批交付** — P0 → P1 → P2，每批独立验证、独立回滚
4. **与架构方案不重复** — BC 边界、共享内核、样板去重、gRPC 等仍归既有方案，本 spec 仅推进交叉点（`IIdempotencyStore` 落地、防腐层错误处理）
5. **安全默认 fail-closed** — 鉴权、金额校验、幂等等安全相关逻辑默认拒绝，不得 fail-open

---

## 与既有方案的边界

| 既有方案主线 | 本 spec 关系 |
|---|---|
| 主线 1 BC 边界修复 | 不重复（仍归既有方案） |
| 主线 2 共享内核清理 | 不重复 |
| 主线 3.4 `IIdempotencyStore` / Consumer 基类合并 | **本 spec 推进落地**（与新发现 2.1 幂等缺陷交叉，本 spec 负责实施） |
| 主线 4.7 防腐层静默兜底 | **本 spec 细化并实施**（新发现 1.1-1.4 补充具体业务影响） |
| 主线 7 网关增强 | 不重复 |
| 主线 9 gRPC 迁移 | 不重复 |

---

## 风险与缓解

- **支付金额校验破坏既有流程**：缓解 — 灰度上线，先记录不匹配告警观察一周再强制拦截
- **优惠券 Lock 流程上线后历史未锁定券**：缓解 — 数据迁移脚本扫描未锁定但已下单的券，按订单回填 `LockedOrderId`
- **InternalApiKey fail-closed 阻止启动**：缓解 — 部署文档明确配置项，CI 增加配置检查
- **Saga 补偿自身失败**：缓解 — 补偿动作记入补偿表，后台任务重试；关键补偿（释放库存）使用 Redis Lua 原子操作
- **Outbox 两阶段引入 `Publishing` 中间态**：缓解 — 重启扫描逻辑 + 下游幂等兜底，监控 `Publishing` 超时消息数
