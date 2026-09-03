import type { RouteRecordRaw } from 'vue-router'

/**
 * 03-catalog 商品目录路由（4 条）
 */
const routes: RouteRecordRaw[] = [
  {
    path: 'category',
    name: 'catalog.categoryNav',
    component: () => import('./views/CategoryNav.vue'),
    meta: { title: '分类', tabbar: true },
  },
  {
    path: 'search',
    name: 'catalog.search',
    component: () => import('./views/Search.vue'),
    meta: { title: '搜索' },
  },
  {
    path: 'search/results',
    name: 'catalog.searchResults',
    component: () => import('./views/SearchResults.vue'),
    meta: { title: '搜索结果' },
  },
  {
    path: 'product/:id',
    name: 'catalog.productDetail',
    component: () => import('./views/ProductDetail.vue'),
    meta: { title: '商品详情' },
  },
]

export default routes
