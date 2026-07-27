# BC1 用户与认证授权域 — API 缺失对比报告

> 本文件由 BC 级 subagent 严格遵循本模板产出。模板源：docs/feature-inventory/_shared/report-template.md

> **域拆分迁移阶段1-2 已完成（2026-07-26）**：UserAuth 旧域已按职责拆分为三个新域并经网关双轨挂载：
> - **Identity 域**（28 端点）：认证、OAuth、忘记密码、双因子、个人资料、密码修改、外部登录绑定（`AuthController` / `AccountController` / `UsersController` / `AdminUsersController` / `AdminOAuthClientsController` / `InternalUsersController`）
> - **UserCenter 域**（17 端点）：收货地址、收藏、浏览历史、通知偏好 HTTP 端点（`AddressesController` / `FavoritesController` / `BrowseHistoryController` / `NotificationPreferencesController`）
> - **AccessControl 域**（7 端点）：角色与权限管理（`AdminRolesController`）
>
> 旧域 UserAuth 代码保留作回滚兜底，待阶段3观察期结束后下线。design-prompts 与 feature-list 中的「服务归属」已更新为新域，端点路径不变。详见 `docs/feature-inventory/domain-migration-status.md`。

## 1. 概览
- **BC 编号**：BC1
- **中文名**：用户与认证授权域
- **英文名**：UserAuth（旧域；新域为 Identity / UserCenter / AccessControl）
- **涉及端**：buyer-app / operations / system-admin
- **涉及页面数**：15 页（来自 feature-list）
  - buyer-app：01-auth 5 页（login/register/forgot-password/oauth-login/two-factor）+ 13-profile 4 页（profile/security/addresses/settings）+ 12-notification/preferences 1 页 + 13-profile 2 页（favorites/history，➕）
  - operations：09-account 2 页（login/profile）
  - system-admin：02-user-access 3 页（oauth-clients/role-management/user-management，operators 归 BC11 不计入）+ 06-account 2 页（login-2fa/profile）
- **已实现 API 端点数**：42 个（来自源码 Controller 扫描；UserAuth 39 个对外 + Identity 3 个待切换 + 2 个内部不计入差异）
- **差异统计**：缺失 12 / 闲置 0 / 路径不一致 4 / 能力不匹配 1

## 2. 源码 API 端点清单（实际实现）

| HTTP 方法 | 路径 | Controller 文件:行号 | 用途 | 鉴权角色 |
|-|-|-|-|-|
| POST | /api/auth/register | [AuthController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L42) | 注册账户并签发令牌 | 匿名 |
| POST | /api/auth/login | [AuthController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L51) | 账号密码登录并签发令牌 | 匿名 |
| POST | /api/auth/refresh-token | [AuthController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L60) | 刷新令牌轮换 | 匿名 |
| POST | /api/auth/logout | [AuthController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L69) | 登出并吊销当前 JWT | 已认证 |
| GET | /api/auth/oauth/{provider}/login | [AuthController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L98) | 获取第三方授权 URL | 匿名 |
| GET | /api/auth/oauth/{provider}/callback | [AuthController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L117) | OAuth2 回调交换 token | 匿名 |
| POST | /api/auth/two-factor/verify | [AuthController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L164) | 双因子二次验证签发 JWT | 匿名 |
| POST | /api/auth/forgot-password | [AuthController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L173) | 发送重置验证码/链接 | 匿名 |
| POST | /api/auth/reset-password | [AuthController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L182) | 重置密码 | 匿名 |
| POST | /api/account/external-logins | [AccountController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AccountController.cs#L29) | 绑定外部登录 | 已认证 |
| DELETE | /api/account/external-logins/{provider} | [AccountController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AccountController.cs#L39) | 解绑外部登录 | 已认证 |
| GET | /api/users/me | [UsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/UsersController.cs#L29) | 查询当前用户资料 | 已认证 |
| PUT | /api/users/me | [UsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/UsersController.cs#L38) | 修改当前用户资料 | 已认证 |
| PUT | /api/users/me/password | [UsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/UsersController.cs#L47) | 修改当前用户密码 | 已认证 |
| POST | /api/users/me/two-factor/enable | [UsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/UsersController.cs#L56) | 启用双因子生成 QR 码 | 已认证 |
| POST | /api/users/me/two-factor/confirm | [UsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/UsersController.cs#L65) | 确认启用双因子验证 TOTP | 已认证 |
| POST | /api/users/me/two-factor/disable | [UsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/UsersController.cs#L74) | 禁用双因子认证 | 已认证 |
| GET | /api/users/me/addresses | [AddressesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AddressesController.cs#L29) | 查询地址列表（默认优先） | 已认证 |
| POST | /api/users/me/addresses | [AddressesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AddressesController.cs#L38) | 新增收货地址 | 已认证 |
| PUT | /api/users/me/addresses/{id:guid} | [AddressesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AddressesController.cs#L47) | 修改收货地址 | 已认证 |
| DELETE | /api/users/me/addresses/{id:guid} | [AddressesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AddressesController.cs#L56) | 软删除收货地址 | 已认证 |
| POST | /api/users/me/addresses/{id:guid}/default | [AddressesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AddressesController.cs#L65) | 设为默认地址 | 已认证 |
| GET | /api/admin/users | [AdminUsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminUsersController.cs#L29) | 分页查询用户列表 | Admin,Operator |
| GET | /api/admin/users/{id:guid} | [AdminUsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminUsersController.cs#L38) | 查询用户详情 | Admin,Operator |
| POST | /api/admin/users/{id:guid}/roles | [AdminUsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminUsersController.cs#L47) | 为用户分配角色（幂等） | Admin,Operator |
| POST | /api/admin/users/{id:guid}/suspend | [AdminUsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminUsersController.cs#L56) | 锁定用户账户 | Admin,Operator |
| POST | /api/admin/users/{id:guid}/resume | [AdminUsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminUsersController.cs#L65) | 解锁/恢复用户账户 | Admin,Operator |
| GET | /api/admin/roles | [AdminRolesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminRolesController.cs#L29) | 分页查询角色列表 | Admin |
| GET | /api/admin/roles/{roleId:guid} | [AdminRolesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminRolesController.cs#L42) | 查询角色详情 | Admin |
| POST | /api/admin/roles | [AdminRolesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminRolesController.cs#L51) | 创建角色 | Admin |
| PUT | /api/admin/roles/{roleId:guid} | [AdminRolesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminRolesController.cs#L60) | 编辑角色 | Admin |
| DELETE | /api/admin/roles/{roleId:guid} | [AdminRolesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminRolesController.cs#L69) | 删除角色（内置不可删） | Admin |
| GET | /api/admin/roles/{roleId:guid}/permissions | [AdminRolesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminRolesController.cs#L78) | 查看角色权限列表 | Admin |
| PUT | /api/admin/roles/{roleId:guid}/permissions | [AdminRolesController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminRolesController.cs#L87) | 更新角色权限（全量替换） | Admin |
| GET | /api/admin/oauth-clients | [AdminOAuthClientsController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminOAuthClientsController.cs#L29) | 查询全部 OAuth 客户端配置 | Admin |
| POST | /api/admin/oauth-clients/{provider} | [AdminOAuthClientsController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminOAuthClientsController.cs#L41) | 新建 OAuth 客户端配置（默认禁用） | Admin |
| PUT | /api/admin/oauth-clients/{provider} | [AdminOAuthClientsController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminOAuthClientsController.cs#L51) | 更新指定提供方配置 | Admin |
| POST | /api/admin/oauth-clients/{provider}/enable | [AdminOAuthClientsController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminOAuthClientsController.cs#L61) | 启用指定提供方 | Admin |
| POST | /api/admin/oauth-clients/{provider}/disable | [AdminOAuthClientsController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminOAuthClientsController.cs#L70) | 禁用指定提供方 | Admin |
| GET | /internal/v1/users/{userId:guid}/contacts | [InternalUsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs#L30) | 内部脱敏联系方式查询（内部） | 内部 X-Internal-Key |
| GET | /internal/v1/users/{userId:guid}/contacts/full | [InternalUsersController.cs](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs#L49) | 内部完整 PII 查询（内部） | 内部 X-Internal-Key |
| POST | /api/auth/login | [AuthController.cs](file:///e:/Leno/src/Services/Identity/Leno.Identity.Api/Controllers/AuthController.cs#L36) | 账号密码登录（Identity，待切换） | 匿名 |
| POST | /api/auth/refresh | [AuthController.cs](file:///e:/Leno/src/Services/Identity/Leno.Identity.Api/Controllers/AuthController.cs#L57) | 刷新令牌轮换（Identity，待切换） | 匿名 |
| POST | /api/auth/logout | [AuthController.cs](file:///e:/Leno/src/Services/Identity/Leno.Identity.Api/Controllers/AuthController.cs#L77) | 登出吊销刷新令牌（Identity，待切换） | 已认证 |

> 来源：grep `src/Services/UserAuth/**/Controllers/*.cs` 与 `src/Services/Identity/**/Controllers/*.cs` 的 `[Route]/[Http*]` 特性
> Internal*Controller.cs 中的端点已标注「（内部）」，不计入对外差异
> Identity 目录下的端点已标注「（Identity，待切换）」，与 UserAuth 同名端点形成双轨

## 3. 设计稿需求 API 清单（期望实现）

| HTTP 方法 | 路径 | 来源页面 | 用途 | 实现状态 | 鉴权角色 |
|-|-|-|-|-|-|
| POST | /api/account/login | [login.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/login.md) | 买家账号密码登录 | ✅ | 匿名 |
| POST | /api/auth/refresh | [login.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/login.md) | 刷新令牌 | ✅ | 匿名 |
| GET | /api/auth/oauth/{provider}/login | [login.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/login.md), [oauth-login.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/oauth-login.md) | 获取第三方授权 URL | ✅ | 匿名 |
| POST | /api/auth/register | [register.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/register.md) | 注册账号并签发令牌 | ✅ | 匿名 |
| POST | /api/auth/forgot-password | [register.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/register.md), [forgot-password.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/forgot-password.md), [login.md](file:///e:/Leno/docs/design-prompts/operations/09-account/login.md), [login-2fa.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/login-2fa.md) | 发送重置验证码/链接 | ✅ | 匿名 |
| POST | /api/auth/reset-password | [forgot-password.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/forgot-password.md), [login.md](file:///e:/Leno/docs/design-prompts/operations/09-account/login.md), [login-2fa.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/login-2fa.md) | 重置密码 | ✅ | 匿名 |
| GET | /api/auth/oauth/{provider}/callback | [oauth-login.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/oauth-login.md) | 第三方回调交换 token | ✅ | 匿名 |
| POST | /api/account/external-logins | [oauth-login.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/oauth-login.md), [security.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/security.md), [profile.md](file:///e:/Leno/docs/design-prompts/operations/09-account/profile.md) | 绑定外部登录 | ✅ | Buyer/已认证 |
| DELETE | /api/account/external-logins/{provider} | [oauth-login.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/oauth-login.md), [security.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/security.md), [profile.md](file:///e:/Leno/docs/design-prompts/operations/09-account/profile.md) | 解绑外部登录 | ✅ | Buyer/已认证 |
| POST | /api/auth/two-factor/verify | [two-factor.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/two-factor.md), [login.md](file:///e:/Leno/docs/design-prompts/operations/09-account/login.md), [login-2fa.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/login-2fa.md) | 双因子二次验证 | ✅ | 匿名 |
| POST | /api/users/me/two-factor/enable | [two-factor.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/two-factor.md), [security.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/security.md), [profile.md](file:///e:/Leno/docs/design-prompts/operations/09-account/profile.md), [profile.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/profile.md) | 启用双因子生成 QR | ✅ | Buyer/已认证 |
| POST | /api/users/me/two-factor/confirm | [two-factor.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/two-factor.md), [security.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/security.md), [profile.md](file:///e:/Leno/docs/design-prompts/operations/09-account/profile.md), [profile.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/profile.md) | 确认启用双因子 | ✅ | Buyer/已认证 |
| POST | /api/users/me/two-factor/disable | [two-factor.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/two-factor.md), [security.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/security.md), [profile.md](file:///e:/Leno/docs/design-prompts/operations/09-account/profile.md), [profile.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/profile.md) | 禁用双因子 | ✅ | Buyer/已认证 |
| GET | /api/users/me | [profile.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/profile.md), [security.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/security.md), [profile.md](file:///e:/Leno/docs/design-prompts/operations/09-account/profile.md), [profile.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/profile.md) | 查询当前用户资料 | ✅ | Buyer/已认证 |
| PUT | /api/users/me | [profile.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/profile.md), [profile.md](file:///e:/Leno/docs/design-prompts/operations/09-account/profile.md), [profile.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/profile.md) | 修改当前用户资料 | ✅ | Buyer/已认证 |
| PUT | /api/users/me/password | [security.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/security.md), [profile.md](file:///e:/Leno/docs/design-prompts/operations/09-account/profile.md), [profile.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/profile.md) | 修改当前用户密码 | ✅ | Buyer/已认证 |
| GET | /api/users/me/addresses | [addresses.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/addresses.md) | 查询地址列表（默认优先） | ✅ | Buyer |
| POST | /api/users/me/addresses | [addresses.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/addresses.md) | 新增地址 | ✅ | Buyer |
| PUT | /api/users/me/addresses/{id} | [addresses.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/addresses.md) | 修改地址 | ✅ | Buyer |
| DELETE | /api/users/me/addresses/{id} | [addresses.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/addresses.md) | 删除地址 | ✅ | Buyer |
| POST | /api/users/me/addresses/{id}/default | [addresses.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/addresses.md) | 设为默认 | ✅ | Buyer |
| POST | /api/auth/logout | [settings.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/settings.md), [login.md](file:///e:/Leno/docs/design-prompts/operations/09-account/login.md) | 退出登录吊销 JWT | ✅ | Buyer/已认证 |
| GET | /api/users/me/notification-preferences | [settings.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/settings.md), [preferences.md](file:///e:/Leno/docs/design-prompts/buyer-app/12-notification/preferences.md) | 查询通知偏好 | ✅ | Buyer |
| PUT | /api/users/me/notification-preferences | [settings.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/settings.md), [preferences.md](file:///e:/Leno/docs/design-prompts/buyer-app/12-notification/preferences.md) | 设置通知偏好 | ✅ | Buyer |
| POST | /api/auth/login | [login.md](file:///e:/Leno/docs/design-prompts/operations/09-account/login.md), [login-2fa.md](file:///e:/Leno/docs/design-prompts/system-admin/06-account/login-2fa.md) | 账号密码登录 | ✅ | 匿名 |
| POST | /api/auth/refresh-token | [login.md](file:///e:/Leno/docs/design-prompts/operations/09-account/login.md) | 刷新令牌 | ✅ | 匿名 |
| GET | /api/admin/oauth-clients | [oauth-clients.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/oauth-clients.md) | 查询 OAuth 客户端列表 | ✅ | Admin |
| POST | /api/admin/oauth-clients/{provider} | [oauth-clients.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/oauth-clients.md) | 新建 OAuth 客户端 | ✅ | Admin |
| PUT | /api/admin/oauth-clients/{provider} | [oauth-clients.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/oauth-clients.md) | 更新 OAuth 客户端 | ✅ | Admin |
| POST | /api/admin/oauth-clients/{provider}/enable | [oauth-clients.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/oauth-clients.md) | 启用提供方 | ✅ | Admin |
| POST | /api/admin/oauth-clients/{provider}/disable | [oauth-clients.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/oauth-clients.md) | 禁用提供方 | ✅ | Admin |
| GET | /api/admin/roles | [role-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/role-management.md) | 分页查询角色 | ✅ | Admin |
| GET | /api/admin/roles/{roleId} | [role-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/role-management.md) | 查询角色详情 | ✅ | Admin |
| POST | /api/admin/roles | [role-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/role-management.md) | 创建角色 | ✅ | Admin |
| PUT | /api/admin/roles/{roleId} | [role-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/role-management.md) | 编辑角色 | ✅ | Admin |
| DELETE | /api/admin/roles/{roleId} | [role-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/role-management.md) | 删除角色 | ✅ | Admin |
| GET | /api/admin/roles/{roleId}/permissions | [role-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/role-management.md) | 查看角色权限 | ✅ | Admin |
| PUT | /api/admin/roles/{roleId}/permissions | [role-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/role-management.md) | 更新角色权限 | ✅ | Admin |
| GET | /api/admin/users | [user-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/user-management.md) | 分页查询用户 | ✅ | Admin,Operator |
| GET | /api/admin/users/{id} | [user-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/user-management.md) | 查询用户详情 | ✅ | Admin,Operator |
| PUT | /api/admin/users/{id}/roles | [user-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/user-management.md) | 为用户分配角色（幂等） | ✅ | Admin,Operator |
| PUT | /api/admin/users/{id}/status | [user-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/user-management.md) | 锁定/恢复账户（body: status） | ✅ | Admin,Operator |
| GET | /api/users/me/favorites | [favorites.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/favorites.md) | 分页查询我的收藏 | ➕ | Buyer |
| POST | /api/users/me/favorites | [favorites.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/favorites.md) | 收藏商品 | ➕ | Buyer |
| DELETE | /api/users/me/favorites/{spuId} | [favorites.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/favorites.md) | 取消收藏单个 | ➕ | Buyer |
| POST | /api/users/me/favorites/batch-delete | [favorites.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/favorites.md) | 批量取消收藏 | ➕ | Buyer |
| GET | /api/users/me/favorites/count | [favorites.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/favorites.md) | 查询收藏总数 | ➕ | Buyer |
| GET | /api/users/me/browse-history | [history.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/history.md) | 分页查询浏览历史 | ➕ | Buyer |
| POST | /api/users/me/browse-history | [history.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/history.md) | 记录浏览历史 | ➕ | Buyer |
| DELETE | /api/users/me/browse-history/{id} | [history.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/history.md) | 删除单条历史 | ➕ | Buyer |
| POST | /api/users/me/browse-history/batch-delete | [history.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/history.md) | 批量删除历史 | ➕ | Buyer |
| DELETE | /api/users/me/browse-history | [history.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/history.md) | 清空全部历史 | ➕ | Buyer |

> 来源：design-prompts 的「3. 数据模型与 API 对接」段
> 实现状态沿用 design-prompts 标注（✅ 已实现 / 🚧 规划中 / ➕ 补充功能）
> 注：02-user-access/operators 页面虽位于 02-user-access 模块，但 feature-list 标注为 BC11 域，且源码不在 UserAuth/Identity 目录，故不计入 BC1 范围

## 4. 差异分析

### 4.1 设计稿需要但后端未提供（缺失）

| 期望方法 | 期望路径 | 来源页面 | 用途 | 优先级 | 建议补充方式 |
|-|-|-|-|-|-|
| GET | /api/users/me/notification-preferences | [settings.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/settings.md), [preferences.md](file:///e:/Leno/docs/design-prompts/buyer-app/12-notification/preferences.md) | 查询通知偏好（design 误标 ✅，源码未实现） | P1 | 在 UsersController 或独立 NotificationPreferencesController 新增 GET 端点 |
| PUT | /api/users/me/notification-preferences | [settings.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/settings.md), [preferences.md](file:///e:/Leno/docs/design-prompts/buyer-app/12-notification/preferences.md) | 设置通知偏好（design 误标 ✅，源码未实现） | P1 | 在 UsersController 或独立 NotificationPreferencesController 新增 PUT 端点 |
| GET | /api/users/me/favorites | [favorites.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/favorites.md) | 分页查询收藏列表 | P2 | 新增 FavoritesController（含分页+排序） |
| POST | /api/users/me/favorites | [favorites.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/favorites.md) | 收藏商品 | P2 | 新增 FavoritesController POST 端点 |
| DELETE | /api/users/me/favorites/{spuId} | [favorites.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/favorites.md) | 取消收藏单个 | P2 | 新增 FavoritesController DELETE 端点 |
| POST | /api/users/me/favorites/batch-delete | [favorites.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/favorites.md) | 批量取消收藏 | P2 | 新增 FavoritesController 批量端点 |
| GET | /api/users/me/favorites/count | [favorites.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/favorites.md) | 查询收藏总数 | P2 | 新增 FavoritesController count 端点 |
| GET | /api/users/me/browse-history | [history.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/history.md) | 分页查询浏览历史 | P2 | 新增 BrowseHistoryController |
| POST | /api/users/me/browse-history | [history.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/history.md) | 记录浏览历史 | P2 | 新增 BrowseHistoryController POST 端点 |
| DELETE | /api/users/me/browse-history/{id} | [history.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/history.md) | 删除单条历史 | P2 | 新增 BrowseHistoryController DELETE 端点 |
| POST | /api/users/me/browse-history/batch-delete | [history.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/history.md) | 批量删除历史 | P2 | 新增 BrowseHistoryController 批量端点 |
| DELETE | /api/users/me/browse-history | [history.md](file:///e:/Leno/docs/design-prompts/buyer-app/13-profile/history.md) | 清空全部历史 | P2 | 新增 BrowseHistoryController 清空端点 |

> 说明：design-prompts 标 🚧/➕ 的端点，且源码 Controller 中无对应实现；含 design 误标 ✅ 但源码未实现的端点

### 4.2 后端已有但设计稿未调用（闲置）

| 实际方法 | 实际路径 | Controller:行号 | 用途 | 建议处理方式 |
|-|-|-|-|-|
| - | - | - | 无闲置端点 | - |

> 说明：源码有实现但 design-prompts 中无任何页面引用。本 BC 所有对外端点均被至少一个 design-prompts 页面引用（含跨端引用），无闲置。

### 4.3 路径或方法不一致

| 期望方法→实际方法 | 期望路径→实际路径 | 来源页面 | Controller:行号 | 建议调整方向 |
|-|-|-|-|-|
| POST→POST | /api/account/login → /api/auth/login | [login.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/login.md) | [AuthController.cs#L51](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L51) | 改文档：buyer-app 登录页应引用 /api/auth/login（源码与 operations/login-2fa 一致） |
| POST→POST | /api/auth/refresh → /api/auth/refresh-token | [login.md](file:///e:/Leno/docs/design-prompts/buyer-app/01-auth/login.md) | [AuthController.cs#L60](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L60) | 双轨期：buyer-app 文档对齐 UserAuth（/api/auth/refresh-token）；切换 Identity 后改回 /api/auth/refresh |
| PUT→POST | /api/admin/users/{id}/roles → /api/admin/users/{id}/roles | [user-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/user-management.md) | [AdminUsersController.cs#L47](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminUsersController.cs#L47) | 改代码：将 POST 改为 PUT 以匹配 design 幂等语义（或改文档接受 POST） |
| PUT→POST | /api/admin/users/{id}/status → /api/admin/users/{id}/suspend + /api/admin/users/{id}/resume | [user-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/user-management.md) | [AdminUsersController.cs#L56](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminUsersController.cs#L56), [AdminUsersController.cs#L65](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminUsersController.cs#L65) | 改文档：design 改为引用 suspend/resume 两个端点；或改代码合并为 PUT /status（兼容性较差，建议改文档） |

> 说明：方法（GET/POST/PUT/DELETE/PATCH）或路径（/api/xxx）不匹配

### 4.4 参数/能力范围不匹配

| 期望能力 | 实际能力 | 差异点 | 来源页面 | Controller:行号 | 建议补充 |
|-|-|-|-|-|-|
| 多维筛选：keyword + roles（数组多选）+ statuses（数组多选）+ fromTime/toTime（注册时间范围）+ 分页 | 单值筛选：Keyword + Role（单值）+ Status（单值）+ 分页 | 缺少 roles 多选、statuses 多选、注册时间范围筛选 | [user-management.md](file:///e:/Leno/docs/design-prompts/system-admin/02-user-access/user-management.md) | [AdminUsersController.cs#L29](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminUsersController.cs#L29)（DTO: AdminUserQueryDto） | 扩展 AdminUserQueryDto：Roles(string[])、Statuses(string[])、FromTime(DateTime?)、ToTime(DateTime?) |

> 说明：分页/筛选/排序/批量/字段过滤等能力差异。AdminUserQueryDto 当前仅支持单角色/单状态筛选，design 期望多角色多状态+时间范围。

## 5. 拆分过渡说明

> BC1 出现 UserAuth（旧）↔ Identity（新）拆分双轨期，按主任务要求双轨期优先引用 UserAuth，Identity 端点标 🚧 待切换。

### 5.1 UserAuth ↔ Identity 端点对照表

| 业务能力 | UserAuth（旧，优先引用） | Identity（新，待切换） | 路径差异 | 状态 |
|-|-|-|-|-|
| 账号密码登录 | POST /api/auth/login [AuthController.cs#L51](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L51) | POST /api/auth/login [AuthController.cs#L36](file:///e:/Leno/src/Services/Identity/Leno.Identity.Api/Controllers/AuthController.cs#L36) | 路径一致 | 双轨 |
| 刷新令牌 | POST /api/auth/refresh-token [AuthController.cs#L60](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L60) | POST /api/auth/refresh [AuthController.cs#L57](file:///e:/Leno/src/Services/Identity/Leno.Identity.Api/Controllers/AuthController.cs#L57) | 路径不一致（refresh-token vs refresh） | 双轨，需对齐 |
| 登出 | POST /api/auth/logout [AuthController.cs#L69](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L69) | POST /api/auth/logout [AuthController.cs#L77](file:///e:/Leno/src/Services/Identity/Leno.Identity.Api/Controllers/AuthController.cs#L77) | 路径一致（响应体不同：UserAuth 返回 ApiResponse，Identity 返回 204） | 双轨 |
| 注册 | POST /api/auth/register [AuthController.cs#L42](file:///e:/Leno/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L42) | 未实现 | - | UserAuth 独有 |
| OAuth2 登录 | GET /api/auth/oauth/{provider}/login + callback | 未实现 | - | UserAuth 独有 |
| 双因子验证 | POST /api/auth/two-factor/verify | 未实现 | - | UserAuth 独有 |
| 忘记/重置密码 | POST /api/auth/forgot-password + reset-password | 未实现 | - | UserAuth 独有 |

### 5.2 双轨期端点引用规范

1. **对外文档与前端引用优先使用 UserAuth 端点**（旧 BC 仍为生产路径）。
2. **Identity 端点标 🚧 待切换**，在 design-prompts 与 API 文档中暂不引用，待切换完成后统一替换。
3. **路径差异预警**：refresh 端点 UserAuth 为 `/api/auth/refresh-token`，Identity 为 `/api/auth/refresh`。buyer-app/login.md 当前引用 `/api/auth/refresh`（匹配 Identity），与 UserAuth 源码不一致——双轨期需将 buyer-app 文档对齐到 UserAuth（`/api/auth/refresh-token`），切换后再回滚到 Identity 路径。
4. **响应体差异**：UserAuth 端点统一返回 `ApiResponse<T>` 包装；Identity 登出返回 `204 NoContent`，登录/刷新返回裸 `TokenDto`。前端拦截器需兼容两种格式直到切换完成。

### 5.3 待切换端点清单

| Identity 端点 | 替换的 UserAuth 端点 | 切换优先级 | 备注 |
|-|-|-|-|
| 🚧 POST /api/auth/login（Identity） | POST /api/auth/login（UserAuth） | 中 | 路径一致，仅需切换路由 |
| 🚧 POST /api/auth/refresh（Identity） | POST /api/auth/refresh-token（UserAuth） | 高 | 路径不一致，需前端同步改路径 |
| 🚧 POST /api/auth/logout（Identity） | POST /api/auth/logout（UserAuth） | 中 | 路径一致，响应体不同 |

## 6. 优先级矩阵

| 优先级 | 缺失端点 | 闲置端点 | 不一致端点 | 不匹配端点 |
|-|-|-|-|-|
| P0 | - | - | POST /api/account/login → /api/auth/login（buyer-app 登录闭环阻塞） | - |
| P1 | GET/PUT /api/users/me/notification-preferences（通知偏好配置） | - | POST /api/auth/refresh → /api/auth/refresh-token（buyer-app 令牌刷新）；PUT /api/admin/users/{id}/roles → POST（管理员角色分配）；PUT /api/admin/users/{id}/status → POST suspend/resume（账户状态管理） | GET /api/admin/users 多维筛选能力缺失（roles/statuses 多选 + 时间范围） |
| P2 | GET/POST/DELETE /api/users/me/favorites + batch-delete + count（5 个收藏端点）；GET/POST/DELETE /api/users/me/browse-history + batch-delete + 清空（5 个历史端点） | - | - | - |

> P0=阻塞交易闭环；P1=影响体验；P2=补充增强

## 7. 跨 BC 依赖

> 来源：docs/spec/01-用户与认证授权域.md 第 1.3 节「与其他上下文的关系」与第 3 章「领域事件清单」

### 7.1 上游依赖（本 BC 依赖哪些 BC）

- **BC9 消息通知域**：注册欢迎、密码找回验证码、登录异常、双因子等通知统一经 BC9 的 `INotificationService` 发送，本域不直接持有邮件/短信渠道客户端。
- **BC11 平台运营域（AccessControl 子域）**：Identity BC 的 JwtTokenService 通过 AccessControl 的 `GetUserRoles` RPC 完成角色填充（拆分后 Identity 依赖 AccessControl 提供角色数据）。

### 7.2 下游依赖（哪些 BC 依赖本 BC）

- **BC4 订单域**：订单创建时以快照方式固化买家身份与收货地址，通过本域查询接口或 `DefaultAddressChangedEvent` 集成事件获取用户身份摘要。
- **BC7 积分与会员域**：消费 `UserRegisteredEvent` 发放新人积分。
- **BC3 购物车域**：登录后以用户 ID 关联匿名购物车并合并，消费登录态完成归属转换。
- **BC6 评价与售后域**：评价与售后单归属用户，以用户 ID 引用。
- **BC11 审计/风控**：消费 `UserLoggedInEvent`、`UserLoggedOutEvent`、`AccountLockedEvent`、`AccountDisabledEvent` 等事件。

### 7.3 集成事件发布清单

| 事件名 | 触发时机 | 消费方 |
|-|-|-|
| UserRegisteredEvent | 注册成功 | BC7 积分与会员域（发新人积分）、BC9 消息通知域（欢迎通知） |
| UserLoggedInEvent | 登录成功 | BC11 审计、风控 |
| UserLoggedOutEvent | 登出 | BC11 审计 |
| PasswordChangedEvent | 密码修改成功 | BC9 通知、BC11 安全审计 |
| ForgotPasswordRequestedEvent | 发起密码找回 | BC9 通知（发验证码） |
| ExternalLoginBoundEvent | 第三方账号绑定 | BC9 通知、BC11 审计 |
| ExternalLoginUnboundEvent | 第三方账号解绑 | BC11 审计 |
| TwoFactorEnabledEvent | 双因子启用 | BC9 通知 |
| TwoFactorDisabledEvent | 双因子关闭 | BC9 通知 |
| AccountLockedEvent | 登录失败达阈值锁定 | BC9 通知、BC11 审计 |
| AccountUnlockedEvent | 管理员或超时解锁 | BC11 审计 |
| AccountDisabledEvent | 账户禁用 | BC11 审计 |
| ProfileUpdatedEvent | 资料修改 | BC2 搜索（昵称） |
| DefaultAddressChangedEvent | 默认地址变更 | BC4 订单域（可选） |
| RoleAssignedEvent | 角色分配 | BC11 审计 |
| RoleRevokedEvent | 角色撤销 | BC11 审计 |
| RefreshTokenRevokedEvent | Refresh Token 撤销 | —（本域内部） |

> 集成事件订阅：本 BC 不订阅其他 BC 的集成事件，仅对外发布。

## 8. 行动建议

### 立即修复（P0 缺失/不一致）

1. **buyer-app 登录路径对齐**：buyer-app/01-auth/login.md 引用 `POST /api/account/login`，源码实际为 `POST /api/auth/login`。需更新 design-prompts 文档将路径改为 `/api/auth/login`，避免前端按错误路径调用导致 404 阻塞登录闭环。

### 短期补充（P1 缺失/不匹配）

1. **通知偏好端点实现**：在 UsersController 新增 `GET /api/users/me/notification-preferences` 与 `PUT /api/users/me/notification-preferences`，或拆分到独立 NotificationPreferencesController。design 误标 ✅，需同步修正 design-prompts 实现状态。
2. **buyer-app 令牌刷新路径对齐**：buyer-app/login.md 引用 `/api/auth/refresh`，双轨期需对齐到 UserAuth 的 `/api/auth/refresh-token`（避免 404）；待 Identity 切换后再改回 `/api/auth/refresh`。
3. **管理员用户管理端点对齐**：与前端团队确认 `PUT /api/admin/users/{id}/roles` vs 源码 `POST`、`PUT /api/admin/users/{id}/status` vs 源码 `suspend/resume` 的方案——建议改文档接受源码现状（POST + 拆分端点），避免破坏已上线接口。
4. **AdminUserQueryDto 能力扩展**：扩展 DTO 支持 `Roles[]`、`Statuses[]`、`FromTime`、`ToTime`，使 GET /api/admin/users 支持多角色多状态+时间范围筛选，对齐 user-management.md 期望。

### 长期规划（P2 闲置/废弃）

1. **收藏功能后端实现**：新增 FavoritesController 提供 5 个端点（GET 列表分页+排序、POST 收藏、DELETE 单条、POST batch-delete、GET count），支撑 buyer-app/13-profile/favorites 页面（➕ 补充功能）。
2. **浏览历史功能后端实现**：新增 BrowseHistoryController 提供 5 个端点（GET 列表分页+按日期分组、POST 记录、DELETE 单条、POST batch-delete、DELETE 清空），支撑 buyer-app/13-profile/history 页面（➕ 补充功能）。
3. **Identity BC 切换规划**：制定 UserAuth → Identity 端点切换路线图，优先切换 refresh 端点（路径差异最大），登录与登出可平滑迁移。

### 文档同步（design-prompts API 引用对齐到源码）

1. **buyer-app/01-auth/login.md**：`POST /api/account/login` → `POST /api/auth/login`；`POST /api/auth/refresh` → `POST /api/auth/refresh-token`（双轨期）。
2. **buyer-app/13-profile/settings.md 与 12-notification/preferences.md**：通知偏好端点标 🚧（源码未实现），修正误标的 ✅。
3. **system-admin/02-user-access/user-management.md**：`PUT /api/admin/users/{id}/roles` → `POST /api/admin/users/{id}/roles`；`PUT /api/admin/users/{id}/status` → `POST /api/admin/users/{id}/suspend` 与 `POST /api/admin/users/{id}/resume`；或在 design 中保留 PUT 语义并标记为「待源码对齐」。
4. **system-admin/02-user-access/operators.md**：feature-list 标注为 BC11，建议从 02-user-access 模块迁出或标注「BC11 域，非 BC1 范围」。
5. **拆分过渡标注**：在 buyer-app/login.md、operations/login.md、system-admin/login-2fa.md 中标注「双轨期：认证端点优先引用 UserAuth，Identity 端点 🚧 待切换」。
