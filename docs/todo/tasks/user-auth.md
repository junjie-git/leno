# 用户与认证授权域 - 任务执行计划

> **模块**: BC1 用户与认证授权域
> **对应文档**: `01-用户与认证授权域.md`
> **任务 ID 前缀**: UA
> **总任务数**: 8 | **P0**: 2 | **P1**: 3 | **P2**: 3

---

## 模块概述

用户域负责账户生命周期管理、认证授权、地址管理与安全审计。已实现核心功能（注册、登录、JWT、地址管理、管理员用户管理），但缺失 OAuth2 第三方登录、双因子认证、密码找回、RBAC 权限管理、OAuth2 客户端配置、第三方账号绑定/解绑、审计中间件与测试项目。

---

## Task UA-01: 测试项目创建 [P0] ✅

### 子任务 Checklist

- [x] UA-01.1: 创建 `Leno.UserAuth.Domain.Tests` 项目，引用 `Leno.UserAuth.Domain`
- [x] UA-01.2: 创建 `Leno.UserAuth.Application.Tests` 项目
- [x] UA-01.3: 创建 `Leno.UserAuth.Api.Tests` 项目（使用 `WebApplicationFactory`）
- [x] UA-01.4: 覆盖 User 聚合（Create、ChangePassword、VerifyPassword、Lock、Unlock、Disable、Activate、AssignRole、RevokeRole、CanLogin、UpdateProfile、RecordLogin）— 31 个测试
- [x] UA-01.5: 覆盖 Address 聚合（Create、UpdateInfo、MarkAsDefault、UnmarkDefault、SoftDelete）— 9 个测试
- [x] UA-01.6: 覆盖应用服务（RegisterAsync、LoginAsync、ChangePasswordAsync、GetProfileAsync、UpdateProfileAsync、RefreshTokenAsync、Address CRUD）— 29 个测试
- [x] UA-01.7: 覆盖 API 集成测试（WebApplicationFactory + HealthCheck + 鉴权）— 2 个测试
- [x] UA-01.8: 配置测试覆盖率 ≥ 80%（coverlet 已配置，覆盖率报告在 CI 中生成）

### 验收标准
- [x] 领域层单元测试覆盖率 ≥ 80%
- [x] 每个聚合根方法覆盖正常/边界/异常路径
- [x] API 集成测试覆盖鉴权与权限校验

---

## Task UA-02: OAuth2 第三方登录 [P0]

### 子任务 Checklist

- [ ] UA-02.1: 在领域层定义 `IExternalAuthService` 接口（`GetAuthorizationUrlAsync`、`ExchangeCodeAsync`、`GetUserInfoAsync`）
- [ ] UA-02.2: 在领域层定义 `ExternalLoginInfo` 值对象（Provider、ProviderUserId、Email、Name、AvatarUrl）
- [ ] UA-02.3: 在 User 聚合中实现 `FindByExternalLogin` 和 `CreateFromExternal` 工厂方法
- [ ] UA-02.4: 在基础设施层实现 `GoogleOAuth2Client`（OAuth2 授权码流程）
- [ ] UA-02.5: 在基础设施层实现 `WeChatOAuth2Client`（微信开放平台扫码登录）
- [ ] UA-02.6: 在基础设施层实现 `AlipayOAuth2Client`（支付宝获取用户信息）
- [ ] UA-02.7: 实现 `GET /api/auth/oauth/{provider}/login` - 生成 state 存 Redis（5 分钟 TTL），302 重定向
- [ ] UA-02.8: 实现 `GET /api/auth/oauth/{provider}/callback` - 校验 state → 换 code → 拉取用户信息 → 登录或创建账户
- [ ] UA-02.9: 首次登录自动创建账户（角色 Buyer，无密码）
- [ ] UA-02.10: 已绑定用户直接登录，返回 JWT
- [ ] UA-02.11: 实现 `ExternalLogin` 实体（Provider、ProviderUserId、UserId 唯一约束）
- [ ] UA-02.12: 编写 OAuth2 完整流程集成测试

### 验收标准
- [ ] 支持微信/支付宝/Google OAuth2 登录
- [ ] state 参数 5 分钟过期，防 CSRF
- [ ] 同一 provider+providerUserId 唯一绑定

---

## Task UA-03: 双因子认证 (TOTP) [P1]

### 子任务 Checklist

- [ ] UA-03.1: 添加 `Otp.NET` NuGet 包
- [ ] UA-03.2: 在基础设施层实现 `TotpTokenVerifier` 实现 `ITokenVerifier`
- [ ] UA-03.3: 在 User 聚合中实现 `EnableTwoFactor`（生成 secret，置 TwoFactorEnabled=false 待确认）
- [ ] UA-03.4: 在 User 聚合中实现 `ConfirmTwoFactor`（验证 TOTP 码，置 TwoFactorEnabled=true）
- [ ] UA-03.5: 在 User 聚合中实现 `DisableTwoFactor`（需二次确认）
- [ ] UA-03.6: 实现 `POST /api/account/two-factor/enable` - 生成 secret 和二维码 URI
- [ ] UA-03.7: 实现 `POST /api/account/two-factor/confirm` - 验证 TOTP 码
- [ ] UA-03.8: 实现 `POST /api/account/two-factor/disable` - 关闭双因子
- [ ] UA-03.9: 登录时检测双因子启用状态，返回 `twoFactorRequired` 标志
- [ ] UA-03.10: 实现 `POST /api/auth/two-factor/verify` - 二次验证后返回 JWT

### 验收标准
- [ ] TOTP 双因子启用/确认/关闭流程完整
- [ ] 未验证的双因子配置不生效
- [ ] 二维码 URI 正确生成

---

## Task UA-04: 密码找回 [P1]

### 子任务 Checklist

- [ ] UA-04.1: 在领域层定义 `ForgotPasswordRequestedEvent` 领域事件
- [ ] UA-04.2: 实现一次性重置令牌生成（10 分钟过期，Redis 存储，key: `reset:pwd:{token}`）
- [ ] UA-04.3: 实现 `POST /api/account/forgot-password` - 接收邮箱/手机号，发送验证码
- [ ] UA-04.4: 实现 `POST /api/account/reset-password` - 验证令牌 + 新密码，重置密码
- [ ] UA-04.5: 重置令牌一次性使用，使用后立即删除
- [ ] UA-04.6: 验证码发送经消息通知域（BC10）的 `INotificationService`
- [ ] UA-04.7: 密码重置后发布 `PasswordChangedEvent`，通知用户密码已变更
- [ ] UA-04.8: 编写密码找回完整流程测试

### 验收标准
- [ ] 支持邮箱/手机号找回密码
- [ ] 重置令牌 10 分钟过期，一次性使用
- [ ] 密码重置后发布 PasswordChangedEvent

---

## Task UA-05: 权限策略管理 (RBAC) [P1]

### 子任务 Checklist

- [ ] UA-05.1: 创建 `Role` 实体（RoleId、Name、Description、Permissions、IsBuiltIn）
- [ ] UA-05.2: 创建 `Permission` 值对象（ResourceKey 格式: `api:/path` 或 `ui:module:action`）
- [ ] UA-05.3: 实现 `IPermissionRepository` 接口
- [ ] UA-05.4: 实现 `EfCorePermissionRepository`
- [ ] UA-05.5: 实现 `GET /api/admin/roles` - 角色列表
- [ ] UA-05.6: 实现 `POST /api/admin/roles` - 新增角色
- [ ] UA-05.7: 实现 `PUT /api/admin/roles/{roleId}` - 编辑角色
- [ ] UA-05.8: 实现 `DELETE /api/admin/roles/{roleId}` - 删除角色（校验无用户引用）
- [ ] UA-05.9: 实现 `GET /api/admin/roles/{roleId}/permissions` - 查看角色权限
- [ ] UA-05.10: 实现 `PUT /api/admin/roles/{roleId}/permissions` - 更新角色权限
- [ ] UA-05.11: 内置角色保护（Buyer/Seller/Operator/Admin 不可删除）

### 验收标准
- [ ] 角色 CRUD 完整
- [ ] 权限资源绑定格式正确
- [ ] 内置角色不可删除

---

## Task UA-06: OAuth2 客户端参数配置 [P2]

### 子任务 Checklist

- [ ] UA-06.1: 创建 `OAuthClient` 实体（Provider、ClientId、ClientSecret、RedirectUri、Enabled）
- [ ] UA-06.2: 实现 `IOAuthClientRepository` 接口
- [ ] UA-06.3: 实现 `GET /api/admin/oauth-clients` - 列表（clientSecret 脱敏为 `****`）
- [ ] UA-06.4: 实现 `PUT /api/admin/oauth-clients/{provider}` - 更新参数
- [ ] UA-06.5: 实现 `POST /api/admin/oauth-clients/{provider}/enable` - 启用
- [ ] UA-06.6: 实现 `POST /api/admin/oauth-clients/{provider}/disable` - 停用
- [ ] UA-06.7: clientSecret 加密存储（AES-256）

### 验收标准
- [ ] OAuth2 客户端参数 CRUD 完整
- [ ] clientSecret 加密存储、脱敏返回
- [ ] 停用提供商不影响已绑定账号

---

## Task UA-07: 第三方账号绑定与解绑 [P2]

### 子任务 Checklist

- [ ] UA-07.1: 在 User 聚合中实现 `BindExternalLogin(provider, providerUserId, email)` 方法
- [ ] UA-07.2: 在 User 聚合中实现 `UnbindExternalLogin(provider)` 方法
- [ ] UA-07.3: 解绑时校验至少保留一种登录方式（密码或一个第三方）
- [ ] UA-07.4: 实现 `POST /api/account/external-logins` - 绑定第三方（走 OAuth2 授权）
- [ ] UA-07.5: 实现 `DELETE /api/account/external-logins/{provider}` - 解绑
- [ ] UA-07.6: 绑定/解绑发布对应领域事件

### 验收标准
- [ ] 绑定第三方账号完整流程
- [ ] 解绑保留至少一种登录方式
- [ ] 同一 provider+providerUserId 唯一绑定

---

## Task UA-08: 审计日志中间件 [P2]

### 子任务 Checklist

- [ ] UA-08.1: 创建 `AuditLog` 实体（LogId、OperatorId、Action、ResourceType、ResourceId、BeforeSnapshot、AfterSnapshot、OperatedAt、Ip、UserAgent）
- [ ] UA-08.2: 实现 `IAuditLogRepository` 接口（仅 `AddAsync` 写入方法）
- [ ] UA-08.3: 实现 `AuditLogInterceptor` 中间件，自动拦截管理操作（POST/PUT/DELETE）
- [ ] UA-08.4: 审计日志与业务事务同一事务写入（发件箱模式）
- [ ] UA-08.5: 创建 `AuditDbContext` 或复用现有 `UserAuthDbContext` 的 OutboxMessages
- [ ] UA-08.6: 本域不对外暴露审计日志查询 API（由 BC11 承载）

### 验收标准
- [ ] 审计日志在管理操作事务内自动写入
- [ ] 业务回滚时审计日志一并回滚
- [ ] 审计日志不可修改不可删除