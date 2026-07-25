# 统一术语表

**文档版本**：V1.0
**适用范围**：4 端所有页面提示词
**最后更新**：2026-07-26
**来源**：`docs/spec/00-需求文档总览与DDD架构.md` 第 3.4 节 + 项目实际 BC 划分

本文件是 4 端提示词的术语唯一来源。所有 subagent 必须使用以下术语，不得使用禁用同义词。

---

## 1. 跨上下文核心术语

| 术语 | 英文 | 定义 |
|-|-|-|
| 标准化产品单元 | SPU | 一类商品的标准化抽象，如"某品牌某型号手机" |
| 库存量单位 | SKU | SPU 下可售卖的最小规格单元，如"黑色 256G 版" |
| 聚合根 | Aggregate Root | 聚合的对外入口，唯一持有外部引用权 |
| 领域事件 | Domain Event | 上下文内部已发生的重要业务事实 |
| 集成事件 | Integration Event | 跨上下文传递的事件，经事件总线发布 |
| 预占库存 | Pre-occupied Stock | 下单时锁定但未真实扣减的库存 |
| 真实库存 | Physical Stock | 实际可售库存，支付成功后扣减 |
| 积分 | Points | 平台内可赚可花的虚拟权益，100 积分 = 1 元 |
| 成长值 | Growth Value | 仅用于会员等级评定的不可消耗指标 |
| 会员等级 | Member Level | 基于近 12 个月成长值的免费等级 V0–V4 |
| 付费会员 | Paid Member | 年费制高级身份，与免费等级并行、权益叠加 |
| 支付单 | Payment Order | 支付集成域对接渠道的独立单据，与订单一对多或一对一 |
| 店铺 | Shop | 卖家在平台上的经营主体，由入驻审核通过后创建 |
| 卖家账号 | Seller Account | 用户域中具备卖家角色的用户身份，以 UserId 标识 |
| 入驻申请 | Seller Application | 卖家提交的经营资质申请单，审核通过后生成店铺 |
| 店铺标识 | ShopId | 店铺唯一标识，商品域与订单域以此引用店铺归属 |
| 资质 | Qualification | 卖家经营所需的证照材料，如营业执照、特许经营证 |
| 看板报表 | DashboardReport | 按周期与维度聚合的运营指标只读快照 |
| 死信消息 | DeadLetterMessage | 各域事件总线消费失败进入死信队列的消息 |
| 索引重建任务 | IndexRebuildTask | 触发并跟踪某域 ES 读库全量重建的任务 |
| 审计日志条目 | AuditLogEntry | 管理员关键操作的不可篡改记录 |
| 限流规则 | RateLimitRule | 针对某 API 的限流阈值与算法配置 |

---

## 2. BC 缩写与全称

| 缩写 | 全称 | 中文名称 |
|-|-|-|
| BC1 | UserAuth | 用户与认证授权域 |
| BC2 | Product | 商品域 |
| BC3 | Cart | 购物车域 |
| BC4 | Order | 订单与交易域 |
| BC5 | Promotion | 促销域 |
| BC6 | ReviewAfterSales（旧） | 评价与售后域（双轨期遗留） |
| BC7 | Payment | 支付集成域 |
| BC8 | PointsMembership（旧） | 积分与会员域（双轨期遗留） |
| BC9 | Inventory | 库存域 |
| BC10 | Notification | 消息通知域 |
| BC11 | SellerShop | 卖家与店铺管理域 |
| BC12 | SystemAdmin | 系统管理域 |

**新拆分 BC（双轨期）**：
| 缩写 | 全称 | 中文名称 | 状态 |
|-|-|-|-|
| BC6a | Review（新） | 评价域 | 仅 Program.cs 占位 |
| BC6b | AfterSales（新） | 售后域 | 仅 Program.cs 占位 |
| BC8a | Points（新） | 积分域 | 仅 Program.cs 占位 |
| BC8b | Membership（新） | 会员域 | 已实现 2 控制器 9 端点 |

---

## 3. 角色术语

| 术语 | 英文 | 首次出现 | 后续简称 | 禁用同义词 |
|-|-|-|-|-|
| 买家 | Buyer | 买家 | 买家 | 用户（指代消费者时）、消费者、customer |
| 卖家 | Seller | 卖家 | 卖家 | 商户、商家、vendor、merchant |
| 运营管理员 | Operator | 运营管理员 | 运营 | 运营人员、运营专员、ops |
| 系统管理员 | Admin | 系统管理员 | Admin（代码语境） | 管理员、超级管理员、superuser |

**说明**：
- 首次出现使用全称，后续可使用简称
- 代码语境（如角色枚举、API 鉴权标注）保留英文：`Admin`、`Operator`、`Seller`、`Buyer`
- 「用户」一词泛指账号主体时不限角色（如"用户登录"可指任意角色）；指代消费者时必须用「买家」

---

## 4. 业务术语同义词禁用清单

以下同义词严格禁用，括号内为正确术语：

| ❌ 禁用 | ✅ 正确 |
|-|-|
| 商铺 | 店铺 |
| 商户 | 卖家 |
| 闪购 | 秒杀 |
| 产品 | 商品（仅业务语境；BC 名称 Product 保留英文） |
| 优惠券（中文语境混用英文） | 优惠券（中文）/ coupon（API 端点引用） |
| 折扣券 | 优惠券 |
| 订单号 | 订单编号（或保留 API 字段名 orderNo） |
| 发货单 | 履约单（或保留 API 字段名） |
| 退款单 | 售后单（或保留 API 字段名） |
| 评价星 | 评分 |
| 好评率 | 好评率（保留，已是标准术语） |
| 积分点 | 积分 |
| 经验值 | 成长值 |
| VIP | 付费会员 |
| 黑卡/金卡/银卡 | 会员等级 V0/V1/V2/V3/V4 |

---

## 5. 技术术语

以下技术术语在提示词中保留英文原词，不强制翻译：

- Component、Props、Emit、Slot、Composable、Store、Route、Guard
- Token、Theme、ConfigProvider
- API、Endpoint、DTO、VO、Entity、Aggregate、Repository
- JWT、OAuth、CORS、CSRF
- CRUD、Idempotency、Optimistic Lock、Pagination、Virtual Scroll
- Skeleton、Spin、Empty、Toast、Modal、Drawer、Popover、Tooltip
- Table、Form、Input、Select、DatePicker、Switch、Radio、Checkbox
- Line、Pie、Bar、Gauge（图表类型）

---

## 6. 状态术语

### 6.1 订单状态

| 状态 | 英文 | 颜色 |
|-|-|-|
| 待支付 | PendingPayment | `#FAAD14`（warning） |
| 待发货 | PendingShipment | `#FAAD14`（warning） |
| 已发货 | Shipped | `#1677FF`（primary） |
| 待收货 | PendingReceipt | `#FAAD14`（warning） |
| 已完成 | Completed | `#52C41A`（success） |
| 已取消 | Cancelled | `#8C8C8C`（neutral） |
| 已关闭 | Closed | `#8C8C8C`（neutral） |

### 6.2 售后状态

| 状态 | 英文 | 颜色 |
|-|-|-|
| 待审核 | PendingReview | `#FAAD14` |
| 待退货 | PendingReturn | `#FAAD14` |
| 待退款 | PendingRefund | `#FAAD14` |
| 已完成 | Completed | `#52C41A` |
| 已拒绝 | Rejected | `#FF4D4F` |
| 已取消 | Cancelled | `#8C8C8C` |

### 6.3 商品状态

| 状态 | 英文 | 颜色 |
|-|-|-|
| 草稿 | Draft | `#8C8C8C` |
| 待审核 | PendingReview | `#FAAD14` |
| 审核通过 | Approved | `#52C41A` |
| 审核驳回 | Rejected | `#FF4D4F` |
| 已上架 | Listed | `#52C41A` |
| 已下架 | Unlisted | `#8C8C8C` |

### 6.4 店铺状态

| 状态 | 英文 | 颜色 |
|-|-|-|
| 待审核 | PendingReview | `#FAAD14` |
| 营业中 | Active | `#52C41A` |
| 已暂停 | Suspended | `#FAAD14` |
| 已关闭 | Closed | `#8C8C8C` |
| 资质过期 | QualificationExpired | `#FF4D4F` |

### 6.5 支付状态

| 状态 | 英文 | 颜色 |
|-|-|-|
| 待支付 | Pending | `#FAAD14` |
| 支付中 | Processing | `#1677FF` |
| 已支付 | Succeeded | `#52C41A` |
| 已关闭 | Closed | `#8C8C8C` |
| 已退款 | Refunded | `#8C8C8C` |
| 支付失败 | Failed | `#FF4D4F` |
