import { client } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type { TodoBoardDto, TodoCategoryDto, TodoItemDto } from '../types/account.dto'

/**
 * 待办工作台 API（聚合各业务域列表端点，无独立待办端点）
 *
 * 并行请求 5 个端点，各取 Total（概览卡片计数）+ Top10（分类列表）：
 * - GET /api/admin/products/all?status=PendingAudit      待审核商品
 * - GET /api/admin/shops?status=PendingReview            待审核入驻
 * - GET /api/admin/after-sales?status=PendingIntervention 待介入售后
 * - GET /api/admin/reviews?status=Pending                待审核评价
 * - GET /api/notifications/records?status=DeadLetter     死信通知
 *
 * 用 Promise.allSettled 并行：单端点失败降级为该分类空数据（failed=true），
 * 不影响其他分类展示，页面层对 failed 分类显示 -- 并允许重试。
 */

/** 待办取数固定分页：Top 10 */
const TOP_PAGE_SIZE = 10

/* ---------- 各业务域列表项原始形状（取所需字段子集） ---------- */

interface ProductTodoRawDto {
  id: string
  name: string | null
  shopName: string | null
  submittedAt: string | null
}

interface ShopTodoRawDto {
  id: string
  shopName: string | null
  submittedAt: string | null
}

interface AfterSaleTodoRawDto {
  id: string
  orderNo: string | null
  buyerName: string | null
  createdAt: string | null
}

interface ReviewTodoRawDto {
  id: string
  productName: string | null
  memberName: string | null
  createdAt: string | null
}

interface NotificationRecordTodoRawDto {
  id: string
  title: string | null
  channel: string | null
  createdAt: string | null
}

/* ---------- 单分类取数 ---------- */

/** 待审核商品 */
export function fetchPendingProducts(): Promise<TodoCategoryDto> {
  return client
    .get<PageResult<ProductTodoRawDto>>('/admin/products/all', {
      params: { status: 'PendingAudit', page: 1, pageSize: TOP_PAGE_SIZE },
    })
    .then(({ data }) => ({
      total: data.total,
      items: data.items.map(
        (it): TodoItemDto => ({
          id: it.id,
          title: it.name ? `${it.name} 待审核` : `商品 ${it.id} 待审核`,
          source: it.shopName,
          submittedAt: it.submittedAt,
        }),
      ),
      failed: false,
    }))
}

/** 待审核入驻 */
export function fetchPendingShops(): Promise<TodoCategoryDto> {
  return client
    .get<PageResult<ShopTodoRawDto>>('/admin/shops', {
      params: { status: 'PendingReview', page: 1, pageSize: TOP_PAGE_SIZE },
    })
    .then(({ data }) => ({
      total: data.total,
      items: data.items.map(
        (it): TodoItemDto => ({
          id: it.id,
          title: it.shopName ? `店铺「${it.shopName}」入驻待审核` : `入驻申请 ${it.id} 待审核`,
          source: it.shopName,
          submittedAt: it.submittedAt,
        }),
      ),
      failed: false,
    }))
}

/** 待介入售后 */
export function fetchPendingAfterSales(): Promise<TodoCategoryDto> {
  return client
    .get<PageResult<AfterSaleTodoRawDto>>('/admin/after-sales', {
      params: { status: 'PendingIntervention', page: 1, pageSize: TOP_PAGE_SIZE },
    })
    .then(({ data }) => ({
      total: data.total,
      items: data.items.map(
        (it): TodoItemDto => ({
          id: it.id,
          title: it.orderNo ? `售后单 ${it.orderNo} 待介入` : `售后单 ${it.id} 待介入`,
          source: it.buyerName,
          submittedAt: it.createdAt,
        }),
      ),
      failed: false,
    }))
}

/** 待审核评价 */
export function fetchPendingReviews(): Promise<TodoCategoryDto> {
  return client
    .get<PageResult<ReviewTodoRawDto>>('/admin/reviews', {
      params: { status: 'Pending', page: 1, pageSize: TOP_PAGE_SIZE },
    })
    .then(({ data }) => ({
      total: data.total,
      items: data.items.map(
        (it): TodoItemDto => ({
          id: it.id,
          title: it.productName ? `「${it.productName}」评价待审核` : `评价 ${it.id} 待审核`,
          source: it.memberName,
          submittedAt: it.createdAt,
        }),
      ),
      failed: false,
    }))
}

/** 死信通知 */
export function fetchDeadLetterNotifications(): Promise<TodoCategoryDto> {
  return client
    .get<PageResult<NotificationRecordTodoRawDto>>('/notifications/records', {
      params: { status: 'DeadLetter', page: 1, pageSize: TOP_PAGE_SIZE },
    })
    .then(({ data }) => ({
      total: data.total,
      items: data.items.map(
        (it): TodoItemDto => ({
          id: it.id,
          title: it.title ?? `通知 ${it.id} 发送失败（死信）`,
          source: it.channel,
          submittedAt: it.createdAt,
        }),
      ),
      failed: false,
    }))
}

/* ---------- 聚合 ---------- */

/** 单端点失败时的降级空分类 */
function emptyFailedCategory(): TodoCategoryDto {
  return { total: 0, items: [], failed: true }
}

/** allSettled 结果归一化：rejected 降级为空分类 */
function settleCategory(result: PromiseSettledResult<TodoCategoryDto>): TodoCategoryDto {
  return result.status === 'fulfilled' ? result.value : emptyFailedCategory()
}

/**
 * 聚合拉取待办面板：5 端点并行，单点失败降级不整体报错
 *
 * @returns 五个分类的 Total + Top10
 */
export async function fetchTodoBoard(): Promise<TodoBoardDto> {
  const [products, shops, afterSales, reviews, notifications] = await Promise.allSettled([
    fetchPendingProducts(),
    fetchPendingShops(),
    fetchPendingAfterSales(),
    fetchPendingReviews(),
    fetchDeadLetterNotifications(),
  ])

  return {
    products: settleCategory(products),
    shops: settleCategory(shops),
    afterSales: settleCategory(afterSales),
    reviews: settleCategory(reviews),
    notifications: settleCategory(notifications),
  }
}
