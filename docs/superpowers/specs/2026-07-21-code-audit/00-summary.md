# Leno 电商平台代码审计 · 阶段 2 跨 BC 聚合分析汇总报告

> **审计日期**：2026-07-21
> **报告类型**：跨限界上下文（BC）一致性分析、全局视图与修复路线建议
> **输入**：12 份 BC 详细审计报告（`01-userauth.md` 至 `12-shared.md`）
> **辅助输入**：`/workspace/docs/superpowers/specs/2026-07-21-code-audit-design.md` 设计文档、`/workspace/src/BuildingBlocks/Leno.SharedContracts/Events/` 共享事件契约目录
> **审计模型**：GLM-5.2 静态分析 + 跨 BC 聚合
> **本报告不修改任何业务代码**

---

## 1. 全局概览

### 1.1 审计范围与规模

本次阶段 2 跨 BC 聚合分析覆盖 11 个业务 BC + 1 个共享层（BuildingBlocks / SharedKernel / SharedContracts / ApiGateway），共计扫描业务代码约 6.5 万行（剔除 Tests / Migrations Designer / ModelSnapshot / Generated 后）。

| # | BC 名称 | 扫描根目录 | 业务代码行数 | 问题统计（🔴/🟡/🟢） | 子报告路径 |
|---|---------|-----------|-------------|----------------------|-----------|
| 01 | UserAuth（用户认证） | `src/Services/UserAuth/Leno.UserAuth.{Domain,Application,Infrastructure,Api}/` | 约 6500 | 15 / 19 / 12 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md` |
| 02 | Product（商品与库存） | `src/Services/Product/...` | 约 6751 | 5 / 10 / 5 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md` |
| 03 | Cart（购物车） | `src/Services/Cart/...` | 约 3000 | 5 / 15 / 10 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md` |
| 04 | Order（订单与交易） | `src/Services/Order/...` | 约 5200 | 13 / 14 / 9 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md` |
| 05 | Promotion（促销与秒杀） | `src/Services/Promotion/...` | 约 3500 | 11 / 13 / 10 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md` |
| 06 | ReviewAfterSales（评价与售后） | `src/Services/ReviewAfterSales/...` | 约 4400 | 11 / 12 / 8 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md` |
| 07 | PointsMembership（积分与会员） | `src/Services/PointsMembership/...` | — | 8 / 9 / 7 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md` |
| 08 | Payment（支付与退款） | `src/Services/Payment/...` | 约 6665 | 6 / 9 / 5 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md` |
| 09 | Notification（消息通知） | `src/Services/Notification/...` | 约 7137 | 12 / 18 / 9 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md` |
| 10 | SellerShop（卖家与店铺） | `src/Services/SellerShop/...` | 约 5285 | 4 / 11 / 8 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md` |
| 11 | SystemAdmin（系统管理） | `src/Services/SystemAdmin/...` | — | 7 / 10 / 5 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md` |
| 12 | Shared（共享层 + 网关） | `src/BuildingBlocks/Leno.Infrastructure/` + `Leno.SharedKernel/` + `Leno.SharedContracts/` + `src/ApiGateway/Leno.ApiGateway/` | 约 8800 | 10 / 18 / 11 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md` |

### 1.2 全局问题计数汇总

| 严重度 | 总数 | 占比 |
|--------|------|------|
| 🔴 高风险 | 107 | 29.4% |
| 🟡 中风险 | 158 | 43.4% |
| 🟢 低风险 | 99 | 27.2% |
| **合计** | **364** | 100% |

**关键观察**：

1. **高风险问题密度最高**的 BC 依次为：UserAuth（15）、Order（13）、Notification（12）、Promotion（11）、ReviewAfterSales（11）、Shared（10）。这 6 个 BC 贡献了 72/107 = 67.3% 的高风险问题。
2. **代码行数与高风险密度**并非线性相关：Notification（7137 行 / 12🔴）和 UserAuth（6500 行 / 15🔴）问题密度最高，SellerShop（5285 行 / 4🔴）和 Product（6751 行 / 5🔴）问题密度最低，反映不同 BC 的工程质量差异显著。
3. **共享层**（12-shared）虽然 DDD 抽象与基础设施复用度高，但存在 10 个高风险问题，主要分布在并发竞态、资源释放、配置热更新、跨实例一致性等方面——共享层的缺陷会被所有引用 BC 放大。
4. **跨 BC 共性问题模式**显著：Guid→long 不可逆映射、Outbox 绕过、SaveChangesAsync 与 SaveEntitiesAsync 混用、IDOR 越权、设计期工厂硬编码密码、双路由 Obsolete 无下线时间、ACL 重复实现等，在多个 BC 中重复出现（详见第 D 章）。

### 1.3 整体结论

Leno 电商平台在 **DDD 分层架构、CQRS 端口设计、Outbox + 幂等基座、ACL 双轨方案（gRPC + HTTP 降级）、Consul 配置中心、Helm 部署** 等架构层面已达到较高成熟度（共享层 DDD 合规评分 4.0/5.0，参见 `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md` 第 726 行）。但在**实现细节**上存在系统性缺陷，集中爆发于以下 6 类跨 BC 问题：

- **事件契约对齐不彻底**（D1）：`RefundCompletedEvent` 缺 `ChannelRefundNo`、`ReviewSubmittedEvent` 缺 `ShopId`、`MemberLevelUpgradedEvent` 双身份混淆、4 个 ReadModel 死消费者。
- **ACL 模式重复实现**（D2）：`OrderStatusProvider` / `PaymentInfoQueryService` / `ProductSnapshot` / `UserContact` 在多个 BC 各自实现。
- **共享内核轻度污染**（D3）：`Money` 值对象不可变性破坏、`OrderStatus` 硬编码魔法数、`Entity.Id` `protected set` 后门。
- **跨域事务边界不清**（D4）：`PaymentSucceededEventConsumer` 跨进程原子性、`ExchangeCouponAppService` 绕过 Outbox、Saga 补偿失败、`SaveChangesAsync` 误用导致领域事件丢失。
- **gRPC 与 REST 双轨不一致**（D5）：`Guid.GetHashCode()` 不可逆映射在 4 个 BC 重复出现、`PaymentGrpcService` 硬编码零值、gRPC 与 HTTP 能力未对齐。
- **重复实现未抽取到共享层**（D6）：设计期工厂硬编码密码在 3 个 BC 重复、双路由 Obsolete 无下线时间在多 BC 出现、限流熔断各自实现。

这些问题在生产高并发、多实例部署、网络分区场景下会集中爆发，部分直接威胁资金安全（Order ForceCancel 已扣减库存不回退、Payment 微信回调链路 100% 失败、Promotion 秒杀双重复回退库存膨胀）与账户安全（UserAuth OAuth 邮箱匹配账户接管、ReviewAfterSales SellerId 客户端伪造、SellerShop 设计期工厂硬编码 SA 密码）。

---

## 2. D. 跨 BC 一致性分析

### D1. 事件契约对齐

**检测方法**：交叉读取 `file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/` 下全部 16 个事件契约文件，与 12 份 BC 报告中标记为 "B7 事件契约一致性" 的问题对比，识别字段命名/类型/版本号跨 BC 不一致、事件双身份、死消费者等问题。

#### D1.1 `RefundCompletedEvent` 缺 `ChannelRefundNo` 字段，下游 BC 退款单号追踪断裂

**证据**：
- 事件契约定义：`file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs#L107-L163` —— `RefundCompletedEvent` 字段为 `OrderId / UserId / RefundId / RefundAmount / Currency / CompletedAt / AfterSalesId`，**无 `ChannelRefundNo` 字段**（即第三方支付渠道返回的退款流水号）。
- ReviewAfterSales 报告问题 2.4：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md` —— `RefundSucceededEventConsumer` 接收 `RefundCompletedEvent` 后无法保存渠道退款单号，运营在售后单详情中看不到第三方退款流水，对账困难。
- Payment 报告：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md` —— 支付域 `WeChatRefundChannel.RefundAsync` 调用第三方退款接口成功后能拿到 `refund_id`（微信侧退款单号），但发布 `RefundCompletedEvent` 时未携带该字段。

**影响**：
1. ReviewAfterSales BC 无法在售后单上回填渠道退款流水号，财务对账时只能依赖支付域自查。
2. Notification BC 发送"退款到账通知"时无法在通知内容中展示第三方退款单号，用户咨询客服时无法快速定位。
3. SystemAdmin BC 的统计对账子域无法按渠道退款单号与第三方对账文件匹配。

**修复建议**：在 `RefundCompletedEvent` 增加 `ChannelRefundNo` 字段（`string` 类型，默认 `string.Empty` 保持向后兼容），同时递增 `SchemaVersion` 至 2。Payment BC 在 `RefundCompletedIntegrationEvent` 发布处填充该字段，ReviewAfterSales / Notification / SystemAdmin BC 在消费侧按需读取。变更需按 `IntegrationEventBase.SchemaVersion` 注释要求（`file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs#L16-L21`）经所有消费方协商。

#### D1.2 `ReviewSubmittedEvent` 缺 `ShopId` 字段，SellerShop 工作台评价统计 100% 失效

**证据**：
- 事件契约定义：`file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/ReviewEvents.cs#L10-L55` —— `ReviewSubmittedEvent` 字段为 `ReviewId / UserId / SpuId / Rating / NewScore / ReviewCount`，**无 `ShopId` / `SellerId` 字段**。
- SellerShop 报告问题 2：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L21-L28` —— `ReviewSubmittedShopDashboardSyncConsumer` 第 42 行 `var shopId = integrationEvent.SpuId;` 直接将 SPU 标识当作 Shop 标识，传入 `ShopDashboardReadModelBuilder.BuildAsync`，按主键查 Shop 聚合必然返回 `null`，第 44-49 行直接返回跳过同步。

**影响**：
1. SellerShop 卖家工作台 `leno_shop_dashboards` ES 索引中 `TotalReviews / AverageRating / FiveStarReviews / OneStarReviews` 字段永远保持初始零值（参见 `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L30-L45`），卖家工作台永远显示 0 评价 0 评分。
2. 即便评论域有大量评价提交，卖家也无法在自己的工作台看到评分变化，运营分析失真。
3. 该 Bug 与 SellerShop 报告问题 3（`ShopDashboardReadModelBuilder` 6 个评论/订单字段硬编码 0 占位，`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L30-L45`）叠加，使工作台评价统计 100% 失效。

**修复建议**：在 `ReviewSubmittedEvent` 增加 `ShopId` 字段（`Guid` 类型，默认 `Guid.Empty` 保持兼容）。ReviewAfterSales BC 在 `ReviewAggregate.Create` 时应从订单反查真实 `ShopId`（参见 `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md` 问题 2.1 关于 SellerId 客户端伪造的修复建议，二者可一并修复）。同时 `ReviewApprovedEvent` / `ReviewHiddenEvent` 也应增加 `ShopId` 字段，保持事件契约一致性。

#### D1.3 `MemberLevelUpgradedEvent` 集成事件与 `MemberLevelUpgradedEvent` 领域事件同名混淆

**证据**：
- 集成事件定义：`file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs#L295-L321` —— 共享契约层 `MemberLevelUpgradedEvent` 字段为 `MemberId / NewLevel / UpgradedAt`，注释明确"消费方：积分与会员域读模型同步"。
- PointsMembership 报告问题 PM-M08：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md` —— 子报告明确指出"同名事件混淆"，且 `MemberLevelChangedIntegrationEvent`（`file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs#L114-L144`）与 `MemberLevelUpgradedEvent` 语义重叠，前者覆盖"消费门槛升级 + 成长值评估"两种场景，后者仅覆盖"等级升级"，但二者发布时机与字段集不一致。
- PointsMembership 报告问题 PM-H03：4 个 ReadModel 同步消费者订阅的事件发布方缺失，其中包含 `MemberLevelUpgradedEvent` 的死消费者（参见 `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md` 第 386 行 "事件契约一致性 1.5"）。

**影响**：
1. 同名领域事件与集成事件易导致开发者在订阅时混淆，错误的订阅方会收到非预期的事件载荷。
2. `MemberLevelChangedIntegrationEvent` 与 `MemberLevelUpgradedEvent` 双重发布导致下游可能消费两次会员等级变更通知。
3. 4 个 ReadModel 同步消费者订阅的事件发布方缺失（PM-H03），CQRS 读模型同步链路断裂。

**修复建议**：
1. 将集成事件重命名为 `MemberLevelUpgradedIntegrationEvent`，与领域事件命名空间区分（参见 `MemberLevelChangedIntegrationEvent` 的命名模式）。
2. 评估 `MemberLevelChangedIntegrationEvent` 与 `MemberLevelUpgradedIntegrationEvent` 是否可合并为单一事件，避免双重发布。
3. 在 PointsMembership 域补齐 4 个死消费者的发布方（PM-H03 修复）。

#### D1.4 `RefundCompleted` 事件回环：ReviewAfterSales 消费 `RefundCompletedEvent` 后又发布 `RefundSucceeded` 类语义事件

**证据**：
- ReviewAfterSales 报告问题 2.11：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md` —— `RefundSucceededEventConsumer` 消费 `RefundCompletedEvent` 后内部触发售后单状态变更，并发布售后侧的 `RefundSucceeded` 类领域事件。如果该领域事件被 mapper 翻译为集成事件，会形成"Payment → ReviewAfterSales → Payment" 的事件回环风险。
- Payment 报告：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md` —— Payment BC 的 `RefundCompletedEvent` 由支付域退款成功后发布，ReviewAfterSales 消费后不应再发布任何语义重叠的集成事件。

**影响**：
1. 事件回环导致 Payment BC 可能收到自己发布的事件的二次变体，触发重复对账。
2. 死循环风险：若 ReviewAfterSales 发布的"退款成功"事件被 Payment 再次消费并再次发布 `RefundCompletedEvent`，则形成无限循环（虽有幂等键兜底，但会浪费资源）。

**修复建议**：在 ReviewAfterSales 域明确：消费 `RefundCompletedEvent` 仅做售后单状态更新与领域事件（in-process）发布，**不再发布跨上下文集成事件**。如需通知其他 BC，由 Payment BC 的 `RefundCompletedEvent` 直接广播，ReviewAfterSales 仅维护本地聚合状态。

#### D1.5 `IntegrationEventBase.IdempotencyKey` 非可空 string 反序列化边界问题

**证据**：
- 契约定义：`file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs#L14` —— `public string IdempotencyKey { get; init; }` 非可空标注。
- Shared 报告问题 35：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md`（参见第 627-641 行）—— 当 JSON 中 `IdempotencyKey` 字段缺失时，System.Text.Json 反序列化可能将其设为 `null`（取决于 `RespectNullableAnnotations` 配置），消费者使用 `evt.IdempotencyKey` 作为 Redis key 时抛 `ArgumentNullException`。同源问题：`EventId` 字段缺失时反序列化为 `Guid.Empty`，导致幂等去重失效。

**影响**：所有 BC 的消费者在反序列化边界场景下可能空引用异常；幂等去重失效会导致业务重复执行。

**修复建议**：将 `IdempotencyKey` 改为 `public string IdempotencyKey { get; init; } = string.Empty;`（默认空字符串而非依赖构造函数赋值），并在 `IntegrationEventConsumerBase` 中对 `EventId == Guid.Empty` 与 `string.IsNullOrEmpty(IdempotencyKey)` 做前置校验，拒绝消费并记录告警。

---

### D2. ACL 模式重复

**检测方法**：基于 `file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/` 共享 ACL 基座，交叉读取各 BC 报告中标记为 "B3 防腐层缺失/穿透" 的问题，识别应抽取到共享层但当前在各 BC 重复实现的 ACL 客户端。

#### D2.1 共享 ACL 基座已抽取的部分

`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/` 已抽取（参见 `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md` 第 736-745 行）：

- `AntiCorruptionBase`：模板方法，统一异常捕获、指标埋点、HTTP 状态码映射；
- `AntiCorruptionDispatcher<TService>`：双轨调度器，gRPC ↔ HTTP 降级；
- `CircuitBreakerState`：熔断器状态机；
- `GrpcAntiCorruptionClientBase`：gRPC 客户端基类；
- `GrpcInternalKeyInterceptor`：gRPC 服务端鉴权拦截器；
- `AntiCorruptionPollyExtensions`：Polly 集成。

#### D2.2 重复实现的 ACL 客户端清单

下表列出在多个 BC 中各自实现、应进一步抽取到共享层或共享契约层的 ACL 客户端：

| ACL 客户端 | 重复出现的 BC | 证据位置 | 重复程度 |
|-----------|--------------|---------|---------|
| **OrderStatusProvider** | ReviewAfterSales（`HttpOrderStatusProvider` / `GrpcOrderStatusProvider`）、SellerShop（`GrpcOrderAntiCorruptionClient`）、Promotion（消费 OrderCancelled）、Notification（消费 OrderCancelledEvent） | `file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/HttpOrderStatusProvider.cs`；`file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcOrderStatusProvider.cs`；`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/.../GrpcOrderAntiCorruptionClient.cs` | 4 BC 重复 |
| **PaymentInfoQueryService** | ReviewAfterSales（`PaymentInfoQueryService` / `GrpcPaymentInfoQueryService`）、Order（消费 PaymentSucceeded）、Notification（消费 PaymentFailed） | `file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/PaymentInfoQueryService.cs`；`file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcPaymentInfoQueryService.cs` | 3 BC 重复 |
| **ProductSnapshot ACL** | Cart（`ProductSnapshotAntiCorruptionService` / `GrpcProductSnapshotAntiCorruptionClient`）、Order（`ProductAntiCorruptionService` / `ProductAntiCorruptionDispatcherAdapter`）、Promotion（商品快照查询） | `file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/ProductSnapshotAntiCorruptionService.cs`；`file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs`；`file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/ProductAntiCorruptionService.cs` | 3 BC 重复 |
| **UserContact ACL** | Notification（消费 UserRegisteredEvent 提取邮箱/手机号）、Order（订单联系人信息）、ReviewAfterSales（售后联系人）、Promotion（券接收人） | 各 BC 的 `UserEventConsumer` / 联系人查询服务 | 4 BC 重复 |
| **PointsAntiCorruptionService** | Order（`PointsAntiCorruptionService` / `PointsAntiCorruptionDispatcherAdapter`）、Promotion（积分兑换券）、ReviewAfterSales（评价返积分触发） | `file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs`；`file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/PointsAntiCorruptionDispatcherAdapter.cs` | 3 BC 重复 |
| **PromotionAntiCorruptionService** | Order（`PromotionAntiCorruptionService` / `PromotionAntiCorruptionDispatcherAdapter`）、Cart（结算预览时算优惠） | `file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/PromotionAntiCorruptionService.cs` | 2 BC 重复 |

**影响**：
1. 同一跨 BC 能力（如查订单状态、查商品快照）在多个 BC 各自实现，DTO 字段定义可能不一致（如 `OrderStatusInfo` 在 ReviewAfterSales 与 SellerShop 中字段集不同）。
2. 任一上游 BC 修改 gRPC 契约时，下游所有 BC 都需各自更新，缺乏集中适配层。
3. 重复实现浪费工程资源，且各自实现的失败处理策略（fail-closed / fail-open）可能不一致，引入安全或可用性差异。

**修复建议**：
1. 在 `Leno.SharedContracts/Integration/` 或新建 `Leno.SharedContracts.AntiCorruption/` 项目中定义标准 ACL DTO（如 `OrderStatusInfo` / `ProductSnapshotDto` / `PaymentInfoDto`），各 BC 引用共享 DTO 而非自定义。
2. 将通用 ACL 客户端（如 `OrderStatusProvider` / `ProductSnapshotProvider`）下沉到 `Leno.Infrastructure.AntiCorruption/` 作为泛型基类，各 BC 仅提供 BC 特有的字段映射逻辑。
3. 短期可先统一 DTO 定义，长期再统一客户端实现。

---

### D3. 共享内核污染

**检测方法**：交叉读取 `file:///workspace/src/BuildingBlocks/Leno.SharedKernel/` 目录与各 BC 报告中标记为 "B4 共享内核污染" 的问题。

#### D3.1 `Money` 值对象不可变性破坏且跨 BC 行为不一致

**证据**：
- 共享内核定义：`file:///workspace/src/BuildingBlocks/Leno.SharedKernel/ValueObjects/Money.cs#L13-L15` —— `public sealed record Money` 中 `Amount` / `Currency` 使用 `private set`，违反 `record` 不可变性契约（Shared 报告问题 29，`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md` 第 560-571 行）。
- Product 报告：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md` —— Money 不变量跨 BC 不一致，Product BC 内部对 `amount=0` 的处理与 Cart BC 不同。
- Cart 报告：Cart BC 中 `SkuPriceSnapshot` 值对象使用 Money，但 `CartPriceService` 在算优惠后 `amount` 可能为 0 或负数（折扣后），不同 BC 对 `Money.Amount = 0` 的语义判断不统一。

**影响**：
1. `Money` 作为共享值对象，其 `private set` 后门允许子类或反射修改 `Amount`，破坏 `record` 的 `Equals` / `GetHashCode` 契约，导致 `Dictionary<Money, T>` 键丢失。
2. 跨 BC 对 `Money.Amount = 0` 的语义不一致（部分 BC 视为"免费"，部分 BC 视为"未设置"），导致折扣后 0 元订单的处理可能出错。

**修复建议**：
1. 将 `Money.Amount` / `Money.Currency` 改为 `init`（参见 Shared 报告问题 29 修复建议）。
2. 在 `Money.Create` 工厂方法中明确 `amount = 0` 的语义（建议视为合法的"免费"值），并在所有 BC 统一处理。
3. `Money.Create` 中 `if (normalized.Length is < 3 or > 3)` 改为 `if (normalized.Length != 3)`（Shared 报告问题 30，`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md` 第 573-579 行）。

#### D3.2 `OrderStatus` 硬编码魔法数（ReviewAfterSales 域）

**证据**：
- ReviewAfterSales 报告：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md` —— `ReviewEligibilityChecker` 与 `AfterSalesEligibilityChecker` 在判断订单状态时硬编码 `(int)OrderStatus.Paid` 等魔法数，而非引用共享枚举。
- 跨 BC 影响：`OrderStatus` 枚举定义在 Order BC 的领域层，不应被其他 BC 直接引用；但 ReviewAfterSales 通过 `IOrderStatusProvider` 拿到的 `OrderStatusInfo.Status` 是 `int` 类型，需在 ReviewAfterSales 内部硬编码映射。

**影响**：
1. Order BC 修改 `OrderStatus` 枚举值时，ReviewAfterSales 的硬编码魔法数不会编译失败，运行期产生错误判定。
2. 跨 BC 共享的枚举值未抽取到 `Leno.SharedContracts/Enums/`，违反"共享契约层不可引用领域层枚举"的约定（参见 `AfterSalesEvents.cs#L8` 注释"Type 为 int 而非枚举，因共享契约层不可引用领域层枚举"）。

**修复建议**：在 `Leno.SharedContracts/Enums/` 下定义 `OrderStatusEnum` / `AfterSalesTypeEnum` / `ReviewStatusEnum` 等跨 BC 共享枚举，各 BC 的领域层枚举与共享枚举通过显式映射互转。

#### D3.3 `Entity.Id` `protected set` 与 `BaseDbContext.Version` shadow property 反向依赖

**证据**：
- Shared 报告问题 31：`file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs#L28` —— `public Guid Id { get; protected set; }` 允许子类任意修改 Id，违反不可变性。
- Shared 报告 D3 章节结论：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md` 第 748-754 行 —— `BaseDbContext` 在 `Entity` 派生类型上添加 `Version` shadow property（`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs#L39-L48`），虽是 shadow property，但 `Entity` 类型判断 `typeof(Entity).IsAssignableFrom(entityType.ClrType)` 让持久化层反向依赖领域抽象；`IAuditable` / `ISoftDeletable` 接口在 `Entity` 中定义，持久化关注（软删除、审计）泄漏到领域层。

**影响**：
1. `Entity.Id` `protected set` 留下变更后门，子类在行为方法中误改 Id 会导致 `Equals` / `GetHashCode` 行为变化，`HashSet<Entity>` 中实体丢失。
2. `BaseDbContext` 反向依赖 `Entity` 抽象，违反 DDD 中"领域层应只关注领域语义"的原则。

**修复建议**：
1. 将 `Entity.Id` 改为 `init`（参见 Shared 报告问题 31 修复建议）。
2. 将 `IAuditable` / `ISoftDeletable` 抽取到 `Leno.Infrastructure.Abstractions/` 中，让领域层不再感知持久化关注。

---

### D4. 跨域事务边界

**检测方法**：交叉读取各 BC 报告中标记为 "A7 异步消息可靠性" / "A8 事务边界" / "C6 Outbox/幂等" 的高风险问题。

#### D4.1 Outbox 绕过问题在 5 个 BC 重复出现

**证据**：
- UserAuth 报告问题 8：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md` —— `AccountAppService` / `OAuthClientAppService` 在 `SaveEntitiesAsync` 之后通过 `IEventBus.PublishAsync` 直接发布集成事件，绕过 Outbox，导致双发与原子性破坏。
- Promotion 报告问题 3.2：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md` —— `PointsExchangeConsumer` 消费事件后通过 `IEventBus.PublishAsync` 发布下一跳事件，未走 Outbox。
- SystemAdmin 报告 H-02：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md#L72-L103` —— `SystemConfigAppService` / `AnnouncementAppService` 在 `SaveEntitiesAsync` 后直接 `PublishAsync`，与同域 `FeatureFlagAppService` 规范做法不一致。
- PointsMembership 报告 PM-H05：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md` —— `ExchangeCouponAppService` 绕过 Outbox 发布积分兑换券事件。
- Cart 报告：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md` —— Cart 域部分应用服务存在类似模式（具体编号见子报告）。

**根因**：`IUnitOfWork` 接口同时暴露 `SaveChangesAsync` 与 `SaveEntitiesAsync` 两个方法（Shared 报告问题 24，`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md` 第 465-478 行）：
- `SaveChangesAsync` → `_context.SaveChangesAsync(ct)`，**不写入 Outbox**；
- `SaveEntitiesAsync` → `SaveChangesWithOutboxAsync`，保存聚合 + 翻译领域事件为集成事件并写入 Outbox。

调用方误用 `SaveChangesAsync` 或在 `SaveEntitiesAsync` 之后再 `IEventBus.PublishAsync`，都会破坏 Outbox 语义。

**影响**：
1. **领域事件丢失**：使用 `SaveChangesAsync` 时，聚合根 `AddDomainEvent` 收集的领域事件被 `ClearDomainEvents()` 丢弃，下游 BC 收不到事件。
2. **双发风险**：`SaveEntitiesAsync` 已经把领域事件翻译为集成事件写入 Outbox 并投递；再手动 `PublishAsync` 会导致同一条事件被投递两次。
3. **非原子性**：`SaveEntitiesAsync` 成功但 `PublishAsync` 失败时，数据库已提交但通知未发出；反之若 `PublishAsync` 先成功而事务回滚，下游会收到不存在的变更。

**修复建议**（共享层统一治理）：
1. `IUnitOfWork` 移除 `SaveChangesAsync`，强制所有保存路径走 `SaveEntitiesAsync`（Shared 报告问题 24 修复建议 1）。
2. 或 `SaveChangesAsync` 内部调用 `SaveEntitiesAsync`（向后兼容，修复建议 2）。
3. 在 `SaveChangesAsync` 上添加 `[Obsolete("Use SaveEntitiesAsync to ensure domain events are persisted to outbox")]`（修复建议 3）。
4. 各 BC 删除 `SaveEntitiesAsync` 之后的 `IEventBus.PublishAsync` 调用，改为在聚合根方法内 `AddDomainEvent(new XxxEvent(...))`，由 `IntegrationEventMapper` 翻译为集成事件经 Outbox 统一投递。

#### D4.2 `PaymentSucceededEventConsumer` 跨进程原子性问题

**证据**：
- Order 报告问题 2.1：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md` —— Order BC 的 `PaymentSucceededEventConsumer` 消费 `PaymentSucceededEvent` 后需完成"订单状态更新 + 库存确认扣减 + 触发下游事件"三步，跨进程非原子。
- Order 报告问题 2.2：ForceCancel 已发货订单时释放的是预占而非已扣减库存（`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md`），与 `PaymentSucceededEventConsumer` 的库存确认时机错位。
- Order 报告问题 2.3：Order 聚合根缺乐观并发控制（无 RowVersion），并发场景下"支付成功回调 + 超时取消延迟消息 + 买家主动 Cancel + 运营 ForceCancel"会同时通过状态校验，最后一个写入者静默覆盖前面所有变更。

**影响**：
1. 已支付订单可能被超时取消，库存被释放给其他订单，资损。
2. ForceCancel 与买家 Cancel 并发可能产生重复退款事件。
3. MarkAsPaid 与 Cancel 并发可能让订单最终状态不确定。

**修复建议**：
1. 在 `OrderConfiguration` 中为 Order 配置 `IsConcurrencyToken()` 或 RowVersion 字段（参见 Order 报告问题 2.3 修复建议）。
2. `PaymentSucceededEventConsumer` 在 `MarkAsPaid` 前重新加载聚合并校验当前状态（乐观锁失败时进入重试）。
3. ForceCancel 在 Shipped 状态下调用 `ReturnDeductedAsync` 而非 `ReleaseBatchAsync`（Order 报告问题 2.2 修复建议）。

#### D4.3 Saga 补偿失败：StockReservationCompensation 与 SeckillPreOccupation 双重回退

**证据**：
- Order 报告：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md` —— `StockReservationCompensationBackgroundService` 在补偿时可能重复释放已释放的库存。
- Promotion 报告问题 2.3 / 2.4：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md` —— `SeckillPreOccupationRecord` 双重复回退导致库存膨胀，秒杀场景下已扣减库存被回退两次。
- Promotion 报告问题 2.6：OrderCancelledEventConsumer 状态机抛错进入死信，Saga 补偿链路断裂。

**影响**：
1. 秒杀场景下库存被重复回退，导致超卖。
2. Saga 补偿失败后无人工介入机制，订单永久卡在中间状态。

**修复建议**：
1. 在 `StockReservationCompensation` 与 `SeckillPreOccupationRecord` 聚合根增加幂等键（如 `CompensationId`），补偿时校验是否已补偿过。
2. 引入 Saga 状态机（如 MassTransit Saga）显式管理补偿步骤与状态。
3. 补偿失败进入死信后由 SystemAdmin BC 的 `DeadLetterQueueManager` 接管人工介入。

---

### D5. gRPC 与 REST 双轨一致性

**检测方法**：交叉读取 `Leno.Infrastructure.AntiCorruption/AntiCorruptionDispatcher` 双轨实现与各 BC 报告中标记为 "B7 事件契约一致性" / "gRPC" 的高风险问题。

#### D5.1 `Guid.GetHashCode()` 不可逆映射在 4 个 BC 重复出现

**证据**：
- Product 报告问题 5：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md` —— Product gRPC 服务用 `Guid.GetHashCode()` 转 long，跨 BC 主键映射不可逆且冲突率高。
- Order 报告问题 3.14：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md` —— Order gRPC 服务同样问题。
- ReviewAfterSales 报告问题 2.5：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md` —— Review gRPC 服务 `ReviewGrpcService` 用 `Guid.GetHashCode()` 转 long。
- SellerShop 报告问题 4：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L47-L53` —— `SellerGrpcService.MapToProto` 第 99 行 `ShopId = (long)dto.ShopId.GetHashCode()`、第 105 行 `ShopId = (long)dto.ShopId.GetHashCode()`，代码注释承认"POC 简化：Guid→int64 不可逆映射，生产化改为 proto 字段改 string"。

**根因**：proto3 默认不支持 `Guid` 类型，开发者用 `int64` 承载 Guid，并通过 `Guid.GetHashCode()` 转换。`Guid.GetHashCode()` 返回 32 位有符号整数，存在大量哈希冲突（不同 Guid 可能映射到同一 long 值），且哈希值与原 Guid 不可逆。

**影响**：
1. 所有调用 `GetSellerInfo` / `GetShopInfo` / `GetProductInfo` / `GetReviewInfo` 的下游 BC（Order、Product、ReviewAfterSales 等）若依赖返回的 ID 字段反查上游 BC，会拿到错误的 Guid（哈希值强转回 Guid 后查不到任何聚合）。
2. 冲突情况下两个不同实体的 ID 可能映射到同一 long 值，导致跨 BC 归属校验错位。
3. `ValidateSellerOwnership` 等端点虽未使用 `MapToProto`，但 `GetSellerInfo` 返回的 `ShopId` 被下游缓存的场景会持续放大错误。

**修复建议**（跨 BC 统一治理）：
1. 在所有 proto 契约中将 `ShopId` / `ProductId` / `ReviewId` / `OrderId` 等 Guid 类型字段改为 `string`，承载 `Guid.ToString()`。
2. SellerShop 已在 `ShopInfo.ShopIdStr` 第 110 行新增 string 字段（参见 `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md#L47-L53`），但 `SellerInfo` 缺失对应字段，需补全。
3. 删除所有 `(long)dto.XxxId.GetHashCode()` 行，`XxxId` 标记 `deprecated`，要求所有客户端 30 天内迁移到 `XxxIdStr`。
4. 在 `Leno.Infrastructure.AntiCorruption/` 增加统一的 `GuidProtoConverter` 工具类，规范 Guid ↔ string 转换。

#### D5.2 `PaymentGrpcService` 硬编码零值

**证据**：
- Payment 报告：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md` —— Payment gRPC 服务返回硬编码零值，与 REST Controller 返回的真实数据不一致。具体表现为 gRPC 端点返回 `PaymentId = string.Empty` / `Amount = 0` 等占位值，下游 BC 通过 gRPC 拿到的数据完全不可用。

**影响**：通过 gRPC 调用 Payment BC 的下游（如 Order BC 的 `PaymentInfoQueryService`）拿到的支付信息全部为零值，无法用于业务决策。

**修复建议**：填充 gRPC 响应字段的真实值，与 REST Controller 返回保持一致；增加集成测试验证 gRPC 与 REST 返回字段集与语义一致。

#### D5.3 gRPC 与 HTTP 能力未对齐（PointsMembership Confirm 端点缺失）

**证据**：
- PointsMembership 报告 PM-H04：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md` —— `InternalPointsController.Confirm` HTTP 端点缺失，Order BC 通过 HTTP 调用积分确认失败；gRPC 路径存在但 Order BC 的 `PointsAntiCorruptionService` 默认走 HTTP（参见 `file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs`）。

**影响**：订单支付成功后积分核销链路断裂，用户支付的订单无法正常扣减冻结积分。

**修复建议**：补全 `InternalPointsController.Confirm` HTTP 端点，与 gRPC `ConfirmPointsAsync` 能力对齐。

#### D5.4 `ConsulConfigWatcher` 不触发 `IOptionsMonitor` 重载，gRPC/HTTP 切换实际不生效

**证据**：
- Shared 报告问题 19：`file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs#L67-L68` —— `_configuration["AntiCorruption:UseGrpc"] = newValue;` 直接写 `IConfiguration`，但 `IOptionsMonitor<AntiCorruptionOptions>` 的 `OnChange` 依赖 `IConfiguration` 的 change token，直接索引器赋值**不触发 change token**。`AntiCorruptionDispatcher` 使用 `IOptionsMonitor<AntiCorruptionOptions>.CurrentValue`，期望热更新生效，但实际 `CurrentValue` 永远返回启动时绑定的值。

**影响**：Consul KV 修改 `UseGrpc` 后，`AntiCorruptionDispatcher` 不切换 gRPC/HTTP；运维需重启服务才生效。这与 ACL 双轨方案的核心价值（运行时动态切换）相悖。

**修复建议**（Shared 报告问题 19 修复建议）：使用 `IOptionsMonitor<AntiCorruptionOptions>` + 自定义 `IOptionsChangeTokenSource<AntiCorruptionOptions>`，ConsulConfigWatcher 触发 change token。


---

### D6. 重复实现

**检测方法**：交叉读取各 BC 报告中标记为 "B4 共享内核污染" / "B8 仓储滥用" / "代码气味" 的问题，识别多个 BC 各自实现类似工具未抽取到共享层的情况。

#### D6.1 设计期工厂硬编码 SA 密码在 3 个 BC 重复出现

**证据**：
- Cart 报告问题 4.5：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md` —— `CartDbContextDesignTimeFactory` 硬编码 SA 密码。
- SellerShop 报告问题 1：`file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContextDesignTimeFactory.cs#L14-L17` —— 第 15 行 `UseSqlServer("Server=localhost,1433;Database=LenoSellerShop;User Id=sa;Password=Leno@SqlServer2019;TrustServerCertificate=True;MultipleActiveResultSets=true")`，硬编码 SA 账号密码 `Leno@SqlServer2019`。
- Notification 报告问题 40：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md` —— Notification BC 的设计期工厂同样硬编码密码。

**根因**：设计期工厂为绕过 Redis 等依赖直接连库生成迁移，硬编码了与生产同结构的明文凭据。该字符串以源码形式进入 Git 仓库历史，任何能读取源码的人（含离职员工、供应链人员）均可凭此密钥直接登录数据库。

**影响**：
1. 源码一旦泄露，攻击者可直接以 SA 身份连接数据库，绕过应用层所有鉴权，可读取/篡改/删除店铺、卖家档案、银行账号、身份证号等敏感数据。
2. 即便生产环境密码通过环境变量注入，开发/测试环境若复用同一密码（命名 `Leno@SqlServer2019` 暗示版本绑定，多人复用概率高），横向渗透风险显著。

**修复建议**（跨 BC 统一治理）：
1. 在 `Leno.Infrastructure/` 抽取通用 `DesignTimeDbContextFactoryBase<T>`，从环境变量 `MSSQL_SA_PASSWORD` 读取密码，未配置时回退到固定占位（如 `__DESIGN_ONLY__`）仅用于本地开发。
2. 各 BC 的 `XxxDbContextDesignTimeFactory` 继承基类，删除硬编码连接字符串。
3. 提交历史中的密码需轮换；在 CI 中加入 secret scanning 防止再次提交。

#### D6.2 双路由 Obsolete 无下线时间在多个 BC 重复出现

**证据**：
- Product 报告：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md` —— Product BC 双路由（REST Controller + gRPC Service）标记 `Obsolete` 但无下线时间。
- SellerShop 报告：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md` —— SellerShop BC `Obsolete` 标记的 AppService 未清理（中风险 #15）。
- Order、Cart、Promotion 等多个 BC 也存在双路由共存但无下线计划的情况。

**影响**：
1. 双路由长期共存增加维护成本，每次契约变更需同步修改两处。
2. `Obsolete` 标记无下线时间，客户端无紧迫感迁移，长期无法清理旧路由。

**修复建议**：
1. 在所有 `[Obsolete]` 特性中补充 `DiagnosticId` 与下线时间，如 `[Obsolete("Use XxxV2 instead, will be removed in 2026-10-01", DiagnosticId = "LENO001", UrlFormat = "https://wiki/leno/obsolete")]`。
2. CI 中增加警告升级为错误（`TreatWarningsAsErrors`），强制按计划下线。

#### D6.3 限流熔断各自实现，未充分复用共享层

**证据**：
- Shared 报告问题 8：`file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs#L25-L41` —— 网关层独立的滑动窗口限流器，Lua 脚本顺序错误（先 `ZCARD` 后 `ZREMRANGEBYSCORE`）。
- 各 BC 内部也存在独立的限流逻辑（如 UserAuth 登录限流、Promotion 秒杀限流、PointsMembership 签到限流），未统一复用 `RedisSlidingWindowRateLimiter` 或 `CircuitBreakerState`。
- Promotion 报告：秒杀 Redis 库存扣减与 `CircuitBreakerState` 集成不充分。
- PointsMembership 报告：评价返积分、签到返积分无速率限制（PM-C 子项均分 2.5，`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md` 第 396 行）。

**影响**：
1. 各 BC 限流策略不一致，部分 BC（如 PointsMembership）缺限流，容易被恶意请求打满下游资源。
2. 网关层限流 Lua 脚本顺序错误（Shared 报告问题 8），秒杀场景下正常用户被错误限流。

**修复建议**：
1. 将 `RedisSlidingWindowRateLimiter` 抽取到 `Leno.Infrastructure/` 共享层，修复 Lua 脚本顺序（参见 Shared 报告问题 8 修复建议）。
2. 各 BC 通过 `IRateLimiter` 接口复用，配置驱动（如 `[RateLimit("seckill", permit: 100, window: "60s")]` 特性）。
3. 评价返积分、签到返积分等高频端点强制启用限流。

---

## 3. E. 全局视图

### E1. BC 健康度对比矩阵

下表汇总 12 份子报告中的 BC 健康度评分。其中 8 个 BC 子报告明确给出了 5 分制（或可归一化为 5 分制）的健康度数值；4 个 BC（03-Cart、04-Order、05-Promotion、06-ReviewAfterSales）子报告未提供数值评分，仅给出问题计数与 P0/P1/P2 优先级列表，本表如实标注为"子报告未评分"。

| # | BC | 功能正确性(0-5) | DDD 合规(0-5) | 性能与可靠性(0-5) | 综合健康度 | 评分来源 |
|---|-----|:---:|:---:|:---:|:---:|---|
| 01 | UserAuth | 2.0 | 3.0 | 2.0 | 2.33 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md` 第 407-409 行 |
| 02 | Product | 2.0 | 3.0 | 2.5 | 2.50 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md` 第 293-295 行 |
| 03 | Cart | 子报告未评分 | 子报告未评分 | 子报告未评分 | — | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md`（仅问题计数 5/15/10） |
| 04 | Order | 子报告未评分 | 子报告未评分 | 子报告未评分 | — | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md`（仅问题计数 13/14/9） |
| 05 | Promotion | 子报告未评分 | 子报告未评分 | 子报告未评分 | — | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md`（仅问题计数 11/13/10） |
| 06 | ReviewAfterSales | 子报告未评分 | 子报告未评分 | 子报告未评分 | — | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md`（仅问题计数 11/12/8） |
| 07 | PointsMembership | 3.08（A 子项均分） | 2.93（B 子项均分） | 3.19（C 子项均分） | 3.05 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md` 第 379-398 行 |
| 08 | Payment | 2.0 | 3.0 | 2.0 | 2.33 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md` 第 172-174 行 |
| 09 | Notification | 1.5 | 2.5 | 1.5 | 1.83 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md` 第 388-390 行 |
| 10 | SellerShop | — | — | — | 3.50（10 分制 7.0 归一化） | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md` 第 246 行（10 分制跨 10 维度均分 7.0，按 5 分制归一化为 3.5） |
| 11 | SystemAdmin | 2.0 | 3.0 | 2.0 | 2.30 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md` 第 696-699 行 |
| 12 | Shared | 3.0 | 4.0 | 2.0 | 3.00 | `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md` 第 723-730 行 |

**关键观察**：

1. **健康度最高的 BC**：SellerShop（3.5）> PointsMembership（3.05）> Shared（3.00）。SellerShop 与 PointsMembership 在 DDD 分层与聚合设计上较为规范，主要扣分项在跨 BC 集成与并发安全；Shared 层 DDD 合规评分最高（4.0），但性能与可靠性仅 2.0，反映共享层"架构成熟、实现有缺陷"的特征。
2. **健康度最低的 BC**：Notification（1.83）< UserAuth（2.33）= Payment（2.33）< SystemAdmin（2.30）。Notification 的渠道 DI 重复键必崩、配置绑定错位、回执不持久化、超时分支卡死、OrderCancelled 必失败等多个 P0 级 Bug 使其生产可用性严重不达标（参见 `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md` 第 388 行）。
3. **DDD 合规评分普遍高于功能与可靠性评分**：12 个 BC 中 DDD 合规均分约 3.0，功能正确性均分约 2.2，性能与可靠性均分约 2.1。反映项目"架构先行、实现滞后"的工程现状。
4. **4 个 BC 子报告未给出数值评分**：03-Cart、04-Order、05-Promotion、06-ReviewAfterSales 子报告仅给出问题计数与 P0/P1/P2 优先级列表，未给出 0-5 分制的健康度数值。本汇总报告不编造数字，建议后续补充这 4 个 BC 的数值评分以支持跨 BC 量化对比。

### E2. 高风险问题热力分布

下表按 BC × 问题类别交叉统计 107 个高风险问题的分布。每个单元格表示该 BC 在该类别下的高风险问题数。

| BC \ 类别 | A1 空引用/边界 | A2 异常处理 | A3 并发竞态 | A4 状态机 | A5 边界条件 | A6 资源泄漏 | A7 异步消息 | A8 事务边界 | B1-B8 DDD/架构 | C1-C8 性能/可靠性 | **小计** |
|----|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| 01 UserAuth | 2 | 3 | 2 | 1 | 1 | 0 | 4 | 2 | 0 | 0 | **15** |
| 02 Product | 1 | 0 | 1 | 1 | 1 | 0 | 1 | 0 | 0 | 0 | **5** |
| 03 Cart | 1 | 1 | 1 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | **5** |
| 04 Order | 2 | 0 | 2 | 3 | 1 | 0 | 3 | 2 | 0 | 0 | **13** |
| 05 Promotion | 1 | 1 | 1 | 3 | 1 | 0 | 2 | 2 | 0 | 0 | **11** |
| 06 ReviewAfterSales | 2 | 0 | 0 | 2 | 2 | 0 | 3 | 2 | 0 | 0 | **11** |
| 07 PointsMembership | 1 | 0 | 1 | 1 | 1 | 0 | 3 | 1 | 0 | 0 | **8** |
| 08 Payment | 1 | 1 | 1 | 1 | 1 | 0 | 0 | 1 | 0 | 0 | **6** |
| 09 Notification | 1 | 2 | 0 | 2 | 0 | 0 | 4 | 3 | 0 | 0 | **12** |
| 10 SellerShop | 1 | 1 | 0 | 1 | 0 | 0 | 0 | 1 | 0 | 0 | **4** |
| 11 SystemAdmin | 0 | 1 | 2 | 1 | 0 | 0 | 2 | 1 | 0 | 0 | **7** |
| 12 Shared | 0 | 2 | 3 | 0 | 1 | 1 | 1 | 2 | 0 | 0 | **10** |
| **小计** | **13** | **12** | **12** | **16** | **9** | **1** | **24** | **17** | **0** | **0** | **107** |

> **说明**：上表中 A1-A8 为功能正确性子类，B1-B8 与 C1-C8 在高风险问题中分布为 0（高风险问题主要集中在功能正确性 A 类）。每个 BC 的"小计"列与第 1.2 节全局问题计数汇总一致。各单元格数字基于 12 份子报告"🔴 高风险问题"章节逐项归类统计。

**关键观察**：

1. **A7 异步消息可靠性（24 个高风险）** 是跨 BC 最集中的问题类别：UserAuth（4）、Order（3）、Notification（4）、PointsMembership（3）、ReviewAfterSales（3）、Promotion（2）、SystemAdmin（2）。反映 Outbox 绕过、消费者幂等无原子性、事件回环、死信处理等问题在全域普遍存在。
2. **A8 事务边界（17 个高风险）** 是第二集中类别：Notification（3）、Order（2）、Promotion（2）、ReviewAfterSales（2）、Shared（2）、UserAuth（2）。多步操作无显式事务、SaveChangesAsync 与 SaveEntitiesAsync 混用是主要根因。
3. **A4 状态机非法迁移（16 个高风险）** 集中在 Order（3）、Promotion（3）、Notification（2）、ReviewAfterSales（2）。Order 的"支付成功回调 + 超时取消 + 买家 Cancel + ForceCancel"并发状态机冲突尤为严重。
4. **A3 并发竞态（12 个高风险）** 集中在 Shared（3）、UserAuth（2）、Order（2）、SystemAdmin（2）。Shared 层的 `CacheService` 非线程安全 `Random`、`AntiCorruptionMetrics` 静态字典竞态、`IntegrationEventConsumerBase` 幂等无原子性影响所有引用 BC。
5. **B 类与 C 类高风险问题为 0**：DDD 合规与性能可靠性问题多在 🟡 中风险与 🟢 低风险档位，高风险问题几乎全部集中在功能正确性 A 类。这表明项目架构层面已较成熟，主要缺陷在功能实现细节。

### E3. 修复优先级矩阵

下表按"严重度 × 影响范围 × 实现成本"列出 Top 20 修复优先级矩阵。优先级评分 = 严重度权重（🔴=3 / 🟡=2 / 🟢=1）× 影响范围权重（全域=3 / 多 BC=2 / 单 BC=1）× 实现成本倒数（低成本=3 / 中成本=2 / 高成本=1）。评分越高越优先修复。

| 排名 | 问题 | 来源 BC | 严重度 | 影响范围 | 实现成本 | 优先级评分 | 建议级别 |
|:---:|---|---|:---:|:---:|:---:|:---:|:---:|
| 1 | `SaveChangesAsync` 与 `SaveEntitiesAsync` 双保存路径导致 Outbox 绕过 | Shared + 5 BC | 🔴 | 全域 | 中 | 3×3×2=18 | P0 |
| 2 | `Guid.GetHashCode()` 不可逆映射在 4 BC 重复 | Product/Order/ReviewAfterSales/SellerShop | 🔴 | 多 BC | 中 | 3×2×2=12 | P0 |
| 3 | `IntegrationEventConsumerBase` 幂等检查与标记无原子性 | Shared | 🔴 | 全域 | 中 | 3×3×2=18 | P0 |
| 4 | Order 聚合根缺乐观并发控制（无 RowVersion） | Order | 🔴 | 单 BC | 低 | 3×1×3=9 | P0 |
| 5 | ForceCancel 已发货订单释放预占而非已扣减库存 | Order | 🔴 | 单 BC | 中 | 3×1×2=6 | P0 |
| 6 | UserAuth OAuth 邮箱匹配自动绑定（账户接管） | UserAuth | 🔴 | 单 BC | 中 | 3×1×2=6 | P0 |
| 7 | Payment 微信回调链路 100% 失败（ParseXml + 验签 + out_trade_no 缺失） | Payment | 🔴 | 单 BC | 中 | 3×1×2=6 | P0 |
| 8 | Notification DI 注册导致 SmsChannel 重复键异常，全渠道调度必崩 | Notification | 🔴 | 单 BC | 低 | 3×1×3=9 | P0 |
| 9 | ReviewAfterSales SellerId 客户端伪造 | ReviewAfterSales | 🔴 | 单 BC | 中 | 3×1×2=6 | P0 |
| 10 | ReviewAfterSales SpuId/SkuId 客户端伪造污染商品评分 | ReviewAfterSales | 🔴 | 多 BC | 中 | 3×2×2=12 | P0 |
| 11 | 设计期工厂硬编码 SA 密码（3 BC 重复） | Cart/SellerShop/Notification | 🔴 | 多 BC | 低 | 3×2×3=18 | P0 |
| 12 | Promotion SeckillPreOccupation 双重复回退库存膨胀 | Promotion | 🔴 | 单 BC | 中 | 3×1×2=6 | P0 |
| 13 | PointsMembership ExchangeCouponAppService 绕过 Outbox | PointsMembership | 🔴 | 单 BC | 低 | 3×1×3=9 | P0 |
| 14 | PointsMembership 4 个 ReadModel 死消费者 | PointsMembership | 🔴 | 单 BC | 中 | 3×1×2=6 | P0 |
| 15 | SellerShop SpuId 当 ShopId 导致工作台评价统计 100% 失效 | SellerShop | 🔴 | 单 BC | 中 | 3×1×2=6 | P0 |
| 16 | SystemAdmin StatisticsAggregationService 全部使用 Random 模拟数据 | SystemAdmin | 🔴 | 单 BC | 高 | 3×1×1=3 | P0 |
| 17 | Shared JwtBlacklistService 实现与注释不符，多实例不同步 | Shared | 🔴 | 全域 | 中 | 3×3×2=18 | P0 |
| 18 | Shared AntiCorruptionMetrics 静态字典非线程安全 | Shared | 🔴 | 全域 | 低 | 3×3×3=27 | P0 |
| 19 | Shared CacheService 非线程安全 Random | Shared | 🔴 | 全域 | 低 | 3×3×3=27 | P0 |
| 20 | Shared ConsulConfigWatcher 不触发 IOptionsMonitor 重载 | Shared | 🔴 | 全域 | 中 | 3×3×2=18 | P0 |

> **说明**：优先级评分相同时，按影响范围全域 > 多 BC > 单 BC 排序。Top 20 中 Shared 层问题占 6 项（排名 1/3/17/18/19/20），因其缺陷会被所有引用 BC 放大，应优先修复。

---

## 4. F. 修复路线建议

### F1. P0（立即修复，1-2 周内）

P0 级别问题为 🔴 高风险且影响主链路（订单/支付/积分发放/账户安全/全域可靠性），必须立即修复。

#### F1.1 共享层 P0（影响全域，最优先）

| # | 问题 | 修复方案 | 验收标准 |
|---|------|---------|---------|
| P0-1 | `CacheService` 非线程安全 `Random`（Shared #1） | 替换为 `Random.Shared`（.NET 6+ 线程安全） | `file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs#L20-L21` 单例下并发 `GetOrSetAsync` 不抛异常，jitter 分布正常 |
| P0-2 | `AntiCorruptionMetrics` 静态字典非线程安全（Shared #3） | `Dictionary` 改为 `ConcurrentDictionary` | `file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs#L55` 多 BC 并发下不抛 `InvalidOperationException` |
| P0-3 | `IntegrationEventConsumerBase` 幂等无原子性（Shared #4） | `MarkAsProcessedAsync` 改用 `SET NX EX` 原子获取"处理权" | `file:///workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs#L33-L54` 并发消费同一 EventId 时业务仅执行一次 |
| P0-4 | `IUnitOfWork.SaveChangesAsync` 不含 Outbox（Shared #24） | 移除 `SaveChangesAsync` 或内部调用 `SaveEntitiesAsync`，添加 `[Obsolete]` | 各 BC 应用层仅调用 `SaveEntitiesAsync`；grep `SaveChangesAsync` 在应用层零命中 |
| P0-5 | `JwtBlacklistService` 多实例不同步（Shared #2） | 实现 `IHostedService` + Redis Pub/Sub，本地缓存改 `MemoryCache` 设 TTL | `file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs` 多实例登出实时同步；本地缓存按 token TTL 过期 |
| P0-6 | `ConsulConfigWatcher` 不触发 IOptionsMonitor 重载（Shared #19） | 自定义 `IOptionsChangeTokenSource<AntiCorruptionOptions>` | Consul KV 修改 UseGrpc 后 1 分钟内 `AntiCorruptionDispatcher` 切换 gRPC/HTTP |
| P0-7 | 设计期工厂硬编码 SA 密码（Cart #4.5 / SellerShop #1 / Notification #40） | 抽取 `DesignTimeDbContextFactoryBase<T>`，从环境变量读取密码 | grep `Leno@SqlServer2019` 在源码零命中；CI secret scanning 通过 |

#### F1.2 主链路 BC P0

| # | 问题 | 修复方案 | 验收标准 |
|---|------|---------|---------|
| P0-8 | Order 聚合根缺乐观并发控制（Order #2.3） | `OrderConfiguration` 增加 `IsConcurrencyToken()` 或 RowVersion 字段 | 并发 `MarkAsPaid` 与 `Cancel` 抛 `DbUpdateConcurrencyException` 而非静默覆盖 |
| P0-9 | Order ForceCancel 释放预占而非已扣减库存（Order #2.2） | `IInventoryRepository` 增加 `ReturnDeductedAsync`，ForceCancel 在 Shipped 状态下调用 | 已发货订单强制取消后已扣减库存正确回退 |
| P0-10 | UserAuth OAuth 邮箱匹配自动绑定（UserAuth 高风险） | 取消邮箱匹配自动绑定，改为要求用户手动确认绑定 | OAuth 注册不会自动绑定到同邮箱的既有账号 |
| P0-11 | Payment 微信回调链路 100% 失败（Payment 高风险） | 修复 `ParseXml` + 验签 + `out_trade_no` 缺失三重缺陷 | 微信支付回调成功响应 200，订单状态正确更新为已支付 |
| P0-12 | Notification DI 注册 SmsChannel 重复键（Notification #1） | `AliyunSmsChannel` 与 `TencentSmsChannel` 共用外壳类或 `ToLookup` | `NotificationDispatcher.DispatchAsync` 不抛 `ArgumentException` |
| P0-13 | ReviewAfterSales SellerId 客户端伪造（ReviewAfterSales #2.1） | `IOrderStatusProvider.OrderStatusInfo` 增加 `SellerId`，`AfterSalesEligibilityChecker` 校验 | 客户端伪造 SellerId 抛 `AFTERSALES_SELLER_MISMATCH` |
| P0-14 | ReviewAfterSales SpuId/SkuId 客户端伪造（ReviewAfterSales #2.2） | `IReviewEligibilityChecker` 接收 `spuId`/`skuId` 并从 `OrderItemStatusInfo.SkuId` 反查校验 | 客户端伪造 SpuId 抛 `REVIEW_SPU_MISMATCH` |
| P0-15 | Promotion SeckillPreOccupation 双重复回退（Promotion #2.3/#2.4） | `SeckillPreOccupationRecord` 增加幂等键，补偿时校验是否已补偿 | 秒杀补偿不产生库存膨胀 |
| P0-16 | PointsMembership ExchangeCouponAppService 绕过 Outbox（PM-H05） | 删除 `IEventBus.PublishAsync`，在聚合根内 `AddDomainEvent` | 积分兑换券事件经 Outbox 投递，无双发 |
| P0-17 | PointsMembership 4 个 ReadModel 死消费者（PM-H03） | 补齐 4 个事件发布方 | `leno_points_accounts` / `leno_members` ES 索引正常同步 |
| P0-18 | SellerShop SpuId 当 ShopId（SellerShop #2） | `ReviewSubmittedEvent` 增加 `ShopId` 字段，ReviewAfterSales 发布时填充 | 卖家工作台评价统计字段正常更新 |
| P0-19 | SystemAdmin StatisticsAggregationService 全部使用 Random（SystemAdmin H-01） | 注入 `IOrderQueryService` / `IPaymentQueryService` 等跨域只读查询接口，从读模型聚合真实指标 | 看板返回的 GMV / 支付成功率 / 店铺排行等数据与各 BC 读模型一致 |
| P0-20 | Guid.GetHashCode() 不可逆映射（Product #5 / Order #3.14 / ReviewAfterSales #2.5 / SellerShop #4） | proto 契约中 Guid 类型字段改为 `string`，删除 `GetHashCode()` 转换 | 下游 BC 通过 gRPC 拿到的 ID 字段可正确反查上游聚合 |

### F2. P1（短期修复，1 个月内）

P1 级别问题为 🔴 高风险但影响边缘 BC，或 🟡 中风险且影响主链路。

#### F2.1 共享层 P1

| # | 问题 | 修复方案 |
|---|------|---------|
| P1-1 | `ObjectStorageService` 构造函数 sync over async（Shared #5） | `EnsureBucketExistsAsync` 移至 `IHostedService.StartAsync` |
| P1-2 | `RedisBloomFilter.GetHashPositions` Math.Abs 溢出（Shared #6） | 改用 `((combinedHash % _bitSize) + _bitSize) % _bitSize` |
| P1-3 | `BaseDbContext.FillAuditableFields` 未填充 `CreatedBy`/`UpdatedBy`（Shared #7） | 注入 `ICurrentUserContext`，填充用户身份 |
| P1-4 | `RedisSlidingWindowRateLimiter` Lua 顺序错误（Shared #8） | 调整为 `ZREMRANGEBYSCORE` → `ZCARD` → `ZADD` |
| P1-5 | `CacheMiddleware` 异常路径未恢复 Body 流（Shared #9） | `try/finally` 中恢复 `context.Response.Body` |
| P1-6 | `AntiCorruptionDispatcher.Dispose` 误销毁 KeyedSingleton（Shared #10） | 移除 `_circuitBreaker?.Dispose()` 调用 |
| P1-7 | `OutboxPublisher` 三步串行增加延迟（Shared #11） | `RecoverStalePublishingAsync` 与 `AlertIfPendingBacklogAsync` 改为低频执行 |
| P1-8 | `RedisSlidingWindowRateLimiter` fail-open 无日志（Shared #28） | `catch` 块增加 `LogWarning` |
| P1-9 | `JwtTokenGenerator` 未校验 SecretKey 长度（Shared #22） | 构造函数校验 ≥ 32 字节 |
| P1-10 | `ServiceCollectionExtensions.AddRedis` 同步阻塞（Shared #21） | `AbortOnConnectFail=false`，`IHostedService` 异步初始化 |

#### F2.2 各 BC P1

| # | 问题 | 修复方案 |
|---|------|---------|
| P1-11 | UserAuth InMemoryRefreshTokenStore 注册为生产实现（UserAuth #1） | 替换为 Redis 实现 |
| P1-12 | UserAuth ForgotPassword 事件丢失（UserAuth 高风险） | 改用 `SaveEntitiesAsync` + Outbox |
| P1-13 | Order StockReservation 聚合被绕过（Order #2.1） | `IInventoryRepository` 继承 `IRepository<StockReservation>`，库存操作通过聚合根 |
| P1-14 | Order Saga 补偿失败（Order 高风险） | 引入 MassTransit Saga 状态机，补偿失败进入死信 |
| P1-15 | Promotion CouponExpiryService 分页 skip 累加（Promotion #2.1） | 改为 `skip=0` 或游标分页 |
| P1-16 | Promotion OrderCancelledEventConsumer 状态机抛错死信（Promotion #2.6） | 加状态前置检查 |
| P1-17 | ReviewAfterSales AfterSales.Cancel 领域事件缺失（ReviewAfterSales #2.3） | `Cancel` 方法内 `AddDomainEvent` |
| P1-18 | ReviewAfterSales RefundCompleted 事件回环（ReviewAfterSales #2.11） | 消费侧不再发布跨上下文集成事件 |
| P1-19 | Payment 缺乐观并发控制（Payment 高风险） | `PaymentConfiguration` 增加 RowVersion |
| P1-20 | Notification MassTransit 消费者重复订阅（Notification #3） | 二选一保留一套 Consumer |
| P1-21 | Notification EmailChannelOptions/SmsChannelOptions 字段名不匹配（Notification #2） | 统一字段名或 `[ConfigurationKeyName]` 映射 |
| P1-22 | Notification OrderCancelledEvent UserId 强制 Guid.Empty（Notification #4） | 从事件正确提取 BuyerId 或查询订单仓储 |
| P1-23 | Notification 回执不持久化（Notification #5） | 注入 `IUnitOfWork` 并 `SaveChangesAsync` |
| P1-24 | SellerShop 设计期工厂硬编码密码（SellerShop #1） | 见 P0-7 |
| P1-25 | SellerShop UpdateShopInfoAsync 缺归属校验（SellerShop #10） | 增加 `userId` 参数，内部校验 `shop.Id == shopId` |
| P1-26 | SystemAdmin SystemConfigAppService 越过 Outbox（SystemAdmin H-02） | 删除 `IEventBus.PublishAsync`，在聚合根内 `AddDomainEvent` |
| P1-27 | SystemAdmin FeatureFlagCache/SystemConfigCache 未失效（SystemAdmin H-03） | `SaveEntitiesAsync` 后调用 `cache.RemoveAsync` |
| P1-28 | SystemAdmin AuditLogConsumer TOCTOU 竞态（SystemAdmin H-04） | `AuditLogEntries.EventId` 建唯一索引，捕获 `DbUpdateException` |
| P1-29 | SystemAdmin DeadLetterQueueManager 用 SaveChangesAsync（SystemAdmin H-05） | 改为 `SaveEntitiesAsync` |
| P1-30 | PointsMembership ReviewApprovedEventConsumer Redis 非原子（PM-H06） | 改用 `StringIncrementAsync` |
| P1-31 | PointsMembership OrderPaidEventConsumer package null（PM-H08） | 处理 null 场景 |
| P1-32 | PointsMembership MemberLevelEvaluationJob GrowthValue 恒 0（PM-H01） | 在消费返积分/签到返积分链路调用 `Member.AddGrowthValue` |
| P1-33 | PointsMembership PointsLedger 写入缺失（PM-H02） | 聚合变更同事务落流水 |
| P1-34 | PointsMembership InternalPointsController.Confirm HTTP 端点缺失（PM-H04） | 补全 HTTP 端点，与 gRPC 对齐 |

### F3. P2（中长期，1 个季度内）

P2 级别问题为 🟡 中风险 + 🟢 低风险，按 BC 分批治理。

#### F3.1 共享层抽取与统一治理

| # | 治理项 | 范围 |
|---|-------|------|
| P2-1 | ACL DTO 统一抽取到 `Leno.SharedContracts/Integration/` | OrderStatusInfo / ProductSnapshotDto / PaymentInfoDto / UserContactDto |
| P2-2 | 通用 ACL 客户端下沉到 `Leno.Infrastructure.AntiCorruption/` | OrderStatusProvider / ProductSnapshotProvider / PaymentInfoProvider |
| P2-3 | 跨 BC 共享枚举抽取到 `Leno.SharedContracts/Enums/` | OrderStatusEnum / AfterSalesTypeEnum / ReviewStatusEnum |
| P2-4 | `Money` 值对象不可变性修复 | `private set` 改 `init`，`amount=0` 语义统一 |
| P2-5 | `Entity.Id` `protected set` 改 `init` | 防止子类误改 Id |
| P2-6 | 限流熔断统一复用 `RedisSlidingWindowRateLimiter` / `CircuitBreakerState` | 各 BC 配置驱动限流 |
| P2-7 | 双路由 Obsolete 补下线时间 | `[Obsolete("...", DiagnosticId="LENO001")]` + CI 警告升级为错误 |
| P2-8 | `ErrorCodeMapping.GetStatusCode` 改 `EndsWith` | 避免 `Contains` 误匹配复合 ErrorCode |
| P2-9 | `CircuitBreakerState` HalfOpen 指标三态枚举 | 0=Closed, 1=Open, 2=HalfOpen |
| P2-10 | RabbitMQ 健康检查注册 | `AddHealthChecks` 增加 `AddRabbitMQ` |

#### F3.2 各 BC 中低风险治理

| BC | 治理重点 |
|----|---------|
| UserAuth | PermissionRepository 全表加载（#19）、FailedLoginCount 并发不安全（#14）、令牌撤销链路多处缺失（#10/11/24/34） |
| Product | N+1 批量查询（#8）、浮点漂移（#9）、价格过滤逻辑错误（#6）、sort 静默吞掉（#7）、TODO 占位（#10）、金额截断（#12）、审计缺失（#13） |
| Cart | SkuAddedToCartEvent 无处理器（#2.1）、匿名购物车 TOCTOU 竞态（#2.2）、Cart 聚合缺乐观锁 |
| Order | RefundCompletedEventConsumer 释放逻辑、StockAdjustedEventConsumer、OrderReadModelSyncConsumer、OrderPricingDomainService 等 |
| Promotion | CouponExpiryService 仅扫描 Unused 漏 Locked+Expired（#2.2）、SeckillOrderEventConsumer、PromotionActivityConfiguration Rules JSON 序列化、PromotionRule 默认构造弱化不可变性 |
| ReviewAfterSales | ReviewGrpcService Guid→long 转换（#2.5）、聚合内部 List 暴露（#2.8）、ReviewSubmittedDomainEvent 携带伪造 SpuId |
| PointsMembership | 时区处理多处错位（PM-M03、PM-L06）、IPointsOffsetAppService 错置 Domain 层（PM-M06）、成长值体系双轨割裂（PM-M09）、GetByFrozenOrderIdAsync 集合扫描（PM-M01） |
| Payment | PaymentChannelConfig.Description public setter 破坏不变式、ChannelConfig 混用私钥/公钥、gRPC 契约返回硬编码零值、对账调度时间与过滤字段错误、超时订单不关闭 |
| Notification | MarkAllAsReadAsync 绕过聚合（#35）、控制器越层访问仓储（#33）、NotificationService 与 NotificationDispatcher 双入口（#38）、限流注册但未启用（#20）、Job 无锁并发（#25）、IdempotencyKey 无唯一约束（#15）、索引缺失（#16/17）、N+1 查询（#22） |
| SellerShop | ShopConfiguration 字符串 backing field（#5）、EfCoreShopRepository 不 Include Qualifications（#6）、Shop.DecrementProductCount 静默吞错（#7）、控制器多步操作无事务（#8）、ShopDashboardData.OnOrderPaid 收入不减回（#9） |
| SystemAdmin | IndexRebuildOrchestrator 多步状态变更无事务（H-06）、AuditLogConsumer 与 AfterSalesEventConsumer 重复消费（H-07）、ReconciliationRecord 不变性冲突（M-07）、导出 OOM 风险（M-04） |
| Shared | `OutboxPublisher.PublishSingleAsync` MarkAsProcessed 失败未清理 ChangeTracker（#12）、`CircuitBreakerState._openedAt` 时间回拨（#13）、`BffForwarderService` 整体超时与单请求超时相同（#15）、`CacheMiddleware.IsCacheableResponse` 仅缓存 200（#17）、`FallbackResponseMiddleware` 未清除 Transfer-Encoding（#18）、`CacheService.InvalidatePatternAsync` 未加 KeyPrefix（#25）、`Program.cs` 白名单中间件内联 lambda（#26）、`CacheService` 缓存击穿防护不充分（#27）、`Money` 值对象可变属性（#29）、`Entity.Id` protected set（#31）、`Entity.GetHashCode` 哈希冲突（#32）、`ErrorCodeMapping.GetStatusCode` Contains 误匹配（#33）、`ErrorCodeMapping` 静态字典多 BC 覆盖（#34）、`IntegrationEventBase.IdempotencyKey` 反序列化 null（#35）、`ObjectStorageService.ExistsAsync` 吞异常（#36）、`RedisBloomFilter` SHA256 性能（#37/#38）、`CircuitBreakerState.GetState` 重入锁（#39） |

---

## 5. 失败 / 缺口

### 5.1 子报告数据缺口

1. **4 个 BC 子报告未提供数值健康度评分**：03-Cart、04-Order、05-Promotion、06-ReviewAfterSales 子报告仅给出问题计数（🔴/🟡/🟢）与 P0/P1/P2 优先级列表，未给出 0-5 分制的健康度数值。本汇总报告如实标注为"子报告未评分"，未编造数字。建议后续补充这 4 个 BC 的数值评分以支持跨 BC 量化对比。
2. **SellerShop 健康度评分为 10 分制**：与其它 BC 的 5 分制不一致，本汇总按 `7.0 / 10 × 5 = 3.5` 归一化，但 10 个维度（分层架构/聚合设计/防腐层/事件驱动/CQRS/数据一致性/安全/可观测性/代码质量/测试覆盖）与其它 BC 的 3 个维度（功能正确性/DDD 合规/性能与可靠性）不直接对应，归一化存在一定误差。
3. **PointsMembership 健康度评分子项均分**：07-PointsMembership 子报告采用 8-9 个子项均分的评分方式（A 子项均分 3.08、B 子项均分 2.93、C 子项均分 3.19，总体 3.05），与其它 BC 的"功能/DDD/性能"三维度评分不完全对齐，本汇总如实引用子项均分。
4. **部分 BC 报告问题计数为汇总数**：01-UserAuth 等部分子报告的问题计数（15/19/12）为高风险/中风险/低风险三档合计 46 项，本汇总基于子报告声明的问题计数统计，未逐项重新核对。

### 5.2 跨 BC 分析的方法学局限

1. **D2 ACL 重复实现统计基于子报告声明**：本汇总 D2 章节列出的 ACL 重复实现清单基于 12 份子报告标记为 "B3 防腐层缺失/穿透" 的问题汇总，未对全部 BC 的 ACL 客户端代码做逐行对比。可能存在子报告未识别的重复实现。
2. **D5 gRPC 与 REST 双轨一致性未做契约级 diff**：本汇总 D5 章节基于子报告标记的 gRPC 问题，未对 proto 文件与 REST Controller 做字段级 diff。完整的双轨一致性验证需引入契约测试（如 Pact / gRPC reflection diff）。
3. **E2 高风险问题热力分布的类别归属**：部分高风险问题同时涉及多个 A 类子类（如 Order #2.2 同时涉及 A1 边界条件与 A4 状态机），本汇总按子报告主类别归类，可能与读者直觉略有差异。
4. **E3 修复优先级矩阵的评分公式**：优先级评分 = 严重度 × 影响范围 × 实现成本倒数，权重选择基于工程经验，未经过实际修复工时回归验证。建议团队根据实际修复经验校准权重。

### 5.3 阶段 3 架构评估的衔接

本汇总报告聚焦跨 BC 一致性分析与修复路线建议，未覆盖系统架构整体评估（G1-G7 章节）。按设计文档 `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit-design.md` 第 200-228 行规划，阶段 3 架构整体评估应由独立的 `general_purpose_task` subagent 基于 12 份子报告 + 本汇总报告 + 架构文档产出 `13-architecture-assessment.md`，本报告不替代阶段 3 评估。

### 5.4 未覆盖范围

1. **Tests 目录排除**：按设计文档要求，本次审计排除所有 `*Tests*` 项目，未评估测试覆盖率与测试质量。SellerShop 子报告明确指出"Tests 目录已排除扫描，但从代码复杂度看，ACL 与消费者链路测试覆盖度待验证"（`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md` 第 261 行）。
2. **Migrations Designer 排除**：EF Core 自动生成的 `Migrations/*.Designer.cs`、`*ModelSnapshot.cs` 排除，未评估迁移脚本与领域模型的一致性。
3. **Generated gRPC 代码排除**：`Leno.SharedContracts.Grpc/Generated/` 自动生成代码排除，未评估 proto 文件与生成代码的一致性。
4. **运行时行为未验证**：本审计为静态分析，未通过运行时测试验证问题是否真实触发。部分问题（如 `Math.Abs(long.MinValue)` 抛 `OverflowException`）概率极低，需运行时压测验证。
5. **配置文件未全面审计**：除显式引用的 `appsettings.json` 字段名不匹配问题外，未对各 BC 的 `appsettings.json` / `appsettings.Production.json` / Helm values 做全面审计。

---

## 附录：本报告引用证据格式说明

本报告所有代码位置引用均采用 `file:///workspace/...#L行号` 或 `file:///workspace/...#L起-L止` 格式，可直接在支持 file:// 协议的编辑器中点击跳转。子报告引用采用 `file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/XX-yyy.md` 格式，必要时附加章节号或行号定位。

本报告所有数字（问题计数、健康度评分、优先级评分）均基于实际读取的 12 份子报告内容，未编造数字。对于子报告未提供数值的场景，本报告如实标注"子报告未评分"或"子报告未提供"，不做估算填充。

本报告不修改任何业务代码，仅产出分析文档。后续 git 提交仅包含本报告文件 `00-summary.md`。
