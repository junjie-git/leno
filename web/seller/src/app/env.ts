export const env = {
  apiBase: import.meta.env.VITE_API_BASE,
  require2FA: import.meta.env.VITE_REQUIRE_2FA === 'true',
  useMock: import.meta.env.VITE_USE_MOCK === 'true',
  appVersion: import.meta.env.VITE_APP_VERSION ?? '0.0.0',
} as const
