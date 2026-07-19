# 第 2 章 本地环境搭建

## 学习目标

读完本章你将：

- 完成本地开发环境一站式搭建
- 启动 docker compose 全套基础设施并验证健康
- 掌握数据库迁移与 Consul KV 初始化操作
- 配置 IDE 调试单个 BC

## 适用读者

开发

## 术语速查

本章将遇到的术语：

| 术语 | 简释 |
|---|---|
| Docker | 容器化运行时引擎，将应用与依赖打包为可移植容器 |
| 容器 | 运行中的镜像实例，类似 OOP 中对象之于类 |
| 镜像 | 容器只读模板，类似 OOP 中的类 |
| docker compose | 多容器编排工具，用 YAML 描述多服务依赖 |
| mise | 跨语言运行时版本管理器，类似 nvm/asdf |
| SDK | 软件开发工具包，.NET SDK 含运行时+编译器+工具 |
| IDE | 集成开发环境，如 Visual Studio/Rider/VS Code |
| healthcheck | 容器健康探针，编排平台据此判断容器状态 |
| 数据卷 | Docker 中持久化容器数据的机制 |

---

## 2.1 前置依赖清单

要在本地顺利运行 Leno 项目，开发机需准备以下五类前置依赖。下表先给出总览，再逐项展开说明。

| 依赖项 | 版本要求 | 说明 |
|---|---|---|
| .NET SDK | 10.0.301+（推荐 .NET 10） | 编译与运行所有 BC 的基础 |
| Docker | Docker 24.0+（含 compose v2） | 启动基础设施容器 |
| IDE | Visual Studio 2026 / Rider / VS Code + C# Dev Kit | 三选一 |
| Git | 2.40+ | 需支持 sparse-checkout |
| mise | 最新版 | 统一管理 .NET / Node 等运行时版本 |

### 2.1.1 .NET 10 SDK

Leno 全部 11 个 BC 与共享内核均基于 .NET 10 构建，需安装 .NET 10 SDK（建议版本 10.0.301 或更高）。SDK 内含运行时、编译器（`csc`/`fsc`）与 CLI 工具（`dotnet` 命令）。Windows、macOS、Linux 平台均可在 [dotnet.microsoft.com](https://dotnet.microsoft.com) 下载官方安装包；推荐使用 `mise` 统一管理版本，便于多项目并行开发。

### 2.1.2 Docker

Docker 用于启动 SQL Server、Redis、RabbitMQ、Elasticsearch、Consul、Jaeger、Prometheus、Grafana 等第三方基础设施。不同平台安装方式略有差异：

- **Windows**：安装 [Docker Desktop for Windows](https://www.docker.com/products/docker-desktop/)，启用 WSL 2 后端以获得更好性能
- **macOS**：安装 Docker Desktop for Mac（Apple Silicon 选 ARM64 版本）
- **Linux**：安装 Docker Engine + docker compose plugin（v2），无需 Desktop

要求 `docker compose` 为 v2 版本（命令为 `docker compose` 而非旧版 `docker-compose`），可在终端执行 `docker compose version` 验证。

### 2.1.3 IDE 三选一

Leno 是标准 .NET 解决结构项目，三种主流 IDE 均可支持日常开发：

- **Visual Studio 2026**（Windows only）：开箱即用，含调试器、性能分析、SQL Server Object Explorer
- **JetBrains Rider**（跨平台）：智能补全、重构、内联调试体验最佳
- **VS Code + C# Dev Kit 扩展**（跨平台）：轻量，适合资源紧张的开发机

三种 IDE 均支持 F5 调试、`launchSettings.json` 读取、断点与条件断点、Hot Reload 等核心功能，按个人偏好选择即可。

### 2.1.4 Git 2.40+

Git 用于克隆仓库与提交变更，要求 2.40 或更高版本以支持 `git sparse-checkout`（仓库较大时可只克隆部分目录）。Windows 推荐 Git for Windows，macOS 推荐 Homebrew 安装：`brew install git`。

### 2.1.5 mise 安装与使用

`mise`（一个版本管理器，类似 nvm/asdf，但跨语言，用 `.tool-versions` 或 `mise.toml` 文件声明项目所需版本）用于统一管理 .NET、Node 等多语言运行时版本。在多项目并行开发场景下，mise 可根据当前目录自动切换 .NET 版本，避免全局版本污染。

安装与配置命令示例：

```bash
# 安装 mise（Windows）
winget install jdx.mise

# 安装 .NET 10 SDK
mise install dotnet@10.0.301
mise use dotnet@10.0.301  # 写入 mise.toml

# 验证
dotnet --version  # 应输出 10.0.301
```

执行 `mise use` 后，当前目录会生成或更新 `mise.toml` 文件，团队成员克隆仓库后执行 `mise install` 即可自动安装指定版本，保证环境一致性。

### 2.1.6 Docker 验证

完成 Docker 安装后，执行以下命令验证：

```bash
docker --version          # Docker 24.0+
docker compose version    # v2
```

若 `docker compose` 命令不存在，说明安装的是旧版 Docker，需升级或手动安装 [compose v2 插件](https://docs.docker.com/compose/install/)。

### 2.1.7 安装清单速查

完成所有前置依赖后，可执行以下命令一次性验证：

```bash
dotnet --version          # 10.0.301+
docker --version          # 24.0+
docker compose version    # v2
git --version             # 2.40+
mise --version            # 最新版
```

所有命令均输出符合版本要求的版本号即表示环境就绪，可继续 2.2 节启动 docker compose。

---

## 2.2 一键启动 docker compose

Leno 仓库根目录提供了完整的 `docker-compose.yml`，包含全部 11 个 BC、BFF 网关与 8 个第三方基础设施的容器定义。开发者首次拉取仓库后，只需一条命令即可启动所有基础设施。

### 2.2.1 启动命令

注意：实际在项目根目录执行，而非 `deploy/` 目录：

```bash
docker compose -f docker-compose.yml up -d
```

参数说明：

- `-f docker-compose.yml`：指定 compose 文件路径（根目录下可省略 `-f`）
- `up`：创建并启动容器
- `-d`：后台运行（detached mode），释放当前终端

### 2.2.2 8 个第三方基础设施组件详解

实际 `docker-compose.yml` 共 19 个 service（9 基础设施 + 11 BC API + 1 网关），本节聚焦 8 个第三方基础设施，11 BC 与网关的启动见 2.4 节。

`容器`（运行中的镜像实例）与`镜像`（容器只读模板，类似 OOP 中的类）是 Docker 的核心概念；`docker compose`（多容器编排工具，用 YAML 描述多服务依赖）则用于一次性管理所有容器。

| 服务 | 镜像 | 端口 | 用途 | 凭据 |
|---|---|---|---|---|
| sqlserver | mcr.microsoft.com/mssql/server:2022-latest | 1433 | 业务数据库 | sa/Your_password123 |
| redis | redis:7-alpine | 6379 | 缓存/分布式锁 | （无密码） |
| rabbitmq | rabbitmq:3-management | 5672/15672 | 消息队列 | guest/guest |
| elasticsearch | elasticsearch:8.11.0 | 9200 | 商品/订单搜索 | elastic/Your_password123 |
| consul | hashicorp/consul:1.18 | 8500 | 服务发现+配置中心 | （无 token） |
| jaeger | jaegertracing/all-in-one:1.50 | 16686 | 分布式追踪 | （无凭据） |
| prometheus | prom/prometheus:v2.48.0 | 9090 | 指标采集 | （无凭据） |
| grafana | grafana/grafana:10.2.0 | 3000 | 仪表盘 | admin/admin |

各组件角色说明：

- **sqlserver**：11 个 BC 的业务数据库均部署在同一 SQL Server 实例上，按数据库名隔离（如 `Leno_Cart`、`Leno_Order`）
- **redis**：用于缓存、分布式锁（`MigrateWithLockAsync`）、限流计数器
- **rabbitmq**：BC 间事件总线（如订单创建后通知库存、积分服务）；15672 端口提供管理 UI
- **elasticsearch**：商品与订单的全文检索，含分词、聚合
- **consul**：服务注册发现 + KV 配置中心，存储 InternalApiKey、CORS 白名单、gRPC 端点等
- **jaeger**：分布式追踪后端，所有 BC 通过 OpenTelemetry 上报 Trace
- **prometheus**：拉取各 BC `/metrics` 端点采集指标
- **grafana**：可视化面板，预置 Leno 业务与服务监控仪表盘

### 2.2.3 启动后验证

容器启动需要时间（首次拉取镜像约 5-15 分钟），可执行以下命令查看与验证：

```bash
docker compose ps                 # 查看状态（应全为 healthy）
docker compose logs -f sqlserver  # 查看某服务日志
docker compose down               # 停止全部
docker compose down -v            # 停止并删除数据卷（慎用）
```

`docker compose ps` 输出示例：

```
NAME                IMAGE                                    STATUS                     PORTS
leno-sqlserver      mcr.microsoft.com/mssql/server:2022      Up (healthy)               0.0.0.0:1433->1433/tcp
leno-redis          redis:7-alpine                           Up (healthy)               0.0.0.0:6379->6379/tcp
leno-rabbitmq       rabbitmq:3-management                    Up (healthy)               0.0.0.0:5672->5672, 15672
...
```

所有服务 STATUS 列均为 `Up (healthy)` 即表示启动成功，可继续 2.3 节健康检查与日志查看。

---

## 2.3 健康检查与日志查看

容器启动后，需通过健康检查确认每个组件真正可用。每个组件都有独立的健康检查方式。

### 2.3.1 各组件健康检查端点

| 组件 | 健康检查方式 |
|---|---|
| SQL Server | `sqlcmd -S localhost -U sa -P Your_password123 -Q "SELECT 1"` |
| Redis | `redis-cli ping`（应返回 PONG） |
| RabbitMQ | `curl http://localhost:15672/api/aliveness-test/%2F` |
| Elasticsearch | `curl http://localhost:9200/_cluster/health` |
| Consul | `curl http://localhost:8500/v1/status/leader` |
| Jaeger | `curl http://localhost:16686/` |
| Prometheus | `curl http://localhost:9090/-/healthy` |
| Grafana | `curl http://localhost:3000/api/health` |

示例：Redis 健康检查应返回 `PONG`：

```bash
$ docker exec -it leno-redis redis-cli ping
PONG
```

Consul 健康检查应返回 leader 地址：

```bash
$ curl -s http://localhost:8500/v1/status/leader
"127.0.0.1:8300"
```

### 2.3.2 docker compose ps 解读

`docker compose ps` 的 STATUS 列含义：

- `Up (healthy)`：运行中且健康检查通过
- `Up (health: starting)`：运行中但健康检查未完成
- `Up (unhealthy)`：运行中但健康检查失败
- `Restarting`：崩溃重启中

正常情况下，所有服务应在启动后 1-2 分钟内进入 `Up (healthy)` 状态。若长时间停留在 `health: starting` 或变为 `unhealthy`，需排查日志。

### 2.3.3 日志查看

查看单个服务日志：

```bash
docker compose logs -f sqlserver       # 跟踪 SQL Server 日志
docker compose logs --tail 200 redis   # 查看 Redis 最近 200 行
docker compose logs -t rabbitmq        # 带时间戳查看 RabbitMQ 日志
```

### 2.3.4 常见启动失败排查

5 项常见问题与排查方法：

1. **端口占用**：本地已有服务占用相同端口导致容器无法绑定。排查：`netstat -ano | findstr :1433` 找占用进程，结束进程或修改 `docker-compose.yml` 端口映射
2. **磁盘空间不足**：Docker 至少需 20GB 可用空间用于镜像与数据卷。排查：`docker system df` 查看 Docker 占用
3. **内存不足**：Docker Desktop 至少分配 4GB 内存。排查：Settings → Resources → Memory 调整
4. **镜像拉取失败**：网络问题或镜像源不可达。排查：配置国内镜像源（如阿里云 `https://<cr-id>.mirror.aliyuncs.com`）
5. **WSL 2 问题**（Windows）：WSL 2 内核过期或损坏导致 Docker Desktop 无法启动。排查：`wsl --update` 升级 WSL

---

## 2.4 仅启动基础设施模式

### 2.4.1 适用场景

日常开发中，开发者通常只调试单个 BC，无需启动全部 19 个 service。`仅启动基础设施模式`指：仅启动 8 个第三方基础设施 + BFF 网关，BC 由开发者在 IDE 中调试运行。此模式可大幅降低资源占用（节省约 2-4GB 内存），加快启动速度。

### 2.4.2 启动命令

仅启动 8 个基础设施 + 网关：

```bash
docker compose -f docker-compose.yml up -d sqlserver redis rabbitmq elasticsearch consul jaeger prometheus grafana
```

命令在 `up -d` 后显式列出要启动的 service 名称，未列出的 BC 容器不会启动。开发者可在 IDE 中单独调试需要修改的 BC。

### 2.4.3 IDE 配置

每个 BC 的 `Properties/launchSettings.json` 含 `applicationUrl` 端口配置，例如 Cart BC 端口 5103。该文件位于每个 BC 的 Api 项目下：

```
src/Services/Cart/Leno.Cart.Api/Properties/launchSettings.json
```

`launchSettings.json` 示例：

```json
{
  "profiles": {
    "Leno.Cart.Api": {
      "commandName": "Project",
      "applicationUrl": "http://localhost:5103",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### 2.4.4 调试单个 BC 步骤

以 Cart BC 为例，调试步骤：

1. 在 IDE 中打开 `src/Services/Cart/Leno.Cart.Api/Leno.Cart.Api.csproj`
2. 按 F5 启动（IDE 会读取 launchSettings.json）
3. 控制台输出 `Now listening on: http://localhost:5103`
4. 用 Postman 调用 `http://localhost:5103/api/cart`
5. 在 Controller 或 AppService 设断点调试

> 注：调试时需确保 `ASPNETCORE_ENVIRONMENT=Development`，且 `appsettings.Development.json` 中的连接字符串指向 localhost。

### 2.4.5 11 BC 端口表

| BC | 端口 | BC | 端口 |
|---|---|---|---|
| Product | 5101 | Order | 5106 |
| Promotion | 5102 | Payment | 5107 |
| Cart | 5103 | SellerShop | 5108 |
| Points | 5104 | ReviewAfterSales | 5109 |
| User | 5105 | Notification | 5110 |
| BFF 网关 | 8080 | | |

端口范围 5101-5110 为 BC，8080 为 BFF 网关，规划清晰便于记忆。开发者可同时调试多个 BC（不同端口），亦可同时启动 BFF 网关进行端到端联调。

---

## 2.5 连接字符串与凭据速查

### 2.5.1 数据库连接字符串模板

11 个 BC 的数据库连接字符串模板（以 Cart 为例）：

```
Server=localhost,1433;Database=Leno_Cart;User Id=sa;Password=Your_password123;TrustServerCertificate=True;MultipleActiveResultSets=true
```

参数说明：

- `Server=localhost,1433`：SQL Server 地址与端口
- `Database=Leno_Cart`：数据库名，每个 BC 独立
- `User Id=sa;Password=Your_password123`：开发环境默认凭据
- `TrustServerCertificate=True`：跳过证书校验（仅开发环境）
- `MultipleActiveResultSets=true`：启用多活动结果集，提升性能

### 2.5.2 11 BC 数据库名清单

| BC | 数据库名 |
|---|---|
| Product | Leno_Product |
| Promotion | Leno_Promotion |
| Cart | Leno_Cart |
| Points | Leno_Points |
| User | Leno_User |
| Order | Leno_Order |
| Payment | Leno_Payment |
| SellerShop | Leno_SellerShop |
| ReviewAfterSales | Leno_ReviewAfterSales |
| Notification | Leno_Notification |

数据库名遵循 `Leno_{BC}` 命名约定，便于识别与维护。

### 2.5.3 敏感凭据的本地存储方式

敏感凭据（如连接字符串、JWT SecretKey）的本地存储有两种方式：

- `appsettings.Development.json`：可含明文但仅本地，需加入 `.gitignore`
- `dotnet user-secrets`：.NET 提供的本地开发密钥管理工具

`dotnet user-secrets`（.NET 提供的本地开发密钥管理工具，机密存储在用户目录 `%APPDATA%\Microsoft\UserSecrets\{userSecretsId}` 而非项目，避免提交到 git）是推荐方式，将凭据与代码彻底分离，避免误提交。

### 2.5.4 user-secrets 使用示例

```bash
cd src/Services/Cart/Leno.Cart.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:CartDb" "Server=localhost,1433;Database=Leno_Cart;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:SecretKey" "your-super-secret-key-at-least-32-chars-long"
```

执行 `dotnet user-secrets init` 后，项目 `.csproj` 会新增 `<UserSecretsId>` 元素，配置读取时优先级高于 `appsettings.json`。其他常用命令：

```bash
dotnet user-secrets list           # 列出所有机密
dotnet user-secrets remove "Jwt:SecretKey"  # 删除某项
dotnet user-secrets clear          # 清空所有机密
```

---

## 2.6 数据库迁移操作

### 2.6.1 EF Core 概念

`EF Core`（Entity Framework Core，.NET 官方 ORM 框架，支持 LINQ 查询、变更跟踪、迁移）是 Leno 全部 BC 的数据访问层基础。Leno 采用 Code First 模式：先写实体类（C# 代码），再通过 `dotnet ef` 命令生成数据库结构与迁移文件。

Code First 工作流：

1. 在 `Leno.{BC}.Domain` 中定义实体类
2. 在 `Leno.{BC}.Infrastructure` 中编写 `DbContext` 与实体配置
3. 执行 `dotnet ef migrations add` 生成迁移文件
4. 执行 `dotnet ef database update` 应用迁移到数据库

### 2.6.2 迁移命令清单

| 命令 | 用途 |
|---|---|
| `dotnet ef migrations add <Name>` | 添加迁移 |
| `dotnet ef migrations remove` | 撤销最近迁移（未应用时） |
| `dotnet ef migrations list` | 列出所有迁移 |
| `dotnet ef database update` | 应用迁移到数据库 |
| `dotnet ef database update <Name>` | 回滚到指定迁移 |
| `dotnet ef migrations script` | 生成 SQL 脚本 |

### 2.6.3 添加迁移命令完整示例

以 Cart BC 为例，添加迁移命令：

```bash
dotnet ef migrations add AddItemRemark \
  --project src/Services/Cart/Leno.Cart.Infrastructure \
  --startup-project src/Services/Cart/Leno.Cart.Api \
  --output-dir Migrations
```

参数说明：

- `--project`：DbContext 所在项目（Infrastructure 层）
- `--startup-project`：启动项目（Api 层），用于读取连接字符串等配置
- `--output-dir`：迁移文件输出目录（默认 `Migrations`）

### 2.6.4 应用迁移命令

```bash
# 方式 1：手动应用
dotnet ef database update \
  --project src/Services/Cart/Leno.Cart.Infrastructure \
  --startup-project src/Services/Cart/Leno.Cart.Api

# 方式 2：启动时自动应用（生产推荐）
# Program.cs 中调用 MigrateWithLockAsync<CartDbContext>()
```

生产环境推荐方式 2，服务启动时自动应用迁移，避免人工操作遗漏。

### 2.6.5 MigrateWithLockAsync 机制详解

来自 [DatabaseMigrationExtensions.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Persistence/DatabaseMigrationExtensions.cs)：

基于 `IDistributedLockProvider` 的 Redis 分布式锁防止多实例并发迁移，lockKey 为 `db-migrate:{DbContextName}`，默认超时 30 秒。锁获取失败则跳过迁移（说明其他实例正在迁移）。

```csharp
public static async Task MigrateWithLockAsync<TDbContext>(
    this IServiceProvider services, TimeSpan? acquireTimeout = null, CancellationToken ct = default)
    where TDbContext : DbContext
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
    var lockProvider = scope.ServiceProvider.GetRequiredService<IDistributedLockProvider>();
    var lockKey = $"db-migrate:{typeof(TDbContext).Name}";
    await using var handle = await lockProvider.TryAcquireLockAsync(lockKey, timeout, ct);
    if (handle == null)
    {
        logger?.LogInformation("数据库迁移锁 {LockKey} 已被其他实例持有，跳过迁移", lockKey);
        return;
    }
    await db.Database.MigrateAsync(ct);
}
```

机制要点：

- **分布式锁**：基于 Redis（`IDistributedLockProvider` 来自 RedLock.net 库），保证集群中只有一个实例执行迁移
- **lockKey 命名**：`db-migrate:{DbContextName}`，每个 BC 独立锁，互不影响
- **超时 30 秒**：默认超时 30 秒，可通过 `acquireTimeout` 参数自定义
- **跳过而非等待**：锁获取失败直接跳过，不阻塞启动

### 2.6.6 迁移文件命名规范

迁移文件命名规范：`yyyyMMddHHmmss_PascalCaseName.cs`，例如 `20260717174927_InitialCreate.cs`。每个迁移含 3 个文件：

- `.cs`：迁移代码，含 `Up` 与 `Down` 方法
- `.Designer.cs`：当前模型的快照，用于 diff 生成新迁移
- `.sql`：SQL 预览，便于 DBA 审查

### 2.6.7 "仅追加"原则

"仅追加"原则：禁止删除或修改既有迁移文件，只允许新增。破坏性变更需分版本灰度（详见第 6 章 6.3 节）。

原因：

- 既有迁移已应用到生产数据库，删除会导致 EF Core 无法计算 diff
- 修改既有迁移可能使已应用的数据库进入不一致状态
- 新增迁移可通过 `Down` 方法回滚，安全可控

### 2.6.8 11 BC 迁移目录位置

11 个 BC 的 `Migrations/` 目录位置：`src/Services/{BC}/Leno.{BC}.Infrastructure/Migrations/`

例如：

- `src/Services/Cart/Leno.Cart.Infrastructure/Migrations/`
- `src/Services/Order/Leno.Order.Infrastructure/Migrations/`
- `src/Services/Product/Leno.Product.Infrastructure/Migrations/`

---

## 2.7 Consul KV 初始化

### 2.7.1 Consul 概念

`Consul`（一个分布式服务发现与 KV 配置中心工具，HashiCorp 出品，提供服务注册/发现/健康检查/KV 存储功能）在 Leno 中承担两个角色：

1. **服务发现**：BC 启动时向 Consul 注册，网关与其他 BC 通过 Consul 查询服务地址
2. **KV 配置中心**：存储 InternalApiKey、CORS 白名单、gRPC 端点等配置，支持热更新

Consul 默认监听 8500 端口，提供 HTTP API 与 Web UI（`http://localhost:8500/ui`）。

### 2.7.2 种子文件说明

`docs/consul-kv-seed.md` 文件说明：仓库中的 Consul KV 种子文件，包含所有需要初始化的 KV 清单与生成命令。开发者克隆仓库后，需按该文件指引执行初始化脚本，将 KV 写入本地 Consul。

### 2.7.3 必须初始化的 KV 清单

必须初始化的 KV 清单（4 类）：

| KV 路径 | 用途 | 示例值 |
|---|---|---|
| `leno/security/internal-key/{bc}` | 11 个 BC 的 InternalApiKey | openssl rand -base64 32 生成的 44 字符串 |
| `leno/cors/origins` | CORS 白名单 | `https://localhost:3000,https://buyer.leno.com` |
| `leno/grpc/endpoints/{bc}` | 6 个 BC 的 gRPC 端点 | `http://product-api:5101` |
| `leno/anticorruption/use-grpc/{bc}` | 6 个 BC 的 gRPC 开关 | `true` 或 `false` |

### 2.7.4 11 BC InternalApiKey 路径清单

11 个 BC 的 InternalApiKey 路径：

- `leno/security/internal-key/UserAuth`
- `leno/security/internal-key/Product`
- `leno/security/internal-key/Cart`
- `leno/security/internal-key/Order`
- `leno/security/internal-key/Promotion`
- `leno/security/internal-key/ReviewAfterSales`
- `leno/security/internal-key/PointsMembership`
- `leno/security/internal-key/Payment`
- `leno/security/internal-key/Notification`
- `leno/security/internal-key/SellerShop`
- `leno/security/internal-key/SystemAdmin`

InternalApiKey 用于 BC 间内部调用的鉴权，避免内部接口被外部直接访问。每个 BC 拥有独立的 key，互不影响。

### 2.7.5 6 BC gRPC 端点路径清单

6 个 BC 的 gRPC 端点路径（仅 6 个 BC 暴露 gRPC 服务，其余 BC 仅 HTTP）：

- `leno/grpc/endpoints/Product`
- `leno/grpc/endpoints/Order`
- `leno/grpc/endpoints/Payment`
- `leno/grpc/endpoints/Cart`
- `leno/grpc/endpoints/SellerShop`
- `leno/grpc/endpoints/ReviewAfterSales`

对应的 gRPC 开关路径：

- `leno/anticorruption/use-grpc/Product`
- `leno/anticorruption/use-grpc/Order`
- `leno/anticorruption/use-grpc/Payment`
- `leno/anticorruption/use-grpc/Cart`
- `leno/anticorruption/use-grpc/SellerShop`
- `leno/anticorruption/use-grpc/ReviewAfterSales`

开关为 `true` 时走 gRPC，为 `false` 时走 HttpClient 回退路径，便于降级与灰度切换。

### 2.7.6 初始化命令（PowerShell）

```powershell
# 生成 11 个 BC 的 InternalApiKey
$services = @('UserAuth','Product','Cart','Order','Promotion','ReviewAfterSales','PointsMembership','Payment','Notification','SellerShop','SystemAdmin')
foreach ($svc in $services) {
    $key = [Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
    $uri = "http://localhost:8500/v1/kv/leno/security/internal-key/$svc"
    Invoke-RestMethod -Method PUT -Uri $uri -Body $key
}
```

### 2.7.7 初始化命令（bash）

```bash
for svc in UserAuth Product Cart Order Promotion ReviewAfterSales PointsMembership Payment Notification SellerShop SystemAdmin; do
    key=$(openssl rand -base64 32)
    curl -X PUT "http://localhost:8500/v1/kv/leno/security/internal-key/$svc" -d "$key"
done
```

### 2.7.8 验证

访问 `http://localhost:8500/ui/dc1/kv` 查看所有 KV。也可通过 HTTP API 查询：

```bash
curl http://localhost:8500/v1/kv/?keys   # 列出所有 KV 路径
curl http://localhost:8500/v1/kv/leno/security/internal-key/Cart  # 查询 Cart 的 key
```

### 2.7.9 ConsulConfigWatcher 机制简介

来自 [ConsulConfigWatcher.cs](file:///c:/Users/Junjie/.trae-cn/worktrees/Leno/feat-project-optimization-plan-O7ECNx/src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs)：

监听 `leno/anticorruption/use-grpc/{bc}` KV 变更，5 分钟长轮询 + 10 秒重试，实现 1-2 秒热更新。无需重启服务即可切换 gRPC 开关。

工作机制：

1. **长轮询**：每 5 分钟向 Consul 发起长轮询请求，Consul 在 KV 变更时立即返回
2. **重试**：长轮询失败时 10 秒后重试，保证连接稳定
3. **热更新**：检测到 KV 变更后，更新内存中的 `UseGrpc` 标志，后续请求立即生效
4. **降级**：若 Consul 不可达，保持上一次配置不变，不影响业务

此机制使得在 Consul UI 中修改 `leno/anticorruption/use-grpc/Cart` 从 `true` 改为 `false` 后，1-2 秒内 Cart BC 的所有防腐层调用将从 gRPC 切换到 HttpClient，无需重启服务。

---

## 2.8 验证安装

### 2.8.1 4 项验证步骤

完成环境搭建后，通过以下 4 项验证确保安装成功：

#### 1. 网关健康检查

访问 `http://localhost:8080/health/ready`：

- 返回 `Healthy` 表示网关与下游 BC 就绪
- 返回 `Degraded` 或 `Unhealthy` 检查下游 BC 状态

示例响应：

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "self": { "status": "Healthy" },
    "product-api": { "status": "Healthy" },
    "cart-api": { "status": "Healthy" }
  }
}
```

#### 2. 网关聚合 Swagger

访问 `http://localhost:8080/swagger`：

- 应显示 11 个 BC 的 API 文档聚合
- 可在线测试 API

聚合 Swagger 通过 Ocelot 的 SwaggerGen 集成实现，开发者无需逐个访问 BC 的 Swagger，即可在网关层一站式查看与测试所有 API。

#### 3. Grafana 仪表盘

访问 `http://localhost:3000`（admin/admin）：

- 首次登录需改密码
- Dashboards 应含 `Leno Gateway Dashboard` 与 `Leno Business Services Dashboard`

`Leno Gateway Dashboard` 展示网关层 QPS、延迟、错误率；`Leno Business Services Dashboard` 展示各 BC 的请求量、异常计数、数据库连接数等。

#### 4. Jaeger 追踪

访问 `http://localhost:16686`：

- Service 下拉应含 11 个 BC + 网关
- 搜索一次请求的 Trace

通过 Trace 可查看请求在网关、BC、数据库、Redis 等组件间的完整调用链与耗时分布，便于定位性能瓶颈。

### 2.8.2 单个 BC 验证

直接访问 `http://localhost:5103/swagger`（Cart BC Swagger），应显示 Cart BC 的所有端点。其他 BC 验证方式相同，替换端口即可（参见 2.4.5 节端口表）。

### 2.8.3 故障排查清单

5 个常见问题与解决方案：

| 问题 | 可能原因 | 解决方案 |
|---|---|---|
| 网关 503 | 某个 BC 未启动 | `docker compose ps` 查看 BC 状态，重启失败服务 |
| 数据库连接失败 | 凭据错误或迁移未执行 | 验证连接字符串，执行 `dotnet ef database update` |
| Consul KV 空白 | 未执行初始化脚本 | 运行 2.7 节初始化命令 |
| Jaeger 无 Trace | OpenTelemetry 未配置或采样率 0 | 检查 `appsettings.json` 的 OTLP Endpoint |
| Grafana 无数据源 | Prometheus 未启动或抓取失败 | 检查 Prometheus 状态与 prometheus.yml |

每项问题均对应明确的排查路径，开发者可按表格顺序定位。

---

## 要点回顾

- 前置依赖：.NET 10 SDK（mise 管理）+ Docker + IDE 三选一 + Git 2.40+
- 一键启动：`docker compose -f docker-compose.yml up -d`，含 8 个第三方基础设施
- 基础设施模式：仅启动 8 个基础设施 + IDE 调试单个 BC，端口 5101-5110
- 数据库迁移：`dotnet ef migrations add` + `MigrateWithLockAsync` 分布式锁防并发
- Consul KV：4 类（internal-key/cors/grpc-endpoints/use-grpc），脚本初始化 + ConsulConfigWatcher 热更新
- 验证：网关 health/Swagger + Grafana + Jaeger 四项

## 常见问题

**Q1：Docker Desktop 启动失败（Windows）？**
A：升级 WSL 2（`wsl --update`），分配至少 4GB 内存，关闭 Hyper-V 冲突。

**Q2：dotnet ef 命令找不到？**
A：安装工具：`dotnet tool install --global dotnet-ef`，并确保 `%USERPROFILE%\.dotnet\tools` 在 PATH 中。

**Q3：迁移失败提示 "数据库已存在"？**
A：删除数据库重建：`dotnet ef database drop --force` 然后 `dotnet ef database update`。

**Q4：Consul KV 修改后服务未生效？**
A：ConsulConfigWatcher 5 分钟长轮询，可重启服务或缩短轮询间隔。

**Q5：本地调试时如何模拟 gRPC 调用？**
A：设置 `AntiCorruption:UseGrpc=false`（appsettings.Development.json），强制走 HttpClient 路径。

## 下一章衔接

第 3 章将介绍 Leno 的架构总览，包括 DDD 战略设计（限界上下文与上下文映射）、战术设计（聚合根/实体/值对象等）、共享内核、分层架构、CQRS 读写分离与微服务部署架构。
