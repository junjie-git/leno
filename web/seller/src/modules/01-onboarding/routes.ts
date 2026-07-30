import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/shop/application',
    name: 'shop.application',
    component: () => import('./views/ShopApplication.vue'),
    meta: {
      title: '入驻申请',
      menuKey: 'shop.application',
      roles: ['Seller'],
      permission: 'shop:application:submit',
      requiresActiveShop: false,
      menuGroup: '01-onboarding',
    },
  },
  {
    path: '/shop/qualifications',
    name: 'shop.qualifications',
    component: () => import('./views/ShopQualifications.vue'),
    meta: {
      title: '资质管理',
      menuKey: 'shop.qualifications',
      roles: ['Seller'],
      permission: 'shop:qualification:upload',
      menuGroup: '01-onboarding',
    },
  },
  {
    path: '/shop/profile',
    name: 'shop.profile',
    component: () => import('./views/ShopProfile.vue'),
    meta: {
      title: '店铺资料',
      menuKey: 'shop.profile',
      roles: ['Seller'],
      permission: 'shop:profile:view',
      menuGroup: '01-onboarding',
    },
  },
  {
    path: '/shop/preview',
    name: 'shop.preview',
    component: () => import('./views/ShopPreview.vue'),
    meta: {
      title: '店铺预览',
      menuKey: 'shop.preview',
      roles: ['Seller'],
      permission: 'shop:profile:view',
      menuGroup: '01-onboarding',
    },
  },
]

export default routes
