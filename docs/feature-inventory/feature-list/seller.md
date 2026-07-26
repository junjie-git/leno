# 商家管理后台功能清单

> 来源：docs/design-prompts/seller/
> 页面数：23

| 序号 | 模块 | 页面 | 路由 | 实现状态 | 引用 API 端点 | 涉及 BC |
|-|-|-|-|-|-|-|
| 1 | 01-onboarding | application | /shop/application | ✅ | POST /api/shops/application, GET /api/shops/me | BC10 |
| 2 | 01-onboarding | qualifications | /shop/qualifications | ✅ | POST /api/shops/me/qualifications, GET /api/shops/me | BC10 |
| 3 | 01-onboarding | shop-preview | /shop/preview | 🚧 | GET /api/shops/me, GET /api/products?shopId={myShopId}&status=Listed | BC10+BC2 |
| 4 | 01-onboarding | shop-profile | /shop/profile | ✅ | GET /api/shops/me, PUT /api/shops/me | BC10 |
| 5 | 02-dashboard | overview | /dashboard/overview | ✅ | GET /api/seller/dashboard, GET /api/seller/sales-trend?from=&to= | BC10 |
| 6 | 02-dashboard | sales-trend | /dashboard/sales-trend | ✅ | GET /api/seller/sales-trend?from=&to=, GET /api/seller/metrics?from=&to= | BC10 |
| 7 | 02-dashboard | low-stock-alert | /dashboard/low-stock | ➕ | GET /api/products?shopId={myShopId}&status=Listed, GET /api/products/{id} | BC2 |
| 8 | 03-product-management | product-list | /products | ✅ | GET /api/seller/products, POST /api/seller/products/{id}/submit-review, POST /api/seller/products/{id}/take-down | BC2 |
| 9 | 03-product-management | product-edit | /products/new, /products/:id/edit | ✅ | POST /api/products, PUT /api/products/{id}, GET /api/products/{id}, POST /api/products/{id}/submit | BC2 |
| 10 | 03-product-management | sku-management | /products/:id/skus | ✅ | GET /api/products/{id}, POST /api/products/{id}/skus, PUT /api/products/{id}, POST /api/products/{id}/skus/{skuId}/price | BC2 |
| 11 | 03-product-management | price-history | /products/:id/price-history | ✅ | GET /api/products/{id}/price-history, GET /api/products/{id} | BC2 |
| 12 | 04-logistics | freight-templates | /logistics/freight-templates | ✅ | GET /api/seller/freight-templates/mine, POST /api/seller/freight-templates, PUT /api/seller/freight-templates/{id}/rules, POST /api/seller/freight-templates/{id}/enable, POST /api/seller/freight-templates/{id}/disable | BC4 |
| 13 | 04-logistics | logistics-companies | /logistics/companies | ➕ | GET /api/admin/logistics-companies | BC4 |
| 14 | 05-order-fulfillment | pending-shipment | /orders/pending-shipment | ➕ | POST /api/seller/orders/{id}/ship | BC4 |
| 15 | 05-order-fulfillment | order-list | /orders | ➕ | GET /api/seller/orders | BC4 |
| 16 | 05-order-fulfillment | logistics-trace | /orders/:id/trace | ✅ | GET /api/orders/{id}/logistics-trace | BC4 |
| 17 | 06-after-sales | after-sales-list | /after-sales | ✅ | GET /api/seller/after-sales | BC6 |
| 18 | 06-after-sales | after-sales-detail | /after-sales/:id | ➕ | GET /api/seller/after-sales/{id}, POST /api/seller/after-sales/{id}/approve, POST /api/seller/after-sales/{id}/reject, POST /api/seller/after-sales/{id}/confirm-return | BC6 |
| 19 | 07-review | review-reply | /reviews | ➕ | GET /api/seller/reviews, POST /api/reviews/{id}/reply | BC6 |
| 20 | 08-account | login | /login | ✅ | POST /api/auth/login, POST /api/auth/two-factor/verify, POST /api/auth/refresh-token, POST /api/auth/logout | BC1 |
| 21 | 08-account | profile | /account/profile | ✅ | GET /api/users/me, PUT /api/users/me, PUT /api/users/me/password, POST /api/users/me/two-factor/enable, POST /api/users/me/two-factor/confirm, POST /api/users/me/two-factor/disable | BC1 |
| 22 | 08-account | notifications | /account/notifications | ✅ | GET /api/notifications, GET /api/notifications/unread-count, POST /api/notifications/read, POST /api/notifications/read-all | BC9 |
| 23 | 09-export | sales-export | /export/sales | ➕ | GET /api/seller/sales-trend, GET /api/seller/metrics, POST /api/seller/export/sales, GET /api/seller/export/tasks, GET /api/seller/export/tasks/{id}/download | BC10 |
