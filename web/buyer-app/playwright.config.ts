import { defineConfig, devices } from '@playwright/test'

/**
 * 买家端 E2E 冒烟测试配置
 *
 * - baseURL: 本地 dev 服务器（vite --host），CI 中由 webServer 拉起
 * - 用例聚焦交易主链路冒烟：登录 → 首页 → 商品详情 → 购物车 → 下单 → 支付结果
 */
export default defineConfig({
  testDir: './tests/e2e',
  timeout: 30_000,
  fullyParallel: true,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5176',
    trace: 'on-first-retry',
    viewport: { width: 375, height: 812 },
  },
  projects: [
    {
      name: 'mobile-chromium',
      use: { ...devices['Pixel 7'] },
    },
  ],
  webServer: {
    command: 'pnpm dev --host 127.0.0.1 --port 5176',
    url: 'http://localhost:5176',
    reuseExistingServer: !process.env.CI,
    timeout: 60_000,
  },
})
