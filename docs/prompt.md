# Vibe Coding Prompt — Leno 电商平台 DDD 全栈自动开发

> **文档性质**: 自主编码（Vibe Coding）起始指令
> **目标读者**: AI Agent 编排系统（主 Agent + 子 Agent）
> **项目**: Leno 电商平台 DDD 微服务系统
> **技术栈**: .NET 10 / ASP.NET Core / EF Core / SQL Server / Redis / RabbitMQ / Elasticsearch
> **创建日期**: 2026-07-10

---

## 0 角色定义

你是本项目的 **主 Agent（Master Agent）**，负责全流程自主编排、进度跟踪与质量把控。整个过程无人工参与，你必须自主完成所有决策、实现、测试与集成。

### 你的职责

1. **进度管理**: 维护 `docs/tasks/progress.md` 中的 Checklist，实时标记模块与任务完成状态。
2. **子 Agent 调度**: 根据模块依赖关系与任务文件，生成子 Agent 实现每个模块。
3. **质量门禁**: 每个模块完成后，验证编译通过、单元测试通过、集成测试通过，方可标记为完成。
4. **跨模块协调**: 处理模块间的接口契约对齐、集成事件字段一致性、防腐层接口匹配。
5. **额外子 Agent**: 根据需要生成其他子 Agent，如数据库迁移脚本生成、Docker 编排、API 文档生成、端到端集成测试等。

### 子 Agent 类型

| 子 Agent 类型         | 职责                                                                  | 触发时机                          |
| --------------------- | --------------------------------------------------------------------- | --------------------------------- |
| **Module Agent**      | 实现单个模块的全部任务（Domain → Infrastructure → Application → API） | 主 Agent 按阶段顺序逐个或并行调度 |
| **Integration Agent** | 编写跨模块集成测试、验证事件链路                                      | 每个阶段完成后                    |
| **Infra Agent**       | 生成 Docker Compose、CI 脚本、数据库初始化脚本                        | 基础设施阶段与部署准备阶段        |
| **Review Agent**      | 代码审查：DDD 合规性、编码规范、安全检查                              | 每个模块完成后                    |

---

## 1 项目概述

### 1.1 项目定位

Leno 是一个高可用、可扩展的标准 B2C 电商平台，覆盖商品浏览、购物车、订单交易、用户管理、促销运营、售后、积分会员、支付集成与消息通知全链路。采用 DDD 领域驱动设计，CQRS 读写分离，事件驱动架构，按角色模块独立部署。

### 1.2 用户角色

| 角色       | 职责边界   | 典型操作                                                         |
| ---------- | ---------- | ---------------------------------------------------------------- |
| 买家       | 个人消费侧 | 商品搜索、加入购物车、创建订单、支付、申请售后、评价、签到领积分 |
| 卖家       | 店铺经营侧 | 发布商品、库存管理、订单发货、查看店铺订单与售后                 |
| 运营管理员 | 平台经营侧 | 商品审核、商家管理、优惠券发放、促销配置、数据看板               |
| 系统管理员 | 技术运维侧 | 角色权限、接口限流、渠道参数配置、日志监控                       |

### 1.3 非功能指标

- 订单创建支持 1000 TPS，秒杀接口限流后支持 5000 QPS
- 95% 的 API 请求响应时间低于 500ms，复杂搜索低于 800ms
- JWT 有效期 2 小时，Refresh Token 有效期 7 天
- 用户密码采用 BCrypt 哈希
- 异步消息通过死信队列与重试机制保障最终一致性

---

## 2 架构设计

### 2.1 架构风格

DDD 限界上下文 + 模块化部署。逻辑上以限界上下文为边界组织代码，物理上以"角色端 × 上下文"为单元独立打包部署。初期可部署于同一进程（模块化单体），后续可沿限界上下文边界平滑拆分为独立微服务。

### 2.2 DDD 分层架构

严格遵循依赖倒置原则，编译时依赖从外向内指向领域核心：

```
表现层 (API) → 应用层 (Application) → 领域层 (Domain) → 共享内核 (SharedKernel)
                    ↑                        ↑
              基础设施层 (Infrastructure) ────┘
```

| 层         | 职责                                               | 关键约束                                              |
| ---------- | -------------------------------------------------- | ----------------------------------------------------- |
| 领域层     | 实体、值对象、聚合根、领域服务、仓储接口、领域事件 | 不引用任何技术框架；业务规则集中于此                  |
| 应用层     | 任务编排、事务管理、输入验证、安全认证、事件发布   | 本身很薄，不含业务规则                                |
| 基础设施层 | 仓储实现、工作单元、消息发布、缓存、外部渠道适配   | 按 Persistence/Messaging/Caching/Storage 等子目录组织 |
| 表现层     | 控制器、过滤器、中间件、DTO 转换                   | 控制器不含业务逻辑；RESTful 风格；按角色拆分          |

### 2.3 CQRS 读写分离

- **写侧 (Command)**: EF Core + 关系型数据库 (SQL Server)，保证 ACID
- **读侧 (Query)**: Elasticsearch + Redis 缓存，优化复杂检索
- **同步机制**: 写库变更产生领域事件 → 发件箱表 → 事件总线 → 查询侧同步器消费事件更新 ES 读库
- **强一致补强**: 库存预占等强一致操作在命令侧以 Redis Lua 脚本原子完成

### 2.4 事件驱动与发件箱模式

跨上下文协作以事件驱动为主。发件箱模式保证数据库事务与消息发布原子性：

1. 聚合方法执行业务逻辑，产生领域事件
2. EF Core SaveChanges 在同一事务写入聚合状态变更与 Outbox 表记录
3. 后台进程轮询 Outbox 表，发布到 RabbitMQ
4. 消费者通过 EventId 实现幂等去重，消费失败按指数退避重试，超阈值进入死信队列

### 2.5 模块独立部署

按角色拆分为独立部署单元：买家端、卖家端、运营端、系统管理端、支付集成服务、消息通知服务、用户与认证服务。模块间不共享数据库，跨模块协作走集成事件异步通信与防腐层同步查询。

---

## 3 技术栈

| 类别         | 技术                                                           | 版本  |
| ------------ | -------------------------------------------------------------- | ----- |
| 运行时与框架 | .NET 10 / ASP.NET Core 10 Web API / EF Core 10                 | 10    |
| 写库         | SQL Server（每个限界上下文独立 Schema 或独立库）               | 2019+ |
| 读库         | Elasticsearch + NEST（`ik_smart` 中文分词）                    | 8.x   |
| 缓存         | Redis（会话、购物车、库存预占、限流、积分余额）                | 7.x   |
| 消息队列     | RabbitMQ + MassTransit（Topic Exchange + 死信队列 + 延迟队列） | 3.12+ |
| 认证授权     | JWT + OAuth2（OpenIddict）                                     | -     |
| 支付渠道     | 微信支付 SDK + 支付宝 SDK，`IPaymentChannelAdapter` 适配       | -     |
| 邮件         | MailKit（SMTP），`IChannel` 适配                               | -     |
| 短信         | 阿里云/腾讯云短信 SDK，`IChannel` 适配                         | -     |
| 文件存储     | `IFileStorageService` 抽象（Local / MinIO/OSS）                | -     |
| 日志         | Serilog（结构化 JSON 输出，RequestId/TraceId 贯穿）            | -     |
| 链路追踪     | OpenTelemetry                                                  | -     |
| 监控         | Prometheus + Grafana                                           | -     |
| 配置         | appsettings.json + 环境变量 + Consul/Apollo（生产）            | -     |
| 容器化       | Docker + docker-compose                                        | -     |
| API 文档     | Swagger / OpenAPI（Swashbuckle）                               | -     |
| 输入校验     | FluentValidation                                               | -     |
| 测试         | xUnit + Moq + Testcontainers                                   | -     |

---

## 4 编码规范摘要

### 4.1 项目结构

每个限界上下文包含四个分层项目，所有项目共享前缀 `Leno`：

```
src/Services/{ContextName}/
├── Leno.{ContextName}.Domain/              # 领域层
│   ├── Aggregates/                    # 聚合根与实体
│   ├── ValueObjects/                  # 值对象
│   ├── DomainServices/               # 领域服务
│   ├── Repositories/                  # 仓储接口 (I{AggregateRoot}Repository)
│   ├── Events/                        # 领域事件
│   └── Exceptions/                    # 领域异常
├── Leno.{ContextName}.Application/         # 应用层
│   ├── Services/                      # 应用服务 (I{X}AppService + 实现)
│   ├── Commands/                      # 命令对象
│   ├── Queries/                       # 查询对象
│   ├── DTOs/                          # 数据传输对象
│   └── Validators/                    # FluentValidation 校验
├── Leno.{ContextName}.Infrastructure/      # 基础设施层
│   ├── Persistence/                   # DbContext + 仓储实现 + 配置 + 迁移
│   ├── Messaging/                     # 事件总线 + 发件箱 + 消费者
│   ├── Caching/                       # Redis 缓存
│   ├── Storage/                       # 文件存储
│   └── Dependencies/                  # DI 注册扩展
└── Leno.{ContextName}.Api/                 # 表现层
    ├── Controllers/                   # API 控制器
    ├── Filters/                       # 过滤器
    ├── Middleware/                    # 中间件
    └── Program.cs                     # 应用入口
```

共享内核项目（同样使用 `Leno` 前缀）：

```
src/BuildingBlocks/
├── Leno.SharedKernel/                      # 值对象、领域基础抽象、异常
├── Leno.SharedContracts/                   # 集成事件契约、DTO 基类、通用响应
└── Leno.Infrastructure/                    # 基础设施通用实现（DbContext 基类、发件箱、事件总线、ES 读模型）
```

### 4.2 命名约定

| 元素     | 规则                                  | 示例                                         |
| -------- | ------------------------------------- | -------------------------------------------- |
| 项目     | `Leno.{ContextName}.{Layer}`          | `Leno.Order.Domain`                          |
| 命名空间 | `Leno.{ContextName}.{Layer}.{Folder}` | `Leno.Order.Domain.Aggregates`               |
| 聚合根   | 业务名词，继承 `AggregateRoot`        | `Order`、`SPU`、`PaymentOrder`               |
| 仓储接口 | `I{AggregateRoot}Repository`          | `IOrderRepository`                           |
| 应用服务 | `I{Module}AppService` + 实现          | `IOrderAppService` / `OrderAppService`       |
| 领域事件 | 过去时，`{Entity}{Action}Event`       | `OrderCreatedEvent`、`PaymentSucceededEvent` |
| 集成事件 | `{Action}IntegrationEvent`            | `PaymentRequestedIntegrationEvent`           |
| 控制器   | 资源名词复数                          | `OrdersController`、`ProductsController`     |
| API 路由 | RESTful 名词复数                      | `/api/orders`、`/api/products/{id}`          |
| 数据库表 | 蛇形命名                              | `orders`、`order_items`、`payment_orders`    |
| 解决方案 | `Leno.sln`                            | —                                            |

### 4.3 领域层规范

1. 领域层不引用基础设施层、应用层、表现层的任何类型
2. 仓储接口定义在领域层，命名 `I{AggregateRoot}Repository`
3. 聚合根方法行为意图明确，禁止贫血模型（避免纯 get/set）
4. 聚合根通过工厂方法 `Create()` 保证创建时即处于有效状态
5. 状态流转方法校验前置状态，非法流转抛出 `DomainException`
6. 领域异常继承 `DomainException` 基类，携带错误码与业务语义
7. 所有聚合根继承 `AggregateRoot` 基类（持有 `_domainEvents` 列表）
8. 实体属性使用 `private set`，仅通过聚合方法修改

### 4.4 API 规范

- RESTful 风格，资源名词复数，动作通过 HTTP 方法表达
- 统一响应结构：`{ code, message, data }`
- 分页响应：`{ items, total, page, pageSize }`（page 从 1 开始，pageSize 默认 20 最大 100）
- 鉴权：`Authorization: Bearer {token}`
- 幂等键：`Idempotency-Key` 头（注册、下单、支付接口强制）
- 时间字段：ISO 8601 UTC
- HTTP 状态码：200 成功、201 创建、400 参数错误、401 未认证、403 无权限、404 不存在、409 业务冲突、500 服务器错误

### 4.5 提交规范

```
feat(<scope>): <description>     # 新功能
fix(<scope>): <description>      # 修复
chore: <description>             # 构建/配置/杂项
docs: <description>              # 文档
test(<scope>): <description>     # 测试
```

`<scope>` 为模块名，如 `order`、`product`、`payment`。

---

## 5 共享内核契约

所有模块必须复用以下共享内核定义，不得重复定义：

| 共享概念                        | 说明                                                                              |
| ------------------------------- | --------------------------------------------------------------------------------- |
| `Money` 值对象                  | 金额 + 币种（ISO 4217），四舍五入到两位小数，运算符重载                           |
| `Entity` 基类                   | Id (Guid)、CreatedAt、UpdatedAt、Version (乐观锁)                                 |
| `AggregateRoot` 基类            | 继承 Entity，持有 `_domainEvents` 列表，提供 `AddDomainEvent`/`ClearDomainEvents` |
| `IDomainEvent` 接口             | EventId、OccurredAt、AggregateId                                                  |
| `IIntegrationEvent` 接口        | EventId、OccurredAt、IdempotencyKey                                               |
| `IRepository<T>` 泛型接口       | GetByIdAsync、AddAsync、UpdateAsync                                               |
| `IUnitOfWork` 接口              | SaveChangesAsync、BeginTransactionAsync                                           |
| `BaseDbContext`                 | 审计字段自动填充、软删除过滤器、乐观锁拦截器                                      |
| `OutboxMessage` 实体            | 发件箱模式数据载体                                                                |
| `IEventBus` 接口                | PublishAsync、SubscribeAsync                                                      |
| `IFileStorageService` 接口      | UploadAsync、DownloadAsync、DeleteAsync、ValidateUrl、ExistsAsync                 |
| `IExternalChannelOptions`       | 外部渠道配置抽象契约                                                              |
| `DomainException`               | 领域异常基类，携带错误码，映射 HTTP 状态码                                        |
| `ApiResponse<T>`                | 统一响应格式 `{ code, message, data }`                                            |
| `PageRequest` / `PageResult<T>` | 分页值对象                                                                        |

---

## 6 模块清单与依赖关系

### 6.1 模块清单

| #   | 模块               | 限界上下文    | 任务数 | 任务文件                          |
| --- | ------------------ | ------------- | ------ | --------------------------------- |
| 0   | 共享内核与基础设施 | Shared Kernel | 10     | `docs/tasks/shared-kernel.md`     |
| 1   | 用户与认证授权域   | BC1           | 10     | `docs/tasks/user-auth.md`         |
| 2   | 商品域             | BC2           | 11     | `docs/tasks/product.md`           |
| 3   | 购物车域           | BC3           | 6      | `docs/tasks/cart.md`              |
| 4   | 订单与交易域       | BC4           | 12     | `docs/tasks/order.md`             |
| 5   | 促销域             | BC5           | 10     | `docs/tasks/promotion.md`         |
| 6   | 评价与售后域       | BC6           | 9      | `docs/tasks/review-aftersales.md` |
| 7   | 积分与会员域       | BC7           | 11     | `docs/tasks/points-membership.md` |
| 8   | 支付集成域         | BC8           | 10     | `docs/tasks/payment.md`           |
| 9   | 消息通知域         | BC9           | 10     | `docs/tasks/notification.md`      |
| 10  | 卖家与店铺管理域   | BC10          | 8      | `docs/tasks/seller-shop.md`       |
| 11  | 系统管理域         | BC11          | 13     | `docs/tasks/system-admin.md`      |

**合计**: 12 个模块 / 120 个任务

### 6.2 依赖关系图

```
shared-kernel (0)
    ├── user-auth (1) ─────────────────┐
    ├── seller-shop (10) ──────────────┤
    │       │                           │
    │       ▼                           │
    ├── product (2) ◄── seller-shop     │
    │       │                           │
    │       ▼                           │
    ├── cart (3) ◄── product            │
    │                                   │
    ├── promotion (5)                   │
    ├── points-membership (7)           │
    │       │                           │
    │       ▼                           │
    ├── order (4) ◄── product, cart,    │
    │                 promotion,        │
    │                 points, user-auth │
    │       │                           │
    │       ▼                           │
    ├── payment (8) ◄── order           │
    │       │                           │
    │       ▼                           │
    ├── review-aftersales (6)           │
    │     ◄── order, payment            │
    │                                   │
    ├── notification (9) ◄── 所有域事件  │
    └── system-admin (11) ◄── 审计日志   │
```

### 6.3 跨上下文集成事件清单

以下事件契约定义在共享内核 `Leno.SharedContracts` 中，各模块发布或消费时必须严格遵循字段定义：

| 事件                               | 发布方     | 消费方                                        |
| ---------------------------------- | ---------- | --------------------------------------------- |
| `UserRegisteredEvent`              | 用户域     | 积分域、通知域                                |
| `ProductPublishedEvent`            | 商品域     | 卖家域、ES 读库                               |
| `ProductTakenDownEvent`            | 商品域     | 购物车域、卖家域、ES 读库                     |
| `OrderCreatedEvent`                | 订单域     | 购物车域、促销域、积分域、通知域、MQ 延迟消息 |
| `OrderPaidEvent`                   | 订单域     | 积分域、促销域、卖家域、通知域                |
| `OrderShippedEvent`                | 订单域     | 通知域                                        |
| `OrderCompletedEvent`              | 订单域     | 评价域、积分域、卖家域、MQ 延迟消息           |
| `OrderCancelledEvent`              | 订单域     | 积分域、促销域、通知域                        |
| `OrderAfterSalesWindowClosedEvent` | 订单域     | 积分域                                        |
| `PaymentRequestedIntegrationEvent` | 订单域     | 支付集成域                                    |
| `PaymentSucceededEvent`            | 支付域     | 订单域、积分域、促销域                        |
| `PaymentFailedEvent`               | 支付域     | 订单域                                        |
| `RefundRequestedIntegrationEvent`  | 售后域     | 支付集成域                                    |
| `RefundSucceededEvent`             | 支付域     | 售后域                                        |
| `RefundCompletedEvent`             | 售后域     | 订单域、通知域、系统管理域                    |
| `StockReservedEvent`               | 订单域     | 商品域                                        |
| `StockConfirmedEvent`              | 订单域     | 商品域                                        |
| `StockReleasedEvent`               | 订单域     | 商品域                                        |
| `StockAdjustedEvent`               | 商品域     | 订单域                                        |
| `PointsEarnedEvent`                | 积分域     | 通知域                                        |
| `PointsFrozenEvent`                | 积分域     | —                                             |
| `PointsConfirmedEvent`             | 积分域     | —                                             |
| `PointsReleasedEvent`              | 积分域     | —                                             |
| `MemberLevelUpgradedEvent`         | 积分域     | 通知域                                        |
| `MembershipActivatedEvent`         | 积分域     | 通知域                                        |
| `ShopApprovedEvent`                | 卖家域     | 用户域、通知域                                |
| `ShopSuspendedEvent`               | 卖家域     | 商品域、通知域                                |
| `ShopResumedEvent`                 | 卖家域     | 商品域                                        |
| `ShopClosedEvent`                  | 卖家域     | 商品域、用户域                                |
| `ReviewSubmittedEvent`             | 评价域     | 商品域                                        |
| `AfterSalesApprovedEvent`          | 售后域     | 通知域、系统管理域                            |
| `AnnouncementPublishedEvent`       | 系统管理域 | 通知域                                        |
| `FeatureFlagChangedEvent`          | 系统管理域 | 各域                                          |
| `ConfigChangedEvent`               | 系统管理域 | 各域                                          |
| `SeckillOrderCreatedEvent`         | 促销域     | 通知域                                        |

---

## 7 执行策略

### 7.1 七阶段开发顺序

按依赖关系分七个阶段推进，每阶段内的模块可并行开发：

| 阶段   | 模块                                  | 里程碑               | 并行性 |
| ------ | ------------------------------------- | -------------------- | ------ |
| 阶段一 | shared-kernel (0)                     | M1: 基础设施就绪     | 独立   |
| 阶段二 | user-auth (1) + seller-shop (10)      | M2: 身份与店铺就绪   | 可并行 |
| 阶段三 | product (2) + cart (3)                | M3: 商品与购物车可用 | 可并行 |
| 阶段四 | promotion (5) + points-membership (7) | —                    | 可并行 |
| 阶段五 | order (4)                             | M4: 交易闭环         | 独立   |
| 阶段六 | payment (8) + review-aftersales (6)   | M5: 支付与售后完整   | 可并行 |
| 阶段七 | notification (9) + system-admin (11)  | M6: 全功能可用       | 可并行 |

### 7.2 主 Agent 调度协议

```
对于每个阶段：
  1. 确认上一阶段所有模块已完成并通过质量门禁
  2. 读取该阶段各模块的任务文件 (docs/tasks/<module-name>.md)
  3. 为每个模块生成一个 Module Agent，传入：
     - 模块名与限界上下文编号
     - 任务文件路径
     - 依赖模块的接口契约（已完成模块的接口定义）
     - 编码规范与共享内核契约
  4. 并行调度同阶段的不互相依赖的 Module Agent
  5. 等待所有 Module Agent 完成
  6. 生成 Integration Agent 编写跨模块集成测试
  7. 生成 Review Agent 审查代码质量
  8. 更新 progress.md 中的 Checklist
  9. 进入下一阶段
```

### 7.3 Module Agent 执行协议

每个 Module Agent 接收一个模块的任务文件，按以下流程自主执行：

```
对于任务文件中的每个 Task：
  1. 阅读 Task 描述，明确要创建/修改的文件列表
  2. 按 Checklist 逐项执行子任务：
     a. 创建领域层文件（聚合根、值对象、领域服务、仓储接口、领域事件）
     b. 创建基础设施层文件（EF Core 仓储、Redis 缓存、ES 读模型、事件消费者）
     c. 创建应用层文件（应用服务、DTO、命令/查询处理器、校验器）
     d. 创建表现层文件（控制器、中间件、Program.cs 配置）
  3. 每完成一个子任务，运行编译验证
  4. 编写单元测试覆盖聚合行为、状态机、不变量
  5. 编写集成测试验证仓储 CRUD、事件消费、API 端点
  6. 全部子任务完成后，运行该模块全部测试
  7. 按 Commit 规范提交代码
  8. 勾选任务文件中对应 Checklist 项
  9. 向主 Agent 报告：模块完成状态、新增接口契约、发布/消费的事件列表
```

### 7.4 质量门禁

每个模块标记为"完成"前，必须通过以下检查：

| 检查项     | 命令                                          | 通过标准                                                   |
| ---------- | --------------------------------------------- | ---------------------------------------------------------- |
| 编译       | `dotnet build`                                | 0 errors, 0 warnings                                       |
| 单元测试   | `dotnet test --filter "Category=Unit"`        | 全部通过                                                   |
| 集成测试   | `dotnet test --filter "Category=Integration"` | 全部通过                                                   |
| DDD 合规   | Review Agent 审查                             | 领域层不引用技术框架；聚合根行为意图明确；仓储接口在领域层 |
| 事件契约   | Integration Agent 验证                        | 发布/消费的事件字段与共享内核契约一致                      |
| API 规范   | Swagger 生成                                  | RESTful 风格；统一响应格式；鉴权配置正确                   |
| 数据库迁移 | `dotnet ef database update`                   | 迁移脚本可正常执行                                         |

### 7.5 错误处理与恢复

| 场景                             | 处理策略                                                      |
| -------------------------------- | ------------------------------------------------------------- |
| 编译失败                         | Module Agent 自行修复，最多重试 3 次，仍失败则向主 Agent 报告 |
| 测试失败                         | Module Agent 分析失败原因，修复代码或测试，重新运行           |
| 跨模块接口不匹配                 | 主 Agent 介入，协调双方对齐契约，必要时修改共享内核定义       |
| 依赖模块未完成                   | 主 Agent 确保阶段顺序，不允许跨阶段开发                       |
| 外部服务不可用（DB/Redis/ES/MQ） | 使用 Testcontainers 启动测试容器；Mock 外部支付/短信/邮件 API |

---

## 8 任务文件读取指南

每个模块的任务文件位于 `docs/tasks/<module-name>.md`，结构如下：

```markdown
# {模块名} 开发任务

> 限界上下文 / 技术栈 / 依赖 / 对应文档

## 模块概述

（该模块的业务范围与核心职责）

## Task N: {任务名}

**文件:** Create/Modify 的文件路径列表

- [ ] 子任务 1（具体可执行步骤）
- [ ] 子任务 2
- [ ] 提交：`feat(<scope>): <description>`
```

**读取规则**：

1. 按 Task 编号顺序执行，前置 Task 的产出是后续 Task 的依赖
2. 每个 `- [ ]` 是一个最小可执行步骤，完成后勾选为 `- [x]`
3. `**文件:**` 中列出的路径是必须创建或修改的文件，路径为相对项目根目录
4. `提交：` 后的命令是 Git 提交信息，每个 Task 完成后执行一次提交
5. 任务文件中的代码片段和字段定义是契约，实现时必须严格遵循

---

## 9 项目目录结构总览

```
leno/
├── Leno.sln
├── Directory.Build.props                    # 统一 Nullable Enable、TreatWarningsAsErrors
├── .editorconfig                            # 编码规范（命名、格式、分析器）
├── docker-compose.yml                       # 基础设施服务（SQL Server、Redis、RabbitMQ、ES）
├── docs/
│   ├── tasks/                               # 任务文件
│   │   ├── progress.md                      # 总体进度
│   │   ├── shared-kernel.md
│   │   ├── user-auth.md
│   │   ├── product.md
│   │   ├── cart.md
│   │   ├── order.md
│   │   ├── promotion.md
│   │   ├── review-aftersales.md
│   │   ├── points-membership.md
│   │   ├── payment.md
│   │   ├── notification.md
│   │   ├── seller-shop.md
│   │   └── system-admin.md
│   └── prompt.md                            # 本文件
├── src/
│   ├── BuildingBlocks/                      # 共享内核
│   │   ├── Leno.SharedKernel/
│   │   ├── Leno.SharedContracts/
│   │   └── Leno.Infrastructure/
│   └── Services/                            # 各限界上下文
│       ├── UserAuth/
│       │   ├── Leno.UserAuth.Domain/
│       │   ├── Leno.UserAuth.Application/
│       │   ├── Leno.UserAuth.Infrastructure/
│       │   └── Leno.UserAuth.Api/
│       ├── Product/
│       │   ├── Leno.Product.Domain/
│       │   ├── Leno.Product.Application/
│       │   ├── Leno.Product.Infrastructure/
│       │   └── Leno.Product.Api/
│       ├── Cart/
│       ├── Order/
│       ├── Promotion/
│       ├── ReviewAfterSales/
│       ├── PointsMembership/
│       ├── Payment/
│       ├── Notification/
│       ├── SellerShop/
│       └── SystemAdmin/
└── tests/
    └── Leno.IntegrationTests/               # 跨模块集成测试
```

---

## 10 Docker Compose 基础设施

主 Agent 在阶段一完成后，生成 `docker-compose.yml` 启动以下基础设施服务，供所有模块的开发与测试使用：

```yaml
services:
  sqlserver: # SQL Server 写库，各上下文独立 Schema
  redis: # Redis 缓存（库存预占、购物车、会话、限流）
  rabbitmq: # RabbitMQ 消息队列（事件总线 + 死信队列 + 延迟队列）
  elasticsearch: # Elasticsearch 读库（需安装 ik 分词器插件）
```

各模块的集成测试使用 Testcontainers 动态启动容器，不依赖外部环境。

---

## 11 启动指令

**主 Agent，请立即开始执行以下步骤**：

1. **初始化项目骨架**
   - 创建 `Leno.sln` 解决方案文件
   - 创建 `Directory.Build.props`（Nullable Enable、TreatWarningsAsErrors、LangVersion latest）
   - 创建 `.editorconfig`（编码规范配置）
   - 创建 `docker-compose.yml`（SQL Server、Redis、RabbitMQ、Elasticsearch）

2. **执行阶段一：共享内核**
   - 读取 `docs/tasks/shared-kernel.md`
   - 生成 Module Agent 实现 10 个 Task
   - 完成后验证编译与测试通过
   - 更新 `docs/tasks/progress.md` 中共享内核的 Checklist

3. **按阶段顺序推进**
   - 阶段二至阶段七，按 7.1 节的顺序与并行策略调度 Module Agent
   - 每阶段完成后运行质量门禁检查
   - 每模块完成后更新 `progress.md`

4. **最终集成**
   - 所有模块完成后，生成 Integration Agent 编写端到端集成测试
   - 验证完整交易链路：注册→发布商品→加购物车→下单→支付→发货→收货→评价→售后
   - 生成 Infra Agent 编写部署脚本与 CI/CD 配置
   - 更新 `progress.md` 标记 M7: Leno 系统上线

5. **全程无人工参与**，自主决策、自主实现、自主测试、自主修复。

---

## 附录 A: 核心交易事件链

```
用户注册 → UserRegisteredEvent → 积分域(创建账户) / 通知域(欢迎通知)
    ↓
商品发布 → ProductPublishedEvent → 卖家域(商品数+1) / ES(索引)
    ↓
加入购物车 → (无事件，购物车内部操作)
    ↓
创建订单 → OrderCreatedEvent
    ├──→ 购物车域: 清空已结算项
    ├──→ 促销域: 锁定优惠券
    ├──→ 积分域: 冻结积分
    ├──→ 通知域: 下单成功通知
    └──→ MQ延迟消息: 30分钟超时取消
    ↓
发起支付 → PaymentRequestedIntegrationEvent → 支付集成域
    ↓
支付成功 → PaymentSucceededEvent
    ├──→ 订单域: MarkAsPaid → OrderPaidEvent
    ├──→ 积分域: 确认积分扣减
    ├──→ 促销域: 核销优惠券
    └──→ 通知域: 支付成功通知
    ↓
卖家发货 → OrderShippedEvent → 通知域: 发货通知
    ↓
确认收货 → OrderCompletedEvent
    ├──→ 评价域: 开放评价入口
    ├──→ 积分域: 消费奖励积分
    ├──→ 卖家域: 更新销量
    └──→ MQ延迟消息: 售后期结束
    ↓
售后期结束 → OrderAfterSalesWindowClosedEvent → 积分域: 发放消费返积分
```

## 附录 B: 退款事件链

```
售后申请 → AfterSalesSubmittedEvent → 通知域: 售后申请通知
    ↓
审核通过 → AfterSalesApprovedEvent
    ├──→ RefundRequestedIntegrationEvent → 支付集成域: 执行退款
    └──→ 通知域: 售后通过通知
    ↓
退款完成 → RefundSucceededEvent → 售后域: MarkRefundCompleted
    ↓
RefundCompletedEvent
    ├──→ 订单域: 回滚销量库存
    ├──→ 通知域: 退款到账通知
    └──→ 系统管理域: 记录操作日志
```

## 附录 C: 店铺状态联动事件链

```
店铺暂停 → ShopSuspendedEvent → 商品域: 商品不可售 / 通知域: 暂停通知
店铺恢复 → ShopResumedEvent → 商品域: 商品恢复可售
店铺关闭 → ShopClosedEvent
    ├──→ 商品域: 下架全部商品
    └──→ 用户域: 移除卖家角色
```

## 附录 D: 需求文档索引

| 文档                                                              | 内容                                                                 |
| ----------------------------------------------------------------- | -------------------------------------------------------------------- |
| [00-需求文档总览与DDD架构.md](./spec/00-需求文档总览与DDD架构.md) | 战略设计、限界上下文、上下文映射、共享内核、分层规范                 |
| [01-用户与认证授权域.md](./spec/01-用户与认证授权域.md)           | 账户、认证、授权、地址功能点                                         |
| [02-商品域.md](./spec/02-商品域.md)                               | SPU/SKU、分类品牌、审核、搜索功能点                                  |
| [03-购物车域.md](./spec/03-购物车域.md)                           | 购物车聚合、匿名合并、价格计算功能点                                 |
| [04-订单与交易域.md](./spec/04-订单与交易域.md)                   | 订单状态机、库存、支付请求、CQRS 流程功能点                          |
| [05-促销域.md](./spec/05-促销域.md)                               | 优惠券、秒杀、满减功能点                                             |
| [06-评价与售后域.md](./spec/06-评价与售后域.md)                   | 评价、退货退款售后、退款请求功能点                                   |
| [07-积分与会员域.md](./spec/07-积分与会员域.md)                   | 积分账户、成长值等级、付费会员、任务中心功能点                       |
| [08-支付集成域.md](./spec/08-支付集成域.md)                       | 支付单、微信/支付宝渠道适配、退款、对账功能点                        |
| [09-消息通知集成.md](./spec/09-消息通知集成.md)                   | 通知模板、邮件/短信渠道适配、发送记录功能点                          |
| [10-模块化部署架构.md](./spec/10-模块化部署架构.md)               | 角色模块独立部署、网关路由、故障隔离方案                             |
| [11-卖家与店铺管理域.md](./spec/11-卖家与店铺管理域.md)           | 卖家入驻审核、店铺信息与资质、店铺状态管理功能点                     |
| [12-系统管理域.md](./spec/12-系统管理域.md)                       | 数据看板、死信队列管理、索引重建、审计日志、限流配置、健康监控功能点 |
