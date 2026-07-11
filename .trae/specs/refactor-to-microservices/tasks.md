# Tasks

## 阶段一：服务容器化（Dockerfile）

- [x] Task 1: 为 11 个服务 API 项目创建 Dockerfile
  - [x] SubTask 1.1: 在 `src/Services/UserAuth/Leno.UserAuth.Api/Dockerfile` 创建多阶段构建（SDK 构建阶段 + aspnet:10.0 运行阶段），`ASPNETCORE_URLS=http://+:8080`
  - [x] SubTask 1.2: 在 `src/Services/Product/Leno.Product.Api/Dockerfile` 创建多阶段构建
  - [x] SubTask 1.3: 在 `src/Services/Cart/Leno.Cart.Api/Dockerfile` 创建多阶段构建
  - [x] SubTask 1.4: 在 `src/Services/Order/Leno.Order.Api/Dockerfile` 创建多阶段构建
  - [x] SubTask 1.5: 在 `src/Services/Promotion/Leno.Promotion.Api/Dockerfile` 创建多阶段构建
  - [x] SubTask 1.6: 在 `src/Services/Payment/Leno.Payment.Api/Dockerfile` 创建多阶段构建
  - [x] SubTask 1.7: 在 `src/Services/PointsMembership/Leno.PointsMembership.Api/Dockerfile` 创建多阶段构建
  - [x] SubTask 1.8: 在 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Dockerfile` 创建多阶段构建
  - [x] SubTask 1.9: 在 `src/Services/SellerShop/Leno.SellerShop.Api/Dockerfile` 创建多阶段构建
  - [x] SubTask 1.10: 在 `src/Services/Notification/Leno.Notification.Api/Dockerfile` 创建多阶段构建
  - [x] SubTask 1.11: 在 `src/Services/SystemAdmin/Leno.SystemAdmin.Api/Dockerfile` 创建多阶段构建
  - [x] SubTask 1.12: 各 Dockerfile 的构建上下文为仓库根目录（`docker build` 从 repo root 执行），`COPY` 源路径以 `src/...` 相对根目录

## 阶段二：容器环境配置

- [x] Task 2: 为各服务创建 appsettings.Docker.json 容器环境覆盖配置
  - [x] SubTask 2.1: 各服务 API 项目新增 `appsettings.Docker.json`，ConnectionStrings 指向 `Server=sqlserver,1433`，Redis 配置指向 `redis:6379`，RabbitMQ 指向 `rabbitmq:5672`
  - [x] SubTask 2.2: 各服务 `appsettings.Docker.json` 的 `ServiceUrls` 指向容器服务名（如 `http://product-api:8080`），按端口约定表配置
  - [x] SubTask 2.3: 各 Dockerfile 中设置 `ASPNETCORE_ENVIRONMENT=Docker` 环境变量

## 阶段三：健康检查端点

- [x] Task 3: 各服务 Program.cs 映射健康检查端点并补充 DB 检查
  - [x] SubTask 3.1: 各服务 `Program.cs` 在 `app.UseAuthorization()` 后添加 `app.MapHealthChecks("/health/live", ...)`（仅存活检查，过滤 tag）与 `app.MapHealthChecks("/health/ready", ...)`（就绪检查，含 ready tag）
  - [x] SubTask 3.2: 各服务 `Program.cs` 在 `AddXxxInfrastructure` 调用后追加 `services.AddHealthChecks().AddDbContextCheck<XxxDbContext>(tags: ready)`，使数据库纳入就绪探针
  - [x] SubTask 3.3: `Leno.Infrastructure` 的 `AddHealthChecks` 中为存活探针注册一个轻量自检查（如 `self` check，始终 Healthy，不带 ready tag）

## 阶段四：API 网关

- [x] Task 4: 创建 Leno.ApiGateway 项目（YARP 反向代理）
  - [x] SubTask 4.1: 创建 `src/ApiGateway/Leno.ApiGateway/Leno.ApiGateway.csproj`，引用 `Yarp.ReverseProxy` 包
  - [x] SubTask 4.2: 创建 `Program.cs`，注册 YARP 反向代理，从 `appsettings.json` 的 `ReverseProxy:Routes` 加载路由配置，按路径前缀路由到 11 个后端服务
  - [x] SubTask 4.3: 网关 `appsettings.json` 配置 11 条路由（Products→product-api、Orders→order-api 等），Clusters 指向容器服务名:8080
  - [x] SubTask 4.4: 网关配置 JWT 鉴权（与后端共享同一 JwtOptions），将 Authorization 头透传至后端
  - [x] SubTask 4.5: 网关 `Program.cs` 映射 `/health` 端点，通过 HttpClient 并发调用各后端 `/health/ready`，任一不可用返回 503
  - [x] SubTask 4.6: 将 `Leno.ApiGateway` 项目加入 `Leno.slnx`

- [x] Task 5: 为 API 网关创建 Dockerfile 并配置
  - [x] SubTask 5.1: 在 `src/ApiGateway/Leno.ApiGateway/Dockerfile` 创建多阶段构建，`ASPNETCORE_URLS=http://+:8080`
  - [x] SubTask 5.2: 网关 `appsettings.Docker.json` 的 Clusters 指向容器服务名

## 阶段五：docker-compose 编排

- [x] Task 6: 在 docker-compose.yml 新增全部微服务与网关定义
  - [x] SubTask 6.1: 新增 `api-gateway` 服务定义，端口 `8080:8080`，depends_on 各后端服务，环境 `ASPNETCORE_ENVIRONMENT=Docker`
  - [x] SubTask 6.2: 新增 11 个后端服务定义（user-auth-api / product-api / cart-api / order-api / promotion-api / payment-api / points-api / review-aftersales-api / seller-shop-api / notification-api / system-admin-api），各端口 `51xx:8080`，depends_on sqlserver/redis/rabbitmq/elasticsearch，环境 `ASPNETCORE_ENVIRONMENT=Docker`
  - [x] SubTask 6.3: 各服务定义配置 `build.context` 指向仓库根目录、`dockerfile` 指向对应 Dockerfile 路径
  - [x] SubTask 6.4: 各服务定义配置 healthcheck 指向 `/health/live`，与基础设施 healthcheck 一致

## 阶段六：CI/CD 独立化

- [x] Task 7: 修复并重构 CI 工作流
  - [x] SubTask 7.1: 修复 `.github/workflows/ci.yml` 中 `Leno.sln` → `Leno.slnx`
  - [x] SubTask 7.2: CI 新增服务矩阵 job，对 11 个 API 项目 + 1 个网关项目分别执行 `dotnet build`，矩阵项列出各项目路径
  - [x] SubTask 7.3: CI 新增 `docker build` 验证步骤，对每个服务的 Dockerfile 执行构建验证（从仓库根目录执行）
  - [x] SubTask 7.4: 保留原有全量 `dotnet build Leno.slnx` 作为兜底构建

## 阶段七：构建验证

- [x] Task 8: 全量构建验证
  - [x] SubTask 8.1: 执行 `dotnet build Leno.slnx` 确认 0 Error / 0 Warning（含网关项目）
  - [x] SubTask 8.2: 对各服务 Dockerfile 执行 `docker build` 验证镜像可构建
  - [x] SubTask 8.3: 执行 `docker-compose config` 验证编排配置合法

# Task Dependencies

- Task 1 (Dockerfile) → 无依赖，可与 Task 2/3 并行
- Task 2 (appsettings.Docker) → 无依赖，可与 Task 1/3 并行
- Task 3 (健康端点) → 无依赖，可与 Task 1/2 并行
- Task 4 (网关项目) → 依赖 Task 3（后端健康端点就绪后网关才能聚合）
- Task 5 (网关 Dockerfile) → 依赖 Task 4
- Task 6 (docker-compose) → 依赖 Task 1（各服务 Dockerfile）、Task 2（容器配置）、Task 5（网关 Dockerfile）
- Task 7 (CI) → 依赖 Task 1（Dockerfile 就绪后 CI 才能验证 docker build）
- Task 8 (构建验证) → 依赖全部前序任务完成
