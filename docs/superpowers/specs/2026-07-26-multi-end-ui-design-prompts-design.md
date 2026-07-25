# Leno 电商平台 4 端 UI 设计提示词生成 - 设计文档

**文档版本**：V1.0
**创建日期**：2026-07-26
**作者**：brainstorming skill
**关联文档**：
- `docs/spec/00-需求文档总览与DDD架构.md`
- `docs/spec/10-模块化部署架构.md`
- `docs/spec/01-用户与认证授权域.md` 至 `docs/spec/12-系统管理域.md`
- `docs/contracts/internal-api-contracts.md`
- `docs/superpowers/specs/2026-07-21-code-audit/` 各 BC 审计报告

## 0 摘要

本文档定义 Leno 电商平台 4 端（系统管理后台、运营管理后台、商家管理后台、用户 APP）UI 设计提示词的生成方案。基于工区已实现的 346+ API 端点与 `docs/spec/` 12 篇需求文档逆向还原功能清单，采用「共享设计系统 + 4 端并行 subagent」方案，最终产出 133 份页面提示词 + 4 份端总览 + 5 份共享文档 + 1 份 README = 143 个文件。

## 1 项目背景与目标

### 1.1 项目背景

Leno 是基于 .NET 10 + DDD 架构的 B2C 电商平台，已实现 17 个限界上下文（BC）、346+ 个 API 端点。平台按角色拆分为 4 个独立部署端：
- **系统管理后台**（Admin）：13 控制器 74 端点
- **运营管理后台**（Operator）：跨 BC 的 `/api/admin/*` 端点
- **商家管理后台**（Seller）：`/api/seller/*` + 卖家可访问端点
- **用户 APP**（Buyer）：移动端，`/api/*` 买家端点

各端 API 已具备，但缺少前端 UI 设计基线。需生成 Design with TRAE 用的页面设计提示词，指导前端 Vue 3 项目开发。

### 1.2 项目目标

| 目标 | 衡量标准 |
|-|-|
| 逆向还原 4 端功能清单 | 4 端功能模块清单 + 实现状态标注（已实现/规划中/补充） |
| 统一设计系统规范 | 5 份共享文档（设计令牌、模板、文案、术语、组件） |
| 生成 4 端页面提示词 | 133 个页面提示词 + 4 份端总览 |
| 保证 4 端视觉一致 | 设计令牌统一、术语统一、共享组件引用一致 |
| 并行执行提升效率 | 4 个 subagent 并行，总时长约等于单端最长时长 |

### 1.3 范围与约束

**在范围内**：
- 4 端共 133 个页面提示词文档
- 5 份共享设计规范文档
- 1 份 README 总览
- Git 提交与推送

**不在范围内**：
- 实际生成 Vue 组件代码（仅生成 Design with TRAE 用的提示词）
- 搭建 Vue 项目脚手架
- 实现路由、状态管理、API 调用代码
- 生成暗色主题样式
- 生成多语言文本（仅 zh-CN）
- 生成单元测试或 E2E 测试
- 后端 API 实现（含规划中功能的端点开发）
- 设计稿（Figma/Sketch）产出

**约束**：
- 技术栈：Vue 3.5 + TypeScript + Vite 6 + Pinia + Vue Router 4
- UI 库：Ant Design Vue 4.x（三端后台）+ Vant 4.x（用户 APP）
- 主题：Ant Design 默认蓝色主题 #1677FF
- 语言：中文为主，预留 i18n 骨架
- 主题模式：亮色为主，预留暗色切换点

## 2 共享设计系统规范

### 2.1 技术栈选型

| 端 | 框架与构建 | UI 库 | 状态/路由 | 图表 | 表单 |
|-|-|-|-|-|-|
| 系统管理后台 | Vue 3.5 + TypeScript 5.x + Vite 6 | Ant Design Vue 4.x | Pinia 2.x + Vue Router 4.x | @vue-echarts 7.x（ECharts 5.5） | Ant Design Vue Form |
| 运营管理后台 | Vue 3.5 + TypeScript 5.x + Vite 6 | Ant Design Vue 4.x | Pinia 2.x + Vue Router 4.x | @vue-echarts 7.x | Ant Design Vue Form |
| 商家管理后台 | Vue 3.5 + TypeScript 5.x + Vite 6 | Ant Design Vue 4.x | Pinia 2.x + Vue Router 4.x | @vue-echarts 7.x | Ant Design Vue Form |
| 用户 APP | Vue 3.5 + TypeScript 5.x + Vite 6 + PWA | Vant 4.x | Pinia 2.x + Vue Router 4.x | — | Vant Form |

**选型理由**：
- **Vue 3.5**：当前稳定主线，`<script setup>` + Composition API 是新项目标准
- **Vite 6**：最新构建工具，HMR 与构建速度领先
- **Ant Design Vue 4.x**：与 React 版 Ant Design 5.x 设计语言对齐，支持 ConfigProvider token 主题定制，4.x 版本对 Vue 3.5 完整适配
- **Vant 4.x**：移动端 Vue 3 生态最成熟的组件库，与 Ant Design Vue 共享设计令牌
- **Pinia 2.x**：Vue 官方推荐的状态管理，TS 支持优于 Vuex
- **@vue-echarts 7.x**：ECharts 5.5 的 Vue 3 包装，Composition API 友好

### 2.2 设计令牌（Design Tokens）

统一采用 W3C DTCG 格式，4 端共享：

- **color/primary**: `#1677FF`（Ant Design 默认蓝）
- **color/success**: `#52C41A` / **color/warning**: `#FAAD14` / **color/error**: `#FF4D4F`
- **color/neutral**: 1-10 灰阶（背景 #FFFFFF → 文字 #000000D9）
- **radius/base**: `6px` / **radius/card**: `8px`
- **spacing/unit**: `4px`（4/8/12/16/24/32/48）
- **font/family**: `"PingFang SC", "Microsoft YaHei", -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`
- **font/size**: 12/14/16/20/24/30（rem 单位，根字号 16px）

### 2.3 布局栅格

- **三端后台**：基于 Ant Design Vue 的 `BasicLayout`（顶部 Logo + 左侧 Sider + 主内容区），Sider 可折叠，主内容 24 栅格
- **用户 APP**：Vant `Tabbar` 底部导航（首页/分类/购物车/我的）+ 顶部 NavBar，单列流式布局，使用 `van-tabs` 与 `van-list` 实现无限滚动

### 2.4 i18n 与主题预留

- **i18n 骨架**：使用 `vue-i18n 9.x`，所有文案走 `$t('namespace.key')` 或 `t()` 组合式函数。提示词中明确「中文文案为默认 locale（zh-CN）」，生成时输出 `zh-CN.json` 与 key 引用，不生成其他语言
- **主题预留**：通过 Ant Design Vue 的 `<a-config-provider :theme="{ token: {...} }">` 注入设计令牌；提示词中提及「亮色为默认，需预留 `algorithm: theme.darkAlgorithm` 切换点」但不生成暗色样式

### 2.5 文案与微交互风格

- **文案风格**：简洁中性、动词开头、避免口语化（如「提交审核」而非「点击这里提交审核」）
- **微交互**：表单提交防抖、列表加载骨架屏（Ant Design Vue Skeleton / Vant Skeleton）、操作成功 message 反馈 1.5s、危险操作 `Modal.confirm` 二次确认
- **空状态**：所有列表页须含 Empty 占位 + CTA 按钮
- **加载态**：所有数据请求须含 Skeleton 或 Spin

### 2.6 组件库统一约定

- **数据展示**：Table（虚拟滚动 `:scroll="{ y: 500 }"`，超过 100 行启用）、Descriptions、Statistic、Card
- **表单**：`<a-form>` + `<a-form-item>` + `rules` 校验规则（required/pattern/min-max）
- **反馈**：`message.success/error`、`Modal.confirm`、`notification.warning`
- **导航**：Menu（侧边/顶部）、Breadcrumb、Steps
- **图表**：Line（趋势）、Pie（分布）、Bar（排行）、Gauge（成功率），统一通过 @vue-echarts 封装为业务图表组件

### 2.7 工程化约定（Vue 3 特有）

- **组件风格**：统一 `<script setup lang="ts">` 语法，不使用 Options API
- **状态管理**：Pinia store 按 BC 模块拆分（如 `useOrderStore`、`useProductStore`），不使用全局单一 store
- **路由**：Vue Router 4 动态路由 + 路由守卫（`beforeEach` 处理鉴权与菜单加载）
- **请求层**：axios 封装 + 拦截器（统一处理 401/403/500、Idempotency-Key 注入、Bearer Token）
- **类型定义**：与后端 DTO 对齐的 TypeScript interface 定义在 `types/` 目录，按 BC 分文件

## 3 4 端功能清单划分

基于已扫描的 346+ 端点 + `docs/spec/` 12 篇需求文档逆向还原，按角色与端拆分功能清单。每项标注实现状态：✅ 已实现 / 🚧 规划中（仅有需求文档或占位）/ ➕ 补充（API 未提供但合理推断的常规功能）。

### 3.1 系统管理后台（System Admin Console）

**用户角色**：系统管理员（Admin）
**职责定位**：技术运维侧，保障系统稳定。强调安全、审计、低频重操作。
**网关鉴权**：JWT + 双因子 + IP 白名单 + 全操作审计

#### 3.1.1 已实现功能（来源：SystemAdmin BC 13 控制器 74 端点）

| 模块 | 功能点 | 端点示例 |
|-|-|-|
| 仪表盘 | 运营总览、支付统计、积分统计、通知送达率、售后统计、店铺排行、报表快照 | `/api/admin/dashboard/*` |
| 用户与权限管理 | 用户列表/详情、角色 CRUD、角色权限分配、OAuth 客户端管理、用户封禁/恢复 | `/api/admin/users`, `/api/admin/roles`, `/api/admin/oauth-clients` |
| 运营人员管理 | 运营人员列表、创建、权限、激活/停用 | `/api/admin/operators` |
| 功能开关 | 功能开关 CRUD、启停、评估 | `/api/admin/feature-flags` |
| 系统配置 | 配置项 CRUD、按组分类、按 key 查询、启停 | `/api/admin/system-configs` |
| 数据字典 | 字典 CRUD、字典项 CRUD、按 code 公开查询 | `/api/admin/dictionaries`, `/api/dictionaries/{code}` |
| 限流规则 | 规则 CRUD、启停 | `/api/admin/rate-limit-rules` |
| 索引重建 | 重建任务列表、触发、进度跟踪、重试 | `/api/admin/index-rebuild/tasks` |
| 死信队列管理 | 死信列表、详情、单条/批量重投/丢弃 | `/api/admin/dead-letters` |
| 审计日志 | 审计日志列表、详情、导出、操作日志 | `/api/admin/audit-logs` |
| 定时任务 | 任务 CRUD、启停、立即执行 | `/api/admin/scheduled-tasks` |
| 健康监控 | 整体健康、模块健康详情 | `/api/admin/health` |
| 公告管理 | 公告 CRUD、发布/撤回、公开查询 | `/api/admin/announcements` |
| 对账管理 | 对账状态、触发对账、对账记录 | `/api/admin/statistics/reconciliation*` |

#### 3.1.2 规划中功能（来源：docs/spec/12-系统管理域.md，部分端点已实现）

| 功能点 | 来源 | 备注 |
|-|-|-|
| DashboardReport 快照管理 | F-SYS-001~006 | 已部分实现，需补充快照版本对比 UI |
| 限流规则按 BC 分治 | 6.2.1 节 | 11 BC 独立 InternalApiKey 配置界面 |
| Outbox 积压监控 | 6.5.1 节 | outbox_pending_count 指标可视化 |
| Alertmanager 告警闭环 | 6.5.1 节 | 4 条核心告警规则配置与查看 |

#### 3.1.3 合理补充功能（API 未提供但常规运维需要）

| 功能点 | 理由 |
|-|-|
| 登录二次验证页面 | 系统管理员强制双因子，需独立验证页 |
| 个人中心 / 修改密码 | AdminUsersController 仅管理他人，自己改密走 `/api/users/me/password` |
| 通知中心 | 系统管理员接收的告警与待办通知 |
| 操作日志（自己的） | 区别于审计日志（全员），展示当前管理员操作历史 |
| 系统监控大盘 | 整合 Prometheus 指标（QPS/延迟/MQ 队列/Redis 命中率）的可视化 |

### 3.2 运营管理后台（Operations Console）

**用户角色**：运营管理员（Operator）
**职责定位**：平台经营侧，治理商家与活动。低频重操作 + 数据看板。
**网关鉴权**：JWT + 操作二次确认 + IP 白名单 + 操作审计

#### 3.2.1 已实现功能（来源：跨 BC 的 `/api/admin/*` 端点 + SellerShop Admin）

| 模块 | 功能点 | 端点示例 |
|-|-|-|
| 数据看板 | 运营总览、支付统计、积分统计、通知送达率、售后统计、店铺排行 | `/api/admin/dashboard/*`（与系统管理共享） |
| 商品审核 | 审核通过/驳回、补货、调整库存、查询全部商品 | `/api/admin/products/*` |
| 品牌管理 | 品牌 CRUD、启停 | `/api/admin/brands` |
| 分类管理 | 分类树 CRUD、启停 | `/api/admin/categories` |
| 促销活动 | 促销活动 CRUD、激活/暂停/关闭 | `/api/admin/promotions` |
| 优惠券管理 | 优惠券模板 CRUD、启停、批量发放 | `/api/admin/coupons` |
| 秒杀活动 | 秒杀活动 CRUD、激活、关闭 | `/api/admin/seckill/activities` |
| 卖家入驻审核 | 入驻申请列表、详情、通过/驳回、批量审核 | `/api/admin/applications`, `/api/admin/shops/{id}/approve` |
| 店铺治理 | 店铺列表、详情、暂停/恢复/关闭、资质审核 | `/api/admin/shops/*` |
| 订单管理 | 运营查询全部订单、强制取消 | `/api/admin/orders`, `/api/admin/orders/{id}/force-cancel` |
| 售后处理 | 售后列表、运营同意/拒绝 | `/api/admin/after-sales/*` |
| 评价审核 | 评价列表、审核通过/隐藏 | `/api/admin/reviews/*` |
| 物流公司管理 | 物流公司 CRUD、启停 | `/api/admin/logistics-companies` |
| 会员等级 | 等级列表、创建/更新/启停 | `/api/admin/members/levels` |
| 会员套餐 | 套餐 CRUD、启停 | `/api/admin/membership-packages` |
| 支付记录 | 全平台支付记录分页 | `/api/admin/payments` |
| 退款记录 | 全平台退款记录分页 | `/api/admin/refunds` |
| 通知模板 | 模板 CRUD、启停、预览 | `/api/admin/notification-templates` |
| 通知记录 | 记录列表、详情、按业务查询、重发、统计 | `/api/notifications/records`, `/api/admin/notifications/*` |
| 通知配置 | 配置查询/更新、测试发送 | `/api/admin/notification-config` |
| 通知限流 | 限流规则查询/更新 | `/api/admin/notification-rate-limits` |
| 支付渠道配置 | 渠道列表、详情、更新、启停 | `/api/admin/payment-channels` |

#### 3.2.2 规划中功能（来源：docs/spec/）

| 功能点 | 来源 | 备注 |
|-|-|-|
| 积分规则配置 | F-PTS-007（积分与会员域） | 任务中心、积分规则管理 |
| 卖家统计看板 | F-SHP-002 | `topShopsBySales`、临期资质列表 |
| 评价隐藏后通知用户 | F-REV | ReviewHiddenEvent 触发 |

#### 3.2.3 合理补充功能

| 功能点 | 理由 |
|-|-|
| 登录 / 双因子验证 | Operator 同样需要安全登录 |
| 个人中心 / 修改密码 | 复用 `/api/users/me/*` |
| 待办工作台 | 汇总待审核商品/入驻申请/售后单/评价，提供入口 |
| 通知中心 | 接收系统告警与待办提醒 |
| 数据导出 | 看板与列表的 Excel/CSV 导出（部分端点已支持 `export`） |

### 3.3 商家管理后台（Seller Console）

**用户角色**：卖家（Seller）
**职责定位**：店铺经营侧，管理商品与履约。中低频写。
**网关鉴权**：JWT + 卖家角色校验 + 店铺级限流

#### 3.3.1 已实现功能（来源：跨 BC 的 `/api/seller/*` + 卖家可访问的 `/api/*`）

| 模块 | 功能点 | 端点示例 |
|-|-|-|
| 卖家入驻 | 提交入驻申请、查询我的申请 | `/api/shops/application`, `/api/seller/applications/current` |
| 店铺管理 | 查询我的店铺、更新店铺资料、上传资质 | `/api/shops/me`, `/api/shops/me/qualifications` |
| 卖家工作台 | 店铺看板、销售趋势、店铺指标 | `/api/seller/dashboard`, `/api/seller/sales-trend`, `/api/seller/metrics` |
| 商品管理 | 创建草稿、更新、提交审核、下架/重新上架、SKU 管理、价格调整、价格历史 | `/api/products`, `/api/products/{id}/skus/*` |
| 运费模板 | 模板 CRUD、规则更新、启停、查询我的模板 | `/api/seller/freight-templates` |
| 订单履约 | 卖家发货、物流轨迹查询 | `/api/seller/orders/{id}/ship`, `/api/orders/{id}/logistics-trace` |
| 售后处理 | 卖家售后列表、同意/拒绝/确认收货 | `/api/seller/after-sales/*` |
| 评价回复 | 卖家回复评价 | `/api/reviews/{id}/reply` |

#### 3.3.2 规划中功能（来源：docs/spec/11-卖家与店铺管理域.md）

| 功能点 | 来源 | 备注 |
|-|-|-|
| 资质过期续期提醒 | F-SHP-004, AC-SHP-004a | 临期资质提醒（提前 7 天） |
| 店铺前台展示预览 | F-SHP-007 | 卖家预览买家看到的店铺页 |

#### 3.3.3 合理补充功能

| 功能点 | 理由 |
|-|-|
| 登录 / 注册入口 | 卖家账号登录（与买家共用 `/api/auth/login`） |
| 个人中心 / 修改密码 | 复用 `/api/users/me/*` |
| 待发货订单提醒 | 工作台突出展示待发货数量 |
| 商品库存预警 | 低库存商品列表，便于补货决策 |
| 销售报表导出 | 销售趋势、订单明细的 Excel 导出 |
| 通知中心 | 接收新订单、售后申请、平台公告 |

### 3.4 用户 APP（Buyer App）

**用户角色**：买家（Buyer）
**职责定位**：个人消费侧，浏览、下单、售后、积分会员。高并发读 + 秒杀峰值。
**网关鉴权**：JWT + 买家角色校验 + 滑动窗口限流

#### 3.4.1 已实现功能（来源：跨 BC 的 `/api/*` 买家端点）

| 模块 | 功能点 | 端点示例 |
|-|-|-|
| 用户认证 | 注册、登录、刷新令牌、登出、OAuth 登录、双因子、忘记/重置密码 | `/api/auth/*` |
| 个人资料 | 查询/修改资料、修改密码、双因子管理、外部登录绑定 | `/api/users/me/*`, `/api/account/external-logins` |
| 收货地址 | 地址 CRUD、设默认 | `/api/users/me/addresses` |
| 商品浏览 | 全文搜索、商品详情、品牌/分类查询 | `/api/products/search`, `/api/products/{id}`, `/api/brands`, `/api/categories/tree` |
| 店铺浏览 | 店铺公开信息 | `/api/shops/{shopId}` |
| 购物车 | 匿名购物车、登录购物车、登录合并、结算预览 | `/api/cart`, `/api/cart/anonymous`, `/api/cart/preview` |
| 订单交易 | 创建订单、立即购买、下单预览、查询我的订单、订单详情、确认收货、取消 | `/api/orders/*` |
| 支付 | 发起支付、查询支付结果、查询退款结果 | `/api/payments`, `/api/refunds/{afterSalesId}` |
| 秒杀 | 进行中活动列表、活动详情、秒杀下单 | `/api/seckill/activities/*` |
| 优惠券 | 可领券列表、领取、我的优惠券 | `/api/coupons/*` |
| 评价 | 提交评价、上传评价图片、我的评价、商品评价列表 | `/api/reviews/*` |
| 售后 | 申请售后、退货、取消、按订单查询、我的售后、上传凭证 | `/api/after-sales/*` |
| 积分 | 签到、积分账户、积分流水、积分兑换券 | `/api/points/*` |
| 任务中心 | 任务列表、完成任务 | `/api/points/tasks/*` |
| 会员 | 我的会员、等级列表、套餐列表、订阅套餐 | `/api/members/me`, `/api/members/levels`, `/api/membership-packages` |
| 通知 | 我的通知、未读数、标记已读 | `/api/notifications/*` |
| 通知偏好 | 查询/更新通知偏好 | `/api/users/me/notification-preferences` |
| 物流轨迹 | 查询订单物流 | `/api/orders/{id}/logistics-trace` |
| 公告 | 公开公告查询 | `/api/announcements` |
| 数据字典 | 按 code 公开查询 | `/api/dictionaries/{code}` |

#### 3.4.2 规划中功能（来源：docs/spec/）

| 功能点 | 来源 | 备注 |
|-|-|-|
| 付费会员订阅 | F-MEM-004 | 已部分实现（套餐订阅），需补充权益展示 |
| 积分抽奖 | F-PTS | 任务中心扩展 |
| 商品收藏 | 常规电商功能 | API 未提供，需补充 |
| 浏览历史 | 常规电商功能 | API 未提供，需补充 |

#### 3.4.3 合理补充功能

| 功能点 | 理由 |
|-|-|
| 首页推荐流 | APP 首页必须，整合 banner/秒杀/推荐商品/分类入口 |
| 分类导航页 | 商品分类树 + 二级分类商品列表 |
| 搜索历史与热搜 | 搜索体验标配 |
| 商品详情增强 | SKU 选择器、规格对比、客服咨询入口 |
| 购物车多店拆单提示 | Cart BC 已支持按卖家拆单 |
| 订单状态机可视化 | 订单详情页展示待支付→待发货→待收货→已完成流转 |
| 售后进度可视化 | 售后单状态机展示 |
| 会员权益中心 | 等级权益、付费会员权益对比 |
| 我的页面聚合 | 订单/售后/优惠券/积分/地址/设置入口聚合 |

### 3.5 功能清单划分汇总

| 端 | 已实现模块数 | 规划中功能数 | 补充功能数 | 总功能模块数 |
|-|-|-|-|-|
| 系统管理后台 | 14 | 4 | 5 | 23 |
| 运营管理后台 | 22 | 3 | 5 | 30 |
| 商家管理后台 | 8 | 2 | 6 | 16 |
| 用户 APP | 19 | 4 | 9 | 32 |

## 4 提示词写作模板与产出结构

### 4.1 产出目录结构

```
docs/design-prompts/
├── README.md                                    # 总览与使用说明
├── shared/
│   ├── design-system.md                         # 共享设计系统规范（第 2 节内容）
│   ├── prompt-template.md                       # 提示词写作模板（4.2 节）
│   ├── writing-guide.md                         # 文案/i18n/微交互风格指南
│   ├── glossary.md                              # 统一术语表（来自 docs/spec/00）
│   └── components.md                            # 跨端共享业务组件清单
├── system-admin/                                # 系统管理后台
│   ├── 00-overview.md                          # 端总览
│   ├── 01-dashboard/                           # 仪表盘模块
│   │   ├── operations-overview.md
│   │   ├── payment-stats.md
│   │   ├── points-stats.md
│   │   ├── notification-delivery.md
│   │   ├── after-sales-stats.md
│   │   ├── shop-ranking.md
│   │   └── report-snapshots.md
│   ├── 02-user-access/                         # 用户与权限模块
│   │   ├── user-management.md
│   │   ├── role-management.md
│   │   ├── oauth-clients.md
│   │   └── operators.md
│   ├── 03-system-governance/                   # 系统治理模块
│   │   ├── feature-flags.md
│   │   ├── system-configs.md
│   │   ├── data-dictionaries.md
│   │   └── announcements.md
│   ├── 04-runtime-ops/                         # 运行时运维模块
│   │   ├── rate-limit-rules.md
│   │   ├── index-rebuild.md
│   │   ├── dead-letter-queue.md
│   │   ├── scheduled-tasks.md
│   │   ├── health-monitoring.md
│   │   └── alert-management.md                 # 🚧 规划中
│   ├── 05-audit/                               # 审计与对账模块
│   │   ├── audit-logs.md
│   │   ├── reconciliation.md
│   │   └── outbox-monitor.md                   # 🚧 规划中
│   ├── 06-account/                             # 个人账号模块
│   │   ├── login-2fa.md
│   │   ├── profile.md
│   │   └── notifications.md
│   └── 07-monitoring/                          # 系统监控大盘
│       └── prometheus-dashboard.md             # ➕ 补充
├── operations/                                  # 运营管理后台
│   ├── 00-overview.md
│   ├── 01-dashboard/                           # 数据看板（与系统管理共享端点，各自独立成文）
│   │   ├── operations-overview.md              # 同 system-admin 但权限/入口不同
│   │   ├── payment-stats.md
│   │   ├── points-stats.md
│   │   ├── notification-delivery.md
│   │   ├── after-sales-stats.md
│   │   └── shop-ranking.md                     # 不含 report-snapshots（系统管理专有）
│   ├── 02-product-ops/                         # 商品运营模块
│   │   ├── product-audit.md
│   │   ├── brand-management.md
│   │   └── category-management.md
│   ├── 03-promotion-ops/                       # 促销运营模块
│   │   ├── promotions.md
│   │   ├── coupons.md
│   │   └── seckill.md
│   ├── 04-seller-ops/                          # 卖家运营模块
│   │   ├── application-audit.md
│   │   ├── shop-governance.md
│   │   └── seller-statistics.md                # 🚧 规划中
│   ├── 05-order-ops/                           # 订单运营模块
│   │   ├── order-management.md
│   │   ├── after-sales.md
│   │   ├── review-audit.md
│   │   └── logistics-companies.md
│   ├── 06-payment-ops/                         # 支付运营模块
│   │   ├── payment-records.md
│   │   ├── refund-records.md
│   │   └── payment-channels.md
│   ├── 07-notification-ops/                    # 通知运营模块
│   │   ├── templates.md
│   │   ├── records.md
│   │   ├── config.md
│   │   └── rate-limits.md
│   ├── 08-membership-ops/                      # 会员运营模块
│   │   ├── member-levels.md
│   │   ├── membership-packages.md
│   │   └── points-rules.md                     # 🚧 规划中
│   ├── 09-account/                             # 个人账号模块
│   │   ├── login.md
│   │   ├── profile.md
│   │   ├── todo-workbench.md                   # ➕ 补充
│   │   └── notifications.md
│   └── 10-data-export/                         # 数据导出
│       └── export-center.md                    # ➕ 补充
├── seller/                                      # 商家管理后台
│   ├── 00-overview.md
│   ├── 01-onboarding/                          # 入驻与店铺模块
│   │   ├── application.md
│   │   ├── shop-profile.md
│   │   ├── qualifications.md
│   │   └── shop-preview.md                     # 🚧 规划中
│   ├── 02-dashboard/                           # 工作台模块
│   │   ├── overview.md
│   │   ├── sales-trend.md
│   │   └── low-stock-alert.md                  # ➕ 补充
│   ├── 03-product-management/                  # 商品管理模块
│   │   ├── product-list.md
│   │   ├── product-edit.md
│   │   ├── sku-management.md
│   │   └── price-history.md
│   ├── 04-logistics/                           # 物流模块
│   │   ├── freight-templates.md
│   │   └── logistics-companies.md
│   ├── 05-order-fulfillment/                   # 订单履约模块
│   │   ├── pending-shipment.md
│   │   ├── order-list.md
│   │   └── logistics-trace.md
│   ├── 06-after-sales/                         # 售后处理模块
│   │   ├── after-sales-list.md
│   │   └── after-sales-detail.md
│   ├── 07-review/                              # 评价模块
│   │   └── review-reply.md
│   ├── 08-account/                             # 个人账号模块
│   │   ├── login.md
│   │   ├── profile.md
│   │   └── notifications.md
│   └── 09-export/                              # 报表导出
│       └── sales-export.md                     # ➕ 补充
└── buyer-app/                                   # 用户 APP
    ├── 00-overview.md
    ├── 01-auth/                                # 认证模块
    │   ├── login.md
    │   ├── register.md
    │   ├── forgot-password.md
    │   ├── oauth-login.md
    │   └── two-factor.md
    ├── 02-home/                                # 首页模块
    │   ├── home-feed.md
    │   ├── banner.md
    │   └── seckill-entry.md
    ├── 03-catalog/                             # 商品目录模块
    │   ├── category-nav.md
    │   ├── search.md
    │   ├── search-results.md
    │   └── product-detail.md
    ├── 04-shop/                                # 店铺模块
    │   └── shop-detail.md
    ├── 05-cart/                                # 购物车模块
    │   ├── anonymous-cart.md
    │   ├── cart.md
    │   └── checkout-preview.md
    ├── 06-order/                               # 订单交易模块
    │   ├── order-create.md
    │   ├── order-list.md
    │   ├── order-detail.md
    │   ├── logistics-trace.md
    │   └── seckill-order.md
    ├── 07-payment/                             # 支付模块
    │   ├── payment-initiate.md
    │   └── payment-result.md
    ├── 08-promotion/                           # 优惠模块
    │   ├── coupons-available.md
    │   └── my-coupons.md
    ├── 09-review/                              # 评价模块
    │   ├── review-submit.md
    │   ├── my-reviews.md
    │   └── product-reviews.md
    ├── 10-after-sales/                         # 售后模块
    │   ├── after-sales-apply.md
    │   ├── my-after-sales.md
    │   └── after-sales-detail.md
    ├── 11-points-membership/                   # 积分会员模块
    │   ├── points-account.md
    │   ├── check-in.md
    │   ├── points-ledger.md
    │   ├── tasks-center.md
    │   ├── points-exchange.md
    │   ├── member-level.md
    │   └── membership-packages.md
    ├── 12-notification/                        # 通知模块
    │   ├── notifications.md
    │   └── preferences.md
    ├── 13-profile/                             # 我的模块
    │   ├── profile.md
    │   ├── addresses.md
    │   ├── security.md
    │   ├── favorites.md                        # ➕ 补充
    │   ├── history.md                          # ➕ 补充
    │   └── settings.md
    └── 14-public/                              # 公共模块
        ├── announcements.md
        └── dictionaries.md
```

**统计**：
- 系统管理后台：7 个模块 / 28 个页面提示词
- 运营管理后台：10 个模块 / 34 个页面提示词
- 商家管理后台：9 个模块 / 23 个页面提示词
- 用户 APP：14 个模块 / 48 个页面提示词
- **合计**：40 个模块 / 133 个页面提示词 + 4 份端总览 + 5 份共享文档 + 1 份 README = **143 个文件**

**说明**：
- "页面提示词数"不含端总览（`00-overview.md`），端总览单独统计
- 运营管理后台的 dashboard 模块（6 个页面）与系统管理后台的 dashboard 模块（7 个页面，多出 report-snapshots）共享端点但各自独立成文，权限说明与入口路径不同

### 4.2 提示词写作模板（每个页面通用）

每个页面提示词文档严格遵循以下 8 段结构，确保 4 个 subagent 输出格式一致：

```markdown
# {页面名称} - {端名称}

## 1. 页面定位
- **所属端**：{系统管理后台 / 运营管理后台 / 商家管理后台 / 用户 APP}
- **所属模块**：{模块名}
- **页面类型**：{列表页 / 详情页 / 表单页 / 看板页 / 流程页 / 嵌入页}
- **目标用户**：{角色}
- **核心目标**：{一句话描述用户来此页面要完成的任务}
- **访问入口**：{从哪些导航/页面/链接进入}
- **实现状态**：✅ 已实现 / 🚧 规划中 / ➕ 补充功能

## 2. 页面布局与信息架构
- **整体布局**：{描述整体页面结构，如"顶部面包屑 + 左侧筛选 + 右侧主表格 + 底部分页"}
- **关键区域**：
  - 区域 A（{名称}）：{内容与位置}
  - 区域 B（{名称}）：{内容与位置}
- **响应式断点**：{桌面/平板/移动端的布局差异}
- **首屏内容**：{首屏可见的关键信息}
- **线框图描述**：{用文字描述页面骨架，便于 Design with TRAE 理解}

## 3. 数据模型与 API 对接
- **主要 API**：
  | 方法 | 端点 | 用途 | 鉴权 |
  |-|-|-|-|
  | {GET} | {/api/...} | {查询} | {角色} |
- **请求参数**：{关键参数及含义}
- **响应字段**：{关键字段及类型，标注哪些用于表格列、表单、统计卡片等}
- **数据加载策略**：{进入页面时加载 / 滚动加载 / 分页加载 / 按需加载}
- **缓存策略**：{是否缓存、缓存键、过期时间}

## 4. 交互流程
- **主流程**：
  1. {用户操作} → {系统响应} → {页面变化}
  2. {用户操作} → {系统响应} → {页面变化}
- **分支流程**：
  - {条件 A}：{处理方式}
  - {条件 B}：{处理方式}
- **跨页面流转**：{跳转到哪些页面，携带哪些参数}
- **状态机可视化**：{若有状态字段，描述状态流转图}

## 5. 组件清单
- **基础组件**：{Ant Design Vue / Vant 组件列表，如 Table、Form、Modal}
- **业务组件**：{需要自研的组件，如 OrderStatusTag、ShopStatusBadge}
- **图表组件**：{若有图表，列出图表类型与数据源}
- **图标使用**：{关键图标，使用 @ant-design/icons-vue 或 Vant 内置}
- **空状态**：{无数据时的展示内容与 CTA}

## 6. 视觉规范
- **主色应用**：{哪些元素使用主色 #1677FF}
- **状态色**：{成功/警告/危险/禁用的色彩应用场景}
- **间距**：{关键间距，遵循 4/8/12/16/24/32 体系}
- **字体**：{标题/正文/辅助文字号}
- **图标尺寸**：{16/20/24/32}

## 7. 异常处理与边界
- **加载态**：{Skeleton / Spin 的应用位置}
- **空数据**：{Empty 组件的展示与 CTA}
- **错误态**：{网络错误、权限不足、数据不存在的处理}
- **权限控制**：{按钮级权限、字段级脱敏}
- **并发与乐观锁**：{若有编辑操作，描述乐观锁冲突处理}
- **危险操作确认**：{删除/暂停/关闭等操作的二次确认}

## 8. 验收要点
- [ ] {可验证的设计要点 1}
- [ ] {可验证的设计要点 2}
- [ ] {可验证的设计要点 3}
- **性能要求**：{首屏加载时间、虚拟滚动阈值、防抖节流}
- **可访问性**：{键盘导航、对比度、aria 标签}
```

### 4.3 端总览文档（00-overview.md）特殊结构

每个端的 `00-overview.md` 不使用上述页面模板，而是采用端级总览结构：

```markdown
# {端名称}总览

## 1. 端定位与角色画像
- **目标用户**：{角色描述、典型场景、技能水平}
- **核心目标**：{端的核心价值}
- **使用频率**：{高频/中频/低频}
- **设备特征**：{桌面 1440+/移动 375+/平板}

## 2. 信息架构与导航
- **一级菜单**：{菜单项列表}
- **二级菜单**：{每个一级菜单下的二级项}
- **菜单组织原则**：{按业务域/按用户任务/按使用频率}
- **快捷入口**：{工作台/待办/搜索等}

## 3. 页面路由规划
- **路由表**：{path → component → 鉴权要求}
- **路由守卫**：{登录/角色/权限校验}

## 4. 全局布局
- **布局结构**：{Sider/Header/Content/Footer}
- **全局组件**：{顶栏用户菜单/消息中心/主题切换}

## 5. 设计风格基调
- **整体气质**：{严肃专业 / 简洁现代 / 活泼亲和}
- **与共享设计系统的关系**：{遵循/微调/差异点}

## 6. 模块清单
- **模块表**：{模块名 → 页面数 → 实现状态}
- **优先级**：{P0/P1/P2}
```

### 4.4 README.md 总览结构

```markdown
# Leno 电商平台 UI 设计提示词

## 1. 项目背景
{一段话介绍 Leno 项目与本文档集的目的}

## 2. 设计决策
- 视觉风格：Ant Design Vue 4.x + Vant 4.x
- 技术栈：Vue 3.5 + TypeScript + Vite 6 + Pinia + Vue Router 4
- 主题：Ant Design 默认蓝色主题 #1677FF
- 语言：中文为主，预留 i18n
- 主题模式：亮色为主，预留暗色切换

## 3. 文档结构
{目录树说明}

## 4. 使用方式
1. 阅读共享设计系统规范（shared/design-system.md）
2. 阅读目标端的 00-overview.md 了解端总览
3. 按模块阅读页面提示词
4. 将单个提示词复制到 Design with TRAE 生成页面

## 5. 4 端模块统计
{统计表}

## 6. 与后端 API 的对应关系
{说明提示词中标注的 API 端点来自 docs/spec/ 与已实现代码}
```

### 4.5 写作规范

- **语言**：中文为主，技术术语保留英文（如 Component、Token、API）
- **代码块**：所有代码示例使用 `vue` / `typescript` / `json` 语言标签
- **长度**：每个页面提示词 800-1500 字（足够 Design with TRAE 理解，不冗余）
- **API 端点格式**：统一 `METHOD /api/path`，标注鉴权角色
- **实现状态标注**：✅ 已实现 / 🚧 规划中 / ➕ 补充功能（在每个提示词的"页面定位"段标注）
- **共享组件引用**：跨页面共享的组件在 `shared/components.md` 中定义，页面提示词中引用

## 5 Subagent 任务编排与输入输出契约

### 5.1 整体编排流程

```
阶段 0：主代理准备共享基线（同步执行）
├─ 产出 shared/design-system.md（第 2 节内容）
├─ 产出 shared/prompt-template.md（4.2 节模板）
├─ 产出 shared/writing-guide.md（文案/i18n/微交互风格）
├─ 产出 shared/glossary.md（术语表，源自 docs/spec/00）
└─ 产出 shared/components.md（跨端共享业务组件清单）
        │
        ▼
阶段 1：4 端并行 subagent（同一消息内 4 个 Task 调用）
├─ Subagent A: system-admin   (28 页面提示词 + 1 总览 = 29 文件)
├─ Subagent B: operations      (34 页面提示词 + 1 总览 = 35 文件)
├─ Subagent C: seller          (23 页面提示词 + 1 总览 = 24 文件)
└─ Subagent D: buyer-app       (48 页面提示词 + 1 总览 = 49 文件)
        │
        ▼
阶段 2：主代理一致性校验与收尾（同步执行）
├─ 检查 4 端产出格式一致性
├─ 检查术语统一性（glossary.md 对齐）
├─ 检查 API 端点引用准确性
├─ 生成 README.md 总览
├─ 提交 git
└─ 推送远程
```

### 5.2 主代理阶段 0：共享基线产出

主代理在并行启动 subagent 之前，**同步**产出 5 份共享文档，作为 4 个 subagent 的输入：

| 文件 | 内容 | 字数 |
|-|-|-|
| `shared/design-system.md` | 第 2 节完整内容（技术栈、设计令牌、布局、i18n、组件约定、工程化） | ~1500 字 |
| `shared/prompt-template.md` | 4.2 节 8 段模板 + 4.3 节端总览模板 + 4.5 节写作规范 | ~1200 字 |
| `shared/writing-guide.md` | 文案风格、微交互、空状态、加载态、危险操作、按钮文案统一约定 | ~800 字 |
| `shared/glossary.md` | 从 `docs/spec/00-需求文档总览与DDD架构.md` 3.4 节提取的统一语言术语表 + BC 缩写 | ~600 字 |
| `shared/components.md` | 跨端共享业务组件清单（如 StatusTag、IdempotencyButton、PermissionGuard） | ~500 字 |

### 5.3 Subagent 输入契约（统一格式）

4 个 subagent 使用相同的输入契约，仅替换端特定的参数。每个 subagent 的 Task 描述包含以下结构：

```markdown
# 任务：生成 {端名称} UI 设计提示词

## 你的角色
你是 Leno 电商平台的 UI 设计提示词工程师，负责为 {端名称} 生成 Design with TRAE 用的页面设计提示词。

## 工作目录
e:\Leno

## 输入资源（必读）
1. 共享设计系统规范：{主代理阶段 0 产出的 5 份文档路径}
2. 端功能清单：{该端的功能模块清单，含已实现/规划中/补充标注}
3. 后端 API 端点：{该端对应的 API 端点清单，含方法/路径/鉴权}
4. 后端需求文档：{docs/spec/ 中相关的 BC 文档路径列表}
5. 提示词写作模板：shared/prompt-template.md
6. 写作风格指南：shared/writing-guide.md
7. 术语表：shared/glossary.md
8. 共享组件清单：shared/components.md

## 端定位
- 目标用户：{角色}
- 设备形态：{桌面 1440+ / 移动 375+}
- 网关鉴权：{JWT + 双因子 + IP 白名单等}
- 整体气质：{严肃专业 / 简洁现代 / 活泼亲和}

## 输出目录
docs/design-prompts/{端目录}/

## 输出要求
1. **必产出** `00-overview.md`（端总览，使用 4.3 节模板）
2. **必产出** 所有模块目录下的页面提示词文件（按 4.1 节目录结构）
3. 每个页面提示词严格遵循 `shared/prompt-template.md` 的 8 段结构
4. 每个提示词文件 800-1500 字
5. 所有 API 端点引用必须真实存在（来自阶段 0 提供的端点清单），不得编造
6. 实现状态标注：✅ 已实现 / 🚧 规划中 / ➕ 补充功能
7. 文案统一中文，技术术语保留英文
8. 代码示例使用 vue/typescript/json 语言标签

## 模块与页面清单
{该端完整的模块与页面清单，从 4.1 节提取}

## API 端点清单（按模块分组）
{该端对应的所有 API 端点，从阶段 0 扫描结果提取}

## 相关需求文档路径
{docs/spec/ 下相关 BC 文档路径列表}

## 写作顺序
1. 先写 00-overview.md
2. 按模块顺序逐个写页面提示词
3. 每写完一个模块自检：API 端点引用是否准确、术语是否统一、格式是否符合模板

## 完成标志
- 所有文件已写入指定目录
- 每个文件符合 8 段结构
- 无 TODO/TBD/占位符
- 返回简短摘要：产出文件数、按模块统计、关键决策点
```

### 5.4 各 Subagent 的端特定参数

#### 5.4.1 Subagent A: 系统管理后台

- **目标用户**：系统管理员（Admin）
- **设备形态**：桌面 1440+，不支持移动端
- **网关鉴权**：JWT + 双因子 + IP 白名单 + 全操作审计
- **整体气质**：严肃专业，低频重操作
- **模块清单**（7 个模块 / 28 个页面 + 1 总览 = 29 文件）：
  - 01-dashboard（7 页面）：运营总览、支付统计、积分统计、通知送达率、售后统计、店铺排行、报表快照
  - 02-user-access（4 页面）：用户管理、角色管理、OAuth 客户端、运营人员
  - 03-system-governance（4 页面）：功能开关、系统配置、数据字典、公告管理
  - 04-runtime-ops（6 页面）：限流规则、索引重建、死信队列、定时任务、健康监控、告警闭环（🚧）
  - 05-audit（3 页面）：审计日志、对账管理、Outbox 监控（🚧）
  - 06-account（3 页面）：登录与双因子、个人中心、通知中心
  - 07-monitoring（1 页面）：Prometheus 监控大盘（➕）
- **API 端点来源**：
  - SystemAdmin BC：13 控制器 74 端点（已扫描，完整清单见阶段 0 输出）
  - UserAuth BC：AdminUsersController、AdminRolesController、AdminOAuthClientsController 端点
  - 共享端点：dashboard/health 等与运营后台共享
- **相关需求文档**：
  - `docs/spec/12-系统管理域.md`（F-SYS-001~011）
  - `docs/spec/01-用户与认证授权域.md`（用户/角色/权限部分）
  - `docs/spec/10-模块化部署架构.md`（系统管理端部署模块）

#### 5.4.2 Subagent B: 运营管理后台

- **目标用户**：运营管理员（Operator）
- **设备形态**：桌面 1440+，不支持移动端
- **网关鉴权**：JWT + 操作二次确认 + IP 白名单 + 操作审计
- **整体气质**：简洁现代，低频重操作 + 数据看板
- **模块清单**（10 个模块 / 34 个页面 + 1 总览 = 35 文件）：
  - 01-dashboard（6 页面）：运营总览、支付统计、积分统计、通知送达率、售后统计、店铺排行
  - 02-product-ops（3 页面）：商品审核、品牌管理、分类管理
  - 03-promotion-ops（3 页面）：促销活动、优惠券管理、秒杀活动
  - 04-seller-ops（3 页面）：入驻审核、店铺治理、卖家统计看板（🚧）
  - 05-order-ops（4 页面）：订单管理、售后处理、评价审核、物流公司管理
  - 06-payment-ops（3 页面）：支付记录、退款记录、支付渠道配置
  - 07-notification-ops（4 页面）：通知模板、通知记录、通知配置、通知限流
  - 08-membership-ops（3 页面）：会员等级、会员套餐、积分规则（🚧）
  - 09-account（4 页面）：登录、个人中心、待办工作台（➕）、通知中心
  - 10-data-export（1 页面）：导出中心（➕）
- **API 端点来源**：
  - 跨 BC 的 `/api/admin/*` 端点（Product/Promotion/Order/Payment/Notification/SellerShop/SystemAdmin BC）
  - UserAuth BC：AdminUsersController（运营管理员账号）
  - 共享端点：dashboard 与系统管理后台共享
- **相关需求文档**：
  - `docs/spec/02-商品域.md`（审核部分）
  - `docs/spec/05-促销域.md`
  - `docs/spec/06-评价与售后域.md`（审核部分）
  - `docs/spec/07-积分与会员域.md`（运营管理部分）
  - `docs/spec/08-支付集成域.md`（运营管理部分）
  - `docs/spec/09-消息通知集成.md`（运营管理部分）
  - `docs/spec/11-卖家与店铺管理域.md`（运营审核部分）

#### 5.4.3 Subagent C: 商家管理后台

- **目标用户**：卖家（Seller）
- **设备形态**：桌面 1440+，不支持移动端
- **网关鉴权**：JWT + 卖家角色校验 + 店铺级限流
- **整体气质**：简洁现代，中低频写
- **模块清单**（9 个模块 / 23 个页面 + 1 总览 = 24 文件）：
  - 01-onboarding（4 页面）：入驻申请、店铺资料、资质管理、店铺前台预览（🚧）
  - 02-dashboard（3 页面）：工作台概览、销售趋势、库存预警（➕）
  - 03-product-management（4 页面）：商品列表、商品编辑、SKU 管理、价格历史
  - 04-logistics（2 页面）：运费模板、物流公司查询
  - 05-order-fulfillment（3 页面）：待发货订单、全部订单、物流轨迹
  - 06-after-sales（2 页面）：售后列表、售后详情
  - 07-review（1 页面）：评价回复
  - 08-account（3 页面）：登录、个人中心、通知中心
  - 09-export（1 页面）：销售报表导出（➕）
- **API 端点来源**：
  - SellerShop BC：ShopsController、SellerDashboardController、AdminShopsController（卖家可访问部分）
  - Product BC：ProductsController（Seller 角色端点）
  - Order BC：OrdersController（Seller 端点）、FreightTemplatesController
  - ReviewAfterSales BC：ReviewsController（reply）、AfterSalesController（seller 端点）
  - UserAuth BC：AuthController、UsersController（个人中心）
- **相关需求文档**：
  - `docs/spec/11-卖家与店铺管理域.md`（F-SHP-001~007）
  - `docs/spec/02-商品域.md`（卖家发布部分）
  - `docs/spec/04-订单与交易域.md`（卖家履约部分）
  - `docs/spec/06-评价与售后域.md`（卖家处理部分）

#### 5.4.4 Subagent D: 用户 APP

- **目标用户**：买家（Buyer）
- **设备形态**：移动 375+，PWA，底部 TabBar 导航
- **网关鉴权**：JWT + 买家角色校验 + 滑动窗口限流
- **整体气质**：简洁现代，高并发读 + 秒杀峰值
- **模块清单**（14 个模块 / 48 个页面 + 1 总览 = 49 文件）：
  - 01-auth（5 页面）：登录、注册、忘记密码、OAuth 登录、双因子
  - 02-home（3 页面）：首页推荐流、轮播 Banner、秒杀入口
  - 03-catalog（4 页面）：分类导航、搜索、搜索结果、商品详情
  - 04-shop（1 页面）：店铺详情
  - 05-cart（3 页面）：匿名购物车、登录购物车、结算预览
  - 06-order（5 页面）：创建订单、我的订单、订单详情、物流轨迹、秒杀下单
  - 07-payment（2 页面）：发起支付、支付结果
  - 08-promotion（2 页面）：可领优惠券、我的优惠券
  - 09-review（3 页面）：提交评价、我的评价、商品评价列表
  - 10-after-sales（3 页面）：申请售后、我的售后、售后详情
  - 11-points-membership（7 页面）：积分账户、签到、积分流水、任务中心、积分兑换、会员等级、会员套餐
  - 12-notification（2 页面）：通知列表、通知偏好
  - 13-profile（6 页面）：个人资料、收货地址、账号安全、商品收藏（➕）、浏览历史（➕）、设置
  - 14-public（2 页面）：公告、数据字典
- **API 端点来源**：
  - UserAuth BC：AuthController、AccountController、AddressesController、UsersController
  - Product BC：ProductsController（Buyer 可见）、SearchController、BrandsController、CategoriesController
  - Cart BC：CartsController、AnonymousCartsController
  - Order BC：OrdersController（Buyer 端点）、PaymentsController（Order BC 内）
  - Payment BC：PaymentsController、NotifyController（回调）
  - Promotion BC：CouponsController、SeckillController（Buyer 端点）
  - ReviewAfterSales BC：ReviewsController、AfterSalesController（Buyer 端点）
  - PointsMembership BC（旧，双轨期）：PointsController、TasksController、MembersController、MembershipPackagesController
  - Notification BC：NotificationsController、NotificationPreferencesController
  - SellerShop BC：ShopsController（公开端点）
  - SystemAdmin BC：AnnouncementsController（公开端点）、DataDictionariesController（公开端点）
- **相关需求文档**：
  - `docs/spec/01-用户与认证授权域.md`
  - `docs/spec/02-商品域.md`（浏览部分）
  - `docs/spec/03-购物车域.md`
  - `docs/spec/04-订单与交易域.md`（买家下单部分）
  - `docs/spec/05-促销域.md`（买家领取/使用部分）
  - `docs/spec/06-评价与售后域.md`（买家评价/售后部分）
  - `docs/spec/07-积分与会员域.md`
  - `docs/spec/08-支付集成域.md`（买家支付部分）
  - `docs/spec/09-消息通知集成.md`（买家通知部分）

### 5.5 Subagent 输出契约

每个 subagent 完成后必须返回以下格式的摘要：

```markdown
## {端名称} 提示词生成完成

### 产出统计
- 总文件数：{N}
- 端总览：1 个（00-overview.md）
- 模块数：{M}
- 页面提示词数：{P}
- 总字数：约 {X} 字

### 模块清单
| 模块 | 文件数 | 实现状态分布 |
|-|-|-|
| 01-xxx | N | ✅x / 🚧y / ➕z |
| ... | ... | ... |

### 关键决策点
1. {决策 1：如"工作台首页采用卡片式布局而非列表"}
2. {决策 2：如"商品列表虚拟滚动阈值设为 200 行"}
3. {决策 3}

### 引用的 API 端点数
- 已实现端点：{N}
- 规划中端点：{N}
- 补充功能（无 API）：{N}

### 与共享设计系统的偏离点
- {偏离 1：如"用户 APP 搜索页使用 Vant Search 而非 Ant Design Vue Input.Search"}
- {偏离 2}
- 若无偏离，标注"无偏离，完全遵循共享设计系统"

### 待主代理校验项
- [ ] {校验项 1：如"4 端 dashboard 模块是否共享同一组端点引用"}
- [ ] {校验项 2}
```

### 5.6 主代理阶段 2：一致性校验清单

4 个 subagent 并行完成后，主代理执行以下校验：

| 校验项 | 方法 | 处理 |
|-|-|-|
| 文件完整性 | 检查每个端目录下文件数是否与清单一致 | 缺失文件由主代理补写 |
| 8 段结构合规 | 抽检每个端 3 个文件，确认 8 段标题齐全 | 不合规文件退回 subagent 重写 |
| 术语统一性 | grep 检查关键术语（如"店铺"vs"商铺"、"卖家"vs"商户"） | 不一致处主代理统一修正 |
| API 端点准确性 | 抽检 10 个 API 端点引用，对照阶段 0 扫描结果 | 错误引用主代理修正 |
| 设计令牌一致 | grep 检查 `#1677FF`、`4px`、`6px` 等令牌数值 | 不一致处主代理修正 |
| 实现状态标注 | grep 检查 ✅🚧➕ 标注是否在每个文件"页面定位"段出现 | 缺失处主代理补标 |
| 跨端共享组件引用 | 检查 shared/components.md 中定义的组件是否被正确引用 | 错误引用主代理修正 |
| README.md 总览 | 主代理生成 docs/design-prompts/README.md | — |
| Git 提交 | 主代理执行 git add + commit + push | — |

### 5.7 并行执行的技术细节

- **并行调用**：在**同一条消息**内发起 4 个 Task 工具调用，4 个 subagent 同时执行
- **subagent 类型**：`general_purpose_task`（需要写文件）
- **subagent 隔离**：每个 subagent 独立上下文，不感知其他 subagent 存在
- **依赖关系**：4 个 subagent 都依赖阶段 0 的共享文档，阶段 0 必须在并行调用前完成
- **失败处理**：若某个 subagent 失败，主代理重新调用该 subagent，不影响其他端
- **资源限制**：每个 subagent 最多写 50 个文件（用户 APP 49 个最接近上限，必要时拆分为两个 subagent）

## 6 验收与一致性保障

### 6.1 验收标准（Acceptance Criteria）

采用 Given/When/Then 格式，覆盖功能、格式、一致性三个维度。

#### AC-001 共享设计系统规范完整产出

- **Given** 主代理阶段 0 完成
- **When** 检查 `docs/design-prompts/shared/` 目录
- **Then** 存在 5 个文件：`design-system.md`、`prompt-template.md`、`writing-guide.md`、`glossary.md`、`components.md`，每个文件无 TODO/TBD 占位符

#### AC-002 4 端目录结构完整

- **Given** 4 个 subagent 并行执行完成
- **When** 检查 `docs/design-prompts/` 目录
- **Then** 存在 4 个端目录（`system-admin`、`operations`、`seller`、`buyer-app`），每个目录下有 `00-overview.md` + 按模块组织的子目录，文件总数 = 5（shared）+ 1（README）+ 29（system-admin，含 1 总览 + 28 页面）+ 35（operations，含 1 总览 + 34 页面）+ 24（seller，含 1 总览 + 23 页面）+ 49（buyer-app，含 1 总览 + 48 页面）= **143 个文件**

#### AC-003 每个页面提示词符合 8 段模板

- **Given** 任意一个页面提示词文件
- **When** 解析文件结构
- **Then** 包含 8 个二级标题：`页面定位`、`页面布局与信息架构`、`数据模型与 API 对接`、`交互流程`、`组件清单`、`视觉规范`、`异常处理与边界`、`验收要点`

#### AC-004 端总览符合 6 段结构

- **Given** 任意端的 `00-overview.md`
- **When** 解析文件结构
- **Then** 包含 6 个二级标题：`端定位与角色画像`、`信息架构与导航`、`页面路由规划`、`全局布局`、`设计风格基调`、`模块清单`

#### AC-005 API 端点引用准确

- **Given** 任意页面提示词中的 API 表格
- **When** 抽取所有 `METHOD /api/path` 引用
- **Then** 100% 的端点能在阶段 0 扫描结果或 `docs/contracts/internal-api-contracts.md` 中找到对应记录；规划中功能标注 🚧 且对应需求文档章节存在

#### AC-006 实现状态标注齐全

- **Given** 任意页面提示词的「页面定位」段
- **When** 检查「实现状态」字段
- **Then** 存在 `✅ 已实现` / `🚧 规划中` / `➕ 补充功能` 三种标注之一，且与功能清单（第 3 节）划分一致

#### AC-007 设计令牌数值统一

- **Given** 任意页面提示词
- **When** grep 检查关键令牌数值
- **Then** 主色统一为 `#1677FF`、圆角统一为 `6px`/`8px`、间距统一为 4/8/12/16/24/32/48 体系、字体统一为 PingFang SC 优先

#### AC-008 术语统一

- **Given** 4 端全部产出
- **When** grep 检查关键术语
- **Then** 统一使用以下术语（不允许同义词混用）：
  - 「店铺」而非「商铺」
  - 「卖家」而非「商户」
  - 「买家」而非「用户」（指代消费者角色时）
  - 「运营管理员」而非「运营人员」（首次出现）或「运营」（后续简称）
  - 「系统管理员」而非「管理员」（首次出现）或「Admin」（代码语境）
  - 「商品」而非「产品」
  - 「优惠券」而非「coupon」（中文语境）
  - 「秒杀」而非「闪购」

#### AC-009 跨端共享端点视觉一致

- **Given** dashboard 模块在 system-admin 和 operations 两端都存在
- **When** 对比两端的 dashboard 提示词
- **Then** 使用相同的图表类型、相同的色彩应用、相同的 Statistic 组件；仅权限说明与入口路径不同

#### AC-010 危险操作二次确认覆盖

- **Given** 任意端包含「删除」「暂停」「关闭」「驳回」「强制取消」「丢弃」「重投」等危险操作的提示词
- **When** 检查「异常处理与边界」段
- **Then** 必须包含 `Modal.confirm` 二次确认的描述，且确认按钮使用 `danger` 类型

#### AC-011 移动端与桌面端组件库区分

- **Given** buyer-app 端的提示词
- **When** 检查「组件清单」段
- **Then** 使用 Vant 4.x 组件（`van-` 前缀），不出现 Ant Design Vue 组件（`a-` 前缀）；反之，三端后台使用 `a-` 前缀，不出现 `van-` 前缀

#### AC-012 Git 提交与推送

- **Given** 全部产出完成且通过一致性校验
- **When** 主代理执行 git 操作
- **Then** 所有文件已提交，commit message 采用中文 conventional commit 格式（如 `docs: 生成 4 端 UI 设计提示词`），并推送到远程仓库

### 6.2 一致性保障机制

#### 6.2.1 共享基线锁定

阶段 0 产出的 5 份共享文档是 4 个 subagent 的**唯一**设计规范来源，subagent 不得自行定义设计令牌、术语、组件命名。若 subagent 发现共享基线缺失某项，应在摘要中提出「待主代理校验项」，由主代理决定是否补充共享基线。

#### 6.2.2 写作顺序约束

每个 subagent 必须先写 `00-overview.md`，再按模块顺序写页面提示词。这样保证端级决策（导航结构、路由规划）先于页面级决策，避免页面间导航不一致。

#### 6.2.3 模块内自检

每个 subagent 完成一个模块后，对该模块所有文件执行自检：
- 8 段标题是否齐全
- API 端点引用是否真实
- 术语是否符合 glossary
- 实现状态是否标注
- 字数是否在 800-1500 区间

#### 6.2.4 主代理全局校验

阶段 2 主代理执行 5.6 节的 9 项校验清单，对不一致处**直接修正**而非退回 subagent，避免二次并行开销。修正策略：
- 术语不一致：sed 全局替换
- 令牌数值不一致：sed 全局替换
- 8 段结构缺失：主代理补写缺失段落
- API 引用错误：主代理对照扫描结果修正

#### 6.2.5 双轨期处理

由于工区存在双轨期遗留 BC（PointsMembership 旧 / ReviewAfterSales 旧 vs 新拆分的 Points/Membership/Review/AfterSales），提示词中 API 引用遵循以下规则：
- **优先引用已实现端点**：即使新 BC 已规划，只要旧 BC 仍承载端点，提示词引用旧 BC 端点
- **规划中标注**：新 BC 完全独立的端点标注 🚧，并在「数据模型与 API 对接」段注明「待新 BC 上线后切换」
- **不混用**：单个页面提示词不混用新旧 BC 端点，避免迁移期混乱

### 6.3 风险与应对

| 风险 | 概率 | 影响 | 应对 |
|-|-|-|-|
| 用户 APP 文件数（49）超过 subagent 上下文上限 | 中 | subagent 中断 | 拆分为两个 subagent（01-07 模块 / 08-14 模块） |
| 4 端 dashboard 提示词重复但措辞不一致 | 高 | 跨端视觉漂移 | 阶段 0 在 shared/components.md 中预定义 dashboard 布局规范 |
| 双轨期 API 引用混乱 | 中 | 开发者困惑 | 6.2.5 节规则 + 主代理校验 |
| 术语同义词混用 | 高 | 文档不专业 | glossary.md + 主代理 sed 全局替换 |
| subagent 编造不存在的 API 端点 | 中 | 提示词不可执行 | 主代理抽检 10 个端点 + 阶段 0 提供完整端点清单 |
| Git 提交冲突 | 低 | 推送失败 | 主代理在阶段 2 末尾统一提交，4 个 subagent 不执行 git 操作 |

### 6.4 成功指标

| 指标 | 目标值 | 验证方式 |
|-|-|-|
| 文件总数 | 143 个 | 文件系统统计 |
| 文件结构合规率 | 100% | 8 段标题 + 6 段总览结构检查 |
| API 引用准确率 | 100% | 抽检 40 个端点（每端 10 个） |
| 术语一致率 | 100% | grep 检查 8 个关键术语 |
| 设计令牌一致率 | 100% | grep 检查 4 个关键数值 |
| 实现状态标注率 | 100% | 每个页面提示词「页面定位」段 |
| Git 提交完成 | 是 | git log 验证 |
| 远程推送完成 | 是 | git status 验证 |

## 7 后续工作

本设计文档完成后，下一步通过 `writing-plans` skill 创建实现计划，将设计转化为可执行的任务清单。实现阶段将按以下顺序：
1. 主代理执行阶段 0（5 份共享文档）
2. 并行启动 4 个 subagent 执行阶段 1
3. 主代理执行阶段 2（校验 + README + git）

## 8 附录

### 8.1 已扫描 BC 与端点统计

| BC 名称 | 控制器数 | 端点数 | 实现完整度 |
|-|-|-|-|
| UserAuth | 8 | 41 | 已实现 |
| Identity | 1 | 3 | 已实现（仅 AuthN 端点） |
| AccessControl | 0（1 gRPC） | 0 HTTP（2 gRPC） | 部分实现（仅 gRPC，无 REST） |
| Product | 6 | 32 | 已实现 |
| Cart | 2 | 15 | 已实现 |
| Order | 5 | 25 | 已实现 |
| Promotion | 4 | 28 | 已实现 |
| Review（新） | 0 | 0 | 仅 Program.cs 占位，无业务端点 |
| AfterSales（新） | 0 | 0 | 仅 Program.cs 占位，无业务端点 |
| Payment | 5 | 15 | 已实现 |
| Points（新） | 0 | 0 | 仅 Program.cs 占位，无业务端点 |
| Membership（新） | 2 | 9 | 已实现 |
| Inventory | 1 | 1 | 部分实现（仅 internal 查询） |
| Notification | 9 | 30 | 已实现 |
| SellerShop | 3 | 17 | 已实现 |
| SystemAdmin | 13 | 74 | 已实现 |
| ApiGateway BFF | 5 + 1 Minimal | 6 | 已实现 |
| **PointsMembership（旧，双轨遗留）** | 5 | 27 | 已实现（双轨期遗留） |
| **ReviewAfterSales（旧，双轨遗留）** | 2 | 22 | 已实现（双轨期遗留） |
| **合计** | **71 + 1 gRPC + 1 Minimal** | **346** | — |

### 8.2 术语表（源自 docs/spec/00 第 3.4 节）

| 术语 | 英文 | 定义 |
|-|-|-|
| 标准化产品单元 | SPU | 一类商品的标准化抽象，如"某品牌某型号手机" |
| 库存量单位 | SKU | SPU 下可售卖的最小规格单元，如"黑色 256G 版" |
| 聚合根 | Aggregate Root | 聚合的对外入口，唯一持有外部引用权 |
| 领域事件 | Domain Event | 上下文内部已发生的重要业务事实 |
| 集成事件 | Integration Event | 跨上下文传递的事件，经事件总线发布 |
| 预占库存 | Pre-occupied Stock | 下单时锁定但未真实扣减的库存 |
| 真实库存 | Physical Stock | 实际可售库存，支付成功后扣减 |
| 积分 | Points | 平台内可赚可花的虚拟权益，100 积分 = 1 元 |
| 成长值 | Growth Value | 仅用于会员等级评定的不可消耗指标 |
| 会员等级 | Member Level | 基于近 12 个月成长值的免费等级 V0–V4 |
| 付费会员 | Paid Member | 年费制高级身份，与免费等级并行、权益叠加 |
| 支付单 | Payment Order | 支付集成域对接渠道的独立单据，与订单一对多或一对一 |
| 店铺 | Shop | 卖家在平台上的经营主体，由入驻审核通过后创建 |
| 卖家账号 | Seller Account | 用户域中具备卖家角色的用户身份，以 UserId 标识 |
| 入驻申请 | Seller Application | 卖家提交的经营资质申请单，审核通过后生成店铺 |
| 店铺标识 | ShopId | 店铺唯一标识，商品域与订单域以此引用店铺归属 |
| 资质 | Qualification | 卖家经营所需的证照材料，如营业执照、特许经营证 |
| 看板报表 | DashboardReport | 按周期与维度聚合的运营指标只读快照 |
| 死信消息 | DeadLetterMessage | 各域事件总线消费失败进入死信队列的消息 |
| 索引重建任务 | IndexRebuildTask | 触发并跟踪某域 ES 读库全量重建的任务 |
| 审计日志条目 | AuditLogEntry | 管理员关键操作的不可篡改记录 |
| 限流规则 | RateLimitRule | 针对某 API 的限流阈值与算法配置 |
