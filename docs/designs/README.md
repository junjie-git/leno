# Leno 电商平台 · 全站前端界面设计稿

本目录收录 Leno 电商平台四端共 **133 个** 前端界面设计稿。每份设计稿均为自包含 HTML 单文件（内联全部 CSS 与 SVG 图标/图表），可直接用浏览器打开预览，不依赖任何外部资源。

## 设计规范

全站统一遵循 Ant Design 风格语言：

- 设计令牌：主色 `#1677FF`，状态色（成功 `#52C41A`、警告 `#FAAD14`、错误 `#FF4D4F`），圆角 6/8px，字号 12/14/16/20/24px，阴影 `--sh-card`
- 共享令牌定义见 `_shared/tokens.css`
- 买家端为移动端竖屏（375×812），其余三端为桌面端（Header 64px + Sider 200px 深色 `#001529` + Content 灰底 + Footer 32px）
- 图表均用 SVG 模拟 ECharts/Grafana 效果
- 全站零占位符：无 Lorem Ipsum、无 TODO/FIXME、无未实现函数
- 中文文案，技术术语保留英文（GMV、SKU、OAuth、Outbox、Prometheus 等）
- `<html lang="zh-CN">`，`<meta charset="UTF-8">`

## 目录总览

| 端 | 路径 | 模块数 | 页面数 |
| --- | --- | --- | --- |
| 买家端 APP | `buyer-app/` | 14 | 48 |
| 运营管理后台 | `operations/` | 10 | 34 |
| 商家管理后台 | `seller/` | 9 | 23 |
| 系统管理后台 | `system-admin/` | 7 | 28 |
| **合计** | | **40** | **133** |

---

## 一、买家端 APP（移动端，48 页）

```
buyer-app/
├── 01-auth/                  认证模块（5 页）
│   ├── login.html                  登录
│   ├── register.html               注册
│   ├── forgot-password.html        忘记密码
│   ├── two-factor.html             双因子验证
│   └── oauth-login.html            三方登录
├── 02-home/                  首页模块（3 页）
│   ├── home-feed.html              首页推荐流
│   ├── banner.html                 轮播 Banner 管理
│   └── seckill-entry.html          秒杀入口
├── 03-catalog/               商品目录（4 页）
│   ├── search.html                 搜索
│   ├── search-results.html         搜索结果
│   ├── category-nav.html           分类导航
│   └── product-detail.html         商品详情
├── 04-shop/                  店铺模块（1 页）
│   └── shop-detail.html            店铺详情
├── 05-cart/                  购物车（3 页）
│   ├── cart.html                   购物车
│   ├── checkout-preview.html       结算预览
│   └── checkout-settle.html        结算确认
├── 06-order/                 订单交易（5 页）
│   ├── order-create.html           创建订单
│   ├── order-list.html             订单列表
│   ├── order-detail.html           订单详情
│   ├── seckill-order.html          秒杀下单
│   └── logistics-trace.html        物流轨迹
├── 07-payment/               支付（2 页）
│   ├── payment-initiate.html       发起支付
│   └── payment-result.html         支付结果
├── 08-promotion/             优惠（2 页）
│   ├── coupons-available.html      可领优惠券
│   └── my-coupons.html             我的优惠券
├── 09-review/                评价（3 页）
│   ├── review-submit.html          提交评价
│   ├── product-reviews.html        商品评价列表
│   └── my-reviews.html             我的评价
├── 10-after-sales/           售后（3 页）
│   ├── after-sales-apply.html      申请售后
│   ├── my-after-sales.html         我的售后
│   └── after-sales-detail.html     售后详情
├── 11-points-membership/     积分会员（7 页）
│   ├── points-account.html         积分账户
│   ├── points-ledger.html          积分流水
│   ├── points-exchange.html        积分兑换
│   ├── check-in.html               每日签到
│   ├── tasks-center.html           任务中心
│   ├── member-level.html           会员等级
│   └── membership-packages.html    会员套餐
├── 12-notification/          通知（2 页）
│   ├── notifications.html          通知列表
│   └── preferences.html            通知偏好
├── 13-profile/               我的（6 页）
│   ├── profile.html                个人主页
│   ├── addresses.html             收货地址
│   ├── favorites.html             收藏
│   ├── history.html               浏览历史
│   ├── security.html              账号安全
│   └── settings.html               设置
└── 14-public/                公共（2 页）
    ├── announcements.html          公告
    └── dictionaries.html          字典数据
```

## 二、运营管理后台（桌面端，34 页）

```
operations/
├── 01-dashboard/             数据看板（6 页）
│   ├── operations-overview.html    运营总览
│   ├── payment-stats.html          支付统计
│   ├── points-stats.html           积分统计
│   ├── notification-delivery.html  通知送达率
│   ├── after-sales-stats.html      售后统计
│   └── shop-ranking.html           店铺排行
├── 02-product-ops/           商品运营（3 页）
│   ├── product-audit.html          商品审核
│   ├── category-management.html    分类管理
│   └── brand-management.html       品牌管理
├── 03-promotion-ops/         促销运营（3 页）
│   ├── promotions.html             促销活动
│   ├── coupons.html                优惠券
│   └── seckill.html                秒杀活动
├── 04-seller-ops/            卖家运营（3 页）
│   ├── application-audit.html      入驻审核
│   ├── shop-governance.html        店铺治理
│   └── seller-statistics.html      卖家统计
├── 05-order-ops/             订单运营（4 页）
│   ├── order-management.html       订单管理
│   ├── after-sales.html            售后处理
│   ├── logistics-companies.html   物流公司
│   └── review-audit.html           评价审核
├── 06-payment-ops/           支付运营（3 页）
│   ├── payment-channels.html       支付渠道
│   ├── payment-records.html        支付记录
│   └── refund-records.html         退款记录
├── 07-notification-ops/     通知运营（4 页）
│   ├── config.html                 通知配置
│   ├── templates.html              通知模板
│   ├── records.html                发送记录
│   └── rate-limits.html            限流规则
├── 08-membership-ops/        会员运营（3 页）
│   ├── points-rules.html           积分规则
│   ├── member-levels.html          会员等级
│   └── membership-packages.html     会员套餐
├── 09-account/               个人账号（4 页）
│   ├── login.html                  登录
│   ├── profile.html                个人中心
│   ├── notifications.html          通知中心
│   └── todo-workbench.html         待办工作台
└── 10-data-export/           数据导出（1 页）
    └── export-center.html          导出中心
```

## 三、商家管理后台（桌面端，23 页）

```
seller/
├── 01-onboarding/            入驻与店铺（4 页）
│   ├── application.html            入驻申请
│   ├── qualifications.html        资质管理
│   ├── shop-preview.html          店铺预览
│   └── shop-profile.html           店铺资料
├── 02-dashboard/             工作台（3 页）
│   ├── overview.html               工作台首页
│   ├── sales-trend.html            销售趋势
│   └── low-stock-alert.html         库存预警
├── 03-product-management/    商品管理（4 页）
│   ├── product-list.html           商品列表
│   ├── product-edit.html           商品编辑
│   ├── sku-management.html         SKU 管理
│   └── price-history.html          价格历史
├── 04-logistics/             物流管理（2 页）
│   ├── freight-templates.html      运费模板
│   └── logistics-companies.html    物流公司
├── 05-order-fulfillment/     订单履约（3 页）
│   ├── pending-shipment.html       待发货
│   ├── order-list.html             全部订单
│   └── logistics-trace.html         物流轨迹
├── 06-after-sales/           售后处理（2 页）
│   ├── after-sales-list.html       售后列表
│   └── after-sales-detail.html      售后详情
├── 07-review/                评价管理（1 页）
│   └── review-reply.html           评价回复
├── 08-account/               个人账号（3 页）
│   ├── login.html                  登录
│   ├── profile.html                个人中心
│   └── notifications.html          通知中心
└── 09-export/               报表导出（1 页）
    └── sales-export.html           销售报表导出
```

## 四、系统管理后台（桌面端，28 页）

```
system-admin/
├── 01-dashboard/             仪表盘（7 页）
│   ├── operations-overview.html    运营总览
│   ├── payment-stats.html          支付统计
│   ├── points-stats.html           积分统计
│   ├── notification-delivery.html  通知送达率
│   ├── after-sales-stats.html      售后统计
│   ├── shop-ranking.html           店铺排行
│   └── report-snapshots.html       报表快照
├── 02-user-access/           用户与权限（4 页）
│   ├── user-management.html        用户管理
│   ├── role-management.html        角色管理
│   ├── oauth-clients.html          OAuth 客户端
│   └── operators.html              运营人员
├── 03-system-governance/     系统治理（4 页）
│   ├── feature-flags.html          功能开关
│   ├── system-configs.html         系统配置
│   ├── data-dictionaries.html      数据字典
│   └── announcements.html          公告管理
├── 04-runtime-ops/           运行时运维（6 页）
│   ├── rate-limit-rules.html       限流规则
│   ├── index-rebuild.html          索引重建
│   ├── dead-letter-queue.html      死信队列
│   ├── scheduled-tasks.html        定时任务
│   ├── health-monitoring.html      健康监控
│   └── alert-management.html       告警管理
├── 05-audit/                 审计与对账（3 页）
│   ├── audit-logs.html             审计日志
│   ├── reconciliation.html         对账管理
│   └── outbox-monitor.html         Outbox 监控
├── 06-account/               个人账号（3 页）
│   ├── login-2fa.html              登录与双因子
│   ├── profile.html                个人中心
│   └── notifications.html          通知中心
└── 07-monitoring/            系统监控（1 页）
    └── prometheus-dashboard.html   Prometheus 监控大盘
```

## 预览方式

直接双击任一 `.html` 文件即可在浏览器中打开预览。所有样式、图标、图表均为内联实现，无需联网或安装依赖。
