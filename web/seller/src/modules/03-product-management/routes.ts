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
      menuKey: 'product.create',
      roles: ['Seller'],
      permission: 'product:create',
      requiresActiveShop: true,
      menuGroup: '03-product-management',
    },
  },
  {
    path: '/products/:id/edit',
    name: 'product.edit',
    component: () => import('./views/ProductEdit.vue'),
    meta: {
      title: '编辑商品',
      menuKey: 'product.edit',
      roles: ['Seller'],
      permission: 'product:edit',
      requiresActiveShop: true,
      menuGroup: '03-product-management',
    },
  },
  {
    path: '/products/:id/skus',
    name: 'product.sku',
    component: () => import('./views/SkuManagement.vue'),
    meta: {
      title: 'SKU 管理',
      menuKey: 'product.sku',
      roles: ['Seller'],
      permission: 'product:sku:manage',
      menuGroup: '03-product-management',
    },
  },
  {
    path: '/products/:id/price-history',
    name: 'product.price-history',
    component: () => import('./views/PriceHistory.vue'),
    meta: {
      title: '价格历史',
      menuKey: 'product.price-history',
      roles: ['Seller'],
      permission: 'product:price-history:view',
      menuGroup: '03-product-management',
    },
  },
]

export default routes
