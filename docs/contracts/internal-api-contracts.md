# Internal API 契约清单

> 本文档列出所有 BC 的 internal 端点契约，为 M4.2 Internal API 版本治理做准备。
> 最后更新：2026-07-18

## 概述

Internal API 是 BC 间同步调用的契约边界。所有 internal 端点路由以 `internal/` 为前缀，
由各 BC 的 `InternalApiKeyMiddleware` 校验 `X-Internal-Key` 请求头鉴权（不经过 JWT 鉴权），
仅允许受信任的内部服务调用。本文档按 BC 列出每个 internal 端点的路由、入参、返回、调用方与契约版本。

**统计摘要：**

| 项 | 数量 |
|---|---|
| 限界上下文（BC）总数 | 11 |
| 暴露 internal 端点的 BC | 7（UserAuth、Promotion、Product、PointsMembership、Payment、Notification、Order） |
| 未暴露 internal 端点的 BC | 4（SystemAdmin、SellerShop、ReviewAfterSales、Cart） |
| internal 端点总数 | 11 |

**BC 章节顺序：** UserAuth → SystemAdmin → SellerShop → ReviewAfterSales → Promotion → Product → PointsMembership → Payment → Notification → Cart → Order

---

## 1. UserAuth BC

### 1.1 GET internal/users/{userId:guid}/contacts
- **调用方 BC**：Notification（`UserContactAntiCorruptionService`）
- **入参**：userId (Guid, path)
- **返回**：`ApiResponse<UserContactsDto>`
- **错误码**：404 用户不存在
- **契约版本**：1.0
- **源文件**：`src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs`

---

## 2. SystemAdmin BC

（无 internal 端点）

> 备注：该 BC 的 `Program.cs` 注册了 `InternalApiKeyMiddleware`，但未暴露任何 `internal/` 前缀的控制器端点。

---

## 3. SellerShop BC

（无 internal 端点）

> 备注：该 BC 的 `Program.cs` 注册了 `InternalApiKeyMiddleware`，但未暴露任何 `internal/` 前缀的控制器端点。

---

## 4. ReviewAfterSales BC

（无 internal 端点）

> 备注：该 BC 的 `Program.cs` 注册了 `InternalApiKeyMiddleware`，但未暴露任何 `internal/` 前缀的控制器端点。
> 该 BC 作为调用方消费 Order BC（`internal/orders/{orderId}/status`）与 Payment BC（`internal/payments/{orderId}/info`）的 internal 端点。

---

## 5. Promotion BC

### 5.1 POST internal/promotions/calculate
- **调用方 BC**：Order（`AntiCorruptionServices`）、Cart（`CartPriceService`）
- **入参**：`CalculateDiscountDto` (body)
- **返回**：`ApiResponse<DiscountResultDto>`
- **错误码**：无显式错误码（成功返回 200）
- **契约版本**：1.0
- **源文件**：`src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs`

### 5.2 POST internal/promotions/lock-coupon
- **调用方 BC**：Order（`AntiCorruptionServices`）
- **入参**：`LockCouponRequestDto` (body，含 UserId/CouponId/OrderId)
- **返回**：`ApiResponse`
- **错误码**：404 券不存在；业务错误码 `USER_COUPON_LOCK_INVALID`（券已被并发订单占用，非 Unused）
- **契约版本**：1.0
- **源文件**：`src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs`

---

## 6. Product BC

### 6.1 GET internal/products/skus/{skuId:guid}
- **调用方 BC**：Order（`AntiCorruptionServices.ProductAntiCorruptionService`）
- **入参**：skuId (Guid, path)
- **返回**：`ApiResponse<SkuInfoResultDto>`
- **错误码**：404 SKU 不存在
- **契约版本**：1.0
- **源文件**：`src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs`

### 6.2 POST internal/products/skus/batch
- **调用方 BC**：Cart（`CartPriceService`，常量 `BatchEndpoint = "internal/products/skus/batch"`）
- **入参**：`List<Guid>` skuIds (body)
- **返回**：`ApiResponse<List<SkuInfoResultDto>>`（跳过不存在的 SKU）
- **错误码**：无（成功返回 200）
- **契约版本**：1.0
- **源文件**：`src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs`

---

## 7. PointsMembership BC

### 7.1 POST internal/points/trial-offset
- **调用方 BC**：Order（`AntiCorruptionServices`）
- **入参**：`TrialOffsetDto` (body)
- **返回**：`ApiResponse<TrialOffsetResultDto>`（试算可抵扣金额，不修改账户状态）
- **错误码**：无显式错误码
- **契约版本**：1.0
- **源文件**：`src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs`

### 7.2 POST internal/points/freeze
- **调用方 BC**：Order（`AntiCorruptionServices`）
- **入参**：`FreezePointsDto` (body)
- **返回**：`ApiResponse`（下单预占冻结积分）
- **错误码**：无显式错误码
- **契约版本**：1.0
- **源文件**：`src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs`

### 7.3 POST internal/points/release
- **调用方 BC**：Order（`AntiCorruptionServices`）
- **入参**：`ReleasePointsDto` (body)
- **返回**：`ApiResponse`（订单取消回退释放冻结积分）
- **错误码**：无显式错误码
- **契约版本**：1.0
- **源文件**：`src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs`

---

## 8. Payment BC

### 8.1 GET internal/payments/{orderId:guid}/info
- **调用方 BC**：ReviewAfterSales（`PaymentInfoQueryService`）
- **入参**：orderId (Guid, path)
- **返回**：`ApiResponse<PaymentInfoResultDto>`（支付单标识与渠道）
- **错误码**：404 支付单不存在
- **契约版本**：1.0
- **源文件**：`src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs`

---

## 9. Notification BC

### 9.1 POST internal/notifications/send
- **调用方 BC**：未发现直接 HTTP 调用方（在所有 BC 的 Infrastructure 防腐层中均未检索到对该端点的 HTTP 调用；推测主要由 Notification BC 内部消费集成事件后触发，或由尚未实现的调用方使用）
- **入参**：`SendNotificationRequest` (body，含 TemplateCode/UserId/Variables/IdempotencyKey/BusinessRef)
- **返回**：`ApiResponse<SendNotificationResponse>`（含 Succeeded/RecordId/ErrorCode/ErrorMessage）
- **错误码**：400 模板编码不可为空；400 用户标识不可为空；400 发送失败（业务失败时 HTTP 仍为 200，错误信息在 response body 的 `ErrorMessage` 字段）
- **契约版本**：1.0
- **源文件**：`src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs`

---

## 10. Cart BC

（无 internal 端点）

> 备注：该 BC 的 `Program.cs` 注册了 `InternalApiKeyMiddleware`，但未暴露任何 `internal/` 前缀的控制器端点。
> 该 BC 作为调用方消费 Product BC（`internal/products/skus/batch`）的 internal 端点。

---

## 11. Order BC

### 11.1 GET internal/orders/{orderId:guid}/status
- **调用方 BC**：ReviewAfterSales（`ReviewEligibilityChecker`、`AfterSalesEligibilityChecker`）
- **入参**：orderId (Guid, path)
- **返回**：`ApiResponse<OrderStatusResultDto>`
- **错误码**：404 订单不存在
- **契约版本**：1.0
- **源文件**：`src/Services/Order/Leno.Order.Api/Controllers/InternalOrdersController.cs`

---

## 附录 A：网关与内部鉴权

### A.1 InternalApiKeyMiddleware（internal 端点主鉴权）
- 所有 BC 的 `Program.cs` 均注册 `app.UseMiddleware<InternalApiKeyMiddleware>()` 与
  `builder.Services.AddInternalApiKeyAuth(builder.Configuration)`。
- 所有 `internal/` 前缀端点由该中间件校验 `X-Internal-Key` 请求头，**不经过 JWT 鉴权**。
- 调用方防腐层（如 `Order/AntiCorruptionServices.cs`、`Cart/CartPriceService.cs`）通过
  `InternalApiKeyOptions` 注入相同的 ApiKey，并以 `X-Internal-Key` 头部发送（常量名 `InternalKeyName = "X-Internal-Key"`）。
- 启动时通过 `app.EnsureInternalApiKeyConfigured()` 校验配置完整性。

### A.2 GatewayAuthHandler（网关头认证，与 internal 端点鉴权相互独立）
- 源文件：`src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthHandler.cs`
- 作用：从 `X-User-Id` / `X-Role` / `X-Shop-Id` 头构造 `ClaimsPrincipal`，用于后端服务在内网部署时
  信任网关 JWT 验签后注入的用户上下文。
- 可选校验：当 `GatewayAuthOptions.RequireInternalCallHeader = true` 时，额外校验 `X-Internal-Call` 头
  存在性（缺失返回 `AuthenticateResult.Fail("Missing X-Internal-Call header")`）。
- 与 `InternalApiKeyMiddleware` 的关系：两者为并行的独立鉴权机制。`GatewayAuthHandler` 处理
  网关转发的用户身份头；`InternalApiKeyMiddleware` 专门保护 `internal/` 前缀的服务间同步调用端点。
- 灰度配置：`Auth:Mode` 可切换 `GatewayHeader` / `JWT` 模式，详见 Plan 2 F2.3 GatewayHeader 安全修复。

### A.3 Internal API 版本治理（M4.2 预告）
- 当前所有 internal 端点契约版本均为 1.0，未引入版本号路由前缀（如 `internal/v1/...`）。
- M4.2 将基于本文档的契约清单设计版本治理策略（URI 版本 / Header 版本 / 兼容窗口）。

---

## 附录 B：发现的问题与契约不一致

在 Task 8 的代码扫描过程中，发现以下调用方引用了**未定义的 internal 端点**，属于契约不一致，
需在 M4.2 版本治理阶段修复（建议作为遗留问题登记）：

| 序号 | 调用方源文件 | 引用的端点 | 状态 |
|---|---|---|---|
| 1 | `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs:205` | `POST internal/promotions/release-coupons` | Promotion BC 的 `InternalPromotionsController` 仅暴露 `calculate` 与 `lock-coupon`，未定义 `release-coupons` 端点 |
| 2 | `src/Services/Order/Leno.Order.Infrastructure/Services/AntiCorruptionServices.cs:429` | `POST internal/points/confirm` | PointsMembership BC 的 `InternalPointsController` 仅暴露 `trial-offset` / `freeze` / `release`，未定义 `confirm` 端点 |

**建议处理方式：**
- 确认这两个端点是缺失实现还是调用方遗留代码，二选一：
  - 在被调用方 BC 补齐端点实现并补充到本契约清单；或
  - 移除调用方防腐层中的失效调用。
