# ADR-0009: 保留 DbSet\<StockReservation\> 作为库存聚合审计/对账源

## 状态

已接受

## 上下文

审计 P2-T36（4.9）指出：`OrderDbContext` 暴露了 `DbSet<StockReservation>`，但原实现中 `IInventoryRepository` 绕过聚合根直接操作 Redis，该 DbSet 仅被 `InventoryReconciliationBackgroundService` 用 `Skip/Take` 分页读取，无业务代码通过 `_context.StockReservations` 操作聚合根。审计结论是 `StockReservation` 聚合"形同虚设，维护成本高"，建议二选一：

1. 删除 `StockReservation` 聚合（Redis 是真相源，DB 仅作对账快照，将对账后台改为直接读 Redis 并与 `stock_reservation_snapshots` 表对账）；
2. 让所有库存操作经过 `StockReservation` 聚合根（按审计 2.1 修复建议）。

该决策与 P0-T1 修复联动：P0-T1 已在 `RedisInventoryRepository` 中实现"Redis 原子层 + DB 聚合审计源"双写策略，需要评估是否保留 `DbSet<StockReservation>`。

## 决策

**保留 `DbSet<StockReservation>`，并保留 `StockReservationConfiguration` 现有映射配置。**

理由：P0-T1 已选择审计建议方案 2，所有库存操作（`ReserveAsync`/`ConfirmAsync`/`ReleaseAsync`/`ReturnDeductedAsync`/`SetBaseLineAsync`）均经过 `StockReservation` 聚合根，`DbSet<StockReservation>` 不再形同虚设，而是承担以下两项职责：

1. **聚合审计源（dual-write 目标）**：`RedisInventoryRepository` 在 Redis Lua 脚本原子操作成功后，通过 `IStockReservationRepository`（绑定 `EfCoreStockReservationRepository`）加载 `StockReservation` 聚合根，调用 `ReserveStock`/`ConfirmStockDeduction`/`ReleaseStock`/`Replenish` 等聚合方法维护不变量并发布领域事件，再经 `_context.StockReservations` 持久化。DB 成为聚合变更的审计轨迹，Redis 故障或脚本错误时可追溯。

2. **对账快照源**：`InventoryReconciliationBackgroundService.RunReconciliationCycleAsync` 通过 `dbContext.StockReservations.OrderBy(r => r.Id).Skip(skip).Take(batchSize)` 分页扫描全量聚合，将 `StockReservation.AvailableQty` 与 Redis `inventory:stock:{skuId}` 比较，不一致时以 DB 为准刷新 Redis（DB 是事务真相源，避免 Redis 故障/脚本错误导致超卖）。

**不删除 `StockReservation` 聚合的依据：**
- 双写策略下 DB 聚合是事务真相源（Redis 双写失败仅告警不回滚，最终以 DB 对账为准），删除聚合将丧失审计能力与对账锚点；
- `StockReservation` 聚合封装了 `BaseLineQty`/`ReservedQty`/`DeductedQty` 不变量与领域事件，删除聚合意味着将不变量校验下沉到 Redis Lua 脚本，可读性与可测试性下降；
- `StockReservationCompensation` 补偿聚合依赖 `StockReservation` 的语义模型，删除聚合将引发补偿流程模型坍塌。

**关于"导航关系"的说明：** 审计标题中的"导航关系"指 EF Core `HasOne`/`HasMany` 关联。当前 `StockReservationConfiguration` 仅配置属性映射与 `sku_id` 唯一索引，未定义任何导航属性——这是聚合间不直接引用的 DDD 正确实践（`Order` 聚合与 `StockReservation` 聚合通过 `SkuId`/`OrderId` 值关联，而非对象引用），无需调整。

## 后果

**正面：**
- 保留聚合审计能力，Redis 故障后可通过 DB 重建；
- 对账流程无需改造，`InventoryReconciliationBackgroundService` 与 `StockReconciliationService` 现有实现与测试（`InventoryReconciliationBackgroundServiceTests`/`StockReconciliationServiceTests`）继续有效；
- `StockReservationCompensation` 补偿聚合语义模型完整；
- 不引入破坏性变更，前端/调用方无感知。

**负面：**
- 维持双写开销：每次库存操作需额外加载聚合 + 持久化 DB，写路径延迟略增（已通过"Redis 已成功，DB 双写失败仅告警"降级策略缓解）；
- `StockReservation` 表写入压力需监控（高频秒杀场景下聚合 Update 可能成为瓶颈）。

**风险缓解：**
- `RedisInventoryRepository.PersistAggregateAsync` 已实现降级：DB 双写失败仅 `LogWarning` 不回滚 Redis，由 `InventoryReconciliationBackgroundService` 周期对账兜底；
- `StockReservationConfiguration` 配置 `ix_stock_reservations_sku_id` 唯一索引保证按 SKU 维度聚合唯一；
- 对账间隔与批量大小可经 `InventoryReconciliationOptions`（`appsettings.json` 的 `InventoryReconciliation` 节）调优。
