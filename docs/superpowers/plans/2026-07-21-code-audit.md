# Leno 代码仓库全盘分析实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 排除测试代码后，对 11 个业务 BC + 共享模块并行启动 subagent 详细分析代码问题，主 agent 跨 BC 聚合，最后由专项 subagent 输出系统架构整体评估，归档至 `docs/superpowers/specs/2026-07-21-code-audit/`。

**Architecture:** 三阶段编排：阶段 1（12 个 search subagent 并行，分两批 6+6）→ 阶段 2（主 agent 跨 BC 聚合，产出 `00-summary.md`）→ 阶段 3（1 个 `general_purpose_task` subagent 产出 `13-architecture-assessment.md`）。

**Tech Stack:** Trae Task subagent 编排；subagent 通用清单（A1-A8 功能、B1-B8 DDD、C1-C8 性能）；Markdown 报告 + `file:///` 链接。

**设计文档**：[2026-07-21-code-audit-design.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit-design.md)

---

## File Structure

| 路径 | 责任 | 产出方 |
|------|------|--------|
| `docs/superpowers/specs/2026-07-21-code-audit/README.md` | 入口索引 + 全局概览 | 主 agent |
| `docs/superpowers/specs/2026-07-21-code-audit/00-summary.md` | 跨 BC 聚合报告（D/E/F 章节） | 主 agent |
| `docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md` ~ `12-shared.md` | 12 份 BC/共享子报告 | 对应 subagent |
| `docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md` | 系统架构整体评估 | 阶段 3 subagent |

## 通用清单（嵌入每个 BC subagent prompt）

```
A. 功能正确性与 Bug
  A1 空引用与边界条件  A2 异常处理不当  A3 并发与竞态  A4 状态机非法迁移
  A5 边界条件          A6 资源泄漏      A7 异步消息可靠性  A8 事务边界

B. DDD/架构合规
  B1 BC 边界泄露      B2 聚合设计违规  B3 防腐层缺失/穿透  B4 共享内核污染
  B5 CQRS 职责混乱    B6 层依赖反向    B7 事件契约一致性   B8 仓储滥用

C. 性能与可靠性
  C1 N+1 查询         C2 缺失索引      C3 缓存策略         C4 大对象/全表扫
  C5 异步消息堆积     C6 Outbox/幂等性 C7 资源/连接池      C8 限流/熔断

严重度：🔴 高 / 🟡 中 / 🟢 低
```

## 通用报告格式（嵌入每个 BC subagent prompt）

````markdown
# {BC名称} 代码分析报告

## 概述
- 扫描范围：{路径}
- 代码行数（业务，非测试）：约 N 行
- 问题总数：高 X / 中 Y / 低 Z

## 🔴 高风险问题
### 1. {问题标题}
- **位置**：`src/.../File.cs#L120-L145`（必须使用 file:// 链接格式）
- **类别**：A1 空引用 / B2 聚合违规 / C1 N+1 ...
- **根因**：{2-3 句}
- **影响**：{1-2 句，含触发场景}
- **修复建议**：{具体到代码片段或改造步骤}
- **影响范围**：{涉及的聚合/接口/消费者}

## 🟡 中风险问题
（同上结构）

## 🟢 低风险问题
（同上结构，可简化）

## BC 健康度评分
| 维度 | 评分(0-5) | 说明 |
|------|-----------|------|
| 功能正确性 |  |  |
| DDD 合规 |  |  |
| 性能与可靠性 |  |  |
````

## subagent prompt 模板（每个 BC 替换 `{占位符}`）

````text
对 Leno DDD 微服务电商系统中的 {BC_NAME} 业务域进行代码静态分析。

**扫描范围**（绝对路径）：
{BC_SCAN_PATHS}

**严格排除**：
- 任何路径包含 `Tests` 的目录
- `Migrations/*.Designer.cs`、`*ModelSnapshot.cs`
- `SharedContracts.Grpc/Generated/`

**检查清单**：
{嵌入通用清单 A/B/C}

**输出要求**：
1. 使用 Read/Grep/SearchCodebase/Glob 探索代码
2. 每发现一个问题必须给出具体文件路径与行号（`file:///workspace/...#L120-L145` 格式）
3. 不要修改任何代码，仅产出分析报告
4. 严格按以下 Markdown 格式产出最终报告（用 Write 工具写入文件）：

{嵌入通用报告格式，BC名称替换为 {BC_NAME}}

**输出文件路径**：{OUTPUT_FILE_PATH}

**完成后**：用 Write 工具将完整报告写入上述路径，并将报告内容作为最终消息返回。
````

---

### Task 1: 创建报告归档目录

**Files:**
- Create: `docs/superpowers/specs/2026-07-21-code-audit/.gitkeep`

- [ ] **Step 1: 创建目录与占位文件**

```bash
mkdir -p docs/superpowers/specs/2026-07-21-code-audit
touch docs/superpowers/specs/2026-07-21-code-audit/.gitkeep
```

- [ ] **Step 2: 验证目录存在**

Run: `ls -la docs/superpowers/specs/2026-07-21-code-audit/`
Expected: 目录存在且包含 `.gitkeep`

- [ ] **Step 3: 提交**

```bash
git add docs/superpowers/specs/2026-07-21-code-audit/.gitkeep
git commit -m "chore: 创建代码分析报告归档目录"
```

---

### Task 2: 阶段 1 批次 1 —— 并行启动 BC1-BC6 subagent

**目标**：并行调度 6 个 search subagent 分析 UserAuth/Product/Cart/Order/Promotion/ReviewAfterSales。

**Files:**
- Create: `docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md`
- Create: `docs/superpowers/specs/2026-07-21-code-audit/02-product.md`
- Create: `docs/superpowers/specs/2026-07-21-code-audit/03-cart.md`
- Create: `docs/superpowers/specs/2026-07-21-code-audit/04-order.md`
- Create: `docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md`
- Create: `docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md`

- [ ] **Step 1: 并行启动 6 个 search subagent**

在**单条消息**中调用 6 次 `Task` 工具，`subagent_type: "search"`，每次使用上述 subagent prompt 模板。占位符替换如下：

| # | {BC_NAME} | {BC_SCAN_PATHS} | {OUTPUT_FILE_PATH} |
|---|-----------|-----------------|---------------------|
| 1 | UserAuth（用户与认证授权域） | `src/Services/UserAuth/Leno.UserAuth.Domain/`、`src/Services/UserAuth/Leno.UserAuth.Application/`、`src/Services/UserAuth/Leno.UserAuth.Infrastructure/`、`src/Services/UserAuth/Leno.UserAuth.Api/`（排除 `*Tests*`、`Migrations/*.Designer.cs`、`*ModelSnapshot.cs`） | `docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md` |
| 2 | Product（商品域） | `src/Services/Product/Leno.Product.Domain/`、`src/Services/Product/Leno.Product.Application/`、`src/Services/Product/Leno.Product.Infrastructure/`、`src/Services/Product/Leno.Product.Api/`（同排除项） | `docs/superpowers/specs/2026-07-21-code-audit/02-product.md` |
| 3 | Cart（购物车域） | `src/Services/Cart/Leno.Cart.Domain/`、`src/Services/Cart/Leno.Cart.Application/`、`src/Services/Cart/Leno.Cart.Infrastructure/`、`src/Services/Cart/Leno.Cart.Api/` | `docs/superpowers/specs/2026-07-21-code-audit/03-cart.md` |
| 4 | Order（订单与交易域） | `src/Services/Order/Leno.Order.Domain/`、`src/Services/Order/Leno.Order.Application/`、`src/Services/Order/Leno.Order.Infrastructure/`、`src/Services/Order/Leno.Order.Api/` | `docs/superpowers/specs/2026-07-21-code-audit/04-order.md` |
| 5 | Promotion（促销域） | `src/Services/Promotion/Leno.Promotion.Domain/`、`src/Services/Promotion/Leno.Promotion.Application/`、`src/Services/Promotion/Leno.Promotion.Infrastructure/`、`src/Services/Promotion/Leno.Promotion.Api/` | `docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md` |
| 6 | ReviewAfterSales（评价与售后域） | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/`、`src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/`、`src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/`、`src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/` | `docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md` |

每次 `Task` 调用的 `query` 字段 = 完整替换后的 subagent prompt（含检查清单、报告格式、输出路径）。

- [ ] **Step 2: 等待全部 6 个 subagent 完成**

记录每个 subagent 的返回摘要。失败的 subagent 在主 agent 日志中标注 "BC{N} 分析未完成"。

- [ ] **Step 3: 验证 6 份报告文件已生成**

Run: `ls -la docs/superpowers/specs/2026-07-21-code-audit/0[1-6]-*.md`
Expected: 列出 6 个 `.md` 文件，每个文件大小 > 1KB

- [ ] **Step 4: 提交批次 1 报告**

```bash
git add docs/superpowers/specs/2026-07-21-code-audit/0[1-6]-*.md
git commit -m "docs: 完成阶段1批次1代码分析报告（BC1-BC6）"
```

---

### Task 3: 阶段 1 批次 2 —— 并行启动 BC7-BC11 + Shared subagent

**目标**：并行调度 6 个 search subagent 分析 PointsMembership/Payment/Notification/SellerShop/SystemAdmin/Shared。

**Files:**
- Create: `docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md`
- Create: `docs/superpowers/specs/2026-07-21-code-audit/08-payment.md`
- Create: `docs/superpowers/specs/2026-07-21-code-audit/09-notification.md`
- Create: `docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md`
- Create: `docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md`
- Create: `docs/superpowers/specs/2026-07-21-code-audit/12-shared.md`

- [ ] **Step 1: 并行启动 6 个 search subagent**

在**单条消息**中调用 6 次 `Task` 工具：

| # | {BC_NAME} | {BC_SCAN_PATHS} | {OUTPUT_FILE_PATH} |
|---|-----------|-----------------|---------------------|
| 7 | PointsMembership（积分与会员域） | `src/Services/PointsMembership/Leno.PointsMembership.{Domain,Application,Infrastructure,Api}/` | `docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md` |
| 8 | Payment（支付集成域） | `src/Services/Payment/Leno.Payment.{Domain,Application,Infrastructure,Api}/` | `docs/superpowers/specs/2026-07-21-code-audit/08-payment.md` |
| 9 | Notification（消息通知域） | `src/Services/Notification/Leno.Notification.{Domain,Application,Infrastructure,Api}/` | `docs/superpowers/specs/2026-07-21-code-audit/09-notification.md` |
| 10 | SellerShop（卖家与店铺管理域） | `src/Services/SellerShop/Leno.SellerShop.{Domain,Application,Infrastructure,Api}/` | `docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md` |
| 11 | SystemAdmin（系统管理域） | `src/Services/SystemAdmin/Leno.SystemAdmin.{Domain,Application,Infrastructure,Api}/` | `docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md` |
| 12 | Shared（BuildingBlocks + SharedKernel + ApiGateway） | `src/BuildingBlocks/Leno.Infrastructure/`、`src/BuildingBlocks/Leno.Infrastructure.Abstractions/`、`src/BuildingBlocks/Leno.SharedKernel/`、`src/BuildingBlocks/Leno.SharedContracts/`（排除 `Leno.SharedContracts.Grpc/Generated/`）、`src/ApiGateway/Leno.ApiGateway/` | `docs/superpowers/specs/2026-07-21-code-audit/12-shared.md` |

Shared subagent 额外说明（追加到 prompt）：
> 此 subagent 分析的是共享层。检查重点：B3 防腐层模式是否被各 BC 重复实现、B4 共享内核是否被污染、D2 重复实现是否应抽取到共享层。同时审视 ACL 模式、Outbox、CQRS 基础设施、ApiGateway 中间件（限流/缓存/JWT 黑名单）的设计合理性。

- [ ] **Step 2: 等待全部 6 个 subagent 完成**

记录每个 subagent 的返回摘要。失败的 subagent 在主 agent 日志中标注 "BC{N} 分析未完成"。

- [ ] **Step 3: 验证 6 份报告文件已生成**

Run: `ls -la docs/superpowers/specs/2026-07-21-code-audit/{07,08,09,10,11,12}-*.md`
Expected: 列出 6 个 `.md` 文件，每个文件大小 > 1KB

- [ ] **Step 4: 提交批次 2 报告**

```bash
git add docs/superpowers/specs/2026-07-21-code-audit/{07,08,09,10,11,12}-*.md
git commit -m "docs: 完成阶段1批次2代码分析报告（BC7-BC11+共享模块）"
```

---

### Task 4: 阶段 2 —— 主 agent 跨 BC 聚合分析

**目标**：读取全部 12 份子报告，执行 D1-D6 跨 BC 一致性分析，产出 `00-summary.md`。

**Files:**
- Create: `docs/superpowers/specs/2026-07-21-code-audit/00-summary.md`

- [ ] **Step 1: 读取全部 12 份子报告**

并行调用 12 次 `Read` 工具读取：
- `docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md` ~ `12-shared.md`

对于失败 BC，跳过读取，在汇总中标注。

- [ ] **Step 2: 执行 D 跨 BC 一致性分析**

基于 12 份报告内容，分析以下 6 项：

| # | 检查项 | 检测方法 |
|---|--------|---------|
| D1 | 事件契约对齐 | 同一事件类型在所有引用方有相同字段命名/类型；版本号统一。读取 `src/BuildingBlocks/Leno.SharedContracts/Events/*.cs` 交叉比对 |
| D2 | ACL 模式重复 | 多个 BC 各自实现类似防腐层（如 UserContact、ProductSnapshot、OrderQuery）应抽取到 BuildingBlocks |
| D3 | 共享内核污染 | `src/BuildingBlocks/Leno.SharedKernel/` 中出现业务逻辑、聚合、特定 BC 的概念 |
| D4 | 跨域事务边界 | 涉及多 BC 的事务未走 Outbox/事件最终一致；同步调用链过长 |
| D5 | gRPC 与 REST 双轨一致性 | 同一能力在 `*.proto` 与 Controller 上定义不一致；返回语义不同 |
| D6 | 重复实现 | 多个 BC 各自实现类似工具（限流、重试、模板渲染）未抽取到共享层 |

对每项列出：发现的问题清单（引用子报告位置）+ 修复建议。

- [ ] **Step 3: 生成 E 全局视图**

**E1 BC 健康度对比矩阵**：

| BC | 功能正确性(0-5) | DDD 合规(0-5) | 性能与可靠性(0-5) | 综合 |
|----|-----------------|----------------|-------------------|------|
| UserAuth | | | | |
| Product | | | | |
| Cart | | | | |
| Order | | | | |
| Promotion | | | | |
| ReviewAfterSales | | | | |
| PointsMembership | | | | |
| Payment | | | | |
| Notification | | | | |
| SellerShop | | | | |
| SystemAdmin | | | | |
| Shared | | | | |

评分从各子报告"BC 健康度评分"章节汇总。

**E2 高风险问题热力分布**：按 BC × 类别（A/B/C）统计 🔴 高风险数量，列出 Top 10 🔴 问题清单（跨 BC 排序）。

**E3 修复优先级矩阵**：基于严重度 × 影响范围 × 实现成本，列出 Top 20 待修复问题。

- [ ] **Step 4: 生成 F 修复路线建议**

按 P0/P1/P2 三档列出：

**P0（立即修复）**：🔴 高风险且影响主链路（订单/支付/积分发放）
- 每项含：问题描述、位置（file:// 链接）、修复步骤、负责人建议

**P1（短期修复）**：🔴 高风险但影响边缘 BC，或 🟡 中风险且影响主链路

**P2（中长期）**：🟡 中风险 + 🟢 低风险，按 BC 分批治理

- [ ] **Step 5: 写入 00-summary.md**

用 `Write` 工具将以下结构写入 `docs/superpowers/specs/2026-07-21-code-audit/00-summary.md`：

````markdown
# Leno 代码仓库全盘分析汇总报告

**生成日期**：2026-07-21
**子报告数量**：12 份 BC 报告 + 本汇总
**分析范围**：11 业务 BC + 共享模块（BuildingBlocks/SharedKernel/ApiGateway）

## 1 全局概览
- 总代码行数（业务）：约 N 行
- 问题总数：🔴 X / 🟡 Y / 🟢 Z
- 健康度平均分：功能 a/5、DDD b/5、性能 c/5

## 2 D 跨 BC 一致性分析
### D1 事件契约对齐
（问题清单 + 修复建议）
### D2 ACL 模式重复
### D3 共享内核污染
### D4 跨域事务边界
### D5 gRPC 与 REST 双轨一致性
### D6 重复实现

## 3 E 全局视图
### E1 BC 健康度对比矩阵
（表格）
### E2 高风险问题热力分布
（表格 + Top 10 🔴 清单）
### E3 修复优先级矩阵
（Top 20 待修复问题）

## 4 F 修复路线建议
### P0 立即修复
### P1 短期修复
### P2 中长期

## 5 失败/缺口
（如有 BC 分析未完成，列在此处）
````

- [ ] **Step 6: 提交汇总报告**

```bash
git add docs/superpowers/specs/2026-07-21-code-audit/00-summary.md
git commit -m "docs: 完成阶段2跨BC聚合分析报告"
```

---

### Task 5: 阶段 3 —— 启动架构整体评估 subagent

**目标**：启动 1 个 `general_purpose_task` subagent，读取全部报告 + 架构文档，产出 `13-architecture-assessment.md`。

**Files:**
- Create: `docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md`

- [ ] **Step 1: 启动架构评估 subagent**

调用 `Task` 工具，`subagent_type: "general_purpose_task"`，`query` 字段为以下完整 prompt：

````text
你是 Leno 电商平台架构评估专家。基于已完成的代码分析报告，对系统整体架构进行评估。

**输入文件**（必须全部 Read）：
1. `docs/superpowers/specs/2026-07-21-code-audit/00-summary.md` —— 跨 BC 聚合报告
2. `docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md` ~ `12-shared.md` —— 12 份 BC 详细报告
3. `docs/superpowers/specs/2026-07-21-code-audit-design.md` —— 分析设计文档
4. `docs/spec/00-需求文档总览与DDD架构.md` —— DDD 战略设计
5. `docs/architecture/anticorruption-pattern.md` —— 防腐层模式
6. `docs/decisions/0001-grpc-dual-track-with-http-fallback.md` ~ `0007-guid-string-migration-strategy.md` —— ADR 决策
7. `docs/handbook/03-architecture-overview.md`、`05-cross-bc-communication.md`、`06-storage-and-cache.md` —— 架构手册
8. `src/` 顶层目录结构（使用 LS 工具，不重读细节）

**任务**：产出 `docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md`，包含以下 7 个章节，每章节必须引用具体子报告或代码位置作为证据：

# Leno 系统架构整体评估报告

## G1 架构定位与成熟度
- 评估当前架构相对 DDD+CQRS+事件驱动+微服务目标的达成度
- 按 5 个维度量化（每项百分比 + 说明）：
  - DDD 战略设计落地度
  - CQRS 读写分离实施度
  - 事件驱动与最终一致性
  - 微服务边界与防腐层
  - 可观测性与运维成熟度
- 给出综合架构成熟度评分（0-100）

## G2 架构优点
列出已做得好的设计，每项含：
- 优点名称
- 价值说明
- 适用场景
- 引用证据（子报告位置或代码 file:// 链接）

候选优点（基于设计文档已知）：ACL 模式、Outbox、Consul 配置中心、Helm 部署、CQRS Query Handler、gRPC 双轨、限流熔断三态机、可观测性增强。但需基于实际代码验证后保留或剔除。

## G3 架构缺点
列出系统级问题，每项含：
- 缺点名称
- 根因分析
- 影响
- 引用子报告证据（file:// 链接到子报告章节）

重点检查：BC 边界模糊处、共享内核污染、跨域事务边界不清、重复实现、过度设计、设计不足。

## G4 技术债清单
按"业务影响 × 修复成本"四象限分类，列出 Top 10 技术债：

| 排名 | 技术债 | 业务影响 | 修复成本 | 象限 | 引用 |
|------|--------|----------|----------|------|------|
| 1 | | 高/中/低 | 高/中/低 | I/II/III/IV | |

象限说明：
- I：高影响低成本（立即修复）
- II：高影响高成本（规划修复）
- III：低影响低成本（机会主义修复）
- IV：低影响高成本（暂不修复）

## G5 优化方案
按 3 个时间维度给出：

### 短期（1-2 周）
列出 3-5 项零散修复，每项含：措施、预期收益、影响 BC、实施步骤要点

### 中期（1-2 月）
列出 3-5 项 BC 边界整治、共享层抽取、跨 BC 事件治理

### 长期（3-6 月）
列出 2-3 项架构演进方向（读模型分离、CQRS 深化、可观测性升级等）

## G6 风险评估
列出未修复状态下 Top 5 生产风险：

| 排名 | 风险 | 触发条件 | 影响范围 | 严重度 | 缓解措施 |
|------|------|----------|----------|--------|----------|
| 1 | | | | 🔴/🟡/🟢 | |

候选风险类型：资金损失、数据不一致、系统宕机、安全漏洞、性能退化。但必须基于子报告证据，不得虚构。

## G7 与业界实践对比
对比以下参考架构的差距与优势：
- Microsoft eShopOnContainers
- 亚马逊电商参考架构
- Alibaba COLA 架构

每项对比含：
- 维度（如分层、事件驱动、CQRS、限界上下文）
- Leno 现状
- 业界实践
- 差距分析
- Leno 的独特优势（如有）

可使用 WebSearch 工具检索上述参考架构的最新设计要点。

**输出要求**：
- 用 Write 工具写入 `docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md`
- 引用证据必须使用 `file:///workspace/...#L行号` 格式
- 不要修改任何业务代码
- 将完整报告内容作为最终消息返回
````

- [ ] **Step 2: 等待 subagent 完成**

记录返回摘要。

- [ ] **Step 3: 验证报告已生成**

Run: `ls -la docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md`
Expected: 文件存在，大小 > 5KB

- [ ] **Step 4: 提交架构评估报告**

```bash
git add docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md
git commit -m "docs: 完成阶段3系统架构整体评估报告"
```

---

### Task 6: 生成 README.md 索引

**目标**：创建入口索引文件，列出全部 14 份报告的链接与简介。

**Files:**
- Create: `docs/superpowers/specs/2026-07-21-code-audit/README.md`

- [ ] **Step 1: 写入 README.md**

用 `Write` 工具写入以下内容（subagent 完成后实际链接生效）：

````markdown
# Leno 代码仓库全盘分析报告

**生成日期**：2026-07-21
**分析方法**：12 个 BC subagent 并行 + 主 agent 跨 BC 聚合 + 1 个架构评估 subagent
**设计文档**：[2026-07-21-code-audit-design.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit-design.md)

## 报告索引

### 顶层报告
- [00-summary.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md) —— 跨 BC 聚合分析（D 一致性 / E 全局视图 / F 修复路线）
- [13-architecture-assessment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md) —— 系统架构整体评估（G1-G7）

### BC 详细报告
| # | BC | 类型 | 报告 |
|---|-----|------|------|
| 1 | UserAuth | 核心 | [01-userauth.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md) |
| 2 | Product | 核心 | [02-product.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/02-product.md) |
| 3 | Cart | 核心 | [03-cart.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md) |
| 4 | Order | 核心 | [04-order.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md) |
| 5 | Promotion | 核心 | [05-promotion.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/05-promotion.md) |
| 6 | ReviewAfterSales | 核心 | [06-reviewaftersales.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md) |
| 7 | PointsMembership | 支撑 | [07-pointsmembership.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/07-pointsmembership.md) |
| 8 | Payment | 支撑 | [08-payment.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/08-payment.md) |
| 9 | Notification | 通用子域 | [09-notification.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/09-notification.md) |
| 10 | SellerShop | 支撑 | [10-sellershop.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/10-sellershop.md) |
| 11 | SystemAdmin | 通用子域 | [11-systemadmin.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/11-systemadmin.md) |
| 12 | Shared | 共享层 | [12-shared.md](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md) |

## 检查清单覆盖

- **A 功能正确性**：A1 空引用 / A2 异常处理 / A3 并发 / A4 状态机 / A5 边界 / A6 资源泄漏 / A7 异步消息 / A8 事务边界
- **B DDD 架构合规**：B1 BC 边界 / B2 聚合设计 / B3 防腐层 / B4 共享内核 / B5 CQRS / B6 层依赖 / B7 事件契约 / B8 仓储滥用
- **C 性能与可靠性**：C1 N+1 / C2 索引 / C3 缓存 / C4 大对象 / C5 消息堆积 / C6 Outbox/幂等 / C7 连接池 / C8 限流熔断

## 阅读建议

1. **快速了解全局**：先读 `00-summary.md` 的健康度矩阵与 Top 10 🔴 问题
2. **了解架构优劣**：读 `13-architecture-assessment.md` 的 G1/G2/G3
3. **修复 P0 问题**：参考 `00-summary.md` 的 F1 章节，跳转到对应 BC 报告查看修复建议
4. **长期规划**：参考 `13-architecture-assessment.md` 的 G5 优化方案
````

- [ ] **Step 2: 提交 README**

```bash
git add docs/superpowers/specs/2026-07-21-code-audit/README.md
git commit -m "docs: 新增代码分析报告入口索引"
```

---

### Task 7: 最终验证与清理

**目标**：验证全部 14 份报告已生成、可访问；清理临时文件；确认 git 提交完整。

- [ ] **Step 1: 列出全部报告文件**

Run: `ls -la docs/superpowers/specs/2026-07-21-code-audit/`
Expected: 包含 README.md、00-summary.md、01-12 BC 报告、13-architecture-assessment.md，共 14 份 `.md` 文件 + `.gitkeep`

- [ ] **Step 2: 验证每份报告大小**

Run: `find docs/superpowers/specs/2026-07-21-code-audit -name "*.md" -size -1k -print`
Expected: 无输出（所有 .md 文件均 > 1KB）

- [ ] **Step 3: 删除 .gitkeep**

```bash
rm docs/superpowers/specs/2026-07-21-code-audit/.gitkeep
```

- [ ] **Step 4: 检查 git 状态**

Run: `git status`
Expected: `nothing to commit, working tree clean` 或仅显示 .gitkeep 删除

- [ ] **Step 5: 提交清理**

```bash
git add -A
git commit -m "chore: 清理临时占位文件"
```

- [ ] **Step 6: 推送到远程（可选，如配置了远程）**

```bash
git push origin HEAD 2>&1 || echo "推送失败：远程凭据未配置，本地提交保留"
```

- [ ] **Step 7: 输出最终摘要**

向用户输出：
- 报告总数：14 份
- 报告目录：`docs/superpowers/specs/2026-07-21-code-audit/`
- 入口索引：`README.md`
- 全局问题统计：🔴 X / 🟡 Y / 🟢 Z（从 00-summary.md 抓取）
- 架构成熟度评分：（从 13-architecture-assessment.md G1 抓取）
- 下一步建议：参考 00-summary.md 的 F 修复路线

---

## Self-Review

### 1. Spec coverage 检查

| 设计章节 | 覆盖任务 |
|---------|---------|
| 阶段 1（12 subagent 并行） | Task 2（BC1-6）+ Task 3（BC7-12+Shared） |
| 阶段 2（跨 BC 聚合） | Task 4 |
| 阶段 3（架构评估 subagent） | Task 5 |
| 检查清单 A1-A8/B1-B8/C1-C8 | 嵌入 subagent prompt 模板 |
| 报告格式 | 嵌入 subagent prompt 模板 |
| D1-D6 跨 BC 分析 | Task 4 Step 2 |
| E1/E2/E3 全局视图 | Task 4 Step 3 |
| F1/F2/F3 修复路线 | Task 4 Step 4 |
| G1-G7 架构评估章节 | Task 5 prompt |
| 14 份报告输出 | Task 1-6 |
| git 提交 | 每个任务末尾 |
| 失败处理 | Task 2/3 Step 2、Task 4 Step 1 |

### 2. Placeholder scan

- ✅ 无 TBD/TODO
- ✅ 每步都有具体命令或工具调用
- ✅ 检查清单完整列出 24 项
- ✅ 报告格式完整
- ✅ subagent prompt 模板完整

### 3. Type consistency

- ✅ subagent 边界路径与设计文档 2.1 节一致
- ✅ 检查清单编号 A1-C8 与设计文档 3.1-3.3 节一致
- ✅ 严重度评级 🔴/🟡/🟢 与设计文档 3.4 节一致
- ✅ 报告文件路径与设计文档 1.3 节一致

无问题，计划完成。
