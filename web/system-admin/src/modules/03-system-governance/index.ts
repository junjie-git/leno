// web/system-admin/src/modules/03-system-governance/index.ts
// 模块对外出口：routes + 各 api 对象
export { default as systemGovernanceRoutes } from './routes'
export { featureFlagsApi } from './api/feature-flags.api'
export { systemConfigsApi } from './api/system-configs.api'
export { dataDictionariesApi } from './api/data-dictionaries.api'
export { announcementsApi } from './api/announcements.api'
