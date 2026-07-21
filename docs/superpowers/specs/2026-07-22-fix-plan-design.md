# Leno 代码审计修复实施计划设计文档

**文档版本**：v1.0
**创建日期**：2026-07-22
**作者**：brainstorming skill（基于用户 4 项决策 + 方案 B 选择）
**状态**：待用户审查
**关联文档**：
- 输入：[2026-07-21-code-audit/](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/) 15 份审计报告
- 产出：`docs/superpowers/plans/2026-07-22-fix-by-bc/` 14 份修复实施计划

---

## 1 目标与范围

### 1.1 目标

基于 2026-07-21 代码审计产出的 15 份报告（12 BC 详细 + 00-summary 聚合 + 13-architecture-assessment 架构评估 + README），启动 13 个 `general_purpose_task` subagent 产出 13 份分 BC / 跨 BC 修复实施计划 + 1 份总览索引，归档至 `docs/superpowers/plans/2026-07-22-fix-by-bc/`。

### 1.2 范围

- **输入问题总数**：364 项（🔴 107 / 🟡 158 / 🟢 99）
- **覆盖范围**：全量（高+中+低），不裁剪
- **跨 BC 共性问题（D1-D6）**：全归第 13 个跨 BC subagent，12 个 BC subagent 不处理 D 章节
- **不覆盖**：已在既有计划中修复的问题（主 agent 预处理去重）

### 1.3 用户决策记录

| # | 决策项 | 选定方案 | 理由 |
|---|--------|---------|------|
| 1 | subagent 切分粒度 | 按 BC 切（12+1） | 与审计编排同构，边界清晰 |
| 2 | 修复范围 | 全量（高+中+低） | 覆盖最全，避免遗漏 |
| 3 | 计划深度 | 分层（P0 TDD + P1/P2 清单） | 主问题可直接执行，中低问题有清晰指引 |
| 4 | 跨 BC 共性问题归属 | 全归跨 BC subagent | 边界清晰无重复 |
| 5 | 编排方案 | 方案 B（带预处理与后校验） | 避免为已修复问题重复出计划，后校验保证一致性 |

---

## 2 总体编排（方案 B：三阶段）

### 2.1 编排总览

| 阶段 | 执行方 | 输入 | 输出 |
|------|--------|------|------|
| **阶段 1：预处理** | 主 agent | 3 份既有计划/spec 文件 | `已修复问题清单`（嵌入各 subagent prompt） |
| **阶段 2：并行 subagent** | 13 个 `general_purpose_task` subagent，分两批 6+7 | 对应审计报告 + 已修复清单 + 代码扫描 | 13 份修复计划 `.md` |
| **阶段 3：后校验聚合** | 主 agent | 13 份计划 | `README.md` 总览索引 + 一致性校验报告 |

### 2.2 13 个 subagent 划分

| 批次 | subagent # | 输入审计报告 | 输出计划文件 |
|------|-----------|-------------|-------------|
| 批次 1（6 并行）| 1-6 | 01-userauth ~ 06-reviewaftersales | `fix-01-userauth.md` ~ `fix-06-reviewaftersales.md` |
| 批次 2（7 并行）| 7-12 | 07-pointsmembership ~ 12-shared | `fix-07-pointsmembership.md` ~ `fix-12-shared.md` |
| 批次 2 | 13 | 00-summary D 章节 + 13-architecture-assessment G4/G5 | `fix-13-cross-bc-architecture.md` |

### 2.3 关键约束

- 所有 subagent 用 `general_purpose_task`（需 Write 工具写文件 + git commit）
- subagent 不修改业务代码，只产出计划文件
- 引用证据统一用 `file:///workspace/...#L行号` 格式
- 每个 subagent 完成后自行 `git add` + `git commit` + `git push`
- 并行批次 6+7，不超过系统并行限制
- 失败不阻塞：单 subagent 失败不影响其他，主 agent 在 README 记录

---

## 3 阶段 1：预处理（去重清单）

### 3.1 目的

避免 subagent 为已修复问题重复出计划。主 agent 在启动 subagent 前，自己执行一轮扫描，提取"已修复问题清单"。

### 3.2 扫描输入（3 个文件）

| 文件 | 提取内容 |
|------|---------|
| `docs/superpowers/plans/2026-07-20-p0a-placeholder-implementation.md` | 已完成的占位符实现任务清单（任务标题 + 涉及 BC） |
| `docs/superpowers/plans/2026-07-20-p1b1-async-reliability-hardening.md` | 已完成的异步可靠性加固任务清单 |
| `.trae/specs/fix-critical-business-vulnerabilities/tasks.md` | 已完成的安全/业务漏洞修复任务清单 |

### 3.3 提取方式

1. 主 agent 用 `Read` 读取 3 份文件
2. 提取已完成（checked `[x]` 或 status=completed）的任务项
3. 每项提取：任务 ID/标题、涉及 BC、修复要点（1-2 句）、对应审计报告章节（若可识别）
4. 汇总为 `已修复问题清单` Markdown 片段，按 BC 分组

### 3.4 清单格式（嵌入各 subagent prompt）

```markdown
## 已修复问题清单（来自既有计划，跳过这些项，标注 [ALREADY-FIXED]）

### UserAuth
- [ALREADY-FIXED] UA-03 双因子认证 TOTP（来自 p1b1）—— 审计 01-userauth.md 未覆盖
- [ALREADY-FIXED] UA-07 第三方账号绑定（来自 p2）—— 审计 01-userauth.md 未覆盖

### Order
- [ALREADY-FIXED] ORD-09 运营强制取消（来自 p2）—— 对应审计 04-order.md H-XX
...
```

### 3.5 嵌入规则

- 每个 BC subagent 的 prompt 只嵌入该 BC 的已修复项
- 跨 BC subagent 的 prompt 嵌入所有 BC 的已修复项（因 D1-D6 跨 BC）
- subagent 收到清单后，在计划中对命中项标注 `[ALREADY-FIXED]` 并跳过详细修复步骤，仅在"问题清单"章节列出行号引用

### 3.6 去重边界

- 既有计划主要是"功能未实现"类任务（P0/P1/P2 功能完成率），与 2026-07-21 审计发现的"实现有缺陷"类问题重叠度预计较低
- 若既有计划已覆盖审计发现的某个问题，subagent 标注 `[ALREADY-FIXED]` 并给出既有计划文件链接
- 若既有计划覆盖但实现可能仍有缺陷，subagent 仍出修复计划，但标注 `[VERIFY-EXISTING-FIX]` 并引用既有计划

---

## 4 阶段 2：subagent prompt 与输出格式

### 4.1 BC subagent prompt 模板（12 个 BC 共用，替换占位符）

````text
你是 Leno 电商平台修复实施计划制定专家。基于 {BC_NAME} 代码审计报告，制定该 BC 全量问题的修复实施计划。

**输入文件**（必须全部 Read）：
1. `/workspace/docs/superpowers/specs/2026-07-21-code-audit/{AUDIT_REPORT}` —— {BC_NAME} 审计报告（含 🔴/🟡/🟢 全量问题）
2. `/workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md` —— 跨 BC 聚合报告（仅参考 F 章节 P0/P1/P2 归档，不处理 D 章节）
3. `/workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md` —— 架构评估报告（仅参考 G4/G5，不处理）
4. `{ALREADY_FIXED_LIST}` —— 已修复问题清单（嵌入下方）

## 已修复问题清单
{ALREADY_FIXED_LIST_CONTENT}

**扫描范围**（用于校验问题是否仍存在）：
{BC_SCAN_PATHS}

**严格排除**：
- 任何路径包含 `Tests` 的目录
- `Migrations/*.Designer.cs`、`*ModelSnapshot.cs`
- `SharedContracts.Grpc/Generated/`

**任务**：产出 `/workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/{OUTPUT_FILE}`，严格按以下格式：

# {BC_NAME} 修复实施计划

## 元数据
- 审计报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/{AUDIT_REPORT}]
- 问题总数：🔴 {H} / 🟡 {M} / 🟢 {L}
- 已修复（跳过）：{N} 项
- 本计划覆盖：{N} 项

## 问题清单总表
| # | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|---|--------|---------|---------|--------|------|
| 1 | 🔴 | {标题} | {报告章节} | P0 | TODO |
| 2 | 🔴 | {标题} | {报告章节} | P0 | [ALREADY-FIXED] |
...

## P0 详细修复计划（TDD bite-sized 格式）
对每个 P0 问题，按以下格式：

### P0-{N}: {问题标题}
- **审计位置**：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/{AUDIT_REPORT}#{行号}`
- **代码位置**：`file:///workspace/src/.../File.cs#L{行号}`
- **根因**：{2-3 句}
- **影响**：{1-2 句}
- **修复方案**：{改造步骤概述}

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**
（给出完整测试代码，含 Arrange/Act/Assert）

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "Name=..."`
Expected: FAIL

- [ ] **Step 3: 最小实现**
（给出完整修复代码片段）

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "Name=..."`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add {files}
git commit -m "fix({bc}): {问题描述}"
```

## P1 修复清单（任务清单格式）
对每个 P1 问题，按以下格式：

### P1-{N}: {问题标题}
- **审计位置**：`file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/{AUDIT_REPORT}#{行号}`
- **代码位置**：`file:///workspace/src/.../File.cs#L{行号}`
- **根因**：{1-2 句}
- **修复步骤**：
  1. {步骤}
  2. {步骤}
- **影响范围**：{涉及的聚合/接口/消费者}
- **验证方法**：{测试命令或手动验证步骤}

## P2 修复清单（任务清单格式，可简化）
（同 P1 格式，根因可 1 句）

## 已修复项（标注 [ALREADY-FIXED]）
| # | 问题 | 既有计划位置 | 备注 |
|---|------|-------------|------|
| 1 | {标题} | [file:///workspace/docs/superpowers/plans/{既有计划}#{行号}] | 既有修复可能仍需验证 |

**输出要求**：
1. 使用 Read/Grep/SearchCodebase/Glob 探索代码，校验每个问题是否仍存在
2. 对 [ALREADY-FIXED] 项跳过详细计划，仅列入"已修复项"表
3. 若审计问题在代码中已不存在（非既有计划修复，可能是误报或已被其他变更修复），标注 `[VERIFIED-NOT-REPRODUCIBLE]` 跳过详细计划，列入"已修复项"表并说明校验结论
4. P0 必须给出可执行的完整 TDD 步骤（含测试代码与修复代码）
5. P1/P2 给出任务清单（修复步骤 + 影响范围 + 验证方法）
6. 引用证据用 `file:///workspace/...#L行号` 格式
7. 不修改任何业务代码
8. 用 Write 工具写入指定路径
9. 完成后 `git add` + `git commit -m "docs: 新增{BC_NAME}修复实施计划"` + `git push`
10. 将完整计划内容作为最终消息返回
````

### 4.2 跨 BC subagent prompt（第 13 个，独立模板）

````text
你是 Leno 电商平台修复实施计划制定专家。基于跨 BC 聚合报告与架构评估报告，制定跨 BC 共性问题与架构级问题的修复实施计划。

**输入文件**（必须全部 Read）：
1. `/workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md` —— 重点读 D 章节（D1-D6）与 F 章节（修复路线）
2. `/workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md` —— 重点读 G3（缺点）、G4（技术债 Top10）、G5（优化方案）、G6（风险评估 Top5）
3. `{ALREADY_FIXED_LIST}` —— 已修复问题清单（全 BC）

## 已修复问题清单
{ALREADY_FIXED_LIST_CONTENT}

**扫描范围**：
- `/workspace/src/BuildingBlocks/` —— 共享层
- `/workspace/src/Services/*/Leno.*.Domain/` —— 各 BC 领域层（用于校验 D 章节问题）
- `/workspace/docs/decisions/` —— ADR 决策

**任务**：产出 `/workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-13-cross-bc-architecture.md`，严格按以下格式：

# 跨 BC 与架构级修复实施计划

## 元数据
- 输入报告：00-summary.md（D1-D6）+ 13-architecture-assessment.md（G3/G4/G5/G6）
- 问题总数：D1-D6 共 {N} 子问题 + G4 技术债 Top10 + G6 风险 Top5
- 已修复（跳过）：{N} 项
- 本计划覆盖：{N} 项

## 问题清单总表
| # | 类别 | 问题标题 | 来源 | 优先级 | 状态 |
|---|------|---------|------|--------|------|
| 1 | D1 事件契约 | RefundCompletedEvent 缺 ChannelRefundNo | 00-summary D1 | P0 | TODO |
| 2 | G4 技术债 | TD1 Outbox 旁路修复 | 13 G4 | P0 | TODO |
...

## D1-D6 跨 BC 共性问题修复计划（P0 TDD + P1/P2 清单）

### D1: 事件契约对齐
（每个子问题按 P0 TDD 或 P1 清单格式，取决于严重度）

### D2: ACL 模式重复
...

### D3: 共享内核污染
...

### D4: 跨域事务边界
...

### D5: gRPC 与 REST 双轨一致性
...

### D6: 重复实现
...

## G4 技术债 Top10 修复计划
（每个技术债按四象限归类 + P0 TDD 或 P1/P2 清单格式）

## G6 风险 Top5 缓解计划
（每个风险给出缓解步骤）

## G5 优化方案落地计划
（短期/中期/长期各列出，引用 13 G5 章节但不复制，给出落地步骤）

**输出要求**：
1-9 同 BC subagent 模板
10. commit message: `docs: 新增跨BC与架构级修复实施计划`
````

### 4.3 占位符替换表

| # | {BC_NAME} | {AUDIT_REPORT} | {OUTPUT_FILE} | {BC_SCAN_PATHS} |
|---|-----------|----------------|---------------|-----------------|
| 1 | UserAuth（用户与认证授权域） | 01-userauth.md | fix-01-userauth.md | `src/Services/UserAuth/Leno.UserAuth.{Domain,Application,Infrastructure,Api}/` |
| 2 | Product（商品域） | 02-product.md | fix-02-product.md | `src/Services/Product/Leno.Product.{Domain,Application,Infrastructure,Api}/` |
| 3 | Cart（购物车域） | 03-cart.md | fix-03-cart.md | `src/Services/Cart/Leno.Cart.{Domain,Application,Infrastructure,Api}/` |
| 4 | Order（订单与交易域） | 04-order.md | fix-04-order.md | `src/Services/Order/Leno.Order.{Domain,Application,Infrastructure,Api}/` |
| 5 | Promotion（促销域） | 05-promotion.md | fix-05-promotion.md | `src/Services/Promotion/Leno.Promotion.{Domain,Application,Infrastructure,Api}/` |
| 6 | ReviewAfterSales（评价与售后域） | 06-reviewaftersales.md | fix-06-reviewaftersales.md | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.{Domain,Application,Infrastructure,Api}/` |
| 7 | PointsMembership（积分与会员域） | 07-pointsmembership.md | fix-07-pointsmembership.md | `src/Services/PointsMembership/Leno.PointsMembership.{Domain,Application,Infrastructure,Api}/` |
| 8 | Payment（支付集成域） | 08-payment.md | fix-08-payment.md | `src/Services/Payment/Leno.Payment.{Domain,Application,Infrastructure,Api}/` |
| 9 | Notification（消息通知域） | 09-notification.md | fix-09-notification.md | `src/Services/Notification/Leno.Notification.{Domain,Application,Infrastructure,Api}/` |
| 10 | SellerShop（卖家与店铺管理域） | 10-sellershop.md | fix-10-sellershop.md | `src/Services/SellerShop/Leno.SellerShop.{Domain,Application,Infrastructure,Api}/` |
| 11 | SystemAdmin（系统管理域） | 11-systemadmin.md | fix-11-systemadmin.md | `src/Services/SystemAdmin/Leno.SystemAdmin.{Domain,Application,Infrastructure,Api}/` |
| 12 | Shared（共享层） | 12-shared.md | fix-12-shared.md | `src/BuildingBlocks/Leno.Infrastructure/`、`src/BuildingBlocks/Leno.Infrastructure.Abstractions/`、`src/BuildingBlocks/Leno.SharedKernel/`、`src/BuildingBlocks/Leno.SharedContracts/`（排除 `Leno.SharedContracts.Grpc/Generated/`）、`src/ApiGateway/Leno.ApiGateway/` |

---

## 5 阶段 3：后校验与 README 聚合

### 5.1 后校验检查项

主 agent 读取 13 份计划后，执行 3 项一致性校验：

| # | 校验项 | 检测方法 | 处置 |
|---|--------|---------|------|
| 1 | **D 章节重复** | 检查 12 份 BC 计划是否误处理了 D1-D6 子问题（应全归 fix-13） | 若 BC 计划出现 D 章节问题，标注 `[MOVE-TO-CROSS-BC]` 并在 README 记录 |
| 2 | **已修复项遗漏** | 检查 13 份计划是否对 [ALREADY-FIXED] 或 [VERIFIED-NOT-REPRODUCIBLE] 项出了详细修复步骤（应跳过） | 若有详细步骤，标注 `[DUPLICATE-FIX]` 并在 README 记录 |
| 3 | **P0 覆盖完整性** | 统计 13 份计划中 P0 项总数，与审计报告 🔴 107 项交叉比对 | 缺失项列入 README "缺口"章节 |

### 5.2 README.md 总览索引格式

主 agent 产出 `/workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/README.md`：

````markdown
# Leno 代码审计修复实施计划总览

**生成日期**：2026-07-22
**输入**：[2026-07-21-code-audit/](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/) 15 份审计报告
**编排方式**：主 agent 预处理去重 + 13 个 subagent 并行（12 BC + 1 跨 BC）+ 主 agent 后校验

## 修复计划索引

### 顶层计划
| # | 计划 | 覆盖范围 |
|---|------|---------|
| 13 | [fix-13-cross-bc-architecture.md](file:///workspace/docs/superpowers/plans/2026-07-22-fix-by-bc/fix-13-cross-bc-architecture.md) | D1-D6 跨 BC 共性 + G4 技术债 + G6 风险 + G5 优化方案 |

### BC 修复计划
| # | BC | 高/中/低 | P0/P1/P2 | 已修复 | 计划 |
|---|-----|---------|----------|--------|------|
| 1 | UserAuth | 15/19/12 | -/-/- | - | [fix-01-userauth.md] |
| 2 | Product | 5/10/5 | -/-/- | - | [fix-02-product.md] |
| ... | ... | ... | ... | ... | ... |
| 12 | Shared | 10/18/11 | -/-/- | - | [fix-12-shared.md] |
| **合计** | - | **107/158/99** | **-/-/-** | **-** | 13 份 |

（P0/P1/P2 列与已修复列由主 agent 读取各计划元数据后填入）

## P0 覆盖完整性校验
- 审计 🔴 高风险总数：107
- 计划 P0 覆盖数：{N}
- 缺口：{N} 项（列于下表）

| 缺失项 | 来源审计报告 | 原因 |
|--------|-------------|------|
| {标题} | {报告}#{行号} | {未覆盖原因} |

## 后校验报告
| 校验项 | 结果 | 异常数 |
|--------|------|--------|
| D 章节重复 | ✅ 通过 / ⚠️ 异常 | {N} |
| 已修复项遗漏 | ✅ 通过 / ⚠️ 异常 | {N} |
| P0 覆盖完整性 | ✅ 通过 / ⚠️ 异常 | {N} |

## 阅读建议
1. **修复主链路**：优先读 fix-13 的 G6 风险 Top5 + 各 BC fix 的 P0 章节
2. **修复单 BC**：直接读对应 fix-0X 文件的 P0/P1/P2 章节
3. **跨 BC 协调**：读 fix-13 的 D1-D6 章节，涉及多 BC 的修复步骤已标注影响范围
````

### 5.3 主 agent 提交策略

| 步骤 | 操作 |
|------|------|
| 1 | 主 agent 写入 `README.md` |
| 2 | `git add README.md && git commit -m "docs: 新增修复计划总览索引与后校验报告"` |
| 3 | `git push` |

### 5.4 失败处置

| 场景 | 处置 |
|------|------|
| 某 BC subagent 失败 | 主 agent 在 README "缺口"章节标注 `BC{N} 计划未生成`，不影响其他计划提交 |
| 预处理扫描文件不存在 | 跳过该文件，`已修复问题清单`标注 `[{文件名} 不可读]` |
| git push 失败 | 本地 commit 保留，README 记录 `push 失败，本地 commit 已保留` |

---

## 6 验收标准与执行约束

### 6.1 交付物清单

| # | 交付物 | 路径 | 产出方 |
|---|--------|------|--------|
| 1 | 已修复问题清单 | 嵌入各 subagent prompt（不单独存文件） | 主 agent |
| 2 | 12 份 BC 修复计划 | `docs/superpowers/plans/2026-07-22-fix-by-bc/fix-01-userauth.md` ~ `fix-12-shared.md` | 各 BC subagent |
| 3 | 1 份跨 BC/架构修复计划 | `docs/superpowers/plans/2026-07-22-fix-by-bc/fix-13-cross-bc-architecture.md` | 跨 BC subagent |
| 4 | 总览索引 + 后校验报告 | `docs/superpowers/plans/2026-07-22-fix-by-bc/README.md` | 主 agent |

合计：14 份文件（13 份计划 + 1 份 README）

### 6.2 单份计划验收标准

| # | 标准 | 验证方法 |
|---|------|---------|
| 1 | 元数据章节齐全（审计报告链接、问题总数、已修复数、覆盖数） | 主 agent 检查文件头 |
| 2 | 问题清单总表覆盖该 BC 审计报告全量 🔴/🟡/🟢 问题 | 主 agent 比对审计报告问题数 |
| 3 | P0 项全部用 TDD bite-sized 格式（5 步：测试→验证失败→实现→验证通过→提交） | 主 agent 抽查 |
| 4 | P1/P2 项用任务清单格式（修复步骤+影响范围+验证方法） | 主 agent 抽查 |
| 5 | [ALREADY-FIXED] 项仅列入"已修复项"表，无详细修复步骤 | 主 agent 校验 |
| 6 | 所有引用用 `file:///workspace/...#L行号` 格式 | 主 agent Grep 检查 |
| 7 | 不修改任何业务代码 | 主 agent `git status` 检查只有计划文件变更 |
| 8 | subagent 已自行 git commit + push | 主 agent `git log` 检查 |

### 6.3 整体编排验收标准

| # | 标准 | 验证方法 |
|---|------|---------|
| 1 | 13 份计划 + README 共 14 份文件全部生成 | `ls docs/superpowers/plans/2026-07-22-fix-by-bc/*.md \| wc -l` = 14 |
| 2 | P0 覆盖完整性 ≥ 95%（允许 ≤5 项缺口） | README "P0 覆盖完整性校验"章节 |
| 3 | 后校验 3 项全部通过或异常已记录 | README "后校验报告"章节 |
| 4 | 所有 commit 已推送到远程 | `git status` 显示 up to date |

### 6.4 执行约束

| # | 约束 | 说明 |
|---|------|------|
| 1 | subagent 类型固定 `general_purpose_task` | 需 Write 工具写文件 + git commit |
| 2 | 并行批次 6+7 | 不超过系统并行限制 |
| 3 | subagent 不修改业务代码 | 只产出计划文件 |
| 4 | 引用格式统一 | `file:///workspace/...#L行号` |
| 5 | commit message 统一 | `docs: 新增{BC_NAME}修复实施计划` / `docs: 新增跨BC与架构级修复实施计划` |
| 6 | git push 由 subagent 自行执行 | 主 agent 仅 push README |
| 7 | 失败不阻塞 | 单 subagent 失败不影响其他，主 agent 在 README 记录 |

### 6.5 代码完整性契约

所有 subagent 产出的 P0 TDD 步骤中的代码片段必须：
- 完整实现，禁止 `// TODO`、`// ...`、空函数体、`throw new NotImplementedException()`
- 含完整 `using`/`import` 语句
- 可直接复制到 IDE 编译执行（仅缺少外部环境配置除外）
- 测试代码必须含完整 Arrange/Act/Assert

---

## 7 后续步骤

本设计文档经用户审查通过后，将转交 `writing-plans` skill 生成可执行的实施计划，实施计划将指导主 agent 按三阶段编排执行：
1. 阶段 1：主 agent 预处理提取已修复清单
2. 阶段 2：并行启动 13 个 subagent 产出 13 份修复计划
3. 阶段 3：主 agent 后校验聚合产出 README
