# 跨 BC 与架构级修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基于 00-summary.md（D1-D6 跨 BC 共性问题）+ 13-architecture-assessment.md（G3/G4/G5/G6 架构评估），制定跨 BC 共性问题与架构级问题的修复实施计划
**Architecture:** 跨 BC 协调层，治理事件契约对齐、ACL 模式重复、共享内核污染、跨域事务边界、gRPC/REST 双轨一致性、重复实现 6 大类共性问题 + 技术债 Top10 + 风险 Top5 + 优化方案落地
**Tech Stack:** .NET 10 + EF Core + MassTransit + RabbitMQ + Redis + gRPC + xUnit + FluentAssertions
**关联审计报告:** `docs/superpowers/specs/2026-07-21-code-audit/00-summary.md`（D+F 章节）+ `docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md`（G3/G4/G5/G6）

---

## 元数据
- 输入报告：00-summary.md（D1-D6）+ 13-architecture-assessment.md（G3/G4/G5/G6）
- 问题总数：D1-D6 共 22 子问题 + G4 技术债 Top10 + G6 风险 Top5 + G5 优化方案 12 项
- 已修复（跳过）：14 项（来自既有修复批次，覆盖 D2/D4/D6 部分）
- 本计划覆盖：35 项（22 D 子问题中 8 项已修复跳过；G4 Top10 中 4 项已修复跳过；G6 风险 5 项；G5 优化方案 12 项）

## 问题统计总览

| 类别 | 总数 | ALREADY-FIXED | VERIFIED-NOT-REPRODUCIBLE | 待修复 |
|------|------|---------------|---------------------------|--------|
| D1 事件契约 | 5 | 0 | 0 | 5 |
| D2 ACL 模式重复 | 6 | 1（T17 部分） | 0 | 5 |
| D3 共享内核污染 | 3 | 0 | 0 | 3 |
| D4 跨域事务边界 | 3 | 2（T7/T8/T9/T13/T18） | 0 | 1 |
| D5 gRPC/REST 双轨 | 4 | 1（p0a-T6 gRPC 端） | 0 | 3 |
| D6 重复实现 | 3 | 0 | 0 | 3 |
| G4 技术债 Top10 | 10 | 4（TD1 部分/TD2/TD3 部分/TD4 部分） | 0 | 6 |
| G6 风险 Top5 | 5 | N/A | N/A | 5 |
| G5 优化方案 | 12 | N/A | N/A | 12 |
| **合计** | **71** | **8** | **0** | **63** |

## 已修复问题清单（[ALREADY-FIXED]）

以下既有修复批次已覆盖的跨 BC/架构相关问题，本计划跳过详细步骤：

| 既有编号 | 问题 | 关联 D/G 项 | 验证位置 | 状态 |
|---------|------|------------|---------|------|
| p0a-T2 | Order 域 `SeckillOrderCreationService.PublishFailedEventAsync` 占位补齐 | D1 事件契约 | Order.Infrastructure | [ALREADY-FIXED] |
| p0a-T5 | Promotion 域 `LockCouponAsync` 接口与 `internal/promotions/lock-coupon` 端点 | D2 ACL | Promotion.Api | [ALREADY-FIXED] |
| p0a-T6 | PointsMembership `PointsInternalAppService.ConfirmAsync` 占位补齐 + gRPC Confirm 真实调用 | D5.3（仅 gRPC 端） | PointsMembership.Application/Api | [ALREADY-FIXED]（HTTP 端点仍待补，见 D5.3） |
| p0a-T7 | SellerShop `ValidateOwnershipAsync` 占位补齐 + 防腐层扩展 | D2 ACL | SellerShop.Infrastructure | [ALREADY-FIXED] |
| T3 | Promotion 优惠券 Lock 流程贯通（含 ACL） | D2 ACL | Promotion.Api/Infrastructure | [ALREADY-FIXED] |
| T7 | Order 多卖家拆单 Saga 补偿 | D4.2 跨域事务边界 | Order.Application | [ALREADY-FIXED] |
| T8 | Order 单组下单库存/积分原子回滚 | D4.3 Saga 补偿 | Order.Application | [ALREADY-FIXED] |
| T9 | Order PayAsync 事件发布原子化（Outbox） | D4.1 Outbox 旁路 | Order.Domain | [ALREADY-FIXED] |
| T10 | PointsMembership 积分防腐层显式异常 | D2 ACL | Order.Infrastructure | [ALREADY-FIXED] |
| T13 | Shared Outbox 两阶段标记防重复发布 | D4.1 Outbox 旁路 | Leno.Infrastructure/Outbox | [ALREADY-FIXED] |
| T14 | Shared 消费者幂等强制（`IIdempotencyStore` + `RedisIdempotencyStore`） | D1.5 部分 + 跨 BC 基础设施 | Leno.Infrastructure/EventBus | [ALREADY-FIXED]（`IdempotencyKey` 默认值问题仍存在，见 D1.5） |
| T16 | Order Redis-DB 库存对账与秒杀回写 | D4.3 Saga 补偿 | Order.Infrastructure | [ALREADY-FIXED] |
| T17 | Shared 防腐层降级告警（AntiCorruptionMetrics） | D2 ACL 重复（仅指标侧） | Leno.Infrastructure/AntiCorruption | [ALREADY-FIXED]（仍待修复字典线程安全，见 fix-12 P0-T3） |
| T18 | Order 批量库存预占回滚补偿表 | D4.3 Saga 补偿 | Order.Infrastructure | [ALREADY-FIXED] |

## 问题清单总表

| # | 类别 | 问题标题 | 来源 | 优先级 | 状态 |
|---|------|---------|------|--------|------|
| 1 | D1 事件契约 | D1.1 `RefundCompletedEvent` 缺 `ChannelRefundNo` 字段 | 00-summary D1.1 | P0 | TODO |
| 2 | D1 事件契约 | D1.2 `ReviewSubmittedEvent` 缺 `ShopId` 字段 | 00-summary D1.2 | P0 | TODO |
| 3 | D1 事件契约 | D1.3 `MemberLevelUpgradedEvent` 同名混淆 | 00-summary D1.3 | P1 | TODO |
| 4 | D1 事件契约 | D1.4 `RefundCompleted` 事件回环风险 | 00-summary D1.4 | P1 | TODO |
| 5 | D1 事件契约 | D1.5 `IdempotencyKey` 非可空反序列化边界 | 00-summary D1.5 | P0 | TODO |
| 6 | D2 ACL 模式重复 | D2.1 `OrderStatusProvider` 4 BC 重复 | 00-summary D2.2 | P2 | TODO |
| 7 | D2 ACL 模式重复 | D2.2 `PaymentInfoQueryService` 3 BC 重复 | 00-summary D2.2 | P2 | TODO |
| 8 | D2 ACL 模式重复 | D2.3 `ProductSnapshot ACL` 3 BC 重复 | 00-summary D2.2 | P2 | TODO |
| 9 | D2 ACL 模式重复 | D2.4 `UserContact ACL` 4 BC 重复 | 00-summary D2.2 | P2 | TODO |
| 10 | D2 ACL 模式重复 | D2.5 `PointsAntiCorruptionService` 3 BC 重复 | 00-summary D2.2 | P2 | TODO |
| 11 | D2 ACL 模式重复 | D2.6 `PromotionAntiCorruptionService` 2 BC 重复 | 00-summary D2.2 | P2 | TODO |
| 12 | D3 共享内核污染 | D3.1 `Money` 值对象不可变性破坏 | 00-summary D3.1 | P1 | TODO |
| 13 | D3 共享内核污染 | D3.2 `OrderStatus` 硬编码魔法数 | 00-summary D3.2 | P1 | TODO |
| 14 | D3 共享内核污染 | D3.3 `Entity.Id` `protected set` 后门 | 00-summary D3.3 | P1 | TODO |
| 15 | D4 跨域事务边界 | D4.1 Outbox 旁路（5 BC 剩余） | 00-summary D4.1 | P0 | TODO |
| 16 | D4 跨域事务边界 | D4.2 `PaymentSucceededEventConsumer` 跨进程原子性 | 00-summary D4.2 | P1 | TODO |
| 17 | D4 跨域事务边界 | D4.3 Saga 补偿失败（剩余幂等键） | 00-summary D4.3 | P1 | TODO |
| 18 | D5 gRPC/REST 双轨 | D5.1 `Guid.GetHashCode()` 不可逆映射 | 00-summary D5.1 | P0 | TODO |
| 19 | D5 gRPC/REST 双轨 | D5.2 `PaymentGrpcService` 硬编码零值 | 00-summary D5.2 | P1 | TODO |
| 20 | D5 gRPC/REST 双轨 | D5.3 PointsMembership Confirm HTTP 端点缺失 | 00-summary D5.3 | P0 | TODO |
| 21 | D5 gRPC/REST 双轨 | D5.4 `ConsulConfigWatcher` 不触发 IOptionsMonitor 重载 | 00-summary D5.4 | P1 | TODO |
| 22 | D6 重复实现 | D6.1 设计期工厂硬编码 SA 密码（11 BC） | 00-summary D6.1 | P0 | TODO |
| 23 | D6 重复实现 | D6.2 双路由 Obsolete 无下线时间 | 00-summary D6.2 | P2 | TODO |
| 24 | D6 重复实现 | D6.3 限流熔断各自实现 | 00-summary D6.3 | P2 | TODO |
| 25 | G4 技术债 | TD1 Outbox 旁路修复（剩余 BC） | 13 G4 象限 I | P0 | TODO（与 D4.1 合并） |
| 26 | G4 技术债 | TD2 静态状态竞态加锁 | 13 G4 象限 I | P0 | [ALREADY-FIXED]（fix-12 P0-T1/T3） |
| 27 | G4 技术债 | TD3 DesignTime 密码外部化 | 13 G4 象限 I | P0 | TODO（与 D6.1 合并） |
| 28 | G4 技术债 | TD4 IDOR 归属校验补全 | 13 G4 象限 I | P0 | TODO |
| 29 | G4 技术债 | TD5 Guid→string 迁移 | 13 G4 象限 II | P1 | TODO（与 D5.1 合并） |
| 30 | G4 技术债 | TD6 跨域 Saga 编排补全 | 13 G4 象限 II | P1 | TODO |
| 31 | G4 技术债 | TD7 共享内核 Money 标准化 | 13 G4 象限 II | P1 | TODO（与 D3.1 合并） |
| 32 | G4 技术债 | TD8 死消费者清理 | 13 G4 象限 III | P1 | TODO |
| 33 | G4 技术债 | TD9 ACL 适配器样板代码生成 | 13 G4 象限 III | P2 | TODO |
| 34 | G4 技术债 | TD10 BFF 聚合层重构 | 13 G4 象限 IV | P2 | TODO |
| 35 | G6 风险 | R1 gRPC Guid→int64 碰撞 | 13 G6 | P0 | TODO（与 D5.1 合并） |
| 36 | G6 风险 | R2 Outbox 旁路分布式一致性故障 | 13 G6 | P0 | TODO（与 D4.1 合并） |
| 37 | G6 风险 | R3 IDOR 越权用户数据泄露 | 13 G6 | P0 | TODO（与 TD4 合并） |
| 38 | G6 风险 | R4 跨域 Saga 缺补偿动作 | 13 G6 | P1 | TODO（与 TD6 合并） |
| 39 | G6 风险 | R5 DesignTime SA 密码泄露 | 13 G6 | P0 | TODO（与 D6.1 合并） |
| 40 | G5 优化 | G5 短期 S1-S5 速赢修复 | 13 G5.1 | P0 | TODO |
| 41 | G5 优化 | G5 中期 M1-M4 战略性修复 | 13 G5.2 | P1 | TODO |
| 42 | G5 优化 | G5 长期 L1-L4 架构演进 | 13 G5.3 | P2 | TODO |

---

## D1-D6 跨 BC 共性问题修复计划

### D1: 事件契约对齐

#### P0-D1.1：`RefundCompletedEvent` 增加 `ChannelRefundNo` 字段

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L70-L82]
- **代码位置**：
  - 契约定义：[file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs#L107-L163]（缺 `ChannelRefundNo`）
  - 消费侧：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundSucceededEventConsumer.cs#L67]（`channelRefundNo: null` 硬编码 null）
- **根因**：`RefundCompletedEvent` 由 Payment BC 发布，但未携带第三方支付渠道退款流水号 `ChannelRefundNo`，ReviewAfterSales BC 消费时只能传 null，财务对账无渠道流水可查。
- **影响**：ReviewAfterSales 售后单详情缺渠道退款单号；Notification 退款到账通知无流水号；SystemAdmin 对账子域无法按渠道单号匹配第三方对账文件。
- **跨 BC 协调范围**：Payment（发布方）+ ReviewAfterSales/Notification/SystemAdmin（消费方）+ SharedContracts（契约层）。

##### Step 1: 写失败测试

在 `Leno.SharedContracts.Tests/Events/RefundCompletedEventTests.cs` 新建测试文件：

```csharp
// 文件：src/BuildingBlocks/Leno.SharedContracts.Tests/Events/RefundCompletedEventTests.cs
using Leno.SharedContracts.Events;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace Leno.SharedContracts.Tests.Events;

public class RefundCompletedEventTests
{
    [Fact]
    public void RefundCompletedEvent_ShouldHaveChannelRefundNoField()
    {
        // Arrange
        var evt = new RefundCompletedEvent(
            orderId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            refundId: Guid.NewGuid(),
            afterSalesId: Guid.NewGuid(),
            refundAmount: 100m,
            currency: "CNY",
            completedAt: DateTime.UtcNow);

        // Act
        var channelRefundNo = evt.ChannelRefundNo;

        // Assert — 字段存在且默认为 string.Empty（向后兼容）
        channelRefundNo.Should().BeEmpty("ChannelRefundNo 默认为空字符串以保持向后兼容");
    }

    [Fact]
    public void RefundCompletedEvent_WithChannelRefundNo_ShouldRoundTripThroughJson()
    {
        // Arrange
        var originalChannelRefundNo = "4200_2026072200001";
        var evt = new RefundCompletedEvent(
            orderId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            refundId: Guid.NewGuid(),
            refundAmount: 88.5m,
            currency: "CNY",
            completedAt: DateTime.UtcNow)
        {
            ChannelRefundNo = originalChannelRefundNo
        };

        // Act
        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<RefundCompletedEvent>(json)!;

        // Assert
        deserialized.ChannelRefundNo.Should().Be(originalChannelRefundNo,
            "ChannelRefundNo 应通过 JSON 序列化/反序列化保留");
    }

    [Fact]
    public void RefundCompletedEvent_OldJsonWithoutChannelRefundNo_ShouldDeserializeToEmpty()
    {
        // Arrange — 旧版事件 JSON 无 ChannelRefundNo 字段
        var oldJson = """{"OrderId":"00000000-0000-0000-0000-000000000001","UserId":"00000000-0000-0000-0000-000000000002","RefundId":"00000000-0000-0000-0000-000000000003","RefundAmount":50.0,"Currency":"CNY","CompletedAt":"2026-07-22T00:00:00Z","AfterSalesId":"00000000-0000-0000-0000-000000000004","EventId":"00000000-0000-0000-0000-000000000005","OccurredAt":"2026-07-22T00:00:00Z","IdempotencyKey":"k1","SchemaVersion":1}""";

        // Act
        var deserialized = JsonSerializer.Deserialize<RefundCompletedEvent>(oldJson)!;

        // Assert — 旧版 JSON 缺字段时反序列化为空字符串而非 null
        deserialized.ChannelRefundNo.Should().BeEmpty("旧版事件 JSON 缺 ChannelRefundNo 时应反序列化为空字符串");
    }

    [Fact]
    public void RefundCompletedEvent_SchemaVersion_ShouldBeIncrementedToTwo()
    {
        // Arrange & Act
        var evt = new RefundCompletedEvent();

        // Assert — SchemaVersion 默认为 1（构造函数未显式传入时）
        // 新增字段后，发布的版本号应可显式传入 2
        var evtV2 = new RefundCompletedEvent
        {
            SchemaVersion = 2,
            ChannelRefundNo = "R20260722001"
        };
        evtV2.SchemaVersion.Should().Be(2, "新增字段后 SchemaVersion 应递增到 2");
        evtV2.ChannelRefundNo.Should().Be("R20260722001");
    }
}
```

##### Step 2: 运行测试验证失败

Run:
```bash
dotnet test src/BuildingBlocks/Leno.SharedContracts.Tests/Leno.SharedContracts.Tests.csproj --filter "FullyQualifiedName~RefundCompletedEventTests"
```
Expected: FAIL — 编译错误：`RefundCompletedEvent` 不存在 `ChannelRefundNo` 字段。

##### Step 3: 写最小实现

修改契约文件 [file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs]，在 `RefundCompletedEvent` 中新增 `ChannelRefundNo` 字段：

```csharp
// 文件：src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs
// 在 RefundCompletedEvent 类中 AfterSalesId 字段后新增 ChannelRefundNo

public sealed class RefundCompletedEvent : IntegrationEventBase
{
    public Guid OrderId { get; init; }

    public Guid UserId { get; init; }

    public Guid RefundId { get; init; }

    public decimal RefundAmount { get; init; }

    public string Currency { get; init; } = "CNY";

    public DateTime CompletedAt { get; init; }

    public Guid AfterSalesId { get; init; }

    /// <summary>
    /// 第三方支付渠道返回的退款流水号（如微信 refund_id、支付宝 trade_no）。
    /// 默认 string.Empty 保持向后兼容；旧版消费方无需修改即可工作。
    /// 新版消费方按需读取用于财务对账与运营查询。
    /// </summary>
    public string ChannelRefundNo { get; init; } = string.Empty;

    public Guid AggregateId => RefundId;

    public RefundCompletedEvent() : base() { }

    public RefundCompletedEvent(Guid orderId, Guid userId, Guid refundId, decimal refundAmount, string currency, DateTime completedAt)
        : base()
    {
        OrderId = orderId;
        UserId = userId;
        RefundId = refundId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
    }

    public RefundCompletedEvent(Guid orderId, Guid userId, Guid refundId, Guid afterSalesId, decimal refundAmount, string currency, DateTime completedAt)
        : base()
    {
        OrderId = orderId;
        UserId = userId;
        RefundId = refundId;
        AfterSalesId = afterSalesId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// 带渠道退款流水号与售后单标识的构造重载，由支付域退款成功时发布。
    /// SchemaVersion 递增为 2 以标识新契约。
    /// </summary>
    public RefundCompletedEvent(Guid orderId, Guid userId, Guid refundId, Guid afterSalesId, decimal refundAmount, string currency, DateTime completedAt, string channelRefundNo)
        : base(eventId: null, occurredAt: null, idempotencyKey: null, schemaVersion: 2)
    {
        OrderId = orderId;
        UserId = userId;
        RefundId = refundId;
        AfterSalesId = afterSalesId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
        ChannelRefundNo = channelRefundNo ?? string.Empty;
    }
}
```

修改消费侧 [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundSucceededEventConsumer.cs#L67]，将 `channelRefundNo: null` 改为读取事件字段：

```csharp
// 第 67 行原代码：
// afterSales.MarkRefundCompleted(integrationEvent.RefundId, integrationEvent.RefundAmount, channelRefundNo: null);

// 替换为：
afterSales.MarkRefundCompleted(
    integrationEvent.RefundId,
    integrationEvent.RefundAmount,
    channelRefundNo: integrationEvent.ChannelRefundNo);
```

Payment BC 发布 `RefundCompletedEvent` 处需补 `channelRefundNo` 参数（需在 Payment.Infrastructure 的 `WeChatRefundChannel.RefundAsync` 等发布事件处补字段，详见 fix-08-payment.md）。

##### Step 4: 运行测试验证通过

Run:
```bash
dotnet test src/BuildingBlocks/Leno.SharedContracts.Tests/Leno.SharedContracts.Tests.csproj --filter "FullyQualifiedName~RefundCompletedEventTests"
```
Expected: PASS — 4 个测试全部通过。

##### Step 5: 提交

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs src/BuildingBlocks/Leno.SharedContracts.Tests/Events/RefundCompletedEventTests.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundSucceededEventConsumer.cs
git commit -m "fix(跨BC): RefundCompletedEvent 新增 ChannelRefundNo 字段并递增 SchemaVersion，售后域消费侧回填渠道退款流水号"
```

---

#### P0-D1.2：`ReviewSubmittedEvent` 增加 `ShopId` 字段

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L84-L95]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/ReviewEvents.cs#L10-L55]（缺 `ShopId`）
- **根因**：`ReviewSubmittedEvent` 字段为 `ReviewId / UserId / SpuId / Rating / NewScore / ReviewCount`，无 `ShopId`。SellerShop BC 的 `ReviewSubmittedShopDashboardSyncConsumer` 第 42 行 `var shopId = integrationEvent.SpuId;` 将 SPU 当作 Shop，导致工作台评价统计 100% 失效。
- **影响**：SellerShop 卖家工作台 `leno_shop_dashboards` ES 索引中评价字段永远为 0；运营分析失真。
- **跨 BC 协调范围**：ReviewAfterSales（发布方）+ SellerShop（消费方）+ SharedContracts（契约层）。同时 `ReviewApprovedEvent`/`ReviewHiddenEvent` 也需补 `ShopId` 字段保持一致。

##### Step 1: 写失败测试

```csharp
// 文件：src/BuildingBlocks/Leno.SharedContracts.Tests/Events/ReviewSubmittedEventTests.cs
using Leno.SharedContracts.Events;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace Leno.SharedContracts.Tests.Events;

public class ReviewSubmittedEventTests
{
    [Fact]
    public void ReviewSubmittedEvent_ShouldHaveShopIdField()
    {
        // Arrange
        var evt = new ReviewSubmittedEvent(
            reviewId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            spuId: Guid.NewGuid(),
            rating: 5);

        // Act
        var shopId = evt.ShopId;

        // Assert — 字段存在且默认为 Guid.Empty（向后兼容）
        shopId.Should().Be(Guid.Empty, "ShopId 默认为 Guid.Empty 以保持向后兼容");
    }

    [Fact]
    public void ReviewSubmittedEvent_WithShopId_ShouldRoundTripThroughJson()
    {
        // Arrange
        var shopId = Guid.NewGuid();
        var evt = new ReviewSubmittedEvent(
            reviewId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            spuId: Guid.NewGuid(),
            rating: 5)
        {
            ShopId = shopId
        };

        // Act
        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<ReviewSubmittedEvent>(json)!;

        // Assert
        deserialized.ShopId.Should().Be(shopId, "ShopId 应通过 JSON 序列化保留");
    }

    [Fact]
    public void ReviewApprovedEvent_ShouldAlsoHaveShopIdField()
    {
        var evt = new ReviewApprovedEvent(
            reviewId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            spuId: Guid.NewGuid(),
            rating: 4);
        evt.ShopId.Should().Be(Guid.Empty, "ReviewApprovedEvent 也应有 ShopId 字段，默认 Guid.Empty");
    }

    [Fact]
    public void ReviewHiddenEvent_ShouldAlsoHaveShopIdField()
    {
        var evt = new ReviewHiddenEvent(
            reviewId: Guid.NewGuid(),
            spuId: Guid.NewGuid(),
            rating: 1);
        evt.ShopId.Should().Be(Guid.Empty, "ReviewHiddenEvent 也应有 ShopId 字段，默认 Guid.Empty");
    }
}
```

##### Step 2: 运行测试验证失败

Run:
```bash
dotnet test src/BuildingBlocks/Leno.SharedContracts.Tests/Leno.SharedContracts.Tests.csproj --filter "FullyQualifiedName~ReviewSubmittedEventTests"
```
Expected: FAIL — 编译错误：`ReviewSubmittedEvent` / `ReviewApprovedEvent` / `ReviewHiddenEvent` 不存在 `ShopId` 字段。

##### Step 3: 写最小实现

修改 [file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/ReviewEvents.cs]，在三个事件类中均新增 `ShopId` 字段：

```csharp
// 文件：src/BuildingBlocks/Leno.SharedContracts/Events/ReviewEvents.cs
// 在 ReviewSubmittedEvent、ReviewApprovedEvent、ReviewHiddenEvent 三个类中
// 于 SpuId 字段后新增 ShopId 字段

public sealed class ReviewSubmittedEvent : IntegrationEventBase
{
    public Guid ReviewId { get; init; }
    public Guid UserId { get; init; }
    public Guid SpuId { get; init; }

    /// <summary>
    /// 店铺标识，由评价域在创建评价时从订单反查真实 ShopId 后填充。
    /// 默认 Guid.Empty 保持向后兼容；SellerShop BC 消费时按此字段同步工作台统计。
    /// </summary>
    public Guid ShopId { get; init; }

    public int Rating { get; init; }
    public double NewScore { get; init; }
    public int ReviewCount { get; init; }
    public Guid AggregateId => ReviewId;

    public ReviewSubmittedEvent() : base() { }

    public ReviewSubmittedEvent(Guid reviewId, Guid userId, Guid spuId, int rating) : base()
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        Rating = rating;
    }

    public ReviewSubmittedEvent(Guid reviewId, Guid userId, Guid spuId, int rating, double newScore, int reviewCount) : base()
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        Rating = rating;
        NewScore = newScore;
        ReviewCount = reviewCount;
    }

    /// <summary>带 ShopId 的构造重载，由评价域创建评价时发布。</summary>
    public ReviewSubmittedEvent(Guid reviewId, Guid userId, Guid spuId, Guid shopId, int rating, double newScore, int reviewCount)
        : base(eventId: null, occurredAt: null, idempotencyKey: null, schemaVersion: 2)
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        ShopId = shopId;
        Rating = rating;
        NewScore = newScore;
        ReviewCount = reviewCount;
    }
}

// ReviewApprovedEvent 与 ReviewHiddenEvent 同样新增 ShopId 字段（init 仅构造时赋值）
public sealed class ReviewApprovedEvent : IntegrationEventBase
{
    public Guid ReviewId { get; init; }
    public Guid UserId { get; init; }
    public Guid SpuId { get; init; }

    /// <summary>店铺标识，由评价域发布审核通过事件时填充。</summary>
    public Guid ShopId { get; init; }

    public int Rating { get; init; }
    public Guid AggregateId => ReviewId;

    public ReviewApprovedEvent() : base() { }

    public ReviewApprovedEvent(Guid reviewId, Guid userId, Guid spuId, int rating) : base()
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        Rating = rating;
    }

    public ReviewApprovedEvent(Guid reviewId, Guid userId, Guid spuId, Guid shopId, int rating)
        : base(eventId: null, occurredAt: null, idempotencyKey: null, schemaVersion: 2)
    {
        ReviewId = reviewId;
        UserId = userId;
        SpuId = spuId;
        ShopId = shopId;
        Rating = rating;
    }
}

public sealed class ReviewHiddenEvent : IntegrationEventBase
{
    public Guid ReviewId { get; init; }
    public Guid SpuId { get; init; }

    /// <summary>店铺标识，由评价域发布隐藏事件时填充。</summary>
    public Guid ShopId { get; init; }

    public int Rating { get; init; }
    public Guid AggregateId => ReviewId;

    public ReviewHiddenEvent() : base() { }

    public ReviewHiddenEvent(Guid reviewId, Guid spuId, int rating) : base()
    {
        ReviewId = reviewId;
        SpuId = spuId;
        Rating = rating;
    }

    public ReviewHiddenEvent(Guid reviewId, Guid spuId, Guid shopId, int rating)
        : base(eventId: null, occurredAt: null, idempotencyKey: null, schemaVersion: 2)
    {
        ReviewId = reviewId;
        SpuId = spuId;
        ShopId = shopId;
        Rating = rating;
    }
}
```

SellerShop BC 的 `ReviewSubmittedShopDashboardSyncConsumer` 第 42 行需修改为 `var shopId = integrationEvent.ShopId;`，由 fix-10-sellershop.md 跟进。

##### Step 4: 运行测试验证通过

Run:
```bash
dotnet test src/BuildingBlocks/Leno.SharedContracts.Tests/Leno.SharedContracts.Tests.csproj --filter "FullyQualifiedName~ReviewSubmittedEventTests"
```
Expected: PASS — 4 个测试全部通过。

##### Step 5: 提交

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Events/ReviewEvents.cs src/BuildingBlocks/Leno.SharedContracts.Tests/Events/ReviewSubmittedEventTests.cs
git commit -m "fix(跨BC): ReviewSubmittedEvent/ApprovedEvent/HiddenEvent 新增 ShopId 字段并递增 SchemaVersion，卖家工作台评价统计可正常同步"
```

---

#### P0-D1.5：`IntegrationEventBase.IdempotencyKey` 反序列化默认值修复

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L126-L134]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs#L14]（`public string IdempotencyKey { get; init; }` 无默认值）
- **根因**：`IdempotencyKey` 非可空且无默认值，旧版事件 JSON 缺该字段时 System.Text.Json 反序列化为 null（取决于 `RespectNullableAnnotations` 配置），消费侧用作 Redis key 抛 `ArgumentNullException`。
- **影响**：所有 BC 消费者反序列化旧版事件可能空引用；幂等去重失效。
- **跨 BC 协调范围**：SharedContracts（契约层）+ 所有 BC 消费侧（自动受益，无需改动）。

##### Step 1: 写失败测试

```csharp
// 文件：src/BuildingBlocks/Leno.SharedContracts.Tests/Events/IntegrationEventBaseTests.cs
using Leno.SharedContracts.Events;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace Leno.SharedContracts.Tests.Events;

public class IntegrationEventBaseTests
{
    [Fact]
    public void IdempotencyKey_DefaultValue_ShouldBeEmptyStringNotNull()
    {
        // Arrange & Act — 用无参构造创建子类实例
        var evt = new TestEvent();

        // Assert — 默认值应为 string.Empty 而非 null
        evt.IdempotencyKey.Should().NotBeNull("IdempotencyKey 不应为 null");
        evt.IdempotencyKey.Should().BeEmpty("无参构造时 IdempotencyKey 应为空字符串");
    }

    [Fact]
    public void IdempotencyKey_OldJsonWithoutField_ShouldDeserializeToEmpty()
    {
        // Arrange — 旧版 JSON 无 IdempotencyKey 字段
        var oldJson = """{"EventId":"00000000-0000-0000-0000-000000000001","OccurredAt":"2026-07-22T00:00:00Z","SchemaVersion":1}""";

        // Act
        var deserialized = JsonSerializer.Deserialize<TestEvent>(oldJson)!;

        // Assert — 旧版 JSON 缺字段时反序列化为空字符串而非 null
        deserialized.IdempotencyKey.Should().NotBeNull("反序列化后 IdempotencyKey 不应为 null");
        deserialized.IdempotencyKey.Should().BeEmpty("旧版事件缺 IdempotencyKey 字段时应反序列化为空字符串");
    }

    [Fact]
    public void EventId_DefaultValue_ShouldBeNewGuid()
    {
        // Arrange & Act
        var evt = new TestEvent();

        // Assert — EventId 应为新生成的 Guid，不应为 Guid.Empty
        evt.EventId.Should().NotBeEmpty("EventId 应为新生成的 Guid");
    }

    private sealed class TestEvent : IntegrationEventBase
    {
        public Guid AggregateId => EventId;
    }
}
```

##### Step 2: 运行测试验证失败

Run:
```bash
dotnet test src/BuildingBlocks/Leno.SharedContracts.Tests/Leno.SharedContracts.Tests.csproj --filter "FullyQualifiedName~IntegrationEventBaseTests"
```
Expected: FAIL — `IdempotencyKey_OldJsonWithoutField_ShouldDeserializeToEmpty` 测试失败：反序列化后 `IdempotencyKey` 为 null（因 `init` 无默认值且 JSON 缺字段）。

##### Step 3: 写最小实现

修改 [file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs]，将 `IdempotencyKey` 增加默认值 `= string.Empty`：

```csharp
// 文件：src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs
// 第 14 行原代码：
// public string IdempotencyKey { get; init; }
// 替换为：

/// <summary>
/// 幂等键，用于消费者去重。
/// 默认 string.Empty 保持向后兼容：旧版事件 JSON 缺该字段时反序列化为空字符串而非 null。
/// 消费侧应使用 <see cref="string.IsNullOrEmpty"/> 校验并回退到 <see cref="EventId"/> 作为幂等键。
/// </summary>
public string IdempotencyKey { get; init; } = string.Empty;
```

`IntegrationEventBase` 的无参构造函数已赋值 `IdempotencyKey = EventId.ToString()`，但 JSON 反序列化不走构造函数，因此 `init` 的默认值才生效。修改后旧版 JSON 反序列化时 `IdempotencyKey` 为 `string.Empty` 而非 null。

同时修改 `IntegrationEventConsumerBase.Consume` 在使用 `IdempotencyKey` 前做防御性校验（参见 fix-12-shared.md P0-T4 改造后的 `TryMarkAsProcessingAsync`，使用 `EventId` 作为幂等键主键）：

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs
// 在 Consume 方法开头增加前置校验
public async Task Consume(ConsumeContext<T> context)
{
    ArgumentNullException.ThrowIfNull(context);
    var evt = context.Message;

    // 前置校验：EventId 不能为 Guid.Empty，否则幂等去重失效
    if (evt.EventId == Guid.Empty)
    {
        Logger.LogWarning("集成事件 EventId 为 Guid.Empty，拒绝消费 Type={EventType}", typeof(T).Name);
        throw new InvalidOperationException($"集成事件 {typeof(T).Name} 的 EventId 为 Guid.Empty，无法保证幂等性");
    }

    // IdempotencyKey 为空时回退到 EventId（向后兼容旧版事件）
    var effectiveKey = string.IsNullOrEmpty(evt.IdempotencyKey) ? evt.EventId.ToString() : evt.IdempotencyKey;
    Logger.LogDebug("消费集成事件 EventId={EventId} IdempotencyKey={Key}", evt.EventId, effectiveKey);

    // 后续原子幂等检查逻辑保持不变（fix-12 P0-T4 已实施 TryMarkAsProcessingAsync）
    var acquired = await TryMarkAsProcessingAsync(evt.EventId, context.CancellationToken);
    if (!acquired)
    {
        Logger.LogInformation("事件已被其他消费者占用或已处理，跳过 EventId={EventId} Type={EventType}",
            evt.EventId, typeof(T).Name);
        return;
    }

    try
    {
        await HandleAsync(evt, context.CancellationToken);
    }
    catch
    {
        await ReleaseProcessingLockAsync(evt.EventId, context.CancellationToken);
        throw;
    }

    await MarkAsProcessedAsync(evt.EventId, context.CancellationToken);
}
```

##### Step 4: 运行测试验证通过

Run:
```bash
dotnet test src/BuildingBlocks/Leno.SharedContracts.Tests/Leno.SharedContracts.Tests.csproj --filter "FullyQualifiedName~IntegrationEventBaseTests"
```
Expected: PASS — 3 个测试全部通过。

##### Step 5: 提交

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs src/BuildingBlocks/Leno.SharedContracts.Tests/Events/IntegrationEventBaseTests.cs
git commit -m "fix(跨BC): IntegrationEventBase.IdempotencyKey 增加默认空字符串，消费侧前置校验 EventId 与幂等键回退"
```

---

### P1-D1.3：`MemberLevelUpgradedEvent` 重命名为 `MemberLevelUpgradedIntegrationEvent`

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L97-L112]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs#L295]（`public sealed class MemberLevelUpgradedEvent`）
- **根因**：集成事件 `MemberLevelUpgradedEvent` 与领域事件 `Leno.PointsMembership.Domain.Events.MemberLevelUpgradedEvent` 同名，开发者易混淆。
- **修复步骤**：
  1. 将集成事件重命名为 `MemberLevelUpgradedIntegrationEvent`，保留旧类名作为 `[Obsolete]` 别名以向后兼容
  2. 评估与 `MemberLevelChangedIntegrationEvent`（[file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs#L114]）合并的可行性
  3. PointsMembership 域补齐 4 个 ReadModel 死消费者的事件发布方（与 fix-07 协同）
- **影响范围**：PointsMembership 域发布方 + 各 BC 消费方（若有）
- **验证方法**：编译通过；PointsMembership 域 ES 索引 `leno_members` 正常同步

### P1-D1.4：消除 `RefundCompleted` 事件回环风险

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L114-L124]
- **代码位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundSucceededEventConsumer.cs]（消费 `RefundCompletedEvent` 后内部触发售后单状态变更）
- **根因**：若 ReviewAfterSales 消费 `RefundCompletedEvent` 后再发布跨上下文"退款成功"集成事件，会形成 Payment → ReviewAfterSales → Payment 回环。
- **修复步骤**：
  1. 在 ReviewAfterSales 域明确：消费 `RefundCompletedEvent` 仅做售后单状态更新与 in-process 领域事件发布，**不再发布跨上下文集成事件**
  2. 全文搜索 ReviewAfterSales.Infrastructure/Consumers/ 下 `PublishAsync` 调用，删除跨上下文事件发布
  3. 若需通知其他 BC，由 Payment BC 的 `RefundCompletedEvent` 直接广播
- **影响范围**：ReviewAfterSales 域消费者
- **验证方法**：单元测试验证消费 `RefundCompletedEvent` 后不发布任何 `IIntegrationEvent`

---

### D2: ACL 模式重复

> 共性根因：缺少跨 BC 的"公共能力下沉"机制，各 BC 各自实现一份相似 ACL 客户端。
> 治理策略：先统一 DTO 定义（P2 阶段），再统一客户端实现（P2 长期）。

### P2-D2.1：`OrderStatusProvider` 4 BC 重复 → 抽取共享 DTO

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L153-L174]
- **代码位置**：
  - ReviewAfterSales：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/HttpOrderStatusProvider.cs]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcOrderStatusProvider.cs]
  - ReviewAfterSales 域接口：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IOrderStatusProvider.cs]
- **修复步骤**：
  1. 在 `Leno.SharedContracts/Integration/Dto/` 新建 `OrderStatusInfoDto`（含 `OrderId / Status / SellerId / StatusText`）
  2. 各 BC 的 `OrderStatusInfo` 改为引用共享 DTO，删除自定义重复定义
  3. 通用 `OrderStatusProvider` 客户端下沉到 `Leno.Infrastructure.AntiCorruption/` 作为泛型基类
  4. 各 BC 仅提供 BC 特有的字段映射逻辑
- **影响范围**：ReviewAfterSales / SellerShop / Promotion / Notification 4 BC
- **验证方法**：编译通过；各 BC 单元测试通过

### P2-D2.2：`PaymentInfoQueryService` 3 BC 重复 → 抽取共享 DTO

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L160]
- **代码位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/PaymentInfoQueryService.cs]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcPaymentInfoQueryService.cs]
- **修复步骤**：
  1. 在 `Leno.SharedContracts/Integration/Dto/` 新建 `PaymentInfoDto`（含 `OrderId / PaymentId / Amount / Status / PaidAt`）
  2. 各 BC 的 `PaymentInfo` 改为引用共享 DTO
  3. 通用 `PaymentInfoProvider` 客户端下沉到 `Leno.Infrastructure.AntiCorruption/`
- **影响范围**：ReviewAfterSales / Order / Notification 3 BC
- **验证方法**：编译通过；单元测试通过

### P2-D2.3：`ProductSnapshot ACL` 3 BC 重复 → 抽取共享 DTO

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L161]
- **代码位置**：
  - Cart：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/ProductSnapshotAntiCorruptionService.cs]、[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs]
  - Order：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/ProductAntiCorruptionService.cs]
- **修复步骤**：
  1. 在 `Leno.SharedContracts/Integration/Dto/` 新建 `ProductSnapshotDto`（含 `SkuId / SpuId / Name / Price / Stock / ImageUrl`）
  2. 各 BC 的 `ProductSnapshot` 改为引用共享 DTO
  3. 通用 `ProductSnapshotProvider` 客户端下沉到 `Leno.Infrastructure.AntiCorruption/`
- **影响范围**：Cart / Order / Promotion 3 BC
- **验证方法**：编译通过；单元测试通过

### P2-D2.4：`UserContact ACL` 4 BC 重复 → 抽取共享 DTO

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L162]
- **修复步骤**：
  1. 在 `Leno.SharedContracts/Integration/Dto/` 新建 `UserContactDto`（含 `UserId / Email / Phone / Nickname`）
  2. 各 BC 的 `UserEventConsumer` / 联系人查询服务改为引用共享 DTO
- **影响范围**：Notification / Order / ReviewAfterSales / Promotion 4 BC
- **验证方法**：编译通过；单元测试通过

### P2-D2.5：`PointsAntiCorruptionService` 3 BC 重复 → 抽取共享 DTO

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L163]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs]、[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/PointsAntiCorruptionDispatcherAdapter.cs]
- **修复步骤**：
  1. 在 `Leno.SharedContracts/Integration/Dto/` 新建 `PointsFreezeResultDto` / `PointsConfirmResultDto` / `PointsReleaseResultDto`
  2. 各 BC 的 `PointsAntiCorruptionService` 改为引用共享 DTO
- **影响范围**：Order / Promotion / ReviewAfterSales 3 BC
- **验证方法**：编译通过；单元测试通过

### P2-D2.6：`PromotionAntiCorruptionService` 2 BC 重复 → 抽取共享 DTO

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L164]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/PromotionAntiCorruptionService.cs]
- **修复步骤**：
  1. 在 `Leno.SharedContracts/Integration/Dto/` 新建 `DiscountCalculationResultDto` / `CouponLockResultDto`
  2. 各 BC 的 `PromotionAntiCorruptionService` 改为引用共享 DTO
- **影响范围**：Order / Cart 2 BC
- **验证方法**：编译通过；单元测试通过

---

### D3: 共享内核污染

### P1-D3.1：`Money` 值对象不可变性修复

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L182-L196]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.SharedKernel/ValueObjects/Money.cs#L13-L15]（`public decimal Amount { get; private set; }` / `public string Currency { get; private set; }`）
- **根因**：`record` 中 `private set` 破坏不可变性契约，允许子类或反射修改 `Amount`，导致 `Equals`/`GetHashCode` 行为异常。
- **修复步骤**：
  1. `Amount` / `Currency` 改为 `init`（仅构造时赋值）
  2. `Money.Create` 中 `if (normalized.Length is < 3 or > 3)` 改为 `if (normalized.Length != 3)`（可读性优化）
  3. `Money.Create` 中明确 `amount = 0` 的语义为"合法的免费值"
  4. EF Core 通过 backing field 或 `[JsonConstructor]` 支持反序列化
- **影响范围**：Product / Promotion / Order / Cart 4 BC（共享内核修复需跨 BC 回归测试）
- **验证方法**：单元测试验证 `Amount`/`Currency` init 后不可变；EF Core 反序列化通过

### P1-D3.2：跨 BC 共享枚举抽取到 `Leno.SharedContracts/Enums/`

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L198-L208]
- **根因**：`OrderStatus` 枚举定义在 Order BC 领域层，不应被其他 BC 直接引用；ReviewAfterSales 通过 `IOrderStatusProvider` 拿到的 `OrderStatusInfo.Status` 是 `int`，需在内部硬编码映射。
- **修复步骤**：
  1. 在 `Leno.SharedContracts/Enums/` 下定义 `OrderStatusEnum` / `AfterSalesTypeEnum` / `ReviewStatusEnum` 等跨 BC 共享枚举
  2. 各 BC 的领域层枚举与共享枚举通过显式映射互转
  3. ReviewAfterSales 的 `ReviewEligibilityChecker` 与 `AfterSalesEligibilityChecker` 改为引用共享枚举
- **影响范围**：Order / ReviewAfterSales / SellerShop 等 BC
- **验证方法**：编译通过；单元测试验证枚举映射正确

### P1-D3.3：`Entity.Id` `protected set` 改 `init` + 持久化关注抽取

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L210-L222]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs#L28]（`public Guid Id { get; protected set; }`）
- **根因**：`protected set` 留下变更后门，子类在行为方法中误改 Id 会导致 `Equals`/`GetHashCode` 行为变化。
- **修复步骤**：
  1. `Entity.Id` 改为 `public Guid Id { get; init; }`
  2. `IAuditable` / `ISoftDeletable` 抽取到 `Leno.Infrastructure.Abstractions/` 中，让领域层不再感知持久化关注
  3. `BaseDbContext` 通过 backing field 或 `init` 支持 EF Core 反序列化
- **影响范围**：所有 BC 实体继承 `Entity` 基类，需跨 BC 回归测试
- **验证方法**：单元测试验证 `Id` init 后不可变；EF Core 反序列化通过

---

### D4: 跨域事务边界

#### P0-D4.1：消除剩余 BC 的 Outbox 旁路（与 TD1 合并）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L226-L254]
- **代码位置**：
  - UserAuth：`AccountAppService` / `OAuthClientAppService` 在 `SaveEntitiesAsync` 之后通过 `IEventBus.PublishAsync` 直接发布
  - Promotion：`PointsExchangeConsumer` 消费事件后通过 `IEventBus.PublishAsync` 发布下一跳
  - SystemAdmin H-02：`SystemConfigAppService` / `AnnouncementAppService` 在 `SaveEntitiesAsync` 后直接 `PublishAsync`
  - PointsMembership PM-H05：`ExchangeCouponAppService` 绕过 Outbox（已在 T9/T13 修复 Order BC，但其他 BC 仍存在）
- **根因**：`IUnitOfWork` 同时暴露 `SaveChangesAsync`（不写 Outbox）与 `SaveEntitiesAsync`（写 Outbox），调用方误用导致领域事件丢失或双发。
- **影响**：业务事务与消息发送非原子，可能出现"业务提交了但消息丢失"或"业务回滚了但消息已发"。
- **跨 BC 协调范围**：Shared（`IUnitOfWork` 接口）+ UserAuth/Promotion/SystemAdmin/PointsMembership 4 BC 应用层。

##### Step 1: 写失败测试

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/UnitOfWorkOutboxBypassTests.cs
using Leno.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using FluentAssertions;
using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.EventBus;
using Microsoft.Extensions.Logging.Abstractions;

namespace Leno.Infrastructure.Tests.Persistence;

public class UnitOfWorkOutboxBypassTests
{
    [Fact]
    public async Task SaveChangesAsync_ShouldThrowObsoleteException_ToPreventBypass()
    {
        // Arrange — 准备 in-memory DbContext 与 UoW
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("uow-bypass-test")
            .Options;
        await using var context = new TestDbContext(options);
        var mockEventBus = new Mock<IEventBus>();
        var uow = new EfCoreUnitOfWork<TestDbContext>(context, mockEventBus.Object);

        // Act & Assert — SaveChangesAsync 应标记 [Obsolete] 并在编译期警告，运行期仍可调用但不写 Outbox
        // 通过 Roslyn 分析器禁止 Infrastructure 层调用 SaveChangesAsync（推荐）
        // 这里通过文档约定 + 编译警告方式验证
        var saveChangesMethod = typeof(EfCoreUnitOfWork<TestDbContext>)
            .GetMethod("SaveChangesAsync", new[] { typeof(CancellationToken) });

        // Assert — SaveChangesAsync 应有 [Obsolete] 特性
        var obsoleteAttr = saveChangesMethod!.GetCustomAttributes(typeof(ObsoleteAttribute), false);
        obsoleteAttr.Should().HaveCount(1, "SaveChangesAsync 应标记 [Obsolete] 警告旁路 Outbox");
        ((ObsoleteAttribute)obsoleteAttr[0]).Message.Should().Contain("SaveEntitiesAsync",
            "Obsolete 提示应引导使用 SaveEntitiesAsync");
    }

    [Fact]
    public async Task SaveEntitiesAsync_ShouldPersistOutboxMessages()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("uow-outbox-test")
            .Options;
        await using var context = new TestDbContext(options);
        var mockEventBus = new Mock<IEventBus>();
        var uow = new EfCoreUnitOfWork<TestDbContext>(context, mockEventBus.Object);

        var entity = new TestEntity { Name = "test" };
        context.Entities.Add(entity);

        // Act
        await uow.SaveEntitiesAsync(CancellationToken.None);

        // Assert — OutboxMessages 应有记录（领域事件被翻译为集成事件并写入 Outbox）
        context.OutboxMessages.Should().NotBeEmpty("SaveEntitiesAsync 应将领域事件翻译为集成事件写入 Outbox");
    }

    private sealed class TestDbContext : BaseDbContext
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();
        public TestDbContext(DbContextOptions options) : base(options) { }
    }

    private sealed class TestEntity : Leno.SharedKernel.Abstractions.Entity
    {
        public string Name { get; set; } = string.Empty;
    }
}
```

##### Step 2: 运行测试验证失败

Run:
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~UnitOfWorkOutboxBypassTests"
```
Expected: FAIL — `SaveChangesAsync` 未标记 `[Obsolete]`。

##### Step 3: 写最小实现

修改 [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs]，将 `SaveChangesAsync` 标记 `[Obsolete]` 并内部委托 `SaveEntitiesAsync`：

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs
// 在 SaveChangesAsync 方法上添加 [Obsolete] 特性，并内部调用 SaveEntitiesAsync

/// <summary>
/// 已废弃：使用 <see cref="SaveEntitiesAsync"/> 替代，确保领域事件经 Outbox 持久化。
/// 此方法保留仅为向后兼容，内部委托给 SaveEntitiesAsync。
/// </summary>
/// <param name="cancellationToken">取消令牌。</param>
[Obsolete("Use SaveEntitiesAsync to ensure domain events are persisted to outbox. 此方法旁路 Outbox 会导致事件丢失或双发。")]
public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    return SaveEntitiesAsync(cancellationToken);
}
```

各 BC 应用层删除 `SaveEntitiesAsync` 之后的 `IEventBus.PublishAsync` 调用（由分 BC 修复计划 fix-01/05/07/11 跟进）：

```text
UserAuth BC（fix-01）：
- AccountAppService.RegisterAsync：删除 _eventBus.PublishAsync(new UserRegisteredEvent(...))
- OAuthClientAppService.BindAsync：删除 _eventBus.PublishAsync(new OAuthBoundEvent(...))
改为：在聚合根内 AddDomainEvent(new UserRegisteredDomainEvent(...))

Promotion BC（fix-05）：
- PointsExchangeConsumer.ConsumeAsync：删除 _eventBus.PublishAsync(new CouponExchangedEvent(...))
改为：在 UserCoupon 聚合根内 AddDomainEvent

SystemAdmin BC（fix-11）：
- SystemConfigAppService.UpdateAsync：删除 _eventBus.PublishAsync(new SystemConfigChangedEvent(...))
- AnnouncementAppService.PublishAsync：删除 _eventBus.PublishAsync(new AnnouncementPublishedEvent(...))
改为：在聚合根内 AddDomainEvent

PointsMembership BC（fix-07）：
- ExchangeCouponAppService.ExchangeAsync：删除 _eventBus.PublishAsync(new CouponExchangedEvent(...))
改为：在 Member 聚合根内 AddDomainEvent
```

CI 增加 Roslyn 分析器禁止 Infrastructure 层直接调 `SaveChangesAsync`：

```xml
<!-- 文件：src/Directory.Build.props -->
<!-- 在现有 ItemGroup 中增加 analyzer 报警 -->
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.3.4">
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>
<!-- 文件：src/BuildingBlocks/Leno.Infrastructure/BannedSymbols.txt -->
T:System.Threading.Tasks.Task`1<...> M:Leno.Infrastructure.Persistence.EfCoreUnitOfWork`1.SaveChangesAsync(System.Threading.CancellationToken)
; Use SaveEntitiesAsync to ensure domain events are persisted to outbox
```

##### Step 4: 运行测试验证通过

Run:
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~UnitOfWorkOutboxBypassTests"
```
Expected: PASS — 2 个测试全部通过。

##### Step 5: 提交

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/UnitOfWorkOutboxBypassTests.cs src/Directory.Build.props src/BuildingBlocks/Leno.Infrastructure/BannedSymbols.txt
git commit -m "fix(跨BC): EfCoreUnitOfWork.SaveChangesAsync 标记 Obsolete 并委托 SaveEntitiesAsync，Roslyn 分析器禁止旁路 Outbox"
```

---

### P1-D4.2：`PaymentSucceededEventConsumer` 跨进程原子性 + Order 乐观锁

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L256-L271]
- **根因**：Order BC 的 `PaymentSucceededEventConsumer` 消费 `PaymentSucceededEvent` 后需完成"订单状态更新 + 库存确认扣减 + 触发下游事件"三步，跨进程非原子。Order 聚合根缺乐观并发控制（无 RowVersion）。
- **修复步骤**：
  1. 在 `OrderConfiguration` 中为 Order 配置 `IsConcurrencyToken()` 或 RowVersion 字段
  2. `PaymentSucceededEventConsumer` 在 `MarkAsPaid` 前重新加载聚合并校验当前状态（乐观锁失败时进入重试）
  3. ForceCancel 在 Shipped 状态下调用 `ReturnDeductedAsync` 而非 `ReleaseBatchAsync`
- **影响范围**：Order BC（详见 fix-04-order.md）
- **验证方法**：并发 `MarkAsPaid` 与 `Cancel` 抛 `DbUpdateConcurrencyException` 而非静默覆盖

### P1-D4.3：Saga 补偿幂等键补全

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L273-L288]
- **根因**：`StockReservationCompensation` 与 `SeckillPreOccupationRecord` 聚合根缺幂等键，补偿时可能重复释放已释放的库存。Order BC 的 T7/T8/T18 已修复 Saga 补偿，但 Promotion BC 的 `SeckillPreOccupationRecord` 双重复回退仍存在。
- **修复步骤**：
  1. `SeckillPreOccupationRecord` 聚合根增加 `CompensationId` 幂等键，补偿时校验是否已补偿过
  2. 引入 MassTransit Saga 状态机显式管理补偿步骤与状态（详见 TD6）
  3. 补偿失败进入死信后由 SystemAdmin BC 的 `DeadLetterQueueManager` 接管人工介入
- **影响范围**：Promotion BC（详见 fix-05-promotion.md）
- **验证方法**：秒杀补偿不产生库存膨胀

---

### D5: gRPC 与 REST 双轨一致性

#### P0-D5.1：`Guid.GetHashCode()` 不可逆映射治理（与 TD5/R1 合并）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L295-L314]
- **代码位置**（已验证存在）：
  - Product BC：[file:///workspace/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L142]（`SellerId = (long)dto.SellerId.GetHashCode()`）、L150/L167/L168/L174
  - Order BC：[file:///workspace/src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs#L79]（`SellerId = (long)sellerId.GetHashCode()`）、L107
  - ReviewAfterSales BC：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs#L78]（`SpuId = (long)dto.SpuId.GetHashCode()`）、L95
  - SellerShop BC：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs#L99]（`ShopId = (long)dto.ShopId.GetHashCode()`）、L105（已有 `ShopIdStr = dto.ShopId.ToString()` 在 L110，部分双写）
  - 消费侧 Cart：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcCartPriceService.cs#L54]、L63、[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs#L46]
  - 消费侧 Order：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs#L47]、[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/Grpc/GrpcPromotionAntiCorruptionClient.cs#L49]
- **根因**：proto3 默认不支持 `Guid` 类型，开发者用 `int64` 承载 Guid 并通过 `Guid.GetHashCode()` 转换。`GetHashCode()` 返回 32 位有符号整数，存在大量哈希冲突且与原 Guid 不可逆。
- **影响**：跨 BC ID 透传时可能错配对象，引发数据错乱（订单查到错误 SKU、评价归属错用户、店铺数据错乱）。
- **跨 BC 协调范围**：4 BC gRPC 服务端（Product/Order/ReviewAfterSales/SellerShop）+ 2 BC gRPC 客户端（Cart/Order）+ proto 契约层。

##### Step 1: 写失败测试

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/GuidProtoConverterTests.cs
using Leno.Infrastructure.AntiCorruption;
using Xunit;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class GuidProtoConverterTests
{
    [Fact]
    public void GuidToString_AndBack_ShouldRoundTrip()
    {
        // Arrange
        var originalGuid = Guid.NewGuid();

        // Act
        var str = GuidProtoConverter.ToString(originalGuid);
        var parsed = GuidProtoConverter.TryParse(str, out var resultGuid);

        // Assert
        parsed.Should().BeTrue("Guid 字符串应可解析回 Guid");
        resultGuid.Should().Be(originalGuid, "往返转换应保持一致");
    }

    [Fact]
    public void ToString_ShouldNotUseGetHashCode()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var str = GuidProtoConverter.ToString(guid);

        // Assert — 应为 Guid.ToString() 而非 GetHashCode 的数字表示
        str.Should().NotBe(guid.GetHashCode().ToString(),
            "不应使用 GetHashCode()，应为 Guid.ToString() 格式");
        str.Should().Be(guid.ToString("D"),
            "应为 Guid 的 D 格式（默认）");
    }

    [Fact]
    public void TryParse_InvalidString_ShouldReturnFalse()
    {
        // Arrange
        var invalid = "not-a-guid";

        // Act
        var parsed = GuidProtoConverter.TryParse(invalid, out var result);

        // Assert
        parsed.Should().BeFalse("无效字符串应返回 false");
        result.Should().Be(Guid.Empty, "无效字符串应返回 Guid.Empty");
    }

    [Fact]
    public void ToString_EmptyGuid_ShouldReturnEmptyString()
    {
        // Arrange & Act
        var str = GuidProtoConverter.ToString(Guid.Empty);

        // Assert
        str.Should().Be(Guid.Empty.ToString("D"),
            "Guid.Empty 应返回 00000000-0000-0000-0000-000000000000");
    }
}
```

##### Step 2: 运行测试验证失败

Run:
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~GuidProtoConverterTests"
```
Expected: FAIL — 编译错误：`GuidProtoConverter` 类不存在。

##### Step 3: 写最小实现

新建 `GuidProtoConverter` 工具类到 `Leno.Infrastructure.AntiCorruption/`：

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GuidProtoConverter.cs
namespace Leno.Infrastructure.AntiCorruption;

/// <summary>
/// Guid 与 proto string 字段之间的统一转换工具。
/// 替代历史 POC 阶段的 (long)guid.GetHashCode() 不可逆映射（ADR-0006/0007）。
/// 所有 gRPC 服务端填充 string xxx_id_str 字段时应使用此工具。
/// 所有 gRPC 客户端读取 string xxx_id_str 字段时应使用此工具解析。
/// </summary>
public static class GuidProtoConverter
{
    /// <summary>
    /// 将 Guid 转换为 proto string 字段值（D 格式：xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx）。
    /// </summary>
    public static string ToString(Guid guid) => guid.ToString("D");

    /// <summary>
    /// 尝试将 proto string 字段值解析为 Guid。
    /// 解析失败返回 false 且 result 为 Guid.Empty。
    /// </summary>
    public static bool TryParse(string? value, out Guid result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = Guid.Empty;
            return false;
        }

        return Guid.TryParse(value, out result);
    }

    /// <summary>
    /// 将 proto string 字段值解析为 Guid，解析失败抛 <see cref="FormatException"/>。
    /// </summary>
    public static Guid Parse(string? value)
    {
        if (!TryParse(value, out var result))
        {
            throw new FormatException($"无效的 Guid 字符串: {value}");
        }
        return result;
    }
}
```

修改 4 个 BC 的 gRPC 服务端，将所有 `(long)dto.XxxId.GetHashCode()` 改为 `XxxIdStr = GuidProtoConverter.ToString(dto.XxxId)`（保留 `XxxId` int64 字段但标记 `[deprecated = true]`）。以 SellerShop BC 为例（其他 BC 同模式，详见各分 BC 计划）：

```csharp
// 文件：src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs
// 第 99 行原代码：
// ShopId = (long)dto.ShopId.GetHashCode()
// 第 105 行原代码：
// ShopId = (long)dto.ShopId.GetHashCode(),

// 替换为（保留 int64 字段向后兼容，新增 string 字段为权威值）：
using Leno.Infrastructure.AntiCorruption;

// 在 MapToProto 方法中：
ShopId = (long)dto.ShopId.GetHashCode(),  // 保留 int64 字段（标记 deprecated，30 天后删除）
ShopIdStr = GuidProtoConverter.ToString(dto.ShopId)  // 新增 string 字段为权威值
```

proto 契约需修改（详见各 BC proto 文件，由分 BC 计划 fix-02/04/06/10 跟进）：

```protobuf
// 文件：src/BuildingBlocks/Leno.SharedContracts.Grpc/Protos/seller.proto
// 在 ShopInfo / SellerInfo 中增加 string 字段
message ShopInfo {
  int64 shop_id = 1 [deprecated = true];  // 保留向后兼容，30 天后删除
  string shop_id_str = 5;  // 新增权威字段
  // ... 其他字段
}
```

消费侧（Cart/Order）改读 `XxxIdStr` 字段，回退 `XxxId` int64 字段（向后兼容期内）：

```csharp
// 文件：src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs
// 第 46 行原代码：
// SkuId = (long)skuId.GetHashCode(),
// 替换为：
SkuIdStr = GuidProtoConverter.ToString(skuId),
```

##### Step 4: 运行测试验证通过

Run:
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~GuidProtoConverterTests"
```
Expected: PASS — 4 个测试全部通过。

##### Step 5: 提交

```bash
git add src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GuidProtoConverter.cs src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/GuidProtoConverterTests.cs
git commit -m "fix(跨BC): 新增 GuidProtoConverter 工具类规范 Guid↔string 转换，替代 Guid.GetHashCode() 不可逆映射"
```

各 BC 的 gRPC 服务端与消费侧修改由分 BC 计划 fix-02/04/06/10 跟进。

---

#### P0-D5.3：PointsMembership `InternalPointsController.Confirm` HTTP 端点补全

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L325-L332]
- **代码位置**：[file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs]（仅有 `TrialOffset`/`Freeze`/`Release` 三个端点，缺 `Confirm`）
- **根因**：p0a-T6 修复了 `PointsInternalAppService.ConfirmAsync` 占位与 gRPC `ConfirmPointsAsync` 真实调用，但 HTTP 端点 `InternalPointsController.Confirm` 仍缺失，Order BC 通过 HTTP 调用积分确认失败。
- **影响**：订单支付成功后积分核销链路断裂，用户支付的订单无法正常扣减冻结积分。
- **跨 BC 协调范围**：PointsMembership（补 HTTP 端点）+ Order（消费方，`PointsAntiCorruptionService` 默认走 HTTP）。

##### Step 1: 写失败测试

```csharp
// 文件：src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/Controllers/InternalPointsControllerConfirmTests.cs
using Leno.PointsMembership.Api.Controllers;
using Leno.PointsMembership.Application;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using FluentAssertions;

namespace Leno.PointsMembership.Api.Tests.Controllers;

public class InternalPointsControllerConfirmTests
{
    [Fact]
    public async Task ConfirmAsync_ShouldReturnSuccess_WhenServiceSucceeds()
    {
        // Arrange
        var mockService = new Mock<IPointsInternalAppService>();
        mockService.Setup(x => x.ConfirmAsync(It.IsAny<ConfirmPointsDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new InternalPointsController(mockService.Object);
        var input = new ConfirmPointsDto
        {
            UserId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            FrozenRecordId = Guid.NewGuid()
        };

        // Act
        var result = await controller.ConfirmAsync(input, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse>().Subject;
        apiResponse.Success.Should().BeTrue("积分确认应成功");
        mockService.Verify(x => x.ConfirmAsync(input, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ConfirmAsync_ShouldHaveInternalRouteAttribute()
    {
        // Arrange
        var method = typeof(InternalPointsController)
            .GetMethod("ConfirmAsync");

        // Assert — 应有 internal/v1/points/confirm 路由特性
        method.Should().NotBeNull("ConfirmAsync 方法应存在");
        var httpPostAttrs = method!.GetCustomAttributes(typeof(HttpPostAttribute), false);
        httpPostAttrs.Should().HaveCountGreaterOrEqualTo(1, "应有 HttpPost 特性");
        var route = ((HttpPostAttribute)httpPostAttrs[0]).Template;
        route.Should().Be("internal/v1/points/confirm",
            "Confirm 端点路由应为 internal/v1/points/confirm，与 Freeze/Release 对齐");
    }
}
```

##### Step 2: 运行测试验证失败

Run:
```bash
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/Leno.PointsMembership.Api.Tests.csproj --filter "FullyQualifiedName~InternalPointsControllerConfirmTests"
```
Expected: FAIL — 编译错误：`InternalPointsController` 不存在 `ConfirmAsync` 方法，`ConfirmPointsDto` 不存在。

##### Step 3: 写最小实现

修改 [file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs]，新增 `Confirm` 端点：

```csharp
// 文件：src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs
// 在 ReleaseAsync 方法后新增 ConfirmAsync

/// <summary>确认扣减冻结积分（订单支付成功后核销）。</summary>
[HttpPost("internal/v1/points/confirm")]
[Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]
[HttpPost("internal/points/confirm")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
public async Task<IActionResult> ConfirmAsync([FromBody] ConfirmPointsDto input, CancellationToken ct)
{
    await _service.ConfirmAsync(input, ct);
    return Ok(ApiResponse.Success());
}
```

在 Application 层新建 `ConfirmPointsDto`（若尚不存在）：

```csharp
// 文件：src/Services/PointsMembership/Leno.PointsMembership.Application/Dto/ConfirmPointsDto.cs
namespace Leno.PointsMembership.Application;

/// <summary>
/// 确认扣减冻结积分请求 DTO，由订单域在支付成功后调用。
/// </summary>
public sealed class ConfirmPointsDto
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>订单标识，用于关联冻结记录。</summary>
    public Guid OrderId { get; init; }

    /// <summary>冻结记录标识（可选，若不传则按 OrderId 查询冻结记录）。</summary>
    public Guid FrozenRecordId { get; init; }
}
```

确保 `IPointsInternalAppService` 已声明 `ConfirmAsync` 方法（p0a-T6 已实现）：

```csharp
// 文件：src/Services/PointsMembership/Leno.PointsMembership.Application/IPointsInternalAppService.cs
// 接口应已含（p0a-T6 已修复）：
// Task ConfirmAsync(ConfirmPointsDto input, CancellationToken ct = default);
```

##### Step 4: 运行测试验证通过

Run:
```bash
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/Leno.PointsMembership.Api.Tests.csproj --filter "FullyQualifiedName~InternalPointsControllerConfirmTests"
```
Expected: PASS — 2 个测试全部通过。

##### Step 5: 提交

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs src/Services/PointsMembership/Leno.PointsMembership.Application/Dto/ConfirmPointsDto.cs src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/Controllers/InternalPointsControllerConfirmTests.cs
git commit -m "fix(跨BC): InternalPointsController 新增 Confirm HTTP 端点，与 gRPC ConfirmPointsAsync 能力对齐"
```

---

### P1-D5.2：`PaymentGrpcService` 硬编码零值修复

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L316-L323]
- **根因**：Payment gRPC 服务返回硬编码零值（`PaymentId = string.Empty` / `Amount = 0`），与 REST Controller 返回的真实数据不一致。
- **修复步骤**：
  1. 填充 gRPC 响应字段的真实值，与 REST Controller 返回保持一致
  2. 增加集成测试验证 gRPC 与 REST 返回字段集与语义一致
- **影响范围**：Payment BC + Order BC（消费方 `PaymentInfoQueryService`）
- **验证方法**：集成测试验证 gRPC 与 REST 返回字段一致

### P1-D5.4：`ConsulConfigWatcher` 触发 IOptionsMonitor 重载

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L334-L341]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs#L68]（`_configuration["AntiCorruption:UseGrpc"] = newValue`）
- **根因**：直接写 `IConfiguration` 不触发 `IOptionsMonitor<T>.OnChange` 回调，`AntiCorruptionDispatcher` 的 `IOptionsMonitor<AntiCorruptionOptions>.CurrentValue` 永远返回启动时绑定的值。
- **修复步骤**：
  1. 使用自定义 `IOptionsChangeTokenSource<AntiCorruptionOptions>`，ConsulConfigWatcher 触发 change token
  2. 或使用 `IConfigurationRoot.Reload()` 触发所有 `IOptionsMonitor` 重载
  3. 单元测试验证 `IOptionsMonitor.CurrentValue` 在配置变更后更新
- **影响范围**：所有 BC 的 ACL 双轨切换
- **验证方法**：Consul KV 修改 UseGrpc 后 1 分钟内 `AntiCorruptionDispatcher` 切换 gRPC/HTTP

---

### D6: 重复实现

#### P0-D6.1：设计期工厂硬编码 SA 密码外部化（与 TD3/R5 合并）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L350-L366]
- **代码位置**（已验证扩大到 11 个 BC，原报告说 3 个）：
  - Cart：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/CartDbContextDesignTimeFactory.cs#L15]
  - SellerShop：[file:///workspace/src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContextDesignTimeFactory.cs#L15]
  - Notification：[file:///workspace/src/Services/Notification/Leno.Notification.Infrastructure/NotificationDbContextDesignTimeFactory.cs#L15]
  - Order：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/OrderDbContextDesignTimeFactory.cs#L15]
  - Product：[file:///workspace/src/Services/Product/Leno.Product.Infrastructure/ProductDbContextDesignTimeFactory.cs#L15]
  - Promotion：[file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/PromotionDbContextDesignTimeFactory.cs#L15]
  - UserAuth：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/UserAuthDbContextDesignTimeFactory.cs#L15]
  - Payment：[file:///workspace/src/Services/Payment/Leno.Payment.Infrastructure/PaymentDbContextDesignTimeFactory.cs#L15]
  - PointsMembership：[file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/PointsMembershipDbContextDesignTimeFactory.cs#L15]
  - ReviewAfterSales：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReviewAfterSalesDbContextDesignTimeFactory.cs#L15]
  - SystemAdmin：[file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/SystemAdminDbContextDesignTimeFactory.cs#L15]
- **根因**：设计期工厂为绕过 Redis 等依赖直接连库生成迁移，硬编码了与生产同结构的明文凭据 `Leno@SqlServer2019`。该字符串以源码形式进入 Git 仓库历史。
- **影响**：源码一旦泄露，攻击者可直接以 SA 身份连接数据库，绕过应用层所有鉴权。
- **跨 BC 协调范围**：11 个 BC 的设计期工厂 + 共享层 `DesignTimeDbContextFactoryBase<T>` 抽取。

##### Step 1: 写失败测试

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/DesignTimeDbContextFactoryBaseTests.cs
using Leno.Infrastructure.Persistence;
using Xunit;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.Persistence;

public class DesignTimeDbContextFactoryBaseTests
{
    [Fact]
    public void ResolveConnectionString_ShouldReadFromEnvironmentVariable()
    {
        // Arrange — 设置环境变量
        var expectedConnStr = "Server=test,1433;Database=LenoTest;User Id=sa;Password=FromEnv;TrustServerCertificate=True";
        Environment.SetEnvironmentVariable("LENO_DESIGNTIME_CONNECTION_STRING", expectedConnStr);

        try
        {
            // Act
            var resolved = DesignTimeDbContextFactoryBase<object>.ResolveConnectionString("LenoTest");

            // Assert — 应从环境变量读取，而非硬编码
            resolved.Should().Be(expectedConnStr, "应从 LENO_DESIGNTIME_CONNECTION_STRING 环境变量读取连接字符串");
            resolved.Should().NotContain("Leno@SqlServer2019",
                "不应包含硬编码的 SA 密码");
        }
        finally
        {
            // 清理环境变量
            Environment.SetEnvironmentVariable("LENO_DESIGNTIME_CONNECTION_STRING", null);
        }
    }

    [Fact]
    public void ResolveConnectionString_NotSet_ShouldThrowWithClearMessage()
    {
        // Arrange — 清除环境变量
        Environment.SetEnvironmentVariable("LENO_DESIGNTIME_CONNECTION_STRING", null);

        // Act & Assert — 未配置时应抛异常并给出明确提示
        var act = () => DesignTimeDbContextFactoryBase<object>.ResolveConnectionString("LenoTest");
        var ex = act.Should().Throw<InvalidOperationException>().Subject;
        ex.Message.Should().Contain("LENO_DESIGNTIME_CONNECTION_STRING",
            "异常消息应提示需要设置环境变量");
        ex.Message.Should().NotContain("Leno@SqlServer2019",
            "异常消息不应暴露旧密码");
    }

    [Fact]
    public void ResolveConnectionString_ShouldNeverContainLegacyPassword()
    {
        // Arrange — 各种环境变量场景
        var testValues = new[]
        {
            "Server=localhost,1433;Database=Test;User Id=sa;Password=AnyPassword;TrustServerCertificate=True",
            null,
            ""
        };

        foreach (var value in testValues)
        {
            Environment.SetEnvironmentVariable("LENO_DESIGNTIME_CONNECTION_STRING", value);
            try
            {
                if (string.IsNullOrEmpty(value))
                {
                    var act = () => DesignTimeDbContextFactoryBase<object>.ResolveConnectionString("Test");
                    act.Should().Throw<InvalidOperationException>("空值应抛异常");
                }
                else
                {
                    var resolved = DesignTimeDbContextFactoryBase<object>.ResolveConnectionString("Test");
                    resolved.Should().NotContain("Leno@SqlServer2019",
                        "解析结果绝不应包含旧硬编码密码");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("LENO_DESIGNTIME_CONNECTION_STRING", null);
            }
        }
    }
}
```

##### Step 2: 运行测试验证失败

Run:
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DesignTimeDbContextFactoryBaseTests"
```
Expected: FAIL — 编译错误：`DesignTimeDbContextFactoryBase<T>` 类不存在。

##### Step 3: 写最小实现

新建 `DesignTimeDbContextFactoryBase<T>` 到 `Leno.Infrastructure/Persistence/`：

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/Persistence/DesignTimeDbContextFactoryBase.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Leno.Infrastructure.Persistence;

/// <summary>
/// 设计期 DbContext 工厂基类，统一从环境变量读取连接字符串，消除各 BC 硬编码 SA 密码的安全风险。
/// 各 BC 的 XxxDbContextDesignTimeFactory 继承此类，仅需实现 CreateDbContext 与提供 DbContext 类型参数。
/// </summary>
/// <typeparam name="TContext">DbContext 派生类型。</typeparam>
public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    private const string ConnectionStringEnvVar = "LENO_DESIGNTIME_CONNECTION_STRING";

    /// <summary>
    /// 解析连接字符串，优先从 LENO_DESIGNTIME_CONNECTION_STRING 环境变量读取。
    /// 未配置时抛 <see cref="InvalidOperationException"/>，避免回退到硬编码密码。
    /// </summary>
    /// <param name="databaseName">数据库名（仅用于错误提示，不参与拼接）。</param>
    /// <returns>连接字符串。</returns>
    /// <exception cref="InvalidOperationException">环境变量未设置时抛出。</exception>
    public static string ResolveConnectionString(string databaseName)
    {
        var connStr = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        if (string.IsNullOrWhiteSpace(connStr))
        {
            throw new InvalidOperationException(
                $"设计期工厂需要环境变量 {ConnectionStringEnvVar} 才能生成迁移。" +
                $"请在本地设置：export {ConnectionStringEnvVar}='Server=localhost,1433;Database={databaseName};User Id=sa;Password=<YOUR_PASSWORD>;TrustServerCertificate=True'" +
                $"。CI 流水线会自动注入该变量。详细说明见 docs/handbook/06-storage-and-cache.md。");
        }
        return connStr;
    }

    /// <summary>
    /// 创建设计期 DbContext 实例。
    /// 子类应覆盖此方法以指定 DbContextOptions 配置（如 UseSqlServer vs UseNpgsql）。
    /// </summary>
    public abstract TContext CreateDbContext(string[] args);

    /// <summary>
    /// 构建 DbContextOptions，使用从环境变量解析的连接字符串。
    /// 子类在 CreateDbContext 中调用此方法。
    /// </summary>
    protected DbContextOptionsBuilder<TContext> CreateOptionsBuilder(string databaseName)
    {
        var connStr = ResolveConnectionString(databaseName);
        var builder = new DbContextOptionsBuilder<TContext>();
        builder.UseSqlServer(connStr, sqlOptions =>
        {
            sqlOptions.MigrationsAssembly(typeof(TContext).Assembly.GetName().Name);
        });
        return builder;
    }
}
```

各 BC 的设计期工厂改为继承基类。以 Cart BC 为例（其他 10 个 BC 同模式，由各分 BC 计划跟进）：

```csharp
// 文件：src/Services/Cart/Leno.Cart.Infrastructure/CartDbContextDesignTimeFactory.cs
using Leno.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Leno.Cart.Infrastructure;

/// <summary>
/// Cart BC 设计期 DbContext 工厂，从环境变量读取连接字符串。
/// 不再硬编码 SA 密码，消除源码泄露风险（ADR 与安全审计统一要求）。
/// </summary>
public sealed class CartDbContextDesignTimeFactory : DesignTimeDbContextFactoryBase<CartDbContext>
{
    public override CartDbContext CreateDbContext(string[] args)
    {
        var builder = CreateOptionsBuilder(databaseName: "LenoCart");
        return new CartDbContext(builder.Options);
    }
}
```

CI 流水线注入环境变量（GitHub Actions / GitLab CI 示例）：

```yaml
# 文件：.github/workflows/ci.yml（或对应 CI 配置）
# 在生成迁移的 job 中注入环境变量（使用 CI Secret）
env:
  LENO_DESIGNTIME_CONNECTION_STRING: ${{ secrets.LENO_DESIGNTIME_CONNECTION_STRING }}
```

开发文档更新（[file:///workspace/docs/handbook/06-storage-and-cache.md]）增加章节：

```markdown
## 设计期工厂连接字符串配置

生成 EF Core 迁移时，设计期工厂从环境变量 `LENO_DESIGNTIME_CONNECTION_STRING` 读取连接字符串。

### 本地开发设置

```bash
export LENO_DESIGNTIME_CONNECTION_STRING='Server=localhost,1433;Database=Leno<BC>;User Id=sa;Password=<YOUR_LOCAL_PASSWORD>;TrustServerCertificate=True'
```

### CI 流水线

CI Secret `LENO_DESIGNTIME_CONNECTION_STRING` 已注入到生成迁移的 job 中。
```

历史密码轮换：若 `Leno@SqlServer2019` 曾用于生产环境，立即轮换生产 SA 密码；CI 增加 secret scanning 防止再次提交。

##### Step 4: 运行测试验证通过

Run:
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~DesignTimeDbContextFactoryBaseTests"
```
Expected: PASS — 3 个测试全部通过。

各 BC 设计期工厂的修改由分 BC 计划 fix-01 ~ fix-11 跟进。验证全仓无硬编码：

Run:
```bash
grep -r "Leno@SqlServer2019" src/ || echo "PASS: 无硬编码 SA 密码"
```
Expected: PASS — 无硬编码 SA 密码。

##### Step 5: 提交

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Persistence/DesignTimeDbContextFactoryBase.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/DesignTimeDbContextFactoryBaseTests.cs
git commit -m "fix(跨BC): 新增 DesignTimeDbContextFactoryBase 基类从环境变量读取连接字符串，消除 11 BC 硬编码 SA 密码"
```

各 BC 设计期工厂的修改由分 BC 计划跟进提交。

---

### P2-D6.2：双路由 Obsolete 补下线时间

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L368-L382]
- **代码位置**（示例）：[file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L24]（`[Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]` 无具体日期）
- **修复步骤**：
  1. 在所有 `[Obsolete]` 特性中补充 `DiagnosticId` 与下线时间，如 `[Obsolete("Use internal/v1/points/confirm instead, will be removed in 2026-10-01", DiagnosticId = "LENO001", UrlFormat = "https://wiki/leno/obsolete")]`
  2. CI 中增加警告升级为错误（`TreatWarningsAsErrors`），强制按计划下线
  3. 全文搜索 `[Obsolete("` 并按统一格式补充
- **影响范围**：Product / SellerShop / Order / Cart / Promotion 等多 BC
- **验证方法**：编译期警告含 DiagnosticId；CI 检测过期 Obsolete 报错

### P2-D6.3：限流熔断统一复用共享层

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L384-L398]
- **代码位置**：[file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs#L25-L41]（网关层独立的滑动窗口限流器）
- **修复步骤**：
  1. 将 `RedisSlidingWindowRateLimiter` 抽取到 `Leno.Infrastructure/` 共享层，修复 Lua 脚本顺序（详见 fix-12 P0-T8）
  2. 各 BC 通过 `IRateLimiter` 接口复用，配置驱动（如 `[RateLimit("seckill", permit: 100, window: "60s")]` 特性）
  3. 评价返积分、签到返积分等高频端点强制启用限流
- **影响范围**：UserAuth / Promotion / PointsMembership 等 BC
- **验证方法**：单元测试验证限流策略一致；高频端点限流生效

---

## G4 技术债 Top10 修复计划

### 四象限归类矩阵

按 G4 章节定义的四象限归类，标注与 D 章节问题的合并关系：

```
                    修复成本 低                          修复成本 高
                ┌──────────────────────────┬──────────────────────────┐
                │  象限 I：速赢（高影响低成本）│  象限 II：战略性（高影响高成本）│
  业务影响 高     │  - TD1 Outbox 旁路修复     │  - TD5 Guid→string 迁移     │
                │    （与 D4.1 合并，P0）   │    （与 D5.1 合并，P1）    │
                │  - TD2 静态状态竞态加锁    │  - TD6 跨域 Saga 编排补全   │
                │    （[ALREADY-FIXED]      │    （P1）                  │
                │    fix-12 P0-T1/T3）      │  - TD7 共享内核 Money 标准化│
                │  - TD3 DesignTime 密码外部化│    （与 D3.1 合并，P1）    │
                │    （与 D6.1 合并，P0）   │                            │
                │  - TD4 IDOR 归属校验补全   │                            │
                │    （P0）                │                            │
                ├──────────────────────────┼──────────────────────────┤
                │  象限 III：顺手做（低影响低成本）│  象限 IV：暂缓（低影响高成本）│
  业务影响 低     │  - TD8 死消费者清理       │  - TD9 ACL 适配器样板代码生成│
                │    （P1）                │    （P2）                  │
                │  - TD9 ACL 适配器样板代码生成│  - TD10 BFF 聚合层重构     │
                │    （P2）                │    （P2）                  │
                └──────────────────────────┴──────────────────────────┘
```

### TD1 Outbox 旁路修复（象限 I 速赢，P0，与 D4.1 合并）

- **业务影响**：高（分布式一致性故障）
- **修复成本**：低（改 `SaveChangesAsync` → `SaveEntitiesAsync` + 标记 `[Obsolete]`）
- **证据**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md] D2/D4.1
- **修复计划**：见本计划 P0-D4.1 章节（含完整 TDD 5 步骤）
- **依赖**：各 BC 应用层需删除 `SaveEntitiesAsync` 之后的 `IEventBus.PublishAsync` 调用（由分 BC 计划 fix-01/05/07/11 跟进）

### TD2 静态状态竞态加锁（象限 I 速赢，[ALREADY-FIXED]）

- **业务影响**：高（指标失真、Random 退化）
- **修复成本**：低（改 `ConcurrentDictionary` / `Random.Shared`）
- **证据**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md]
- **状态**：[ALREADY-FIXED] —— 由 fix-12-shared.md 的 P0-T1（`CacheService` 改 `Random.Shared`）与 P0-T3（`AntiCorruptionMetrics` 改 `ConcurrentDictionary`）已规划完整 TDD 步骤，本计划不重复。

### TD3 DesignTime 密码外部化（象限 I 速赢，P0，与 D6.1 合并）

- **业务影响**：高（安全风险）
- **修复成本**：低（改读环境变量）
- **证据**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md] D6.1
- **修复计划**：见本计划 P0-D6.1 章节（含完整 TDD 5 步骤，覆盖 11 个 BC 设计期工厂）

### TD4 IDOR 归属校验补全（象限 I 速赢，P0，与 R3 合并）

- **业务影响**：高（OWASP A01:2021 Broken Access Control）
- **修复成本**：低（每端点加 `userId == resource.OwnerId` 校验）
- **证据**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md]（PaymentsController IDOR）、[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md]（Reject/ReturnGoods/Cancel/SellerReply 多处缺归属校验）
- **修复计划**：跨 BC 协调，所有按资源 ID 查询/操作的端点加归属校验。具体由分 BC 计划 fix-06（ReviewAfterSales）与 fix-08（Payment）跟进，本计划给出统一治理步骤：

#### P0-TD4：跨 BC IDOR 归属校验统一治理

- **审计位置**：13-architecture-assessment.md G3.10
- **跨 BC 协调范围**：Payment + ReviewAfterSales（详见 fix-06/fix-08）

##### Step 1: 写失败测试

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/ResourceOwnershipCheckerTests.cs
using Leno.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.Auth;

public class ResourceOwnershipCheckerTests
{
    [Fact]
    public async Task EnsureOwnerAsync_ResourceOwnedByCurrentUser_ShouldNotThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns(userId);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);

        var checker = new ResourceOwnershipChecker(userContext.Object);

        // Act & Assert — 资源所有者为当前用户，不应抛异常
        await FluentActions.Awaiting(() => checker.EnsureOwnerAsync(userId, "ORDER"))
            .Should().NotThrowAsync("资源所有者与当前用户一致时不应抛异常");
    }

    [Fact]
    public async Task EnsureOwnerAsync_ResourceOwnedByOther_ShouldThrowForbiddenException()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var resourceOwnerId = Guid.NewGuid();
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns(currentUserId);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);

        var checker = new ResourceOwnershipChecker(userContext.Object);

        // Act & Assert — 资源所有者为他人，应抛 ForbiddenException
        var act = () => checker.EnsureOwnerAsync(resourceOwnerId, "ORDER");
        var ex = await act.Should().ThrowAsync<ForbiddenAccessException>();
        ex.WhichMessage.Should().Contain("ORDER");
        ex.WhichMessage.Should().NotContain(resourceOwnerId.ToString(),
            "错误消息不应暴露资源所有者的 UserId");
    }

    [Fact]
    public async Task EnsureOwnerAsync_UnauthenticatedUser_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns((Guid?)null);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(false);

        var checker = new ResourceOwnershipChecker(userContext.Object);

        // Act & Assert — 未认证用户应抛 UnauthorizedAccessException
        var act = () => checker.EnsureOwnerAsync(Guid.NewGuid(), "ORDER");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
```

##### Step 2: 运行测试验证失败

Run:
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ResourceOwnershipCheckerTests"
```
Expected: FAIL — 编译错误：`ResourceOwnershipChecker` 与 `ForbiddenAccessException` 不存在。

##### Step 3: 写最小实现

新建 `ResourceOwnershipChecker` 与 `ForbiddenAccessException` 到 `Leno.Infrastructure/Auth/`：

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/Auth/ForbiddenAccessException.cs
namespace Leno.Infrastructure.Auth;

/// <summary>
/// 当用户尝试访问不属于自己的资源时抛出，对应 HTTP 403 Forbidden。
/// 错误消息不暴露资源所有者标识，防止信息泄露。
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    public string ResourceType { get; }

    public ForbiddenAccessException(string resourceType)
        : base($"当前用户无权访问该 {resourceType} 资源")
    {
        ResourceType = resourceType;
    }
}
```

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/Auth/ResourceOwnershipChecker.cs
namespace Leno.Infrastructure.Auth;

/// <summary>
/// 资源归属校验器，统一处理 IDOR 越权防护。
/// 所有按资源 ID 查询/操作的端点应调用 EnsureOwnerAsync 校验当前用户是否为资源所有者。
/// </summary>
public sealed class ResourceOwnershipChecker
{
    private readonly ICurrentUserContext _userContext;

    public ResourceOwnershipChecker(ICurrentUserContext userContext)
    {
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    /// <summary>
    /// 校验当前用户是否为资源所有者，不是则抛 <see cref="ForbiddenAccessException"/>。
    /// </summary>
    /// <param name="resourceOwnerId">资源所有者的 UserId。</param>
    /// <param name="resourceType">资源类型名称（用于错误提示，如 "ORDER"、"REVIEW"）。</param>
    public Task EnsureOwnerAsync(Guid resourceOwnerId, string resourceType)
    {
        if (!_userContext.IsAuthenticated || _userContext.UserId is null)
        {
            throw new UnauthorizedAccessException("用户未认证");
        }

        if (_userContext.UserId.Value != resourceOwnerId)
        {
            throw new ForbiddenAccessException(resourceType);
        }

        return Task.CompletedTask;
    }
}
```

各 BC 的 Controller / AppService 调用 `ResourceOwnershipChecker.EnsureOwnerAsync`，由分 BC 计划 fix-06（ReviewAfterSales）与 fix-08（Payment）跟进。

##### Step 4: 运行测试验证通过

Run:
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ResourceOwnershipCheckerTests"
```
Expected: PASS — 3 个测试全部通过。

##### Step 5: 提交

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Auth/ResourceOwnershipChecker.cs src/BuildingBlocks/Leno.Infrastructure/Auth/ForbiddenAccessException.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Auth/ResourceOwnershipCheckerTests.cs
git commit -m "fix(跨BC): 新增 ResourceOwnershipChecker 统一 IDOR 越权校验，防止用户访问他人资源"
```

### TD5 Guid→string 迁移（象限 II 战略性，P1，与 D5.1/R1 合并）

- **业务影响**：高（ID 碰撞数据错乱）
- **修复成本**：高（双写过渡 + 客户端逐步升级 + 旧字段废弃）
- **证据**：[file:///workspace/docs/decisions/0007-guid-string-migration-strategy.md#L18-L29]
- **修复计划**：见本计划 P0-D5.1 章节（短期：`GuidProtoConverter` 工具类 + 4 BC gRPC 服务端双写）；中期由 G5 M1 跟进（6 周，按 proto 文件分批迁移）；长期由 G5 L1 跟进（3 月，删除 int64 字段）。

### TD6 跨域 Saga 编排补全（象限 II 战略性，P1，与 D4/R4 合并）

- **业务影响**：高（跨域半完成状态）
- **修复成本**：高（需设计 Saga 协调器 + 补偿动作 + 幂等保证）
- **证据**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md] D4
- **修复计划**：详见 G5 中期 M2 章节（6 周，Order Saga + Payment 消费者 + Notification 回执）。
- **本计划短期缓解**：跨域操作失败告警 + 人工对账脚本

### TD7 共享内核 Money 标准化（象限 II 战略性，P1，与 D3.1 合并）

- **业务影响**：高（财务对账分位差）
- **修复成本**：高（需评审 4 个 BC 的小数位策略 + 统一迁移）
- **证据**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md] D3
- **修复计划**：详见 G5 中期 M3 章节（4 周，统一"银行家舍入 + 2 位小数"策略）。

### TD8 死消费者清理（象限 III 顺手做，P1）

- **业务影响**：中（资源浪费 + 误导排查）
- **修复成本**：低（删除或修复事件发布）
- **证据**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md] PM-H03
- **修复步骤**：
  1. PointsMembership BC 补齐 4 个 ReadModel 死消费者的事件发布方
  2. ES 索引 `leno_points_accounts` / `leno_members` 正常同步
- **影响范围**：PointsMembership BC（详见 fix-07-pointsmembership.md）
- **验证方法**：ES 索引正常同步

### TD9 ACL 适配器样板代码生成（象限 III 顺手做，P2）

- **业务影响**：低（维护成本）
- **修复成本**：低（T4 模板或 Source Generator）
- **证据**：[file:///workspace/docs/decisions/0003-anticorruption-dispatcher-adapter-pattern.md]
- **修复计划**：详见 G5 长期 L2 章节（2 月，Roslyn Source Generator 自动生成 `{Service}DispatcherAdapter`）。

### TD10 BFF 聚合层重构（象限 IV 暂缓，P2）

- **业务影响**：低（Dashboard 数据失真）
- **修复成本**：高（需重新设计聚合查询 + 读模型）
- **证据**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md]
- **修复计划**：详见 G5 长期 L3 章节（4 月，重新设计 ShopDashboardReadModelBuilder 聚合查询）。

---

## G6 风险 Top5 缓解计划

### R1：gRPC Guid→int64 碰撞导致跨 BC ID 错配（P0，与 D5.1 合并）

- **严重度**：🔴 高
- **触发条件**：4 个 BC（Order/Product/ReviewAfterSales/SellerShop）的 GrpcService 仍用 `(long)guid.GetHashCode()`
- **影响**：跨 BC ID 错配引发订单查到错误 SKU、评价归属错用户、店铺数据错乱等数据错乱
- **缓解步骤**：
  1. **立即（1 天）**：在 `Leno.Infrastructure.AntiCorruption/` 新增 `GuidProtoConverter` 工具类（见本计划 P0-D5.1 Step 3）
  2. **短期（1-2 周）**：4 BC gRPC 服务端双写 `int64` + `string xxx_id_str` 字段，`int64` 标记 `[deprecated = true]`
  3. **短期（1-2 周）**：2 BC gRPC 客户端（Cart/Order）改读 `string` 字段，回退 `int64`
  4. **短期（1-2 周）**：在 `GrpcAntiCorruptionClientBase` 增加碰撞日志告警：当 `int64` 与 `string` 解析的 Guid 不一致时记 LogWarning
  5. **中期（6 周）**：按 G5 M1 完成 6 个 .proto 文件的迁移
  6. **长期（3 月）**：按 G5 L1 删除 `int64` 字段

### R2：Outbox 旁路导致分布式一致性故障（P0，与 D4.1/TD1 合并）

- **严重度**：🔴 高
- **触发条件**：5 个 BC 存在 `SaveChangesAsync` 或 `PublishAsync` 旁路 Outbox 的代码
- **影响**：业务提交了但消息丢失，或业务回滚了但消息已发
- **缓解步骤**：
  1. **立即（1 天）**：`EfCoreUnitOfWork.SaveChangesAsync` 标记 `[Obsolete]` 并内部委托 `SaveEntitiesAsync`（见本计划 P0-D4.1 Step 3）
  2. **短期（1 周）**：各 BC 删除 `SaveEntitiesAsync` 之后的 `IEventBus.PublishAsync` 调用（由分 BC 计划 fix-01/05/07/11 跟进）
  3. **短期（1 周）**：CI 增加 Roslyn 分析器（`BannedApiAnalyzers`）禁止 Infrastructure 层直接调 `SaveChangesAsync`
  4. **短期（1 周）**：单元测试覆盖：验证 Outbox 表有对应记录
  5. **中期**：监控 Outbox 表 pending 数量，超阈值告警（已由 T22 实施）

### R3：IDOR 越权导致用户数据泄露（P0，与 TD4 合并）

- **严重度**：🔴 高
- **触发条件**：PaymentsController 直查 orderId 无归属校验、ReviewAfterSales 多处缺归属校验
- **影响**：攻击者遍历他人订单 ID/评价 ID，越权查询或操作他人数据
- **缓解步骤**：
  1. **立即（1 天）**：在 `Leno.Infrastructure/Auth/` 新增 `ResourceOwnershipChecker` 与 `ForbiddenAccessException`（见本计划 P0-TD4 Step 3）
  2. **短期（1 周）**：PaymentsController 所有按 orderId 查询/操作的端点调用 `EnsureOwnerAsync`（由 fix-08 跟进）
  3. **短期（1 周）**：ReviewAfterSales 的 Reject/ReturnGoods/Cancel/SellerReply/GetAfterSalesByOrder/GetReviewByOrderLine 全部加归属校验（由 fix-06 跟进）
  4. **短期（1 周）**：单元测试覆盖：用例覆盖"他人资源访问返回 403"
  5. **中期**：CI 增加静态分析检测未加归属校验的端点

### R4：跨域 Saga 缺补偿动作导致半完成状态（P1，与 TD6/D4 合并）

- **严重度**：🟡 中
- **触发条件**：Order Saga、Payment 消费者、Notification 回执等跨域操作缺原子性保证
- **影响**：跨域操作失败时出现"半完成"状态，需人工介入修复数据
- **缓解步骤**：
  1. **短期（1 周）**：增加跨域操作失败告警：消费 `PaymentSucceededEvent` 等关键事件失败时发告警
  2. **短期（1 周）**：编写人工对账脚本：扫描"订单已创建但库存未扣"、"支付已成功但订单状态未推进"等异常状态
  3. **中期（6 周）**：按 G5 M2 实施 OrderSagaOrchestrator（基于状态机 + Outbox 事件驱动）
  4. **中期（6 周）**：Notification 回执持久化（消费发送结果事件，落库 NotificationRecord）
  5. **中期（6 周）**：Payment 消费者原子性保证（同一事务内更新支付单 + 发 PaymentSucceededEvent）
  6. **中期**：集成测试：模拟各 BC 故障，验证补偿动作触发与最终一致

### R5：DesignTimeFactory SA 密码泄露（P0，与 D6.1/TD3 合并）

- **严重度**：🟡 中（实际 11 BC 范围应升为 🔴 高）
- **触发条件**：11 个 BC 的 DesignTimeFactory 硬编码 `Password=Leno@SqlServer2019`，源码仓库可见
- **影响**：密码泄露到源码仓库，攻击者拿到源码即可尝试用该密码连接生产数据库
- **缓解步骤**：
  1. **立即（1 天）**：新建 `DesignTimeDbContextFactoryBase<T>` 基类从环境变量读取连接字符串（见本计划 P0-D6.1 Step 3）
  2. **短期（1 周）**：11 个 BC 的 `XxxDbContextDesignTimeFactory` 改为继承基类（由各分 BC 计划 fix-01 ~ fix-11 跟进）
  3. **立即（1 天）**：若 `Leno@SqlServer2019` 曾用于生产环境，立即轮换生产 SA 密码
  4. **短期（1 周）**：CI 增加 secret scanning（如 `truffleHog` 或 GitHub Secret Scanning）防止再次提交
  5. **短期（1 周）**：开发文档说明本地需设置 `LENO_DESIGNTIME_CONNECTION_STRING` 环境变量
  6. **验证**：`grep -r "Leno@SqlServer2019" src/` 零命中

---

## G5 优化方案落地计划

引用 13-architecture-assessment.md G5 章节，给出短期/中期/长期落地步骤（不复制 G5 原文）。

### G5 短期（1-2 周）：速赢修复

引用：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md#L348-L403]

#### S1：Outbox 旁路全面修复（1 周，与 D4.1/TD1/R2 合并）

- **落地步骤**：
  1. 见本计划 P0-D4.1 完整 TDD 5 步骤
  2. 各 BC 删除 `SaveEntitiesAsync` 之后的 `IEventBus.PublishAsync` 调用（由分 BC 计划跟进）
  3. CI 增加 Roslyn 分析器禁止旁路
  4. 单元测试覆盖 Outbox 表记录

#### S2：静态状态竞态加锁（3 天，[ALREADY-FIXED]）

- **状态**：由 fix-12-shared.md 的 P0-T1（`CacheService` 改 `Random.Shared`）与 P0-T3（`AntiCorruptionMetrics` 改 `ConcurrentDictionary`）已规划，本计划不重复。

#### S3：DesignTimeFactory 密码外部化（1 天，与 D6.1/TD3/R5 合并）

- **落地步骤**：见本计划 P0-D6.1 完整 TDD 5 步骤
- **额外步骤**：
  1. 若 `Leno@SqlServer2019` 曾用于生产，立即轮换生产 SA 密码
  2. CI 流水线注入 `LENO_DESIGNTIME_CONNECTION_STRING` 环境变量（使用 CI Secret）
  3. CI 增加 secret scanning 防止再次提交

#### S4：IDOR 归属校验补全（1 周，与 TD4/R3 合并）

- **落地步骤**：
  1. 见本计划 P0-TD4 完整 TDD 5 步骤（`ResourceOwnershipChecker` 工具类）
  2. PaymentsController 所有按 orderId 查询/操作的端点调用 `EnsureOwnerAsync`（由 fix-08 跟进）
  3. ReviewAfterSales 的所有写操作端点加归属校验（由 fix-06 跟进）
  4. 单元测试覆盖"他人资源访问返回 403"

#### S5：SystemAdmin 指标误用修复（3 天）

- **目标**：H-01 `StatisticsAggregationService` 用 `new Random()` 生成所有指标值的代码替换为真实查询
- **落地步骤**：
  1. `StatisticsAggregationService` 改用真实 EF Core 查询聚合各指标
  2. 注入 `IOrderQueryService` / `IPaymentQueryService` 等跨域只读查询接口，从读模型聚合真实指标
  3. 单元测试验证指标值与底层数据一致
- **影响范围**：SystemAdmin BC（详见 fix-11-systemadmin.md）

### G5 中期（1-2 月）：战略性修复

引用：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md#L405-L455]

#### M1：Guid→string 迁移启动（6 周，与 D5.1/TD5/R1 合并）

- **落地步骤**：
  1. 第 1 周：`order.proto`、`product.proto` 新增 `string xxx_id_str = N;`，原 `int64 xxx_id` 标记 `[deprecated = true]`
  2. 第 2-3 周：Order/Product BC 的 GrpcService 双写 `int64` + `string`，GrpcClient 优先读 `string` 回退 `int64`
  3. 第 4 周：`review.proto`、`seller.proto`、`cart.proto`、`payment.proto` 同步改造
  4. 第 5-6 周：ReviewAfterSales/SellerShop/Cart/Payment BC 的 GrpcService/GrpcClient 改造
  5. CI 监控 deprecated 字段使用情况，跟踪迁移进度
- **依赖**：本计划 P0-D5.1 的 `GuidProtoConverter` 工具类（短期已落地）

#### M2：跨域 Saga 编排补全（6 周，与 TD6/D4/R4 合并）

- **落地步骤**：
  1. 第 1-2 周：梳理 Order 创建→库存扣减→支付→发货的完整 Saga 状态机，绘制状态图
  2. 第 3-4 周：实现 `OrderSagaOrchestrator`（基于状态机 + Outbox 事件驱动），定义补偿动作（如 ForceCancel 的库存类型修正、StockReservation 的回滚）
  3. 第 5 周：Notification 回执持久化（消费发送结果事件，落库 NotificationRecord）
  4. 第 6 周：Payment 消费者原子性保证（同一事务内更新支付单 + 发 PaymentSucceededEvent）
  5. 集成测试：模拟各 BC 故障，验证补偿动作触发与最终一致

#### M3：共享内核 Money 标准化（4 周，与 D3.1/TD7 合并）

- **落地步骤**：
  1. 第 1 周：评审会议确定统一策略（建议 `MidpointRounding.ToEven` + 2 位小数，即银行家舍入）
  2. 第 2-3 周：`Money` 值对象增加 `Round(decimal, int, MidpointRounding)` 工厂方法，废弃直接构造
  3. 第 4 周：Product/Promotion/Order/Cart 4 个 BC 的金额计算改用统一工厂方法
  4. 财务对账测试：验证订单金额、促销优惠、支付金额三方一致
- **依赖**：本计划 P1-D3.1 的 `Money` 不可变性修复（短期已落地）

#### M4：缓存失效机制补全（2 周）

- **目标**：FeatureFlagCache/SystemConfigCache 订阅配置变更事件主动失效，或加合理 TTL
- **落地步骤**：
  1. SystemConfigAppService 修改配置时发布 `SystemConfigChangedIntegrationEvent`
  2. FeatureFlagCache/SystemConfigCache 订阅该事件，收到后清除对应 key
  3. 兜底 TTL：FeatureFlag 5 分钟、SystemConfig 1 分钟
  4. 集成测试：修改配置后验证 5 秒内生效
- **影响范围**：SystemAdmin BC（详见 fix-11-systemadmin.md）

### G5 长期（3-6 月）：架构演进

引用：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md#L457-L504]

#### L1：Guid→string 迁移完成 + int64 字段废弃（3 月，与 D5.1/TD5/R1 合并）

- **落地步骤**：
  1. 监控 deprecated 字段使用情况，待所有客户端都读 `string` 后启动下线
  2. `.proto` v2.0 版本删除 `int64 xxx_id` 字段，`buf breaking` 配置允许 major version 删除
  3. GrpcService 移除 `int64` 写入逻辑，GrpcClient 移除回退逻辑
  4. 文档更新：ADR-0006 标记为"已完全 superseded"

#### L2：ACL 适配器样板代码自动化生成（2 月，与 D2/TD9 合并）

- **落地步骤**：
  1. 设计 Roslyn Source Generator：扫描 `IAntiCorruptionService` 接口，自动生成 `XxxDispatcherAdapter` 包装类
  2. 各 BC 删除手写的 Adapter 类，改用生成代码
  3. 单元测试验证生成代码与手写代码行为一致
- **依赖**：D2 章节 ACL DTO 统一抽取（P2 阶段完成）

#### L3：BFF 聚合层重构（4 月，与 TD10 合并）

- **落地步骤**：
  1. 梳理 Dashboard 真实数据需求（订单数、销售额、商品数、评价数、退款数、活跃度）
  2. 各 BC 发布对应聚合事件，BFF 订阅物化 Dashboard 读模型
  3. `ShopDashboardReadModelBuilder` 改为读 ES 读模型而非硬编码 0
  4. 前端联调验证 Dashboard 数据真实
- **影响范围**：SellerShop BC + 各 BC 发布聚合事件

#### L4：跨 BC 契约评审机制建立（持续）

- **落地步骤**：
  1. 所有集成事件 schema 集中到 `Leno.SharedContracts/Events/` 目录，PR 修改需触发跨 BC 评审
  2. CI 校验：消费方代码引用的集成事件字段必须在 schema 中存在（基于 Roslyn 分析或反射）
  3. 集成事件 schema 版本号（`SchemaVersion`）演进规则文档化，新增字段递增版本号，消费方按版本路由
  4. 跨 BC 契约变更周会：每周评审本周集成事件 schema 变更，确保消费方知晓
- **依赖**：本计划 D1 章节的事件契约对齐修复（P0 已落地）

---

## 附录：跨 BC 修复依赖关系图

```text
短期（1-2 周）：
  P0-D1.1 RefundCompletedEvent+ChannelRefundNo ── Payment BC 发布方填充（fix-08 跟进）
  P0-D1.2 ReviewSubmittedEvent+ShopId ── ReviewAfterSales 发布方填充（fix-06 跟进）
  P0-D1.5 IdempotencyKey 默认值 ── 所有 BC 自动受益
  P0-D4.1 Outbox 旁路 ── 4 BC 应用层删除 PublishAsync（fix-01/05/07/11 跟进）
  P0-D5.1 GuidProtoConverter ── 4 BC gRPC 服务端双写（fix-02/04/06/10 跟进）
  P0-D5.3 PointsMembership Confirm HTTP ── Order BC 消费方受益
  P0-D6.1 DesignTimeFactoryBase ── 11 BC 工厂改造（fix-01~11 跟进）
  P0-TD4 ResourceOwnershipChecker ── Payment/ReviewAfterSales 端点改造（fix-06/08 跟进）

中期（1-2 月）：
  M1 Guid→string 迁移 ── 依赖 P0-D5.1
  M2 跨域 Saga 编排 ── 依赖 P0-D4.1
  M3 Money 标准化 ── 依赖 P1-D3.1
  M4 缓存失效机制 ── 独立

长期（3-6 月）：
  L1 int64 字段废弃 ── 依赖 M1
  L2 ACL 适配器自动化 ── 依赖 D2 DTO 统一
  L3 BFF 聚合层重构 ── 独立
  L4 跨 BC 契约评审 ── 依赖 D1 对齐
```

---

## 附录：与分 BC 修复计划的协同关系

本计划聚焦"跨 BC 协调层"，各分 BC 修复计划负责 BC 内部具体实现：

| 本计划项 | 协同的分 BC 计划 | 协同内容 |
|---------|-----------------|---------|
| P0-D1.1 RefundCompletedEvent+ChannelRefundNo | fix-08-payment | Payment BC 发布方填充 ChannelRefundNo 字段 |
| P0-D1.2 ReviewSubmittedEvent+ShopId | fix-06-reviewaftersales | ReviewAfterSales 发布方填充 ShopId 字段（从订单反查） |
| P0-D1.2 ReviewSubmittedEvent+ShopId | fix-10-sellershop | SellerShop 消费侧改读 ShopId 字段 |
| P0-D4.1 Outbox 旁路 | fix-01-userauth / fix-05-promotion / fix-07-pointsmembership / fix-11-systemadmin | 各 BC 应用层删除 PublishAsync |
| P0-D5.1 GuidProtoConverter | fix-02-product / fix-04-order / fix-06-reviewaftersales / fix-10-sellershop | 各 BC gRPC 服务端双写 |
| P0-D5.1 GuidProtoConverter | fix-03-cart / fix-04-order | 各 BC gRPC 客户端改读 string 字段 |
| P0-D5.3 PointsMembership Confirm HTTP | fix-07-pointsmembership | HTTP 端点补全（本计划已含完整步骤） |
| P0-D6.1 DesignTimeFactoryBase | fix-01 ~ fix-11 | 各 BC 设计期工厂改为继承基类 |
| P0-TD4 ResourceOwnershipChecker | fix-06-reviewaftersales / fix-08-payment | 各 BC 端点调用 EnsureOwnerAsync |
| P1-D3.1 Money 不可变性 | fix-02-product / fix-05-promotion / fix-04-order / fix-03-cart | 各 BC 金额计算回归测试 |
| P1-D3.3 Entity.Id init | fix-01 ~ fix-12 | 各 BC 实体继承回归测试 |
| P1-D4.2 Order 乐观锁 | fix-04-order | OrderConfiguration 增加 IsConcurrencyToken |
| P1-D4.3 Saga 补偿幂等键 | fix-05-promotion | SeckillPreOccupationRecord 增加 CompensationId |
| P1-D5.2 PaymentGrpcService 硬编码零值 | fix-08-payment | 填充 gRPC 响应字段真实值 |
| P1-D5.4 ConsulConfigWatcher | fix-12-shared | 自定义 IOptionsChangeTokenSource |
| P2-D2.x ACL DTO 统一抽取 | fix-02 ~ fix-11 | 各 BC 引用共享 DTO |

---

## 附录：本计划不修改任何业务代码

本计划仅为修复实施计划文档，所有 P0 TDD 5 步骤中的"Step 3: 写最小实现"代码片段为**实施指南**，由后续执行人按步骤落地。本计划的 git 提交仅包含本文件 `fix-13-cross-bc-architecture.md`。
