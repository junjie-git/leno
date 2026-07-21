# PointsMembership BC 修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基于 07-pointsmembership.md 审计报告，制定 PointsMembership BC 全量问题的修复实施计划
**Architecture:** DDD 限界上下文，按 Domain/Application/Infrastructure/Api 四层治理
**Tech Stack:** .NET 10 + EF Core + MassTransit + RabbitMQ + Redis + gRPC + xUnit + FluentAssertions
**关联审计报告:** `docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md`

---

## 问题统计总览

| 严重度 | 总数 | ALREADY-FIXED | VERIFIED-NOT-REPRODUCIBLE | 待修复 |
|--------|------|---------------|---------------------------|--------|
| 🔴 P0  | 8    | 0             | 0                         | 8      |
| 🟡 P1  | 9    | 0             | 0                         | 9      |
| 🟢 P2  | 7    | 0             | 0                         | 7      |

> **说明**：审计报告 PM-H01 至 PM-H08 为 🔴 高风险问题，按任务要求 P0 必须给出 TDD 5 步骤；PM-M01 至 PM-M09 为 🟡 中风险问题，按 P1 给任务清单；PM-L01 至 PM-L07 为 🟢 低风险问题，按 P2 给任务清单。p0a-T6 与 T10 为既有计划已修复项，独立列出，不计入 PM-H01 至 PM-H08 任一项。

## 已修复问题清单（[ALREADY-FIXED]）

### [ALREADY-FIXED] p0a-T6：PointsInternalAppService.ConfirmAsync 占位实现补齐，Confirm gRPC RPC 改为真实调用

- **来源计划**：`docs/superpowers/plans/2026-07-20-p0a-placeholder-implementation.md`
- **验证状态**：已通过代码校验确认修复
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/IPointsInternalAppService.cs#L26-L32`：接口已新增 `ConfirmAsync(ConfirmPointsDto input, CancellationToken ct)` 方法
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/ConfirmPointsDto.cs#L1-L7`：`ConfirmPointsDto` record 已新建，含 `OrderId` 字段
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsInternalAppService.cs#L73-L84`：`ConfirmAsync` 实现已补齐，调用 `account.ConfirmDeduct(input.OrderId)` 并 `SaveEntitiesAsync`，无 `throw new NotImplementedException()` 等占位
  - `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs#L76-L88`：gRPC `Confirm` RPC 已改为真实调用 `_internalAppService.ConfirmAsync`，使用 `Guid.TryParse` 校验并构造 `ConfirmPointsDto`
- **后续仍需关注**：本修复仅补齐了 gRPC Confirm 链路，PM-H04 的 HTTP Confirm 端点缺失仍待修复（见 P0 详细计划）。

### [ALREADY-FIXED] T10：积分防腐层显式异常，PointsAntiCorruptionService.Freeze/Confirm/Release 移除 try-catch 吞异常

- **来源计划**：`.trae/specs/fix-critical-business-vulnerabilities/tasks.md`
- **验证状态**：已通过代码校验确认修复
  - `file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs#L41-L98`：`TryOffsetAsync` / `FreezeAsync` / `ReleaseAsync` / `ConfirmDeductionAsync` 全部经基类 `AntiCorruptionBase.ExecuteAsync` 模板方法包装，远程失败（网络异常、非 2xx、超时）经 `EnsureSuccessStatusCode(response, "xxx")` 统一抛 `AntiCorruptionException`，无 try-catch 静默吞异常
  - 类头注释（L11-L16）明确："所有远程失败（网络异常、非 2xx、超时）统一抛 `AntiCorruptionException`，不再静默返回 0；用户取消透传 `OperationCanceledException`"
- **后续仍需关注**：本修复位于 Order BC，但语义上影响 PointsMembership 调用契约。PM-H04 的 HTTP Confirm 端点缺失会导致 `ConfirmDeductionAsync` 收到 404 仍抛 `AntiCorruptionException`，需配合 PM-H04 修复。

---

## 问题清单总表

| 编号 | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|------|--------|---------|---------|--------|------|
| PM-H01 | 🔴 | Member.AddGrowthValue 在生产代码无任何调用方，V0-V4 等级体系失效 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs#L119-L133` | P0 | 待修复 |
| PM-H02 | 🔴 | PointsLedger.Create 永不被调用，积分流水永不落库，PointsExpiryService 永远过期 0 积分 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCorePointsAccountRepository.cs#L52-L58` | P0 | 待修复 |
| PM-H03 | 🔴 | 4 个 ReadModel 同步消费者订阅的集成事件在本 BC 中永不发布（死消费者） | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/PointsAccountCreatedReadModelSyncConsumer.cs#L13-L14` | P0 | 待修复 |
| PM-H04 | 🔴 | InternalPointsController 缺失 Confirm HTTP 端点，订单域 HTTP 防腐层 ConfirmDeductionAsync 必然 404 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L22-L53` | P0 | 待修复 |
| PM-H05 | 🔴 | ExchangeCouponAppService.ExchangeCouponAsync 未使用 Outbox，冻结积分与发布事件非原子 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/ExchangeCouponAppService.cs#L39-L76` | P0 | 待修复 |
| PM-H06 | 🔴 | ReviewApprovedEventConsumer Redis 计数为非原子读改写，并发会突破每日 5 条上限 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs#L43-L72` | P0 | 待修复 |
| PM-H07 | 🔴 | OrderCompletedEventConsumer 与 OrderAfterSalesWindowClosedEventConsumer 同时发放消费返积分，存在双倍发放风险 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs#L40-L72` | P0 | 待修复 |
| PM-H08 | 🔴 | OrderPaidEventConsumer 在 package 为 null 或 DurationDays<=0 时抛异常，导致消费者整体失败 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderPaidEventConsumer.cs#L52-L63` | P0 | 待修复 |
| PM-M01 | 🟡 | EfCorePointsAccountRepository.GetByFrozenOrderIdAsync 通过集合扫描定位订单，未利用索引 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCorePointsAccountRepository.cs#L36-L39` | P1 | 待修复 |
| PM-M02 | 🟡 | Member.AddGrowthValue 的 reason 参数被忽略 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs#L119-L133` | P1 | 待修复 |
| PM-M03 | 🟡 | PointsAppService.CheckInAsync 使用 DateTime.UtcNow 计算 today，签到日期与用户时区错位 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsAppService.cs#L36-L51` | P1 | 待修复 |
| PM-M04 | 🟡 | UserMembership.Activate 与 OrderPaidEventConsumer 之间无并发控制，同一订单重复事件可能导致重复激活 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/UserMembership.cs#L84-L108` | P1 | 待修复 |
| PM-M05 | 🟡 | MemberLevelUpgradedReadModelSyncConsumer 期望消费集成事件版 MemberLevelUpgradedEvent，但 mapper 永不发布该事件 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/MemberLevelUpgradedReadModelSyncConsumer.cs#L15-L16` | P1 | 待修复 |
| PM-M06 | 🟡 | IPointsOffsetAppService 接口定义在 Domain 层，PointsOffsetAppService 实现位于 Application 层，防腐层职责混乱 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Services/IPointsOffsetAppService.cs#L1-L35` | P1 | 待修复 |
| PM-M07 | 🟡 | PointsAppService.GetLedgerAsync 返回空列表，注释承认"当前域尚未定义" | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsAppService.cs#L86-L91` | P1 | 待修复 |
| PM-M08 | 🟡 | 领域事件与集成事件同名 MemberLevelUpgradedEvent，依赖文件路径与别名消歧 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MemberLevelUpgradedEvent.cs#L1-L27` | P1 | 待修复 |
| PM-M09 | 🟡 | Member.CheckUpgrade 与 Member.EvaluateGrowthLevel 两套等级体系并存但消费链路仅打通前者 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs#L97-L117` | P1 | 待修复 |
| PM-L01 | 🟢 | 后台服务 Task.Delay 在异常路径后仍延后一日，且无指数退避 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/MemberLevelEvaluationJob.cs#L32-L49` | P2 | 部分待修复 |
| PM-L02 | 🟢 | 硬编码 12 个月过期阈值与 TimeSpan.FromHours(25) Redis Key 过期时间 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/PointsExpiryService.cs#L16` | P2 | 待修复 |
| PM-L03 | 🟢 | MemberLevel.EvaluateLevel 存在双重排序，可优化为单次 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/MemberLevel.cs#L99-L111` | P2 | 待修复 |
| PM-L04 | 🟢 | InternalPointsController 每个端点使用双 [HttpPost] 路由（含 [Obsolete]） | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L23-L25` | P2 | 待修复 |
| PM-L05 | 🟢 | gRPC 服务在 TrialOffset/Freeze/Release 中使用 new Guid(request.UserId)，格式非法时抛 ArgumentException | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs#L37` | P2 | 待修复 |
| PM-L06 | 🟢 | ReviewApprovedEventConsumer 使用 DateTime.UtcNow.ToString("yyyyMMdd") 计算 Redis Key 的"日"，与用户时区错位 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs#L44` | P2 | 待修复 |
| PM-L07 | 🟢 | OrderCancelledEventConsumer 与 OrderPaidEventConsumer 均调用 GetByFrozenOrderIdAsync，但失败语义不一致 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs#L100-L105` | P2 | 待修复 |

> **PM-L01 校验说明**：审计报告原描述包含两点（① 异常路径后仍延后一日；② 无 StoppingToken 主动取消时的快速退出保障）。代码校验发现第二点已修复——`MemberLevelEvaluationJob.cs#L38-L41` 与 `PointsExpiryService.cs#L41-L44` 均已显式 `catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) break;` 跳出循环。第一点（异常后无指数退避，仍固定 24 小时）未修复，故 PM-L01 整体仍标记为"部分待修复"。

---

## P0 详细修复计划（TDD 5 步骤）

### P0-PM-H01 修复 Member.AddGrowthValue 在生产代码无调用方

**审计位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs#L119-L133`、`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/MemberLevelEvaluationJob.cs#L80-L90`

**根因**：消费返积分链路（`OrderCompletedEventConsumer` / `OrderAfterSalesWindowClosedEventConsumer`）与签到返积分链路（`PointsAppService.CheckInAsync`）只调用 `account.Earn(...)`，未联动调用 `member.AddGrowthValue(...)`。成长值体系与积分入账链路未打通。

**修复方向**：在三个积分入账链路中，按"1 积分 = 1 成长值"的简化规则同步累加会员成长值，并触发 `EvaluateGrowthLevel`。考虑 PM-H07 修复后仅保留 `OrderAfterSalesWindowClosedEventConsumer` 发放消费返积分，故成长值累加仅在以下三处补齐：
1. `OrderAfterSalesWindowClosedEventConsumer`（消费返积分）
2. `PointsAppService.CheckInAsync`（签到返积分）
3. `ReviewApprovedEventConsumer`（评价返积分）

**步骤 1：编写失败测试**

测试文件：`src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Consumers/OrderAfterSalesWindowClosedEventConsumerGrowthTests.cs`

```csharp
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.PointsMembership.Domain.Tests.Consumers;

public sealed class OrderAfterSalesWindowClosedEventConsumerGrowthTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IMembershipLevelRepository> _levelRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyMock = new();
    private readonly OrderAfterSalesWindowClosedEventConsumer _consumer;

    public OrderAfterSalesWindowClosedEventConsumerGrowthTests()
    {
        _idempotencyMock.Setup(s => s.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _idempotencyMock.Setup(s => s.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _levelRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MembershipLevel>());

        _consumer = new OrderAfterSalesWindowClosedEventConsumer(
            _accountRepoMock.Object,
            _memberRepoMock.Object,
            _levelRepoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<OrderAfterSalesWindowClosedEventConsumer>.Instance,
            _idempotencyMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_Accumulate_GrowthValue_Equal_To_Points()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Consumption, 100, "种子积分");
        var member = Member.Create(MemberId, UserId);

        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var evt = new OrderAfterSalesWindowClosedEvent(
            OrderId, UserId, paidAmount: 80m, windowClosedAt: DateTime.UtcNow);

        await _consumer.ConsumeAsync(evt, CancellationToken.None);

        Assert.Equal(80, member.GrowthValue);
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Accumulate_GrowthValue_When_Points_Zero()
    {
        var member = Member.Create(MemberId, UserId);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var evt = new OrderAfterSalesWindowClosedEvent(
            OrderId, UserId, paidAmount: 0.4m, windowClosedAt: DateTime.UtcNow);

        await _consumer.ConsumeAsync(evt, CancellationToken.None);

        Assert.Equal(0, member.GrowthValue);
    }
}
```

测试文件：`src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsAppServiceCheckInGrowthTests.cs`

```csharp
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.PointsMembership.Application.Tests;

public sealed class PointsAppServiceCheckInGrowthTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<ICheckInRecordRepository> _checkInRepoMock = new();
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly PointsAppService _service;

    public PointsAppServiceCheckInGrowthTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _checkInRepoMock.Setup(r => r.GetByUserIdAndDateAsync(UserId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        _checkInRepoMock.Setup(r => r.GetLatestByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CheckInRecord?)null);
        _checkInRepoMock.Setup(r => r.AddAsync(It.IsAny<CheckInRecord>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new PointsAppService(
            _accountRepoMock.Object,
            _checkInRepoMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task CheckInAsync_Should_Accumulate_GrowthValue_For_Member()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        var member = Member.Create(MemberId, UserId);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        await _service.CheckInAsync(UserId, CancellationToken.None);

        Assert.Equal(10, member.GrowthValue);
    }
}
```

> **注**：上述测试假设 `PointsAppService` 注入 `IMemberRepository`（修复后新增依赖），且 `OrderAfterSalesWindowClosedEventConsumer` 在调用 `account.Earn` 后调用 `member.AddGrowthValue(points, reason)`。测试中 `ConsumeAsync` 为 `IntegrationEventConsumerBase<T>` 暴露的入口方法，实际签名以基类为准。

**步骤 2：运行测试验证失败**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~OrderAfterSalesWindowClosedEventConsumerGrowthTests"
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/Leno.PointsMembership.Application.Tests.csproj \
  --filter "FullyQualifiedName~PointsAppServiceCheckInGrowthTests"
```

预期：两个测试均编译失败（`OrderAfterSalesWindowClosedEventConsumer` 未调用 `AddGrowthValue`，`PointsAppService` 未注入 `IMemberRepository`）。

**步骤 3：写最小实现**

修改 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs` 中 `OrderAfterSalesWindowClosedEventConsumer.HandleAsync` 方法（在 `member.CheckUpgrade(thresholds);` 之前插入成长值累加）：

```csharp
// 累加会员消费金额并检查升级
if (integrationEvent.PaidAmount > 0)
{
    var member = await _memberRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
    if (member is not null)
    {
        if (points > 0)
        {
            member.AddGrowthValue(points, $"订单 {integrationEvent.OrderId} 消费返积分");
        }
        member.AddConsumption(integrationEvent.PaidAmount);

        var levels = await _levelRepository.GetAllEnabledAsync(ct);
        var thresholds = levels
            .Select(l => new LevelThreshold(l.Level, l.Name, l.MinConsumption))
            .ToList();
        member.CheckUpgrade(thresholds);
    }
}
```

修改 `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsAppService.cs`：

1. 构造函数新增 `IMemberRepository _memberRepository` 依赖；
2. 在 `CheckInAsync` 中 `account.Earn(...)` 之后，加载 `member` 并调用 `member.AddGrowthValue(pointsAwarded, $"每日签到（连续 {continuousDays} 天）")`。

```csharp
private readonly IMemberRepository _memberRepository;

public PointsAppService(
    IPointsAccountRepository accountRepository,
    ICheckInRecordRepository checkInRepository,
    IMemberRepository memberRepository,
    IUnitOfWork unitOfWork)
{
    _accountRepository = accountRepository;
    _checkInRepository = checkInRepository;
    _memberRepository = memberRepository;
    _unitOfWork = unitOfWork;
}

public async Task<CheckInResultDto> CheckInAsync(Guid userId, CancellationToken ct = default)
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);

    var existing = await _checkInRepository.GetByUserIdAndDateAsync(userId, today, ct);
    if (existing is not null)
    {
        throw new PointsDomainException("今日已签到，不可重复签到", "CHECKIN_ALREADY");
    }

    var latest = await _checkInRepository.GetLatestByUserIdAsync(userId, ct);
    var continuousDays = latest is not null && latest.CheckInDate == today.AddDays(-1)
        ? latest.ContinuousDays + 1
        : 1;

    var pointsAwarded = continuousDays >= 30
        ? CheckInMonthlyBonus
        : continuousDays >= 7
            ? CheckInWeeklyBonus
            : CheckInBasePoints;

    var record = CheckInRecordAggregate.CheckIn(
        Guid.NewGuid(), userId, today, continuousDays, pointsAwarded);
    await _checkInRepository.AddAsync(record, ct);

    var account = await RequireAccountAsync(userId, ct);
    account.Earn(PointsSource.CheckIn, pointsAwarded, $"每日签到（连续 {continuousDays} 天）");

    var member = await _memberRepository.GetByUserIdAsync(userId, ct);
    if (member is not null)
    {
        member.AddGrowthValue(pointsAwarded, $"每日签到（连续 {continuousDays} 天）");
    }

    await _unitOfWork.SaveEntitiesAsync(ct);

    return new CheckInResultDto
    {
        RecordId = record.Id,
        UserId = record.UserId,
        CheckInDate = record.CheckInDate,
        ContinuousDays = record.ContinuousDays,
        PointsAwarded = record.PointsAwarded
    };
}
```

修改 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs`：在 `account.Earn(...)` 之后加载 `member` 并调用 `member.AddGrowthValue(ReviewPointsPerReview, $"评价 {integrationEvent.ReviewId} 返积分")`。

```csharp
account.Earn(PointsSource.Review, ReviewPointsPerReview,
    $"评价 {integrationEvent.ReviewId} 返积分");

var member = await _memberRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
if (member is not null)
{
    member.AddGrowthValue(ReviewPointsPerReview, $"评价 {integrationEvent.ReviewId} 返积分");
}
```

并在 `ReviewApprovedEventConsumer` 构造函数注入 `IMemberRepository`。

**步骤 4：运行测试验证通过**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~OrderAfterSalesWindowClosedEventConsumerGrowthTests"
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/Leno.PointsMembership.Application.Tests.csproj \
  --filter "FullyQualifiedName~PointsAppServiceCheckInGrowthTests"
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~ReviewApprovedEventConsumerTests"
```

预期：所有测试通过。

**步骤 5：提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Consumers/OrderAfterSalesWindowClosedEventConsumerGrowthTests.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsAppServiceCheckInGrowthTests.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsAppService.cs
git commit -m "修复 PM-H01：在消费返积分、签到返积分、评价返积分链路调用 Member.AddGrowthValue，打通 V0-V4 成长值等级体系"
```

---

### P0-PM-H02 修复 PointsLedger.Create 永不被调用，积分流水永不落库

**审计位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCorePointsAccountRepository.cs#L52-L58`、`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/PointsExpiryService.cs#L104-L143`

**根因**：`PointsAccount.Earn/Freeze/ConfirmDeduct/Release/ConsumePoints/RevertPoints/ExpirePoints` 七个状态变更方法均只 `AddDomainEvent`，没有任何一处 `PointsLedger.Create(...)` 写入流水。`PointsLedgers` 表永远为空。

**修复方向**：在 `PointsAccount` 聚合根内部维护 `List<PointsLedger> _ledgers` 私有集合（EF Core 导航属性），在每个状态变更方法内同事务 `PointsLedger.Create(...)` 后 `Add` 到集合，EF Core 跟踪自动落库。

**步骤 1：编写失败测试**

测试文件：`src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/PointsLedgerWriteTests.cs`

```csharp
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.ValueObjects;
using Xunit;

namespace Leno.PointsMembership.Domain.Tests;

public sealed class PointsLedgerWriteTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    [Fact]
    public void Earn_Should_Write_PointsLedger_With_Earn_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);

        account.Earn(PointsSource.CheckIn, 50, "签到返积分");

        var ledger = Assert.Single(account.Ledgers);
        Assert.Equal(AccountId, ledger.AccountId);
        Assert.Equal(PointsTxType.Earn, ledger.TxType);
        Assert.Equal(50, ledger.Amount);
        Assert.Equal(50, ledger.BalanceAfter);
        Assert.Equal(PointsSource.CheckIn, ledger.Source);
        Assert.Equal("签到返积分", ledger.Reason);
    }

    [Fact]
    public void Freeze_Should_Write_PointsLedger_With_Freeze_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 200, "种子积分");

        var orderId = Guid.NewGuid();
        account.Freeze(100, orderId);

        var freezeLedger = account.Ledgers.Single(l => l.TxType == PointsTxType.Freeze);
        Assert.Equal(100, freezeLedger.Amount);
        Assert.Equal(100, freezeLedger.BalanceAfter);
        Assert.Equal(orderId, freezeLedger.ReferenceId);
    }

    [Fact]
    public void ConfirmDeduct_Should_Write_PointsLedger_With_Consume_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 200, "种子积分");
        var orderId = Guid.NewGuid();
        account.Freeze(100, orderId);

        account.ConfirmDeduct(orderId);

        var consumeLedger = account.Ledgers.Single(l => l.TxType == PointsTxType.Consume);
        Assert.Equal(100, consumeLedger.Amount);
        Assert.Equal(0, consumeLedger.BalanceAfter);
    }

    [Fact]
    public void Release_Should_Write_PointsLedger_With_Release_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 200, "种子积分");
        var orderId = Guid.NewGuid();
        account.Freeze(100, orderId);

        account.Release(orderId);

        var releaseLedger = account.Ledgers.Single(l => l.TxType == PointsTxType.Release);
        Assert.Equal(100, releaseLedger.Amount);
        Assert.Equal(200, releaseLedger.BalanceAfter);
    }

    [Fact]
    public void ExpirePoints_Should_Write_PointsLedger_With_Expire_Type()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 100, "种子积分");

        account.ExpirePoints(30);

        var expireLedger = account.Ledgers.Single(l => l.TxType == PointsTxType.Expire);
        Assert.Equal(30, expireLedger.Amount);
        Assert.Equal(70, expireLedger.BalanceAfter);
    }
}
```

**步骤 2：运行测试验证失败**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~PointsLedgerWriteTests"
```

预期：编译失败（`PointsAccount` 无 `Ledgers` 属性）。

**步骤 3：写最小实现**

修改 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/PointsAccount.cs`：

1. 新增 `public List<PointsLedger> Ledgers { get; private set; } = new();` 导航属性；
2. 在 `Earn` 方法末尾追加：

```csharp
Ledgers.Add(PointsLedger.Create(
    Guid.NewGuid(), Id, PointsTxType.Earn, amount, Balance, source, Guid.Empty, reason, DateTime.UtcNow));
```

3. 在 `Freeze` 方法末尾追加：

```csharp
Ledgers.Add(PointsLedger.Create(
    Guid.NewGuid(), Id, PointsTxType.Freeze, amount, Balance, PointsSource.Offset, orderId, $"冻结-订单{orderId}", DateTime.UtcNow));
```

4. 在 `ConfirmDeduct` 方法末尾追加：

```csharp
Ledgers.Add(PointsLedger.Create(
    Guid.NewGuid(), Id, PointsTxType.Consume, entry.Amount, Balance, PointsSource.Offset, orderId, $"确认扣减-订单{orderId}", DateTime.UtcNow));
```

5. 在 `Release` 方法末尾追加：

```csharp
Ledgers.Add(PointsLedger.Create(
    Guid.NewGuid(), Id, PointsTxType.Release, entry.Amount, Balance, PointsSource.Offset, orderId, $"释放-订单{orderId}", DateTime.UtcNow));
```

6. 在 `ConsumePoints` 方法末尾追加：

```csharp
Ledgers.Add(PointsLedger.Create(
    Guid.NewGuid(), Id, PointsTxType.Consume, amount, Balance, PointsSource.Offset, referenceId, reason, DateTime.UtcNow));
```

7. 在 `RevertPoints` 方法末尾追加：

```csharp
Ledgers.Add(PointsLedger.Create(
    Guid.NewGuid(), Id, PointsTxType.Revert, amount, Balance, PointsSource.Refund, referenceId, reason, DateTime.UtcNow));
```

8. 在 `ExpirePoints` 方法末尾追加：

```csharp
Ledgers.Add(PointsLedger.Create(
    Guid.NewGuid(), Id, PointsTxType.Expire, points, Balance, PointsSource.Activity, Guid.Empty, "积分过期清理", DateTime.UtcNow));
```

修改 `src/Services/PointsMembership/Leno.PointsMembership.Domain/ValueObjects/PointsMembershipEnums.cs`：补齐 `PointsTxType` 枚举值 `Earn / Freeze / Consume / Release / Revert / Expire`（若已存在则跳过）。

修改 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Configurations/PointsAccountConfiguration.cs`：新增 `HasMany(a => a.Ledgers).WithOne().HasForeignKey(l => l.AccountId)` 关系映射（若已配置则跳过）。

**步骤 4：运行测试验证通过**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~PointsLedgerWriteTests"
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~PointsAccountConsumeRevertTests"
```

预期：所有测试通过，流水表写入路径打通，`PointsExpiryService.CalculateExpiredPointsAsync` 不再返回 0。

**步骤 5：提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/PointsAccount.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Domain/ValueObjects/PointsMembershipEnums.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Configurations/PointsAccountConfiguration.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/PointsLedgerWriteTests.cs
git commit -m "修复 PM-H02：PointsAccount 七个状态变更方法同事务写入 PointsLedger 流水，打通积分审计与过期任务链路"
```

---

### P0-PM-H03 修复 4 个 ReadModel 同步消费者订阅的集成事件在本 BC 中永不发布

**审计位置**：
- `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/PointsAccountCreatedReadModelSyncConsumer.cs#L13-L14`
- `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/PointsAdjustedReadModelSyncConsumer.cs#L15-L16`
- `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/MemberRegisteredReadModelSyncConsumer.cs#L13-L14`
- `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/MemberLevelUpgradedReadModelSyncConsumer.cs#L15-L16`
- `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/EventBus/PointsMembershipIntegrationEventMapper.cs#L17-L42`

**根因**：4 个 ReadModel 同步消费者订阅 `PointsAccountCreatedEvent` / `PointsAdjustedEvent` / `MemberRegisteredEvent` / 集成事件版 `MemberLevelUpgradedEvent`，但本 BC 的 `PointsMembershipIntegrationEventMapper` 仅注册 6 类映射，未发布上述 4 类事件。`PointsAccount.Create` 工厂方法也未 `AddDomainEvent` 任何"账户创建"领域事件。`UserRegisteredEventConsumer` 创建 `Member` 时未发布 `MemberRegisteredEvent`。

**修复方向**：
1. 在 `PointsAccount` 新增 `PointsAccountCreatedDomainEvent` 领域事件，工厂方法 `Create` 内 `AddDomainEvent`；在 mapper 中翻译为 `PointsAccountCreatedEvent` 集成事件；
2. 在 `PointsAccount.Earn/Freeze/ConfirmDeduct/Release/ConsumePoints/RevertPoints/ExpirePoints` 七个方法内 `AddDomainEvent(new PointsAdjustedDomainEvent(...))`，mapper 翻译为 `PointsAdjustedEvent` 集成事件；
3. 在 `Member.Create` 工厂方法内 `AddDomainEvent(new MemberRegisteredDomainEvent(...))`，mapper 翻译为 `MemberRegisteredEvent` 集成事件；
4. 在 `Member.CheckUpgrade` 内 `AddDomainEvent` 领域事件后，mapper 增加翻译为集成事件版 `MemberLevelUpgradedEvent`（含 `MemberId` 字段，需查 `Member.Id` 填充）。

> **说明**：PM-M05 与 PM-M08 与本问题同源，建议本步骤一并修复（重命名集成事件为 `MemberLevelUpgradedIntegrationEvent`，并修正 mapper 翻译方向）。

**步骤 1：编写失败测试**

测试文件：`src/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/ReadModels/PointsAccountCreatedPublishTests.cs`

```csharp
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Infrastructure.EventBus;
using Xunit;

namespace Leno.PointsMembership.Infrastructure.Tests.ReadModels;

public sealed class PointsAccountCreatedPublishTests
{
    [Fact]
    public void PointsAccount_Create_Should_Raise_PointsAccountCreatedDomainEvent()
    {
        var userId = Guid.NewGuid();

        var account = PointsAccount.Create(Guid.NewGuid(), userId);

        var domainEvent = account.DomainEvents.OfType<PointsAccountCreatedDomainEvent>().SingleOrDefault();
        Assert.NotNull(domainEvent);
        Assert.Equal(account.Id, domainEvent!.AccountId);
        Assert.Equal(userId, domainEvent.UserId);
    }

    [Fact]
    public void Member_Create_Should_Raise_MemberRegisteredDomainEvent()
    {
        var userId = Guid.NewGuid();

        var member = Member.Create(Guid.NewGuid(), userId);

        var domainEvent = member.DomainEvents.OfType<MemberRegisteredDomainEvent>().SingleOrDefault();
        Assert.NotNull(domainEvent);
        Assert.Equal(member.Id, domainEvent!.MemberId);
        Assert.Equal(userId, domainEvent.UserId);
    }

    [Fact]
    public void PointsAccount_Earn_Should_Raise_PointsAdjustedDomainEvent()
    {
        var account = PointsAccount.Create(Guid.NewGuid(), Guid.NewGuid());

        account.Earn(PointsSource.CheckIn, 30, "签到返积分");

        var domainEvent = account.DomainEvents.OfType<PointsAdjustedDomainEvent>().SingleOrDefault();
        Assert.NotNull(domainEvent);
        Assert.Equal(account.Id, domainEvent!.AccountId);
        Assert.Equal(30, domainEvent.Delta);
    }
}
```

测试文件：`src/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/EventBus/PointsMembershipMapperPublishTests.cs`

```csharp
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.PointsMembership.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Xunit;

namespace Leno.PointsMembership.Infrastructure.Tests.EventBus;

public sealed class PointsMembershipMapperPublishTests
{
    [Fact]
    public void Mapper_Should_Translate_PointsAccountCreatedDomainEvent_To_IntegrationEvent()
    {
        var mapper = new PointsMembershipIntegrationEventMapper();
        var domainEvent = new PointsAccountCreatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), 0, DateTime.UtcNow);

        var integrationEvents = mapper.Translate(domainEvent).ToList();

        var typed = Assert.Single(integrationEvents);
        var created = Assert.IsType<PointsAccountCreatedEvent>(typed);
        Assert.Equal(domainEvent.AccountId, created.PointsAccountId);
    }

    [Fact]
    public void Mapper_Should_Translate_PointsAdjustedDomainEvent_To_IntegrationEvent()
    {
        var mapper = new PointsMembershipIntegrationEventMapper();
        var domainEvent = new PointsAdjustedDomainEvent(Guid.NewGuid(), 50, "签到返积分", DateTime.UtcNow);

        var integrationEvents = mapper.Translate(domainEvent).ToList();

        var typed = Assert.Single(integrationEvents);
        var adjusted = Assert.IsType<PointsAdjustedEvent>(typed);
        Assert.Equal(50, adjusted.Delta);
    }

    [Fact]
    public void Mapper_Should_Translate_MemberRegisteredDomainEvent_To_IntegrationEvent()
    {
        var mapper = new PointsMembershipIntegrationEventMapper();
        var memberId = Guid.NewGuid();
        var domainEvent = new MemberRegisteredDomainEvent(memberId, Guid.NewGuid(), 1, DateTime.UtcNow);

        var integrationEvents = mapper.Translate(domainEvent).ToList();

        var typed = Assert.Single(integrationEvents);
        var registered = Assert.IsType<MemberRegisteredEvent>(typed);
        Assert.Equal(memberId, registered.MemberId);
    }
}
```

> **注**：`AggregateRoot.DomainEvents` 集合的具体名称以 `Leno.SharedKernel.Abstractions.AggregateRoot` 基类为准（实际为 `IReadOnlyCollection<DomainEventBase>`，通过 `AddDomainEvent` 累加）。`mapper.Translate(domainEvent)` 方法签名以 `IntegrationEventMapperBase` 为准。

**步骤 2：运行测试验证失败**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/Leno.PointsMembership.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~PointsAccountCreatedPublishTests|FullyQualifiedName~PointsMembershipMapperPublishTests"
```

预期：编译失败（`PointsAccountCreatedDomainEvent` / `MemberRegisteredDomainEvent` / `PointsAdjustedDomainEvent` 领域事件类不存在，mapper 未注册翻译）。

**步骤 3：写最小实现**

1. 新建 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsAccountCreatedDomainEvent.cs`：

```csharp
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

public sealed class PointsAccountCreatedDomainEvent : DomainEventBase
{
    public Guid AccountId { get; init; }
    public Guid UserId { get; init; }
    public int InitialPoints { get; init; }
    public DateTime CreatedAt { get; init; }

    public PointsAccountCreatedDomainEvent(Guid accountId, Guid userId, int initialPoints, DateTime createdAt)
        : base(accountId)
    {
        AccountId = accountId;
        UserId = userId;
        InitialPoints = initialPoints;
        CreatedAt = createdAt;
    }
}
```

2. 新建 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsAdjustedDomainEvent.cs`：

```csharp
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

public sealed class PointsAdjustedDomainEvent : DomainEventBase
{
    public Guid AccountId { get; init; }
    public int Delta { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime AdjustedAt { get; init; }

    public PointsAdjustedDomainEvent(Guid accountId, int delta, string reason, DateTime adjustedAt)
        : base(accountId)
    {
        AccountId = accountId;
        Delta = delta;
        Reason = reason ?? string.Empty;
        AdjustedAt = adjustedAt;
    }
}
```

3. 新建 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MemberRegisteredDomainEvent.cs`：

```csharp
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

public sealed class MemberRegisteredDomainEvent : DomainEventBase
{
    public Guid MemberId { get; init; }
    public Guid UserId { get; init; }
    public int Level { get; init; }
    public DateTime RegisteredAt { get; init; }

    public MemberRegisteredDomainEvent(Guid memberId, Guid userId, int level, DateTime registeredAt)
        : base(memberId)
    {
        MemberId = memberId;
        UserId = userId;
        Level = level;
        RegisteredAt = registeredAt;
    }
}
```

4. 修改 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/PointsAccount.cs`：
   - 在 `Create` 工厂方法 `return new PointsAccount(...) { ... };` 之前 `AddDomainEvent(new PointsAccountCreatedDomainEvent(...))`。注意：工厂方法内无法直接调 `AddDomainEvent`（构造未完成），可在工厂方法返回后由调用方触发；或在 `Create` 内构造后立即 `account.AddDomainEvent(...)` 再返回。采用后者：

```csharp
public static PointsAccount Create(Guid accountId, Guid userId)
{
    if (userId == Guid.Empty)
    {
        throw new PointsDomainException("UserId 不可为空", "POINTS_USER_EMPTY");
    }

    var account = new PointsAccount(accountId == Guid.Empty ? Guid.NewGuid() : accountId)
    {
        UserId = userId,
        Balance = 0,
        FrozenBalance = 0,
        TotalEarned = 0,
        TotalSpent = 0
    };
    account.AddDomainEvent(new PointsAccountCreatedDomainEvent(account.Id, userId, 0, DateTime.UtcNow));
    return account;
}
```

   - 在 `Earn` 末尾追加 `AddDomainEvent(new PointsAdjustedDomainEvent(Id, amount, reason, DateTime.UtcNow));`
   - 在 `Freeze` 末尾追加 `AddDomainEvent(new PointsAdjustedDomainEvent(Id, -amount, $"冻结-订单{orderId}", DateTime.UtcNow));`
   - 在 `ConfirmDeduct` 末尾追加 `AddDomainEvent(new PointsAdjustedDomainEvent(Id, -entry.Amount, $"确认扣减-订单{orderId}", DateTime.UtcNow));`
   - 在 `Release` 末尾追加 `AddDomainEvent(new PointsAdjustedDomainEvent(Id, entry.Amount, $"释放-订单{orderId}", DateTime.UtcNow));`
   - 在 `ConsumePoints` 末尾追加 `AddDomainEvent(new PointsAdjustedDomainEvent(Id, -amount, reason, DateTime.UtcNow));`
   - 在 `RevertPoints` 末尾追加 `AddDomainEvent(new PointsAdjustedDomainEvent(Id, -amount, reason, DateTime.UtcNow));`
   - 在 `ExpirePoints` 末尾追加 `AddDomainEvent(new PointsAdjustedDomainEvent(Id, -points, "积分过期清理", DateTime.UtcNow));`

5. 修改 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs`：在 `Create` 工厂方法返回前 `AddDomainEvent(new MemberRegisteredDomainEvent(...))`：

```csharp
public static Member Create(Guid memberId, Guid userId)
{
    if (userId == Guid.Empty)
    {
        throw new PointsDomainException("UserId 不可为空", "POINTS_USER_EMPTY");
    }

    var now = DateTime.UtcNow;
    var member = new Member(memberId == Guid.Empty ? Guid.NewGuid() : memberId)
    {
        UserId = userId,
        CurrentLevel = 1,
        TotalConsumption = 0,
        JoinedAt = now,
        LevelUpgradedAt = now,
        Status = MemberStatus.Active,
        GrowthValue = 0,
        GrowthValueUpdatedAt = now,
        CurrentGrowthLevel = 0
    };
    member.AddDomainEvent(new MemberRegisteredDomainEvent(member.Id, userId, 1, now));
    return member;
}
```

6. 修改 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/EventBus/PointsMembershipIntegrationEventMapper.cs`，新增 4 类映射：

```csharp
// PointsAccountCreatedDomainEvent → PointsAccountCreatedEvent（积分账户读模型同步）
RegisterHandler<PointsAccountCreatedDomainEvent, PointsAccountCreatedEvent>(e =>
    new PointsAccountCreatedEvent(e.AccountId, e.UserId, e.InitialPoints, e.CreatedAt));

// PointsAdjustedDomainEvent → PointsAdjustedEvent（积分账户读模型同步）
RegisterHandler<PointsAdjustedDomainEvent, PointsAdjustedEvent>(e =>
    new PointsAdjustedEvent(e.AccountId, e.Delta, e.Reason, e.AdjustedAt));

// MemberRegisteredDomainEvent → MemberRegisteredEvent（会员读模型同步）
RegisterHandler<MemberRegisteredDomainEvent, MemberRegisteredEvent>(e =>
    new MemberRegisteredEvent(e.MemberId, e.UserId, e.Level, e.RegisteredAt));

// DomainMemberLevelUpgradedEvent → MemberLevelUpgradedEvent（集成事件版，会员等级升级读模型同步）
RegisterHandler<DomainMemberLevelUpgradedEvent, MemberLevelUpgradedEvent>(e =>
{
    // 领域事件仅含 UserId，需在聚合根层补充 MemberId；此处简化为在聚合根 CheckUpgrade 时已填充 MemberId 字段
    // 实际实现需在 Member.CheckUpgrade 内 AddDomainEvent 时传入 Member.Id
    throw new InvalidOperationException("MemberId 应在领域事件发布时填充");
});
```

> **关键**：为正确填充集成事件版 `MemberLevelUpgradedEvent` 的 `MemberId` 字段，需修改领域事件 `MemberLevelUpgradedEvent` 增加 `MemberId` 字段（与 PM-M08 修复同步推进），或在 mapper 中通过 `IMemberRepository` 反查（不推荐，mapper 应保持纯翻译）。推荐方案：领域事件 `MemberLevelUpgradedEvent` 增加 `MemberId` 字段：

修改 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MemberLevelUpgradedEvent.cs`：

```csharp
public sealed class MemberLevelUpgradedEvent : DomainEventBase
{
    public Guid MemberId { get; init; }
    public Guid UserId { get; init; }
    public int OldLevel { get; init; }
    public int NewLevel { get; init; }
    public DateTime UpgradedAt { get; init; }

    public MemberLevelUpgradedEvent(Guid memberId, Guid userId, int oldLevel, int newLevel, DateTime upgradedAt)
        : base(userId)
    {
        MemberId = memberId;
        UserId = userId;
        OldLevel = oldLevel;
        NewLevel = newLevel;
        UpgradedAt = upgradedAt;
    }
}
```

修改 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs` 中 `CheckUpgrade`：

```csharp
AddDomainEvent(new MemberLevelUpgradedEvent(Id, UserId, oldLevel, CurrentLevel, LevelUpgradedAt));
```

修改 mapper 中第 4 类映射：

```csharp
RegisterHandler<DomainMemberLevelUpgradedEvent, MemberLevelUpgradedEvent>(e =>
    new MemberLevelUpgradedEvent(e.MemberId, e.NewLevel, e.UpgradedAt));
```

> **同步修复 PM-M05**：mapper 现在发布集成事件版 `MemberLevelUpgradedEvent`（含 `MemberId` 字段），`MemberLevelUpgradedReadModelSyncConsumer` 订阅的事件将正常抵达。
> **同步修复 PM-M08**：领域事件与集成事件虽仍同名，但 mapper 与消费者字段访问已统一为 `MemberId`，建议在后续迭代中将集成事件版重命名为 `MemberLevelUpgradedIntegrationEvent`（与本计划 PM-M08 任务清单一致）。

**步骤 4：运行测试验证通过**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/Leno.PointsMembership.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~PointsAccountCreatedPublishTests|FullyQualifiedName~PointsMembershipMapperPublishTests"
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/Leno.PointsMembership.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~MemberReadModelSyncConsumerTests|FullyQualifiedName~PointsAccountReadModelSyncConsumerTests"
```

预期：所有测试通过，4 个 ReadModel 同步消费者订阅的事件正常发布。

**步骤 5：提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsAccountCreatedDomainEvent.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsAdjustedDomainEvent.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MemberRegisteredDomainEvent.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MemberLevelUpgradedEvent.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/PointsAccount.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/EventBus/PointsMembershipIntegrationEventMapper.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/ReadModels/PointsAccountCreatedPublishTests.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/EventBus/PointsMembershipMapperPublishTests.cs
git commit -m "修复 PM-H03：补齐 4 个 ReadModel 同步消费者的事件发布方，PointsAccount/Member 工厂与状态变更方法发布对应领域事件，mapper 翻译为集成事件"
```

---

### P0-PM-H04 修复 InternalPointsController 缺失 Confirm HTTP 端点

**审计位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L22-L53`、`file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs#L89-L98`

**根因**：`InternalPointsController` 仅暴露 `trial-offset` / `freeze` / `release` 三个 HTTP 端点，缺 `confirm`。订单域 `PointsAntiCorruptionService.ConfirmDeductionAsync` 调用 `internal/v1/points/confirm` 必然 404。

**修复方向**：在 `InternalPointsController` 新增 `ConfirmAsync` HTTP 端点，路由 `internal/v1/points/confirm`，调用 `IPointsInternalAppService.ConfirmAsync`（p0a-T6 已实现）。

**步骤 1：编写失败测试**

测试文件：`src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/InternalPointsControllerConfirmTests.cs`

```csharp
using Leno.PointsMembership.Api.Controllers;
using Leno.PointsMembership.Application;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.PointsMembership.Api.Tests;

public sealed class InternalPointsControllerConfirmTests
{
    private readonly Mock<IPointsInternalAppService> _serviceMock = new();
    private readonly InternalPointsController _controller;

    public InternalPointsControllerConfirmTests()
    {
        _controller = new InternalPointsController(_serviceMock.Object);
    }

    [Fact]
    public async Task ConfirmAsync_Should_Return_Success_When_Service_Completes()
    {
        var orderId = Guid.NewGuid();
        _serviceMock.Setup(s => s.ConfirmAsync(It.IsAny<ConfirmPointsDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var result = await _controller.ConfirmAsync(new ConfirmPointsDto(orderId), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse>(okResult.Value);
        Assert.True(response.Success);
        _serviceMock.Verify(s => s.ConfirmAsync(It.Is<ConfirmPointsDto>(d => d.OrderId == orderId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_Should_Throw_When_Input_Null()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _controller.ConfirmAsync(null!, CancellationToken.None));
    }
}
```

**步骤 2：运行测试验证失败**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/Leno.PointsMembership.Api.Tests.csproj \
  --filter "FullyQualifiedName~InternalPointsControllerConfirmTests"
```

预期：编译失败（`InternalPointsController` 无 `ConfirmAsync` 方法）。

**步骤 3：写最小实现**

修改 `src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs`，在 `ReleaseAsync` 方法之后追加：

```csharp
/// <summary>确认扣减冻结积分（订单支付成功核销）。</summary>
[HttpPost("internal/v1/points/confirm")]
[Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]
[HttpPost("internal/points/confirm")]
[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
public async Task<IActionResult> ConfirmAsync([FromBody] ConfirmPointsDto input, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(input);
    await _service.ConfirmAsync(input, ct);
    return Ok(ApiResponse.Success());
}
```

**步骤 4：运行测试验证通过**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/Leno.PointsMembership.Api.Tests.csproj \
  --filter "FullyQualifiedName~InternalPointsControllerConfirmTests"
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/Leno.PointsMembership.Api.Tests.csproj \
  --filter "FullyQualifiedName~ApiTests"
```

预期：所有测试通过。

**步骤 5：提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/InternalPointsControllerConfirmTests.cs
git commit -m "修复 PM-H04：InternalPointsController 新增 Confirm HTTP 端点，与 gRPC Confirm 能力对齐，订单域 HTTP 防腐层不再 404"
```

---

### P0-PM-H05 修复 ExchangeCouponAppService 未使用 Outbox，冻结积分与发布事件非原子

**审计位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/ExchangeCouponAppService.cs#L39-L76`

**根因**：`ExchangeCouponAsync` 在 `SaveEntitiesAsync` 提交事务后再 `IEventBus.PublishAsync(evt, ct)` 发布 `PointsExchangeCouponRequestedEvent`，两者不在同一事务内，未走 Outbox。

**修复方向**：在 `PointsAccount` 聚合根新增 `RequestExchangeCoupon(amount, exchangeId, couponTemplateId)` 方法，内部 `Freeze` + `AddDomainEvent(new PointsExchangeCouponRequestedDomainEvent(...))`，由 mapper 翻译为 `PointsExchangeCouponRequestedEvent` 集成事件经 Outbox 投递。删除应用层的 `IEventBus.PublishAsync` 调用。

**步骤 1：编写失败测试**

测试文件：`src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/ExchangeCouponOutboxTests.cs`

```csharp
using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Events;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.PointsMembership.Application.Tests;

public sealed class ExchangeCouponOutboxTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid CouponTemplateId = Guid.NewGuid();

    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly ExchangeCouponAppService _service;

    public ExchangeCouponOutboxTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new ExchangeCouponAppService(
            _accountRepoMock.Object,
            _unitOfWorkMock.Object,
            _eventBusMock.Object,
            NullLogger<ExchangeCouponAppService>.Instance);
    }

    [Fact]
    public async Task ExchangeCouponAsync_Should_Raise_DomainEvent_Instead_Of_Calling_EventBus()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 500, "种子积分");
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var dto = new ExchangeCouponDto
        {
            UserId = UserId,
            CouponTemplateId = CouponTemplateId,
            PointsRequired = 100
        };

        await _service.ExchangeCouponAsync(dto, CancellationToken.None);

        // 不应直接调用 IEventBus.PublishAsync（应通过 Outbox）
        _eventBusMock.Verify(
            b => b.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // 应在聚合根上添加领域事件
        var domainEvent = account.DomainEvents.OfType<PointsExchangeCouponRequestedDomainEvent>().SingleOrDefault();
        Assert.NotNull(domainEvent);
        Assert.Equal(CouponTemplateId, domainEvent!.CouponTemplateId);
        Assert.Equal(100, domainEvent.PointsRequired);
    }
}
```

**步骤 2：运行测试验证失败**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/Leno.PointsMembership.Application.Tests.csproj \
  --filter "FullyQualifiedName~ExchangeCouponOutboxTests"
```

预期：编译失败（`PointsExchangeCouponRequestedDomainEvent` 不存在）。

**步骤 3：写最小实现**

1. 新建 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsExchangeCouponRequestedDomainEvent.cs`：

```csharp
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Events;

public sealed class PointsExchangeCouponRequestedDomainEvent : DomainEventBase
{
    public Guid ExchangeId { get; init; }
    public Guid UserId { get; init; }
    public Guid CouponTemplateId { get; init; }
    public int PointsRequired { get; init; }

    public PointsExchangeCouponRequestedDomainEvent(
        Guid exchangeId, Guid userId, Guid couponTemplateId, int pointsRequired)
        : base(exchangeId)
    {
        ExchangeId = exchangeId;
        UserId = userId;
        CouponTemplateId = couponTemplateId;
        PointsRequired = pointsRequired;
    }
}
```

2. 修改 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/PointsAccount.cs`，新增方法：

```csharp
/// <summary>
/// 请求兑换优惠券：冻结积分并发起兑换请求领域事件（经 Outbox 翻译为集成事件）。
/// </summary>
/// <param name="amount">兑换所需积分。</param>
/// <param name="exchangeId">兑换业务标识。</param>
/// <param name="couponTemplateId">优惠券模板标识。</param>
public void RequestExchangeCoupon(int amount, Guid exchangeId, Guid couponTemplateId)
{
    if (amount <= 0)
    {
        throw new PointsDomainException("兑换积分数量须大于 0", "POINTS_EXCHANGE_AMOUNT_INVALID");
    }
    if (exchangeId == Guid.Empty)
    {
        throw new PointsDomainException("ExchangeId 不可为空", "POINTS_EXCHANGE_ID_EMPTY");
    }
    if (couponTemplateId == Guid.Empty)
    {
        throw new PointsDomainException("CouponTemplateId 不可为空", "POINTS_COUPON_TEMPLATE_EMPTY");
    }

    Freeze(amount, exchangeId);
    AddDomainEvent(new PointsExchangeCouponRequestedDomainEvent(exchangeId, UserId, couponTemplateId, amount));
}
```

3. 修改 `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/ExchangeCouponAppService.cs`：

```csharp
public async Task<ExchangeCouponResultDto> ExchangeCouponAsync(ExchangeCouponDto input, CancellationToken ct = default)
{
    var account = await _accountRepository.GetByUserIdAsync(input.UserId, ct)
        ?? throw new PointsDomainException(
            $"用户 {input.UserId} 的积分账户不存在",
            "POINTS_ACCOUNT_NOT_FOUND");

    if (account.Balance < input.PointsRequired)
    {
        throw new PointsDomainException(
            $"积分余额不足：可用 {account.Balance}，兑换需要 {input.PointsRequired}",
            "POINTS_BALANCE_INSUFFICIENT");
    }

    var exchangeId = Guid.NewGuid();
    account.RequestExchangeCoupon(input.PointsRequired, exchangeId, input.CouponTemplateId);
    await _unitOfWork.SaveEntitiesAsync(ct);

    _logger.LogInformation(
        "积分兑换优惠券请求已提交 ExchangeId={ExchangeId} UserId={UserId} Points={Points}",
        exchangeId, input.UserId, input.PointsRequired);

    return new ExchangeCouponResultDto
    {
        ExchangeId = exchangeId,
        UserId = input.UserId,
        CouponTemplateId = input.CouponTemplateId,
        PointsFrozen = input.PointsRequired,
        Status = "Pending"
    };
}
```

4. 修改 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/EventBus/PointsMembershipIntegrationEventMapper.cs`，新增映射：

```csharp
// PointsExchangeCouponRequestedDomainEvent → PointsExchangeCouponRequestedEvent（优惠券域）
RegisterHandler<PointsExchangeCouponRequestedDomainEvent, PointsExchangeCouponRequestedEvent>(e =>
    new PointsExchangeCouponRequestedEvent(e.ExchangeId, e.UserId, e.CouponTemplateId, e.PointsRequired));
```

5. 可选：从 `ExchangeCouponAppService` 移除 `IEventBus _eventBus` 依赖（不再需要），或保留以备其他用例。本步骤建议移除以避免后续误用，同时更新构造函数与 DI 注册。

**步骤 4：运行测试验证通过**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/Leno.PointsMembership.Application.Tests.csproj \
  --filter "FullyQualifiedName~ExchangeCouponOutboxTests"
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~CouponExchangeConsumerTests"
```

预期：所有测试通过。

**步骤 5：提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsExchangeCouponRequestedDomainEvent.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/PointsAccount.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Application/Services/ExchangeCouponAppService.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/EventBus/PointsMembershipIntegrationEventMapper.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/ExchangeCouponOutboxTests.cs
git commit -m "修复 PM-H05：ExchangeCouponAppService 改用聚合根 AddDomainEvent + Outbox 发布兑换事件，删除 IEventBus.PublishAsync 直发，保证冻结积分与事件发布原子性"
```

---

### P0-PM-H06 修复 ReviewApprovedEventConsumer Redis 计数为非原子读改写

**审计位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs#L43-L72`

**根因**：`StringGetAsync` 读 → 上限检查 → `Earn` → `StringSetAsync(currentCount + 1)` 三步不在同一原子操作中，并发场景下多个消费者实例同时读到 `currentCount=4`，同时通过上限检查，实际发出 N 条 10 分积分，远超每日 5 条上限。

**修复方向**：改用 `_redisDb.StringIncrementAsync(dailyKey)` 原子自增并返回新值，若返回值 `> MaxDailyReviewPoints` 则 `StringDecrementAsync` 复原并跳过 `account.Earn`。

**步骤 1：编写失败测试**

测试文件：`src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/ReviewApprovedEventConsumerAtomicTests.cs`

```csharp
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Leno.PointsMembership.Domain.Tests;

public sealed class ReviewApprovedEventConsumerAtomicTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid ReviewId = Guid.NewGuid();

    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyMock = new();
    private readonly Mock<IConnectionMultiplexer> _redisMuxMock = new();
    private readonly Mock<IDatabase> _redisDbMock = new();
    private readonly ReviewApprovedEventConsumer _consumer;

    public ReviewApprovedEventConsumerAtomicTests()
    {
        _idempotencyMock.Setup(s => s.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _idempotencyMock.Setup(s => s.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _redisMuxMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_redisDbMock.Object);

        _consumer = new ReviewApprovedEventConsumer(
            _accountRepoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<ReviewApprovedEventConsumer>.Instance,
            _idempotencyMock.Object,
            _redisMuxMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_Use_StringIncrementAsync_Atomic_Operation()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _redisDbMock.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        var evt = new ReviewApprovedEvent(
            reviewId: ReviewId, userId: UserId, spuId: Guid.NewGuid(), rating: 5, newScore: 5.0, reviewCount: 1);

        await _consumer.ConsumeAsync(evt, CancellationToken.None);

        // 验证使用 StringIncrementAsync（原子自增），而非 StringGetAsync + StringSetAsync
        _redisDbMock.Verify(
            d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()),
            Times.Once);
        _redisDbMock.Verify(
            d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_Decrement_And_Skip_When_Exceed_Limit()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        // 自增后返回 6（超过 5 上限）
        _redisDbMock.Setup(d => d.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(6);
        _redisDbMock.Setup(d => d.StringDecrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(5);

        var evt = new ReviewApprovedEvent(
            reviewId: ReviewId, userId: UserId, spuId: Guid.NewGuid(), rating: 5, newScore: 5.0, reviewCount: 1);

        await _consumer.ConsumeAsync(evt, CancellationToken.None);

        // 应回滚计数
        _redisDbMock.Verify(
            d => d.StringDecrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()),
            Times.Once);
        // 不应调用 Earn（积分未发放）
        Assert.Equal(0, account.Balance);
    }
}
```

**步骤 2：运行测试验证失败**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~ReviewApprovedEventConsumerAtomicTests"
```

预期：测试失败（当前实现使用 `StringGetAsync` + `StringSetAsync`，`StringIncrementAsync` 未被调用）。

**步骤 3：写最小实现**

修改 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs` 的 `HandleAsync` 方法：

```csharp
protected override async Task HandleAsync(ReviewApprovedEvent integrationEvent, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(integrationEvent);

    var today = DateTime.UtcNow.ToString("yyyyMMdd");
    var dailyKey = $"review:points:{integrationEvent.UserId}:{today}";

    // 原子自增并返回新值
    var newCount = (long)await _redisDb.StringIncrementAsync(dailyKey);

    // 设置过期时间（仅首次自增时设置）
    if (newCount == 1)
    {
        await _redisDb.KeyExpireAsync(dailyKey, TimeSpan.FromHours(25));
    }

    if (newCount > MaxDailyReviewPoints)
    {
        // 超过上限，回滚计数并跳过
        await _redisDb.StringDecrementAsync(dailyKey);
        Logger.LogInformation("用户 {UserId} 今日评价积分已达上限 {Max}，跳过发放",
            integrationEvent.UserId, MaxDailyReviewPoints);
        return;
    }

    var account = await _accountRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
    if (account is null)
    {
        // 账户不存在也回滚计数
        await _redisDb.StringDecrementAsync(dailyKey);
        Logger.LogWarning("用户 {UserId} 积分账户不存在，跳过评价积分发放", integrationEvent.UserId);
        return;
    }

    account.Earn(PointsSource.Review, ReviewPointsPerReview,
        $"评价 {integrationEvent.ReviewId} 返积分");

    await _unitOfWork.SaveEntitiesAsync(ct);

    Logger.LogInformation("评价 {ReviewId} 审核通过，发放 {Points} 积分给用户 {UserId}（今日第 {Count} 条）",
        integrationEvent.ReviewId, ReviewPointsPerReview, integrationEvent.UserId, newCount);
}
```

**步骤 4：运行测试验证通过**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~ReviewApprovedEventConsumerAtomicTests"
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~ReviewApprovedEventConsumerTests"
```

预期：所有测试通过。

**步骤 5：提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/ReviewApprovedEventConsumerAtomicTests.cs
git commit -m "修复 PM-H06：ReviewApprovedEventConsumer 改用 StringIncrementAsync 原子自增，超限时 StringDecrementAsync 回滚，消除并发突破每日 5 条上限的风险"
```

---

### P0-PM-H07 修复 OrderCompletedEventConsumer 与 OrderAfterSalesWindowClosedEventConsumer 双倍发放消费返积分

**审计位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs#L40-L72`、`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs#L142-L177`

**根因**：两个消费者分别订阅 `OrderCompletedEvent` 与 `OrderAfterSalesWindowClosedEvent`，都调用 `account.Earn(PointsSource.Consumption, points, ...)` 与 `member.AddConsumption(...)`。若订单域同时发布两类事件，同一笔订单用户获得 2 倍消费返积分，累计消费金额翻倍。

**修复方向**：明确业务规则——消费返积分应在售后窗口关闭后发放（避免退货后已发积分难以追回）。删除 `OrderCompletedEventConsumer` 中的 `account.Earn` 与 `member.AddConsumption` 逻辑，仅保留日志（或改为发布"订单完成"通知事件供其它 BC 使用）。`OrderAfterSalesWindowClosedEventConsumer` 保留为唯一发放点。

**步骤 1：编写失败测试**

测试文件：`src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/OrderCompletedNoDoublePointsTests.cs`

```csharp
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.PointsMembership.Domain.Tests;

public sealed class OrderCompletedNoDoublePointsTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IMembershipLevelRepository> _levelRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyMock = new();

    public OrderCompletedNoDoublePointsTests()
    {
        _idempotencyMock.Setup(s => s.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _idempotencyMock.Setup(s => s.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _levelRepoMock.Setup(r => r.GetAllEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MembershipLevel>());
    }

    [Fact]
    public async Task OrderCompletedEventConsumer_Should_Not_Earn_Points_Nor_AddConsumption()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        var member = Member.Create(MemberId, UserId);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var consumer = new OrderCompletedEventConsumer(
            _accountRepoMock.Object,
            _memberRepoMock.Object,
            _levelRepoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<OrderCompletedEventConsumer>.Instance,
            _idempotencyMock.Object);

        var evt = new OrderCompletedEvent(
            orderId: OrderId, userId: UserId, totalAmount: 100m, completedAt: DateTime.UtcNow);

        await consumer.ConsumeAsync(evt, CancellationToken.None);

        // 不应发放积分
        Assert.Equal(0, account.Balance);
        Assert.Equal(0, account.TotalEarned);
        // 不应累加消费金额
        Assert.Equal(0m, member.TotalConsumption);
    }

    [Fact]
    public async Task OrderAfterSalesWindowClosedEventConsumer_Should_Still_Earn_Points()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        var member = Member.Create(MemberId, UserId);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var consumer = new OrderAfterSalesWindowClosedEventConsumer(
            _accountRepoMock.Object,
            _memberRepoMock.Object,
            _levelRepoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<OrderAfterSalesWindowClosedEventConsumer>.Instance,
            _idempotencyMock.Object);

        var evt = new OrderAfterSalesWindowClosedEvent(
            OrderId, UserId, paidAmount: 100m, windowClosedAt: DateTime.UtcNow);

        await consumer.ConsumeAsync(evt, CancellationToken.None);

        Assert.Equal(100, account.Balance);
        Assert.Equal(100m, member.TotalConsumption);
    }
}
```

**步骤 2：运行测试验证失败**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~OrderCompletedNoDoublePointsTests"
```

预期：第一个测试失败（当前 `OrderCompletedEventConsumer` 仍调用 `account.Earn` 与 `member.AddConsumption`）。

**步骤 3：写最小实现**

修改 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs` 中 `OrderCompletedEventConsumer.HandleAsync`：

```csharp
protected override async Task HandleAsync(OrderCompletedEvent integrationEvent, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(integrationEvent);

    // 消费返积分与消费金额累加改由 OrderAfterSalesWindowClosedEventConsumer 在售后窗口关闭后统一发放
    // 避免同一订单双倍发放与退货后已发积分难追回的问题
    // 本消费者仅记录日志，便于后续若需触发"订单完成通知"等下游事件时扩展
    await Task.CompletedTask;

    Logger.LogInformation(
        "订单 {OrderId} 已完成，消费返积分将在售后窗口关闭后由 OrderAfterSalesWindowClosedEventConsumer 发放",
        integrationEvent.OrderId);
}
```

可移除 `OrderCompletedEventConsumer` 中已不再使用的 `_accountRepository` / `_memberRepository` / `_levelRepository` / `_unitOfWork` 依赖（保留 `ILogger` 与 `IIdempotencyStore` 基类依赖）。如保留以备未来扩展，需在构造函数中保留参数。

**步骤 4：运行测试验证通过**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~OrderCompletedNoDoublePointsTests"
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj
```

预期：所有测试通过。

**步骤 5：提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/OrderCompletedNoDoublePointsTests.cs
git commit -m "修复 PM-H07：OrderCompletedEventConsumer 不再发放消费返积分与累加消费金额，统一由 OrderAfterSalesWindowClosedEventConsumer 在售后窗口关闭后发放，消除双倍发放风险"
```

---

### P0-PM-H08 修复 OrderPaidEventConsumer 在 package 为 null 时抛异常导致消费者整体失败

**审计位置**：`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderPaidEventConsumer.cs#L52-L63`、`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/UserMembership.cs#L84-L108`

**根因**：`package?.DurationDays ?? 0` 在 package 软删除后返回 0，`UserMembership.Activate` 在 `durationDays <= 0` 时抛 `PointsDomainException`，异常未被捕获，触发 MassTransit 重试。同时 `account.ConfirmDeduct` 已成功执行但消息进入死信后再次重试会触发 `POINTS_FROZEN_ENTRY_NOT_FOUND` 死循环。

**修复方向**：
1. `OrderPaidEventConsumer.HandleAsync` 中 package 为 null 时记录告警并跳过 `Activate`，仅完成 `ConfirmDeduct`；
2. `UserMembership.Activate` 增加基于 `OrderId` 的幂等检查（已激活且 OrderId 相同则直接返回，不抛异常）；
3. 消费者层捕获 `DbUpdateConcurrencyException` 视为已处理。

**步骤 1：编写失败测试**

测试文件：`src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/OrderPaidEventConsumerPackageNullTests.cs`

```csharp
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.PointsMembership.Domain.Tests;

public sealed class OrderPaidEventConsumerPackageNullTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid UserMembershipId = Guid.NewGuid();
    private static readonly Guid PackageId = Guid.NewGuid();

    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<IUserMembershipRepository> _userMembershipRepoMock = new();
    private readonly Mock<IMembershipPackageRepository> _packageRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IIdempotencyStore> _idempotencyMock = new();
    private readonly OrderPaidEventConsumer _consumer;

    public OrderPaidEventConsumerPackageNullTests()
    {
        _idempotencyMock.Setup(s => s.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _idempotencyMock.Setup(s => s.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _consumer = new OrderPaidEventConsumer(
            _accountRepoMock.Object,
            _userMembershipRepoMock.Object,
            _packageRepoMock.Object,
            _unitOfWorkMock.Object,
            NullLogger<OrderPaidEventConsumer>.Instance,
            _idempotencyMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_Skip_Activate_And_Not_Throw_When_Package_Is_Null()
    {
        var account = PointsAccount.Create(AccountId, UserId);
        account.Earn(PointsSource.Activity, 200, "种子积分");
        account.Freeze(100, OrderId);
        var userMembership = UserMembership.Create(UserMembershipId, UserId, PackageId, level: 1);

        _accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _userMembershipRepoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userMembership);
        _packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipPackage?)null);

        var evt = new OrderPaidEvent(
            orderId: OrderId, userId: UserId, paidAt: DateTime.UtcNow, paidAmount: 100m);

        // 不应抛异常
        await _consumer.ConsumeAsync(evt, CancellationToken.None);

        // ConfirmDeduct 仍应执行
        Assert.Equal(100, account.TotalSpent);
        // UserMembership 不应被激活
        Assert.Equal(UserMembershipStatus.Pending, userMembership.Status);
    }

    [Fact]
    public async Task HandleAsync_Should_Skip_Activate_When_UserMembership_Already_Active_With_Same_OrderId()
    {
        var userMembership = UserMembership.Create(UserMembershipId, UserId, PackageId, level: 1);
        var package = MembershipPackage.Create(
            Guid.NewGuid(), name: "月度会员", level: 1, price: 30m, durationDays: 30, description: "月度");

        _accountRepoMock.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);
        _userMembershipRepoMock.Setup(r => r.GetByOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userMembership);
        _packageRepoMock.Setup(r => r.GetByIdAsync(PackageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(package);

        // 首次激活
        userMembership.Activate(OrderId, DateTime.UtcNow, 30);
        Assert.Equal(UserMembershipStatus.Active, userMembership.Status);

        var evt = new OrderPaidEvent(
            orderId: OrderId, userId: UserId, paidAt: DateTime.UtcNow, paidAmount: 30m);

        // 重复事件不应抛异常（幂等）
        await _consumer.ConsumeAsync(evt, CancellationToken.None);

        Assert.Equal(UserMembershipStatus.Active, userMembership.Status);
    }
}
```

**步骤 2：运行测试验证失败**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~OrderPaidEventConsumerPackageNullTests"
```

预期：第一个测试失败（`Activate` 在 package null 时抛 `PointsDomainException`）；第二个测试失败（重复激活抛 `MEMBERSHIP_ACTIVATE_INVALID`）。

**步骤 3：写最小实现**

1. 修改 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/UserMembership.cs` 中 `Activate` 方法，增加 OrderId 幂等检查：

```csharp
public void Activate(Guid orderId, DateTime startTime, int durationDays)
{
    if (orderId == Guid.Empty)
    {
        throw new PointsDomainException("OrderId 不可为空", "POINTS_ORDER_EMPTY");
    }

    // 幂等：已激活且 OrderId 相同则直接返回（重复事件）
    if (Status == UserMembershipStatus.Active && OrderId == orderId)
    {
        return;
    }

    if (durationDays <= 0)
    {
        throw new PointsDomainException("权益时长须大于 0", "MEMBERSHIP_DURATION_INVALID");
    }

    if (Status != UserMembershipStatus.Pending)
    {
        throw new PointsDomainException(
            $"当前状态 {Status} 不可激活，仅 Pending 可激活",
            "MEMBERSHIP_ACTIVATE_INVALID");
    }

    OrderId = orderId;
    StartTime = startTime;
    EndTime = startTime.AddDays(durationDays);
    Status = UserMembershipStatus.Active;
    AddDomainEvent(new MembershipActivatedEvent(UserId, PackageId, Level, EndTime));
}
```

2. 修改 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderPaidEventConsumer.cs` 的 `HandleAsync` 方法：

```csharp
protected override async Task HandleAsync(OrderPaidEvent integrationEvent, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(integrationEvent);

    // 1. 确认积分扣减（若该订单冻结了积分）
    var account = await _accountRepository.GetByFrozenOrderIdAsync(integrationEvent.OrderId, ct);
    if (account is not null)
    {
        account.ConfirmDeduct(integrationEvent.OrderId);
        Logger.LogInformation("订单 {OrderId} 支付成功，已确认扣减积分", integrationEvent.OrderId);
    }
    else
    {
        Logger.LogInformation("订单 {OrderId} 无冻结积分，跳过 ConfirmDeduct", integrationEvent.OrderId);
    }

    // 2. 若为会员订阅订单，激活 UserMembership
    var userMembership = await _userMembershipRepository.GetByOrderIdAsync(integrationEvent.OrderId, ct);
    if (userMembership is not null && userMembership.Status == UserMembershipStatus.Pending)
    {
        var package = await _packageRepository.GetByIdAsync(userMembership.PackageId, ct);
        if (package is null)
        {
            // 套餐已下架或数据异常，记录告警并跳过 Activate，避免消费者整体失败
            Logger.LogWarning(
                "会员订阅订单 {OrderId} 对应套餐 {PackageId} 不存在或已下架，跳过 UserMembership 激活，需人工处理",
                integrationEvent.OrderId, userMembership.PackageId);
        }
        else if (package.DurationDays <= 0)
        {
            Logger.LogWarning(
                "会员订阅订单 {OrderId} 对应套餐 {PackageId} DurationDays={Days} 异常，跳过 UserMembership 激活，需人工处理",
                integrationEvent.OrderId, userMembership.PackageId, package.DurationDays);
        }
        else
        {
            userMembership.Activate(integrationEvent.OrderId, integrationEvent.PaidAt, package.DurationDays);
            Logger.LogInformation("会员订阅订单 {OrderId} 支付成功，已激活会员 {UserMembershipId}",
                integrationEvent.OrderId, userMembership.Id);
        }
    }

    await _unitOfWork.SaveEntitiesAsync(ct);
}
```

**步骤 4：运行测试验证通过**

```bash
cd /workspace
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj \
  --filter "FullyQualifiedName~OrderPaidEventConsumerPackageNullTests"
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/Leno.PointsMembership.Domain.Tests.csproj
```

预期：所有测试通过。

**步骤 5：提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/UserMembership.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderPaidEventConsumer.cs \
        src/Services/PointsMembership/Leno.PointsMembership.Domain.Tests/OrderPaidEventConsumerPackageNullTests.cs
git commit -m "修复 PM-H08：OrderPaidEventConsumer 在 package null/DurationDays<=0 时记录告警并跳过 Activate，UserMembership.Activate 增加 OrderId 幂等检查，消除消息重试死循环"
```

---

## P1 任务清单（🟡 中风险问题）

### P1-PM-M01 修复 EfCorePointsAccountRepository.GetByFrozenOrderIdAsync 集合扫描

- **修复要点**：
  1. 新增 `IPointsFrozenEntryRepository.GetByOrderIdAsync(orderId)` 直接按 `order_id` 单表查询，命中 `ix_points_frozen_entries_order_id` 索引，返回 `PointsFrozenEntry` 与对应 `AccountId`；
  2. `EfCorePointsAccountRepository.GetByFrozenOrderIdAsync` 改为先调 `IPointsFrozenEntryRepository.GetByOrderIdAsync` 拿到 `AccountId`，再按 `AccountId` 加载聚合并 Include `FrozenEntries`；
  3. 或在 `PointsAccount.FrozenEntries` 上建立 `Dictionary<OrderId, PointsFrozenEntry>` 索引字段，避免聚合内 `FirstOrDefault` 二次扫描。
- **涉及文件**：
  - `src/Services/PointsMembership/Leno.PointsMembership.Domain/Repositories/IPointsAccountRepository.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCorePointsAccountRepository.cs`
  - 新建 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Repositories/IPointsFrozenEntryRepository.cs`
  - 新建 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCorePointsFrozenEntryRepository.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`（DI 注册）

### P1-PM-M02 修复 Member.AddGrowthValue 的 reason 参数被忽略

- **修复要点**：
  1. 将 `reason` 参数写入 `MemberLevelChangeHistory` 子实体集合（已存在，参见 `Member.cs#L45`）；
  2. 新增 `MemberLevelChangeHistory.Create(memberId, reason, amount, occurredAt)` 工厂方法；
  3. 在 `AddGrowthValue` 内 `LevelChangeHistories.Add(MemberLevelChangeHistory.Create(...))`；
  4. 或简化为 `AddGrowthValue(int amount)` 移除 `reason` 参数，由应用层记录审计日志（与 `PointsAccount.Earn` 的 `reason` 处理保持一致，PM-H02 修复后流水已记录原因）。
- **涉及文件**：
  - `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Domain/ValueObjects/MemberLevelChangeHistory.cs`
  - 各调用方（PM-H01 修复后新增的 3 处调用）

### P1-PM-M03 修复 PointsAppService.CheckInAsync 使用 DateTime.UtcNow 计算 today

- **修复要点**：
  1. 在应用层注入用户时区（建议通过 `ICurrentUserContext.TimeZone` 或 `IOptions<PointsMembershipOptions>.DefaultTimeZone`，默认 `Asia/Shanghai`）；
  2. `today` 计算改为 `DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz))`；
  3. 同步修复 PM-L06 的 Redis Key "日" 计算（统一时区策略）。
- **涉及文件**：
  - `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsAppService.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs`（PM-L06 同步）
  - 新建 `src/Services/PointsMembership/Leno.PointsMembership.Application/ICurrentUserContext.cs`（或复用共享层 `ICurrentUserContext` 若已存在）
  - `src/Services/PointsMembership/Leno.PointsMembership.Api/appsettings.json`（新增 `PointsMembership:DefaultTimeZone` 配置项）

### P1-PM-M04 修复 UserMembership.Activate 与 OrderPaidEventConsumer 之间无并发控制

- **修复要点**：
  1. PM-H08 修复已增加 `OrderId` 幂等检查（已激活且 OrderId 相同则直接返回）；
  2. 在 `UserMembershipConfiguration` 增加 `IsConcurrencyToken()` 或 RowVersion 字段，保证乐观锁；
  3. 消费者层捕获 `DbUpdateConcurrencyException` 视为已处理（记录日志并返回，不重试）。
- **涉及文件**：
  - `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/UserMembership.cs`（PM-H08 已改）
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Configurations/UserMembershipConfiguration.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderPaidEventConsumer.cs`

### P1-PM-M05 修复 MemberLevelUpgradedReadModelSyncConsumer 期望消费集成事件版 MemberLevelUpgradedEvent

- **修复要点**：已在 PM-H03 修复中同步解决（mapper 新增 `DomainMemberLevelUpgradedEvent → MemberLevelUpgradedEvent` 翻译，集成事件版正常发布）。
- **后续验证**：PM-H03 修复后运行 `MemberReadModelSyncConsumerTests` 验证消费者正常触发。

### P1-PM-M06 修复 IPointsOffsetAppService 接口定义在 Domain 层

- **修复要点**：
  1. 删除 `src/Services/PointsMembership/Leno.PointsMembership.Domain/Services/IPointsOffsetAppService.cs`（已被 `IPointsInternalAppService` 替代，无任何调用方）；
  2. 删除 `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsOffsetAppService.cs`（无注册无调用）；
  3. 删除对应测试 `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsOffsetAppServiceTests.cs`；
  4. 验证 `Domain` 层不再引用应用层概念。
- **涉及文件**：
  - `src/Services/PointsMembership/Leno.PointsMembership.Domain/Services/IPointsOffsetAppService.cs`（删除）
  - `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsOffsetAppService.cs`（删除）
  - `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsOffsetAppServiceTests.cs`（删除）

### P1-PM-M07 修复 PointsAppService.GetLedgerAsync 返回空列表

- **修复要点**：
  1. PM-H02 修复后 `PointsLedger` 已同事务写入 `PointsAccount.Ledgers` 集合，可通过聚合根加载流水；
  2. 新增 `IPointsAccountRepository.GetLedgersByUserIdAsync(userId, page, pageSize, ct)` 分页查询方法；
  3. `PointsAppService.GetLedgerAsync` 实现真实分页查询，返回 `PointsLedgerDto` 列表；
  4. 删除注释 `// 流水查询需独立的 IPointsLedgerRepository，当前域尚未定义，暂返回空列表。`。
- **涉及文件**：
  - `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsAppService.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Domain/Repositories/IPointsAccountRepository.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCorePointsAccountRepository.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Application/DTOs/PointsDtos.cs`（确认 `PointsLedgerDto` 字段集）

### P1-PM-M08 修复领域事件与集成事件同名 MemberLevelUpgradedEvent

- **修复要点**：
  1. 将 `Leno.SharedContracts.Events.MemberLevelUpgradedEvent`（集成事件）重命名为 `MemberLevelUpgradedIntegrationEvent`，与 `PointsEarnedIntegrationEvent` 等命名一致；
  2. 更新 mapper 中 `DomainMemberLevelUpgradedEvent → MemberLevelUpgradedIntegrationEvent` 翻译；
  3. 更新 `MemberLevelUpgradedReadModelSyncConsumer` 订阅 `MemberLevelUpgradedIntegrationEvent`；
  4. 更新所有引用方（包括测试）；
  5. 移除 mapper 中的 `using DomainMemberLevelUpgradedEvent = ...` 别名消歧（领域事件不再与集成事件同名）。
- **涉及文件**：
  - `src/BuildingBlocks/Leno.SharedContracts/Events/PointsMembershipEvents.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/EventBus/PointsMembershipIntegrationEventMapper.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/MemberLevelUpgradedReadModelSyncConsumer.cs`
  - 各测试文件
- **注**：本修复需与 PM-H03 协调，建议 PM-H03 修复完成后再执行重命名，避免冲突。

### P1-PM-M09 修复 Member.CheckUpgrade 与 Member.EvaluateGrowthLevel 双轨体系割裂

- **修复要点**：需产品决策——
  - **方案 A（保留双轨）**：PM-H01 修复后成长值体系已打通，确认 `MemberLevelEvaluationJob` 每日评估正常工作，无需删除；运营在 `MembershipLevel`（消费门槛）与 `MemberLevel`（成长值）两张表均需配置，但两套等级独立维护。
  - **方案 B（废弃成长值体系）**：删除 `GrowthValue` / `CurrentGrowthLevel` / `MemberLevel` 聚合 / `MemberLevelEvaluationJob`，仅保留消费门槛体系。
  - **方案 C（合并为单一体系）**：将成长值等级与消费门槛等级合并为单一字段，删除其中一套。
- **涉及文件**（方案 B）：
  - `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs`（移除 GrowthValue / CurrentGrowthLevel / AddGrowthValue / EvaluateGrowthLevel）
  - `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/MemberLevel.cs`（删除）
  - `src/Services/PointsMembership/Leno.PointsMembership.Domain/Repositories/IMemberLevelRepository.cs`（删除）
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCoreMemberLevelRepository.cs`（删除）
  - `src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/MemberLevelEvaluationJob.cs`（删除）
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Configurations/MemberConfiguration.cs`（移除 GrowthValue/CurrentGrowthLevel 映射）
- **建议**：先按方案 A 执行（PM-H01 修复后双轨打通），待产品决策后再决定是否执行方案 B/C。

---

## P2 任务清单（🟢 低风险问题）

### P2-PM-L01 修复后台服务异常路径后仍延后一日

- **修复要点**：
  1. PM-L01 的"显式捕获 OperationCanceledException 跳出循环"部分已修复（`MemberLevelEvaluationJob.cs#L38-L41`、`PointsExpiryService.cs#L41-L44`）；
  2. 剩余"异常后仍固定 24 小时延后"部分需改为指数退避（如初次异常后 1 分钟、二次 5 分钟、三次 30 分钟、四次以上 1 小时）；
  3. 实现方式：在 catch 块内累加 `failureCount`，`Task.Delay` 时根据 `failureCount` 计算延迟；正常执行后重置 `failureCount = 0`。
- **涉及文件**：
  - `src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/MemberLevelEvaluationJob.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/PointsExpiryService.cs`

### P2-PM-L02 修复硬编码 12 个月过期阈值与 25 小时 Redis Key 过期时间

- **修复要点**：
  1. 新建 `PointsMembershipOptions` 配置类，含 `ExpiryMonths`（默认 12）、`ReviewDailyLimit`（默认 5）、`DefaultTimeZone`（默认 `Asia/Shanghai`）；
  2. `PointsExpiryService.ExpiryMonths` 改为从 `IOptions<PointsMembershipOptions>` 读取；
  3. `ReviewApprovedEventConsumer` 的 `TimeSpan.FromHours(25)` 改为根据用户时区当日剩余时间 + 缓冲计算（或从配置读取 `RedisKeyTtlHours`）；
  4. `appsettings.json` 新增 `PointsMembership` 配置节。
- **涉及文件**：
  - 新建 `src/Services/PointsMembership/Leno.PointsMembership.Application/PointsMembershipOptions.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/PointsExpiryService.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs`
  - `src/Services/PointsMembership/Leno.PointsMembership.Api/appsettings.json`
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`

### P2-PM-L03 修复 MemberLevel.EvaluateLevel 双重排序

- **修复要点**：将 `OrderBy(l => l.MinGrowthValue)` + 单遍历查找最大，改为先 `Where(l => l.MinGrowthValue <= growthValue)` 过滤再 `OrderByDescending(l => l.Level).FirstOrDefault()`，或直接单遍历记录最大值：
```csharp
public static int EvaluateLevel(int growthValue, List<MemberLevel> allLevels)
{
    MemberLevel? matched = null;
    foreach (var level in allLevels)
    {
        if (growthValue >= level.MinGrowthValue &&
            (matched is null || level.Level > matched.Level))
        {
            matched = level;
        }
    }
    return matched?.Level ?? 0;
}
```
- **涉及文件**：
  - `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/MemberLevel.cs`

### P2-PM-L04 修复 InternalPointsController 双 [HttpPost] 路由（含 [Obsolete]）

- **修复要点**：
  1. 建立 GitHub Issue 跟踪 1 周后下线旧路由；
  2. 1 周后删除每个端点的 `[Obsolete] [HttpPost("internal/points/xxx")]` 旧路由，仅保留 `[HttpPost("internal/v1/points/xxx")]`；
  3. 或将过渡期改为通过 `ApiVersion` 中介者管理；
  4. 在 `[Obsolete]` 特性中补充 `DiagnosticId` 与下线时间：`[Obsolete("Use internal/v1/... route, will be removed in 2026-08-01", DiagnosticId = "LENO_PM001")]`。
- **涉及文件**：
  - `src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs`

### P2-PM-L05 修复 gRPC 服务在 TrialOffset/Freeze/Release 中使用 new Guid(request.UserId)

- **修复要点**：将 `TrialOffset`（L37）、`Freeze`（L55-L56）、`Release`（L68）的 `new Guid(request.UserId)` / `new Guid(request.OrderId)` 改为 `Guid.TryParse` + `RpcException(StatusCode.InvalidArgument)`，与 `Confirm`（L78）与 `GetPointsBalance`（L93）保持一致：
```csharp
if (!Guid.TryParse(request.UserId, out var userId))
{
    throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid user id: {request.UserId}"));
}
```
- **涉及文件**：
  - `src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs`

### P2-PM-L06 修复 ReviewApprovedEventConsumer 使用 DateTime.UtcNow.ToString("yyyyMMdd") 计算 Redis Key

- **修复要点**：与 PM-M03 同源，统一时区策略后，`today` 计算改为 `TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTz).ToString("yyyyMMdd")`。
- **涉及文件**：
  - `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs`
- **注**：本修复与 PM-M03 一并执行。

### P2-PM-L07 修复 OrderPaidEventConsumer 的 account null 分支日志缺失

- **修复要点**：在 PM-H08 修复中已补齐（`OrderPaidEventConsumer` 的 account null 分支已新增 `Logger.LogInformation("订单 {OrderId} 无冻结积分，跳过 ConfirmDeduct", ...)`）。
- **后续验证**：PM-H08 修复后此问题自动关闭。

---

## 修复执行顺序建议

1. **第一批（P0 高优先级，按依赖顺序）**：
   - PM-H02（PointsLedger 写入）→ 为 PM-H07 测试提供流水断言基础
   - PM-H03（4 个 ReadModel 死消费者修复，含 PM-M05/PM-M08 同源）→ 独立修复
   - PM-H04（InternalPointsController Confirm HTTP 端点）→ 独立修复
   - PM-H05（ExchangeCouponAppService Outbox）→ 独立修复
   - PM-H06（ReviewApprovedEventConsumer Redis 原子）→ 独立修复
   - PM-H07（OrderCompletedEventConsumer 不再双发）→ 独立修复
   - PM-H08（OrderPaidEventConsumer package null，含 PM-L07 同源）→ 独立修复
   - PM-H01（AddGrowthValue 调用方补齐）→ 依赖 PM-H07 修复后的 `OrderAfterSalesWindowClosedEventConsumer` 链路

2. **第二批（P1 中风险）**：按 PM-M01 → PM-M02 → PM-M03（含 PM-L06）→ PM-M04 → PM-M06 → PM-M07 → PM-M08 → PM-M09 顺序推进。

3. **第三批（P2 低风险）**：随相关模块迭代修复。

---

## 引用证据汇总

| 问题 | 关键证据位置 |
|------|-------------|
| PM-H01 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs#L119-L133`；Grep `AddGrowthValue` 在生产代码零命中（仅 `Domain.Tests/DomainTests.cs` 测试命中） |
| PM-H02 | Grep `PointsLedger\.Create` 在 `/workspace/src/Services/PointsMembership` 零匹配；`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/PointsAccount.cs#L72-L236` 七个状态变更方法仅 `AddDomainEvent` |
| PM-H03 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/EventBus/PointsMembershipIntegrationEventMapper.cs#L17-L42` 仅注册 6 类映射；Grep `PointsAccountCreatedEvent\|PointsAdjustedEvent\|MemberRegisteredEvent` 仅 ReadModels + Tests 命中 |
| PM-H04 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L22-L53` 仅 3 个端点；`file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs#L96` 调用 `internal/v1/points/confirm` |
| PM-H05 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/ExchangeCouponAppService.cs#L57-L62` `SaveEntitiesAsync` 后 `_eventBus.PublishAsync` |
| PM-H06 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs#L46-L67` `StringGetAsync` + `StringSetAsync` 非原子 |
| PM-H07 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs#L51` 与 `#L153` 均调用 `account.Earn(PointsSource.Consumption, ...)` |
| PM-H08 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderPaidEventConsumer.cs#L56-L58` `package?.DurationDays ?? 0` + `Activate`；`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/UserMembership.cs#L91-L94` `durationDays <= 0` 抛异常 |
| p0a-T6 已修复 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsInternalAppService.cs#L73-L84`；`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs#L76-L88` |
| T10 已修复 | `file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs#L41-L98` 经 `AntiCorruptionBase.ExecuteAsync` + `EnsureSuccessStatusCode` 抛 `AntiCorruptionException` |
| PM-L01 部分已修复 | `file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/MemberLevelEvaluationJob.cs#L38-L41`；`file:///workspace/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/PointsExpiryService.cs#L41-L44` 显式捕获 `OperationCanceledException` 跳出循环 |

---

## 附录：审计方法说明

- **工具**：`Read`（精确读取文件）、`Grep`（全局检索符号/调用方）、`SearchCodebase`（语义检索）、`Glob`（文件模式匹配）
- **关键检索验证**：
  - `Grep "PointsLedger\.Create" /workspace/src/Services/PointsMembership` → **0 匹配**，证实 PM-H02
  - `Grep "AddGrowthValue" /workspace/src/Services/PointsMembership` → **仅测试目录命中**（14 处，其中 12 处在 `Domain.Tests/DomainTests.cs`，2 处在 `Domain/Aggregates/Member.cs` 的方法定义与签名），证实 PM-H01
  - `Grep "PointsAccountCreatedEvent|PointsAdjustedEvent|MemberRegisteredEvent" /workspace/src/Services/PointsMembership` → **仅 ReadModels + Tests 命中**（14 处），证实 PM-H03
- **交叉验证**：对照 `Leno.Order.Infrastructure/Services/PointsAntiCorruptionService.cs` 确认 HTTP `confirm` 路径调用，对照 `Leno.Order.Infrastructure/Consumers/PaymentSucceededEventConsumer.cs` 确认调用链
- **本计划所有文件路径与行号均基于审计当日（2026-07-21）代码库快照，引用格式 `file:///workspace/src/.../File.cs#Lstart-Lend`**
