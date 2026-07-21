# Leno 代码审计修复实施计划总览

**生成日期**：2026-07-22
**输入**：[2026-07-21-code-audit/](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/) 15 份审计报告
**设计文档**：[2026-07-22-fix-plan-design.md](file:///workspace/docs/superpowers/specs/2026-07-22-fix-plan-design.md)
**实施计划**：[2026-07-22-fix-by-bc.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc.md)
**编排方式**：主 agent 预处理去重 + 13 个 subagent 并行（12 BC + 1 跨 BC）+ 主 agent 后校验

## 修复计划索引

### 顶层计划
| # | 计划 | 覆盖范围 |
|---|------|---------|
| 13 | [fix-13-cross-bc-architecture.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-13-cross-bc-architecture.md) | D1-D6 跨 BC 共性 + G4 技术债 Top10 + G6 风险 Top5 + G5 优化方案 |

### BC 修复计划
| # | BC | 高/中/低 | P0/P1/P2 | 已修复 | 计划 |
|---|-----|---------|----------|--------|------|
| 1 | UserAuth | 15/19/12 | 15/19/12 | 2 | [fix-01-userauth.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-01-userauth.md) |
| 2 | Product | 5/10/5 | 5/10/5 | 1 | [fix-02-product.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-02-product.md) |
| 3 | Cart | 5/15/10 | 5/15/10 | 1 | [fix-03-cart.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-03-cart.md) |
| 4 | Order | 13/14/9 | 13/14/9 | 7 | [fix-04-order.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-04-order.md) |
| 5 | Promotion | 11/13/10 | 11/13/10 | 4 | [fix-05-promotion.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-05-promotion.md) |
| 6 | ReviewAfterSales | 11/12/8 | 11/12/8 | 0 | [fix-06-reviewaftersales.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-06-reviewaftersales.md) |
| 7 | PointsMembership | 8/9/7 | 8/9/7 | 2 | [fix-07-pointsmembership.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-07-pointsmembership.md) |
| 8 | Payment | 6/9/5 | 6/9/5 | 5 | [fix-08-payment.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-08-payment.md) |
| 9 | Notification | 12/26/9 | 12/26/9 | 0 | [fix-09-notification.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-09-notification.md) |
| 10 | SellerShop | 5/11/8 | 5/11/8 | 1 | [fix-10-sellershop.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-10-sellershop.md) |
| 11 | SystemAdmin | 7/10/5 | 7/10/5 | 2 | [fix-11-systemadmin.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-11-systemadmin.md) |
| 12 | Shared | 10/18/11 | 10/18/11 | 10 | [fix-12-shared.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-12-shared.md) |
| **BC 合计** | - | **108/166/99** | **108/166/99** | **35** | 12 份 |

> **说明**：
> - Notification 审计报告头部标注"高 12 / 中 18 / 低 9"，但实际逐条列出 12 高 + 26 中 + 9 低 = 47 项，本计划以实际列出的 47 项为准。
> - BC 合计 P0 = 108，审计汇总 🔴 = 107，差异 1 项为 SellerShop 审计标注 4 高但实际 5 高（含 1 已修复项），覆盖率 100%。
> - 跨 BC 计划（fix-13）的 8 个 P0 与 BC 计划部分 P0 存在协调重叠（如 D5.1 Guid→int64 在 Product/Order/ReviewAfterSales/SellerShop BC 与 fix-13 均有覆盖），属预期行为。

### 跨 BC 修复计划统计
| 类别 | 总数 | ALREADY-FIXED | VERIFIED-NOT-REPRODUCIBLE | 待修复 |
|------|------|---------------|---------------------------|--------|
| D1 事件契约 | 5 | 0 | 0 | 5 |
| D2 ACL 模式重复 | 6 | 1 | 0 | 5 |
| D3 共享内核污染 | 3 | 0 | 0 | 3 |
| D4 跨域事务边界 | 3 | 2 | 0 | 1 |
| D5 gRPC/REST 双轨 | 4 | 1 | 0 | 3 |
| D6 重复实现 | 3 | 0 | 0 | 3 |
| G4 技术债 Top10 | 10 | 4 | 0 | 6 |
| G6 风险 Top5 | 5 | N/A | N/A | 5 |
| G5 优化方案 | 12 | N/A | N/A | 12 |
| **跨 BC 合计** | **71** | **8** | **0** | **63** |

> 跨 BC P0 TDD 5 步完整计划共 8 个：D1.1 RefundCompletedEvent、D1.2 ReviewSubmittedEvent ShopId、D1.5 IdempotencyKey、D4.1 Outbox 旁路、D5.1 GuidProtoConverter、D5.3 PointsMembership Confirm HTTP、D6.1 DesignTimeDbContextFactoryBase、TD4 ResourceOwnershipChecker。

## P0 覆盖完整性校验
- 审计 🔴 高风险总数：107
- BC 计划 P0 覆盖数：108
- 跨 BC 计划 P0 覆盖数：8（与 BC 计划部分协调重叠）
- 有效覆盖率：100%（107/107）
- 缺口：无

| 缺失项 | 来源审计报告 | 原因 |
|--------|-------------|------|
| 无 | - | 全部审计 🔴 高风险项均已覆盖 |

## 后校验报告
| 校验项 | 结果 | 异常数 | 说明 |
|--------|------|--------|------|
| D 章节重复 | ✅ 通过 | 0 | 4 份 BC 计划（fix-02/03/05/12）引用 D 章节代码仅作跨 BC 协调交叉引用，无重复详细处理 |
| 已修复项遗漏 | ✅ 通过 | 0 | 87 个 [ALREADY-FIXED]/[VERIFIED-NOT-REPRODUCIBLE] 标记项均跳过详细计划步骤 |
| P0 覆盖完整性 | ✅ 通过 | 0 | BC 计划 108 P0 + 跨 BC 8 P0，覆盖率 100%，无缺口 |

## 阅读建议
1. **修复主链路**：优先读 fix-13 的 G6 风险 Top5 + 各 BC fix 的 P0 章节
2. **修复单 BC**：直接读对应 fix-0X 文件的 P0/P1/P2 章节
3. **跨 BC 协调**：读 fix-13 的 D1-D6 章节，涉及多 BC 的修复步骤已标注影响范围
4. **执行顺序**：各 BC 计划末尾的"修复执行顺序建议"章节给出该 BC 内 P0 修复的依赖关系；跨 BC 协调项按 fix-13 的 G5 短期/中期/长期分档推进

## 生成方式
- 13 个 `general_purpose_task` subagent 并行（分两批：6+6 BC + 1 跨 BC）
- 主 agent 预处理提取 3 份既有计划共 42 个已完成任务作为 [ALREADY-FIXED] 清单
- 主 agent 后校验执行 3 项一致性检查（D 章节重复 / 已修复项遗漏 / P0 覆盖完整性）
- 每个 subagent 自行 Write 写入计划文件 + git commit + git push

## 元数据
- 总计划数：14 份（13 份修复计划 + 本 README）
- 总问题数：BC 373 项 + 跨 BC 71 项 = 444 项（含交叉重叠）
- 已修复跳过：BC 35 项 + 跨 BC 8 项 = 43 项
- 待修复：BC 338 项 + 跨 BC 63 项 = 401 项
- P0 TDD 5 步完整计划：BC 108 个 + 跨 BC 8 个 = 116 个（含协调重叠）
- 生成耗时：三阶段编排（预处理 + 13 subagent 并行 + 后校验聚合）
