# 功能清单与 API 缺失对比报告 — 设计文档

**文档版本**：V1.0
**创建日期**：2026-07-26
**作者**：Trae Brainstorming
**关联文档**：
- 设计稿：`docs/designs/`（153 个 HTML 视觉稿）
- 设计提示词：`docs/design-prompts/`（143 个 Markdown，含 API 端点映射与实现状态标注）
- BC 需求文档：`docs/spec/00-需求文档总览与DDD架构.md` 等 12 篇
- 源码 Controllers：`src/Services/{BC}/**/Controllers/*.cs`（71 个 Controller 文件）

---

## 1. 背景与目标

### 1.1 背景

Leno 电商平台已实现 11 个限界上下文（BC）、71 个 Controller 文件、346+ 个 API 端点。前端界面设计稿分为两层：

- `docs/designs/`：153 个 HTML 视觉设计稿（自包含单文件，无 API 信息）
- `docs/design-prompts/`：143 个 Markdown 设计提示词，已含结构化 API 端点引用与实现状态标注（109 ✅ 已实现 / 9 🚧 规划中 / 15 ➕ 补充功能）

但 design-prompts 中的 API 标注为「期望清单」，未与源码 Controller 实际实现做系统化比对，存在以下未知风险：

- 标 ✅ 的端点是否在源码中真实存在
- 标 🚧/➕ 的端点是否已部分实现
- 路径/方法是否与源码一致
- 后端是否有「闲置端点」（已实现但设计稿未调用）
- 拆分过渡态 BC（UserAuth↔Identity、PointsMembership↔Points+Membership）端点归属是否清晰

### 1.2 目标

产出一份系统化的「功能清单 + API 缺失对比报告」，回答以下问题：

1. 4 端 133 个页面分别需要哪些 API 端点
2. 11 个 BC 各自的源码端点清单
3. 设计稿期望与源码实际的 4 类差异：
   - 设计稿需要但后端未提供（缺失）
   - 后端已有但设计稿未调用（闲置）
   - 路径或方法不一致
   - 参数/能力范围不匹配
4. 优先级矩阵与实施顺序建议

### 1.3 非目标（YAGNI 裁剪）

- 不修改源码或 design-prompts（仅产出报告）
- 不实现缺失的 API（仅列出建议）
- 不深度解析 HTML 设计稿（HTML 仅作页面清单参考）
- 不评估 API 性能/安全/可扩展性
- 不做 gRPC proto 与 HTTP API 对照（独立任务）
- 不做前端实现代码生成

---

## 2. 数据源选择

经与用户确认，采用「复用 design-prompts」方案：

- **主数据源**：`docs/design-prompts/` 143 个 Markdown（结构化，含 API 段）
- **辅助参考**：`docs/designs/` 153 个 HTML（仅作页面清单参考，不深度解析）
- **对比基准**：`src/Services/{BC}/**/Controllers/*.cs` 源码扫描结果
- **边界澄清**：`docs/spec/{N}-{name}.md` 12 篇 BC 需求文档

**理由**：design-prompts 已含 API 端点映射与实现状态标注，复用可避免重复劳动；HTML 设计稿无 API 信息，深度解析价值低。

---

## 3. 产出文件结构

```
docs/feature-inventory/
├── README.md                                # 入口：方法论、BC 列表、报告索引、统计摘要
├── _shared/
│   └── report-template.md                   # BC 报告统一模板（供 subagent 严格遵循）
├── feature-list/
│   ├── buyer-app.md                         # 买家端 48 页面功能清单
│   ├── operations.md                        # 运营后台 34 页面功能清单
│   ├── seller.md                            # 商家后台 23 页面功能清单
│   ├── system-admin.md                      # 系统后台 28 页面功能清单
│   └── README.md                            # 功能清单索引与统计
└── api-gap/
    ├── 00-summary.md                        # 总览：四类差异统计、优先级矩阵、跨 BC 依赖
    ├── bc1-user-auth.md                     # 用户与认证授权域
    ├── bc2-product.md                       # 商品域
    ├── bc3-cart.md                          # 购物车域
    ├── bc4-order.md                         # 订单与交易域
    ├── bc5-promotion.md                     # 促销域
    ├── bc6-review-aftersales.md             # 评价与售后域
    ├── bc7-points-membership.md             # 积分与会员域
    ├── bc8-payment.md                       # 支付集成域
    ├── bc9-notification.md                  # 消息通知域
    ├── bc10-seller-shop.md                  # 卖家与店铺管理域
    └── bc11-system-admin.md                 # 系统管理域
```

**说明**：
- 功能清单按 4 端拆分（与 design-prompts 目录结构对齐，便于追溯）
- API 缺失对比报告按 11 个 BC 拆分（与 `docs/spec/` 编号对齐，便于后续按 BC 拆任务）
- 拆分过渡态 BC（Identity/Points/PointsMembership/Membership/Inventory）按 spec 主线归并到对应 BC 报告中，过渡态在报告内单列「拆分过渡说明」一节
- `_shared/report-template.md` 是 subagent 必须严格遵循的模板，确保 11 份 BC 报告格式一致
- 文件总数：1 顶层 README + 1 _shared 模板 + 5 feature-list + 12 api-gap = **19 个文件**

---

## 4. BC 报告统一模板

每份 `api-gap/bc{N}-{name}.md` 严格遵循以下 8 节结构：

### 第 1 节：概览
- BC 编号 / 中文名 / 英文名
- 涉及端：buyer-app / operations / seller / system-admin（勾选）
- 涉及页面数（来自 feature-list）
- 已实现 API 端点数（来自源码 Controller 扫描）
- 差异统计：缺失 X / 闲置 Y / 路径不一致 Z / 能力不匹配 W

### 第 2 节：源码 API 端点清单（实际实现）
表格列：HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色

来源：grep `src/Services/{BC}/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
作用：作为对比基准（ground truth）

### 第 3 节：设计稿需求 API 清单（期望实现）
表格列：HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态(✅/🚧/➕) | 鉴权角色

来源：`design-prompts/{端}/{模块}/{页面}.md` 的「数据与 API」段
作用：作为期望清单

### 第 4 节：差异分析

#### 4.1 设计稿需要但后端未提供（缺失）
表格列：期望方法 | 期望路径 | 来源页面 | 用途 | 优先级(P0/P1/P2) | 建议补充方式

说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现

#### 4.2 后端已有但设计稿未调用（闲置）
表格列：实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式

说明：源码有实现但 design-prompts 中无任何页面引用
建议处理方式：保留观察 / 设计稿补调用 / 后端废弃

#### 4.3 路径或方法不一致
表格列：期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向

说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配
建议调整方向：以实际实现为准改文档 / 以文档为准改代码

#### 4.4 参数/能力范围不匹配
表格列：期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充

说明：分页/筛选/排序/批量/字段过滤等能力差异
例如：设计稿需「分页+筛选+排序」，API 仅支持「分页」

### 第 5 节：拆分过渡说明（仅 BC1/BC6/BC7 等涉及拆分的 BC）
- 旧 BC 与新 BC 对照（如 UserAuth ↔ Identity / PointsMembership ↔ Points+Membership）
- 双轨期端点引用规范（哪些端点优先引用旧 BC、哪些已切换新 BC）
- 待切换端点清单

其他 BC 写「本 BC 无拆分过渡」一句话。

### 第 6 节：优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | ... | ... | ... | ... |
| P1 | ... | ... | ... | ... |
| P2 | ... | ... | ... | ... |

P0=阻塞交易闭环；P1=影响体验；P2=补充增强

### 第 7 节：跨 BC 依赖
- 上游依赖：本 BC 依赖哪些 BC 的端点/事件
- 下游依赖：哪些 BC 依赖本 BC 的端点/事件
- 集成事件订阅/发布清单

### 第 8 节：行动建议
- 立即修复（P0 缺失/不一致）
- 短期补充（P1 缺失/不匹配）
- 长期规划（P2 闲置/废弃）
- 文档同步（design-prompts API 引用对齐到源码）

### 模板纪律
- subagent 必须严格按 8 节顺序产出，不得增删节
- 第 5 节仅在涉及拆分过渡的 BC 出现，其他 BC 写「本 BC 无拆分过渡」一句话
- 所有表格列名固定，便于后续脚本化聚合
- 行号引用必须可点击（`file:///` 链接格式）

### 总览报告 `api-gap/00-summary.md` 字段
- 11 个 BC 差异统计汇总表（按 4 类差异计数）
- 全局优先级矩阵（P0/P1/P2 各类差异总数）
- Top 10 高优先级修复项（跨 BC 聚合）
- 拆分过渡态影响范围说明
- 推荐实施顺序（按 BC 优先级）

---

## 5. Subagent 任务边界与执行流程

### 5.1 任务分层

```
阶段 0：主代理预备（同步）
  - 创建 docs/feature-inventory/ 目录骨架
  - 写 _shared/report-template.md（BC 报告模板）
  - 建立 BC → 端 → 源码目录 映射表
                          │
阶段 1：功能清单抽取（4 个 subagent 并行，端级）
  - buyer-app subagent → feature-list/buyer-app.md
  - operations subagent → feature-list/operations.md
  - seller subagent → feature-list/seller.md
  - system-admin subagent → feature-list/system-admin.md
                          │
阶段 2：API 缺失对比（11 个 subagent 并行，BC 级）
  - bc1 ~ bc11 subagent → api-gap/bc{N}-{name}.md
                          │
阶段 3：主代理聚合（同步）
  - 一致性校验（11 份 BC 报告格式/术语/链接）
  - 产出 api-gap/00-summary.md（总览）
  - 产出 feature-list/README.md
  - 产出 docs/feature-inventory/README.md
  - 自检 + 提交 git
```

### 5.2 阶段 1 subagent 任务边界（4 个并行）

**输入**：
- `docs/design-prompts/{端}/00-overview.md`
- `docs/design-prompts/{端}/**/*.md`（所有页面提示词）
- `docs/designs/{端}/**/*.html`（仅作页面清单参考，不深度解析）

**输出**：`docs/feature-inventory/feature-list/{端}.md`

**输出格式**（每个页面一行）：

| 序号 | 模块 | 页面 | 路由 | 实现状态 | 引用 API 端点 | 涉及 BC |
|-|-|-|-|-|-|-|

例如：
| 5 | 01-auth | login | `/login` | ✅ | POST /api/account/login, POST /api/auth/refresh, GET /api/auth/oauth/{provider}/login | BC1 |

**职责边界**：
- 只抽取页面元数据与 API 引用清单，不做对比
- 引用 API 端点逐条列出（method + path），不解析参数
- 实现状态沿用 design-prompts 的 ✅/🚧/➕ 标注
- 涉及 BC 列填 BC 编号（如 BC1+BC9），用于阶段 2 BC 归集

**禁止行为**：
- 不读取源码 Controller
- 不做差异判断
- 不修改 design-prompts 原文件

### 5.3 阶段 2 subagent 任务边界（11 个并行）

**输入**：
- `_shared/report-template.md`（必须严格遵循的模板）
- `docs/feature-inventory/feature-list/{端}.md`（4 份，用于查询本 BC 相关页面）
- `docs/design-prompts/{相关端}/{相关模块}/*.md`（详细 API 表格）
- `src/Services/{BC 相关目录}/**/Controllers/*.cs`（源码 ground truth）
- `docs/spec/{BC 编号}-{名称}.md`（BC 需求文档，用于澄清边界）

**输出**：`docs/feature-inventory/api-gap/bc{N}-{name}.md`

**职责边界**：
- 严格按 8 节模板产出
- 第 2 节源码扫描必须用 Grep 抓 `[Route]/[Http*]` 特性，引用 `file:///` 链接到具体行号
- 第 3 节设计稿期望从 design-prompts 的「数据与 API」段提取
- 第 4 节差异分析按 4 类分别列出，每条差异必须有来源（页面或 Controller）
- 第 6 节优先级矩阵按 P0/P1/P2 分类
- 第 5 节仅在 BC1/BC6/BC7 等涉及拆分的 BC 出现

**禁止行为**：
- 不跨 BC 分析（仅本 BC 范围内）
- 不修改源码或 design-prompts
- 不创建额外文件
- 不臆测端点（必须以源码或文档为准）

### 5.4 BC → 源码目录映射表

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

### 5.5 一致性校验清单（阶段 3 主代理执行）

- [ ] 11 份 BC 报告均含 8 节，节标题与模板完全一致
- [ ] 表格列名与模板完全一致
- [ ] 行号引用均为可点击 `file:///` 链接
- [ ] 术语统一（店铺/卖家/秒杀等遵循 shared/glossary.md）
- [ ] 4 类差异分类无重叠（同一端点不重复出现在多个分类）
- [ ] 优先级矩阵 P0 项必有行动建议
- [ ] 拆分过渡说明仅在 BC1/BC6/BC7 出现
- [ ] 总览统计数字与各 BC 报告数字一致

---

## 6. 数据流、错误处理、测试与验收

### 6.1 数据流

```
输入侧
  docs/design-prompts/{端}/{模块}/{page}.md  ── 含「数据与 API」段：方法+路径+用途+状态
  src/Services/{BC}/**/Controllers/*.cs   ──── 含 [Route]/[Http*] 特性
  docs/spec/{N}-{name}.md  ─────────────────── 含 BC 边界、聚合、领域事件
                          │
                          ▼
                  subagent 处理（端级 4 个 / BC 级 11 个）
                          │
                          ▼
输出侧
  docs/feature-inventory/
  ├── feature-list/{端}.md         ← 4 个端级 subagent 产出
  └── api-gap/bc{N}-{name}.md      ← 11 个 BC 级 subagent 产出
                          │
                          ▼
  主代理聚合：
  ├── api-gap/00-summary.md        ← 跨 BC 聚合 + 优先级矩阵
  ├── feature-list/README.md       ← 清单索引与统计
  └── README.md                    ← 入口与方法论
```

**关键数据约束**：
- 每条差异必须双向可追溯：差异条目 → 来源页面（design-prompts 路径）+ 来源 Controller（file:/// 行号）
- 实现状态标注沿用 design-prompts 的 ✅/🚧/➕，但以源码扫描结果为准校验：标 ✅ 但源码无实现的，记入「路径不一致」并标记为「文档过乐观」
- BC 归集以源码 Controller 物理目录为准（如 `Identity` 目录下的 Controller 归入 BC1 报告，并在第 5 节说明拆分过渡）

### 6.2 错误处理

| 场景 | 处理方式 |
|-|-|
| design-prompts 某页面缺「数据与 API」段 | subagent 在该页面行标注「⚠️ 缺 API 段」，跳过该页面的 API 抽取，但仍计入页面清单 |
| design-prompts 标 ✅ 但源码 Controller 不存在 | 记入 §4.3「路径不一致」，建议方向「文档需修正为 🚧 或补充端点」 |
| 源码 Controller 路径含动态段（如 `[Route("api/{tenant}/orders")]`） | subagent 保留原路径写法，在差异分析时按模式匹配（`{tenant}` 视作占位） |
| 同一端点被多个页面引用 | 在期望清单中保留多行（每页一行），在差异分析中合并为一行并标注「被 N 个页面引用」 |
| 拆分过渡态 BC（如 PointsMembership 同时存在新旧） | 第 5 节单列说明，旧 BC 端点优先，新 BC 端点标注「🚧 待切换」 |
| subagent 输出格式漂移 | 主代理在阶段 3 一致性校验时检测到漂移，对漂移 BC 重新派 subagent 修正（最多重试 1 次） |
| 源码扫描漏掉 Internal API | subagent 必须扫描 `Internal*Controller.cs`，Internal API 在第 2 节单独标注「（内部）」 |
| design-prompts 引用的端点路径与实际仅大小写或尾斜杠差异 | 视为一致，不记入差异，但在期望清单注释「路径规范化后匹配」 |

### 6.3 测试策略

由于本任务是文档产出而非代码实现，测试方式调整为「自检 + 校验脚本」：

**自检（subagent 内部）**：
- 每个 subagent 在产出前自检：
  - 所有引用的 Controller 行号是否真实存在（用 Read 复核 1-2 个采样点）
  - 所有引用的 design-prompts 页面路径是否真实存在
  - 表格列名与模板一致
  - 实现状态标注与差异分类不矛盾（标 ✅ 的不应出现在「缺失」分类）

**主代理校验脚本（阶段 3）**：
- 用 Grep 抓取所有 BC 报告中的 `file:///` 链接，抽样验证 5% 链接指向的行号确实包含所声称的方法/路径
- 用 Grep 抓取所有报告的章节标题，验证 8 节齐全
- 用 Grep 抓取所有表格表头，验证列名统一
- 用 Grep 统计每类差异数量，与总览报告数字交叉校验

**用户验收（最终）**：
- 用户抽样检查 1-2 个 BC 报告的准确性
- 用户确认优先级矩阵与实施顺序符合预期

### 6.4 验收标准

| 验收点 | 标准 |
|-|-|
| 文件完整性 | docs/feature-inventory/ 下文件数 = 1 README + 1 _shared + 5 feature-list + 12 api-gap = 19 个文件 |
| 功能清单覆盖 | 4 份端清单覆盖 design-prompts 中全部 133 个页面（不含 4 个 00-overview.md 与 shared） |
| BC 报告覆盖 | 11 份 BC 报告覆盖全部 11 个 BC（编号 BC1-BC11） |
| 模板遵循 | 每份 BC 报告含 8 节，节标题与模板一致 |
| 差异可追溯 | 每条差异条目可点击追溯到 design-prompts 页面或 Controller 行号 |
| 一致性 | 总览报告统计数字 = 11 份 BC 报告数字之和 |
| 零占位 | 无 TODO/TBD/待补充/未实现 等占位符（遵循 user_rules 第 2 条） |
| 提交规范 | 单次 git 提交，中文提交说明，推送远程（遵循 user_rules 第 6 条） |

### 6.5 边界与范围限制

**包含**：
- 静态对比 design-prompts API 引用 vs 源码 Controller 实现
- 4 类差异分类与优先级标注
- 跨 BC 依赖关系梳理

**不包含**：
- 不修改源码或 design-prompts（仅产出报告）
- 不实现缺失的 API（仅列出建议）
- 不深度解析 HTML 设计稿（HTML 仅作页面清单参考）
- 不评估 API 性能/安全/可扩展性（仅做存在性与一致性对比）
- 不涉及 gRPC proto 与 HTTP API 的对照（如需可作后续独立任务）

---

## 7. 实施顺序、风险与提交规范

### 7.1 实施顺序

```
阶段 0：主代理预备
  ├─ 0.1 创建 docs/feature-inventory/ 目录骨架（19 个文件占位）
  ├─ 0.2 写 _shared/report-template.md（BC 报告模板）
  ├─ 0.3 写 BC → 源码目录映射表（嵌入 README.md）
  └─ 0.4 git 提交「建立功能清单与 API 缺失对比报告骨架」
  ↓
阶段 1：功能清单抽取（4 个端级 subagent 并行）
  ├─ buyer-app subagent
  ├─ operations subagent
  ├─ seller subagent
  └─ system-admin subagent
  ↓
阶段 1.5：主代理校验 + 提交
  ├─ 校验 4 份清单覆盖 133 个页面
  └─ git 提交「完成 4 端功能清单抽取」
  ↓
阶段 2：API 缺失对比（11 个 BC 级 subagent 并行）
  ├─ 批 1：BC1-BC5（5 个并行）
  ├─ 批 2：BC6-BC10（5 个并行）
  └─ 批 3：BC11（1 个）
  ↓
阶段 2.5：主代理校验 + 提交
  ├─ 一致性校验（8 节齐全、列名统一、链接可点）
  ├─ 漂移 BC 重派 subagent 修正（最多 1 次）
  └─ git 提交「完成 11 个 BC API 缺失对比报告」
  ↓
阶段 3：主代理聚合
  ├─ 3.1 产出 api-gap/00-summary.md（总览 + 优先级矩阵 + Top 10 修复项）
  ├─ 3.2 产出 feature-list/README.md（清单索引 + 统计）
  ├─ 3.3 产出 docs/feature-inventory/README.md（入口 + 方法论 + BC 映射表）
  ├─ 3.4 数字交叉校验（总览 = ∑ BC 报告）
  └─ 3.5 git 提交「完成功能清单与 API 缺失对比报告总览」+ 推送远程
```

**并行约束**：受工具上限约束，单次最多 5 个并行调用：
- 阶段 1：4 个 subagent 单消息派发（≤5）
- 阶段 2：分 3 批派发，每批 ≤5 个（批 1：BC1-BC5，批 2：BC6-BC10，批 3：BC11）
- 批次间无依赖，但需等前一批返回后再派下一批

### 7.2 风险与缓解

| 风险 | 概率 | 影响 | 缓解措施 |
|-|-|-|-|
| subagent 输出格式漂移 | 中 | 高 | _shared/report-template.md 强约束 + 阶段 2.5 一致性校验 + 最多 1 次重试 |
| design-prompts API 段缺失或不规范 | 中 | 中 | subagent 标注「⚠️ 缺 API 段」跳过，不阻塞其他页面 |
| 拆分过渡态 BC 归集混乱 | 中 | 中 | 第 5 节单列说明，旧 BC 优先，新 BC 标 🚧 待切换 |
| 源码 Controller 路径含动态段导致误判 | 低 | 中 | 保留原路径写法，按模式匹配（{xxx} 视作占位） |
| 跨 BC 端点重复计数（如通知被多端调用） | 中 | 低 | 期望清单保留多行，差异分析合并并标注「被 N 页面引用」 |
| 并行 subagent 上下文超限 | 低 | 中 | 每个 subagent 仅处理 1 个 BC，输入边界明确 |
| git 推送失败（曾出现 443 超时） | 中 | 低 | 本地提交保留，推送失败时记录并提示用户手动重试 |
| 11 个 BC 同时派发超出工具上限 | 高 | 高 | 分 3 批派发（5+5+1），批间无依赖 |

### 7.3 提交规范

遵循 user_rules 第 6 条，分 4 次提交：

| 提交次序 | 提交说明（中文） | 内容 |
|-|-|-|
| 1 | `chore: 建立功能清单与 API 缺失对比报告骨架` | 目录骨架 + 模板 + BC 映射表 |
| 2 | `docs: 完成 4 端功能清单抽取（133 页面）` | feature-list/ 下 4 份清单 + README |
| 3 | `docs: 完成 11 个 BC API 缺失对比报告` | api-gap/ 下 11 份 BC 报告 |
| 4 | `docs: 完成功能清单与 API 缺失对比报告总览` | 00-summary.md + feature-list/README + 顶层 README + 推送远程 |

**提交纪律**：
- 每次提交前 `git status` 确认变更范围
- 不使用 `git add -A`，按文件路径精确添加
- 提交说明使用 HEREDOC 格式
- 第 4 次提交后 `git push origin`，失败时记录错误并提示用户

### 7.4 YAGNI 裁剪

明确**不做**的事项（避免范围蔓延）：
- 不做 gRPC proto 与 HTTP API 对照（独立任务）
- 不做 API 性能/安全评估
- 不做前端实现代码生成
- 不做 API 缺失的修复实现（仅列出建议）
- 不做 design-prompts 或源码的修改
- 不做 HTML 设计稿的深度解析（仅作页面清单参考）

---

## 8. 后续工作

本设计文档经用户审核通过后，将进入实施阶段：

1. **写实施计划**：调用 `writing-plans` skill，将本设计拆解为可执行的阶段任务清单
2. **按阶段执行**：按阶段 0 → 1 → 1.5 → 2 → 2.5 → 3 顺序实施
3. **每阶段提交**：按 7.3 节提交规范分 4 次 git 提交
4. **最终验收**：用户抽样检查 1-2 个 BC 报告的准确性

---

## 附录 A：BC 编号与文档对照

| BC | 编号 | 中文名 | 英文名 | 源码目录 | 需求文档 |
|-|-|-|-|-|-|
| BC1 | 01 | 用户与认证授权域 | UserAuth | `src/Services/UserAuth/` + `src/Services/Identity/` | `docs/spec/01-用户与认证授权域.md` |
| BC2 | 02 | 商品域 | Product | `src/Services/Product/` + `src/Services/Inventory/` | `docs/spec/02-商品域.md` |
| BC3 | 03 | 购物车域 | Cart | `src/Services/Cart/` | `docs/spec/03-购物车域.md` |
| BC4 | 04 | 订单与交易域 | Order | `src/Services/Order/` | `docs/spec/04-订单与交易域.md` |
| BC5 | 05 | 促销域 | Promotion | `src/Services/Promotion/` | `docs/spec/05-促销域.md` |
| BC6 | 06 | 评价与售后域 | ReviewAfterSales | `src/Services/ReviewAfterSales/` + `src/Services/AfterSales/` | `docs/spec/06-评价与售后域.md` |
| BC7 | 07 | 积分与会员域 | PointsMembership | `src/Services/PointsMembership/` + `src/Services/Points/` + `src/Services/Membership/` | `docs/spec/07-积分与会员域.md` |
| BC8 | 08 | 支付集成域 | Payment | `src/Services/Payment/` | `docs/spec/08-支付集成域.md` |
| BC9 | 09 | 消息通知域 | Notification | `src/Services/Notification/` | `docs/spec/09-消息通知集成.md` |
| BC10 | 11 | 卖家与店铺管理域 | SellerShop | `src/Services/SellerShop/` | `docs/spec/11-卖家与店铺管理域.md` |
| BC11 | 12 | 系统管理域 | SystemAdmin | `src/Services/SystemAdmin/` | `docs/spec/12-系统管理域.md` |

## 附录 B：拆分过渡态说明

| 主 BC | 旧 BC | 新 BC | 过渡策略 |
|-|-|-|-|
| BC1 用户与认证授权 | UserAuth | Identity | 双轨期优先引用 UserAuth，Identity 端点标 🚧 待切换 |
| BC7 积分与会员 | PointsMembership | Points + Membership | 双轨期优先引用 PointsMembership，新拆分端点标 🚧 待切换 |
| BC6 评价与售后 | ReviewAfterSales | AfterSales（独立） | 双轨期优先引用 ReviewAfterSales，AfterSales 端点标 🚧 待切换 |

## 附录 C：术语对照

遵循 `docs/design-prompts/shared/glossary.md`：
- 店铺（不用商铺）、卖家（不用商户）、秒杀（不用闪购）、商品（不用产品）
- 优惠券（不用折扣券）、成长值（不用经验值）、付费会员（不用 VIP）
- 12 个 BC 缩写：UserAuth/Product/Cart/Order/Promotion/ReviewAfterSales/Payment/PointsMembership/Inventory/Notification/SellerShop/SystemAdmin
- 4 类角色：买家(Buyer)/卖家(Seller)/运营管理员(Operator)/系统管理员(Admin)
