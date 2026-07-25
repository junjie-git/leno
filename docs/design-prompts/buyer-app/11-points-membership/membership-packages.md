# 会员套餐 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：11-points-membership 积分会员
- **页面类型**：列表页
- **目标用户**：买家（Buyer）
- **核心目标**：买家浏览可购买的付费会员套餐，对比权益与价格，订阅后享受专属权益（折扣/积分倍数/免邮/专属客服等）。
- **访问入口**：「我的」页会员套餐入口；会员等级页「升级付费会员」；首页付费会员推广。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回 + 标题「会员套餐」）+ 当前会员状态条 + `van-list` 套餐卡片列表 + 底部权益说明，无 Tabbar。
- **关键区域**：
  - 区域 A（当前会员状态条）：展示当前付费会员状态（如「VIP 年卡会员，有效期至 2027-07-26」）或「未开通付费会员」。
  - 区域 B（套餐卡片）：每张卡片展示套餐名称（如「VIP 年卡」）+ 价格（如「¥199/年」）+ 权益清单（折扣/积分倍数/免邮/专属客服/生日礼等）+ 订阅按钮；当前套餐标记「当前套餐」。
  - 区域 C（权益说明）：`van-cell-group` 展示付费会员权益详细说明（使用规则、有效期、退订规则）。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、当前会员状态条、套餐卡片首屏。
- **线框图描述**：
```
┌──────────────────┐
│ ←   会员套餐      │
├──────────────────┤
│ 未开通付费会员    │
├──────────────────┤
│ ┌──────────────┐ │
│ │ VIP 月卡      │ │
│ │ ¥19/月        │ │
│ │ • 9.5 折      │ │
│ │ • 积分 1.5x   │ │
│ │ • 免邮 5次/月 │ │
│ │     [订阅]   │ │
│ └──────────────┘ │
├──────────────────┤
│ ┌──────────────┐ │
│ │ VIP 年卡 推荐 │ │
│ │ ¥199/年 立省¥38│ │
│ │ • 8.8 折      │ │
│ │ • 积分 2x     │ │
│ │ • 免邮不限次  │ │
│ │ • 专属客服    │ │
│ │ • 生日双倍积分│ │
│ │     [订阅]   │ │
│ └──────────────┘ │
├──────────────────┤
│ 权益说明          │
│ • 使用规则        │
│ • 有效期          │
│ • 退订规则        │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/membership-packages` | 查询可购买的会员套餐列表 | Buyer |
| POST | `/api/membership-packages/{packageId}/subscribe` | 订阅会员套餐 | Buyer |
| GET | `/api/members/me` | 查询当前会员信息（含付费会员状态） | Buyer |

- **请求参数**：套餐列表无参数；订阅路径参数 `packageId`；会员查询无参数。
- **响应字段**：套餐 `MembershipPackageDto` 含 `packageId`、`name`、`price`、`period`（Month/Year）、`originalPrice`、`benefits`（权益数组）、`isRecommended`、`isPopular`；会员 `MemberDto` 含 `paidMembershipStatus`（None/Active/Expired）、`paidMembershipExpireAt`、`paidPackageId`。
- **数据加载策略**：进入页面并行调用套餐列表与会员信息接口；下拉刷新。
- **缓存策略**：套餐列表缓存 5 分钟；会员信息缓存 30s。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → 并行调用 `GET /api/membership-packages` 与 `GET /api/members/me` → 渲染当前会员状态条 + 套餐卡片。
  2. 点击套餐卡片 → `van-popup` 展示套餐详情（权益清单、使用规则、对比当前套餐）。
  3. 点击「订阅」→ `van-dialog` 二次确认「确认订阅 {套餐名}，支付 ¥X？」→ 按钮 disabled + loading → `POST /api/membership-packages/{packageId}/subscribe` → 成功跳支付页。
  4. 支付成功 → 跳支付结果页 → 返回本页刷新会员状态。
  5. 查看权益说明 → 了解使用规则、有效期、退订规则。
- **分支流程**：
  - 已开通付费会员：当前套餐标记「当前套餐」，订阅按钮置灰；其他套餐可订阅（续费/升级）。
  - 订阅失败（套餐已下架）：`showToast` 「套餐已下架」+ 刷新列表。
  - 订阅失败（已有付费会员）：`showToast` 「已有付费会员，请先退订」。
  - 空列表：`van-empty`「暂无可购买套餐」。
- **跨页面流转**：订阅成功跳支付页（带订单号）；支付成功跳支付结果页；返回本页刷新。
- **状态机可视化**：未开通 → 订阅中(loading) → 支付中(跳支付页) → 已开通 / 订阅失败(提示)。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-list`、`van-pull-refresh`、`van-button`、`van-cell`、`van-cell-group`、`van-tag`、`van-popup`、`van-dialog`（showDialog）、`van-image`、`van-icon`、`van-empty`、`van-skeleton`、`van-toast`（showToast）。
- **业务组件**：`PaidMembershipStatusBar` 当前会员状态条；`MembershipPackageCard` 套餐卡片（含名称、价格、权益清单、订阅按钮、推荐标签）；`PackageDetailPopup` 套餐详情弹层；`BenefitExplanationCell` 权益说明；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；权益已享 `success`；推荐 `fire-o`；订阅 `gold-o`。
- **空状态**：`van-empty`「暂无可购买套餐」。

## 6. 视觉规范
- **主色应用**：套餐卡片渐变背景 `linear-gradient(135deg, #FAAD14, #D48806)`（付费会员金色风格）；价格主色 `#FF4D4F`；订阅按钮主色 `#FFFFFF` 金色文字；推荐标签 `#FF4D4F`。
- **状态色**：当前套餐 `#52C41A`；已享权益 `#52C41A`；订阅按钮 `#FFFFFF` 金色背景；未订阅按钮主色。
- **间距**：状态条内边距 12px；卡片间距 12px；卡片内边距 16px；权益项间距 8px。
- **字体**：套餐名称 18px semibold `#FFFFFF`；价格 24px semibold `#FF4D4F`；原价 12px `#8C8C8C` 划线；权益项 13px `#FFFFFF`；订阅按钮 16px semibold；推荐标签 11px `#FFFFFF`。
- **图标尺寸**：返回 20px；权益图标 16px；推荐图标 14px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟状态条 + 3 张套餐卡片。
- **空数据**：`van-empty`「暂无可购买套餐」。
- **错误态**：接口失败 `showToast` 「加载失败」+ 重试按钮；`van-pull-refresh` 下拉刷新。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/membership/packages`。
- **并发与乐观锁**：订阅按钮点击后立即 disabled + loading 直至响应返回；`Idempotency-Key` 头防重复订阅。
- **危险操作确认**：订阅需 `van-dialog` 二次确认，文案「确认订阅 {套餐名}，支付 ¥X？」，确认按钮主色（涉及支付，需确认）。

## 8. 验收要点
- [ ] 当前会员状态条展示付费会员状态与有效期。
- [ ] 套餐卡片展示名称、价格、原价、权益清单、订阅按钮。
- [ ] 推荐套餐标记「推荐」标签。
- [ ] 当前套餐标记「当前套餐」，订阅按钮置灰。
- [ ] 订阅需二次确认，确认后跳支付页。
- [ ] 订阅防重复（按钮 loading + Idempotency-Key）。
- [ ] 权益说明展示使用规则、有效期、退订规则。
- **性能要求**：首屏 < 1s；列表渲染无卡顿；订阅响应 < 1.5s。
- **可访问性**：卡片 `role="article"`；按钮 `aria-label="订阅 VIP 年卡支付 199 元"`；对话框 `role="dialog"`。
