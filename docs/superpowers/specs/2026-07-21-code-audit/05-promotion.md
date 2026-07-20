# Promotion 促销域代码静态分析报告

> 扫描日期：2026-07-21  
> 扫描范围：src/Services/Promotion/Leno.Promotion.{Api,Application,Domain,Infrastructure}/  
> 排除项：Tests 目录、Migrations Designer、ModelSnapshot、Generated

## 1. 概览

- **业务代码行数**：约 3500 行（剔除 Tests/Migrations/Generated 后）
- **问题统计**：🔴 高 11 项 / 🟡 中 13 项 / 🟢 低 10 项
- **风险评级**：🔴 高 = 数据一致性破坏/资损/安全漏洞/可用性故障；🟡 中 = 边界场景 Bug/性能隐患；🟢 低 = 代码质量/可维护性
- **架构合规性**：领域层未引用基础设施层（B1 ✓），仓储接口位于领域层（B3 ✓），但存在聚合根直接暴露给表现层（B4 ✗）、聚合内部 List 被外部可变（B6 ✗）等违规。
- **关键风险点**：秒杀 Redis↔DB 一致性、CouponExpiryService 分页扫描缺陷、SeckillPreOccupationRecord 双重复回退导致库存膨胀、OrderCancelledEventConsumer 状态机抛错死信。

## 2. 🔴 高风险问题

### 2.1 CouponExpiryService 分页 skip 累加导致漏处理过期券
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Api/BackgroundServices/CouponExpiryService.cs#L57-L80
- **类别**：A1 / A4 / C2
- **现象**：扫描循环 `skip += BatchSize`，但 `GetExpiredUnusedCouponsAsync` 的 WHERE 条件是 `Status == Unused`（详见 file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Repositories/EfCoreUserCouponRepository.cs#L76-L88）。第一批 500 张被 `Expire()` 后状态变为 `Expired`，下次查询时它们已不在结果集；再做 `Skip(500)` 实际跳过的是当前结果集的前 500 条（即原 501-1000 号记录），导致这 500 张过期券永远不被处理。
- **影响**：大批量过期券长期滞留 Unused 状态，虽然 `UserCoupon.Lock` 内部有 `EnsureNotExpired` 兜底（不会真的让过期券被使用），但 `IssuedQty` 与状态字段长期不一致，影响运营报表与对账；活动模板的"已发放量"长期偏大。
- **修复建议**：
  ```csharp
  // 方案 1：始终 skip=0，依赖状态过滤淘汰已处理记录
  var skip = 0;
  while (!ct.IsCancellationRequested)
  {
      var batch = await userCouponRepository.GetExpiredUnusedCouponsAsync(0, BatchSize, ct);
      if (batch.Count == 0) break;
      // ... Expire + Save
      // 不再 skip += BatchSize
  }

  // 方案 2：基于 ExpiredAt 游标翻页，避免 offset 跳页
  DateTime? cursor = null;
  while (!ct.IsCancellationRequested)
  {
      var batch = await userCouponRepository.GetExpiredUnusedCouponsAsync(cursor, BatchSize, ct);
      if (batch.Count == 0) break;
      cursor = batch[^1].ExpiredAt;
      // ...
  }
  ```

### 2.2 CouponExpiryService 仅扫描 Unused，遗漏 Locked+Expired 券
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Repositories/EfCoreUserCouponRepository.cs#L82-L83
- **类别**：A2 / A4
- **现象**：查询过滤条件 `Status == CouponStatus.Unused`，但 `UserCoupon.Expire`（file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/UserCoupon.cs#L81-L88）允许从 `Locked` 状态转为 `Expired`。若订单被长时间挂起（既未支付也未取消），券一直处于 `Locked` 且已过期，过期扫描永远不会触及它，被永久占位。
- **影响**：用户过期券无法回收，影响发放量统计；后续若订单终于取消，`Release()` 会再次走 `Expired` 分支（file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/UserCoupon.cs#L73-L79），但中间窗口期数据不一致。
- **修复建议**：将查询条件改为 `Status == Unused || Status == Locked`，扫描两类过期券。

### 2.3 SeckillOrderCreationFailedEventConsumer 与 SeckillPreOccupationCompensationService 双重复回退导致库存膨胀
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/SeckillOrderEventConsumer.cs#L43-L69
- **类别**：A3 / A4
- **现象**：失败事件消费者在 `record.MarkRolledBack()` 后**无条件**继续执行：
  ```csharp
  if (record is not null && !record.IsRolledBack) { record.MarkRolledBack(); }
  await _stockService.RestoreAsync(...);      // 始终执行
  if (activity is not null) { activity.RestoreStock(...); }   // 始终执行
  ```
  若 `SeckillPreOccupationCompensationService`（file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/BackgroundServices/SeckillPreOccupationCompensationService.cs#L77-L102）先扫描到该超时记录并已调用 `RestoreAsync`+`RestoreStock`+`MarkRolledBack`，那么失败事件到达时 `record.IsRolledBack == true`，但代码仅跳过 `MarkRolledBack`，**仍重复** Restore Redis 与 DB 库存。
- **影响**：同一份预占库存被回退两次，Redis 与 DB 库存均超出 `TotalStock` 上限（`RestoreStock` 仅以 `TotalStock` 为上限保护 DB；Redis 无任何上限保护，可无限累加）。秒杀高并发场景下导致超卖或库存数字失真，资损风险。
- **修复建议**：
  ```csharp
  var record = await _preOccupationRecordRepository.GetByOrderIdAsync(integrationEvent.OrderId, ct);
  if (record is null)
  {
      Logger.LogWarning("未找到预占记录 OrderId={OrderId}", integrationEvent.OrderId);
      return;
  }
  if (record.IsRolledBack)
  {
      Logger.LogInformation("预占记录已回退 OrderId={OrderId}，跳过", integrationEvent.OrderId);
      return;
  }
  record.MarkRolledBack();
  await _stockService.RestoreAsync(integrationEvent.ActivityId, integrationEvent.SkuId, integrationEvent.Quantity, ct);
  var activity = await _activityRepository.GetByIdAsync(integrationEvent.ActivityId, ct);
  if (activity is not null) { activity.RestoreStock(integrationEvent.Quantity); }
  await _unitOfWork.SaveEntitiesAsync(ct);
  ```

### 2.4 SeckillPreOccupationCompensationService TOCTOU：补偿与履约竞态导致错误回退
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/BackgroundServices/SeckillPreOccupationCompensationService.cs#L77-L102
- **类别**：A3 / A4
- **现象**：补偿服务读取未履约记录后，在 `RestoreAsync`/`RestoreStock`/`MarkRolledBack` 之间未做任何锁或状态再校验。若在读取与回退之间，订单域刚发布 `SeckillOrderConfirmedIntegrationEvent`，`SeckillOrderConfirmedEventConsumer` 已将 `IsFulfilled=true` 持久化，则补偿仍会回退库存，使 `IsFulfilled=true && IsRolledBack=true` 的非法并存状态出现，且库存被错误回退。
- **影响**：用户已下单成功，却被补偿任务把库存退回，下一笔秒杀可重复下单同一份库存，造成超卖/资损；记录状态字段自相矛盾。
- **修复建议**：补偿逻辑应在事务内重新加载记录并校验状态：
  ```csharp
  using var tx = await unitOfWork.BeginTransactionAsync(ct);
  var fresh = await recordRepository.GetByIdAsync(record.Id, ct);
  if (fresh is null || fresh.IsFulfilled || fresh.IsRolledBack)
  {
      Logger.LogInformation("记录已变更 OrderId={OrderId}，跳过补偿", record.OrderId);
      return;
  }
  await stockService.RestoreAsync(fresh.ActivityId, fresh.SkuId, fresh.Quantity, ct);
  var activity = await activityRepository.GetByIdAsync(fresh.ActivityId, ct);
  activity?.RestoreStock(fresh.Quantity);
  fresh.MarkRolledBack();
  await unitOfWork.SaveEntitiesAsync(ct);
  await tx.CommitAsync(ct);
  ```

### 2.5 SeckillPreOccupationRecord.MarkRolledBack 不校验 IsFulfilled 导致状态机不一致
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillPreOccupationRecord.cs#L82-L91
- **类别**：A2
- **现象**：`MarkRolledBack` 仅幂等检查 `IsRolledBack`，不阻止"已履约再回退"。结合 2.4，可能产生 `IsFulfilled=true && IsRolledBack=true` 的非法并存状态，破坏聚合不变量。
- **影响**：领域模型允许非法状态，下游查询/审计无法判断该预占究竟是履约还是回退。
- **修复建议**：
  ```csharp
  public void MarkRolledBack()
  {
      if (IsRolledBack) return;
      if (IsFulfilled)
          throw new PromotionDomainException("已履约的预占记录不可回退", "PRE_OCCUPATION_FULFILLED");
      IsRolledBack = true;
      RolledBackAt = DateTime.UtcNow;
  }
  ```

### 2.6 OrderCancelledEventConsumer 在券已核销时 Release 抛错导致死信
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/OrderEventConsumer.cs#L83-L99
- **类别**：A2 / A7
- **现象**：`OrderCancelledEventConsumer` 直接 `userCoupon.Release()`，但 `UserCoupon.Release`（file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/UserCoupon.cs#L68-L79）要求 `Status == Locked`。若订单已先经过 `OrderPaidEventConsumer`（券 Locked→Used）后再被取消（业务上不常见但可能发生，例如退款流程触发取消事件），`Release()` 会抛 `USER_COUPON_RELEASE_INVALID`，异常上抛导致 MassTransit 重试耗尽进入死信。
- **影响**：取消事件被死信，订单状态机推进受阻；IdempotencyStore 未标记已处理，重试循环持续报错。
- **修复建议**：消费端先校验状态：
  ```csharp
  if (userCoupon.Status == CouponStatus.Used)
  {
      Logger.LogInformation("订单 {OrderId} 的券已核销，跳过 Release（应由 RefundCompleted 退还）", integrationEvent.OrderId);
      return;
  }
  if (userCoupon.Status != CouponStatus.Locked)
  {
      Logger.LogInformation("订单 {OrderId} 券状态 {Status} 非 Locked，幂等跳过", integrationEvent.OrderId, userCoupon.Status);
      return;
  }
  userCoupon.Release();
  ```

### 2.7 SeckillAppService.ActivateAsync Redis 初始化失败但 DB 仍标记 Active
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L56-L69
- **类别**：A4 / A5
- **现象**：调用顺序为 `activity.Activate()` → `await _stockService.InitializeAsync(...)` → `await _unitOfWork.SaveEntitiesAsync(ct)`。`activity.Activate()` 在内存中将 `Status` 改为 `Active`，但若 Redis 初始化抛异常（Redis 不可用、网络故障、连接被重置），catch 不会回滚聚合状态，异常向上抛出。然而 `_unitOfWork.SaveEntitiesAsync` 尚未执行，DB 中状态仍为 Pending，看似没问题。**实际隐患**：若 Redis `InitializeAsync` 内部由于 Hash 字段已存在而静默覆盖（`HashSetAsync` 会覆盖），则老库存被重置，正在进行的秒杀库存被清零；反之若 Redis 异常半成功（部分字段已写入），后续 `PlaceOrder` 会用错误的库存数据。
- **影响**：Redis 故障期间活动无法激活；若 Redis 部分成功，库存基线丢失；无补偿逻辑。
- **修复建议**：
  ```csharp
  // 先初始化 Redis，成功后再改聚合状态
  var skuStocks = new Dictionary<Guid, int> { { activity.SkuId, activity.TotalStock } };
  try
  {
      await _stockService.InitializeAsync(activity.Id, skuStocks, ct);
  }
  catch (Exception ex)
  {
      throw new PromotionDomainException($"秒杀活动 {activityId} Redis 库存初始化失败：{ex.Message}", "SECKILL_REDIS_INIT_FAILED", ex);
  }
  activity.Activate();
  await _unitOfWork.SaveEntitiesAsync(ct);
  ```
  并在 `InitializeAsync` 中使用 `HSETNX` 或先 `DEL` 后 `HSET` 保证幂等性。

### 2.8 SeckillAppService.PlaceOrderAsync 多 SKU 路径下 DB DeductStock 与 Redis 不一致
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L110-L145
- **类别**：A1 / A3
- **现象**：`var skuId = dto.SkuId != Guid.Empty ? dto.SkuId : activity.SkuId;` Redis 用此 `skuId` 扣减，但 `activity.DeductStock(userId, dto.Quantity)`（file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillActivity.cs#L164-L192）只接受 `quantity`，**不接受 skuId**，仅扣减聚合的单一 `AvailableStock` 字段。`SeckillActivity` 聚合本身只有一个 `SkuId`（file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillActivity.cs#L19-L20），而 `ISeckillStockService` 的接口注释却标榜"支持多 SKU"。
- **影响**：若买家传非默认 SkuId，Redis Hash 中查不到该 field，返回 1（库存不足）会失败（看似安全）；但若运营通过其他路径初始化了多 SKU，则 Redis 扣减成功，DB 扣减却作用在聚合错误的单一字段上，DB↔Redis 基线永久错位。
- **修复建议**：要么去除多 SKU 支持（接口与实现统一为单 SKU）；要么为 `SeckillActivity` 增加 `Dictionary<Guid, int> AvailableStocks` 字段，`DeductStock(skuId, quantity)` 按 sku 扣减。在文件 file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Services/ISeckillStockService.cs#L11-L16 与 file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L112-L113 之间需对齐契约。

### 2.9 SeckillAppService.PlaceOrderAsync 中 DB 乐观锁冲突引发"幽灵失败"
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L131-L152
- **类别**：A3 / C5
- **现象**：`activity.DeductStock` 在内存中扣减，`SaveEntitiesAsync` 经 rowversion 乐观锁提交。高并发下 N 个请求同时通过 Redis Lua 原子扣减（成功），但 DB 提交只能一个个串行；除第一个外，其余均因 rowversion 不匹配抛 `DbUpdateConcurrencyException`，被 `catch` 捕获后回退 Redis，最终用户拿到失败错误。这违背了秒杀"高并发原子扣减"的设计目标。
- **影响**：秒杀高峰期大量用户 Redis 成功但 DB 失败，被强制回退 Redis 后重试，QPS 折损、用户体验差。Redis 与 DB 之间没有原子性保证。
- **修复建议**：将 DB 基线同步从热路径剥离，改为异步对账：
  ```csharp
  // 热路径：仅 Redis 扣减 + 创建预占记录 + 发事件
  var deductResult = await _stockService.TryDeductAsync(...);
  if (deductResult != 0) throw ...;
  var preOccupationRecord = SeckillPreOccupationRecord.Create(...);
  await _preOccupationRecordRepository.AddAsync(preOccupationRecord, ct);
  // 不调用 activity.DeductStock；活动聚合 AvailableStock 由后台任务/活动结束时 SyncFromRedis 同步
  await _unitOfWork.SaveEntitiesAsync(ct);  // 仅写预占记录 + 发件箱事件
  ```
  或者改为基于预占记录的最终一致对账，DB 不参与扣减热路径。

### 2.10 PromotionActivity.Rules 直接暴露 List 违反 DDD 不变量封装
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/PromotionActivity.cs#L32
- **类别**：B6 / A1
- **现象**：`public List<PromotionRule> Rules { get; private set; } = new();` 公开 `List<>` 引用，外部代码可绕过 `AddRule`/`RemoveRule` 直接 `activity.Rules.Add(...)` 或 `activity.Rules.Clear()`，破坏"按门槛升序、不可重复门槛"不变量。`PromotionAppService.UpdateAsync`（file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/PromotionAppService.cs#L48-L56）虽通过 `RemoveRule`/`AddRule` 操作，但任何调用方（包括 EF Core 反序列化后）都可绕过。
- **影响**：聚合不变量可在任意层被绕过，无法保证数据一致性；测试与生产可能出现重复门槛或乱序规则。
- **修复建议**：暴露 `IReadOnlyList<PromotionRule>`：
  ```csharp
  private readonly List<PromotionRule> _rules = new();
  public IReadOnlyList<PromotionRule> Rules => _rules.AsReadOnly();
  // EF Core 用 backing field：builder.Property(a => a.Rules).HasField("_rules")...
  ```

### 2.11 PromotionGrpcService 直接依赖 ICouponRepository 违反分层
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs#L19-L33
- **类别**：B4 / B2
- **现象**：gRPC 服务同时注入 `IPromotionCalculateAppService`、`ICouponRepository`、`ICouponAppService`，其中 `ICouponRepository` 是领域层仓储接口。`GetCouponInfo` 直接读取 `Coupon` 聚合根并暴露其内部字段（FaceValue、Name、Status），跳过了应用层的 DTO 转换，等于表现层直接操作领域模型。
- **影响**：DDD 分层被破坏，领域模型变更直接影响 gRPC 契约；无法在应用层统一加日志、鉴权、缓存；ICouponRepository 任何签名变更都会波及 gRPC 服务。
- **修复建议**：在 `ICouponAppService` 中新增 `GetByIdAsync(Guid)`（已存在 `QueryAsync`，但单条查询缺失），gRPC 服务仅调应用服务：
  ```csharp
  public override async Task<CouponInfo> GetCouponInfo(GetCouponInfoRequest request, ServerCallContext context)
  {
      if (!Guid.TryParse(request.CouponId, out var couponId))
          throw new RpcException(new Status(StatusCode.InvalidArgument, ...));
      var dto = await _couponAppService.GetByIdAsync(couponId, context.CancellationToken)
          ?? throw new RpcException(new Status(StatusCode.NotFound, ...));
      return new CouponInfo { CouponId = dto.Id.ToString(), Title = dto.Name, ... };
  }
  ```

## 3. 🟡 中风险问题

### 3.1 PromotionAppService.UpdateAsync 静默忽略 Name 字段
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/PromotionAppService.cs#L42-L60
- **类别**：A1 / B2
- **现象**：注释明确写"由于 PromotionActivity 无 UpdateName 方法，保留原 Name 不变仅更新规则"，但 `UpdatePromotionActivityDto` 与 `UpdatePromotionActivityDtoValidator`（file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Validators/PromotionValidators.cs#L27-L33）均要求 `Name` 非空，调用方误以为 Name 已更新，实则被丢弃。
- **影响**：运营修改活动名称后无任何反馈，需查询才发现未生效；DTO 与领域行为不一致。
- **修复建议**：在 `PromotionActivity` 增加 `Rename(string name)` 方法，并在 `UpdateAsync` 调用之。

### 3.2 PointsExchangeConsumer 直接调 DbContext.SaveChangesAsync 不走 UnitOfWork，CouponIssuedEvent 未派发
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/PointsExchangeConsumer.cs#L77-L86
- **类别**：A4 / B2
- **现象**：直接 `_dbContext.OutboxMessages.Add(...)` + `await _dbContext.SaveChangesAsync(ct)`，绕过 `IUnitOfWork.SaveEntitiesAsync`。`UserCoupon.Receive` 在聚合内 `AddDomainEvent(new CouponIssuedEvent(...))`（file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/UserCoupon.cs#L40），由于 `SaveChangesWithOutboxAsync` 未被调用，领域事件未被 `ClearDomainEvents` 清除，也未经 `PromotionIntegrationEventMapper` 翻译。虽然 `CouponIssuedEvent` 当前无 mapper 注册（无下游集成事件），但与 `CouponAppService.ReceiveAsync` 的处理路径不一致，将来若新增 mapper 会出现"通过 AppService 走流程的领券会发事件，通过积分兑换的不会"。
- **影响**：架构不一致，未来扩展易踩坑；DbContext 与 UnitOfWork 混用易导致事务边界模糊。
- **修复建议**：统一使用 `_unitOfWork.SaveEntitiesAsync(ct)`，并删除手工写 OutboxMessage 的逻辑（由 mapper 自动翻译）。

### 3.3 CouponAppService.ReceiveAsync 将所有 DbUpdateException 误判为"已领取"
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs#L117-L125
- **类别**：A7
- **现象**：`catch (DbUpdateException)` 直接转 `COUPON_ALREADY_RECEIVED`。`DbUpdateException` 是 EF Core 异常基类，包含数据库连接失败、约束冲突、唯一索引冲突等。唯一索引冲突（(UserId, CouponId)）才是"已领取"，其他错误被误报为"已领取"会让用户重复点击。
- **影响**：数据库故障期间所有用户都被误导为"已领取"，掩盖真实故障。
- **修复建议**：检查 `ex.InnerException` 或 `ex.Entries`，仅唯一索引冲突转业务异常：
  ```csharp
  catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
  {
      throw new PromotionDomainException("已领取过该优惠券，不可重复领取", "COUPON_ALREADY_RECEIVED");
  }
  ```

### 3.4 CouponAppService.LockCouponAsync 未处理乐观锁冲突
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs#L138-L148
- **类别**：A3 / A7
- **现象**：两个并发订单锁定同一券时，第一个 SaveEntities 成功（LockedOrderId=A），第二个 `Lock` 调用本身会抛 `USER_COUPON_LOCK_INVALID`（Status != Unused），逻辑正确。但若两订单并发读同一 Unused 券、都 Lock 成功、都 Update → SaveEntities，第二个会因 rowversion 冲突抛 `DbUpdateConcurrencyException`，未捕获即 500。
- **影响**：高并发下用户体验差，错误码不友好。
- **修复建议**：捕获 `DbUpdateConcurrencyException` 转 `USER_COUPON_LOCK_INVALID`，或重试一次重新加载。

### 3.5 SeckillAppService.ToDtoAsync 在列表查询中循环调用 Redis（N+1）
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L172-L194
- **类别**：C3 / C2
- **现象**：`GetActiveAsync` 与 `QueryAsync` 对每个活动调 `ToDtoAsync`，内部 `await _stockService.GetAvailableAsync(...)` 一次 Redis 往返。10 个活动 10 次 Redis 调用，串行。
- **影响**：活动列表接口延迟随活动数线性增长；Redis 高负载时被放大。
- **修复建议**：用 `GetAllStocksAsync` 一次性拉所有活动库存（或 Pipeline/ batching），DTO 构建时查内存字典。

### 3.6 PromotionCalculateAppService 在循环内 N+1 查询 Coupon 模板
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/PromotionCalculateAppService.cs#L99-L118
- **类别**：C1 / C3
- **现象**：对每张用户券单独 `await _couponRepository.GetByIdAsync(userCoupon.CouponId, ct)`，N 张券 N 次 DB 往返；且未用 AsNoTracking。
- **影响**：试算接口延迟高，DbContext 跟踪实体导致内存占用增加。
- **修复建议**：增加 `GetByIdsAsync(IEnumerable<Guid> ids)` 仓储方法，一次性 `WHERE Id IN (...)` 加载；查询只读使用 `AsNoTracking()`。

### 3.7 SeckillAppService.CloseActivityWithStockWriteBackAsync 嵌套 SaveEntitiesAsync
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Application/Services/SeckillAppService.cs#L80-L89
- **类别**：A4 / C5
- **现象**：`activity.Close()` 后调 `await _stockService.WriteBackToDbAsync(activityId, ct)`，而 `RedisSeckillStockService.WriteBackToDbAsync` 内部又调 `await _unitOfWork.SaveEntitiesAsync(ct)`（file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs#L155-L184）。外层 `CloseActivityWithStockWriteBackAsync` 之后又调 `await _unitOfWork.SaveEntitiesAsync(ct)`。两次 SaveEntities 之间无显式事务，第一次提交后第二次无变更可保存（看似无害），但聚合 Version 字段已变化，若中间有其他并发修改，第二次保存可能失败。
- **影响**：事务边界不清晰，难以推理一致性；并发场景易踩坑。
- **修复建议**：用 `BeginTransactionAsync` 包裹整个流程，或让 `WriteBackToDbAsync` 接受"是否提交"参数。

### 3.8 RedisSeckillStockService.WriteBackToDbAsync 依赖 EF Core Identity Map，脆弱
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs#L159-L184
- **类别**：A5 / C5
- **现象**：`_repository.GetActiveBySkuIdAsync(skuId, ...)` 查询时，由于 `SeckillAppService.CloseActivityWithStockWriteBackAsync` 已在内存中将 `activity.Status` 改为 `Closed`，依赖 EF Core 的 Identity Map 才能返回同一 tracked 实例（含内存中 Closed 状态），从而 `SyncFromRedis` 修改同一实例。一旦仓储改用 `AsNoTracking` 或不同 DbContext 实例，逻辑会失败（拿到 Status=Active 的快照，与外层 activity 不同实例，SyncFromRedis 修改无效果）。
- **影响**：依赖隐式行为，重构易破坏；AsNoTracking 改造时静默失败。
- **修复建议**：直接传入 `activityId`，由 `WriteBackToDbAsync` 显式 `_repository.GetByIdAsync(activityId, ct)` 加载聚合（避免按 SkuId 查找跨活动）。

### 3.9 SeckillPreOccupationCompensationService BatchSize=100 + 30s 间隔，大批量回退慢
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/BackgroundServices/SeckillPreOccupationCompensationService.cs#L18-L20
- **类别**：C8
- **现象**：每 30 秒扫描一批 100 条。若 Redis 抖动导致 1000 条超时记录积压，需 30s * 10 = 5 分钟才能清完。期间用户订单可能已确认但补偿仍误回退（与 2.4 叠加）。
- **影响**：积压期间数据不一致窗口长。
- **修复建议**：BatchSize 提升至 500-1000，扫描间隔改为 10s；或在批量内做"先锁记录（SELECT FOR UPDATE）后处理"。

### 3.10 SeckillPreOccupationRecordConfiguration 表名 PascalCase 与其他 snake_case 不一致
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Configurations/SeckillPreOccupationRecordConfiguration.cs#L14
- **类别**：B8
- **现象**：`builder.ToTable("SeckillPreOccupationRecords");`，其他表如 `coupons`、`user_coupons`、`seckill_activities`、`promotion_activities` 均为 snake_case。
- **影响**：DBA 巡检困惑；多数据库迁移工具链（如 dbmate）按命名规范识别时易遗漏。
- **修复建议**：统一为 `"seckill_pre_occupation_records"`。

### 3.11 Redis Lua RestoreLuaScript 无上限保护，可导致 Redis 库存超 TotalStock
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs#L46-L49
- **类别**：A1 / A3
- **现象**：`RestoreLuaScript` 仅做 `HINCRBY +qty`，不校验结果是否超过 `TotalStock`。结合 2.3 双重复回退，Redis 库存可无界累加。即使不考虑双回退，单次错误的 Restore（如补偿误触发）也会让 Redis 库存超过 TotalStock。
- **影响**：Redis 库存失真，后续秒杀可超卖。
- **修复建议**：Lua 脚本增加上限校验，传入 TotalStock 作为 ARGV：
  ```lua
  local cur = tonumber(redis.call('HGET', KEYS[1], ARGV[1]) or '0')
  local total = tonumber(ARGV[3])
  local new = cur + tonumber(ARGV[2])
  if new > total then return 1 end
  redis.call('HINCRBY', KEYS[1], ARGV[1], ARGV[2])
  return 0
  ```

### 3.12 SeckillPreOccupationRecord.Create 未校验入参合法性
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillPreOccupationRecord.cs#L49-L67
- **类别**：A1 / A6
- **现象**：工厂方法 `Create` 不校验 `activityId/skuId/userId/orderId != Guid.Empty`，也不校验 `quantity > 0`，可创建非法聚合实例（如 quantity=0 或全 Guid.Empty）。
- **影响**：脏数据可能进入补偿扫描，浪费资源；orderId=Guid.Empty 时唯一索引形同虚设。
- **修复建议**：在 `Create` 中加入完整的入参校验并抛 `PromotionDomainException`。

### 3.13 UserCoupon.Return 未清空 LockedOrderId
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/UserCoupon.cs#L90-L107
- **类别**：A1 / A6
- **现象**：`Return` 将 `UsedOrderId=null`、`UsedAt=null`，但不清 `LockedOrderId`（在 `Lock` 时设置，`Consume` 未清，`Return` 也未清）。退还到 Unused 后，`LockedOrderId` 仍是旧订单 ID，对账查询 `GetByLockedOrderIdAsync` 仍会查到这张已退还的券。
- **影响**：数据污染；`OrderCancelledEventConsumer` 退款后再次查询可能误命中。
- **修复建议**：`Return` 中加 `LockedOrderId = null;`。

## 4. 🟢 低风险问题

### 4.1 Coupon.Create 允许 totalQty < -1
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/Coupon.cs#L93-L96
- **类别**：A6
- **现象**：仅校验 `totalQty == 0`，对 -2、-5 等负值不报错。后续 `TotalQty > 0 && IssuedQty + quantity > TotalQty` 永远为 false，等同不限量，但语义模糊。
- **修复建议**：`if (totalQty < -1 || totalQty == 0) throw ...`。

### 4.2 Coupon.IssuedQty 累加未防整数溢出
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/Coupon.cs#L189-L196
- **类别**：A6
- **现象**：`IssuedQty + quantity` 可能 int 溢出（虽然不太现实）。
- **修复建议**：用 `checked` 块或 `long` 校验。

### 4.3 SeckillActivity.RestoreStock 允许 Pending 态回退
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillActivity.cs#L220-L247
- **类别**：A2
- **现象**：仅阻止 `Closed`，允许 `Pending` 态回退。Pending 活动从未激活过、Redis 无库存，回退无意义。
- **修复建议**：仅 `Active` 或 `Ended` 态可回退。

### 4.4 SeckillActivity.SyncFromRedis 允许 Closed 态同步
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/SeckillActivity.cs#L255-L266
- **类别**：A2
- **现象**：不校验 Status，对 Closed 终态活动仍可修改 AvailableStock。
- **修复建议**：Closed 态抛异常。

### 4.5 Coupon.ComputeExpiredAt 对 ValidTo 为 null 直接 .Value 引用
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/Aggregates/Coupon.cs#L244-L249
- **类别**：A6
- **现象**：FixedPeriod 分支 `ValidTo!.Value`。虽然 `ValidateValidity` 保证 FixedPeriod 必有 ValidTo，但若 EF Core 反序列化时数据被破坏（ValidTo 为 null 但 ValidityType=FixedPeriod），运行时 NRE。
- **修复建议**：增加防御性 `if (ValidTo is null) throw`。

### 4.6 CouponExpiryService 重复调用 UpdateAsync（已 tracked）
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Api/BackgroundServices/CouponExpiryService.cs#L69-L73
- **类别**：C1
- **现象**：从仓储查出的实体已被 DbContext 跟踪，调 `Expire()` 后状态自动变 Modified，再调 `UpdateAsync` 是冗余操作。
- **修复建议**：删除 `UpdateAsync` 调用。

### 4.7 PromotionGrpcService.CalculateDiscount 解析 UserId 未抛 RpcException
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs#L42
- **类别**：A6
- **现象**：`new Guid(request.UserId)` 对无效字符串返回 `Guid.Empty` 而非抛异常；后续 `PromotionCalculateAppService` 抛 `ArgumentException`，gRPC 拦截器可能转为 Unknown 状态。
- **修复建议**：用 `Guid.TryParse`，失败抛 `RpcException(StatusCode.InvalidArgument)`，与其他方法一致。

### 4.8 PromotionActivityConfiguration Rules JSON 序列化未指定 options
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Infrastructure/Configurations/PromotionActivityConfiguration.cs#L33-L38
- **类别**：A6 / B8
- **现象**：`JsonSerializer.Serialize(v, (JsonSerializerOptions?)null)` 默认行为是 PascalCase 输出，与 DB 列存储约定（snake_case）不一致；反序列化时若数据库历史数据为 snake_case，属性不匹配，得到空 List（默认值）。
- **修复建议**：统一使用 `JsonSerializerOptions` 配置 `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`。

### 4.9 PromotionGrpcService.CalculateDiscount 金额转分精度风险
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs#L57-L58
- **类别**：A6
- **现象**：`(long)(result.TotalDiscountAmount * 100)` 浮点乘法虽 decimal 精度足够，但若 TotalDiscountAmount 极大（如 decimal.MaxValue / 100），转换溢出未检查。
- **修复建议**：用 `decimal.ToInt64` 风格的显式溢出检查。

### 4.10 PromotionRule 默认构造与 init 字段并存，弱化不可变性
- **文件**：file:///workspace/src/Services/Promotion/Leno.Promotion.Domain/ValueObjects/PromotionRule.cs#L7-L37
- **类别**：B5
- **现象**：`record` 提供 `init` 属性和无参构造，外部仍可在反序列化时通过 init 任意设置无校验的值（绕过有参构造的校验）。EF Core JSON 反序列化正是走无参 + init 路径。
- **修复建议**：去掉无参构造，让 EF Core 通过 `[JsonConstructor]` 走有参构造；或在 init setter 内补校验。

## 5. 修复路线建议

| 优先级 | 问题数 | 建议周期 |
|-|-|-|
| P0（必修）| 11（2.1–2.11）| 1 周内 |
| P1（应修）| 13（3.1–3.13）| 1 个月内 |
| P2（建议）| 10（4.1–4.10）| 1 个季度内 |

**P0 优先级排序建议**：
1. 2.3 + 2.4 + 2.5（库存回退相关，资损风险最高）— 一并修复，需引入事务内重校验 + 状态机守卫。
2. 2.1 + 2.2（过期券扫描缺陷）— 改 skip=0 或游标分页 + 扫描 Unused/Locked 两态。
3. 2.6（OrderCancelled 死信）— 加状态前置检查。
4. 2.9（秒杀并发幽灵失败）— 评估是否剥离 DB 扣减出热路径。
5. 2.7 + 2.8（活动激活与多 SKU 一致性）— 调整初始化顺序，对齐契约。
6. 2.10 + 2.11（DDD 违规）— 重构暴露方式。

## 6. 附录：扫描覆盖的关键文件

### 领域层（Leno.Promotion.Domain）
- Aggregates/Coupon.cs
- Aggregates/UserCoupon.cs
- Aggregates/SeckillActivity.cs
- Aggregates/SeckillPreOccupationRecord.cs
- Aggregates/PromotionActivity.cs
- ValueObjects/{CouponEnums,PromotionEnums,SeckillEnums,PromotionRule}.cs
- Repositories/{ICouponRepository,IUserCouponRepository,ISeckillActivityRepository,ISeckillPreOccupationRecordRepository,IPromotionActivityRepository}.cs
- Services/{IPromotionQueryService,ISeckillStockService}.cs
- Events/{CouponIssuedEvent,CouponExchangeSucceededDomainEvent,SeckillOrderCreatedEvent,SeckillOrderConfirmedEvent,SeckillOrderCreationFailedEvent,SeckillStockSoldOutEvent}.cs
- Exceptions/PromotionDomainException.cs

### 应用层（Leno.Promotion.Application）
- Services/{CouponAppService,PromotionAppService,PromotionCalculateAppService,SeckillAppService}.cs
- Validators/{PromotionValidators,SeckillValidators}.cs
- DTOs/{CouponDtos,PromotionActivityDtos,SeckillDtos}.cs
- {IAppServices,IPromotionCalculateAppService,ISeckillAppService}.cs

### 基础设施层（Leno.Promotion.Infrastructure）
- Services/{RedisSeckillStockService,EfCorePromotionQueryService}.cs
- Repositories/EfCore{Coupon,UserCoupon,SeckillActivity,SeckillPreOccupationRecord,PromotionActivity}Repository.cs
- Consumers/{OrderEventConsumer,PointsExchangeConsumer,SeckillOrderEventConsumer}.cs
- BackgroundServices/SeckillPreOccupationCompensationService.cs
- ReadModels/{CouponReadModel,SeckillActivityReadModel,CouponCreatedReadModelSyncConsumer,CouponDisabledReadModelSyncConsumer,SeckillActivityPublishedReadModelSyncConsumer,SeckillActivityEndedReadModelSyncConsumer}.cs
- Configurations/{CouponConfiguration,UserCouponConfiguration,SeckillActivityConfiguration,SeckillPreOccupationRecordConfiguration,PromotionActivityConfiguration}.cs
- EventBus/PromotionIntegrationEventMapper.cs
- Dependencies/ServiceCollectionExtensions.cs
- PromotionDbContext.cs

### 表现层（Leno.Promotion.Api）
- Controllers/{CouponsController,InternalPromotionsController,PromotionsController,SeckillController,PromotionControllerBase}.cs
- GrpcServices/PromotionGrpcService.cs
- BackgroundServices/CouponExpiryService.cs
- Program.cs

### 共享内核（Leno.SharedKernel / Leno.Infrastructure）
- Abstractions/{AggregateRoot,Entity,IRepository,IUnitOfWork,IDomainEvent,IHasDomainEvents}.cs
- Exceptions/DomainException.cs
- Persistence/{BaseDbContext,EfCoreUnitOfWork}.cs
- Outbox/OutboxDbContextExtensions.cs
- EventBus/{IntegrationEventConsumerBase,RedisIdempotencyStore}.cs
