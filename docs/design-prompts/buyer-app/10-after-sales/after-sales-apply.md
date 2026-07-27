# 申请售后 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：10-after-sales 售后
- **页面类型**：表单页（流程页）
- **目标用户**：买家（Buyer）
- **核心目标**：买家对已支付/已发货/已完成订单的订单行发起售后申请（仅退款/退货退款），上传凭证并提交，进入卖家审核流程。
- **访问入口**：订单详情订单行「申请售后」按钮；我的订单订单行快捷入口。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回 + 标题「申请售后」）+ 可滚动主体（商品卡 + 售后类型 + 原因 + 金额 + 凭证 + 联系方式）+ 底部固定提交按钮，无 Tabbar。
- **关键区域**：
  - 区域 A（商品卡）：`van-card` 展示商品图 + 标题 + 规格 + 单价 + 数量，确认售后对象。
  - 区域 B（售后类型）：`van-radio-group` 单选「仅退款 / 退货退款」；仅退款支持收到货前；退货退款需填写物流单号（提交后）。
  - 区域 C（原因选择）：`van-picker` 弹出选择原因（商品损坏/与描述不符/少件漏发/不喜欢/其他），原因必填。
  - 区域 D（退款金额）：`van-field` 数字键盘输入金额，默认订单行实付金额，最大不超过订单行金额；显示「最多可退 ¥X.XX」。
  - 区域 E（问题描述）：`van-field` textarea，10-300 字，placeholder「请详细描述遇到的问题」。
  - 区域 F（凭证上传）：`van-uploader` 多图上传，最多 5 张，每张 < 5MB，仅支持 JPG/PNG/WebP。
  - 区域 G（联系方式）：`van-field` 手机号，默认填充用户绑定手机号，可修改。
  - 区域 H（底部提交栏）：「提交申请」主色按钮。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、商品卡、售后类型选择、原因选择入口、底部提交按钮。
- **线框图描述**：
```
┌──────────────────┐
│ ←   申请售后      │
├──────────────────┤
│ [图] 商品标题     │
│      规格 红色 L  │
│      ¥99 × 1     │
├──────────────────┤
│ 售后类型          │
│ ◉ 仅退款          │
│ ○ 退货退款        │
├──────────────────┤
│ 原因  请选择 >    │
│ 金额  ¥99.00     │
│ 最多可退 ¥99.00  │
├──────────────────┤
│ ┌──────────────┐ │
│ │请详细描述问题 │ │
│ │              │ │
│ └──────────────┘ │
│ 0/300            │
├──────────────────┤
│ 凭证 [图][+]     │
│ 最多 5 张         │
├──────────────────┤
│ 联系电话 138****  │
├──────────────────┤
│     提交申请      │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **服务归属**：AfterSales 域（售后端点 `/api/after-sales/*`；旧域 ReviewAfterSales 双轨兜底，端点路径不变）；订单查询 `/api/orders/{orderId}` 跨域属 Order BC
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/orders/{orderId}` | 查询订单详情（含订单行） | Buyer |
| POST | `/api/after-sales` | 提交售后申请 | Buyer |
| POST | `/api/after-sales/images` | 上传售后凭证图片 | Buyer |

- **请求参数**：`POST /api/after-sales` body `{ orderId, orderLineId, type, reason, amount, description, images, contactPhone }`；type 枚举 `RefundOnly/ReturnAndRefund`；amount 必填且 ≤ 订单行金额；images URL 数组最多 5 张。
- **响应字段**：`{ afterSalesId, status, createdAt }`；status 初始为 `PendingSellerReview` 待卖家审核。
- **数据加载策略**：进入页面带 `orderId` 与 `orderLineId` 调 `GET /api/orders/{orderId}` 渲染商品卡；图片上传后返回 URL 填入表单。
- **缓存策略**：不缓存；草稿暂存 localStorage（防止误退出丢失内容）。

## 4. 交互流程
- **主流程**：
  1. 进入页面带 `orderId` 与 `orderLineId` → `GET /api/orders/{orderId}` → 渲染商品卡 + 默认退款金额。
  2. 选择售后类型 → `van-radio-group` change → 「退货退款」时显示退货说明提示。
  3. 点击原因 → `van-picker` 弹出选择 → 选中后回填。
  4. 输入退款金额 → `van-field` input → 实时校验不超过最大可退。
  5. 输入问题描述 → `van-field` input → 实时字数统计，10 字以下提交时校验。
  6. 上传凭证 → `van-uploader` after-read → `POST /api/after-sales/images` → 返回 URL 填入 images 数组。
  7. 修改联系方式 → 默认填充，可编辑。
  8. 点击「提交申请」→ 表单校验（类型/原因/金额/描述必填）→ 按钮 disabled + loading → `POST /api/after-sales` → 成功 `showToast` 「提交成功，等待卖家审核」→ 跳售后详情页。
- **分支流程**：
  - 订单不可售后：提示「订单状态不支持售后」+ 「返回订单」CTA。
  - 已申请过售后：提示「该商品已申请售后」+ 「查看售后单」CTA。
  - 图片上传失败：`showToast` 「图片上传失败」+ 重试。
  - 金额超限：实时校验 + 提交时再次校验。
- **跨页面流转**：提交成功跳售后详情页（带 `afterSalesId`）。
- **状态机可视化**：填写中 → 提交中(loading) → 成功(跳详情) / 失败(提示)。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-card`、`van-radio-group`、`van-radio`、`van-cell`、`van-field`、`van-picker`（弹出）、`van-uploader`、`van-button`、`van-image`（lazy-load + preview）、`van-skeleton`、`van-toast`（showToast）、`van-dialog`（showDialog）。
- **业务组件**：`AfterSalesProductCard` 商品卡；`RefundTypeSelector` 售后类型选择；`ReasonPicker` 原因选择器；`RefundAmountInput` 金额输入（含最大可退提示）；`EvidenceUploader` 凭证上传；`EmptyState`（见 shared/components.md §5）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；图片上传 `photograph`；删除 `cross-o`；箭头 `arrow`。
- **空状态**：订单不存在显示全屏错误 + 「返回订单」CTA。

## 6. 视觉规范
- **主色应用**：提交申请按钮主色 `#1677FF`；选中 radio 主色；金额输入框聚焦边框主色。
- **状态色**：仅退款标签 `#1677FF`；退货退款标签 `#FAAD14`；最大可退金额 `#52C41A`；字数统计 `#8C8C8C`。
- **间距**：区域间距 12px；卡片内边距 12px；底部提交栏高 50px。
- **字体**：商品标题 14px `#000000D9`（2 行省略）；规格 12px `#8C8C8C`；单价 14px `#000000D9`；区域标题 14px medium `#000000D9`；描述文字 14px `#000000D9`；字数统计 12px `#8C8C8C`；按钮 16px semibold `#FFFFFF`。
- **图标尺寸**：返回 20px；上传图标 24px；radio 20px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟商品卡与表单布局。
- **空数据**：订单不存在显示全屏错误 + 「返回订单」CTA。
- **错误态**：接口失败 `showToast` 「提交失败」+ 重试按钮；图片上传失败单独提示。
- **权限控制**：Buyer 可见；订单归属校验由服务端完成；只能对本人订单申请售后。
- **并发与乐观锁**：提交按钮点击后立即 disabled + loading 直至响应返回；`Idempotency-Key` 头防重复提交。
- **危险操作确认**：不涉及（提交售后非危险操作，但需校验金额与原因）。

## 8. 验收要点
- [ ] 售后类型支持「仅退款/退货退款」单选。
- [ ] 原因必填，从预设列表选择。
- [ ] 退款金额不超过订单行实付金额。
- [ ] 问题描述 10-300 字，带字数统计。
- [ ] 凭证最多 5 张，每张 < 5MB，仅支持 JPG/PNG/WebP。
- [ ] 提交成功提示「等待卖家审核」并跳售后详情页。
- [ ] 防重复提交（按钮 loading + Idempotency-Key）。
- **性能要求**：首屏 < 1s；图片压缩上传；提交响应 < 1.5s。
- **可访问性**：radio `role="radio"`；输入框 `aria-label`；按钮 `aria-label`。
