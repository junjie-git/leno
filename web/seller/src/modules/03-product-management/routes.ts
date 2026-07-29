import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/products',
    name: 'product.list',
    component: () => import('./views/ProductList.vue'),
    meta: {
      title: '商品列表',
      menuKey: 'product.list',
      roles: ['Seller'],
      permission: 'product:list',
      menuGroup: '03-product-management',
    },
  },
  {
    path: '/products/new',
    name: 'product.create',
    component: () => import('./views/ProductEdit.vue'),
    meta: {
      title: '新增商品',
      roles: ['Seller'],
      permission: 'product:create',
      requiresActiveShop: true,
    },
  },
  {
    path: '/products/:id/edit',
    name: 'product.edit',
    component: () => import('./views/ProductEdit.vue'),
    meta: {
      title: '编辑商品',
      roles: ['Seller'],
      permission: 'product:edit',
      requiresActiveShop: true,
    },
  },
  {
    path: '/products/:id/skus',
    name: 'product.sku',
    component: () => import('./views/SkuManagement.vue'),
    meta: {
      title: 'SKU 管理',
      roles: ['Seller'],
      permission: 'product:sku:manage',
    },
  },
  {
    path: '/products/:id/price-history',
    name: 'product.price-history',
    component: () => import('./views/PriceHistory.vue'),
    meta: {
      title: '价格历史',
      roles: ['Seller'],
      permission: 'product:price-history:view',
    },
  },
]

export default routes
