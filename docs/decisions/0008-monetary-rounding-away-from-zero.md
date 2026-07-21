# ADR-0008: 金融金额舍入策略统一为 AwayFromZero

## 状态
已接受（2026-07-22，P2-T34 决策）

## 上下文
订单域存在多处元与分的转换（`PointsOffsetAmount * 100`），原实现使用 `Math.Round(value)` 默认 `MidpointRounding.ToEven`（银行家舍入），即 0.5 向最近偶数舍入。

问题：

- 金融场景习惯使用四舍五入（`MidpointRounding.AwayFromZero`），与默认 `ToEven` 在 0.5 边界处结果不同
- 例：0.005 元转分，`ToEven` 得 0 分，`AwayFromZero` 得 1 分
- 订单域 `Order.Cancel` / `Order.ForceCancel` 发布 `OrderCancelledDomainEvent` 时积分转分、Saga `OrderSagaOrchestrator` 冻结积分时积分转分，原舍入策略不一致（部分用默认 `ToEven`，部分未指定）
- 跨域对账时，同一笔金额因舍入策略不同可能产生 1 分差异

约束：

- 不能破坏既有金额不变量（TotalAmount = ItemsAmount - Discount - Points + Freight）
- 跨域金额传递需统一舍入策略，避免对账差异

## 决策
订单域所有元与分（金额 × 100）的转换统一使用 `MidpointRounding.AwayFromZero`（四舍五入）。

涉及位置：

- `Order.Cancel` 发布 `OrderCancelledDomainEvent` 时 `(int)Math.Round(PointsOffsetAmount * 100, MidpointRounding.AwayFromZero)`
- `Order.ForceCancel` 发布 `OrderCancelledDomainEvent` 时同上
- `OrderSagaOrchestrator.ExecuteGroupAsync` 计算 `groupPoints` 时 `(int)Math.Round(groupPointsOffset * 100m, MidpointRounding.AwayFromZero)`

不涉及位置（保持 `ToEven`）：

- `InlinePointsAllocationService.AllocateBySellerRatio` 按卖家小计占比分摊积分抵现金额（`Math.Round(totalPointsOffset * (subtotal / sumSubtotals), 2, MidpointRounding.ToEven)`）——这是 2 位小数金额分摊，非元转分，且尾差归最后一组保证总和一致，改用 AwayFromZero 会导致尾差调整逻辑变化

## 后果
**正面：**
- 金融场景符合业务习惯（四舍五入），0.005 元转分 = 1 分
- 跨域对账无 1 分差异
- Saga 与 Order 聚合舍入策略一致

**负面：**
- 与 .NET 默认 `ToEven` 不同，新人需注意显式指定 `MidpointRounding.AwayFromZero`
- 累计舍入误差略大于 `ToEven`（`ToEven` 统计学上更平衡），但金融场景更看重可预期性

**风险缓解：**
- ADR 文档化舍入策略，代码注释引用 P2-T34
- 后续可补充 Roslyn analyzer 检测未指定 `MidpointRounding` 的 `Math.Round` 调用
