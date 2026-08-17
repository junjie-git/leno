/**
 * 02-product-ops 分类管理 DTO
 *
 * 对接 Product 域 CategoryController / AdminCategoriesController：
 * - GET  /api/categories/tree          分类树（支持 keyword 过滤并保留祖先链）
 * - GET  /api/categories/{id}          分类详情（含 productCount）
 * - POST /api/admin/categories         创建分类
 * - PUT  /api/admin/categories/{id}    更新分类
 * - POST /api/admin/categories/{id}/enable|disable  启用 / 停用
 *
 * 业务约束：层级最多 3 级；同级名称唯一；停用含启用子分类或被商品引用的分类时后端返回 409。
 */

/** 分类状态 */
export type CategoryStatus = 'Active' | 'Inactive'

/** 分类树节点（树形结构，Children 递归） */
export interface CategoryDto {
  id: string
  name: string
  /** 父分类 ID，顶级为 null */
  parentId: string | null
  /** 层级：顶级 1，最多 3 级 */
  level: number
  /** 图标标识，可选 */
  icon?: string
  /** 排序值，数字越小越靠前 */
  sortOrder: number
  status: CategoryStatus
  /** 子分类 */
  children?: CategoryDto[]
  /** 关联商品数（详情必返，树节点可能缺省） */
  productCount?: number
}

/** GET /api/categories/tree 查询参数 */
export interface CategoryTreeParams {
  /** 非空时只返回名称包含 keyword 的节点及其祖先节点 */
  keyword?: string
}

/** 创建 / 更新分类请求体（CreateCategoryDto / UpdateCategoryDto 同构） */
export interface SaveCategoryDto {
  /** 父分类 ID，null 为顶级 */
  parentId: string | null
  /** 分类名称（必填，1-30 字，同级唯一） */
  name: string
  icon?: string
  sortOrder: number
  status: CategoryStatus
}

/** 创建分类请求体 */
export type CreateCategoryDto = SaveCategoryDto

/** 更新分类请求体 */
export type UpdateCategoryDto = SaveCategoryDto
