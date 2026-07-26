# 域拆分迁移设计：结束 UserAuth/PointsMembership/ReviewAfterSales 双轨期

> **Spec ID**: 2026-07-26-domain-split-migration
> **作者**: Brainstorming Session
> **状态**: Draft（待用户审阅）
> **创建日期**: 2026-07-26
> **目标完成日期**: 阶段 4 旧域下线（预计 T4，约 6-8 周）

## 0. 背景与问题陈述

### 0.1 背景

系统经历域拆分演进：
- **UserAuth 域** → 拆分为 Identity 域（认证）+ AccessControl 域（权限）
- **PointsMembership 域** → 拆分为 Points 域（积分）+ Membership 域（会员）
- **ReviewAfterSales 域** → 拆分为 Review 域（评价）+ AfterSales 域（售后）

### 0.2 当前问题（双轨期现状）

源码扫描事实（2026-07-26）：

| 旧域 | HTTP 端点数 | gRPC 端点数 | 新域 HTTP 端点数 | 新域 gRPC 端点数 | 双轨状态 |
|------|------------|------------|-----------------|-----------------|---------|
| UserAuth | 47 | 0 | Identity 3 + AccessControl 0 | AccessControl 2 | 仅 3 对 auth 端点路径重叠，44 端点未迁移 |
| PointsMembership | 28 | 1（条件启用） | Membership 9 + Points 0 | 0 | Membership 9 端点契约不一致，Points 完全空白 |
| ReviewAfterSales | 25 | 1（条件启用） | Review 0 + AfterSales 0 | 0 | HTTP/gRPC 均未进入双轨 |

**核心问题**：
1. 真正双轨的端点仅 3 对（UserAuth/Identity 的 auth），其余 ~100 端点要么未迁移，要么新域契约不一致
2. PointsMembership 域 4 个 Internal 端点代码标注 `[Obsolete]` 2026-08-01 下线，deadline 临近
3. 新 Membership 域 9 个端点路径风格（`[controller]` 小写化）、鉴权（Policy PBAC）、响应包装（裸 `ActionResult<T>`）与系统其他域（Order/Payment/Cart 等用 `ApiResponse<T>` + 显式连字符路径 + 角色 RBAC）不一致

### 0.3 设计目标

1. **结束双轨期**：三域全量迁移，仅保留单一端点，3 个旧域完全下线
2. **契约统一**：以旧域风格（`ApiResponse<T>` + 显式连字符路径 + 角色 RBAC）为准，与系统整体保持一致
3. **内部服务间调用重建**：HTTP internal 端点 + gRPC 服务双轨重建
4. **三域并行同步切换**：统一进入双轨验证期，统一旧域下线

## 1. 目标架构与统一契约规范

### 1.1 目标架构

迁移完成后，系统由 3 个旧域演进为 7 个新域，旧域全部下线：

```
旧域                          新域（迁移后）
─────────────────────────────────────────────────────────────
UserAuth               →拆分→  Identity        (认证+用户资料+OAuth客户端+用户管理+内部PII)
                              AccessControl   (角色CRUD+权限，补HTTP Controller)
                              UserCenter      (新建：地址+收藏+浏览历史+通知偏好)

PointsMembership       →拆分→  Points          (积分账户+流水+签到+兑换+任务+规则+内部端点+gRPC)
                              Membership      (会员套餐+订阅+等级+成员信息)

ReviewAfterSales       →拆分→  Review          (评价+追评+图片+回复+审核+gRPC)
                              AfterSales      (售后申请+退货+审核+凭证图片)
```

### 1.2 统一契约规范（以旧域风格为准）

所有新域 Controller 必须遵循以下规范，与系统已实现域（Order/Payment/Cart/Notification 等）保持一致：

| 维度 | 规范 | 示例 |
|------|------|------|
| **类级路由** | 显式 `[Route("api/xxx")]` 连字符命名，禁用 `[controller]` 占位符 | `[Route("api/membership-packages")]` |
| **管理端分层** | 运营端路径统一前缀 `api/admin/`，与买家端 `api/` 物理分离 | `api/admin/members/levels` vs `api/members/me` |
| **响应包装** | 统一 `ApiResponse<T>`（`Leno.SharedContracts.Responses`），成功 `Success(data)`，失败 `Fail(code,msg)` | `return Ok(ApiResponse.Success(result));` |
| **鉴权模型** | `[Authorize(Roles = "Buyer,Operator,Admin")]` 角色 RBAC，禁用 Policy PBAC | `[Authorize(Roles = "Operator,Admin")]` |
| **`/me` 端点** | 当前用户资源走 `/me`，从 JWT 解析 userId，禁用客户端传 userId | `GET api/members/me` |
| **创建动作** | POST 创建成功返回 `200 OK` + `ApiResponse.Success(obj)`，不用 201 CreatedAtAction | `return Ok(ApiResponse.Success(created));` |
| **启停动作** | POST `/enable`、`/disable` 返回 `200 OK` + `ApiResponse.Success()`，不用 204 | `return Ok(ApiResponse.Success());` |
| **Controller 基类** | 各域定义 `<Domain>ControllerBase : ControllerBase`，注入 `ICurrentUserContext`，提供 `GetCurrentUserId()` | 复用旧域 `UserAuthControllerBase` 模式 |
| **匿名端点** | 显式标注 `[AllowAnonymous]`，不依赖"未标即匿名" | `[AllowAnonymous]` on `GET api/products/{spuId}/reviews` |
| **Internal 端点** | 路径 `internal/v1/<domain>/*`，无类级 `[Route]`，走 `InternalApiKeyMiddleware` | `[HttpPost("internal/v1/points/freeze")]` |

### 1.3 新 Membership 域返工清单

新 Membership 域现有 9 个端点全部不符合规范，需返工：

| 现有端点（新 Membership） | 问题 | 返工方向 |
|---------------------------|------|----------|
| `GET api/membershippackages` | 路径小写无连字符 + 无鉴权 | 改为 `GET api/membership-packages`，加 `[Authorize(Roles="Buyer")]` |
| `POST api/membershippackages` (AdminOnly) | 路径 + Policy PBAC + 201 | 改为 `POST api/admin/membership-packages`，角色 RBAC，200 + ApiResponse |
| `PUT/POST enable/disable` | 同上 | 路径改 `api/admin/membership-packages/...`，返回 200 |
| `GET api/members/{userId}` | 越权风险 | 改为 `GET api/members/me`，从 JWT 取 userId |
| `GET api/members/levels` 无鉴权 | 鉴权降级 | 改为 `GET api/admin/members/levels`，`[Authorize(Roles="Operator,Admin")]` |
| `POST/PUT api/members/levels` (AdminOnly) | Policy + 201 | 角色改 `Operator,Admin`，返回 200 |

## 2. 三域端点迁移清单

### 2.1 UserAuth 域迁移清单（47 端点 → 3 个新域）

#### 2.1.1 → Identity 域（认证 + 用户资料 + OAuth 客户端 + 用户管理 + 内部 PII）

Identity 现有 3 个端点（login/refresh/logout）需返工对齐契约，并补齐 25 个端点。

| 旧端点（UserAuth） | 新端点（Identity） | 迁移类型 | Application 层缺口 |
|--------------------|-------------------|----------|-------------------|
| POST `api/auth/login` | POST `api/auth/login` | **返工**：响应改 `ApiResponse<TokenDto>` | 无 |
| POST `api/auth/refresh-token` | POST `api/auth/refresh-token` | **返工**：路径从 `refresh` 改回 `refresh-token`，响应包装 | 无 |
| POST `api/auth/logout` | POST `api/auth/logout` | **返工**：响应包装；语义保留旧域"吊销单个 jti"行为 | 无 |
| POST `api/auth/register` | POST `api/auth/register` | **新建 Controller** | Identity 需补 `IAuthAppService.RegisterAsync` |
| GET `api/auth/oauth/{provider}/login` | GET `api/auth/oauth/{provider}/login` | **新建** | 补 `IOAuthService.GetLoginUrlAsync` |
| GET `api/auth/oauth/{provider}/callback` | GET `api/auth/oauth/{provider}/callback` | **新建** | 补 `IOAuthService.HandleCallbackAsync` |
| POST `api/auth/two-factor/verify` | POST `api/auth/two-factor/verify` | **新建** | 补 `ITwoFactorService.VerifyAsync` |
| POST `api/auth/forgot-password` | POST `api/auth/forgot-password` | **新建** | 补 `IPasswordService.ForgotPasswordAsync` |
| POST `api/auth/reset-password` | POST `api/auth/reset-password` | **新建** | 补 `IPasswordService.ResetPasswordAsync` |
| GET `api/users/me` | GET `api/users/me` | **新建** | 补 `IUserProfileAppService.GetProfileAsync` |
| PUT `api/users/me` | PUT `api/users/me` | **新建** | 补 `UpdateProfileAsync` |
| PUT `api/users/me/password` | PUT `api/users/me/password` | **新建** | 补 `ChangePasswordAsync` |
| POST `api/users/me/two-factor/enable` | POST `api/users/me/two-factor/enable` | **新建** | 补 `EnableTwoFactorAsync` |
| POST `api/users/me/two-factor/confirm` | POST `api/users/me/two-factor/confirm` | **新建** | 补 `ConfirmTwoFactorAsync` |
| POST `api/users/me/two-factor/disable` | POST `api/users/me/two-factor/disable` | **新建** | 补 `DisableTwoFactorAsync` |
| POST `api/account/external-logins` | POST `api/account/external-logins` | **新建** | 补 `IExternalLoginService.BindAsync` |
| DELETE `api/account/external-logins/{provider}` | DELETE `api/account/external-logins/{provider}` | **新建** | 补 `UnbindAsync` |
| GET `api/admin/oauth-clients` | GET `api/admin/oauth-clients` | **新建** | 补 `IOAuthClientAppService.GetAllAsync` |
| POST `api/admin/oauth-clients/{provider}` | POST `api/admin/oauth-clients/{provider}` | **新建** | 补 `CreateAsync` |
| PUT `api/admin/oauth-clients/{provider}` | PUT `api/admin/oauth-clients/{provider}` | **新建** | 补 `UpdateAsync` |
| POST `api/admin/oauth-clients/{provider}/enable` | POST `api/admin/oauth-clients/{provider}/enable` | **新建** | 补 `EnableAsync` |
| POST `api/admin/oauth-clients/{provider}/disable` | POST `api/admin/oauth-clients/{provider}/disable` | **新建** | 补 `DisableAsync` |
| GET `api/admin/users` | GET `api/admin/users` | **新建** | 补 `IUserAdminAppService.QueryUsersAsync` |
| GET `api/admin/users/{id}` | GET `api/admin/users/{id}` | **新建** | 补 `GetUserAsync` |
| POST `api/admin/users/{id}/roles` | POST `api/admin/users/{id}/roles` | **新建** | 补 `AssignRolesAsync`（调 AccessControl） |
| POST `api/admin/users/{id}/suspend` | POST `api/admin/users/{id}/suspend` | **新建** | 补 `SuspendAsync` |
| POST `api/admin/users/{id}/resume` | POST `api/admin/users/{id}/resume` | **新建** | 补 `ResumeAsync` |
| GET `internal/v1/users/{userId}/contacts` | GET `internal/v1/users/{userId}/contacts` | **新建** | 补 `IUserInternalAppService.GetContactsAsync` |
| GET `internal/v1/users/{userId}/contacts/full` | GET `internal/v1/users/{userId}/contacts/full` | **新建** | 补 `GetFullContactsAsync` |

**Identity 域新建 Controller 清单**（6 个）：`AuthController`（返工 3 + 新建 6）、`UsersController`（6）、`AccountController`（2）、`AdminOAuthClientsController`（5）、`AdminUsersController`（5）、`InternalUsersController`（2）。

#### 2.1.2 → AccessControl 域（角色 CRUD + 权限，补 HTTP Controller）

AccessControl 现仅 gRPC（`CheckPermission`/`GetUserRoles`），需补 7 个 HTTP 端点。

| 旧端点（UserAuth AdminRolesController） | 新端点（AccessControl） | Application 层缺口 |
|------------------------------------------|------------------------|-------------------|
| GET `api/admin/roles` | GET `api/admin/roles` | 补 `IRoleAppService.QueryRolesAsync` |
| GET `api/admin/roles/{roleId}` | GET `api/admin/roles/{roleId}` | 补 `GetRoleAsync` |
| POST `api/admin/roles` | POST `api/admin/roles` | 补 `CreateRoleAsync` |
| PUT `api/admin/roles/{roleId}` | PUT `api/admin/roles/{roleId}` | 补 `UpdateRoleAsync` |
| DELETE `api/admin/roles/{roleId}` | DELETE `api/admin/roles/{roleId}` | 补 `DeleteRoleAsync` |
| GET `api/admin/roles/{roleId}/permissions` | GET `api/admin/roles/{roleId}/permissions` | 补 `GetRolePermissionsAsync` |
| PUT `api/admin/roles/{roleId}/permissions` | PUT `api/admin/roles/{roleId}/permissions` | 补 `UpdateRolePermissionsAsync` |

**AccessControl 域新建 Controller**（1 个）：`AdminRolesController`，沿用 `[ApiController] + [Route("api/admin/roles")]`。gRPC 服务保留。

#### 2.1.3 → UserCenter 域（新建域：地址 + 收藏 + 浏览历史 + 通知偏好）

UserCenter 是全新域，需搭建 `Leno.UserCenter.Api/Application/Domain/Infrastructure` 项目骨架，承载 17 个端点。

| 旧端点（UserAuth） | 新端点（UserCenter） | 路径不变 |
|--------------------|---------------------|---------|
| GET `api/users/me/addresses` | GET `api/users/me/addresses` | ✅ |
| POST `api/users/me/addresses` | POST `api/users/me/addresses` | ✅ |
| PUT `api/users/me/addresses/{id}` | PUT `api/users/me/addresses/{id}` | ✅ |
| DELETE `api/users/me/addresses/{id}` | DELETE `api/users/me/addresses/{id}` | ✅ |
| POST `api/users/me/addresses/{id}/default` | POST `api/users/me/addresses/{id}/default` | ✅ |
| GET `api/users/me/favorites` | GET `api/users/me/favorites` | ✅ |
| POST `api/users/me/favorites` | POST `api/users/me/favorites` | ✅ |
| DELETE `api/users/me/favorites/{spuId}` | DELETE `api/users/me/favorites/{spuId}` | ✅ |
| POST `api/users/me/favorites/batch-delete` | POST `api/users/me/favorites/batch-delete` | ✅ |
| GET `api/users/me/favorites/count` | GET `api/users/me/favorites/count` | ✅ |
| GET `api/users/me/browse-history` | GET `api/users/me/browse-history` | ✅ |
| POST `api/users/me/browse-history` | POST `api/users/me/browse-history` | ✅ |
| DELETE `api/users/me/browse-history/{id}` | DELETE `api/users/me/browse-history/{id}` | ✅ |
| POST `api/users/me/browse-history/batch-delete` | POST `api/users/me/browse-history/batch-delete` | ✅ |
| DELETE `api/users/me/browse-history` | DELETE `api/users/me/browse-history` | ✅ |
| GET `api/users/me/notification-preferences` | GET `api/users/me/notification-preferences` | ✅ |
| PUT `api/users/me/notification-preferences` | PUT `api/users/me/notification-preferences` | ✅ |

**UserCenter 域新建 Controller**（4 个）：`AddressesController`、`FavoritesController`、`BrowseHistoryController`、`NotificationPreferencesController`。Application 层接口从 UserAuth 迁移（含防腐层 `IProductPricingQueryService` 调用）。

**通知偏好跨域去重**：HTTP 端点统一归 UserCenter（业务语义属用户中心）；BC9 Notification 域 `NotificationPreferencesController` 改为 internal HTTP 端点（`internal/v1/users/{userId}/notification-preferences`）供通知发送时查询，不对外暴露。

### 2.2 PointsMembership 域迁移清单（28 端点 → 2 个新域）

#### 2.2.1 → Points 域（积分账户 + 流水 + 签到 + 兑换 + 任务 + 规则 + Internal + gRPC）

Points 域现 0 个 HTTP 端点，需从零搭建 16 个端点。

| 旧端点（PointsMembership） | 新端点（Points） | Application 层缺口 |
|----------------------------|-----------------|-------------------|
| POST `api/points/check-in` | POST `api/points/check-in` | 补 `ICheckInAppService.CheckInAsync` |
| GET `api/points/account` | GET `api/points/account` | 对齐现有 `GetOrCreateAccountAsync` 为 `GetAccountAsync` |
| GET `api/points/ledger` | GET `api/points/ledger` | 对齐 `GetFlowsAsync` 为 `GetLedgerAsync` |
| POST `api/points/exchange-coupon` | POST `api/points/exchange-coupon` | 补 `IExchangeCouponAppService.ExchangeAsync` |
| POST `api/admin/points/award` | POST `api/admin/points/award` | 补 `IAwardAppService.AwardAsync` |
| GET `api/points/tasks` | GET `api/points/tasks` | 补 `ITaskAppService.GetTasksAsync` |
| POST `api/points/tasks/{taskId}/complete` | POST `api/points/tasks/{taskId}/complete` | 补 `CompleteTaskAsync` |
| GET `api/admin/points/rules` | GET `api/admin/points/rules` | 补 `IPointsRuleAppService.GetRulesAsync` |
| POST `api/admin/points/rules` | POST `api/admin/points/rules` | 补 `CreateRuleAsync` |
| PUT `api/admin/points/rules/{ruleId}` | PUT `api/admin/points/rules/{ruleId}` | 补 `UpdateRuleAsync` |
| POST `api/admin/points/rules/{ruleId}/enable` | POST `api/admin/points/rules/{ruleId}/enable` | 补 `EnableRuleAsync` |
| POST `api/admin/points/rules/{ruleId}/disable` | POST `api/admin/points/rules/{ruleId}/disable` | 补 `DisableRuleAsync` |
| POST `internal/v1/points/trial-offset` | POST `internal/v1/points/trial-offset` | **重设计** `IPointsInternalAppService`，补 `TrialOffsetAsync`/`FreezeAsync`/`ReleaseAsync`/`ConfirmAsync` |
| POST `internal/v1/points/freeze` | POST `internal/v1/points/freeze` | 同上 |
| POST `internal/v1/points/release` | POST `internal/v1/points/release` | 同上 |
| POST `internal/v1/points/confirm` | POST `internal/v1/points/confirm` | 同上 |

**Points 域新建 Controller**（5 个）：`PointsController`（4 端点）、`TasksController`（2）、`PointsRulesController`（5）、`InternalPointsController`（4，单路径不再双路由）、`AdminPointsController`（1 award）。

**gRPC 重建**：旧 `PointsGrpcService` 在新 Points 域 `Program.cs` 复刻 `app.MapGrpcService<PointsGrpcService>()` 条件映射（`AntiCorruption:UseGrpc=true`）。

#### 2.2.2 → Membership 域（会员套餐 + 订阅 + 等级 + 成员信息）

Membership 域现 9 个端点全部返工，并补齐 3 个缺失端点，共 12 个端点。

| 旧端点（PointsMembership） | 新端点（Membership） | 迁移类型 |
|----------------------------|---------------------|----------|
| GET `api/membership-packages` | GET `api/membership-packages` | **返工**：路径从 `membershippackages` 改连字符，加 `[Authorize(Roles="Buyer")]` |
| POST `api/membership-packages/{id}/subscribe` | POST `api/membership-packages/{id}/subscribe` | **新建**：补 `IMembershipPackageAppService.SubscribeAsync` |
| POST `api/admin/membership-packages` | POST `api/admin/membership-packages` | **返工**：路径改 `api/admin/`，角色 RBAC，200+ApiResponse |
| PUT `api/admin/membership-packages/{id}` | PUT `api/admin/membership-packages/{id}` | **返工** |
| POST `api/admin/membership-packages/{id}/enable` | POST `api/admin/membership-packages/{id}/enable` | **返工** |
| POST `api/admin/membership-packages/{id}/disable` | POST `api/admin/membership-packages/{id}/disable` | **返工** |
| GET `api/members/me` | GET `api/members/me` | **返工**：从 `{userId}` 改回 `/me`，从 JWT 取 userId |
| GET `api/admin/members/levels` | GET `api/admin/members/levels` | **返工**：路径改 `api/admin/`，加角色鉴权 |
| POST `api/admin/members/levels` | POST `api/admin/members/levels` | **返工** |
| PUT `api/admin/members/levels/{id}` | PUT `api/admin/members/levels/{id}` | **返工** |
| POST `api/admin/members/levels/{id}/enable` | POST `api/admin/members/levels/{id}/enable` | **新建**：补 `IMemberAppService.EnableLevelAsync` |
| POST `api/admin/members/levels/{id}/disable` | POST `api/admin/members/levels/{id}/disable` | **新建**：补 `DisableLevelAsync` |

**Membership 域 Controller 返工**（2 个）：`MembershipPackagesController`、`MembersController`。

### 2.3 ReviewAfterSales 域迁移清单（25 HTTP + 2 gRPC → 2 个新域）

#### 2.3.1 → Review 域（评价 + 追评 + 图片 + 回复 + 审核 + gRPC）

Review 域现 0 个 HTTP 端点，需搭建 11 个端点。

| 旧端点（ReviewAfterSales） | 新端点（Review） | Application 层缺口 |
|----------------------------|-----------------|-------------------|
| POST `api/reviews` | POST `api/reviews` | 无（`SubmitReviewAsync` 已就绪） |
| GET `api/reviews/order-line/{orderLineId}` | GET `api/reviews/order-line/{orderLineId}` | 无 |
| GET `api/products/{spuId}/reviews` | GET `api/products/{spuId}/reviews` | 无 |
| GET `api/reviews/mine` | GET `api/reviews/mine` | 无 |
| POST `api/reviews/{id}/append` | POST `api/reviews/{id}/append` | **补** `AppendAdditionalReviewAsync` |
| POST `api/reviews/images` | POST `api/reviews/images` | 无（依赖共享内核 `IFileStorageService`） |
| POST `api/reviews/{id}/reply` | POST `api/reviews/{id}/reply` | 无 |
| GET `api/seller/reviews` | GET `api/seller/reviews` | **补** `GetBySellerAsync` |
| POST `api/admin/reviews/{id}/approve` | POST `api/admin/reviews/{id}/approve` | 无 |
| POST `api/admin/reviews/{id}/hide` | POST `api/admin/reviews/{id}/hide` | 无 |
| GET `api/admin/reviews` | GET `api/admin/reviews` | 无 |

**Review 域新建 Controller**（3 个，按角色拆分）：`ReviewsController`（买家 5）、`SellerReviewsController`（卖家 2）、`AdminReviewsController`（运营 3）。图片上传归买家 Controller。

**gRPC 重建**：新建 `Leno.Review.Api.GrpcServices/ReviewGrpcService.cs`，复用 `IReviewInternalQueryService`，复刻 `MapGrpcService` 条件映射。

#### 2.3.2 → AfterSales 域（售后申请 + 退货 + 审核 + 凭证图片）

AfterSales 域现 0 个 HTTP 端点，需搭建 14 个端点。

| 旧端点（ReviewAfterSales） | 新端点（AfterSales） | Application 层缺口 |
|----------------------------|---------------------|-------------------|
| POST `api/after-sales` | POST `api/after-sales` | 无 |
| POST `api/after-sales/{id}/return-goods` | POST `api/after-sales/{id}/return-goods` | 无 |
| POST `api/after-sales/{id}/cancel` | POST `api/after-sales/{id}/cancel` | 无 |
| GET `api/after-sales/order/{orderId}` | GET `api/after-sales/order/{orderId}` | 无 |
| GET `api/after-sales/mine` | GET `api/after-sales/mine` | 无 |
| POST `api/after-sales/images` | POST `api/after-sales/images` | 无 |
| GET `api/seller/after-sales` | GET `api/seller/after-sales` | 无 |
| GET `api/seller/after-sales/{id}` | GET `api/seller/after-sales/{id}` | **补** `GetByIdForSellerAsync` |
| POST `api/seller/after-sales/{id}/approve` | POST `api/seller/after-sales/{id}/approve` | 无 |
| POST `api/seller/after-sales/{id}/reject` | POST `api/seller/after-sales/{id}/reject` | 无 |
| POST `api/seller/after-sales/{id}/confirm-return` | POST `api/seller/after-sales/{id}/confirm-return` | 无 |
| POST `api/admin/after-sales/{id}/approve` | POST `api/admin/after-sales/{id}/approve` | 无 |
| POST `api/admin/after-sales/{id}/reject` | POST `api/admin/after-sales/{id}/reject` | 无 |
| GET `api/admin/after-sales` | GET `api/admin/after-sales` | 无 |

**AfterSales 域新建 Controller**（3 个）：`AfterSalesController`（买家 6）、`SellerAfterSalesController`（卖家 5）、`AdminAfterSalesController`（运营 3）。

### 2.4 迁移工作量汇总

| 新域 | 新建 Controller | 新建 HTTP 端点 | 返工 HTTP 端点 | Application 层补齐方法 | gRPC 重建 |
|------|----------------|---------------|---------------|----------------------|-----------|
| Identity | 6 | 25 | 3 | 13 | 无 |
| AccessControl | 1 | 7 | 0 | 7 | 保留 |
| UserCenter（新域） | 4 | 17 | 0 | 迁移现有 | 无 |
| Points | 5 | 16 | 0 | 13 + Internal 重设计 | 1 服务 |
| Membership | 2 | 3 | 9 | 3 | 无 |
| Review | 3 | 11 | 0 | 2 | 1 服务 |
| AfterSales | 3 | 14 | 0 | 1 | 无 |
| **合计** | **24** | **93** | **12** | **39 + Internal 重设计** | **2** |

## 3. 迁移执行策略（三域并行同步切换）

### 3.1 总体节奏：四阶段同步推进

```
阶段 1：补齐新域（T0 → T1）
  ├─ 三域并行补齐 Application 层缺口（39 方法 + Internal 重设计）
  ├─ 三域并行新建/返工 24 个 Controller（105 端点）
  ├─ 重建 2 个 gRPC 服务
  └─ 单元测试覆盖（每端点 ≥ 3 用例：成功/失败/鉴权）
  交付物：新域代码就绪，但流量仍走旧域

阶段 2：网关双轨（T1 → T2，建议 1-2 周）
  ├─ API Gateway 配置双轨路由：新域端点挂载，旧域端点保留
  ├─ 灰度策略：按用户 ID hash 灰度 5% → 25% → 50% → 100%
  ├─ 契约测试（Pact）：新域端点与旧域端点契约等价性验证
  ├─ 监控：对比新旧域端点的响应时延、错误率、业务指标
  └─ 回滚开关：网关层一键切回旧域（feature flag）
  交付物：100% 流量切到新域，旧域仅保留作回滚兜底

阶段 3：观察期（T2 → T3，建议 2 周）
  ├─ 100% 流量走新域，旧域不接流量但服务在线
  ├─ 业务指标观察：订单转化、支付成功率、积分发放、评价提交等
  ├─ 日志对比：新域错误日志趋稳，无 P0/P1 事故
  └─ 旧域流量归零确认（网关监控）
  交付物：观察期无异常，具备下线条件

阶段 4：旧域下线（T3 → T4）
  ├─ 移除网关旧域路由
  ├─ 停止旧域服务进程（UserAuth / PointsMembership / ReviewAfterSales）
  ├─ 归档旧域代码仓库（标记 deprecated，不删除）
  ├─ 更新文档：design-prompts、API 清单、架构图
  └─ 移除旧域依赖的数据库/Redis/消息队列资源
  交付物：旧域完全下线，双轨期结束
```

### 3.2 阶段 1 并行任务分解（关键路径）

#### Track A：UserAuth 域拆分（Identity + AccessControl + UserCenter）

```
A1. Identity Application 层补齐（13 方法）
    ├─ IAuthAppService: RegisterAsync
    ├─ IOAuthService: GetLoginUrlAsync, HandleCallbackAsync
    ├─ ITwoFactorService: VerifyAsync, EnableTwoFactorAsync, ConfirmTwoFactorAsync, DisableTwoFactorAsync
    ├─ IPasswordService: ForgotPasswordAsync, ResetPasswordAsync
    ├─ IUserProfileAppService: GetProfileAsync, UpdateProfileAsync, ChangePasswordAsync
    ├─ IUserAdminAppService: QueryUsersAsync, GetUserAsync, AssignRolesAsync, SuspendAsync, ResumeAsync
    ├─ IExternalLoginService: BindAsync, UnbindAsync
    ├─ IOAuthClientAppService: GetAllAsync, CreateAsync, UpdateAsync, EnableAsync, DisableAsync
    └─ IUserInternalAppService: GetContactsAsync, GetFullContactsAsync
    阻塞：AccessControl gRPC GetUserRoles（AssignRoles 依赖）

A2. AccessControl Application 层补齐（7 方法）+ HTTP Controller
    ├─ IRoleAppService: QueryRolesAsync, GetRoleAsync, CreateRoleAsync, UpdateRoleAsync, DeleteRoleAsync
    ├─ IRolePermissionAppService: GetRolePermissionsAsync, UpdateRolePermissionsAsync
    └─ AdminRolesController（7 端点）
    并行：与 A1 无依赖

A3. UserCenter 域骨架搭建 + Application 层迁移
    ├─ 新建 Leno.UserCenter.Api/Application/Domain/Infrastructure 项目
    ├─ 迁移 Addresses/Favorites/BrowseHistory/NotificationPreferences 的 Application 层
    ├─ 迁移防腐层依赖（IProductPricingQueryService 等）
    └─ 注册 DbContext、MassTransit、Redis 等
    并行：与 A1/A2 无依赖

A4. Identity Controller 层（6 Controller，28 端点）
    依赖：A1 完成
    并行：A2/A3 同步推进

A5. UserCenter Controller 层（4 Controller，17 端点）
    依赖：A3 完成
    并行：A4 同步推进

A6. 集成测试
    ├─ Identity ApiTests（28 端点 × 3 用例 = 84）
    ├─ AccessControl ApiTests（7 端点 × 3 用例 = 21）
    └─ UserCenter ApiTests（17 端点 × 3 用例 = 51）
```

#### Track B：PointsMembership 域拆分（Points + Membership）

```
B1. Points Application 层补齐（13 方法 + Internal 重设计）
    ├─ ICheckInAppService: CheckInAsync
    ├─ 对齐 IPointsAppService: GetAccountAsync, GetLedgerAsync（重命名）
    ├─ IExchangeCouponAppService: ExchangeAsync
    ├─ IAwardAppService: AwardAsync
    ├─ ITaskAppService: GetTasksAsync, CompleteTaskAsync
    ├─ IPointsRuleAppService: GetRulesAsync, CreateRuleAsync, UpdateRuleAsync, EnableRuleAsync, DisableRuleAsync
    └─ IPointsInternalAppService 重设计: TrialOffsetAsync, FreezeAsync, ReleaseAsync, ConfirmAsync, GrantLevelBonusAsync（保留）
    阻塞：旧域 InternalPointsController 双路由下线（2026-08-01 deadline）

B2. Membership Application 层补齐（3 方法）+ Controller 返工
    ├─ IMembershipPackageAppService: 补 SubscribeAsync
    ├─ IMemberAppService: 补 EnableLevelAsync, DisableLevelAsync
    ├─ MembershipPackagesController 返工（路径/鉴权/响应包装）
    └─ MembersController 返工（/me 恢复、路径/鉴权）
    并行：与 B1 无依赖

B3. Points Controller 层（5 Controller，16 端点）+ gRPC 重建
    依赖：B1 完成

B4. 集成测试
    ├─ Points ApiTests（16 端点 × 3 用例 = 48）
    └─ Membership ApiTests（12 端点 × 3 用例 = 36）
```

#### Track C：ReviewAfterSales 域拆分（Review + AfterSales）

```
C1. Review Application 层补齐（2 方法）
    ├─ IReviewAppService: 补 AppendAdditionalReviewAsync, GetBySellerAsync
    └─ 迁移文件上传依赖（IFileStorageService, IFileSignatureDetector）

C2. AfterSales Application 层补齐（1 方法）
    └─ IAfterSalesAppService: 补 GetByIdForSellerAsync

C3. Review Controller 层（3 Controller，11 端点）+ gRPC 重建
    依赖：C1 完成

C4. AfterSales Controller 层（3 Controller，14 端点）
    依赖：C2 完成
    并行：与 C3 无依赖

C5. 集成测试
    ├─ Review ApiTests（11 端点 × 3 用例 = 33）
    └─ AfterSales ApiTests（14 端点 × 3 用例 = 42）
```

### 3.3 阶段 2 网关双轨与灰度策略

#### 3.3.1 API Gateway 路由配置

双轨期网关同时挂载新旧域端点，按灰度比例分流。路由策略：

```
api/auth/*              → Identity (新) / UserAuth (旧)   按灰度
api/users/me/*          → Identity (新) / UserAuth (旧)   按灰度
api/account/*           → Identity (新) / UserAuth (旧)   按灰度
api/admin/users/*       → Identity (新) / UserAuth (旧)   按灰度
api/admin/roles/*       → AccessControl (新) / UserAuth (旧)  按灰度
api/admin/oauth-clients/* → Identity (新) / UserAuth (旧)  按灰度
api/users/me/addresses/* → UserCenter (新) / UserAuth (旧)  按灰度
api/users/me/favorites/* → UserCenter (新) / UserAuth (旧)  按灰度
api/users/me/browse-history/* → UserCenter (新) / UserAuth (旧)  按灰度
api/users/me/notification-preferences/* → UserCenter (新) / UserAuth (旧)  按灰度
internal/v1/users/*     → Identity (新) / UserAuth (旧)   100% 切新域（内部调用优先切换）
api/points/*            → Points (新) / PointsMembership (旧)  按灰度
api/admin/points/*      → Points (新) / PointsMembership (旧)  按灰度
api/points/tasks/*      → Points (新) / PointsMembership (旧)  按灰度
internal/v1/points/*    → Points (新) / PointsMembership (旧)  100% 切新域（deadline 驱动）
api/membership-packages/* → Membership (新) / PointsMembership (旧)  按灰度
api/members/me          → Membership (新) / PointsMembership (旧)  按灰度
api/admin/members/*     → Membership (新) / PointsMembership (旧)  按灰度
api/reviews/*           → Review (新) / ReviewAfterSales (旧)  按灰度
api/products/{spuId}/reviews → Review (新) / ReviewAfterSales (旧)  按灰度
api/seller/reviews      → Review (新) / ReviewAfterSales (旧)  按灰度
api/admin/reviews/*     → Review (新) / ReviewAfterSales (旧)  按灰度
api/after-sales/*       → AfterSales (新) / ReviewAfterSales (旧)  按灰度
api/seller/after-sales/* → AfterSales (新) / ReviewAfterSales (旧)  按灰度
api/admin/after-sales/* → AfterSales (新) / ReviewAfterSales (旧)  按灰度
```

#### 3.3.2 灰度策略

- **灰度维度**：按用户 ID hash（`hash(userId) % 100 < threshold`）
- **灰度梯度**：5% → 25% → 50% → 100%，每档观察 ≥ 24 小时
- **内部端点**：`internal/v1/*` 不走灰度，100% 切新域（内部调用方协调切换，可快速回滚）
- **回滚机制**：网关 `feature flag` 一键切回旧域，TTL < 30 秒
- **契约等价性校验**：双轨期对新旧域响应做 schema diff，发现不等价立即暂停灰度

#### 3.3.3 监控指标

| 指标类别 | 指标项 | 告警阈值 |
|---------|--------|---------|
| 可用性 | 新域端点 5xx 错误率 | > 0.5% |
| 性能 | 新域端点 P99 时延 | > 旧域 1.5 倍 |
| 业务 | 登录成功率 | 下降 > 1% |
| 业务 | 下单支付成功率 | 下降 > 0.5% |
| 业务 | 积分发放成功率 | 下降 > 1% |
| 业务 | 评价提交成功率 | 下降 > 1% |
| 一致性 | 新旧域响应 schema diff 不等价数 | > 0 |

### 3.4 阶段 4 旧域下线检查清单

下线前必须全部 ✅：

- [ ] 网关旧域路由 7 天内流量为 0
- [ ] 新域端点 5xx 错误率连续 7 天 < 0.1%
- [ ] 新域端点 P99 时延 ≤ 旧域基线
- [ ] 业务指标（登录/下单/支付/积分/评价）无回归
- [ ] 旧域数据库无新写入（确认只读）
- [ ] 旧域消息队列消费者已停止
- [ ] 旧域 gRPC 服务无调用方（日志确认）
- [ ] design-prompts 文档已更新为新域端点
- [ ] API 清单（feature-inventory）已更新
- [ ] 回滚预案归档（保留 30 天回滚能力）

## 4. 风险与依赖项 + Application 层重设计要点

### 4.1 关键风险与缓解措施

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| **R1：PointsMembership Internal 4 端点 2026-08-01 下线 deadline** | 阶段 1-2 若未在 7 月底完成，旧 Internal 端点需延期下线，违反代码标注承诺 | Points 域 Track B 优先级提升为 P0；若 7 月 25 日前阶段 1 未完成，立即将旧域 `[Obsolete]` 下线日期顺延至 T2+2 周，并在代码中标注延期原因 |
| **R2：新 Membership 域 9 个端点返工破坏现有契约** | 已有调用方（前端/BFF）依赖 `api/membershippackages` 路径与 `ActionResult<T>` 响应，返工后路径变连字符、响应变 `ApiResponse<T>` | 阶段 2 网关双轨期灰度验证；若新 Membership 域已有生产流量，需在网关做响应包装适配层（旧调用方过渡期兼容裸 `ActionResult<T>`），观察期结束后强制切换 |
| **R3：UserCenter 新建域需搭建完整项目骨架** | 新域 DbContext、MassTransit、Redis、防腐层依赖从零搭建，可能引入配置错误 | 复用 `Leno.Notification.Api` 或 `Leno.Order.Api` 的 Program.cs 模板；防腐层（`IProductPricingQueryService`）依赖通过 HttpClientFactory 注册，与 UserAuth 旧域配置对齐 |
| **R4：Identity 域 AssignRolesAsync 依赖 AccessControl gRPC** | Identity 分配角色需调 AccessControl `GetUserRoles`/`CheckPermission` gRPC，跨域调用链增加故障面 | AccessControl gRPC 已有 `GrpcInternalKeyInterceptor` 鉴权；Identity 调用方加 Polly 重试（3 次 exponential backoff）+ 超时（5s）；失败时返回 `ApiResponse.Fail("ROLE_SERVICE_UNAVAILABLE")`，不阻塞用户管理其他操作 |
| **R5：通知偏好端点跨域去重（UserCenter vs BC9 Notification）** | UserAuth 旧域有 `api/users/me/notification-preferences`，BC9 Notification 域 `NotificationPreferencesController` 也实现同一端点，迁移后三处重复 | 决策：HTTP 端点统一归 UserCenter（业务语义属用户中心）；BC9 Notification 域 `NotificationPreferencesController` 改为 internal HTTP 端点（`internal/v1/users/{userId}/notification-preferences`）供通知发送时查询，不对外暴露。design-prompts 同步更新 |
| **R6：Review/AfterSales 域 gRPC 条件映射（`AntiCorruption:UseGrpc=true`）** | 旧域 gRPC 仅在配置开关开启时映射，新域若忘记复刻开关，gRPC 服务静默不可用 | 新域 Program.cs 复刻条件映射；启动日志打印 gRPC 服务状态；集成测试加 gRPC 端点可达性用例 |
| **R7：旧域数据库共享，下线时数据归属** | UserAuth 旧域 DbContext 含 User/Address/Favorite/BrowseHistory/NotificationPreference 等多张表，下线后这些表归新域管理 | 迁移期数据库不改表结构；下线阶段仅停止旧域服务，数据库保留；新域各自 DbContext 映射同一数据库的对应表（共享数据库模式），未来按域拆库时再迁移数据 |
| **R8：三域并行同步切换，单域阻塞整体** | 若某域阶段 1 延期，阶段 2 网关双轨无法统一启动 | 设阶段 1 软性里程碑（T1 前 3 天检查）；若某域延期，其余域先进入双轨，延期域单独排期切换，但旧域统一在所有域完成阶段 3 后才下线 |

### 4.2 跨域依赖关系

```
AccessControl (gRPC)
    ↑
    │ GetUserRoles / CheckPermission
    │
Identity ────gRPC────→ AccessControl
    │
    │ internal/v1/users/{userId}/contacts (供 Order/Payment/Cart 调用)
    │
UserCenter ──防腐层──→ Product (IProductPricingQueryService, 收藏/浏览历史展示商品信息)
    │
    │ 通知偏好查询
    │
BC9 Notification ──internal──→ UserCenter (查通知偏好)

Points ──internal/v1/points/*──→ Order (下单试算/冻结/释放/确认)
    │
    │ GrantLevelBonusAsync
    │
Membership ──事件──→ Points (订阅会员触发积分奖励)

Review ──gRPC──→ Product (GetProductRating, 商品详情页评分)
        ──gRPC──→ Order (GetOrderReviews, 订单详情页评价)

AfterSales ──事件──→ Order (售后审核通过触发退款)
           ──防腐层──→ Order (查询订单/订单行)
           ──防腐层──→ Payment (触发退款)
```

### 4.3 Application 层重设计要点

#### 4.3.1 Points 域 Internal 服务重设计（最关键）

旧域 `IPointsInternalAppService` 含 4 个方法（TrialOffset/Freeze/Release/Confirm），新域现仅 `GrantLevelBonusAsync`，需补齐且契约对齐。

**新 `IPointsInternalAppService` 契约**（与旧域方法签名完全一致，确保调用方零改造）：

```csharp
public interface IPointsInternalAppService
{
    // 试算积分可抵扣金额（下单预览）
    Task<TrialOffsetResultDto> TrialOffsetAsync(Guid userId, decimal orderAmount, CancellationToken ct = default);
    
    // 冻结积分（下单时预占）
    Task<FreezeResultDto> FreezeAsync(Guid userId, int points, Guid orderId, CancellationToken ct = default);
    
    // 释放冻结积分（订单取消回退）
    Task ReleaseAsync(Guid orderId, CancellationToken ct = default);
    
    // 确认扣减冻结积分（订单支付成功后核销）
    Task ConfirmAsync(Guid orderId, CancellationToken ct = default);
    
    // 保留：会员升级奖励积分（新域已有）
    Task GrantLevelBonusAsync(Guid userId, int newLevel, CancellationToken ct = default);
}
```

**Internal 端点单路径**：新域 `InternalPointsController` 每个 Action 仅挂 `internal/v1/points/*` 单路由，不再双路由叠加（旧域双路由是为兼容，新域无需保留）。

#### 4.3.2 Identity 域 AssignRolesAsync 跨域调用

```csharp
public async Task AssignRolesAsync(Guid userId, List<Guid> roleIds, CancellationToken ct)
{
    // 1. 调 AccessControl gRPC GetUserRoles 校验当前角色
    var currentRoles = await _accessControlGrpc.GetUserRolesAsync(userId, ct);
    
    // 2. 计算需新增/移除的角色
    var toAdd = roleIds.Except(currentRoles).ToList();
    var toRemove = currentRoles.Except(roleIds).ToList();
    
    // 3. 调 AccessControl gRPC 批量分配/移除
    await _accessControlGrpc.AssignRolesAsync(userId, toAdd, toRemove, ct);
}
```

**AccessControl 域补 gRPC RPC**：除现有 `CheckPermission`/`GetUserRoles`，需补 `AssignRoles`、`RemoveRoles` 两个 RPC（或通过 HTTP `POST api/admin/users/{id}/roles` 由 Identity 直接调 AccessControl HTTP 端点）。**推荐方案**：Identity 的 `POST api/admin/users/{id}/roles` 端点内部直接调 AccessControl 的 HTTP 端点（`api/admin/users/{id}/roles` 反代），避免 gRPC 双向依赖；AccessControl HTTP 端点鉴权用内部服务间调用模式（X-Internal-Key 或服务间 JWT）。

#### 4.3.3 UserCenter 域防腐层迁移

UserCenter 域 Favorites/BrowseHistory 展示商品信息需调 Product 域。防腐层接口与 UserAuth 旧域完全一致：

```csharp
// 共享内核 Leno.SharedContracts.Abstractions
public interface IProductPricingQueryService
{
    Task<IReadOnlyDictionary<Guid, ProductPriceSnapshot>> GetCurrentPricesAsync(
        IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default);
}

// UserCenter.Infrastructure 注册
services.AddHttpClient<IProductPricingQueryService, ProductPricingQueryService>(client =>
{
    client.BaseAddress = new Uri(configuration["Product:BaseUrl"]!);
    client.DefaultRequestHeaders.Add("X-Internal-Key", configuration["Internal:Key"]!);
})
.AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

#### 4.3.4 Review/AfterSales 域 Application 层接口补齐

**Review 域补 2 方法**（从旧域 `IReviewAppService` 复制签名与实现逻辑）：

```csharp
// IReviewAppService 补充
Task AppendAdditionalReviewAsync(Guid reviewId, Guid userId, string content, CancellationToken ct = default);
Task<PagedResult<ReviewDto>> GetBySellerAsync(Guid sellerId, ReviewFilterDto filter, int page, int pageSize, CancellationToken ct = default);
```

**AfterSales 域补 1 方法**：

```csharp
// IAfterSalesAppService 补充
Task<AfterSalesDetailDto?> GetByIdForSellerAsync(Guid id, Guid sellerId, CancellationToken ct = default);
```

#### 4.3.5 通知偏好跨域去重方案

| 端点 | 迁移后归属 | 实现位置 |
|------|-----------|---------|
| `GET api/users/me/notification-preferences` | UserCenter（对外 HTTP） | UserCenter.NotificationPreferencesController |
| `PUT api/users/me/notification-preferences` | UserCenter（对外 HTTP） | UserCenter.NotificationPreferencesController |
| `GET internal/v1/users/{userId}/notification-preferences` | BC9 Notification（内部 HTTP，供通知发送时查询） | Notification.NotificationPreferencesController 改为 internal 路径 |

**数据层**：通知偏好表 `NotificationPreferences` 由 UserCenter 域 DbContext 管理（读写）；BC9 Notification 域通过 internal HTTP 端点查询 UserCenter，不直接访问表。**双轨期**：UserCenter 写入后，BC9 通过 internal 端点实时读取，无缓存不一致风险。

### 4.4 测试策略

#### 4.4.1 单元测试（阶段 1）

- **Application 层**：xUnit + Moq，覆盖 39 个新方法的成功/失败/边界场景
- **Controller 层**：`WebApplicationFactory<Program>` + Mock AppService，覆盖 105 端点的成功/失败/鉴权（每端点 ≥ 3 用例，共 ≥ 315 用例）

#### 4.4.2 契约测试（阶段 2）

- **Pact 契约测试**：新域端点与旧域端点响应 schema 等价性验证
- **重点**：Membership 域 9 个返工端点（响应包装从 `ActionResult<T>` → `ApiResponse<T>`，需前端适配）

#### 4.4.3 端到端测试（阶段 2 灰度期）

- **关键链路**：注册→登录→浏览→加购→下单→支付→积分→评价→售后 全链路
- **对比**：同一用户 ID 在新域/旧域的链路结果一致

### 4.5 文档同步清单

阶段 4 旧域下线时同步更新：

| 文档 | 更新内容 |
|------|---------|
| `docs/design-prompts/**` | 端点引用从旧域路径更新为新域路径（路径不变的无需改） |
| `docs/feature-inventory/api-gap/**` | 三个旧域 BC 报告标记为"已下线"，新增 7 个新域 BC 报告 |
| `docs/feature-inventory/api-gap/00-summary.md` | 全局优先级矩阵更新，移除双轨期相关项 |
| 架构图 | 更新为 7 个新域拓扑，移除 3 个旧域 |
| API 清单 | 105 个端点标注新域归属 |

## 5. 关键决策记录

| 决策点 | 决策 | 理由 |
|--------|------|------|
| 迁移范围 | 三域全量迁移 | 彻底结束双轨期，避免长期多风格并存 |
| 契约风格 | 以旧域风格为准 | 与系统已实现域（Order/Payment/Cart 等）一致，前端/BFF 零改造 |
| UserAuth 剩余功能归属 | 拆分为 Identity + AccessControl + UserCenter 三域 | 业务语义清晰，权限边界明确 |
| 内部服务间调用 | HTTP+gRPC 双轨重建 | 与旧域完全对等，切换零风险 |
| 迁移节奏 | 三域并行同步切换 | 统一双轨期与下线时机，避免长期双轨 |

## 6. 验收标准

迁移完成的验收标准（全部满足）：

1. **代码层**：3 个旧域服务进程停止，代码仓库标记 deprecated
2. **API 层**：105 个端点全部由 7 个新域承载，路径与契约符合 §1.2 规范
3. **gRPC 层**：2 个 gRPC 服务（Review、Points）在新域重建并条件映射
4. **Internal 层**：6 个 internal HTTP 端点（Identity 2 + Points 4）在新域重建
5. **数据层**：旧域数据库保留（只读），新域 DbContext 映射对应表
6. **文档层**：design-prompts、feature-inventory、架构图全部更新
7. **监控层**：新域端点 5xx 错误率 < 0.1%，P99 时延 ≤ 旧域基线
8. **回滚能力**：保留 30 天回滚兜底，30 天后彻底清理资源

---

> **下一步**：用户审阅本 spec 后，调用 `writing-plans` 技能创建详细实施计划。
