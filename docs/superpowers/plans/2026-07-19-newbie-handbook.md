# 新手友好系统开发手册实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 编写 Leno 电商平台新手友好系统开发手册（11 个 markdown 文件，约 61000 字），覆盖架构/代码/环境/部署/可观测性五大主题，作为团队长期参考。

**Architecture:** 按角色旅程组织 11 章（README + 10 章），术语首次出现行内解释（35 个核心术语），代码示例取自仓库实际代码并标注文件路径链接，调用链路图采用 mermaid。每章独立 Task，按依赖顺序实施：README → 第 1-10 章。

**Tech Stack:** Markdown + mermaid + GitHub-flavored markdown；引用 .NET 10 / C# 13 / EF Core / Redis / RabbitMQ / Consul / OpenTelemetry / Helm 等技术栈。

**关联 Spec:** `docs/superpowers/specs/2026-07-19-newbie-handbook-design.md`

**命名对齐说明（基于代码探查结果）：**
- `AddCartItemRequestValidator` → 实际类名 `AddCartItemDtoValidator`（项目用 Dto 后缀）
- `InventoryReserveService` → 实际类名 `RedisInventoryRepository`（位于 Order BC 仓储层）
- `TokenRevocationService` → 实际类名 `JwtBlacklistService`（仅 API Gateway 层实现）
- `PrometheusExtensions` → 实际类名 `GatewayMetricsService`（6 个网关指标定义在此）
- `ReadModelSyncConsumerBase<TEvent>` → 实际签名 `ReadModelSyncConsumerBase<TEvent, TReadModel>`（双泛型）
- `IInternalQueryService`（共享抽象） → 各 BC 独立 `IXxxInternalQueryService`（无共享抽象）
- `deploy/docker-compose.yml` → 实际路径 `docker-compose.yml`（项目根目录）
- ADR 目录：`docs/architecture/adr/` → 实际目录 `docs/decisions/`

---

## 文件结构映射

| 文件 | 行数预估 | 职责 | Task |
|---|---|---|---|
| `docs/handbook/README.md` | 约 600 | 手册入口、阅读路径、11 BC 速查表、35 术语速查表、配套文档索引 | Task 1 |
| `docs/handbook/01-project-overview.md` | 约 1200 | 业务定位、技术栈全景、仓库目录、解决方案组织、开发模式 | Task 2 |
| `docs/handbook/02-local-env-setup.md` | 约 1800 | 前置依赖、docker compose 一键启动、迁移、Consul KV 初始化、验证 | Task 3 |
| `docs/handbook/03-architecture-overview.md` | 约 2100 | DDD 战略/战术、共享内核、分层架构、CQRS、微服务部署 | Task 4 |
| `docs/handbook/04-code-patterns.md` | 约 2400 | BC 四层结构、命名规范、聚合根/AppService/Controller/Repo/测试模板 | Task 5 |
| `docs/handbook/05-cross-bc-communication.md` | 约 2400 | 通信总览、集成事件、Outbox、防腐层、gRPC 双轨、熔断器、Internal API | Task 6 |
| `docs/handbook/06-storage-and-cache.md` | 约 1500 | 分库策略、EF Core 配置、迁移规范、Redis 三防、ES 读模型、分布式锁 | Task 7 |
| `docs/handbook/07-security-and-auth.md` | 约 1350 | JWT 认证、RBAC、Internal API 鉴权、gRPC 鉴权、敏感配置、输入验证、黑名单 | Task 8 |
| `docs/handbook/08-observability.md` | 约 1500 | 三支柱、Serilog、OpenTelemetry、Prometheus、健康检查、Grafana、Alertmanager | Task 9 |
| `docs/handbook/09-deployment-and-ops.md` | 约 1650 | Docker、compose、Helm、Consul、CI/CD、回滚、Runbook、故障排查 | Task 10 |
| `docs/handbook/11-onboarding-checklist.md` | 约 900 | 五日上手清单、首个 PR、进阶路径 | Task 11 |

---

## Task 1: README.md（手册入口与索引）

**Files:**
- Create: `docs/handbook/README.md`

- [ ] **Step 1: 创建 docs/handbook/ 目录与 README.md**

写入以下完整内容（约 600 行）：

````markdown
# Leno 电商平台系统开发手册

> **版本**：v1.0  ·  **代码版本**：feat-project-optimization-plan-O7ECNx 分支  ·  **更新日期**：2026-07-19

## 手册定位

本手册是 Leno 电商平台的新手友好系统开发手册，面向**会 C# 与 .NET 但不了解 DDD、微服务、容器化等概念**的新手 .NET 开发。手册自成体系，读者无需跳转到 spec/ADR/Runbook 等外部文档即可理解系统全貌。

## 读者画像

- 刚入职的 .NET 开发，会 C# 与 .NET，但不了解 DDD、微服务、容器化等概念
- 需要在 1-2 周内独立承担一个 BC（Bounded Context，限界上下文，详见第 3 章）的开发任务
- 需要查阅架构、代码、环境、部署、可观测性五大主题

## 阅读路径建议

### 一周深度学习路径（推荐）

| 天 | 章节 | 目标 |
|---|---|---|
| Day 1 | 第 1-2 章 | 理解项目定位 + 搭建本地环境 |
| Day 2 | 第 3 章 | 理解 DDD 与架构总览 |
| Day 3 | 第 4 章 | 掌握代码组织与开发模式 |
| Day 4 | 第 5 章 | 掌握跨 BC 通信 |
| Day 5 | 第 6-7 章 | 掌握存储缓存与安全认证 |
| Day 6 | 第 8-9 章 | 掌握可观测性与部署运维 |
| Day 7 | 第 10 章 | 完成上手清单 + 提交首个 PR |

### 一天快速浏览路径

阅读 README → 第 1 章 1.1-1.3 → 第 3 章 3.1-3.4 → 第 4 章 4.1-4.5 → 第 10 章

### 按需查询路径

直接跳转到目标章节，每章含"术语速查"与"要点回顾"可独立查阅。

## 11 个限界上下文速查表

| # | 中文 | 英文 | 主要职责 | 主要聚合根 | 服务端口 |
|---|---|---|---|---|---|
| 1 | 商品 | Product | SPU/SKU 管理、上下架 | Product/Sku | 5101 |
| 2 | 促销 | Promotion | 优惠券/活动 | Promotion/Coupon | 5102 |
| 3 | 购物车 | Cart | 购物车 | Cart | 5103 |
| 4 | 积分 | Points | 积分账户/会员等级 | PointsAccount | 5104 |
| 5 | 用户 | User | 账户/地址/OAuth2 | User | 5105 |
| 6 | 订单 | Order | 订单交易 | Order | 5106 |
| 7 | 支付 | Payment | 支付单/对账 | Payment | 5107 |
| 8 | 店铺 | SellerShop | 卖家店铺 | Shop | 5108 |
| 9 | 评价售后 | ReviewAfterSales | 评价/售后单 | Review/AfterSales | 5109 |
| 10 | 通知 | Notification | 消息推送 | Notification | 5110 |
| 11 | 网关 | BFF | 聚合 + JWT 验签 | （无聚合） | 8080 |

> 注：BC（Bounded Context，限界上下文）是 DDD 中领域模型的显式边界，每个上下文内部拥有独立的聚合、统一语言与持久化模型。详见第 3 章第 1 节。

## 35 个核心术语速查表

| 术语 | 中文 | 首次出现章节 | 简释 |
|---|---|---|---|
| DDD | 领域驱动设计 | 第 1 章 | 将业务逻辑内聚于领域层、通过限界上下文划分系统边界的方法论 |
| BC | 限界上下文 | 第 1 章 | 领域模型的显式边界 |
| 微服务 | Microservices | 第 1 章 | 将单体拆分为多个独立部署的小服务 |
| BFF | Backend for Frontend | 第 1 章 | 为前端定制的后端聚合层 |
| SAGA | 长事务 | 第 1 章 | 跨服务最终一致的事务模式 |
| RESTful | REST 风格 API | 第 1 章 | 基于 HTTP 语义的资源接口风格 |
| SPA | 单页应用 | 第 1 章 | 单页面渲染的前端应用 |
| Docker | 容器运行时 | 第 2 章 | 容器化运行时引擎 |
| 容器 | Container | 第 2 章 | 运行中的镜像实例 |
| 镜像 | Image | 第 2 章 | 容器只读模板 |
| docker compose | 多容器编排工具 | 第 2 章 | 多容器编排工具 |
| mise | 版本管理器 | 第 2 章 | 跨语言运行时版本管理器 |
| healthcheck | 健康检查 | 第 2 章 | 容器健康探针 |
| 上下文映射 | Context Map | 第 3 章 | 描述 BC 之间关系的图 |
| 共享内核 | Shared Kernel | 第 3 章 | 多 BC 共享的代码与模型 |
| 聚合根 | Aggregate Root | 第 3 章 | 聚合对外唯一入口 |
| 实体 | Entity | 第 3 章 | 有唯一标识的领域对象 |
| 值对象 | Value Object | 第 3 章 | 无标识、不可变、可比较 |
| 领域服务 | Domain Service | 第 3 章 | 跨实体的业务逻辑 |
| 领域事件 | Domain Event | 第 3 章 | 聚合内发生的事实 |
| 集成事件 | Integration Event | 第 3 章 | 跨 BC 传播的事实 |
| 仓储 | Repository | 第 3 章 | 聚合持久化的抽象 |
| 工厂 | Factory | 第 3 章 | 创建复杂聚合 |
| CQRS | 读写职责分离 | 第 3 章 | 将写操作与读操作分离到不同模型 |
| 防腐层 | ACL | 第 3 章 | 隔离外部模型变化的翻译层 |
| 分层架构 | Layered Architecture | 第 4 章 | 按职责分层每层只与直接下层交互 |
| 依赖倒置 | DIP | 第 4 章 | 高层不依赖低层，二者都依赖抽象 |
| FluentValidation | 验证库 | 第 4 章 | .NET 强类型验证库 |
| Testcontainers | 容器化测试 | 第 4 章 | 用 Docker 容器提供真实依赖 |
| Outbox 模式 | Outbox Pattern | 第 5 章 | 将消息持久化与业务事务同库提交 |
| 事件总线 | Event Bus | 第 5 章 | 异步消息发布订阅中间件 |
| RabbitMQ | 消息队列 | 第 5 章 | AMQP 消息中间件 |
| MassTransit | .NET 总线库 | 第 5 章 | .NET 消息总线抽象 |
| Polly | 弹性库 | 第 5 章 | .NET 重试/熔断/超时库 |
| gRPC | Google RPC | 第 5 章 | 基于 HTTP/2 + Protobuf 的高性能 RPC |
| Protobuf | Protocol Buffers | 第 5 章 | Google 二进制序列化格式 |
| 熔断器 | Circuit Breaker | 第 5 章 | 失败累积到阈值自动切断调用的开关 |
| 服务发现 | Service Discovery | 第 5 章 | 服务自动注册与发现机制 |
| Consul KV | KV 配置中心 | 第 5 章 | Consul 键值配置存储 |
| EF Core | ORM 框架 | 第 6 章 | .NET 官方 ORM |
| Code First | 代码先行 | 第 6 章 | 先写实体类再生成数据库结构 |
| Redis | 键值缓存 | 第 6 章 | 基于内存的高性能键值存储 |
| 缓存穿透 | Cache Penetration | 第 6 章 | 查询不存在的 key 反复打到 DB |
| 缓存击穿 | Cache Breakdown | 第 6 章 | 热点 key 失效瞬间大量请求打到 DB |
| 缓存雪崩 | Cache Avalanche | 第 6 章 | 大量 key 同时失效 |
| Elasticsearch | 全文检索引擎 | 第 6 章 | 基于 Lucene 的分布式搜索 |
| 读模型 | Read Model | 第 6 章 | 为查询优化的数据视图 |
| JWT | JSON Web Token | 第 7 章 | 紧凑的自包含令牌格式 |
| OAuth2 | 开放授权 | 第 7 章 | 第三方授权协议 |
| RBAC | 基于角色的访问控制 | 第 7 章 | 权限授予角色而非用户 |
| Claims | 声明 | 第 7 章 | 令牌中的键值对声明 |
| XSS | 跨站脚本攻击 | 第 7 章 | 注入恶意脚本到网页 |
| CSRF | 跨站请求伪造 | 第 7 章 | 诱导已登录用户发起非自愿请求 |
| 可观测性三支柱 | Three Pillars | 第 8 章 | 日志/追踪/指标 |
| Serilog | 结构化日志库 | 第 8 章 | .NET 流行结构化日志库 |
| OpenTelemetry | 可观测性标准 | 第 8 章 | CNCF 主推的可观测性标准 |
| Jaeger | 分布式追踪后端 | 第 8 章 | 开源 Trace 存储与查询 |
| Prometheus | 指标采集 | 第 8 章 | 时间序列指标数据库 |
| Grafana | 指标可视化 | 第 8 章 | 开源指标可视化平台 |
| Alertmanager | 告警管理 | 第 8 章 | Prometheus 告警管理组件 |
| Helm | K8s 包管理 | 第 9 章 | Kubernetes 包管理工具 |
| Chart | Helm 包 | 第 9 章 | 含模板+配置+元数据的 Helm 包 |
| K8s | Kubernetes | 第 9 章 | 容器编排平台 |
| HPA | 水平 Pod 自动扩缩 | 第 9 章 | HorizontalPodAutoscaler |
| CI/CD | 持续集成/持续部署 | 第 9 章 | 自动化构建测试部署 |
| 蓝绿部署 | Blue-Green | 第 9 章 | 两套环境切换发布 |
| 金丝雀发布 | Canary Release | 第 9 章 | 逐步将流量导到新版本 |
| Runbook | 运维手册 | 第 9 章 | 常见操作步骤与故障处理流程 |
| PR | Pull Request | 第 10 章 | 代码合并请求 |
| Conventional Commits | 约定式提交 | 第 10 章 | type(scope): subject 提交规范 |

## 配套文档索引

本手册独立完整，但读者深入了解时可参考以下仓库内文档：

| 文档 | 位置 | 用途 |
|---|---|---|
| 编码规范 | `docs/编码规范.md` | 详细命名与编码规则 |
| 命名规范 | `docs/conventions/naming-conventions.md` | 命名规范细化 |
| 内部 API 契约 | `docs/contracts/internal-api-contracts.md` | 12 条 Internal API 清单 |
| 防腐层模式 | `docs/architecture/anticorruption-pattern.md` | 防腐层深度说明 |
| ADR 决策记录 | `docs/decisions/` | 7 个架构决策记录 |
| Runbook | `docs/runbooks/` | 运维手册 |
| 需求文档 | `docs/spec/` | 13 篇需求文档 |
| 技术选型 | `docs/技术选型方案.md` | 技术选型说明 |
| spec 文档 | `docs/superpowers/specs/` | 设计 spec |
| plan 文档 | `docs/superpowers/plans/` | 实施 plan |

## 章节索引

- [第 1 章 项目概览](01-project-overview.md)
- [第 2 章 本地环境搭建](02-local-env-setup.md)
- [第 3 章 架构总览](03-architecture-overview.md)
- [第 4 章 代码组织与开发模式](04-code-patterns.md)
- [第 5 章 跨 BC 通信](05-cross-bc-communication.md)
- [第 6 章 数据存储与缓存](06-storage-and-cache.md)
- [第 7 章 安全与认证](07-security-and-auth.md)
- [第 8 章 可观测性](08-observability.md)
- [第 9 章 部署与运维](09-deployment-and-ops.md)
- [第 10 章 新人上手清单](10-onboarding-checklist.md)

## 反馈与维护

- 发现错误或建议改进：提 PR 修改对应章节
- 代码示例失效：示例代码标注文件路径链接，便于读者交叉验证
- 手册版本与代码版本对应关系见文件头
````

- [ ] **Step 2: 提交**

```bash
git add docs/handbook/README.md
git commit -m "docs(handbook): 新增 README 入口与 35 术语速查表"
```

---

## Task 2: 第 1 章 项目概览

**Files:**
- Create: `docs/handbook/01-project-overview.md`

- [ ] **Step 1: 写入第 1 章完整内容（约 1200 行）**

按 spec 第 4 节大纲写入，包含：

1. 章首固定元素：学习目标（4 条）+ 适用读者（全角色）+ 术语速查（DDD/BC/微服务/SAGA/BFF/RESTful/SPA）
2. 1.1 业务定位（约 800 字）：B2C 电商平台 + 4 类角色 + 8 项核心目标 + 业务术语表（SPU/SKU/订单状态机/支付单/积分/优惠券等 10 项）
3. 1.2 技术栈全景图（约 800 字）：9 类技术栈表 + 每类行内术语解释
4. 1.3 仓库目录结构详解（约 1200 字）：顶层目录树 + BuildingBlocks 三子目录 + Services 11 BC 命名规则 + Cart BC 完整结构示例
5. 1.4 解决方案组织（约 600 字）：Leno.slnx + Directory.Build.props + Directory.Packages.props + 项目引用规则图
6. 1.5 开发模式概览（约 600 字）：Subagent-Driven + Conventional Commits + PR 模板 + check-placeholders.sh + 11 条硬约束概览
7. 章末固定元素：要点回顾（8 条）+ 常见问题（5 个 Q&A）+ 下一章衔接

**关键内容要求**：

- 1.1 业务定位需列出 8 项核心业务目标：商品管理、订单交易、支付结算、营销促销、用户中心、积分会员、评价售后、店铺运营
- 1.2 技术栈表 9 类：后端/.NET 10、数据/SQL Server+EF Core、缓存/Redis、消息/RabbitMQ+MassTransit、搜索/Elasticsearch、网关/YARP、服务发现/Consul、可观测性/OpenTelemetry+Serilog+Jaeger+Prometheus+Grafana、部署/Docker+Helm+K8s
- 1.3 仓库目录树需完整展示 `src/BuildingBlocks/`（Leno.SharedKernel/Leno.SharedContracts/Leno.Infrastructure 三子目录）+ `src/Services/`（11 BC 命名规则 `Leno.{BC}.{层}`）+ Cart BC 完整四层目录树
- 1.5 Conventional Commits 格式：`type(scope): subject`，type 含 feat/fix/docs/refactor/test/chore
- 1.5 引用 `scripts/check-placeholders.sh`（占位符检查脚本）

**术语首次出现行内解释要求**（本章 7 个术语）：

- DDD（领域驱动设计，一种将业务逻辑内聚于领域层、通过限界上下文划分系统边界的方法论）
- BC（Bounded Context，限界上下文，领域模型的显式边界，每个上下文内部拥有独立的聚合、统一语言与持久化模型）
- 微服务（Microservices，将单体应用拆分为多个独立部署的小服务，每个服务围绕业务能力构建）
- BFF（Backend for Frontend，为前端定制的后端聚合层，负责聚合多个 BC 的数据并适配前端需求）
- SAGA（长事务，跨服务最终一致的事务模式，通过补偿操作回滚）
- RESTful（REST 风格 API，基于 HTTP 语义的资源接口风格，使用 GET/POST/PUT/DELETE 操作资源）
- SPA（Single Page Application，单页应用，单页面渲染的前端应用，通过 AJAX 与后端交互）

- [ ] **Step 2: 提交**

```bash
git add docs/handbook/01-project-overview.md
git commit -m "docs(handbook): 新增第 1 章项目概览"
```

---

## Task 3: 第 2 章 本地环境搭建

**Files:**
- Create: `docs/handbook/02-local-env-setup.md`

- [ ] **Step 1: 写入第 2 章完整内容（约 1800 行）**

按 spec 第 5 节大纲写入，包含：

1. 章首固定元素：学习目标（4 条）+ 适用读者（开发）+ 术语速查（Docker/容器/镜像/docker compose/mise/SDK/IDE/healthcheck/数据卷）
2. 2.1 前置依赖清单（约 500 字）：.NET 10 SDK 10.0.301+（mise 管理）、Docker Desktop/Engine、IDE 三选一（VS 2026/Rider/VS Code+C# Dev Kit）、Git 2.40+、mise 工具说明
3. 2.2 一键启动 docker compose（约 1500 字）：命令 `docker compose -f docker-compose.yml up -d`（注意：实际在项目根目录，非 deploy/）+ 8 个组件详解表（含 sqlserver/redis/rabbitmq/elasticsearch/consul/jaeger/prometheus/grafana）+ 行内术语解释（容器/镜像/docker compose）+ 启动后验证命令
4. 2.3 健康检查与日志查看（约 500 字）：每组件健康端点 + `docker compose ps` 解读 + 常见启动失败排查（端口占用/磁盘/内存）
5. 2.4 仅启动基础设施模式（约 600 字）：场景说明 + 命令 + IDE 配置（launchSettings.json 端口）+ 调试单个 BC 步骤（Cart BC 示例）
6. 2.5 连接字符串与凭据速查（约 400 字）：11 BC 数据库连接字符串模板 + appsettings.Development.json + dotnet user-secrets（行内解释）
7. 2.6 数据库迁移操作（约 800 字）：EF Core（行内解释）Code First 模式 + 添加/应用迁移命令（含 project/startup-project 参数）+ `MigrateWithLockAsync` 机制（基于 Redis 分布式锁）+ 迁移文件命名规范 + 11 BC Migrations/ 目录位置
8. 2.7 Consul KV 初始化（约 1000 字）：Consul（行内解释）概念 + `docs/consul-kv-seed.md` 文件说明 + 必须初始化的 KV 清单（4 类：internal-api-keys/cors/grpc-endpoints/use-grpc）+ 初始化命令（bash/PowerShell）+ 验证 + ConsulConfigWatcher 机制简介
9. 2.8 验证安装（约 700 字）：4 项验证步骤（网关 health/Swagger/Grafana/Jaeger）+ 单 BC 验证 + 故障排查清单（5 个常见问题）
10. 章末固定元素：要点回顾 + 常见问题 + 下一章衔接

**关键内容要求**：

- docker compose 路径：`docker-compose.yml`（项目根目录，非 `deploy/docker-compose.yml`）
- 8 个组件凭据（不脱敏）：
  - sqlserver: sa/Your_password123，端口 1433
  - redis: 无密码，端口 6379
  - rabbitmq: guest/guest，端口 5672/15672
  - elasticsearch: elastic/Your_password123，端口 9200
  - consul: 无 token，端口 8500
  - jaeger: 无凭据，端口 16686
  - prometheus: 无凭据，端口 9090
  - grafana: admin/admin，端口 3000
- 迁移命令示例（Cart BC）：
  ```bash
  dotnet ef migrations add AddItemRemark \
    --project src/Services/Cart/Leno.Cart.Infrastructure \
    --startup-project src/Services/Cart/Leno.Cart.Api \
    --output-dir Migrations
  ```
- Consul KV 4 类清单：
  - `leno/security/internal-key/{bc}`：11 个 BC 的 InternalApiKey
  - `leno/cors/origins`：CORS 白名单
  - `leno/grpc/endpoints/{bc}`：6 个 BC 的 gRPC 端点
  - `leno/anticorruption/use-grpc/{bc}`：6 个 BC 的 gRPC 开关
- 引用文件路径链接：`docker-compose.yml`、`docs/consul-kv-seed.md`、`src/BuildingBlocks/Leno.Infrastructure/Persistence/DatabaseMigrationExtensions.cs`（MigrateWithLockAsync 位置）

- [ ] **Step 2: 提交**

```bash
git add docs/handbook/02-local-env-setup.md
git commit -m "docs(handbook): 新增第 2 章本地环境搭建"
```

---

## Task 4: 第 3 章 架构总览

**Files:**
- Create: `docs/handbook/03-architecture-overview.md`

- [ ] **Step 1: 写入第 3 章完整内容（约 2100 行）**

按 spec 第 6 节大纲写入，包含：

1. 章首固定元素：学习目标（4 条）+ 适用读者（全角色）+ 术语速查（限界上下文/上下文映射/共享内核/聚合根/实体/值对象/领域服务/领域事件/集成事件/仓储/工厂/CQRS/防腐层/客户-供应商/遵奉者）
2. 3.1 DDD 战略设计（约 1800 字）：DDD 起源与核心思想 + 限界上下文概念 + 11 BC 划分表（含编号/中文/英文/职责/主要聚合根/服务端口）+ 上下文映射概念 + 6 类映射关系详解（共享内核/客户-供应商/遵奉者/防腐层/开放主机服务/各行其道）+ 上下文映射图（mermaid graph LR）
3. 3.2 DDD 战术设计（约 1500 字）：7 个战术概念 + 代码映射表 + 聚合设计 4 原则 + Cart 聚合根示例代码（来自 [Cart.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L31-L91)，含 Create 工厂方法、AddItem 行为方法、AddDomainEvent 调用，约 60 行）
4. 3.3 共享内核（约 800 字）：共享内核概念 + `Leno.SharedKernel` 项目结构（Abstractions/ValueObjects/Exceptions）+ 使用规则 + 共享契约 vs 共享内核区别
5. 3.4 分层架构（约 1200 字）：分层架构概念 + Leno 四层架构图（mermaid graph TB）+ 每层职责详解 + 依赖方向规则 + 依赖倒置原则（DIP，行内解释）+ 项目引用关系图（mermaid）
6. 3.5 CQRS 读写分离（约 1000 字）：CQRS 概念 + Leno 实现（Command 侧 vs Query 侧）+ `IQueryHandler<TQuery, TResult>` 接口示例代码（来自 [IQueryHandler.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure.Abstractions/Cqrs/IQueryHandler.cs)）+ DI 反射注册说明 + ReadModel 同步机制 + `[Obsolete]` 迁移策略
7. 3.6 微服务部署架构（约 700 字）：微服务概念 + 11 微服务独立性 + 4 类角色端 + 故障隔离原则
8. 3.7 模块化部署拓扑图（约 500 字）：mermaid graph 全景图 + 部署单元划分 + 端口规划表
9. 章末固定元素：要点回顾 + 常见问题 + 下一章衔接

**关键内容要求**：

- 11 BC 划分表（与 README 一致）：Product/Promotion/Cart/Points/User/Order/Payment/SellerShop/ReviewAfterSales/Notification/BFF
- Cart 聚合根示例代码必须引用实际文件路径与行号：[Cart.cs#L31-L91](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L31-L91)
- IQueryHandler 接口路径：[IQueryHandler.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure.Abstractions/Cqrs/IQueryHandler.cs)
- AggregateRoot 基类路径：[AggregateRoot.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.SharedKernel/Abstractions/AggregateRoot.cs)
- ReadModelSyncConsumerBase 实际签名：`ReadModelSyncConsumerBase<TEvent, TReadModel>`（双泛型，非单泛型）
- 上下文映射 6 类关系需各配 1 个 Leno 实例（如共享内核=Leno.SharedKernel、客户-供应商=Order→Product、防腐层=Cart.ProductSnapshotAntiCorruption）

- [ ] **Step 2: 提交**

```bash
git add docs/handbook/03-architecture-overview.md
git commit -m "docs(handbook): 新增第 3 章架构总览"
```

---

## Task 5: 第 4 章 代码组织与开发模式

**Files:**
- Create: `docs/handbook/04-code-patterns.md`

- [ ] **Step 1: 写入第 4 章完整内容（约 2400 行）**

按 spec 第 7 节大纲写入，包含：

1. 章首固定元素：学习目标（4 条）+ 适用读者（开发）+ 术语速查（分层架构/依赖倒置/DTO/Validator/FluentValidation/单元测试/集成测试/Testcontainers/Mock/AAA 模式）
2. 4.1 BC 内四层项目结构（约 800 字）：Cart BC 完整目录树 + 每层职责与文件归属规则 + 测试项目命名约定
3. 4.2 命名规范（约 600 字）：接口/类/私有字段/DTO 后缀/异常后缀/错误码格式/防腐层客户端/gRPC 服务命名规则
4. 4.3 聚合根开发模板（约 1200 字）：聚合根行内解释 + 完整 Cart 代码示例（来自 [Cart.cs#L31-L91](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L31-L91)，含 Create 工厂方法、AddItem 行为方法、AddDomainEvent 调用）+ 4 条聚合设计原则 + 反例对比
5. 4.4 应用服务开发模板（约 1000 字）：应用服务行内解释 + CartAppService 代码示例（来自 [CartAppService.cs#L24-L50](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L24-L50)，含构造函数注入、async/await + CancellationToken、SaveEntitiesAsync）+ FluentValidation 行内解释 + Validator 代码示例（来自 [CartValidators.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Application/Validators/CartValidators.cs)，实际类名 `AddCartItemDtoValidator` 非 `AddCartItemRequestValidator`）+ 应用服务与 DTO 关系图（mermaid graph LR）
6. 4.5 Controller 开发模板（约 800 字）：路由约定 + JWT 授权 + ApiResponse 包装（来自 [ApiResponse.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.SharedContracts/Responses/ApiResponse.cs)）+ 错误码到 HTTP 状态码映射表（来自 [ErrorCodeMapping.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs)）+ Controller 完整代码示例（来自 [CartsController.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Api/Controllers/CartsController.cs)）
7. 4.6 仓储开发模板（约 800 字）：仓储行内解释 + `ICartRepository` 接口（来自 [ICartRepository.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Domain/Repositories/ICartRepository.cs)）+ `EfCoreCartRepository` 实现（来自 [EfCoreCartRepository.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/EfCoreCartRepository.cs)）+ `BaseDbContext` 公共特性（来自 [BaseDbContext.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs)）+ EF Core 配置类示例（来自 [CartConfiguration.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Infrastructure/Configurations/CartConfiguration.cs)）
8. 4.7 单元测试模板（约 1200 字）：单元测试行内解释 + 技术栈（xUnit + FluentAssertions + Moq）行内解释 + AAA 模式行内解释 + 测试命名约定 + 完整测试代码示例（来自 [CartTests.cs#L29-L40](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Domain.Tests/CartTests.cs#L29-L40)）+ 覆盖率要求
9. 4.8 集成测试模板（约 1200 字）：集成测试行内解释 + 技术栈（Testcontainers + MassTransit TestHarness）行内解释 + ContainerFixture 示例（来自 [ContainerFixture.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Testing/Fixtures/ContainerFixture.cs)）+ CrossBcIntegrationTestBase 基类（来自 [CrossBcIntegrationTestBase.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Testing/Fixtures/CrossBcIntegrationTestBase.cs)）+ 完整集成测试示例 + 测试金字塔
10. 4.9 一个完整 PR 示例（约 400 字）：场景（为 CartItem 添加 Remark 字段）+ 6 步骤清单 + Conventional Commits 示例
11. 章末固定元素：要点回顾 + 常见问题 + 下一章衔接

**关键内容要求**：

- Validator 实际类名：`AddCartItemDtoValidator`（非 `AddCartItemRequestValidator`）
- Controller 实际类名：`CartsController`（复数，非 `CartController`）
- Controller 基类：`CartControllerBase`（含 `GetCurrentUserId()` 辅助方法）
- ApiResponse 实际结构：`Code`/`Message`/`Data`/`TraceId`（非 `Success`/`ErrorCode`）
- ErrorCodeMapping 实际后缀规则：`_NOT_FOUND`=404、`_ALREADY_`=409、`_EXISTS`=409、`_CONFLICT`=409、`_FORBIDDEN`=403、`_UNAVAILABLE`=503、`_FAILED`=502
- CartTests 实际测试方法示例（AddItem_NewSku_ShouldAddToCart）使用 `CreateCart()` 辅助方法
- CartConfiguration 实际使用 snake_case 命名（`carts`/`user_id`/`ix_carts_user_id`）

- [ ] **Step 2: 提交**

```bash
git add docs/handbook/04-code-patterns.md
git commit -m "docs(handbook): 新增第 4 章代码组织与开发模式"
```

---

## Task 6: 第 5 章 跨 BC 通信

**Files:**
- Create: `docs/handbook/05-cross-bc-communication.md`

- [ ] **Step 1: 写入第 5 章完整内容（约 2400 行）**

按 spec 第 8 节大纲写入，包含：

1. 章首固定元素：学习目标（5 条）+ 适用读者（开发）+ 术语速查（Outbox 模式/事件总线/RabbitMQ/MassTransit/Topic Exchange/死信队列/Polly/gRPC/Protobuf/.proto/熔断器/降级/服务发现/Consul KV/Internal API/X-Internal-Key）
2. 5.1 通信方式总览（约 500 字）：同步 vs 异步行内解释 + Leno 两类通信 + 11 BC 通信关系矩阵表
3. 5.2 集成事件 vs 领域事件（约 800 字）：领域事件行内解释 + 集成事件行内解释 + 4 条规则 + 代码示例对比 + 事件流转链路图
4. 5.3 Outbox 模式详解（约 1500 字）：Outbox 模式行内解释 + 为何需要 + OutboxMessage 表结构（来自 [OutboxMessage.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessage.cs)，实际字段：Id/Type/Payload/Status/SchemaVersion）+ `IUnitOfWork.SaveEntitiesAsync` 流程（来自 [EfCoreUnitOfWork.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs)）+ OutboxPublisher 代码示例（来自 [OutboxPublisher.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs)，含两阶段标记 Pending→Publishing→Processed + 并行发布 DOP=4 + 积压告警阈值 100）+ 类型解析
5. 5.4 防腐层概念与 AntiCorruptionBase 基类（约 800 字）：防腐层行内解释 + Leno 防腐层架构图（mermaid graph LR）+ AntiCorruptionBase 代码示例（来自 [AntiCorruptionBase.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs)）+ AntiCorruptionMetrics 三个指标（来自 [AntiCorruptionMetrics.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs)）+ AntiCorruptionException 错误码规范
6. 5.5 HttpClient 防腐层实现模板（约 800 字）：ProductSnapshotAntiCorruptionService 完整代码示例（来自 [ProductSnapshotAntiCorruptionService.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Infrastructure/Services/ProductSnapshotAntiCorruptionService.cs)，含 ServiceName="product"、X-Internal-Key 头、调用 /api/v1/internal/products/skus/{id}/snapshot）+ DI 注册示例
7. 5.6 gRPC 双轨方案（约 1500 字）：gRPC 行内解释 + Protobuf 行内解释 + 为何需要 gRPC + 双轨方案设计动机 + UseGrpc 开关机制 + AntiCorruptionDispatcher 调度器代码示例（来自 [AntiCorruptionDispatcher.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs)，含 UseGrpc 检查 + 熔断器状态检查 + gRPC 失败降级到 HttpClient）+ 适配器模式行内解释 + GrpcCartPriceService 代码示例（来自 [GrpcCartPriceService.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcCartPriceService.cs)）+ ConsulConfigWatcher 热更新
8. 5.7 熔断器三状态机（约 800 字）：熔断器行内解释 + 三状态机详解（mermaid stateDiagram-v2：Closed/Open/HalfOpen）+ CircuitBreakerState 代码示例（来自 [CircuitBreakerState.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs)，Keyed Singleton 按服务名隔离）+ gRPC 降级触发条件（仅基础设施不可用状态码）
9. 5.8 Internal API 契约（约 800 字）：12 条 Internal API 路由清单表（按 BC 分组：Product 3 条/Promotion 1 条/Points 1 条/User 2 条/Order 2 条/SellerShop 1 条/ReviewAfterSales 2 条）+ X-Internal-Key 头鉴权机制 + /v1/ 版本治理 + InternalApiKeyMiddleware 代码示例（来自 [InternalApiKeyMiddleware.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Middleware/InternalApiKeyMiddleware.cs)）
10. 5.9 gRPC 服务端开发模板（约 800 字）：CartGrpcService 完整代码示例（来自 [CartGrpcService.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs)）+ IInternalQueryService 抽象（实际：各 BC 独立 `IXxxInternalQueryService`，如 `ICartInternalQueryService`，无共享抽象）+ Program.cs 条件映射 + TestServerCallContext 单元测试模板
11. 5.10 跨 BC 通信调用链路图（约 700 字）：完整调用链路 mermaid sequence diagram（业务层→Adapter→Dispatcher→HttpClient/gRPC→对端 GrpcService→InternalQueryService→AppService→Repository→DB）+ 4 个关键节点日志埋点 + TraceId 跨 BC 传播 + 故障场景调用链路图（gRPC 失败降级到 HttpClient）
12. 章末固定元素：要点回顾 + 常见问题 + 下一章衔接

**关键内容要求**：

- OutboxMessage 实际字段：`Id`/`Type`/`Payload`/`Status`（OutboxMessageStatus 枚举：Pending/Publishing/Processed/DeadLetter）/`SchemaVersion`
- OutboxPublisher 实际参数：BatchSize=50、MaxRetryCount=5、PollingInterval=5 秒、PublishingStaleTimeout=5 分钟
- AntiCorruptionBase.ExecuteAsync 实际签名：`ExecuteAsync<T>(string operation, Func<CancellationToken, Task<T>> execute, CancellationToken ct)`
- AntiCorruptionException 错误码格式：`{SERVICE.ToUpperInvariant()}_UNAVAILABLE`
- AntiCorruptionDispatcher.ExecuteAsync 实际逻辑：检查 UseGrpc → 检查熔断器状态 → Open 时直接降级 → 调用 gRPC → 失败时 RecordFailure + 降级到 HttpClient
- CircuitBreakerState 实际方法：`GetState()`/`RecordSuccess()`/`RecordFailure()`，三状态 Closed/Open/HalfOpen
- InternalApiKeyMiddleware 实际逻辑：检查路径前缀 `/api/v1/internal` → 检查 X-Internal-Key 头 → FixedTimeEquals 防计时侧信道 → Development 环境可降级
- GrpcInternalKeyInterceptor 实际逻辑：检查 `x-internal-key` metadata → 不匹配抛 RpcException(Unauthenticated)
- 12 条 Internal API 清单需与 spec 第 5.8 节一致

- [ ] **Step 2: 提交**

```bash
git add docs/handbook/05-cross-bc-communication.md
git commit -m "docs(handbook): 新增第 5 章跨 BC 通信"
```

---

## Task 7: 第 6 章 数据存储与缓存

**Files:**
- Create: `docs/handbook/06-storage-and-cache.md`

- [ ] **Step 1: 写入第 6 章完整内容（约 1500 行）**

按 spec 第 9 节大纲写入，包含：

1. 章首固定元素：学习目标（5 条）+ 适用读者（开发）+ 术语速查（EF Core/Code First/Fluent API/迁移/乐观锁/软删除/Redis/布隆过滤器/缓存穿透/击穿/雪崩/双删一致性/Elasticsearch/读模型/Lua 脚本）
2. 6.1 数据库分库策略（约 800 字）：分库行内解释 + 11 独立数据库清单表 + BaseDbContext 公共特性（来自 [BaseDbContext.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs)，含 OutboxMessages DbSet/乐观锁 shadow property "Version"/软删除全局查询过滤器/ApplyConfigurationsFromAssembly）+ 3 大优势 + 跨库查询 3 种方案
3. 6.2 EF Core 配置（约 800 字）：EF Core 行内解释 + Fluent API 行内解释 + 配置类规范代码示例（来自 [CartConfiguration.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Infrastructure/Configurations/CartConfiguration.cs)，实际 snake_case 命名）+ 值对象映射 + 枚举映射 + IDesignTimeDbContextFactory
4. 6.3 数据库迁移规范（约 1400 字）：Code First 行内解释 + 迁移命令清单表 + 命令完整示例 + 迁移文件命名规范（实际格式 `yyyyMMddHHmmss_PascalCaseName.cs`，如 `20260717174927_InitialCreate`）+ "仅追加"原则 + 破坏性变更分版本灰度策略（3 阶段示例代码：AddItemRemarkNew→BackfillRemarkNew→RemoveRemarkOld）+ `MigrateWithLockAsync` 机制详解（来自 [DatabaseMigrationExtensions.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Persistence/DatabaseMigrationExtensions.cs)，实际基于 `IDistributedLockProvider` + lockKey=`db-migrate:{DbContextName}`）+ 11 BC Migrations/ 目录位置
5. 6.4 Redis 缓存（约 1500 字）：Redis 行内解释 + `ICacheService` 接口（来自 [ICacheService.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure.Abstractions/ICacheService.cs)，实际含 GetOrSetAsync/SetAsync/GetAsync/RemoveAsync/InvalidateWithDoubleDeleteAsync/InvalidatePatternAsync/PreWarmBloomFilterAsync 7 个方法）+ 三防策略表（穿透/击穿/雪崩）+ 3 张 mermaid 图（穿透/击穿/雪崩）+ 双删一致性 + 缓存键规范（`leno:{bc}:{role}:{shopId}:{resource}:{id}`）
6. 6.5 Elasticsearch 读模型（约 900 字）：Elasticsearch 行内解释 + CQRS 读写分离架构图（mermaid graph LR）+ 读模型行内解释 + `ReadModelSyncConsumerBase<TEvent, TReadModel>` 抽象基类（来自 [ReadModelSyncConsumerBase.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs)，实际双泛型，含 `BuildReadModelAsync` 抽象方法 + `BuildDeleteActionAsync` 虚方法）+ 5 个读模型清单表（ProductReadModel 含 15 字段，来自 [ProductReadModel.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModel.cs)）+ 11 Consumer 实现 + 索引重建 + 6 Query 示例
7. 6.6 分布式锁（约 500 字）：分布式锁行内解释 + 两个使用场景（数据库迁移 + 库存预占）+ 库存预占 Lua 脚本示例（来自 [RedisInventoryRepository.cs#L24-L54](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Order/Leno.Order.Infrastructure/Repositories/RedisInventoryRepository.cs#L24-L54)，实际 3 个脚本：ReserveLuaScript/ReleaseLuaScript/ConfirmLuaScript，Key 命名 `inventory:stock:{skuId}`/`inventory:reserved:{skuId}:{orderId}`）+ 锁超时与续期
8. 章末固定元素：要点回顾 + 常见问题 + 下一章衔接

**关键内容要求**：

- BaseDbContext 实际特性：乐观锁使用 shadow property `Version`（`IsRowVersion()`），软删除使用 `ApplySoftDeleteQueryFilters`，自动 `ApplyConfigurationsFromAssembly`
- CartConfiguration 实际使用 snake_case：`carts`/`user_id`/`ix_carts_user_id`，外键 `OnDelete(DeleteBehavior.Cascade)`
- MigrateWithLockAsync 实际实现：`IDistributedLockProvider.TryAcquireLockAsync(lockKey, timeout, ct)`，lockKey=`db-migrate:{typeof(TDbContext).Name}`
- ICacheService 实际方法（7 个）：GetOrSetAsync/SetAsync/GetAsync/RemoveAsync/InvalidateWithDoubleDeleteAsync/InvalidatePatternAsync/PreWarmBloomFilterAsync
- RedisInventoryRepository 实际类名（非 InventoryReserveService）
- Lua 脚本实际 3 个：ReserveLuaScript/ReleaseLuaScript/ConfirmLuaScript
- 3 张缓存 mermaid 图必须按 spec 第 9.2 节 6.4 节内容绘制

- [ ] **Step 2: 提交**

```bash
git add docs/handbook/06-storage-and-cache.md
git commit -m "docs(handbook): 新增第 6 章数据存储与缓存"
```

---

## Task 8: 第 7 章 安全与认证

**Files:**
- Create: `docs/handbook/07-security-and-auth.md`

- [ ] **Step 1: 写入第 7 章完整内容（约 1350 行）**

按 spec 第 10 节大纲写入，包含：

1. 章首固定元素：学习目标（5 条）+ 适用读者（开发+运维）+ 术语速查（JWT/OAuth2/RBAC/Claims/Bearer Token/环境变量/配置中心/CSRF/XSS/SQL 注入）
2. 7.1 认证体系（约 800 字）：认证 vs 授权行内解释 + JWT 行内解释 + JWT 三段结构图（mermaid graph）+ Leno JWT 无状态认证流程（mermaid sequence diagram）+ JwtTokenGenerator 代码示例（来自 [JwtTokenGenerator.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Auth/JwtTokenGenerator.cs)，实际 Claims：Sub/Jti/NameIdentifier/Role/role/shop_id，HS256 签名）+ JWT 优势
3. 7.2 授权体系（约 700 字）：RBAC 行内解释 + 4 类角色（buyer/seller/operation/admin）+ 角色权限矩阵表（4 角色 × 11 BC 完整表）+ Claims 提取代码示例 + 网关 JWT 本地验签机制 + 资源级授权（`[Authorize(Policy = "ShopOwner")]`）
4. 7.3 内部 API 鉴权（约 800 字）：内部 API 行内解释 + X-Internal-Key 头鉴权流程（3 步：从 Consul KV 读取/请求头携带/被调用方校验）+ 11 BC 独立 InternalApiKey 设计动机 + InternalApiKeyMiddleware 完整代码示例（来自 [InternalApiKeyMiddleware.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Middleware/InternalApiKeyMiddleware.cs)，实际逻辑：路径前缀检查 `/api/v1/internal` + X-Internal-Key 头检查 + FixedTimeEquals 防计时侧信道 + Development 环境降级 + 错误响应 JSON）+ `/api/v1/internal/*` 路由前缀约定 + 12 条 Internal API 路由清单
5. 7.4 gRPC 鉴权（约 500 字）：gRPC 鉴权机制（metadata 携带 `x-internal-key`）+ GrpcInternalKeyInterceptor 代码示例（来自 [GrpcInternalKeyInterceptor.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcInternalKeyInterceptor.cs)）+ gRPC 客户端注入 metadata + 与 HTTP 鉴权一致性
6. 7.5 敏感配置管理（约 700 字）：敏感配置行内解释 + 4 层配置优先级（环境变量/Consul KV/appsettings.{Env}.json/appsettings.json）+ 环境变量注入示例（docker-compose.yml）+ ValidateSensitiveConfig 启动校验机制（来自 [ConfigCenterExtensions.cs#L168-L214](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Configuration/ConfigCenterExtensions.cs#L168-L214)，实际为扩展方法，检查 SensitiveConfigKeys 13 项 + InternalApiKey 长度 ≥ 44）+ 4 类必须环境变量配置清单
7. 7.6 输入验证（约 600 字）：FluentValidation 规则示例（来自 [CartValidators.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Application/Validators/CartValidators.cs)，实际 `AddCartItemDtoValidator`）+ XSS 行内解释与防护 + SQL 注入行内解释与防护 + CSRF 行内解释与防护
8. 7.7 JWT 黑名单与令牌撤销（约 400 字）：JWT 黑名单场景 + Leno JWT 黑名单实现（Redis SET 存储被撤销的 `jti`）+ JwtBlacklistService 代码示例（来自 [JwtBlacklistService.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs)，实际类名 `JwtBlacklistService` 非 `TokenRevocationService`，含本地缓存 + Redis 双层检查）+ 网关校验流程 + 与 User BC 登出实现的关联
9. 章末固定元素：要点回顾 + 常见问题 + 下一章衔接

**关键内容要求**：

- JwtTokenGenerator 实际 Claims：`Sub`(userId)、`Jti`(Guid)、`NameIdentifier`(userId)、`ClaimTypes.Role`(role)、`role`(role)、`shop_id`(shopId，仅非空时添加)
- JwtOptions 实际字段：Issuer/Audience/SecretKey/AccessTokenExpiryMinutes(默认 120)/RefreshTokenExpiryDays(默认 7)
- InternalApiKeyMiddleware 实际特性：路径前缀 `NormalizePrefix(_options.RoutePrefix)` + `FixedTimeEqualsKey` 防计时侧信道 + Development 降级 + 错误响应 JSON（`{"errorCode":"UNAUTHORIZED","message":"..."}` 风格）
- ValidateSensitiveConfig 实际为 `ConfigCenterExtensions.ValidateSensitiveConfig` 扩展方法（非独立类）
- SensitiveConfigKeys 实际 13 项（含 Payment:Alipay:AppId/PrivateKey、Jwt:SecretKey 等）
- InternalApiKey 长度要求：≥ 44 字符
- JwtBlacklistService 实际类名（非 TokenRevocationService），位于 API Gateway 层（业务 BC 未实现）
- JwtBlacklistService 实际双层检查：`ConcurrentDictionary<string, byte>` 本地缓存 + Redis `leno:jwt:blacklist:{jti}`

- [ ] **Step 2: 提交**

```bash
git add docs/handbook/07-security-and-auth.md
git commit -m "docs(handbook): 新增第 7 章安全与认证"
```

---

## Task 9: 第 8 章 可观测性

**Files:**
- Create: `docs/handbook/08-observability.md`

- [ ] **Step 1: 写入第 8 章完整内容（约 1500 行）**

按 spec 第 11 节大纲写入，包含：

1. 章首固定元素：学习目标（5 条）+ 适用读者（开发+运维）+ 术语速查（可观测性三支柱/Serilog/结构化日志/OpenTelemetry/Jaeger/TraceId/SpanId/Prometheus/Grafana/Histogram/Counter/Gauge/Alertmanager）
2. 8.1 可观测性三支柱（约 600 字）：可观测性行内解释 + 三支柱行内解释（表：日志/追踪/指标）+ 三支柱关系图（mermaid graph）+ 关联 ID 贯穿三支柱
3. 8.2 日志（约 900 字）：Serilog 行内解释 + 结构化日志行内解释 + Leno 日志配置（来自 [appsettings.json](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/ApiGateway/Leno.ApiGateway/appsettings.json#L35-L63)，实际含 Console + File sink，按天滚动 retainedFileCountLimit=7）+ 业务 BC 简版配置（来自 [Leno.Cart.Api/appsettings.json#L9-L17](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Api/appsettings.json#L9-L17)）+ SerilogConfig 配置代码（来自 [SerilogConfig.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Logging/SerilogConfig.cs)，含 TraceIdEnricher）+ 日志级别规范表（5 级）+ 关联 ID 行内解释 + CorrelationId 中间件 + 按天滚动 + 30 天保留期
4. 8.3 分布式追踪（约 1100 字）：分布式追踪行内解释 + OpenTelemetry 行内解释 + 核心概念（Trace/Span/上下文传播）+ Leno OpenTelemetry 配置（来自 [OpenTelemetryExtensions.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Telemetry/OpenTelemetryExtensions.cs)，实际含 AddAspNetCoreInstrumentation/AddHttpClientInstrumentation/AddEntityFrameworkCoreInstrumentation/AddSource("MassTransit")/AddSource("Leno.Order") 等）+ 采样策略配置（Sampler，实际 `CreateSampler(builder.Environment)` 方法）+ Jaeger 行内解释 + TraceId 传播机制（HTTP `traceparent`/gRPC metadata/RabbitMQ Headers）+ 跨 BC 调用链路示例（mermaid sequence diagram）+ Jaeger UI 查询示例
5. 8.4 指标（约 1000 字）：prometheus-net 行内解释 + 3 种指标类型表（Counter/Histogram/Gauge）+ 6 个核心网关指标表（来自 [GatewayMetricsService.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/ApiGateway/Leno.ApiGateway/Services/GatewayMetricsService.cs)，实际类名 `GatewayMetricsService` 非 `PrometheusExtensions`，6 个指标：gateway_requests_total/gateway_request_duration/gateway_active_requests/gateway_circuit_breaker_state/gateway_rate_limit_rejected/gateway_blacklist_hits）+ AntiCorruptionMetrics 5 个指标（来自 [AntiCorruptionMetrics.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs)，实际指标：anticorruption_failure_total/anticorruption_fallback_total/anticorruption_circuit_open/anticorruption_grpc_request_total/anticorruption_grpc_duration_seconds）+ `/metrics` 端点配置 + Prometheus 抓取配置
6. 8.5 健康检查（约 500 字）：健康检查行内解释 + 3 类端点（live/ready/startup）+ Leno 健康检查实现（来自 [HealthChecksUIExtensions.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/HealthChecks/HealthChecksUIExtensions.cs)，实际 4 项依赖：redis/elasticsearch/sqlserver/rabbitmq）+ HealthChecksUI + K8s 探针配置示例
7. 8.6 Grafana 仪表盘（约 600 字）：Grafana 行内解释 + 数据源 provisioning（来自 `grafana/prometheus.yml`）+ 10 面板网关仪表盘清单（PromQL）+ 仪表盘 JSON 文件位置（实际 `grafana/leno-gateway-dashboard.json` + `grafana/leno-business-services-dashboard.json`）
8. 8.7 Alertmanager 告警规则与抑制（约 300 字）：Alertmanager 行内解释 + 5 条核心告警规则 + 告警抑制示例 + 实际配置文件位置（`grafana/provisioning/alerting/leno-alerts.yml` + `alertmanager/alertmanager.yml`）
9. 章末固定元素：要点回顾 + 常见问题 + 下一章衔接

**关键内容要求**：

- GatewayMetricsService 实际类名（非 PrometheusExtensions）
- 6 个网关指标实际名称：`gateway_requests_total`(Counter)/`gateway_request_duration`(Histogram)/`gateway_active_requests`(Gauge)/`gateway_circuit_breaker_state`(Gauge)/`gateway_rate_limit_rejected`(Counter)/`gateway_blacklist_hits`(Counter)
- AntiCorruptionMetrics 实际 5 个指标（非 3 个）：`anticorruption_failure_total`/`anticorruption_fallback_total`/`anticorruption_circuit_open`/`anticorruption_grpc_request_total`/`anticorruption_grpc_duration_seconds`
- SerilogConfig 实际含 `TraceIdEnricher`（TraceId 富化）
- OpenTelemetry 实际含 `AddSource("MassTransit")` + `AddSource("Leno.Order")` 等业务 ActivitySource
- OpenTelemetry 采样策略：`CreateSampler(builder.Environment)` 方法（按环境切换）
- 健康检查实际 4 项依赖：redis/elasticsearch/sqlserver/rabbitmq（非含 consul）
- Grafana 仪表盘实际 2 个文件：`leno-gateway-dashboard.json` + `leno-business-services-dashboard.json`

- [ ] **Step 2: 提交**

```bash
git add docs/handbook/08-observability.md
git commit -m "docs(handbook): 新增第 8 章可观测性"
```

---

## Task 10: 第 9 章 部署与运维

**Files:**
- Create: `docs/handbook/09-deployment-and-ops.md`

- [ ] **Step 1: 写入第 9 章完整内容（约 1650 行）**

按 spec 第 12 节大纲写入，包含：

1. 章首固定元素：学习目标（5 条）+ 适用读者（运维+开发）+ 术语速查（Helm/Chart/Kubernetes/Deployment/Service/Ingress/HPA/CI/CD/蓝绿部署/金丝雀发布/Runbook/Consul 服务注册）
2. 9.1 容器化基础（约 700 字）：Dockerfile 多阶段构建示例（来自 [Leno.Cart.Api/Dockerfile](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/Services/Cart/Leno.Cart.Api/Dockerfile)，实际 2 阶段 sdk:10.0→aspnet:10.0，EXPOSE 8080，ENV ASPNETCORE_URLS=http://+:8080）+ 镜像分层优化技巧 + 镜像标签规范
3. 9.2 docker compose 编排（约 800 字）：服务依赖关系图（mermaid graph TB）+ docker-compose.yml 结构（来自 [docker-compose.yml](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/docker-compose.yml)，实际 19 个 service：9 基础设施 + 11 BC + 1 网关）+ healthcheck 配置示例 + leno-net 网络与数据卷设计 + 启动顺序 + 仅启动基础设施模式
4. 9.3 Helm Chart 部署（约 1200 字）：Helm 行内解释 + Chart 行内解释 + Leno Helm Chart 结构（来自 `deploy/helm/leno/`，实际含 Chart.yaml/values.yaml/values-dev/staging/prod.yaml + templates/(_helpers.tpl/configmap/deployment/hpa/ingress/migration-job/secret/service)）+ deployment.yaml 模板核心片段 + HPA 模板代码（来自 `deploy/helm/leno/templates/hpa.yaml`，autoscaling/v2 + CPU/内存指标）+ 三环境差异化配置表 + 部署命令
5. 9.4 Consul 服务发现与配置中心（约 700 字）：Consul 行内解释 + 服务自注册机制 + ConsulDestinationResolver + Consul KV 配置中心（4 类清单）+ KV 热更新机制（ConsulConfigWatcher 长轮询 + 1-2 秒生效）
6. 9.5 CI/CD 流水线（约 900 字）：CI/CD 行内解释 + Leno CI 流水线（来自 [.github/workflows/ci.yml](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/.github/workflows/ci.yml)，实际 7+ 个 Job：build-solution/integration-tests/build-services(matrix 12)/docker-build(matrix 12)/validate-compose/migration-check/proto-lint-breaking+generate-grpc-contracts/staging-integration-tests）+ CI 流程图（mermaid graph LR）+ CD 流水线（手动触发 Helm upgrade + 健康检查 + 失败回滚）+ 镜像仓库选择
7. 9.6 发布与回滚（约 400 字）：蓝绿部署行内解释 + 金丝雀发布行内解释 + Helm rollback 命令 + 回滚决策（健康检查失败率 > 5% 持续 5 分钟自动回滚）+ 数据库迁移回滚策略
8. 9.7 Runbook（约 400 字）：Runbook 行内解释 + Leno Runbook 清单（实际 `docs/runbooks/m4-grpc-poc-verification.md` 1 个，规划中 5 个）+ Runbook 结构规范（背景/前置条件/操作步骤/验证/回滚/常见问题）
9. 9.8 常见故障排查（约 400 字）：5 类故障排查清单表（503 网关错误/数据库连接失败/分析器警告/Redis 连接失败/消息积压）
10. 章末固定元素：要点回顾 + 常见问题 + 下一章衔接

**关键内容要求**：

- docker-compose.yml 实际路径：项目根目录（非 `deploy/docker-compose.yml`）
- docker-compose.yml 实际 19 个 service：9 基础设施（sqlserver/redis/consul/rabbitmq/elasticsearch/jaeger/prometheus/alertmanager/grafana）+ 11 BC（user-auth-api/product-api/cart-api/order-api/promotion-api/payment-api/points-api/review-aftersales-api/seller-shop-api/notification-api/system-admin-api）+ 1 网关（api-gateway）
- Dockerfile 实际端口：8080（非 5103/5106 等）
- Helm Chart 实际模板：`_helpers.tpl`/`configmap.yaml`/`deployment.yaml`/`hpa.yaml`/`ingress.yaml`/`migration-job.yaml`/`secret.yaml`/`service.yaml`
- Chart.yaml 实际内容：apiVersion=v2, name=leno, type=application, version=1.0.0, appVersion="1.0.0"
- CI 流水线实际 Job：build-solution/integration-tests/build-services(matrix 12)/docker-build(matrix 12)/validate-compose/migration-check/proto-lint-breaking/generate-grpc-contracts/staging-integration-tests
- Volumes 实际：sqldata/redisdata/rabbitmqdata/esdata/prometheusdata/grafanadata/alertmanager-data
- 业务 API 服务名（连字符风格）：user-auth-api/product-api/cart-api/order-api/promotion-api/payment-api/points-api/review-aftersales-api/seller-shop-api/notification-api/system-admin-api

- [ ] **Step 2: 提交**

```bash
git add docs/handbook/09-deployment-and-ops.md
git commit -m "docs(handbook): 新增第 9 章部署与运维"
```

---

## Task 11: 第 10 章 新人上手清单

**Files:**
- Create: `docs/handbook/10-onboarding-checklist.md`

- [ ] **Step 1: 写入第 10 章完整内容（约 900 行）**

按 spec 第 13 节大纲写入，包含：

1. 章首固定元素：学习目标（3 条）+ 适用读者（新人）+ 术语速查（PR/Conventional Commits/Code Review）
2. 10.1 第一天：环境就绪（约 500 字）：6 步骤清单（git clone + 安装 .NET 10 SDK + Docker Desktop + docker compose up + 验证 Consul/Grafana + 阅读 README + 第 1 章）
3. 10.2 第二天：业务理解（约 500 字）：5 步骤清单（阅读第 1-3 章 + 浏览需求文档 + 跑通单元测试 + Postman 调网关 API + Jaeger 查看链路）
4. 10.3 第三天：动手开发（约 500 字）：5 步骤清单（阅读第 4 章 + 修改 CartItem 加字段 + 跑测试 + 本地启动 Cart BC + Postman 验证）
5. 10.4 第四天：跨 BC 通信（约 500 字）：5 步骤清单（阅读第 5 章 + 阅读 Outbox 模式 + 添加 Internal API 端点 + 用另一 BC 调用 + Jaeger 观察链路）
6. 10.5 第五天：可观测与部署（约 500 字）：5 步骤清单（阅读第 6-9 章 + Jaeger 追踪完整请求 + Grafana 看指标 + 阅读 Helm Chart + 阅读 Runbook）
7. 10.6 提交首个 PR（约 300 字）：6 步骤清单（创建 feature 分支 + Conventional Commits 提交 + 推送 + 创建 PR + 等 CI 通过 + 等 reviewer 审阅）+ PR 模板结构说明
8. 10.7 进阶学习路径（约 200 字）：5 项进阶路径（需求文档 13 篇 + ADR 7 个 + Runbook + Plan 实施 + 下一阶段优化 spec）
9. 章末固定元素：要点回顾 + 常见问题 + 结语

**关键内容要求**：

- 第一天命令：`git clone` + `mise install dotnet@10.0.301` + `docker compose -f docker-compose.yml up -d`（根目录路径）
- 第二天命令：`dotnet test` + `GET http://localhost:8080/api/products`
- 第三天命令：修改 `src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs` 的 CartItem
- 第四天引用第 5.8 章 12 条 Internal API 清单
- 第五天引用 `deploy/helm/leno/` + `docs/runbooks/m4-grpc-poc-verification.md`
- 第六天 Conventional Commits 示例：`feat(cart): 添加购物车项备注字段`
- PR 模板位置：`docs/pr-template.md`（如不存在则说明"参考仓库 PR 模板"）
- 进阶路径 ADR 目录：`docs/decisions/`（非 `docs/architecture/adr/`）
- 进阶路径需求文档：`docs/spec/` 13 篇
- 进阶路径下一阶段优化 spec：`docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md`

- [ ] **Step 2: 提交**

```bash
git add docs/handbook/10-onboarding-checklist.md
git commit -m "docs(handbook): 新增第 10 章新人上手清单"
```

---

## Self-Review

### 1. Spec 覆盖检查

| Spec 章节 | 对应 Task | 覆盖 |
|---|---|---|
| §1 设计目标与读者画像 | Task 1 README（读者画像 + 阅读路径） | ✅ |
| §2 术语解释策略 | Task 1 README（35 术语速查表） + 各章首"术语速查" | ✅ |
| §3 代码示例与调用链路图规范 | 各章代码示例标注文件路径链接 + mermaid 图 | ✅ |
| §4 第 1 章项目概览 | Task 2 | ✅ |
| §5 第 2 章本地环境搭建 | Task 3 | ✅ |
| §6 第 3 章架构总览 | Task 4 | ✅ |
| §7 第 4 章代码组织与开发模式 | Task 5 | ✅ |
| §8 第 5 章跨 BC 通信 | Task 6 | ✅ |
| §9 第 6 章数据存储与缓存 | Task 7 | ✅ |
| §10 第 7 章安全与认证 | Task 8 | ✅ |
| §11 第 8 章可观测性 | Task 9 | ✅ |
| §12 第 9 章部署与运维 | Task 10 | ✅ |
| §13 第 10 章新人上手清单 | Task 11 | ✅ |
| §14 篇幅统计与交付清单 | 11 个 Task 总行数预估 16650 行 | ✅ |
| §15 验收标准 | 各 Task 含学习目标/代码示例/术语解释/要点回顾 | ✅ |
| §16 风险与对策 | 代码示例标注路径链接 + 章节独立文件 | ✅ |

### 2. 占位符扫描

✅ 无 TBD/TODO/占位符。每个 Task 的 Step 1 均明确写入内容大纲与关键要求。

### 3. 类型一致性

✅ 各章引用的类名/文件路径已在"命名对齐说明"中统一修正：
- `AddCartItemDtoValidator`（非 `AddCartItemRequestValidator`）
- `CartsController`（非 `CartController`）
- `ApiResponse<T>` 实际字段 `Code/Message/Data/TraceId`（非 `Success/ErrorCode`）
- `RedisInventoryRepository`（非 `InventoryReserveService`）
- `JwtBlacklistService`（非 `TokenRevocationService`）
- `GatewayMetricsService`（非 `PrometheusExtensions`）
- `ReadModelSyncConsumerBase<TEvent, TReadModel>`（双泛型）
- 各 BC 独立 `IXxxInternalQueryService`（无共享抽象）
- `docker-compose.yml`（项目根目录）
- ADR 目录 `docs/decisions/`

### 4. 已知风险

- **风险 1**：代码示例文件路径链接使用绝对路径（`file:///c:/Users/Junjie/...`），跨机器不可用。**对策**：保留绝对路径（项目当前工作目录约定），读者可依相对路径定位。
- **风险 2**：35 术语速查表与各章首次出现解释可能重复。**对策**：README 速查表为简释（1 句话），各章首次出现为详释（1-3 句话），不冲突。
- **风险 3**：部分章节内容（如 11 BC 通信矩阵、12 条 Internal API 清单）需在多个章节重复出现以保持独立完整。**对策**：按 spec §3.5 章节间引用规则，跨章引用使用"详见第 N 章第 M 节"。

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-07-19-newbie-handbook.md`. Two execution options:

1. **Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration
2. **Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
