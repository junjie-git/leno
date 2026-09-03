import type MockAdapter from 'axios-mock-adapter'
import type {
  ProductDetailDto,
  ProductSort,
  ProductSummaryDto,
} from '@/modules/03-catalog/types/product.dto'
import {
  seedBrands,
  seedCategories,
  seedProductDetails,
  seedProductSummaries,
} from '../data/seed'
import { fail, ok, paginate, pathParam, queryParams } from './helpers'

/**
 * 商品目录 handlers（Product BC）
 *
 * - GET /products/search（关键词/分类/品牌/店铺/排序/分页）
 * - GET /products/{id}
 * - GET /products/{id}/price-history
 * - GET /categories/tree
 * - GET /brands
 */

export function registerProductHandlers(mock: MockAdapter): void {
  // 商品搜索（注意：先于 /products/{id} 正则注册）
  mock.onGet('/products/search').reply((config) => {
    const params = queryParams(config)
    let list: ProductSummaryDto[] = [...seedProductSummaries]

    if (params.keyword) {
      const kw = params.keyword.toLowerCase()
      list = list.filter(
        (p) =>
          p.name.toLowerCase().includes(kw) ||
          p.shopName.toLowerCase().includes(kw) ||
          p.tags.some((t) => t.includes(kw)),
      )
    }
    if (params.categoryId) {
      const cat = seedCategories.find((c) => c.id === params.categoryId)
      const childIds = cat ? cat.children.map((c) => c.id) : []
      list = list.filter((p) => p.categoryId === params.categoryId || childIds.includes(p.categoryId))
    }
    if (params.brandId) {
      const brand = seedBrands.find((b) => b.id === params.brandId)
      if (brand) {
        list = list.filter((p) => p.name.includes(brand.name))
      }
    }
    if (params.shopId) {
      list = list.filter((p) => p.shopId === params.shopId)
    }

    const sort = (params.sort ?? 'default') as ProductSort
    switch (sort) {
      case 'sales':
        list.sort((a, b) => b.sales - a.sales)
        break
      case 'priceAsc':
        list.sort((a, b) => a.priceMin - b.priceMin)
        break
      case 'priceDesc':
        list.sort((a, b) => b.priceMin - a.priceMin)
        break
      case 'newest':
        list.reverse()
        break
      default:
        break
    }

    return ok(paginate(list, Number(params.page ?? 1), Number(params.pageSize ?? 10)))
  })

  // 商品详情
  mock.onGet(/\/products\/spu-\d+$/).reply((config) => {
    const id = pathParam(config.url?.match(/\/products\/(spu-\d+)$/) ?? null)
    const detail = seedProductDetails.find((p) => p.id === id)
    if (!detail) {
      return fail(40401, '商品不存在或已下架')
    }
    return ok(detail as ProductDetailDto)
  })

  // 价格历史
  mock.onGet(/\/products\/spu-\d+\/price-history$/).reply((config) => {
    const id = pathParam(config.url?.match(/\/products\/(spu-\d+)\/price-history$/) ?? null)
    const detail = seedProductDetails.find((p) => p.id === id)
    if (!detail) {
      return fail(40401, '商品不存在或已下架')
    }
    return ok(detail.priceHistory)
  })

  // 分类树
  mock.onGet('/categories/tree').reply(() => ok(seedCategories))

  // 品牌列表
  mock.onGet('/brands').reply(() => ok(seedBrands))
}
