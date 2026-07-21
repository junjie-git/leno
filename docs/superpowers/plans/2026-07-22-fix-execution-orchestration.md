# 代码审计修复 subagent 编排实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 编排 26 个 `general_purpose_task` subagent，分 4 波实施全部 401 项代码审计修复

**Architecture:** 4 波串行编排（跨BC P0 → BC P0 → BC P1+P2 → 跨BC P1+P2），波内 3 批 × 4 并行 subagent。主 agent 负责启动 subagent、验证 commit、推送远程。

**Tech Stack:** Trae Task subagent 编排（`general_purpose_task`）；.NET 10 / C# 代码修复；git commit + push

**设计文档**：[2026-07-22-fix-execution-design.md](file:///workspace/docs/superpowers/specs/2026-07-22-fix-execution-design.md)

---

## File Structure

| 路径 | 责任 | 产出方 |
|------|------|--------|
| `src/Services/*/Leno.*.{Domain,Application,Infrastructure,Api}/` | 12 BC 代码修复 | Wave 2/3 BC subagent |
| `src/BuildingBlocks/Leno.{Infrastructure,Infrastructure.Abstractions,SharedKernel,SharedContracts}/` | 共享层修复 | Wave 1/4 跨 BC subagent |
| `src/ApiGateway/Leno.ApiGateway/` | 网关修复 | Wave 1/4 跨 BC subagent |
| `docs/superpowers/plans/2026-07-22-fix-by-bc/EXECUTION-REPORT.md` | 执行报告 | 主 agent |

## Subagent Prompt 模板参考

### BC subagent 模板（Wave 2/3 共用）

每个 BC subagent 的 `query` 参数按以下模板填充（`{占位符}` 由各 Task 的参数表替换）：

> **`{type}` 替换规则**：P0/P1 问题用 `fix`，P2 问题用 `refactor`。Wave 2（P0）统一用 `fix`；Wave 3 中 P1 部分用 `fix`、P2 部分用 `refactor`，subagent 根据问题清单总表的优先级列自行判断。

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

### 跨 BC subagent 模板（Wave 1/4 共用）

````text
你是 Leno 电商平台 .NET 代码修复实施专家。基于已有修复实施计划，实施跨 BC 与架构级的 {PRIORITY_TIER} 项修复。

**输入文件**（必须 Read）：
/workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-13-cross-bc-architecture.md

**实施范围**：仅 {PRIORITY_SECTION}
- Wave 1: 问题清单中标注 P0 的 8 项（D1.1/D1.2/D1.5/D4.1/D5.1/D5.3/D6.1/TD4）
- Wave 4: 问题清单中标注 P1/P2 的全部项（D1-D6 的 P1/P2 + G4/G5/G6）

**严格规则**：
1. 代码完整性：禁止任何占位符（TODO/FIXME/省略/空实现/throw NotImplementedException）。每函数完整实现。
2. 无 dotnet SDK：不运行 dotnet test/build。按计划写入测试代码 + 修复代码。
3. 修改现有文件时用 Edit 工具做精确替换，不可跳过未修改部分。
4. 每个问题实施流程：
   a. Read 计划中该问题的详细步骤与代码片段
   b. Write 新测试文件（如计划给出测试代码）
   c. Edit 修改业务代码（按计划修复方案）
   d. git add {具体文件路径} && git commit -m "fix(cross-bc): {问题描述} [unverified]"
5. 不执行 git push（主 agent 统一推送）
6. 完成后返回：已实施问题编号清单 + 修改文件清单 + commit hash 列表

**扫描范围**：
- `src/BuildingBlocks/` —— 共享层
- `src/Services/*/Leno.*.Domain/` —— 各 BC 领域层
- `src/Services/*/Leno.*.Infrastructure/` —— 各 BC 基础设施层
- `src/Services/*/Leno.*.Api/` —— 各 BC API 层
````

### BC 参数替换表

| BC | {NUM} | {bc} | {BC_NAME} | {BC_SCAN_PATHS} |
|----|-------|------|-----------|-----------------|
| UserAuth | 01 | userauth | 用户与认证授权域 | `src/Services/UserAuth/Leno.UserAuth.{Domain,Application,Infrastructure,Api}/` |
| Product | 02 | product | 商品域 | `src/Services/Product/Leno.Product.{Domain,Application,Infrastructure,Api}/` |
| Cart | 03 | cart | 购物车域 | `src/Services/Cart/Leno.Cart.{Domain,Application,Infrastructure,Api}/` |
| Order | 04 | order | 订单与交易域 | `src/Services/Order/Leno.Order.{Domain,Application,Infrastructure,Api}/` |
| Promotion | 05 | promotion | 促销域 | `src/Services/Promotion/Leno.Promotion.{Domain,Application,Infrastructure,Api}/` |
| ReviewAfterSales | 06 | reviewaftersales | 评价与售后域 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.{Domain,Application,Infrastructure,Api}/` |
| PointsMembership | 07 | pointsmembership | 积分与会员域 | `src/Services/PointsMembership/Leno.PointsMembership.{Domain,Application,Infrastructure,Api}/` |
| Payment | 08 | payment | 支付集成域 | `src/Services/Payment/Leno.Payment.{Domain,Application,Infrastructure,Api}/` |
| Notification | 09 | notification | 消息通知域 | `src/Services/Notification/Leno.Notification.{Domain,Application,Infrastructure,Api}/` |
| SellerShop | 10 | sellershop | 卖家与店铺管理域 | `src/Services/SellerShop/Leno.SellerShop.{Domain,Application,Infrastructure,Api}/` |
| SystemAdmin | 11 | systemadmin | 系统管理域 | `src/Services/SystemAdmin/Leno.SystemAdmin.{Domain,Application,Infrastructure,Api}/` |
| Shared | 12 | shared | 共享层 | `src/BuildingBlocks/Leno.Infrastructure/`、`Leno.Infrastructure.Abstractions/`、`Leno.SharedKernel/`、`Leno.SharedContracts/`、`src/ApiGateway/Leno.ApiGateway/` |

---

## Task 1: 执行前环境准备

**Files:**
- Verify: git branch, git config

- [ ] **Step 1: 验证 git 分支与身份**

Run:
```bash
git branch --show-current && git config user.name && git config user.email
```
Expected: `improve-0720` + 已配置的用户名和邮箱

若 user.name/user.email 为空，执行：
```bash
git config user.name "junjie-git"
git config user.email "junjie-git@users.noreply.github.com"
```

- [ ] **Step 2: 验证工作区干净**

Run:
```bash
git status --short
```
Expected: 无输出（工作区干净）。若有未提交变更，先 `git stash` 或 `git commit`。

- [ ] **Step 3: 记录起始 commit hash**

Run:
```bash
git rev-parse HEAD
```
记录输出作为 `START_COMMIT`，供后续验证 commit 数量使用。

---

## Task 2: Wave 1 — 跨 BC P0 共享层修复（1 个 subagent）

**Goal:** 启动 1 个 subagent 实施 fix-13 的 8 个 P0 项（D1.1/D1.2/D1.5/D4.1/D5.1/D5.3/D6.1/TD4），修改共享层代码。

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs` 等
- Modify: `src/BuildingBlocks/Leno.Infrastructure/` 等
- Modify: 多 BC 的 Infrastructure 文件

- [ ] **Step 1: 启动跨 BC P0 subagent**

调用 `Task` 工具：
- `subagent_type`: `"general_purpose_task"`
- `response_language`: `"中文"`
- `description`: `"Wave1 跨BC P0修复"`
- `query`: 跨 BC subagent 模板，替换：
  - `{PRIORITY_TIER}` → `P0`
  - `{PRIORITY_SECTION}` → `问题清单中标注 P0 的 8 项（D1.1/D1.2/D1.5/D4.1/D5.1/D5.3/D6.1/TD4）`

- [ ] **Step 2: 等待 subagent 完成，记录返回摘要**

记录 subagent 返回的：
- 已实施问题编号清单
- 修改文件清单
- commit hash 列表

若 subagent 失败，记录失败原因，未完成项加入缺口表。

- [ ] **Step 3: 验证 commit 存在**

Run:
```bash
git log --oneline -10
```
Expected: 看到以 `fix(cross-bc):` 开头的 commit，标注 `[unverified]`

- [ ] **Step 4: 验证文件已修改**

Run:
```bash
git diff --stat {START_COMMIT} HEAD
```
Expected: 显示 `src/BuildingBlocks/` 和多 BC 目录下的文件修改

- [ ] **Step 5: 推送到远程**

Run:
```bash
git push origin improve-0720
```
若 push 失败（凭据限制），记录 `push 失败，本地 commit 已保留`，继续下一 Task。

---

## Task 3: Wave 2 Batch a — BC P0 修复（UserAuth / Order / Notification / Promotion）

**Goal:** 并行启动 4 个 subagent，分别实施 UserAuth(15 P0) / Order(13 P0) / Notification(12 P0) / Promotion(11 P0) 的 P0 修复。

**Files:**
- Create/Modify: `src/Services/UserAuth/Leno.UserAuth.*/` 下文件
- Create/Modify: `src/Services/Order/Leno.Order.*/` 下文件
- Create/Modify: `src/Services/Notification/Leno.Notification.*/` 下文件
- Create/Modify: `src/Services/Promotion/Leno.Promotion.*/` 下文件

- [ ] **Step 1: 并行启动 4 个 BC P0 subagent**

在**单条消息**中调用 4 次 `Task` 工具（`subagent_type: "general_purpose_task"`, `response_language: "中文"`），每次 `query` 为 BC subagent 模板替换以下参数：

| subagent | {BC_NAME} | {NUM} | {bc} | {PRIORITY_TIER} | {PRIORITY_SECTION} | {BC_SCAN_PATHS} |
|----------|-----------|-------|------|-----------------|-------------------|-----------------|
| 1 | 用户与认证授权域 | 01 | userauth | P0 | "## P0 详细修复计划" | `src/Services/UserAuth/Leno.UserAuth.{Domain,Application,Infrastructure,Api}/` |
| 2 | 订单与交易域 | 04 | order | P0 | "## P0 详细修复计划" | `src/Services/Order/Leno.Order.{Domain,Application,Infrastructure,Api}/` |
| 3 | 消息通知域 | 09 | notification | P0 | "## P0 详细修复计划" | `src/Services/Notification/Leno.Notification.{Domain,Application,Infrastructure,Api}/` |
| 4 | 促销域 | 05 | promotion | P0 | "## P0 详细修复计划" | `src/Services/Promotion/Leno.Promotion.{Domain,Application,Infrastructure,Api}/` |

`description` 分别为：`"Wave2a UserAuth P0"`, `"Wave2a Order P0"`, `"Wave2a Notification P0"`, `"Wave2a Promotion P0"`

- [ ] **Step 2: 等待全部 4 个 subagent 完成**

记录每个 subagent 返回的：
- 已实施 P0 编号清单
- 修改文件清单
- commit hash 列表

失败的 subagent 标注 "BC{N} P0 修复未完成"，未完成项加入缺口表。

- [ ] **Step 3: 验证 commit 存在**

Run:
```bash
git log --oneline -30 | grep -E "fix\((userauth|order|notification|promotion)\):"
```
Expected: 看到 4 个 BC 的 P0 修复 commit

- [ ] **Step 4: 推送到远程**

Run:
```bash
git push origin improve-0720
```
若 push 失败，记录并继续。

---

## Task 4: Wave 2 Batch b — BC P0 修复（ReviewAfterSales / Shared / PointsMembership / SystemAdmin）

**Goal:** 并行启动 4 个 subagent，分别实施 ReviewAfterSales(11 P0) / Shared(10 P0) / PointsMembership(8 P0) / SystemAdmin(7 P0) 的 P0 修复。

- [ ] **Step 1: 并行启动 4 个 BC P0 subagent**

在**单条消息**中调用 4 次 `Task` 工具，参数替换：

| subagent | {BC_NAME} | {NUM} | {bc} | {PRIORITY_TIER} | {PRIORITY_SECTION} | {BC_SCAN_PATHS} |
|----------|-----------|-------|------|-----------------|-------------------|-----------------|
| 1 | 评价与售后域 | 06 | reviewaftersales | P0 | "## P0 详细修复计划" | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.{Domain,Application,Infrastructure,Api}/` |
| 2 | 共享层 | 12 | shared | P0 | "## P0 详细修复计划" | `src/BuildingBlocks/Leno.Infrastructure/`、`Leno.Infrastructure.Abstractions/`、`Leno.SharedKernel/`、`Leno.SharedContracts/`、`src/ApiGateway/Leno.ApiGateway/` |
| 3 | 积分与会员域 | 07 | pointsmembership | P0 | "## P0 详细修复计划" | `src/Services/PointsMembership/Leno.PointsMembership.{Domain,Application,Infrastructure,Api}/` |
| 4 | 系统管理域 | 11 | systemadmin | P0 | "## P0 详细修复计划" | `src/Services/SystemAdmin/Leno.SystemAdmin.{Domain,Application,Infrastructure,Api}/` |

`description` 分别为：`"Wave2b ReviewAfterSales P0"`, `"Wave2b Shared P0"`, `"Wave2b PointsMembership P0"`, `"Wave2b SystemAdmin P0"`

- [ ] **Step 2: 等待全部 4 个 subagent 完成**

记录返回摘要。失败的 subagent 标注并加入缺口表。

- [ ] **Step 3: 验证 commit 存在**

Run:
```bash
git log --oneline -30 | grep -E "fix\((reviewaftersales|shared|pointsmembership|systemadmin)\):"
```
Expected: 看到 4 个 BC 的 P0 修复 commit

- [ ] **Step 4: 推送到远程**

Run:
```bash
git push origin improve-0720
```

---

## Task 5: Wave 2 Batch c — BC P0 修复（Payment / Product / Cart / SellerShop）

**Goal:** 并行启动 4 个 subagent，分别实施 Payment(6 P0) / Product(5 P0) / Cart(5 P0) / SellerShop(5 P0) 的 P0 修复。

- [ ] **Step 1: 并行启动 4 个 BC P0 subagent**

在**单条消息**中调用 4 次 `Task` 工具，参数替换：

| subagent | {BC_NAME} | {NUM} | {bc} | {PRIORITY_TIER} | {PRIORITY_SECTION} | {BC_SCAN_PATHS} |
|----------|-----------|-------|------|-----------------|-------------------|-----------------|
| 1 | 支付集成域 | 08 | payment | P0 | "## P0 详细修复计划" | `src/Services/Payment/Leno.Payment.{Domain,Application,Infrastructure,Api}/` |
| 2 | 商品域 | 02 | product | P0 | "## P0 详细修复计划" | `src/Services/Product/Leno.Product.{Domain,Application,Infrastructure,Api}/` |
| 3 | 购物车域 | 03 | cart | P0 | "## P0 详细修复计划" | `src/Services/Cart/Leno.Cart.{Domain,Application,Infrastructure,Api}/` |
| 4 | 卖家与店铺管理域 | 10 | sellershop | P0 | "## P0 详细修复计划" | `src/Services/SellerShop/Leno.SellerShop.{Domain,Application,Infrastructure,Api}/` |

`description` 分别为：`"Wave2c Payment P0"`, `"Wave2c Product P0"`, `"Wave2c Cart P0"`, `"Wave2c SellerShop P0"`

- [ ] **Step 2: 等待全部 4 个 subagent 完成**

记录返回摘要。失败的 subagent 标注并加入缺口表。

- [ ] **Step 3: 验证 Wave 2 全部 commit**

Run:
```bash
git log --oneline -60 | grep -c "fix(.*): .*\[unverified\]"
```
Expected: 数字 >= 108（全部 BC P0 修复 commit 总数，减去失败项）

- [ ] **Step 4: 推送到远程**

Run:
```bash
git push origin improve-0720
```

---

## Task 6: Wave 3 Batch a — BC P1+P2 修复（UserAuth / Order / Notification / Promotion）

**Goal:** 并行启动 4 个 subagent，分别实施 UserAuth(31 项) / Order(23 项) / Notification(35 项) / Promotion(23 项) 的 P1+P2 修复。

- [ ] **Step 1: 并行启动 4 个 BC P1+P2 subagent**

在**单条消息**中调用 4 次 `Task` 工具，参数替换：

| subagent | {BC_NAME} | {NUM} | {bc} | {PRIORITY_TIER} | {PRIORITY_SECTION} | {BC_SCAN_PATHS} |
|----------|-----------|-------|------|-----------------|-------------------|-----------------|
| 1 | 用户与认证授权域 | 01 | userauth | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/Services/UserAuth/Leno.UserAuth.{Domain,Application,Infrastructure,Api}/` |
| 2 | 订单与交易域 | 04 | order | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/Services/Order/Leno.Order.{Domain,Application,Infrastructure,Api}/` |
| 3 | 消息通知域 | 09 | notification | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/Services/Notification/Leno.Notification.{Domain,Application,Infrastructure,Api}/` |
| 4 | 促销域 | 05 | promotion | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/Services/Promotion/Leno.Promotion.{Domain,Application,Infrastructure,Api}/` |

`description` 分别为：`"Wave3a UserAuth P1P2"`, `"Wave3a Order P1P2"`, `"Wave3a Notification P1P2"`, `"Wave3a Promotion P1P2"`

注意：P1 commit type 用 `fix`，P2 commit type 用 `refactor`。subagent prompt 模板中 `{type}` 需根据问题优先级替换。

- [ ] **Step 2: 等待全部 4 个 subagent 完成**

记录返回摘要。失败的 subagent 标注并加入缺口表。

- [ ] **Step 3: 验证 commit 存在**

Run:
```bash
git log --oneline -50 | grep -E "(fix|refactor)\((userauth|order|notification|promotion)\):"
```
Expected: 看到 4 个 BC 的 P1/P2 修复 commit

- [ ] **Step 4: 推送到远程**

Run:
```bash
git push origin improve-0720
```

---

## Task 7: Wave 3 Batch b — BC P1+P2 修复（ReviewAfterSales / Shared / PointsMembership / SystemAdmin）

**Goal:** 并行启动 4 个 subagent，分别实施 ReviewAfterSales(20 项) / Shared(29 项) / PointsMembership(16 项) / SystemAdmin(15 项) 的 P1+P2 修复。

- [ ] **Step 1: 并行启动 4 个 BC P1+P2 subagent**

在**单条消息**中调用 4 次 `Task` 工具，参数替换：

| subagent | {BC_NAME} | {NUM} | {bc} | {PRIORITY_TIER} | {PRIORITY_SECTION} | {BC_SCAN_PATHS} |
|----------|-----------|-------|------|-----------------|-------------------|-----------------|
| 1 | 评价与售后域 | 06 | reviewaftersales | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.{Domain,Application,Infrastructure,Api}/` |
| 2 | 共享层 | 12 | shared | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/BuildingBlocks/Leno.Infrastructure/`、`Leno.Infrastructure.Abstractions/`、`Leno.SharedKernel/`、`Leno.SharedContracts/`、`src/ApiGateway/Leno.ApiGateway/` |
| 3 | 积分与会员域 | 07 | pointsmembership | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/Services/PointsMembership/Leno.PointsMembership.{Domain,Application,Infrastructure,Api}/` |
| 4 | 系统管理域 | 11 | systemadmin | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/Services/SystemAdmin/Leno.SystemAdmin.{Domain,Application,Infrastructure,Api}/` |

`description` 分别为：`"Wave3b ReviewAfterSales P1P2"`, `"Wave3b Shared P1P2"`, `"Wave3b PointsMembership P1P2"`, `"Wave3b SystemAdmin P1P2"`

- [ ] **Step 2: 等待全部 4 个 subagent 完成**

记录返回摘要。失败的 subagent 标注并加入缺口表。

- [ ] **Step 3: 验证 commit 存在**

Run:
```bash
git log --oneline -50 | grep -E "(fix|refactor)\((reviewaftersales|shared|pointsmembership|systemadmin)\):"
```
Expected: 看到 4 个 BC 的 P1/P2 修复 commit

- [ ] **Step 4: 推送到远程**

Run:
```bash
git push origin improve-0720
```

---

## Task 8: Wave 3 Batch c — BC P1+P2 修复（Payment / Product / Cart / SellerShop）

**Goal:** 并行启动 4 个 subagent，分别实施 Payment(14 项) / Product(15 项) / Cart(25 项) / SellerShop(19 项) 的 P1+P2 修复。

- [ ] **Step 1: 并行启动 4 个 BC P1+P2 subagent**

在**单条消息**中调用 4 次 `Task` 工具，参数替换：

| subagent | {BC_NAME} | {NUM} | {bc} | {PRIORITY_TIER} | {PRIORITY_SECTION} | {BC_SCAN_PATHS} |
|----------|-----------|-------|------|-----------------|-------------------|-----------------|
| 1 | 支付集成域 | 08 | payment | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/Services/Payment/Leno.Payment.{Domain,Application,Infrastructure,Api}/` |
| 2 | 商品域 | 02 | product | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/Services/Product/Leno.Product.{Domain,Application,Infrastructure,Api}/` |
| 3 | 购物车域 | 03 | cart | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/Services/Cart/Leno.Cart.{Domain,Application,Infrastructure,Api}/` |
| 4 | 卖家与店铺管理域 | 10 | sellershop | P1+P2 | "## P1 修复清单" + "## P2 修复清单" | `src/Services/SellerShop/Leno.SellerShop.{Domain,Application,Infrastructure,Api}/` |

`description` 分别为：`"Wave3c Payment P1P2"`, `"Wave3c Product P1P2"`, `"Wave3c Cart P1P2"`, `"Wave3c SellerShop P1P2"`

- [ ] **Step 2: 等待全部 4 个 subagent 完成**

记录返回摘要。失败的 subagent 标注并加入缺口表。

- [ ] **Step 3: 验证 Wave 3 全部 commit**

Run:
```bash
git log --oneline -80 | grep -cE "(fix|refactor)\(.*\): .*\[unverified\]"
```
Expected: 数字 >= 265（全部 BC P1+P2 修复 commit 总数，减去失败项）

- [ ] **Step 4: 推送到远程**

Run:
```bash
git push origin improve-0720
```

---

## Task 9: Wave 4 — 跨 BC P1+P2 修复（1 个 subagent）

**Goal:** 启动 1 个 subagent 实施 fix-13 的全部 P1/P2 项（D1-D6 的 P1/P2 + G4/G5/G6，~55 项）。

- [ ] **Step 1: 启动跨 BC P1+P2 subagent**

调用 `Task` 工具：
- `subagent_type`: `"general_purpose_task"`
- `response_language`: `"中文"`
- `description`: `"Wave4 跨BC P1P2修复"`
- `query`: 跨 BC subagent 模板，替换：
  - `{PRIORITY_TIER}` → `P1+P2`
  - `{PRIORITY_SECTION}` → `问题清单中标注 P1/P2 的全部项（D1-D6 的 P1/P2 + G4/G5/G6）`

- [ ] **Step 2: 等待 subagent 完成**

记录返回摘要。失败项加入缺口表。

- [ ] **Step 3: 验证 commit 存在**

Run:
```bash
git log --oneline -30 | grep "fix(cross-bc):"
```
Expected: 看到跨 BC P1/P2 修复 commit

- [ ] **Step 4: 推送到远程**

Run:
```bash
git push origin improve-0720
```

---

## Task 10: 生成执行报告

**Goal:** 汇总 4 波执行结果，生成 EXECUTION-REPORT.md。

**Files:**
- Create: `docs/superpowers/plans/2026-07-22-fix-by-bc/EXECUTION-REPORT.md`

- [ ] **Step 1: 统计全部 commit**

Run:
```bash
git log --oneline {START_COMMIT}..HEAD | wc -l
git log --oneline {START_COMMIT}..HEAD | grep -c "fix(cross-bc):"
git log --oneline {START_COMMIT}..HEAD | grep -c "fix(.*): .*\[unverified\]"
git log --oneline {START_COMMIT}..HEAD | grep -c "refactor(.*): .*\[unverified\]"
```
记录各统计数字。

- [ ] **Step 2: 验证未修改 Tests 目录外的不相关文件**

Run:
```bash
git diff --stat {START_COMMIT}..HEAD -- src/ | grep -v "src/Services/" | grep -v "src/BuildingBlocks/" | grep -v "src/ApiGateway/"
```
Expected: 无输出（所有修改均在预期范围内）

- [ ] **Step 3: 写入 EXECUTION-REPORT.md**

用 `Write` 工具写入以下内容（`{占位符}` 由各步骤统计结果填入）：

````markdown
# 代码审计修复 subagent 编排执行报告

**执行日期**：2026-07-22
**设计文档**：[2026-07-22-fix-execution-design.md](file:///workspace/docs/superpowers/specs/2026-07-22-fix-execution-design.md)
**起始 commit**：{START_COMMIT}
**结束 commit**：{END_COMMIT}

## 执行总览

| 波次 | subagent 数 | 预期项数 | 已实施 | 失败/缺口 |
|------|------------|---------|--------|----------|
| Wave 1 跨BC P0 | 1 | 8 | {n} | {n} |
| Wave 2 BC P0 | 12 | 108 | {n} | {n} |
| Wave 3 BC P1+P2 | 12 | 265 | {n} | {n} |
| Wave 4 跨BC P1+P2 | 1 | ~55 | {n} | {n} |
| **合计** | **26** | **~401** | **{n}** | **{n}** |

## 各 BC 执行详情

### BC P0 修复（Wave 2）

| BC | 预期 P0 | 已实施 | commit 数 | 状态 |
|----|---------|--------|----------|------|
| UserAuth | 15 | {n} | {n} | {✅完成/⚠️部分} |
| Order | 13 | {n} | {n} | {状态} |
| Notification | 12 | {n} | {n} | {状态} |
| Promotion | 11 | {n} | {n} | {状态} |
| ReviewAfterSales | 11 | {n} | {n} | {状态} |
| Shared | 10 | {n} | {n} | {状态} |
| PointsMembership | 8 | {n} | {n} | {状态} |
| SystemAdmin | 7 | {n} | {n} | {状态} |
| Payment | 6 | {n} | {n} | {状态} |
| Product | 5 | {n} | {n} | {状态} |
| Cart | 5 | {n} | {n} | {状态} |
| SellerShop | 5 | {n} | {n} | {状态} |

### BC P1+P2 修复（Wave 3）

（同上格式，P1+P2 合计列）

### 跨 BC 修复（Wave 1/4）

| 波次 | 预期项数 | 已实施 | 状态 |
|------|---------|--------|------|
| Wave 1 P0 | 8 | {n} | {状态} |
| Wave 4 P1+P2 | ~55 | {n} | {状态} |

## 缺口表

| BC | 问题编号 | 原因 | 建议后续操作 |
|----|---------|------|-------------|
| {BC} | {P0-N / P1-N / P2-N} | {失败原因} | {重试/手动修复/跳过} |

（若无缺口，填"无"）

## git commit 统计

- 总 commit 数：{n}
- `fix(cross-bc):` commit 数：{n}
- `fix({bc}):` commit 数（P0+P1）：{n}
- `refactor({bc}):` commit 数（P2）：{n}
- 全部标注 `[unverified]`

## CI 验证待办

- [ ] push 后检查 GitHub Actions（`.github/workflows/ci.yml`）构建结果
- [ ] 编译失败项记录并安排修复
- [ ] 测试失败项记录并安排修复
- [ ] `scripts/check-placeholders.sh` 验证无占位符

## 备注

- 本次执行在无 dotnet SDK 的沙箱环境中进行，所有 commit 标注 `[unverified]`
- 编译与测试验证推迟到 CI 环境
- push 可能因沙箱凭据限制失败，本地 commit 已保留
````

- [ ] **Step 4: 提交执行报告**

Run:
```bash
git add docs/superpowers/plans/2026-07-22-fix-by-bc/EXECUTION-REPORT.md
git commit -m "docs: 新增代码审计修复subagent编排执行报告"
git push origin improve-0720
```

- [ ] **Step 5: 最终验证**

Run:
```bash
ls docs/superpowers/plans/2026-07-22-fix-by-bc/EXECUTION-REPORT.md
git log --oneline -5
```
Expected: EXECUTION-REPORT.md 存在；最近 commit 为执行报告提交

---

## 自审清单

执行人在完成全部 Task 后，对照以下自审：

- [ ] **Spec 覆盖**：设计文档 12 章节均有对应 Task
  - 第 1 范围与约束 → Task 1（环境准备）+ 全 Task（约束执行）
  - 第 2 总体架构 → Task 2-9（4 波编排）
  - 第 3 Wave 1 → Task 2
  - 第 4 Wave 2 → Task 3/4/5
  - 第 5 Wave 3 → Task 6/7/8
  - 第 6 Wave 4 → Task 9
  - 第 7 Prompt 模板 → 本计划开头"Subagent Prompt 模板参考"章节
  - 第 8 Git 策略 → 各 Task 的 Step 3/4
  - 第 9 错误处理 → 各 Task 的 Step 2（失败记录）
  - 第 10 验证策略 → 各 Task 的 Step 3/4 + Task 10
  - 第 11 执行报告 → Task 10
  - 第 12 编排步骤总览 → Task 1-10 全覆盖
- [ ] **占位扫描**：本计划中 `{占位符}` 均为运行时由主 agent 填入的动态值（BC 名称、文件路径、统计数字），非内容占位；无 TBD/TODO
- [ ] **类型一致性**：BC subagent 模板在 Task 3-8 中参数替换一致；跨 BC subagent 模板在 Task 2/9 中参数替换一致；commit message 格式在所有 Task 中一致
- [ ] **并行安全**：每批 4 个 subagent 分别修改不同 BC 目录，目录互斥无冲突；跨 BC subagent 单独运行避免共享层冲突
