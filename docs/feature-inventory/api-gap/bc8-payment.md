# BC8 支付集成域 — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

## 1. 概览
- **BC 编号**：BC8
- **中文名**：支付集成域
- **英文名**：Payment
- **涉及端**：buyer-app + operations
- **涉及页面数**：5 页（来自 feature-list：buyer-app 07-payment 2 页 + operations 06-payment-ops 3 页）
- **已实现 API 端点数**：15 个（来自源码 Controller 扫描，含 1 个内部端点）
- **差异统计**：缺失 0 / 闲置 7 / 路径不一致 2 / 能力不匹配 2

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| GET | /api/payments/{orderId} | [PaymentsController.cs#L40](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L40) | 按订单标识查询支付结果（含渠道预支付参数） | Buyer |
| GET | /api/payments/{paymentId}/status | [PaymentsController.cs#L60](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L60) | 主动查询渠道支付状态，若已支付则补偿更新支付单 | Buyer |
| GET | /api/refunds/{afterSalesId} | [PaymentsController.cs#L80](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L80) | 按售后单标识查询退款结果 | Buyer |
| GET | /api/admin/payments | [PaymentsController.cs#L102](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L102) | 运营端分页查询全平台支付记录 | Operator,Admin |
| GET | /api/admin/refunds | [PaymentsController.cs#L120](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L120) | 运营端分页查询全平台退款记录 | Operator,Admin |
| GET | /api/admin/payment-channels | [PaymentChannelConfigController.cs#L36](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentChannelConfigController.cs#L36) | 获取所有渠道配置项列表 | Admin,Operator |
| GET | /api/admin/payment-channels/{id} | [PaymentChannelConfigController.cs#L45](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentChannelConfigController.cs#L45) | 按标识获取配置项详情 | Admin,Operator |
| PUT | /api/admin/payment-channels/{id} | [PaymentChannelConfigController.cs#L60](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentChannelConfigController.cs#L60) | 更新配置项值 | Admin,Operator |
| POST | /api/admin/payment-channels/{id}/enable | [PaymentChannelConfigController.cs#L69](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentChannelConfigController.cs#L69) | 启用配置项 | Admin,Operator |
| POST | /api/admin/payment-channels/{id}/disable | [PaymentChannelConfigController.cs#L78](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentChannelConfigController.cs#L78) | 禁用配置项 | Admin,Operator |
| GET | /api/admin/reconciliation/diffs | [ReconciliationController.cs#L34](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/ReconciliationController.cs#L34) | 分页查询对账差异列表 | Admin |
| POST | /api/admin/reconciliation/trigger | [ReconciliationController.cs#L51](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/ReconciliationController.cs#L51) | 手动触发对账（指定日期） | Admin |
| POST | /api/notify/wechat-pay | [NotifyController.cs#L44](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/NotifyController.cs#L44) | 微信支付异步通知回调（第三方调用） | 匿名（仅验签） |
| POST | /api/notify/alipay | [NotifyController.cs#L79](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/NotifyController.cs#L79) | 支付宝异步通知回调（第三方调用） | 匿名（仅验签） |
| GET | /internal/v1/payments/{orderId}/info | [InternalPaymentsController.cs#L24](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs#L24) | 按订单标识查询支付单概要信息（内部） | 内部（InternalApiKey） |

> 来源：grep `src/Services/Payment/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> Internal*Controller.cs 中的端点单独标注「（内部）」

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| POST | /api/payments | [payment-initiate.md](file:///e:/Leno/docs/design-prompts/buyer-app/07-payment/payment-initiate.md) | 发起支付（body: `{ orderId, channel }`，发布支付请求集成事件） | ✅ | Buyer |
| GET | /api/payments/result/{orderId} | [payment-result.md](file:///e:/Leno/docs/design-prompts/buyer-app/07-payment/payment-result.md) | 查询支付结果（含渠道信息） | ✅ | Buyer |
| GET | /api/admin/payments | [payment-records.md](file:///e:/Leno/docs/design-prompts/operations/06-payment-ops/payment-records.md) | 运营端分页查询全平台支付记录 | ✅ | Operator,Admin |
| GET | /api/admin/refunds | [refund-records.md](file:///e:/Leno/docs/design-prompts/operations/06-payment-ops/refund-records.md) | 运营端分页查询全平台退款记录 | ✅ | Operator,Admin |
| GET | /api/admin/payment-channels | [payment-channels.md](file:///e:/Leno/docs/design-prompts/operations/06-payment-ops/payment-channels.md) | 获取所有渠道配置项列表 | ✅ | Admin,Operator |
| GET | /api/admin/payment-channels/{id} | [payment-channels.md](file:///e:/Leno/docs/design-prompts/operations/06-payment-ops/payment-channels.md) | 获取单个配置项详情 | ✅ | Admin,Operator |
| PUT | /api/admin/payment-channels/{id} | [payment-channels.md](file:///e:/Leno/docs/design-prompts/operations/06-payment-ops/payment-channels.md) | 更新配置项值 | ✅ | Admin,Operator |
| POST | /api/admin/payment-channels/{id}/enable | [payment-channels.md](file:///e:/Leno/docs/design-prompts/operations/06-payment-ops/payment-channels.md) | 启用配置项 | ✅ | Admin,Operator |
| POST | /api/admin/payment-channels/{id}/disable | [payment-channels.md](file:///e:/Leno/docs/design-prompts/operations/06-payment-ops/payment-channels.md) | 禁用配置项 | ✅ | Admin,Operator |

> 来源：design-prompts 的「3. 数据模型与 API 对接」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）
> 注：buyer-app payment-initiate / payment-result 页面同时引用 `GET /api/orders/{id}`（归属 BC4 订单域），本表不列入

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|
| — | — | — | — | — | — |

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现
> 本 BC8 全部 5 个 design-prompts 页面均标 ✅，按判定规则无「缺失」类差异
> 注：POST /api/payments 与 GET /api/payments/result/{orderId} 虽在 design-prompts 标 ✅ 但源码路径不一致，归入 4.3 路径不一致

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| GET | /api/payments/{paymentId}/status | [PaymentsController.cs#L60](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L60) | 主动查询渠道支付状态（补偿回调丢失） | 设计稿补调用：payment-result 页轮询应改用此端点而非订单状态查询 |
| GET | /api/refunds/{afterSalesId} | [PaymentsController.cs#L80](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L80) | 按售后单标识查询退款结果（被 BC6 after-sales-detail 页引用，但 BC8 主页面无引用） | 保留观察：跨 BC 引用，BC6 报告中应已列入期望清单 |
| GET | /api/admin/reconciliation/diffs | [ReconciliationController.cs#L34](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/ReconciliationController.cs#L34) | 分页查询对账差异列表 | 设计稿补调用：spec F-PAY-012 提及对账功能，但 operations 06-payment-ops 无对账页面 |
| POST | /api/admin/reconciliation/trigger | [ReconciliationController.cs#L51](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/ReconciliationController.cs#L51) | 手动触发对账（指定日期） | 设计稿补调用：spec F-PAY-012 运营人工触发对账入口缺失 |
| POST | /api/notify/wechat-pay | [NotifyController.cs#L44](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/NotifyController.cs#L44) | 微信支付异步通知回调（第三方渠道调用） | 保留观察：第三方回调端点，前端无页面引用属正常 |
| POST | /api/notify/alipay | [NotifyController.cs#L79](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/NotifyController.cs#L79) | 支付宝异步通知回调（第三方渠道调用） | 保留观察：第三方回调端点，前端无页面引用属正常 |
| GET | /internal/v1/payments/{orderId}/info | [InternalPaymentsController.cs#L24](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/InternalPaymentsController.cs#L24) | 按订单标识查询支付单概要信息（内部） | 保留观察：内部接口供其他微服务调用，前端无页面引用属正常 |

> 说明：源码有实现但 design-prompts 中无任何页面引用
> 注：对账类端点（diffs/trigger）对应 spec F-PAY-012 渠道对账功能，design-prompts operations 06-payment-ops 模块未提供对账页面，建议补充设计稿

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|
| POST → （无） | /api/payments → （源码无对应端点） | [payment-initiate.md](file:///e:/Leno/docs/design-prompts/buyer-app/07-payment/payment-initiate.md) | [PaymentsController.cs#L40](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L40)（仅有 GET /api/payments/{orderId}） | 改代码：补充 POST /api/payments 端点实现发起支付（spec F-PAY-001 明确要求），或在 design-prompts 标注改用事件驱动发起 |
| GET → GET | /api/payments/result/{orderId} → /api/payments/{orderId} | [payment-result.md](file:///e:/Leno/docs/design-prompts/buyer-app/07-payment/payment-result.md) | [PaymentsController.cs#L40](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L40) | 改文档或改代码：design-prompts 期望 /api/payments/result/{orderId}，源码实际为 /api/payments/{orderId}，建议统一路径（推荐改文档对齐源码，避免破坏现有调用） |

> 说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配
> 注：POST /api/payments 在 design-prompts 标 ✅ 但源码完全缺失，归此处因方法/路径均无匹配

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 筛选含支付单号(PaymentNo)+订单号(OrderNo) | 仅支持 userId/orderId 无 PaymentNo | UI 筛选条含「支付单号」「订单号」输入框，API 仅支持 userId/channel/status/startDate/endDate/page/pageSize | [payment-records.md](file:///e:/Leno/docs/design-prompts/operations/06-payment-ops/payment-records.md) | [PaymentsController.cs#L102](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L102) | 补 query 参数：paymentNo、orderId（订单号筛选） |
| 筛选含退款编号(RefundNo)+时间范围 | 仅支持 orderId/status/page/pageSize，无 RefundNo 与时间范围 | UI 筛选条含「退款编号」「时间范围」输入框，API 无 refundNo/startDate/endDate 参数 | [refund-records.md](file:///e:/Leno/docs/design-prompts/operations/06-payment-ops/refund-records.md) | [PaymentsController.cs#L120](file:///e:/Leno/src/Services/Payment/Leno.Payment.Api/Controllers/PaymentsController.cs#L120) | 补 query 参数：refundNo、startDate、endDate |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异
> 注：design-prompts「3. 数据模型与 API 对接」段列出的请求参数与源码一致，但「2. 页面布局」筛选条 UI 含额外筛选字段，按 UI 期望标注能力不匹配

## 5. 拆分过渡说明

本 BC 无拆分过渡。

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | — | — | POST /api/payments（design-prompts 标 ✅ 但源码完全缺失，阻塞买家发起支付交易闭环） | — |
| P1 | — | GET /api/admin/reconciliation/diffs、POST /api/admin/reconciliation/trigger（spec F-PAY-012 对账功能无运营页面） | GET /api/payments/result/{orderId} → /api/payments/{orderId}（影响支付结果页轮询） | GET /api/admin/payments 缺 PaymentNo/OrderNo 筛选；GET /api/admin/refunds 缺 RefundNo/时间范围筛选 |
| P2 | — | GET /api/payments/{paymentId}/status、GET /api/refunds/{afterSalesId}、POST /api/notify/wechat-pay、POST /api/notify/alipay、GET /internal/v1/payments/{orderId}/info（内部） | — | — |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强

## 7. 跨 BC 依赖
- **上游依赖**：
  - BC4 订单域：消费 `PaymentRequestedIntegrationEvent`（订单域买家发起支付请求，本域创建支付单对接渠道）
  - BC6 评价与售后域：消费 `RefundRequestedIntegrationEvent`（售后退款审核通过，本域创建退款单向渠道退款）
  - BC1 用户域：JWT 中携带 UserId 与角色声明供本域鉴权买家发起支付权限
- **下游依赖**：
  - BC4 订单域：发布 `PaymentSucceededIntegrationEvent`（订单标记已支付）、`PaymentFailedIntegrationEvent`（订单关单或保持待支付）、`PaymentClosedEvent`（订单取消/超时关单）
  - BC6 评价与售后域：发布 `RefundSucceededIntegrationEvent`（售后单流转退款完成）、`RefundFailedIntegrationEvent`（售后域重试或转人工）
  - BC9 消息通知域：支付成功与退款完成后可选发布通知请求事件，由通知域向买家发送支付成功或退款到账通知
- **集成事件订阅/发布清单**：
  - 订阅（入站）：`PaymentRequestedIntegrationEvent`（BC4）、`RefundRequestedIntegrationEvent`（BC6）
  - 发布（出站）：`PaymentSucceededIntegrationEvent`、`PaymentFailedIntegrationEvent`、`PaymentClosedEvent`、`RefundSucceededIntegrationEvent`、`RefundFailedIntegrationEvent`

## 8. 行动建议
- **立即修复**（P0 缺失/不一致）：
  - 补充 `POST /api/payments` 端点实现：源码 PaymentsController 仅有 GET 查询端点，缺失发起支付的 POST 端点。spec F-PAY-001 明确要求买家可经 `POST /api/payments` 同步发起支付（body `{ orderId, channel?, scene? }`），返回 `{ paymentOrderId, paymentNo, channel, payParams, expireAt }`。此端点缺失直接阻塞买家端 payment-initiate 页面的「确认支付」核心交易闭环，需立即在 PaymentsController 中新增 `[HttpPost("api/payments")]` 端点，编排 IPaymentAppService 发起支付用例。
- **短期补充**（P1 缺失/不匹配）：
  - 路径对齐：design-prompts payment-result 页期望 `GET /api/payments/result/{orderId}`，源码实际为 `GET /api/payments/{orderId}`。建议统一为源码路径并更新 design-prompts 文档，避免破坏现有调用。
  - 对账页面补设计稿：源码已实现 `GET /api/admin/reconciliation/diffs` 与 `POST /api/admin/reconciliation/trigger`（对应 spec F-PAY-012 渠道对账功能），但 operations 06-payment-ops 模块无对账页面。建议补充对账记录页面引用现有端点。
  - 筛选能力补齐：`GET /api/admin/payments` 补 paymentNo、orderId query 参数；`GET /api/admin/refunds` 补 refundNo、startDate、endDate query 参数，对齐 design-prompts UI 筛选条。
- **长期规划**（P2 闲置/废弃）：
  - 闲置端点保留观察：`GET /api/payments/{paymentId}/status`（补偿查询，可由 payment-result 页轮询调用）、`GET /api/refunds/{afterSalesId}`（被 BC6 页面引用，跨 BC 协调）、第三方回调端点 `POST /api/notify/wechat-pay` 与 `POST /api/notify/alipay`（渠道调用属正常无前端引用）、内部端点 `GET /internal/v1/payments/{orderId}/info`（供其他微服务调用）均建议保留，不废弃。
- **文档同步**（design-prompts API 引用对齐到源码）：
  - payment-initiate.md：`POST /api/payments` 标 ✅ 与源码不符，待源码补充后保留；或在源码决定改用事件驱动发起时更新文档说明。
  - payment-result.md：`GET /api/payments/result/{orderId}` 路径更新为 `GET /api/payments/{orderId}` 对齐源码。
  - payment-records.md：请求参数补 paymentNo、orderId 字段说明（待源码补齐后同步）。
  - refund-records.md：请求参数补 refundNo、startDate、endDate 字段说明（待源码补齐后同步）。
