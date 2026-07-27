import { test, expect } from '@playwright/test'

test('登录闭环：填表 → 拦截登录 API → 持久化 token → 跳转 redirect', async ({ page }) => {
  await page.route('**/api/auth/login', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        code: 0,
        message: 'ok',
        data: {
          token: 'e2e-tok',
          expiresIn: 3600,
          user: {
            id: 'u1',
            username: 'admin',
            email: 'a@l.com',
            status: 'Active',
            roles: ['Admin'],
          },
          roles: ['Admin'],
          permissions: ['*'],
        },
        traceId: 'e2e-trace',
      }),
    })
  })

  await page.goto('/login?redirect=/dashboard/operations-overview')

  await page.getByPlaceholder('请输入用户名').fill('admin')
  await page.getByPlaceholder('请输入密码').fill('Admin123')
  await page.getByRole('button', { name: '登录' }).click()

  // token 持久化到 localStorage（auth store 持久化 key 为 'auth'）
  await expect.poll(async () => {
    const raw = await page.evaluate(() => localStorage.getItem('auth'))
    return raw ? JSON.parse(raw).token : null
  }).toBe('e2e-tok')

  // 跳转 redirect 目标（Plan 1 中 dashboard 路由未实现，URL 仍为 redirect 目标）
  await expect(page).toHaveURL(/\/dashboard\/operations-overview/)
})
