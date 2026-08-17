import type { RouteRecordRaw } from 'vue-router'
import { GiftOutlined, TagsOutlined, ThunderboltOutlined } from '@ant-design/icons-vue'

/**
 * 03-promotion-ops 促销运营模块路由
 *
 * 菜单组：促销运营（menuGroup: '03-promotion-ops'）
 * 访问角色：Operator / Admin
 */
const routes: RouteRecordRaw[] = [
  {
    path: '/promotion-ops/promotions',
    name: 'promotionOps.promotions',
    component: () => import('./views/Promotions.vue'),
    meta: {
      title: '促销活动',
      menuKey: 'promotionOps.promotions',
      icon: GiftOutlined,
      roles: ['Operator', 'Admin'],
      menuGroup: '03-promotion-ops',
    },
  },
  {
    path: '/promotion-ops/coupons',
    name: 'promotionOps.coupons',
    component: () => import('./views/Coupons.vue'),
    meta: {
      title: '优惠券管理',
      menuKey: 'promotionOps.coupons',
      icon: TagsOutlined,
      roles: ['Operator', 'Admin'],
      menuGroup: '03-promotion-ops',
    },
  },
  {
    path: '/promotion-ops/seckill',
    name: 'promotionOps.seckill',
    component: () => import('./views/Seckill.vue'),
    meta: {
      title: '秒杀活动',
      menuKey: 'promotionOps.seckill',
      icon: ThunderboltOutlined,
      roles: ['Operator', 'Admin'],
      menuGroup: '03-promotion-ops',
    },
  },
]

export default routes
