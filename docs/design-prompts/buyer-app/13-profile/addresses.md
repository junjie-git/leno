# 收货地址 - 用户 APP

## 1. 页面定位
- **所属端**：用户 APP
- **所属模块**：13-profile 我的
- **页面类型**：列表页（含新增/编辑弹层）
- **目标用户**：买家（Buyer）
- **核心目标**：买家管理收货地址列表，支持新增、编辑、删除、设为默认，下单时从该列表选择收货地址。
- **访问入口**：「我的」页 → 收货地址；下单页「选择地址」；个人资料页「收货地址」；URL `/profile/addresses`。
- **实现状态**：✅ 已实现

## 2. 页面布局与信息架构
- **整体布局**：顶部 `van-nav-bar`（返回 + 标题「收货地址」）+ 可滚动地址卡片列表 + 底部固定「新增地址」按钮，无 Tabbar。
- **关键区域**：
  - 区域 A（地址卡片）：每张卡片展示收件人 + 电话（脱敏）+ 完整地址（省市区 + 详情）+ 标签（家/公司/学校）；默认地址顶部主色「默认」标签 + 卡片主色边框；底部「编辑」「删除」操作按钮。
  - 区域 B（新增/编辑弹层）：`van-popup` 自底部弹出表单，含收件人、手机号、省市区选择（`van-area` + 地区数据）、详情地址、标签选择、设为默认开关。
  - 区域 C（底部新增栏）：固定底部「新增地址」主色按钮，适配 `safe-area-inset-bottom`。
  - 区域 D（空状态）：`van-empty`「暂无地址」+ 「新增地址」CTA。
- **响应式断点**：375px 基准；≥768px 居中最大 480px。
- **首屏内容**：导航栏、地址卡片列表（默认地址置顶）、底部新增按钮。
- **线框图描述**：
```
┌──────────────────┐
│ ←   收货地址      │
├──────────────────┤
│ ┌──────────────┐ │
│ │[默认] 家      │ │
│ │ 李* 138****  │ │
│ │ 广东省深圳市  │ │
│ │ 南山区科技园  │ │
│ │ 1 号楼       │ │
│ │ [编辑] [删除]│ │
│ └──────────────┘ │
│ ┌──────────────┐ │
│ │ 公司         │ │
│ │ 王* 139****  │ │
│ │ 北京市朝阳区  │ │
│ │ 国贸中心     │ │
│ │ [编辑] [删除]│ │
│ └──────────────┘ │
├──────────────────┤
│    新增地址       │
└──────────────────┘
```

## 3. 数据模型与 API 对接
- **服务归属**：UserCenter 域（旧域 UserAuth 双轨兜底，端点路径不变）
- **主要 API**：

| 方法 | 端点 | 用途 | 鉴权 |
|-|-|-|-|
| GET | `/api/users/me/addresses` | 查询地址列表（默认优先） | Buyer |
| POST | `/api/users/me/addresses` | 新增地址 | Buyer |
| PUT | `/api/users/me/addresses/{id}` | 修改地址 | Buyer |
| DELETE | `/api/users/me/addresses/{id}` | 删除地址 | Buyer |
| POST | `/api/users/me/addresses/{id}/default` | 设为默认 | Buyer |

- **请求参数**：查询无参数（默认地址优先排序）；新增/修改 body `{ recipientName, recipientPhone, province, city, district, detail, tag?, isDefault }`；设默认无 body。
- **响应字段**：列表 `AddressDto[]`，每项含 `id`、`recipientName`、`recipientPhone`、`province`、`city`、`district`、`detail`、`tag?`（家/公司/学校）、`isDefault`、`createdAt`、`updatedAt`；新增/修改/设默认返回 `AddressDto`；删除返回 `ApiResponse`。
- **数据加载策略**：进入页面调 `GET /api/users/me/addresses` 渲染列表；下拉刷新。
- **缓存策略**：不缓存，每次进入页面重新拉取；下单页通过 Pinia `useCheckoutStore` 引用最近一次列表。

## 4. 交互流程
- **主流程**：
  1. 进入页面 → `GET /api/users/me/addresses` → 渲染地址卡片（默认地址置顶）。
  2. 点击「新增地址」→ `van-popup` 自底部弹出表单 → 填写收件人/手机号/省市区/详情/标签/默认开关 → 点击「保存」→ `POST /api/users/me/addresses` → 成功 `showToast` 「新增成功」→ 关闭弹层 + 刷新列表。
  3. 点击卡片「编辑」→ `van-popup` 弹出表单回填 → 修改字段 → 点击「保存」→ `PUT /api/users/me/addresses/{id}` → 成功 `showToast` 「保存成功」→ 关闭弹层 + 刷新列表。
  4. 点击卡片「删除」→ `showConfirmDialog` 二次确认（危险操作）→ `DELETE /api/users/me/addresses/{id}` → 成功 `showToast` 「删除成功」→ 刷新列表。
  5. 点击卡片「设为默认」（非默认地址）→ `POST /api/users/me/addresses/{id}/default` → 成功刷新列表，原默认变为普通地址。
  6. 下拉刷新 → 重新拉取列表。
- **分支流程**：
  - 地址上限：地址数 ≥ 20 时禁用「新增地址」按钮，提示「地址数量已达上限」。
  - 默认地址删除：删除默认地址时，后端自动将最早创建的地址设为默认。
  - 省市区选择：`van-area` 联动选择，确认后回填省市区字段。
  - 手机号校验：失焦校验 11 位手机号格式，不符合禁用「保存」。
- **跨页面流转**：从下单页进入时，点击卡片选中地址并返回下单页（通过 `router.back()` + 事件总线回传选中地址）。
- **状态机可视化**：普通地址 →（设默认）→ 默认地址；默认地址 →（其他设默认）→ 普通地址。

## 5. 组件清单
- **基础组件**：`van-nav-bar`、`van-cell`、`van-cell-group`、`van-button`、`van-popup`、`van-field`、`van-area`、`van-radio-group`、`van-radio`、`van-switch`、`van-tag`、`van-dialog`（showConfirmDialog）、`van-empty`、`van-skeleton`、`van-toast`（showToast）、`van-icon`、`van-pull-refresh`。
- **业务组件**：`AddressCard` 地址卡片（含标签、默认角标、操作按钮）；`AddressFormPopup` 地址表单弹层（含字段校验、地区联动、标签选择、默认开关）；`TagSelector` 标签选择器（家/公司/学校）。
- **图表组件**：无。
- **图标使用**：返回 `arrow-left`；新增 `add-o`；编辑 `edit`；删除 `delete-o`；默认 `success`；定位 `location-o`；箭头 `arrow`。
- **空状态**：`van-empty`「暂无地址」+ 「新增地址」CTA。

## 6. 视觉规范
- **主色应用**：默认地址卡片边框主色 `#1677FF`；「默认」标签主色；新增/保存按钮主色；设默认按钮主色文字。
- **状态色**：默认地址边框 `#1677FF`；普通地址边框 `#F5F5F5`；删除按钮 `#FF4D4F`；标签「家」`#1677FF`，「公司」`#52C41A`，「学校」`#FAAD14`。
- **间距**：卡片间距 8px；卡片内边距 12px；操作按钮间距 12px；底部新增栏高 56px + `safe-area-inset-bottom`。
- **字体**：收件人姓名 16px semibold `#000000D9`；电话 14px `#595959`；地址 14px `#000000D9`；标签 12px；操作按钮 13px；新增按钮 16px semibold `#FFFFFF`。
- **图标尺寸**：返回 20px；新增 16px；编辑/删除 16px；定位 16px。

## 7. 异常处理与边界
- **加载态**：首屏 `van-skeleton` 模拟 3 张地址卡片。
- **空数据**：`van-empty`「暂无地址」+ 「新增地址」CTA。
- **错误态**：查询失败 `showToast` 「加载失败」+ 重试按钮；保存失败 `showToast` 「保存失败」+ 重试；`van-pull-refresh` 下拉刷新。
- **权限控制**：Buyer 可见；未登录跳 `/login?redirect=/profile/addresses`；后端校验地址归属，越权访问返回 403。
- **并发与乐观锁**：保存按钮点击后立即 disabled + loading 直至响应返回；`Idempotency-Key` 头防重复保存；删除二次确认；设默认串行（一次只允许一个默认）。
- **危险操作确认**：删除地址 `showConfirmDialog` 标题「确认删除」、内容「删除后将无法恢复，关联订单的收货地址不受影响。」、确认按钮红色 `#FF4D4F`。

## 8. 验收要点
- [ ] 地址列表按默认优先排序，默认地址置顶并主色边框。
- [ ] 地址卡片展示收件人、电话（脱敏）、完整地址、标签。
- [ ] 新增/编辑弹层含收件人、手机号、省市区联动、详情、标签、默认开关。
- [ ] 手机号失焦校验 11 位格式，校验失败禁用「保存」。
- [ ] 删除地址需二次确认，确认按钮红色危险色。
- [ ] 设默认成功后刷新列表，原默认变为普通地址。
- [ ] 地址上限 20，达到上限禁用「新增地址」。
- [ ] 下拉刷新与无限滚动不适用（一次性返回全部地址）。
- [ ] 保存防重复（按钮 loading + Idempotency-Key）。
- **性能要求**：首屏 < 1s；保存响应 < 1s；删除响应 < 800ms。
- **可访问性**：卡片 `role="article"`；按钮 `aria-label`；表单字段 `label` 与 `aria-label`；弹层 `role="dialog"`。
