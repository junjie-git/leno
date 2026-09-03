/**
 * SVG 图片生成器
 *
 * 与设计稿一致：商品图 / Banner / 头像等均为内联 SVG data URI（自包含、零外部依赖、离线可用）。
 * 视觉规格对齐 docs/designs/buyer-app 设计稿 —— 灰底 + 品类文字的商品占位、渐变 + 标题的 Banner。
 */

/** 对 SVG 源码做 URL 编码（data URI 的 utf8 形式） */
function encodeSvg(svg: string): string {
  return `data:image/svg+xml;utf8,${encodeURIComponent(svg)
    .replace(/'/g, '%27')
    .replace(/"/g, "'")}`
}

/** 品类色板（与设计稿分类入口色系一致） */
const PALETTE: Array<{ bg: string; fg: string }> = [
  { bg: '#E6F4FF', fg: '#1677FF' },
  { bg: '#FFF1F0', fg: '#FF4D4F' },
  { bg: '#F6FFED', fg: '#52C41A' },
  { bg: '#FFF7E6', fg: '#FAAD14' },
  { bg: '#F9F0FF', fg: '#722ED1' },
  { bg: '#E6FFFB', fg: '#13C2C2' },
  { bg: '#FFF0F6', fg: '#EB2F96' },
  { bg: '#F0F5FF', fg: '#2F54EB' },
]

function hashText(text: string): number {
  let h = 0
  for (let i = 0; i < text.length; i++) {
    h = (h * 31 + text.charCodeAt(i)) & 0x7fffffff
  }
  return h
}

/**
 * 商品图：浅灰底 + 品类文字标签（对齐设计稿 rec-card / 商品主图视觉）
 */
export function productImage(label: string, size = 300): string {
  const p = PALETTE[hashText(label) % PALETTE.length]
  const fontSize = Math.max(14, Math.round(size / 8))
  return encodeSvg(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}">` +
      `<rect width="${size}" height="${size}" fill="${p.bg}"/>` +
      `<text x="50%" y="52%" font-size="${fontSize}" text-anchor="middle" dominant-baseline="middle" ` +
      `fill="${p.fg}" font-family="PingFang SC, Microsoft YaHei, sans-serif" font-weight="500">${escapeXml(label)}</text>` +
      `</svg>`,
  )
}

/** Banner 背景：渐变 + 主/副标题（对齐设计稿 banner-slide 视觉） */
export function bannerImage(
  title: string,
  subtitle: string,
  from = '#1677FF',
  to = '#0952C9',
  width = 690,
  height = 320,
): string {
  return encodeSvg(
    `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">` +
      `<defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1">` +
      `<stop offset="0" stop-color="${from}"/><stop offset="1" stop-color="${to}"/></linearGradient></defs>` +
      `<rect width="${width}" height="${height}" fill="url(#g)"/>` +
      `<text x="48" y="${height / 2 - 12}" font-family="PingFang SC, Microsoft YaHei, sans-serif" font-size="52" font-weight="600" fill="#fff">${escapeXml(title)}</text>` +
      `<text x="48" y="${height / 2 + 28}" font-family="PingFang SC, Microsoft YaHei, sans-serif" font-size="28" fill="rgba(255,255,255,0.9)">${escapeXml(subtitle)}</text>` +
      `</svg>`,
  )
}

/** 用户头像：渐变圆角方块 + 昵称首字 */
export function avatarImage(nickname: string): string {
  const ch = nickname.trim().charAt(0).toUpperCase() || 'L'
  const p = PALETTE[hashText(nickname) % PALETTE.length]
  return encodeSvg(
    `<svg xmlns="http://www.w3.org/2000/svg" width="96" height="96">` +
      `<rect width="96" height="96" rx="48" fill="${p.fg}" opacity="0.12"/>` +
      `<text x="50%" y="54%" font-size="40" text-anchor="middle" dominant-baseline="middle" fill="${p.fg}" ` +
      `font-family="PingFang SC, Microsoft YaHei, sans-serif" font-weight="600">${escapeXml(ch)}</text>` +
      `</svg>`,
  )
}

/** 店铺头像：品牌色圆角矩形 + 店名首字 */
export function shopAvatar(name: string): string {
  const ch = name.trim().charAt(0).toUpperCase() || '铺'
  const p = PALETTE[hashText(name) % PALETTE.length]
  return encodeSvg(
    `<svg xmlns="http://www.w3.org/2000/svg" width="120" height="120">` +
      `<rect width="120" height="120" rx="24" fill="${p.fg}" opacity="0.14"/>` +
      `<text x="50%" y="54%" font-size="52" text-anchor="middle" dominant-baseline="middle" fill="${p.fg}" ` +
      `font-family="PingFang SC, Microsoft YaHei, sans-serif" font-weight="600">${escapeXml(ch)}</text>` +
      `</svg>`,
  )
}

/** 频道图标底色块（分类快捷入口 44px 图标底） */
export function categoryTile(label: string): string {
  return productImage(label, 88)
}

/** 转义 XML 特殊字符 */
function escapeXml(text: string): string {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}
