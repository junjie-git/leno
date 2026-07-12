# 共享内核与基础设施 - 缺失功能任务

> **限界上下文**: 共享内核 (Shared Kernel)
> **对应文档**: `00-需求文档总览与DDD架构.md`、`技术选型方案.md`、`编码规范.md`
> **审计日期**: 2026-07-11

---

## 核验摘要

共享内核与基础设施层已基本完成，但存在以下关键缺失：

| 缺失项 | 严重程度 | 说明 |
|---------|----------|------|
| 测试项目 | P0 关键 | 全部 48 个项目中无任何测试项目 |
| 对象存储适配器 | P1 重要 | 仅有 LocalFileStorageService，缺少 MinIO/OSS 实现 |
| 配置中心集成 | P2 一般 | 仅使用 appsettings.json，缺少 Consul/Apollo 热更新 |
| OpenTelemetry 链路追踪 | P2 一般 | 编码规范中定义但未实际集成 |
| 健康检查 UI | P2 一般 | 缺少 HealthChecksUI 仪表盘 |
| 布隆过滤器 | P2 一般 | 编码规范中定义防缓存穿透，未实际实现 |

---

## Task 1: 测试基础设施搭建

**严重程度**: P0 关键

### 功能描述
按照编码规范第 13 章要求，为全部 11 个限界上下文创建测试项目，遵循 `{BC}.{层}.Tests` 命名规范，使用 xUnit + Moq + FluentAssertions 技术栈。

### 技术实现路径
1. 创建测试项目结构：
   - 每个 BC 创建 `{BC}.Domain.Tests`（领域层单元测试）
   - 核心 BC（Order/Product/UserAuth）创建 `{BC}.Application.Tests`（应用层单元测试）
   - 核心 BC 创建 `{BC}.Infrastructure.Tests`（基础设施层集成测试）
   - 核心 BC 创建 `{BC}.Api.Tests`（API 层集成测试）
2. 配置 Testcontainers 用于集成测试（SQL Server、Redis、RabbitMQ、Elasticsearch）
3. 配置 xUnit Collection Fixtures 共享容器实例
4. 添加测试配置到 Directory.Build.props

### 预期完成标准
- [ ] 每个 BC 至少拥有领域层单元测试项目
- [ ] 测试方法命名遵循 `Method_Scenario_ExpectedResult` 模式
- [ ] 每个聚合根方法至少覆盖正常路径、边界条件、异常路径
- [ ] 集成测试使用 Testcontainers
- [ ] CI 流水线中集成测试可运行

### 参考
- `编码规范.md` 第 13 章
- 测试命名约定：`{BC}.Domain.Tests`、方法命名 `Method_Scenario_ExpectedResult`

---

## Task 2: 对象存储适配器实现

**严重程度**: P1 重要

### 功能描述
实现 `ObjectStorageService`（MinIO 适配器），补全 `IFileStorageService` 的生产环境实现。当前仅有 `LocalFileStorageService`。

### 技术实现路径
1. 在 `Leno.Infrastructure/Storage/` 下创建 `ObjectStorageService.cs`
2. 对接 MinIO .NET SDK（`Minio` NuGet 包）
3. 实现 `IFileStorageService` 全部方法：
   - `UploadAsync` - 上传到 MinIO bucket
   - `DownloadAsync` - 从 MinIO 下载
   - `DeleteAsync` - 从 MinIO 删除
   - `ValidateUrl` - 校验 URL 为合法 MinIO 地址
   - `ExistsAsync` - 检查 MinIO 对象是否存在
4. 配置 `FileStorageOptions` 支持 MinIO 与阿里云 OSS 切换
5. 添加 DI 扩展方法根据配置选择存储实现

### 预期完成标准
- [ ] `ObjectStorageService` 实现 `IFileStorageService` 全部方法
- [ ] 支持 MinIO 作为对象存储后端
- [ ] 通过 `FileStorage:Provider` 配置可切换 Local/MinIO
- [ ] 编写集成测试验证上传/下载/删除流程
- [ ] 敏感参数（AccessKey/SecretKey）从环境变量读取

### 参考
- `00-需求文档总览与DDD架构.md` 第 4.9 节
- `编码规范.md` 第 11.4 节

---

## Task 3: OpenTelemetry 链路追踪集成

**严重程度**: P2 一般

### 功能描述
按照编码规范第 12.3 节，集成 OpenTelemetry 实现跨服务链路追踪，traceId 贯穿网关到各服务到事件消费。

### 技术实现路径
1. 添加 OpenTelemetry NuGet 包：
   - `OpenTelemetry.Extensions.Hosting`
   - `OpenTelemetry.Instrumentation.AspNetCore`
   - `OpenTelemetry.Instrumentation.Http`
   - `OpenTelemetry.Instrumentation.EntityFrameworkCore`
   - `OpenTelemetry.Exporter.OpenTelemetryProtocol`
2. 在 `Leno.Infrastructure` 中创建 `OpenTelemetryExtensions.cs` 扩展方法
3. 配置 MassTransit 的 ActivitySource 追踪
4. 在各服务 Program.cs 中调用扩展方法
5. 配置 OTLP Exporter 端点

### 预期完成标准
- [ ] 所有 11 个微服务集成 OpenTelemetry
- [ ] traceId 在网关→服务→事件消费链路中贯穿
- [ ] 自定义 ActivitySource 覆盖关键业务操作（下单、支付、库存预占）
- [ ] 日志中携带 TraceId 字段

### 参考
- `编码规范.md` 第 12.3 节
- `00-需求文档总览与DDD架构.md` 第 6.5 节

---

## Task 4: 配置中心集成

**严重程度**: P2 一般

### 功能描述
集成 Consul 配置中心，支持配置热更新与敏感参数外部化。当前仅使用 `appsettings.json` + 环境变量。

### 技术实现路径
1. 添加 `Winton.Extensions.Configuration.Consul` 或 `Consul` NuGet 包
2. 在 `Leno.Infrastructure/Configuration/` 下创建 `ConfigCenterExtensions.cs`
3. 支持 Consul KV 作为配置源
4. 实现配置热更新监听（`IOptionsSnapshot` 或 `IChangeToken`）
5. 敏感参数（支付密钥、短信 API Key）存于 Consul 而非代码仓库

### 预期完成标准
- [ ] 支持 Consul 作为可选配置源
- [ ] 配置变更后无需重启服务即可生效
- [ ] 敏感参数（支付渠道密钥、短信 API Key）不落代码仓库
- [ ] 保留 `appsettings.json` 作为默认配置源

### 参考
- `00-需求文档总览与DDD架构.md` 第 7 章
- `编码规范.md` 第 11 章

---

## Task 5: 布隆过滤器实现

**严重程度**: P2 一般

### 功能描述
实现布隆过滤器防缓存穿透，按照编码规范第 12.4 节在 `CacheService` 中集成。

### 技术实现路径
1. 添加 `StackExchange.Redis.Extensions.BloomFilter` 或自行实现基于 Redis Bitmap 的布隆过滤器
2. 在 `Leno.Infrastructure/Caching/` 下创建 `RedisBloomFilter.cs`
3. 定义 `IBloomFilter` 接口（`AddAsync`、`MightContainAsync`）
4. 修改 `CacheService.GetOrSetAsync` 方法集成布隆过滤器校验
5. 在各服务启动时预热布隆过滤器（加载已有 ID 集合）

### 预期完成标准
- [ ] `IBloomFilter` 接口与 Redis 实现
- [ ] `CacheService` 集成布隆过滤器防穿透
- [ ] 缓存空值短过期（2 分钟）防穿透
- [ ] 随机过期时间（30-120 秒抖动）防雪崩

### 参考
- `编码规范.md` 第 12.4 节
- `00-需求文档总览与DDD架构.md` 第 6.1 节

---

## Task 6: 健康检查 UI 仪表盘

**严重程度**: P2 一般

### 功能描述
为所有服务添加 HealthChecksUI 仪表盘，可视化各服务健康状态。

### 技术实现路径
1. 添加 `AspNetCore.HealthChecks.UI` 和 `AspNetCore.HealthChecks.UI.Client` NuGet 包
2. 在各服务 Program.cs 中配置 HealthChecksUI
3. 创建独立的健康检查仪表盘服务或集成到 API Gateway
4. 配置各服务健康端点注册到仪表盘

### 预期完成标准
- [ ] 所有服务 `/health` 和 `/health/ready` 端点可用
- [ ] HealthChecksUI 仪表盘可查看所有服务状态
- [ ] 健康检查覆盖 DB、Redis、ES、RabbitMQ、支付渠道、通知渠道

### 参考
- `编码规范.md` 第 12.2 节
- `00-需求文档总览与DDD架构.md` 第 6.5 节