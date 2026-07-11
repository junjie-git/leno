# 单体模块重构为微服务架构 Spec

## Why

代码库已按限界上下文拆分为 11 个独立服务（DDD 分层、独立数据库、HttpClient 防腐层 + RabbitMQ 事件总线），但部署层面仍是"单体"形态：无 Dockerfile、docker-compose 仅含基础设施依赖（SQL Server/Redis/RabbitMQ/ES）而无应用服务、无 API 网关统一入口、服务各自暴露在不同端口、健康检查未映射端点且未纳入 DB 检查、CI 将整个解决方案作为单一构建单元（且引用了不存在的 `Leno.sln`）。需补齐容器化、网关路由、健康端点与独立构建能力，使各服务真正可独立部署、独立运行、独立扩缩容，落地 `docs/spec/10-模块化部署架构.md` 描述的微服务部署拓扑。

## What Changes

### 一、服务容器化
- 为 11 个服务 API 项目各创建一个 `Dockerfile`（多阶段构建：SDK 构建阶段 + Runtime 运行阶段）。
- 在 `docker-compose.yml` 中为 11 个微服务新增服务定义，依赖现有 sqlserver/redis/rabbitmq/elasticsearch，配置容器间网络通信。
- 各服务分配固定端口，统一通过 `ASPNETCORE_URLS` 暴露。

### 二、服务间网络配置
- 各服务 `appsettings.json` 的 `ServiceUrls` 由 `localhost:端口` 改为容器服务名（如 `http://product-api:8080`），使容器网络内可互通。
- 新增 `RabbitMQ`、`Redis`、`ConnectionStrings` 配置节使用容器服务名（`sqlserver`、`redis`、`rabbitmq`）。
- 通过 `appsettings.Docker.json`（环境覆盖文件）承载容器环境配置，保留 `appsettings.json` 本地开发默认值。

### 三、健康检查端点
- 各服务 API 的 `Program.cs` 映射 `/health/live`（存活）与 `/health/ready`（就绪）端点。
- `Leno.Infrastructure` 健康检查注册新增 SQL Server 数据库检查（按各服务 DbContext 注册 `AddDbContext` 后追加）。
- 各服务 `Program.cs` 在注册 `AddXxxInfrastructure` 后，补充 `AddDbContextCheck<XxxDbContext>`。

### 四、API 网关
- 新增 `Leno.ApiGateway` 项目（基于 YARP 反向代理），作为统一外部入口，按路径前缀路由到 11 个后端服务。
- 网关承载 JWT 鉴权透传、全局异常处理、健康检查聚合（`/health` 聚合后端各服务就绪状态）。
- 网关加入解决方案与 docker-compose，对外暴露 8080 端口。

### 五、CI/CD 独立化
- 修复 CI 工作流引用错误（`Leno.sln` → `Leno.slnx`）。
- CI 构建改为按服务矩阵触发，每个服务 API 项目独立构建验证。
- 新增 `docker build` 验证步骤，确保各 Dockerfile 可成功构建镜像。

### 六、端口与命名约定
| 服务 | 容器服务名 | 宿主端口:容器端口 |
|-|-|-|
| ApiGateway | api-gateway | 8080:8080 |
| UserAuth | user-auth-api | 5151:8080 |
| Product | product-api | 5152:8080 |
| Cart | cart-api | 5153:8080 |
| Order | order-api | 5154:8080 |
| Promotion | promotion-api | 5155:8080 |
| Payment | payment-api | 5156:8080 |
| PointsMembership | points-api | 5157:8080 |
| ReviewAfterSales | review-aftersales-api | 5158:8080 |
| SellerShop | seller-shop-api | 5159:8080 |
| Notification | notification-api | 5160:8080 |
| SystemAdmin | system-admin-api | 5161:8080 |

## Impact

- **Affected specs**: `docs/spec/10-模块化部署架构.md`（部署拓扑从文档落地为可运行拓扑）
- **Affected code**:
  - `docker-compose.yml`（新增 11 个微服务 + 网关服务定义）
  - 各服务 API 项目根目录新增 `Dockerfile`（11 个）
  - 各服务 `Program.cs`（映射健康检查端点、补充 DbContext 健康检查）
  - 各服务 `appsettings.json` / 新增 `appsettings.Docker.json`（容器网络配置）
  - `Leno.slnx`（新增 ApiGateway 项目）
  - 新增 `src/ApiGateway/Leno.ApiGateway/` 项目
  - `.github/workflows/ci.yml`（修复引用、改为服务矩阵构建）
  - `src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`（健康检查注册调整）

## ADDED Requirements

### Requirement: 服务容器化镜像构建
每个服务 API 项目 SHALL 提供一个多阶段 `Dockerfile`，以 .NET 10 SDK 镜像构建、Runtime 镜像运行，镜像内服务监听 8080 端口。

#### Scenario: 镜像构建成功
- **WHEN** 执行 `docker build -t <service> -f Dockerfile .`
- **THEN** 镜像构建成功且无错误，镜像基于 `mcr.microsoft.com/dotnet/aspnet:10.0`

#### Scenario: 容器启动监听
- **WHEN** 镜像以 `docker run -p <port>:8080` 启动
- **THEN** 服务在容器内 8080 端口监听 HTTP 请求

### Requirement: docker-compose 微服务编排
`docker-compose.yml` SHALL 定义全部 11 个微服务与 1 个 API 网关，各服务依赖 sqlserver/redis/rabbitmq/elasticsearch，通过 `depends_on` 声明启动顺序与健康条件，服务间以容器服务名互访。

#### Scenario: 一键启动全栈
- **WHEN** 执行 `docker-compose up -d`
- **THEN** 基础设施与 12 个应用服务全部启动，各服务健康检查通过

#### Scenario: 服务间通过容器名互通
- **WHEN** Order 服务调用 Product 服务
- **THEN** HttpClient 请求目标为 `http://product-api:8080`，无需 localhost 端口映射

### Requirement: 容器环境配置覆盖
每个服务 SHALL 提供 `appsettings.Docker.json`，将数据库连接、Redis、RabbitMQ、ServiceUrls 指向容器服务名，通过 `ASPNETCORE_ENVIRONMENT=Docker` 激活覆盖。

#### Scenario: 容器内数据库连接
- **WHEN** 服务在容器中以 `ASPNETCORE_ENVIRONMENT=Docker` 启动
- **THEN** 数据库连接字符串指向 `Server=sqlserver,1433`，而非 `localhost`

### Requirement: 健康检查端点暴露
每个服务 API SHALL 映射 `/health/live`（轻量存活探针，不检查依赖）与 `/health/ready`（就绪探针，检查 DB/Redis/ES 依赖）端点，供容器编排与网关健康聚合使用。

#### Scenario: 存活探针
- **WHEN** 请求 `GET /health/live`
- **THEN** 返回 200 Healthy（不检查外部依赖）

#### Scenario: 就绪探针含数据库
- **WHEN** 请求 `GET /health/ready` 且数据库可达
- **THEN** 返回 200 Healthy，结果包含该服务 DbContext 的检查项

#### Scenario: 就绪探针依赖故障
- **WHEN** 请求 `GET /health/ready` 且数据库不可达
- **THEN** 返回 503 Unhealthy

### Requirement: API 网关统一入口
系统 SHALL 提供 `Leno.ApiGateway` 服务作为统一外部入口，基于 YARP 按路径前缀将请求路由到对应后端服务，对外仅暴露网关端口。

#### Scenario: 路由到后端服务
- **WHEN** 客户端请求 `GET /api/products/...`
- **THEN** 网关将请求转发至 product-api 服务，返回后端响应

#### Scenario: JWT 鉴权透传
- **WHEN** 客户端携带 `Authorization: Bearer {token}` 请求网关
- **THEN** 网关将 Authorization 头透传至后端服务，由后端完成鉴权

### Requirement: 网关健康聚合
API 网关 SHALL 提供 `/health` 端点，聚合所有后端服务的 `/health/ready` 状态，任一后端不可用时网关返回 503。

#### Scenario: 全部后端就绪
- **WHEN** 所有后端服务 `/health/ready` 返回 Healthy
- **THEN** 网关 `/health` 返回 200 Healthy

#### Scenario: 任一后端不可用
- **WHEN** 任一后端服务 `/health/ready` 返回 Unhealthy
- **THEN** 网关 `/health` 返回 503 Unhealthy

### Requirement: 按服务独立 CI 构建
CI 工作流 SHALL 以服务矩阵方式对每个 API 项目独立执行 `dotnet build`，并新增 `docker build` 验证步骤确保各 Dockerfile 可构建。

#### Scenario: CI 引用正确的解决方案文件
- **WHEN** CI 触发构建
- **THEN** 使用 `Leno.slnx`（而非不存在的 `Leno.sln`）执行还原与构建

#### Scenario: 单服务 Docker 镜像验证
- **WHEN** CI 矩阵执行某服务的 `docker build`
- **THEN** 该服务镜像构建成功

## MODIFIED Requirements

### Requirement: 服务间通信地址配置
各服务 `appsettings.json` 的 `ServiceUrls` 默认值保留 localhost 开发地址；新增 `appsettings.Docker.json` 覆盖为容器服务名，使同一镜像可在本地开发与容器编排两种环境运行。

### Requirement: 基础设施健康检查注册
`Leno.Infrastructure` 的 `AddHealthChecks` SHALL 注册 Redis 与 Elasticsearch 通用检查；各服务在 `AddXxxInfrastructure` 后 SHALL 自行追加 `AddDbContextCheck<XxxDbContext>` 注册数据库检查，使就绪探针覆盖该服务全部关键依赖。
