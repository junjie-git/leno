import type {
  BrandDto,
  CategoryDto,
  ProductDetailDto,
  ProductSummaryDto,
} from '@/modules/03-catalog/types/product.dto'
import type { CartItemDto } from '@/modules/05-cart/types/cart.dto'
import type { OrderDto, LogisticsTraceDto } from '@/modules/06-order/types/order.dto'
import type { PaymentDto } from '@/modules/07-payment/types/payment.dto'
import type {
  AvailableCouponDto,
  MyCouponDto,
  SeckillActivityDto,
} from '@/modules/08-promotion/types/promotion.dto'
import type { ReviewDto } from '@/modules/09-review/types/review.dto'
import type { AfterSalesDto, RefundDto } from '@/modules/10-after-sales/types/after-sales.dto'
import type {
  PointsAccountDto,
  PointsLedgerEntryDto,
  PointsTaskDto,
} from '@/modules/11-points-membership/types/points.dto'
import type {
  MemberLevelInfoDto,
  MemberProfileDto,
  MembershipPackageDto,
} from '@/modules/11-points-membership/types/member.dto'
import type { NotificationDto } from '@/modules/12-notification/types/notification.dto'
import type {
  AddressDto,
  BrowseHistoryDto,
  FavoriteDto,
  BuyerProfileDto,
} from '@/modules/13-profile/types/profile.dto'
import type { AnnouncementDto, DictionaryDto } from '@/modules/14-public/types/public.dto'
import { productImage as productImg } from '@/shared/utils/svg-image'

/**
 * Mock 种子数据（仅 dev + VITE_USE_MOCK 时装配，生产不打包）
 *
 * 覆盖买家端全部业务域：用户/商品/购物车/订单/支付/促销/评价/售后/积分会员/通知/个人中心/公共。
 * 数据取自设计稿（docs/designs/buyer-app）中的真实示例文案与价格。
 * 时间字段相对 Date.now() 生成，保证倒计时/过期态在任何时刻打开都符合预期。
 */

/** 相对当前时间的偏移（分钟） */
function minutesFromNow(min: number): string {
  return new Date(Date.now() + min * 60_000).toISOString()
}

/** 相对当前时间的偏移（天） */
function daysFromNow(days: number): string {
  return new Date(Date.now() + days * 86_400_000).toISOString()
}

// ---------------------------------------------------------------------------
// 用户
// ---------------------------------------------------------------------------

export const seedUser: BuyerProfileDto = {
  id: 'u-1001',
  username: 'zhangxiaoya',
  nickname: '张小雅',
  phone: '13812345678',
  email: 'zhangxiaoya@example.com',
  avatar: '',
  memberLevelName: '黄金会员 V3',
  points: 2860,
  gender: 'Female',
  birthday: '1996-05-20',
  createdAt: '2025-06-18T10:24:00.000Z',
  twoFactorEnabled: false,
}

/** 登录演示账号（Login 页提示） */
export const DEMO_ACCOUNT = 'zhangxiaoya'
export const DEMO_PASSWORD = 'Zhang123456'
/** 演示 2FA 账号：登录后需要双因子验证码 123456 */
export const DEMO_2FA_ACCOUNT = 'demo2fa'
export const DEMO_2FA_CODE = '123456'

// ---------------------------------------------------------------------------
// 商品与目录
// ---------------------------------------------------------------------------

export const seedCategories: CategoryDto[] = [
  {
    id: 'cat-1',
    name: '手机数码',
    children: [
      { id: 'cat-1-1', name: '手机', children: [] },
      { id: 'cat-1-2', name: '智能穿戴', children: [] },
      { id: 'cat-1-3', name: '耳机音箱', children: [] },
    ],
  },
  {
    id: 'cat-2',
    name: '服饰穿搭',
    children: [
      { id: 'cat-2-1', name: '男装', children: [] },
      { id: 'cat-2-2', name: '女装', children: [] },
      { id: 'cat-2-3', name: '内衣配饰', children: [] },
    ],
  },
  {
    id: 'cat-3',
    name: '食品生鲜',
    children: [
      { id: 'cat-3-1', name: '零食坚果', children: [] },
      { id: 'cat-3-2', name: '乳品饮料', children: [] },
      { id: 'cat-3-3', name: '粮油调味', children: [] },
    ],
  },
  {
    id: 'cat-4',
    name: '美妆个护',
    children: [
      { id: 'cat-4-1', name: '面部护理', children: [] },
      { id: 'cat-4-2', name: '身体护理', children: [] },
    ],
  },
  {
    id: 'cat-5',
    name: '家居日用',
    children: [
      { id: 'cat-5-1', name: '家清纸品', children: [] },
      { id: 'cat-5-2', name: '厨房用品', children: [] },
    ],
  },
  {
    id: 'cat-6',
    name: '运动户外',
    children: [
      { id: 'cat-6-1', name: '运动鞋服', children: [] },
      { id: 'cat-6-2', name: '健身器材', children: [] },
    ],
  },
  {
    id: 'cat-7',
    name: '母婴玩具',
    children: [
      { id: 'cat-7-1', name: '积木玩具', children: [] },
      { id: 'cat-7-2', name: '婴儿用品', children: [] },
    ],
  },
  {
    id: 'cat-8',
    name: '图书文创',
    children: [
      { id: 'cat-8-1', name: '文学小说', children: [] },
      { id: 'cat-8-2', name: '社科经管', children: [] },
    ],
  },
]

export const seedBrands: BrandDto[] = [
  { id: 'brand-1', name: '小米', logo: '' },
  { id: 'brand-2', name: '南极人', logo: '' },
  { id: 'brand-3', name: '三只松鼠', logo: '' },
  { id: 'brand-4', name: '漫步者', logo: '' },
  { id: 'brand-5', name: '膳魔师', logo: '' },
  { id: 'brand-6', name: '安踏', logo: '' },
  { id: 'brand-7', name: '欧莱雅', logo: '' },
  { id: 'brand-8', name: '蓝月亮', logo: '' },
  { id: 'brand-9', name: '乐高', logo: '' },
  { id: 'brand-10', name: '蒙牛', logo: '' },
]

interface SeedProduct {
  spu: ProductDetailDto
  summary: ProductSummaryDto
}

/** 由设计稿商品构建完整 SPU + SKU */
function buildProduct(input: {
  id: string
  name: string
  subtitle: string
  imageLabel: string
  categoryId: string
  categoryName: string
  brandId: string
  brandName: string
  shopId: string
  shopName: string
  description: string
  tags: string[]
  sales: number
  attributes: Array<{ name: string; value: string }>
  skus: Array<{ specs: string; price: number; originalPrice: number; stock: number }>
  priceHistory?: Array<{ daysAgo: number; price: number }>
  reviewCount: number
  averageRating: number
  goodRate: number
}): SeedProduct {
  const prices = input.skus.map((s) => s.price)
  const priceMin = Math.min(...prices)
  const priceMax = Math.max(...prices)
  const img = productImg(input.imageLabel)
  const images = [img, productImg(`${input.imageLabel}·2`), productImg(`${input.imageLabel}·3`), productImg(`${input.imageLabel}·4`)]
  const spu: ProductDetailDto = {
    id: input.id,
    name: input.name,
    subtitle: input.subtitle,
    mainImage: img,
    images,
    categoryId: input.categoryId,
    categoryName: input.categoryName,
    brandId: input.brandId,
    brandName: input.brandName,
    shopId: input.shopId,
    shopName: input.shopName,
    description: input.description,
    priceMin,
    priceMax,
    sales: input.sales,
    stock: input.skus.reduce((acc, s) => acc + s.stock, 0),
    tags: input.tags,
    skus: input.skus.map((s, i) => ({
      id: `${input.id}-sku${i + 1}`,
      spuId: input.id,
      specs: s.specs,
      price: s.price,
      originalPrice: s.originalPrice,
      stock: s.stock,
      image: img,
    })),
    attributes: input.attributes,
    priceHistory: (input.priceHistory ?? [
      { daysAgo: 60, price: Math.round(priceMax * 1.15) },
      { daysAgo: 45, price: Math.round(priceMax * 1.1) },
      { daysAgo: 30, price: priceMax },
      { daysAgo: 15, price: Math.round(priceMax * 0.97) },
      { daysAgo: 3, price: priceMin },
    ]).map((p) => ({ date: daysFromNow(-p.daysAgo).slice(0, 10), price: p.price })),
    reviewSummary: {
      count: input.reviewCount,
      averageRating: input.averageRating,
      goodRate: input.goodRate,
    },
  }
  const summary: ProductSummaryDto = {
    id: input.id,
    name: input.name,
    mainImage: img,
    priceMin,
    priceMax,
    sales: input.sales,
    tags: input.tags,
    shopId: input.shopId,
    shopName: input.shopName,
    categoryId: input.categoryId,
  }
  return { spu, summary }
}

export const seedProducts: SeedProduct[] = [
  buildProduct({
    id: 'spu-101',
    name: '南极人纯棉短袖T恤夏季透气半袖',
    subtitle: '100%新疆棉 亲肤透气 多色可选',
    imageLabel: '纯棉T恤',
    categoryId: 'cat-2-1',
    categoryName: '男装',
    brandId: 'brand-2',
    brandName: '南极人',
    shopId: 'shop-1002',
    shopName: '南极人服饰旗舰店',
    description:
      '精选新疆长绒棉，克重 220g，领口三针五线工艺不变形。夏季透气吸汗，宽松版型百搭耐穿。下单享 7 天无理由退换。',
    tags: ['秒杀', '包邮'],
    sales: 32100,
    attributes: [
      { name: '面料', value: '100% 棉' },
      { name: '克重', value: '220g' },
      { name: '版型', value: '宽松' },
    ],
    skus: [
      { specs: '颜色:白色;尺码:M', price: 2990, originalPrice: 3990, stock: 580 },
      { specs: '颜色:白色;尺码:L', price: 2990, originalPrice: 3990, stock: 460 },
      { specs: '颜色:黑色;尺码:M', price: 2990, originalPrice: 3990, stock: 320 },
      { specs: '颜色:藏青;尺码:XL', price: 3190, originalPrice: 4190, stock: 150 },
    ],
    reviewCount: 12856,
    averageRating: 4.8,
    goodRate: 96,
  }),
  buildProduct({
    id: 'spu-102',
    name: '小米 Redmi Note 13 5G 智能手机',
    subtitle: '1亿像素直面屏 5000mAh 大电池',
    imageLabel: 'Redmi手机',
    categoryId: 'cat-1-1',
    categoryName: '手机',
    brandId: 'brand-1',
    brandName: '小米',
    shopId: 'shop-1001',
    shopName: '小米官方旗舰店',
    description:
      '6.67 英寸 OLED 直面屏，1 亿像素主摄，5000mAh 长续航 + 33W 快充。支持双卡 5G，出厂预装 MIUI。赠品：有线耳机一副。',
    tags: ['补贴', '赠耳机'],
    sales: 8650,
    attributes: [
      { name: '屏幕', value: '6.67 英寸 OLED' },
      { name: '电池', value: '5000mAh' },
      { name: '快充', value: '33W' },
    ],
    skus: [
      { specs: '颜色:子夜黑;存储:8GB+128GB', price: 99900, originalPrice: 119900, stock: 230 },
      { specs: '颜色:子夜黑;存储:8GB+256GB', price: 109900, originalPrice: 129900, stock: 180 },
      { specs: '颜色:时光蓝;存储:8GB+128GB', price: 99900, originalPrice: 119900, stock: 96 },
    ],
    priceHistory: [
      { daysAgo: 60, price: 119900 },
      { daysAgo: 30, price: 109900 },
      { daysAgo: 10, price: 99900 },
      { daysAgo: 2, price: 99900 },
    ],
    reviewCount: 5230,
    averageRating: 4.9,
    goodRate: 98,
  }),
  buildProduct({
    id: 'spu-103',
    name: '三只松鼠每日坚果 30 包混合干果',
    subtitle: '科学配比 孕妇儿童零食礼盒',
    imageLabel: '每日坚果',
    categoryId: 'cat-3-1',
    categoryName: '零食坚果',
    brandId: 'brand-3',
    brandName: '三只松鼠',
    shopId: 'shop-1003',
    shopName: '三只松鼠零食专卖店',
    description:
      '每日一包科学配比：巴旦木、腰果、核桃仁、蓝莓干、蔓越莓干等 6 种坚果果干。独立小包装锁鲜，开袋即食。',
    tags: ['满减'],
    sales: 120000,
    attributes: [
      { name: '净含量', value: '30 包 × 25g' },
      { name: '保质期', value: '180 天' },
      { name: '包装', value: '独立小包' },
    ],
    skus: [
      { specs: '规格:30包装', price: 6990, originalPrice: 8990, stock: 1500 },
      { specs: '规格:15包装', price: 3990, originalPrice: 4990, stock: 860 },
    ],
    reviewCount: 28430,
    averageRating: 4.7,
    goodRate: 94,
  }),
  buildProduct({
    id: 'spu-104',
    name: '漫步者 LolliPods 真无线蓝牙耳机',
    subtitle: '半入耳式 24H续航 蓝牙5.3',
    imageLabel: '蓝牙耳机',
    categoryId: 'cat-1-3',
    categoryName: '耳机音箱',
    brandId: 'brand-4',
    brandName: '漫步者',
    shopId: 'shop-1004',
    shopName: '漫步者音频旗舰店',
    description:
      '半入耳人体工学设计，复合振膜单元，支持蓝牙 5.3 低延迟模式。单次续航 6 小时，配合充电仓 24 小时。IP54 防尘防水。',
    tags: ['新品'],
    sales: 21000,
    attributes: [
      { name: '蓝牙版本', value: '5.3' },
      { name: '续航', value: '6h + 18h 充电仓' },
      { name: '防水等级', value: 'IP54' },
    ],
    skus: [
      { specs: '颜色:白色', price: 19900, originalPrice: 24900, stock: 410 },
      { specs: '颜色:黑色', price: 19900, originalPrice: 24900, stock: 260 },
      { specs: '颜色:粉色', price: 20900, originalPrice: 25900, stock: 88 },
    ],
    reviewCount: 8620,
    averageRating: 4.6,
    goodRate: 92,
  }),
  buildProduct({
    id: 'spu-105',
    name: '膳魔师不锈钢真空保温杯 350ml',
    subtitle: '12小时保温保冷 一键开盖',
    imageLabel: '保温杯',
    categoryId: 'cat-5-2',
    categoryName: '厨房用品',
    brandId: 'brand-5',
    brandName: '膳魔师',
    shopId: 'shop-1005',
    shopName: '优品百货专营店',
    description: '304 不锈钢内胆，真空双层结构，保温 12 小时保冷 6 小时。一键弹盖带安全锁，杯口食品级硅胶圈。',
    tags: ['满减', '包邮'],
    sales: 5400,
    attributes: [
      { name: '容量', value: '350ml' },
      { name: '材质', value: '304 不锈钢' },
      { name: '保温时长', value: '12 小时' },
    ],
    skus: [
      { specs: '颜色:星空蓝', price: 15800, originalPrice: 19800, stock: 320 },
      { specs: '颜色:樱花粉', price: 15800, originalPrice: 19800, stock: 150 },
    ],
    reviewCount: 3120,
    averageRating: 4.8,
    goodRate: 95,
  }),
  buildProduct({
    id: 'spu-106',
    name: '安踏男款轻跑运动鞋透气减震休闲鞋',
    subtitle: '氮科技中底 网面透气',
    imageLabel: '运动鞋',
    categoryId: 'cat-6-1',
    categoryName: '运动鞋服',
    brandId: 'brand-6',
    brandName: '安踏',
    shopId: 'shop-1005',
    shopName: '优品百货专营店',
    description: 'A-FLASHFOAM 氮科技中底，能量回弹 68%。飞织网面轻量透气，橡胶大底防滑耐磨，日常通勤慢跑两相宜。',
    tags: ['秒杀'],
    sales: 9800,
    attributes: [
      { name: '中底科技', value: '氮科技' },
      { name: '鞋面', value: '飞织网面' },
      { name: '适用场景', value: '通勤 / 慢跑' },
    ],
    skus: [
      { specs: '颜色:标准黑;尺码:40', price: 25900, originalPrice: 32900, stock: 88 },
      { specs: '颜色:标准黑;尺码:41', price: 25900, originalPrice: 32900, stock: 76 },
      { specs: '颜色:标准黑;尺码:42', price: 25900, originalPrice: 32900, stock: 54 },
      { specs: '颜色:荧光绿;尺码:41', price: 27900, originalPrice: 34900, stock: 30 },
    ],
    reviewCount: 4560,
    averageRating: 4.7,
    goodRate: 93,
  }),
  buildProduct({
    id: 'spu-107',
    name: '欧莱雅复颜玻尿酸水光充盈导入面膜 30片',
    subtitle: '玻尿酸保湿 紧致提拉',
    imageLabel: '玻尿酸面膜',
    categoryId: 'cat-4-1',
    categoryName: '面部护理',
    brandId: 'brand-7',
    brandName: '欧莱雅',
    shopId: 'shop-1005',
    shopName: '优品百货专营店',
    description: '高浓度玻尿酸复配，一片 25ml 精华液。3D 立体剪裁贴合面部，连续使用 7 天肌肤水润度提升 32%。',
    tags: ['满减'],
    sales: 15600,
    attributes: [
      { name: '规格', value: '30 片装' },
      { name: '功效', value: '保湿 紧致' },
      { name: '保质期', value: '3 年' },
    ],
    skus: [
      { specs: '规格:30片装', price: 15900, originalPrice: 21900, stock: 520 },
      { specs: '规格:15片装', price: 9900, originalPrice: 12900, stock: 300 },
    ],
    reviewCount: 9870,
    averageRating: 4.9,
    goodRate: 97,
  }),
  buildProduct({
    id: 'spu-108',
    name: '蓝月亮深层洁净洗衣液 3kg×2 组合装',
    subtitle: '深层洁净 温和不伤手',
    imageLabel: '洗衣液',
    categoryId: 'cat-5-1',
    categoryName: '家清纸品',
    brandId: 'brand-8',
    brandName: '蓝月亮',
    shopId: 'shop-1005',
    shopName: '优品百货专营店',
    description: '深层洁净因子去渍力提升 30%，中性配方温和不伤手。组装配手提泵头，用量更省。',
    tags: ['包邮'],
    sales: 23000,
    attributes: [
      { name: '规格', value: '3kg × 2' },
      { name: '香型', value: '茉莉清香' },
    ],
    skus: [
      { specs: '规格:3kg×2+泵头', price: 6600, originalPrice: 8800, stock: 990 },
      { specs: '规格:3kg×2', price: 5900, originalPrice: 7900, stock: 1200 },
    ],
    reviewCount: 15870,
    averageRating: 4.8,
    goodRate: 96,
  }),
  buildProduct({
    id: 'spu-109',
    name: '乐高得宝系列大颗粒积木缤纷创意箱',
    subtitle: '儿童益智 3-6岁 大颗粒防误吞',
    imageLabel: '积木玩具',
    categoryId: 'cat-7-1',
    categoryName: '积木玩具',
    brandId: 'brand-9',
    brandName: '乐高',
    shopId: 'shop-1005',
    shopName: '优品百货专营店',
    description: '得宝大颗粒专为小手设计，防误吞安全尺寸。缤纷创意箱 65 粒 + 创意灵感手册，锻炼手眼协调与想象力。',
    tags: [],
    sales: 4300,
    attributes: [
      { name: '颗粒数', value: '65 粒' },
      { name: '适用年龄', value: '3-6 岁' },
    ],
    skus: [
      { specs: '款式:缤纷创意箱', price: 19900, originalPrice: 24900, stock: 140 },
      { specs: '款式:数字火车', price: 27900, originalPrice: 32900, stock: 66 },
    ],
    reviewCount: 2210,
    averageRating: 4.9,
    goodRate: 98,
  }),
  buildProduct({
    id: 'spu-110',
    name: '蒙牛特仑苏纯牛奶 250ml×12 整箱',
    subtitle: '3.6g乳蛋白 高品质奶源',
    imageLabel: '纯牛奶',
    categoryId: 'cat-3-2',
    categoryName: '乳品饮料',
    brandId: 'brand-10',
    brandName: '蒙牛',
    shopId: 'shop-1003',
    shopName: '三只松鼠零食专卖店',
    description: '专属牧场奶源，每 100ml 含 3.6g 优质乳蛋白。250ml×12 盒整箱冷链直达，早餐搭档。',
    tags: ['满减'],
    sales: 45000,
    attributes: [
      { name: '规格', value: '250ml × 12' },
      { name: '乳蛋白', value: '3.6g/100ml' },
      { name: '保质期', value: '6 个月' },
    ],
    skus: [
      { specs: '规格:12盒装', price: 6590, originalPrice: 7990, stock: 800 },
      { specs: '规格:24盒装', price: 12590, originalPrice: 15590, stock: 400 },
    ],
    reviewCount: 19300,
    averageRating: 4.9,
    goodRate: 98,
  }),
  buildProduct({
    id: 'spu-111',
    name: '小米手环9 智能运动手环',
    subtitle: '21天长续航 150+运动模式',
    imageLabel: '智能手环',
    categoryId: 'cat-1-2',
    categoryName: '智能穿戴',
    brandId: 'brand-1',
    brandName: '小米',
    shopId: 'shop-1001',
    shopName: '小米官方旗舰店',
    description: '1.62 英寸 AMOLED 高亮屏，支持 150+ 运动模式与心率血氧监测，21 天超长续航，5ATM 防水。',
    tags: ['新品', '包邮'],
    sales: 12000,
    attributes: [
      { name: '屏幕', value: '1.62 英寸 AMOLED' },
      { name: '续航', value: '21 天' },
      { name: '防水', value: '5ATM' },
    ],
    skus: [
      { specs: '颜色:黑色', price: 24900, originalPrice: 29900, stock: 350 },
      { specs: '颜色:金色', price: 25900, originalPrice: 30900, stock: 120 },
    ],
    reviewCount: 6800,
    averageRating: 4.8,
    goodRate: 95,
  }),
  buildProduct({
    id: 'spu-112',
    name: '优衣库摇粒绒拉链外套女宽松休闲夹克',
    subtitle: '柔软保暖 秋冬百搭',
    imageLabel: '摇粒绒外套',
    categoryId: 'cat-2-2',
    categoryName: '女装',
    brandId: 'brand-2',
    brandName: '南极人',
    shopId: 'shop-1002',
    shopName: '南极人服饰旗舰店',
    description: '细密摇粒绒面料，轻盈保暖不起球。宽松廓形内搭卫衣外穿皆可，多色可选。',
    tags: ['包邮'],
    sales: 7600,
    attributes: [
      { name: '面料', value: '摇粒绒' },
      { name: '版型', value: '宽松' },
    ],
    skus: [
      { specs: '颜色:燕麦色;尺码:M', price: 12900, originalPrice: 16900, stock: 220 },
      { specs: '颜色:燕麦色;尺码:L', price: 12900, originalPrice: 16900, stock: 180 },
      { specs: '颜色:黑色;尺码:M', price: 12900, originalPrice: 16900, stock: 90 },
    ],
    reviewCount: 5400,
    averageRating: 4.6,
    goodRate: 91,
  }),
]

/** 商品详情索引 */
export const seedProductDetails: ProductDetailDto[] = seedProducts.map((p) => p.spu)
/** 商品摘要索引 */
export const seedProductSummaries: ProductSummaryDto[] = seedProducts.map((p) => p.summary)

// ---------------------------------------------------------------------------
// 秒杀
// ---------------------------------------------------------------------------

export const seedSeckillActivities: SeckillActivityDto[] = [
  {
    id: 'seckill-2026-0903-10',
    name: '10 点场 · 品质秒杀',
    startTime: minutesFromNow(-105),
    endTime: minutesFromNow(135),
    status: 'Active',
    items: [
      {
        skuId: 'spu-101-sku1',
        spuId: 'spu-101',
        name: '南极人纯棉短袖T恤夏季透气半袖',
        image: seedProducts[0].spu.mainImage,
        specs: '颜色:白色;尺码:M',
        seckillPrice: 1990,
        originalPrice: 2990,
        stock: 66,
        limitPerUser: 2,
      },
      {
        skuId: 'spu-102-sku1',
        spuId: 'spu-102',
        name: '小米 Redmi Note 13 5G 智能手机',
        image: seedProducts[1].spu.mainImage,
        specs: '颜色:子夜黑;存储:8GB+128GB',
        seckillPrice: 93900,
        originalPrice: 99900,
        stock: 12,
        limitPerUser: 1,
      },
      {
        skuId: 'spu-103-sku1',
        spuId: 'spu-103',
        name: '三只松鼠每日坚果 30 包混合干果',
        image: seedProducts[2].spu.mainImage,
        specs: '规格:30包装',
        seckillPrice: 4990,
        originalPrice: 6990,
        stock: 200,
        limitPerUser: 3,
      },
      {
        skuId: 'spu-104-sku1',
        spuId: 'spu-104',
        name: '漫步者 LolliPods 真无线蓝牙耳机',
        image: seedProducts[3].spu.mainImage,
        specs: '颜色:白色',
        seckillPrice: 12900,
        originalPrice: 19900,
        stock: 45,
        limitPerUser: 1,
      },
    ],
  },
  {
    id: 'seckill-2026-0903-20',
    name: '20 点场 · 数码专场',
    startTime: minutesFromNow(375),
    endTime: minutesFromNow(615),
    status: 'Upcoming',
    items: [
      {
        skuId: 'spu-111-sku1',
        spuId: 'spu-111',
        name: '小米手环9 智能运动手环',
        image: seedProducts[10].spu.mainImage,
        specs: '颜色:黑色',
        seckillPrice: 19900,
        originalPrice: 24900,
        stock: 80,
        limitPerUser: 1,
      },
      {
        skuId: 'spu-102-sku2',
        spuId: 'spu-102',
        name: '小米 Redmi Note 13 5G 智能手机',
        image: seedProducts[1].spu.mainImage,
        specs: '颜色:子夜黑;存储:8GB+256GB',
        seckillPrice: 102900,
        originalPrice: 109900,
        stock: 20,
        limitPerUser: 1,
      },
    ],
  },
]

// ---------------------------------------------------------------------------
// 优惠券
// ---------------------------------------------------------------------------

export const seedAvailableCoupons: AvailableCouponDto[] = [
  {
    couponId: 'ct-300-50',
    name: '满 300 减 50 元券',
    type: 'Threshold',
    threshold: 30000,
    discount: 5000,
    validDays: 30,
    remainCount: 500,
    received: false,
    scopeText: '全场通用',
  },
  {
    couponId: 'ct-99-15',
    name: '食品生鲜满 99 减 15 券',
    type: 'Threshold',
    threshold: 9900,
    discount: 1500,
    validDays: 15,
    remainCount: 320,
    received: true,
    scopeText: '限食品生鲜类目',
  },
  {
    couponId: 'ct-free-ship',
    name: '无门槛包邮券',
    type: 'Shipping',
    threshold: 0,
    discount: 0,
    validDays: 7,
    remainCount: 1000,
    received: false,
    scopeText: '全场通用 · 每单限用 1 张',
  },
  {
    couponId: 'ct-500-80',
    name: '数码专享满 500 减 80 券',
    type: 'Threshold',
    threshold: 50000,
    discount: 8000,
    validDays: 20,
    remainCount: 88,
    received: false,
    scopeText: '限手机数码类目',
  },
  {
    couponId: 'ct-199-30',
    name: '美妆满 199 减 30 券',
    type: 'Threshold',
    threshold: 19900,
    discount: 3000,
    validDays: 25,
    remainCount: 210,
    received: false,
    scopeText: '限美妆个护类目',
  },
  {
    couponId: 'ct-new-user',
    name: '新人首单立减 15 元券',
    type: 'Threshold',
    threshold: 100,
    discount: 1500,
    validDays: 7,
    remainCount: 999,
    received: false,
    scopeText: '全场通用 · 限新注册用户',
  },
]

export const seedMyCoupons: MyCouponDto[] = [
  {
    id: 'mc-9001',
    couponId: 'ct-300-50',
    name: '满 300 减 50 元券',
    type: 'Threshold',
    threshold: 30000,
    discount: 5000,
    status: 'Usable',
    validFrom: daysFromNow(-5),
    validTo: daysFromNow(25),
    scopeText: '全场通用',
  },
  {
    id: 'mc-9002',
    couponId: 'ct-99-15',
    name: '食品生鲜满 99 减 15 券',
    type: 'Threshold',
    threshold: 9900,
    discount: 1500,
    status: 'Usable',
    validFrom: daysFromNow(-2),
    validTo: daysFromNow(13),
    scopeText: '限食品生鲜类目',
  },
  {
    id: 'mc-9003',
    couponId: 'ct-free-ship',
    name: '无门槛包邮券',
    type: 'Shipping',
    threshold: 0,
    discount: 0,
    status: 'Used',
    validFrom: daysFromNow(-20),
    validTo: daysFromNow(-13),
    scopeText: '全场通用 · 每单限用 1 张',
  },
  {
    id: 'mc-9004',
    couponId: 'ct-199-30',
    name: '美妆满 199 减 30 券',
    type: 'Threshold',
    threshold: 19900,
    discount: 3000,
    status: 'Expired',
    validFrom: daysFromNow(-40),
    validTo: daysFromNow(-10),
    scopeText: '限美妆个护类目',
  },
]

// ---------------------------------------------------------------------------
// 购物车
// ---------------------------------------------------------------------------

export const seedCartItems: CartItemDto[] = [
  {
    skuId: 'spu-101-sku1',
    spuId: 'spu-101',
    name: '南极人纯棉短袖T恤夏季透气半袖',
    image: seedProducts[0].spu.mainImage,
    specs: '颜色:白色;尺码:M',
    price: 2990,
    quantity: 2,
    selected: true,
    stock: 580,
    shopId: 'shop-1002',
    shopName: '南极人服饰旗舰店',
  },
  {
    skuId: 'spu-112-sku1',
    spuId: 'spu-112',
    name: '优衣库摇粒绒拉链外套女宽松休闲夹克',
    image: seedProducts[11].spu.mainImage,
    specs: '颜色:燕麦色;尺码:M',
    price: 12900,
    quantity: 1,
    selected: true,
    stock: 220,
    shopId: 'shop-1002',
    shopName: '南极人服饰旗舰店',
  },
  {
    skuId: 'spu-102-sku1',
    spuId: 'spu-102',
    name: '小米 Redmi Note 13 5G 智能手机',
    image: seedProducts[1].spu.mainImage,
    specs: '颜色:子夜黑;存储:8GB+128GB',
    price: 99900,
    quantity: 1,
    selected: true,
    stock: 230,
    shopId: 'shop-1001',
    shopName: '小米官方旗舰店',
  },
  {
    skuId: 'spu-103-sku1',
    spuId: 'spu-103',
    name: '三只松鼠每日坚果 30 包混合干果',
    image: seedProducts[2].spu.mainImage,
    specs: '规格:30包装',
    price: 6990,
    quantity: 1,
    selected: false,
    stock: 1500,
    shopId: 'shop-1003',
    shopName: '三只松鼠零食专卖店',
  },
]

// ---------------------------------------------------------------------------
// 订单
// ---------------------------------------------------------------------------

function orderAddressSnapshot(): { receiver: string; phone: string; fullAddress: string } {
  return {
    receiver: '张小雅',
    phone: '138****5678',
    fullAddress: '上海市浦东新区张江高科技园区博云路 2 号 601 室',
  }
}

export const seedOrders: OrderDto[] = [
  {
    id: 'so-20260901-0001',
    orderNo: '202609011000120001',
    status: 'PendingPayment',
    items: [
      {
        orderLineId: 'ol-1001',
        spuId: 'spu-104',
        skuId: 'spu-104-sku1',
        name: '漫步者 LolliPods 真无线蓝牙耳机',
        image: seedProducts[3].spu.mainImage,
        specs: '颜色:白色',
        price: 19900,
        quantity: 1,
        reviewed: false,
      },
    ],
    shopId: 'shop-1004',
    shopName: '漫步者音频旗舰店',
    amounts: { goodsAmount: 19900, freight: 0, couponDiscount: 0, pointsDiscount: 0, payableAmount: 19900 },
    address: orderAddressSnapshot(),
    createdAt: minutesFromNow(-25),
    payDeadline: minutesFromNow(5),
    remark: '',
  },
  {
    id: 'so-20260830-0002',
    orderNo: '202608301530450002',
    status: 'Paid',
    items: [
      {
        orderLineId: 'ol-2001',
        spuId: 'spu-102',
        skuId: 'spu-102-sku1',
        name: '小米 Redmi Note 13 5G 智能手机',
        image: seedProducts[1].spu.mainImage,
        specs: '颜色:子夜黑;存储:8GB+128GB',
        price: 99900,
        quantity: 1,
        reviewed: false,
      },
      {
        orderLineId: 'ol-2002',
        spuId: 'spu-111',
        skuId: 'spu-111-sku1',
        name: '小米手环9 智能运动手环',
        image: seedProducts[10].spu.mainImage,
        specs: '颜色:黑色',
        price: 24900,
        quantity: 1,
        reviewed: false,
      },
    ],
    shopId: 'shop-1001',
    shopName: '小米官方旗舰店',
    amounts: { goodsAmount: 124800, freight: 0, couponDiscount: 5000, pointsDiscount: 500, payableAmount: 119300 },
    address: orderAddressSnapshot(),
    createdAt: daysFromNow(-4),
    paidAt: daysFromNow(-4),
    remark: '周一至周五白天配送',
  },
  {
    id: 'so-20260828-0003',
    orderNo: '202608281102360003',
    status: 'Shipped',
    items: [
      {
        orderLineId: 'ol-3001',
        spuId: 'spu-105',
        skuId: 'spu-105-sku1',
        name: '膳魔师不锈钢真空保温杯 350ml',
        image: seedProducts[4].spu.mainImage,
        specs: '颜色:星空蓝',
        price: 15800,
        quantity: 1,
        reviewed: false,
      },
      {
        orderLineId: 'ol-3002',
        spuId: 'spu-108',
        skuId: 'spu-108-sku1',
        name: '蓝月亮深层洁净洗衣液 3kg×2 组合装',
        image: seedProducts[7].spu.mainImage,
        specs: '规格:3kg×2+泵头',
        price: 6600,
        quantity: 1,
        reviewed: false,
      },
    ],
    shopId: 'shop-1005',
    shopName: '优品百货专营店',
    amounts: { goodsAmount: 22400, freight: 0, couponDiscount: 0, pointsDiscount: 200, payableAmount: 22200 },
    address: orderAddressSnapshot(),
    createdAt: daysFromNow(-6),
    paidAt: daysFromNow(-6),
    shippedAt: daysFromNow(-5),
    logisticsCompany: '顺丰速运',
    logisticsNo: 'SF1390881234567',
  },
  {
    id: 'so-20260820-0004',
    orderNo: '202608201640570004',
    status: 'Completed',
    items: [
      {
        orderLineId: 'ol-4001',
        spuId: 'spu-103',
        skuId: 'spu-103-sku1',
        name: '三只松鼠每日坚果 30 包混合干果',
        image: seedProducts[2].spu.mainImage,
        specs: '规格:30包装',
        price: 6990,
        quantity: 1,
        reviewed: true,
      },
      {
        orderLineId: 'ol-4002',
        spuId: 'spu-110',
        skuId: 'spu-110-sku1',
        name: '蒙牛特仑苏纯牛奶 250ml×12 整箱',
        image: seedProducts[9].spu.mainImage,
        specs: '规格:12盒装',
        price: 6590,
        quantity: 2,
        reviewed: false,
      },
    ],
    shopId: 'shop-1003',
    shopName: '三只松鼠零食专卖店',
    amounts: { goodsAmount: 20170, freight: 0, couponDiscount: 1500, pointsDiscount: 0, payableAmount: 18670 },
    address: orderAddressSnapshot(),
    createdAt: daysFromNow(-14),
    paidAt: daysFromNow(-14),
    shippedAt: daysFromNow(-13),
    completedAt: daysFromNow(-11),
    logisticsCompany: '中通快递',
    logisticsNo: 'ZT7845009988771',
  },
  {
    id: 'so-20260815-0005',
    orderNo: '202608150915180005',
    status: 'Completed',
    items: [
      {
        orderLineId: 'ol-5001',
        spuId: 'spu-107',
        skuId: 'spu-107-sku1',
        name: '欧莱雅复颜玻尿酸水光充盈导入面膜 30片',
        image: seedProducts[6].spu.mainImage,
        specs: '规格:30片装',
        price: 15900,
        quantity: 1,
        reviewed: true,
      },
    ],
    shopId: 'shop-1005',
    shopName: '优品百货专营店',
    amounts: { goodsAmount: 15900, freight: 0, couponDiscount: 0, pointsDiscount: 1590, payableAmount: 14310 },
    address: orderAddressSnapshot(),
    createdAt: daysFromNow(-19),
    paidAt: daysFromNow(-19),
    shippedAt: daysFromNow(-18),
    completedAt: daysFromNow(-16),
    logisticsCompany: '圆通速递',
    logisticsNo: 'YT5520134478902',
  },
  {
    id: 'so-20260810-0006',
    orderNo: '202608101420110006',
    status: 'Cancelled',
    items: [
      {
        orderLineId: 'ol-6001',
        spuId: 'spu-109',
        skuId: 'spu-109-sku1',
        name: '乐高得宝系列大颗粒积木缤纷创意箱',
        image: seedProducts[8].spu.mainImage,
        specs: '款式:缤纷创意箱',
        price: 19900,
        quantity: 1,
        reviewed: false,
      },
    ],
    shopId: 'shop-1005',
    shopName: '优品百货专营店',
    amounts: { goodsAmount: 19900, freight: 0, couponDiscount: 0, pointsDiscount: 0, payableAmount: 19900 },
    address: orderAddressSnapshot(),
    createdAt: daysFromNow(-24),
    cancelledAt: daysFromNow(-24),
    cancelReason: '超时未支付，系统自动取消',
  },
  {
    id: 'so-20260805-0007',
    orderNo: '202608051108320007',
    status: 'AfterSales',
    items: [
      {
        orderLineId: 'ol-7001',
        spuId: 'spu-106',
        skuId: 'spu-106-sku1',
        name: '安踏男款轻跑运动鞋透气减震休闲鞋',
        image: seedProducts[5].spu.mainImage,
        specs: '颜色:标准黑;尺码:41',
        price: 25900,
        quantity: 1,
        reviewed: false,
      },
    ],
    shopId: 'shop-1005',
    shopName: '优品百货专营店',
    amounts: { goodsAmount: 25900, freight: 0, couponDiscount: 0, pointsDiscount: 0, payableAmount: 25900 },
    address: orderAddressSnapshot(),
    createdAt: daysFromNow(-29),
    paidAt: daysFromNow(-29),
    shippedAt: daysFromNow(-28),
    completedAt: daysFromNow(-26),
    logisticsCompany: '韵达快递',
    logisticsNo: 'YD3309987112455',
  },
]

export const seedLogisticsTraces: Record<string, LogisticsTraceDto> = {
  'so-20260828-0003': {
    logisticsCompany: '顺丰速运',
    logisticsNo: 'SF1390881234567',
    traces: [
      { time: daysFromNow(-5.2), description: '您的快件已签收，感谢您使用顺丰速运', status: '已签收' },
      { time: daysFromNow(-5.4), description: '快件到达上海浦东新区张江营业点，正在派送途中（派送员：李强 138****2233）', status: '派送中' },
      { time: daysFromNow(-5.6), description: '快件已到达上海浦东集散中心', status: '运输中' },
      { time: daysFromNow(-5.9), description: '快件已从杭州萧山集散中心发出', status: '运输中' },
      { time: daysFromNow(-6.1), description: '顺丰速运已揽收快件', status: '已揽收' },
      { time: daysFromNow(-6.2), description: '商家拣货完成，等待快递揽收', status: '已发货' },
    ],
  },
  'so-20260820-0004': {
    logisticsCompany: '中通快递',
    logisticsNo: 'ZT7845009988771',
    traces: [
      { time: daysFromNow(-11.1), description: '您的快件已签收（放在丰巢快递柜），感谢使用中通快递', status: '已签收' },
      { time: daysFromNow(-11.3), description: '快件到达上海浦东新区唐镇服务点，正在派送途中', status: '派送中' },
      { time: daysFromNow(-11.8), description: '快件已从上海转运中心发出', status: '运输中' },
      { time: daysFromNow(-12.4), description: '中通快递已揽收快件', status: '已揽收' },
    ],
  },
}

// ---------------------------------------------------------------------------
// 支付
// ---------------------------------------------------------------------------

export const seedPayments: PaymentDto[] = [
  {
    id: 'pay-8001',
    orderId: 'so-20260830-0002',
    channel: 'Alipay',
    amount: 119300,
    status: 'Success',
    createdAt: daysFromNow(-4),
    expireAt: daysFromNow(-4),
    paidAt: daysFromNow(-4),
    channelTradeNo: '20260830220014987612345',
  },
  {
    id: 'pay-8002',
    orderId: 'so-20260801-0008',
    channel: 'WeChatPay',
    amount: 6990,
    status: 'Refunded',
    createdAt: daysFromNow(-33),
    expireAt: daysFromNow(-33),
    paidAt: daysFromNow(-33),
  },
]

// ---------------------------------------------------------------------------
// 评价
// ---------------------------------------------------------------------------

export const seedMyReviews: ReviewDto[] = [
  {
    id: 'rev-7001',
    orderLineId: 'ol-4001',
    spuId: 'spu-103',
    nickname: '张小雅',
    avatar: '',
    skuSpecs: '规格:30包装',
    rating: 5,
    content: '坚果很新鲜，独立小包装方便携带，每天一包刚刚好。家里人都爱吃，回购第三次了！',
    images: [],
    appendContent: '追加：吃完了，日期很新鲜，下次活动继续囤。',
    appendAt: daysFromNow(-9),
    createdAt: daysFromNow(-10),
    reply: {
      content: '感谢亲的支持与认可，三只松鼠会继续为您带来美味零食，期待再次光临～',
      repliedAt: daysFromNow(-9.8),
    },
  },
  {
    id: 'rev-7002',
    orderLineId: 'ol-5001',
    spuId: 'spu-107',
    nickname: '张小雅',
    avatar: '',
    skuSpecs: '规格:30片装',
    rating: 4,
    content: '面膜精华液很多，敷完水润感明显。就是味道稍微有点浓，其他都好。',
    images: [],
    createdAt: daysFromNow(-15),
  },
]

/** 商品评价（商品详情/评价页匿名可见） */
export const seedProductReviews: ReviewDto[] = [
  {
    id: 'rev-10101',
    orderLineId: 'ol-x101',
    spuId: 'spu-101',
    nickname: '李**明',
    avatar: '',
    skuSpecs: '颜色:白色;尺码:L',
    rating: 5,
    content: '面料很舒服，夏天穿不闷，洗了也不变形。这个价格真的很划算了。',
    images: [],
    createdAt: daysFromNow(-3),
    reply: {
      content: '感谢您的认可，南极人祝您生活愉快！',
      repliedAt: daysFromNow(-2.9),
    },
  },
  {
    id: 'rev-10102',
    orderLineId: 'ol-x102',
    spuId: 'spu-101',
    nickname: '王**婷',
    avatar: '',
    skuSpecs: '颜色:黑色;尺码:M',
    rating: 4,
    content: '做工可以，黑色稍微容易粘毛，整体满意。',
    images: [],
    createdAt: daysFromNow(-6),
  },
  {
    id: 'rev-10103',
    orderLineId: 'ol-x103',
    spuId: 'spu-102',
    nickname: '陈**军',
    avatar: '',
    skuSpecs: '颜色:子夜黑;存储:8GB+128GB',
    rating: 5,
    content: '给爸妈买的，屏幕清晰续航给力，系统流畅无广告。千元机里的战斗机！',
    images: [],
    createdAt: daysFromNow(-2),
    reply: {
      content: '感谢您选择小米，祝您使用愉快！',
      repliedAt: daysFromNow(-1.9),
    },
  },
  {
    id: 'rev-10104',
    orderLineId: 'ol-x104',
    spuId: 'spu-103',
    nickname: '刘**芳',
    avatar: '',
    skuSpecs: '规格:30包装',
    rating: 5,
    content: '每天都吃一包，搭配牛奶当早餐，坚果很香脆，日期新鲜到明年。',
    images: [],
    createdAt: daysFromNow(-1),
  },
  {
    id: 'rev-10105',
    orderLineId: 'ol-x105',
    spuId: 'spu-104',
    nickname: '赵**磊',
    avatar: '',
    skuSpecs: '颜色:白色',
    rating: 4,
    content: '音质对得起这个价位，延迟打手游基本无感。佩戴两小时耳朵不痛。',
    images: [],
    createdAt: daysFromNow(-4),
  },
]

// ---------------------------------------------------------------------------
// 售后
// ---------------------------------------------------------------------------

export const seedAfterSales: AfterSalesDto[] = [
  {
    id: 'as-2001',
    orderId: 'so-20260805-0007',
    orderNo: '202608051108320007',
    orderLineId: 'ol-7001',
    spuId: 'spu-106',
    skuId: 'spu-106-sku1',
    name: '安踏男款轻跑运动鞋透气减震休闲鞋',
    image: seedProducts[5].spu.mainImage,
    specs: '颜色:标准黑;尺码:41',
    price: 25900,
    quantity: 1,
    type: 'ReturnRefund',
    status: 'Returning',
    reason: '尺码不合适',
    description: '尺码偏小，穿着挤脚，希望换大一码或退货退款。',
    images: [],
    refundAmount: 25900,
    applyAt: daysFromNow(-5),
    handleAt: daysFromNow(-4.8),
  },
  {
    id: 'as-2002',
    orderId: 'so-20260801-0008',
    orderNo: '202608011410020008',
    orderLineId: 'ol-8001',
    spuId: 'spu-103',
    skuId: 'spu-103-sku1',
    name: '三只松鼠每日坚果 30 包混合干果',
    image: seedProducts[2].spu.mainImage,
    specs: '规格:30包装',
    price: 6990,
    quantity: 1,
    type: 'RefundOnly',
    status: 'Completed',
    reason: '包装破损',
    description: '收到时外箱压变形，两包坚果包装破损，申请部分退款。',
    images: [],
    refundAmount: 1398,
    applyAt: daysFromNow(-31),
    handleAt: daysFromNow(-30.9),
  },
]

export const seedRefunds: Record<string, RefundDto> = {
  'as-2002': {
    id: 'rf-3002',
    afterSalesId: 'as-2002',
    amount: 1398,
    status: 'Success',
    channel: '原路退回（微信支付）',
    appliedAt: daysFromNow(-31),
    refundedAt: daysFromNow(-30),
  },
}

// ---------------------------------------------------------------------------
// 积分与会员
// ---------------------------------------------------------------------------

export const seedPointsAccount: PointsAccountDto = {
  balance: 2860,
  totalEarned: 12480,
  totalSpent: 9620,
  expiringPoints: 120,
  expiringAt: daysFromNow(15),
  checkedInToday: false,
  checkInStreakDays: 3,
}

export const seedPointsLedger: PointsLedgerEntryDto[] = [
  { id: 'pl-1', type: 'Earn', points: 5, balanceAfter: 2860, description: '每日签到（连续 3 天）', createdAt: minutesFromNow(-1440) },
  { id: 'pl-2', type: 'Earn', points: 5, balanceAfter: 2855, description: '每日签到（连续 2 天）', createdAt: minutesFromNow(-2880) },
  { id: 'pl-3', type: 'Earn', points: 5, balanceAfter: 2850, description: '每日签到（连续 1 天）', createdAt: minutesFromNow(-4320) },
  { id: 'pl-4', type: 'Spend', points: -500, balanceAfter: 2845, description: '兑换优惠券「满 300 减 50 元券」', createdAt: minutesFromNow(-7200) },
  { id: 'pl-5', type: 'Earn', points: 119, balanceAfter: 3345, description: '订单 SO2026083002 交易完成赠送', createdAt: minutesFromNow(-8600) },
  { id: 'pl-6', type: 'Earn', points: 10, balanceAfter: 3226, description: '评价晒单奖励（每日坚果）', createdAt: minutesFromNow(-10000) },
  { id: 'pl-7', type: 'Earn', points: 2, balanceAfter: 3216, description: '浏览商品任务奖励', createdAt: minutesFromNow(-10200) },
  { id: 'pl-8', type: 'Expire', points: -30, balanceAfter: 3214, description: '2025 年度积分过期清零', createdAt: minutesFromNow(-14400) },
  { id: 'pl-9', type: 'Earn', points: 15, balanceAfter: 3244, description: '分享商品至微信好友', createdAt: minutesFromNow(-15600) },
  { id: 'pl-10', type: 'Spend', points: -1590, balanceAfter: 3229, description: '订单 SO2026081505 下单积分抵扣', createdAt: minutesFromNow(-20160) },
]

export const seedPointsTasks: PointsTaskDto[] = [
  {
    id: 'task-1',
    name: '每日签到',
    description: '连续签到 7 天可获额外 20 积分奖励',
    points: 5,
    icon: 'calendar-o',
    status: 'Pending',
    action: 'CheckIn',
  },
  {
    id: 'task-2',
    name: '浏览商品 10 秒',
    description: '每日浏览任意商品详情满 10 秒',
    points: 2,
    icon: 'eye-o',
    status: 'Completed',
    action: 'Browse',
    completedAt: minutesFromNow(-170),
  },
  {
    id: 'task-3',
    name: '搜索商品',
    description: '每日完成 1 次商品搜索',
    points: 2,
    icon: 'search',
    status: 'Pending',
    action: 'Search',
  },
  {
    id: 'task-4',
    name: '分享商品',
    description: '将商品分享给好友或朋友圈',
    points: 10,
    icon: 'share-o',
    status: 'Pending',
    action: 'Share',
  },
  {
    id: 'task-5',
    name: '完成首笔评价',
    description: '本月内完成 1 笔订单评价',
    points: 20,
    icon: 'comment-o',
    status: 'Completed',
    action: 'Review',
    completedAt: minutesFromNow(-10000),
  },
  {
    id: 'task-6',
    name: '完善个人资料',
    description: '补全头像、生日等个人信息',
    points: 50,
    icon: 'user-o',
    status: 'Pending',
    action: 'Profile',
  },
]

export const seedMemberLevels: MemberLevelInfoDto[] = [
  { level: 1, name: '普通会员', threshold: 0, icon: 'medal-o', benefits: ['注册即享', '积分累计'] },
  { level: 2, name: '白银会员', threshold: 1000, icon: 'medal-o', benefits: ['积分累计', '生日礼包'] },
  { level: 3, name: '黄金会员', threshold: 3000, icon: 'diamond-o', benefits: ['积分累计', '生日礼包', '专属客服', '95 折优惠'] },
  { level: 4, name: '铂金会员', threshold: 8000, icon: 'diamond-o', benefits: ['积分累计', '生日礼包', '专属客服', '92 折优惠', '优先购'] },
  { level: 5, name: '钻石会员', threshold: 20000, icon: 'crown-o', benefits: ['积分累计', '生日礼包', '专属客服', '9 折优惠', '优先购', '免赔极速退'] },
  { level: 6, name: '黑金会员', threshold: 50000, icon: 'crown-o', benefits: ['全部权益', '专属客户经理', '大促专属券', '免费试用'] },
]

export const seedMemberProfile: MemberProfileDto = {
  level: 3,
  levelName: '黄金会员',
  points: 2860,
  nextLevelName: '铂金会员',
  nextLevelPoints: 8000,
  benefits: ['积分累计', '生日礼包', '专属客服', '95 折优惠'],
  joinedAt: '2025-06-18T10:24:00.000Z',
  isPremium: false,
}

export const seedMembershipPackages: MembershipPackageDto[] = [
  {
    id: 'pkg-month',
    name: '月度会员',
    price: 1500,
    originalPrice: 2500,
    durationDays: 30,
    benefits: ['全场 95 折', '每月 4 张包邮券', '专属客服', '双倍积分'],
  },
  {
    id: 'pkg-quarter',
    name: '季度会员',
    price: 3900,
    originalPrice: 7500,
    durationDays: 90,
    benefits: ['全场 95 折', '每月 4 张包邮券', '专属客服', '双倍积分', '每月 1 张满 200 减 20 券'],
    tag: '人气',
  },
  {
    id: 'pkg-year',
    name: '年度会员',
    price: 12800,
    originalPrice: 30000,
    durationDays: 365,
    benefits: ['全场 92 折', '每月 8 张包邮券', '专属客服', '三倍积分', '每月 2 张满 200 减 20 券', '生日礼包升级'],
    tag: '超值',
  },
]

// ---------------------------------------------------------------------------
// 通知
// ---------------------------------------------------------------------------

export const seedNotifications: NotificationDto[] = [
  {
    id: 'nt-1',
    type: 'Logistics',
    title: '包裹已到达派送网点',
    content: '您的订单 20260828110236 由顺丰速运承运，已到达张江营业点，预计今日送达。派送员：李强 138****2233。',
    isRead: false,
    createdAt: minutesFromNow(-240),
    linkUrl: '/order/so-20260828-0003/logistics',
  },
  {
    id: 'nt-2',
    type: 'Promotion',
    title: '限时秒杀即将开抢',
    content: '今晚 20:00 数码专场秒杀：小米手环 9 直降 50 元，Redmi Note 13 限时 1029 元，每款限购 1 件，先到先得！',
    isRead: false,
    createdAt: minutesFromNow(-420),
    linkUrl: '/seckill/order/seckill-2026-0903-20',
  },
  {
    id: 'nt-3',
    type: 'Order',
    title: '待支付提醒',
    content: '订单 20260901100012 尚未支付，30 分钟后自动取消，请及时完成付款保留商品。',
    isRead: false,
    createdAt: minutesFromNow(-25),
    linkUrl: '/order/so-20260901-0001',
  },
  {
    id: 'nt-4',
    type: 'AfterSales',
    title: '售后申请已审核通过',
    content: '您的退货退款申请（安踏运动鞋）已审核通过，请在 7 天内寄回商品并上传退货物流单号。',
    isRead: true,
    createdAt: minutesFromNow(-6912),
    linkUrl: '/after-sales/as-2001',
  },
  {
    id: 'nt-5',
    type: 'Points',
    title: '积分到账通知',
    content: '订单 20260830153045 交易完成，赠送 119 积分已到账，当前余额 2860 分。',
    isRead: true,
    createdAt: minutesFromNow(-8600),
    linkUrl: '/points/ledger',
  },
  {
    id: 'nt-6',
    type: 'System',
    title: '账户安全提醒',
    content: '检测到您的账号于新设备登录，如非本人操作请及时修改密码。',
    isRead: true,
    createdAt: minutesFromNow(-13000),
    linkUrl: '/profile/security',
  },
  {
    id: 'nt-7',
    type: 'Promotion',
    title: '您有一张优惠券即将过期',
    content: '「食品生鲜满 99 减 15 券」将于 13 天后过期，抓紧使用哦～',
    isRead: true,
    createdAt: minutesFromNow(-15000),
    linkUrl: '/coupons/mine',
  },
  {
    id: 'nt-8',
    type: 'System',
    title: '平台系统升级维护公告',
    content: '本周日 02:00-04:00 平台进行系统升级，期间下单与支付服务可能出现短暂不可用，敬请谅解。',
    isRead: true,
    createdAt: minutesFromNow(-25000),
    linkUrl: '/announcements',
  },
]

// ---------------------------------------------------------------------------
// 个人中心
// ---------------------------------------------------------------------------

export const seedAddresses: AddressDto[] = [
  {
    id: 'addr-1',
    receiver: '张小雅',
    phone: '13812345678',
    province: '上海市',
    city: '上海市',
    district: '浦东新区',
    detail: '张江高科技园区博云路 2 号 601 室',
    isDefault: true,
    tag: '公司',
  },
  {
    id: 'addr-2',
    receiver: '张小雅',
    phone: '13812345678',
    province: '上海市',
    city: '上海市',
    district: '闵行区',
    detail: '莘庄工业区申北路 88 弄 12 号 302 室',
    isDefault: false,
    tag: '家',
  },
  {
    id: 'addr-3',
    receiver: '张伟',
    phone: '13998765432',
    province: '江苏省',
    city: '苏州市',
    district: '工业园区',
    detail: '金鸡湖大道 99 号玲珑湾花园 5 幢 801 室',
    isDefault: false,
    tag: '亲友',
  },
]

export const seedFavorites: FavoriteDto[] = [
  {
    spuId: 'spu-102',
    name: '小米 Redmi Note 13 5G 智能手机',
    mainImage: seedProducts[1].spu.mainImage,
    price: 99900,
    sales: 8650,
    shopName: '小米官方旗舰店',
    favoritedAt: daysFromNow(-2),
  },
  {
    spuId: 'spu-104',
    name: '漫步者 LolliPods 真无线蓝牙耳机',
    mainImage: seedProducts[3].spu.mainImage,
    price: 19900,
    sales: 21000,
    shopName: '漫步者音频旗舰店',
    favoritedAt: daysFromNow(-5),
  },
  {
    spuId: 'spu-107',
    name: '欧莱雅复颜玻尿酸水光充盈导入面膜 30片',
    mainImage: seedProducts[6].spu.mainImage,
    price: 15900,
    sales: 15600,
    shopName: '优品百货专营店',
    favoritedAt: daysFromNow(-8),
  },
  {
    spuId: 'spu-109',
    name: '乐高得宝系列大颗粒积木缤纷创意箱',
    mainImage: seedProducts[8].spu.mainImage,
    price: 19900,
    sales: 4300,
    shopName: '优品百货专营店',
    favoritedAt: daysFromNow(-12),
  },
]

export const seedBrowseHistory: BrowseHistoryDto[] = [
  { id: 'bh-1', spuId: 'spu-101', name: '南极人纯棉短袖T恤夏季透气半袖', mainImage: seedProducts[0].spu.mainImage, price: 2990, shopName: '南极人服饰旗舰店', viewedAt: minutesFromNow(-30) },
  { id: 'bh-2', spuId: 'spu-103', name: '三只松鼠每日坚果 30 包混合干果', mainImage: seedProducts[2].spu.mainImage, price: 6990, shopName: '三只松鼠零食专卖店', viewedAt: minutesFromNow(-120) },
  { id: 'bh-3', spuId: 'spu-111', name: '小米手环9 智能运动手环', mainImage: seedProducts[10].spu.mainImage, price: 24900, shopName: '小米官方旗舰店', viewedAt: minutesFromNow(-300) },
  { id: 'bh-4', spuId: 'spu-105', name: '膳魔师不锈钢真空保温杯 350ml', mainImage: seedProducts[4].spu.mainImage, price: 15800, shopName: '优品百货专营店', viewedAt: minutesFromNow(-1440) },
  { id: 'bh-5', spuId: 'spu-106', name: '安踏男款轻跑运动鞋透气减震休闲鞋', mainImage: seedProducts[5].spu.mainImage, price: 25900, shopName: '优品百货专营店', viewedAt: minutesFromNow(-2160) },
  { id: 'bh-6', spuId: 'spu-110', name: '蒙牛特仑苏纯牛奶 250ml×12 整箱', mainImage: seedProducts[9].spu.mainImage, price: 6590, shopName: '三只松鼠零食专卖店', viewedAt: minutesFromNow(-2880) },
]

// ---------------------------------------------------------------------------
// 公共
// ---------------------------------------------------------------------------

export const seedAnnouncements: AnnouncementDto[] = [
  {
    id: 'ann-1',
    title: '618 年中大促全场上折上折',
    content:
      '活动时间：6 月 1 日 00:00 - 6 月 18 日 23:59。全场商品折上折，跨店满 300 立减 50；新人首单立减 15 元；包邮券限时免费领取。秒杀场次每日 10 点、20 点准时开抢，先到先得。',
    type: 'Promotion',
    publishedAt: daysFromNow(-9),
    pinned: true,
  },
  {
    id: 'ann-2',
    title: '物流时效调整公告',
    content: '受大促单量影响，6 月 15 日 - 20 日期间发货时效由 48 小时延长至 72 小时，敬请谅解。已下单订单可在订单详情页实时查看物流轨迹。',
    type: 'System',
    publishedAt: daysFromNow(-6),
    pinned: false,
  },
  {
    id: 'ann-3',
    title: '系统升级维护通知',
    content: '本周日 02:00 - 04:00 平台进行系统升级，期间下单、支付与查询服务可能出现短暂波动，升级完成后将恢复。给您带来不便敬请谅解。',
    type: 'Maintenance',
    publishedAt: daysFromNow(-3),
    pinned: false,
  },
  {
    id: 'ann-4',
    title: '新用户专享礼包上线',
    content: '2026 年新注册用户可领取新人专享礼包：新人首单立减 15 元券 × 1、无门槛包邮券 × 2、食品生鲜满 99 减 15 券 × 1，注册后 7 天内有效。',
    type: 'Promotion',
    publishedAt: daysFromNow(-1),
    pinned: false,
  },
]

export const seedDictionaries: DictionaryDto[] = [
  {
    code: 'after-sales-reasons',
    name: '售后申请原因',
    items: [
      { label: '不想要了 / 多拍错拍', value: 'NOT_WANTED' },
      { label: '尺码不合适', value: 'SIZE_MISMATCH' },
      { label: '质量问题', value: 'QUALITY_ISSUE' },
      { label: '包装破损', value: 'PACKAGE_DAMAGED' },
      { label: '发错货 / 少发', value: 'WRONG_DELIVERY' },
      { label: '与描述不符', value: 'NOT_AS_DESCRIBED' },
      { label: '其他', value: 'OTHER' },
    ],
  },
  {
    code: 'order-cancel-reasons',
    name: '订单取消原因',
    items: [
      { label: '不想买了', value: 'NOT_WANTED' },
      { label: '拍错了', value: 'ORDERED_WRONG' },
      { label: '地址填写错误', value: 'WRONG_ADDRESS' },
      { label: '优惠券无法使用', value: 'COUPON_INVALID' },
      { label: '价格变化', value: 'PRICE_CHANGED' },
      { label: '其他', value: 'OTHER' },
    ],
  },
  {
    code: 'address-tags',
    name: '地址标签',
    items: [
      { label: '家', value: 'HOME' },
      { label: '公司', value: 'COMPANY' },
      { label: '学校', value: 'SCHOOL' },
      { label: '亲友', value: 'FRIEND' },
    ],
  },
  {
    code: 'hot_search_keywords',
    name: '热门搜索词',
    items: [
      { label: 'iPhone 15', value: '98.5万' },
      { label: '夏季短袖T恤', value: '76.2万' },
      { label: '蓝牙耳机', value: '65.8万' },
      { label: '运动鞋', value: '54.1万' },
      { label: '空调', value: '48.3万' },
      { label: '防晒霜', value: '42.7万' },
      { label: '每日坚果', value: '38.9万' },
      { label: '充电宝', value: '35.4万' },
    ],
  },
]

// ---------------------------------------------------------------------------
// 运行时状态（handlers 可变状态，resetSeedData 时重置）
// ---------------------------------------------------------------------------

export interface MockRuntimeState {
  /** 2FA 二段登录票据 → 账号 */
  twoFactorTickets: Map<string, string>
  /** 订单号自增序号 */
  orderSeq: number
  /** 通知自增序号 */
  notificationSeq: number
  /** 已上报浏览的 SPU（避免重复插入历史头部） */
  reportedViews: Set<string>
}

export const runtime: MockRuntimeState = {
  twoFactorTickets: new Map(),
  orderSeq: 9,
  notificationSeq: 100,
  reportedViews: new Set(),
}

/** 深拷贝工具 */
function snapshot<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

/** 模块加载时捕获的原始快照（resetSeedData 的数据源，不可被运行时篡改） */
const initialSnapshots = {
  cart: snapshot(seedCartItems),
  myCoupons: snapshot(seedMyCoupons),
  orders: snapshot(seedOrders),
  payments: snapshot(seedPayments),
  myReviews: snapshot(seedMyReviews),
  afterSales: snapshot(seedAfterSales),
  pointsAccount: snapshot(seedPointsAccount),
  pointsLedger: snapshot(seedPointsLedger),
  pointsTasks: snapshot(seedPointsTasks),
  memberProfile: snapshot(seedMemberProfile),
  notifications: snapshot(seedNotifications),
  addresses: snapshot(seedAddresses),
  favorites: snapshot(seedFavorites),
  history: snapshot(seedBrowseHistory),
  availableCoupons: snapshot(seedAvailableCoupons),
  seckill: snapshot(seedSeckillActivities),
  user: snapshot(seedUser),
}

/** 深拷贝重置所有可变数据（mock/reset 端点调用），恢复到模块加载时的初始状态 */
export function resetSeedData(): void {
  seedCartItems.splice(0, seedCartItems.length, ...snapshot(initialSnapshots.cart))
  seedMyCoupons.splice(0, seedMyCoupons.length, ...snapshot(initialSnapshots.myCoupons))
  seedOrders.splice(0, seedOrders.length, ...snapshot(initialSnapshots.orders))
  seedPayments.splice(0, seedPayments.length, ...snapshot(initialSnapshots.payments))
  seedMyReviews.splice(0, seedMyReviews.length, ...snapshot(initialSnapshots.myReviews))
  seedAfterSales.splice(0, seedAfterSales.length, ...snapshot(initialSnapshots.afterSales))
  Object.assign(seedPointsAccount, snapshot(initialSnapshots.pointsAccount))
  seedPointsLedger.splice(0, seedPointsLedger.length, ...snapshot(initialSnapshots.pointsLedger))
  seedPointsTasks.splice(0, seedPointsTasks.length, ...snapshot(initialSnapshots.pointsTasks))
  Object.assign(seedMemberProfile, snapshot(initialSnapshots.memberProfile))
  seedNotifications.splice(0, seedNotifications.length, ...snapshot(initialSnapshots.notifications))
  seedAddresses.splice(0, seedAddresses.length, ...snapshot(initialSnapshots.addresses))
  seedFavorites.splice(0, seedFavorites.length, ...snapshot(initialSnapshots.favorites))
  seedBrowseHistory.splice(0, seedBrowseHistory.length, ...snapshot(initialSnapshots.history))
  seedAvailableCoupons.splice(
    0,
    seedAvailableCoupons.length,
    ...snapshot(initialSnapshots.availableCoupons),
  )
  seedSeckillActivities.splice(0, seedSeckillActivities.length, ...snapshot(initialSnapshots.seckill))
  Object.assign(seedUser, snapshot(initialSnapshots.user))
  runtime.twoFactorTickets.clear()
  runtime.orderSeq = 9
  runtime.notificationSeq = 100
  runtime.reportedViews.clear()
}

/** dev 模式装配时确保种子就绪（当前种子为静态初始化，保留幂等入口供扩展） */
export function ensureSeedData(): void {
  if (seedCartItems.length === 0) {
    resetSeedData()
  }
}
