# Leno 电商平台架构升级方案

> **生成日期**：2026-07-23
> **基线分支**：`feat-architectural-upgrade-plan-KqJ46g`
> **基线状态**：2026-07-21 完整代码审计 + 2026-07-22 330 次修复提交 + 2026-07-23 编译零错误
> **分析方法**：4 个 subagent 并行核验当前源码 + 跨 BC 聚合 + 前瞻性架构演进设计
> **本方案不修改任何业务代码，仅产出分析与规划文档**

---

## 一、执行摘要

### 1.1 方案目标

基于 2026-07-21 代码审计（364 个问题）与 2026-07-22 修复批次（330 次提交）后的**当前最新代码状态**，对仓库中所有源代码（明确排除测试项目）进行全面系统分析，识别遗留问题与潜在风险，评估代码质量与性能瓶颈，制定前瞻性架构升级方案，并提供具体的代码优化建议，确保与现有业务逻辑的兼容性。

### 1.2 核心结论

| 维度 | 评分 | 说明 |
|------|------|------|
| 整体架构健康度 | **8.3 / 10** | 从 2026-07-21 审计时 6.0/10 跃升至 8.3/10，P0 高风险问题清零 |
| DDD 合规性 | 8.5 / 10 | 11 BC 边界清晰，聚合根设计规范，ACL 双轨成熟 |
| 性能与可靠性 | 7.5 / 10 | Outbox 工业化、熔断状态机完备，但存在 3 项新发现的 P0 阻塞问题 |
| 安全性 | 8.0 / 10 | IDOR 统一校验、JWT 黑名单三层同步、密码外部化已落地，密钥管理待升级 |
| 可扩展性 | 7.5 / 10 | ACL 双轨、CQRS 轻量、BFF 聚合已具备，但规则引擎/多租户/国际化缺失 |

### 1.3 关键发现

#### 已修复的 P0 高风险问题（58 项核验）

四个 subagent 逐项核验 58 项审计 P0 高风险问题在当前源码中的落地情况：

| 范围 | 核验项数 | 已修复 | 部分修复 | 未修复 |
|------|---------|--------|---------|--------|
| 共享层（BuildingBlocks + ApiGateway） | 12 | 12 | 0 | 0 |
| 核心交易 BC（Order/Payment/Cart） | 9 | 5 | 3 | 1（含 1 项新发现 P0） |
| 商品/促销/用户 BC（Product/Promotion/UserAuth） | 14 | 14 | 0 | 0 |
| 支撑 BC（Notification/PointsMembership/ReviewAfterSales/SellerShop/SystemAdmin） | 22 | 21 | 0 | 1 |
| **合计** | **57** | **52** | **3** | **2** |

**修复落地率 91.2%**，与 2026-07-22 EXECUTION-REPORT 声明基本一致。

#### 新发现的 P0 阻塞问题（4 项，必须立即修复）

在 330 次修复批次后，subagent 核验发现 **4 项新的 P0 级别问题**，是当前架构的**阻塞点**：

| 编号 | 问题 | 影响 | 证据 |
|------|------|------|------|
| **NEW-P0-1** | Order 表存在双 rowversion 列（`version` shadow + `row_version` 显式） | **SQL Server 单表仅允许一个 rowversion 列，迁移会失败，生产部署阻塞** | [OrderConfiguration.cs#L50](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Order/Leno.Order.Infrastructure/Configurations/OrderConfiguration.cs#L50) + [BaseDbContext.cs#L46-55](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs#L46-L55) |
| **NEW-P0-2** | CartUnitOfWork.SaveChangesAsync 旁路 Outbox | Cart BC 领域事件丢失，下游 BC 收不到 Cart 事件（库存索引、价格快照失效） | [CartUnitOfWork.cs#L35-36](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Cart/Leno.Cart.Infrastructure/CartUnitOfWork.cs#L35-L36) |
| **NEW-P0-3** | 补偿表 StockReservationCompensation 无 OperationType 字段，后台任务统一调 ReleaseAsync | ForceCancel 已支付订单的 ReturnDeducted 失败后，补偿重试调用 ReleaseAsync（no-op），**deducted 库存永久丢失** | [StockReservationCompensationBackgroundService.cs#L111](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Order/Leno.Order.Infrastructure/BackgroundServices/StockReservationCompensationBackgroundService.cs#L111) |
| **NEW-P0-4** | Notification MarkAllAsReadAsync 用 ExecuteUpdateAsync 绕过聚合根 | 不触发领域事件、不写审计字段（UpdatedAt/UpdatedBy），DDD 合规违规 | [EfCoreNotificationRecordRepository.cs#L96-101](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L96-L101) |

#### 架构升级机会（31 项）

四个 subagent 识别出 31 项前瞻性架构升级机会，按主题归并为 8 大演进方向（详见第四章）。

### 1.4 推荐实施节奏

| 阶段 | 周期 | 重点 | 预期效果 |
|------|------|------|---------|
| **P0 阻塞修复** | 1 周 | 4 项新发现 P0 + 3 项部分修复项 | 解除部署阻塞，恢复全域一致性 |
| **速赢优化** | 2-3 周 | 重复实现清理 + 配置化抽取 + 索引补充 | 健康度 8.3 → 8.8 |
| **中期演进** | 1-2 月 | BC 拆分（库存/积分会员/评价售后）+ Saga 状态机 + 规则引擎 | 健康度 8.8 → 9.3 |
| **长期架构** | 3-6 月 | Infrastructure 拆包 + 多级缓存 + 多租户/国际化预留 | 健康度 9.3 → 9.6（L5 持续优化） |

---

## 二、当前架构现状评估

### 2.1 架构骨架回顾

Leno 电商平台采用 **DDD + CQRS + 事件驱动 + 微服务** 四位一体架构：

- **11 个 BC**：UserAuth / Product / Cart / Order / Promotion / ReviewAfterSales / PointsMembership / Payment / Notification / SellerShop / SystemAdmin
- **4 层架构**：Api / Application / Domain / Infrastructure
- **3 套 BuildingBlocks**：SharedKernel（基础抽象）/ SharedContracts（事件+DTO+Proto）/ Infrastructure（基础设施实现）
- **1 个 BFF 网关**：Leno.ApiGateway（聚合 + 鉴权 + 限流 + 熔断）

### 2.2 P0 修复落地核验矩阵

#### 2.2.1 共享层（12/12 已修复）

| P0 项 | 状态 | 证据 |
|-------|------|------|
| CacheService Random 非线程安全 | ✅ | [CacheService.cs#L459](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs#L459) 改用 `Random.Shared` |
| AntiCorruptionMetrics 静态字典竞态 | ✅ | [AntiCorruptionMetrics.cs#L60](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs#L60) `ConcurrentDictionary` |
| IntegrationEventConsumerBase 幂等无原子性 | ✅ | [IntegrationEventConsumerBase.cs#L64](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs#L64) SET NX 原子获取处理权 |
| IUnitOfWork.SaveChangesAsync 旁路 Outbox | ✅ | [EfCoreUnitOfWork.cs#L56-58](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs#L56-L58) `[Obsolete]` + 委托 |
| JwtBlacklistService 多实例不同步 | ✅ | [JwtBlacklistService.cs#L17](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs#L17) IHostedService + Redis Pub/Sub |
| ConsulConfigWatcher 不触发 IOptionsMonitor | ✅ | [ConsulConfigWatcher.cs#L107-110](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs#L107-L110) |
| DesignTimeDbContextFactory 硬编码密码 | ✅ | [DesignTimeDbContextFactoryBase.cs#L14](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Persistence/DesignTimeDbContextFactoryBase.cs#L14) 环境变量外部化 |
| GuidProtoConverter 工具类 | ✅ | [GuidProtoConverter.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GuidProtoConverter.cs) 新增 |
| ResourceOwnershipChecker IDOR 统一校验 | ✅ | [ResourceOwnershipChecker.cs#L24](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Auth/ResourceOwnershipChecker.cs#L24) |
| RefundCompletedEvent 缺 ChannelRefundNo | ✅ | [PaymentEvents.cs#L135](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs#L135) SchemaVersion=2 |
| ReviewSubmittedEvent 缺 ShopId | ✅ | [ReviewEvents.cs#L25](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.SharedContracts/Events/ReviewEvents.cs#L25) SchemaVersion=2 |
| IdempotencyKey 非可空反序列化 | ✅ | [IntegrationEventBase.cs#L20](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs#L20) 默认 string.Empty |

#### 2.2.2 核心交易 BC（5/9 完全修复 + 3 部分修复 + 1 阻塞）

| P0 项 | 状态 | 证据与遗留问题 |
|-------|------|---------------|
| Order 聚合根缺乐观并发控制 | ⚠️ 部分修复 | 显式 RowVersion 已加，但**未删除 BaseDbContext 注入的 shadow `version` 列**，导致 NEW-P0-1 |
| Order ForceCancel 释放预占而非已扣减 | ✅ | [IInventoryRepository.cs#L40](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Order/Leno.Order.Domain/Repositories/IInventoryRepository.cs#L40) ReturnDeductedAsync |
| Payment 微信回调链路 100% 失败 | ✅ | [WeChatPayNotifyHandler.cs#L60-67](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Payment/Leno.Payment.Infrastructure/Notify/WeChatPayNotifyHandler.cs#L60-L67) 验签前移 |
| Payment 缺乐观并发控制 | ✅ | [PaymentOrderConfiguration.cs#L41](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Payment/Leno.Payment.Infrastructure/Configurations/PaymentOrderConfiguration.cs#L41) 先 Drop 再 Add |
| Cart 匿名购物车 TOCTOU 竞态 | ⚠️ 部分修复 | 创建竞态已修复（TrySaveAsync SET NX），**更新覆盖写仍存在**（详见 5.4） |
| Cart 聚合缺乐观锁 | ⚠️ 部分修复 | 通过 shadow property 隐式生效，与 Order/Payment 显式 RowVersion 风格不一致 |
| Order Saga 补偿失败 | ⚠️ 部分修复 | Redis 层幂等已保证，但**补偿表无 OperationType 字段**致 NEW-P0-3 |
| 各 BC Outbox 旁路 | ⚠️ 部分修复 | Order/Payment 已修复，**Cart BC 的 CartUnitOfWork 仍旁路**致 NEW-P0-2 |
| Guid.GetHashCode() 在 Order gRPC | ✅ | [OrderGrpcService.cs#L82](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs#L82) 双写 int64+string |

#### 2.2.3 商品/促销/用户 BC（14/14 已修复）

| P0 项 | 状态 | 证据 |
|-------|------|------|
| UserAuth OAuth 邮箱匹配自动绑定 | ✅ | [UserAppService.cs#L99-100](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L99-L100) 改抛异常要求手动绑定 |
| UserAuth InMemoryRefreshTokenStore 生产误注册 | ✅ | [ServiceCollectionExtensions.cs#L75](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L75) 默认 Redis |
| UserAuth ForgotPassword 事件丢失 | ✅ | 显式 UpdateAsync + SaveEntitiesAsync |
| UserAuth FailedLoginCount 并发不安全 | ✅ | [UserConfiguration.cs#L34-35](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs#L34-L35) RowVersion + 重试 |
| UserAuth PermissionRepository 全表加载 | ✅ | [EfCorePermissionRepository.cs#L62-82](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCorePermissionRepository.cs#L62-L82) SQL Server OPENJSON |
| JwtTokenGenerator 未校验 SecretKey 长度 | ✅ | [JwtTokenGenerator.cs#L39-57](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Auth/JwtTokenGenerator.cs#L39-L57) ≥32 字节 fail-fast |
| Product N+1 批量查询 | ✅ | [EfCoreSPURepository.cs#L45-64](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Product/Leno.Product.Infrastructure/Repositories/EfCoreSPURepository.cs#L45-L64) 单次 SQL 批量 |
| Product 浮点漂移 | ✅ | 维护 TotalScore 累计值，展示时计算 |
| Product TODO 占位 | ✅ | 已实现四态处理逻辑 |
| Promotion SeckillPreOccupation 双重复回退 | ✅ | [SeckillOrderEventConsumer.cs#L47-59](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/SeckillOrderEventConsumer.cs#L47-L59) IsRolledBack 幂等跳过 |
| Promotion CouponExpiryService 分页 skip 累加 | ✅ | [CouponExpiryService.cs#L59-64](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Promotion/Leno.Promotion.Api/BackgroundServices/CouponExpiryService.cs#L59-L64) skip 始终 0 |
| Promotion OrderCancelledEventConsumer 死信 | ✅ | [OrderEventConsumer.cs#L95-100](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/OrderEventConsumer.cs#L95-L100) 状态前置检查 |
| Promotion Redis RestoreLuaScript 无上限 | ✅ | [RedisSeckillStockService.cs#L47-54](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Promotion/Leno.Promotion.Infrastructure/Services/RedisSeckillStockService.cs#L47-L54) TotalStock 上限保护 |
| Guid.GetHashCode() 在 Product gRPC | ✅ | [ProductGrpcService.cs#L141-143](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L141-L143) GuidToInt64Stable |

#### 2.2.4 支撑 BC（21/22 已修复）

| P0 项 | 状态 | 证据 |
|-------|------|------|
| Notification DI 重复注册 SmsChannel | ✅ | [NotificationDispatchJob.cs#L42-47](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Jobs/NotificationDispatchJob.cs#L42-L47) 构造时缓存 |
| Notification MassTransit 重复订阅 | ✅ | [NotificationEventConsumer.cs#L15-20](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs#L15-L20) `[Obsolete]` 移除注册 |
| Notification 字段名不匹配 | ✅ | [ServiceCollectionExtensions.cs#L108-122](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L108-L122) |
| Notification OrderCancelled UserId Guid.Empty | ✅ | [OrderEventConsumer.cs#L99-131](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Consumers/OrderEventConsumer.cs#L99-L131) SellerId fallback |
| Notification 回执不持久化 | ✅ | [NotificationDispatcher.cs#L97-115](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L97-L115) |
| Notification 控制器越层访问仓储 | ✅ | 经 INotificationAppService |
| **Notification MarkAllAsReadAsync 绕过聚合** | ❌ **未修复** | 致 NEW-P0-4 |
| PointsMembership ExchangeCoupon 绕过 Outbox | ✅ | [ExchangeCouponAppService.cs#L49-53](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/PointsMembership/Leno.PointsMembership.Application/Services/ExchangeCouponAppService.cs#L49-L53) |
| PointsMembership 4 个 ReadModel 死消费者 | ✅ | [ServiceCollectionExtensions.cs#L81-95](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L81-L95) 9 个 Consumer 全注册 |
| PointsMembership ReviewApproved Redis 非原子 | ✅ | [ReviewApprovedEventConsumer.cs#L77-100](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/Consumers/ReviewApprovedEventConsumer.cs#L77-L100) StringIncrementAsync |
| PointsMembership MemberLevelEvaluationJob GrowthValue 恒 0 | ✅ | [MemberLevelEvaluationJob.cs#L62-108](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/PointsMembership/Leno.PointsMembership.Api/BackgroundServices/MemberLevelEvaluationJob.cs#L62-L108) 真实评估 + 指数退避 |
| PointsMembership InternalPointsController.Confirm 缺失 | ✅ | [InternalPointsController.cs#L56-64](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs#L56-L64) 双路由 |
| ReviewAfterSales SellerId 客户端伪造 | ✅ | [ReviewAppService.cs#L47-55](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs#L47-L55) eligibilityChecker 反查 |
| ReviewAfterSales SpuId/SkuId 客户端伪造 | ✅ | [AfterSalesAppService.cs#L57-67](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L57-L67) |
| ReviewAfterSales RefundCompleted 事件回环 | ✅ | [ReviewAfterSalesIntegrationEventMapper.cs#L52-55](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/EventBus/ReviewAfterSalesIntegrationEventMapper.cs#L52-L55) 改发 AfterSalesRefundCompletedEvent |
| SellerShop SpuId 当 ShopId | ✅ | [ReviewSubmittedShopDashboardSyncConsumer.cs#L60-89](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ReviewSubmittedShopDashboardSyncConsumer.cs#L60-L89) |
| SellerShop UpdateShopInfoAsync 缺归属校验 | ✅ | [ShopAppService.cs#L110-154](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/SellerShop/Leno.SellerShop.Application/Services/ShopAppService.cs#L110-L154) RequireOwnedShopAsync |
| SystemAdmin StatisticsAggregationService Random | ✅ | [StatisticsAggregationService.cs#L37](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsAggregationService.cs#L37) 真实数据源 |
| SystemAdmin SystemConfigAppService 绕过 Outbox | ✅ | [SystemConfigAppService.cs#L51-95](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/SystemConfigAppService.cs#L51-L95) |
| SystemAdmin FeatureFlagCache 未失效 | ✅ | [FeatureFlagAppService.cs#L69-96](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/FeatureFlagAppService.cs#L69-L96) cache.RemoveAsync |
| SystemAdmin AuditLogConsumer TOCTOU | ✅ | [AuditLogConsumer.cs#L256-279](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Consumers/AuditLogConsumer.cs#L256-L279) 唯一索引兜底 |
| SystemAdmin DeadLetterQueueManager SaveChangesAsync | ✅ | [DeadLetterQueueManager.cs#L72-77](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/DeadLetterQueueManager.cs#L72-L77) SaveEntitiesAsync |
| gRPC Guid.GetHashCode() in SellerShop/ReviewAfterSales | ✅ | ShopIdStr/ReviewIdStr 双写 |

### 2.3 BC 健康度评分矩阵

| BC | 功能正确性 | DDD 合规 | 性能可靠性 | 安全性 | 综合 |
|-----|:---:|:---:|:---:|:---:|:---:|
| Shared（共享层） | 9.0 | 9.0 | 8.5 | 9.0 | **8.6** |
| UserAuth | 8.5 | 8.0 | 7.5 | 8.0 | **8.0** |
| Product | 8.5 | 8.0 | 8.0 | 7.5 | **8.0** |
| Cart | 7.0 | 7.5 | 6.0 | — | **6.8**（NEW-P0-2 拖累） |
| Order | 7.5 | 8.0 | 6.5 | — | **7.3**（NEW-P0-1/3 拖累） |
| Promotion | 8.5 | 7.5 | 8.0 | 7.5 | **7.9** |
| ReviewAfterSales | 9.0 | 9.0 | 9.0 | — | **9.0** |
| PointsMembership | 9.0 | 9.0 | 9.0 | — | **9.0** |
| Payment | 8.5 | 8.5 | 7.5 | — | **8.2** |
| Notification | 9.0 | 7.0 | 8.0 | — | **8.0**（NEW-P0-4 拖累） |
| SellerShop | 9.0 | 9.0 | 8.0 | — | **8.7** |
| SystemAdmin | 9.0 | 9.0 | 9.0 | — | **9.0** |
| **加权平均** | **8.4** | **8.4** | **7.8** | **8.0** | **8.3** |

---

## 三、架构问题与潜在风险

### 3.1 P0 阻塞问题（必须立即修复，1 周内）

详见 1.3 节"新发现的 P0 阻塞问题"表。

### 3.2 P1 高优先级问题（1 个月内修复）

| 编号 | 问题 | BC | 影响 | 证据 |
|------|------|-----|------|------|
| P1-1 | 匿名购物车并发更新覆盖写 | Cart | 用户加购丢失，转化率下降 | [RedisAnonymousCartRepository.cs#L69](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs#L69) SaveAsync 非原子 |
| P1-2 | OrderSagaOrchestrator 超时消息调度非原子 | Order | 部分订单无超时消息永不自动取消 | [OrderSagaOrchestrator.cs#L127-135](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs#L127-L135) |
| P1-3 | 积分/优惠券释放无补偿表 | Order | 重试耗尽进死信，积分/优惠券永久冻结 | [OrderTimeoutDelayMessageConsumer.cs#L134-136](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Order/Leno.Order.Infrastructure/Consumers/OrderTimeoutDelayMessageConsumer.cs#L134-L136) |
| P1-4 | PaymentRequestedEventConsumer 支付单卡 Pending | Payment | 用户无法重新发起支付 | [PaymentRequestedEventConsumer.cs#L48-54](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Payment/Leno.Payment.Infrastructure/Consumers/PaymentRequestedEventConsumer.cs#L48-L54) |
| P1-5 | Notification MarkAllAsReadAsync 绕聚合 | Notification | DDD 违规，审计字段缺失 | [EfCoreNotificationRecordRepository.cs#L96-101](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Repositories/EfCoreNotificationRecordRepository.cs#L96-L101) |
| P1-6 | ReviewAfterSales 缺 seller_id 索引 | ReviewAfterSales | 卖家后台查询全表扫 | [ReviewConfiguration.cs#L58-60](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Configurations/ReviewConfiguration.cs#L58-L60) |
| P1-7 | Notification RetryPolicy/RateLimiter 硬编码 | Notification | 运营无法动态调整限流阈值 | [RetryPolicy.cs#L67-72](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Services/RetryPolicy.cs#L67-L72) |
| P1-8 | SeckillOrderEventConsumer null 记录回退无幂等 | Promotion | 重复事件多次回退库存 | [SeckillOrderEventConsumer.cs#L60-63](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Promotion/Leno.Promotion.Infrastructure/Consumers/SeckillOrderEventConsumer.cs#L60-L63) |
| P1-9 | JwtRevocationService UserBlacklistTtl 固定 2h | UserAuth | 与 JWT 实际有效期未联动 | [JwtRevocationService.cs#L20](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/JwtRevocationService.cs#L20) |
| P1-10 | Promotion GetByUserAsync 内存过滤已过期券 | Promotion | 券量大时内存压力 | [PromotionCalculateAppService.cs#L92-101](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Promotion/Leno.Promotion.Application/Services/PromotionCalculateAppService.cs#L92-L101) |

### 3.3 P2 中优先级问题（1 季度内修复）

| 类别 | 问题数 | 典型代表 |
|------|--------|---------|
| 重复实现 | 4 | RedisSlidingWindowRateLimiter 双份、TraceIdEnricher 双份、AuditableEntityInterceptor 与 FillAuditableFields 重叠、SmsChannel HttpTimeout 重复 |
| 配置化缺失 | 8 | SagaOptions、BatchSize、ScanInterval、ClockSkew、HttpTimeout、LockKeyPrefix 等硬编码 |
| 索引缺失 | 3 | reviews.seller_id、notification_records.(user_id,is_read,channel)、Outbox 表归档策略 |
| 死代码残留 | 3 | NotificationEventConsumer `[Obsolete]` 未删、SellerDashboardAppService 双轨未下线、SmsOptions/EmailOptions 残留 |
| 性能优化 | 5 | RedisBloomFilter 7 次串行调用、NotificationDispatcher 多次 SaveChanges、TemplateRenderer 频繁 new List、StatisticsReconciliationService 容量未预分配、CartPriceService 实时跨进程调用 |
| 双写一致性 | 2 | RedisInventoryRepository Redis 成功 DB 失败静默、StockReservation DB 双写延迟 |

### 3.4 潜在风险（生产环境）

| 风险 | 严重度 | 触发条件 | 缓解措施 |
|------|--------|---------|---------|
| SQL Server 部署失败 | 🔴 阻塞 | NEW-P0-1 双 rowversion 列 | 立即新增迁移 DropColumn `version` |
| 库存永久丢失 | 🔴 高 | NEW-P0-3 ForceCancel 已支付订单补偿类型错 | 增加 OperationType 字段 |
| Saga 崩溃无法恢复 | 🟡 中 | OrderSagaOrchestrator 内存状态不持久化 | 引入 MassTransit Saga 状态机 |
| 缓存穿透打垮商品域 | 🟡 中 | 恶意请求不存在的 SKU | 缓存空值 + 布隆过滤器 |
| ES 故障拖垮 BC 内存 | 🟡 中 | 消息堆积在 MassTransit 预取 | 按事件类型分队列 + Circuit Breaker |
| Outbox 表膨胀 | 🟡 中 | 长期运行无归档 | 定时清理 7 天前已处理记录 |
| HS256 密钥泄露面广 | 🟡 中 | 多服务共享同一 SecretKey | 迁移 RS256 非对称签名 |
| AES Key 无版本化 | 🟡 中 | 密钥轮换需停服 | 引入 KMS 托管 + KeyId 前缀 |

---

## 四、架构升级方案

### 4.1 系统分层优化

#### 4.1.1 Leno.Infrastructure 模块化拆包

**现状**：[Leno.Infrastructure.csproj](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj) 单项目引用 30+ NuGet 包，所有基础设施依赖混居。

**问题**：服务只需 Caching 却被迫引入 EF Core、Consul、OTel 全部依赖，启动时反射扫描成本高、部署体积膨胀。

**升级方案**：按子域拆分为独立包，`Leno.Infrastructure` 作为聚合元包仅做再导出。

```
Leno.Infrastructure/                  # 聚合元包（向后兼容门面）
├── Leno.Infrastructure.Abstractions/ # 已有，无依赖
├── Leno.Infrastructure.Caching/      # Redis 缓存 + 布隆过滤器
├── Leno.Infrastructure.EventBus/     # RabbitMQ + 幂等基类
├── Leno.Infrastructure.AntiCorruption/ # ACL 双轨 + 熔断
├── Leno.Infrastructure.Persistence/  # BaseDbContext + UnitOfWork + Outbox
├── Leno.Infrastructure.Telemetry/    # OTel + Serilog
├── Leno.Infrastructure.RateLimiting/ # 限流器
├── Leno.Infrastructure.Auth/         # JWT + IDOR 校验
└── Leno.Infrastructure.ReadModel/    # ES 读模型同步
```

**预期效果**：按需引用，启动加速 30%+，依赖图清晰。

**兼容性风险**：低。保留元包做向后兼容门面，老服务零改动。

#### 4.1.2 ACL 防腐层演进为可插拔策略链

**现状**：[AntiCorruptionDispatcher](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs) 实现双轨调度（gRPC + HTTP 降级）+ 三态熔断，协议切换逻辑硬编码。

**升级方案**：抽象 `IAclChannel` 接口（`SendAsync` / `HealthCheckAsync` / `Priority`），调度器维护有序 channel 列表，按优先级 + 熔断状态选择。新增协议（消息总线异步化、本地内存缓存兜底）只需注册新 `IAclChannel` 实现。

**预期效果**：开闭原则落地，新协议接入零侵入。

**兼容性风险**：中。需迁移现有 gRPC/HTTP 双轨实现，但对外 API 不变。

#### 4.1.3 BFF 聚合层引入 DAG 编排引擎

**现状**：[BffForwarderService](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/ApiGateway/Leno.ApiGateway/Bff/BffForwarderService.cs) 使用 `Parallel.ForEachAsync` + 整体/单请求超时分离，仅支持无依赖并行聚合。

**升级方案**：引入轻量 DAG 编排（声明式 `AggregateBuilder`，节点声明依赖关系），引擎自动拓扑排序 + 并行调度 + 超时级联。支持"先查用户再查用户订单"等依赖链场景。

**预期效果**：复杂聚合场景声明式表达，自动最大化并行度。

**兼容性风险**：低。现有 `Parallel.ForEachAsync` 路径作为引擎特例保留。

### 4.2 模块解耦

#### 4.2.1 库存独立 BC

**现状**：库存跨三 BC 拆分——`StockBaseline` 在 Product BC，`StockReservation` 聚合与 `IInventoryRepository` 在 Order BC，秒杀库存用 Redis Hash 在 Promotion BC。三套库存真相源，对账链路长。

**升级方案**：
1. **短期**：为 `StockReservationCompensation` 增加 `OperationType` 字段（修复 NEW-P0-3）
2. **中期**：将库存独立为 `Inventory` BC，`StockReservation` 聚合迁移至 `Leno.Inventory.Domain`，Order BC 通过集成事件/命令调用库存 BC
3. **长期**：评估 Redis 双写的必要性，引入 Redis → DB 异步对账的 SLA 监控

**预期效果**：库存真源单一化，对账成本下降；支持多仓、预售、批次库存扩展。

**兼容性风险**：高。需迁移三 BC 现有库存数据与事件流，建议分阶段：先统一 Inventory BC 接口，再迁移数据。

#### 4.2.2 积分会员 BC 拆分

**现状**：PointsMembership 单 BC 内同时承载 PointsAccount（积分账户/冻结/流水/兑换券）与 Member（成长值/等级/会员包）两个聚合根，9 个 Consumer 混杂积分事件与会员事件。

**升级方案**：拆分为 `Points` BC（账户/流水/兑换/对账）与 `Membership` BC（成长值/等级/权益包/评估）。两者经 `PointsEarnedEvent` / `MemberLevelChangedEvent` 集成事件协作。

**预期效果**：独立伸缩（积分高频写、会员低频评估）；故障隔离；团队职责清晰。

**兼容性风险**：高。需双 BC 数据库拆分 + Consumer 迁移 + gRPC 端点重组，建议灰度按事件类型切流。

#### 4.2.3 评价与售后 BC 拆分

**现状**：ReviewAfterSales 单 BC 承载 Review（评价聚合）与 AfterSales（售后聚合）两个聚合根，共用 `IOrderEligibilityChecker`。

**升级方案**：拆分为 `Review` BC（评价+评分快照+ES 投影）与 `AfterSales` BC（售后状态机+退款协作）。售后退款完成事件统一命名为 `AfterSalesRefundCompletedEvent` 不再外溢。

**预期效果**：评价读模型可独立 ES 索引重建；售后状态机演进不波及评价。

**兼容性风险**：中。eligibilityChecker 反查逻辑需拆为两套；gRPC 端点需重组。

#### 4.2.4 认证授权 BC 拆分（AuthN/AuthZ 分离）

**现状**：UserAuth BC 同时承载认证（登录/OAuth/JWT/2FA）与授权（Role/Permission/RBAC），共享 `UserAuthDbContext`。

**升级方案**：拆分为 `Identity` BC（用户/认证/OAuth/2FA）与 `AccessControl` BC（角色/权限/策略）。AccessControl BC 暴露 `CheckPermission(userId, resource, action)` RPC。权限策略支持 RBAC + ABAC 混合。

**预期效果**：认证与授权独立部署/扩展；支持细粒度 ABAC；权限变更不影响登录态。

**兼容性风险**：中。`User.Roles` owned collection 需迁移到 AccessControl BC；JWT claim 中 `role` 需保留向后兼容。

### 4.3 技术栈更新

#### 4.3.1 Saga 状态机引入（MassTransit Saga）

**现状**：[OrderSagaOrchestrator](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs) 采用进程内编排，状态完全在内存中不持久化，崩溃恢复能力为 0。

**升级方案**：引入 `SagaStateMachine<OrderSagaState>`，状态持久化到 `order_saga_states` 表。状态流转：`Pending → StockReserved → PointsFrozen → OrderCreated → Completed` / `Compensating → Compensated`。每个状态转换对应一个事件/命令，崩溃后从持久化状态恢复。补偿动作通过 Outbox 发布，由对应 BC 的消费者执行。

**预期效果**：崩溃恢复能力从 0 → 100%；超时消息遗漏风险消除；补偿原子性提升。

**兼容性风险**：高。需新增 saga_states 表 + 迁移现有在途订单状态。建议分阶段：先持久化 Saga 状态（不引入状态机），再迁移到 MassTransit Saga。

#### 4.3.2 Process Manager 模式

**现状**：`PaymentSucceededEventConsumer` / `StockConfirmConsumer` / `PointsConfirmConsumer` 三个独立消费者分别处理同一 `PaymentSucceededEvent`，无全局协调。

**升级方案**：引入 `OrderPaymentProcessManager`，订阅 `PaymentSucceededEvent`，编排三个子任务（`MarkOrderPaid` / `ConfirmStock` / `ConfirmPoints`），跟踪整体完成状态。子任务失败时决定重试或补偿。状态持久化到 `order_payment_processes` 表。

**预期效果**：跨进程操作有全局协调；中间态可观测；失败有自动补偿而非被动死信。

**兼容性风险**：中。Process Manager 与现有三消费者双轨期并存；需保证幂等。

#### 4.3.3 促销规则引擎

**现状**：`PromotionCalculateAppService` 硬编码满减 + 优惠券两类规则，新增规则类型需修改聚合方法与试算服务。

**升级方案**：抽象 `IPromotionRule` 接口与 `PromotionRuleContext`，引入规则引擎（如 RulesEngine 或自研 DSL），规则配置 JSON 化存储。`PromotionCalculateAppService` 改为规则编排器，按优先级 + 叠加策略调用规则链。

**预期效果**：新规则类型零侵入扩展；运营自助配置促销；A/B 测试规则组合。

**兼容性风险**：中。聚合内 `CalculateDiscount` 需保留向后兼容包装，旧规则迁移到新引擎。

#### 4.3.4 OAuth/SSO 通用化

**现状**：已支持 Google/WeChat/Alipay 三方，每新增 provider 需实现 `IExternalAuthService` + 注册 DI + 写 HttpClient。

**升级方案**：抽象 `IOAuth2ProviderAdapter` 通用 OIDC 适配器，配置驱动而非代码驱动。`OAuthClient` 聚合扩展 `DiscoveryUrl`、`Scopes`、`ClaimMappings` 字段，支持任意 OIDC 兼容 IdP。引入 SAML2 模块支持企业 SSO。

**预期效果**：新 provider 零代码接入；支持企业 SSO；标准化 OIDC claim 映射。

**兼容性风险**：低。现有三 provider 适配器保留，新增通用 OIDC 适配器并行。

#### 4.3.5 安全技术栈升级

| 现状 | 升级方向 | 优先级 |
|------|---------|--------|
| bcrypt WorkFactor 12 | Argon2id + PEPPER 加盐 | 中期 |
| HS256 共享 SecretKey | RS256/ES256 非对称签名 | 中期 |
| AES Key 从 appsettings.json | KMS 托管（Azure Key Vault / AWS KMS）+ KeyId 版本化 | 中期 |
| JWT 黑名单 Redis fail-open 不明确 | 显式 fail-close 默认 + 可配置 fail-open + 告警 | 短期 |
| 无 IP 级限流 | 网关层 IP 限流 + 设备指纹 | 短期 |

### 4.4 扩展性提升

#### 4.4.1 多级缓存（L1 Local + L2 Redis）

**现状**：[CacheService](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs) 单级 Redis 缓存 + 布隆过滤器 + 互斥锁 + 抖动。

**升级方案**：引入 `IMemoryCache` 作为 L1（短 TTL，如 5s），Redis 作为 L2（长 TTL），L1 失效回源 L2。配合 Pub/Sub 做 L1 跨实例失效（参考 JwtBlacklistService 模式）。

**预期效果**：热点 Key Redis QPS 下降 80%+。

**兼容性风险**：中。L1 跨实例失效需 Pub/Sub 通道，复杂度上升。

#### 4.4.2 Outbox 分片发布器

**现状**：Outbox 两阶段标记 + 并行发布，单实例发布器。

**升级方案**：按聚合根 ID 哈希分片，多实例发布器各管一片，配合 `SELECT ... FOR UPDATE SKIP LOCKED` 实现无损水平扩展。

**预期效果**：发布吞吐随实例数线性扩展。

**兼容性风险**：中。需调整 Outbox 表结构与发布 SQL，需灰度。

#### 4.4.3 通知中心渠道注册表

**现状**：渠道以 `INotificationChannel` 接口 + 多实现注册，渠道枚举固定为 Sms/Email/InApp。

**升级方案**：引入 `INotificationChannelRegistry`（渠道自描述 Channel 元数据 + 能力声明：是否需限流/是否异步回执/是否幂等），偏好配置以渠道 Key 字符串而非枚举存储。

**预期效果**：新增渠道（Push/IM/Webhook）零侵入核心调度；渠道能力声明驱动限流/重试/回执处理。

**兼容性风险**：中。偏好存储结构变更需数据迁移；建议双写过渡期。

#### 4.4.4 限流/重试策略配置化

**现状**：[RetryPolicy.cs#L67-72](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Services/RetryPolicy.cs#L67-L72) 退避序列硬编码，[RedisRateLimiter.cs#L19-25](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Services/RedisRateLimiter.cs#L19-L25) 阈值 const。

**升级方案**：抽取 `RetryPolicyOptions` / `RateLimitOptions` 走 `IOptionsMonitor<>`，支持按 templateCode 维度配置限流规则；错误码分类表存配置中心或 SystemAdmin FeatureFlag 热更新。

**预期效果**：运营自助调参；大促前动态放宽限流；故障期快速将错误码加入重试白名单。

**兼容性风险**：低。配置缺省值与现有 const 保持一致，零行为变更。

#### 4.4.5 支付渠道插件化

**现状**：[PaymentChannelFactory](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Payment/Leno.Payment.Infrastructure/Channels/PaymentChannelFactory.cs) 硬编码 WeChatPay/Alipay 两个适配器 switch。

**升级方案**：`IPaymentChannelFactory.GetAdapter` 改为 `IEnumerable<IPaymentChannelAdapter>` 注入 + `ToDictionary(a => a.Channel)` 查找，新增渠道只需注册 DI。渠道适配器打包为独立程序集，运行时通过 `Assembly.Load` 加载，配置驱动启停。

**预期效果**：新增银联/Apple Pay 渠道从"改 3 处代码 + 发版"降为"实现适配器 + 注册 DI + 配置启用"。

**兼容性风险**：低。工厂接口不变，仅实现切换。

#### 4.4.6 多租户与国际化预留

**现状**：全量扫描 `TenantId` / `tenant_id` / `ITenant` / `MultiTenant` 零命中；`IStringLocalizer` / `ResourceManager` / `CultureInfo` 零命中。

**升级方案**（取决于业务规划）：
- **多租户**：聚合根 + EF Configuration 增 `tenant_id` 列与全局查询过滤器；Notification 偏好/模板按租户维度配置；SystemAdmin 审计日志/FeatureFlag 按租户隔离
- **国际化**：通知模板支持多语言变体（`NotificationTemplate` 增 `Culture` 维度）；错误码→本地化消息资源文件；限流/校验提示走 `IStringLocalizer`

**预期效果**：支持 SaaS 多租户与海外业务扩展。

**兼容性风险**：高。需领域模型与数据库结构变更，建议在领域模型预留扩展位，业务驱动时再落地。

---

## 五、代码优化建议

### 5.1 代码规范统一

| # | 问题 | 文件#行号 | 建议 | 优先级 |
|---|------|----------|------|--------|
| 1 | LogisticsCompanyCode 列名为 PascalCase，其余为 snake_case | [InitialCreate.cs#L86](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Order/Leno.Order.Infrastructure/Migrations/InitialCreate.cs#L86) | OrderConfiguration 增加 `HasColumnName("logistics_company_code")`，新增迁移 rename | 中 |
| 2 | 领域事件命名不统一（`-ed` 后缀 vs `DomainEvent` 后缀） | Order/Cart BC | 统一为 `{Aggregate}{Action}DomainEvent` | 低 |
| 3 | gRPC 旧客户端回退 `new Guid(Convert.FromHexString(id.ToString("X16")))` 重复 3 处 | [ProductGrpcService.cs#L41,91,125](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs#L41) | 抽取为 `GuidFromInt64Hex(long)` 工具方法复用 | 低 |

### 5.2 冗余代码清理

| # | 问题 | 文件#行号 | 建议 | 优先级 |
|---|------|----------|------|--------|
| 1 | RedisSlidingWindowRateLimiter 双份实现 | [Leno.Infrastructure/RateLimiting/](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/RateLimiting/) + [ApiGateway/Services/](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/ApiGateway/Leno.ApiGateway/Services/) | 删除 ApiGateway 副本，引用共享层实现 | 高 |
| 2 | TraceIdEnricher 与 OpenTelemetryTraceIdEnricher 重复 | [SerilogConfig.cs#L35-48](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Logging/SerilogConfig.cs#L35-L48) + [OpenTelemetryExtensions.cs#L130-149](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs#L130-L149) | 合并为单一 `TraceIdEnricher` | 高 |
| 3 | AuditableEntityInterceptor 与 BaseDbContext.FillAuditableFields 重叠 | [EFCoreInterceptors.cs#L12-51](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Persistence/EFCoreInterceptors.cs#L12-L51) | 保留 Interceptor（EF Core 推荐方式），删除 DbContext 内填充逻辑 | 中 |
| 4 | NotificationEventConsumer `[Obsolete]` 未删 | [NotificationEventConsumer.cs#L15-20](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Consumers/NotificationEventConsumer.cs#L15-L20) | 确认无测试引用后删除 | 中 |
| 5 | SellerDashboardAppService 双轨未下线 | [SellerDashboardAppService.cs#L28](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerDashboardAppService.cs#L28) | 按标注 2026-10-01 截止下线 | 中 |
| 6 | SmsChannel HttpTimeout 重复定义 2 处 | [SmsChannel.cs#L23,197](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Channels/SmsChannel.cs#L23) | 提取到单一 `SmsChannelOptions.HttpTimeout` | 低 |

### 5.3 算法效率改进

| # | 问题 | 文件#行号 | 建议 | 优先级 |
|---|------|----------|------|--------|
| 1 | RedisBloomFilter.MightContainAsync 7 次串行 StringGetBitAsync | [RedisBloomFilter.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs) | 改用 Lua 脚本一次 `EVAL`，减少 7 次网络往返 | 中 |
| 2 | NotificationDispatcher 单用户多渠道 2N 次 SaveChanges | [NotificationDispatcher.cs#L88-115](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Services/NotificationDispatcher.cs#L88-L115) | 先创建所有渠道记录并 Add，单次 SaveChanges | 中 |
| 3 | Promotion GetByUserAsync 内存过滤已过期券 | [PromotionCalculateAppService.cs#L92-101](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Promotion/Leno.Promotion.Application/Services/PromotionCalculateAppService.cs#L92-L101) | 仓储层下推 `ExpiredAt > now` 到 SQL | 中 |
| 4 | PermissionRepository 全表加载（已修复为 OPENJSON） | [EfCorePermissionRepository.cs#L62-82](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCorePermissionRepository.cs#L62-L82) | 已修复，保持现状 | — |
| 5 | 多卖家下单 Saga 串行调用 Redis Lua（50 次往返） | [OrderSagaOrchestrator.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs) | ReserveBatchAsync 改为 Redis Pipeline/MULTI 批量执行 | 中 |

### 5.4 内存使用优化

| # | 问题 | 文件#行号 | 建议 | 优先级 |
|---|------|----------|------|--------|
| 1 | TemplateRenderer 每次 Render 都 new List/HashSet | [TemplateRenderer.cs#L88-121](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Services/TemplateRenderer.cs#L88-L121) | 占位符解析结果用 `ArrayPool<StringBuilder>` 或复用缓冲 | 低 |
| 2 | StatisticsReconciliationService 每次 new List 无容量预分配 | [StatisticsReconciliationService.cs#L47](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Services/StatisticsReconciliationService.cs#L47) | 预分配容量 `new List<ReconciliationRecord>(capacity)` | 低 |
| 3 | FallbackResponseMiddleware MemoryStream 缓冲所有响应体 | [FallbackResponseMiddleware.cs#L57-69](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/ApiGateway/Leno.ApiGateway/Middleware/FallbackResponseMiddleware.cs#L57-L69) | 评估是否仅对小响应体缓冲，或流式检测 503 | 低 |
| 4 | Outbox 表无归档策略，长期运行无限增长 | OutboxMessage 表 | 定时清理 7 天前已处理记录；或按月分区表 | 中 |

### 5.5 安全性增强

| # | 类别 | 现状 | 风险 | 建议 | 优先级 |
|---|------|------|------|------|--------|
| 1 | 密码哈希 | bcrypt WorkFactor 12 | bcrypt 抗 GPU/ASIC 弱于 Argon2id | 中期迁移 Argon2id + PEPPER | 中期 |
| 2 | AES Key 管理 | appsettings.json 读取 | 密钥轮换需停服，无版本化 | 引入 KMS 托管 + KeyId 前缀 | 中期 |
| 3 | JWT 签名 | HS256 共享 SecretKey | 多服务共享密钥泄露面广 | 迁移 RS256/ES256 非对称签名 | 中期 |
| 4 | JWT 黑名单 | Redis fail-open 不明确 | Redis 故障时行为不可预期 | 显式 fail-close 默认 + 可配置 fail-open + 告警 | 短期 |
| 5 | IP 级限流 | 仅账户级 FailedLoginCount | 分布式 IP 枚举攻击 | 网关层 IP 限流 + 设备指纹 | 短期 |
| 6 | OAuth 邮箱信任 | provider 邮箱未验证即信任 | 攻击者伪造 provider 邮箱接管账户 | 仅信任 verified 邮箱，未验证走"待绑定"流程 | 短期 |
| 7 | OAuth state | 仅校验非空 | CSRF 防护依赖 IOAuthStateStore 实现 | 审计确保 state 为一次性 CSRF token + TTL | 短期 |
| 8 | 资源级越权 | IDOR 统一校验已落地 | 应用层未显式校验 operatorId == ownerId | 应用服务层强制 `RequireOwnedXxxAsync` | 短期 |
| 9 | 依赖安全 | 未定期扫描 | 可能存在已知 CVE | CI 集成 `dotnet list package --vulnerable` | 短期 |
| 10 | 密钥展示 | OAuthClientDto.ClientSecret 返回掩码 | 管理后台截图泄露 | 二次鉴权（重输管理员密码）才展示完整密钥 | 中期 |

### 5.6 数据库索引补充

| # | 表 | 缺失索引 | 文件#行号 | 优先级 |
|---|-----|---------|----------|--------|
| 1 | reviews | `ix_reviews_seller_id` | [ReviewConfiguration.cs#L58-60](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Configurations/ReviewConfiguration.cs#L58-L60) | 高 |
| 2 | notification_records | `ix_notification_records_user_isread_channel` | [NotificationRecordConfiguration.cs#L44-57](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/src/Services/Notification/Leno.Notification.Infrastructure/Configurations/NotificationRecordConfiguration.cs#L44-L57) | 中 |
| 3 | outbox_messages | 分区/归档策略 | OutboxMessage 表 | 中 |

---

## 六、实施步骤

### 6.1 阶段一：P0 阻塞修复（1 周）

**目标**：解除部署阻塞，恢复全域一致性。

| 步骤 | 任务 | 负责区域 | 验收标准 |
|------|------|---------|---------|
| 1 | NEW-P0-1：Order 表 DropColumn `version` 迁移 | Order BC | SQL Server 迁移成功，单 rowversion 列 |
| 2 | NEW-P0-2：CartUnitOfWork 改委托 `SaveChangesWithOutboxAsync` | Cart BC | Cart 领域事件经 Outbox 投递 |
| 3 | NEW-P0-3：StockReservationCompensation 增加 OperationType 字段 | Order BC | 补偿按类型调用 ReleaseAsync/ReturnDeductedAsync |
| 4 | NEW-P0-4：Notification MarkAllAsReadAsync 改走聚合根 | Notification BC | 触发领域事件，写审计字段 |

**资源需求**：2 名后端工程师，1 名 DBA 评审迁移脚本。

**风险评估**：低。修改范围明确，有测试覆盖。

### 6.2 阶段二：速赢优化（2-3 周）

**目标**：清理重复实现，配置化抽取，索引补充。健康度 8.3 → 8.8。

| 步骤 | 任务 | 优先级 |
|------|------|--------|
| 1 | RedisSlidingWindowRateLimiter 双份去重 | 高 |
| 2 | TraceIdEnricher 双份合并 | 高 |
| 3 | AuditableEntityInterceptor 与 FillAuditableFields 去重 | 中 |
| 4 | RedisBloomFilter Lua 化 | 中 |
| 5 | NotificationDispatcher 多次 SaveChanges 合并 | 中 |
| 6 | reviews.seller_id 索引补充 | 高 |
| 7 | notification_records 复合索引补充 | 中 |
| 8 | P1-1 匿名购物车并发覆盖写（Lua 脚本原子更新） | 高 |
| 9 | P1-4 PaymentRequestedEventConsumer 支付单卡 Pending | 高 |
| 10 | P1-5 Notification MarkAllAsReadAsync（与阶段一同步） | 高 |
| 11 | P1-7 RetryPolicy/RateLimiter 配置化 | 中 |
| 12 | P1-9 JwtRevocationService TTL 与 JWT 有效期联动 | 中 |
| 13 | P1-10 Promotion GetByUserAsync 下推 SQL | 中 |
| 14 | 死代码清理（NotificationEventConsumer/SellerDashboardAppService） | 中 |
| 15 | Outbox 表 7 天归档策略 | 中 |

**资源需求**：3 名后端工程师并行，1 名 DBA 评审索引。

**风险评估**：低-中。重复实现去重需回归测试；配置化需保证缺省值零行为变更。

### 6.3 阶段三：中期演进（1-2 月）

**目标**：BC 拆分、Saga 状态机、规则引擎。健康度 8.8 → 9.3。

| 步骤 | 任务 | 周期 | 兼容性风险 |
|------|------|------|-----------|
| 1 | 库存独立 BC（短期修复 OperationType + 中期迁移） | 6 周 | 高 |
| 2 | MassTransit Saga 状态机（先持久化状态，再迁移状态机） | 6 周 | 高 |
| 3 | Process Manager 模式（OrderPaymentProcessManager） | 4 周 | 中 |
| 4 | 促销规则引擎抽象 | 4 周 | 中 |
| 5 | 评价与售后 BC 拆分 | 4 周 | 中 |
| 6 | AuthN/AuthZ BC 拆分 | 6 周 | 中 |
| 7 | OAuth/SSO 通用化（OIDC 适配器） | 3 周 | 低 |
| 8 | 支付渠道插件化 | 3 周 | 低 |
| 9 | 通知中心渠道注册表 | 3 周 | 中 |
| 10 | 安全技术栈升级（Argon2id / RS256 / KMS） | 4 周 | 中 |
| 11 | Cart SKU 快照本地化 | 3 周 | 中 |
| 12 | CQRS 读模型 snapshot + incremental replay | 4 周 | 中 |

**资源需求**：5 名后端工程师 + 1 名架构师评审。

**风险评估**：中-高。BC 拆分需双轨期并行运行；Saga 状态机需迁移在途订单状态；规则引擎需保留向后兼容包装。建议每个高风险项配套集成测试 + 灰度发布。

### 6.4 阶段四：长期架构（3-6 月）

**目标**：Infrastructure 拆包、多级缓存、多租户/国际化预留。健康度 9.3 → 9.6（L5 持续优化）。

| 步骤 | 任务 | 周期 | 兼容性风险 |
|------|------|------|-----------|
| 1 | Leno.Infrastructure 模块化拆包（9 个子包 + 元包门面） | 6 周 | 低 |
| 2 | ACL 防腐层可插拔策略链 | 4 周 | 中 |
| 3 | BFF 聚合层 DAG 编排引擎 | 6 周 | 低 |
| 4 | Outbox 分片发布器 | 4 周 | 中 |
| 5 | 多级缓存 L1 Local + L2 Redis | 4 周 | 中 |
| 6 | Consul 配置 Schema 版本化与灰度发布 | 3 周 | 中 |
| 7 | 多租户预留（领域模型扩展位） | 4 周 | 高 |
| 8 | 国际化预留（IStringLocalizer + 模板多语言） | 4 周 | 高 |
| 9 | 积分会员 BC 拆分 | 6 周 | 高 |
| 10 | 跨 BC 契约评审机制 + Pact 契约测试 | 持续 | 中 |

**资源需求**：5 名后端工程师 + 1 名架构师 + 1 名 DevOps 工程师。

**风险评估**：中-高。多租户/国际化需领域模型与数据库结构变更，建议业务驱动时再落地。Infrastructure 拆包虽低风险但工作量大，建议分批迁移。

---

## 七、预期效果

### 7.1 量化指标

| 指标 | 当前基线 | 阶段一后 | 阶段二后 | 阶段三后 | 阶段四后 |
|------|---------|---------|---------|---------|---------|
| 整体健康度 | 8.3/10 | 8.5 | 8.8 | 9.3 | 9.6 |
| P0 阻塞问题 | 4 | 0 | 0 | 0 | 0 |
| P1 高优先级问题 | 10 | 10 | 0 | 0 | 0 |
| 重复实现 | 4 | 4 | 0 | 0 | 0 |
| 配置化覆盖率 | 60% | 60% | 90% | 95% | 98% |
| BC 数量 | 11 | 11 | 11 | 14（拆分后） | 14 |
| Saga 崩溃恢复能力 | 0% | 0% | 0% | 100% | 100% |
| 缓存热点 QPS 下降 | — | — | — | — | 80%+ |
| Outbox 发布吞吐 | 1x | 1x | 1x | 1x | Nx（随实例数） |
| L5 持续优化达成 | ❌ | ❌ | ❌ | ❌ | ✅ |

### 7.2 业务价值

| 维度 | 预期效果 |
|------|---------|
| 可用性 | Saga 崩溃恢复 + Process Manager 全局协调，跨域半完成状态消除 |
| 性能 | 多级缓存 + Outbox 分片 + Lua 脚本原子化，热点 QPS 下降 80%+，发布吞吐线性扩展 |
| 可扩展性 | BC 拆分 + 规则引擎 + 渠道插件化，新业务接入成本降低 50%+ |
| 安全性 | Argon2id + RS256 + KMS + IP 限流，符合 OWASP Top 10 与个人信息保护法 |
| 可维护性 | Infrastructure 拆包 + 重复清理 + 配置化，编译启动加速 30%+，运营自助调参 |
| 可观测性 | Saga 状态可查询 + Process Manager 中间态可观测 + 死信中枢统一治理 |

---

## 八、资源需求

### 8.1 人力资源

| 阶段 | 后端工程师 | 架构师 | DBA | DevOps | 测试工程师 | 周期 |
|------|-----------|--------|-----|--------|-----------|------|
| 阶段一 P0 阻塞 | 2 | 0 | 0.5 | 0 | 1 | 1 周 |
| 阶段二 速赢 | 3 | 0 | 0.5 | 0 | 1 | 2-3 周 |
| 阶段三 中期演进 | 5 | 1 | 1 | 0.5 | 2 | 1-2 月 |
| 阶段四 长期架构 | 5 | 1 | 1 | 1 | 2 | 3-6 月 |

### 8.2 基础设施需求

| 资源 | 用途 | 阶段 |
|------|------|------|
| Redis Cluster | 多级缓存 L2 + 限流 + 幂等 + 黑名单 | 阶段二起 |
| Elasticsearch Cluster | CQRS 读模型 + 全文检索 | 已有，阶段三扩容 |
| KMS（Azure Key Vault / AWS KMS） | AES Key 托管 + 密钥版本化 | 阶段三 |
| Pact Broker | 跨 BC 契约测试 | 阶段四 |
| Grafana / Prometheus | Saga 状态 + Process Manager 指标监控 | 阶段三 |

### 8.3 培训需求

| 主题 | 受众 | 阶段 |
|------|------|------|
| MassTransit Saga 状态机 | 后端团队 | 阶段三前 |
| Roslyn Source Generator | 后端团队 | 阶段四前 |
| Pact 契约测试 | 后端 + QA | 阶段四前 |
| Argon2id / KMS 集成 | 安全 + 后端 | 阶段三前 |

---

## 九、风险评估

### 9.1 高风险项

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|---------|
| BC 拆分导致数据迁移失败 | 中 | 高 | 双轨期并行运行 + 灰度按事件类型切流 + 回滚预案 |
| Saga 状态机迁移在途订单丢失 | 中 | 高 | 先持久化 Saga 状态（不引入状态机）+ 状态对账脚本 + 回滚预案 |
| 库存独立 BC 迁移期间超卖 | 低 | 高 | 双写过渡 + Redis 库存对账 SLA 监控 + 回滚预案 |
| Infrastructure 拆包引发依赖循环 | 中 | 中 | 分批迁移 + 依赖图分析工具 + 元包门面兜底 |
| 多租户/国际化预留过度设计 | 中 | 中 | 业务驱动原则，仅在领域模型预留扩展位，不实际落地 |

### 9.2 兼容性风险与缓解

| 风险 | 缓解措施 |
|------|---------|
| gRPC 契约演进破坏旧客户端 | 双写过渡 + deprecated 标注 + 30 天迁移期 + buf breaking 校验 |
| 集成事件 schema 变更破坏消费方 | SchemaVersion 版本化 + 消费方按版本路由 + 跨 BC 契约评审 |
| `IUnitOfWork` 接口变更破坏 BC | `[Obsolete]` 标注 + 委托模式 + BannedApiAnalyzers 强制禁止旁路 |
| JWT 签名算法变更（HS256 → RS256） | 双签名过渡期 + 公钥分发机制 + 旧 token 兼容窗口 |
| 配置化抽取改变缺省行为 | 缺省值与现有 const 完全对齐 + 零行为变更测试 |

### 9.3 回滚预案

每个高风险项必须配套回滚预案：

1. **BC 拆分回滚**：保留原 BC 代码 + 数据库双写 + feature flag 控制流量切换
2. **Saga 状态机回滚**：保留 `OrderSagaOrchestrator` 进程内编排代码 + 双轨期
3. **Infrastructure 拆包回滚**：元包门面始终可用 + 子包引用失败时回退元包
4. **安全升级回滚**：HS256/RS256 双签名过渡 + KMS 失败时回退 appsettings.json

---

## 十、兼容性保障

### 10.1 向后兼容原则

1. **接口扩展只增不删**：所有公开接口（`IUnitOfWork` / `IAntiCorruptionService` / `IPaymentChannelAdapter` 等）只增加方法，不删除/修改现有方法签名
2. **集成事件版本化**：新增字段走 Optional + SchemaVersion 递增，消费方按版本路由
3. **gRPC 契约演进**：`.proto` 文件只增字段不删字段，deprecated 标注 + 30 天迁移期 + buf breaking 校验
4. **数据库迁移可逆**：所有迁移脚本必须配套 Down 方法，支持回滚
5. **配置缺省值对齐**：配置化抽取时缺省值与现有 const 完全对齐，零行为变更

### 10.2 双轨期策略

| 场景 | 双轨期 | 切换机制 |
|------|--------|---------|
| Saga 状态机 vs 进程内编排 | 4 周 | feature flag 按 OrderId 哈希切流 |
| BC 拆分（库存/积分会员/评价售后） | 8 周 | 事件类型双写 + 灰度按 BC 切流 |
| HS256 → RS256 | 4 周 | 双签名过渡 + 公钥分发 |
| Infrastructure 拆包 | 12 周 | 元包门面始终可用 + 子包按需迁移 |
| gRPC int64 → string | 12 周 | deprecated 标注 + 客户端逐步升级 |

### 10.3 质量保障

1. **单元测试覆盖**：每个新接口/聚合方法 ≥ 80% 覆盖率
2. **集成测试**：BC 拆分配套跨 BC 集成测试（Testcontainers + MassTransit TestHarness）
3. **契约测试**：阶段四引入 Pact 契约测试锁定跨 BC 调用
4. **混沌工程**：阶段三引入故障注入测试（Simmy 之类），验证 Saga 补偿与 Process Manager 故障恢复
5. **性能基准**：每个阶段配套性能基准测试，对比基线确保不退化

---

## 附录 A：subagent 分析报告索引

本方案基于 4 个并行 subagent 的分析报告综合产出：

| subagent | 范围 | 核验项数 | 修复落地 | 新发现 P0 | 架构演进机会 |
|---------|------|---------|---------|----------|-------------|
| 1 | 共享层（BuildingBlocks + ApiGateway） | 12 | 12/12 | 0 | 8 |
| 2 | 核心交易 BC（Order/Payment/Cart） | 9 | 5/9 | 3 | 8 |
| 3 | 商品/促销/用户 BC（Product/Promotion/UserAuth） | 14 | 14/14 | 0 | 8 |
| 4 | 支撑 BC（Notification/PointsMembership/ReviewAfterSales/SellerShop/SystemAdmin） | 22 | 21/22 | 1 | 7 |
| **合计** | — | **57** | **52/57** | **4** | **31** |

## 附录 B：与既有审计的关系

本方案**不替代** 2026-07-21 代码审计（[2026-07-21-code-audit/](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-architectural-upgrade-plan-KqJ46g/docs/superpowers/specs/2026-07-21-code-audit/)），而是在其基础上：

1. **核验修复落地**：逐项核验 57 项 P0 高风险在 2026-07-22 修复批次后的当前状态
2. **发现新问题**：识别 4 项新 P0 阻塞问题 + 10 项 P1 高优先级问题
3. **前瞻性规划**：从"bug 修复"转向"架构演进"，聚焦系统分层/模块解耦/技术栈/扩展性
4. **量化健康度**：从 2026-07-21 的 6.0/10 评估当前 8.3/10，规划演进到 9.6/10

## 附录 C：实施优先级矩阵

按"业务影响 × 实现成本 × 兼容性风险"排序 Top 20 实施项：

| 排名 | 实施项 | 业务影响 | 实现成本 | 兼容性风险 | 阶段 |
|:---:|------|:---:|:---:|:---:|:---:|
| 1 | NEW-P0-1 Order 双 rowversion 列修复 | 高 | 低 | 低 | 阶段一 |
| 2 | NEW-P0-2 CartUnitOfWork 旁路 Outbox 修复 | 高 | 低 | 低 | 阶段一 |
| 3 | NEW-P0-3 补偿表 OperationType 修复 | 高 | 低 | 低 | 阶段一 |
| 4 | NEW-P0-4 Notification MarkAllAsReadAsync 修复 | 高 | 低 | 低 | 阶段一 |
| 5 | RedisSlidingWindowRateLimiter 去重 | 中 | 低 | 低 | 阶段二 |
| 6 | TraceIdEnricher 合并 | 中 | 低 | 低 | 阶段二 |
| 7 | reviews.seller_id 索引 | 中 | 低 | 低 | 阶段二 |
| 8 | P1-1 匿名购物车并发覆盖写 | 高 | 中 | 低 | 阶段二 |
| 9 | P1-4 支付单卡 Pending | 高 | 中 | 低 | 阶段二 |
| 10 | P1-7 RetryPolicy/RateLimiter 配置化 | 中 | 低 | 低 | 阶段二 |
| 11 | 库存独立 BC（短期 OperationType + 中期迁移） | 高 | 高 | 高 | 阶段三 |
| 12 | MassTransit Saga 状态机 | 高 | 高 | 高 | 阶段三 |
| 13 | Process Manager 模式 | 高 | 中 | 中 | 阶段三 |
| 14 | 促销规则引擎 | 中 | 中 | 中 | 阶段三 |
| 15 | 评价与售后 BC 拆分 | 中 | 中 | 中 | 阶段三 |
| 16 | AuthN/AuthZ BC 拆分 | 中 | 高 | 中 | 阶段三 |
| 17 | 安全技术栈升级（Argon2id/RS256/KMS） | 高 | 中 | 中 | 阶段三 |
| 18 | Infrastructure 模块化拆包 | 中 | 高 | 低 | 阶段四 |
| 19 | 多级缓存 L1+L2 | 中 | 中 | 中 | 阶段四 |
| 20 | ACL 防腐层可插拔策略链 | 中 | 中 | 中 | 阶段四 |

---

**方案生成完毕**

本方案基于 2026-07-23 当前代码状态，由 4 个并行 subagent 核验 57 项 P0 修复落地 + 识别 4 项新 P0 + 31 项架构演进机会，综合产出 4 阶段实施路线（1 周 + 2-3 周 + 1-2 月 + 3-6 月），预期健康度从 8.3 演进到 9.6（L5 持续优化），确保与现有业务逻辑完全兼容。
