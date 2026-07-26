# 功能清单索引

**文档版本**：V1.0
**创建日期**：2026-07-26

---

## 1. 4 端清单统计

| 端 | 文件 | 页面数 | 实现状态分布 |
|-|-|-|-|
| 买家端 APP | [buyer-app.md](./buyer-app.md) | 48 | ✅×43 / 🚧×1 / ➕×4 |
| 运营管理后台 | [operations.md](./operations.md) | 34 | ✅×29 / 🚧×2 / ➕×3 |
| 商家管理后台 | [seller.md](./seller.md) | 23 | ✅×15 / 🚧×1 / ➕×7 |
| 系统管理后台 | [system-admin.md](./system-admin.md) | 28 | ✅×24 / 🚧×2 / ➕×2 |
| **合计** | | **133** | ✅×111 / 🚧×6 / ➕×16 |

> 实现状态分布由各端清单「实现状态」列统计得出

---

## 2. BC 分布统计

按 feature-list 中「涉及 BC」列统计页面数（一个页面可能涉及多个 BC，故总和 ≥ 133）：

| BC | 中文名 | 涉及页面数 | 报告链接 |
|-|-|-|-|
| BC1 | 用户与认证授权 | ~18 | [../api-gap/bc1-user-auth.md](../api-gap/bc1-user-auth.md) |
| BC2 | 商品 | ~15 | [../api-gap/bc2-product.md](../api-gap/bc2-product.md) |
| BC3 | 购物车 | 3 | [../api-gap/bc3-cart.md](../api-gap/bc3-cart.md) |
| BC4 | 订单与交易 | ~16 | [../api-gap/bc4-order.md](../api-gap/bc4-order.md) |
| BC5 | 促销 | ~8 | [../api-gap/bc5-promotion.md](../api-gap/bc5-promotion.md) |
| BC6 | 评价与售后 | ~10 | [../api-gap/bc6-review-aftersales.md](../api-gap/bc6-review-aftersales.md) |
| BC7 | 积分与会员 | ~9 | [../api-gap/bc7-points-membership.md](../api-gap/bc7-points-membership.md) |
| BC8 | 支付集成 | ~5 | [../api-gap/bc8-payment.md](../api-gap/bc8-payment.md) |
| BC9 | 消息通知 | ~8 | [../api-gap/bc9-notification.md](../api-gap/bc9-notification.md) |
| BC10 | 卖家与店铺 | ~12 | [../api-gap/bc10-seller-shop.md](../api-gap/bc10-seller-shop.md) |
| BC11 | 系统管理 | ~30 | [../api-gap/bc11-system-admin.md](../api-gap/bc11-system-admin.md) |

> "~" 表示跨 BC 页面被多端统计；精确数字以各端清单为准

---

## 3. 使用方式

1. **按端查找页面**：打开对应 {端}.md，按模块/路由定位
2. **按 BC 查找 API 缺失**：跳转 [../api-gap/bc{N}-{name}.md](../api-gap/bc1-user-auth.md)
3. **查看全局总览**：[../api-gap/00-summary.md](../api-gap/00-summary.md)

---

## 4. 抽取过程

由 4 个端级 subagent 并行扫描 `docs/design-prompts/{端}/` 下 143 个 Markdown（不含 4 个 00-overview.md）产出：
- 每个 design-prompts 页面对应一行
- 字段：序号 / 模块 / 页面 / 路由 / 实现状态 / 引用 API 端点 / 涉及 BC
- 引用 API 端点逐条列出（METHOD /api/path），不解析参数
- 涉及 BC 根据 API 路径前缀推断

抽取过程未读取源码 Controller，差异分析在 [../api-gap/](../api-gap/) 各 BC 报告中完成。
