# 系统管理后台功能清单

> 来源：docs/design-prompts/system-admin/
> 页面数：28

| 序号 | 模块 | 页面 | 路由 | 实现状态 | 引用 API 端点 | 涉及 BC |
|-|-|-|-|-|-|-|
| 1 | 01-dashboard | after-sales-stats | /dashboard/after-sales-stats | ✅ | GET /api/admin/dashboard/after-sales-stats | BC11 |
| 2 | 01-dashboard | notification-delivery | /dashboard/notification-delivery | ✅ | GET /api/admin/dashboard/notification-delivery | BC11 |
| 3 | 01-dashboard | operations-overview | /dashboard/operations-overview | ✅ | GET /api/admin/dashboard/overview | BC11 |
| 4 | 01-dashboard | payment-stats | /dashboard/payment-stats | ✅ | GET /api/admin/dashboard/payment-stats | BC11 |
| 5 | 01-dashboard | points-stats | /dashboard/points-stats | ✅ | GET /api/admin/dashboard/points-stats | BC11 |
| 6 | 01-dashboard | report-snapshots | /dashboard/report-snapshots | ✅ | GET /api/admin/dashboard/reports, GET /api/admin/dashboard/reports/{id} | BC11 |
| 7 | 01-dashboard | shop-ranking | /dashboard/shop-ranking | ✅ | GET /api/admin/dashboard/shop-ranking | BC11 |
| 8 | 02-user-access | oauth-clients | /user-access/oauth-clients | ✅ | GET /api/admin/oauth-clients, POST /api/admin/oauth-clients/{provider}, PUT /api/admin/oauth-clients/{provider}, POST /api/admin/oauth-clients/{provider}/enable, POST /api/admin/oauth-clients/{provider}/disable | BC1 |
| 9 | 02-user-access | operators | /user-access/operators | ✅ | GET /api/admin/operators, GET /api/admin/operators/{operatorId}, POST /api/admin/operators, PUT /api/admin/operators/{operatorId}/permissions, POST /api/admin/operators/{operatorId}/activate, POST /api/admin/operators/{operatorId}/deactivate | BC11 |
| 10 | 02-user-access | role-management | /user-access/roles | ✅ | GET /api/admin/roles, GET /api/admin/roles/{roleId}, POST /api/admin/roles, PUT /api/admin/roles/{roleId}, DELETE /api/admin/roles/{roleId}, GET /api/admin/roles/{roleId}/permissions, PUT /api/admin/roles/{roleId}/permissions | BC1 |
| 11 | 02-user-access | user-management | /user-access/users | ✅ | GET /api/admin/users, GET /api/admin/users/{id}, PUT /api/admin/users/{id}/roles, PUT /api/admin/users/{id}/status | BC1 |
| 12 | 03-system-governance | announcements | /system-governance/announcements | ✅ | GET /api/admin/announcements, POST /api/admin/announcements, PUT /api/admin/announcements/{announcementId}, POST /api/admin/announcements/{announcementId}/publish, POST /api/admin/announcements/{announcementId}/unpublish, GET /api/announcements | BC11 |
| 13 | 03-system-governance | data-dictionaries | /system-governance/data-dictionaries | ✅ | GET /api/admin/dictionaries, POST /api/admin/dictionaries, PUT /api/admin/dictionaries/{dictionaryId}, POST /api/admin/dictionaries/{dictionaryId}/enable, POST /api/admin/dictionaries/{dictionaryId}/disable, POST /api/admin/dictionaries/{dictionaryId}/items, PUT /api/admin/dictionaries/{dictionaryId}/items/{itemId}, DELETE /api/admin/dictionaries/{dictionaryId}/items/{itemId}, GET /api/dictionaries/{code} | BC11 |
| 14 | 03-system-governance | feature-flags | /system-governance/feature-flags | ✅ | GET /api/admin/feature-flags, POST /api/admin/feature-flags, PUT /api/admin/feature-flags/{flagId}, POST /api/admin/feature-flags/{flagId}/enable, POST /api/admin/feature-flags/{flagId}/disable, POST /api/admin/feature-flags/evaluate | BC11 |
| 15 | 03-system-governance | system-configs | /system-governance/system-configs | ✅ | GET /api/admin/system-configs, GET /api/admin/system-configs/groups, GET /api/admin/system-configs/by-key/{key}, POST /api/admin/system-configs, PUT /api/admin/system-configs/{configId}, POST /api/admin/system-configs/{configId}/enable, POST /api/admin/system-configs/{configId}/disable | BC11 |
| 16 | 04-runtime-ops | alert-management | /runtime-ops/alert-management | 🚧 | GET /api/admin/alerts, GET /api/admin/alerts/{id}, POST /api/admin/alerts/{id}/acknowledge, POST /api/admin/alerts/silences, GET /api/admin/alerts/silences, DELETE /api/admin/alerts/silences/{id} | BC11 |
| 17 | 04-runtime-ops | dead-letter-queue | /runtime-ops/dead-letter-queue | ✅ | GET /api/admin/dead-letters, GET /api/admin/dead-letters/{id}, POST /api/admin/dead-letters/{id}/retry, POST /api/admin/dead-letters/{id}/discard, POST /api/admin/dead-letters/batch-retry, POST /api/admin/dead-letters/batch-discard | BC11 |
| 18 | 04-runtime-ops | health-monitoring | /runtime-ops/health-monitoring | ✅ | GET /api/admin/health, GET /api/admin/health/modules | BC11 |
| 19 | 04-runtime-ops | index-rebuild | /runtime-ops/index-rebuild | ✅ | GET /api/admin/index-rebuild/tasks, POST /api/admin/index-rebuild/trigger, GET /api/admin/index-rebuild/tasks/{id}, POST /api/admin/index-rebuild/tasks/{id}/retry | BC11 |
| 20 | 04-runtime-ops | rate-limit-rules | /runtime-ops/rate-limit-rules | ✅ | GET /api/admin/rate-limit-rules, GET /api/admin/rate-limit-rules/{id}, POST /api/admin/rate-limit-rules, PUT /api/admin/rate-limit-rules/{id}, POST /api/admin/rate-limit-rules/{id}/enable, POST /api/admin/rate-limit-rules/{id}/disable | BC11 |
| 21 | 04-runtime-ops | scheduled-tasks | /runtime-ops/scheduled-tasks | ✅ | GET /api/admin/scheduled-tasks, POST /api/admin/scheduled-tasks, PUT /api/admin/scheduled-tasks/{taskId}, POST /api/admin/scheduled-tasks/{taskId}/enable, POST /api/admin/scheduled-tasks/{taskId}/disable, POST /api/admin/scheduled-tasks/{taskId}/run-now | BC11 |
| 22 | 05-audit | audit-logs | /audit/audit-logs | ✅ | GET /api/admin/audit-logs, GET /api/admin/audit-logs/{id}, GET /api/admin/audit-logs/export, GET /api/admin/operation-logs, GET /api/admin/audit-log-entries | BC11 |
| 23 | 05-audit | outbox-monitor | /audit/outbox-monitor | 🚧 | GET /api/admin/outbox/summary, GET /api/admin/outbox/trend, GET /api/admin/outbox/{context}/messages, POST /api/admin/outbox/{context}/republish, POST /api/admin/outbox/{context}/archive, GET /api/admin/outbox/{context}/archive-history | BC11 |
| 24 | 05-audit | reconciliation | /audit/reconciliation | ✅ | GET /api/admin/statistics/reconciliation-status, POST /api/admin/statistics/reconcile, GET /api/admin/statistics/reconciliation-records | BC11 |
| 25 | 06-account | login-2fa | /login | ➕ | POST /api/auth/login, POST /api/auth/two-factor/verify, POST /api/auth/forgot-password, POST /api/auth/reset-password | BC1 |
| 26 | 06-account | notifications | /account/notifications | ✅ | GET /api/notifications, GET /api/notifications/unread-count, POST /api/notifications/read, POST /api/notifications/read-all | BC9 |
| 27 | 06-account | profile | /account/profile | ✅ | GET /api/users/me, PUT /api/users/me, PUT /api/users/me/password, POST /api/users/me/two-factor/enable, POST /api/users/me/two-factor/confirm, POST /api/users/me/two-factor/disable | BC1 |
| 28 | 07-monitoring | prometheus-dashboard | /monitoring/prometheus-dashboard | ➕ | GET /api/admin/monitoring/metrics/summary, GET /api/admin/monitoring/metrics/query, GET /api/admin/monitoring/metrics/trend, GET /api/admin/monitoring/instances | BC11 |
