# BC5 促销域 — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

## 1. 概览
- **BC 编号**：BC5
- **中文名**：促销域
- **英文名**：Promotion
- **涉及端**：buyer-app + operations
- **涉及页面数**：7 页（buyer-app 4 + operations 3，来自 feature-list）
- **已实现 API 端点数**：26 个（外部 24 + 内部 2；内部双路由期保留共 4 个路由声明）
- **差异统计**：缺失 0 / 闲置 1 / 路径不一致 2 / 能力不匹配 3

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| POST | /api/admin/coupons | [CouponsController.cs#L32](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L32) | 创建优惠券模板 | Operator,Admin |
| PUT | /api/admin/coupons/{couponId} | [CouponsController.cs#L42](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L42) | 更新优惠券模板 | Operator,Admin |
| POST | /api/admin/coupons/{couponId}/enable | [CouponsController.cs#L52](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L52) | 启用优惠券模板 | Operator,Admin |
| POST | /api/admin/coupons/{couponId}/disable | [CouponsController.cs#L62](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L62) | 停用优惠券模板 | Operator,Admin |
| POST | /api/admin/coupons/{couponId}/issue | [CouponsController.cs#L72](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L72) | 批量发放优惠券（增加发放量） | Operator,Admin |
| GET | /api/admin/coupons | [CouponsController.cs#L82](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L82) | 分页查询券模板（按状态过滤） | Operator,Admin |
| GET | /api/coupons/available | [CouponsController.cs#L94](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L94) | 查询可领券列表 | Buyer |
| POST | /api/coupons/{couponId}/receive | [CouponsController.cs#L104](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L104) | 领取优惠券 | Buyer |
| GET | /api/coupons/mine | [CouponsController.cs#L114](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L114) | 我的优惠券（按状态过滤） | Buyer |
| POST | /api/admin/seckill/activities | [SeckillController.cs#L32](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs#L32) | 创建秒杀活动（待生效态） | Operator,Admin |
| POST | /api/admin/seckill/activities/{activityId}/activate | [SeckillController.cs#L42](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs#L42) | 激活秒杀活动（初始化 Redis 多 SKU 库存） | Operator,Admin |
| POST | /api/admin/seckill/activities/{activityId}/close | [SeckillController.cs#L52](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs#L52) | 关闭秒杀活动（Redis 库存回写 DB） | Operator,Admin |
| GET | /api/admin/seckill/activities | [SeckillController.cs#L62](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs#L62) | 分页查询秒杀活动（按状态过滤） | Operator,Admin |
| GET | /api/seckill/activities | [SeckillController.cs#L74](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs#L74) | 查询进行中秒杀活动列表 | Buyer |
| GET | /api/seckill/activities/{activityId} | [SeckillController.cs#L84](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs#L84) | 秒杀活动详情（含 Redis 实时库存） | Buyer |
| POST | /api/seckill/activities/{activityId}/place | [SeckillController.cs#L98](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs#L98) | 秒杀下单（异步预扣库存） | Buyer |
| POST | /api/seckill/{activityId}/order | [SeckillController.cs#L110](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs#L110) | 秒杀下单（带 skuId 等价入口） | Buyer |
| POST | /api/admin/promotions | [PromotionsController.cs#L30](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/PromotionsController.cs#L30) | 创建满减活动 | Operator,Admin |
| PUT | /api/admin/promotions/{activityId} | [PromotionsController.cs#L39](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/PromotionsController.cs#L39) | 更新满减活动规则 | Operator,Admin |
| POST | /api/admin/promotions/{activityId}/activate | [PromotionsController.cs#L48](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/PromotionsController.cs#L48) | 激活满减活动 | Operator,Admin |
| POST | /api/admin/promotions/{activityId}/pause | [PromotionsController.cs#L57](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/PromotionsController.cs#L57) | 暂停满减活动 | Operator,Admin |
| POST | /api/admin/promotions/{activityId}/close | [PromotionsController.cs#L66](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/PromotionsController.cs#L66) | 关闭满减活动（终态） | Operator,Admin |
| GET | /api/admin/promotions/{activityId} | [PromotionsController.cs#L75](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/PromotionsController.cs#L75) | 满减活动详情 | Operator,Admin |
| GET | /api/admin/promotions | [PromotionsController.cs#L84](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/PromotionsController.cs#L84) | 分页查询满减活动（按状态过滤） | Operator,Admin |
| POST | /internal/v1/promotions/calculate（内部） | [InternalPromotionsController.cs#L28](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs#L28) | 试算用户订单可用优惠总金额 | 内部（X-Internal-Key） |
| POST | /internal/promotions/calculate（内部） | [InternalPromotionsController.cs#L30](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs#L30) | 试算优惠总金额（双路由期保留，2026-09-15 下线） | 内部（X-Internal-Key） |
| POST | /internal/v1/promotions/lock-coupon（内部） | [InternalPromotionsController.cs#L42](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs#L42) | 下单锁定优惠券（Unused→Locked 绑定 orderId） | 内部（X-Internal-Key） |
| POST | /internal/promotions/lock-coupon（内部） | [InternalPromotionsController.cs#L44](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/InternalPromotionsController.cs#L44) | 锁定优惠券（双路由期保留，2026-09-15 下线） | 内部（X-Internal-Key） |

> 来源：grep `src/Services/Promotion/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> Internal*Controller.cs 中的端点单独标注「（内部）」，由 `InternalApiKeyMiddleware` 校验 `X-Internal-Key` 请求头，仅供订单域等服务间调用
> InternalPromotionsController 每个方法同时声明双路由（v1 与无版本前缀），属双路由期过渡保留，将于 2026-09-15 下线
> PromotionsController 在控制器级声明 `[Route("api/admin/promotions")]`，方法级路径为相对路径，实际拼接为 `/api/admin/promotions{...}`

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| GET | /api/seckill/activities | [seckill-entry.md](file:///e:/Leno/docs/design-prompts/buyer-app/02-home/seckill-entry.md) | 查询进行中秒杀活动列表 | ✅ | Buyer |
| GET | /api/seckill/activities/{activityId} | [seckill-entry.md](file:///e:/Leno/docs/design-prompts/buyer-app/02-home/seckill-entry.md), [seckill-order.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/seckill-order.md) | 秒杀活动详情（含 Redis 实时库存） | ✅ | Buyer |
| POST | /api/seckill/activities/{activityId}/place | [seckill-order.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/seckill-order.md) | 秒杀下单（异步预扣库存） | ✅ | Buyer |
| GET | /api/coupons/available | [coupons-available.md](file:///e:/Leno/docs/design-prompts/buyer-app/08-promotion/coupons-available.md) | 查询可领取优惠券列表 | ✅ | Buyer |
| POST | /api/coupons/{couponId}/receive | [coupons-available.md](file:///e:/Leno/docs/design-prompts/buyer-app/08-promotion/coupons-available.md) | 领取优惠券 | ✅ | Buyer |
| GET | /api/coupons/mine | [my-coupons.md](file:///e:/Leno/docs/design-prompts/buyer-app/08-promotion/my-coupons.md) | 我的优惠券（按状态过滤） | ✅ | Buyer |
| GET | /api/admin/promotions | [promotions.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/promotions.md) | 分页查询满减活动（按状态过滤） | ✅ | Operator,Admin |
| GET | /api/admin/promotions/{activityId} | [promotions.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/promotions.md) | 查询满减活动详情 | ✅ | Operator,Admin |
| POST | /api/admin/promotions | [promotions.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/promotions.md) | 创建满减活动 | ✅ | Operator,Admin |
| PUT | /api/admin/promotions/{activityId} | [promotions.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/promotions.md) | 更新满减活动规则 | ✅ | Operator,Admin |
| POST | /api/admin/promotions/{activityId}/activate | [promotions.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/promotions.md) | 激活满减活动 | ✅ | Operator,Admin |
| POST | /api/admin/promotions/{activityId}/pause | [promotions.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/promotions.md) | 暂停满减活动 | ✅ | Operator,Admin |
| POST | /api/admin/promotions/{activityId}/close | [promotions.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/promotions.md) | 关闭满减活动（终态） | ✅ | Operator,Admin |
| GET | /api/admin/coupons | [coupons.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/coupons.md) | 分页查询券模板（按状态过滤） | ✅ | Operator,Admin |
| POST | /api/admin/coupons | [coupons.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/coupons.md) | 创建券模板 | ✅ | Operator,Admin |
| PUT | /api/admin/coupons/{couponId} | [coupons.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/coupons.md) | 更新券模板 | ✅ | Operator,Admin |
| POST | /api/admin/coupons/{couponId}/publish | [coupons.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/coupons.md) | 发布券模板（启用） | 🚧 | Operator,Admin |
| POST | /api/admin/coupons/{couponId}/stop | [coupons.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/coupons.md) | 停用券模板 | 🚧 | Operator,Admin |
| POST | /api/admin/coupons/{couponId}/issue | [coupons.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/coupons.md) | 批量发放优惠券（增加发放量） | ✅ | Operator,Admin |
| GET | /api/admin/seckill/activities | [seckill.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/seckill.md) | 分页查询秒杀活动（按状态过滤） | ✅ | Operator,Admin |
| POST | /api/admin/seckill/activities | [seckill.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/seckill.md) | 创建秒杀活动（待生效态） | ✅ | Operator,Admin |
| POST | /api/admin/seckill/activities/{activityId}/activate | [seckill.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/seckill.md) | 激活秒杀活动（初始化 Redis 库存） | ✅ | Operator,Admin |
| POST | /api/admin/seckill/activities/{activityId}/close | [seckill.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/seckill.md) | 关闭秒杀活动（Redis 库存回写 DB） | ✅ | Operator,Admin |

> 来源：design-prompts 的「3. 数据模型与 API 对接」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）
> feature-list 中 BC5 相关 7 个页面整体均为 ✅ 已实现；coupons.md 中的 /publish 与 /stop 端点未在源码以同名路径实现，源码实际为 /enable 与 /disable（详见 4.3）
> seckill-order.md 中 GET /api/addresses 归 BC1，不计入本 BC 期望端点

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现
> 本 BC5 所有相关页面在 feature-list 中均为 ✅ 已实现，未出现页面级 🚧/➕ 标记
> coupons.md 中 /publish、/stop 标 🚧 但源码已提供等价启停能力（/enable、/disable），仅路径不一致，归入 4.3 而非 4.1

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| POST | /api/seckill/{activityId}/order | [SeckillController.cs#L110](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs#L110) | 秒杀下单（带 skuId 的等价入口，与 /place 同方法实现） | 保留观察：评估是否作为多 SKU 场景的标准入口；若保留则在 design-prompts 补充引用，否则后端废弃 |

> 说明：源码有实现但 design-prompts 中无任何页面引用
> 内部接口（/internal/v1/promotions/calculate、/internal/v1/promotions/lock-coupon 及双路由）属服务间调用，design-prompts 不应引用，不计入闲置

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|
| POST→POST | /api/admin/coupons/{couponId}/publish → /api/admin/coupons/{couponId}/enable | [coupons.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/coupons.md) | [CouponsController.cs#L52](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L52) | 二选一：建议改 design-prompts 文档（/publish→/enable）以对齐当前源码；或改源码路由以贴合「发布」语义 |
| POST→POST | /api/admin/coupons/{couponId}/stop → /api/admin/coupons/{couponId}/disable | [coupons.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/coupons.md) | [CouponsController.cs#L62](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L62) | 二选一：建议改 design-prompts 文档（/stop→/disable）以对齐当前源码；或改源码路由以贴合「停用」语义 |

> 说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配
> 源码方法名 EnableAsync/DisableAsync 与路径 /enable、/disable 一致；design-prompts 使用 /publish、/stop 命名，语义相近但路径字面不同
> 若前端按 design-prompts 调用 /publish 或 /stop 将返回 404，需优先对齐

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 按名称关键词、状态、时间范围筛选 + 分页 | 仅按 status + page + pageSize 筛选 + 分页 | 缺少名称关键词（name）与时间范围（startTime/endTime）筛选 | [promotions.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/promotions.md) | [PromotionsController.cs#L84](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/PromotionsController.cs#L84) | 补 name、startTime、endTime 查询参数到 QueryAsync |
| 按券名称关键词、状态、券类型筛选 + 分页 | 仅按 status + page + pageSize 筛选 + 分页 | 缺少名称关键词（name）与券类型（type）筛选 | [coupons.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/coupons.md) | [CouponsController.cs#L82](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L82) | 补 name、type 查询参数到 QueryAsync |
| 按活动名称关键词、状态筛选 + 分页 | 仅按 status + page + pageSize 筛选 + 分页 | 缺少名称关键词（name）筛选 | [seckill.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/seckill.md) | [SeckillController.cs#L62](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs#L62) | 补 name 查询参数到 QueryAsync |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异
> 三个运营后台列表接口均仅支持状态+分页，design-prompts 的筛选条设计要求更丰富的筛选条件，影响运营操作体验但不阻塞功能闭环

## 5. 拆分过渡说明

本 BC 无拆分过渡。

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | - | - | - | - |
| P1 | - | - | /publish→/enable（coupons.md）；/stop→/disable（coupons.md） | - |
| P2 | - | POST /api/seckill/{activityId}/order | - | /api/admin/promotions 筛选缺失；/api/admin/coupons 筛选缺失；/api/admin/seckill/activities 筛选缺失 |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强
> 路径不一致列为 P1：若前端按 design-prompts 调用 /publish、/stop 会 404，影响运营启停券模板流程
> 列表筛选缺失列为 P2：状态+分页基本可用，名称/类型/时间范围筛选缺失影响运营效率但不阻塞功能

## 7. 跨 BC 依赖

- **上游依赖**（本 BC 依赖哪些 BC 的端点/事件）：
  - **BC2 商品域**：秒杀活动以 SKU ID 引用商品；秒杀商品的原价由商品域权威持有，活动库存独立配置
  - **BC1 用户域**：优惠券领用人、秒杀下单人均以用户 ID 引用，本域不持有用户聚合，仅校验身份与归属
  - **BC9 消息通知域**：秒杀成功、优惠券到账、优惠券即将过期等通知经 BC9 的 `INotificationService` 发送，本域不直接持有邮件/短信渠道客户端
  - **BC4 订单域**（事件订阅）：订阅 `OrderPaidEvent`（触发券核销与满额赠判断）、`RefundCompletedEvent`（触发券退还）、`OrderCancelledEvent`（触发券预占释放）
  - **BC7 积分与会员域**（事件订阅）：订阅 `PointsExchangeCouponRequestedEvent`（触发积分换券，校验券模板与库存后创建用户券实例）

- **下游依赖**（哪些 BC 依赖本 BC 的端点/事件）：
  - **BC4 订单域**：消费 `SeckillOrderCreatedEvent` 完成秒杀异步建单落单；结算与下单时作为下游消费者调用促销域应用服务（试算优惠、绑定券、内部接口 `/internal/v1/promotions/calculate`、`/internal/v1/promotions/lock-coupon`）
  - **BC7 积分与会员域**：消费 `CouponExchangeSucceededEvent`（扣减积分）、`CouponExchangeFailedEvent`（不扣减积分）
  - **BC9 消息通知域**：消费 `CouponClaimedEvent`（券到账通知）、`CouponExpiredEvent`（过期提醒）、`SeckillOrderCreatedEvent`（秒杀成功通知）
  - **BC1 用户域 / 分析域**：消费 `CouponIssuedEvent`（券批次发布通知与分析）

- **集成事件订阅/发布清单**：
  - **订阅（入站）**：
    - `OrderPaidEvent`（BC4 订单域）→ 券核销 + 满额赠判断
    - `RefundCompletedEvent`（BC4 订单域）→ 券退还
    - `OrderCancelledEvent`（BC4 订单域）→ 券预占释放
    - `PointsExchangeCouponRequestedEvent`（BC7 积分与会员域）→ 积分换券
  - **发布（出站）**：
    - `CouponIssuedEvent`（券批次创建并发布）→ BC1/分析
    - `CouponClaimedEvent`（用户领券成功）→ BC9/分析
    - `CouponUsedEvent`（支付成功核销券）→ 促销域内部
    - `CouponExpiredEvent`（券超期过期）→ BC9/促销域内部
    - `CouponRevertedEvent`（退款退还券）→ 促销域内部
    - `CouponExchangeSucceededEvent`（积分换券成功）→ BC7
    - `CouponExchangeFailedEvent`（积分换券失败）→ BC7
    - `SeckillStockPreDeductedEvent`（秒杀 Redis 预减库存成功）→ 秒杀建单消费者
    - `SeckillOrderCreatedEvent`（秒杀订单异步创建成功）→ BC4/库存/BC9
    - `SeckillStockRevertedEvent`（秒杀建单失败或超时回补库存）→ 促销域内部

## 8. 行动建议

- **立即修复**（P0 缺失/不一致）：无 P0 项
- **短期补充**（P1 缺失/不匹配）：
  - 对齐优惠券启停路径：二选一执行
    - 方案 A（推荐，改动小）：更新 [coupons.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/coupons.md) 第 3 节 API 表的 /publish → /enable、/stop → /disable，与 [CouponsController.cs#L52](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L52) 和 [CouponsController.cs#L62](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/CouponsController.cs#L62) 当前实现对齐
    - 方案 B：在 CouponsController 中将 /enable、/disable 路由改名为 /publish、/stop，并同步方法名（影响范围更大，需评估已上线客户端兼容性）
- **长期规划**（P2 闲置/废弃）：
  - 列表筛选能力补齐：为 `PromotionsController.QueryAsync`、`CouponsController.QueryAsync`、`SeckillController.QueryAsync` 增加 name（promotions/seckill/coupons）、type（coupons）、startTime/endTime（promotions）查询参数，对齐 design-prompts 筛选条
  - 评估 [SeckillController.cs#L110](file:///e:/Leno/src/Services/Promotion/Leno.Promotion.Api/Controllers/SeckillController.cs#L110) 的 POST /api/seckill/{activityId}/order 是否保留：该端点与 /place 共用 `PlaceOrderAsync` 实现，功能等价。若保留则在 [seckill-order.md](file:///e:/Leno/docs/design-prompts/buyer-app/06-order/seckill-order.md) 中补充引用说明多 SKU 入口；若不再需要则后端废弃
- **文档同步**（design-prompts API 引用对齐到源码）：
  - [coupons.md](file:///e:/Leno/docs/design-prompts/operations/03-promotion-ops/coupons.md) 第 3 节：/publish → /enable、/stop → /disable（与 CouponsController 实际路由对齐）
  - 三个运营列表接口（promotions.md、coupons.md、seckill.md）补充说明当前仅支持 status+分页，name/type/时间范围筛选为待补能力
