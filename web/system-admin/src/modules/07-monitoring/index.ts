// web/system-admin/src/modules/07-monitoring/index.ts
// 模块对外出口：routes + monitoringApi 对象
// 供 app/router.ts 聚合 import 与菜单渲染使用
export { default as monitoringRoutes } from './routes'
export { monitoringApi } from './api/monitoring.api'
