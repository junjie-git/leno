# Architecture Decision Records (ADR)

本目录记录 Leno 项目的关键架构决策，采用 Michael Nygard 的 ADR 格式。

## 格式

每个 ADR 文件命名：`NNNN-kebab-case-title.md`（NNNN 为四位数字编号）。

内容结构：

~~~markdown
# ADR-NNNN: 标题

## 状态
已接受 / 已取代 / 已弃用 / 已提议

## 上下文
（决策背景、约束、问题）

## 决策
（选择方案 + 理由）

## 后果
**正面：**
**负面：**
**风险缓解：**
~~~

## ADR 索引

| 编号 | 标题 | 状态 | 日期 |
|---|---|---|---|
| [ADR-0001](0001-grpc-dual-track-with-http-fallback.md) | gRPC 双轨方案（保留 HttpClient fallback） | 已接受 | 2026-07-19 |
| [ADR-0002](0002-circuit-breaker-three-state-machine.md) | 熔断器三状态机（Closed/Open/HalfOpen） | 已接受 | 2026-07-19 |
| [ADR-0003](0003-anticorruption-dispatcher-adapter-pattern.md) | AntiCorruptionDispatcher 适配器模式 | 已接受 | 2026-07-19 |
| [ADR-0004](0004-iorderstatus-provider-refactor.md) | IOrderStatusProvider 重构 | 已接受 | 2026-07-19 |
| [ADR-0005](0005-proto-backward-compatibility-constraint.md) | .proto 向后兼容约束 | 已接受 | 2026-07-19 |
| [ADR-0006](0006-guid-int64-poc-simplification-history.md) | Guid→int64 POC 简化历史 | 已接受 | 2026-07-19 |
| [ADR-0007](0007-guid-string-migration-strategy.md) | Guid→string 迁移策略 | 已接受 | 2026-07-19 |
| [ADR-0008](0008-monetary-rounding-away-from-zero.md) | 金融金额舍入策略统一为 AwayFromZero | 已接受 | 2026-07-22 |
