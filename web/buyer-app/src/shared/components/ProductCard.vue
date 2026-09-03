<script setup lang="ts">
import { useRouter } from 'vue-router'
import PriceText from './PriceText.vue'
import { formatSales } from '@/shared/utils/format'
import type { ProductSummaryDto } from '@/modules/03-catalog/types/product.dto'

/**
 * 商品卡片（推荐流双列瀑布卡片）
 *
 * 对齐设计稿 rec-card：图、双行标题、标签、价格 + 月销。
 */
const props = defineProps<{
  product: ProductSummaryDto
}>()

const router = useRouter()

/** 促销类标签（红底），其余为蓝底 */
const PROMO_TAG_KEYWORDS = ['秒杀', '满减', '补贴', '直降', '限时'] as const

function isPromoTag(tag: string): boolean {
  return PROMO_TAG_KEYWORDS.some((k) => tag.includes(k))
}

function goDetail(): void {
  router.push(`/product/${props.product.id}`)
}
</script>

<template>
  <div class="product-card" @click="goDetail">
    <img class="img" :src="product.mainImage" :alt="product.name" loading="lazy" />
    <div class="info">
      <div class="name text-ellipsis-2">{{ product.name }}</div>
      <div v-if="product.tags.length > 0" class="tags">
        <span v-for="tag in product.tags.slice(0, 2)" :key="tag" class="tag" :class="{ blue: !isPromoTag(tag) }">
          {{ tag }}
        </span>
      </div>
      <div class="price-row">
        <PriceText :amount="product.priceMin" :size="16" />
        <span class="sales">月销 {{ formatSales(product.sales) }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.product-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  overflow: hidden;
  box-shadow: var(--sh-card);
}

.img {
  width: 100%;
  aspect-ratio: 1;
  object-fit: cover;
  background: var(--n3);
}

.info {
  padding: var(--s2);
}

.name {
  font-size: var(--fs-base);
  color: var(--n10);
  line-height: 1.4;
  height: 40px;
}

.tags {
  display: flex;
  gap: var(--s1);
  margin-top: var(--s1);
}

.tag {
  font-size: 10px;
  padding: 1px 5px;
  border-radius: var(--r-base);
  background: #fff1f0;
  color: var(--c-error);
}

.tag.blue {
  background: #e6f4ff;
  color: var(--c-primary);
}

.price-row {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--s2);
  margin-top: var(--s2);
}

.sales {
  font-size: var(--fs-sm);
  color: var(--n7);
  flex-shrink: 0;
}
</style>
