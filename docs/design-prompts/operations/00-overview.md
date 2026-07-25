# 运营管理后台总览

## 1. 端定位与角色画像
- **目标用户**：运营管理员（Operator），负责平台商品审核、促销活动配置、卖家治理、订单售后处置、支付对账、通知运营与会员体系维护。典型场景包括每日审核卖家提交的商品与入驻申请、配置满减/优惠券/秒杀活动、处置售后单与违规评价、监控支付成功率与通知送达率。技能水平熟练，能理解业务状态机与权限边界。
- **核心目标**：以数据看板驱动决策，以低频重操作保障平台秩序，所有关键操作可审计可回溯。
- **使用频率**：中频，工作日每日登录，审核与配置类操作集中在上午，看板监控全天滚动。
- **设备特征**：桌面 1440px 及以上，不支持移动端；最小支持 1200px，992-1199px 侧边栏自动折叠。

## 2. 信息架构与导航
- **一级菜单**（10 项，按业务域组织）：
  1. 数据看板
  2. 商品运营
  3. 促销运营
  4. 卖家运营
  5. 订单运营
  6. 支付运营
  7. 通知运营
  8. 会员运营
  9. 个人中心
  10. 数据导出
- **二级菜单**：
  - 数据看板：运营总览、支付统计、积分统计、通知送达率、售后统计、店铺排行
  - 商品运营：商品审核、品牌管理、分类管理
  - 促销运营：促销活动、优惠券管理、秒杀活动
  - 卖家运营：入驻审核、店铺治理、卖家统计
  - 订单运营：订单管理、售后处理、评价审核、物流公司管理
  - 支付运营：支付记录、退款记录、支付渠道配置
  - 通知运营：通知模板、通知记录、通知配置、通知限流
  - 会员运营：会员等级、会员套餐、积分规则
  - 个人中心：待办工作台、个人资料、通知中心、退出登录
  - 数据导出：导出中心
- **菜单组织原则**：按业务域聚拢，数据看板置顶，个人中心与导出收尾。
- **快捷入口**：Header 右上角提供待办工作台快捷入口（徽标显示待审核数）、通知铃铛（未读数）、用户头像下拉菜单。

## 3. 页面路由规划
- **路由表**（节选，命名格式 `{module}.{page}`）：

```typescript
const routes = [
  { path: '/login', name: 'account.login', component: () => import('@/views/account/Login.vue'), meta: { requiresAuth: false, title: '登录' } },
  { path: '/', redirect: '/dashboard/overview' },
  { path: '/dashboard/overview', name: 'dashboard.overview', meta: { requiresAuth: true, roles: ['Operator', 'Admin'], title: '运营总览' } },
  { path: '/dashboard/payment-stats', name: 'dashboard.paymentStats', meta: { requiresAuth: true, roles: ['Operator', 'Admin'] } },
  { path: '/product-ops/product-audit', name: 'productOps.audit', meta: { requiresAuth: true, roles: ['Operator', 'Admin'] } },
  { path: '/seller-ops/application-audit', name: 'sellerOps.application', meta: { requiresAuth: true, roles: ['Operator', 'Admin'] } },
  { path: '/order-ops/orders', name: 'orderOps.list', meta: { requiresAuth: true, roles: ['Operator', 'Admin'] } },
  { path: '/account/todo', name: 'account.todo', meta: { requiresAuth: true, roles: ['Operator', 'Admin'] } }
]
```

- **路由守卫**：`beforeEach` 校验登录态（JWT 未过期）、角色权限（Operator/Admin）、IP 白名单由网关层校验；动态加载菜单基于 `useUserStore.permissions`。

## 4. 全局布局
- **布局结构**：基于 Ant Design Vue `BasicLayout`，Header 64px + Sider 200px（可折叠至 80px）+ Content 24 栅格 padding 24px。Sider 深色背景 `#001529`，Menu inline 模式。
- **全局组件**：
  - Header：左侧 Logo + 折叠按钮，中部 Breadcrumb，右侧待办铃铛（`<a-badge>`）+ 通知铃铛 + 用户头像下拉（个人资料/修改密码/退出登录/切换主题预留）
  - Content 顶部：页头（`<a-page-header>`）含标题与面包屑回退
  - 全局请求层：axios 拦截器注入 `Authorization: Bearer {token}`、`Idempotency-Key`（POST/PUT/DELETE）、`X-Trace-Id`；401 跳登录，403 提示无权限，500 提示服务异常

## 5. 设计风格基调
- **整体气质**：简洁现代，低频重操作 + 数据看板。强调信息密度与操作确定性，避免花哨动效。
- **与共享设计系统的关系**：完全遵循 `shared/design-system.md`，主色 `#1677FF`，圆角按钮 6px/卡片 8px，间距取自 4/8/12/16/24/32/48 体系，字体 PingFang SC 优先。差异点：数据看板数值使用 24px semibold 突出，表格行高压缩至 48px 提升信息密度。

## 6. 模块清单
- **模块表**：

| 模块 | 页面数 | 实现状态分布 | 优先级 |
|-|-|-|-|
| 01-dashboard | 6 | ✅×6 | P0 |
| 02-product-ops | 3 | ✅×3 | P0 |
| 03-promotion-ops | 3 | ✅×3 | P0 |
| 04-seller-ops | 3 | ✅×2 / 🚧×1 | P0 |
| 05-order-ops | 4 | ✅×4 | P0 |
| 06-payment-ops | 3 | ✅×3 | P1 |
| 07-notification-ops | 4 | ✅×4 | P1 |
| 08-membership-ops | 3 | ✅×2 / 🚧×1 | P1 |
| 09-account | 4 | ✅×2 / ➕×2 | P2 |
| 10-data-export | 1 | ➕×1 | P2 |

- **优先级说明**：P0 为日常运营刚需（审核/治理/订单），P1 为配置与监控类，P2 为辅助与个人功能。合计 34 页面 + 1 总览 = 35 文件。
