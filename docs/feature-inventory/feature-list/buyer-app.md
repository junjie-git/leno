# 买家端 APP 功能清单

> 来源：docs/design-prompts/buyer-app/
> 页面数：48

| 序号 | 模块 | 页面 | 路由 | 实现状态 | 引用 API 端点 | 涉及 BC |
|-|-|-|-|-|-|-|
| 1 | 01-auth | login | /login | ✅ | POST /api/account/login, POST /api/auth/refresh, GET /api/auth/oauth/{provider}/login | BC1 |
| 2 | 01-auth | register | /register | ✅ | POST /api/auth/register, POST /api/auth/forgot-password | BC1 |
| 3 | 01-auth | forgot-password | /forgot-password | ✅ | POST /api/auth/forgot-password, POST /api/auth/reset-password | BC1 |
| 4 | 01-auth | oauth-login | /oauth/:provider | ✅ | GET /api/auth/oauth/{provider}/login, GET /api/auth/oauth/{provider}/callback, POST /api/account/external-logins, DELETE /api/account/external-logins/{provider} | BC1 |
| 5 | 01-auth | two-factor | /two-factor | ✅ | POST /api/auth/two-factor/verify, POST /api/users/me/two-factor/enable, POST /api/users/me/two-factor/confirm, POST /api/users/me/two-factor/disable | BC1 |
| 6 | 02-home | home-feed | / | ➕ | GET /api/products/search, GET /api/seckill/activities, GET /api/categories/tree, GET /api/announcements, GET /api/notifications/unread-count | BC2+BC5+BC11+BC9 |
| 7 | 02-home | banner | 嵌入首页（无独立路由） | ➕ | GET /api/announcements | BC11 |
| 8 | 02-home | seckill-entry | 嵌入首页（无独立路由） | ✅ | GET /api/seckill/activities, GET /api/seckill/activities/{activityId} | BC5 |
| 9 | 03-catalog | category-nav | /category | ✅ | GET /api/categories/tree, GET /api/products/search | BC2 |
| 10 | 03-catalog | product-detail | /product/:id | ✅ | GET /api/products/{id}, GET /api/products/{id}/price-history, GET /api/products/{spuId}/reviews, POST /api/cart/items, POST /api/orders/buy-now | BC2+BC3+BC4 |
| 11 | 03-catalog | search-results | /search/results | ✅ | GET /api/products/search, GET /api/brands | BC2 |
| 12 | 03-catalog | search | /search | ✅ | GET /api/products/search | BC2 |
| 13 | 04-shop | shop-detail | /shop/:shopId | 🚧 | GET /api/products/search, GET /api/brands | BC2 |
| 14 | 05-cart | cart | /cart | ✅ | GET /api/cart, POST /api/cart/items, PUT /api/cart/items/{skuId}, DELETE /api/cart/items/{skuId}, POST /api/cart/items/select, PATCH /api/cart/selection, POST /api/cart/merge, POST /api/cart/preview | BC3 |
| 15 | 05-cart | checkout-preview | /checkout/preview | ✅ | POST /api/cart/preview, POST /api/orders/preview | BC3+BC4 |
| 16 | 05-cart | checkout-settle | /checkout/settle | ✅ | GET /api/addresses, POST /api/orders/preview, POST /api/orders, POST /api/orders/buy-now, GET /api/coupons/mine?status=Usable, GET /api/points/account | BC1+BC4+BC5+BC7 |
| 17 | 06-order | order-create | /order/create | ✅ | GET /api/products/{id}, GET /api/addresses, GET /api/points/account, POST /api/orders/buy-now | BC2+BC1+BC7+BC4 |
| 18 | 06-order | order-list | /orders | ✅ | GET /api/orders, POST /api/orders/{id}/cancel, POST /api/orders/{id}/confirm | BC4 |
| 19 | 06-order | order-detail | /order/:id | ✅ | GET /api/orders/{id}, POST /api/orders/{id}/cancel, POST /api/orders/{id}/confirm, POST /api/payments?orderId={id} | BC4+BC8 |
| 20 | 06-order | logistics-trace | /order/:id/logistics | ✅ | GET /api/orders/{id}, GET /api/orders/{id}/logistics | BC4 |
| 21 | 06-order | seckill-order | /seckill/order/:activityId | ✅ | GET /api/seckill/activities/{activityId}, POST /api/seckill/activities/{activityId}/place, GET /api/addresses | BC5+BC1 |
| 22 | 07-payment | payment-initiate | /payment/initiate/:orderId | ✅ | GET /api/orders/{id}, POST /api/payments | BC4+BC8 |
| 23 | 07-payment | payment-result | /payment/result/:orderId | ✅ | GET /api/orders/{id}, GET /api/payments/result/{orderId} | BC4+BC8 |
| 24 | 08-promotion | coupons-available | /coupons/available | ✅ | GET /api/coupons/available, POST /api/coupons/{couponId}/receive | BC5 |
| 25 | 08-promotion | my-coupons | /coupons/mine | ✅ | GET /api/coupons/mine | BC5 |
| 26 | 09-review | my-reviews | /reviews/mine | ✅ | GET /api/reviews/mine, POST /api/reviews/{reviewId}/append | BC6 |
| 27 | 09-review | product-reviews | /product/:spuId/reviews | ✅ | GET /api/products/{spuId}/reviews | BC2 |
| 28 | 09-review | review-submit | /review/submit/:orderLineId | ✅ | GET /api/orders/{orderId}, POST /api/orders/{orderId}/reviews | BC4 |
| 29 | 10-after-sales | after-sales-apply | /after-sales/apply/:orderLineId | ✅ | GET /api/orders/{orderId}, POST /api/after-sales, POST /api/after-sales/images | BC4+BC6 |
| 30 | 10-after-sales | after-sales-detail | /after-sales/:id | ✅ | GET /api/after-sales/order/{orderId}, POST /api/after-sales/{id}/cancel, POST /api/after-sales/{id}/return-goods, GET /api/refunds/{afterSalesId} | BC6+BC8 |
| 31 | 10-after-sales | my-after-sales | /after-sales/mine | ✅ | GET /api/after-sales/mine, POST /api/after-sales/{id}/cancel, POST /api/after-sales/{id}/return-goods | BC6 |
| 32 | 11-points-membership | points-account | /points/account | ✅ | GET /api/points/account, GET /api/points/ledger, POST /api/points/check-in | BC7 |
| 33 | 11-points-membership | points-ledger | /points/ledger | ✅ | GET /api/points/ledger, GET /api/points/account | BC7 |
| 34 | 11-points-membership | check-in | /points/check-in | ✅ | POST /api/points/check-in, GET /api/points/account | BC7 |
| 35 | 11-points-membership | tasks-center | /points/tasks | ✅ | GET /api/points/tasks, POST /api/points/tasks/{taskId}/complete | BC7 |
| 36 | 11-points-membership | points-exchange | /points/exchange | ✅ | GET /api/points/account, POST /api/points/exchange-coupon, GET /api/coupons/claimable | BC7+BC5 |
| 37 | 11-points-membership | member-level | /member/level | ✅ | GET /api/members/me | BC7 |
| 38 | 11-points-membership | membership-packages | /member/packages | ✅ | GET /api/membership-packages, POST /api/membership-packages/{packageId}/subscribe, GET /api/members/me | BC7 |
| 39 | 12-notification | notifications | /notifications | ✅ | GET /api/notifications, GET /api/notifications/unread-count, POST /api/notifications/read, POST /api/notifications/read-all | BC9 |
| 40 | 12-notification | preferences | /notifications/preferences | ✅ | GET /api/users/me/notification-preferences, PUT /api/users/me/notification-preferences | BC1 |
| 41 | 13-profile | profile | /profile | ✅ | GET /api/users/me, PUT /api/users/me | BC1 |
| 42 | 13-profile | addresses | /profile/addresses | ✅ | GET /api/users/me/addresses, POST /api/users/me/addresses, PUT /api/users/me/addresses/{id}, DELETE /api/users/me/addresses/{id}, POST /api/users/me/addresses/{id}/default | BC1 |
| 43 | 13-profile | security | /profile/security | ✅ | GET /api/users/me, PUT /api/users/me/password, POST /api/users/me/two-factor/enable, POST /api/users/me/two-factor/confirm, POST /api/users/me/two-factor/disable, POST /api/account/external-logins, DELETE /api/account/external-logins/{provider} | BC1 |
| 44 | 13-profile | favorites | /profile/favorites | ➕ | GET /api/users/me/favorites, POST /api/users/me/favorites, DELETE /api/users/me/favorites/{spuId}, POST /api/users/me/favorites/batch-delete, GET /api/users/me/favorites/count | BC1 |
| 45 | 13-profile | history | /profile/history | ➕ | GET /api/users/me/browse-history, POST /api/users/me/browse-history, DELETE /api/users/me/browse-history/{id}, POST /api/users/me/browse-history/batch-delete, DELETE /api/users/me/browse-history | BC1 |
| 46 | 13-profile | settings | /settings | ✅ | POST /api/auth/logout, GET /api/users/me/notification-preferences, PUT /api/users/me/notification-preferences | BC1 |
| 47 | 14-public | announcements | /announcements | ✅ | GET /api/announcements | BC11 |
| 48 | 14-public | dictionaries | /dictionaries/:code | ✅ | GET /api/dictionaries/{code} | BC11 |
