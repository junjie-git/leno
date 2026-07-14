# Leno 代码库审计执行实施计划（计划 1）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 按 spec `2026-07-15-comprehensive-code-audit-design.md` 执行 4 阶段审计，填充 spec 附录 A/B/C，产出完整问题清单与修复方案。

**Architecture:** 4 阶段流水线：阶段 1 模式扫描（Grep/SearchCodebase 并行）→ 阶段 2 候选复核与分类（Read 上下文+决策树）→ 阶段 3 spec 差距分析（11 BC × 4 类清单全量核查）→ 阶段 4 修复方案编排（合并清单+标注既有 spec+批次分组）。全程只读，不修改 `src/` 任何文件。

**Tech Stack:** Grep（模式匹配）、SearchCodebase（语义查询）、Read（上下文复核）、Write（最终填充 spec 附录）、bash（批量扫描辅助）

**关联文档:**
- 设计 spec：`docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md`
- 本计划填充该 spec 的附录 A（规则命中原始数据）、附录 B（问题清单+修复方案）、附录 C（正向差距清单）

**关键约束:**
- 全程只读 `src/`，不修改任何业务代码
- 阶段 1/2/3 禁止读取既有优化 spec（`docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md` 与 `.trae/specs/*/spec.md`）
- 阶段 4 才允许读取既有优化 spec 做标注
- 每条问题必须含"命中规则 + 文件:行号 + 命中代码片段"三要素
- 工具调用预算 200-300 次，通过并行压缩

---

## 阶段 1：自动化模式扫描

### Task 1: 扫描类别 1 — 未实现业务代码（R1.1-R1.9）

**Files:**
- Scan: `src/**/*.cs`（排除 `*Tests*`、`obj/`、`bin/`）
- Output: 填充 `docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md` 附录 A 的 R1.* 部分

- [ ] **Step 1: 并行执行 R1.1-R1.9 的 Grep 扫描**

  对 `src/` 执行以下 9 条 Grep（并行，output_mode=content，-n=true，glob="*.cs"）：

  | 规则 | Grep pattern | 说明 |
  |---|---|---|
  | R1.1 | `// TODO\|// FIXME\|// 待实现\|// 待补充\|// 暂未实现\|// 待完善\|// 待开发` | 未实现标记 |
  | R1.2 | `// 应该\|// 应当\|// 需要\|// 这里应该\|// 此处应\|// 实际应\|// 真实应` | 注释意图（主场景） |
  | R1.3 | `_logger\.Log` | 日志调用（候选，需阶段 2 判定是否"日志即实现"） |
  | R1.4 | `return Task\.CompletedTask\|return Task\.FromResult` | 空任务占位 |
  | R1.5 | `await Task\.Delay` | 假异步占位 |
  | R1.6 | `return default\|return null\|return default!\|return null!` | 空返回占位 |
  | R1.7 | `throw new NotImplementedException\|throw new NotSupportedException` | 显式未实现 |
  | R1.8 | `模拟\|mock\|假数据\|临时\|stub\|placeholder\|dummy` | 模拟数据伪装 |
  | R1.9 | `=> throw new NotImplementedException` | 表达式体未实现 |

  对每条 Grep 在结果中排除 `*Tests*` 目录（R1.7 除外，需检查测试中是否误用）。

- [ ] **Step 2: 汇总 R1.* 命中数据到附录 A**

  将每条规则的命中格式化为附录 A 条目：
  ```
  ### R1.X: <规则说明>
  - Grep 命令: `grep -rn "<pattern>" --include="*.cs" --exclude-dir=obj --exclude-dir=bin src/`
  - 命中文件数: N
  - 命中行数: M
  - 前 10 条命中样本:
    1. src/path/file.cs:LL - <命中代码>
    2. ...
  ```
  写入 spec 附录 A 的"类别 1：未实现业务代码"小节。

- [ ] **Step 3: Commit 阶段 1 类别 1 扫描结果**

  ```bash
  git add docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md
  git commit -m "audit: phase 1 scan category 1 (R1.1-R1.9) unimplemented code"
  ```

### Task 2: 扫描类别 3 — 架构反模式（R3.1-R3.6, R3.9-R3.11）

**Files:**
- Scan: `src/**/*.cs`、`src/**/*.csproj`
- Output: 填充附录 A 的 R3.* 部分

- [ ] **Step 1: 并行执行 R3.1-R3.6, R3.9-R3.11 的扫描**

  | 规则 | 工具 | pattern / 查询 | 范围 |
  |---|---|---|---|
  | R3.1 | Grep | `ProjectReference.*\.Domain\|ProjectReference.*\.Application` | `src/Services/*/Leno.*.Infrastructure/*.csproj` |
  | R3.2 | Grep | `using Leno\.\w+\.Domain` | `src/Services/**/*.cs`（排除 Testing） |
  | R3.3 | Grep | `using Leno\.\w+\.Application` | `src/Services/**/*.cs`（排除 Testing） |
  | R3.4 | Grep | `EF\|DbContext\|Microsoft\.EntityFrameworkCore\|SqlClient\|HttpStatusCode` | `src/BuildingBlocks/Leno.SharedKernel/**/*.cs` |
  | R3.5 | Grep | `Microsoft\.EntityFrameworkCore\|StackExchange\.Redis\|RabbitMQ\|MassTransit` | `src/Services/*/Leno.*.Domain/*.csproj` |
  | R3.6 | Glob + Read | 列出所有 `UnitOfWork.cs` 与 `Program.cs`，diff 比对相似度 | `src/Services/**/UnitOfWork.cs`、`src/Services/*/Leno.*.Api/Program.cs` |
  | R3.9 | Grep | `async\s+\w+.*\{[^}]*\}` 无 `await`（人工判定） | `src/**/*.cs`（排除 Tests） |
  | R3.10 | Grep | `static readonly.*List<\|static readonly.*Dictionary<\|static readonly.*HashSet<` | `src/**/*.cs` |
  | R3.11 | Grep | `static.*new List<\|static.*new Dictionary<\|static.*new HashSet<` | `src/**/*.cs` |

  R3.1 需人工判定 ProjectReference 是否为"其他 BC"（非本 BC 的 Domain/Application）。

- [ ] **Step 2: 汇总 R3.* 命中数据到附录 A**

  格式同 Task 1 Step 2，写入附录 A 的"类别 3：架构反模式"小节。

- [ ] **Step 3: Commit 阶段 1 类别 3 扫描结果**

  ```bash
  git add docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md
  git commit -m "audit: phase 1 scan category 3 (R3.1-R3.11) architecture anti-patterns"
  ```

### Task 3: 扫描类别 4 — 冗余代码（R4.1-R4.8）

**Files:**
- Scan: `src/**/*.cs`、`src/**/*.csproj`
- Output: 填充附录 A 的 R4.* 部分

- [ ] **Step 1: 并行执行 R4.1-R4.8 的扫描**

  | 规则 | 工具 | pattern / 方法 | 范围 |
  |---|---|---|---|
  | R4.1 | Read | 读取命中 R3.6 的高相似文件，标记文件内 3+ 重复块 | 同 R3.6 文件集 |
  | R4.2 | Glob | 列出跨 BC 同名文件（`UnitOfWork.cs`、`GlobalUsings.cs`、`appsettings.json`） | `src/**/{UnitOfWork,GlobalUsings}.cs`、`src/**/appsettings*.json` |
  | R4.3 | SearchCodebase | 查询"private/internal 方法无调用方"（分批查高频 private 方法名） | `src/**/*.cs`（排除 Tests） |
  | R4.4 | SearchCodebase | 查询"private/internal 字段无读取" | `src/**/*.cs`（排除 Tests） |
  | R4.5 | Grep | `#if DEBUG` 与多行注释块 `/\*[\s\S]*\*/` > 5 行 | `src/**/*.cs` |
  | R4.6 | Grep | `class \w+\s*\{\s*\}` 空类 | `src/**/*.cs` |
  | R4.7 | Grep | `using ` 后未在文件内引用（人工抽样判定） | `src/**/*.cs` |
  | R4.8 | Glob + Read | 列出所有 `*Tests*` 项目，检查是否仅含 `GlobalUsings.cs` | `src/**/*Tests*/` |

  R4.3/R4.4 工作量大，优先扫 Application/Infrastructure 层的 private 方法。

- [ ] **Step 2: 汇总 R4.* 命中数据到附录 A**

  格式同 Task 1 Step 2，写入附录 A 的"类别 4：冗余代码"小节。

- [ ] **Step 3: Commit 阶段 1 类别 4 扫描结果**

  ```bash
  git add docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md
  git commit -m "audit: phase 1 scan category 4 (R4.1-R4.8) redundant code"
  ```

### Task 4: 扫描类别 2 — 缺失功能辅助（R2.1, R2.4）+ 待观察项（R3.7, R3.8）

**Files:**
- Scan: `src/**/*.cs`
- Output: 填充附录 A 的 R2.* 与待观察项部分

- [ ] **Step 1: 执行 R2.1 接口实现反查**

  用 SearchCodebase 查询每个 BC.Application 的 `IXxxAppService` 接口的所有实现类：
  - 查询：`"查找 IXxxAppService 的所有实现类"`（逐个 BC 执行）
  - 比对：接口方法 vs 实现类方法，标记"接口声明但无实现"的方法

  11 个 BC 的接口列表（从 Glob `src/Services/*/Leno.*.Application/IXxx*AppService.cs` 获取）：
  - Cart: ICartAppService, IAnonymousCartAppService
  - Notification: INotificationAppService, IDeadLetterAppService, INotificationConfigAppService, INotificationPreferenceAppService, INotificationRecordAppService, INotificationTemplateAppService, IRateLimitAppService
  - Order: IOrderAppService, IFreightTemplateAppService, ILogisticsCompanyAppService, IOrderInternalQueryService
  - Payment: IPaymentAppService, IPaymentChannelConfigAppService, IPaymentInternalQueryService, IReconciliationAppService
  - PointsMembership: IPointsAppService, IExchangeCouponAppService, IMemberAppService, IMembershipPackageAppService, IPointsInternalAppService, ITaskAppService
  - Product: ISPUAppService, IBrandAppService, ICategoryAppService, IInventoryAppService, IProductInternalQueryService, IProductSearchService
  - Promotion: IPromotionCalculateAppService, ISeckillAppService, IAppServices
  - ReviewAfterSales: IAfterSalesAppService, IReviewAppService
  - SellerShop: ISellerAppService, ISellerDashboardAppService, IShopAppService
  - SystemAdmin: IAnnouncementAppService, IAuditLogAppService, IAuditLogEntryAppService, IDataDictionaryAppService, IDeadLetterAppService, IFeatureFlagAppService, IHealthAppService, IIndexRebuildAppService
  - UserAuth: （需 Glob 确认）

- [ ] **Step 2: 执行 R2.4 功能禁用标记扫描**

  Grep pattern: `// 暂不支持\|// 未启用\|// 已禁用\|// 跳过`，范围 `src/**/*.cs`（排除 Tests）。

- [ ] **Step 3: 执行 R3.7/R3.8 待观察项扫描**

  | 规则 | Grep pattern | 范围 |
  |---|---|---|
  | R3.7 | `catch\s*\(\s*Exception` 后续无 `throw`（人工判定） | `src/**/*.cs`（排除 Tests、Program.cs） |
  | R3.8 | 防腐层 catch 后 `return null\|return default\|return Array\.Empty\|return new List` | `src/Services/*/Leno.*.Infrastructure/Services/**/*.cs` |

  R3.7 先 Grep `catch\s*\(\s*Exception` 产出候选，阶段 2 复核时判定是否无 throw。

- [ ] **Step 4: 汇总 R2.* 与待观察项到附录 A**

  写入附录 A 的"类别 2：缺失功能辅助"与"待观察项"小节。

- [ ] **Step 5: Commit 阶段 1 类别 2 与待观察项扫描结果**

  ```bash
  git add docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md
  git commit -m "audit: phase 1 scan category 2 (R2.1-R2.4) + watchlist (R3.7-R3.8)"
  ```

---

## 阶段 2：候选复核与分类

### Task 5: 复核 R1.* 候选，分类未实现业务代码

**Files:**
- Read: 阶段 1 R1.* 命中的所有候选文件上下文
- Output: 填充附录 B 的"批次 2: 核心功能补全"中 AUDIT-IMPL-NNN 条目

- [ ] **Step 1: 逐条复核 R1.2 候选（主场景，优先）**

  对 R1.2 每条命中：
  1. Read 命中文件，读取命中行前后 20-40 行
  2. 按 spec 4.1 决策树判定：
     - Q1: 方法是否完成声明职责？是→误报；否→Q2
     - Q2: 是否有意图注释但缺业务调用？是→分类"未实现业务代码"
  3. 若分类为真问题，按 spec 4.2 定级（P0/P1/P2）
  4. 按 spec 4.3 字段格式记录到附录 B

  候选量大时（> 50），按 BC 分批处理，每批 10 条。

- [ ] **Step 2: 逐条复核 R1.1, R1.3-R1.9 候选**

  对其余 R1.* 规则的命中，按同样决策树流程复核。
  - R1.1（TODO/FIXME）：多数为真问题
  - R1.3（日志即实现）：需读上下文判定方法体是否仅含 Log+return
  - R1.4/R1.5/R1.6：按 spec 4.4 误报原则排除合法场景（Dispose、TryGet、Cancellation 回调）
  - R1.7/R1.9：既有脚本已覆盖，预期 0 命中，确认即可
  - R1.8：需读上下文判定是否"模拟数据后接 return"

- [ ] **Step 3: 汇总 AUDIT-IMPL-NNN 条目到附录 B**

  按编号顺序写入附录 B"批次 2"小节，每条含：
  ```
  #### AUDIT-IMPL-NNN: <问题标题>
  - 类别: 未实现业务代码
  - 严重级: P0/P1/P2
  - 命中规则: R1.X
  - 位置: src/path/file.cs:起始行-结束行
  - 证据: <命中代码片段>
  - 根因: <一句话>
  - 影响范围: <调用方/端点数>
  ```
  修复方案字段在阶段 4 补充。

- [ ] **Step 4: Commit 阶段 2 R1.* 复核结果**

  ```bash
  git add docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md
  git commit -m "audit: phase 2 review R1.* candidates, classify unimplemented code"
  ```

### Task 6: 复核 R3.* 候选，分类架构反模式

**Files:**
- Read: 阶段 1 R3.* 命中的所有候选文件上下文
- Output: 填充附录 B 的"批次 1: 架构合规修复"中 AUDIT-ARCH-NNN 条目

- [ ] **Step 1: 复核 R3.1-R3.5 候选（边界与分层违规，P0 优先）**

  对每条命中：
  1. Read 对应 `.csproj` 文件确认 ProjectReference
  2. 判定是否跨 BC 引用（非本 BC 的 Domain/Application）
  3. 按 spec 4.1 决策树 Q4 判定为"架构反模式"
  4. 按 spec 4.2 定级（边界穿透/领域层泄漏基础设施 = P0）
  5. 记录到附录 B"批次 1"

  特别关注：
  - R3.1: Notification.Infrastructure 是否引用 Promotion.Domain / PointsMembership.Domain
  - R3.4: SharedKernel 是否含 EF/DbContext/HttpStatusCode
  - R3.5: 任何 Domain.csproj 是否引用 EF/Redis/MQ

- [ ] **Step 2: 复核 R3.6, R3.9-R3.11 候选**

  - R3.6: Read 所有 `UnitOfWork.cs` 与 `Program.cs`，diff 比对相似度，标记重复 > 80% 的文件组
  - R3.9: 对 `async` 方法候选，读上下文确认无 `await`
  - R3.10/R3.11: 读上下文确认可变集合无保护

- [ ] **Step 3: 汇总 AUDIT-ARCH-NNN 条目到附录 B**

  P0 的写入"批次 1"，P1/P2 的写入"批次 3"。

- [ ] **Step 4: Commit 阶段 2 R3.* 复核结果**

  ```bash
  git add docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md
  git commit -m "audit: phase 2 review R3.* candidates, classify architecture anti-patterns"
  ```

### Task 7: 复核 R4.* 候选，分类冗余代码

**Files:**
- Read: 阶段 1 R4.* 命中的所有候选文件上下文
- Output: 填充附录 B 的"批次 3: 代码质量优化"中 AUDIT-REDUN-NNN 条目

- [ ] **Step 1: 复核 R4.1-R4.2 候选（跨文件重复）**

  - R4.2: Read 跨 BC 同名文件（UnitOfWork.cs、GlobalUsings.cs），diff 比对，标记重复 > 80% 的文件组
  - R4.1: 在 R4.2 标记的文件内，查找 3+ 处相同代码块

- [ ] **Step 2: 复核 R4.3-R4.4 候选（死代码）**

  对 R4.3/R4.4 的 SearchCodebase 反查结果：
  - 确认 private/internal 方法/字段在全文搜索中无调用方
  - 排除反射调用、序列化用途（如 EF Core shadow property）
  - 记录到附录 B"批次 3"

- [ ] **Step 3: 复核 R4.5-R4.8 候选**

  - R4.5: Read 注释块上下文，确认 > 5 行死代码
  - R4.6: 确认空类
  - R4.7: 抽样 20 个 using 未使用候选
  - R4.8: 列出仅含 GlobalUsings.cs 的 Tests 项目

- [ ] **Step 4: 汇总 AUDIT-REDUN-NNN 条目到附录 B"批次 3"**

- [ ] **Step 5: Commit 阶段 2 R4.* 复核结果**

  ```bash
  git add docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md
  git commit -m "audit: phase 2 review R4.* candidates, classify redundant code"
  ```

### Task 8: 复核 R3.7/R3.8 待观察项

**Files:**
- Read: 阶段 1 R3.7/R3.8 命中的候选文件上下文
- Output: 填充附录 B 的"批次 4: 待观察项标注"

- [ ] **Step 1: 复核 R3.7 候选（吞异常）**

  对每条 `catch (Exception` 命中：
  1. Read 上下文确认 catch 块内无 `throw`
  2. 按 spec 4.4 排除顶层兜底（Program.cs、BackgroundService 主循环）的误报
  3. 非误报的记录到附录 B"批次 4"，格式：
     ```
     #### WATCH-NNN: <位置简述>
     - 位置: src/path/file.cs:LL
     - 命中代码: <catch 块片段>
     - 标注建议: // AUDIT-NOTE: 此处吞异常，建议改为抛 DomainException + 告警
     ```
  4. 不定严重级、不进 P0/P1/P2 排序

- [ ] **Step 2: 复核 R3.8 候选（静默兜底）**

  对防腐层 catch 后 return 空值的命中：
  1. Read 上下文确认是防腐层服务（Services/ 目录下）
  2. 确认 catch 后 return null/default/空集合
  3. 记录到附录 B"批次 4"，标注建议：
     `// AUDIT-NOTE: 此处静默兜底，建议改为抛 DomainException + 告警，避免数据不一致被掩盖`

- [ ] **Step 3: Commit 阶段 2 待观察项复核结果**

  ```bash
  git add docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md
  git commit -m "audit: phase 2 review R3.7/R3.8 watchlist items"
  ```

---

## 阶段 3：Spec 与实现差距分析

### Task 9: 阶段 3 准备 — 提取 11 个 BC 的 spec 清单

**Files:**
- Read: `docs/spec/01-用户与认证授权域.md` 至 `docs/spec/09-消息通知集成.md`、`11-卖家与店铺管理域.md`、`12-系统管理域.md`（排除 00、10）
- Output: 临时清单（记录在审计工作笔记中，最终汇入附录 B 的 AUDIT-MISS 条目）

- [ ] **Step 1: 逐个 Read 11 篇 BC spec，提取 4 类清单**

  对每个 BC 的 spec 文档执行：
  1. Read `docs/spec/{NN}-{BC名}.md`
  2. 提取并记录：
     - E_spec: 端点清单（HTTP 路径 + 方法 + 功能描述）
     - A_spec: AppService 清单（接口名 + 方法名 + 功能描述）
     - V_spec: 事件清单（事件类型 + 发布方 + 订阅方）
     - R_spec: 领域规则清单（规则描述 + 适用聚合/值对象）
  3. 若 spec 描述模糊无法提取，记录"spec 解析受阻"，跳过该清单类型

  11 个 BC 顺序：01-用户认证、02-商品、03-购物车、04-订单交易、05-促销、06-评价售后、07-积分会员、08-支付集成、09-消息通知、11-卖家店铺、12-系统管理。

- [ ] **Step 2: Commit 阶段 3 清单提取进度**

  ```bash
  # 此步不修改 spec 文档，仅记录工作笔记
  # 进度提交到工作笔记或临时文件（审计完成后删除）
  echo "phase 3 spec extraction done for 11 BCs" >> /tmp/audit-progress.log
  ```

### Task 10: 阶段 3 执行 — 逐 BC 求差集

**Files:**
- Scan: 各 BC 的 Api/Application/Domain/Infrastructure 代码
- Output: 填充附录 B 的"批次 2"中 AUDIT-MISS-NNN 条目 + 附录 C 正向差距

- [ ] **Step 1: 对每个 BC 执行差距分析（11 个 BC 顺序处理）**

  对每个 BC：
  1. Grep `app\.Map(Get|Post|Put|Delete)` 扫描 BC.Api 的端点 → E_code
  2. Glob `src/Services/{BC}/Leno.*.Application/IXxx*.cs` + Read 接口与实现 → A_code
  3. Grep `IntegrationEvent\|DomainEvent` 扫描 SharedContracts/Events 与各 Consumer → V_code
  4. Read Domain 聚合根/值对象的校验方法 → R_code
  5. 求差集：
     - E_spec - E_code → 缺失端点
     - A_spec - A_code → 缺失 AppService 方法
     - V_spec - V_code → 缺失事件
     - R_spec - R_code → 缺失领域规则（全量核查，不抽样）
  6. 按 spec 5.3 判定细则定级：
     - 部分实现（内部方法存在但无端点）→ P1
     - 实现但禁用（#if DEBUG 或配置开关）→ P2
     - 完全缺失 → P0 或 P1（按 spec 4.2）
  7. 代码有但 spec 未描述的 → 附录 C 正向差距

- [ ] **Step 2: 汇总 AUDIT-MISS-NNN 条目到附录 B"批次 2"**

  每条差距条目格式：
  ```
  #### AUDIT-MISS-NNN: <缺失功能简述>
  - 类别: 缺失功能
  - 严重级: P0/P1/P2
  - 位置: docs/spec/{NN}-{BC}.md#<章节> + 预期代码位置 src/path/
  - 证据: <spec 原文摘录>
  - 根因: spec 声明但代码未实现
  - 影响范围: <受影响的业务流程>
  - 差距类型: 缺失端点 / 缺失AppService / 缺失事件 / 缺失领域规则
  ```

- [ ] **Step 3: 汇总正向差距到附录 C**

  记录代码有但 spec 未描述的能力，仅记录不判问题。

- [ ] **Step 4: Commit 阶段 3 结果**

  ```bash
  git add docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md
  git commit -m "audit: phase 3 spec gap analysis for 11 BCs, fill AUDIT-MISS entries"
  ```

---

## 阶段 4：修复方案编排

### Task 11: 阶段 4 准备 — 读取既有优化 spec 做标注

**Files:**
- Read: `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md`、`.trae/specs/*/spec.md`
- Output: 为附录 B 每条问题追加"既有 spec 标注"字段

- [ ] **Step 1: Read 既有优化 spec 建立问题索引**

  Read 以下文档，提取已识别问题清单：
  - `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md`（9 大主线）
  - `.trae/specs/replace-placeholder-implementations/spec.md`（30 处占位）
  - `.trae/specs/refactor-to-microservices/spec.md`
  - `.trae/specs/p0-task-completion/spec.md`
  - `.trae/specs/p1-task-completion/spec.md`
  - `.trae/specs/p2-task-completion/spec.md`

  为每条既有问题建立索引：问题描述 + 章节号 + 验收状态。

- [ ] **Step 2: 对附录 B 每条问题执行既有 spec 比对**

  按 spec 6.6 规则：
  1. 在既有 spec 索引中检索相似问题
  2. 命中则追加：
     - 既有 spec 覆盖: 是
     - 既有 spec 引用: 文档路径#章节号
     - 既有 spec 状态: 已修复 / 部分修复 / 未修复（通过 Grep 代码复扫判定，不轻信验收勾选）
  3. 未命中则追加：既有 spec 覆盖: 否（新发现问题）

### Task 12: 阶段 4 执行 — 为每条问题补充修复方案

**Files:**
- Modify: `docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md` 附录 B
- Output: 每条问题追加修复方案字段

- [ ] **Step 1: 为"批次 1: 架构合规修复"问题补充修复方案**

  对批次 1 的每条 AUDIT-ARCH-NNN（P0 架构反模式）：
  1. 按 spec 6.2 策略选择修复方向（重构到合规结构，最小侵入）
  2. 追加字段：
     ```
     修复方案:
       方向: <一句话思路>
       影响文件: <绝对路径列表>
       代码 sketch: <伪代码，≤15 行>
       依赖问题: <问题编号或"无">
       验证方式: 编译验证 + 既有测试全绿 + Grep 复扫
       风险: <副作用或"低">
     ```
  3. 架构反模式类不强制新增测试（按 spec 6.5）

- [ ] **Step 2: 为"批次 2: 核心功能补全"问题补充修复方案**

  对批次 2 的每条 AUDIT-IMPL-NNN 与 AUDIT-MISS-NNN：
  1. 按 spec 6.2 策略：
     - 未实现业务代码：补全真实业务调用，删除意图注释
     - 缺失功能：按 spec 补全端点/AppService/事件/校验
  2. 追加修复方案字段（同 Step 1 格式）
  3. 验证方式必须含"新增单元测试"（按 spec 6.5，功能类问题强制）

- [ ] **Step 3: 为"批次 3: 代码质量优化"问题补充修复方案**

  对批次 3 的每条 AUDIT-REDUN-NNN 与剩余 AUDIT-ARCH-NNN：
  1. 按 spec 6.2 策略：删除或合并到公共位置
  2. 追加修复方案字段
  3. 验证方式：编译验证 + 既有测试全绿（不强制新增测试）

- [ ] **Step 4: 确认批次 4 待观察项无需修复方案**

  批次 4 的 WATCH-NNN 条目仅含标注建议，不编排修复方案（按 spec 4.5）。

- [ ] **Step 5: Commit 阶段 4 修复方案**

  ```bash
  git add docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md
  git commit -m "audit: phase 4 add fix plans for all issues across 4 batches"
  ```

### Task 13: 阶段 4 收尾 — 验收检查与文档定稿

**Files:**
- Read: `docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md`（完整）
- Output: 验收 checklist 通过，文档定稿

- [ ] **Step 1: 执行 spec 8.2 验收 checklist**

  逐项核对：
  - [ ] 范围完整性：7.1 全部范围已扫描，11 BC 无遗漏
  - [ ] 规则覆盖：R1.1-R1.9、R2.1-R2.4、R3.1-R3.11、R4.1-R4.8 均有附录 A 记录
  - [ ] 证据完整性：抽查 10 条问题，三要素齐全
  - [ ] 分类一致性：抽查 10 条问题按决策树重新分类，一致率 ≥ 90%
  - [ ] spec 差距覆盖：11 BC × 4 类清单均有记录，领域规则全量核查
  - [ ] 修复方案可执行：每条问题含方向+影响文件+sketch+验证方式
  - [ ] 既有 spec 标注：每条问题有"既有 spec 覆盖"字段
  - [ ] 批次划分：4 批次组织，依赖无环

- [ ] **Step 2: 修复验收发现的缺陷**

  对未通过的验收项：
  - 证据缺失：补 Read 上下文，补全三要素
  - 分类不一致：重新走决策树，修正分类
  - 修复方案不全：补全字段

- [ ] **Step 3: 更新 spec 文档版本号**

  将 spec 头部"文档版本：V1.0"改为"V1.1（审计已执行）"。

- [ ] **Step 4: Commit 最终定稿**

  ```bash
  git add docs/superpowers/specs/2026-07-15-comprehensive-code-audit-design.md
  git commit -m "audit: finalize spec v1.1 with completed audit results in appendices A-C"
  ```

- [ ] **Step 5: 输出审计总结**

  在 spec 末尾追加"审计总结"小节：
  - 总问题数：N（按类别、严重级、批次三维统计）
  - 既有 spec 覆盖率：X%
  - 新发现问题数：M
  - 各批次问题数分布
  - 推荐实施顺序：批次 1 → 2 → 3（批次 4 可并行）

---

## 计划 2 预留：修复执行计划

> **说明**：本计划（计划 1）完成审计并填充 spec 附录 A/B/C 后，需另写一份"修复执行计划"（计划 2），将附录 B 的修复方案拆解为可执行的任务卡片。
>
> 计划 2 将在审计完成后基于附录 B 的实际问题清单编写，包含：
> - 按 spec 6.4 的 4 批次组织任务
> - 每个修复方案拆为：写失败测试 → 实现 → 运行测试 → Commit（TDD 流程）
> - 任务卡片含：问题编号、影响文件、代码 sketch、验证命令、依赖任务
> - 计划 2 路径：`docs/superpowers/plans/2026-07-15-audit-fix-execution.md`（审计后创建）
>
> 当前不预写计划 2，因其内容依赖审计发现的具体问题。

---

## Self-Review

**1. Spec 覆盖检查：**

| Spec 章节 | 覆盖任务 | 状态 |
|---|---|---|
| 2.1 阶段 1 模式扫描 | Task 1-4 | ✓ 全部规则覆盖 |
| 2.2 阶段 2 候选复核 | Task 5-8 | ✓ 4 类问题+待观察项覆盖 |
| 2.3 阶段 3 spec 差距 | Task 9-10 | ✓ 11 BC × 4 类清单覆盖 |
| 2.4 阶段 4 修复编排 | Task 11-13 | ✓ 标注+修复方案+验收覆盖 |
| 3.1-3.4 规则集 R1-R4 | Task 1-4 扫描, Task 5-8 复核 | ✓ |
| 4.1 决策树 | Task 5-8 复核步骤 | ✓ |
| 4.4 误报原则 | Task 5-8 复核步骤引用 | ✓ |
| 4.5 待观察项 | Task 8 | ✓ |
| 5.1-5.4 spec 差距全量 | Task 9-10 | ✓ 全量核查 |
| 6.1 修复方案模板 | Task 12 | ✓ |
| 6.4 批次划分 | Task 12 + 附录 B 组织 | ✓ |
| 6.5 验证策略 | Task 12 修复方案字段 | ✓ |
| 6.6 既有 spec 标注 | Task 11 | ✓ |
| 7.1-7.5 范围与约束 | 全计划遵守 | ✓ |
| 8.2 验收标准 | Task 13 | ✓ |

**2. 占位符扫描：** 无 TBD/TODO，所有步骤含具体 Grep pattern、文件路径、命令。计划 2 为合理预留（依赖审计结果）。

**3. 类型一致性：** 问题编号前缀（AUDIT-IMPL/MISS/ARCH/REDUN + WATCH）在 Task 5-8 定义，Task 11-12 引用一致。

---

**Plan complete and saved to `docs/superpowers/plans/2026-07-15-comprehensive-code-audit-execution.md`.**

两种执行方式：

**1. Subagent-Driven（推荐）** — 每个 Task 派发独立 subagent，任务间复核，迭代快

**2. Inline Execution** — 在当前会话按批次执行，带检查点复核

选择哪种方式？
