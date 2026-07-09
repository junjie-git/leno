# 积分与会员域 (Points & Membership) 开发任务

> **限界上下文**: BC7 积分与会员域  
> **技术栈**: ASP.NET Core / EF Core / SQL Server / Redis  
> **依赖**: `shared-kernel`、`order`（订单完成/取消事件）、`payment`（支付成功事件）  
> **对应文档**: `07-积分与会员域.md`

---

## 模块概述

积分与会员域管理用户积分账户与会员等级。积分来源于签到、消费奖励与活动发放，消费时抵现使用。会员等级按累计消费金额自动升降级。积分变更以流水记录，所有操作不可逆，确保审计可追溯。会员订阅订单经订单域创建支付后激活。

---

## Task 1: 项目初始化与领域层 — PointsAccount 聚合

**文件:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Leno.PointsMembership.Domain.csproj`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/PointsAccount.cs`

- [ ] 创建 Leno.PointsMembership.Domain 类库项目，引用 Leno.SharedKernel
- [ ] 实现 `PointsAccount` 聚合根（AccountId、UserId、Balance、FrozenBalance、TotalEarned、TotalSpent、CreatedAt、UpdatedAt、Version）
- [ ] 实现 `PointsAccount.Create` 工厂方法（新用户注册时自动创建，初始余额 0）
- [ ] 实现 `Earn(source, amount, reason)`（积分入账，更新 Balance 与 TotalEarned，附加 `PointsEarnedEvent`）
- [ ] 实现 `Freeze(amount, orderId)`（下单冻结积分，Balance-=amount、FrozenBalance+=amount，附加 `PointsFrozenEvent`）
- [ ] 实现 `ConfirmDeduct(orderId)`（支付成功确认扣减，FrozenBalance-=amount、TotalSpent+=amount，附加 `PointsConfirmedEvent`）
- [ ] 实现 `Release(orderId)`（订单取消释放冻结，FrozenBalance-=amount、Balance+=amount，附加 `PointsReleasedEvent`）
- [ ] 实现不变量：`Balance + FrozenBalance ≥ 0`
- [ ] 编写单元测试覆盖积分生命周期
- [ ] 提交：`feat(points): add PointsAccount aggregate root`

---

## Task 2: 领域层 — PointsLedger 实体与签到记录

**文件:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/PointsLedger.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/CheckInRecord.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/ValueObjects/PointsSource.cs`

- [ ] 实现 `PointsLedger` 实体（LedgerId、AccountId、TxType、Amount、BalanceAfter、Source、ReferenceId、Reason、OccurredAt）
- [ ] 实现 `CheckInRecord` 聚合（RecordId、UserId、CheckInDate、ContinuousDays、PointsAwarded）
- [ ] 实现 `CheckInRecord.CheckIn(userId)`（每日签到，连续签到加倍奖励，校验当日未签）
- [ ] 定义 `PointsSource` 值对象（CheckIn/Consumption/Activity/Refund/Offset）
- [ ] 定义 `TxType`（Earn/Freeze/ConfirmDeduct/Release/Refund）
- [ ] 编写单元测试覆盖签到与流水
- [ ] 提交：`feat(points): add PointsLedger and CheckInRecord`

---

## Task 3: 领域层 — Member 聚合与等级规则

**文件:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/Member.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/MembershipLevel.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/ValueObjects/LevelThreshold.cs`

- [ ] 实现 `Member` 聚合根（MemberId、UserId、CurrentLevel、TotalConsumption、JoinedAt、LevelUpgradedAt、Status、Version）
- [ ] 实现 `Member.Create` 工厂方法（新用户注册时自动创建普通会员）
- [ ] 实现 `Member.AddConsumption(amount)`（累加消费金额，检查是否触发升级）
- [ ] 实现 `Member.CheckUpgrade(thresholds)`（按累计消费匹配等级，升级发布 `MemberLevelUpgradedEvent`）
- [ ] 实现 `MemberLevel` 聚合根（LevelId、Name、MinConsumption、DiscountRate、Status、Version）
- [ ] 实现 `MemberLevel.Create`/`Update`/`Enable`/`Disable` 方法
- [ ] 实现 `LevelThreshold` 值对象（LevelName、MinConsumption）
- [ ] 编写单元测试覆盖升级逻辑
- [ ] 提交：`feat(points): add Member aggregate and membership level rules`

---

## Task 4: 领域层 — 会员订阅包聚合

**文件:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/MembershipPackage.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/UserMembership.cs`

- [ ] 实现 `MembershipPackage` 聚合根（PackageId、Name、Level、Price、DurationDays、Benefits、Status、Version）
- [ ] 实现 `MembershipPackage.Create`/`Update`/`Enable`/`Disable` 方法
- [ ] 实现 `UserMembership` 聚合（UserMembershipId、UserId、PackageId、Level、StartTime、EndTime、Status、OrderId、Version）
- [ ] 实现 `UserMembership.Activate(orderId, startTime)`（支付成功后激活，设 EndTime=StartTime+DurationDays）
- [ ] 实现 `UserMembership.Expire()`（到期失效，后台任务检查）
- [ ] 编写单元测试
- [ ] 提交：`feat(points): add MembershipPackage and UserMembership aggregates`

---

## Task 5: 领域层 — 仓储接口与领域服务

**文件:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Repositories/IPointsAccountRepository.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Repositories/ICheckInRecordRepository.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Repositories/IMemberRepository.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Repositories/IMembershipLevelRepository.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Repositories/IMembershipPackageRepository.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Repositories/IUserMembershipRepository.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Services/IPointsOffsetService.cs`

- [ ] 定义各仓储接口
- [ ] 定义 `IPointsOffsetService` 防腐层接口（TryOffsetAsync 试算积分抵现、FreezeAsync 冻结、ConfirmDeductAsync 确认、ReleaseAsync 释放）
- [ ] 提交：`feat(points): add repository interfaces and points offset service`

---

## Task 6: 领域事件定义

**文件:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsEarnedEvent.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsFrozenEvent.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsConfirmedEvent.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/PointsReleasedEvent.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MemberLevelUpgradedEvent.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Domain/Events/MembershipActivatedEvent.cs`

- [ ] 定义 `PointsEarnedEvent`（accountId、userId、amount、source）— 消费方：通知域
- [ ] 定义 `PointsFrozenEvent`/`PointsConfirmedEvent`/`PointsReleasedEvent`（accountId、userId、amount、orderId）
- [ ] 定义 `MemberLevelUpgradedEvent`（userId、oldLevel、newLevel、upgradedAt）— 消费方：通知域
- [ ] 定义 `MembershipActivatedEvent`（userId、packageId、level、endTime）— 消费方：通知域
- [ ] 提交：`feat(points): add domain integration events`

---

## Task 7: 基础设施层 — EF Core 仓储实现

**文件:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/PointsMembershipDbContext.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCorePointsAccountRepository.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCoreCheckInRecordRepository.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCoreMemberRepository.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCoreMembershipLevelRepository.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCoreMembershipPackageRepository.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Repositories/EfCoreUserMembershipRepository.cs`

- [ ] 实现 `PointsMembershipDbContext`（各 DbSet 配置）
- [ ] 实现各 EF Core 仓储
- [ ] 配置 PointsLedger 为 PointsAccount 的 Owned Collection
- [ ] 创建 EF Core Migration 脚本
- [ ] 编写集成测试验证仓储 CRUD
- [ ] 提交：`feat(points): add EF Core repository implementations`

---

## Task 8: 基础设施层 — 事件消费者

**文件:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/OrderEventConsumer.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/PaymentSucceededEventConsumer.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/UserRegisteredEventConsumer.cs`

- [ ] 实现 `OrderCompletedEventConsumer`（消费奖励：Earn 积分，累加会员消费金额，检查升级）
- [ ] 实现 `OrderCancelledEventConsumer`（释放冻结积分：Release，附 PointsToRelease 字段）
- [ ] 实现 `PaymentSucceededEventConsumer`（确认积分扣减：ConfirmDeduct；会员订阅订单激活 UserMembership）
- [ ] 实现 `UserRegisteredEventConsumer`（自动创建 PointsAccount 和 Member 聚合）
- [ ] 幂等消费以 EventId 去重
- [ ] 编写集成测试
- [ ] 提交：`feat(points): add event consumers for order and payment events`

---

## Task 9: 应用层 — 积分管理用例

**文件:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application/IPointsAppService.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsAppService.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsOffsetAppService.cs`

- [ ] 实现 `CheckInAsync`（每日签到，连续签到加倍奖励）
- [ ] 实现 `GetPointsAccountAsync`（查询积分余额与流水）
- [ ] 实现 `AwardPointsAsync`（运营手动发放积分，附原因）
- [ ] 实现 `PointsOffsetAppService`（防腐层实现，供订单域调用：试算、冻结、确认、释放）
- [ ] 编写单元测试
- [ ] 提交：`feat(points): add points application services`

---

## Task 10: 应用层 — 会员管理用例

**文件:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application/IMemberAppService.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application/IMembershipPackageAppService.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/MemberAppService.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/MembershipPackageAppService.cs`

- [ ] 实现 `GetMemberInfoAsync`（查询当前会员等级、权益、消费进度）
- [ ] 实现会员等级配置用例（运营 CRUD 等级阈值）
- [ ] 实现会员订阅包管理用例（运营 CRUD 订阅包）
- [ ] 实现会员订阅购买用例（创建订阅订单，转发至订单域）
- [ ] 编写单元测试
- [ ] 提交：`feat(points): add member application services`

---

## Task 11: 表现层 — API 控制器

**文件:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/PointsController.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembersController.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/MembershipPackagesController.cs`

- [ ] 实现 `PointsController`（POST /api/points/check-in、GET /api/points/account、GET /api/points/ledger）
- [ ] 实现运营接口（POST /api/admin/points/award）
- [ ] 实现 `MembersController`（GET /api/members/me、GET /api/admin/members/levels）
- [ ] 实现 `MembershipPackagesController`（GET /api/membership-packages、POST /api/membership-packages/{id}/subscribe）
- [ ] 实现运营接口（POST/PUT /api/admin/membership-packages、POST/PUT /api/admin/members/levels）
- [ ] 配置 JWT 鉴权与角色策略
- [ ] 编写 API 集成测试
- [ ] 提交：`feat(points): add API controllers`
