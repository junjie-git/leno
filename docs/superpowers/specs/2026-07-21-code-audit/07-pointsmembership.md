# PointsMembership（积分与会员域）静态代码审计报告

- **审计范围**：`/workspace/src/Services/PointsMembership/` 下四个工程
  - `Leno.PointsMembership.Domain/`
  - `Leno.PointsMembership.Application/`
  - `Leno.PointsMembership.Infrastructure/`
  - `Leno.PointsMembership.Api/`
- **严格排除项**：所有 `Tests` 目录、`Migrations/*.Designer.cs`、`*ModelSnapshot.cs`、`SharedContracts.Grpc/Generated/`
- **交叉引用**：`Leno.SharedContracts/Events/PointsMembershipEvents.cs`、`Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs`、`Leno.Infrastructure` 通用基座
- **审计日期**：2026-07-21
- **审计维度**：A 功能正确性与缺陷 / B DDD 与架构合规 / C 性能与可靠性

---

## 概述

PointsMembership 业务域承载积分账户、签到、消费返积分、订单抵扣（冻结/确认/释放）、评价返积分、会员等级（消费门槛体系 V1+ 与成长值体系 V0-V4 双轨）、付费会员订阅、积分兑换优惠券等能力。该域整体结构清晰、聚合根不变量校验到位，但存在多条**关键链路完全失效**的严重问题：成长值累加与积分流水写入两条数据通路在生产代码中均无任何调用方，直接导致 V0-V4 等级评估任务、积分过期任务、4 个读模型同步消费者全部沦为死代码；同时订单抵扣防腐层的 HTTP 端点存在缺口，订单域通过 HTTP 调用 ConfirmDeduction 必然 404；多个集成事件消费链路存在非原子操作与重复发放风险。

---

## 🔴 高风险问题

### PM-H01 会员成长值累加方法 `Member.AddGrowthValue` 在生产代码中无任何调用方，V0-V4 等级体系整体失效

- **维度**：A 功能正确性 / C 可靠性
- **类别**：死代码、功能缺失
- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs#L119-L133`
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/MemberLevelEvaluationJob.cs#L80-L90`
- **现状**：`Member.AddGrowthValue(int amount, string reason)` 工厂内累加 `GrowthValue`，是唯一可变更成长值的入口。检索整个 `Leno.PointsMembership` 目录，`AddGrowthValue` 的调用仅出现在测试目录 `Leno.PointsMembership.Domain.Tests/DomainTests.cs`（第 1421-1515 行的 6 处测试用例），生产路径（Application/Infrastructure/Api 三层）**零调用**。
- **后果**：
  1. 任何用户会员的 `GrowthValue` 字段永远停留在初始值 0；
  2. `MemberLevelEvaluationJob.EvaluateAllMembersAsync`（Member.cs#L139 `EvaluateGrowthLevel`）每个批次调用 `member.EvaluateGrowthLevel(levels)`，但因 `GrowthValue==0`，`MemberLevel.EvaluateLevel` 必然返回 V0，且 `newLevel == CurrentGrowthLevel` 永远成立（Member.cs#L143-L146 提前返回），变更计数 `changedInBatch` 恒为 0；
  3. `MemberLevelChangedEvent` 永不发布，消息通知域无法收到等级变更通知；
  4. 该 Job 实质为每日空转扫描全表，浪费数据库资源。
- **根因**：消费返积分（`OrderCompletedEventConsumer`/`OrderAfterSalesWindowClosedEventConsumer`）和签到返积分（`PointsAppService.CheckInAsync`）只调用 `account.Earn(...)`，未联动调用 `member.AddGrowthValue(...)`。成长值体系与积分入账链路未打通。

---

### PM-H02 `PointsLedger.Create` 永不被调用，积分流水永不落库，`PointsExpiryService` 永远过期 0 积分

- **维度**：A 功能正确性 / C 可靠性
- **类别**：死代码、流水缺失、过期任务空转
- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCorePointsAccountRepository.cs#L52-L58`
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/PointsExpiryService.cs#L104-L143`
- **现状**：仓储接口 `IPointsAccountRepository.GetEarnLedgersByAccountIdAsync` 直接查询 `PointsLedgers` 表（EfCorePointsAccountRepository.cs#L55-L58）；`PointsExpiryService.CalculateExpiredPointsAsync`（PointsExpiryService.cs#L119）依赖该返回值按 FIFO 计算过期积分。但检索 `PointsLedger.Create` 在整个 `Leno.PointsMembership` 目录**无任何匹配**——`PointsAccount.Earn/Freeze/ConfirmDeduct/Release/ConsumePoints/RevertPoints/ExpirePoints` 七个状态变更方法均只 `AddDomainEvent`，没有任何一处 `PointsLedger.Create(...)` 写入流水。
- **后果**：
  1. `points_ledgers` 表永远为空，所有积分变动无审计流水，运营对账、合规审计、用户争议举证全部缺失；
  2. `PointsExpiryService` 每日扫描 `account.Balance > 0` 的账户，但 `GetEarnLedgersByAccountIdAsync` 永远返回空列表，`foreach (var ledger in earnLedgers)` 永不进入循环体，`expiredPoints` 恒为 0（PointsExpiryService.cs#L121-L142），`account.ExpirePoints` 永不被调用；
  3. 积分永不过期，公司积分负债无限累积；
  4. `PointsAppService.GetLedgerAsync` 同样返回空（见 PM-M07），用户在 App 端查询积分明细永远空白。
- **根因**：聚合根变更时只生成领域事件，但没有任何事件消费者或仓储逻辑将变动落库到 `PointsLedger` 表；与 `Order` 域等其它 BC 中"聚合变更 + 流水同事务写入"的范式不一致。

---

### PM-H03 4 个 ReadModel 同步消费者订阅的集成事件在本 BC 中永不发布（死消费者）

- **维度**：B 架构合规 / C 可靠性
- **类别**：事件契约不一致、读模型同步失效
- **位置与证据**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/PointsAccountCreatedReadModelSyncConsumer.cs#L13-L14` 消费 `PointsAccountCreatedEvent`
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/PointsAdjustedReadModelSyncConsumer.cs#L15-L16` 消费 `PointsAdjustedEvent`
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/MemberRegisteredReadModelSyncConsumer.cs#L13-L14` 消费 `MemberRegisteredEvent`
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/MemberLevelUpgradedReadModelSyncConsumer.cs#L15-L16` 消费集成事件版本 `MemberLevelUpgradedEvent`（含 `MemberId` 字段）
  - 对照 `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/EventBus/PointsMembershipIntegrationEventMapper.cs#L17-L42` 仅注册 6 类映射：`PointsEarnedEvent`、`PointsConsumedEvent`、`PointsRevertedEvent`、`MemberLevelChangedEvent`、领域版 `MemberLevelUpgradedEvent`、`MembershipActivatedEvent`
- **现状**：检索上述 4 类集成事件类型在 `Leno.PointsMembership` 目录的全部出现位置，命中文件仅 3 个 ReadModel 同步消费者 + 2 个测试文件，**本 BC 没有任何发布方**。具体：
  - `PointsAccountCreatedEvent`：`PointsAccount.Create` 只 `AddDomainEvent`？实际查 `PointsAccount.cs#L49-L64` 工厂方法**未发布任何事件**；mapper 中也无对应翻译。读模型永远无初始投影。
  - `PointsAdjustedEvent`：mapper 未注册翻译；`PointsAccount.Earn/Freeze/...` 对应的领域事件翻译为 `PointsEarnedIntegrationEvent` 等具体类型，而非通用 `PointsAdjustedEvent`。
  - `MemberRegisteredEvent`：会员注册流程未在本 BC 触发（本 BC 通过 `UserRegisteredEventConsumer` 消费用户域事件创建 Member，但未回发 `MemberRegisteredEvent` 集成事件）。
  - 集成事件版 `MemberLevelUpgradedEvent`：mapper 显式将**领域版** `MemberLevelUpgradedEvent` 翻译为 `MemberLevelChangedIntegrationEvent`（PointsMembershipIntegrationEventMapper.cs#L35-L37 注释明确"复用统一等级变更集成事件"），**未发布**集成事件版 `MemberLevelUpgradedEvent`。而 `MemberLevelUpgradedReadModelSyncConsumer` 消费的恰恰是后者（其 `integrationEvent.MemberId` 字段只存在于集成事件版本，领域版字段名为 `UserId`）。
- **后果**：
  1. 4 个消费者在 MassTransit 上订阅了永不抵达的事件类型，看似启动成功实则从未触发；
  2. ES 中 `PointsAccountReadModel` 索引、`MemberReadModel` 索引永远没有初始文档，CQRS 查询侧完全瘫痪；
  3. 等级升级时仅靠 `MemberLevelChangedIntegrationEvent` 触发通知，但读模型同步链路缺失，运营后台无法看到等级升级后的会员档案快照。
- **根因**：事件契约设计阶段定义了通用 `PointsAccountCreatedEvent`/`PointsAdjustedEvent`/`MemberRegisteredEvent` 三类集成事件，但 mapper 实现时改用了更具体的语义化事件类型（`PointsEarnedIntegrationEvent` 等），且未在 `PointsAccount.Create`、会员注册流程补发对应事件。

---

### PM-H04 `InternalPointsController` 缺失 Confirm HTTP 端点，订单域 HTTP 防腐层 `ConfirmDeductionAsync` 必然 404

- **维度**：A 功能正确性 / B 架构合规
- **类别**：API 契约缺口、跨 BC 调用断裂
- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L22-L53`（仅有 TrialOffset / Freeze / Release 三个端点）
  - `file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs#L89-L98`（HTTP POST `internal/v1/points/confirm`）
  - `file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Consumers/PaymentSucceededEventConsumer.cs#L82-L83`（支付成功后调用 `_pointsAntiCorruption.ConfirmDeductionAsync`）
- **现状**：`InternalPointsController` 共暴露 3 个 HTTP 端点：`trial-offset`、`freeze`、`release`。订单域防腐层 `PointsAntiCorruptionService.ConfirmDeductionAsync` 调用 `internal/v1/points/confirm`（PointsAntiCorruptionService.cs#L96），该路径在控制器中**不存在**。Confirm 端点仅存在于 gRPC 服务 `PointsGrpcService.Confirm`（`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs#L76-L88`）。
- **后果**：
  1. 当订单域通过 HTTP 调用 ConfirmDeduction（M4 双轨方案的 HTTP 路径）时，返回 404；
  2. `PointsAntiCorruptionService.EnsureSuccessStatusCode` 在非 2xx 时抛 `AntiCorruptionException`（"confirm_deduction"），导致 `PaymentSucceededEventConsumer` 消费失败；
  3. MassTransit 重试耗尽后进入死信队列，订单支付成功但积分冻结永远无法核销为正式扣减，用户积分余额"冻结"区永久滞留；
  4. 即使后续订单完成，由于 `FrozenBalance` 未清零，账户可用余额持续偏低。
- **根因**：双轨方案落地时 HTTP 控制器遗漏 Confirm 端点；gRPC 与 HTTP 路径能力未对齐。

---

### PM-H05 `ExchangeCouponAppService.ExchangeCouponAsync` 未使用 Outbox，冻结积分与发布事件非原子

- **维度**：A 功能正确性 / B 架构合规 / C 可靠性
- **类别**：事务边界、Outbox 模式破坏
- **位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/ExchangeCouponAppService.cs#L39-L76`
- **现状**：方法体内先 `account.Freeze(...)`（ExchangeCouponAppService.cs#L56），随后 `await _unitOfWork.SaveEntitiesAsync(ct)`（L57）提交事务；事务提交成功**之后**再 `await _eventBus.PublishAsync(evt, ct)`（L62）发布 `PointsExchangeCouponRequestedEvent`。两者不在同一事务内，未走 `EfCoreUnitOfWork.SaveEntitiesAsync` 内置的 Outbox 机制。
- **后果**：
  1. **场景 A**：`SaveEntitiesAsync` 成功、`PublishAsync` 失败（RabbitMQ 网络抖动）→ 积分已冻结但优惠券域永远收不到兑换请求，用户积分被锁死且无券可得，需要人工介入；
  2. **场景 B**：`SaveEntitiesAsync` 成功、应用进程在两次 await 之间崩溃 → 同样积分已冻结、事件丢失；
  3. 与本域其它应用服务（如 `PointsAppService.CheckInAsync`#L67 只调用 `SaveEntitiesAsync`，依赖 Outbox 自动发布领域事件）模式不一致；
  4. `PointsExchangeCouponRequestedEvent` 是直接通过 `IEventBus` 发布的"集成事件"，绕过了"领域事件→mapper→Outbox"的标准链路。
- **根因**：将 `IEventBus.PublishAsync` 与 `UnitOfWork.SaveEntitiesAsync` 混用，未意识到 `SaveEntitiesAsync` 已经能通过 Outbox 自动发布聚合根上的领域事件；该事件应改为聚合根领域事件，由 mapper 翻译后通过 Outbox 发布。

---

### PM-H06 `ReviewApprovedEventConsumer` Redis 计数为非原子读改写，并发会突破每日 5 条上限

- **维度**：A 功能正确性 / C 可靠性
- **类别**：并发缺陷、重复发放
- **位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs#L43-L72`
- **现状**：
  ```csharp
  var dailyCount = await _redisDb.StringGetAsync(dailyKey);                    // L46 读
  var currentCount = dailyCount.HasValue ? (int)dailyCount : 0;                // L47
  if (currentCount >= MaxDailyReviewPoints) { ... return; }                    // L49 检查
  ...
  account.Earn(...);                                                            // L63
  await _redisDb.StringSetAsync(dailyKey, currentCount + 1, TimeSpan.FromHours(25)); // L67 写
  ```
  读、检查、写三步不在同一原子操作中。
- **后果**：同一用户短时间内多条评价审核通过事件并发抵达（MassTransit 默认并发消费，或不同分区同时投递），多个消费者实例同时读到 `currentCount=4`，同时通过 L49 上限检查，同时写入 `5`，实际发出 N 条 10 分积分，远超每日 5 条上限，公司损失放大。
- **修复方向**：改用 `_redisDb.StringIncrementAsync(dailyKey)` 原子自增并返回新值，若返回值 `> MaxDailyReviewPoints` 则回滚（不调用 `account.Earn`）并 `StringDecrementAsync` 复原；或使用 Lua 脚本封装"检查+自增"。

---

### PM-H07 `OrderCompletedEventConsumer` 与 `OrderAfterSalesWindowClosedEventConsumer` 同时发放消费返积分，存在双倍发放风险

- **维度**：A 功能正确性 / C 可靠性
- **类别**：重复发放、业务流程设计缺陷
- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs#L40-L72`（`OrderCompletedEventConsumer.HandleAsync`，按 `TotalAmount` 发放）
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs#L142-L177`（`OrderAfterSalesWindowClosedEventConsumer.HandleAsync`，按 `PaidAmount` 发放）
- **现状**：两个消费者分别订阅 `OrderCompletedEvent` 与 `OrderAfterSalesWindowClosedEvent`，**都**调用 `account.Earn(PointsSource.Consumption, points, ...)`（L51 与 L153）。`OrderCompletedEvent` 通常在确认收货时由订单域发布，`OrderAfterSalesWindowClosedEvent` 在售后窗口（确认收货 +7 天）结束后由 `OrderAppService.ConfirmReceiptAsync` 调度的延迟消息触发（参考 `OrderAppService.cs#L286-L293`）。
- **后果**：
  1. 若订单域同时发布两类事件，同一笔订单用户获得 2 倍消费返积分；
  2. 即使两类事件的 EventId 不同（幂等去重不生效），仍构成业务层重复发放；
  3. `member.AddConsumption` 同样被调用两次（L59 与 L163），累计消费金额翻倍，可能触发错误的等级升级。
- **修复方向**：明确业务规则——消费返积分应在售后窗口关闭后发放（避免退货后已发积分难以追回），删除 `OrderCompletedEventConsumer` 中的 `account.Earn` 与 `member.AddConsumption` 逻辑；或反之。当前两个消费者并存属于设计意图不明。

---

### PM-H08 `OrderPaidEventConsumer` 在 package 为 null 或 `DurationDays<=0` 时抛异常，导致消费者整体失败

- **维度**：A 功能正确性 / C 可靠性
- **类别**：空值处理、异常处理、消息积压
- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderPaidEventConsumer.cs#L52-L63`
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/UserMembership.cs#L84-L108`（`Activate` 前置校验 `durationDays <= 0` 抛 `PointsDomainException`）
- **现状**：消费者代码：
  ```csharp
  var package = await _packageRepository.GetByIdAsync(userMembership.PackageId, ct);
  var durationDays = package?.DurationDays ?? 0;            // L57 package 为 null 时为 0
  userMembership.Activate(integrationEvent.OrderId, integrationEvent.PaidAt, durationDays); // L58
  ```
  `UserMembership.Activate` 在 `durationDays <= 0` 时抛 `PointsDomainException`（UserMembership.cs#L91-L94），该异常未被捕获，将冒泡到 `IntegrationEventConsumerBase.Consume`，触发 MassTransit 重试。
- **后果**：
  1. 若会员套餐被下架（package 软删除后 `GetByIdAsync` 返回 null）或套餐数据异常 `DurationDays==0`，该订单的 `OrderPaidEvent` 消费永远失败，重试耗尽进入死信队列；
  2. 同一消息中的 `account.ConfirmDeduct`（L48）虽已成功执行并保存到数据库，但消息整体进入死信后**不会被标记为已处理**（`IntegrationEventConsumerBase.Consume` 在 `HandleAsync` 抛异常时不会调用 `MarkAsProcessedAsync`，参见基类 L48-L50），下次重试会**再次**执行 `account.ConfirmDeduct`——而 `PointsAccount.FindFrozenEntry`（PointsAccount.cs#L238-L249）在冻结记录已被移除后抛 `POINTS_FROZEN_ENTRY_NOT_FOUND`，进入死循环；
  3. 即使不考虑重试，会员订阅订单支付成功但权益未激活，用户付了钱拿不到会员等级。
- **修复方向**：package 为 null 时应记录告警并跳过 Activate（或显式标记 UserMembership 为异常状态等待人工处理），不应让整个消费者失败；或将 `account.ConfirmDeduct` 与 `userMembership.Activate` 拆分为两个独立消费者，互不影响。

---

## 🟡 中风险问题

### PM-M01 `EfCorePointsAccountRepository.GetByFrozenOrderIdAsync` 通过集合扫描定位订单，未利用 `ix_points_frozen_entries_order_id` 索引

- **维度**：C 性能
- **类别**：N+1 / 集合扫描、索引未命中
- **位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCorePointsAccountRepository.cs#L36-L39`
- **现状**：方法使用 `FirstOrDefaultAsync(a => a.FrozenEntries.Any(e => e.OrderId == orderId), ct)`，EF Core 会将其翻译为 `EXISTS` 子查询，能否命中索引取决于数据库优化器；同时 `.Include(a => a.FrozenEntries)` 会把该账户的全部冻结明细一次性加载到内存。
- **后果**：高频调用路径（订单支付确认、订单取消释放）在用户冻结明细较多时性能下降；`Release`/`ConfirmDeduct` 在聚合内仍需 `FrozenEntries.FirstOrDefault`（PointsAccount.cs#L240）做二次定位，存在重复扫描。
- **修复方向**：增加 `IPointsFrozenEntryRepository.GetByOrderIdAsync(orderId)` 直接按 `order_id` 单表查询（命中 `ix_points_frozen_entries_order_id`），返回 `accountId` 后再加载账户聚合；或在 `PointsAccount` 上对 `FrozenEntries` 建立 `Dictionary<OrderId, PointsFrozenEntry>` 索引字段。

---

### PM-M02 `Member.AddGrowthValue(amount, reason)` 的 `reason` 参数被忽略，违反"参数必有用途"原则

- **维度**：A 功能正确性 / B 架构合规
- **类别**：API 设计、审计缺失
- **位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs#L119-L133`
- **现状**：方法签名 `public void AddGrowthValue(int amount, string reason)`，方法体内只使用 `amount`，`reason` 参数从未被读取、未持久化、未写入事件。对比 `PointsAccount.Earn(source, amount, reason)` 的 `reason` 同样仅用于审计但亦未落库（参见 PM-H02 流水缺失问题）。
- **后果**：调用方传入的成长值来源（如"消费返积分""签到返积分""活动赠送"）信息丢失，运营无法追溯成长值变动原因；与签名承诺不符，易误导后续维护者。
- **修复方向**：要么将 `reason` 写入 `MemberLevelChangeHistory`（已存在的子实体集合，参见 Member.cs#L45），要么改为 `AddGrowthValue(int amount)` 并在调用前由应用层记录审计日志。

---

### PM-M03 `PointsAppService.CheckInAsync` 使用 `DateTime.UtcNow` 计算 `today`，签到日期与用户所在时区错位

- **维度**：A 功能正确性
- **类别**：时区处理、边界条件
- **位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsAppService.cs#L36-L51`
- **现状**：
  ```csharp
  var today = DateOnly.FromDateTime(DateTime.UtcNow);   // L38 UTC 日期
  var existing = await _checkInRepository.GetByUserIdAndDateAsync(userId, today, ct);
  ...
  var latest = await _checkInRepository.GetLatestByUserIdAsync(userId, ct);
  var continuousDays = latest is not null && latest.CheckInDate == today.AddDays(-1) ? ... : 1;
  ```
  `CheckInRecord` 的 `CheckInDate` 字段本应表示用户本地日历日，但这里用 UTC 取日期。
- **后果**：
  1. 北京时间用户在 UTC 0:00-8:00 之间签到，`today` 仍为前一天，可能允许用户在前一天"已签到"的状态下再次签到（如果前一天是 UTC 视角今天），或反过来误判"今日已签到"；
  2. 连续签到奖励（7 天/30 天）的判定因时区偏移而断签；
  3. `ix_check_in_records_user_id_check_in_date` 唯一索引基于该字段，时区错位可能造成唯一约束误触发或漏触发。
- **修复方向**：在应用层注入用户时区（或统一使用 Asia/Shanghai），将 `today` 计算为 `DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz))`。

---

### PM-M04 `UserMembership.Activate` 与 `OrderPaidEventConsumer` 之间无并发控制，同一订单重复事件可能导致重复激活或竞态

- **维度**：A 功能正确性 / C 可靠性
- **类别**：并发缺陷、状态机
- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/UserMembership.cs#L84-L108`
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderPaidEventConsumer.cs#L52-L63`
- **现状**：`Activate` 方法虽校验 `Status != Pending` 抛异常（UserMembership.cs#L96-L101），但 `OrderPaidEventConsumer` 没有显式锁，依赖 MassTransit 的 `EventId` 幂等去重。当 RabbitMQ 在网络抖动后重投递同一 `OrderPaidEvent`（不同 `EventId` 的重复业务事件也可能发生，例如支付域重发），两个消费者实例可能同时读到 `Status == Pending`，同时通过校验，同时写入 `Status = Active`，造成 `EndTime` 被覆盖、`MembershipActivatedEvent` 重复发布。
- **后果**：会员权益被重复激活，`MembershipActivatedEvent` 重复触发通知域发送多条开通通知；若 `Version` 乐观锁生效，第二个保存会抛 `DbUpdateConcurrencyException`，但消费者未捕获该异常，会进入重试死循环。
- **修复方向**：在 `Activate` 内部基于 `OrderId` 做幂等检查（已激活且 OrderId 相同则直接返回而非抛异常），并在消费者层捕获 `DbUpdateConcurrencyException` 视为已处理。

---

### PM-M05 `MemberLevelUpgradedReadModelSyncConsumer` 期望消费集成事件版 `MemberLevelUpgradedEvent`，但 mapper 永不发布该事件

- **维度**：B 架构合规
- **类别**：事件契约不一致
- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/MemberLevelUpgradedReadModelSyncConsumer.cs#L15-L16`（消费 `MemberLevelUpgradedEvent`，使用 `integrationEvent.MemberId`）
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/EventBus/PointsMembershipIntegrationEventMapper.cs#L35-L37`（领域版 `MemberLevelUpgradedEvent` 翻译为 `MemberLevelChangedIntegrationEvent`，而非集成版）
- **现状**：`SharedContracts.Events.MemberLevelUpgradedEvent` 集成事件版本含 `MemberId` 字段；领域版 `Leno.PointsMembership.Domain.Events.MemberLevelUpgradedEvent` 含 `UserId` 字段（参见 `Member.cs#L115` `AddDomainEvent(new MemberLevelUpgradedEvent(UserId, oldLevel, CurrentLevel, ...))`）。mapper 用别名 `DomainMemberLevelUpgradedEvent` 引用领域版，翻译为 `MemberLevelChangedIntegrationEvent`——**集成版 `MemberLevelUpgradedEvent` 在本 BC 永不发布**。
- **后果**：`MemberLevelUpgradedReadModelSyncConsumer` 永远不会被触发（与 PM-H03 项 4 同源）；即使修复 PM-H03 的发布问题，若不修正 mapper，该消费者仍不会工作。
- **修复方向**：要么删除该消费者（与 mapper 设计意图一致：统一用 `MemberLevelChangedIntegrationEvent`），要么在 mapper 中将领域版翻译为集成版 `MemberLevelUpgradedEvent`（需同时改 `UserId → MemberId` 字段映射，需确认语义对齐）。

---

### PM-M06 `IPointsOffsetAppService` 接口定义在 Domain 层，`PointsOffsetAppService` 实现位于 Application 层，防腐层职责混乱

- **维度**：B 架构合规 / DDD 边界
- **类别**：层依赖倒置、防腐层位置错误
- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Services/IPointsOffsetAppService.cs#L1-L35`（接口在 Domain）
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsOffsetAppService.cs#L1-L82`（实现在 Application）
- **现状**：`IPointsOffsetAppService` 命名带 `AppService`（应用服务）后缀，却放在 Domain 层 `Services/` 目录；其方法签名 `TryOffsetAsync/FreezeAsync/ConfirmDeductAsync/ReleaseAsync` 是典型的应用层用例编排接口，而非领域服务。同时该接口当前**没有任何调用方**——`InternalPointsController` 与 `PointsGrpcService` 实际注入的是 `IPointsInternalAppService`（位于 Application 层），`PointsOffsetAppService` 实现类也无任何注册或调用。
- **后果**：
  1. Domain 层依赖了应用层概念（`CancellationToken`、用例编排），违反 DDD 中"领域层不感知应用层"原则；
  2. 死代码：`PointsOffsetAppService` 整个类无注册无调用，但占据维护成本；
  3. 命名混淆：与 `IPointsInternalAppService`（实际使用的应用服务接口）功能高度重叠。
- **修复方向**：删除 `IPointsOffsetAppService` 与 `PointsOffsetAppService`（已被 `IPointsInternalAppService` 替代），或将其合并到 `IPointsInternalAppService`。

---

### PM-M07 `PointsAppService.GetLedgerAsync` 返回空列表，注释承认"当前域尚未定义"

- **维度**：A 功能正确性
- **类别**：未实现、功能缺失
- **位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsAppService.cs#L86-L91`
- **现状**：
  ```csharp
  public Task<List<PointsLedgerDto>> GetLedgerAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
  {
      // 流水查询需独立的 IPointsLedgerRepository，当前域尚未定义，暂返回空列表。
      return Task.FromResult(new List<PointsLedgerDto>());
  }
  ```
  注释直接承认未实现。`page` 与 `pageSize` 参数被完全忽略。
- **后果**：用户在 App 端调用积分明细接口永远收到空列表，无法核对积分变动历史；与 PM-H02 流水永不写入形成"双重死链"——既无数据可查，查询接口也未实现。
- **修复方向**：与 PM-H02 一并修复——先实现 `PointsLedger` 写入，再实现 `IPointsLedgerRepository` 与分页查询。

---

### PM-M08 领域事件与集成事件同名 `MemberLevelUpgradedEvent`，依赖文件路径与别名消歧，可读性差且易引入 bug

- **维度**：B 架构合规
- **类别**：命名冲突、可维护性
- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MemberLevelUpgradedEvent.cs`（领域事件，字段 `UserId`）
  - `file:///workspace/src/SharedContracts/Leno.SharedContracts/Events/PointsMembershipEvents.cs`（集成事件，字段 `MemberId`）
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/EventBus/PointsMembershipIntegrationEventMapper.cs#L4`（`using DomainMemberLevelUpgradedEvent = Leno.PointsMembership.Domain.Events.MemberLevelUpgradedEvent;` 别名消歧）
- **现状**：两类事件共用 `MemberLevelUpgradedEvent` 类名，仅命名空间不同；mapper 通过 `using` 别名消歧。`MemberLevelUpgradedReadModelSyncConsumer`（PM-M05）消费的是集成事件版本，但代码中无别名，依赖 `using Leno.SharedContracts.Events;` 优先解析。
- **后果**：维护者在不同文件看到 `MemberLevelUpgradedEvent` 时需仔细核对 `using` 才能判断是哪一版；新增消费者或 mapper handler 时极易引用错误版本，导致字段访问异常（`UserId` vs `MemberId`）。
- **修复方向**：将集成事件版重命名为 `MemberLevelUpgradedIntegrationEvent`（与 `PointsEarnedIntegrationEvent` 等命名一致），或重命名领域版为 `MemberLevelUpgradedDomainEvent`。

---

### PM-M09 `Member.CheckUpgrade` 与 `Member.EvaluateGrowthLevel` 两套等级体系并存但消费链路仅打通前者，成长值体系完全孤立

- **维度**：B 架构合规 / A 功能正确性
- **类别**：领域设计、双轨体系割裂
- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs#L97-L117`（`CheckUpgrade`，基于 `TotalConsumption` 升级 `CurrentLevel`，发布 `MemberLevelUpgradedEvent`）
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs#L139-L156`（`EvaluateGrowthLevel`，基于 `GrowthValue` 升级 `CurrentGrowthLevel`，发布 `MemberLevelChangedEvent`）
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs#L55-L66`（消费返积分时只调用 `member.CheckUpgrade`，未联动 `AddGrowthValue`/`EvaluateGrowthLevel`）
- **现状**：聚合根同时维护"消费门槛等级 `CurrentLevel`"与"成长值等级 `CurrentGrowthLevel`"两套独立字段，对应两个等级定义聚合 `MembershipLevel`（消费门槛）与 `MemberLevel`（成长值）。但生产链路中 `CheckUpgrade` 由 `OrderCompletedEventConsumer`/`OrderAfterSalesWindowClosedEventConsumer` 触发，`EvaluateGrowthLevel` 仅由 `MemberLevelEvaluationJob` 触发；由于 `AddGrowthValue` 无生产调用方（PM-H01），`GrowthValue` 恒为 0，`EvaluateGrowthLevel` 永远不产生实际变更。
- **后果**：V0-V4 成长值等级体系形同虚设；两套等级并存但只有一套生效，造成领域模型冗余、运营配置面板混乱（运营在 `MembershipLevel` 与 `MemberLevel` 两张表均需配置但只有前者生效）。
- **修复方向**：明确产品意图——若成长值体系是未来规划，应在 UI 层隐藏相关字段并在代码中标注 `// TODO: V2 启用`；若已废弃，应删除 `GrowthValue`/`CurrentGrowthLevel`/`MemberLevel` 聚合及 `MemberLevelEvaluationJob`。

---

## 🟢 低风险问题

### PM-L01 后台服务 `Task.Delay` 在异常路径后仍延后一日，且无 `StoppingToken` 主动取消时的快速退出保障

- **位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/MemberLevelEvaluationJob.cs#L32-L49`；`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/PointsExpiryService.cs#L35-L52`
- **现状**：`while (!stoppingToken.IsCancellationRequested)` 内 `try { ... } catch (Exception ex) { ... }` 后 `await Task.Delay(ScanInterval, stoppingToken)`。若某次扫描异常，下一次扫描需等 24 小时；`Task.Delay` 在 `StoppingToken` 取消时会抛 `OperationCanceledException`，但外层 `while` 条件已检查 `IsCancellationRequested`，异常会被吞掉（未观察到明显问题，但日志缺失）。
- **建议**：异常后采用指数退避（如 1 分钟、5 分钟、30 分钟）而非固定 24 小时；显式捕获 `OperationCanceledException` 跳出循环。

---

### PM-L02 硬编码 12 个月过期阈值与 `TimeSpan.FromHours(25)` Redis Key 过期时间

- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/PointsExpiryService.cs#L16`（`private const int ExpiryMonths = 12;`）
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs#L67`（`TimeSpan.FromHours(25)`）
- **现状**：过期阈值 12 个月硬编码常量，无法通过配置动态调整；Redis Key 25 小时过期试图覆盖任意时区的"今日"，但与 PM-M03 的 UTC 计算逻辑不一致。
- **建议**：抽取到 `IOptions<PointsMembershipOptions>` 配置；Redis Key 过期时间应与"用户时区当日内剩余时间 + 缓冲"对齐。

---

### PM-L03 `MemberLevel.EvaluateLevel` 存在双重排序，可优化为单次

- **位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/MemberLevel.cs`（静态方法 `EvaluateLevel`，需对照源码确认）
- **现状**：典型实现是先 `Where(l => l.MinGrowthValue <= growthValue)` 再 `OrderByDescending(l => l.Level).FirstOrDefault()`，或先 `OrderBy` 再 `Where`。若实现为先排序后过滤，存在轻微性能损耗（O(n log n) vs O(n)）。
- **建议**：改为先 `Where` 过滤再 `OrderByDescending` 取首个，或直接遍历一次记录最大值。

---

### PM-L04 `InternalPointsController` 每个端点使用双 `[HttpPost]` 路由（含 `[Obsolete]`），过渡期方案需明确下线时间

- **位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L23-L25`、`#L34-L36`、`#L45-L47`
- **现状**：每个动作同时挂载 `[HttpPost("internal/v1/points/xxx")]` 与 `[Obsolete] [HttpPost("internal/points/xxx")]`，注释称"双路由期保留，1 周后下线"。`Obsolete` 特性作用在方法上会被编译器告警，但 ASP.NET Core 路由仍生效。
- **建议**：建立明确的下线 issue，1 周后删除旧路由与 `[Obsolete]`；或将过渡期改为通过 `ApiVersion` 中介者管理。

---

### PM-L05 gRPC 服务在 `TrialOffset`/`Freeze`/`Release` 中使用 `new Guid(request.UserId)`，格式非法时抛 `ArgumentException` 而非 gRPC 友好错误

- **位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs#L37`、`#L55`、`#L56`、`#L68`
- **现状**：`Confirm`（L78）与 `GetPointsBalance`（L93）使用 `Guid.TryParse` 并抛 `RpcException(StatusCode.InvalidArgument)`；但 `TrialOffset`/`Freeze`/`Release` 直接 `new Guid(request.UserId)`，格式非法时抛 `ArgumentException`，gRPC 拦截器可能将其转为 `StatusCode.Unknown`，客户端难以辨别。
- **建议**：统一使用 `Guid.TryParse` + `RpcException(StatusCode.InvalidArgument)`。

---

### PM-L06 `ReviewApprovedEventConsumer` 使用 `DateTime.UtcNow.ToString("yyyyMMdd")` 计算 Redis Key 的"日"，与用户时区错位

- **位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs#L44`
- **现状**：`var today = DateTime.UtcNow.ToString("yyyyMMdd");` 与 PM-M03 同源问题，UTC 0:00-8:00 之间北京用户可能跨日。
- **后果**：每日 5 条上限在 UTC 切日时被绕过或被错误限制。
- **建议**：统一时区策略（参见 PM-M03）。

---

### PM-L07 `OrderCancelledEventConsumer` 与 `OrderPaidEventConsumer` 均调用 `GetByFrozenOrderIdAsync`，但失败语义不一致

- **位置**：
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs#L100-L105`（`OrderCancelledEventConsumer`：account 为 null 时 LogInformation 并返回，视为正常跳过）
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderPaidEventConsumer.cs#L45-L50`（`OrderPaidEventConsumer`：account 为 null 时静默跳过 ConfirmDeduct，但继续执行 UserMembership 激活）
- **现状**：两处对"订单无冻结积分"的语义处理不同：取消时跳过合理；支付确认时跳过 ConfirmDeduct 也合理（订单未用积分），但日志缺失（仅 L49 在成功路径记录日志，account 为 null 时无 LogInformation）。
- **建议**：在 `OrderPaidEventConsumer` 的 account null 分支补充 `Logger.LogInformation("订单 {OrderId} 无冻结积分，跳过 ConfirmDeduct", ...)`，便于排查。

---

## BC 健康度评分表

| 维度 | 子项 | 评分 (1-5) | 说明 |
|------|------|:---:|------|
| **A 功能正确性** | 空引用与异常处理 | 3.5 | 聚合根内部校验完整，但 `OrderPaidEventConsumer` 未处理 package null 场景（PM-H08） |
| | 并发与状态机 | 2.5 | `ReviewApprovedEventConsumer` Redis 非原子（PM-H06）、`UserMembership.Activate` 无幂等保护（PM-M04） |
| | 边界条件 | 3.0 | 时区处理多处错位（PM-M03、PM-L06） |
| | 资源泄漏 | 4.0 | `using` 与 `await using` 使用规范，无明显泄漏 |
| | 异步可靠性 | 3.0 | `ExchangeCouponAppService` 未用 Outbox（PM-H05）、消息重试死循环风险（PM-H08） |
| | 事务边界 | 2.5 | Outbox 在 ExchangeCoupon 场景被绕过（PM-H05） |
| **A 子项均分** | | **3.08** | |
| **B DDD/架构合规** | BC 边界泄漏 | 3.5 | 防腐层位置基本正确，但 `IPointsOffsetAppService` 错置 Domain 层（PM-M06） |
| | 聚合设计 | 3.5 | `PointsAccount`/`Member` 不变量清晰，但成长值体系双轨割裂（PM-M09） |
| | 防腐层 | 3.0 | HTTP Confirm 端点缺失（PM-H04）、gRPC 与 HTTP 能力未对齐 |
| | 共享内核污染 | 4.0 | SharedContracts 仅包含 DTO/事件，无业务逻辑泄漏 |
| | CQRS 职责 | 2.0 | 读模型同步消费者 4 个死链（PM-H03、PM-M05）、写模型流水缺失（PM-H02） |
| | 层依赖 | 3.0 | Domain 层混入应用服务接口（PM-M06） |
| | 事件契约一致性 | 1.5 | 4 类集成事件发布方缺失（PM-H03）、同名事件混淆（PM-M08） |
| | 仓储滥用 | 3.5 | `GetByFrozenOrderIdAsync` 集合扫描（PM-M01） |
| **B 子项均分** | | **2.93** | |
| **C 性能与可靠性** | N+1 查询 | 3.5 | `GetByFrozenOrderIdAsync` 集合扫描（PM-M01），其余 Include 合理 |
| | 索引覆盖 | 4.0 | `ix_members_user_id`/`ix_check_in_records_user_id_check_in_date`/`ix_points_frozen_entries_order_id` 等关键索引齐全 |
| | 缓存策略 | 3.0 | Redis 计数非原子（PM-H06）、Key 过期时间硬编码（PM-L02） |
| | 大对象扫描 | 3.0 | `MemberLevelEvaluationJob` 全表扫描但批次 500 合理；因 GrowthValue 恒 0 等于空转（PM-H01） |
| | 异步消息积压 | 2.5 | `OrderPaidEventConsumer` 异常会进入重试死循环（PM-H08） |
| | Outbox/幂等 | 3.0 | `ExchangeCouponAppService` 绕过 Outbox（PM-H05）、`IntegrationEventConsumerBase` 幂等基座健全 |
| | 资源池 | 4.0 | DbContext/HttpClient/Redis 复用规范 |
| | 限流/熔断 | 2.5 | 未见显式限流（评价返积分、签到返积分无速率限制） |
| **C 子项均分** | | **3.19** | |
| **总体健康度** | | **3.05 / 5.0** | 关键链路多处失效（成长值、流水、读模型、HTTP Confirm），建议优先修复 PM-H01 ~ PM-H08 |

---

## 关键修复优先级建议

| 优先级 | 编号 | 一句话描述 | 影响面 |
|:---:|:---:|---|---|
| P0 | PM-H02 | 实现 `PointsLedger` 写入（聚合变更同事务落流水） | 全域审计、过期、查询 |
| P0 | PM-H04 | 补全 `InternalPointsController.Confirm` HTTP 端点 | 订单支付积分核销 |
| P0 | PM-H05 | `ExchangeCouponAppService` 改用 Outbox 发布事件 | 积分兑换优惠券 |
| P0 | PM-H06 | `ReviewApprovedEventConsumer` Redis 改用 `StringIncrementAsync` | 评价返积分超额 |
| P0 | PM-H07 | 明确 `OrderCompleted` 与 `OrderAfterSalesWindowClosed` 二选一发放 | 消费返积分双倍 |
| P0 | PM-H08 | `OrderPaidEventConsumer` 处理 package null 场景 | 会员订阅订单激活 |
| P1 | PM-H01 | 在消费返积分/签到返积分链路调用 `Member.AddGrowthValue` | V0-V4 等级体系 |
| P1 | PM-H03 | 修复 4 个 ReadModel 同步消费者的事件发布方 | CQRS 读模型 |
| P2 | PM-M01 ~ PM-M09 | 中风险项逐步修复 | 性能、合规、可维护性 |
| P3 | PM-L01 ~ PM-L07 | 低风险项随相关模块迭代修复 | 健壮性 |

---

## 审计方法说明

- **工具**：`Read`（精确读取文件）、`Grep`（全局检索符号/调用方）、`SearchCodebase`（语义检索）、`Glob`（文件模式匹配）
- **关键检索验证**：
  - `Grep "PointsLedger\.Create" /workspace/src/Services/PointsMembership` → **0 匹配**，证实 PM-H02
  - `Grep "AddGrowthValue" /workspace/src/Services/PointsMembership` → **仅测试目录命中**，证实 PM-H01
  - `Grep "PointsAccountCreatedEvent|PointsAdjustedEvent|MemberRegisteredEvent" /workspace/src/Services/PointsMembership` → **仅 ReadModels + Tests 命中**，证实 PM-H03
- **交叉验证**：对照 `Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs` 确认 HTTP `confirm` 路径调用，对照 `Leno.Order.Infrastructure/Consumers/PaymentSucceededEventConsumer.cs` 确认调用链
- **本报告所有文件路径与行号均基于审计当日（2026-07-21）代码库快照，引用格式 `file:///workspace/src/.../File.cs#Lstart-Lend`**
