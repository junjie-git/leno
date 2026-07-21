# 代码审计修复 subagent 编排执行设计

**日期**：2026-07-22
**输入**：[2026-07-22-fix-by-bc/](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/) 13 份修复实施计划
**前置计划**：[2026-07-22-fix-by-bc.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc.md)
**目标**：编排 26 个 `general_purpose_task` subagent（1 跨BC P0 + 12 BC P0 + 12 BC P1P2 + 1 跨BC P1P2），分 4 波实施全部 401 项代码修复

---

## 1. 范围与约束

### 1.1 实施范围

全量 401 项修复（已排除 43 项 [ALREADY-FIXED]）：

| 类别 | P0 | P1 | P2 | 合计 |
|------|-----|-----|-----|------|
| 12 BC 计划 | 108 | 166 | 99 | 373（含 35 已修复跳过，实际 338 待实施） |
| 跨 BC 计划 | 8 | ~25 | ~30 | 63 待实施 |
| **合计** | **116** | **~191** | **~129** | **401** |

### 1.2 关键约束

- **无 dotnet SDK**：沙箱环境无法安装 dotnet 10.0.301（网络限制导致下载失败）。subagent 按计划写入测试代码 + 修复代码并提交，跳过 `dotnet test` / `dotnet build` 验证。commit message 标注 `[unverified]`。编译与测试验证推迟到 CI。
- **5 并行限制**：每批最多 4 个 subagent（留 1 个 slot 给主 agent 操作）。
- **git 冲突避免**：subagent 各自 `git add` 自己 BC 目录的文件 + `git commit`（不 push），主 agent 在每批完成后统一 `git push`。BC 目录互斥，无文件冲突。
- **代码完整性**：遵循用户代码完整性强制契约——禁止占位符、TODO、空实现、截断输出。每函数完整实现。

### 1.3 执行顺序决策

跨 BC P0 优先：fix-13 的 8 个 P0 项修改共享层代码（`SharedContracts`、`SharedKernel`、`Leno.Infrastructure`），必须先于 BC 修复完成，确保 BC subagent 能直接引用更新后的契约。

---

## 2. 总体架构

4 波串行编排，波内并行 subagent：

```
Wave 1 (串行, 1 agent)     Wave 2 (3 批 × 4 并行)     Wave 3 (3 批 × 4 并行)     Wave 4 (串行, 1 agent)
┌──────────────────┐       ┌──────────────────┐       ┌──────────────────┐       ┌──────────────────┐
│ fix-13 跨BC P0   │       │ 12 BC P0 (108项) │       │ 12 BC P1+P2(265)│       │ fix-13 跨BC P1+P2│
│ 8 项共享层修复    │ ────► │ Batch 2a: 4 agent│ ────► │ Batch 3a: 4 agent│ ────► │ ~55 项           │
│ SharedContracts  │       │ Batch 2b: 4 agent│       │ Batch 3b: 4 agent│       │ D1-D6 P1/P2     │
│ SharedKernel     │       │ Batch 2c: 4 agent│       │ Batch 3c: 4 agent│       │ G4/G5/G6        │
│ Infrastructure   │       └──────────────────┘       └──────────────────┘       └──────────────────┘
└──────────────────┘
```

**subagent 总数**：1 + 12 + 12 + 1 = 26 个 `general_purpose_task` subagent
**波数**：4 波串行，波内 3 批 × 4 并行

### BC 分批策略（Wave 2/3 共用，按 P0 数量均衡）

| 批次 | BC | Wave 2 P0 项数 | Wave 3 P1+P2 项数 |
|------|-----|---------------|-------------------|
| Batch a | UserAuth, Order, Notification, Promotion | 15+13+12+11=51 | 31+23+35+23=112 |
| Batch b | ReviewAfterSales, Shared, PointsMembership, SystemAdmin | 11+10+8+7=36 | 20+29+16+15=80 |
| Batch c | Payment, Product, Cart, SellerShop | 6+5+5+5=21 | 14+15+25+19=73 |

---

## 3. Wave 1 — 跨 BC P0 共享层修复

**1 个 subagent，8 个 P0 项，修改共享代码 + 多 BC 文件**

### 3.1 P0 项清单

| # | 问题 ID | 修改范围 | 影响 BC |
|---|---------|---------|---------|
| 1 | D1.1 | `SharedContracts/Events/PaymentEvents.cs` 加 `ChannelRefundNo` 字段 | Payment(发布) + ReviewAfterSales/Notification(消费) |
| 2 | D1.2 | `SharedContracts/Events/` 加 `ShopId` 到 `ReviewSubmittedEvent` | ReviewAfterSales(发布) + PointsMembership/SellerShop(消费) |
| 3 | D1.5 | `SharedContracts/Integration/` `IdempotencyKey` 可空性修复 | 全 BC 消费者 |
| 4 | D4.1 | 5 BC 的 Outbox 旁路修复（`SaveChangesAsync`→`SaveEntitiesAsync`） | Cart/Promotion/ReviewAfterSales/Payment/Notification |
| 5 | D5.1 | `SharedContracts.Grpc/` 新增 `GuidProtoConverter`，替换 `Guid.GetHashCode()` | Product/Order/ReviewAfterSales/SellerShop |
| 6 | D5.3 | `PointsMembership.Api/` 新增 HTTP Confirm 端点 | PointsMembership |
| 7 | D6.1 | `Leno.Infrastructure/` 新增 `DesignTimeDbContextFactoryBase`，11 BC 继承 | 全 BC Infrastructure |
| 8 | TD4 | `Leno.Infrastructure/` `ResourceOwnershipChecker` 修复 | Shared |

### 3.2 subagent 指令要点

- Read `fix-13-cross-bc-architecture.md`，仅处理 8 个 P0 项（问题清单中标注 P0 的）
- 每项按计划的 TDD 步骤：写测试代码 → 写修复代码 → `git add {具体文件}` → `git commit -m "fix(cross-bc): {问题} [unverified]"`
- 不运行 `dotnet test`（无 SDK）
- 严格遵循代码完整性规则：无占位符、无 TODO、完整实现
- 完成后返回：已修改文件清单 + commit hash 列表

### 3.3 为什么单独一个 subagent

这些修复修改共享层代码（`SharedContracts`、`SharedKernel`、`Leno.Infrastructure`），如果与 BC subagent 并行会产生文件冲突。单独运行确保共享层先稳定，后续 BC 修复能直接引用新契约。

---

## 4. Wave 2 — BC P0 修复

**12 个 subagent（3 批 × 4 并行），108 个 P0 项**

每个 subagent 处理一个 BC 的全部 P0 项（~9 项/agent），按计划 TDD 5 步实施。

### 4.1 各 BC P0 项数

| BC | P0 项数 | 主要内容 |
|----|--------|---------|
| UserAuth | 15 | RefreshToken 存储、OAuth 安全、密码变更令牌撤销、AES-GCM |
| Order | 13 | 库存预占、Saga 补偿、Outbox、强制取消 |
| Notification | 12 | 模板渲染、限流、死信、多渠道 |
| Promotion | 11 | 优惠券锁、秒杀库存、价格计算 |
| ReviewAfterSales | 11 | 售后状态机、评价审核、图片校验 |
| Shared | 10 | Outbox、幂等、缓存、布隆过滤器 |
| PointsMembership | 8 | 积分防腐层、会员升级、退款积分 |
| SystemAdmin | 7 | 审计日志、特性开关、定时任务 |
| Payment | 6 | 对账、渠道配置、退款回调 |
| Product | 5 | SKU 唯一性、库存基线、价格变更 |
| Cart | 5 | 匿名购物车合并、价格快照、库存校验 |
| SellerShop | 5 | 店铺所有权、资质审核 |
| **合计** | **108** | |

### 4.2 实施流程（每个 subagent）

1. Read `fix-{NUM}-{bc}.md`，定位 "## P0 详细修复计划" 章节
2. 对每个 P0-N 问题：
   a. Read 该问题的审计位置、代码位置、根因、修复方案
   b. Write 新测试文件（如计划给出测试代码）
   c. Edit 修改业务代码（按计划修复方案，精确替换）
   d. `git add {具体文件路径} && git commit -m "fix({bc}): {问题描述} [unverified]"`
3. 不运行 `dotnet test`，不 `git push`
4. 返回：已实施 P0 编号清单 + 修改文件清单 + commit hash 列表

---

## 5. Wave 3 — BC P1+P2 修复

**12 个 subagent（3 批 × 4 并行），265 个 P1+P2 项**

每个 subagent 处理一个 BC 的全部 P1 + P2 项（~22 项/agent）。P1/P2 使用任务清单格式（修复步骤 + 影响范围 + 验证方法），代码量小于 P0 的 TDD 格式。

### 5.1 各 BC P1+P2 项数

| BC | P1 | P2 | 合计 |
|----|-----|-----|------|
| UserAuth | 19 | 12 | 31 |
| Order | 14 | 9 | 23 |
| Notification | 26 | 9 | 35 |
| Promotion | 13 | 10 | 23 |
| ReviewAfterSales | 12 | 8 | 20 |
| Shared | 18 | 11 | 29 |
| PointsMembership | 9 | 7 | 16 |
| SystemAdmin | 10 | 5 | 15 |
| Payment | 9 | 5 | 14 |
| Product | 10 | 5 | 15 |
| Cart | 15 | 10 | 25 |
| SellerShop | 11 | 8 | 19 |
| **合计** | **166** | **99** | **265** |

### 5.2 实施流程

同 Wave 2，但读取 "## P1 修复清单" + "## P2 修复清单" 章节。commit type：P1 用 `fix`，P2 用 `refactor`。

---

## 6. Wave 4 — 跨 BC P1+P2 修复

**1 个 subagent，~55 项**（63 跨 BC 待修复项减去 Wave 1 的 8 个 P0 项）

### 6.1 项清单

> 下表为主要类别概览，精确项数由 subagent 读取 fix-13 确定。跨 BC 计划共 63 项待修复，Wave 1 已处理 8 项 P0，本波处理剩余 ~55 项。

| 类别 | 项数 | 内容 |
|------|------|------|
| D1.3-D1.4 | 2 | 事件契约 P1（MemberLevelUpgradedEvent 同名混淆、RefundCompleted 回环风险） |
| D2.1-D2.6 | 5 | ACL 模式去重 P2（OrderStatusProvider/PaymentInfoQueryService/ProductSnapshot 等 6 类抽取共享 DTO） |
| D3.1-D3.3 | 3 | 共享内核污染 P1（Money 不可变性、OrderStatus 魔法数、Entity.Id 后门） |
| D4.2-D4.3 | 2 | 跨域事务边界 P1（PaymentSucceededEventConsumer 原子性、Saga 补偿幂等键） |
| D5.2, D5.4 | 2 | gRPC/REST 双轨 P1（PaymentGrpcService 硬编码零值、ConsulConfigWatcher 重载） |
| D6.2-D6.3 | 2 | 重复实现 P2（双路由 Obsolete 下线时间、其他重复实现项） |
| G4 Top10 剩余 | 6 | 技术债修复（TD1/TD5/TD6/TD8/TD9 等，排除 Wave 1 的 TD4） |
| G5 优化方案 | 12 | 短期 S1-S5 + 中期 M1-M4 + 长期 L1-L3 落地 |
| G6 风险 Top5 | 5 | 风险缓解步骤 |
| 其他子项 | ~16 | D 章节与 G4 章节的子项展开（由 subagent 读 fix-13 逐条实施） |

### 6.2 subagent 指令

Read `fix-13-cross-bc-architecture.md`，实施所有非 P0 章节（D1-D6 的 P1/P2 + G4/G5/G6），修改 `src/BuildingBlocks/` + 各 BC 领域层。每项 `git commit`，不 push。

---

## 7. Subagent Prompt 模板

### 7.1 BC subagent 模板（Wave 2/3 共用）

````text
你是 Leno 电商平台 .NET 代码修复实施专家。基于已有修复实施计划，实施 {BC_NAME} 的 {PRIORITY_TIER} 项修复。

**输入文件**（必须 Read）：
/workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-{NUM}-{bc}.md

**实施范围**：仅 "{PRIORITY_SECTION}" 章节
- Wave 2: "## P0 详细修复计划" 章节，每个 P0-N 按 TDD 5 步实施
- Wave 3: "## P1 修复清单" + "## P2 修复清单" 章节

**严格规则**：
1. 代码完整性：禁止任何占位符（TODO/FIXME/省略/空实现/throw NotImplementedException）。每函数完整实现。
2. 无 dotnet SDK：不运行 dotnet test/build。按计划写入测试代码 + 修复代码。
3. 修改现有文件时用 Edit 工具做精确替换，不可跳过未修改部分。
4. 每个问题实施流程：
   a. Read 计划中该问题的详细步骤与代码片段
   b. Write 新测试文件（如计划给出测试代码）
   c. Edit 修改业务代码（按计划修复方案）
   d. git add {具体文件路径} && git commit -m "{type}({bc}): {问题描述} [unverified]"
5. 不执行 git push（主 agent 统一推送）
6. 完成后返回：已实施问题编号清单 + 修改文件清单 + commit hash 列表

**扫描范围**（仅修改此 BC 目录下文件）：
{BC_SCAN_PATHS}

**排除**：Tests 目录只写新测试文件，不修改既有测试；Migrations/*.Designer.cs、*ModelSnapshot.cs 不修改。
````

### 7.2 参数替换表

| BC | {NUM} | {bc} | {BC_SCAN_PATHS} |
|----|-------|------|-----------------|
| UserAuth | 01 | userauth | `src/Services/UserAuth/Leno.UserAuth.{Domain,Application,Infrastructure,Api}/` |
| Product | 02 | product | `src/Services/Product/Leno.Product.{Domain,Application,Infrastructure,Api}/` |
| Cart | 03 | cart | `src/Services/Cart/Leno.Cart.{Domain,Application,Infrastructure,Api}/` |
| Order | 04 | order | `src/Services/Order/Leno.Order.{Domain,Application,Infrastructure,Api}/` |
| Promotion | 05 | promotion | `src/Services/Promotion/Leno.Promotion.{Domain,Application,Infrastructure,Api}/` |
| ReviewAfterSales | 06 | reviewaftersales | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.{Domain,Application,Infrastructure,Api}/` |
| PointsMembership | 07 | pointsmembership | `src/Services/PointsMembership/Leno.PointsMembership.{Domain,Application,Infrastructure,Api}/` |
| Payment | 08 | payment | `src/Services/Payment/Leno.Payment.{Domain,Application,Infrastructure,Api}/` |
| Notification | 09 | notification | `src/Services/Notification/Leno.Notification.{Domain,Application,Infrastructure,Api}/` |
| SellerShop | 10 | sellershop | `src/Services/SellerShop/Leno.SellerShop.{Domain,Application,Infrastructure,Api}/` |
| SystemAdmin | 11 | systemadmin | `src/Services/SystemAdmin/Leno.SystemAdmin.{Domain,Application,Infrastructure,Api}/` |
| Shared | 12 | shared | `src/BuildingBlocks/Leno.Infrastructure/`、`Leno.Infrastructure.Abstractions/`、`Leno.SharedKernel/`、`Leno.SharedContracts/`、`src/ApiGateway/Leno.ApiGateway/` |

### 7.3 跨 BC subagent 模板（Wave 1/4 共用）

````text
你是 Leno 电商平台 .NET 代码修复实施专家。基于已有修复实施计划，实施跨 BC 与架构级的 {PRIORITY_TIER} 项修复。

**输入文件**（必须 Read）：
/workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-13-cross-bc-architecture.md

**实施范围**：仅 {PRIORITY_SECTION}
- Wave 1: 问题清单中标注 P0 的 8 项（D1.1/D1.2/D1.5/D4.1/D5.1/D5.3/D6.1/TD4）
- Wave 4: 问题清单中标注 P1/P2 的全部项（D1-D6 的 P1/P2 + G4/G5/G6）

**严格规则**：
1-6 同 BC subagent 模板
7. 修改范围跨多个 BC 与共享层，按计划标注的代码位置精确修改

**扫描范围**：
- `src/BuildingBlocks/` —— 共享层
- `src/Services/*/Leno.*.Domain/` —— 各 BC 领域层
- `src/Services/*/Leno.*.Infrastructure/` —— 各 BC 基础设施层
- `src/Services/*/Leno.*.Api/` —— 各 BC API 层
````

---

## 8. Git 提交策略

| 层级 | 操作 | 时机 |
|------|------|------|
| subagent 内 | `git add {具体文件} && git commit -m "{type}({bc}): {描述} [unverified]"` | 每个问题修复后 |
| 主 agent 每批后 | `git push origin improve-0720` | 每批 4 个 subagent 全部完成后 |
| 主 agent 每波后 | 验证 `git log --oneline` commit 数量 | 每波全部批次完成后 |

**commit message 格式**：
- P0：`fix({bc}): {问题描述} [unverified]`
- P1：`fix({bc}): {问题描述} [unverified]`
- P2：`refactor({bc}): {问题描述} [unverified]`
- 跨 BC：`fix(cross-bc): {问题描述} [unverified]`

**分支**：全部在 `improve-0720` 分支上操作。

---

## 9. 错误处理策略

| 场景 | 处理 |
|------|------|
| subagent 返回失败 | 主 agent 记录 BC/问题编号到缺口表，不阻塞同批其他 subagent |
| subagent 部分完成 | 已完成的问题保留 commit；未完成的记入缺口表，Wave 结束后统一重试 |
| git commit 冲突 | subagent 仅 `git add` 自己 BC 目录的文件，目录互斥避免冲突；若仍冲突，主 agent 用 `git add -A && git commit` 兜底 |
| 文件不存在（计划引用的代码已被其他修复改变） | subagent 用 Grep/SearchCodebase 重新定位，若确认已修复则跳过并记录 |

---

## 10. 验证策略（无 dotnet SDK）

| 层级 | 方法 |
|------|------|
| subagent 内 | 按计划 TDD 步骤写入测试代码 + 修复代码，不做编译验证 |
| 主 agent 每批后 | `git diff --stat HEAD~{N}` 验证文件已修改；`git log --oneline` 验证 commit 存在 |
| 主 agent 每波后 | `git push origin improve-0720` 推送到远程 |
| 全部完成后 | 主 agent 生成执行报告：已实施项数 / 缺口项数 / commit 列表 |
| CI 验证 | GitHub Actions（`.github/workflows/ci.yml`）在 push 后自动编译 + 测试，失败项由后续迭代修复 |

---

## 11. 执行报告（最终产出）

主 agent 在全部 4 波完成后，生成 `docs/superpowers/plans/2026-07-22-fix-by-bc/EXECUTION-REPORT.md`：

- 各波次 subagent 执行状态表
- 已实施问题编号清单（按 BC 分组）
- 缺口表（未完成项 + 原因）
- git commit 历史摘要
- CI 验证待办项

---

## 12. 编排执行步骤总览

| 步骤 | 操作 | 工具 |
|------|------|------|
| 1 | Wave 1：启动 1 个跨 BC P0 subagent | Task × 1 |
| 2 | Wave 1 验证：`git push` + `git log` | RunCommand |
| 3 | Wave 2 Batch a：并行启动 4 个 BC P0 subagent | Task × 4 |
| 4 | Batch a 验证 + push | RunCommand |
| 5 | Wave 2 Batch b：并行启动 4 个 BC P0 subagent | Task × 4 |
| 6 | Batch b 验证 + push | RunCommand |
| 7 | Wave 2 Batch c：并行启动 4 个 BC P0 subagent | Task × 4 |
| 8 | Batch c 验证 + push | RunCommand |
| 9 | Wave 3 Batch a：并行启动 4 个 BC P1+P2 subagent | Task × 4 |
| 10 | Batch a 验证 + push | RunCommand |
| 11 | Wave 3 Batch b：并行启动 4 个 BC P1+P2 subagent | Task × 4 |
| 12 | Batch b 验证 + push | RunCommand |
| 13 | Wave 3 Batch c：并行启动 4 个 BC P1+P2 subagent | Task × 4 |
| 14 | Batch c 验证 + push | RunCommand |
| 15 | Wave 4：启动 1 个跨 BC P1+P2 subagent | Task × 1 |
| 16 | Wave 4 验证 + push | RunCommand |
| 17 | 生成 EXECUTION-REPORT.md | Write + RunCommand |
