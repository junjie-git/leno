# UserAuth（用户与认证授权域）代码分析报告

## 概述
- 扫描范围：`src/Services/UserAuth/Leno.UserAuth.{Domain,Application,Infrastructure,Api}/`
- 代码行数（业务，非测试，含迁移文件已按规则排除 Designer/Snapshot）：约 6500 行
- 问题总数：高 15 / 中 19 / 低 12

---

## 🔴 高风险问题

### 1. InMemoryRefreshTokenStore 被注册为生产实现，多实例部署即生产事故
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/InMemoryRefreshTokenStore.cs#L1-L65` 与 `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L64`
- **类别**：C7 资源/连接池 / A7 异步消息可靠性 / C5 异步消息堆积
- **根因**：`InMemoryRefreshTokenStore` 使用 `ConcurrentDictionary` 进程内存储刷新令牌，注释虽提示"生产环境应替换为基于 Redis 或数据库的实现"，但 `ServiceCollectionExtensions.AddUserAuthInfrastructure` 中以 `services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>()` 注册且**没有任何保护**：未读取配置开关、未做生产环境断言、未提供 Redis 替代实现。所有写代码（登录、刷新、忘记密码）都直接依赖该实现。
- **影响**：水平扩容到 2+ 实例后，A 实例签发的 RefreshToken 在 B 实例验证失败；进程重启后所有用户被强制登出；`RevokeAllAsync` 只能撤销当前实例上的令牌，安全语义失效（用户改密 / 锁定后旧令牌在其他实例仍可用）。这是 BC 内最严重的高可用与数据一致性风险。
- **修复建议**：
  1. 新增 `RedisRefreshTokenStore` 实现：`SET key value EX ttl`，`ValidateAndRotateAsync` 使用 Lua 脚本原子 `GETDEL`；
  2. 在 `AddUserAuthInfrastructure` 中读取 `RefreshToken:Provider` 配置，默认 Redis；仅当显式配置 `InMemory` 且环境为 Development 时才使用 `InMemoryRefreshTokenStore`；
  3. 增加 `InMemoryRefreshTokenStore` 启动期日志告警。
- **影响范围**：`UserAppService` 全部登录、刷新、忘记密码、改密路径；`AccountAppService`；安全语义。

### 2. OAuth 回调"邮箱匹配静默绑定"导致账户接管漏洞
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L287-L306`
- **类别**：A2 异常处理不当（更准确是身份认证缺失） / A8 事务边界
- **根因**：OAuth 首次登录回调时，若第三方返回的 `Email` 在 `users` 表已存在，则**直接将外部登录绑定到该已有账户**并签发令牌，不验证用户对该邮箱的所有权。攻击者只要控制一个 Google 账户且把邮箱改成受害者邮箱（或注册一个同名邮箱的 Google 账户），即可登录受害者账户并获取 JWT。
- **影响**：账户接管，资金与个人信息泄露。Google 邮箱可被用户随意设置（未验证邮箱也可暴露在 `userinfo` 端点，取决于 Google Workspace 配置），微信 / 支付宝构造的伪邮箱 `{openId}@wechat.local` 与 `{userId}@alipay.local` 还可能撞库。
- **修复建议**：
  1. 删除"邮箱已存在则自动绑定"分支；
  2. OAuth 首次登录一律创建新账户，邮箱冲突时返回错误并要求用户先登录已有账户后在 `AccountController.BindExternalLogin` 完成绑定（该路径已有 `existingUser.Id != userId` 校验）；
  3. 若必须保留自动绑定，需校验第三方返回的 `email_verified=true` 且只对 Google / 微信 UnionID 等强身份可信。
- **影响范围**：`HandleOAuthCallbackAsync` 全路径；下游 Membership / Notification BC 通过 `UserRegisteredEvent` 误创建账户。

### 3. `HandleOAuthCallbackAsync` 使用反射绕过聚合封装修改 Username
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L312-L331`
- **类别**：B2 聚合设计违规 / A1 空引用
- **根因**：当用户名冲突时，代码使用 `typeof(User).GetProperty(nameof(User.Username))!.SetValue(newUser, candidate);` 直接反射写 `User.Username` 的 `private set`。`User` 聚合明确以 private setter 隔离外部修改并要求经工厂或行为方法变更，应用层通过反射绕过是 BC 内聚的根本性破坏。此外：
  - `User.CreateFromExternal(Guid.NewGuid(), externalLoginInfo)` 在循环内被多次调用，每次产生新 Id 与新的 `UserRegisteredDomainEvent`，前一次的事件被丢弃但 Id 已变化，若 UoW 持有上一引用会导致脏跟踪；
  - 反射写入后未触发任何校验（如 `ValidateUsername`），可能写入超长 / 非法字符；
  - 死循环兜底 `retry > 10` 抛 `USER_USERNAME_CONFLICT`，但 10 次重试都重新 `CreateFromExternal` + 反射，无指数退避。
- **影响**：聚合不变量被绕过；用户名可能写入非法值；UUID 在重试中漂移，潜在的脏跟踪 + 主键冲突。
- **修复建议**：
  1. 在 `User` 聚合上新增 `Rename(string newUsername)` 行为方法（带 `ValidateUsername` 校验），应用层调用它；
  2. 一次构造 `User` 后只调用 `Rename` 重试用户名，不重建整个聚合；
  3. 用户名唯一性应在 DB 唯一索引 `ix_users_username` 上兜底，应用层捕获 `DbUpdateException` 后重试。
- **影响范围**：OAuth 注册全部路径；`User` 聚合契约。

### 4. `ForgotPasswordAsync` 未调用 `UpdateAsync`，领域事件 / Outbox 可能丢失
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L419-L449`
- **类别**：A7 异步消息可靠性 / C6 Outbox / 幂等性
- **根因**：`user.PublishForgotPasswordRequested(resetToken)` 仅向聚合添加 `ForgotPasswordRequestedEvent` 领域事件，然后直接 `await _unitOfWork.SaveEntitiesAsync(ct);`。但**全程未调用 `_userRepository.UpdateAsync(user, ct)`**。与同文件其他方法（`ChangePasswordAsync` L206、`EnableTwoFactorAsync` L345 等）的写法不一致。若 BaseDbContext / UoW 对未显式 Attach 的实体在 SaveChanges 时跳过领域事件收集（取决于 BaseDbContext 是否在 SaveEntitiesAsync 中扫描 ChangeTracker.Entries 的 DomainEvents），事件将丢失。
- **影响**：忘记密码通知邮件不发送，用户体验受影响；且 Redis 中重置令牌已写入但通知未发出，导致令牌泄漏且用户体验破裂。
- **修复建议**：
  1. 在 `PublishForgotPasswordRequested` 之后增加 `await _userRepository.UpdateAsync(user, ct);`；
  2. 单测覆盖：断言 `ForgotPasswordRequestedEvent` 已进入 Outbox；
  3. 检查 `BaseDbContext.SaveEntitiesAsync` 实现是否依赖 `Entry.Entity` 的 `State` 为 `Modified`，若是则必须显式 Update。
- **影响范围**：忘记密码全流程；通知 BC 消费者。

### 5. `RefreshTokenAsync` 不校验 Locked 状态，被锁用户仍可刷新令牌
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L157-L177`
- **类别**：A4 状态机非法迁移 / A2 异常处理不当
- **根因**：刷新令牌时仅检查 `user.Status == AccountStatus.Disabled`，未检查 `Locked` 与 `LockedUntil`。被锁定用户持有的 RefreshToken 仍然有效，可换取新 AccessToken 继续访问 API，绕过登录锁定机制。同时未检查 `TwoFactorEnabled` 状态——用户启用 2FA 后，旧刷新令牌仍可直接换 AccessToken 而无需二次验证。
- **影响**：登录锁定机制可被绕过；2FA 强度被削弱。
- **修复建议**：
  ```csharp
  if (user.Status == AccountStatus.Disabled)
      throw new UnauthorizedAccessException("账户已被禁用");
  if (user.Status == AccountStatus.Locked &&
      user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
      throw new UserAuthDomainException($"账户已锁定，请于 {user.LockedUntil:O} 后重试", "USER_LOCKED");
  // 若启用 2FA，刷新令牌不应直接换发 AccessToken，应改为短 TTL 的 AccessToken 仅访问 /api/auth/verify-2fa
  ```
- **影响范围**：`RefreshTokenAsync`；账户锁定 / 2FA 安全策略。

### 6. `UserConfiguration` 的 Email/Phone 唯一索引使用 PostgreSQL 语法，与 UseSqlServer 不匹配
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs#L69-L72` 与 `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L45`
- **类别**：A5 边界条件 / C2 缺失索引
- **根因**：`ServiceCollectionExtensions.AddUserAuthInfrastructure` 调用 `options.UseSqlServer(connectionString)`，但 `UserConfiguration` 中过滤索引写为 PostgreSQL 风格：`.HasFilter("\"email\" IS NOT NULL")`。SQL Server 的过滤索引语法为 `WHERE ([email] IS NOT NULL)`，使用方括号标识符与 `WHERE` 关键字。该配置在 SQL Server 上要么迁移失败，要么 `HasFilter` 被忽略导致索引退化为非过滤唯一索引——而 `email` 为 NULL 的多行会因唯一约束冲突插入失败（OAuth 用户 email 列允许 NULL）。
- **影响**：开发环境若使用 PostgreSQL 或 SQLite 测试则不暴露问题；部署到 SQL Server 生产环境后，第二个 OAuth 用户注册即因唯一约束冲突失败，业务阻断。
- **修复建议**：改为 `.HasFilter("[email] IS NOT NULL")`，并在迁移中验证生成的 SQL。或使用 `IS NOT NULL` 不带标识符的 SQL-92 标准（SQL Server 支持）。
- **影响范围**：所有 OAuth 用户与多邮箱用户注册路径。

### 7. `AddressConfiguration` 默认地址索引未唯一，应用层并发不安全
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/AddressConfiguration.cs#L34-L35` 与 `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AddressAppService.cs#L145-L167`
- **类别**：A3 并发与竞态 / B2 聚合设计违规
- **根因**：`AddressAppService.SetDefaultAsync` 通过 `ClearExistingDefaultAsync` 读改写所有地址的 `IsDefault` 字段实现"默认地址唯一"不变量。但 `AddressConfiguration` 中 `ix_addresses_user_default` 索引**未设置 `IsUnique()` 且无 `WHERE is_default = true` 过滤**，数据库层面不约束。两个并发的 `SetDefaultAsync`（A 把 add1 设默认，B 把 add2 设默认）可能同时通过 `ClearExistingDefault` 后各自写入 `IsDefault=true`，最终用户存在两条默认地址，破坏 `User.DefaultAddressId` 的单一性语义，并使下游 Order BC 拉取默认地址时行为不确定。
- **影响**：默认地址漂移；下单地址错乱；订单路由错发。
- **修复建议**：
  ```csharp
  builder.HasIndex(a => new { a.UserId, a.IsDefault })
      .HasDatabaseName("ix_addresses_user_default")
      .IsUnique()
      .HasFilter("[is_default] = 1");
  ```
  并配合乐观锁（`RowVersion`）或 `SERIALIZABLE` 事务隔离；考虑用单条 `UPDATE addresses SET is_default = (id = @targetId) WHERE user_id = @userId` 原子化。
- **影响范围**：地址写操作；Order BC 默认地址消费方。

### 8. `AccountAppService` 与 `OAuthClientAppService` 使用 `SaveChangesAsync` 而非 `SaveEntitiesAsync`，领域事件 / Outbox 丢失
- **位置**：
  - `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AccountAppService.cs#L77` `BindExternalLoginAsync`
  - `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AccountAppService.cs#L96` `UnbindExternalLoginAsync`
  - `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L67` `UpdateAsync`
  - `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L75` `EnableAsync`
  - `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L83` `DisableAsync`
- **类别**：A7 异步消息可靠性 / B7 事件契约一致性 / C6 Outbox
- **根因**：`IUnitOfWork.SaveChangesAsync` 通常只调用 `DbContext.SaveChangesAsync`，不会处理领域事件收集与 Outbox 写入。`User.LinkExternalLogin` 触发 `ExternalLoginLinkedEvent`、`OAuthClient.Enable/Disable` 等虽然当前未在 `UserAuthIntegrationEventMapper` 注册翻译（mapper 注释明确"保持内部领域事件"），但只要后续订阅方出现（Notification BC 监听登录绑定通知、审计 BC 监听 OAuth 客户端变更），这些事件就会丢失。其他应用服务（`UserAppService`、`AddressAppService`、`UserAdminAppService`、`PermissionAppService`）都使用 `SaveEntitiesAsync`，只有这两个服务遗漏。
- **影响**：未来添加集成事件订阅方时事件丢失；同事务内审计日志也不写入（因为审计写入也是通过 ChangeTracker + 拦截器配合 SaveEntitiesAsync 完成的）。
- **修复建议**：全部替换为 `await _unitOfWork.SaveEntitiesAsync(ct);`，并添加单测断言 `ExternalLoginLinkedEvent` 与 `OAuthClient` 变更事件进入 Outbox。
- **影响范围**：外部登录绑定 / 解绑、OAuth 客户端配置变更；下游订阅方。

### 9. `PermissionAppService` 与 `OAuthClientAppService` 管理操作无审计日志
- **位置**：
  - `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/PermissionAppService.cs#L52-L137`（CreateRole / UpdateRole / DeleteRole / UpdateRolePermissions）
  - `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L38-L84`（Update / Enable / Disable）
- **类别**：B7 事件契约一致性 / A2 异常处理不当
- **根因**：`UserAdminAppService.AssignRolesAsync/SuspendAsync/ResumeAsync` 都写 `AuditLog`，但 `PermissionAppService` 与 `OAuthClientAppService` 对同等敏感的管理员操作（角色 CRUD、权限全量替换、OAuth 提供方启停）**完全无审计**。攻击者拿到 Admin 账户后修改 OAuth ClientSecret / RedirectUri 到自己的服务器，或添加 `ui:admin:*` 权限给 Buyer 角色——这些动作无审计追溯。
- **影响**：RBAC 被篡改后无追溯；OAuth 客户端被替换为恶意配置后无审计；合规审计失败。
- **修复建议**：在 `PermissionAppService` 与 `OAuthClientAppService` 中注入 `IAuditLogRepository`，每个写操作前后做 `Snapshot` 并写 `AuditLog.Create`，与 `UserAdminAppService` 保持一致。同时审计 `AccountAppService.BindExternalLoginAsync`（用户绑定外部登录是安全敏感事件）。
- **影响范围**：角色、权限、OAuth 客户端全部写操作；合规审计。

### 10. `ChangePassword` / `ResetPassword` 不撤销其他刷新令牌，密码变更后旧令牌仍可用
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L200-L208`（ChangePassword） 与 `#L452-L502`（ResetPassword）
- **类别**：A4 状态机非法迁移 / A2 异常处理不当
- **根因**：`ChangePasswordAsync` 调用 `user.ChangePassword` 后仅 `UpdateAsync + SaveEntitiesAsync`，未调用 `IRefreshTokenStore.RevokeAllAsync(user.Id)`。密码改后，已签发的 RefreshToken 仍可换取新 AccessToken；Access 黑名单也未写入（`IJwtRevocationService` 只在显式 Logout 时写入）。同样地，`ResetPasswordAsync` 也未撤销。`UserAppService` 构造函数已经注入了 `IRefreshTokenStore`，但忘记密码 / 改密路径根本不调用。
- **影响**：账户被盗后用户改密，攻击者持有的旧令牌仍可继续访问直到自然过期；管理员禁用账户同样失效（见问题 11）。
- **修复建议**：
  ```csharp
  await _refreshTokenStore.RevokeAllAsync(user.Id, ct);
  // 可选：调用 IJwtRevocationService 批量撤销当前用户所有未过期 jti（需要维护用户-jti 映射）
  ```
- **影响范围**：密码修改 / 重置全流程；安全策略。

### 11. `User.Disable / Lock` 不撤销已签发的 JWT 与 RefreshToken
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L182-L224` 与 `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAdminAppService.cs#L85-L131`
- **类别**：A4 状态机非法迁移 / A8 事务边界
- **根因**：`UserAdminAppService.SuspendAsync` / `ResumeAsync`(Activate 路径) 调用聚合行为后只更新 User 实体，不撤销该用户已签发的所有 RefreshToken，也不批量加入 JWT 黑名单。被锁定 / 禁用用户在令牌自然过期前仍可访问受保护资源。
- **影响**：管理员紧急封禁恶意账户的响应时间被拉长到 JWT TTL（通常 15-60 分钟）；安全事件扩散。
- **修复建议**：在 `UserAdminAppService` 注入 `IRefreshTokenStore`，`SuspendAsync` 末尾调用 `RevokeAllAsync(targetUserId, ct)`；考虑在网关侧增加按 `userId` 查询黑名单的能力（Redis Set），管理员封禁时把 `userId` 加入短期黑名单 Set，网关侧拒绝。
- **影响范围**：用户封禁 / 恢复流程；网关 JWT 校验。

### 12. `AesEncryptionService` 使用 CBC 模式无认证，存在 Padding Oracle 攻击向量
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/AesEncryptionService.cs#L7-L86`
- **类别**：A2 异常处理不当（更准确是密码学误用）
- **根因**：注释明确"使用 CBC 模式 + PKCS7 填充"，但未做 Encrypt-then-MAC（HMAC-SHA256）或使用 AES-GCM。`Decrypt` 方法捕获异常时只判断 `fullCipher.Length < 16`，未验证密文完整性。若攻击者获得数据库 `client_secret` 字段写入权限，可通过修改密文 + 观察响应错误类型推断明文（Padding Oracle）。该字段存储 OAuth ClientSecret，一旦泄露可冒充 Leno 调用 Google/WeChat/Alipay OAuth API。
- **影响**：OAuth ClientSecret 泄露风险；第三方平台账户接管。
- **修复建议**：
  1. 改用 `AesGcm`（.NET 8+ 内置）：`nonce(12B) + ciphertext + tag(16B)`；
  2. 或保留 CBC，但 `Encrypt` 后追加 `HMACSHA256(key2, iv || cipher)`，`Decrypt` 先验 HMAC 再解密；
  3. 重新加密所有现有 ClientSecret（一次性迁移脚本）。
- **影响范围**：所有 OAuth ClientSecret 存取；OAuth 配置管理路径。

### 13. OAuth `state` 不校验回调 provider 与 state 内 provider 一致
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L216-L260`
- **类别**：A2 异常处理不当 / A8 事务边界
- **根因**：`GetOAuthLoginUrlAsync` 把 `$"{authService.Provider}|{redirectUri}"` 存入 Redis state；`HandleOAuthCallbackAsync` 读取 state 后只取 `parts[0]`（`stateProvider`）但**从不与 callback URL 的 `provider` 参数比较**。`parts.Length < 1` 永远为 false（split 至少返回 1 元素），校验形同虚设。攻击者可以用 Google 的 state 在 WeChat callback 端点完成回调，触发 `ResolveAuthService("wechat")` 拿 WeChat 的 ClientId/Secret 调用——state 与 provider 跨实例失配的语义不明确，CSRF 防护被削弱。
- **影响**：跨 OAuth 提供方的 CSRF；state 重放。
- **修复建议**：
  ```csharp
  if (!string.Equals(stateProvider, provider.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
      throw new UserAuthDomainException("State 与 provider 不匹配", "OAUTH_STATE_PROVIDER_MISMATCH");
  ```
  并把 `redirectUri` 从 state 取出与 callback `redirectUri` 比较；同时校验 `parts.Length == 2`。
- **影响范围**：OAuth 登录全流程；CSRF 防护。

### 14. `FailedLoginCount` 并发累加无原子保护，可能绕过锁定阈值
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L122-L142` 与 `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L134-L145`
- **类别**：A3 并发与竞态
- **根因**：`VerifyPassword` 中 `FailedLoginCount++` 与 `Lock(...)` 在聚合内串行执行，但 EF Core 的并发控制依赖 `RowVersion` / 乐观锁。`UserConfiguration` 中未配置 `RowVersion` 字段。两个并发请求同时读取 `FailedLoginCount=4`，各自 `++` 写回 5，DB 中最终值是 5（不是 6），下一次失败才能触发锁定。极端情况下 N 个并发请求使实际失败次数被低估 N-1 倍。同样地，`Lock` 在并发下可能被多次触发（同事务内的两次 `Lock` 第二次抛 `USER_DISABLED` 异常，但若 `Status` 已是 `Locked`，第二次 `Lock` 会重置 `LockedUntil`）。
- **影响**：暴力破解阈值被削弱；账户锁定延迟触发。
- **修复建议**：
  1. 在 `UserConfiguration` 增加 `RowVersion` 字段（`byte[]`），`User` 聚合继承支持；
  2. 捕获 `DbUpdateConcurrencyException` 后重试 `VerifyPassword`；
  3. 或在 DB 层用 `UPDATE users SET failed_login_count = failed_login_count + 1 WHERE id = @id` 原子累加，并检查更新后的值。
- **影响范围**：登录全流程；账户锁定机制。

### 15. `AlipayOAuth2Client` 实际请求未做 RSA2 签名，调用真实支付宝网关必然失败
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/AlipayOAuth2Client.cs#L55-L91` 与 `#L100-L141`
- **类别**：A2 异常处理不当 / B3 防腐层缺失
- **根因**：支付宝开放平台所有 API 请求必须包含 `sign` 与 `sign_type` 参数，`sign` 由请求参数按字典序拼接后用商户私钥做 RSA2 签名。代码注释里写了 `sign_type=RSA2` 但**完全没生成 `sign` 参数**，只有参数列表。同样，响应也未做 RSA2 验签。调用真实支付宝网关必然返回 `isv.InvalidSignatures` 或类似错误，支付宝登录完全不可用。
- **影响**：支付宝登录在生产环境 100% 失败；开发环境若未连接真实支付宝，缺陷被掩盖。
- **修复建议**：引入 `AlipaySDKNet` 或自行实现 RSA2 签名：
  1. 加载商户私钥（PEM）与支付宝公钥；
  2. 按 ASCII 字典序拼接所有非空业务参数，`&` 连接；
  3. `RSA-SHA256` 签名后 Base64 编码作为 `sign`；
  4. 响应验签：用支付宝公钥校验响应中 `sign` 字段。
- **影响范围**：支付宝登录全部路径。

---

## 🟡 中风险问题

### 16. `JwtRevocationService` 不传递 CancellationToken
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/JwtRevocationService.cs#L21-L26`
- **类别**：A2 异常处理不当
- **根因**：`await db.StringSetAsync($"leno:jwt:blacklist:{jti}", "1", ttl);` 未传 `ct`，客户端断开后操作继续，浪费 Redis 连接。同时 `JwtRevocationService` 在 Application 层直接 `using StackExchange.Redis`（应用层不应依赖基础设施库），且 `IJwtRevocationService` 抽象在 Application 层但实现也在 Application 层（`Leno.UserAuth.Application/Services/JwtRevocationService.cs`）——应迁移到 Infrastructure。
- **修复建议**：传 `ct`；把 `JwtRevocationService` 移到 `Leno.UserAuth.Infrastructure.Services` 命名空间。
- **影响范围**：登出路径。

### 17. `LoginAsync` 账号枚举时序差异（注释声称防枚举但实际未做）
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L109-L154`
- **类别**：A2 异常处理不当
- **根因**：注释 `// 账号不存在统一返回账号或密码错误，防账号枚举（INV-18）`，但实际：账号不存在立即返回 401（耗时 < 1ms）；账号存在则执行 bcrypt.Verify（耗时 50-200ms）。攻击者通过响应时间差异可枚举有效账户。
- **修复建议**：账号不存在时也执行一次 bcrypt 哈希（用预设 dummy hash）再返回 401，使两条路径耗时一致。
- **影响范围**：登录路径。

### 18. `UserAppService` 直接依赖 `StackExchange.Redis.IConnectionMultiplexer` 与 `IDatabase`，应用层穿透基础设施
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L12-L13`、`#L48-L49`、`#L63`、`#L216-L219`、`#L237-L247`、`#L391-L407`、`#L439-L469`、`#L506-L518`
- **类别**：B6 层依赖反向 / B3 防腐层缺失
- **根因**：Application 层应当只依赖 Domain 抽象与自身抽象。当前直接 `using StackExchange.Redis;`，把 Redis 当成应用层一等公民使用，用于：OAuth state、2FA 临时令牌、密码重置令牌。若未来要替换为分布式缓存或内存缓存，需要修改 Application 层代码。
- **修复建议**：抽象三个接口 `IOAuthStateStore`、`ITwoFactorTempTokenStore`、`IPasswordResetTokenStore`，在 Infrastructure 提供基于 Redis 的实现。
- **影响范围**：`UserAppService` 全部 OAuth / 2FA / 密码重置逻辑。

### 19. `EfCorePermissionRepository.GetRolesByPermissionAsync` 全表加载内存过滤
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCorePermissionRepository.cs#L61-L67`
- **类别**：C4 大对象 / 全表扫
- **根因**：注释明确"permissions are stored as JSON, we load all roles and filter in memory"。权限以 JSON `nvarchar(max)` 存储，无法在 DB 端查询。当角色数达到几百时，每次权限校验都要全表加载 + 反序列化。
- **修复建议**：拆出 `role_permissions` 子表（`RoleId` + `ResourceKey` 复合主键 + 索引），或在 PostgreSQL 用 `jsonb` 与 GIN 索引；当前 UseSqlServer 不支持 jsonb。
- **影响范围**：权限查询 / RBAC 校验。

### 20. `WeChatOAuth2Client` / `AlipayOAuth2Client` 构造伪邮箱入库并触发集成事件
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/WeChatOAuth2Client.cs#L126` 与 `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/AlipayOAuth2Client.cs#L138`
- **类别**：B7 事件契约一致性 / A5 边界条件
- **根因**：`ExternalLoginInfo` 强制 `email` 非空，微信 / 支付宝不返回邮箱，代码硬构造 `{openId}@wechat.local` / `{userId}@alipay.local` 作为邮箱入库。该邮箱：(1) 经过 `UserRegisteredDomainEvent` 广播给 Membership / Notification BC，下游若发欢迎邮件必然失败；(2) 进入 `users.email` 字段并受 `ix_users_email` 唯一约束，若两个不同 OAuth 提供方返回相同 `openId`（实际不会，但 unionid 跨小程序可能）会冲突；(3) `UserAppService.HandleOAuthCallbackAsync` 邮箱匹配逻辑（问题 2）会把这些伪邮箱当作真实邮箱做匹配。
- **修复建议**：把 `ExternalLoginInfo.Email` 改为可空；`User.CreateFromExternal` 中 `Email = null`；`UserRegisteredDomainEvent` 增加 `IsEmailVerified` 字段，下游据此判断是否发邮件。
- **影响范围**：所有微信 / 支付宝用户；下游 BC。

### 21. `OAuthClientAppService.UpdateAsync` PUT 自动创建且默认 Enabled=true
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L38-L68`
- **类别**：A4 状态机非法迁移 / B8 仓储滥用
- **根因**：`UpdateAsync` 在 client 不存在时调用 `OAuthClient.Create` 创建，默认 `enabled = true`。管理员误传一个未校验的 `provider` 名称（如 `gogle`），会自动创建一个 Enabled 的 OAuth 客户端配置，污染 OAuth 解析器；同时违反 PUT 幂等性语义。
- **修复建议**：拆分为 `CreateAsync`（POST）与 `UpdateAsync`（PUT，不存在抛 `OAUTH_CLIENT_NOT_FOUND`）；新建 OAuth 客户端默认 `Enabled=false`，需显式 enable。
- **影响范围**：OAuth 客户端管理路径。

### 22. `AuditLogMiddleware` 写入的 HttpContext.Items 从未被读取，死代码
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Audit/AuditLogMiddleware.cs#L31-L48` 与 `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Audit/AuditLogInterceptor.cs#L39-L73`
- **类别**：B5 CQRS 职责混乱 / A2 异常处理不当
- **根因**：中间件在请求开始时解析 Action / ResourceType / ResourceId / OperatorId 存入 `HttpContext.Items["AuditLog:Action"]` 等，但拦截器 `EnrichAuditLogs` 只读取 `Ip / UserAgent / TraceId`，**从不读取中间件存的字段**。这些字段并未传递给 `AuditLog.Create`——`AuditLog.Create` 仅在应用服务（`UserAdminAppService.WriteAuditAsync`）显式调用。中间件实质上是死代码；且 `ResolveAction` / `ResolveResourceType` / `ResolveResourceId` / `ResolveOperatorId` 在请求体还未读取时执行（在 `_next` 之前），无法获取路由参数（除非路由已绑定），`ResolveResourceId` 依赖 `Guid.TryParse(segment)` 仅在路径模板 `{id:guid}` 已绑定时有效。
- **修复建议**：要么删除中间件，要么让中间件真正在响应阶段创建 `AuditLog` 记录（捕获响应状态码与异常）。
- **影响范围**：审计中间件；可观察性。

### 23. `OAuth2 redirectUri` 不做白名单校验，开放重定向
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L211-L222`
- **类别**：A2 异常处理不当 / B3 防腐层缺失
- **根因**：`GetOAuthLoginUrlAsync(state, redirectUri)` 直接把客户端传入的 `redirectUri` 拼接到第三方授权 URL 与存入 Redis state，无白名单校验。攻击者可构造钓鱼链接 `?redirectUri=https://evil.com/callback`，用户点击后被引导到 Google 授权页面授权 Leno 应用，授权码回调到 `evil.com`，攻击者用该 code 调用 Leno callback 完成登录（如果攻击者也能控制 state 流程）。
- **修复建议**：维护 `OAuth2:AllowedRedirectUris` 配置白名单，校验 `redirectUri` 必须在白名单内或匹配 `*.leno.com` 后缀。
- **影响范围**：OAuth 登录入口。

### 24. `UserRolesAssignment` 不影响已签发 JWT，特权提升延迟
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAdminAppService.cs#L58-L82` 与 `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Events/UserRoleAssignedEvent.cs#L7-L8`
- **类别**：A4 状态机非法迁移 / B7 事件契约一致性
- **根因**：事件注释明确"Token 中角色声明在下一次登录或刷新后生效"。但 `RefreshTokenAsync` 重新签发 JWT 时确实会拉取最新 `user.Roles`，所以正向提权有效。但**反向撤销 Admin 角色**后，被撤销用户的现有 JWT 仍带 Admin 角色声明直到自然过期（15-60 分钟），期间可继续执行 Admin 操作。结合问题 10、11（不撤销令牌），管理员撤销权限的实际生效时间被显著拉长。
- **修复建议**：管理员变更用户角色后，强制撤销该用户所有 RefreshToken 并把 `userId` 加入 JWT 短期黑名单。
- **影响范围**：RBAC 变更生效延迟；安全策略。

### 25. `User.ChangePassword` / `UpdateProfile` 不校验账户状态
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L147-L171`（ChangePassword）、`#L279-L286`（UpdateProfile）
- **类别**：A4 状态机非法迁移
- **根因**：聚合行为方法未检查 `Status`。Disabled / Locked 用户仍可调用 `ChangePassword` 与 `UpdateProfile`，结合问题 11（Disable 不撤销令牌），被禁用用户可改密码后继续使用已有令牌。
- **修复建议**：在 `ChangePassword` / `UpdateProfile` 入口检查 `Status == AccountStatus.Disabled` 抛 `USER_DISABLED`；Locked 状态可允许改密（视为正常流程）。
- **影响范围**：用户管理路径。

### 26. `InMemoryRefreshTokenStore` 不清理过期 token，内存泄漏
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/InMemoryRefreshTokenStore.cs#L1-L65`
- **类别**：C5 异步消息堆积 / A6 资源泄漏
- **根因**：`ConcurrentDictionary` 只在 `TryRemove` 时清除条目，过期 token（用户从未刷新）永远不清理。长期运行下内存持续增长。
- **修复建议**：使用 `IHostedService` 定时清理；或改用 `MemoryCache.Set` 带 TTL。
- **影响范围**：长期运行实例内存。

### 27. `EfCoreUserRepository.QueryAsync` LIKE 通配符 `%` / `_` 不转义
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCoreUserRepository.cs#L60-L65`
- **类别**：A5 边界条件
- **根因**：`EF.Functions.Like(u.Username, $"%{kw}%")` 中 `kw` 未转义 `%` 与 `_`。用户搜索 `%` 会匹配所有用户，搜索 `_` 会匹配任意单字符。虽无安全影响（管理员才能调用），但搜索结果不可预测。
- **修复建议**：`kw = kw.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");` 并使用 `EF.Functions.Like(u.Username, $"%{kw}%", "\\")`。
- **影响范围**：管理后台用户搜索。

### 28. `InternalUsersController` 返回未脱敏的 PII 给"内部"调用方
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserInternalQueryService.cs#L20-L35` 与 `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs#L22-L35`
- **类别**：B3 防腐层缺失 / A2 异常处理不当
- **根因**：`UserContactsDto.PhoneNumber` 与 `Email` 直接返回用户原始字段，未脱敏。`InternalUsersController` 仅靠 `InternalApiKeyMiddleware` 保护（per 注释），但若中间件配置错误（开发环境常跳过），任意调用方可拉取全部用户手机号 / 邮箱。同时 `UserContactsDto.Email` 是 `string` 非空，但 `user.Email` 可能是 null（OAuth 用户），代码用 `?? string.Empty` 兜底，下游无法区分"无邮箱"与"空邮箱"。
- **修复建议**：DTO 用 `string?`；增加 `InternalApiKey` 强校验 + mTLS；记录所有内部查询审计日志。
- **影响范围**：跨 BC 用户信息查询。

### 29. `UserAuthGrpcService` 标 `[Authorize]` 但实际靠拦截器，可能失效
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/GrpcServices/UserAuthGrpcService.cs#L13-L14`
- **类别**：A2 异常处理不当
- **根因**：注释说"鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）"，但类上加了 `[Authorize]`。若 ASP.NET Core 鉴权管线未对 gRPC 启用 JWT Bearer，`[Authorize]` 不生效；同时若拦截器顺序错误或被移除，gRPC 端点完全开放。`InternalUsersController` 路径同样依赖 `InternalApiKeyMiddleware` 但未见 `[AllowAnonymous]` 显式标注。
- **修复建议**：明确文档化 gRPC 与 HTTP 的鉴权策略；为 gRPC 添加专用鉴权拦截器测试。
- **影响范围**：gRPC 内部调用。

### 30. `OAuth2 callback` 的 `redirectUri` 缺省值使用 `Request.Host`，存在 Host Header 注入风险
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L125-L129`
- **类别**：A2 异常处理不当 / B3 防腐层缺失
- **根因**：`redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/oauth/{provider}/callback";` 直接信任 `Host` 头。若反向代理未设置 `ForwardedHost`，攻击者可构造 `Host: evil.com` 的请求让 callback 用 evil.com 作为 redirectUri，进而被发送给 OAuth 提供方。结合问题 23（无白名单），可放大为开放重定向。
- **修复建议**：从配置读取固定 `BaseUrl`；或严格校验 `Request.Host` 在已知主机列表内。
- **影响范围**：OAuth callback 路径。

### 31. `ResetPasswordAsync` 的 if/else 分支完全相同（死代码）
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L489-L498`
- **类别**：A1 空引用（代码异味）
- **根因**：`if (string.IsNullOrEmpty(user.PasswordHash))` 与 `else` 两个分支都执行 `user.ResetPassword(_passwordHasher.Hash(dto.NewPassword), _passwordHasher);`。意图大概是纯 OAuth 用户首次设置密码要走不同路径，但实际行为一致。
- **修复建议**：删除 if/else，直接 `user.ResetPassword(_passwordHasher.Hash(dto.NewPassword), _passwordHasher);`。
- **影响范围**：密码重置路径。

### 32. `User.GenerateUsernameFromEmail` 不去除保留字与最小长度边界处理脆弱
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L408-L425`
- **类别**：A5 边界条件
- **根因**：当邮箱前缀 `< 3` 字符时 `PadRight(3, '0')`，但若邮箱前缀经 sanitize 后为空（如 `@example.com`），`sanitized` 为空字符串，`PadRight(3, '0')` 后是 `"000"`——可能与其他 OAuth 用户冲突。同时未排除保留字（如 `admin`、`root`、`system`）。
- **修复建议**：保留字黑名单；空 sanitized 时使用 `user_{guid8}`。
- **影响范围**：OAuth 用户注册。

### 33. `OAuth2ProviderResolver` 与 `UserAppService.ResolveAuthService` 双重解析逻辑
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L584-L602` 与 `file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/OAuth2ProviderResolver.cs#L23-L41`
- **类别**：B3 防腐层缺失 / DRY 违反
- **根因**：应用服务已注入 `IEnumerable<IExternalAuthService>` 并自己实现 `ResolveAuthService`，与 `OAuth2ProviderResolver` 重复。`UserAppService` 应直接注入 `IOAuth2ProviderResolver` 抽象，防腐层职责单一。
- **修复建议**：`UserAppService` 构造函数改为注入 `IOAuth2ProviderResolver`；删除 `ResolveAuthService`。
- **影响范围**：OAuth 解析路径。

### 34. `RefreshTokenAsync` 中 `user.Status == AccountStatus.Disabled` 检查后未撤销已签发令牌
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L170-L176`
- **类别**：A4 状态机非法迁移
- **根因**：发现用户被禁用后只抛 `UnauthorizedAccessException`，未调用 `RevokeAllAsync` 撤销该 RefreshToken。攻击者持有的 RefreshToken 仍可重试，仅在下一次刷新时再次失败。
- **修复建议**：禁用检查失败时调用 `_refreshTokenStore.RevokeAllAsync(user.Id, ct)`。
- **影响范围**：刷新令牌路径。

---

## 🟢 低风险问题

### 35. `OAuthClientAppService.MaskSecret` 任意长度返回 "****"
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L126-L134`
- **类别**：代码气味
- **根因**：`if (string.IsNullOrEmpty(secret))` 与 else 都返回 `"****"`，分支冗余；且掩码无信息量，不能用于核对配置。
- **修复建议**：返回 `secret[..4] + "****" + secret[^4:]`（前 4 + 后 4）。

### 36. `AuditLogInterceptor` 直接操作 EF `Property().CurrentValue` 而非聚合行为方法
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Audit/AuditLogInterceptor.cs#L51-L72`
- **类别**：B2 聚合设计违规
- **根因**：`AuditLog` 的 `Ip / UserAgent / TraceId` 是 `private set`，拦截器用 EF 元数据 API 绕过 C# 访问修饰符写入。耦合 EF 内部 API，难以测试。
- **修复建议**：在 `AuditLog` 添加 `internal void Enrich(string? ip, string? ua, string? traceId)`。

### 37. `InternalUsersController` 标记 Obsolete 但同时映射同一路由
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs#L22-L35`
- **类别**：代码气味
- **根因**：`[Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]` 与 `[HttpGet("internal/v1/users/...")]` 同时存在；Obsolete 标注自身指向当前路径。注释自相矛盾。
- **修复建议**：要么新旧两个 action 拆分（旧的 Obsolete + 新的不 Obsolete），要么去掉 Obsolete。

### 38. `User.VerifyPassword` 未做时序安全防护（恒定时间比较）
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L122-L142`
- **类别**：A2 异常处理不当
- **根因**：bcrypt.Verify 内部已恒定时间，但当 `PasswordHash` 为空时直接 `return false`（< 1ms），存在账户时序差异。
- **修复建议**：PasswordHash 为空时执行一次 bcrypt 哈希 dummy 后再返回 false。

### 39. `UserConfiguration.password_hash` 最大长度 128 偏小
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs#L23`
- **类别**：C4 大对象
- **根因**：bcrypt 哈希固定 60 字符，128 留有余量；但若未来切换到 Argon2id（典型 96+ 字符）需扩列。
- **修复建议**：扩到 256。

### 40. `IssueTokensAsync.GetPrimaryRole` 只取最高权限角色，丢失多角色信息
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L571-L582`
- **类别**：B7 事件契约一致性
- **根因**：JWT 中只携带一个 role claim。同时持有 Buyer + Seller 角色的用户在网关 RBAC 校验时只能匹配 Seller 路由，Buyer 路由被拒。
- **修复建议**：JWT 中以数组形式携带所有角色 claim。

### 41. `RegisterDtoValidator` 不复用领域校验，校验逻辑重复
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Validators/RegisterDtoValidator.cs#L12-L14`
- **类别**：DRY 违反
- **根因**：用户名 / 邮箱 / 手机号正则在 `RegisterDtoValidator`、`User.ValidateUsername` / `ValidateEmail` / `ValidatePhone`、`SaveAddressDtoValidator` 中分别定义。
- **修复建议**：抽到 `Leno.UserAuth.Domain.ValueObjects.UsernamePattern` 等共享 VO。

### 42. `UserAppService.HandleOAuthCallbackAsync` 缺少 OAuth 用户 2FA 启用检测
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L266-L336`
- **类别**：A4 状态机非法迁移
- **根因**：OAuth 登录路径不检查 `user.TwoFactorEnabled`，已启用 2FA 的 OAuth 用户也直接签发完整 AccessToken。
- **修复建议**：与 `LoginAsync` 一致，OAuth 路径同样检查 2FA 并签发临时令牌。

### 43. `EfCoreUserRepository.UpdateAsync` 注释解释合理但 `Attach` 行为需注意
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCoreUserRepository.cs#L100-L110`
- **类别**：B8 仓储滥用
- **根因**：`if (_context.Entry(user).State == EntityState.Detached) _context.Users.Attach(user);` 注释说"避免对 owned 集合调用 Update 覆盖 Added 状态"。但 `Attach` 后实体状态为 `Unchanged`，对其导航集合的修改不会被检测——除非应用层显式调用 `Entry(user).Reference(...).IsModified = true`。
- **修复建议**：若 User 被显式从外部传入（脱离跟踪），考虑直接报错而非静默 Attach，避免变更丢失。

### 44. `AddressAppService.ClearExistingDefaultAsync` 调用 UpdateAsync 多次（实际无 DB 调用但代码气味）
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AddressAppService.cs#L159-L167`
- **类别**：代码气味
- **根因**：循环内 `await _addressRepository.UpdateAsync(existing, ct);` 实际是 no-op，但 `await` 误导读者以为是 DB 操作。
- **修复建议**：循环外调用一次或注释说明。

### 45. `UserAppService.ForgotPasswordAsync` 重置令牌使用 Guid.NewGuid 而非密码学安全随机
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L439`
- **类别**：A2 异常处理不当
- **根因**：`Guid.NewGuid().ToString("N")` 在 .NET 7+ 内部使用 `RandomNumberGenerator`，但 GUID 结构有版本位与保留位，实际熵 < 122 位。
- **修复建议**：`Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace("+", "-").Replace("/", "_")`。

### 46. `OAuth2 AesKey` 配置缺失时单例不注册，运行期才发现
- **位置**：`file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L88-L93`
- **类别**：A2 异常处理不当
- **根因**：`if (!string.IsNullOrWhiteSpace(aesKey)) services.AddSingleton<IClientSecretEncryptionService>(...)`，配置缺失时容器中无 `IClientSecretEncryptionService`。`OAuthClientAppService` 构造函数 `IClientSecretEncryptionService? encryptionService = null` 默认为 null，调用 `UpdateAsync` 时才抛 `InvalidOperationException`。
- **修复建议**：配置缺失时启动期 fail-fast（throw）。

---

## BC 健康度评分

| 维度 | 评分(0-5) | 说明 |
|------|-----------|------|
| 功能正确性 | 2 | OAuth 邮箱匹配自动绑定（账户接管）、Alipay 未签名（100% 失败）、ForgotPassword 事件丢失、RefreshToken 不校验锁定状态等多处功能正确性问题，部分直接威胁资金与账户安全 |
| DDD 合规 | 3 | 聚合边界与行为方法设计规范，但应用层使用反射绕过聚合封装（问题 3）、应用层穿透 StackExchange.Redis（问题 18）、防腐层缺失（问题 33）等问题违反 DDD 原则 |
| 性能与可靠性 | 2 | InMemoryRefreshTokenStore 注册为生产实现（问题 1）、PermissionRepository 全表加载（问题 19）、FailedLoginCount 并发不安全（问题 14）、令牌撤销链路多处缺失（问题 10/11/24/34），整体可靠性在生产多实例下会崩溃 |
