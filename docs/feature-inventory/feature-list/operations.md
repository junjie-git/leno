# 运营管理后台功能清单

> 来源：docs/design-prompts/operations/
> 页面数：34

| 序号 | 模块 | 页面 | 路由 | 实现状态 | 引用 API 端点 | 涉及 BC |
|-|-|-|-|-|-|-|
| 1 | 01-dashboard | operations-overview | /dashboard/overview | ✅ | GET /api/admin/dashboard/overview | BC11 |
| 2 | 01-dashboard | payment-stats | /dashboard/payment-stats | ✅ | GET /api/admin/dashboard/payment-stats | BC11 |
| 3 | 01-dashboard | points-stats | /dashboard/points-stats | ✅ | GET /api/admin/dashboard/points-stats | BC11 |
| 4 | 01-dashboard | notification-delivery | /dashboard/notification-delivery | ✅ | GET /api/admin/dashboard/notification-delivery, GET /api/admin/notifications/statistics | BC11+BC9 |
| 5 | 01-dashboard | after-sales-stats | /dashboard/after-sales-stats | ✅ | GET /api/admin/dashboard/after-sales-stats | BC11 |
| 6 | 01-dashboard | shop-ranking | /dashboard/shop-ranking | ✅ | GET /api/admin/dashboard/shop-ranking | BC11 |
| 7 | 02-product-ops | product-audit | /product-ops/product-audit | ✅ | GET /api/admin/products/all, POST /api/admin/products/{id}/approve, POST /api/admin/products/{id}/reject, POST /api/admin/products/{id}/skus/{skuId}/stock, POST /api/admin/products/skus/{skuId}/replenish | BC2 |
| 8 | 02-product-ops | brand-management | /product-ops/brand-management | ✅ | GET /api/brands, GET /api/brands/{id}, POST /api/admin/brands, PUT /api/admin/brands/{id}, POST /api/admin/brands/{id}/enable, POST /api/admin/brands/{id}/disable | BC2 |
| 9 | 02-product-ops | category-management | /product-ops/category-management | ✅ | GET /api/categories/tree, GET /api/categories/{id}, POST /api/admin/categories, PUT /api/admin/categories/{id}, POST /api/admin/categories/{id}/enable, POST /api/admin/categories/{id}/disable | BC2 |
| 10 | 03-promotion-ops | promotions | /promotion-ops/promotions | ✅ | GET /api/admin/promotions, GET /api/admin/promotions/{activityId}, POST /api/admin/promotions, PUT /api/admin/promotions/{activityId}, POST /api/admin/promotions/{activityId}/activate, POST /api/admin/promotions/{activityId}/pause, POST /api/admin/promotions/{activityId}/close | BC5 |
| 11 | 03-promotion-ops | coupons | /promotion-ops/coupons | ✅ | GET /api/admin/coupons, POST /api/admin/coupons, PUT /api/admin/coupons/{couponId}, POST /api/admin/coupons/{couponId}/publish, POST /api/admin/coupons/{couponId}/stop, POST /api/admin/coupons/{couponId}/issue | BC5 |
| 12 | 03-promotion-ops | seckill | /promotion-ops/seckill | ✅ | GET /api/admin/seckill/activities, POST /api/admin/seckill/activities, POST /api/admin/seckill/activities/{activityId}/activate, POST /api/admin/seckill/activities/{activityId}/close | BC5 |
| 13 | 04-seller-ops | application-audit | /seller-ops/application-audit | ✅ | GET /api/admin/shops, GET /api/admin/shops/{id}, POST /api/admin/shops/{id}/approve, POST /api/admin/shops/{id}/reject, GET /api/admin/shops/{id}/qualifications, POST /api/admin/shops/{id}/qualifications/{qualId}/approve, POST /api/admin/shops/{id}/qualifications/{qualId}/reject | BC10 |
| 14 | 04-seller-ops | shop-governance | /seller-ops/shop-governance | ✅ | GET /api/admin/shops, GET /api/admin/shops/{id}, POST /api/admin/shops/{id}/suspend, POST /api/admin/shops/{id}/resume, POST /api/admin/shops/{id}/close, GET /api/admin/shops/{id}/qualifications, POST /api/admin/shops/{id}/qualifications/{qualId}/approve, POST /api/admin/shops/{id}/qualifications/{qualId}/reject | BC10 |
| 15 | 04-seller-ops | seller-statistics | /seller-ops/seller-statistics | 🚧 | GET /api/admin/dashboard/shop-ranking, GET /api/admin/shops | BC11+BC10 |
| 16 | 05-order-ops | order-management | /order-ops/orders | ✅ | GET /api/admin/orders, POST /api/admin/orders/{id}/force-cancel | BC4 |
| 17 | 05-order-ops | after-sales | /order-ops/after-sales | ✅ | GET /api/admin/after-sales, POST /api/admin/after-sales/{id}/approve, POST /api/admin/after-sales/{id}/reject | BC6 |
| 18 | 05-order-ops | review-audit | /order-ops/review-audit | ✅ | GET /api/admin/reviews, POST /api/admin/reviews/{id}/approve, POST /api/admin/reviews/{id}/hide | BC6 |
| 19 | 05-order-ops | logistics-companies | /order-ops/logistics-companies | ✅ | GET /api/admin/logistics-companies, POST /api/admin/logistics-companies, PUT /api/admin/logistics-companies/{id}, POST /api/admin/logistics-companies/{id}/enable, POST /api/admin/logistics-companies/{id}/disable | BC4 |
| 20 | 06-payment-ops | payment-records | /payment-ops/payment-records | ✅ | GET /api/admin/payments | BC8 |
| 21 | 06-payment-ops | refund-records | /payment-ops/refund-records | ✅ | GET /api/admin/refunds | BC8 |
| 22 | 06-payment-ops | payment-channels | /payment-ops/payment-channels | ✅ | GET /api/admin/payment-channels, GET /api/admin/payment-channels/{id}, PUT /api/admin/payment-channels/{id}, POST /api/admin/payment-channels/{id}/enable, POST /api/admin/payment-channels/{id}/disable | BC8 |
| 23 | 07-notification-ops | templates | /notification-ops/templates | ✅ | GET /api/admin/notification-templates, GET /api/admin/notification-templates/{templateId}, POST /api/admin/notification-templates, PUT /api/admin/notification-templates/{templateId}, POST /api/admin/notification-templates/{templateId}/enable, POST /api/admin/notification-templates/{templateId}/disable, POST /api/admin/notification-templates/{templateId}/preview | BC9 |
| 24 | 07-notification-ops | records | /notification-ops/records | ✅ | GET /api/notifications/records, GET /api/notifications/records/{id}, GET /api/notifications/records/by-business/{businessRef}, POST /api/admin/notifications/records/{id}/resend, GET /api/admin/notifications/statistics | BC9 |
| 25 | 07-notification-ops | config | /notification-ops/config | ✅ | GET /api/admin/notification-config, PUT /api/admin/notification-config, POST /api/admin/notification-config/test | BC9 |
| 26 | 07-notification-ops | rate-limits | /notification-ops/rate-limits | ✅ | GET /api/admin/notification-rate-limits, PUT /api/admin/notification-rate-limits | BC9 |
| 27 | 08-membership-ops | member-levels | /membership-ops/member-levels | ✅ | GET /api/admin/members/levels, POST /api/admin/members/levels, PUT /api/admin/members/levels/{levelId}, POST /api/admin/members/levels/{levelId}/enable, POST /api/admin/members/levels/{levelId}/disable | BC7 |
| 28 | 08-membership-ops | membership-packages | /membership-ops/membership-packages | ✅ | GET /api/membership-packages, POST /api/admin/membership-packages, PUT /api/admin/membership-packages/{packageId}, POST /api/admin/membership-packages/{packageId}/enable, POST /api/admin/membership-packages/{packageId}/disable | BC7 |
| 29 | 08-membership-ops | points-rules | /membership-ops/points-rules | 🚧 | POST /api/admin/points/award | BC7 |
| 30 | 09-account | login | /login | ✅ | POST /api/auth/login, POST /api/auth/two-factor/verify, POST /api/auth/refresh-token, POST /api/auth/logout, POST /api/auth/forgot-password, POST /api/auth/reset-password | BC1 |
| 31 | 09-account | profile | /account/profile | ✅ | GET /api/users/me, PUT /api/users/me, PUT /api/users/me/password, POST /api/users/me/two-factor/enable, POST /api/users/me/two-factor/confirm, POST /api/users/me/two-factor/disable, POST /api/account/external-logins, DELETE /api/account/external-logins/{provider} | BC1 |
| 32 | 09-account | notifications | /account/notifications | ✅ | GET /api/notifications, GET /api/notifications/unread-count, POST /api/notifications/read, POST /api/notifications/read-all | BC9 |
| 33 | 09-account | todo-workbench | /account/todo | ➕ | GET /api/admin/products/all, GET /api/admin/shops, GET /api/admin/after-sales, GET /api/admin/reviews, GET /api/notifications/records | BC2+BC10+BC6+BC9 |
| 34 | 10-data-export | export-center | /data-export/export-center | ➕ | POST /api/admin/data-exports, GET /api/admin/data-exports, GET /api/admin/data-exports/{taskId}, GET /api/admin/data-exports/{taskId}/download, DELETE /api/admin/data-exports/{taskId} | BC11 |
