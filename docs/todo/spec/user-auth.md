# 用户与认证授权域 - 缺失功能任务

> **限界上下文**: BC1 用户与认证授权域
> **对应文档**: `01-用户与认证授权域.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

用户域已实现核心功能（注册、登录、JWT、地址管理、管理员用户管理），但以下功能缺失：

| 缺失项 | 严重程度 | 说明 |
|---------|----------|------|
| OAuth2 第三方登录 | P0 关键 | 微信/支付宝/Google 登录未实现 |
| 双因子认证 (TOTP) | P1 重要 | 双因子启用/确认/关闭流程未实现 |
| 密码找回 | P1 重要 | 通过邮箱/手机验证码重置密码未实现 |
| 权限策略管理 (RBAC) | P1 重要 | 角色与权限资源绑定管理未实现 |
| OAuth2 客户端参数配置 | P2 一般 | 管理员维护 OAuth2 提供商参数未实现 |
| 审计日志中间件 | P2 一般 | 审计日志自动拦截写操作未实现 |
| 第三方账号绑定/解绑 | P2 一般 | 已登录用户绑定/解绑第三方账号未实现 |
| 测试项目 | P0 关键 | 无任何测试项目 |

---

## Task 1: 测试项目创建

**严重程度**: P0 关键

### 功能描述
创建 `Leno.UserAuth.Domain.Tests`、`Leno.UserAuth.Application.Tests`、`Leno.UserAuth.Api.Tests` 测试项目，覆盖 User 聚合、Address 聚合、应用服务与 API 端点。

### 技术实现路径
1. 创建 `src/Services/UserAuth/Leno.UserAuth.Domain.Tests/` 项目
2. 覆盖 User 聚合所有方法（Register、ChangePassword、VerifyPassword、Lock、Unlock、Disable、AssignRole 等）
3. 覆盖 Address 聚合所有方法（Create、UpdateInfo、MarkAsDefault、SoftDelete）
4. 覆盖应用服务（RegisterAsync、LoginAsync、ChangePasswordAsync）
5. 覆盖 API 控制器集成测试

### 预期完成标准
- [ ] 领域层单元测试覆盖率 ≥ 80%
- [ ] 每个聚合根方法覆盖正常/边界/异常路径
- [ ] 应用层测试覆盖注册→登录→获取信息全流程
- [ ] API 集成测试覆盖鉴权与权限校验

### 参考
- `编码规范.md` 第 13 章
- `01-用户与认证授权域.md` 第 8 章验收标准

---

## Task 2: OAuth2 第三方登录

**严重程度**: P0 关键

### 功能描述
实现微信、支付宝、Google 的 OAuth2 授权码模式登录，支持首次登录自动创建账户与已绑定用户直接登录。

### 技术实现路径
1. 在领域层实现 `IExternalAuthService` 接口与实现
2. 创建 `ExternalAuthService` 协调 OAuth2 流程
3. 实现 `OAuth2Controller`：
   - `GET /api/auth/oauth/{provider}/login` - 生成 state 存 Redis，302 重定向
   - `GET /api/auth/oauth/{provider}/callback` - 校验 state，换 code，拉取用户信息
4. 实现微信/支付宝/Google 三个 OAuth2 客户端适配器
5. 实现 `FindUserByExternalLoginAsync` 和 `CreateUserFromExternalAsync`
6. OAuth2 客户端参数从配置读取（支持后续 FR-22 管理）

### 预期完成标准
- [ ] 支持微信 OAuth2 登录
- [ ] 支持支付宝 OAuth2 登录
- [ ] 支持 Google OAuth2 登录
- [ ] 首次登录自动创建账户（角色 Buyer，无密码）
- [ ] state 参数 5 分钟过期，防 CSRF
- [ ] 同一 provider+providerUserId 唯一绑定

### 参考
- `01-用户与认证授权域.md` 第 4.4 节
- `01-用户与认证授权域.md` 验收标准 AC-14 ~ AC-17

---

## Task 3: 双因子认证 (TOTP)

**严重程度**: P1 重要

### 功能描述
实现基于 TOTP 的双因子认证，支持启用（生成二维码）、确认验证、关闭流程。

### 技术实现路径
1. 添加 `Otp.NET` 或 `GoogleAuthenticator` NuGet 包
2. 在基础设施层实现 `TotpTokenVerifier`
3. 实现 `ITokenVerifier` 接口（`VerifyTotp`、`VerifySms`）
4. 在 User 聚合中实现 `EnableTwoFactor`、`ConfirmTwoFactor`、`DisableTwoFactor` 方法
5. 实现双因子二次验证 API：
   - `POST /api/account/two-factor/enable` - 生成 secret 和二维码 URI
   - `POST /api/account/two-factor/confirm` - 验证 TOTP 码
   - `POST /api/account/two-factor/disable` - 关闭双因子（需二次确认）
6. 登录时检测双因子启用状态，返回 `twoFactorRequired` 标志

### 预期完成标准
- [ ] TOTP 双因子启用/确认/关闭流程完整
- [ ] 未验证的双因子配置不生效
- [ ] 登录时双因子启用则返回临时凭证
- [ ] 双因子启用/关闭发布对应领域事件
- [ ] 二维码 URI 正确生成

### 参考
- `01-用户与认证授权域.md` 第 4.5 节
- `01-用户与认证授权域.md` 第 7.3 节状态机
- 验收标准 AC-21 ~ AC-22

---

## Task 4: 密码找回

**严重程度**: P1 重要

### 功能描述
实现通过邮箱或手机验证码找回密码的完整流程。

### 技术实现路径
1. 实现 `ForgotPasswordRequestedEvent` 领域事件
2. 实现一次性重置令牌生成（10 分钟过期，Redis 存储）
3. 实现 API：
   - `POST /api/account/forgot-password` - 发送验证码到邮箱/手机
   - `POST /api/account/reset-password` - 验证令牌并重置密码
4. 验证码发送经消息通知域（BC9）的 `INotificationService`
5. 重置令牌一次性使用，使用后失效

### 预期完成标准
- [ ] 支持邮箱找回密码
- [ ] 支持手机号找回密码
- [ ] 重置令牌 10 分钟过期
- [ ] 重置令牌一次性使用
- [ ] 密码重置后发布 `PasswordChangedEvent`

### 参考
- `01-用户与认证授权域.md` 第 4.5 节
- 验收标准 AC-20

---

## Task 5: 权限策略管理 (RBAC)

**严重程度**: P1 重要

### 功能描述
实现管理员维护角色与 API/UI 资源绑定关系的完整 RBAC 权限策略管理。

### 技术实现路径
1. 实现 `Role` 实体（含权限绑定）与 `Permission` 值对象
2. 实现 `IPermissionRepository` 接口
3. 实现 `EfCorePermissionRepository`
4. 实现 API：
   - `GET /api/admin/roles` - 角色列表
   - `POST /api/admin/roles` - 新增角色
   - `PUT /api/admin/roles/{roleId}` - 编辑角色
   - `DELETE /api/admin/roles/{roleId}` - 删除角色
   - `GET /api/admin/roles/{roleId}/permissions` - 查看角色权限
   - `PUT /api/admin/roles/{roleId}/permissions` - 更新角色权限
5. 权限资源格式：`api:/api/admin/products`、`ui:product:force-take-down`
6. 内置角色（Buyer/Seller/Operator/Admin）不可删除，可编辑权限绑定

### 预期完成标准
- [ ] 角色 CRUD 完整
- [ ] 权限资源绑定格式正确
- [ ] 内置角色保护（不可删除）
- [ ] 删除角色前校验无用户引用
- [ ] 权限绑定变更附加审计日志

### 参考
- `01-用户与认证授权域.md` 第 4.9 节
- `01-用户与认证授权域.md` 第 2.1.3 节 Role 值对象

---

## Task 6: OAuth2 客户端参数配置

**严重程度**: P2 一般

### 功能描述
实现管理员维护微信/支付宝/Google OAuth2 提供商客户端参数。

### 技术实现路径
1. 创建 `OAuthClient` 实体或聚合
2. 实现 `IOAuthClientRepository` 接口
3. 实现 API：
   - `GET /api/admin/oauth-clients` - 列表（clientSecret 脱敏）
   - `PUT /api/admin/oauth-clients/{provider}` - 更新参数
   - `POST /api/admin/oauth-clients/{provider}/enable` - 启用
   - `POST /api/admin/oauth-clients/{provider}/disable` - 停用
4. clientSecret 加密存储
5. 参数变更附加审计日志

### 预期完成标准
- [ ] OAuth2 客户端参数 CRUD 完整
- [ ] clientSecret 加密存储、脱敏返回
- [ ] 停用提供商不影响已绑定账号
- [ ] 参数变更附加审计日志

### 参考
- `01-用户与认证授权域.md` 第 4.10 节

---

## Task 7: 第三方账号绑定与解绑

**严重程度**: P2 一般

### 功能描述
实现已登录用户绑定或解绑第三方账号（微信/支付宝/Google）。

### 技术实现路径
1. 实现 API：
   - `POST /api/account/external-logins` - 绑定第三方（走 OAuth2 授权）
   - `DELETE /api/account/external-logins/{provider}` - 解绑
2. 在 User 聚合中实现 `BindExternalLogin`、`UnbindExternalLogin` 方法
3. 解绑时校验至少保留一种登录方式（密码或一个第三方）
4. 绑定/解绑发布对应领域事件

### 预期完成标准
- [ ] 绑定第三方账号完整流程
- [ ] 解绑第三方账号（保留至少一种登录方式）
- [ ] 同一 provider+providerUserId 唯一绑定
- [ ] 绑定/解绑发布领域事件

### 参考
- `01-用户与认证授权域.md` 第 4.4 节
- 验收标准 AC-17

---

## Task 8: 审计日志中间件

**严重程度**: P2 一般

### 功能描述
实现审计中间件，自动拦截管理操作并在事务内写入 AuditLog 记录。

### 技术实现路径
1. 实现 `AuditLog` 实体（LogId、OperatorId、Action、ResourceType、ResourceId、BeforeSnapshot、AfterSnapshot、OperatedAt、Ip、UserAgent）
2. 实现 `IAuditLogRepository` 接口（仅 `AddAsync` 写入方法）
3. 实现 `AuditLogInterceptor` 中间件，自动拦截管理操作
4. 审计日志与业务事务同一事务写入
5. 本域不对外暴露审计日志查询 API（由 BC11 承载）

### 预期完成标准
- [ ] 审计日志在管理操作事务内自动写入
- [ ] 业务回滚时审计日志一并回滚
- [ ] 审计日志不可修改不可删除
- [ ] 本域不暴露审计日志查询 API

### 参考
- `01-用户与认证授权域.md` 第 4.11 节
- `01-用户与认证授权域.md` 第 2.1.4 节