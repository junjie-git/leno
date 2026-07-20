# Leno 代码仓库全盘分析报告

**生成日期**：2026-07-21
**分析方法**：12 个 BC subagent 并行 + 主 agent 跨 BC 聚合 + 1 个架构评估 subagent
**设计文档**：[2026-07-21-code-audit-design.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit-design.md)
**实施计划**：[2026-07-21-code-audit.md](file:///workspace/docs/superpowers/plans/2026-07-21-code-audit.md)

## 总览

- **扫描范围**：11 业务 BC + 共享模块（BuildingBlocks/SharedKernel/ApiGateway）
- **问题总数**：🔴 107 / 🟡 158 / 🟢 99，合计 364
- **架构成熟度**：78.25 / 100（L4 量化管理）
- **健康度均分**：功能正确性、DDD 合规、性能与可靠性三维平均

## 报告索引

### 顶层报告

| # | 报告 | 简介 |
|---|------|------|
| 00 | [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) | 跨 BC 聚合分析（D 一致性 / E 全局视图 / F 修复路线） |
| 13 | [13-architecture-assessment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md) | 系统架构整体评估（G1-G7：成熟度 / 优缺点 / 技术债 / 优化方案 / 风险评估 / 业界对比） |

### BC 详细报告

| # | BC | 类型 | 高/中/低 | 报告 |
|---|-----|------|----------|------|
| 1 | UserAuth | 核心 | 15/19/12 | [01-userauth.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md) |
| 2 | Product | 核心 | 5/10/5 | [02-product.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md) |
| 3 | Cart | 核心 | 5/15/10 | [03-cart.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md) |
| 4 | Order | 核心 | 13/14/9 | [04-order.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md) |
| 5 | Promotion | 核心 | 11/13/10 | [05-promotion.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md) |
| 6 | ReviewAfterSales | 核心 | 11/12/8 | [06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md) |
| 7 | PointsMembership | 支撑 | 8/9/7 | [07-pointsmembership.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md) |
| 8 | Payment | 支撑 | 6/9/5 | [08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md) |
| 9 | Notification | 通用子域 | 12/18/9 | [09-notification.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md) |
| 10 | SellerShop | 支撑 | 4/11/8 | [10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md) |
| 11 | SystemAdmin | 通用子域 | 7/10/5 | [11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md) |
| 12 | Shared | 共享层 | 10/18/11 | [12-shared.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md) |

## 关键发现速览

### Top 5 生产风险（来自 13-architecture-assessment.md G6）

| # | 风险 | 严重度 | 触发 BC |
|---|------|--------|---------|
| R1 | gRPC Guid→int64 碰撞导致跨 BC ID 错配 | 🔴 高 | Order/Product/ReviewAfterSales/SellerShop |
| R2 | Outbox 旁路导致分布式一致性故障 | 🔴 高 | UserAuth/Promotion/SystemAdmin/PointsMembership/Cart |
| R3 | IDOR 越权导致用户数据泄露 | 🔴 高 | Payment/ReviewAfterSales |
| R4 | 跨域 Saga 缺补偿动作导致半完成状态 | 🟡 中 | Order/Payment/Notification |
| R5 | DesignTimeFactory SA 密码泄露 | 🟡 中 | Cart/SellerShop/Notification |

### 6 项跨 BC 共性问题（来自 00-summary.md D 章节）

- **D1 事件契约对齐**：RefundCompletedEvent 缺 ChannelRefundNo、ReviewSubmittedEvent 缺 ShopId、MemberLevelUpgradedEvent 双身份混淆
- **D2 ACL 模式重复**：6 类客户端在多 BC 重复实现（OrderStatusProvider/PaymentInfoQueryService 等）
- **D3 共享内核污染**：Money 不可变性破坏、OrderStatus 硬编码魔法数
- **D4 跨域事务边界**：Outbox 绕过在 5 个 BC 重复、Saga 补偿失败
- **D5 gRPC 与 REST 双轨一致性**：Guid.GetHashCode() 在 4 BC 重复、PaymentGrpcService 硬编码零值
- **D6 重复实现**：设计期工厂硬编码密码在 3 BC 重复、双路由 Obsolete 无下线时间

### 架构金牌能力（来自 13-architecture-assessment.md G2）

1. ACL 双轨调度 + 三态熔断状态机
2. Outbox 两阶段标记 + IntegrationEventConsumerBase 幂等基类
3. Consul 配置中心 KV 热更新
4. Helm Chart 部署 + HPA + K8s 探针
5. IQueryHandler 轻量 CQRS（无 MediatR 依赖）
6. 7 份 ADR 决策追溯体系（0001-0007）
7. BaseDbContext 统一基础设施（消除重复）
8. ReadModelSyncConsumerBase 读模型同步抽象
9. 共享内核 vs 共享契约清晰分层
10. M5.1/M5.3 可观测性增强（OTel + Prometheus + Serilog）

## 检查清单覆盖

- **A 功能正确性**：A1 空引用 / A2 异常处理 / A3 并发 / A4 状态机 / A5 边界 / A6 资源泄漏 / A7 异步消息 / A8 事务边界
- **B DDD 架构合规**：B1 BC 边界 / B2 聚合设计 / B3 防腐层 / B4 共享内核 / B5 CQRS / B6 层依赖 / B7 事件契约 / B8 仓储滥用
- **C 性能与可靠性**：C1 N+1 / C2 索引 / C3 缓存 / C4 大对象 / C5 消息堆积 / C6 Outbox/幂等 / C7 连接池 / C8 限流熔断
- **D 跨 BC 一致性**：D1 事件契约对齐 / D2 ACL 模式重复 / D3 共享内核污染 / D4 跨域事务边界 / D5 gRPC 与 REST 双轨 / D6 重复实现
- **G 架构评估**：G1 成熟度 / G2 优点 / G3 缺点 / G4 技术债 / G5 优化方案 / G6 风险评估 / G7 业界对比

## 修复路线（来自 00-summary.md F 章节 + 13-architecture-assessment.md G5）

- **P0 立即修复**（20 项）：🔴 高风险且影响主链路（订单/支付/积分发放），1-2 周内完成
- **P1 短期修复**（34 项）：🔴 高风险但影响边缘 BC，或 🟡 中风险且影响主链路，1-2 月内完成
- **P2 中长期治理**（10 项共享层治理 + 12 个 BC 中低风险清单），3-6 月持续推进

## 阅读建议

1. **快速了解全局**：先读 [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) 的 E1 健康度矩阵与 E2 Top 10 🔴 问题清单
2. **了解架构优劣**：读 [13-architecture-assessment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md) 的 G1（成熟度评分）/ G2（优点）/ G3（缺点）
3. **修复 P0 问题**：参考 [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) 的 F1 章节，跳转到对应 BC 报告查看修复建议
4. **生产风险排查**：参考 [13-architecture-assessment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md) 的 G6 Top 5 风险表
5. **长期规划**：参考 [13-architecture-assessment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md) 的 G5 短/中/长期优化方案
6. **业界对比**：参考 [13-architecture-assessment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md) 的 G7（与 eShopOnContainers / Amazon / COLA 对比）

## 元数据

- **总报告数**：14 份（00 汇总 + 12 BC 详细 + 13 架构评估 + README）
- **总字节数**：约 671 KB
- **生成耗时**：三阶段编排（阶段 1 批次 1 并行 6 subagent + 阶段 1 批次 2 并行 6 subagent + 阶段 2 主 agent 聚合 + 阶段 3 1 个架构评估 subagent）
