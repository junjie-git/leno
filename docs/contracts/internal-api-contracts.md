# Leno 平台 Internal API 契约

> 本文档记录 11 个限界上下文（BC）间同步调用的 Internal API 契约。
> M4.2 落地 `/v1/` 路由前缀与版本治理；M5.2 落地 11 BC 独立 InternalApiKey 与 Consul KV 配置中心；M6.5 同步本文件并修复契约不一致端点（`release-coupons`）。
> 最后更新：2026-07-19

---

## 1 概述

Internal API 用于 BC 间同步通信，与面向终端用户的公开 API 隔离治理。所有 internal 端点：

- **路由前缀**：统一以 `/internal/v1/` 开头（M4.2 落地），与公开 API 的 `/api/` 前缀物理隔离。
- **鉴权**：由各 BC 的 `InternalApiKeyMiddleware` 校验 `X-Internal-Key` 请求头，**不经过 JWT 鉴权**，仅允许受信任的内部服务调用。
- **传输协议**：默认 REST/HTTP（本文件第 2 节）；高频防腐层可灰度切换 gRPC（本文件第 5 节，M4.3 落地）。
- **版本治理**：当前版本 v1，详见第 4 节。

---

## 2 11 条 Internal API 路由清单（按 BC 分组）

下表列出所有 internal 端点契约。M4.2 已统一加 `/v1/` 前缀，双路由期 1 周后下线旧无前缀路由。

| BC | 路由 | HTTP 方法 | 用途 |
|---|---|---|---|
| Product | `/internal/v1/products/skus/{skuId}` | GET | 查询 SKU 详情 |
| Product | `/internal/v1/products/skus/batch` | POST | 批量查询 SKU |
| Promotion | `/internal/v1/promotions/calculate` | POST | 计算订单优惠 |
| Promotion | `/internal/v1/promotions/lock-coupon` | POST | 锁定优惠券 |
| Promotion | `/internal/v1/promotions/release-coupons` | POST | 释放优惠券 |
| PointsMembership | `/internal/v1/points/trial-offset` | POST | 试算积分抵扣 |
| PointsMembership | `/internal/v1/points/freeze` | POST | 冻结积分 |
| PointsMembership | `/internal/v1/points/release` | POST | 释放积分 |
| UserAuth | `/internal/v1/users/{userId}/contacts` | GET | 查询用户联系方式 |
| Order | `/internal/v1/orders/{orderId}/status` | GET | 查询订单状态 |
| Payment | `/internal/v1/payments/{orderId}/info` | GET | 查询支付信息 |
| Notification | `/internal/v1/notifications/send` | POST | 发送通知 |

**端点统计：**

| 项 | 数量 |
|---|---|
| 暴露 internal 端点的 BC | 7（Product、Promotion、PointsMembership、UserAuth、Order、Payment、Notification） |
| 未暴露 internal 端点的 BC | 4（Cart、ReviewAfterSales、SellerShop、SystemAdmin，仅作为调用方消费） |
| internal 端点总数 | 12（M4.2 基线 11 条 + M6.5 修复 1 条 `release-coupons`） |

> **M6.5 契约修复说明**：`POST /internal/v1/promotions/release-coupons` 端点此前在 Order BC 防腐层被调用但 Promotion BC 未实现（旧版附录 B 记录的契约不一致项）。M6.5 已在 Promotion BC 补齐端点实现并纳入本契约清单。同样地，旧版附录 B 中提到的 `POST /internal/v1/points/confirm` 经核对不在订单取消/释放流程的最终契约中，调用方遗留代码已清理。

### 2.1 Product BC

#### GET `/internal/v1/products/skus/{skuId}`
- **调用方 BC**：Order（`ProductAntiCorruptionService`）
- **入参**：skuId (Guid, path)
- **返回**：`ApiResponse<SkuInfoResultDto>`
- **错误码**：404 SKU 不存在
- **契约版本**：v1
- **源文件**：`src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs`

#### POST `/internal/v1/products/skus/batch`
- **调用方 BC**：Cart（`CartPriceService`，常量 `BatchEndpoint = "internal/v1/products/skus/batch"`）
- **入参**：`List<Guid>` skuIds (body)
- **返回**：`ApiResponse<List<SkuInfoResultDto>>`（跳过不存在的 SKU）
- **错误码**：无（成功返回 200）
- **契约版本**：v1
- **源文件**：`src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs`

### 2.2 Promotion BC

#### POST `/internal/v1/promotions/calculate`
- **调用方 BC**：Order（`AntiCorruptionServices`）、Cart（`CartPriceService`）
- **入参**：`CalculateDiscountDto` (body)
- **返回**：`ApiResponse<DiscountResultDto>`
- **错误码**：无显式错误码（成功返回 200）
- **契约版本**：v1
- **源文件**：`src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs`

#### POST `/internal/v1/promotions/lock-coupon`
- **调用方 BC**：Order（`AntiCorruptionServices`）
- **入参**：`LockCouponRequestDto` (body，含 UserId/CouponId/OrderId)
- **返回**：`ApiResponse`
- **错误码**：404 券不存在；409 券已被并发订单占用（业务错误码 `USER_COUPON_LOCK_INVALID`）
- **契约版本**：v1
- **源文件**：`src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs`

#### POST `/internal/v1/promotions/release-coupons`（M6.5 修复）
- **调用方 BC**：Order（`AntiCorruptionServices`，订单取消时释放已锁定优惠券）
- **入参**：`ReleaseCouponsRequestDto` (body，含 OrderId/CouponIds)
- **返回**：`ApiResponse`
- **错误码**：404 订单不存在；409 优惠券状态非已锁定
- **契约版本**：v1
- **源文件**：`src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs`

### 2.3 PointsMembership BC

#### POST `/internal/v1/points/trial-offset`
- **调用方 BC**：Order（`AntiCorruptionServices`）
- **入参**：`TrialOffsetDto` (body)
- **返回**：`ApiResponse<TrialOffsetResultDto>`（试算可抵扣金额，不修改账户状态）
- **错误码**：无显式错误码
- **契约版本**：v1
- **源文件**：`src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs`

#### POST `/internal/v1/points/freeze`
- **调用方 BC**：Order（`AntiCorruptionServices`）
- **入参**：`FreezePointsDto` (body)
- **返回**：`ApiResponse`（下单预占冻结积分）
- **错误码**：无显式错误码
- **契约版本**：v1
- **源文件**：`src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs`

#### POST `/internal/v1/points/release`
- **调用方 BC**：Order（`AntiCorruptionServices`）
- **入参**：`ReleasePointsDto` (body)
- **返回**：`ApiResponse`（订单取消回退释放冻结积分）
- **错误码**：无显式错误码
- **契约版本**：v1
- **源文件**：`src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs`

### 2.4 UserAuth BC

#### GET `/internal/v1/users/{userId}/contacts`
- **调用方 BC**：Notification（`UserContactAntiCorruptionService`）
- **入参**：userId (Guid, path)
- **返回**：`ApiResponse<UserContactsDto>`
- **错误码**：404 用户不存在
- **契约版本**：v1
- **源文件**：`src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs`

### 2.5 Order BC

#### GET `/internal/v1/orders/{orderId}/status`
- **调用方 BC**：ReviewAfterSales（`ReviewEligibilityChecker`、`AfterSalesEligibilityChecker`）
- **入参**：orderId (Guid, path)
- **返回**：`ApiResponse<OrderStatusResultDto>`
- **错误码**：404 订单不存在
- **契约版本**：v1
- **源文件**：`src/Services/Order/Leno.Order.Api/Controllers/InternalOrdersController.cs`

### 2.6 Payment BC

#### GET `/internal/v1/payments/{orderId}/info`
- **调用方 BC**：ReviewAfterSales（`PaymentInfoQueryService`）
- **入参**：orderId (Guid, path)
- **返回**：`ApiResponse<PaymentInfoResultDto>`（支付单标识与渠道）
- **错误码**：404 支付单不存在
- **契约版本**：v1
- **源文件**：`src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs`

### 2.7 Notification BC

#### POST `/internal/v1/notifications/send`
- **调用方 BC**：未发现直接 HTTP 调用方（主要由 Notification BC 内部消费集成事件后触发，或由尚未实现的调用方使用）
- **入参**：`SendNotificationRequest` (body，含 TemplateCode/UserId/Variables/IdempotencyKey/BusinessRef)
- **返回**：`ApiResponse<SendNotificationResponse>`（含 Succeeded/RecordId/ErrorCode/ErrorMessage）
- **错误码**：400 模板编码不可为空；400 用户标识不可为空；400 发送失败（业务失败时 HTTP 仍为 200，错误信息在 response body 的 `ErrorMessage` 字段）
- **契约版本**：v1
- **源文件**：`src/Services/Notification/Leno.Notification.Api/Controllers/NotificationSendController.cs`

### 2.8 未暴露 internal 端点的 BC

| BC | 角色 | 说明 |
|---|---|---|
| Cart | 仅调用方 | 消费 Product BC `internal/v1/products/skus/batch` 端点 |
| ReviewAfterSales | 仅调用方 | 消费 Order BC `internal/v1/orders/{orderId}/status` 与 Payment BC `internal/v1/payments/{orderId}/info` 端点 |
| SellerShop | 仅调用方 | 当前不直接消费 internal 端点，BFF 聚合层通过网关调用其公开 API |
| SystemAdmin | 仅调用方 | 监控/限流/索引重建走管理 API，不消费 internal 端点 |

> 以上 4 个 BC 的 `Program.cs` 仍注册 `InternalApiKeyMiddleware`，预留未来扩展 internal 端点的能力。

---

## 3 鉴权约定

### 3.1 请求头鉴权

所有 internal 端点请求必须携带 `X-Internal-Key` 请求头：

```http
GET /internal/v1/products/skus/123e4567-e89b-12d3-a456-426614174000 HTTP/1.1
Host: product.internal:5152
X-Internal-Key: {目标BC的InternalApiKey}
Content-Type: application/json
```

- 头部名称常量：`InternalKeyName = "X-Internal-Key"`（定义于 `InternalApiKeyOptions`）
- 头部值：**目标 BC** 的 InternalApiKey（不是调用方自己的 key）。例如 Order 调用 Product BC 时，`X-Internal-Key` 携带 Product BC 的 InternalApiKey。
- 中间件：`InternalApiKeyMiddleware` 在 `Program.cs` 中注册，所有 `/internal/` 前缀端点经此中间件校验，**不经过 JWT 鉴权**。

### 3.2 11 BC 独立 InternalApiKey（M5.2 落地）

| BC | Consul KV 路径 | 说明 |
|---|---|---|
| UserAuth | `leno/security/internal-key/user-auth` | 用户与认证授权域 |
| Product | `leno/security/internal-key/product` | 商品域 |
| Cart | `leno/security/internal-key/cart` | 购物车域 |
| Order | `leno/security/internal-key/order` | 订单与交易域 |
| Promotion | `leno/security/internal-key/promotion` | 促销域 |
| ReviewAfterSales | `leno/security/internal-key/review-aftersales` | 评价与售后域 |
| PointsMembership | `leno/security/internal-key/points-membership` | 积分与会员域 |
| Payment | `leno/security/internal-key/payment` | 支付集成域 |
| Notification | `leno/security/internal-key/notification` | 消息通知域 |
| SellerShop | `leno/security/internal-key/seller-shop` | 卖家与店铺管理域 |
| SystemAdmin | `leno/security/internal-key/system-admin` | 系统管理域 |

- **废除共用 key**：M5.2 之前全平台共用单一 InternalApiKey，M5.2 之后 11 BC 各自独立。任一 BC 密钥泄露不影响其余 BC。
- **启动期 fail-closed**：应用启动时 `EnsureInternalApiKeyConfigured()` 校验本 BC 的 InternalApiKey 已配置，缺失则 fail-closed 阻止启动。
- **生产环境降级**：生产环境若 Consul 不可达，降级为 warning 日志并继续启动（依赖本地缓存配置），但 InternalApiKey 缺失仍 fail-closed。

### 3.3 调用方配置

调用方在 `appsettings.json` 或 Consul KV 中配置 `AntiCorruption:TargetInternalApiKeys` 字典，按 BC 名映射目标 BC 的 InternalApiKey：

```json
{
  "AntiCorruption": {
    "TargetInternalApiKeys": {
      "Product": "{Product BC 的 InternalApiKey}",
      "Promotion": "{Promotion BC 的 InternalApiKey}",
      "PointsMembership": "{PointsMembership BC 的 InternalApiKey}",
      "UserAuth": "{UserAuth BC 的 InternalApiKey}",
      "Order": "{Order BC 的 InternalApiKey}",
      "Payment": "{Payment BC 的 InternalApiKey}",
      "Notification": "{Notification BC 的 InternalApiKey}"
    }
  }
}
```

防腐层 HttpClient 在发送请求时，根据目标 BC 名称从字典中查找对应 InternalApiKey 并注入 `X-Internal-Key` 头部。密钥本身不进入防腐层代码或日志。

---

## 4 版本治理

### 4.1 当前版本

- **当前版本**：v1
- **路由前缀**：`/internal/v1/`（所有 internal 端点统一）

### 4.2 双路由期（M4.2 落地策略）

M4.2 引入 `/v1/` 前缀时采用双路由期策略，确保平滑迁移：

1. **第 0 周**：M4.2 上线，所有 internal 端点同时暴露新旧两套路由（旧无前缀 `/internal/...` + 新带前缀 `/internal/v1/...`），调用方逐步切换到新路由。
2. **第 1 周**：验证新路由全量调用方切换完成，下线旧无前缀路由。
3. **第 2 周起**：仅保留 `/internal/v1/` 前缀路由。

### 4.3 版本演进约定

- **未来 v2**：引入 v2 时保留 v1 服务（双发期 ≥ 4 周），客户端按批次迁移。
- **URI 版本策略**：版本号体现在 URI 路径中（`/internal/v1/` vs `/internal/v2/`），不使用 Header 版本或 Query 参数版本，便于网关路由与监控统计。
- **向后兼容**：v2 上线后 v1 必须保持向后兼容，禁止删除字段或修改字段类型。删除字段需先在 v1 标记 `deprecated`，v2 移除。

### 4.4 SchemaVersion 持久化

`IntegrationEventBase.SchemaVersion` 字段（M1 落地）持久化到 Outbox 表 `schema_version` 列（M4.2 落地），消费端按版本号兼容处理事件结构演进。事件 schema 版本与 Internal API 路由版本独立治理，互不耦合。

---

## 5 gRPC 契约（M4.3 落地，灰度并行）

M4.3 在 REST Internal API 之外补充 gRPC 通道，用于高频、强类型、低延迟的 BC 间同步调用。gRPC 与 REST 并行提供，由灰度开关切换，**gRPC 不替换 REST**。

### 5.1 .proto 契约

- **目录**：`Leno.SharedContracts/Protos/`，11 个 .proto 文件按 BC 命名（如 `product.proto`、`order.proto`、`promotion.proto`）。
- **package 命名**：`leno.{bc}.v1`（如 `leno.product.v1`、`leno.order.v1`）。
- **服务命名**：`{BC}InternalService`（如 `ProductInternalService`、`OrderInternalService`）。
- **方法命名**：动词前缀（如 `GetProductById`、`ValidateSellerOwnership`、`CalculateDiscount`）。
- **buf CLI 校验**：`buf lint` 风格校验 + `buf breaking` 向后兼容校验，CI 强制执行。

### 5.2 灰度开关

```json
{
  "AntiCorruption": {
    "UseGrpc": false
  }
}
```

- **默认值**：`false`（走 REST）
- **3 批次迁移**：
  1. 高频防腐层（Order → Product/Promotion/PointsMembership）
  2. Cart/SellerShop 防腐层
  3. ReviewAfterSales/Notification/SystemAdmin 防腐层

### 5.3 客户端基类

防腐层新建 `GrpcAntiCorruptionClientBase` 基类，封装 gRPC 调用 + Polly 策略链：

- **重试**：3 次，指数退避 1s/2s/4s
- **熔断**：50% 失败率 / 30s 熔断窗口
- **超时**：10s 单次调用超时
- **注入方式**：`AddAntiCorruptionPolicies()` 链式注入到 gRPC client

调用方按 BC 选择目标 InternalApiKey 注入 gRPC metadata（`X-Internal-Key` 头部转换为 gRPC metadata key）。

### 5.4 gRPC 端口分配

| BC | HTTP 端口 | gRPC 端口（HTTP + 100） |
|---|---|---|
| UserAuth | 5151 | 5251 |
| Product | 5152 | 5252 |
| Cart | 5153 | 5253 |
| Order | 5154 | 5254 |
| ReviewAfterSales | 5155 | 5255 |
| Promotion | 5156 | 5256 |
| PointsMembership | 5157 | 5257 |
| Payment | 5158 | 5258 |
| Notification | 5159 | 5259 |
| SellerShop | 5160 | 5260 |
| SystemAdmin | 5161 | 5261 |

---

## 6 错误响应

Internal API 错误响应统一遵循 `ApiResponse<T>` 结构（`code` + `message` + `data` 三段），HTTP 状态码与业务错误码协同表达错误语义。

### 6.1 标准 HTTP 状态码

| HTTP 状态码 | 错误码示例 | 触发场景 |
|---|---|---|
| 401 Unauthorized | `INTERNAL_API_KEY_INVALID` | `X-Internal-Key` 头部缺失或无效（InternalApiKey 校验失败） |
| 404 Not Found | `PRODUCT_SKU_NOT_FOUND`、`ORDER_NOT_FOUND`、`PAYMENT_NOT_FOUND` | 资源不存在（SKU/订单/支付单等） |
| 409 Conflict | `USER_COUPON_LOCK_INVALID`、`COUPON_ALREADY_LOCKED` | 状态冲突（如优惠券已被并发订单锁定） |
| 503 Service Unavailable | `PROMOTION_UNAVAILABLE`、`POINTS_UNAVAILABLE` | 下游服务不可达（防腐层调用失败映射） |

### 6.2 错误响应体示例

```json
{
  "code": "USER_COUPON_LOCK_INVALID",
  "message": "优惠券已被并发订单占用，当前状态非 Unused",
  "data": null
}
```

### 6.3 错误码命名约定

错误码格式：`{DOMAIN}_{ENTITY}_{ACTION}`，如 `ORDER_PAYMENT_PAID`、`PRODUCT_SPU_NOT_FOUND`。后缀自动推断 HTTP 状态码：

- `_NOT_FOUND` → 404
- `_ALREADY_*` / `_EXISTS` / `_CONFLICT` / `_LOCK_INVALID` → 409
- `_FORBIDDEN` → 403
- `_UNAVAILABLE` → 503
- `_FAILED` → 502
- `_EXPIRED` / `_REQUIRED` / `_INVALID` → 401
- 其余 → 400

### 6.4 防腐层错误处理

防腐层远程调用失败统一映射为 `DomainException`，由全局异常处理中间件转换为 HTTP 响应：

- 下游 503 → `DomainException("{SERVICE}_UNAVAILABLE")` → HTTP 503
- 下游 502 → `DomainException("{SERVICE}_REMOTE_FAILED")` → HTTP 502
- 下游 404 → 透传 404（资源不存在，调用方业务降级）
- 下游 409 → 透传 409（状态冲突，调用方业务处理）
- 网络异常/超时 → `DomainException("{SERVICE}_UNAVAILABLE")` → HTTP 503

**禁止**：防腐层返回 null（读操作也抛异常，避免上层空引用）。

---

## 7 网关与内部鉴权（附录）

### 7.1 InternalApiKeyMiddleware（internal 端点主鉴权）

- 所有 BC 的 `Program.cs` 均注册 `app.UseMiddleware<InternalApiKeyMiddleware>()` 与 `builder.Services.AddInternalApiKeyAuth(builder.Configuration)`。
- 所有 `/internal/` 前缀端点由该中间件校验 `X-Internal-Key` 请求头，**不经过 JWT 鉴权**。
- 调用方防腐层通过 `InternalApiKeyOptions` 注入目标 BC 的 InternalApiKey，并以 `X-Internal-Key` 头部发送。
- 启动时通过 `app.EnsureInternalApiKeyConfigured()` 校验配置完整性（M5.2 fail-closed）。

### 7.2 GatewayAuthHandler（网关头认证，与 internal 端点鉴权相互独立）

- 源文件：`src/BuildingBlocks/Leno.Infrastructure/Auth/GatewayAuthHandler.cs`
- 作用：从 `X-User-Id` / `X-Role` / `X-Shop-Id` 头构造 `ClaimsPrincipal`，用于后端服务在内网部署时信任网关 JWT 验签后注入的用户上下文。
- 可选校验：当 `GatewayAuthOptions.RequireInternalCallHeader = true` 时，额外校验 `X-Internal-Call` 头存在性。
- 与 `InternalApiKeyMiddleware` 的关系：两者为并行的独立鉴权机制。`GatewayAuthHandler` 处理网关转发的用户身份头；`InternalApiKeyMiddleware` 专门保护 `/internal/` 前缀的服务间同步调用端点。
- 灰度配置：`Auth:Mode` 可切换 `GatewayHeader` / `JWT` 模式。

---

## 8 版本演进记录

| 版本 | 日期 | 变更说明 |
|---|---|---|
| v1.0 | 2026-07-17 | M4.2 准备阶段初始版本，记录 11 个 internal 端点契约（路由前缀 `internal/`，未带 `/v1/`） |
| v1.1 | 2026-07-18 | M4.2 落地，11 条路由统一加 `/v1/` 前缀，双路由期开始 |
| v1.2 | 2026-07-19 | M5.2 落地，11 BC 独立 InternalApiKey + Consul KV 配置中心；M6.5 修复 `release-coupons` 契约不一致端点，端点总数 12 条；新增第 5 节 gRPC 契约（M4.3）、第 6 节错误响应、第 7 节网关与内部鉴权附录 |
