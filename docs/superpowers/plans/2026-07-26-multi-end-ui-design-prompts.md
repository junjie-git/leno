# 4 端 UI 设计提示词生成 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基于 `docs/superpowers/specs/2026-07-26-multi-end-ui-design-prompts-design.md` 设计文档，产出 143 个 UI 设计提示词文件（5 共享 + 4 总览 + 133 页面 + 1 README），用于指导 Design with TRAE 生成 Vue 3 前端界面。

**Architecture:** 三阶段编排——主代理阶段 0 同步产出 5 份共享基线文档 → 阶段 1 在同一消息内并行启动 4 个 `general_purpose_task` subagent（每端独立产出总览 + 页面提示词）→ 阶段 2 主代理执行 9 项一致性校验、生成 README、git 提交推送。

**Tech Stack:** Markdown 文档；4 端共享设计系统基于 Vue 3.5 + Ant Design Vue 4.x（三端后台）+ Vant 4.x（用户 APP）+ Pinia 2.x + Vue Router 4.x + @vue-echarts 7.x。

**Spec Reference:** `docs/superpowers/specs/2026-07-26-multi-end-ui-design-prompts-design.md`

---

## File Structure

总产出 143 个文件，分布在 `docs/design-prompts/` 下：

```
docs/design-prompts/
├── README.md                                    # 主代理阶段 2 产出
├── shared/                                      # 主代理阶段 0 产出（5 个文件）
│   ├── design-system.md                         # 第 2 节设计系统规范
│   ├── prompt-template.md                       # 4.2/4.3/4.5 节模板
│   ├── writing-guide.md                         # 文案/微交互/异常处理约定
│   ├── glossary.md                              # 术语表（源自 docs/spec/00 第 3.4 节）
│   └── components.md                            # 跨端共享业务组件清单
├── system-admin/                                # Subagent A 产出（29 个文件）
│   ├── 00-overview.md
│   └── 01-dashboard/ ... 07-monitoring/         # 28 个页面提示词
├── operations/                                  # Subagent B 产出（35 个文件）
│   ├── 00-overview.md
│   └── 01-dashboard/ ... 10-data-export/        # 34 个页面提示词
├── seller/                                      # Subagent C 产出（24 个文件）
│   ├── 00-overview.md
│   └── 01-onboarding/ ... 09-export/            # 23 个页面提示词
└── buyer-app/                                   # Subagent D 产出（49 个文件）
    ├── 00-overview.md
    └── 01-auth/ ... 14-public/                  # 48 个页面提示词
```

**文件责任划分**：
- `shared/*.md`：4 端共同遵循的设计规范，是 subagent 的唯一规范来源
- `{端}/00-overview.md`：端级总览（6 段结构），先于页面提示词产出
- `{端}/{模块}/{页面}.md`：单个页面提示词（8 段结构），自包含且可独立喂给 Design with TRAE
- `README.md`：4 端总览入口，最后产出

---

## Phase 1: 主代理阶段 0 - 共享基线产出

### Task 1: 创建 shared 目录与 design-system.md

**Files:**
- Create: `docs/design-prompts/shared/design-system.md`

- [ ] **Step 1: 创建目录**

Run: `mkdir -p docs/design-prompts/shared`
Expected: 目录创建成功，无输出

- [ ] **Step 2: 写入 design-system.md**

文件内容必须严格对齐 spec 第 2 节（2.1-2.7），包含：
- 2.1 技术栈选型表（4 端 × 6 列）
- 2.2 设计令牌（color/radius/spacing/font，含具体数值）
- 2.3 布局栅格（三端后台 BasicLayout + 用户 APP Tabbar）
- 2.4 i18n 与主题预留（vue-i18n 9.x + ConfigProvider token）
- 2.5 文案与微交互风格（4 条风格规则）
- 2.6 组件库统一约定（5 类组件）
- 2.7 工程化约定（5 条 Vue 3 特有约定）

写入路径：`docs/design-prompts/shared/design-system.md`
字数目标：~1500 字

- [ ] **Step 3: 验证文件无占位符**

Run: `grep -E "TODO|TBD|FIXME|占位|省略" docs/design-prompts/shared/design-system.md`
Expected: 无匹配输出（exit code 1）

- [ ] **Step 4: 验证关键令牌数值存在**

Run: `grep -E "#1677FF|#52C41A|#FAAD14|#FF4D4F|6px|8px|4px" docs/design-prompts/shared/design-system.md`
Expected: 至少 7 行匹配

- [ ] **Step 5: Commit**

```bash
git add docs/design-prompts/shared/design-system.md
git commit -m "docs(design-prompts): 新增共享设计系统规范"
```

---

### Task 2: 写入 prompt-template.md

**Files:**
- Create: `docs/design-prompts/shared/prompt-template.md`

- [ ] **Step 1: 写入模板文档**

内容必须包含三部分：
1. **页面提示词 8 段模板**（spec 4.2 节）：页面定位 / 页面布局与信息架构 / 数据模型与 API 对接 / 交互流程 / 组件清单 / 视觉规范 / 异常处理与边界 / 验收要点。每段给出标题 + 字段列表 + 简短示例
2. **端总览 6 段模板**（spec 4.3 节）：端定位与角色画像 / 信息架构与导航 / 页面路由规划 / 全局布局 / 设计风格基调 / 模块清单
3. **写作规范**（spec 4.5 节）：语言/代码块/长度/API 格式/实现状态标注/共享组件引用 6 条规则

字数目标：~1200 字

- [ ] **Step 2: 验证 8 段标题齐全**

Run: `grep -c "^## [1-8]\." docs/design-prompts/shared/prompt-template.md`
Expected: 至少 8（页面模板部分）

- [ ] **Step 3: 验证实现状态标注说明存在**

Run: `grep -E "✅ 已实现|🚧 规划中|➕ 补充功能" docs/design-prompts/shared/prompt-template.md`
Expected: 3 行匹配

- [ ] **Step 4: Commit**

```bash
git add docs/design-prompts/shared/prompt-template.md
git commit -m "docs(design-prompts): 新增提示词写作模板与端总览模板"
```

---

### Task 3: 写入 writing-guide.md

**Files:**
- Create: `docs/design-prompts/shared/writing-guide.md`

- [ ] **Step 1: 写入文案与微交互指南**

内容包含 6 部分：
1. **文案风格**：简洁中性、动词开头、避免口语化（含 3 个正反例）
2. **微交互**：表单防抖 300ms、列表骨架屏、message 1.5s、Modal.confirm 二次确认
3. **空状态**：Empty 占位 + CTA 按钮（含 CTA 文案规范）
4. **加载态**：Skeleton（首屏/卡片）vs Spin（按钮/局部刷新）使用场景
5. **危险操作确认**：删除/暂停/关闭/驳回/强制取消/丢弃/重投 必须二次确认，确认按钮 `danger` 类型
6. **按钮文案统一**：提交/保存/确认/取消/删除/编辑/查看/导出 等动词清单

字数目标：~800 字

- [ ] **Step 2: 验证关键规则存在**

Run: `grep -E "Modal.confirm|danger|Skeleton|Spin|Empty" docs/design-prompts/shared/writing-guide.md`
Expected: 至少 5 行匹配

- [ ] **Step 3: Commit**

```bash
git add docs/design-prompts/shared/writing-guide.md
git commit -m "docs(design-prompts): 新增文案与微交互风格指南"
```

---

### Task 4: 写入 glossary.md

**Files:**
- Create: `docs/design-prompts/shared/glossary.md`

- [ ] **Step 1: 读取源术语表**

Run: 无需执行命令，直接读取 `docs/spec/00-需求文档总览与DDD架构.md` 第 107-125 行（3.4 节统一语言术语表）

- [ ] **Step 2: 写入术语表文档**

内容包含两部分：
1. **跨上下文核心术语表**：从 spec 00 第 3.4 节完整复制 12 个术语（SPU/SKU/聚合根/领域事件/集成事件/预占库存/真实库存/积分/成长值/会员等级/付费会员/支付单）
2. **BC 缩写与角色术语**：
   - 11 个 BC 缩写（BC1 UserAuth ~ BC11 SystemAdmin）
   - 4 类角色术语：买家（Buyer，不用"用户"指代消费者）、卖家（Seller，不用"商户"）、运营管理员（Operator，首次全称后简称"运营"）、系统管理员（Admin，首次全称后简称"Admin"仅代码语境）
   - 同义词禁用清单：店铺 vs 商铺、商品 vs 产品、优惠券 vs coupon、秒杀 vs 闪购

字数目标：~600 字

- [ ] **Step 3: 验证术语数量**

Run: `grep -c "^|" docs/design-prompts/shared/glossary.md`
Expected: 至少 20（12 核心术语 + 11 BC + 4 角色 + 表头分隔）

- [ ] **Step 4: Commit**

```bash
git add docs/design-prompts/shared/glossary.md
git commit -m "docs(design-prompts): 新增统一术语表与同义词禁用清单"
```

---

### Task 5: 写入 components.md

**Files:**
- Create: `docs/design-prompts/shared/components.md`

- [ ] **Step 1: 写入跨端共享业务组件清单**

定义 4 端可复用的业务组件（每个组件给出名称、用途、Props、使用场景）：

1. **StatusTag**：通用状态标签，Props：`status` / `color` / `text`，用于订单/售后/店铺/商品等状态展示
2. **IdempotencyButton**：幂等提交按钮，Props：`loading` / `disabled` / `@click`，内置 300ms 防抖 + 重复点击拦截
3. **PermissionGuard**：权限守卫组件，Props：`permission` / `fallback`，用于按钮级权限控制
4. **DateTimeRangePicker**：日期时间范围选择器（封装 Ant Design Vue RangePicker），Props：`v-model` / `presets`
5. **EmptyState**：空状态组件，Props：`title` / `description` / `ctaText` / `@cta-click`
6. **DataTable**：数据表格组件（封装 Ant Design Vue Table，含虚拟滚动 + 分页 + 排序），Props：`columns` / `data` / `loading` / `total`
7. **ChartLine / ChartPie / ChartBar / ChartGauge**：4 类图表组件（封装 @vue-echarts），Props：`data` / `options`
8. **DashboardCard**：看板卡片组件（含标题 + 数值 + 趋势 + 图表插槽），Props：`title` / `value` / `trend` / `#chart`
9. **AuditLogViewer**：审计日志查看器（只读 Descriptions + JSON 展开），Props：`logId`
10. **ConfirmDialog**：危险操作确认对话框（封装 Modal.confirm），Props：`title` / `content` / `danger` / `@confirm`

字数目标：~500 字

- [ ] **Step 2: 验证组件数量**

Run: `grep -c "^### " docs/design-prompts/shared/components.md`
Expected: 至少 10

- [ ] **Step 3: Commit**

```bash
git add docs/design-prompts/shared/components.md
git commit -m "docs(design-prompts): 新增跨端共享业务组件清单（10 个组件）"
```

---

### Task 6: 阶段 0 完整性验证

**Files:**
- Verify: `docs/design-prompts/shared/`

- [ ] **Step 1: 验证 5 个文件全部存在**

Run: `ls docs/design-prompts/shared/`
Expected: 输出包含 `components.md`、`design-system.md`、`glossary.md`、`prompt-template.md`、`writing-guide.md` 共 5 个文件

- [ ] **Step 2: 验证无占位符**

Run: `grep -rE "TODO|TBD|FIXME|占位|省略|此处" docs/design-prompts/shared/`
Expected: 无匹配输出（exit code 1）

- [ ] **Step 3: 推送到远程**

```bash
git push origin dev
```

Expected: 推送成功

---

## Phase 2: 4 端并行 Subagent 执行

### Task 7: 并行启动 4 个 subagent

**Files:**
- Create: `docs/design-prompts/system-admin/` 下 29 个文件
- Create: `docs/design-prompts/operations/` 下 35 个文件
- Create: `docs/design-prompts/seller/` 下 24 个文件
- Create: `docs/design-prompts/buyer-app/` 下 49 个文件

- [ ] **Step 1: 在同一条消息内并行发起 4 个 Task 工具调用**

**关键约束**：4 个 Task 调用必须在**同一条消息**内，4 个 subagent 才会真正并行执行。

每个 Task 的 `subagent_type` 为 `general_purpose_task`，`query` 字段使用下方 Task 8-11 的完整描述。

**Subagent 任务清单**：
- Task 8: Subagent A - 系统管理后台（29 文件）
- Task 9: Subagent B - 运营管理后台（35 文件）
- Task 10: Subagent C - 商家管理后台（24 文件）
- Task 11: Subagent D - 用户 APP（49 文件，若超限则拆分为 01-07 + 08-14 两个 subagent）

- [ ] **Step 2: 等待 4 个 subagent 全部完成**

收集每个 subagent 返回的摘要（产出统计 / 模块清单 / 关键决策 / API 端点数 / 偏离点 / 待校验项）。

- [ ] **Step 3: 记录 4 份摘要到主代理上下文**

主代理暂存 4 份摘要，用于阶段 2 校验。

---

### Task 8: Subagent A - 系统管理后台

**Subagent 任务描述（直接作为 Task 工具的 query 参数）：**

你是 Leno 电商平台的 UI 设计提示词工程师，负责为「系统管理后台」生成 Design with TRAE 用的页面设计提示词。

**工作目录**：e:\Leno

**输入资源（必读）**：
1. 共享设计系统规范：`docs/design-prompts/shared/design-system.md`
2. 提示词写作模板：`docs/design-prompts/shared/prompt-template.md`
3. 写作风格指南：`docs/design-prompts/shared/writing-guide.md`
4. 术语表：`docs/design-prompts/shared/glossary.md`
5. 共享组件清单：`docs/design-prompts/shared/components.md`
6. 设计文档（含功能清单与目录结构）：`docs/superpowers/specs/2026-07-26-multi-end-ui-design-prompts-design.md`（重点参考第 3.1 节功能清单、第 4.1 节目录结构、第 5.4.1 节端特定参数）
7. 后端需求文档：`docs/spec/12-系统管理域.md`、`docs/spec/01-用户与认证授权域.md`、`docs/spec/10-模块化部署架构.md`
8. 后端 API 端点：扫描 `src/Backends/Leno.SystemAdmin.Api/Controllers/` 下所有控制器，提取方法/路径/鉴权要求

**端定位**：
- 目标用户：系统管理员（Admin）
- 设备形态：桌面 1440+，不支持移动端
- 网关鉴权：JWT + 双因子 + IP 白名单 + 全操作审计
- 整体气质：严肃专业，低频重操作

**输出目录**：`docs/design-prompts/system-admin/`

**输出要求**：
1. **必产出** `00-overview.md`（端总览，使用 prompt-template.md 中的 6 段端总览模板）
2. **必产出** 所有模块目录下的页面提示词文件（按设计文档 4.1 节目录结构）
3. 每个页面提示词严格遵循 `prompt-template.md` 的 8 段结构
4. 每个提示词文件 800-1500 字
5. 所有 API 端点引用必须真实存在（来自控制器扫描或 docs/spec/），不得编造
6. 实现状态标注：✅ 已实现 / 🚧 规划中 / ➕ 补充功能
7. 文案统一中文，技术术语保留英文
8. 代码示例使用 vue/typescript/json 语言标签

**模块与页面清单**（7 个模块 / 28 个页面 + 1 总览 = 29 文件）：
- `00-overview.md`：端总览
- `01-dashboard/`（7 页面）：operations-overview.md、payment-stats.md、points-stats.md、notification-delivery.md、after-sales-stats.md、shop-ranking.md、report-snapshots.md
- `02-user-access/`（4 页面）：user-management.md、role-management.md、oauth-clients.md、operators.md
- `03-system-governance/`（4 页面）：feature-flags.md、system-configs.md、data-dictionaries.md、announcements.md
- `04-runtime-ops/`（6 页面）：rate-limit-rules.md、index-rebuild.md、dead-letter-queue.md、scheduled-tasks.md、health-monitoring.md、alert-management.md（🚧）
- `05-audit/`（3 页面）：audit-logs.md、reconciliation.md、outbox-monitor.md（🚧）
- `06-account/`（3 页面）：login-2fa.md、profile.md、notifications.md
- `07-monitoring/`（1 页面）：prometheus-dashboard.md（➕）

**API 端点来源**：
- SystemAdmin BC：13 控制器 74 端点（扫描 `src/Backends/Leno.SystemAdmin.Api/Controllers/`）
- UserAuth BC：AdminUsersController、AdminRolesController、AdminOAuthClientsController 端点
- 共享端点：dashboard/health 等与运营后台共享

**写作顺序**：
1. 先写 `00-overview.md`
2. 按模块顺序 01 → 07 逐个写页面提示词
3. 每写完一个模块自检：API 端点引用是否准确、术语是否统一、格式是否符合 8 段模板

**完成标志**：
- 29 个文件已写入 `docs/design-prompts/system-admin/`
- 每个文件符合 8 段结构（总览符合 6 段）
- 无 TODO/TBD/占位符
- 返回简短摘要：产出文件数、按模块统计、关键决策点、引用的 API 端点数、与共享设计系统的偏离点、待主代理校验项

---

### Task 9: Subagent B - 运营管理后台

**Subagent 任务描述（直接作为 Task 工具的 query 参数）：**

你是 Leno 电商平台的 UI 设计提示词工程师，负责为「运营管理后台」生成 Design with TRAE 用的页面设计提示词。

**工作目录**：e:\Leno

**输入资源（必读）**：
1. 共享设计系统规范：`docs/design-prompts/shared/design-system.md`
2. 提示词写作模板：`docs/design-prompts/shared/prompt-template.md`
3. 写作风格指南：`docs/design-prompts/shared/writing-guide.md`
4. 术语表：`docs/design-prompts/shared/glossary.md`
5. 共享组件清单：`docs/design-prompts/shared/components.md`
6. 设计文档：`docs/superpowers/specs/2026-07-26-multi-end-ui-design-prompts-design.md`（重点参考第 3.2 节、第 4.1 节、第 5.4.2 节）
7. 后端需求文档：`docs/spec/02-商品域.md`、`docs/spec/05-促销域.md`、`docs/spec/06-评价与售后域.md`、`docs/spec/07-积分与会员域.md`、`docs/spec/08-支付集成域.md`、`docs/spec/09-消息通知集成.md`、`docs/spec/11-卖家与店铺管理域.md`
8. 后端 API 端点：扫描跨 BC 的 `/api/admin/*` 端点（Product/Promotion/Order/Payment/Notification/SellerShop/SystemAdmin BC 的 Controllers）

**端定位**：
- 目标用户：运营管理员（Operator）
- 设备形态：桌面 1440+，不支持移动端
- 网关鉴权：JWT + 操作二次确认 + IP 白名单 + 操作审计
- 整体气质：简洁现代，低频重操作 + 数据看板

**输出目录**：`docs/design-prompts/operations/`

**输出要求**：
1. **必产出** `00-overview.md`（端总览，6 段结构）
2. **必产出** 所有模块目录下的页面提示词文件
3. 每个页面提示词严格遵循 8 段结构
4. 每个提示词文件 800-1500 字
5. 所有 API 端点引用必须真实存在，不得编造
6. 实现状态标注：✅ 已实现 / 🚧 规划中 / ➕ 补充功能
7. 文案统一中文，技术术语保留英文
8. 代码示例使用 vue/typescript/json 语言标签

**模块与页面清单**（10 个模块 / 34 个页面 + 1 总览 = 35 文件）：
- `00-overview.md`：端总览
- `01-dashboard/`（6 页面）：operations-overview.md、payment-stats.md、points-stats.md、notification-delivery.md、after-sales-stats.md、shop-ranking.md（不含 report-snapshots，那是系统管理专有）
- `02-product-ops/`（3 页面）：product-audit.md、brand-management.md、category-management.md
- `03-promotion-ops/`（3 页面）：promotions.md、coupons.md、seckill.md
- `04-seller-ops/`（3 页面）：application-audit.md、shop-governance.md、seller-statistics.md（🚧）
- `05-order-ops/`（4 页面）：order-management.md、after-sales.md、review-audit.md、logistics-companies.md
- `06-payment-ops/`（3 页面）：payment-records.md、refund-records.md、payment-channels.md
- `07-notification-ops/`（4 页面）：templates.md、records.md、config.md、rate-limits.md
- `08-membership-ops/`（3 页面）：member-levels.md、membership-packages.md、points-rules.md（🚧）
- `09-account/`（4 页面）：login.md、profile.md、todo-workbench.md（➕）、notifications.md
- `10-data-export/`（1 页面）：export-center.md（➕）

**与系统管理后台 dashboard 的关系**：
- 01-dashboard 模块的 6 个页面与 system-admin/01-dashboard 共享同一组 API 端点（`/api/admin/dashboard/*`）
- 但权限说明不同（运营管理员 vs 系统管理员）、入口路径不同（左侧菜单"数据看板" vs "仪表盘"）
- 各自独立成文，不互相引用

**API 端点来源**：
- 跨 BC 的 `/api/admin/*` 端点（扫描各 BC 的 Controllers 下的 AdminXxxController）
- UserAuth BC：AdminUsersController（运营管理员账号）
- 共享端点：dashboard 与系统管理后台共享

**写作顺序**：
1. 先写 `00-overview.md`
2. 按模块顺序 01 → 10 逐个写页面提示词
3. 每写完一个模块自检

**完成标志**：
- 35 个文件已写入 `docs/design-prompts/operations/`
- 每个文件符合 8 段结构
- 无 TODO/TBD/占位符
- 返回简短摘要

---

### Task 10: Subagent C - 商家管理后台

**Subagent 任务描述（直接作为 Task 工具的 query 参数）：**

你是 Leno 电商平台的 UI 设计提示词工程师，负责为「商家管理后台」生成 Design with TRAE 用的页面设计提示词。

**工作目录**：e:\Leno

**输入资源（必读）**：
1. 共享设计系统规范：`docs/design-prompts/shared/design-system.md`
2. 提示词写作模板：`docs/design-prompts/shared/prompt-template.md`
3. 写作风格指南：`docs/design-prompts/shared/writing-guide.md`
4. 术语表：`docs/design-prompts/shared/glossary.md`
5. 共享组件清单：`docs/design-prompts/shared/components.md`
6. 设计文档：`docs/superpowers/specs/2026-07-26-multi-end-ui-design-prompts-design.md`（重点参考第 3.3 节、第 4.1 节、第 5.4.3 节）
7. 后端需求文档：`docs/spec/11-卖家与店铺管理域.md`、`docs/spec/02-商品域.md`、`docs/spec/04-订单与交易域.md`、`docs/spec/06-评价与售后域.md`
8. 后端 API 端点：扫描 SellerShop BC（`src/Backends/Leno.SellerShop.Api/Controllers/`）、Product BC（Seller 角色端点）、Order BC（Seller 端点 + FreightTemplatesController）、ReviewAfterSales BC（reply + seller 端点）、UserAuth BC（AuthController + UsersController 个人中心）

**端定位**：
- 目标用户：卖家（Seller）
- 设备形态：桌面 1440+，不支持移动端
- 网关鉴权：JWT + 卖家角色校验 + 店铺级限流
- 整体气质：简洁现代，中低频写

**输出目录**：`docs/design-prompts/seller/`

**输出要求**：
1. **必产出** `00-overview.md`（端总览，6 段结构）
2. **必产出** 所有模块目录下的页面提示词文件
3. 每个页面提示词严格遵循 8 段结构
4. 每个提示词文件 800-1500 字
5. 所有 API 端点引用必须真实存在，不得编造
6. 实现状态标注：✅ 已实现 / 🚧 规划中 / ➕ 补充功能
7. 文案统一中文，技术术语保留英文
8. 代码示例使用 vue/typescript/json 语言标签

**模块与页面清单**（9 个模块 / 23 个页面 + 1 总览 = 24 文件）：
- `00-overview.md`：端总览
- `01-onboarding/`（4 页面）：application.md、shop-profile.md、qualifications.md、shop-preview.md（🚧）
- `02-dashboard/`（3 页面）：overview.md、sales-trend.md、low-stock-alert.md（➕）
- `03-product-management/`（4 页面）：product-list.md、product-edit.md、sku-management.md、price-history.md
- `04-logistics/`（2 页面）：freight-templates.md、logistics-companies.md
- `05-order-fulfillment/`（3 页面）：pending-shipment.md、order-list.md、logistics-trace.md
- `06-after-sales/`（2 页面）：after-sales-list.md、after-sales-detail.md
- `07-review/`（1 页面）：review-reply.md
- `08-account/`（3 页面）：login.md、profile.md、notifications.md
- `09-export/`（1 页面）：sales-export.md（➕）

**API 端点来源**：
- SellerShop BC：ShopsController、SellerDashboardController、AdminShopsController（卖家可访问部分）
- Product BC：ProductsController（Seller 角色端点）
- Order BC：OrdersController（Seller 端点）、FreightTemplatesController
- ReviewAfterSales BC：ReviewsController（reply）、AfterSalesController（seller 端点）
- UserAuth BC：AuthController、UsersController（个人中心）

**写作顺序**：
1. 先写 `00-overview.md`
2. 按模块顺序 01 → 09 逐个写页面提示词
3. 每写完一个模块自检

**完成标志**：
- 24 个文件已写入 `docs/design-prompts/seller/`
- 每个文件符合 8 段结构
- 无 TODO/TBD/占位符
- 返回简短摘要

---

### Task 11: Subagent D - 用户 APP

**Subagent 任务描述（直接作为 Task 工具的 query 参数）：**

你是 Leno 电商平台 APP 端的 UI 设计提示词工程师，负责为「用户 APP」生成 Design with TRAE 用的页面设计提示词。

**工作目录**：e:\Leno

**输入资源（必读）**：
1. 共享设计系统规范：`docs/design-prompts/shared/design-system.md`
2. 提示词写作模板：`docs/design-prompts/shared/prompt-template.md`
3. 写作风格指南：`docs/design-prompts/shared/writing-guide.md`
4. 术语表：`docs/design-prompts/shared/glossary.md`
5. 共享组件清单：`docs/design-prompts/shared/components.md`
6. 设计文档：`docs/superpowers/specs/2026-07-26-multi-end-ui-design-prompts-design.md`（重点参考第 3.4 节、第 4.1 节、第 5.4.4 节）
7. 后端需求文档：`docs/spec/01-用户与认证授权域.md`、`docs/spec/02-商品域.md`、`docs/spec/03-购物车域.md`、`docs/spec/04-订单与交易域.md`、`docs/spec/05-促销域.md`、`docs/spec/06-评价与售后域.md`、`docs/spec/07-积分与会员域.md`、`docs/spec/08-支付集成域.md`、`docs/spec/09-消息通知集成.md`
8. 后端 API 端点：扫描各 BC 的 Buyer 端点（UserAuth、Product、Cart、Order、Payment、Promotion、ReviewAfterSales、PointsMembership、Notification、SellerShop 公开端点、SystemAdmin 公开端点）

**端定位**：
- 目标用户：买家（Buyer）
- 设备形态：移动 375+，PWA，底部 TabBar 导航（首页/分类/购物车/我的）
- 网关鉴权：JWT + 买家角色校验 + 滑动窗口限流
- 整体气质：简洁现代，高并发读 + 秒杀峰值

**输出目录**：`docs/design-prompts/buyer-app/`

**输出要求**：
1. **必产出** `00-overview.md`（端总览，6 段结构）
2. **必产出** 所有模块目录下的页面提示词文件
3. 每个页面提示词严格遵循 8 段结构
4. 每个提示词文件 800-1500 字
5. 所有 API 端点引用必须真实存在，不得编造
6. 实现状态标注：✅ 已实现 / 🚧 规划中 / ➕ 补充功能
7. 文案统一中文，技术术语保留英文
8. 代码示例使用 vue/typescript/json 语言标签
9. **组件库使用 Vant 4.x（`van-` 前缀），不得出现 Ant Design Vue 组件（`a-` 前缀）**

**模块与页面清单**（14 个模块 / 48 个页面 + 1 总览 = 49 文件）：
- `00-overview.md`：端总览
- `01-auth/`（5 页面）：login.md、register.md、forgot-password.md、oauth-login.md、two-factor.md
- `02-home/`（3 页面）：home-feed.md、banner.md、seckill-entry.md
- `03-catalog/`（4 页面）：category-nav.md、search.md、search-results.md、product-detail.md
- `04-shop/`（1 页面）：shop-detail.md
- `05-cart/`（3 页面）：anonymous-cart.md、cart.md、checkout-preview.md
- `06-order/`（5 页面）：order-create.md、order-list.md、order-detail.md、logistics-trace.md、seckill-order.md
- `07-payment/`（2 页面）：payment-initiate.md、payment-result.md
- `08-promotion/`（2 页面）：coupons-available.md、my-coupons.md
- `09-review/`（3 页面）：review-submit.md、my-reviews.md、product-reviews.md
- `10-after-sales/`（3 页面）：after-sales-apply.md、my-after-sales.md、after-sales-detail.md
- `11-points-membership/`（7 页面）：points-account.md、check-in.md、points-ledger.md、tasks-center.md、points-exchange.md、member-level.md、membership-packages.md
- `12-notification/`（2 页面）：notifications.md、preferences.md
- `13-profile/`（6 页面）：profile.md、addresses.md、security.md、favorites.md（➕）、history.md（➕）、settings.md
- `14-public/`（2 页面）：announcements.md、dictionaries.md

**双轨期处理**：
- PointsMembership BC（旧，双轨遗留）：PointsController、TasksController、MembersController、MembershipPackagesController 端点已实现，优先引用
- 新拆分的 Points BC / Membership BC：仅 Program.cs 占位，无业务端点，标注 🚧 并注明「待新 BC 上线后切换」
- 单个页面提示词不混用新旧 BC 端点

**写作顺序**：
1. 先写 `00-overview.md`
2. 按模块顺序 01 → 14 逐个写页面提示词
3. 每写完一个模块自检

**完成标志**：
- 49 个文件已写入 `docs/design-prompts/buyer-app/`
- 每个文件符合 8 段结构
- 无 TODO/TBD/占位符
- 返回简短摘要

---

## Phase 3: 主代理阶段 2 - 一致性校验与收尾

### Task 12: 文件完整性校验

**Files:**
- Verify: `docs/design-prompts/`

- [ ] **Step 1: 验证文件总数**

Run: `find docs/design-prompts -type f -name "*.md" | wc -l`
Expected: 143

- [ ] **Step 2: 验证各端文件数**

```bash
find docs/design-prompts/system-admin -type f -name "*.md" | wc -l   # 期望 29
find docs/design-prompts/operations -type f -name "*.md" | wc -l     # 期望 35
find docs/design-prompts/seller -type f -name "*.md" | wc -l         # 期望 24
find docs/design-prompts/buyer-app -type f -name "*.md" | wc -l      # 期望 49
find docs/design-prompts/shared -type f -name "*.md" | wc -l         # 期望 5
```

Expected: 29 / 35 / 24 / 49 / 5

- [ ] **Step 3: 验证端总览存在**

```bash
ls docs/design-prompts/system-admin/00-overview.md
ls docs/design-prompts/operations/00-overview.md
ls docs/design-prompts/seller/00-overview.md
ls docs/design-prompts/buyer-app/00-overview.md
```

Expected: 4 个文件全部存在

- [ ] **Step 4: 缺失文件补写**

若 Step 1-3 发现缺失文件，主代理直接补写。补写时遵循对应端的 prompt-template.md 8 段结构。

---

### Task 13: 8 段结构合规校验

**Files:**
- Verify: 4 端各抽检 3 个文件（共 12 个）

- [ ] **Step 1: 抽检 system-admin 3 个文件**

```bash
grep -c "^## " docs/design-prompts/system-admin/01-dashboard/operations-overview.md
grep -c "^## " docs/design-prompts/system-admin/02-user-access/user-management.md
grep -c "^## " docs/design-prompts/system-admin/05-audit/audit-logs.md
```

Expected: 每个文件至少 8（8 段标题）

- [ ] **Step 2: 抽检 operations 3 个文件**

```bash
grep -c "^## " docs/design-prompts/operations/02-product-ops/product-audit.md
grep -c "^## " docs/design-prompts/operations/05-order-ops/order-management.md
grep -c "^## " docs/design-prompts/operations/07-notification-ops/templates.md
```

Expected: 每个文件至少 8

- [ ] **Step 3: 抽检 seller 3 个文件**

```bash
grep -c "^## " docs/design-prompts/seller/02-dashboard/overview.md
grep -c "^## " docs/design-prompts/seller/03-product-management/product-list.md
grep -c "^## " docs/design-prompts/seller/05-order-fulfillment/pending-shipment.md
```

Expected: 每个文件至少 8

- [ ] **Step 4: 抽检 buyer-app 3 个文件**

```bash
grep -c "^## " docs/design-prompts/buyer-app/02-home/home-feed.md
grep -c "^## " docs/design-prompts/buyer-app/06-order/order-detail.md
grep -c "^## " docs/design-prompts/buyer-app/11-points-membership/check-in.md
```

Expected: 每个文件至少 8

- [ ] **Step 5: 端总览 6 段结构校验**

```bash
grep -c "^## " docs/design-prompts/system-admin/00-overview.md
grep -c "^## " docs/design-prompts/operations/00-overview.md
grep -c "^## " docs/design-prompts/seller/00-overview.md
grep -c "^## " docs/design-prompts/buyer-app/00-overview.md
```

Expected: 每个文件至少 6

- [ ] **Step 6: 不合规文件修复**

若任一文件段数不足，主代理读取该文件，补写缺失段落。

---

### Task 14: 术语统一性校验

**Files:**
- Verify: `docs/design-prompts/` 全目录

- [ ] **Step 1: 检查禁用同义词**

```bash
grep -rE "商铺|商户|闪购" docs/design-prompts/
```

Expected: 无匹配输出（exit code 1）

- [ ] **Step 2: 检查中文语境出现 coupon**

```bash
grep -rE "coupon" docs/design-prompts/ | grep -v "CouponsController\|/api/coupons\|couponId\|Coupon 模板\|英文"
```

Expected: 无匹配输出（仅允许 API 端点引用与英文对照保留）

- [ ] **Step 3: 检查"产品"误用**

```bash
grep -rE "产品" docs/design-prompts/ | grep -v "Product BC\|产品域\|ProductController"
```

Expected: 无匹配输出（仅允许 BC 名称与文档引用保留）

- [ ] **Step 4: 修复不一致术语**

若 Step 1-3 发现违规，使用 Edit 工具逐个文件修正：
- 商铺 → 店铺
- 商户 → 卖家
- 闪购 → 秒杀
- 产品 → 商品（仅业务语境）
- coupon → 优惠券（仅业务语境）

---

### Task 15: API 端点准确性抽检

**Files:**
- Verify: 4 端各抽检 10 个端点（共 40 个）

- [ ] **Step 1: 抽取 system-admin 10 个 API 引用**

```bash
grep -rE "(GET|POST|PUT|DELETE|PATCH) /api/" docs/design-prompts/system-admin/ | head -10
```

- [ ] **Step 2: 对照后端控制器验证**

对每个抽取的端点，使用 Grep 工具在 `src/Backends/Leno.SystemAdmin.Api/Controllers/` 或对应 BC 下查找：
- `[HttpGet("path")]` / `[HttpPost("path")]` 等特性
- 或 `MapGet("path")` / `MapPost("path")` 最小 API

Expected: 10 个端点全部能找到对应实现

- [ ] **Step 3: 抽取 operations 10 个 API 引用并验证**

```bash
grep -rE "(GET|POST|PUT|DELETE|PATCH) /api/admin/" docs/design-prompts/operations/ | head -10
```

对照各 BC 的 AdminXxxController 验证

- [ ] **Step 4: 抽取 seller 10 个 API 引用并验证**

```bash
grep -rE "(GET|POST|PUT|DELETE|PATCH) /api/(seller|shops|products|orders|reviews|after-sales|freight-templates)/" docs/design-prompts/seller/ | head -10
```

对照 SellerShop/Product/Order/ReviewAfterSales BC 验证

- [ ] **Step 5: 抽取 buyer-app 10 个 API 引用并验证**

```bash
grep -rE "(GET|POST|PUT|DELETE|PATCH) /api/" docs/design-prompts/buyer-app/ | head -10
```

对照各 BC 的 Buyer 端点验证

- [ ] **Step 6: 修复错误引用**

若发现编造或不存在的端点，主代理读取对应页面提示词文件，将错误端点替换为真实端点，或标注 🚧 规划中并注明对应需求文档章节。

---

### Task 16: 设计令牌与实现状态校验

**Files:**
- Verify: `docs/design-prompts/` 全目录

- [ ] **Step 1: 验证主色一致**

```bash
grep -rE "#1677FF" docs/design-prompts/system-admin/ docs/design-prompts/operations/ docs/design-prompts/seller/ docs/design-prompts/buyer-app/ | wc -l
```

Expected: 至少 20（4 端的视觉规范段都会引用主色）

- [ ] **Step 2: 验证无其他主色数值混入**

```bash
grep -rE "#1890FF|#0052CC|#409EFF" docs/design-prompts/
```

Expected: 无匹配输出（exit code 1）

- [ ] **Step 3: 验证实现状态标注齐全**

```bash
grep -rL "实现状态" docs/design-prompts/system-admin/0[1-7]*/  docs/design-prompts/operations/0[1-9]*/ docs/design-prompts/operations/10*/ docs/design-prompts/seller/0[1-9]*/ docs/design-prompts/buyer-app/0[1-9]*/ docs/design-prompts/buyer-app/1[0-4]*/
```

Expected: 无输出（所有页面提示词都包含「实现状态」字段）

- [ ] **Step 4: 验证 Vant/Ant Design Vue 区分**

```bash
grep -rE "<a-|@ant-design" docs/design-prompts/buyer-app/
```

Expected: 无匹配输出（用户 APP 不得使用 Ant Design Vue）

```bash
grep -rE "<van-|@vant" docs/design-prompts/system-admin/ docs/design-prompts/operations/ docs/design-prompts/seller/
```

Expected: 无匹配输出（三端后台不得使用 Vant）

- [ ] **Step 5: 修复违规**

若发现违规，主代理读取对应文件并修正：
- 错误主色 → `#1677FF`
- 缺失实现状态 → 在「页面定位」段补写
- 用户 APP 误用 Ant Design Vue → 替换为 Vant 等价组件
- 三端后台误用 Vant → 替换为 Ant Design Vue 等价组件

---

### Task 17: 危险操作二次确认校验

**Files:**
- Verify: `docs/design-prompts/` 全目录

- [ ] **Step 1: 查找包含危险操作的文件**

```bash
grep -rlE "删除|暂停|关闭|驳回|强制取消|丢弃|重投" docs/design-prompts/
```

- [ ] **Step 2: 对每个文件验证 Modal.confirm 存在**

对 Step 1 列出的每个文件，检查「异常处理与边界」段是否包含 `Modal.confirm`（三端后台）或 `Dialog.confirm`（用户 APP）描述：

```bash
grep -lE "Modal.confirm|Dialog.confirm" docs/design-prompts/system-admin/04-runtime-ops/dead-letter-queue.md
# 对每个危险操作文件重复
```

Expected: 危险操作文件都包含二次确认描述

- [ ] **Step 3: 修复缺失**

若发现危险操作文件未描述二次确认，主代理在该文件「异常处理与边界」段补写：
- 三端后台：`Modal.confirm({ title: '确认{操作}', content: '{后果说明}', okType: 'danger', onOk: () => 执行 })`
- 用户 APP：`showConfirmDialog({ title: '确认{操作}', message: '{后果说明}' }).then(() => 执行)`

---

### Task 18: 生成 README.md

**Files:**
- Create: `docs/design-prompts/README.md`

- [ ] **Step 1: 写入 README.md**

内容遵循 spec 4.4 节结构，包含 6 部分：
1. **项目背景**：一段话介绍 Leno 项目与本文档集的目的
2. **设计决策**：5 条决策（视觉风格、技术栈、主题、语言、主题模式）
3. **文档结构**：目录树说明（引用 spec 4.1 节）
4. **使用方式**：4 步使用流程
5. **4 端模块统计**：统计表（system-admin 7模块/28页面、operations 10/34、seller 9/23、buyer-app 14/48，合计 40 模块/133 页面）
6. **与后端 API 的对应关系**：说明提示词中标注的 API 端点来自 docs/spec/ 与已实现代码

- [ ] **Step 2: 验证 README 结构**

```bash
grep -c "^## " docs/design-prompts/README.md
```

Expected: 至少 6

- [ ] **Step 3: Commit**

```bash
git add docs/design-prompts/README.md
git commit -m "docs(design-prompts): 新增 README 总览与使用说明"
```

---

### Task 19: 最终 Git 提交与推送

**Files:**
- Modify: `docs/design-prompts/` 全目录（阶段 2 校验修正）

- [ ] **Step 1: 检查 git 状态**

Run: `git status`
Expected: 显示 docs/design-prompts/ 下的修改/新增文件

- [ ] **Step 2: 提交所有校验修正**

```bash
git add docs/design-prompts/
git commit -m "docs(design-prompts): 完成阶段 2 一致性校验与修正" -m "- 8 段结构合规校验（12 文件抽检）" -m "- 术语统一性校验（8 类禁用同义词）" -m "- API 端点准确性抽检（40 个端点）" -m "- 设计令牌与实现状态校验" -m "- 危险操作二次确认覆盖校验" -m "- README.md 总览生成"
```

- [ ] **Step 3: 推送到远程**

```bash
git push origin dev
```

Expected: 推送成功

---

### Task 20: 验收标准最终核验

**Files:**
- Verify: `docs/design-prompts/` 全目录

- [ ] **Step 1: 核验 AC-001（共享设计系统规范完整产出）**

```bash
ls docs/design-prompts/shared/design-system.md docs/design-prompts/shared/prompt-template.md docs/design-prompts/shared/writing-guide.md docs/design-prompts/shared/glossary.md docs/design-prompts/shared/components.md
```

Expected: 5 个文件全部存在

```bash
grep -rE "TODO|TBD|FIXME" docs/design-prompts/shared/
```

Expected: 无匹配

- [ ] **Step 2: 核验 AC-002（4 端目录结构完整，143 文件）**

```bash
find docs/design-prompts -type f -name "*.md" | wc -l
```

Expected: 143

- [ ] **Step 3: 核验 AC-003/004（8 段 / 6 段结构）**

参考 Task 12-13 的校验结果，全部通过

- [ ] **Step 4: 核验 AC-005（API 端点引用准确）**

参考 Task 15 的抽检结果，40 个端点全部真实存在

- [ ] **Step 5: 核验 AC-006（实现状态标注齐全）**

参考 Task 16 Step 3 的校验结果，全部通过

- [ ] **Step 6: 核验 AC-007（设计令牌统一）**

参考 Task 16 Step 1-2 的校验结果，主色统一 #1677FF

- [ ] **Step 7: 核验 AC-008（术语统一）**

参考 Task 14 的校验结果，8 类禁用同义词已清除

- [ ] **Step 8: 核验 AC-009（跨端共享端点视觉一致）**

```bash
diff <(grep -E "图表|Statistic|Chart" docs/design-prompts/system-admin/01-dashboard/operations-overview.md) <(grep -E "图表|Statistic|Chart" docs/design-prompts/operations/01-dashboard/operations-overview.md)
```

Expected: 图表类型与组件引用基本一致（仅权限/入口描述不同）

- [ ] **Step 9: 核验 AC-010（危险操作二次确认覆盖）**

参考 Task 17 的校验结果，全部通过

- [ ] **Step 10: 核验 AC-011（移动端与桌面端组件库区分）**

参考 Task 16 Step 4 的校验结果，Vant/Ant Design Vue 未混用

- [ ] **Step 11: 核验 AC-012（Git 提交与推送）**

```bash
git log --oneline -n 10
```

Expected: 显示本次任务的提交记录

```bash
git status
```

Expected: working tree clean，与 origin/dev 同步

- [ ] **Step 12: 输出验收报告**

主代理向用户输出验收报告，包含：
- 12 条 AC 的核验结果（全部 PASS）
- 4 端文件统计表
- 关键决策点汇总（来自 4 个 subagent 摘要）
- 推送到远程的 commit hash

---

## Self-Review Checklist

完成计划撰写后，对照 spec 检查：

**1. Spec 覆盖检查**：
- ✅ Spec 第 2 节（共享设计系统）→ Task 1（design-system.md）
- ✅ Spec 第 3 节（4 端功能清单）→ Task 8-11（subagent 任务描述中包含完整功能清单）
- ✅ Spec 第 4 节（提示词模板与产出结构）→ Task 2（prompt-template.md）+ Task 8-11（按目录结构产出）
- ✅ Spec 第 5 节（subagent 编排）→ Task 7（并行启动）+ Task 8-11（4 个 subagent 任务）
- ✅ Spec 第 6 节（验收与一致性保障）→ Task 12-20（9 项校验 + README + git + AC 核验）
- ✅ Spec 第 7 节（后续工作）→ 执行完毕后自然结束

**2. 占位符扫描**：
- 所有 Task 的代码块均为完整可执行内容
- 所有 Step 均有明确的 Run/Expected 或具体动作描述
- 无 "TBD"/"TODO"/"implement later"/"similar to Task N" 等占位

**3. 类型一致性**：
- 文件路径在 Task 间一致（如 `docs/design-prompts/shared/design-system.md` 在 Task 1 创建，Task 8-11 引用）
- 8 段结构标题在 Task 2（模板定义）与 Task 13（校验）一致
- 实现状态标注符号（✅🚧➕）在 Task 2（模板说明）与 Task 16（校验）一致

---

## Execution Handoff

计划已完成并保存至 `docs/superpowers/plans/2026-07-26-multi-end-ui-design-prompts.md`。两种执行方式：

**1. Subagent-Driven（推荐）** - 我每个 Task 派发独立 subagent，Task 之间进行评审，迭代快

**2. Inline Execution** - 在当前会话内顺序执行，批量执行 + 检查点评审

请选择执行方式。
