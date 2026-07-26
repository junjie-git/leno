# 用户 APP 总览

## 1. 端定位与角色画像
- **目标用户**：买家（Buyer）。典型场景：碎片化浏览商品、参与秒杀、下单支付、追踪物流、申请售后、领取积分与优惠券。技能水平参差，对操作链路简洁度与响应速度高度敏感。
- **核心目标**：提供从浏览到下单到售后的全链路移动购物体验，支撑高并发读与秒杀峰值，确保交易闭环可靠。
- **使用频率**：高频。日常浏览与促销节点（秒杀、大促）均产生大量 DAU。
- **设备特征**：移动 375px 为基准设计，PWA 安装态可达 480px 居中；底部 TabBar 单手操作优先。

## 2. 信息架构与导航
- **一级菜单**（底部 Tabbar，4 个入口）：首页、分类、购物车、我的。
- **二级菜单**（按一级入口展开）：
  - 首页：[搜索框](./03-catalog/search.md)、[轮播 Banner](./02-home/banner.md)、[秒杀入口](./02-home/seckill-entry.md)、[推荐流](./02-home/home-feed.md)、[分类快捷入口](./03-catalog/category-nav.md)、[公告条](./14-public/announcements.md)。
  - 分类：[分类导航](./03-catalog/category-nav.md)（左侧一级分类树 + 右侧二级分类商品列表）。
  - 购物车：[购物车](./05-cart/cart.md)（匿名未登录/登录态，按卖家分组）、[结算预览](./05-cart/checkout-preview.md)。
  - 我的：[订单聚合入口](./06-order/order-list.md)（待支付/待发货/待收货/退款售后）、[优惠券](./08-promotion/my-coupons.md)、[积分账户](./11-points-membership/points-account.md)、[积分流水](./11-points-membership/points-ledger.md)、[签到](./11-points-membership/check-in.md)、[任务中心](./11-points-membership/tasks-center.md)、[积分兑换](./11-points-membership/points-exchange.md)、[会员等级](./11-points-membership/member-level.md)、[会员套餐](./11-points-membership/membership-packages.md)、[个人资料](./13-profile/profile.md)、[地址](./13-profile/addresses.md)、[安全设置](./13-profile/security.md)、[收藏](./13-profile/favorites.md)、[历史](./13-profile/history.md)、[设置](./13-profile/settings.md)、[通知](./12-notification/notifications.md)、[通知偏好](./12-notification/preferences.md)。
- **其他入口页面**（非 Tabbar 入口，通过路由跳转进入）：[登录](./01-auth/login.md)、[注册](./01-auth/register.md)、[忘记密码](./01-auth/forgot-password.md)、[OAuth 登录](./01-auth/oauth-login.md)、[双因素验证](./01-auth/two-factor.md)、[商品详情](./03-catalog/product-detail.md)、[搜索结果](./03-catalog/search-results.md)、[店铺详情](./04-shop/shop-detail.md)、[下单](./06-order/order-create.md)、[订单详情](./06-order/order-detail.md)、[物流跟踪](./06-order/logistics-trace.md)、[秒杀下单](./06-order/seckill-order.md)、[发起支付](./07-payment/payment-initiate.md)、[支付结果](./07-payment/payment-result.md)、[领券中心](./08-promotion/coupons-available.md)、[提交评价](./09-review/review-submit.md)、[我的评价](./09-review/my-reviews.md)、[商品评价](./09-review/product-reviews.md)、[申请售后](./10-after-sales/after-sales-apply.md)、[我的售后](./10-after-sales/my-after-sales.md)、[售后详情](./10-after-sales/after-sales-detail.md)、[公告](./14-public/announcements.md)、[字典](./14-public/dictionaries.md)。
- **菜单组织原则**：按用户购物任务路径组织（浏览→决策→交易→售后→复购），Tabbar 固定高频入口，次级功能收敛至「我的」。
- **快捷入口**：首页搜索框、秒杀倒计时入口、购物车角标未读数、我的页通知铃铛未读数。

## 3. 页面路由规划
- **路由表**：

| path | component | 鉴权要求 |
|-|-|-|
| `/login` | `auth/Login.vue` | 匿名 |
| `/register` | `auth/Register.vue` | 匿名 |
| `/forgot-password` | `auth/ForgotPassword.vue` | 匿名 |
| `/oauth/:provider` | `auth/OauthLogin.vue` | 匿名 |
| `/two-factor` | `auth/TwoFactor.vue` | 匿名 |
| `/` | `home/HomeFeed.vue` | Buyer |
| `/category` | `catalog/CategoryNav.vue` | Buyer |
| `/search` | `catalog/Search.vue` | Buyer |
| `/search/results` | `catalog/SearchResults.vue` | Buyer |
| `/product/:id` | `catalog/ProductDetail.vue` | Buyer |
| `/shop/:shopId` | `shop/ShopDetail.vue` | Buyer（🚧 规划中） |
| `/cart` | `cart/Cart.vue` | Buyer |
| `/checkout/preview` | `cart/CheckoutPreview.vue` | Buyer |
| `/order/create` | `order/OrderCreate.vue` | Buyer |
| `/orders` | `order/OrderList.vue` | Buyer |
| `/order/:id` | `order/OrderDetail.vue` | Buyer |
| `/order/:id/logistics` | `order/LogisticsTrace.vue` | Buyer |
| `/seckill/order/:activityId` | `order/SeckillOrder.vue` | Buyer |
| `/payment/initiate/:orderId` | `payment/PaymentInitiate.vue` | Buyer |
| `/payment/result/:orderId` | `payment/PaymentResult.vue` | Buyer |
| `/coupons/available` | `promotion/CouponsAvailable.vue` | Buyer |
| `/coupons/mine` | `promotion/MyCoupons.vue` | Buyer |
| `/review/submit/:orderLineId` | `review/ReviewSubmit.vue` | Buyer |
| `/reviews/mine` | `review/MyReviews.vue` | Buyer |
| `/product/:spuId/reviews` | `review/ProductReviews.vue` | 匿名 |
| `/after-sales/apply/:orderLineId` | `afterSales/AfterSalesApply.vue` | Buyer |
| `/after-sales/mine` | `afterSales/MyAfterSales.vue` | Buyer |
| `/after-sales/:id` | `afterSales/AfterSalesDetail.vue` | Buyer |
| `/points/account` | `pointsMembership/PointsAccount.vue` | Buyer |
| `/points/check-in` | `pointsMembership/CheckIn.vue` | Buyer |
| `/points/ledger` | `pointsMembership/PointsLedger.vue` | Buyer |
| `/points/tasks` | `pointsMembership/TasksCenter.vue` | Buyer |
| `/points/exchange` | `pointsMembership/PointsExchange.vue` | Buyer |
| `/member/level` | `pointsMembership/MemberLevel.vue` | Buyer |
| `/member/packages` | `pointsMembership/MembershipPackages.vue` | Buyer |
| `/notifications` | `notification/Notifications.vue` | Buyer |
| `/notifications/preferences` | `notification/Preferences.vue` | Buyer |
| `/profile` | `profile/Profile.vue` | Buyer |
| `/profile/addresses` | `profile/Addresses.vue` | Buyer |
| `/profile/security` | `profile/Security.vue` | Buyer |
| `/profile/favorites` | `profile/Favorites.vue` | Buyer（➕ 补充） |
| `/profile/history` | `profile/History.vue` | Buyer（➕ 补充） |
| `/settings` | `profile/Settings.vue` | Buyer |
| `/announcements` | `public/Announcements.vue` | Buyer |
| `/dictionaries/:code` | `public/Dictionaries.vue` | Buyer |

- **路由守卫**：`beforeEach` 校验登录态（JWT 是否存在且未过期），未登录访问受保护路由跳 `/login` 并携带 `redirect` query；登录后按 `Buyer` 角色校验，非买家角色引导至对应端。

## 4. 全局布局
- **布局结构**：`van-nav-bar`（46px，返回+标题+右侧操作） + Content（单列流式，padding 12px） + `van-tabbar`（50px，4 入口）。秒杀与支付页隐藏 Tabbar 以聚焦任务。
- **全局组件**：
  - 顶栏右侧通知铃铛（`van-badge` 显示未读数，调用 `GET /api/notifications/unread-count`）。
  - 「我的」页顶部用户卡片（头像、昵称、会员等级标签）。
  - 全局 `showToast` / `showNotify` 反馈容器，挂载于 App 根。
  - 主题切换入口预留于「设置」页，通过 Vant `ConfigProvider` 的 `theme-vars` 注入。
  - 请求拦截器统一注入 `Authorization: Bearer {token}`、`Idempotency-Key`（POST/PUT/DELETE）、`X-Trace-Id`；响应拦截器处理 401（跳登录）、403（提示无权限）、限流 429（提示稍后重试）。

## 5. 设计风格基调
- **整体气质**：简洁现代。强调内容优先、留白克制、操作明确，秒杀场景采用强对比色与倒计时制造紧迫感。
- **与共享设计系统的关系**：完全遵循 shared/design-system.md。差异点仅在于：用户 APP 使用 Vant 4.x（`van-` 前缀）替代桌面端组件库；不使用图表组件；首页大标题字号 30px（`font/size/3xl`）仅本端使用；底部 Tabbar 适配 `safe-area-inset-bottom`。

## 6. 模块清单
- **模块表**：

| 模块 | 页面数 | 实现状态分布 | 优先级 |
|-|-|-|-|
| 01-auth 认证 | 5 | ✅×5 | P0 |
| 02-home 首页 | 3 | ✅×2 / ➕×1 | P0 |
| 03-catalog 商品目录 | 4 | ✅×4 | P0 |
| 04-shop 店铺 | 1 | 🚧×1 | P2 |
| 05-cart 购物车 | 3 | ✅×3 | P0 |
| 06-order 订单交易 | 5 | ✅×5 | P0 |
| 07-payment 支付 | 2 | ✅×2 | P0 |
| 08-promotion 优惠 | 2 | ✅×2 | P1 |
| 09-review 评价 | 3 | ✅×3 | P1 |
| 10-after-sales 售后 | 3 | ✅×3 | P1 |
| 11-points-membership 积分会员 | 7 | ✅×6 / 🚧×1 | P1 |
| 12-notification 通知 | 2 | ✅×2 | P1 |
| 13-profile 我的 | 6 | ✅×4 / ➕×2 | P1 |
| 14-public 公共 | 2 | ✅×2 | P2 |

- **优先级说明**：P0 为交易闭环必备（认证/浏览/购物车/订单/支付）；P1 为体验增强（优惠/评价/售后/积分会员/通知/个人中心）；P2 为补充（店铺详情/公告字典）。双轨期积分会员端点优先引用旧 PointsMembership BC，新拆分 BC 端点标注 🚧 待切换。

## 7. 与后端 API 的对应关系

API 来源：UserAuth BC（注册、登录、OAuth、忘记密码、双因素、地址、个人资料）+ Product BC 买家端（分类导航、搜索、商品详情、店铺详情）+ Cart BC（购物车、结算预览、结算）+ Order BC 买家端（下单、订单列表、订单详情、物流跟踪、秒杀订单）+ Payment BC（发起支付、支付结果）+ Promotion BC 买家端（领券、我的优惠券）+ ReviewAfterSales BC 买家端（评价提交、我的评价、商品评价、售后申请、我的售后、售后详情）+ PointsMembership BC（积分账户、积分流水、签到、任务中心、积分兑换、会员等级、付费会员套餐）+ Notification BC（通知列表、通知偏好）+ SystemAdmin BC（字典、公告）。详细端点见各页面提示词「数据与 API」段。
