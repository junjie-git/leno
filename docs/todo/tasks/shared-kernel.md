# 共享内核与基础设施 - 任务执行计划

> **模块**: 共享内核 (Shared Kernel)
> **对应文档**: `00-需求文档总览与DDD架构.md`、`技术选型方案.md`、`编码规范.md`
> **任务 ID 前缀**: SK
> **总任务数**: 6 | **P0**: 1 | **P1**: 1 | **P2**: 4

---

## 模块概述

共享内核提供跨全部 11 个限界上下文的基础设施支撑，包括测试框架、对象存储、链路追踪、配置中心、缓存防护与健康监控。当前已有基础实现（LocalFileStorageService、CacheService、HealthChecks），但缺少测试项目、生产级存储适配器、OpenTelemetry 集成、Consul 配置中心、布隆过滤器与 HealthChecksUI。

---

## Task SK-01: 测试基础设施搭建 [P0]

### 功能描述
为全部 11 个限界上下文创建测试项目，遵循 `{BC}.{层}.Tests` 命名规范。

### 子任务 Checklist

- [x] SK-01.1: 在 `Directory.Build.props` 中添加测试相关配置（Testcontainers 版本、xUnit 版本、Moq 版本、FluentAssertions 版本）
- [x] SK-01.2: 创建共享测试工具项目 `Leno.Testing`（含 Testcontainers Fixture、Mock 工厂、测试数据构建器）
- [x] SK-01.3: 为每个 BC 创建 `{BC}.Domain.Tests` 项目（11 个）
- [x] SK-01.4: 为核心 BC（Order/Product/UserAuth/Payment/Promotion）创建 `{BC}.Application.Tests` 项目（5 个）
- [x] SK-01.5: 为核心 BC 创建 `{BC}.Infrastructure.Tests` 集成测试项目（5 个）
- [x] SK-01.6: 为核心 BC 创建 `{BC}.Api.Tests` 集成测试项目（5 个）
- [x] SK-01.7: 配置 `ContainerFixture` 管理 SQL Server/Redis/RabbitMQ/ES Testcontainers 生命周期
- [x] SK-01.8: 配置 xUnit Collection Fixtures 共享容器实例（避免重复启动）
- [x] SK-01.9: 在 CI 流水线中启用集成测试步骤
- [x] SK-01.10: 配置测试覆盖率收集（coverlet + ReportGenerator）

### 技术约束
- 测试方法命名: `Method_Scenario_ExpectedResult`
- 集成测试使用 Testcontainers，不依赖外部环境
- 每个聚合根方法覆盖正常路径、边界条件、异常路径

### 验收标准
- [x] 每个 BC 至少拥有领域层单元测试项目
- [x] 集成测试在 CI 中可运行
- [x] 领域层测试覆盖率 ≥ 80%

---

## Task SK-02: 对象存储适配器实现 [P1]

### 功能描述
实现 MinIO 适配器，补全 `IFileStorageService` 的生产环境实现。

### 子任务 Checklist

- [ ] SK-02.1: 添加 `Minio` NuGet 包到 `Leno.Infrastructure`
- [ ] SK-02.2: 创建 `FileStorageOptions` 配置类（Provider、MinIO 连接参数、Bucket 名称）
- [ ] SK-02.3: 创建 `ObjectStorageService` 实现 `IFileStorageService` 全部方法
- [ ] SK-02.4: 实现 `UploadAsync` - 上传到 MinIO bucket，返回可访问 URL
- [ ] SK-02.5: 实现 `DownloadAsync` - 从 MinIO 下载文件流
- [ ] SK-02.6: 实现 `DeleteAsync` - 从 MinIO 删除对象
- [ ] SK-02.7: 实现 `ValidateUrl` - 校验 URL 为合法 MinIO 地址
- [ ] SK-02.8: 实现 `ExistsAsync` - 检查 MinIO 对象是否存在
- [ ] SK-02.9: 添加 DI 扩展方法 `AddFileStorage` 根据配置切换 Local/MinIO
- [ ] SK-02.10: 编写 `ObjectStorageService` 集成测试（上传/下载/删除/存在性检查）

### 技术约束
- 敏感参数（AccessKey/SecretKey）从环境变量或配置中心读取
- 保留 `LocalFileStorageService` 作为开发环境默认实现

### 验收标准
- [ ] 支持 MinIO 作为对象存储后端
- [ ] 通过 `FileStorage:Provider` 配置可切换 Local/MinIO
- [ ] 集成测试验证上传/下载/删除流程

---

## Task SK-03: OpenTelemetry 链路追踪集成 [P2]

### 功能描述
集成 OpenTelemetry 实现跨服务链路追踪，traceId 贯穿网关到各服务到事件消费。

### 子任务 Checklist

- [ ] SK-03.1: 添加 OpenTelemetry NuGet 包（Extensions.Hosting、Instrumentation.AspNetCore、Instrumentation.Http、Instrumentation.EntityFrameworkCore、Exporter.OpenTelemetryProtocol）
- [ ] SK-03.2: 在 `Leno.Infrastructure` 中创建 `OpenTelemetryExtensions` 扩展方法
- [ ] SK-03.3: 配置 `AddOpenTelemetry` with `AddAspNetCoreInstrumentation`、`AddHttpClientInstrumentation`、`AddEntityFrameworkCoreInstrumentation`
- [ ] SK-03.4: 配置 OTLP Exporter 端点（Jaeger/Collector）
- [ ] SK-03.5: 配置 MassTransit 的 ActivitySource 追踪（消费端自动继承 traceId）
- [ ] SK-03.6: 创建自定义 `ActivitySource` 覆盖关键业务操作（下单、支付、库存预占）
- [ ] SK-03.7: 在日志中携带 TraceId 字段（Serilog Enricher）
- [ ] SK-03.8: 配置采样策略（生产环境 10% 采样，开发环境 100%）

### 技术约束
- 不阻塞主业务流程（异步导出）
- traceId 在网关→服务→事件消费链路中贯穿

### 验收标准
- [ ] 所有 11 个微服务集成 OpenTelemetry
- [ ] traceId 在完整调用链路中保持一致
- [ ] 日志中携带 TraceId 字段

---

## Task SK-04: 配置中心集成 [P2]

### 功能描述
集成 Consul 配置中心，支持配置热更新与敏感参数外部化。

### 子任务 Checklist

- [ ] SK-04.1: 添加 `Winton.Extensions.Configuration.Consul` NuGet 包
- [ ] SK-04.2: 在 `Leno.Infrastructure` 中创建 `ConfigCenterExtensions` 扩展方法
- [ ] SK-04.3: 配置 Consul KV 作为配置源（`AddConsul`）
- [ ] SK-04.4: 实现配置热更新监听（`IOptionsSnapshot` 自动刷新）
- [ ] SK-04.5: 将敏感参数迁移至 Consul（支付密钥、短信 API Key、OAuth2 Secret）
- [ ] SK-04.6: 保留 `appsettings.json` 作为默认配置源（降级）
- [ ] SK-04.7: 配置 Consul 健康检查与服务发现（可选）

### 技术约束
- 保留 `appsettings.json` 作为默认配置源
- 配置变更后无需重启服务即可生效

### 验收标准
- [ ] 支持 Consul 作为可选配置源
- [ ] 配置变更后无需重启服务即可生效
- [ ] 敏感参数不落代码仓库

---

## Task SK-05: 布隆过滤器实现 [P2]

### 功能描述
实现布隆过滤器防缓存穿透，在 `CacheService` 中集成。

### 子任务 Checklist

- [ ] SK-05.1: 定义 `IBloomFilter` 接口（`AddAsync`、`MightContainAsync`）
- [ ] SK-05.2: 实现 `RedisBloomFilter`（基于 Redis Bitmap + 多个 Hash 函数）
- [ ] SK-05.3: 修改 `CacheService.GetOrSetAsync` 集成布隆过滤器校验
- [ ] SK-05.4: 缓存空值短过期（2 分钟）防穿透
- [ ] SK-05.5: 随机过期时间（30-120 秒抖动）防雪崩
- [ ] SK-05.6: 在各服务启动时预热布隆过滤器（加载已有 ID 集合）
- [ ] SK-05.7: 编写布隆过滤器误判率测试

### 技术约束
- 误判率控制在 1% 以内
- Redis Bitmap 预分配大小基于预估数据量

### 验收标准
- [ ] `IBloomFilter` 接口与 Redis 实现
- [ ] `CacheService` 集成布隆过滤器防穿透
- [ ] 缓存空值短过期与随机抖动

---

## Task SK-06: 健康检查 UI 仪表盘 [P2]

### 功能描述
为所有服务添加 HealthChecksUI 仪表盘，可视化各服务健康状态。

### 子任务 Checklist

- [ ] SK-06.1: 添加 `AspNetCore.HealthChecks.UI` 和 `AspNetCore.HealthChecks.UI.Client` NuGet 包
- [ ] SK-06.2: 在各服务 `Program.cs` 中配置 `AddHealthChecks` 覆盖 DB/Redis/ES/RabbitMQ
- [ ] SK-06.3: 配置 `/health`（就绪检查）和 `/health/ready`（存活检查）端点
- [ ] SK-06.4: 创建独立的健康检查仪表盘服务或集成到 API Gateway
- [ ] SK-06.5: 配置各服务健康端点注册到仪表盘
- [ ] SK-06.6: 配置健康检查评价推送（Slack/邮件告警）
- [ ] SK-06.7: 配置健康检查历史记录存储

### 技术约束
- 健康检查不阻塞服务启动
- 仪表盘独立部署，不依赖任一业务服务

### 验收标准
- [ ] 所有服务 `/health` 和 `/health/ready` 端点可用
- [ ] HealthChecksUI 仪表盘可查看所有服务状态
- [ ] 健康检查覆盖 DB、Redis、ES、RabbitMQ、支付渠道、通知渠道