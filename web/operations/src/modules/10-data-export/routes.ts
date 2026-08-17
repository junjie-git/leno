import type { RouteRecordRaw } from 'vue-router'
import { DownloadOutlined } from '@ant-design/icons-vue'

/**
 * 10-data-export 数据导出模块路由
 *
 * 菜单组：数据导出（menuGroup: '10-data-export'）
 * 访问角色：Operator / Admin
 */
const routes: RouteRecordRaw[] = [
  {
    path: '/data-export/export-center',
    name: 'dataExport.exportCenter',
    component: () => import('./views/ExportCenter.vue'),
    meta: {
      title: '导出中心',
      menuKey: 'dataExport.exportCenter',
      icon: DownloadOutlined,
      roles: ['Operator', 'Admin'],
      menuGroup: '10-data-export',
    },
  },
]

export default routes
