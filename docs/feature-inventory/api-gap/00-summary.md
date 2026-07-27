# API 缺失对比报告总览

**文档版本**：V1.0
**创建日期**：2026-07-26
**关联设计**：`docs/superpowers/specs/2026-07-26-feature-inventory-and-api-gap-design.md`
**关联计划**：`docs/superpowers/plans/2026-07-26-feature-inventory-and-api-gap.md`

---

## 1. 11 BC 差异统计汇总

| BC | 中文名 | 缺失 | 闲置 | 路径不一致 | 能力不匹配 | 合计 |
|-|-|-|-|-|-|-|
| BC1 | 用户与认证授权 | 12 | 0 | 4 | 1 | 17 |
| BC2 | 商品 | 0 | 1 | 3 | 3 | 7 |
| BC3 | 购物车 | 0 | 7 | 0 | 0 | 7 |
| BC4 | 订单与交易 | 2 | 4 | 1 | 2 | 9 |
| BC5 | 促销 | 0 | 1 | 2 | 3 | 6 |
| BC6 | 评价与售后 | 3 | 2 | 1 | 4 | 10 |
| BC7 | 积分与会员 | 5 | 4 | 9 | 0 | 18 |
| BC8 | 支付集成 | 0 | 7 | 2 | 2 | 11 |
| BC9 | 消息通知 | 0 | 7 | 0 | 2 | 9 |
| BC10 | 卖家与店铺 | 5 | 0 | 0 | 3 | 8 |
| BC11 | 系统管理 | 12 | 0 | 0 | 1 | 13 |
| **合计** | | **39** | **33** | **22** | **21** | **115** |

> 数字来源：各 BC 报告「## 1. 概览」段的「差异统计」行汇总
> 4 类差异定义见 [../README.md](../README.md) 第 4 节

---

## 2. 全局优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 | 合计 |
|-|-|-|-|-|-|
| P0 | 2 | 0 | 0 | 0 | 2 |
| P1 | 36 | 4 | 17 | 18 | 75 |
| P2 | 1 | 29 | 5 | 3 | 38 |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强
> P0 项仅 2 项：BC4 `GET /api/seller/orders` 卖家履约闭环入口缺失 + BC8 `POST /api/payments` 发起支付端点缺失（design-prompts 误标 ✅）

---

## 3. Top 10 高优先级修复项

| 排名 | BC | 类别 | 端点 | 来源 | 建议操作 |
|-|-|-|-|-|-|
| 1 | BC8 | 缺失(P0) | POST /api/payments | [payment-initiate.md](file:///e:/Leno/docs/design-prompts/buyer-app/07-payment/payment-initiate.md) | 立即新增 PaymentsController.PostAsync，对应 spec F-PAY-001 同步发起支付 |
| 2 | BC4 | 缺失(P0) | GET /api/seller/orders | [order-list.md](file:///e:/Leno/docs/design-prompts/seller/05-order-fulfillment/order-list.md), [pending-shipment.md](file:///e:/Leno/docs/design-prompts/seller/05-order-fulfillment/pending-shipment.md) | 在 OrdersController 新增 Seller 鉴权端点，复用 OrderListQuery |
| 3 | BC4 | 路径不一致(P1) | GET /api/orders/{id}/logistics → /api/orders/{id}/logistics-trace | [logistics-trace.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/logistics-trace.md) | 改文档对齐源码 /logistics-trace |
| 4 | BC8 | 路径不一致(P1) | GET /api/payments/result/{orderId} → /api/payments/{orderId} | [payment-result.md](file:///e:/Leno/docs/design-prompts/buyer-app/07-payment/payment-result.md) | 改文档对齐源码 |
| 5 | BC1 | 缺失(P1) | GET/PUT /api/users/me/notification-preferences | [notification-preferences.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/notification-preferences.md) | 新增 PreferencesController（design 误标 ✅） |
| 6 | BC1 | 缺失(P1) | 收藏 5 个端点（GET/POST/DELETE /api/favorites, GET /api/favorites/{id}） | 待补充 design-prompts 收藏模块 | 新增 FavoritesController |
| 7 | BC1 | 缺失(P1) | 浏览历史 5 个端点（GET/DELETE /api/browse-history 等） | 待补充 design-prompts 浏览历史模块 | 新增 BrowseHistoryController |
| 8 | BC7 | 路径不一致(P1) | Membership 服务 9 个端点路径大小写/前缀不一致 | [points-rules.md](file:///e:/Leno/docs/design-prompts/operations/08-membership-ops/points-rules.md) 等 | 待 BC7 拆分完成后统一对齐 |
| 9 | BC6 | 缺失(P0/P1) | GET /api/seller/after-sales/{id}, GET /api/seller/reviews | [after-sales-detail.md](file:///e:/Leno/docs/design-prompts/seller/06-after-sales/after-sales-detail.md), [review-reply.md](file:///e:/Leno/docs/design-prompts/seller/07-review/review-reply.md) | 新增 Seller 鉴权端点 |
| 10 | BC11 | 缺失(P1) | 告警管理 6 个端点 + Outbox 监控 6 个端点 | [alert-management.md](file:///e:/Leno/docs/design-prompts/system-admin/04-runtime-ops/alert-management.md), [outbox-monitor.md](file:///e:/Leno/docs/design-prompts/system-admin/05-audit/outbox-monitor.md) | 新建 AlertsController + OutboxMonitorController |

---

## 4. 拆分过渡态影响范围

> **域拆分迁移阶段1-2 已完成（2026-07-26）**：Identity / UserCenter / AccessControl / Points / Membership / Review / AfterSales 七个新域已就绪并经网关双轨挂载。下表中「待切换端点数」已降至 0，新域端点全部上线，旧域代码保留作回滚兜底，待阶段3观察期结束后下线。详见 `docs/feature-inventory/domain-migration-status.md`。

| 主 BC | 旧 BC | 新 BC | 影响端点数 | 待切换端点数 | 关键风险 |
|-|-|-|-|-|-|
| BC1 | UserAuth | Identity + UserCenter + AccessControl | 42 | 0（阶段1-2已完成） | 新域已就绪：Identity 接管认证/资料/OAuth 28 端点、UserCenter 接管地址/收藏/浏览历史/通知偏好 17 端点、AccessControl 接管角色与权限 7 端点；旧域 UserAuth 双轨兜底 |
| BC6 | ReviewAfterSales | Review + AfterSales | 22 | 0（阶段1-2已完成） | 新域已就绪：Review 接管评价 11 端点+gRPC、AfterSales 接管售后 14 端点；旧域 ReviewAfterSales 双轨兜底 |
| BC7 | PointsMembership | Points + Membership | 32 | 0（阶段1-2已完成） | 新域已就绪：Points 接管积分 16 端点+gRPC（含 `PointsController` / `AdminPointsController` / `PointsRulesController` / `TasksController` / `InternalPointsController`）、Membership 接管会员 12 端点；旧域 PointsMembership 双轨兜底 |

**双轨期统一规范**：
- 阶段1-2 已完成：新域端点已上线，design-prompts 已将「服务归属」更新为新域
- 网关双轨挂载：灰度默认 5%，可通过 `Grayscale:Threshold` 调整；internal 端点 100% 切新域
- 回滚开关：`Grayscale:RollbackToLegacy=true` 即将流量回退至旧域
- 阶段3观察期结束后，旧域代码下线，新域独占承载

---

## 5. 推荐实施顺序

### 第一梯队（P0 阻塞交易闭环，立即修复）

1. **BC8 支付**：新增 `POST /api/payments` 发起支付端点（阻塞买家端交易闭环）
2. **BC4 订单**：新增 `GET /api/seller/orders` 卖家履约端点（阻塞卖家履约闭环）

### 第二梯队（P1 影响体验，短期补充）

3. **BC1 用户认证**：补 12 个缺失端点（通知偏好 + 收藏 + 浏览历史）+ 修正 4 处路径不一致
4. **BC7 积分会员**：先补 5 个缺失的规则 CRUD 端点，再处理 9 处 Membership 路径不一致
5. **BC6 评价售后**：补 3 个 Seller 端缺失端点 + 4 处能力不匹配
6. **BC11 系统管理**：补 12 个缺失端点（告警 + Outbox 监控）

### 第三梯队（P1 文档/能力对齐）

7. **BC4 订单**：修正 logistics 路径不一致 + 补 2 处能力不匹配
8. **BC8 支付**：修正 payments/result 路径不一致 + 补 2 处能力不匹配
9. **BC2 商品**：修正 3 处 seller 路径不一致 + 补 3 处能力不匹配
10. **BC5 促销**：修正 2 处优惠券启停路径不一致 + 补 3 处能力不匹配

### 第四梯队（P2 闲置处理）

11. **BC3 购物车**：7 个匿名购物车端点保留观察
12. **BC8 支付**：7 个闲置端点（对账/回调/补偿）补设计稿调用或保留
13. **BC9 通知**：7 个闲置端点（死信/回执/内部）补设计稿调用或保留
14. **BC10 卖家店铺**：3 处能力不匹配（批量审核/状态计数/Top 10 GMV 聚合）

### 第五梯队（文档同步批次）

15. 将 design-prompts 中误标 ✅ 的端点修正为 🚧 或补充端点实现：
    - BC1 通知偏好 2 个端点
    - BC8 POST /api/payments（已在第一梯队修复实现）
    - 其他文档对齐项

---

## 6. 关键发现

### 6.1 文档过乐观（design-prompts 标 ✅ 但源码无实现）

| BC | 端点 | 来源页面 | 建议处理 |
|-|-|-|-|
| BC1 | GET/PUT /api/users/me/notification-preferences | notification-preferences.md | 改标 🚧 或补实现 |
| BC8 | POST /api/payments | payment-initiate.md | 已在第一梯队修复实现 |
| BC4 | GET /api/seller/orders | order-list.md, pending-shipment.md | 已在第一梯队修复实现 |

### 6.2 拆分过渡态风险

> **更新（2026-07-26）**：阶段1-2 已完成，下列历史风险已解除，新域全部就绪并经网关双轨挂载。旧域代码保留作回滚兜底，待阶段3观察期结束后下线。

- ~~**BC7 进度最滞后**：Points 服务完全无 Controllers，Membership 服务路径/鉴权不对齐~~ **已解除**：Points 域已就绪（5 个 Controller，含 `PointsController` / `AdminPointsController` / `PointsRulesController` / `TasksController` / `InternalPointsController`，共 16 端点 + gRPC）；Membership 域已就绪（4 个 Controller，共 12 端点）
- ~~**BC6 新 BC 未启动**：`src/Services/AfterSales/` 目录尚未建立~~ **已解除**：AfterSales 域已就绪（4 个 Controller：`AfterSalesController` / `SellerAfterSalesController` / `AdminAfterSalesController` / `AfterSalesControllerBase`，共 14 端点）；Review 域已就绪（5 个 Controller，共 11 端点 + gRPC）
- ~~**BC1 Identity 缺能力**：4 类 UserAuth 独有能力未在 Identity 实现~~ **已解除**：Identity 域已就绪（6 个 Controller，含 `AuthController` / `AccountController` / `UsersController` / `AdminUsersController` / `AdminOAuthClientsController` / `InternalUsersController`，共 28 端点）；UserCenter 域已就绪（5 个 Controller，共 17 端点）；AccessControl 域已就绪（1 个 Controller，共 7 端点）

### 6.3 闲置端点合理保留

多数闲置端点属合理保留：
- BC3 匿名购物车 7 个：支撑登录后购物车合并流程
- BC8 第三方回调 2 个：微信/支付宝支付回调
- BC9 死信管理 3 个 + 内部接口 2 个：运营管理需要
- BC8/BC9 内部接口：跨 BC 内部调用

建议保留观察，必要时补充 design-prompts 调用。

### 6.4 能力不匹配集中在运营端筛选

11 处能力不匹配中，9 处为运营端列表接口缺筛选能力（name/type/时间范围/状态等）。建议统一补 `QueryDto` 字段，不影响接口契约。

---

## 7. 报告索引

| BC | 文件 | 源码端点数 | 期望端点数 | 差异合计 |
|-|-|-|-|-|
| BC1 | [bc1-user-auth.md](./bc1-user-auth.md) | 42 | ~44 | 17 |
| BC2 | [bc2-product.md](./bc2-product.md) | 31 | 28 | 7 |
| BC3 | [bc3-cart.md](./bc3-cart.md) | 15 | 8 | 7 |
| BC4 | [bc4-order.md](./bc4-order.md) | 24 | 23 | 9 |
| BC5 | [bc5-promotion.md](./bc5-promotion.md) | 26 | 23 | 6 |
| BC6 | [bc6-review-aftersales.md](./bc6-review-aftersales.md) | 22 | 23 | 10 |
| BC7 | [bc7-points-membership.md](./bc7-points-membership.md) | 32 | 24 | 18 |
| BC8 | [bc8-payment.md](./bc8-payment.md) | 15 | 9 | 11 |
| BC9 | [bc9-notification.md](./bc9-notification.md) | 26 | 23 | 9 |
| BC10 | [bc10-seller-shop.md](./bc10-seller-shop.md) | 17 | 21 | 8 |
| BC11 | [bc11-system-admin.md](./bc11-system-admin.md) | 68 | 74 | 13 |
| **合计** | | **318** | **300** | **115** |

> 源码端点数含部分内部端点与跨 BC 端点，期望端点数去重后统计
