import type { RouteRecordRaw } from 'vue-router'

/**
 * 02-product-ops 商品运营模块路由
 *
 * 挂载于 BasicLayout 子路由，供 app/router.ts 以
 * `import productOpsRoutes from '@/modules/02-product-ops/routes'` 聚合。
 * 访问角色：Operator / Admin。
 */
const productOpsRoutes: RouteRecordRaw[] = [
  {
    path: 'product-ops/product-audit',
    name: 'productOps.audit',
    component: () => import('./views/ProductAudit.vue'),
    meta: {
      title: '商品审核',
      menuKey: 'productOps.audit',
      icon: 'AuditOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '02-product-ops',
    },
  },
  {
    path: 'product-ops/brand-management',
    name: 'productOps.brands',
    component: () => import('./views/BrandManagement.vue'),
    meta: {
      title: '品牌管理',
      menuKey: 'productOps.brands',
      icon: 'TagOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '02-product-ops',
    },
  },
  {
    path: 'product-ops/category-management',
    name: 'productOps.categories',
    component: () => import('./views/CategoryManagement.vue'),
    meta: {
      title: '分类管理',
      menuKey: 'productOps.categories',
      icon: 'AppstoreOutlined',
      roles: ['Operator', 'Admin'],
      menuGroup: '02-product-ops',
    },
  },
]

export default productOpsRoutes
