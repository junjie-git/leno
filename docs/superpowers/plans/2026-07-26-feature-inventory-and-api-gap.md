# 功能清单与 API 缺失对比报告 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基于 docs/design-prompts 与源码 Controllers，产出 4 端功能清单 + 11 个 BC 的 API 缺失对比报告，共 19 个文件。

**Architecture:** Subagent-Driven 三阶段并行：阶段 0 主代理建骨架 → 阶段 1 四个端级 subagent 并行抽功能清单 → 阶段 2 十一个 BC 级 subagent 并行做 API 对比（分 5+5+1 三批）→ 阶段 3 主代理聚合总览。

**Tech Stack:** Markdown 报告 + Grep/Read 源码扫描 + Git 提交。

**关联设计**：`docs/superpowers/specs/2026-07-26-feature-inventory-and-api-gap-design.md`

---

## 文件结构

```
docs/feature-inventory/
├── README.md                                # 主代理产出：入口 + 方法论 + BC 映射表
├── _shared/
│   └── report-template.md                   # 主代理产出：BC 报告统一模板
├── feature-list/
│   ├── buyer-app.md                         # subagent 产出：买家端 48 页面
│   ├── operations.md                        # subagent 产出：运营后台 34 页面
│   ├── seller.md                            # subagent 产出：商家后台 23 页面
│   ├── system-admin.md                      # subagent 产出：系统后台 28 页面
│   └── README.md                            # 主代理产出：清单索引与统计
└── api-gap/
    ├── 00-summary.md                        # 主代理产出：总览 + 优先级矩阵 + Top 10
    ├── bc1-user-auth.md                     # subagent 产出
    ├── bc2-product.md                       # subagent 产出
    ├── bc3-cart.md                          # subagent 产出
    ├── bc4-order.md                         # subagent 产出
    ├── bc5-promotion.md                     # subagent 产出
    ├── bc6-review-aftersales.md             # subagent 产出
    ├── bc7-points-membership.md             # subagent 产出
    ├── bc8-payment.md                       # subagent 产出
    ├── bc9-notification.md                  # subagent 产出
    ├── bc10-seller-shop.md                  # subagent 产出
    └── bc11-system-admin.md                 # subagent 产出
```

**文件总数**：1 顶层 README + 1 _shared 模板 + 5 feature-list + 12 api-gap = **19 个文件**

---

## Task 1: 创建目录骨架与 BC 报告模板

**Files:**
- Create: `docs/feature-inventory/README.md`
- Create: `docs/feature-inventory/_shared/report-template.md`
- Create: `docs/feature-inventory/feature-list/README.md`
- Create: `docs/feature-inventory/feature-list/buyer-app.md`（占位）
- Create: `docs/feature-inventory/feature-list/operations.md`（占位）
- Create: `docs/feature-inventory/feature-list/seller.md`（占位）
- Create: `docs/feature-inventory/feature-list/system-admin.md`（占位）
- Create: `docs/feature-inventory/api-gap/00-summary.md`（占位）
- Create: `docs/feature-inventory/api-gap/bc1-user-auth.md` ~ `bc11-system-admin.md`（11 个占位）

- [ ] **Step 1.1: 创建目录结构**

```bash
mkdir -p docs/feature-inventory/_shared
mkdir -p docs/feature-inventory/feature-list
mkdir -p docs/feature-inventory/api-gap
```

- [ ] **Step 1.2: 写 _shared/report-template.md**

完整内容（8 节模板，所有 subagent 必须严格遵循）：

```markdown
# BC{N} {BC 中文名} — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

## 1. 概览
- **BC 编号**：BC{N}
- **中文名**：{BC 中文名}
- **英文名**：{BC 英文名}
- **涉及端**：buyer-app / operations / seller / system-admin（勾选实际涉及的）
- **涉及页面数**：{N} 页（来自 feature-list）
- **已实现 API 端点数**：{N} 个（来自源码 Controller 扫描）
- **差异统计**：缺失 {X} / 闲置 {Y} / 路径不一致 {Z} / 能力不匹配 {W}

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| ... | ... | [Controller.cs](file:///e:/Leno/src/Services/.../Controller.cs#L{行号}) | ... | ... |

> 来源：grep `src/Services/{BC 目录}/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> Internal*Controller.cs 中的端点单独标注「（内部）」

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| ... | ... | [page.md](file:///e:/Leno/docs/design-prompts/{端}/{模块}/{page}.md) | ... | ✅/🚧/➕ | ... |

> 来源：design-prompts 的「数据与 API」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|
| ... | ... | [page.md](file:///e:/Leno/docs/design-prompts/.../page.md) | ... | P0/P1/P2 | ... |

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| ... | ... | [Controller.cs](file:///e:/Leno/src/Services/.../Controller.cs#L{行号}) | ... | 保留观察/设计稿补调用/后端废弃 |

> 说明：源码有实现但 design-prompts 中无任何页面引用

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|
| POST→PUT | /api/x → /api/y | [page.md](file:///e:/Leno/docs/design-prompts/.../page.md) | [Controller.cs](file:///e:/Leno/src/Services/.../Controller.cs#L{行号}) | 改文档/改代码 |

> 说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 分页+筛选+排序 | 分页 | 缺少筛选与排序 | [page.md](file:///e:/Leno/docs/design-prompts/.../page.md) | [Controller.cs](file:///e:/Leno/src/Services/.../Controller.cs#L{行号}) | 补 query 参数 |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异

## 5. 拆分过渡说明

> 仅 BC1 / BC6 / BC7 出现此节。其他 BC 写「本 BC 无拆分过渡」一句话。

- **旧 BC 与新 BC 对照**：
- **双轨期端点引用规范**：
- **待切换端点清单**：

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | ... | ... | ... | ... |
| P1 | ... | ... | ... | ... |
| P2 | ... | ... | ... | ... |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强

## 7. 跨 BC 依赖
- **上游依赖**：本 BC 依赖哪些 BC 的端点/事件
- **下游依赖**：哪些 BC 依赖本 BC 的端点/事件
- **集成事件订阅/发布清单**

## 8. 行动建议
- **立即修复**（P0 缺失/不一致）
- **短期补充**（P1 缺失/不匹配）
- **长期规划**（P2 闲置/废弃）
- **文档同步**（design-prompts API 引用对齐到源码）
```

- [ ] **Step 1.3: 写顶层 README.md（含 BC 映射表）**

完整内容：

```markdown
# Leno 功能清单与 API 缺失对比报告

**文档版本**：V1.0
**创建日期**：2026-07-26
**关联设计**：`docs/superpowers/specs/2026-07-26-feature-inventory-and-api-gap-design.md`
**关联源数据**：
- 设计提示词：`docs/design-prompts/`（143 个 Markdown）
- 设计稿：`docs/designs/`（153 个 HTML，仅作页面参考）
- 源码 Controllers：`src/Services/{BC}/**/Controllers/*.cs`（71 个 Controller 文件）

---

## 1. 方法论

采用 Subagent-Driven 三阶段并行方案：

1. **阶段 0**：主代理创建目录骨架与 BC 报告统一模板
2. **阶段 1**：4 个端级 subagent 并行抽取功能清单（buyer-app / operations / seller / system-admin）
3. **阶段 2**：11 个 BC 级 subagent 并行做 API 缺失对比（分 5+5+1 三批）
4. **阶段 3**：主代理聚合产出总览、清单索引与顶层 README

详细方法论见关联设计文档。

---

## 2. 报告索引

### 功能清单（按端拆分）

| 端 | 文件 | 页面数 |
|-|-|-|
| 买家端 APP | [feature-list/buyer-app.md](./feature-list/buyer-app.md) | 48 |
| 运营管理后台 | [feature-list/operations.md](./feature-list/operations.md) | 34 |
| 商家管理后台 | [feature-list/seller.md](./feature-list/seller.md) | 23 |
| 系统管理后台 | [feature-list/system-admin.md](./feature-list/system-admin.md) | 28 |

### API 缺失对比报告（按 BC 拆分）

| BC | 文件 | 涉及端 |
|-|-|-|
| BC1 用户与认证授权 | [api-gap/bc1-user-auth.md](./api-gap/bc1-user-auth.md) | buyer-app + operations + system-admin |
| BC2 商品 | [api-gap/bc2-product.md](./api-gap/bc2-product.md) | buyer-app + operations + seller |
| BC3 购物车 | [api-gap/bc3-cart.md](./api-gap/bc3-cart.md) | buyer-app |
| BC4 订单与交易 | [api-gap/bc4-order.md](./api-gap/bc4-order.md) | buyer-app + operations + seller |
| BC5 促销 | [api-gap/bc5-promotion.md](./api-gap/bc5-promotion.md) | buyer-app + operations |
| BC6 评价与售后 | [api-gap/bc6-review-aftersales.md](./api-gap/bc6-review-aftersales.md) | buyer-app + operations + seller |
| BC7 积分与会员 | [api-gap/bc7-points-membership.md](./api-gap/bc7-points-membership.md) | buyer-app + operations |
| BC8 支付集成 | [api-gap/bc8-payment.md](./api-gap/bc8-payment.md) | buyer-app + operations |
| BC9 消息通知 | [api-gap/bc9-notification.md](./api-gap/bc9-notification.md) | 4 端 |
| BC10 卖家与店铺 | [api-gap/bc10-seller-shop.md](./api-gap/bc10-seller-shop.md) | buyer-app + operations + seller |
| BC11 系统管理 | [api-gap/bc11-system-admin.md](./api-gap/bc11-system-admin.md) | system-admin |

### 总览

| 文件 | 内容 |
|-|-|
| [api-gap/00-summary.md](./api-gap/00-summary.md) | 11 BC 差异统计 + 优先级矩阵 + Top 10 修复项 |
| [feature-list/README.md](./feature-list/README.md) | 4 端清单索引 + 统计 |

---

## 3. BC → 源码目录映射表

| BC | 源码目录 | 涉及端 |
|-|-|-|
| BC1 用户与认证授权 | `src/Services/UserAuth/` + `src/Services/Identity/` | buyer-app + operations + system-admin |
| BC2 商品 | `src/Services/Product/` + `src/Services/Inventory/` | buyer-app + operations + seller |
| BC3 购物车 | `src/Services/Cart/` | buyer-app |
| BC4 订单与交易 | `src/Services/Order/` | buyer-app + operations + seller |
| BC5 促销 | `src/Services/Promotion/` | buyer-app + operations |
| BC6 评价与售后 | `src/Services/ReviewAfterSales/` + `src/Services/AfterSales/` | buyer-app + operations + seller |
| BC7 积分与会员 | `src/Services/PointsMembership/` + `src/Services/Points/` + `src/Services/Membership/` | buyer-app + operations |
| BC8 支付集成 | `src/Services/Payment/` | buyer-app + operations |
| BC9 消息通知 | `src/Services/Notification/` | 4 端 |
| BC10 卖家与店铺 | `src/Services/SellerShop/` | buyer-app + operations + seller |
| BC11 系统管理 | `src/Services/SystemAdmin/` | system-admin |

---

## 4. 4 类差异定义

| 类别 | 说明 |
|-|-|
| 缺失 | 设计稿需要但后端未提供（design-prompts 标 🚧/➕ 且源码无实现） |
| 闲置 | 后端已有但设计稿未调用（源码有实现但 design-prompts 无页面引用） |
| 路径不一致 | 方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配 |
| 能力不匹配 | 分页/筛选/排序/批量/字段过滤等能力差异 |

---

## 5. 拆分过渡态说明

| 主 BC | 旧 BC | 新 BC | 过渡策略 |
|-|-|-|-|
| BC1 用户与认证授权 | UserAuth | Identity | 双轨期优先引用 UserAuth，Identity 端点标 🚧 待切换 |
| BC6 评价与售后 | ReviewAfterSales | AfterSales（独立） | 双轨期优先引用 ReviewAfterSales，AfterSales 端点标 🚧 待切换 |
| BC7 积分与会员 | PointsMembership | Points + Membership | 双轨期优先引用 PointsMembership，新拆分端点标 🚧 待切换 |
```

- [ ] **Step 1.4: 写 feature-list/README.md 占位**

```markdown
# 功能清单索引

> 本文件由主代理在阶段 3 聚合产出，包含 4 端清单统计。

待阶段 3 填充。
```

- [ ] **Step 1.5: 写 4 份 feature-list 占位**

每份占位文件内容：

```markdown
# {端中文名} 功能清单

> 本文件由端级 subagent 在阶段 1 产出。

待阶段 1 填充。
```

- [ ] **Step 1.6: 写 12 份 api-gap 占位**

每份占位文件内容：

```markdown
# BC{N} {BC 中文名} — API 缺失对比报告

> 本文件由 BC 级 subagent 在阶段 2 产出。严格遵循 _shared/report-template.md 模板。

待阶段 2 填充。
```

`00-summary.md` 占位：

```markdown
# API 缺失对比报告总览

> 本文件由主代理在阶段 3 聚合产出。

待阶段 3 填充。
```

- [ ] **Step 1.7: 提交骨架**

```bash
git add docs/feature-inventory/
git commit -m "chore: 建立功能清单与API缺失对比报告骨架" -m "建立 docs/feature-inventory/ 目录结构与 BC 报告统一模板，含 1 顶层 README + 1 模板 + 5 feature-list + 12 api-gap 占位文件，共 19 个文件。"
```

---

## Task 2: 阶段 1 — 4 端功能清单抽取（4 个 subagent 并行）

**Files:**
- Modify: `docs/feature-inventory/feature-list/buyer-app.md`
- Modify: `docs/feature-inventory/feature-list/operations.md`
- Modify: `docs/feature-inventory/feature-list/seller.md`
- Modify: `docs/feature-inventory/feature-list/system-admin.md`

- [ ] **Step 2.1: 派发 4 个端级 subagent（单消息并行）**

主代理在单条消息中并行调用 4 个 `Task` 工具（subagent_type=`general_purpose_task`），每个 subagent 收到统一任务模板：

**通用任务模板**（替换 {端} 为具体端）：

```
你是功能清单抽取 subagent。任务：扫描 docs/design-prompts/{端}/ 下所有页面 Markdown，抽取功能清单并写入 docs/feature-inventory/feature-list/{端}.md。

【输入】
- docs/design-prompts/{端}/00-overview.md：端总览，含模块清单与路由表
- docs/design-prompts/{端}/**/*.md：所有页面提示词（每个页面含「3. 数据模型与 API 对接」段，含 API 表格）
- docs/designs/{端}/**/*.html：仅作页面存在性参考，不深度解析

【输出】
覆盖 docs/feature-inventory/feature-list/{端}.md 现有占位内容，写入完整功能清单。

【输出格式】
文件开头写：
# {端中文名} 功能清单

> 来源：docs/design-prompts/{端}/
> 页面数：{N}

然后是一张表格，每个页面一行：

| 序号 | 模块 | 页面 | 路由 | 实现状态 | 引用 API 端点 | 涉及 BC |
|-|-|-|-|-|-|-|

字段说明：
- 序号：从 1 开始递增
- 模块：design-prompts/{端}/ 下的二级目录名（如 01-auth）
- 页面：文件名去 .md（如 login）
- 路由：从 00-overview.md 的路由表提取（如 /login）
- 实现状态：✅/🚧/➕，从页面 .md 的「1. 页面定位」段的「实现状态」提取
- 引用 API 端点：从页面 .md 的「3. 数据模型与 API 对接」段的 API 表格提取，逐条列出「METHOD /api/path」，多个用逗号分隔
- 涉及 BC：根据 API 路径前缀推断 BC 编号（如 /api/account → BC1，/api/orders → BC4），多个 BC 用 + 连接

BC 路径前缀映射：
- /api/account, /api/auth, /api/users, /api/addresses, /api/roles, /api/oauth-clients → BC1
- /api/products, /api/categories, /api/brands, /api/search, /api/inventory → BC2
- /api/carts, /api/anonymous-carts, /api/checkout → BC3
- /api/orders, /api/logistics, /api/freight-templates → BC4
- /api/promotions, /api/coupons, /api/seckill → BC5
- /api/reviews, /api/after-sales → BC6
- /api/points, /api/members, /api/membership, /api/check-in, /api/tasks → BC7
- /api/payments, /api/refunds, /api/payment-channels, /api/reconciliation → BC8
- /api/notifications, /api/notification-templates, /api/notification-config → BC9
- /api/shops, /api/seller → BC10
- /api/system-configs, /api/feature-flags, /api/announcements, /api/data-dictionaries, /api/operators, /api/audit-logs, /api/rate-limit-rules, /api/index-rebuild, /api/dead-letter, /api/scheduled-tasks, /api/health, /api/dashboard, /api/statistics → BC11

【处理规则】
1. 若某页面缺「数据与 API」段，引用 API 端点列填「⚠️ 缺 API 段」，但仍计入清单
2. 不读取源码 Controller，不做差异判断
3. 不修改 design-prompts 原文件
4. 严格按表格格式输出，不增减列

【页面数预期】
- buyer-app: 48 页（不含 00-overview.md）
- operations: 34 页
- seller: 23 页
- system-admin: 28 页

【自检】
完成后用 Grep 抽样验证 2 个页面的 API 引用是否与 design-prompts 原文一致。
```

- [ ] **Step 2.2: 校验 4 份清单覆盖 133 个页面**

主代理用 Grep 统计每份清单的行数（去掉表头与分隔行），验证：
- buyer-app.md: 48 行数据
- operations.md: 34 行数据
- seller.md: 23 行数据
- system-admin.md: 28 行数据
- 合计 133 行

如有缺失，重新派对应端 subagent 补全。

- [ ] **Step 2.3: 提交功能清单**

```bash
git add docs/feature-inventory/feature-list/
git commit -m "docs: 完成4端功能清单抽取（133页面）" -m "基于 docs/design-prompts 的 143 个 Markdown 抽取 4 端 133 个页面的功能清单，含路由、实现状态、引用 API 端点、涉及 BC 编号。"
```

---

## Task 3: 阶段 2 批 1 — BC1-BC5 API 缺失对比（5 个 subagent 并行）

**Files:**
- Modify: `docs/feature-inventory/api-gap/bc1-user-auth.md`
- Modify: `docs/feature-inventory/api-gap/bc2-product.md`
- Modify: `docs/feature-inventory/api-gap/bc3-cart.md`
- Modify: `docs/feature-inventory/api-gap/bc4-order.md`
- Modify: `docs/feature-inventory/api-gap/bc5-promotion.md`

- [ ] **Step 3.1: 派发 BC1-BC5 五个 subagent（单消息并行）**

主代理在单条消息中并行调用 5 个 `Task` 工具（subagent_type=`general_purpose_task`），每个 subagent 收到统一任务模板：

**通用任务模板**（替换 {N} 与 {BC 信息}）：

```
你是 BC{N} {BC 中文名} API 缺失对比 subagent。任务：扫描源码 Controller 与 design-prompts 相关页面，按模板产出 BC{N} 报告并写入 docs/feature-inventory/api-gap/bc{N}-{name}.md。

【BC 信息】
- BC 编号：BC{N}
- 中文名：{BC 中文名}
- 英文名：{BC 英文名}
- 源码目录：{源码目录列表}
- 涉及端：{端列表}
- 拆分过渡：{是/否}（若是，需写第 5 节）

【输入】
1. 模板：docs/feature-inventory/_shared/report-template.md（必须严格遵循 8 节结构）
2. 功能清单：docs/feature-inventory/feature-list/{涉及端}.md（查询本 BC 相关页面）
3. design-prompts：docs/design-prompts/{涉及端}/{相关模块}/*.md（详细 API 表格，提取「3. 数据模型与 API 对接」段）
4. 源码 Controllers：
   - 用 Glob 找 {源码目录}/**/Controllers/*.cs
   - 用 Grep 抓 [Route("...")] 与 [HttpGet/Post/Put/Delete/Patch("...")] 特性
   - 用 Read 读取 Controller 行号上下文确认用途
5. BC 需求文档：docs/spec/{编号}-{名称}.md（澄清边界，仅参考不深读）

【输出】
覆盖 docs/feature-inventory/api-gap/bc{N}-{name}.md 现有占位，写入完整 8 节报告。

【输出要求】
1. 严格遵循 _shared/report-template.md 的 8 节结构与表格列名
2. 第 2 节源码扫描：所有 Controller 行号必须用 file:/// 链接，格式 [Controller.cs](file:///e:/Leno/src/Services/.../Controller.cs#L{行号})
3. 第 3 节期望清单：所有页面引用必须用 file:/// 链接，格式 [page.md](file:///e:/Leno/docs/design-prompts/{端}/{模块}/{page}.md)
4. 第 4 节差异分析：4 类差异分别列出，每条差异必须有来源（页面或 Controller）
5. 第 5 节：仅 BC1/BC6/BC7 写拆分过渡，其他 BC 写「本 BC 无拆分过渡」
6. 第 6 节优先级矩阵：P0=阻塞交易闭环，P1=影响体验，P2=补充增强
7. 第 7 节跨 BC 依赖：从 docs/spec/{编号}-{名称}.md 的「上下文映射」段提取
8. 第 8 节行动建议：按 P0/P1/P2 + 文档同步分类

【差异判定规则】
1. 缺失：design-prompts 标 🚧/➕ 且源码 Controller 中无对应路径实现
2. 闲置：源码有实现但 design-prompts 任何页面均未引用该端点
3. 路径不一致：方法或路径不匹配（大小写/尾斜杠差异视为一致，注释「路径规范化后匹配」）
4. 能力不匹配：分页/筛选/排序/批量/字段过滤等能力差异（需读取 Controller 方法签名确认）

【动态段处理】
源码路径含 {tenant}/{id} 等占位段时，保留原写法，差异分析按模式匹配。

【Internal API】
Internal*Controller.cs 中的端点必须在第 2 节标注「（内部）」，不计入对外差异。

【禁止行为】
- 不跨 BC 分析（仅本 BC 范围）
- 不修改源码或 design-prompts
- 不创建额外文件
- 不臆测端点（必须以源码或文档为准）

【自检】
完成后用 Read 复核 2 个 Controller 行号确实包含所声称的方法/路径。
```

**BC1-BC5 具体参数**：

| BC | 中文名 | 英文名 | 源码目录 | 涉及端 | 拆分过渡 | spec 文档 |
|-|-|-|-|-|-|-|
| BC1 | 用户与认证授权域 | UserAuth | src/Services/UserAuth/ + src/Services/Identity/ | buyer-app + operations + system-admin | 是（UserAuth↔Identity） | docs/spec/01-用户与认证授权域.md |
| BC2 | 商品域 | Product | src/Services/Product/ + src/Services/Inventory/ | buyer-app + operations + seller | 否 | docs/spec/02-商品域.md |
| BC3 | 购物车域 | Cart | src/Services/Cart/ | buyer-app | 否 | docs/spec/03-购物车域.md |
| BC4 | 订单与交易域 | Order | src/Services/Order/ | buyer-app + operations + seller | 否 | docs/spec/04-订单与交易域.md |
| BC5 | 促销域 | Promotion | src/Services/Promotion/ | buyer-app + operations | 否 | docs/spec/05-促销域.md |

- [ ] **Step 3.2: 校验 BC1-BC5 报告格式**

主代理用 Grep 验证每份报告：
- 包含 8 节标题（## 1. 概览 到 ## 8. 行动建议）
- 表格列名与模板一致
- file:/// 链接存在

如有漂移，重派对应 BC subagent 修正（最多 1 次）。

---

## Task 4: 阶段 2 批 2 — BC6-BC10 API 缺失对比（5 个 subagent 并行）

**Files:**
- Modify: `docs/feature-inventory/api-gap/bc6-review-aftersales.md`
- Modify: `docs/feature-inventory/api-gap/bc7-points-membership.md`
- Modify: `docs/feature-inventory/api-gap/bc8-payment.md`
- Modify: `docs/feature-inventory/api-gap/bc9-notification.md`
- Modify: `docs/feature-inventory/api-gap/bc10-seller-shop.md`

- [ ] **Step 4.1: 派发 BC6-BC10 五个 subagent（单消息并行）**

任务模板同 Step 3.1，参数替换为 BC6-BC10：

| BC | 中文名 | 英文名 | 源码目录 | 涉及端 | 拆分过渡 | spec 文档 |
|-|-|-|-|-|-|-|
| BC6 | 评价与售后域 | ReviewAfterSales | src/Services/ReviewAfterSales/ + src/Services/AfterSales/ | buyer-app + operations + seller | 是（ReviewAfterSales↔AfterSales） | docs/spec/06-评价与售后域.md |
| BC7 | 积分与会员域 | PointsMembership | src/Services/PointsMembership/ + src/Services/Points/ + src/Services/Membership/ | buyer-app + operations | 是（PointsMembership↔Points+Membership） | docs/spec/07-积分与会员域.md |
| BC8 | 支付集成域 | Payment | src/Services/Payment/ | buyer-app + operations | 否 | docs/spec/08-支付集成域.md |
| BC9 | 消息通知域 | Notification | src/Services/Notification/ | 4 端 | 否 | docs/spec/09-消息通知集成.md |
| BC10 | 卖家与店铺管理域 | SellerShop | src/Services/SellerShop/ | buyer-app + operations + seller | 否 | docs/spec/11-卖家与店铺管理域.md |

- [ ] **Step 4.2: 校验 BC6-BC10 报告格式**

同 Step 3.2。

---

## Task 5: 阶段 2 批 3 — BC11 API 缺失对比（1 个 subagent）

**Files:**
- Modify: `docs/feature-inventory/api-gap/bc11-system-admin.md`

- [ ] **Step 5.1: 派发 BC11 subagent**

任务模板同 Step 3.1，参数：

| BC | 中文名 | 英文名 | 源码目录 | 涉及端 | 拆分过渡 | spec 文档 |
|-|-|-|-|-|-|-|
| BC11 | 系统管理域 | SystemAdmin | src/Services/SystemAdmin/ | system-admin | 否 | docs/spec/12-系统管理域.md |

- [ ] **Step 5.2: 校验 BC11 报告格式**

同 Step 3.2。

- [ ] **Step 5.3: 提交 11 份 BC 报告**

```bash
git add docs/feature-inventory/api-gap/bc*.md
git commit -m "docs: 完成11个BC API缺失对比报告" -m "基于源码 71 个 Controller 与 design-prompts 133 个页面对比，产出 BC1-BC11 共 11 份 API 缺失对比报告，覆盖缺失/闲置/路径不一致/能力不匹配 4 类差异。"
```

---

## Task 6: 阶段 3 — 主代理聚合总览与索引

**Files:**
- Modify: `docs/feature-inventory/api-gap/00-summary.md`
- Modify: `docs/feature-inventory/feature-list/README.md`

- [ ] **Step 6.1: 写 api-gap/00-summary.md 总览**

完整内容（主代理用 Grep 聚合 11 份 BC 报告的差异数字后填充）：

```markdown
# API 缺失对比报告总览

**文档版本**：V1.0
**创建日期**：2026-07-26
**关联设计**：`docs/superpowers/specs/2026-07-26-feature-inventory-and-api-gap-design.md`

---

## 1. 11 BC 差异统计汇总

| BC | 中文名 | 缺失 | 闲置 | 路径不一致 | 能力不匹配 | 合计 |
|-|-|-|-|-|-|-|
| BC1 | 用户与认证授权 | {X} | {Y} | {Z} | {W} | {合计} |
| BC2 | 商品 | {X} | {Y} | {Z} | {W} | {合计} |
| BC3 | 购物车 | {X} | {Y} | {Z} | {W} | {合计} |
| BC4 | 订单与交易 | {X} | {Y} | {Z} | {W} | {合计} |
| BC5 | 促销 | {X} | {Y} | {Z} | {W} | {合计} |
| BC6 | 评价与售后 | {X} | {Y} | {Z} | {W} | {合计} |
| BC7 | 积分与会员 | {X} | {Y} | {Z} | {W} | {合计} |
| BC8 | 支付集成 | {X} | {Y} | {Z} | {W} | {合计} |
| BC9 | 消息通知 | {X} | {Y} | {Z} | {W} | {合计} |
| BC10 | 卖家与店铺 | {X} | {Y} | {Z} | {W} | {合计} |
| BC11 | 系统管理 | {X} | {Y} | {Z} | {W} | {合计} |
| **合计** | | {∑X} | {∑Y} | {∑Z} | {∑W} | {∑合计} |

> 数字来源：用 Grep 抓取各 BC 报告「## 1. 概览」段的「差异统计」行汇总

---

## 2. 全局优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 | 合计 |
|-|-|-|-|-|-|
| P0 | {N} | {N} | {N} | {N} | {N} |
| P1 | {N} | {N} | {N} | {N} | {N} |
| P2 | {N} | {N} | {N} | {N} | {N} |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强

---

## 3. Top 10 高优先级修复项

| 排名 | BC | 类别 | 端点 | 来源 | 建议操作 |
|-|-|-|-|-|-|
| 1 | BC{N} | 缺失 | METHOD /api/path | [page.md](file:///e:/Leno/docs/design-prompts/.../page.md) | ... |
| 2 | ... | ... | ... | ... | ... |
| ... | ... | ... | ... | ... | ... |
| 10 | ... | ... | ... | ... | ... |

> 从所有 BC 报告的 P0 项中跨 BC 聚合，按影响面排序

---

## 4. 拆分过渡态影响范围

| 主 BC | 旧 BC | 新 BC | 影响端点数 | 待切换端点数 |
|-|-|-|-|-|
| BC1 | UserAuth | Identity | {N} | {N} |
| BC6 | ReviewAfterSales | AfterSales | {N} | {N} |
| BC7 | PointsMembership | Points+Membership | {N} | {N} |

---

## 5. 推荐实施顺序

按 BC 优先级与差异严重度排序：

1. **P0 修复批次**：先修复所有 BC 报告中 P0 级别的缺失与路径不一致
2. **P1 补充批次**：补充 P1 级别的缺失与能力不匹配
3. **P2 规划批次**：处理 P2 级别的闲置端点（保留观察 / 设计稿补调用 / 后端废弃）
4. **文档同步批次**：将 design-prompts 中过乐观的 ✅ 标注修正为 🚧 或补充端点

具体 BC 顺序建议：
- 第一梯队（P0 集中区）：BC1 认证、BC4 订单、BC8 支付
- 第二梯队：BC3 购物车、BC5 促销、BC6 评价售后
- 第三梯队：BC2 商品、BC7 积分会员、BC10 卖家店铺
- 第四梯队：BC9 通知、BC11 系统管理
```

- [ ] **Step 6.2: 写 feature-list/README.md 索引**

完整内容：

```markdown
# 功能清单索引

**文档版本**：V1.0
**创建日期**：2026-07-26

---

## 1. 4 端清单统计

| 端 | 文件 | 页面数 | 实现状态分布 |
|-|-|-|-|
| 买家端 APP | [buyer-app.md](./buyer-app.md) | 48 | ✅×{N} / 🚧×{N} / ➕×{N} |
| 运营管理后台 | [operations.md](./operations.md) | 34 | ✅×{N} / 🚧×{N} / ➕×{N} |
| 商家管理后台 | [seller.md](./seller.md) | 23 | ✅×{N} / 🚧×{N} / ➕×{N} |
| 系统管理后台 | [system-admin.md](./system-admin.md) | 28 | ✅×{N} / 🚧×{N} / ➕×{N} |
| **合计** | | **133** | ✅×{N} / 🚧×{N} / ➕×{N} |

> 数字来源：用 Grep 统计各端清单的实现状态列

---

## 2. BC 分布统计

按 feature-list 中「涉及 BC」列统计：

| BC | 中文名 | 涉及页面数 |
|-|-|-|
| BC1 | 用户与认证授权 | {N} |
| BC2 | 商品 | {N} |
| ... | ... | ... |
| BC11 | 系统管理 | {N} |

> 一个页面可能涉及多个 BC（如订单页涉及 BC4+BC8），故总和 ≥ 133

---

## 3. 使用方式

1. 按端查找页面：打开对应 {端}.md，按模块/路由定位
2. 按 BC 查找 API 缺失：跳转 [../api-gap/bc{N}-{name}.md](../api-gap/bc{N}-{name}.md)
3. 查看全局总览：[../api-gap/00-summary.md](../api-gap/00-summary.md)
```

- [ ] **Step 6.3: 数字交叉校验**

主代理用 Grep 抓取 11 份 BC 报告的差异数字，与 00-summary.md 的汇总表交叉校验：
- 总览缺失合计 = ∑ BC 报告缺失数
- 总览闲置合计 = ∑ BC 报告闲置数
- 总览路径不一致合计 = ∑ BC 报告路径不一致数
- 总览能力不匹配合计 = ∑ BC 报告能力不匹配数

如不一致，定位差异并修正。

- [ ] **Step 6.4: 提交总览与索引**

```bash
git add docs/feature-inventory/api-gap/00-summary.md docs/feature-inventory/feature-list/README.md
git commit -m "docs: 完成功能清单与API缺失对比报告总览" -m "聚合 11 个 BC 报告产出总览，含 4 类差异统计、全局优先级矩阵、Top 10 修复项、拆分过渡影响范围、推荐实施顺序；同时产出 4 端功能清单索引与统计。"
```

---

## Task 7: 推送远程与最终验收

- [ ] **Step 7.1: 推送远程**

```bash
git push origin dev
```

若失败（网络问题），记录错误并提示用户手动重试。

- [ ] **Step 7.2: 最终验收清单**

主代理执行以下校验：

- [ ] docs/feature-inventory/ 下文件数 = 19 个（1 README + 1 _shared + 5 feature-list + 12 api-gap）
- [ ] 4 份端清单覆盖 133 个页面（buyer 48 + operations 34 + seller 23 + system-admin 28）
- [ ] 11 份 BC 报告均含 8 节（## 1. 概览 到 ## 8. 行动建议）
- [ ] 表格列名与 _shared/report-template.md 一致
- [ ] 所有 Controller 行号引用为 file:/// 可点击链接
- [ ] 总览统计数字 = 11 份 BC 报告数字之和
- [ ] 无 TODO/TBD/待补充/未实现 等占位符
- [ ] 4 次 git 提交完成（commit 1 骨架 / commit 2 清单 / commit 3 BC 报告 / commit 4 总览）

- [ ] **Step 7.3: 报告完成状态**

向用户报告：
- 19 个文件已产出
- 4 类差异数字汇总
- Top 10 高优先级修复项
- 推送状态（成功/失败）

---

## Self-Review

### 1. Spec 覆盖检查

| Spec 节段 | 对应 Task |
|-|-|
| §3 产出文件结构（19 文件） | Task 1 创建骨架 |
| §4 BC 报告统一模板（8 节） | Task 1 写 _shared/report-template.md |
| §5.1 任务分层（4 阶段） | Task 1-7 覆盖全 4 阶段 |
| §5.2 阶段 1 subagent 边界（4 端并行） | Task 2 |
| §5.3 阶段 2 subagent 边界（11 BC 并行） | Task 3 (BC1-5) + Task 4 (BC6-10) + Task 5 (BC11) |
| §5.4 BC → 源码目录映射表 | Task 1 顶层 README + 各 BC Task 参数 |
| §5.5 一致性校验清单 | Task 3.2 / 4.2 / 5.2 / 7.2 |
| §6.1 数据流 | Task 2 → Task 3-5 → Task 6 |
| §6.2 错误处理 | 各 subagent 任务模板的「处理规则」段 |
| §6.3 测试策略（自检 + 校验） | 各 subagent 「自检」段 + Task 7.2 验收清单 |
| §6.4 验收标准 | Task 7.2 |
| §7.1 实施顺序 | Task 1 → 2 → 3 → 4 → 5 → 6 → 7 |
| §7.2 风险与缓解 | 各 Task 的校验步骤 + Task 7.1 推送失败处理 |
| §7.3 提交规范（4 次提交） | Task 1.7 / 2.3 / 5.3 / 6.4 |

**覆盖完整**，无遗漏。

### 2. 占位扫描

- Task 1.3 顶层 README 中「4. 4 类差异定义」段为静态内容，无占位 ✓
- Task 6.1 总览模板中 {X}/{Y}/{Z}/{W} 是 subagent 产出后主代理填充的实际数字，非占位符 ✓
- Task 6.2 索引模板中 {N} 同上 ✓
- 所有 subagent 任务模板为完整指令，无 TBD/TODO ✓

### 3. 类型一致性

- BC 编号在所有 Task 中统一为 BC1-BC11 ✓
- 源码目录映射在 Task 1.3 与 Task 3.1/4.1/5.1 参数表中一致 ✓
- 拆分过渡 BC 在 Task 1.3 顶层 README、Task 3.1（BC1）、Task 4.1（BC6/BC7）中均为 BC1/BC6/BC7 ✓
- 文件路径在所有 Task 中统一为 docs/feature-inventory/... ✓

无类型不一致。

---

## 执行交接

**Plan complete and saved to `docs/superpowers/plans/2026-07-26-feature-inventory-and-api-gap.md`.**

用户已明确选择 Subagent-Driven 方式执行，按本计划 Task 1 → 7 顺序执行。
