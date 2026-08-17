import { test, expect, type Page } from '@playwright/test'

/** 运营后台侧栏 10 个菜单分组标题（与 SiderMenu GROUP_TITLES 对齐） */
const MENU_GROUP_TITLES = [
  '数据看板',
  '商品运营',
  '促销运营',
  '卖家运营',
  '订单运营',
  '支付运营',
  '通知运营',
  '会员运营',
  '个人中心',
  '数据导出',
] as const

/**
 * 登录响应负载（与 auth.store.login / LoginResultDto 取法对齐）：
 * token / expiresIn / user(AdminUserDto) / roles / permissions。
 * user.roles 供 Login.vue 的 Operator/Admin 角色校验；permissions ['*'] 全量放行。
 */
const LOGIN_RESPONSE = {
  code: 200,
  message: 'ok',
  data: {
    token: 'e2e-tok',
    expiresIn: 3600,
    user: {
      id: 'u1',
      username: 'admin',
      email: 'ops@leno.com',
      status: 'Active',
      roles: ['Admin'],
    },
    roles: ['Admin'],
    permissions: ['*'],
  },
  traceId: 'e2e-trace',
}

/**
 * 仅匹配发往后端的 API 请求（路径以 /api/ 开头）。
 *
 * 不能用通配 glob（任意位置匹配 /api/ 片段）：它会误伤 Vite dev 的模块加载 URL，
 * 例如 /src/modules/09-account/api/auth.api.ts 源码路径同样包含 /api/ 片段，
 * 模块脚本被 mock 成 JSON 会导致应用无法启动。
 */
const API_URL_RE = /^https?:\/\/[^/]+\/api\//

/**
 * 注册网络拦截（Playwright 后注册的 route 优先匹配）：
 *
 * 1. 先注册通用兜底（所有 /api/ 开头的请求）：除登录外的所有 API（看板统计、未读数、
 *    列表查询等）返回 code=200 + 空 data，保证页面数据请求不产生网络层失败；
 * 2. 再注册精确的登录接口（/api/auth/login）：返回真实登录闭环所需负载
 *    （后注册优先匹配，覆盖通用兜底）。
 */
async function setupMock(page: Page): Promise<void> {
  await page.route(API_URL_RE, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ code: 200, message: 'ok', data: {}, traceId: 'e2e-trace' }),
    })
  })

  await page.route(/^https?:\/\/[^/]+\/api\/auth\/login/, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(LOGIN_RESPONSE),
    })
  })
}

/** 走真实登录表单流程进入 /dashboard/overview */
async function loginAndEnterDashboard(page: Page): Promise<void> {
  await page.goto('/login?redirect=/dashboard/overview')
  await page.getByPlaceholder('用户名').fill('admin')
  await page.getByPlaceholder('密码').fill('Admin123')
  await page.getByRole('button', { name: '登录' }).click()
  await expect(page).toHaveURL(/\/dashboard\/overview/)
}

test('登录闭环：填表 → 拦截登录 API → 持久化 token → 跳转 redirect', async ({ page }) => {
  await setupMock(page)

  await page.goto('/login?redirect=/dashboard/overview')
  await page.getByPlaceholder('用户名').fill('admin')
  await page.getByPlaceholder('密码').fill('Admin123')
  await page.getByRole('button', { name: '登录' }).click()

  // token 持久化到 localStorage key 'auth'（pinia persist pick: token/user/roles/permissions/expiresAt）
  await expect.poll(async () => {
    const raw = await page.evaluate(() => localStorage.getItem('auth'))
    return raw ? (JSON.parse(raw) as { token: string | null }).token : null
  }).toBe('e2e-tok')

  // 登录成功按 redirect 参数回跳运营总览
  await expect(page).toHaveURL(/\/dashboard\/overview/)
})

test('侧栏导航：渲染 10 个菜单分组 → 点击「商品审核」跳转并高亮', async ({ page }) => {
  await setupMock(page)
  await loginAndEnterDashboard(page)

  // 路由聚合后侧栏自动渲染 10 个菜单分组标题（ant Menu ItemGroup title）
  const groupTitles = page.locator('.ant-menu-item-group-title')
  await expect(groupTitles).toHaveCount(10)
  const titles = await groupTitles.allTextContents()
  expect([...new Set(titles)].sort()).toEqual([...MENU_GROUP_TITLES].sort())

  // 点击「商品运营」分组下的「商品审核」菜单项
  const auditItem = page.locator('li.ant-menu-item', { hasText: '商品审核' })
  await auditItem.click()

  // URL 切换到商品审核页
  await expect(page).toHaveURL(/\/product-ops\/product-audit$/)
  // 菜单项高亮（ant Menu 选中态追加 ant-menu-item-selected class）
  await expect(auditItem).toHaveClass(/ant-menu-item-selected/)
  // 顶栏面包屑展示目标页标题
  await expect(page.locator('.header-breadcrumb')).toContainText('商品审核')
})
