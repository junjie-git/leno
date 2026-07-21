# Promotion（促销域）修复实施计划

## 元数据
- 审计报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md]
- 跨 BC 聚合报告 F 章节：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md#L491-L611]
- 架构评估 G4/G5 章节：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md#L309-L505]
- 问题总数：🔴 11 / 🟡 13 / 🟢 10
- 已修复（跳过）：4 项（来自既有 p0a/critical-vulns 计划）
- 本计划覆盖：34 项（11 P0 + 13 P1 + 10 P2）
- 扫描范围：`src/Services/Promotion/Leno.Promotion.{Domain,Application,Infrastructure,Api}/`
- 排除项：`Tests/`、`Migrations/*.Designer.cs`、`*ModelSnapshot.cs`

## 问题清单总表

| # | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|---|--------|---------|---------|--------|------|
| 2.1 | 🔴 | CouponExpiryService 分页 skip 累加导致漏处理过期券 | file:///workspace/src/Services/Promotion/Leno.Promotion.Api/BackgroundServices/CouponExpiryService.cs#L57-L80 | P0 | 待修复 |
| 2.2 | 🔴 | CouponExpiryService 仅扫描 Unused，遗漏 Locked+Expired 券 | file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Repositories/EfCoreUserCouponRepository.cs#L82-L83 | P0 | 待修复 |
| 2.3 | 🔴 | SeckillOrderCreationFailedEventConsumer 与补偿服务双重复回退库存膨胀 | file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/SeckillOrderEventConsumer.cs#L43-L69 | P0 | 待修复 |
| 2.4 | 🔴 | SeckillPreOccupationCompensationService TOCTOU 补偿与履约竞态 | file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/BackgroundServices/SeckillPreOccupationCompensationService.cs#L77-L102 | P0 | 待修复 |
| 2.5 | 🔴 | SeckillPreOccupationRecord.MarkRolledBack 不校验 IsFulfilled | file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillPreOccupationRecord.cs#L82-L91 | P0 | 待修复 |
| 2.6 | 🔴 | OrderCancelledEventConsumer 在券已核销时 Release 抛错死信 | file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/OrderEventConsumer.cs#L83-L99 | P0 | 待修复 |
| 2.7 | 🔴 | SeckillAppService.ActivateAsync Redis 初始化失败但 DB 仍标记 Active | file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L56-L69 | P0 | 待修复 |
| 2.8 | 🔴 | SeckillAppService.PlaceOrderAsync 多 SKU 路径下 DB DeductStock 与 Redis 不一致 | file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L110-L145 | P0 | 待修复 |
| 2.9 | 🔴 | SeckillAppService.PlaceOrderAsync DB 乐观锁冲突引发"幽灵失败" | file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L131-L152 | P0 | 待修复 |
| 2.10 | 🔴 | PromotionActivity.Rules 直接暴露 List 违反 DDD 不变量封装 | file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/PromotionActivity.cs#L32 | P0 | 待修复 |
| 2.11 | 🔴 | PromotionGrpcService 直接依赖 ICouponRepository 违反分层 | file:///workspace/src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs#L19-L33 | P0 | 待修复 |
| 3.1 | 🟡 | PromotionAppService.UpdateAsync 静默忽略 Name 字段 | file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/PromotionAppService.cs#L42-L60 | P1 | 待修复 |
| 3.2 | 🟡 | PointsExchangeConsumer 直接调 DbContext.SaveChangesAsync 不走 UnitOfWork | file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/PointsExchangeConsumer.cs#L77-L86 | P1 | 待修复 |
| 3.3 | 🟡 | CouponAppService.ReceiveAsync 将所有 DbUpdateException 误判为"已领取" | file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs#L117-L125 | P1 | 待修复 |
| 3.4 | 🟡 | CouponAppService.LockCouponAsync 未处理乐观锁冲突 | file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs#L138-L148 | P1 | 待修复 |
| 3.5 | 🟡 | SeckillAppService.ToDtoAsync 在列表查询中循环调用 Redis（N+1） | file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L172-L194 | P1 | 待修复 |
| 3.6 | 🟡 | PromotionCalculateAppService 循环内 N+1 查询 Coupon 模板 | file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/PromotionCalculateAppService.cs#L99-L118 | P1 | 待修复 |
| 3.7 | 🟡 | SeckillAppService.CloseActivityWithStockWriteBackAsync 嵌套 SaveEntitiesAsync | file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L80-L89 | P1 | 待修复 |
| 3.8 | 🟡 | RedisSeckillStockService.WriteBackToDbAsync 依赖 EF Core Identity Map | file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs#L155-L184 | P1 | 待修复 |
| 3.9 | 🟡 | SeckillPreOccupationCompensationService BatchSize=100 + 30s 间隔大批量回退慢 | file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/BackgroundServices/SeckillPreOccupationCompensationService.cs#L18-L20 | P1 | 待修复 |
| 3.10 | 🟡 | SeckillPreOccupationRecordConfiguration 表名 PascalCase 与其他 snake_case 不一致 | file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Configurations/SeckillPreOccupationRecordConfiguration.cs#L14 | P1 | 待修复 |
| 3.11 | 🟡 | Redis Lua RestoreLuaScript 无上限保护可导致库存超 TotalStock | file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs#L46-L49 | P1 | 待修复 |
| 3.12 | 🟡 | SeckillPreOccupationRecord.Create 未校验入参合法性 | file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillPreOccupationRecord.cs#L49-L67 | P1 | 待修复 |
| 3.13 | 🟡 | UserCoupon.Return 未清空 LockedOrderId | file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/UserCoupon.cs#L90-L107 | P1 | 待修复 |
| 4.1 | 🟢 | Coupon.Create 允许 totalQty < -1 | file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/Coupon.cs#L93-L96 | P2 | 待修复 |
| 4.2 | 🟢 | Coupon.IssuedQty 累加未防整数溢出 | file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/Coupon.cs#L189-L196 | P2 | 待修复 |
| 4.3 | 🟢 | SeckillActivity.RestoreStock 允许 Pending 态回退 | file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillActivity.cs#L220-L247 | P2 | 待修复 |
| 4.4 | 🟢 | SeckillActivity.SyncFromRedis 允许 Closed 态同步 | file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillActivity.cs#L255-L266 | P2 | 待修复 |
| 4.5 | 🟢 | Coupon.ComputeExpiredAt 对 ValidTo 为 null 直接 .Value 引用 | file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/Coupon.cs#L244-L249 | P2 | 待修复 |
| 4.6 | 🟢 | CouponExpiryService 重复调用 UpdateAsync（已 tracked） | file:///workspace/src/Services/Promotion/Leno.Promotion.Api/BackgroundServices/CouponExpiryService.cs#L69-L73 | P2 | 待修复 |
| 4.7 | 🟢 | PromotionGrpcService.CalculateDiscount 解析 UserId 未抛 RpcException | file:///workspace/src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs#L42 | P2 | 待修复 |
| 4.8 | 🟢 | PromotionActivityConfiguration Rules JSON 序列化未指定 options | file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Configurations/PromotionActivityConfiguration.cs#L33-L38 | P2 | 待修复 |
| 4.9 | 🟢 | PromotionGrpcService.CalculateDiscount 金额转分精度风险 | file:///workspace/src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs#L57-L58 | P2 | 待修复 |
| 4.10 | 🟢 | PromotionRule 默认构造与 init 字段并存，弱化不可变性 | file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/ValueObjects/PromotionRule.cs#L7-L37 | P2 | 待修复 |

---

## P0 详细修复计划（TDD bite-sized 格式）

> 优先级排序：2.5 → 2.3 → 2.4（库存回退状态机相关，资损风险最高）；2.1 → 2.2（过期券扫描）；2.6（OrderCancelled 死信）；2.9（秒杀幽灵失败）；2.7 → 2.8（活动激活与多 SKU 一致性）；2.10 → 2.11（DDD 违规）。

### P0-2.5 SeckillPreOccupationRecord.MarkRolledBack 不校验 IsFulfilled

**问题根因**：聚合根 `MarkRolledBack` 仅幂等检查 `IsRolledBack`，未阻止"已履约再回退"，与 2.4 叠加可产生 `IsFulfilled=true && IsRolledBack=true` 非法状态。

**步骤 1：测试** —— 在 `tests/.../Leno.Promotion.Domain.Tests/PromotionDomainTests.cs` 的 `SeckillPreOccupationRecordTests` 类中新增测试方法：

```csharp
[Fact]
public void MarkRolledBack_AfterFulfilled_ShouldThrowException()
{
    var record = CreateRecord();
    record.MarkFulfilled();

    var act = () => record.MarkRolledBack();

    act.Should().Throw<PromotionDomainException>()
        .WithMessage("*已履约*");
}
```

**步骤 2：验证失败** —— 运行 `dotnet test --filter "FullyQualifiedName~MarkRolledBack_AfterFulfilled_ShouldThrowException"`，期望失败（当前实现仅返回幂等无异常）。

**步骤 3：实现** —— 修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillPreOccupationRecord.cs#L82-L91：

```csharp
/// <summary>标记回退；已履约的预占记录不可回退，抛 <see cref="PromotionDomainException"/>。</summary>
public void MarkRolledBack()
{
    if (IsRolledBack)
    {
        return;
    }

    if (IsFulfilled)
    {
        throw new PromotionDomainException(
            "已履约的预占记录不可回退", "PRE_OCCUPATION_FULFILLED");
    }

    IsRolledBack = true;
    RolledBackAt = DateTime.UtcNow;
}
```

**步骤 4：验证通过** —— 重新运行 `dotnet test --filter "FullyQualifiedName~SeckillPreOccupationRecordTests"`，全部测试通过；现有 `MarkRolledBack_ShouldSetRolledBack` / `MarkRolledBack_Twice_ShouldNotThrow` 仍通过（幂等分支保留）。

**步骤 5：提交** —— `git commit -m "fix(promotion): SeckillPreOccupationRecord.MarkRolledBack 校验 IsFulfilled 状态守卫 (#2.5)"`

---

### P0-2.3 SeckillOrderCreationFailedEventConsumer 双重复回退库存膨胀

**问题根因**：失败事件消费者在 `record.MarkRolledBack()` 后**无条件**继续执行 RestoreAsync + RestoreStock；若补偿服务已先行回退，则 `record.IsRolledBack == true`，但代码仅跳过 `MarkRolledBack`，仍重复 Restore Redis 与 DB 库存。

**步骤 1：测试** —— 在 file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure.Tests/ConsumerTests.cs#L302 的 `SeckillOrderCreationFailedEventConsumerTests` 类新增测试方法：

```csharp
[Fact]
public async Task Consume_AlreadyRolledBack_ShouldSkipRestore()
{
    var activityId = Guid.NewGuid();
    var skuId = Guid.NewGuid();
    var orderId = Guid.NewGuid();
    var record = SeckillPreOccupationRecord.Create(activityId, skuId, Guid.NewGuid(), orderId, 5);
    record.MarkRolledBack(); // 模拟补偿服务已先行回退

    _preOccupationRepoMock.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(record);
    _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

    var consumer = new SeckillOrderCreationFailedEventConsumer(
        _activityRepoMock.Object, _stockServiceMock.Object, _preOccupationRepoMock.Object,
        _unitOfWorkMock.Object, _loggerMock.Object, _idempotencyStoreMock.Object);

    var evt = new SeckillOrderCreationFailedIntegrationEvent(activityId, skuId, Guid.NewGuid(), orderId, 5, "fail");
    await consumer.Consume(CreateConsumeContext(evt));

    _stockServiceMock.Verify(
        s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
        Times.Never);
    _activityRepoMock.Verify(
        r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
        Times.Never);
    _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task Consume_RecordNotFound_ShouldSkipAndLog()
{
    var orderId = Guid.NewGuid();
    _preOccupationRepoMock.Setup(r => r.GetByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
        .ReturnsAsync((SeckillPreOccupationRecord?)null);

    var consumer = new SeckillOrderCreationFailedEventConsumer(
        _activityRepoMock.Object, _stockServiceMock.Object, _preOccupationRepoMock.Object,
        _unitOfWorkMock.Object, _loggerMock.Object, _idempotencyStoreMock.Object);

    var evt = new SeckillOrderCreationFailedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), orderId, 5, "fail");
    await consumer.Consume(CreateConsumeContext(evt));

    _stockServiceMock.Verify(
        s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
        Times.Never);
    _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
}
```

**步骤 2：验证失败** —— 运行 `dotnet test --filter "FullyQualifiedName~Consume_AlreadyRolledBack_ShouldSkipRestore"`，期望失败（当前实现仍会调 RestoreAsync）。

**步骤 3：实现** —— 修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/SeckillOrderEventConsumer.cs#L42-L69 的 `HandleAsync`：

```csharp
/// <inheritdoc />
protected override async Task HandleAsync(SeckillOrderCreationFailedIntegrationEvent integrationEvent, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(integrationEvent);

    var record = await _preOccupationRecordRepository.GetByOrderIdAsync(integrationEvent.OrderId, ct);
    if (record is null)
    {
        Logger.LogWarning("未找到预占记录 OrderId={OrderId}，跳过回退", integrationEvent.OrderId);
        return;
    }

    if (record.IsRolledBack)
    {
        Logger.LogInformation("预占记录已回退 OrderId={OrderId}，幂等跳过", integrationEvent.OrderId);
        return;
    }

    record.MarkRolledBack();

    // 回退 Redis 库存
    await _stockService.RestoreAsync(integrationEvent.ActivityId, integrationEvent.SkuId, integrationEvent.Quantity, ct);

    // 回退 DB 基线库存
    var activity = await _activityRepository.GetByIdAsync(integrationEvent.ActivityId, ct);
    if (activity is not null)
    {
        activity.RestoreStock(integrationEvent.Quantity);
    }

    await _unitOfWork.SaveEntitiesAsync(ct);

    Logger.LogInformation(
        "秒杀订单创建失败回退完成 OrderId={OrderId} ActivityId={ActivityId} SkuId={SkuId} Quantity={Quantity} Reason={Reason}",
        integrationEvent.OrderId, integrationEvent.ActivityId, integrationEvent.SkuId, integrationEvent.Quantity, integrationEvent.Reason);
}
```

**步骤 4：验证通过** —— 运行 `dotnet test --filter "FullyQualifiedName~SeckillOrderCreationFailedEventConsumerTests"`，全部测试通过；原 `Consume_ShouldRollbackRedisAndDb` 仍通过（IsRolledBack 初始为 false）。

**步骤 5：提交** —— `git commit -m "fix(promotion): SeckillOrderCreationFailedEventConsumer 校验 IsRolledBack 防双重复回退 (#2.3)"`

---

### P0-2.4 SeckillPreOccupationCompensationService TOCTOU 补偿与履约竞态

**问题根因**：补偿服务读取未履约记录后，在 RestoreAsync/RestoreStock/MarkRolledBack 之间无事务与状态再校验，若中间 SeckillOrderConfirmedEventConsumer 已置 IsFulfilled=true，补偿仍会回退库存，产生非法状态。

**步骤 1：测试** —— 新建测试类 `SeckillPreOccupationCompensationServiceTests` 在 file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure.Tests/ 下（建议新建 `CompensationServiceTests.cs`）：

```csharp
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.Services;
using Leno.Promotion.Infrastructure.BackgroundServices;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Leno.Infrastructure.Abstractions;
using Moq;

namespace Leno.Promotion.Infrastructure.Tests;

public class SeckillPreOccupationCompensationServiceTests
{
    private readonly Mock<ISeckillPreOccupationRecordRepository> _recordRepoMock = new();
    private readonly Mock<ISeckillActivityRepository> _activityRepoMock = new();
    private readonly Mock<ISeckillStockService> _stockServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IUnitOfWorkTransaction> _txMock = new();

    private static readonly Guid ActivityId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();

    [Fact]
    public async Task Compensate_RecordFulfilledDuringWindow_ShouldSkipAndNotRestore()
    {
        var record = CreateRecord();
        record.MarkFulfilled(); // 模拟竞态：补偿读取后，履约事件先行落库
        _recordRepoMock.Setup(r => r.GetUnfulfilledAsync(It.IsAny<DateTime>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SeckillPreOccupationRecord> { record });
        _recordRepoMock.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_txMock.Object);
        _txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await InvokeCompensateAsync();

        _stockServiceMock.Verify(
            s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Compensate_ValidRecord_ShouldRestoreAndMarkRolledBack()
    {
        var activity = SeckillActivity.Create(ActivityId, Guid.NewGuid(), SkuId, 99m, 199m, 100, 1,
            DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(2));
        activity.Activate();
        var record = CreateRecord();
        _recordRepoMock.Setup(r => r.GetUnfulfilledAsync(It.IsAny<DateTime>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SeckillPreOccupationRecord> { record });
        _recordRepoMock.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _activityRepoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        _unitOfWorkMock.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_txMock.Object);
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _stockServiceMock.Setup(s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _txMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await InvokeCompensateAsync();

        record.IsRolledBack.Should().BeTrue();
        _stockServiceMock.Verify(
            s => s.RestoreAsync(ActivityId, SkuId, 1, It.IsAny<CancellationToken>()), Times.Once);
        _txMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task InvokeCompensateAsync()
    {
        var scopeFactory = new ServiceCollection()
            .AddSingleton(_recordRepoMock.Object)
            .AddSingleton(_activityRepoMock.Object)
            .AddSingleton(_stockServiceMock.Object)
            .AddSingleton(_unitOfWorkMock.Object)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var svc = new SeckillPreOccupationCompensationService(
            scopeFactory, new Mock<ILogger<SeckillPreOccupationCompensationService>>().Object);
        var method = typeof(SeckillPreOccupationCompensationService).GetMethod(
            "CompensateAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(svc, new object[] { CancellationToken.None })!;
    }

    private static SeckillPreOccupationRecord CreateRecord()
        => SeckillPreOccupationRecord.Create(ActivityId, SkuId, Guid.NewGuid(), Guid.NewGuid(), 1);
}
```

**步骤 2：验证失败** —— 运行 `dotnet test --filter "FullyQualifiedName~SeckillPreOccupationCompensationServiceTests"`，期望失败（当前实现无事务无再校验，竞态场景仍会 Restore）。

**步骤 3：实现** —— 修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/BackgroundServices/SeckillPreOccupationCompensationService.cs#L59-L103 的 `CompensateAsync` 方法：

```csharp
private async Task CompensateAsync(CancellationToken ct)
{
    using var scope = _scopeFactory.CreateScope();
    var recordRepository = scope.ServiceProvider.GetRequiredService<ISeckillPreOccupationRecordRepository>();
    var activityRepository = scope.ServiceProvider.GetRequiredService<ISeckillActivityRepository>();
    var stockService = scope.ServiceProvider.GetRequiredService<ISeckillStockService>();
    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    var timeout = DateTime.UtcNow - TimeoutThreshold;
    var records = await recordRepository.GetUnfulfilledAsync(timeout, 0, BatchSize, ct);

    if (records.Count == 0)
    {
        return;
    }

    _logger.LogInformation("扫描到 {Count} 条超时未履约预占记录，开始补偿", records.Count);

    foreach (var record in records)
    {
        try
        {
            await using var tx = await unitOfWork.BeginTransactionAsync(ct);

            // 事务内重新加载记录，校验状态是否在读取后被变更
            var fresh = await recordRepository.GetByIdAsync(record.Id, ct);
            if (fresh is null || fresh.IsFulfilled || fresh.IsRolledBack)
            {
                _logger.LogInformation(
                    "记录已变更 OrderId={OrderId} IsFulfilled={IsFulfilled} IsRolledBack={IsRolledBack}，跳过补偿",
                    record.OrderId, fresh?.IsFulfilled ?? false, fresh?.IsRolledBack ?? false);
                continue;
            }

            // 回退 Redis 库存
            await stockService.RestoreAsync(fresh.ActivityId, fresh.SkuId, fresh.Quantity, ct);

            // 回退 DB 基线库存
            var activity = await activityRepository.GetByIdAsync(fresh.ActivityId, ct);
            if (activity is not null)
            {
                activity.RestoreStock(fresh.Quantity);
            }

            fresh.MarkRolledBack();
            await unitOfWork.SaveEntitiesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "补偿回退完成 OrderId={OrderId} ActivityId={ActivityId} SkuId={SkuId} Quantity={Quantity}",
                fresh.OrderId, fresh.ActivityId, fresh.SkuId, fresh.Quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "补偿回退失败 OrderId={OrderId}", record.OrderId);
        }
    }
}
```

**步骤 4：验证通过** —— 运行 `dotnet test --filter "FullyQualifiedName~SeckillPreOccupationCompensationServiceTests"`，两个测试均通过。

**步骤 5：提交** —— `git commit -m "fix(promotion): 补偿服务事务内重校验状态防 TOCTOU 竞态 (#2.4)"`

---

### P0-2.1 CouponExpiryService 分页 skip 累加导致漏处理过期券

**问题根因**：扫描循环 `skip += BatchSize`，第一批 Expire 后状态由 Unused→Expired，下次查询时这批已不在结果集，但 `Skip(500)` 实际跳过的是当前结果集前 500 条（即原 501-1000 号记录），导致这 500 张永远不被处理。

**步骤 1：测试** —— 新建 `CouponExpiryServiceTests.cs` 于 file:///workspace/src/Services/Promotion/Leno.Promotion.Api.Tests/：

```csharp
using Leno.Promotion.Api.BackgroundServices;
using Leno.Promotion.Domain.Aggregates;
using Leno.Promotion.Domain.Repositories;
using Leno.Promotion.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Promotion.Api.Tests;

public class CouponExpiryServiceTests
{
    private readonly Mock<IUserCouponRepository> _userCouponRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task ProcessExpiredCouponsAsync_LargeBatch_ShouldNotSkipRecords()
    {
        // 模拟 1200 张过期券，分两批返回（每批 500 + 200）
        var callCount = 0;
        var allCoupons = Enumerable.Range(0, 700).Select(_ => CreateUserCoupon()).ToList();

        _userCouponRepoMock.Setup(r => r.GetExpiredUnusedCouponsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // 第一批：返回前 500 张，模拟 Expire 后第二批只剩 200 张
                    return allCoupons.Take(500).ToList();
                }
                if (callCount == 2)
                {
                    // 第二批：skip=0 时返回剩余 200 张
                    return allCoupons.Skip(500).Take(200).ToList();
                }
                return new List<UserCoupon>();
            });
        _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await InvokeProcessExpiredCouponsAsync();

        // 关键断言：每次查询 skip 始终为 0（依赖状态过滤淘汰已处理记录），不会漏处理
        _userCouponRepoMock.Verify(
            r => r.GetExpiredUnusedCouponsAsync(0, 500, It.IsAny<CancellationToken>()),
            Times.AtLeast(2));
        _userCouponRepoMock.Verify(
            r => r.GetExpiredUnusedCouponsAsync(It.Is<int>(s => s > 0), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private async Task InvokeProcessExpiredCouponsAsync()
    {
        var scopeFactory = new ServiceCollection()
            .AddSingleton(_userCouponRepoMock.Object)
            .AddSingleton(_unitOfWorkMock.Object)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();
        var svc = new CouponExpiryService(scopeFactory, new Mock<ILogger<CouponExpiryService>>().Object);
        var method = typeof(CouponExpiryService).GetMethod(
            "ProcessExpiredCouponsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(svc, new object[] { CancellationToken.None })!;
    }

    private static UserCoupon CreateUserCoupon()
        => UserCoupon.Receive(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Manual", DateTime.UtcNow.AddDays(30));
}
```

**步骤 2：验证失败** —— 运行 `dotnet test --filter "FullyQualifiedName~CouponExpiryServiceTests"`，期望失败（当前实现 `skip += BatchSize`）。

**步骤 3：实现** —— 修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Api/BackgroundServices/CouponExpiryService.cs#L57-L80，移除 skip 累加，始终 skip=0 依赖状态过滤：

```csharp
private async Task ProcessExpiredCouponsAsync(CancellationToken ct)
{
    using var scope = _scopeFactory.CreateScope();
    var userCouponRepository = scope.ServiceProvider.GetRequiredService<IUserCouponRepository>();
    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    var totalExpired = 0;

    while (!ct.IsCancellationRequested)
    {
        // 始终 skip=0：依赖 WHERE Status==Unused 过滤淘汰已 Expire 的记录
        // 避免原 skip += BatchSize 在状态变更后跳页导致的漏处理
        var batch = await userCouponRepository.GetExpiredUnusedCouponsAsync(0, BatchSize, ct);

        if (batch.Count == 0)
        {
            break;
        }

        foreach (var userCoupon in batch)
        {
            userCoupon.Expire();
        }

        await unitOfWork.SaveEntitiesAsync(ct);
        totalExpired += batch.Count;

        _logger.LogDebug("已处理一批过期优惠券，本批 {Count} 张，累计 {Total} 张", batch.Count, totalExpired);
    }

    if (totalExpired > 0)
    {
        _logger.LogInformation("优惠券过期处理完成，共标记过期 {Total} 张", totalExpired);
    }
}
```

> 说明：同时移除了对 `UpdateAsync` 的冗余调用（已 tracked 实体状态自动变 Modified），一并解决 P2-4.6。

**步骤 4：验证通过** —— 运行 `dotnet test --filter "FullyQualifiedName~CouponExpiryServiceTests"`，测试通过。

**步骤 5：提交** —— `git commit -m "fix(promotion): CouponExpiryService 改用 skip=0 状态过滤避免漏处理过期券 (#2.1) (#4.6)"`

---

### P0-2.2 CouponExpiryService 仅扫描 Unused，遗漏 Locked+Expired 券

**问题根因**：查询过滤条件 `Status == CouponStatus.Unused`，但 `UserCoupon.Expire` 允许从 `Locked` 转 `Expired`。订单长时间挂起导致 Locked+Expired 券被永久占位，过期扫描永远不会触及。

**步骤 1：测试** —— 在 `EfCoreUserCouponRepositoryTests`（如不存在则新建）或仓储契约层增加查询验证。先扩展 `IUserCouponRepository` 接口注释，再验证仓储实现。新增 `CouponExpiryServiceTests` 中的查询覆盖测试：

```csharp
[Fact]
public async Task GetExpiredUnusedCouponsAsync_ShouldIncludeLockedExpiredCoupons()
{
    // 此测试验证仓储接口契约：扫描应同时包含 Unused+Expired 与 Locked+Expired
    var now = DateTime.UtcNow;
    var unusedExpired = UserCoupon.Receive(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Manual", now.AddHours(-1));
    var lockedExpired = UserCoupon.Receive(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Manual", now.AddHours(-1));
    lockedExpired.Lock(Guid.NewGuid());

    _userCouponRepoMock.Setup(r => r.GetExpiredUnusedCouponsAsync(0, 500, It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<UserCoupon> { unusedExpired, lockedExpired });
    _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

    await InvokeProcessExpiredCouponsAsync();

    // 关键断言：仓储应返回 Locked 态过期券，CouponExpiryService 应能调用 Expire() 处理之
    // UserCoupon.Expire 已允许 Locked → Expired 转换
    unusedExpired.Status.Should().Be(CouponStatus.Expired);
    lockedExpired.Status.Should().Be(CouponStatus.Expired);
}
```

**步骤 2：验证失败** —— 当前仓储 `WHERE Status == CouponStatus.Unused` 仅返回 Unused，测试模拟仓储返回的 lockedExpired 在生产环境根本不会被查到。测试需通过契约层 mock 验证期望行为，运行后期望失败（仓储查询条件不匹配）。

**步骤 3：实现** —— 修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Repositories/EfCoreUserCouponRepository.cs#L76-L88，将查询条件扩展为 `Unused || Locked`：

```csharp
/// <inheritdoc />
public async Task<List<UserCouponAggregate>> GetExpiredUnusedCouponsAsync(
    int skip,
    int take,
    CancellationToken ct = default)
{
    var now = DateTime.UtcNow;
    return await _context.UserCoupons
        .Where(u => (u.Status == CouponStatus.Unused || u.Status == CouponStatus.Locked)
                    && u.ExpiredAt.HasValue && u.ExpiredAt.Value < now)
        .OrderBy(u => u.ExpiredAt)
        .Skip(skip)
        .Take(take)
        .ToListAsync(ct);
}
```

> 方法名保持 `GetExpiredUnusedCouponsAsync` 以避免破坏调用方签名，但语义扩展为"过期且未核销（Unused 或 Locked）"。

**步骤 4：验证通过** —— 运行 `dotnet test --filter "FullyQualifiedName~CouponExpiryServiceTests"`，所有测试通过。

**步骤 5：提交** —— `git commit -m "fix(promotion): 过期券扫描扩展为 Unused+Locked 双状态避免遗漏 (#2.2)"`

---

### P0-2.6 OrderCancelledEventConsumer 在券已核销时 Release 抛错死信

**问题根因**：`OrderCancelledEventConsumer` 直接调 `userCoupon.Release()`，但 `Release` 要求 `Status == Locked`。若订单先经 `OrderPaidEventConsumer`（券 Locked→Used）后再被取消，`Release()` 会抛 `USER_COUPON_RELEASE_INVALID`，MassTransit 重试耗尽进入死信。

**步骤 1：测试** —— 在 file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure.Tests/ConsumerTests.cs#L104 的 `OrderCancelledEventConsumerTests` 类新增：

```csharp
[Fact]
public async Task Consume_CouponAlreadyUsed_ShouldSkipWithoutThrowing()
{
    // 业务场景：订单先支付（券 Locked→Used）后又被取消（如退款流程触发取消事件）
    // 此时 Release 会抛 USER_COUPON_RELEASE_INVALID，应改为跳过并记录日志，不应死信
    var orderId = Guid.NewGuid();
    var userCoupon = CreateUserCoupon();
    userCoupon.Lock(orderId);
    userCoupon.Consume(orderId); // 模拟券已核销
    _userCouponRepoMock.Setup(r => r.GetByLockedOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(userCoupon);
    _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

    var consumer = new OrderCancelledEventConsumer(
        _userCouponRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object, _idempotencyStoreMock.Object);

    var evt = new OrderCancelledEvent(orderId, Guid.NewGuid(), "refund-triggered-cancel", DateTime.UtcNow, "System", 0);

    // 关键断言：不应抛异常（避免 MassTransit 死信）
    var act = () => consumer.Consume(CreateConsumeContext(evt));
    await act.Should().NotThrowAsync();

    userCoupon.Status.Should().Be(CouponStatus.Used); // 状态保持 Used，由 RefundCompleted 退还
    _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task Consume_CouponStatusNotLocked_ShouldIdempotentSkip()
{
    // 防御性：券已 Expired 或其他非 Locked 状态时幂等跳过
    var orderId = Guid.NewGuid();
    var userCoupon = CreateUserCoupon();
    userCoupon.Expire(); // 已 Expired
    // 注意 Expire 会清空 LockedOrderId，因此仓储查询不会命中
    // 此测试覆盖其他潜在非 Locked 场景（如直接 mock 返回 Expired 状态）
    _userCouponRepoMock.Setup(r => r.GetByLockedOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(userCoupon);

    var consumer = new OrderCancelledEventConsumer(
        _userCouponRepoMock.Object, _unitOfWorkMock.Object, _loggerMock.Object, _idempotencyStoreMock.Object);

    var evt = new OrderCancelledEvent(orderId, Guid.NewGuid(), "cancel", DateTime.UtcNow, "Buyer", 0);

    var act = () => consumer.Consume(CreateConsumeContext(evt));
    await act.Should().NotThrowAsync();
    _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
}
```

**步骤 2：验证失败** —— 运行 `dotnet test --filter "FullyQualifiedName~Consume_CouponAlreadyUsed_ShouldSkipWithoutThrowing"`，期望失败（当前实现直接调 Release 会抛 `USER_COUPON_RELEASE_INVALID`）。

**步骤 3：实现** —— 修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/OrderEventConsumer.cs#L82-L99 的 `OrderCancelledEventConsumer.HandleAsync`：

```csharp
/// <inheritdoc />
protected override async Task HandleAsync(OrderCancelledEvent integrationEvent, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(integrationEvent);

    var userCoupon = await _userCouponRepository.GetByLockedOrderIdAsync(integrationEvent.OrderId, ct);
    if (userCoupon is null)
    {
        Logger.LogInformation("订单 {OrderId} 未绑定优惠券，跳过退还", integrationEvent.OrderId);
        return;
    }

    // 状态前置检查：券已核销（Used）说明订单已支付后又被取消，
    // 应由 RefundCompletedEventConsumer 走 Return 流程退还，此处不应死信
    if (userCoupon.Status == CouponStatus.Used)
    {
        Logger.LogInformation(
            "订单 {OrderId} 的券 {UserCouponId} 已核销（Used），跳过 Release（应由 RefundCompleted 退还）",
            integrationEvent.OrderId, userCoupon.Id);
        return;
    }

    // 防御性：其他非 Locked 状态（如已 Expired）幂等跳过
    if (userCoupon.Status != CouponStatus.Locked)
    {
        Logger.LogInformation(
            "订单 {OrderId} 券状态 {Status} 非 Locked，幂等跳过",
            integrationEvent.OrderId, userCoupon.Status);
        return;
    }

    userCoupon.Release();
    await _unitOfWork.SaveEntitiesAsync(ct);

    Logger.LogInformation("订单 {OrderId} 已退还用户券 {UserCouponId}",
        integrationEvent.OrderId, userCoupon.Id);
}
```

**步骤 4：验证通过** —— 运行 `dotnet test --filter "FullyQualifiedName~OrderCancelledEventConsumerTests"`，全部测试通过；原 `Consume_ValidCoupon_ShouldRelease` 仍通过（Locked 状态走 Release 分支）。

**步骤 5：提交** —— `git commit -m "fix(promotion): OrderCancelledEventConsumer 加状态前置检查避免死信 (#2.6)"`

---

### P0-2.9 SeckillAppService.PlaceOrderAsync DB 乐观锁冲突引发"幽灵失败"

**问题根因**：`activity.DeductStock` 在内存中扣减，`SaveEntitiesAsync` 经 rowversion 乐观锁提交，高并发下 N 个请求通过 Redis Lua 原子扣减（成功），但 DB 提交只能串行；除第一个外，其余均因 rowversion 不匹配抛 `DbUpdateConcurrencyException`，被 catch 回退 Redis，违背秒杀"高并发原子扣减"目标。

**步骤 1：测试** —— 在 file:///workspace/src/Services/Promotion/Leno.Promotion.Application.Tests/PromotionAppServiceTests.cs#L220 的 `SeckillAppServiceTests` 类新增：

```csharp
[Fact]
public async Task PlaceOrderAsync_DbConcurrencyConflict_ShouldNotAffectRedisSuccess()
{
    // 秒杀高并发场景：N 个请求通过 Redis 扣减成功，但 DB 乐观锁只允许第一个提交，
    // 其余抛 DbUpdateConcurrencyException。修复后应：仅创建预占记录 + 发事件，
    // 不调用 activity.DeductStock，DB 不参与扣减热路径，由后台任务/对账同步基线。
    var activity = CreateActivity();
    activity.Activate();
    _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
    _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 2, 1, It.IsAny<CancellationToken>()))
        .ReturnsAsync(0);
    _preOccupationRecordRepoMock.Setup(r => r.AddAsync(It.IsAny<SeckillPreOccupationRecord>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

    var result = await _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 2 });

    result.Should().NotBeNull();
    result.OrderId.Should().NotBe(Guid.Empty);
    // 关键断言：不再调用 activity.DeductStock，DB AvailableStock 保持初始值
    activity.AvailableStock.Should().Be(100);
    // Redis 扣减成功后即使 DB 保存失败也不应回退 Redis（DB 不再参与扣减热路径）
    _stockServiceMock.Verify(s => s.RestoreAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    _preOccupationRecordRepoMock.Verify(r => r.AddAsync(It.IsAny<SeckillPreOccupationRecord>(), It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task PlaceOrderAsync_PreOccupationRecordSaveFailed_ShouldRollbackRedis()
{
    // 预占记录写入失败（非乐观锁冲突，如网络故障）时仍应回退 Redis，保持最终一致
    var activity = CreateActivity();
    activity.Activate();
    _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
    _stockServiceMock.Setup(s => s.TryDeductAsync(ActivityId, SkuId, UserId, 2, 1, It.IsAny<CancellationToken>()))
        .ReturnsAsync(0);
    _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("Network failure"));

    var act = () => _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = SkuId, Quantity = 2 });

    await act.Should().ThrowAsync<InvalidOperationException>();
    _stockServiceMock.Verify(s => s.RestoreAsync(ActivityId, SkuId, 2, It.IsAny<CancellationToken>()), Times.Once);
}
```

**步骤 2：验证失败** —— 运行 `dotnet test --filter "FullyQualifiedName~PlaceOrderAsync_DbConcurrencyConflict_ShouldNotAffectRedisSuccess"`，期望失败（当前实现调用 `activity.DeductStock`，AvailableStock 会变为 98）。

**步骤 3：实现** —— 修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L92-L163 的 `PlaceOrderAsync`，将 DB 基线扣减从热路径剥离，仅创建预占记录：

```csharp
/// <inheritdoc />
public async Task<SeckillPlaceOrderResultDto> PlaceOrderAsync(
    Guid activityId,
    Guid userId,
    SeckillPlaceOrderDto dto,
    CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(dto);

    if (userId == Guid.Empty)
    {
        throw new PromotionDomainException("UserId 不可为空", "SECKILL_USER_EMPTY");
    }

    if (dto.Quantity <= 0)
    {
        throw new PromotionDomainException("下单数量须大于 0", "SECKILL_QTY_INVALID");
    }

    var activity = await RequireActivityAsync(activityId, ct);

    // 使用请求中的 SkuId，若未指定则使用活动的默认 SkuId
    var skuId = dto.SkuId != Guid.Empty ? dto.SkuId : activity.SkuId;

    // 1. Redis 原子预扣库存 + 限购校验（高频热路径，支持多 SKU）
    var deductResult = await _stockService.TryDeductAsync(
        activity.Id, skuId, userId, dto.Quantity, activity.LimitPerUser, ct);

    if (deductResult != 0)
    {
        var reason = deductResult switch
        {
            1 => "库存不足",
            2 => "超出限购",
            _ => "未知错误"
        };
        throw new PromotionDomainException(
            $"秒杀失败：{reason}", "SECKILL_DEDUCT_FAILED");
    }

    // 2. Redis 预扣成功后仅创建预占记录 + 发事件（不调用 activity.DeductStock）
    // DB 基线（AvailableStock）由后台对账任务或活动结束时 WriteBackToDbAsync 同步，
    // 避免 rowversion 乐观锁冲突导致"幽灵失败"，热路径仅写预占记录 + 发件箱事件
    Guid orderId;
    try
    {
        orderId = Guid.NewGuid();
        activity.RecordOrderCreated(userId, orderId, dto.Quantity);

        var preOccupationRecord = SeckillPreOccupationRecord.Create(
            activity.Id, skuId, userId, orderId, dto.Quantity);
        await _preOccupationRecordRepository.AddAsync(preOccupationRecord, ct);

        await _unitOfWork.SaveEntitiesAsync(ct);
    }
    catch
    {
        // 预占记录写入失败（非乐观锁冲突），回退 Redis 预扣保持库存最终一致
        await _stockService.RestoreAsync(activity.Id, skuId, dto.Quantity, CancellationToken.None);
        throw;
    }

    return new SeckillPlaceOrderResultDto
    {
        OrderId = orderId,
        ActivityId = activity.Id,
        UserId = userId,
        SeckillPrice = activity.SeckillPrice,
        Quantity = dto.Quantity,
        PlacedAt = DateTime.UtcNow
    };
}
```

**步骤 4：验证通过** —— 运行 `dotnet test --filter "FullyQualifiedName~SeckillAppServiceTests"`，所有测试通过（原 `PlaceOrderAsync_Valid_ShouldReturnResult` 也通过，仅依赖 Redis 扣减 + 预占记录写入）。

**步骤 5：提交** —— `git commit -m "fix(promotion): PlaceOrderAsync 剥离 DB 扣减出热路径消除幽灵失败 (#2.9)"`

---

### P0-2.7 SeckillAppService.ActivateAsync Redis 初始化失败但 DB 仍标记 Active

**问题根因**：调用顺序为 `activity.Activate()` → `await _stockService.InitializeAsync(...)` → `await _unitOfWork.SaveEntitiesAsync(ct)`。`activity.Activate()` 在内存中改 Status 为 Active，但若 Redis InitializeAsync 抛异常，DB 中状态仍为 Pending（SaveEntities 未执行）。实际隐患：若 Redis 部分成功（HashSetAsync 覆盖已有库存），老库存被重置；Redis 异常半成功，后续 PlaceOrder 用错误库存。

**步骤 1：测试** —— 在 `SeckillAppServiceTests` 类新增：

```csharp
[Fact]
public async Task ActivateAsync_RedisInitFailed_ShouldNotMarkActivityActive()
{
    // Redis 初始化失败时，聚合状态不应被持久化为 Active
    var activity = CreateActivity();
    _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
    _stockServiceMock.Setup(s => s.InitializeAsync(ActivityId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("Redis connection refused"));
    _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

    var act = () => _sut.ActivateAsync(ActivityId);

    await act.Should().ThrowAsync<PromotionDomainException>()
        .WithMessage("*Redis*");
    // 关键断言：聚合内存状态回退为 Pending（未被持久化）
    activity.Status.Should().Be(SeckillStatus.Pending);
    _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task ActivateAsync_RedisInitSucceeded_ShouldMarkActiveAndSave()
{
    var activity = CreateActivity();
    _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);
    _stockServiceMock.Setup(s => s.InitializeAsync(ActivityId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    _uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

    await _sut.ActivateAsync(ActivityId);

    activity.Status.Should().Be(SeckillStatus.Active);
    _stockServiceMock.Verify(s => s.InitializeAsync(ActivityId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()), Times.Once);
    _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
}
```

**步骤 2：验证失败** —— 运行 `dotnet test --filter "FullyQualifiedName~ActivateAsync_RedisInitFailed_ShouldNotMarkActivityActive"`，期望失败（当前实现先 `activity.Activate()` 再 Initialize，Redis 异常时内存中 Status 已为 Active）。

**步骤 3：实现** —— 修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L56-L69：

```csharp
/// <inheritdoc />
public async Task ActivateAsync(Guid activityId, CancellationToken ct = default)
{
    var activity = await RequireActivityAsync(activityId, ct);

    // 先初始化 Redis 库存，成功后再改聚合状态，避免 Redis 故障期间聚合被错误标记为 Active
    var skuStocks = new Dictionary<Guid, int>
    {
        { activity.SkuId, activity.TotalStock }
    };
    try
    {
        await _stockService.InitializeAsync(activity.Id, skuStocks, ct);
    }
    catch (Exception ex)
    {
        throw new PromotionDomainException(
            $"秒杀活动 {activityId} Redis 库存初始化失败：{ex.Message}", "SECKILL_REDIS_INIT_FAILED", ex);
    }

    activity.Activate();
    await _unitOfWork.SaveEntitiesAsync(ct);
}
```

**步骤 4：验证通过** —— 运行 `dotnet test --filter "FullyQualifiedName~ActivateAsync_RedisInitFailed_ShouldNotMarkActivityActive|FullyQualifiedName~ActivateAsync_RedisInitSucceeded_ShouldMarkActiveAndSave"`，两个测试均通过。

**步骤 5：提交** —— `git commit -m "fix(promotion): ActivateAsync 先初始化 Redis 再改聚合状态保证一致性 (#2.7)"`

---

### P0-2.8 SeckillAppService.PlaceOrderAsync 多 SKU 路径下 DB DeductStock 与 Redis 不一致

**问题根因**：`var skuId = dto.SkuId != Guid.Empty ? dto.SkuId : activity.SkuId;` Redis 用此 skuId 扣减，但 `activity.DeductStock(userId, dto.Quantity)` 不接受 skuId，仅扣减聚合单一 `AvailableStock` 字段。`SeckillActivity` 聚合只有单一 `SkuId`，而接口标榜"支持多 SKU"，契约不一致。

**步骤 1：测试** —— 在 `SeckillAppServiceTests` 类新增：

```csharp
[Fact]
public async Task PlaceOrderAsync_NonDefaultSkuId_ShouldRejectMultiSku()
{
    // 修复策略：移除多 SKU 支持，接口与实现统一为单 SKU
    // 调用方传非默认 SkuId（与 activity.SkuId 不一致）应抛异常
    var activity = CreateActivity();
    activity.Activate();
    var otherSkuId = Guid.NewGuid(); // 与 activity.SkuId 不同
    _repoMock.Setup(r => r.GetByIdAsync(ActivityId, It.IsAny<CancellationToken>())).ReturnsAsync(activity);

    var act = () => _sut.PlaceOrderAsync(ActivityId, UserId, new SeckillPlaceOrderDto { SkuId = otherSkuId, Quantity = 1 });

    await act.Should().ThrowAsync<PromotionDomainException>()
        .WithMessage("*SkuId*");
    _stockServiceMock.Verify(
        s => s.TryDeductAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
        Times.Never);
}
```

> 注：原 `PlaceOrderAsync_WithExplicitSkuId_ShouldUseProvidedSkuId` 测试需同步调整为"传 activity.SkuId 时正常"，"传非默认 SkuId 时拒绝"。

**步骤 2：验证失败** —— 运行 `dotnet test --filter "FullyQualifiedName~PlaceOrderAsync_NonDefaultSkuId_ShouldRejectMultiSku"`，期望失败（当前实现允许任意 SkuId 传入 Redis 扣减）。

**步骤 3：实现** —— 修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L92-L163 的 `PlaceOrderAsync`，移除多 SKU 支持，强制使用 `activity.SkuId`：

```csharp
/// <inheritdoc />
public async Task<SeckillPlaceOrderResultDto> PlaceOrderAsync(
    Guid activityId,
    Guid userId,
    SeckillPlaceOrderDto dto,
    CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(dto);

    if (userId == Guid.Empty)
    {
        throw new PromotionDomainException("UserId 不可为空", "SECKILL_USER_EMPTY");
    }

    if (dto.Quantity <= 0)
    {
        throw new PromotionDomainException("下单数量须大于 0", "SECKILL_QTY_INVALID");
    }

    var activity = await RequireActivityAsync(activityId, ct);

    // 单 SKU 契约：SeckillActivity 聚合仅持有单一 SkuId，
    // 调用方传非默认 SkuId 视为非法（与聚合 SkuId 不一致），拒绝请求
    if (dto.SkuId != Guid.Empty && dto.SkuId != activity.SkuId)
    {
        throw new PromotionDomainException(
            $"SkuId {dto.SkuId} 与活动 {activityId} 的 SkuId {activity.SkuId} 不一致",
            "SECKILL_SKU_MISMATCH");
    }

    var skuId = activity.SkuId;

    // 1. Redis 原子预扣库存 + 限购校验（单 SKU）
    var deductResult = await _stockService.TryDeductAsync(
        activity.Id, skuId, userId, dto.Quantity, activity.LimitPerUser, ct);

    if (deductResult != 0)
    {
        var reason = deductResult switch
        {
            1 => "库存不足",
            2 => "超出限购",
            _ => "未知错误"
        };
        throw new PromotionDomainException(
            $"秒杀失败：{reason}", "SECKILL_DEDUCT_FAILED");
    }

    // 2. Redis 预扣成功后仅创建预占记录 + 发事件（DB 基线由对账同步）
    Guid orderId;
    try
    {
        orderId = Guid.NewGuid();
        activity.RecordOrderCreated(userId, orderId, dto.Quantity);

        var preOccupationRecord = SeckillPreOccupationRecord.Create(
            activity.Id, skuId, userId, orderId, dto.Quantity);
        await _preOccupationRecordRepository.AddAsync(preOccupationRecord, ct);

        await _unitOfWork.SaveEntitiesAsync(ct);
    }
    catch
    {
        await _stockService.RestoreAsync(activity.Id, skuId, dto.Quantity, CancellationToken.None);
        throw;
    }

    return new SeckillPlaceOrderResultDto
    {
        OrderId = orderId,
        ActivityId = activity.Id,
        UserId = userId,
        SeckillPrice = activity.SeckillPrice,
        Quantity = dto.Quantity,
        PlacedAt = DateTime.UtcNow
    };
}
```

**步骤 4：验证通过** —— 运行 `dotnet test --filter "FullyQualifiedName~SeckillAppServiceTests"`，所有测试通过（包括调整后的 `PlaceOrderAsync_WithExplicitSkuId_ShouldUseProvidedSkuId`：传 `activity.SkuId` 时正常）。

**步骤 5：提交** —— `git commit -m "fix(promotion): 移除秒杀多 SKU 支持统一为单 SKU 契约 (#2.8) (#2.9)"`

---

### P0-2.10 PromotionActivity.Rules 直接暴露 List 违反 DDD 不变量封装

**问题根因**：`public List<PromotionRule> Rules { get; private set; } = new();` 公开 List 引用，外部代码可绕过 `AddRule`/`RemoveRule` 直接 `activity.Rules.Add(...)` 或 `activity.Rules.Clear()`，破坏"按门槛升序、不可重复门槛"不变量。

**步骤 1：测试** —— 在 file:///workspace/src/Services/Promotion/Leno.Promotion.Domain.Tests/PromotionDomainTests.cs#L746 的 `PromotionActivityTests` 类新增：

```csharp
[Fact]
public void Rules_ShouldBeReadOnlyList_CannotMutateExternally()
{
    var activity = CreateActivity();
    activity.AddRule(100m, 10m);

    // 关键断言：Rules 属性返回 IReadOnlyList<PromotionRule>，
    // 外部代码不能调用 Add/Remove/Clear 直接修改集合
    activity.Rules.Should().BeAssignableTo<IReadOnlyList<PromotionRule>>();
    var act = () => ((List<PromotionRule>)activity.Rules).Add(new PromotionRule(200m, 20m));
    act.Should().Throw<InvalidCastException>(
        "因为 Rules 返回 IReadOnlyList<PromotionRule> 的只读包装，无法强转回 List<T>");
}
```

**步骤 2：验证失败** —— 运行 `dotnet test --filter "FullyQualifiedName~Rules_ShouldBeReadOnlyList_CannotMutateExternally"`，期望失败（当前 Rules 类型为 `List<PromotionRule>`，可被强转）。

**步骤 3：实现** —— 修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/PromotionActivity.cs#L28-L33 的 Rules 属性，并修改所有引用 `Rules` 内部集合的方法（`AddRule`/`RemoveRule`/`CalculateDiscount`），使用 backing field：

```csharp
private readonly List<PromotionRule> _rules = new();

/// <summary>
/// 满减规则集合（按门槛升序，只读视图）。外部不可直接修改，须通过 AddRule/RemoveRule 维护不变量。
/// </summary>
public IReadOnlyList<PromotionRule> Rules => _rules.AsReadOnly();
```

同时修改以下方法引用 `_rules` 而非 `Rules`：

```csharp
public void AddRule(decimal thresholdAmount, decimal discountAmount)
{
    var rule = new PromotionRule(thresholdAmount, discountAmount);

    if (_rules.Any(r => r.ThresholdAmount == thresholdAmount))
    {
        throw new PromotionDomainException(
            $"门槛金额 {thresholdAmount} 的规则已存在",
            "PROMOTION_RULE_DUPLICATE");
    }

    _rules.Add(rule);
    _rules.Sort((a, b) => a.ThresholdAmount.CompareTo(b.ThresholdAmount));
}

public void RemoveRule(decimal thresholdAmount)
{
    var rule = _rules.FirstOrDefault(r => r.ThresholdAmount == thresholdAmount);
    if (rule is null)
    {
        throw new PromotionDomainException(
            $"门槛金额 {thresholdAmount} 的规则不存在",
            "PROMOTION_RULE_NOT_FOUND");
    }

    _rules.Remove(rule);
}

public decimal CalculateDiscount(decimal orderAmount)
{
    if (Status != PromotionStatus.Active)
    {
        return 0;
    }

    var now = DateTime.UtcNow;
    if (now < StartTime || now >= EndTime)
    {
        return 0;
    }

    var matched = _rules.LastOrDefault(r => orderAmount >= r.ThresholdAmount);
    return matched?.DiscountAmount ?? 0;
}
```

同步修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Configurations/PromotionActivityConfiguration.cs#L33-L38，告诉 EF Core 使用 backing field：

```csharp
// Rules 满减规则集合序列化为 JSON 列，通过 backing field _rules 访问
builder.Property(a => a.Rules)
    .HasColumnName("rules")
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<PromotionRule>>(v, (JsonSerializerOptions?)null)
             ?? new List<PromotionRule>())
    .Metadata;
// 显式指定 backing field，让 EF Core 反序列化时写入 _rules 而非通过 init setter
var rulesNavigation = builder.Metadata.FindNavigation(nameof(PromotionActivity.Rules));
if (rulesNavigation is not null)
{
    rulesNavigation.SetPropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
}
```

同步修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/PromotionAppService.cs#L48-L56 中的 `activity.Rules.ToList()` 仍可工作（IReadOnlyList 支持 ToList）。

**步骤 4：验证通过** —— 运行 `dotnet test --filter "FullyQualifiedName~PromotionActivityTests"`，所有测试通过；现有 `AddRule_Valid_ShouldAddRule`/`CalculateDiscount_*` 等测试通过（行为不变）。

**步骤 5：提交** —— `git commit -m "fix(promotion): PromotionActivity.Rules 改为 IReadOnlyList 封装不变量 (#2.10)"`

---

### P0-2.11 PromotionGrpcService 直接依赖 ICouponRepository 违反分层

**问题根因**：gRPC 服务同时注入 `IPromotionCalculateAppService`、`ICouponRepository`、`ICouponAppService`，其中 `ICouponRepository` 是领域层仓储接口。`GetCouponInfo` 直接读取 `Coupon` 聚合根并暴露其内部字段，跳过应用层 DTO 转换，等于表现层直接操作领域模型。

**步骤 1：测试** —— 在 file:///workspace/src/Services/Promotion/Leno.Promotion.Api.Tests/PromotionGrpcServiceTests.cs 新增测试：

```csharp
[Fact]
public async Task GetCouponInfo_ShouldCallAppServiceNotRepository()
{
    // 修复后 gRPC 服务应仅调用 ICouponAppService.GetByIdAsync，不再注入 ICouponRepository
    var couponId = Guid.NewGuid();
    var couponDto = new CouponDto
    {
        Id = couponId, Name = "Test", Type = CouponType.FixedAmount, FaceValue = 20m,
        MinSpend = 100m, ValidityType = CouponValidityType.RelativeDays,
        TotalQty = 1000, IssuedQty = 0, Status = CouponTemplateStatus.Enabled,
        CreatedAt = DateTime.UtcNow
    };
    _couponAppServiceMock.Setup(s => s.GetByIdAsync(couponId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(couponDto);

    var svc = new PromotionGrpcService(
        _calculateServiceMock.Object, _couponAppServiceMock.Object, _loggerMock.Object);

    var request = new GetCouponInfoRequest { CouponId = couponId.ToString() };
    var result = await svc.GetCouponInfo(request, TestServerCallContext.Create());

    result.CouponId.Should().Be(couponId.ToString());
    result.Title.Should().Be("Test");
    _couponAppServiceMock.Verify(s => s.GetByIdAsync(couponId, It.IsAny<CancellationToken>()), Times.Once);
}
```

**步骤 2：验证失败** —— 当前测试无法编译通过（构造函数签名包含 `ICouponRepository` 参数）。

**步骤 3：实现** —— 三步并行：

1. 在 file:///workspace/src/Services/Promotion/Leno.Promotion.Application/IAppServices.cs 的 `ICouponAppService` 接口新增 `GetByIdAsync` 方法：

```csharp
/// <summary>
/// 按券模板标识查询详情（gRPC/REST 单条查询用）。
/// </summary>
Task<CouponDto?> GetByIdAsync(Guid couponId, CancellationToken ct = default);
```

2. 在 file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs 实现该方法（紧邻 `RequireCouponAsync` 私有方法后插入）：

```csharp
/// <inheritdoc />
public async Task<CouponDto?> GetByIdAsync(Guid couponId, CancellationToken ct = default)
{
    var coupon = await _couponRepository.GetByIdAsync(couponId, ct);
    return coupon is null ? null : ToDto(coupon);
}
```

3. 修改 file:///workspace/src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs，移除 `ICouponRepository` 依赖，`GetCouponInfo` 改调应用服务：

```csharp
[Authorize]
public sealed class PromotionGrpcService : PromotionInternalService.PromotionInternalServiceBase
{
    private readonly IPromotionCalculateAppService _calculateService;
    private readonly ICouponAppService _couponAppService;
    private readonly ILogger<PromotionGrpcService> _logger;

    public PromotionGrpcService(
        IPromotionCalculateAppService calculateService,
        ICouponAppService couponAppService,
        ILogger<PromotionGrpcService> logger)
    {
        _calculateService = calculateService;
        _couponAppService = couponAppService;
        _logger = logger;
    }

    // CalculateDiscount / LockCoupon / ReleaseCoupons 保持原实现
    // ...

    public override async Task<CouponInfo> GetCouponInfo(GetCouponInfoRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.CouponId, out var couponId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid coupon id: {request.CouponId}"));
        }

        var dto = await _couponAppService.GetByIdAsync(couponId, context.CancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Coupon {request.CouponId} not found"));
        }

        return new CouponInfo
        {
            CouponId = dto.Id.ToString(),
            Title = dto.Name,
            DiscountCents = (long)(dto.FaceValue * 100),
            Status = dto.Status.ToString()
        };
    }
}
```

**步骤 4：验证通过** —— 运行 `dotnet test --filter "FullyQualifiedName~PromotionGrpcServiceTests"`，所有测试通过；`GetCouponInfo_ShouldCallAppServiceNotRepository` 通过。

**步骤 5：提交** —— `git commit -m "fix(promotion): PromotionGrpcService 移除 ICouponRepository 依赖走应用层 (#2.11)"`

---

## P1 修复清单（任务清单格式）

### P1-3.1 PromotionAppService.UpdateAsync 静默忽略 Name 字段

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L219-L224
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/PromotionAppService.cs#L42-L60
- **根因**：注释明示"由于 PromotionActivity 无 UpdateName 方法，保留原 Name 不变仅更新规则"，但 DTO 与 Validator 都要求 Name 非空，调用方误以为已更新。
- **修复步骤**：
  1. 在 `PromotionActivity` 聚合新增 `Rename(string name)` 方法，校验非空后赋值：
     ```csharp
     public void Rename(string name)
     {
         if (string.IsNullOrWhiteSpace(name))
             throw new PromotionDomainException("活动名称不可为空", "PROMOTION_NAME_EMPTY");
         Name = name;
     }
     ```
  2. 在 `PromotionAppService.UpdateAsync` 调用 `activity.Rename(dto.Name)` 在更新规则前。
  3. 删除"保留原 Name 不变"的误导性注释。
- **影响范围**：PromotionAppService.UpdateAsync 调用方（运营端活动编辑 API）。
- **验证方法**：单元测试 `UpdateAsync_ShouldUpdateNameAndRules`，更新后 `activity.Name == dto.Name`。

### P1-3.2 PointsExchangeConsumer 直接调 DbContext.SaveChangesAsync 不走 UnitOfWork

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L226-L231
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/PointsExchangeConsumer.cs#L77-L86
- **根因**：直接 `_dbContext.OutboxMessages.Add(...)` + `await _dbContext.SaveChangesAsync(ct)`，绕过 `IUnitOfWork.SaveEntitiesAsync`，领域事件 `CouponIssuedEvent` 未被 `ClearDomainEvents` 清除，未走 `PromotionIntegrationEventMapper` 翻译。手工写 OutboxMessage 也容易遗漏字段。
- **修复步骤**：
  1. 将构造函数注入的 `PromotionDbContext _dbContext` 替换为 `IUnitOfWork _unitOfWork`。
  2. 删除手工 `_dbContext.OutboxMessages.Add(OutboxMessage.Create(exchangeSucceededEvent))` 代码块。
  3. 在 `UserCoupon.Receive` 工厂或紧随其后由聚合根 `AddDomainEvent(new CouponExchangeSucceededDomainEvent(...))`（已存在 `RecordExchangeSucceeded` 方法），由 `PromotionIntegrationEventMapper` 翻译为 `CouponExchangeSucceededEvent` 经 Outbox 投递。
  4. `await _unitOfWork.SaveEntitiesAsync(ct)` 替换 `_dbContext.SaveChangesAsync`。
  5. 校验 `PromotionIntegrationEventMapper` 已注册 `CouponExchangeSucceededDomainEvent → CouponExchangeSucceededEvent` 映射，缺失则补全。
- **影响范围**：PointsExchangeConsumer、PromotionIntegrationEventMapper、可能需要扩展 `UserCoupon.Receive` 触发领域事件。
- **验证方法**：单元测试 `Consume_ValidCoupon_ShouldPublishEventViaOutbox`，验证 `_unitOfWork.SaveEntitiesAsync` 被调用，Outbox 表存在 `CouponExchangeSucceededEvent` 记录。

### P1-3.3 CouponAppService.ReceiveAsync 将所有 DbUpdateException 误判为"已领取"

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L233-L238
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs#L117-L125
- **根因**：`catch (DbUpdateException)` 直接转 `COUPON_ALREADY_RECEIVED`，但 DbUpdateException 包含连接失败、约束冲突等多种类型，仅唯一索引冲突才是"已领取"，其他错误被误报。
- **修复步骤**：
  1. 新增私有方法 `IsUniqueConstraintViolation(DbUpdateException ex)`，检查 `ex.InnerException` 是否为 `Microsoft.Data.SqlClient.SqlException` 且 `Number == 2627`（唯一约束冲突）或 `2601`（唯一索引冲突）。
  2. 将 catch 改为 `catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))`，仅唯一索引冲突转业务异常；其他 DbUpdateException 重新抛出。
- **影响范围**：CouponAppService.ReceiveAsync 调用方。
- **验证方法**：单元测试覆盖三种场景：唯一索引冲突→业务异常；连接失败→原始异常上抛；普通约束冲突→原始异常上抛。

### P1-3.4 CouponAppService.LockCouponAsync 未处理乐观锁冲突

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L240-L244
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs#L138-L148
- **根因**：两订单并发读同一 Unused 券，都 Lock 成功（内存），Update → SaveEntities 第二个会因 rowversion 冲突抛 `DbUpdateConcurrencyException`，未捕获即 500。
- **修复步骤**：
  1. 在 `LockCouponAsync` 中用 `try { await _unitOfWork.SaveEntitiesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new PromotionDomainException("券已被并发订单锁定，请重试", "USER_COUPON_LOCK_INVALID"); }` 包装。
  2. 或重试一次：捕获后重新加载 UserCoupon，若 Status 已非 Unused 抛业务异常；仍为 Unused 则重试 Lock + Save。
- **影响范围**：CouponAppService.LockCouponAsync 调用方。
- **验证方法**：单元测试 `LockCouponAsync_ConcurrencyConflict_ShouldThrowBusinessException`，mock SaveEntitiesAsync 抛 `DbUpdateConcurrencyException`，期望抛 `PromotionDomainException` 错误码 `USER_COUPON_LOCK_INVALID`。

### P1-3.5 SeckillAppService.ToDtoAsync 在列表查询中循环调用 Redis（N+1）

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L246-L251
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L172-L194
- **根因**：`GetActiveAsync` 与 `QueryAsync` 对每个活动调 `ToDtoAsync`，内部 `await _stockService.GetAvailableAsync(...)` 一次 Redis 往返。10 个活动 10 次串行调用。
- **修复步骤**：
  1. 在 `GetActiveAsync` 中先调 `_stockService.GetAllStocksAsync(activityId, ct)` 批量获取每活动的库存字典（已存在该方法），但 `GetAllStocksAsync` 是单活动维度，需进一步扩展为 `GetAllStocksByActivitiesAsync(IEnumerable<Guid> activityIds)` 一次拉所有活动库存。
  2. 或保持现有接口，但用 `Task.WhenAll` 并行调用 `ToDtoAsync`，将 N 次串行改为 N 次并行（Redis 连接池并发）。
  3. 推荐方案 2 作为短期修复：`var dtoTasks = activities.Select(a => ToDtoAsync(a, ct)); var dtos = (await Task.WhenAll(dtoTasks)).ToList();`
- **影响范围**：SeckillAppService.GetActiveAsync / QueryAsync。
- **验证方法**：单元测试 `GetActiveAsync_MultipleActivities_ShouldQueryRedisInParallel`，验证 Redis 调用次数 = 活动数（并行而非串行累积延迟）。

### P1-3.6 PromotionCalculateAppService 循环内 N+1 查询 Coupon 模板

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L253-L258
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/PromotionCalculateAppService.cs#L99-L118
- **根因**：对每张用户券单独 `await _couponRepository.GetByIdAsync(userCoupon.CouponId, ct)`，N 张券 N 次 DB 往返；未用 AsNoTracking。
- **修复步骤**：
  1. 在 `ICouponRepository` 新增 `GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)` 方法，实现为 `WHERE Id IN (...)` + `AsNoTracking()`。
  2. `CalculateCouponDiscountAsync` 改为一次性加载所有 couponId：`var couponIds = userCoupons.Select(uc => uc.CouponId).Distinct().ToList(); var coupons = await _couponRepository.GetByIdsAsync(couponIds, ct); var couponMap = coupons.ToDictionary(c => c.Id);`
  3. 循环改为 `var coupon = couponMap.GetValueOrDefault(userCoupon.CouponId);` 内存查找。
- **影响范围**：ICouponRepository 接口、EfCoreCouponRepository 实现、PromotionCalculateAppService。
- **验证方法**：单元测试 `CalculateDiscountAsync_MultipleCoupons_ShouldQueryRepoOnce`，验证仓储 `GetByIdsAsync` 仅被调用 1 次。

### P1-3.7 SeckillAppService.CloseActivityWithStockWriteBackAsync 嵌套 SaveEntitiesAsync

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L260-L265
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L80-L89
- **根因**：`activity.Close()` 后调 `await _stockService.WriteBackToDbAsync(activityId, ct)`，而 `RedisSeckillStockService.WriteBackToDbAsync` 内部又调 `await _unitOfWork.SaveEntitiesAsync(ct)`（file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs#L155-L184）。外层 `CloseActivityWithStockWriteBackAsync` 之后又调 `await _unitOfWork.SaveEntitiesAsync(ct)`。两次 SaveEntities 之间无显式事务。
- **修复步骤**：
  1. 用 `await using var tx = await _unitOfWork.BeginTransactionAsync(ct);` 包裹整个流程：`activity.Close()` → `WriteBackToDbAsync` → `tx.CommitAsync`。
  2. 移除外层冗余的 `await _unitOfWork.SaveEntitiesAsync(ct)`（已由 `WriteBackToDbAsync` 内部完成）。
  3. 或将 `WriteBackToDbAsync` 接受 `bool autoSave = true` 参数，外层调用时传 `false`，由外层统一 SaveEntities。
- **影响范围**：SeckillAppService.CloseActivityWithStockWriteBackAsync。
- **验证方法**：单元测试 `CloseActivityWithStockWriteBackAsync_ShouldUseSingleTransaction`，验证 `BeginTransactionAsync` 被调用 1 次，`SaveEntitiesAsync` 被调用 1 次（在事务内）。

### P1-3.8 RedisSeckillStockService.WriteBackToDbAsync 依赖 EF Core Identity Map

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L267-L272
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs#L155-L184
- **根因**：`_repository.GetActiveBySkuIdAsync(skuId, ...)` 查询时，由于外层 `CloseActivityWithStockWriteBackAsync` 已在内存中将 `activity.Status` 改为 `Closed`，依赖 EF Core 的 Identity Map 才能返回同一 tracked 实例（含内存中 Closed 状态），从而 `SyncFromRedis` 修改同一实例。一旦仓储改用 AsNoTracking 或不同 DbContext 实例，逻辑会失败。
- **修复步骤**：
  1. 直接传入 `activityId`，由 `WriteBackToDbAsync` 显式 `_repository.GetByIdAsync(activityId, ct)` 加载聚合（避免按 SkuId 查找跨活动）。
  2. 移除多 SKU 循环（与 P0-2.8 单 SKU 契约一致），直接 `GetAllStocksAsync(activityId)` 返回单活动多 SKU 字典，取 `activity.SkuId` 对应库存。
  3. 修改 `ISeckillStockService.WriteBackToDbAsync` 实现签名不变，但内部不再按 SkuId 查活动。
- **影响范围**：RedisSeckillStockService.WriteBackToDbAsync。
- **验证方法**：单元测试 `WriteBackToDbAsync_ShouldLoadActivityByIdNotBySkuId`，验证 `_repository.GetByIdAsync(activityId)` 被调用，`GetActiveBySkuIdAsync` 不被调用。

### P1-3.9 SeckillPreOccupationCompensationService BatchSize=100 + 30s 间隔大批量回退慢

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L274-L279
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/BackgroundServices/SeckillPreOccupationCompensationService.cs#L18-L20
- **根因**：每 30 秒扫描一批 100 条。1000 条超时记录需 5 分钟清完，期间用户订单可能已确认但补偿仍误回退（与 2.4 叠加）。
- **修复步骤**：
  1. `BatchSize` 由 100 提升至 500：`private const int BatchSize = 500;`
  2. `ScanInterval` 由 30s 改为 10s：`private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(10);`
  3. 注意：此调整需与 2.4 事务内重校验配套，确保大批量下不出现误回退。
- **影响范围**：SeckillPreOccupationCompensationService 调度参数。
- **验证方法**：单元测试验证 `BatchSize` 与 `ScanInterval` 常量值；集成测试观察 1000 条积压在 2 分钟内清完。

### P1-3.10 SeckillPreOccupationRecordConfiguration 表名 PascalCase 与其他 snake_case 不一致

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L281-L285
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Configurations/SeckillPreOccupationRecordConfiguration.cs#L14
- **根因**：`builder.ToTable("SeckillPreOccupationRecords");`，其他表如 `coupons`、`user_coupons`、`seckill_activities`、`promotion_activities` 均为 snake_case。
- **修复步骤**：
  1. 修改为 `builder.ToTable("seckill_pre_occupation_records");`
  2. 同步生成 EF Core 迁移：`dotnet ef migrations add RenameSeckillPreOccupationRecordsTable --project src/Services/Promotion/Leno.Promotion.Infrastructure --startup-project src/Services/Promotion/Leno.Promotion.Api`
  3. 迁移脚本包含 `RenameTable` 操作，回滚脚本包含反向 `RenameTable`。
- **影响范围**：数据库 schema、EF Core 迁移历史、可能影响 DBA 巡检脚本与多数据库迁移工具链。
- **验证方法**：迁移成功应用后查询 `information_schema.tables` 验证表名为 `seckill_pre_occupation_records`。

### P1-3.11 Redis Lua RestoreLuaScript 无上限保护可导致库存超 TotalStock

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L287-L294
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs#L46-L49
- **根因**：`RestoreLuaScript` 仅做 `HINCRBY +qty`，不校验结果是否超过 `TotalStock`。结合 2.3 双重复回退，Redis 库存可无界累加。
- **修复步骤**：
  1. 修改 `RestoreLuaScript` 增加 TotalStock 上限校验，传入 TotalStock 作为 ARGV[3]：
     ```csharp
     private const string RestoreLuaScript = @"
     local cur = tonumber(redis.call('HGET', KEYS[1], ARGV[1]) or '0')
     local total = tonumber(ARGV[3])
     local qty = tonumber(ARGV[2])
     local new = cur + qty
     if new > total then return 1 end
     redis.call('HINCRBY', KEYS[1], ARGV[1], qty)
     return 0";
     ```
  2. 修改 `RestoreAsync` 签名增加 `int totalStock` 参数；调用方（`SeckillOrderCreationFailedEventConsumer` / `SeckillPreOccupationCompensationService` / `SeckillAppService.PlaceOrderAsync` catch 块）传入 `activity.TotalStock`。
  3. `ISeckillStockService.RestoreAsync` 接口同步增加 `int totalStock` 参数。
  4. 返回值 1 表示超出上限，调用方记日志但不抛异常（防回退风暴）。
- **影响范围**：ISeckillStockService 接口、RedisSeckillStockService 实现、所有 RestoreAsync 调用方。
- **验证方法**：单元测试 `RestoreAsync_ExceedTotal_ShouldReturnFailureCode`，mock Redis 验证 Lua 脚本包含上限校验。

### P1-3.12 SeckillPreOccupationRecord.Create 未校验入参合法性

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L296-L300
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillPreOccupationRecord.cs#L49-L67
- **根因**：工厂方法 `Create` 不校验 `activityId/skuId/userId/orderId != Guid.Empty`，也不校验 `quantity > 0`，可创建非法聚合实例。
- **修复步骤**：
  1. 在 `Create` 方法顶部加入完整校验：
     ```csharp
     public static SeckillPreOccupationRecord Create(
         Guid activityId, Guid skuId, Guid userId, Guid orderId, int quantity)
     {
         if (activityId == Guid.Empty)
             throw new PromotionDomainException("ActivityId 不可为空", "PRE_OCCUPATION_ACTIVITY_EMPTY");
         if (skuId == Guid.Empty)
             throw new PromotionDomainException("SkuId 不可为空", "PRE_OCCUPATION_SKU_EMPTY");
         if (userId == Guid.Empty)
             throw new PromotionDomainException("UserId 不可为空", "PRE_OCCUPATION_USER_EMPTY");
         if (orderId == Guid.Empty)
             throw new PromotionDomainException("OrderId 不可为空", "PRE_OCCUPATION_ORDER_EMPTY");
         if (quantity <= 0)
             throw new PromotionDomainException("预占数量须大于 0", "PRE_OCCUPATION_QTY_INVALID");

         return new SeckillPreOccupationRecord(Guid.NewGuid())
         {
             ActivityId = activityId,
             SkuId = skuId,
             UserId = userId,
             OrderId = orderId,
             Quantity = quantity,
             PreOccupiedAt = DateTime.UtcNow,
             IsFulfilled = false,
             IsRolledBack = false
         };
     }
     ```
- **影响范围**：SeckillPreOccupationRecord.Create 调用方（SeckillAppService.PlaceOrderAsync）。
- **验证方法**：单元测试 `Create_EmptyActivityId_ShouldThrowException` / `Create_ZeroQuantity_ShouldThrowException` 等覆盖 5 个入参。

### P1-3.13 UserCoupon.Return 未清空 LockedOrderId

- **审计位置**：file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md#L302-L306
- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/UserCoupon.cs#L90-L107
- **根因**：`Return` 将 `UsedOrderId=null`、`UsedAt=null`，但不清 `LockedOrderId`（在 `Lock` 时设置，`Consume` 未清，`Return` 也未清）。退还到 Unused 后，`LockedOrderId` 仍是旧订单 ID，对账查询 `GetByLockedOrderIdAsync` 仍会查到这张已退还的券。
- **修复步骤**：
  1. 在 `UserCoupon.Return` 方法两个分支（Expired 与 Unused）末尾均加 `LockedOrderId = null;`：
     ```csharp
     public void Return()
     {
         if (Status != CouponStatus.Used)
             throw new PromotionDomainException($"当前状态 {Status} 不可退还，仅 Used 可退还", "USER_COUPON_RETURN_INVALID");
         if (IsExpiredAt(DateTime.UtcNow))
         {
             Status = CouponStatus.Expired;
             UsedOrderId = null;
             UsedAt = null;
             LockedOrderId = null;
             return;
         }
         Status = CouponStatus.Unused;
         UsedOrderId = null;
         UsedAt = null;
         LockedOrderId = null;
     }
     ```
- **影响范围**：UserCoupon.Return 调用方（RefundCompletedEventConsumer）。
- **验证方法**：单元测试 `Return_Valid_ShouldClearLockedOrderId`，退还后 `LockedOrderId` 为 null；现有 `UserCouponReturnTests` 全部通过。

---

## P2 修复清单（任务清单格式，简化）

### P2-4.1 Coupon.Create 允许 totalQty < -1

- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/Coupon.cs#L93-L96
- **修复步骤**：将 `if (totalQty == 0)` 改为 `if (totalQty < -1 || totalQty == 0)`，抛 `COUPON_TOTAL_QTY_INVALID`。
- **验证方法**：单元测试 `Create_NegativeTotalQty_ShouldThrowException` 传入 -2 期望抛异常。

### P2-4.2 Coupon.IssuedQty 累加未防整数溢出

- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/Coupon.cs#L189-L196
- **修复步骤**：将 `IssuedQty += quantity` 改为 `checked { IssuedQty += quantity; }`，或 `if ((long)IssuedQty + quantity > int.MaxValue) throw new PromotionDomainException("发放数量溢出", "COUPON_QTY_OVERFLOW");`。
- **验证方法**：单元测试 `Issue_Overflow_ShouldThrowException` 传入 `int.MaxValue` 边界值。

### P2-4.3 SeckillActivity.RestoreStock 允许 Pending 态回退

- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillActivity.cs#L220-L247
- **修复步骤**：将 `if (Status == SeckillStatus.Closed)` 改为 `if (Status != SeckillStatus.Active && Status != SeckillStatus.Ended)`，抛 `SECKILL_RESTORE_INVALID_STATUS`。
- **验证方法**：单元测试 `RestoreStock_FromPending_ShouldThrowException` 期望抛异常。

### P2-4.4 SeckillActivity.SyncFromRedis 允许 Closed 态同步

- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillActivity.cs#L255-L266
- **修复步骤**：在 `SyncFromRedis` 开头加入 `if (Status == SeckillStatus.Closed) throw new PromotionDomainException("活动已关闭，不可同步库存", "SECKILL_SYNC_CLOSED");`。
- **验证方法**：单元测试 `SyncFromRedis_Closed_ShouldThrowException`。

### P2-4.5 Coupon.ComputeExpiredAt 对 ValidTo 为 null 直接 .Value 引用

- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/Coupon.cs#L244-L249
- **修复步骤**：将 `? ValidTo!.Value` 改为：
  ```csharp
  if (ValidityType == CouponValidityType.FixedPeriod)
  {
      if (ValidTo is null)
          throw new PromotionDomainException("FixedPeriod 券模板 ValidTo 不可为空", "COUPON_VALID_TO_NULL");
      return ValidTo.Value;
  }
  return receivedAt.AddDays(ValidDays!.Value);
  ```
- **验证方法**：单元测试 `ComputeExpiredAt_FixedPeriodWithNullValidTo_ShouldThrowException`（需用反射或 internal 构造绕过 `ValidateValidity` 制造异常数据）。

### P2-4.6 CouponExpiryService 重复调用 UpdateAsync（已 tracked）

- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Api/BackgroundServices/CouponExpiryService.cs#L69-L73
- **修复步骤**：删除 `await userCouponRepository.UpdateAsync(userCoupon, ct);` 行（已 tracked 实体状态自动变 Modified）。注：P0-2.1 修复时已一并删除。
- **验证方法**：单元测试 `ProcessExpiredCouponsAsync_ShouldNotCallUpdateAsync`，验证 `UpdateAsync` 调用次数为 0。

### P2-4.7 PromotionGrpcService.CalculateDiscount 解析 UserId 未抛 RpcException

- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs#L42
- **修复步骤**：将 `UserId = new Guid(request.UserId)` 改为：
  ```csharp
  if (!Guid.TryParse(request.UserId, out var userId))
  {
      throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid user_id: {request.UserId}"));
  }
  ```
  并使用 `userId` 变量。
- **验证方法**：单元测试 `CalculateDiscount_InvalidUserId_ShouldThrowRpcException`。

### P2-4.8 PromotionActivityConfiguration Rules JSON 序列化未指定 options

- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Configurations/PromotionActivityConfiguration.cs#L33-L38
- **修复步骤**：定义静态 `JsonSerializerOptions`：
  ```csharp
  private static readonly JsonSerializerOptions RuleJsonOptions = new()
  {
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
  };
  ```
  将 `JsonSerializer.Serialize(v, (JsonSerializerOptions?)null)` 改为 `JsonSerializer.Serialize(v, RuleJsonOptions)`，反序列化同理。注意：需评估历史数据兼容性，若历史为 PascalCase 需双轨兼容期。
- **验证方法**：单元测试 `Rules_Serialization_ShouldUseSnakeCase`，序列化后 JSON 包含 `threshold_amount`/`discount_amount`。

### P2-4.9 PromotionGrpcService.CalculateDiscount 金额转分精度风险

- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs#L57-L58
- **修复步骤**：将 `(long)(result.TotalDiscountAmount * 100)` 改为显式溢出检查：
  ```csharp
  var cents = result.TotalDiscountAmount * 100m;
  if (cents > long.MaxValue || cents < long.MinValue)
  {
      throw new RpcException(new Status(StatusCode.Internal, "Discount amount overflow"));
  }
  DiscountCents = (long)cents
  ```
- **验证方法**：单元测试 `CalculateDiscount_LargeAmount_ShouldThrowRpcException`，传入 decimal.MaxValue/100。

### P2-4.10 PromotionRule 默认构造与 init 字段并存，弱化不可变性

- **代码位置**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/ValueObjects/PromotionRule.cs#L7-L37
- **修复步骤**：
  1. 保留无参构造（EF Core 反序列化必需），但在 `init` setter 内补校验：
     ```csharp
     private decimal _thresholdAmount;
     public decimal ThresholdAmount
     {
         get => _thresholdAmount;
         init
         {
             if (value < 0) throw new ArgumentException("门槛金额不可为负", nameof(value));
             _thresholdAmount = value;
         }
     }
     ```
     同理 `DiscountAmount` 的 init setter 内校验 `> 0` 且 `<= ThresholdAmount`（注意后者需在 init 后校验，可能需要 `init` 后整体校验或用 `[JsonConstructor]` 走有参构造）。
  2. 推荐方案：去掉无参构造，给有参构造加 `[JsonConstructor]` 特性，让 System.Text.Json 走有参构造（带校验）。EF Core 8+ 也支持 `[JsonConstructor]`。
- **影响范围**：PromotionRule 反序列化路径（PromotionActivityConfiguration JSON 列）。
- **验证方法**：单元测试 `PromotionRule_InvalidViaInit_ShouldThrowException` 验证反序列化非法数据抛异常。

---

## 已修复项（标注 [ALREADY-FIXED]）

| # | 问题标题 | 既有计划编号 | 修复证据位置 |
|---|---------|------------|-------------|
| AF-1 | ReleaseCouponsAsync + LockCoupon/ReleaseCoupons gRPC 改实现（原占位代码） | p0a-T5 | file:///workspace/src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs#L61-L93 —— `LockCoupon` 调用 `_couponAppService.LockCouponAsync`，`ReleaseCoupons` 调用 `_couponAppService.ReleaseCouponsAsync`，无占位实现 |
| AF-2 | 优惠券 Lock 流程贯通 | T3 | file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs#L138-L148 —— `LockCouponAsync` 实现 `GetByUserIdAndCouponIdAsync` + `Lock` + `UpdateAsync` + `SaveEntitiesAsync` 完整流程 |
| AF-3 | 优惠券领取并发安全 | T4 | file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs#L99-L125 —— `ReceiveAsync` 包含 `ExistsAsync` 前置校验 + `DbUpdateException` 后置兜底（唯一索引冲突转 `COUPON_ALREADY_RECEIVED`） |
| AF-4 | 优惠券释放与促销计算显式失败传播 | T11 | file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/UserCoupon.cs#L68-L79 —— `Release` 在 Status 非 Locked 时抛 `USER_COUPON_RELEASE_INVALID`，失败显式上抛；file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs#L151-L165 —— `ReleaseCouponsAsync` 仅处理 `LockedOrderId` 匹配的券，幂等返回 |

---

## 附录：验证矩阵

| 优先级 | 问题数 | 计划周期 | 风险评级 |
|--------|-------|---------|---------|
| P0（必修）| 11（2.1–2.11）| 1 周内 | 🔴 数据一致性破坏/资损/可用性故障 |
| P1（应修）| 13（3.1–3.13）| 1 个月内 | 🟡 边界场景 Bug/性能隐患 |
| P2（建议）| 10（4.1–4.10）| 1 个季度内 | 🟢 代码质量/可维护性 |

**P0 推荐执行顺序**（按资损风险递减）：
1. **2.5 + 2.3 + 2.4**（库存回退状态机相关，资损风险最高）—— 一并修复，引入事务内重校验 + 状态机守卫。
2. **2.1 + 2.2**（过期券扫描缺陷）—— 改 skip=0 + 扫描 Unused/Locked 两态。
3. **2.6**（OrderCancelled 死信）—— 加状态前置检查。
4. **2.9 + 2.8**（秒杀并发幽灵失败 + 多 SKU 一致性）—— 剥离 DB 扣减出热路径 + 移除多 SKU 契约，合并为一个提交。
5. **2.7**（活动激活与 Redis 一致性）—— 调整初始化顺序。
6. **2.10 + 2.11**（DDD 违规）—— 重构暴露方式与分层依赖。

**跨 BC 关联项**（参考 00-summary.md F 章节）：
- P0-15 Promotion SeckillPreOccupation 双重复回退（Promotion #2.3/#2.4）：本计划 P0-2.3 + P0-2.4 + P0-2.5 一并覆盖。
- D4.3 Saga 补偿失败：StockReservationCompensation 与 SeckillPreOccupation 双重回退：本计划 P0-2.3 + P0-2.4 覆盖 Promotion 侧；Order BC 侧由 fix-04-order.md 处理。
- P1-15 Promotion CouponExpiryService 分页 skip 累加（Promotion #2.1）：本计划 P0-2.1 覆盖。
- P1-16 Promotion OrderCancelledEventConsumer 状态机抛错死信（Promotion #2.6）：本计划 P0-2.6 覆盖。

**架构评估关联项**（参考 13-architecture-assessment.md G4/G5）：
- TD1 Outbox 旁路修复（5 个 BC）：本计划 P1-3.2 覆盖 Promotion BC 侧的 `PointsExchangeConsumer`。
- G3.6 跨域事务边界不清：本计划 P0-2.4 + P0-2.9 通过事务内重校验 + 剥离 DB 扣减缓解 Promotion 侧 Saga 半完成状态问题。
