# Leno 功能清单与 API 缺失对比报告

**文档版本**：V1.0
**创建日期**：2026-07-26
**关联设计**：`docs/superpowers/specs/2026-07-26-feature-inventory-and-api-gap-design.md`
**关联计划**：`docs/superpowers/plans/2026-07-26-feature-inventory-and-api-gap.md`
**关联源数据**：
- 设计提示词：`docs/design-prompts/`（143 个 Markdown）
- 设计稿：`docs/designs/`（153 个 HTML，仅作页面参考）
- 源码 Controllers：`src/Services/{BC}/**/Controllers/*.cs`（71 个 Controller 文件）

---

## 1. 方法论

采用 Subagent-Driven 三阶段并行方案：

1. **阶段 0**：主代理创建目录骨架与 BC 报告统一模板
2. **阶段 1**：4 个端级 subagent 并行抽取功能清单（buyer-app / operations / seller / system-admin）
3. **阶段 2**：11 个 BC 级 subagent 并行做 API 缺失对比（分 5+5+1 三批）
4. **阶段 3**：主代理聚合产出总览、清单索引与顶层 README

详细方法论见关联设计文档。

---

## 2. 报告索引

### 功能清单（按端拆分）

| 端 | 文件 | 页面数 |
|-|-|-|
| 买家端 APP | [feature-list/buyer-app.md](./feature-list/buyer-app.md) | 48 |
| 运营管理后台 | [feature-list/operations.md](./feature-list/operations.md) | 34 |
| 商家管理后台 | [feature-list/seller.md](./feature-list/seller.md) | 23 |
| 系统管理后台 | [feature-list/system-admin.md](./feature-list/system-admin.md) | 28 |

### API 缺失对比报告（按 BC 拆分）

| BC | 文件 | 涉及端 |
|-|-|-|
| BC1 用户与认证授权 | [api-gap/bc1-user-auth.md](./api-gap/bc1-user-auth.md) | buyer-app + operations + system-admin |
| BC2 商品 | [api-gap/bc2-product.md](./api-gap/bc2-product.md) | buyer-app + operations + seller |
| BC3 购物车 | [api-gap/bc3-cart.md](./api-gap/bc3-cart.md) | buyer-app |
| BC4 订单与交易 | [api-gap/bc4-order.md](./api-gap/bc4-order.md) | buyer-app + operations + seller |
| BC5 促销 | [api-gap/bc5-promotion.md](./api-gap/bc5-promotion.md) | buyer-app + operations |
| BC6 评价与售后 | [api-gap/bc6-review-aftersales.md](./api-gap/bc6-review-aftersales.md) | buyer-app + operations + seller |
| BC7 积分与会员 | [api-gap/bc7-points-membership.md](./api-gap/bc7-points-membership.md) | buyer-app + operations |
| BC8 支付集成 | [api-gap/bc8-payment.md](./api-gap/bc8-payment.md) | buyer-app + operations |
| BC9 消息通知 | [api-gap/bc9-notification.md](./api-gap/bc9-notification.md) | 4 端 |
| BC10 卖家与店铺 | [api-gap/bc10-seller-shop.md](./api-gap/bc10-seller-shop.md) | buyer-app + operations + seller |
| BC11 系统管理 | [api-gap/bc11-system-admin.md](./api-gap/bc11-system-admin.md) | system-admin |

### 总览

| 文件 | 内容 |
|-|-|
| [api-gap/00-summary.md](./api-gap/00-summary.md) | 11 BC 差异统计 + 优先级矩阵 + Top 10 修复项 |
| [feature-list/README.md](./feature-list/README.md) | 4 端清单索引 + 统计 |

---

## 3. BC → 源码目录映射表

| BC | 源码目录 | 涉及端 |
|-|-|-|
| BC1 用户与认证授权 | `src/Services/UserAuth/` + `src/Services/Identity/` | buyer-app + operations + system-admin |
| BC2 商品 | `src/Services/Product/` + `src/Services/Inventory/` | buyer-app + operations + seller |
| BC3 购物车 | `src/Services/Cart/` | buyer-app |
| BC4 订单与交易 | `src/Services/Order/` | buyer-app + operations + seller |
| BC5 促销 | `src/Services/Promotion/` | buyer-app + operations |
| BC6 评价与售后 | `src/Services/ReviewAfterSales/` + `src/Services/AfterSales/` | buyer-app + operations + seller |
| BC7 积分与会员 | `src/Services/PointsMembership/` + `src/Services/Points/` + `src/Services/Membership/` | buyer-app + operations |
| BC8 支付集成 | `src/Services/Payment/` | buyer-app + operations |
| BC9 消息通知 | `src/Services/Notification/` | 4 端 |
| BC10 卖家与店铺 | `src/Services/SellerShop/` | buyer-app + operations + seller |
| BC11 系统管理 | `src/Services/SystemAdmin/` | system-admin |

---

## 4. 4 类差异定义

| 类别 | 说明 |
|-|-|
| 缺失 | 设计稿需要但后端未提供（design-prompts 标 🚧/➕ 且源码无实现） |
| 闲置 | 后端已有但设计稿未调用（源码有实现但 design-prompts 无页面引用） |
| 路径不一致 | 方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配 |
| 能力不匹配 | 分页/筛选/排序/批量/字段过滤等能力差异 |

---

## 5. 拆分过渡态说明

| 主 BC | 旧 BC | 新 BC | 过渡策略 |
|-|-|-|-|
| BC1 用户与认证授权 | UserAuth | Identity | 双轨期优先引用 UserAuth，Identity 端点标 🚧 待切换 |
| BC6 评价与售后 | ReviewAfterSales | AfterSales（独立） | 双轨期优先引用 ReviewAfterSales，AfterSales 端点标 🚧 待切换 |
| BC7 积分与会员 | PointsMembership | Points + Membership | 双轨期优先引用 PointsMembership，新拆分端点标 🚧 待切换 |
