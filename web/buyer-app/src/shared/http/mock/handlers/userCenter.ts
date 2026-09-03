import type MockAdapter from 'axios-mock-adapter'
import type {
  AddressDto,
  BrowseHistoryDto,
  FavoriteDto,
  SaveAddressRequestDto,
} from '@/modules/13-profile/types/profile.dto'
import {
  runtime,
  seedAddresses,
  seedBrowseHistory,
  seedFavorites,
  seedProductDetails,
  seedProductSummaries,
} from '../data/seed'
import { fail, ok, parseBody } from './helpers'

/**
 * 个人中心 handlers（UserCenter 域：地址 / 收藏 / 浏览历史）
 *
 * - GET/POST /users/me/addresses、PUT/DELETE /users/me/addresses/{id}
 * - POST /users/me/addresses/{id}/default
 * - GET/POST /users/me/favorites、DELETE /users/me/favorites/{spuId}
 * - POST /users/me/favorites/batch-delete、GET /users/me/favorites/count
 * - GET/POST /users/me/browse-history、DELETE /users/me/browse-history/{id}
 * - POST /users/me/browse-history/batch-delete、DELETE /users/me/browse-history
 */

let addressSeq = 100

export function registerUserCenterHandlers(mock: MockAdapter): void {
  // ---- 地址 ----

  mock.onGet('/users/me/addresses').reply(() => ok(seedAddresses))

  mock.onPost('/users/me/addresses').reply((config) => {
    const body = parseBody<SaveAddressRequestDto>(config.data)
    if (!body.receiver || !body.phone || !body.province || !body.detail) {
      return fail(40500, '请完整填写收货人、手机号与地址信息')
    }
    if (!/^1[3-9]\d{9}$/.test(body.phone)) {
      return fail(40501, '手机号格式不正确')
    }
    addressSeq += 1
    const address: AddressDto = {
      id: `addr-${addressSeq}`,
      receiver: body.receiver,
      phone: body.phone,
      province: body.province,
      city: body.city,
      district: body.district,
      detail: body.detail,
      isDefault: false,
      tag: body.tag,
    }
    if (body.isDefault) {
      seedAddresses.forEach((a) => {
        a.isDefault = false
      })
      address.isDefault = true
    }
    seedAddresses.push(address)
    return ok(address)
  })

  mock.onPut(/\/users\/me\/addresses\/[\w-]+$/).reply((config) => {
    const id = config.url?.match(/\/users\/me\/addresses\/([\w-]+)$/)?.[1] ?? ''
    const address = seedAddresses.find((a) => a.id === id)
    if (!address) {
      return fail(40502, '地址不存在')
    }
    const body = parseBody<SaveAddressRequestDto>(config.data)
    if (!body.receiver || !body.phone || !body.province || !body.detail) {
      return fail(40500, '请完整填写收货人、手机号与地址信息')
    }
    if (!/^1[3-9]\d{9}$/.test(body.phone)) {
      return fail(40501, '手机号格式不正确')
    }
    Object.assign(address, {
      receiver: body.receiver,
      phone: body.phone,
      province: body.province,
      city: body.city,
      district: body.district,
      detail: body.detail,
      tag: body.tag,
    })
    if (body.isDefault) {
      seedAddresses.forEach((a) => {
        a.isDefault = a.id === id
      })
    }
    return ok(address)
  })

  mock.onDelete(/\/users\/me\/addresses\/[\w-]+$/).reply((config) => {
    const id = config.url?.match(/\/users\/me\/addresses\/([\w-]+)$/)?.[1] ?? ''
    const idx = seedAddresses.findIndex((a) => a.id === id)
    if (idx < 0) {
      return fail(40502, '地址不存在')
    }
    if (seedAddresses[idx].isDefault && seedAddresses.length > 1) {
      return fail(40503, '默认地址不可删除，请先设置其他默认地址')
    }
    seedAddresses.splice(idx, 1)
    return ok(null)
  })

  mock.onPost(/\/users\/me\/addresses\/[\w-]+\/default$/).reply((config) => {
    const id = config.url?.match(/\/users\/me\/addresses\/([\w-]+)\/default$/)?.[1] ?? ''
    const address = seedAddresses.find((a) => a.id === id)
    if (!address) {
      return fail(40502, '地址不存在')
    }
    seedAddresses.forEach((a) => {
      a.isDefault = a.id === id
    })
    return ok(address)
  })

  // ---- 收藏 ----

  mock.onGet('/users/me/favorites').reply(() => ok(seedFavorites))

  mock.onPost('/users/me/favorites').reply((config) => {
    const body = parseBody<{ spuId: string }>(config.data)
    const summary = seedProductSummaries.find((p) => p.id === body.spuId)
    if (!summary) {
      return fail(40401, '商品不存在或已下架')
    }
    const existing = seedFavorites.find((f) => f.spuId === body.spuId)
    if (existing) {
      return ok(null)
    }
    const favorite: FavoriteDto = {
      spuId: summary.id,
      name: summary.name,
      mainImage: summary.mainImage,
      price: summary.priceMin,
      sales: summary.sales,
      shopName: summary.shopName,
      favoritedAt: new Date().toISOString(),
    }
    seedFavorites.unshift(favorite)
    return ok(null)
  })

  mock.onDelete(/\/users\/me\/favorites\/spu-\d+$/).reply((config) => {
    const spuId = config.url?.match(/\/users\/me\/favorites\/(spu-\d+)$/)?.[1] ?? ''
    const idx = seedFavorites.findIndex((f) => f.spuId === spuId)
    if (idx < 0) {
      return fail(40504, '该商品不在收藏列表中')
    }
    seedFavorites.splice(idx, 1)
    return ok(null)
  })

  mock.onPost('/users/me/favorites/batch-delete').reply((config) => {
    const body = parseBody<{ spuIds: string[] }>(config.data)
    for (const spuId of body.spuIds ?? []) {
      const idx = seedFavorites.findIndex((f) => f.spuId === spuId)
      if (idx >= 0) {
        seedFavorites.splice(idx, 1)
      }
    }
    return ok(null)
  })

  mock.onGet('/users/me/favorites/count').reply(() => ok(seedFavorites.length))

  // ---- 浏览历史 ----

  mock.onGet('/users/me/browse-history').reply(() => ok(seedBrowseHistory))

  mock.onPost('/users/me/browse-history').reply((config) => {
    const body = parseBody<{ spuId: string }>(config.data)
    const detail = seedProductDetails.find((p) => p.id === body.spuId)
    if (!detail) {
      return fail(40401, '商品不存在或已下架')
    }
    // 已在历史头部则只刷新时间
    const existing = seedBrowseHistory.find((h) => h.spuId === body.spuId)
    if (existing) {
      existing.viewedAt = new Date().toISOString()
      // 移到头部
      const idx = seedBrowseHistory.indexOf(existing)
      seedBrowseHistory.splice(idx, 1)
      seedBrowseHistory.unshift(existing)
      return ok(null)
    }
    if (runtime.reportedViews.has(body.spuId)) {
      return ok(null)
    }
    runtime.reportedViews.add(body.spuId)
    const entry: BrowseHistoryDto = {
      id: `bh-${Date.now()}`,
      spuId: detail.id,
      name: detail.name,
      mainImage: detail.mainImage,
      price: detail.priceMin,
      shopName: detail.shopName,
      viewedAt: new Date().toISOString(),
    }
    seedBrowseHistory.unshift(entry)
    // 历史上限 50 条
    if (seedBrowseHistory.length > 50) {
      seedBrowseHistory.length = 50
    }
    return ok(null)
  })

  mock.onDelete(/\/users\/me\/browse-history\/[\w-]+$/).reply((config) => {
    const id = config.url?.match(/\/users\/me\/browse-history\/([\w-]+)$/)?.[1] ?? ''
    const idx = seedBrowseHistory.findIndex((h) => h.id === id)
    if (idx < 0) {
      return fail(40505, '浏览记录不存在')
    }
    seedBrowseHistory.splice(idx, 1)
    return ok(null)
  })

  mock.onPost('/users/me/browse-history/batch-delete').reply((config) => {
    const body = parseBody<{ ids: string[] }>(config.data)
    for (const id of body.ids ?? []) {
      const idx = seedBrowseHistory.findIndex((h) => h.id === id)
      if (idx >= 0) {
        seedBrowseHistory.splice(idx, 1)
      }
    }
    return ok(null)
  })

  mock.onDelete('/users/me/browse-history').reply(() => {
    seedBrowseHistory.splice(0, seedBrowseHistory.length)
    return ok(null)
  })
}
