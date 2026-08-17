import type { PageQuery } from '@/shared/types'

/**
 * 05-order-ops 订单管理 DTO
 *
 * 对接 Order 域 AdminOrdersController：
 * - GET  /api/admin/orders                        全平台订单分页查询（走 ES 读模型）
 * - POST /api/admin/orders/{id}/force-cancel      运营强制取消订单（Admin）
 *
 * 状态机：PendingPayment（待支付）→ Paid（已支付）→ Shipped（已发货）→ Delivered（已送达）
 *         → Completed（已完成）；任意前置态可 Cancelled（已取消）；
 *         强制取消跨多态直达 ForceCancelled（强制取消）。
 */

/** 订单状态（与后端 OrderStatus 枚举对齐） */
export type OrderStatus =
  | 'PendingPayment'
  | 'Paid'
  | 'Shipped'
  | 'Delivered'
  | 'Completed'
  | 'Cancelled'
  | 'ForceCancelled'

/** 订单状态展示元数据（md §6 状态色） */
export const ORDER_STATUS_META: Record<OrderStatus, { label: string; color: string }> = {
  PendingPayment: { label: '待支付', color: 'warning' },
  Paid: { label: '已支付', color: 'processing' },
  Shipped: { label: '已发货', color: 'purple' },
  Delivered: { label: '已送达', color: 'geekblue' },
  Completed: { label: '已完成', color: 'success' },
  Cancelled: { label: '已取消', color: 'default' },
  ForceCancelled: { label: '强制取消', color: 'error' },
}

/** 可强制取消的订单状态（待支付 / 已支付 / 已发货） */
export const FORCE_CANCELLABLE_STATUSES: OrderStatus[] = ['PendingPayment', 'Paid', 'Shipped']

/** 订单行（订单商品行） */
export interface OrderLineDto {
  id: string
  /** 商品 ID */
  productId: string
  /** 商品名称 */
  productName: string
  /** 规格描述，如「黑色 / XL」 */
  skuSpec?: string
  /** 商品主图 URL */
  imageUrl?: string
  /** 成交单价（元） */
  unitPrice: number
  /** 购买数量 */
  quantity: number
  /** 小计（元） */
  subtotal: number
}

/** 收货地址 */
export interface OrderAddressDto {
  /** 收货人 */
  receiver: string
  /** 联系电话 */
  phone: string
  /** 省 / 直辖市 */
  province: string
  /** 市 */
  city: string
  /** 区 / 县 */
  district?: string
  /** 详细地址 */
  detail: string
}

/** 支付信息 */
export interface OrderPaymentDto {
  /** 支付方式：WeChatPay / Alipay / UnionPay / ... */
  method: string
  /** 支付状态：Pending / Paid / Refunded / Failed / Unpaid */
  status: string
  /** 支付流水号 */
  transactionNo?: string
  /** 实付金额（元） */
  paidAmount?: number
  /** 支付完成时间（ISO 8601） */
  paidAt?: string
}

/** 物流轨迹节点（详情抽屉 a-timeline 数据源） */
export interface LogisticsTrackNodeDto {
  /** 节点时间（ISO 8601） */
  time: string
  /** 节点描述，如「包裹已到达杭州转运中心」 */
  description: string
  /** 操作方（快递员 / 网点），可选 */
  operator?: string
}

/** 订单状态历史条目（详情抽屉状态时间线数据源） */
export interface OrderStatusHistoryDto {
  /** 变更后状态 */
  status: OrderStatus
  /** 操作人（买家 / 卖家 / 运营 / 系统） */
  operator: string
  /** 备注（如强制取消原因），可选 */
  remark?: string
  /** 变更时间（ISO 8601） */
  createdAt: string
}

/**
 * 订单视图（列表行与详情抽屉共用）
 *
 * 列表端点（ES 读模型）保证基础字段；订单行 / 地址 / 支付 / 物流轨迹 /
 * 状态历史为详情扩展字段，后端返回时抽屉直接渲染。
 */
export interface OrderDto {
  id: string
  /** 订单号（NO 前缀，mono 展示） */
  orderNo: string
  /** 买家用户 ID */
  userId: string
  /** 买家昵称，可选 */
  buyerName?: string
  /** 卖家 ID */
  sellerId: string
  /** 店铺名称，可选 */
  sellerName?: string
  /** 商品摘要，如「A 商品 x2、B 商品 x1」 */
  itemSummary: string
  /** 订单总金额（元） */
  totalAmount: number
  /** 支付方式：WeChatPay / Alipay / UnionPay / ...（未支付可能为空） */
  paymentMethod?: string
  status: OrderStatus
  /** 下单时间（ISO 8601） */
  createdAt: string
  /** 取消原因（Cancelled / ForceCancelled 时后端可能返回） */
  cancelReason?: string
  /** 订单行（详情扩展字段） */
  lines?: OrderLineDto[]
  /** 收货地址（详情扩展字段） */
  address?: OrderAddressDto
  /** 支付信息（详情扩展字段） */
  payment?: OrderPaymentDto
  /** 物流单号（详情扩展字段） */
  trackingNo?: string
  /** 物流轨迹（详情扩展字段） */
  logisticsTrack?: LogisticsTrackNodeDto[]
  /** 状态历史（详情扩展字段） */
  statusHistory?: OrderStatusHistoryDto[]
}

/** GET /api/admin/orders 查询参数 */
export interface OrderQueryParams extends PageQuery {
  /** 订单号模糊匹配 */
  orderNo?: string
  /** 买家用户 ID */
  userId?: string
  /** 卖家 ID */
  sellerId?: string
  /** 订单状态 */
  status?: OrderStatus
  /** 下单时间下界（ISO 8601 UTC） */
  fromTime?: string
  /** 下单时间上界（ISO 8601 UTC） */
  toTime?: string
}

/** 强制取消请求体（ForceCancelOrderDto）：reason 必填 */
export interface ForceCancelOrderDto {
  reason: string
}

/** 状态计数项（统计概览卡数据源） */
export interface OrderStatusCountItem {
  status: OrderStatus
  label: string
  count: number
}
