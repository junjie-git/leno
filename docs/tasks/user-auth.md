# 用户与认证授权域 (User & Auth) 开发任务

> **限界上下文**: BC1 用户与认证授权域  
> **技术栈**: ASP.NET Core / EF Core / SQL Server / Redis / JWT  
> **依赖**: `shared-kernel`  
> **对应文档**: `01-用户与认证授权域.md`

---

## 模块概述

用户与认证授权域是平台身份核心，权威持有用户账户、角色、权限与收货地址。提供注册、登录、JWT 鉴权、密码重置、地址管理与账户审计能力。所有角色端登录依赖本域。

---

## Task 1: 项目初始化与领域层 — User 聚合

**文件:**
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Leno.UserAuth.Domain.csproj`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/UserRole.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/ValueObjects/UserId.cs`

- [ ] 创建 Leno.UserAuth.Domain 类库项目，引用 Leno.SharedKernel
- [ ] 实现 `User` 聚合根（UserId、Username、Email、Phone、PasswordHash、Status、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `User.Create` 工厂方法（校验用户名/邮箱/手机号格式，生成聚合，附加 `UserRegisteredEvent`）
- [ ] 实现 `User.Activate`/`User.Suspend`/`User.Close` 状态流转方法
- [ ] 实现 `User.ChangePassword` 方法（校验旧密码，设置新密码哈希）
- [ ] 实现 `User.AssignRole`/`User.RemoveRole` 方法
- [ ] 定义 `UserStatus` 值对象（枚举：Active/Suspended/Closed）
- [ ] 编写单元测试覆盖工厂方法与状态流转
- [ ] 提交：`feat(user-auth): add User aggregate root`

---

## Task 2: 领域层 — Address 聚合与值对象

**文件:**
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/Address.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/ValueObjects/AddressDetail.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/ValueObjects/RoleType.cs`

- [ ] 实现 `Address` 聚合根（AddressId、UserId、RecipientName、Phone、Province、City、District、Detail、IsDefault）
- [ ] 实现 `Address.Create` 工厂方法（校验手机号 E.164 格式、详细地址 5-200 字符）
- [ ] 实现 `Address.SetDefault` 方法（同一用户仅一个默认地址）
- [ ] 实现 `Address.Update` 方法
- [ ] 定义 `RoleType` 枚举值对象（Buyer/Seller/Operator/SystemAdmin）
- [ ] 编写单元测试覆盖地址校验与默认地址切换
- [ ] 提交：`feat(user-auth): add Address aggregate and role types`

---

## Task 3: 领域层 — 领域服务与仓储接口

**文件:**
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Services/IPasswordHasher.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Services/IUserUniquenessChecker.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Repositories/IUserRepository.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Repositories/IAddressRepository.cs`

- [ ] 定义 `IPasswordHasher` 接口（Hash、Verify，实现 BCrypt）
- [ ] 定义 `IUserUniquenessChecker` 接口（IsUsernameUniqueAsync、IsEmailUniqueAsync、IsPhoneUniqueAsync）
- [ ] 定义 `IUserRepository` 接口（GetByIdAsync、GetByUsernameAsync、GetByEmailAsync、QueryAsync、AddAsync、UpdateAsync）
- [ ] 定义 `IAddressRepository` 接口（GetByIdAsync、GetByUserIdAsync、AddAsync、UpdateAsync、DeleteAsync）
- [ ] 编写单元测试验证接口契约
- [ ] 提交：`feat(user-auth): add domain services and repository interfaces`

---

## Task 4: 领域事件定义

**文件:**
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Events/UserRegisteredEvent.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Events/UserPasswordChangedEvent.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Events/UserRoleAssignedEvent.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Events/UserSuspendedEvent.cs`

- [ ] 定义 `UserRegisteredEvent`（userId、username、email、registeredAt）— 消费方：消息通知域
- [ ] 定义 `UserPasswordChangedEvent`（userId、changedAt）
- [ ] 定义 `UserRoleAssignedEvent`（userId、role、assignedAt）
- [ ] 定义 `UserSuspendedEvent`（userId、reason、suspendedAt）
- [ ] 确保事件均继承 `IntegrationEventBase`，携带 IdempotencyKey
- [ ] 提交：`feat(user-auth): add domain integration events`

---

## Task 5: 基础设施层 — EF Core 仓储实现

**文件:**
- Create: `src/Services/UserAuth/Leno.UserAuth.Infrastructure/UserAuthDbContext.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCoreUserRepository.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCoreAddressRepository.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/AddressConfiguration.cs`

- [ ] 实现 `UserAuthDbContext`（DbSet<User>、DbSet<Address>，引用 BaseDbContext）
- [ ] 配置 `UserConfiguration`（表映射、唯一索引 Username/Email/Phone、PasswordHash 不返回）
- [ ] 配置 `AddressConfiguration`（表映射、UserId 外键索引、IsDefault 字段）
- [ ] 实现 `EfCoreUserRepository`（含 QueryAsync 分页与多条件过滤）
- [ ] 实现 `EfCoreAddressRepository`
- [ ] 创建 EF Core Migration 脚本
- [ ] 编写集成测试验证仓储 CRUD
- [ ] 提交：`feat(user-auth): add EF Core repository implementations`

---

## Task 6: 基础设施层 — 密码哈希与用户唯一性校验

**文件:**
- Create: `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/BcryptPasswordHasher.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/UserUniquenessChecker.cs`

- [ ] 实现 `BcryptPasswordHasher`（BCrypt 哈希与验证，cost factor 可配置）
- [ ] 实现 `UserUniquenessChecker`（查询数据库校验唯一性，支持排除自身 ID）
- [ ] 编写单元测试覆盖密码哈希与唯一性校验
- [ ] 提交：`feat(user-auth): add password hasher and uniqueness checker`

---

## Task 7: 应用层 — 用户注册与登录用例

**文件:**
- Create: `src/Services/UserAuth/Leno.UserAuth.Application/IUserAppService.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Application/DTOs/RegisterDto.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Application/DTOs/LoginDto.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Application/DTOs/UserDto.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs`

- [ ] 定义 `IUserAppService` 接口（RegisterAsync、LoginAsync、GetProfileAsync、ChangePasswordAsync）
- [ ] 实现 `RegisterAsync`（校验唯一性→哈希密码→创建聚合→保存→发件箱发布事件）
- [ ] 实现 `LoginAsync`（查询用户→验证密码→生成 JWT→返回 Token 与用户信息）
- [ ] 实现 `GetProfileAsync`（返回当前用户信息 DTO，敏感字段不返回）
- [ ] 实现 `ChangePasswordAsync`（校验旧密码→哈希新密码→更新→发布事件）
- [ ] 编写 DTO 与输入校验（FluentValidation）
- [ ] 编写单元测试覆盖注册与登录用例
- [ ] 提交：`feat(user-auth): add user registration and login application services`

---

## Task 8: 应用层 — 地址管理用例

**文件:**
- Create: `src/Services/UserAuth/Leno.UserAuth.Application/IAddressAppService.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Application/DTOs/AddressDto.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Application/Services/AddressAppService.cs`

- [ ] 定义 `IAddressAppService` 接口（CreateAsync、UpdateAsync、DeleteAsync、ListAsync、SetDefaultAsync）
- [ ] 实现地址 CRUD 用例（校验归属 UserId 与 JWT 一致）
- [ ] 实现 `SetDefaultAsync`（取消旧默认→设置新默认，同事务）
- [ ] 编写单元测试覆盖地址管理
- [ ] 提交：`feat(user-auth): add address management application service`

---

## Task 9: 表现层 — API 控制器

**文件:**
- Create: `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/UsersController.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AddressesController.cs`

- [ ] 实现 `AuthController`（POST /api/auth/register、POST /api/auth/login、POST /api/auth/refresh-token）
- [ ] 实现 `UsersController`（GET /api/users/me、PUT /api/users/me、PUT /api/users/me/password）
- [ ] 实现 `AddressesController`（GET/POST/PUT/DELETE /api/users/me/addresses，POST .../{id}/default）
- [ ] 配置 JWT 鉴权中间件与角色策略
- [ ] 配置 `Idempotency-Key` 头处理（注册接口强制）
- [ ] 编写 API 集成测试覆盖注册→登录→获取信息→地址管理全流程
- [ ] 提交：`feat(user-auth): add API controllers for auth, users and addresses`

---

## Task 10: 用户管理后台与审计日志

**文件:**
- Create: `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminUsersController.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Infrastructure/Audit/AuditLogInterceptor.cs`
- Create: `src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/AuditLog.cs`

- [ ] 实现 `AuditLog` 聚合（LogId、OperatorId、Action、ResourceType、ResourceId、RequestSummary、ResponseStatus、IpAddress、TraceId、OccurredAt）
- [ ] 实现 `AuditLogInterceptor`（审计中间件，自动拦截写操作记录审计日志，事务内写入）
- [ ] 实现 `AdminUsersController`（GET /api/admin/users 分页查询、POST /api/admin/users/{id}/suspend、POST /api/admin/users/{id}/resume）
- [ ] 审计日志查询收口至 BC11 系统管理域 F-SYS-009，本域只写入
- [ ] 编写集成测试验证审计日志写入
- [ ] 提交：`feat(user-auth): add admin user management and audit log`
