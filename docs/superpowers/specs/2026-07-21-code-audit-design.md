# Leno 代码仓库全盘分析设计文档

**文档版本**：V1.0
**创建日期**：2026-07-21
**目标**：排除测试代码后，针对每个 BC 域启动独立 subagent 详细分析代码中可能存在的问题，最终由主 agent 跨 BC 聚合并由专项 subagent 给出系统架构整体评估。

## 1 目标与范围

### 1.1 目标

对 Leno DDD 微服务电商系统全量业务代码进行静态分析，识别：
- 功能正确性与 Bug
- DDD/架构合规问题
- 性能与可靠性问题

不修改任何业务代码，仅产出分析报告。

### 1.2 范围

**纳入分析**：11 个业务 BC + 3 个共享模块（BuildingBlocks、SharedKernel/SharedContracts、ApiGateway）

**排除项**（按用户要求）：
- 所有 `*Tests*` 项目
- `src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/`（自动生成 gRPC 代码）
- EF Core 自动生成的 `Migrations/*.Designer.cs`、`*ModelSnapshot.cs`
- `docs/`、`deploy/`、`scripts/`、`alertmanager/`、`grafana/`（非业务代码）

### 1.3 输出归档

```
docs/superpowers/specs/2026-07-21-code-audit/
├── README.md                                    # 入口索引 + 全局概览
├── 00-summary.md                                # 阶段 2 汇总报告
├── 01-userauth.md                               # BC1 详细报告
├── 02-product.md
├── 03-cart.md
├── 04-order.md
├── 05-promotion.md
├── 06-reviewaftersales.md
├── 07-pointsmembership.md
├── 08-payment.md
├── 09-notification.md
├── 10-sellershop.md
├── 11-systemadmin.md
├── 12-shared.md                                 # BuildingBlocks/SharedKernel/ApiGateway
└── 13-architecture-assessment.md                # 阶段 3 架构整体评估
```

## 2 总体编排

采用"并行 + 聚合 + 评估"三阶段方案（方案 C + 阶段 3）。

```
阶段 1（并行，6+6 分两批）
   └─ 12 个 search subagent → 各自产出子报告

阶段 2（串行，主 agent）
   └─ 读取 12 份子报告 → 跨 BC 一致性分析（D1-D6）
   └─ 生成 00-summary.md（含健康度矩阵、热力分布、修复路线）

阶段 3（1 个 general_purpose_task subagent）
   └─ 读取 12 份子报告 + 00-summary.md + 架构文档
   └─ 产出 13-architecture-assessment.md
```

### 2.1 阶段 1：BC subagent 边界划分

| # | subagent | 扫描根目录（排除 *Tests*） |
|---|----------|---------------------------|
| 1 | UserAuth | `src/Services/UserAuth/Leno.UserAuth.{Domain,Application,Infrastructure,Api}/` |
| 2 | Product | `src/Services/Product/...` |
| 3 | Cart | `src/Services/Cart/...` |
| 4 | Order | `src/Services/Order/...` |
| 5 | Promotion | `src/Services/Promotion/...` |
| 6 | ReviewAfterSales | `src/Services/ReviewAfterSales/...` |
| 7 | PointsMembership | `src/Services/PointsMembership/...` |
| 8 | Payment | `src/Services/Payment/...` |
| 9 | Notification | `src/Services/Notification/...` |
| 10 | SellerShop | `src/Services/SellerShop/...` |
| 11 | SystemAdmin | `src/Services/SystemAdmin/...` |
| 12 | Shared | `src/BuildingBlocks/` + `src/ApiGateway/` + `src/BuildingBlocks/Leno.SharedKernel` + `src/BuildingBlocks/Leno.SharedContracts`（排除 `Leno.SharedContracts.Grpc/Generated/`） |

### 2.2 串并行约束

- 系统约束：单次最多 5 个并行 subagent
- 阶段 1 分两批执行：批 1 = BC1-BC6，批 2 = BC7-BC12 + Shared
- 阶段 2、3 严格串行依赖阶段 1

## 3 subagent 检查清单

每个 BC subagent 使用统一清单，按三大类逐项排查。

### 3.1 A. 功能正确性与 Bug

| # | 检查项 | 典型问题示例 |
|---|--------|------------|
| A1 | 空引用与边界条件 | `FirstOrDefault` 后未 null 检查直接访问；空集合 `First()` |
| A2 | 异常处理不当 | 吞掉异常 `catch {}`；抛 `Exception` 而非领域异常；未记录原始堆栈 |
| A3 | 并发与竞态 | 缺少乐观锁；缓存写入-读取竞态；`IDistributedCache` 非原子操作 |
| A4 | 状态机非法迁移 | Order/Payment/Refund/Shop 状态机遗漏中间态；非法跃迁未拒绝 |
| A5 | 边界条件 | 分页 `pageIndex=0` 或负数；金额为负；数量为 0；超长字符串 |
| A6 | 资源泄漏 | `IDisposable` 未释放；`HttpClient` 未复用；`CancellationToken` 未传递 |
| A7 | 异步消息可靠性 | 缺少幂等键；消费者失败未入死信；Outbox 未发布残留 |
| A8 | 事务边界 | 聚合多表写入未在同一事务；事件发布与状态写入非原子 |

### 3.2 B. DDD/架构合规

| # | 检查项 | 典型问题示例 |
|---|--------|------------|
| B1 | BC 边界泄露 | Application 层直接引用其他 BC 的 Domain/Repository；通过 ORM 导航属性跨 BC |
| B2 | 聚合设计违规 | 聚合间直接对象引用（应只持 ID）；聚合根外加载完整子图；事务跨越多个聚合 |
| B3 | 防腐层缺失/穿透 | 直接消费其他 BC 的领域模型；缺 ACL 转换；gRPC 直连绕过 ACL |
| B4 | 共享内核污染 | 在 `SharedKernel` 引入业务逻辑；跨 BC 共享具体聚合而非值对象 |
| B5 | CQRS 职责混乱 | Query 端写事务；Command 端直接拼 DTO；读模型与写模型混用 DbContext |
| B6 | 层依赖反向 | Domain 引用 Infrastructure；Api 引用 Infrastructure.Repository 实现 |
| B7 | 事件契约一致性 | 事件字段命名跨 BC 不一致；事件版本未在 `IntegrationEventBase` 标记 |
| B8 | 仓储滥用 | Repository 加业务方法；隐式 `IEnumerable` 加载导致 N+1 |

### 3.3 C. 性能与可靠性

| # | 检查项 | 典型问题示例 |
|---|--------|------------|
| C1 | N+1 查询 | 循环内 `Include` 之外访问导航属性；`SELECT` 后循环再 `SELECT` |
| C2 | 缺失索引 | 高频查询字段无索引；外键无索引导致锁升级 |
| C3 | 缓存策略 | 缓存键冲突；无过期；击穿/雪崩无保护；缓存未失效与数据库不一致 |
| C4 | 大对象/全表扫 | `ToList()` 全量加载；无 `Where`；缺 `IQueryable` 延迟执行 |
| C5 | 异步消息堆积 | 消费者吞吐低于生产；消息积压无背压；重试无退避 |
| C6 | Outbox/幂等性 | Outbox 表无清理策略；幂等键无 TTL 导致内存膨胀 |
| C7 | 资源/连接池 | `DbContext` 单例；`HttpClient` 每次新建；连接池耗尽 |
| C8 | 限流/熔断 | 缺熔断；熔断后无半开探测；缺限流导致下游过载 |

### 3.4 严重度评级

- **🔴 高**：导致数据不一致、资金损失、生产宕机、BC 边界彻底破坏
- **🟡 中**：特定场景下出问题、性能明显退化、维护成本高
- **🟢 低**：代码气味、改进建议、可读性优化

## 4 subagent 报告格式（强制）

每个 BC 必须按以下结构产出 Markdown：

```markdown
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
```

## 5 阶段 2：跨 BC 聚合分析

主 agent 读取全部 12 份子报告后执行：

### 5.1 D. 跨 BC 一致性分析

| # | 检查项 | 检测方法 |
|---|--------|---------|
| D1 | 事件契约对齐 | 同一事件类型在所有引用方有相同字段命名/类型；版本号统一 |
| D2 | ACL 模式重复 | 多个 BC 各自实现类似防腐层应抽取到 BuildingBlocks |
| D3 | 共享内核污染 | `SharedKernel` 中出现业务逻辑、聚合、特定 BC 的概念 |
| D4 | 跨域事务边界 | 涉及多 BC 的事务未走 Outbox/事件最终一致；同步调用链过长 |
| D5 | gRPC 与 REST 双轨一致性 | 同一能力在 proto 与 Controller 上定义不一致；返回语义不同 |
| D6 | 重复实现 | 多个 BC 各自实现类似工具未抽取到共享层 |

### 5.2 E. 全局视图

- BC 健康度对比矩阵（功能/DDD/性能三维雷达）
- 高风险问题热力分布（按 BC 与类别）
- 修复优先级建议（按严重度 × 影响范围 × 实现成本）

### 5.3 F. 修复路线建议

- **P0（立即修复）**：🔴 高风险且影响主链路（订单/支付/积分发放）
- **P1（短期修复）**：🔴 高风险但影响边缘 BC，或 🟡 中风险且影响主链路
- **P2（中长期）**：🟡 中风险 + 🟢 低风险，按 BC 分批治理

## 6 阶段 3：架构整体评估 subagent

### 6.1 subagent 类型

`subagent_type = general_purpose_task`（非 search）

原因：需要读取多个已生成文件、综合分析、产出长篇结构化文档，属于"编码/写作任务"而非"代码搜索"。该类型支持 Read/Write/Edit/WebSearch，可完成报告生成 + Web 搜索业界实践。

### 6.2 输入

- `docs/superpowers/specs/2026-07-21-code-audit/` 下全部 13 份报告
- `docs/spec/00-需求文档总览与DDD架构.md` 等 DDD 战略设计文档
- `docs/architecture/`、`docs/decisions/` 下的 ADR 与架构手册
- `src/` 顶层目录结构（不重读细节，依赖子报告）

### 6.3 输出

`13-architecture-assessment.md`，包含以下章节：

#### G. 系统架构整体评估

| 章节 | 内容 |
|------|------|
| G1 架构定位与成熟度 | 评估当前架构相对 DDD+CQRS+事件驱动+微服务目标的达成度，按百分比量化 |
| G2 架构优点 | 已做得好的设计（如 ACL 模式、Outbox、Consul 配置中心、Helm 部署等），每项说明价值与适用场景 |
| G3 架构缺点 | 系统级问题（如 BC 边界模糊处、共享内核污染、跨域事务边界不清、重复实现等），引用子报告证据 |
| G4 技术债清单 | 按"业务影响 × 修复成本"四象限分类，列出 Top 10 技术债 |
| G5 优化方案 | 按 3 个时间维度给出：<br>• **短期（1-2 周）**：零散修复，低风险高收益<br>• **中期（1-2 月）**：BC 边界整治、共享层抽取、跨 BC 事件治理<br>• **长期（3-6 月）**：架构演进方向（如读模型分离、CQRS 深化、可观测性升级） |
| G6 风险评估 | 列出未修复状态下 top 5 生产风险（资金损失/数据不一致/系统宕机等），含触发条件与影响范围 |
| G7 与业界实践对比 | 对比 Microsoft eShopOnContainers、亚马逊电商参考架构等的差距与优势 |

### 6.4 依赖与触发

- 仅在阶段 1 全部 12 个 subagent 完成、阶段 2 汇总报告生成后启动
- 若阶段 1 有 BC 失败，阶段 3 仍可启动，但需在报告中标注数据缺口

## 7 执行约束

1. **subagent 并行上限**：单次最多 5 个并行，阶段 1 分两批（6+6）
2. **subagent prompt 必须**：
   - 明确路径边界（避免越界扫描）
   - 明确排除项（Tests、Generated、Migrations Designer）
   - 嵌入检查清单（A1-A8、B1-B8、C1-C8）
   - 嵌入报告格式模板
   - 强制要求引用代码位置 `file:///...#L` 格式
3. **失败处理**：单个 subagent 失败时主 agent 记录并跳过，不阻塞其他 BC；失败 BC 在汇总报告中标注"分析未完成"
4. **不修改代码**：本次仅为分析，不写业务代码、不动业务文件
5. **完成后 git 提交**：报告目录提交到 git，commit message 用中文

## 8 验收标准

- [ ] 14 份报告文件全部生成（README + 12 BC/Shared + 00-summary + 13-architecture-assessment）
- [ ] 每份 BC 报告含概述、🔴/🟡/🟢 三档问题清单、健康度评分
- [ ] 所有问题引用代码位置以 `file:///` 链接格式呈现
- [ ] 00-summary.md 包含 D1-D6 跨 BC 分析、E 全局视图、F 修复路线
- [ ] 13-architecture-assessment.md 包含 G1-G7 全部章节
- [ ] 全部报告已 git 提交，commit message 为中文

## 9 后续动作

设计批准并提交后，转入 writing-plans 编写实施计划，将三阶段拆分为可执行的步骤列表。
