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
| v1.3 | 2026-07-19 | M4 双轨方案落地（Task 26 文档收尾）：新增第 9 节"M4 gRPC 双轨契约（基于实施）"，基于实际代码梳理 6 个 gRPC 服务端、7 个 gRPC 客户端、Dispatcher 适配器模式、熔断器、Consul 热更新等实施细节；如与第 5 节 Plan 期描述冲突，以第 9 节为准 |

---

## 9 M4 gRPC 双轨契约（基于实施）

> 本节为 M4 gRPC 双轨方案（Plan 8 M4.3 Task 26）的实际落地契约，基于源代码梳理。如与第 5 节 Plan 期描述冲突，以本节为准。
> 落地日期：2026-07-19；配套 Runbook：`docs/runbooks/m4-grpc-poc-verification.md`；防腐层架构详见 `docs/architecture/anticorruption-pattern.md`。

### 9.1 服务端约定

- 所有 GrpcService 放置在 `{BC}.Api/GrpcServices/` 目录。
- 命名规则：`{BC}GrpcService`，继承 `{BC}InternalService.{BC}InternalServiceBase`（gRPC 自动生成的 base 类）。
- **必须复用 Application 层 `IXxxInternalQueryService` 业务逻辑**，禁止在 GrpcService 中直接访问仓储或重复业务规则。
- 错误码映射约定：

  | 业务场景 | gRPC StatusCode |
  |---|---|
  | 资源不存在（NotFound） | `StatusCode.NotFound` |
  | 权限缺失（InternalApiKey 校验失败） | `StatusCode.Unauthenticated`（由 `GrpcInternalKeyInterceptor` 抛出） |
  | 参数无效（Guid 解析失败等） | `StatusCode.InvalidArgument` |
  | POC 阶段未实现的方法 | `StatusCode.Unimplemented` |
  | 服务端内部异常 | `StatusCode.Internal` |

- 必须添加 `[Authorize]` 特性（与 HTTP 路径 `InternalApiKeyMiddleware` 形成双轨鉴权）。
- 通过 `GrpcInternalKeyInterceptor` 拦截器统一校验 metadata `x-internal-key`（小写），与 HttpClient 模式的 `X-Internal-Key` 请求头语义一致。
- 在 `Program.cs` 中条件性映射（同端口复用 HTTP/1.1 + HTTP/2）：

  ```csharp
  // M4 双轨方案：启用 gRPC 服务端（仅当 AntiCorruption:UseGrpc=true 时映射）
  if (builder.Configuration.GetValue<bool>("AntiCorruption:UseGrpc"))
  {
      app.MapGrpcService<ProductGrpcService>();
  }
  ```

### 9.2 客户端约定

- 所有 gRPC 客户端放置在 `{调用方BC}.Infrastructure/Services/Grpc/` 目录。
- 命名规则：
  - 防腐层语义强的客户端：`Grpc{目标BC}AntiCorruptionClient`（如 `GrpcProductAntiCorruptionClient`）。
  - 功能语义强的客户端：`Grpc{功能}Provider` 或 `Grpc{功能}Service`（如 `GrpcOrderStatusProvider`、`GrpcCartPriceService`）。
- 必须继承 `GrpcAntiCorruptionClientBase` 并实现与 HttpClient 模式相同的防腐层接口（如 `IProductAntiCorruptionService`、`IOrderStatusProvider`）。
- **禁止将 gRPC 客户端直接注入业务层**。必须通过 `AntiCorruptionDispatcher<TService>` 双轨调度，由 Dispatcher 在运行时根据 `UseGrpc` 开关与熔断状态选择 HttpClient 或 gRPC 实现。
- 由于 `AntiCorruptionDispatcher<TService>` **不实现 `TService` 接口**（仅提供 `ExecuteAsync` 模板方法），必须为每个防腐层接口创建 `{Service}DispatcherAdapter` 适配器，作为 DI 容器中 `TService` 的具体实现。
- 熔断器为 `CircuitBreakerState` Keyed Singleton，每个防腐层一个实例，`serviceName` 必须与 Metrics 标签一致（如 `product`、`promotion`、`points`、`order`、`payment`、`user-auth`）。

### 9.3 配置约定

```jsonc
// appsettings.json（Order BC 示例）
{
  "AntiCorruption": {
    "UseGrpc": false,                              // 灰度总开关，Consul KV 热更新
    "GrpcEndpoints": {                              // 仅 UseGrpc=true 时需配置
      "Product": "http://leno-product-api:5150",
      "Promotion": "http://leno-promotion-api:5152",
      "PointsMembership": "http://leno-pointsmembership-api:5153"
    },
    "TargetInternalApiKeys": {                      // 各目标 BC 的 InternalApiKey
      "Product": "${LENO_INTERNAL_API_KEY_PRODUCT}",
      "Promotion": "${LENO_INTERNAL_API_KEY_PROMOTION}",
      "PointsMembership": "${LENO_INTERNAL_API_KEY_POINTSMEMBERSHIP}"
    },
    "CircuitBreaker": {                             // 熔断器参数（可选，缺省 3/2/30s）
      "FailureThreshold": 3,
      "SuccessThreshold": 2,
      "OpenDurationSeconds": 30
    },
    "InternalApiKey": "${LENO_INTERNAL_API_KEY_ORDER}",  // 本 BC 作为被调用方时校验的 key
    "ServiceName": "order"
  }
}
```

| 配置项 | 作用 | 热更新 | 默认值 |
|---|---|---|---|
| `AntiCorruption:UseGrpc` | 调用方 BC 是否启用 gRPC 模式 | 是（Consul KV `leno/anticorruption/use-grpc/{BC}`，5 分钟长轮询） | `false` |
| `AntiCorruption:GrpcEndpoints:{BC}` | 各目标 BC 的 gRPC 端点地址（与 HTTP 端口同端口复用 HTTP/1.1 + HTTP/2） | 是 | 必填（UseGrpc=true 时缺失抛 `InvalidOperationException`） |
| `AntiCorruption:TargetInternalApiKeys:{BC}` | 各目标 BC 的 InternalApiKey，注入 gRPC metadata `x-internal-key` | 是 | 必填 |
| `AntiCorruption:CircuitBreaker` | 熔断器参数 | 是（`IOptionsMonitor` 推送） | `FailureThreshold=3 / SuccessThreshold=2 / OpenDurationSeconds=30` |
| `AntiCorruption:InternalApiKey` | 本 BC 作为被调用方时 `GrpcInternalKeyInterceptor` 校验的 key | 是 | 必填（缺失拒绝所有 gRPC 调用） |
| `AntiCorruption:ServiceName` | 本 BC 服务名 | 是 | 必填 |

> **端口约定**：gRPC 与 HTTP 同端口复用（HTTP/1.1 + HTTP/2 协商），**不再使用第 5.4 节"HTTP + 100"的独立端口分配方案**。各 BC 实际端口以其 `appsettings.json` 的 `ServiceUrls` 或 Consul 服务发现为准。

### 9.4 服务清单（基于实际代码）

#### 9.4.1 gRPC 服务端（6 个 BC）

| BC | GrpcService 类 | .proto service | 复用 Application 接口 | 源文件 |
|---|---|---|---|---|
| Product | `ProductGrpcService` | `ProductInternalService` | `IProductInternalQueryService` | `src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs` |
| Promotion | `PromotionGrpcService` | `PromotionInternalService` | `IPromotionInternalQueryService` | `src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs` |
| PointsMembership | `PointsGrpcService` | `PointsInternalService` | `IPointsInternalQueryService` | `src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs` |
| UserAuth | `UserAuthGrpcService` | `UserAuthInternalService` | `IUserInternalQueryService` | `src/Services/UserAuth/Leno.UserAuth.Api/GrpcServices/UserAuthGrpcService.cs` |
| Order | `OrderGrpcService` | `OrderInternalService` | `IOrderInternalQueryService` | `src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs` |
| Payment | `PaymentGrpcService` | `PaymentInternalService` | `IPaymentInternalQueryService` | `src/Services/Payment/Leno.Payment.Api/GrpcServices/PaymentGrpcService.cs` |

> 未暴露 gRPC 服务端的 BC（Cart/ReviewAfterSales/Notification/SellerShop/SystemAdmin）仅作为调用方消费 gRPC，其 `Program.cs` 不映射 GrpcService。

#### 9.4.2 gRPC 客户端（7 个防腐层）

| 调用方 BC | gRPC 客户端类 | 实现接口 | DispatcherAdapter | serviceName |
|---|---|---|---|---|
| Order | `GrpcProductAntiCorruptionClient` | `IProductAntiCorruptionService` | `ProductAntiCorruptionDispatcherAdapter` | `product` |
| Order | `GrpcPromotionAntiCorruptionClient` | `IPromotionAntiCorruptionService` | `PromotionAntiCorruptionDispatcherAdapter` | `promotion` |
| Order | `GrpcPointsAntiCorruptionClient` | `IPointsAntiCorruptionService` | `PointsAntiCorruptionDispatcherAdapter` | `points` |
| Notification | `GrpcUserContactAntiCorruptionClient` | `IUserContactService` | `UserContactDispatcherAdapter` | `user-auth` |
| Cart | `GrpcCartPriceService` | `ICartPriceService` | `CartPriceDispatcherAdapter` | `product` |
| ReviewAfterSales | `GrpcOrderStatusProvider` | `IOrderStatusProvider` | `OrderStatusDispatcherAdapter` | `order` |
| ReviewAfterSales | `GrpcPaymentInfoQueryService` | `IPaymentInfoQueryService` | `PaymentInfoQueryDispatcherAdapter` | `payment` |

> 共 7 个 gRPC 客户端覆盖 5 个调用方 BC（Order ×3、Notification ×1、Cart ×1、ReviewAfterSales ×2）。Cart BC 的 `GrpcCartPriceService` 复用 Product BC 的 gRPC 服务端。

#### 9.4.3 .proto 契约清单

11 个 .proto 文件位于 `src/BuildingBlocks/Leno.SharedContracts/Protos/`，与 11 个 BC 一一对应：

```
cart.proto          notification.proto    product.proto     seller.proto
order.proto         payment.proto         promotion.proto   system.proto
points.proto        review.proto          user.proto
```

- `package`：`leno.{bc}.v1`（如 `leno.product.v1`、`leno.order.v1`）。
- `option csharp_namespace`：`Leno.SharedContracts.Grpc.{BC}.V1`。
- 服务命名：`{BC}InternalService`（如 `ProductInternalService`，**非** Plan 期描述的 `XxxInternalQueryService`）。
- 字段命名：`snake_case`，C# 自动生成 `PascalCase` 属性。
- 字段扩展只能新增 `optional` 字段或新字段号，禁止修改或删除（保证 wire 兼容，buf breaking 校验）。
- POC 阶段 Guid 字段使用 `int64` 简化（通过 `GetHashCode()` 映射），生产化阶段需迁移为 `string`，迁移时通过新增 `string` 字段保持向后兼容。

### 9.5 DI 注册模式

每个调用方 BC 的 `{BC}.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 按以下模式注册双轨防腐层：

```csharp
// 1. HttpClient 实现（始终注册，作为降级备份）
services.AddHttpClient<ProductAntiCorruptionService>(c => c.BaseAddress = new Uri(productApiUrl))
    .AddAntiCorruptionPolicies();

// 2. UseGrpc=true 时注册 gRPC 链路
if (antiCorruptionOptions.UseGrpc)
{
    // 2.1 gRPC 客户端工厂
    services.AddGrpcClient<ProductInternalService.ProductInternalServiceClient>(options =>
    {
        options.Address = new Uri(productGrpcEndpoint);
    });
    services.AddScoped<GrpcProductAntiCorruptionClient>();

    // 2.2 熔断器 Keyed Singleton（serviceName 与 Metrics 标签一致）
    services.AddKeyedSingleton<CircuitBreakerState>("product", (sp, _) =>
    {
        var opts = sp.GetRequiredService<IOptionsMonitor<AntiCorruptionOptions>>().CurrentValue;
        var cbOpts = opts.CircuitBreaker ?? new CircuitBreakerOptions();
        return new CircuitBreakerState(
            "product",
            cbOpts.FailureThreshold,
            cbOpts.SuccessThreshold,
            TimeSpan.FromSeconds(cbOpts.OpenDurationSeconds));
    });

    // 2.3 Dispatcher（Scoped，组合 HttpClient + gRPC + 熔断器 + IOptionsMonitor）
    services.AddScoped<AntiCorruptionDispatcher<IProductAntiCorruptionService>>(sp => { /* ... */ });

    // 2.4 适配器作为 TService 的具体实现
    services.AddScoped<ProductAntiCorruptionDispatcherAdapter>();
    services.AddScoped<IProductAntiCorruptionService>(sp =>
        sp.GetRequiredService<ProductAntiCorruptionDispatcherAdapter>());
}
else
{
    // UseGrpc=false：直接注册 HttpClient 实现（兼容期）
    services.AddScoped<IProductAntiCorruptionService>(sp =>
        sp.GetRequiredService<ProductAntiCorruptionService>());
}
```

> **设计要点**：业务层仅依赖 `IXxxAntiCorruptionService` 接口，对底层是 HttpClient 还是 gRPC 完全无感。切换由 `UseGrpc` 配置 + `AntiCorruptionDispatcher` 运行时决策。

### 9.6 鉴权与错误处理

#### 9.6.1 服务端鉴权（`GrpcInternalKeyInterceptor`）

- 拦截所有 unary gRPC 调用，从 metadata 读取 `x-internal-key`（大小写不敏感）。
- 与 `AntiCorruptionOptions.InternalApiKey`（被调用方 BC 的 key）比对。
- 缺失配置：抛 `RpcException(Unauthenticated, "Internal API key not configured on server")`，拒绝所有 gRPC 调用（fail-closed）。
- 缺失或值不匹配：抛 `RpcException(Unauthenticated, "Invalid or missing x-internal-key")`。
- 客户端被判定为业务异常（`Unauthenticated`），**不触发降级**，直接抛出给上层业务。

#### 9.6.2 客户端异常分类（`GrpcAntiCorruptionClientBase`）

| 异常类型 | 映射 ErrorCode | 触发熔断降级 |
|---|---|---|
| `OperationCanceledException`（用户取消） | 透传不埋点 | 否 |
| `OperationCanceledException`（超时） | `{SERVICE}_UNAVAILABLE` | 是（`DeadlineExceeded`） |
| `RpcException`（Unavailable/DeadlineExceeded/Internal/ResourceExhausted） | `{SERVICE}_UNAVAILABLE` | **是** |
| `RpcException`（其他 StatusCode：NotFound/PermissionDenied/InvalidArgument/AlreadyExists/...） | `{SERVICE}_REMOTE_FAILED` | 否（业务异常） |
| `DomainException` | 透传不埋点 | 否 |
| 其他 `Exception` | `{SERVICE}_REMOTE_FAILED` | 否 |

> **关键约束**：所有 `RpcException` 必须作为 `AntiCorruptionException.InnerException` 保留，供 `AntiCorruptionDispatcher.IsGrpcUnavailable(ex)` 判断是否降级。

### 9.7 与第 5 节的差异说明

第 5 节为 Plan 期描述，本节为实际实施版本。主要差异：

| 项 | 第 5 节（Plan） | 第 9 节（实施） |
|---|---|---|
| 服务命名 | `XxxInternalQueryService` | `{BC}InternalService`（如 `ProductInternalService`） |
| 端口分配 | HTTP + 100 独立 gRPC 端口（5251-5261） | 同端口复用 HTTP/1.1 + HTTP/2 |
| 客户端基类 | Polly 策略链（重试 + 熔断 + Timeout） | 简单 try/catch + 独立 `CircuitBreakerState` 状态机 |
| 双轨实现 | 单一客户端类内部 `UseGrpc` 分支 | HttpClient 与 gRPC 分离为两个类，由 `AntiCorruptionDispatcher<TService>` 调度 |
| 适配器 | 未提及 | 必须创建 `{Service}DispatcherAdapter`（Dispatcher 不实现 TService 接口） |
| 灰度粒度 | 全局 `UseGrpc` 开关 | 按 BC 粒度（Consul KV `leno/anticorruption/use-grpc/{BC}`） |
