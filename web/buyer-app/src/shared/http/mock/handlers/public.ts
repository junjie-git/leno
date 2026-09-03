import type MockAdapter from 'axios-mock-adapter'
import { seedAnnouncements, seedDictionaries } from '../data/seed'
import { fail, ok } from './helpers'

/**
 * 公共 handlers（SystemAdmin BC 公开端点）
 *
 * - GET /announcements
 * - GET /dictionaries/{code}
 */
export function registerPublicHandlers(mock: MockAdapter): void {
  // 公告列表（置顶在前）
  mock.onGet('/announcements').reply(() =>
    ok(
      [...seedAnnouncements].sort((a, b) => {
        if (a.pinned !== b.pinned) return a.pinned ? -1 : 1
        return new Date(b.publishedAt).getTime() - new Date(a.publishedAt).getTime()
      }),
    ),
  )

  // 数据字典
  mock.onGet(/\/dictionaries\/[\w-]+$/).reply((config) => {
    const code = config.url?.match(/\/dictionaries\/([\w-]+)$/)?.[1] ?? ''
    const dictionary = seedDictionaries.find((d) => d.code === code)
    if (!dictionary) {
      return fail(40510, `字典 ${code} 不存在`)
    }
    return ok(dictionary)
  })
}
