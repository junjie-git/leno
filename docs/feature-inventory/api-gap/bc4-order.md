# BC4 订单与交易域 — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

## 1. 概览
- **BC 编号**：BC4
- **中文名**：订单与交易域
- **英文名**：Order
- **涉及端**：buyer-app / operations / seller
- **涉及页面数**：12 页（来自 feature-list）
  - buyer-app 06-order：5 页（order-create、order-list、order-detail、logistics-trace、seckill-order）
  - operations 05-order-ops：2 页（order-management、logistics-companies）
  - seller 04-logistics：2 页（freight-templates、logistics-companies）
  - seller 05-order-fulfillment：3 页（pending-shipment、order-list、logistics-trace）
- **已实现 API 端点数**：24 个（其中 1 个内部端点含废弃双路由别名，2026-08-15 下线）
- **差异统计**：缺失 2 / 闲置 4 / 路径不一致 1 / 能力不匹配 2

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| POST | /api/orders | [OrdersController.cs#L46](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L46) | 创建订单（按卖家自动拆单） | Buyer |
| POST | /api/orders/buy-now | [OrdersController.cs#L56](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L56) | 立即购买（单 SKU 下单） | Buyer |
| POST | /api/orders/preview | [OrdersController.cs#L66](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L66) | 下单预览，计算预估金额不落库 | Buyer |
| GET | /api/orders | [OrdersController.cs#L76](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L76) | 分页查询当前用户订单（按状态过滤） | Buyer |
| GET | /api/orders/{id:guid} | [OrdersController.cs#L93](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L93) | 获取订单详情 | Buyer |
| POST | /api/orders/{id:guid}/confirm | [OrdersController.cs#L108](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L108) | 确认收货 | Buyer |
| POST | /api/orders/{id:guid}/cancel | [OrdersController.cs#L118](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L118) | 买家取消订单（待支付态） | Buyer |
| POST | /api/seller/orders/{id:guid}/ship | [OrdersController.cs#L130](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L130) | 卖家发货 | Seller |
| GET | /api/admin/orders | [OrdersController.cs#L142](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L142) | 分页查询全部订单 | Operator, Admin |
| POST | /api/admin/orders/{id:guid}/force-cancel | [OrdersController.cs#L160](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L160) | 运营强制取消订单 | Admin |
| POST | /api/seller/freight-templates | [FreightTemplatesController.cs#L37](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/FreightTemplatesController.cs#L37) | 创建运费模板（含区域规则） | Seller, Admin |
| PUT | /api/seller/freight-templates/{id:guid}/rules | [FreightTemplatesController.cs#L47](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/FreightTemplatesController.cs#L47) | 更新运费模板区域规则（整体替换） | Seller, Admin |
| POST | /api/seller/freight-templates/{id:guid}/enable | [FreightTemplatesController.cs#L57](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/FreightTemplatesController.cs#L57) | 启用运费模板 | Seller, Admin |
| POST | /api/seller/freight-templates/{id:guid}/disable | [FreightTemplatesController.cs#L67](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/FreightTemplatesController.cs#L67) | 停用运费模板 | Seller, Admin |
| GET | /api/seller/freight-templates | [FreightTemplatesController.cs#L77](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/FreightTemplatesController.cs#L77) | 分页查询运费模板列表 | Seller, Admin |
| GET | /api/seller/freight-templates/mine | [FreightTemplatesController.cs#L87](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/FreightTemplatesController.cs#L87) | 查询当前卖家运费模板 | Seller, Admin |
| GET | /api/orders/{id:guid}/logistics-trace | [FreightTemplatesController.cs#L99](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/FreightTemplatesController.cs#L99) | 查询订单物流轨迹 | Buyer, Seller, Admin |
| POST | /api/payments | [PaymentsController.cs#L28](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/PaymentsController.cs#L28) | 发起支付（query 传 orderId，发布 PaymentRequestedIntegrationEvent 至 BC8） | Buyer |
| POST | /api/admin/logistics-companies | [LogisticsCompaniesController.cs#L28](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/LogisticsCompaniesController.cs#L28) | 创建物流公司 | Operator, Admin |
| PUT | /api/admin/logistics-companies/{id:guid} | [LogisticsCompaniesController.cs#L38](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/LogisticsCompaniesController.cs#L38) | 更新物流公司可编辑字段 | Operator, Admin |
| POST | /api/admin/logistics-companies/{id:guid}/enable | [LogisticsCompaniesController.cs#L48](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/LogisticsCompaniesController.cs#L48) | 启用物流公司 | Operator, Admin |
| POST | /api/admin/logistics-companies/{id:guid}/disable | [LogisticsCompaniesController.cs#L58](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/LogisticsCompaniesController.cs#L58) | 停用物流公司 | Operator, Admin |
| GET | /api/admin/logistics-companies | [LogisticsCompaniesController.cs#L68](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/LogisticsCompaniesController.cs#L68) | 分页查询物流公司列表 | Operator, Admin |
| GET | /internal/v1/orders/{orderId:guid}/status | [InternalOrdersController.cs#L22](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/InternalOrdersController.cs#L22) | 查询订单状态（内部，受 InternalApiKeyMiddleware 保护） | （内部） |
| GET | /internal/orders/{orderId:guid}/status | [InternalOrdersController.cs#L24](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/InternalOrdersController.cs#L24) | 旧路由别名（内部，[Obsolete]，2026-08-15 下线） | （内部） |

> 来源：grep `src/Services/Order/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> Internal*Controller.cs 中的端点单独标注「（内部）」

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| POST | /api/orders/buy-now | [order-create.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/order-create.md) | 立即购买创建订单 | ✅ | Buyer |
| GET | /api/orders | [order-list.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/order-list.md) | 分页查询订单（按状态过滤） | ✅ | Buyer |
| POST | /api/orders/{id}/cancel | [order-list.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/order-list.md) | 取消订单（待支付态） | ✅ | Buyer |
| POST | /api/orders/{id}/confirm | [order-list.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/order-list.md) | 确认收货 | ✅ | Buyer |
| GET | /api/orders/{id} | [order-detail.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/order-detail.md) | 查询订单详情 | ✅ | Buyer |
| POST | /api/orders/{id}/cancel | [order-detail.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/order-detail.md) | 取消订单 | ✅ | Buyer |
| POST | /api/orders/{id}/confirm | [order-detail.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/order-detail.md) | 确认收货 | ✅ | Buyer |
| POST | /api/payments?orderId={id} | [order-detail.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/order-detail.md) | 发起支付 | ✅ | Buyer |
| GET | /api/orders/{id} | [logistics-trace.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/logistics-trace.md) | 查询订单详情（含物流单号与公司编码） | ✅ | Buyer |
| GET | /api/orders/{id}/logistics | [logistics-trace.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/logistics-trace.md) | 查询物流轨迹节点 | 🚧 路径不一致（源码为 logistics-trace） | Buyer |
| GET | /api/admin/orders | [order-management.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/order-management.md) | 分页查询全部订单 | ✅ | Operator, Admin |
| POST | /api/admin/orders/{id}/force-cancel | [order-management.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/order-management.md) | 运营强制取消订单 | ✅ | Admin |
| GET | /api/admin/logistics-companies | [logistics-companies.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/logistics-companies.md) | 分页查询物流公司列表 | ✅ | Operator, Admin |
| POST | /api/admin/logistics-companies | [logistics-companies.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/logistics-companies.md) | 创建物流公司 | ✅ | Operator, Admin |
| PUT | /api/admin/logistics-companies/{id} | [logistics-companies.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/logistics-companies.md) | 更新物流公司 | ✅ | Operator, Admin |
| POST | /api/admin/logistics-companies/{id}/enable | [logistics-companies.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/logistics-companies.md) | 启用物流公司 | ✅ | Operator, Admin |
| POST | /api/admin/logistics-companies/{id}/disable | [logistics-companies.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/logistics-companies.md) | 停用物流公司 | ✅ | Operator, Admin |
| GET | /api/seller/freight-templates/mine | [freight-templates.md](file:///e:/Leno/docs/design-prompts/seller/04-logistics/freight-templates.md) | 查询当前卖家运费模板 | ✅ | Seller |
| POST | /api/seller/freight-templates | [freight-templates.md](file:///e:/Leno/docs/design-prompts/seller/04-logistics/freight-templates.md) | 创建运费模板 | ✅ | Seller |
| PUT | /api/seller/freight-templates/{id}/rules | [freight-templates.md](file:///e:/Leno/docs/design-prompts/seller/04-logistics/freight-templates.md) | 更新模板区域规则 | ✅ | Seller |
| POST | /api/seller/freight-templates/{id}/enable | [freight-templates.md](file:///e:/Leno/docs/design-prompts/seller/04-logistics/freight-templates.md) | 启用模板 | ✅ | Seller |
| POST | /api/seller/freight-templates/{id}/disable | [freight-templates.md](file:///e:/Leno/docs/design-prompts/seller/04-logistics/freight-templates.md) | 停用模板 | ✅ | Seller |
| GET | /api/admin/logistics-companies（卖家只读访问） | [logistics-companies.md](file:///e:/Leno/docs/design-prompts/seller/04-logistics/logistics-companies.md) | 卖家查询平台物流公司（需新增 /api/seller/logistics-companies 或开放 Seller 角色） | ➕ | Seller（补充） |
| POST | /api/seller/orders/{id}/ship | [pending-shipment.md](file:///e:/Leno/docs/design-prompts/seller/05-order-fulfillment/pending-shipment.md) | 卖家发货 | ✅ | Seller |
| GET | /api/seller/orders | [order-list.md](file:///e:/Leno/docs/design-prompts/seller/05-order-fulfillment/order-list.md) | 卖家查询本店订单（补充端点） | ➕ | Seller（补充） |
| GET | /api/seller/orders?status=Paid | [pending-shipment.md](file:///e:/Leno/docs/design-prompts/seller/05-order-fulfillment/pending-shipment.md) | 卖家查询待发货订单（同 /api/seller/orders + status 过滤） | ➕ | Seller（补充） |
| GET | /api/orders/{id}/logistics-trace | [logistics-trace.md](file:///e:/Leno/docs/design-prompts/seller/05-order-fulfillment/logistics-trace.md) | 查询订单物流轨迹 | ✅ | Seller |

> 来源：design-prompts 的「数据与 API 对接」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|
| GET | /api/seller/orders | [order-list.md](file:///e:/Leno/docs/design-prompts/seller/05-order-fulfillment/order-list.md) | 卖家查询本店订单（按状态/时间/订单号筛选） | P0 | 在 OrdersController 新增 `[Authorize(Roles="Seller")] [HttpGet("api/seller/orders")]`，复用 OrderListQuery（已支持 SellerId/Status/StartDate/EndDate），SellerId 从 JWT 注入；并扩展 OrderListQuery 支持 OrderNo 字段 |
| GET | /api/seller/logistics-companies | [logistics-companies.md](file:///e:/Leno/docs/design-prompts/seller/04-logistics/logistics-companies.md) | 卖家查询启用态物流公司（仅返回启用项供发货选择） | P1 | 在 LogisticsCompaniesController 新增 `[Authorize(Roles="Seller,Operator,Admin")] [HttpGet("api/seller/logistics-companies")]`，仅返回 Status=Active 的字段子集；或在现有 /api/admin/logistics-companies 端点开放 Seller 角色只读权限（推荐新增专用端点以隔离写权限） |

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| POST | /api/orders | [OrdersController.cs#L46](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L46) | 创建订单（按卖家拆单） | 保留观察（在 buyer-app/05-cart/checkout-settle 页面已引用，本 BC4 06-order 范围未直接调用，购物车下单走 /api/orders/buy-now） |
| POST | /api/orders/preview | [OrdersController.cs#L66](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L66) | 下单预览（不落库） | 保留观察（在 buyer-app/05-cart/checkout-preview、checkout-settle 页面已引用，本 BC4 06-order 范围未直接调用） |
| GET | /api/seller/freight-templates | [FreightTemplatesController.cs#L77](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/FreightTemplatesController.cs#L77) | 分页查询运费模板列表 | 保留观察（设计稿 freight-templates 页面仅引用 /mine 单模板查询，列表端点未被调用；可作为管理端扩展能力备用） |
| GET | /internal/v1/orders/{orderId}/status | [InternalOrdersController.cs#L22](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/InternalOrdersController.cs#L22) | 内部查询订单状态（供其他微服务调用） | 保留观察（内部端点，design-prompts 不直接引用；同时存在 L24 旧路由别名为废弃双轨，2026-08-15 下线） |

> 说明：源码有实现但 design-prompts 中无任何页面引用

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|
| GET→GET | /api/orders/{id}/logistics → /api/orders/{id}/logistics-trace | [logistics-trace.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/logistics-trace.md) | [FreightTemplatesController.cs#L99](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/FreightTemplatesController.cs#L99) | 改文档对齐源码（推荐）；卖家端 logistics-trace.md 已使用 /logistics-trace 路径，统一买家端 design-prompts 为 /api/orders/{id}/logistics-trace |

> 说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 按订单号、买家 ID、卖家 ID、状态、时间范围组合筛选 | 仅支持 userId、sellerId、status、page、pageSize | 缺少订单号搜索（orderNo）与下单时间范围筛选 | [order-management.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/order-management.md) | [OrdersController.cs#L142](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L142) | 扩展 OrderListQuery 增加 OrderNo、StartDate、EndDate 字段，并在 Controller 增加 [FromQuery] 绑定；ES 读模型索引同步覆盖 orderNo 与 createdAt 范围查询 |
| 按公司名称关键词与状态筛选（启用/停用） | 仅支持 page、pageSize 分页 | 缺少关键词搜索（name/code）与状态过滤（Active/Inactive） | [logistics-companies.md](file:///e:/Leno/docs/design-prompts/operations/05-order-ops/logistics-companies.md) | [LogisticsCompaniesController.cs#L68](file:///e:/Leno/src/Services/Order/Leno.Order.Api/Controllers/LogisticsCompaniesController.cs#L68) | 在 ListAsync 增加 [FromQuery] keyword、[FromQuery] LogisticsCompanyStatus? status 参数；LogisticsCompanyAppService.ListAsync 签名扩展支持过滤 |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异

## 5. 拆分过渡说明

本 BC 无拆分过渡。

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | GET /api/seller/orders（卖家订单列表，阻塞卖家履约闭环） | — | — | — |
| P1 | GET /api/seller/logistics-companies（卖家查询物流公司，影响发货体验） | — | GET /api/orders/{id}/logistics → /logistics-trace（买家物流轨迹页路径不一致） | GET /api/admin/orders 缺订单号/时间范围筛选；GET /api/admin/logistics-companies 缺关键词/状态筛选 |
| P2 | — | POST /api/orders、POST /api/orders/preview、GET /api/seller/freight-templates、GET /internal/v1/orders/{orderId}/status | — | — |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强

## 7. 跨 BC 依赖

- **上游依赖**（本 BC 依赖哪些 BC 的端点/事件）：
  - **BC1 用户域**：下单瞬间从用户域 `Address` 聚合拷贝地址快照为不可变值对象；下单接口隐式依赖买家身份（JWT 注入 UserId）。
  - **BC2 商品域**：下单瞬间从商品域拷贝 SKU 名称、规格、图片、价格为订单行商品快照；库存预占在 BC4 Redis 侧执行，BC2 订阅本域库存事件同步可售库存基线。
  - **BC3 购物车域**：购物车选中项转化为订单行快照；本域发布 `OrderCreatedEvent` 后购物车域消费清空已结算项。
  - **BC5 促销域**：结算时本域调用促销域获取适用优惠（满减、优惠券），优惠金额与分摊在订单创建瞬间固化。
  - **BC8 支付集成域**：本域表现层 `POST /api/payments` 内部转发为发布 `PaymentRequestedIntegrationEvent` 请求发起支付；本域消费 BC8 的 `PaymentSucceededIntegrationEvent`（驱动 `Order.MarkAsPaid` 并产出 `OrderPaidEvent`）与 `PaymentFailedIntegrationEvent`（关单或保持待支付）。
- **下游依赖**（哪些 BC 依赖本 BC 的端点/事件）：
  - **BC2 商品域**：订阅本域库存事件同步可售库存基线。
  - **BC3 购物车域**：消费 `OrderCreatedEvent` 清空已结算项。
  - **BC5 促销域**：消费 `OrderPaidEvent` 核销优惠券。
  - **BC6 评价与售后域**：消费 `OrderCompletedEvent` 开放评价入口；本域消费 BC6 的 `RefundCompletedEvent` 回滚销量与库存并按规则退还库存与优惠券。
  - **BC7 积分域**：消费 `OrderPaidEvent` 正式扣减抵现冻结积分；售后期结束事件驱动积分发放。
  - **BC9 消息通知域**：消费 `OrderPaidEvent` 触发支付成功通知、消费 `OrderShippedEvent` 触发发货通知、消费 `OrderCompletedEvent` 触发完成通知。
  - **BC10 卖家域**：消费 `OrderPaidEvent` 通知卖家发货。
  - **BC8 支付集成域**：消费本域 `PaymentRequestedIntegrationEvent` 创建支付单对接渠道；订单取消时本域经事件或防腐层通知 BC8 关单。
  - **ES 读库**：订阅本域事件同步订单读模型（OrderListQuery/OrderDetailQuery 走 ES）。
- **集成事件订阅/发布清单**：
  - **本域发布**：`OrderCreatedEvent`、`OrderPaidEvent`、`OrderShippedEvent`、`OrderCompletedEvent`、`OrderCancelledEvent`、`PaymentRequestedIntegrationEvent`、库存预占/扣减/释放事件。
  - **本域消费（入站）**：`PaymentSucceededIntegrationEvent`（BC8）、`PaymentFailedIntegrationEvent`（BC8）、`RefundCompletedEvent`（BC6 经 BC8）。
  - **内部 API**：`GET /internal/v1/orders/{orderId}/status`（受 InternalApiKeyMiddleware 保护，供 BC8/BC6 等微服务查询订单状态）。

## 8. 行动建议

- **立即修复**（P0 缺失/不一致）：
  - 在 OrdersController 新增 `GET /api/seller/orders` 端点，鉴权 Seller，复用 `OrderListQuery`（已支持 SellerId/Status/StartDate/EndDate 字段），SellerId 从 JWT 注入；同步扩展 `OrderListQuery` 增加 OrderNo 字段以支持订单号搜索。这是卖家履约闭环（待发货 → 发货 → 物流轨迹）的入口，缺失将阻塞卖家后台订单管理。
- **短期补充**（P1 缺失/不匹配）：
  - 新增 `GET /api/seller/logistics-companies` 卖家只读端点，仅返回启用态公司清单，避免卖家发货时无法选择物流公司。
  - 同步 design-prompts：将 buyer-app/06-order/logistics-trace.md 的 `GET /api/orders/{id}/logistics` 路径对齐到源码 `GET /api/orders/{id}/logistics-trace`，与 seller/05-order-fulfillment/logistics-trace.md 保持一致。
  - 扩展 `GET /api/admin/orders` 增加 OrderNo、StartDate、EndDate 查询参数，并同步 ES 读模型索引覆盖；扩展 `GET /api/admin/logistics-companies` 增加 keyword、status 过滤参数。
- **长期规划**（P2 闲置/废弃）：
  - 监控 `POST /api/orders`、`POST /api/orders/preview`、`GET /api/seller/freight-templates` 在 06-order 范围外的引用情况，按需保留或废弃。
  - 跟踪 `InternalOrdersController` 旧路由 `internal/orders/{orderId}/status` 的下线进度（issue: order-bc/internal-route-deprecation-2026-08，2026-08-15 下线），届时移除 L24 废弃 [HttpGet] 特性。
- **文档同步**（design-prompts API 引用对齐到源码）：
  - 修正 buyer-app/06-order/logistics-trace.md 中 `GET /api/orders/{id}/logistics` 为 `GET /api/orders/{id}/logistics-trace`。
  - 在 buyer-app/06-order/order-detail.md 与 order-list.md 中补充幂等键（`Idempotency-Key`）请求头约定，与源码 `IdempotencyButton` 行为对齐。
  - 在 operations/05-order-ops/order-management.md 与 logistics-companies.md 中将查询参数更新为 `orderNo`、`startDate`、`endDate`、`keyword`、`status`，与扩展后的源码签名对齐。
