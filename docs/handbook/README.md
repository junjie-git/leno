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
