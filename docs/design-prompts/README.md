# Leno 电商平台 UI 设计提示词

**文档版本**：V1.1
**最后更新**：2026-07-26
**适用工具**：Design with TRAE
**关联文档**：
- 设计文档：`docs/superpowers/specs/2026-07-26-multi-end-ui-design-prompts-design.md`
- 实现计划：`docs/superpowers/plans/2026-07-26-multi-end-ui-design-prompts.md`
- 域拆分迁移状态：`docs/feature-inventory/domain-migration-status.md`

---

## 0. 域拆分迁移双轨期说明（2026-07-26 起）

阶段1-2 已完成：Identity / UserCenter / AccessControl / Points / Membership / Review / AfterSales 七个新域已就绪并经网关双轨挂载。本文档集内所有 API 端点路径保持不变，**服务归属按下表更新**，旧域（UserAuth / PointsMembership / ReviewAfterSales）代码保留作回滚兜底，待阶段3观察期结束后下线。

| 旧域 | 新域 | 端点数 | 状态 |
|-|-|-|-|
| UserAuth（认证部分） | Identity | 28 | 阶段1完成 |
| UserAuth（用户中心部分） | UserCenter | 17 | 阶段1完成 |
| UserAuth（权限部分） | AccessControl | 7 | 阶段1完成 |
| PointsMembership（积分部分） | Points | 16 + gRPC | 阶段1完成 |
| PointsMembership（会员部分） | Membership | 12 | 阶段1完成 |
| ReviewAfterSales（评价部分） | Review | 11 + gRPC | 阶段1完成 |
| ReviewAfterSales（售后部分） | AfterSales | 14 | 阶段1完成 |

**双轨期注意事项**：
1. 网关灰度默认 5%，可通过 `Grayscale:Threshold` 调整；internal 端点 100% 切新域
2. 回滚开关：`Grayscale:RollbackToLegacy=true` 即将流量回退至旧域
3. 各页面提示词中「数据模型与 API 对接」段引用的端点路径不变，仅服务归属从旧域迁移至新域
4. 详细迁移状态见 `docs/feature-inventory/domain-migration-status.md`

---

## 1. 项目背景

Leno 是基于 .NET 10 + DDD 架构的 B2C 电商平台，已实现 17 个限界上下文（BC）、346+ 个 API 端点。平台按角色拆分为 4 个独立部署端：系统管理后台、运营管理后台、商家管理后台、用户 APP。

本文档集是 4 端 UI 设计提示词的完整产出，基于工区已实现的 API 端点与 `docs/spec/` 12 篇需求文档逆向还原功能清单，采用「共享设计系统 + 4 端并行 subagent」方案生成。每个页面提示词是自包含的设计文档，可直接复制到 Design with TRAE 生成 Vue 3 前端界面。

---

## 2. 设计决策

| 决策项 | 选择 | 理由 |
|-|-|-|
| 视觉风格 | Ant Design Vue 4.x（三端后台）+ Vant 4.x（用户 APP） | Vue 3 生态最成熟的组件库，设计语言对齐 |
| 技术栈 | Vue 3.5 + TypeScript + Vite 6 + Pinia + Vue Router 4 | 当前稳定主线，Composition API + `<script setup>` |
| 主题 | Ant Design 默认蓝色主题 `#1677FF` | 与 Ant Design 5.x 设计语言对齐 |
| 语言 | 中文为主（zh-CN），预留 i18n 骨架 | vue-i18n 9.x，按端懒加载 locale 文件 |
| 主题模式 | 亮色为主，预留暗色切换点 | ConfigProvider token + darkAlgorithm |

---

## 3. 文档结构

```
docs/design-prompts/
├── README.md                                    # 本文件
├── shared/                                      # 共享设计规范（5 个文件）
│   ├── design-system.md                         # 设计系统规范（技术栈/令牌/布局/i18n/组件/工程化）
│   ├── prompt-template.md                       # 提示词写作模板（8 段页面 + 6 段总览）
│   ├── writing-guide.md                         # 文案与微交互风格指南
│   ├── glossary.md                              # 统一术语表与同义词禁用清单
│   └── components.md                            # 跨端共享业务组件清单（10 个组件）
├── system-admin/                                # 系统管理后台（29 个文件）
│   ├── 00-overview.md                          # 端总览
│   ├── 01-dashboard/                           # 仪表盘（7 页面）
│   ├── 02-user-access/                         # 用户与权限（4 页面）
│   ├── 03-system-governance/                   # 系统治理（4 页面）
│   ├── 04-runtime-ops/                         # 运行时运维（6 页面）
│   ├── 05-audit/                               # 审计与对账（3 页面）
│   ├── 06-account/                             # 个人账号（3 页面）
│   └── 07-monitoring/                          # 系统监控（1 页面）
├── operations/                                  # 运营管理后台（35 个文件）
│   ├── 00-overview.md
│   ├── 01-dashboard/                           # 数据看板（6 页面）
│   ├── 02-product-ops/                         # 商品运营（3 页面）
│   ├── 03-promotion-ops/                       # 促销运营（3 页面）
│   ├── 04-seller-ops/                          # 卖家运营（3 页面）
│   ├── 05-order-ops/                           # 订单运营（4 页面）
│   ├── 06-payment-ops/                         # 支付运营（3 页面）
│   ├── 07-notification-ops/                    # 通知运营（4 页面）
│   ├── 08-membership-ops/                      # 会员运营（3 页面）
│   ├── 09-account/                             # 个人账号（4 页面）
│   └── 10-data-export/                         # 数据导出（1 页面）
├── seller/                                      # 商家管理后台（24 个文件）
│   ├── 00-overview.md
│   ├── 01-onboarding/                          # 入驻与店铺（4 页面）
│   ├── 02-dashboard/                           # 工作台（3 页面）
│   ├── 03-product-management/                  # 商品管理（4 页面）
│   ├── 04-logistics/                           # 物流（2 页面）
│   ├── 05-order-fulfillment/                   # 订单履约（3 页面）
│   ├── 06-after-sales/                         # 售后处理（2 页面）
│   ├── 07-review/                              # 评价（1 页面）
│   ├── 08-account/                             # 个人账号（3 页面）
│   └── 09-export/                              # 报表导出（1 页面）
└── buyer-app/                                   # 用户 APP（49 个文件）
    ├── 00-overview.md
    ├── 01-auth/                                # 认证（5 页面）
    ├── 02-home/                                # 首页（3 页面）
    ├── 03-catalog/                             # 商品目录（4 页面）
    ├── 04-shop/                                # 店铺（1 页面）
    ├── 05-cart/                                # 购物车（3 页面）
    ├── 06-order/                               # 订单交易（5 页面）
    ├── 07-payment/                             # 支付（2 页面）
    ├── 08-promotion/                           # 优惠（2 页面）
    ├── 09-review/                              # 评价（3 页面）
    ├── 10-after-sales/                         # 售后（3 页面）
    ├── 11-points-membership/                   # 积分会员（7 页面）
    ├── 12-notification/                        # 通知（2 页面）
    ├── 13-profile/                             # 我的（6 页面）
    └── 14-public/                              # 公共（2 页面）
```

**文件统计**：
- 共享文档：5 个
- 系统管理后台：29 个（1 总览 + 28 页面）
- 运营管理后台：35 个（1 总览 + 34 页面）
- 商家管理后台：24 个（1 总览 + 23 页面）
- 用户 APP：49 个（1 总览 + 48 页面）
- README：1 个
- **合计：143 个文件**

---

## 4. 使用方式

### 4.1 阅读顺序

1. **阅读共享设计系统规范**：`shared/design-system.md`，了解技术栈、设计令牌、布局、组件约定
2. **阅读目标端的端总览**：`{端目录}/00-overview.md`，了解端级定位、信息架构、路由规划
3. **按模块阅读页面提示词**：`{端目录}/{模块}/{页面}.md`，每个文件是自包含的设计文档
4. **将单个提示词复制到 Design with TRAE**：生成对应的 Vue 3 页面代码

### 4.2 提示词结构

每个页面提示词包含 8 段：
1. **页面定位**：所属端/模块/类型/目标用户/核心目标/访问入口/实现状态
2. **页面布局与信息架构**：整体布局/关键区域/响应式断点/首屏内容/线框图描述
3. **数据模型与 API 对接**：API 表格/请求参数/响应字段/加载策略/缓存策略
4. **交互流程**：主流程/分支流程/跨页面流转/状态机可视化
5. **组件清单**：基础组件/业务组件/图表组件/图标/空状态
6. **视觉规范**：主色应用/状态色/间距/字体/图标尺寸
7. **异常处理与边界**：加载态/空数据/错误态/权限控制/并发/危险操作确认
8. **验收要点**：可勾选的验收点/性能要求/可访问性

### 4.3 实现状态标注

每个页面提示词标注实现状态：
- ✅ **已实现**：API 端点已存在于后端代码，可直接对接
- 🚧 **规划中**：仅有需求文档，API 未实现，需后端补充
- ➕ **补充功能**：API 未提供但合理推断的常规功能，需后端补充

---

## 5. 4 端模块统计

| 端 | 模块数 | 页面数 | 总览 | 文件总数 | 实现状态分布 |
|-|-|-|-|-|-|
| 系统管理后台 | 7 | 28 | 1 | 29 | ✅ ×25 / 🚧 ×2 / ➕ ×1 |
| 运营管理后台 | 10 | 34 | 1 | 35 | ✅ ×29 / 🚧 ×2 / ➕ ×3 |
| 商家管理后台 | 9 | 23 | 1 | 24 | ✅ ×15 / 🚧 ×1 / ➕ ×7 |
| 用户 APP | 14 | 48 | 1 | 49 | ✅ ×40 / 🚧 ×4 / ➕ ×4 |
| **合计** | **40** | **133** | **4** | **137** | ✅ ×109 / 🚧 ×9 / ➕ ×15 |

> 另有 shared 目录 5 个文件 + README 1 个，总计 143 个文件。

---

## 6. 与后端 API 的对应关系

提示词中标注的 API 端点来自以下来源：
- **已实现端点**（✅）：来自 `src/Backends/Leno.{BC}.Api/Controllers/` 下的控制器，共 346+ 端点
- **规划中端点**（🚧）：来自 `docs/spec/` 12 篇需求文档，后端尚未实现
- **补充端点**（➕）：API 未提供但合理推断的常规功能，已在提示词中给出建议端点契约

API 端点引用规范：
- 格式：`{METHOD} {/api/path}`（大写方法，路径以 /api/ 开头）
- 鉴权：标注角色（Admin / Operator / Seller / Buyer / 公开）
- 真实性：所有 ✅ 端点可在后端控制器中找到实现，🚧/➕ 端点已明确标注

**声明**：各页面提示词中引用的 API 路径与方法以 `docs/spec/` 需求文档为准。如页面提示词中的 API 与实际后端实现存在差异，以实际后端实现为准。设计提示词中的 API 引用主要用于说明页面数据来源与交互逻辑，不作为 API 契约的权威定义。

---

## 7. 共享业务组件

`shared/components.md` 定义了 10 个跨端共享业务组件：

| 组件 | 用途 | 引用章节 |
|-|-|-|
| `StatusTag` | 通用状态标签（订单/售后/商品/店铺/支付） | §1 |
| `IdempotencyButton` | 幂等提交按钮（防抖+重复拦截） | §2 |
| `PermissionGuard` | 权限守卫（按钮级权限控制） | §3 |
| `DateTimeRangePicker` | 日期时间范围选择器（含预设） | §4 |
| `EmptyState` | 空状态（图标+描述+CTA） | §5 |
| `DataTable` | 数据表格（分页+排序+虚拟滚动） | §6 |
| `ChartLine/ChartPie/ChartBar/ChartGauge` | 4 类图表组件 | §7 |
| `DashboardCard` | 看板卡片（标题+数值+趋势+图表插槽） | §8 |
| `AuditLogViewer` | 审计日志查看器（只读 Descriptions + JSON） | §9 |
| `ConfirmDialog` | 危险操作确认对话框 | §10 |

页面提示词的「组件清单」段以 `（见 shared/components.md §章节号）` 格式引用这些组件。

---

## 8. 术语表

`shared/glossary.md` 定义了统一术语，所有页面提示词严格遵循：

- **22 个核心术语**：SPU/SKU/聚合根/领域事件/集成事件/预占库存/真实库存/积分/成长值/会员等级/付费会员/支付单/店铺/卖家账号/入驻申请/店铺标识/资质/看板报表/死信消息/索引重建任务/审计日志条目/限流规则
- **12 个 BC 缩写**：UserAuth/Product/Cart/Order/Promotion/ReviewAfterSales/Payment/PointsMembership/Inventory/Notification/SellerShop/SystemAdmin
- **4 类角色术语**：买家(Buyer)/卖家(Seller)/运营管理员(Operator)/系统管理员(Admin)
- **同义词禁用清单**：商铺→店铺、商户→卖家、闪购→秒杀、产品→商品、折扣券→优惠券、经验值→成长值、VIP→付费会员

---

## 9. 已知偏离点

4 端产出与共享设计系统的偏离点（已在各端 00-overview.md 说明）：

### 9.1 运营管理后台
- 数据看板数值字号：默认 16px → 24px semibold（突出数据看板）
- 表格行高：56px → 48px（提升信息密度）

### 9.2 商家管理后台
- 无偏离，完全遵循共享设计系统

### 9.3 用户 APP
- 购物车模块：原清单 `anonymous-cart.md` + `cart.md` 合并为 `cart.md`（匿名/登录态用 tab 切换），新增 `checkout-settle.md`（结算拆分为 preview + settle 两步）

### 9.4 系统管理后台
- 无偏离，完全遵循共享设计系统

---

## 10. 维护与更新

- **新增页面**：在对应模块目录下新建 `.md` 文件，遵循 `shared/prompt-template.md` 的 8 段结构
- **修改设计令牌**：更新 `shared/design-system.md`，4 端同步生效
- **新增共享组件**：在 `shared/components.md` 中定义，4 端可引用
- **术语变更**：更新 `shared/glossary.md`，4 端同步生效

---

## 11. 生成方式

本文档集采用「共享设计系统 + 4 端并行 subagent」方案生成：
- **阶段 0**：主代理同步产出 5 份共享基线文档
- **阶段 1**：4 个 subagent 并行执行（system-admin / operations / seller / buyer-app）
- **阶段 2**：主代理执行 9 项一致性校验（文件完整性、8 段结构、术语统一、API 准确性、设计令牌、实现状态、组件库区分、危险操作确认）

生成过程详见 `docs/superpowers/plans/2026-07-26-multi-end-ui-design-prompts.md`。
