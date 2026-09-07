# Consul KV 配置覆盖面审计（P0-A）

> 审计范围：`src/BuildingBlocks/`、`src/Services/`（各 BC Infrastructure 层）、`src/ApiGateway/`。
> 审计目标：确定每个配置键的生产注入方式（Consul KV 主通道 / env 引导 / env 兜底），作为
> `deployment.yaml` 改造（P0-B）、`seed-consul-kv.ps1`（P0-D）与启动 fail-fast 校验（P0-C）的依据。

## 1. 配置加载链与优先级（现状，已核实）

每个 BC 的 `Program.cs` 按以下顺序组装配置：

1. `WebApplication.CreateBuilder(args)` 默认链：`appsettings.json` → `appsettings.{Env}.json` → **环境变量**（`__` 映射为 `:`）→ 命令行。
2. `builder.Services.AddLenoApi<TDbContext>(...)`（`src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs`）
   在链尾追加 `ConsulReloadableConfigurationSource`（`ConsulReloadableConfigurationProvider`），
   用于 **AntiCorruption 灰度开关热更新**（运行期由 `ConsulConfigWatcher` 写入）。
3. `builder.AddLenoConsulConfig()`（`src/BuildingBlocks/Leno.Infrastructure.Persistence/Configuration/ConfigCenterExtensions.cs`）
   通过 Winton `AddConsul` 追加 Consul KV 源：前缀 **`leno/config/`**，`Optional=true`、`ReloadOnChange=true`、30s 长轮询。
   **该源位于链尾 → 优先级最高：KV > 环境变量 > appsettings.json。**
   Winton 默认 KeyConverter 将 KV 键中的 `__` 转换为 .NET 配置分隔符 `:`，
   即 KV 键 `leno/config/ConnectionStrings__OrderDb` → 配置键 `ConnectionStrings:OrderDb`。
4. `ConsulConfigWatcher` 监听 `leno/anticorruption/use-grpc/{bc}` 与 `leno/config/**`（灰度百分比、Schema 版本），
   变更经 `ConsulReloadableConfigurationProvider.SetValue` 触发 `IOptionsMonitor` 热重载。

**已确认决策：Consul KV（`leno/config/**`）是生产配置主通道**；所有 BC（19/19）均已调用 `AddLenoConsulConfig()`。
KV 不可达时 Winton 源 `Optional=true` 静默跳过，此时 env 兜底生效——因此
**敏感/关键键必须同时以 env 兜底注入（K8s Secret 引用），KV 种子化后 KV 值自动覆盖 env 值**。

## 2. 引导配置（不能走 KV：KV 配置源自身的依赖）

| 配置键（env 名） | 读取位置 | KV 是否覆盖 | 生产注入方式 |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | 宿主运行时 | ❌ | env 引导（values `global.environment`） |
| `Service:Name`（`Service__Name`） | `ConsulConfigWatcher`、`ConfigCenterExtensions.ValidateSensitiveConfig` | ❌（启动早期读取） | env 引导（helm `$name`） |
| `Consul:Url`（`Consul__Url`） | `ConfigCenterExtensions.AddLenoConsulConfig`（L117）、`ConsulServiceRegistrationExtensions`（L138）、ApiGateway（L44/103） | ❌（KV 源依赖它） | env 引导，值来自 Secret `leno-consul-address`（key=`url`）。**注意：旧 deployment 注入的是 `Consul__Address`，代码中无任何读取点，属键名错误，本审计确认应改为 `Consul__Url`** |
| `Consul:Token`（`Consul__Token`，可选） | 同上 | ❌ | env 引导（如启用 ACL） |
| `OpenTelemetry:OtlpEndpoint`（`OpenTelemetry__OtlpEndpoint`） | `Leno.Infrastructure.Telemetry/Telemetry/OpenTelemetryExtensions.cs`（L51） | 可走 KV 但无必要（非敏感、部署期已知） | env 引导（values `global.jaeger.otlpEndpoint`） |

## 3. 基础设施配置键（各 BC 实际读取点 → 生产注入方式）

### 3.1 数据库连接串（`ConnectionStrings:{Bc}Db`）

读取点：各 BC Infrastructure 层 `GetConnectionString("{Bc}Db")`，键名逐一核实如下：

| BC | 连接串键 | 读取位置（Infrastructure/Dependencies/ServiceCollectionExtensions.cs，除注明外） |
|---|---|---|
| AccessControl | `AccessControlDb` | L31 |
| AfterSales | `AfterSalesDb` | `Leno.AfterSales.Api/Program.cs` L31 |
| Cart | `CartDb` | L38 |
| Identity | `IdentityDb` | L43（UserCenter 也读 IdentityDb，见下） |
| Inventory | `InventoryDb` | L33 |
| Membership | `MembershipDb` | L23 |
| Notification | `NotificationDb` | L35 |
| Order | `OrderDb` | L44 |
| Payment | `PaymentDb` | L39 |
| Points | `PointsDb` | L24 |
| PointsMembership | `PointsMembershipDb` | L29 |
| Product | `ProductDb` | L34 |
| Promotion | `PromotionDb` | L33 |
| Review | `ReviewDb` | L37 |
| ReviewAfterSales | `ReviewAfterSalesDb` | L40 |
| SellerShop | `SellerShopDb` | L47 |
| SystemAdmin | `SystemAdminDb` | L42 |
| UserAuth | `UserAuthDb` | L42 |
| UserCenter | `UserCenterDb`（另读 `IdentityDb`） | L27 / L46 |

| KV 是否覆盖 | 生产注入方式 |
|---|---|
| ✅ `leno/config/ConnectionStrings__{Bc}Db` | **KV 主通道**；env 兜底：Secret `leno-db-connectionstrings`，**key={Bc}Db 与代码读取键严格一致**（旧模板用 key=服务名 + `ConnectionStrings__Default`，代码不读取，属 P0 缺陷） |

### 3.2 Redis

| 配置键 | 读取位置 | KV 是否覆盖 | 生产注入方式 |
|---|---|---|---|
| `Redis:Configuration` | BuildingBlocks `Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` L97（fallback `localhost:6379`）；ApiGateway `Extensions/RedisExtensions.cs`（fallback `ConnectionStrings:Redis` → `localhost:6379`） | ✅ `leno/config/Redis__Configuration` | KV 主通道 + env 兜底：Secret `leno-redis-connection`（key=`configuration`）。**缺失时静默回退 localhost，是生产事故隐患 → 纳入 P0-C 启动校验** |

### 3.3 RabbitMQ（MassTransit 事件总线）

| 配置键 | 读取位置 | KV 是否覆盖 | 生产注入方式 |
|---|---|---|---|
| `RabbitMQ:Host` | BuildingBlocks `ServiceCollectionExtensions.cs` L146（fallback `localhost`）、L211（健康检查） | ✅ | KV 主通道 + env 兜底：Host/Port 为非敏感值，来自 values `global.rabbitmq` |
| `RabbitMQ:Port` | 同上 L147（fallback 5672） | ✅ | env 兜底：values |
| `RabbitMQ:Username` | 同上 L148（fallback `guest`） | ✅ | env 兜底：Secret `leno-mq-rabbitmq`（key=`username`） |
| `RabbitMQ:Password` | 同上 L149（fallback `guest`） | ✅ | env 兜底：Secret `leno-mq-rabbitmq`（key=`password`） |
| `RabbitMQ:VirtualHost` | 同上 L150（fallback `/`） | ✅ | KV 主通道（默认值可接受，不强制 env） |
| `MassTransit:Retry:*` | 同上 L164-182 | ✅ | KV 主通道（有代码默认值） |

### 3.4 Elasticsearch（读模型）

| 配置键 | 读取位置 | KV 是否覆盖 | 生产注入方式 |
|---|---|---|---|
| `ConnectionStrings:ReadDb` / `Elasticsearch:Uri` | BuildingBlocks `ServiceCollectionExtensions.cs` L123-125（fallback `http://localhost:9200`）；`HealthChecks/HealthChecksUIExtensions.cs` L43 | ✅ | KV 主通道 + env 兜底：Secret `leno-es-connection`（key=`uri`，注入为 `Elasticsearch__Uri`） |
| `Elasticsearch:Nodes` / `Elasticsearch:Url` | SystemAdmin `Services/ElasticsearchRebuildTrigger.cs` L19-20 | ✅ | KV 主通道（SystemAdmin 专属，走 KV 即可） |

### 3.5 服务间地址（`ServiceUrls:*`）

读取点（均带 `localhost` fallback，ACL HTTP 通道）：

| 配置键 | 读取位置 |
|---|---|
| `ServiceUrls:ProductApi` | Order L82、Cart L65/L145 |
| `ServiceUrls:PromotionApi` | Order L83 |
| `ServiceUrls:PointsMembershipApi` | Order L84 |
| `ServiceUrls:OrderApi` | Review L59、ReviewAfterSales L64 |
| `ServiceUrls:PaymentApi` | ReviewAfterSales L63 |
| `ServiceUrls:AccessControlApi` | Identity L113/L152 |
| `ServiceUrls:UserAuthApi` | Notification L59 |

| KV 是否覆盖 | 生产注入方式 |
|---|---|
| ✅ `leno/config/ServiceUrls__{Api}` | KV 主通道 + env 兜底：非敏感，由 deployment 按 values `global.serviceUrls`（service + port）渲染为 `http://{release}-{service}:{port}` |

### 3.6 鉴权与安全

| 配置键 | 读取位置 | KV 是否覆盖 | 生产注入方式 |
|---|---|---|---|
| `Jwt:SecretKey` | `WebApplicationExtensions.AddLenoApi` L123（`GetSection("Jwt")`） | ✅ | KV 主通道 + env 兜底：Secret `leno-security-jwt`（key=`secret-key`）。**旧模板注入的键名是 `Security__Jwt__SecretKey`，代码不读取（代码读 `Jwt:SecretKey`），属键名错误** |
| `InternalAuth:ApiKey` | `InternalApiKeyOptions`（SectionName=`InternalAuth`）；Identity ServiceCollectionExtensions L155 | ✅ | KV 主通道 + env 兜底：Secret `leno-security-jwt`（key=`internal-api-key`） |
| `Security:InternalApiKey:{Bc}` / `Security:InternalApiKey:Shared` | `ConfigCenterExtensions.ValidateSensitiveConfig` L185/L189、Identity L156 | ✅ | KV 主通道（启动校验已有兜底逻辑）；`.env.example` 补 `LENO_INTERNAL_API_KEY_SHARED` 供本地/种子脚本引用 |
| `AntiCorruption:UseGrpc`、`AntiCorruption:TargetInternalApiKeys:{Bc}` | `WebApplicationExtensions` L74-75；各 BC ACL 注册 | ✅（含热更新 KV `leno/anticorruption/use-grpc/{bc}`） | KV 主通道（灰度开关），env 兜底可选 |

### 3.7 业务 Options（Payment/SMS/OAuth2/Order/RefreshToken/Cart 等）

各 BC `Configure<T>(GetSection(...))` 绑定：`Payment:Alipay:*`、`Payment:WeChatPay:*`、`SMS:*`、`OAuth2:*`
（含 `OAuth2:PublicBaseUrl`，对应 env `OAUTH2_PUBLIC_BASE_URL`）、`Order:*`、`RefreshToken:Provider`、`Cart:UseSkuSnapshot` 等。
全部 ✅ KV 覆盖（`leno/config/...`），敏感子键已在 `ConfigCenterExtensions.SensitiveConfigKeys` 登记，
`ValidateSensitiveConfig` 启动校验（生产缺失即抛异常）已覆盖。不重复注入 env。

## 4. 结论（驱动 P0-B/C/D）

1. **KV 键清单**（`seed-consul-kv.ps1` 需覆盖）：§3 全部 ✅ 键，KV 键名 = `leno/config/` + 配置键（`:` → `__`）。
2. **env 兜底清单**（deployment.yaml 注入，Secret key 与代码读取键严格一致）：
   `ConnectionStrings__{Bc}Db`、`Redis__Configuration`、`RabbitMQ__Host/Port/Username/Password`、
   `Elasticsearch__Uri`、`ServiceUrls__*`、`Jwt__SecretKey`、`InternalAuth__ApiKey`、`Consul__Url`。
3. **修复两处键名错误**：`Consul__Address` → `Consul__Url`；`Security__Jwt__SecretKey` → `Jwt__SecretKey`；
   连接串 Secret key 由服务名（`order`）改为连接串键（`OrderDb`）。
4. **P0-C fail-fast 范围**（非 Development 环境，缺失或仍为 localhost 默认值时拒绝启动）：
   `ConnectionStrings:{Bc}Db`（按 BC 各自的键）、`Redis:Configuration`、`RabbitMQ:Host`。
