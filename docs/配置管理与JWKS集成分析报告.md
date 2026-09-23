# Leno 配置管理机制分析与 JWKS 集成评估

> 分析日期：2026-09-23 ｜ 范围：全 16 个 BC 的配置组织与加载、与 Identity（JWKS 签发方）的集成、RS256 切换后的配置维护
> 结论先行：**机制上无缝，数据上有缺口（本轮已修复 3 处 P0），密钥轮换需补多钥发布（P1）**

---

## 一、现状：配置管理的三层机制

### 层 1 —— 本地文件（兜底与本地开发）

| 项 | 说明 |
|---|---|
| 文件 | 每个 BC 一份 `appsettings.json`（本地默认值）+ `appsettings.Docker.json`（compose 覆盖） |
| 组织 | 按节：`ConnectionStrings` / `ServiceUrls` / `Consul` / `OpenTelemetry` / `Jwt` / `JwtSigning`（Identity）/ `AntiCorruption` 等 |
| 加载 | 标准宿主链：appsettings.json → appsettings.{Env}.json → 环境变量 → 命令行 |
| 占位符 | 值支持 `${ENV_VAR}`，由 `ConfigCenterExtensions.ResolvePlaceholders` 统一解析（缺失时保留原文本便于排查） |

### 层 2 —— Consul KV 配置中心（生产主通道）

| 项 | 说明 |
|---|---|
| 接入 | **全部 16 个 BC** 的 `Program.cs` 调用 `AddLenoConsulConfig()`（Winton.Extensions.Configuration.Consul） |
| 前缀 | `leno/config`（KV 键名 = 配置键，`:` → `__`） |
| 行为 | `Optional = true`（Consul 不可达回退本地，生产降级 warning 不阻断）；`ReloadOnChange = true`；`PollWaitTime = 30s` |
| 优先级 | **KV 键 > 环境变量兜底**（Winton 源位于配置链尾，最后加载者胜） |
| 种子 | `deploy/consul/kv-seed.json`（`seed-consul-kv.ps1` 写入）：连接串 16 DB + SchedulerDb、Redis、RabbitMQ、ES、`ServiceUrls__*`、`Jwt__*`、内部 API 密钥；**敏感值一律引用环境变量，文件内禁止明文**；非敏感项允许 `default` 兜底 |
| 启动校验 | 双闸：`ValidateSensitiveConfig`（16 BC Program 启动调用，**非 Dev/Testing 校验失败拒绝启动**）+ `LenoStartupConfigurationValidator`（关键键缺失或仍是本地默认值时 fail-fast，Dev/Testing 跳过） |

### 层 3 —— 运行时热更新（watcher + 灰度 + 版本化）

- **`ConsulConfigWatcher`**（BackgroundService）：long-poll `leno/anticorruption/use-grpc/{bc}`（WaitTime 5 分钟，失败 10s 重试）；变更经 `ConsulReloadableConfigurationProvider.SetValue` 触发 `OnReload` → `IOptionsMonitor` 感知。当前唯一监听键是 gRPC 双轨开关。
- **`ConsulConfigPublisher`**：版本化发布骨架已就位 —— `leno/config/{env}/{service}` + `schema-version` + `history/{version}` + `gray-percent`。
- **`ConsulConfigSchemaValidator` / `ConsulGrayReleaseService`**：JSON 配置的 schema 版本校验、按实例 ID 的灰度命中判定（watcher 可选注入）。

### 典型使用场景

1. **基础设施连接**（DB×17、Redis、RabbitMQ、ES）→ KV（生产）+ appsettings（本地）
2. **服务间地址**（`ServiceUrls__*`）→ KV，default 指向 K8s Service 稳定名
3. **敏感参数**（支付/短信/OAuth 密钥、JWT RSA PEM）→ appsettings 写 `${ENV_VAR}` 占位符，真实值走环境变量，禁止明文落盘
4. **行为开关**（gRPC 双轨切换）→ watcher 监听的独立 KV 键，运行时热切换
5. **配置版本化与灰度**（publisher/history/gray-percent）→ 骨架已就位

---

## 二、与 Identity 集成的可行性评估

### ✅ 无缝的部分

- Identity 本身就在 `AddLenoConsulConfig()` 名单内，RSA 私钥/公钥经 `JwtSigning` 节的标准 options 绑定 + `${ENV_VAR}` 占位符获取（`AzureKeyVaultKms` 生产 / `EnvironmentKms` 开发回退），完全符合"敏感值禁明文"约束；
- JWKS 是 HTTP 端点，**不依赖配置中心** —— 消费方只需一个 `Jwt:DiscoveryUrl`；
- 侦察确认：Identity 签发早已委托 `IJwtSigningService`（RS256 基建完整），集成只差"配置指到位 + 端点发布"，已在 A6 完成。

### ⚠️ 发现的缺口（本轮已修复 3 处 P0）

| # | 缺口 | 影响 | 修复 |
|---|------|------|------|
| 1 | `kv-seed.json` 残留 `Jwt__SecretKey`（A6 已删该键） | 旧对称密钥继续存活于 KV，配置语义混乱 | 已替换为 `Jwt__DiscoveryUrl`（default 指向 `leno-identity-api` 稳定服务名） |
| 2 | `SensitiveConfigKeys` 仍含 `Jwt:SecretKey` | **16 个 BC 在 staging/prod 启动校验失败拒绝启动**（该键已被 A6 从所有配置删除） | 已改为 `Jwt:DiscoveryUrl` |
| 3 | `TestWebHostHelper.SensitiveConfigPlaceholders` 与清单漂移 | 测试宿主校验失败 | 已同步补 `Jwt:DiscoveryUrl` 占位 |

### 风险点（未消除，需按建议跟进）

| 级别 | 风险 | 说明 |
|---|---|---|
| 高 | **清单与种子数据漂移** | 本次"删键没同步校验清单/种子"正是实例。建议做 CI 校验脚本：`SensitiveConfigKeys` ↔ `kv-seed items` ↔ 各 BC appsettings 的键三方一致性（类似已有的端口校验） |
| 中 | `RequireHttpsMetadata` 默认 false | 生产若启 TLS 应置 true；建议加环境门禁（IsProduction 且非显式 false 时 warn/fail） |
| 中 | `DiscoveryUrl` 指向漂移地址 | 若误配 Pod IP/localhost，调度后失效。约束：只允许 K8s Service / compose 服务名 |
| 低 | Identity 成为 JWKS 单点 | 消费方有长缓存兜底（见下），但**冷启动**依赖 Identity 可用 → 多副本 + 健康检查已具备，需纳入部署约束 |
| 低 | KV 的 `GrpcEndpoints` 键名未随 A1/A6 改名（UserAuth→Identity、PointsMembership→Points） | 仅在启用 gRPC 轨前需要同步 |

---

## 三、JWKS 统一验签后的配置组织与维护要点

### 1. 公钥获取（消费方视角）

每个非 Identity 服务的**唯一**密钥类配置：

```json
"Jwt": {
  "Issuer": "leno-identity",
  "Audience": "leno-clients",
  "DiscoveryUrl": "http://leno-identity-api:8080/.well-known/openid-configuration",
  "RequireHttpsMetadata": false
}
```

获取链：`DiscoveryUrl` → OIDC 发现文档 → `jwks_uri` → JWKS（`kid` 与令牌头匹配）。生产经 KV 下发（`Jwt__DiscoveryUrl`），本地/测试走 appsettings。

### 2. 缓存与刷新

.NET `JwtBearer` 的 `ConfigurationManager` 默认行为（运维须知）：

| 行为 | 默认值 | 说明 |
|---|---|---|
| 自动刷新 | `AutomaticRefreshInterval` = **12 小时** | 到期后下次验签强制重拉发现文档与 JWKS |
| 失败重试 | `RefreshInterval` = **5 分钟** | 拉取失败按此间隔重试 |
| 容错 | 拉取失败沿用**上次成功缓存** | 不阻断验签；仅新实例/缓存为空时失败 |

建议：轮换期间把 `AutomaticRefreshInterval` 显式调至 1h、`RefreshInterval` 30s；并在 `JwtBearerEvents.OnAuthenticationFailed` 中检测 `kid` 未命中时调用 `ConfigurationManager.RequestRefresh()` 做即时刷新（避免"轮换后等 12h"的坑）。

### 3. 密钥轮换（当前最大缺口 → P1）

**现状风险**：JWKS 只发布**一把**当前公钥（`CurrentKeyId`）。轮换瞬间，"新私钥签的令牌"撞上"消费方缓存的旧公钥" → 大面积 401，持续到所有实例缓存刷新完毕。

**建议改法**（基建已具备）：`IKeyManagementService.ListKeyVersionsAsync` 已存在，JWKS 改为发布**多把**公钥（当前 + 上一把），消费方按 `kid` 匹配。四步轮换 runbook：

1. 新私钥（`key-v2`）注入 KMS（AKV 新版本 / 新环境变量）；
2. JWKS **同时发布 v1 + v2**（此步零风险，旧令牌仍验得过）；
3. 切 `JwtSigning:CurrentKeyId = "key-v2"` 开始用新钥签发（KV 热更新或滚动重启）；
4. 观察一个"令牌 TTL（30min）+ 最大缓存刷新周期（12h）"后，从 JWKS 移除 v1。

### 4. 故障处理

| 故障 | 行为 | 处置 |
|---|---|---|
| Identity 不可达 | 消费方沿用缓存公钥继续验签；30min 令牌 TTL 内用户无感 | Identity 多副本 + k8s 探针已具备；建议 JWKS 端点加 `Cache-Control: public, max-age=300` 输出缓存降压 |
| 消费方冷启动且 Identity 不可达 | 首次拉取失败 → 验签失败 | 部署顺序约束：Identity 先于消费者就绪；或加启动重试 |
| RSA 私钥缺失（PEM env 没配） | `EnvironmentKms` 抛异常 **fail-closed** ✓ | 正确行为；AKV 路径需确认网络策略放行 + `DefaultAzureCredential` 可用 |
| 私钥泄露 | 轮换 = 上述四步；黑名单（JwtBlacklistMiddleware）可即时吊销当前令牌 | 轮换后旧令牌自然过期（≤30min） |

---

## 四、结论与改进建议

### 结论

1. **机制成熟**：本地文件 → Consul KV（16 BC 全接入、热重载、30s 轮询）→ watcher 热更新（灰度 + 版本化骨架）三层职责清晰；敏感参数"env 占位符 + 启动校验 fail-closed"的纪律执行到位。
2. **与 Identity 集成无缝**：A6 之后密钥配置收敛为每服务一个 `Jwt:DiscoveryUrl`（KV 下发），配置面比 HS256 时代更小；Identity 侧签发/密钥管理基建完整。
3. **数据侧曾有三处 A6 遗留缺口，本轮已修复**（kv-seed 残留键、启动校验清单漂移、测试占位漂移）——其中校验清单漂移会直接导致 staging/prod 全量拒绝启动，属 P0。
4. **唯一的功能性缺口是密钥轮换**：单钥 JWKS 会让轮换窗口出现大面积 401，需要多钥发布支持。

### 改进建议（按优先级）

| 优先级 | 建议 | 状态 |
|---|---|---|
| **P0** | kv-seed `Jwt__SecretKey` → `Jwt__DiscoveryUrl`；`SensitiveConfigKeys` 同步；测试占位同步 | ✅ 本轮已修 |
| **P1** | JWKS 多钥发布（当前+上一把，`kid` 匹配）+ 四步轮换 runbook；消费方调 `AutomaticRefreshInterval`/`RefreshInterval`；`kid` 未命中即时 `RequestRefresh()` | ✅ 已完成（见执行记录） |
| **P1** | CI 一致性校验：`SensitiveConfigKeys` ↔ kv-seed ↔ appsettings 键三方比对（复用端口校验脚本的模式） | ✅ 已完成 |
| P2 | JWKS 端点输出缓存（max-age=300）；`RequireHttpsMetadata` 生产环境门禁 | ✅ 已完成 |
| P2 | kv-seed 补 `JwtSigning__RsaPrivateKeyPem/PublicPem`（env 引用）或明确 AKV-only 并完成 G2 例外登记 | ✅ 已完成（2026-09-24）：**定案为 AKV-only** —— 生产经 Azure Key Vault（`UseAzureKeyVault=true` + `KeyVaultUri`），PEM 环境变量仅用于本地/CI 的 `EnvironmentKms` 回退，**不进 kv-seed**（kv-seed 是配置分发物，不应承载密钥材料）；G2 例外登记见 **ADR-0010**（云依赖约束与 AKV 例外，含范围/审计/回退） |
| P2 | KV `GrpcEndpoints` 键名改名（UserAuth→Identity、PointsMembership→Points），启用 gRPC 轨前执行 | ✅ 核实无文件残留（KV 侧启用时按新键名写入） |


---

## 附：清理执行记录（2026-09-23，按本报告改进建议清除残留影响）

> 清理范围 = HS256→RS256 过渡机制、三个退役 BC（UserAuth/PointsMembership/ReviewAfterSales）
> 在代码/配置/部署/CI 四个面的全部残留。**历史溯源注释（"从 UserAuth BC 迁入"等 ~100 处）与
> 活配置（InternalAuth__ApiKey、Jwt:Enabled、ObjectStorage SecretKey）经核对后保留，未误删。**

### 已清除的每一项影响 ↔ 对应建议条目

| # | 清除项 | 文件 | 对应条目 |
|---|--------|------|---------|
| 1 | kv-seed `Jwt__SecretKey` → `Jwt__DiscoveryUrl` | deploy/consul/kv-seed.json | P0-①（上轮已修，本轮核对） |
| 2 | `SensitiveConfigKeys`：Jwt:SecretKey → Jwt:DiscoveryUrl | ConfigCenterExtensions.cs | P0-②（上轮已修） |
| 3 | 测试占位字典同步 | TestWebHostHelper.cs | P0-③（上轮已修） |
| 4 | Identity appsettings 删 `SigningKey`/`Hs256SigningKey`/`SigningMode` | Leno.Identity.Api/appsettings.json | P0 + D-6 |
| 5 | 删 `JwtTokenService.BuildValidationParameters()`（HS256 回退路径）+ 相关注释 | Identity.Application/Services/JwtTokenService.cs | D-6 |
| 6 | 删 Identity `JwtOptions.SigningKey` 属性 | Identity.Application/JwtOptions.cs | D-6 |
| 7 | ConfigCenterExtensionsTests 键名同步（6 处） | Infrastructure.Tests/Configuration/… | P0-② |
| 8 | SystemAdminApiFactory 删 `Jwt:SecretKey` 注入；Issuer/Audience `Leno.UserAuth`/`Leno.Clients` → `leno-identity`/`leno-clients` | SystemAdmin.Api.Tests/SystemAdminApiFactory.cs | P0 + A6 |
| 9 | 网关 4 个集成测试删 `Jwt:SecretKey` 注入与过时 HS256 注释 | ApiGateway.Tests/Integration/…×4 | D-6 |
| 10 | **RsaJwtSigningService 删 Hs256/Dual 分支**（HS256 签发/验签/回退、SigningModeValue、NormalizeMode） | Infrastructure.Auth/Security/RsaJwtSigningService.cs | D-6 单算法 |
| 11 | **JwtSigningOptions 删 `SigningMode`/`Hs256SigningKey`** | Infrastructure.Auth/Security/JwtSigningOptions.cs | D-6 |
| 12 | RsaJwtSigningServiceTests 删 20 个 Hs256/Dual 用例（保留 RS256 全覆盖） | Infrastructure.Tests/Security/… | D-6 |
| 13 | Helm `secret.yaml` 删 jwt secretKey fail 渲染与 `secret-key` 注入（internalAuth 保留） | deploy/helm/leno/templates/secret.yaml | P2-部署清理 |
| 14 | Helm `deployment.yaml` 删 `Jwt__SecretKey` env 块 + 鉴权引导注释更新 | deploy/helm/leno/templates/deployment.yaml | P0-① |
| 15 | Helm `values.yaml` 删 `UserAuthApi` serviceUrl + userauth 注释；**补 `IdentityApi`**（Notification→Identity 依赖） | deploy/helm/leno/values.yaml | A6 残留 |
| 16 | kv-seed **补 `ServiceUrls__IdentityApi`**（同上，K8s 侧） | deploy/consul/kv-seed.json | A6 残留 |
| 17 | Helm `values-dev/prod/staging.yaml` 删 userauth/reviewaftersales/pointsmembership 块（3 文件 × 3 块） | deploy/helm/leno/values-*.yaml | A1/A2/A6 残留 |
| 18 | `cd.yml` SERVICES 删 3 个退役名（15 → 12） | .github/workflows/cd.yml | A1/A2/A6 残留 |
| 19 | `check-migrations.ps1` / `generate-migration-scripts.ps1` 删 3 BC 条目 | scripts/… | A1/A2/A6 残留 |
| 20 | `coverage-thresholds.json` 删 2 个退役豁免 | scripts/coverage-thresholds.json | A1/A2 残留 |
| 21 | `validate-service-ports.ps1` 删 2 个退役映射、补 `IdentityApi=identity` | scripts/validate-service-ports.ps1 | A1/A6 残留 |
| 22 | HealthChecksUI 删 "UserAuth Service" 健康检查端点 | HealthChecksUIExtensions.cs | A6 残留 |
| 23 | `staging/README.md`：JWT_SECRET_KEY → RSA PEM 说明、Secret 命令、BC 数 19→16 | deploy/staging/README.md | P0-① |
| 24 | Points/Membership 删 `PointsMembershipSplit` 配置节与双轨注释 | Points/Membership Api | A1 残留 |

### 保留项（核对后确认非残留）

- `InternalAuth__ApiKey` / `InternalAuth:ApiKey`：活配置（Identity 启动校验、InternalApiKeyMiddleware、
  Order 的 GrpcInventoryGateway、Inventory 的 gRPC 拦截器在用）；
- `Jwt:Enabled`：网关验签功能开关（网关 Program 读取，测试用于禁用验签）；
- `ObjectStorageService` 的 `SecretKey`：文件存储加密密钥，与 JWT 无关；
- "从 UserAuth BC 迁入…"等溯源注释（~100 处）：代码来源说明，有文档价值；
- `deploy/docs/consul-kv-coverage-audit.md`、`production-infrastructure-plan.md`：历史审计文档，保留原貌。

### 清理后状态

```
全量构建 Leno.slnx            -> Build succeeded, 0 Error
Leno.Infrastructure.Tests    -> 581/581 ✅（601→581：删 20 个 Hs256/Dual 用例）
Identity.Application.Tests   -> 131/131 ✅
SystemAdmin.Api.Tests        -> 61/61 ✅
残留扫描                      -> 功能性引用清零（仅存解释性注释与历史文档）；
                                Jwt:DiscoveryUrl / InternalAuth__ApiKey 各就各位
```

### 后续改进（待确认后执行，见上文建议表）

P1：JWKS 多钥发布 + 四步轮换 runbook + 刷新参数调优；CI 三方键一致性校验。
P2：JWKS 输出缓存；`RequireHttpsMetadata` 生产门禁；KV `GrpcEndpoints` 键名改名。


---

## 附：后续改进执行记录（2026-09-23，P1×3 + P2×3 全部落地）

> 用户确认清理无误后执行。原建议表中的 P1/P2 状态全部更新为 ✅。

### P1-1 JWKS 多钥发布 + 轮换 runbook ✅

- `JwtSigningOptions` 新增 `PreviousKeyIds`（逗号分隔的轮换重叠钥，默认空）
- `JwksController` 改为发布 **CurrentKeyId + PreviousKeyIds 去重后的多把公钥**：
  当前钥在前；旧钥在 KMS 不可用时跳过并告警（轮换第 4 步后属预期）；当前钥不可用则拒绝
- 轮换 runbook：`deploy/docs/jwt-key-rotation-runbook.md`（四步法：新钥入 KMS → 原子切换
  CurrentKeyId+PreviousKeyIds → 观察 TTL+刷新周期 → 移除旧钥；含回滚与常见问题表）

### P1-2 消费方刷新调优 + kid 未命中即时刷新 ✅

- `WebApplicationExtensions`（全 BC）与网关 `Program.cs` 的 JwtBearer 统一增加：
  - `AutomaticRefreshInterval = 1h`（默认 12h，轮换后公钥滞后窗口上限）
  - `RefreshInterval = 30s`（默认 5min，拉取失败重试间隔）
  - `OnAuthenticationFailed`：捕获 `SecurityTokenSignatureKeyNotFoundException`（kid 未命中）
    时调用 `ConfigurationManager.RequestRefresh()`，下一次请求即用新公钥

### P1-3 CI 配置键三方一致性校验 ✅

- 新增 `scripts/validate-config-keys.ps1`（复用端口校验脚本模式）：
  1. 16+1 个 appsettings：Jwt 节键集合 = {Issuer, Audience, DiscoveryUrl, RequireHttpsMetadata}（无退役键），
     Issuer/Audience 统一、DiscoveryUrl 指向 openid-configuration
  2. `JwtSigning` 节仅 Identity 可有，且无 SigningMode/Hs256SigningKey 残留
  3. kv-seed：无退役 BC 键、无 Jwt__SecretKey；含 Jwt__DiscoveryUrl 与 ServiceUrls__IdentityApi
  4. `SensitiveConfigKeys`（从代码正则提取）：含 Jwt:DiscoveryUrl、无 Jwt:SecretKey，
     且每个键在 appsettings / kv-seed / 代码字面量三方之一出现（防"清单有、配置无"）
- 首次运行即通过：appsettings×17、kv-seed 键×35、SensitiveConfigKeys×13

### P2-1 JWKS 端点输出缓存 ✅

- `JwksController` 两个端点加 `[ResponseCache(Duration = 300)]`（JWKS 仅在轮换时变化，
  代理层可安全缓存；消费方另有 1h 自动刷新兜底）

### P2-2 `RequireHttpsMetadata` 生产门禁 ✅

- 共享 `WebApplicationExtensions`：环境名取 `ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT`
  （缺省即 Production），**IsProduction 且 RequireHttpsMetadata=false 时拒绝启动**
- 网关 `Program.cs` 同样的门禁（IsProduction + false → throw）

### P2-3 KV `GrpcEndpoints` 键名 ✅（核实为无文件残留）

- 全仓扫描确认：`GrpcEndpoints` 的旧 BC 名（PointsMembership/UserAuth）**不存在于任何配置文件** ——
  该 KV 值从未被 seed（启用 gRPC 轨时才手工写入）。处置：写入轮换 runbook 的运维注意事项，
  真正启用 gRPC 轨前按新键名（Identity/Points）写入 Consul 即可

### 验证

```
全量构建 Leno.slnx          -> Build succeeded, 0 Error
Leno.ApiGateway.Tests      -> 276/276 ✅
Leno.Infrastructure.Tests  -> 581/581 ✅
validate-config-keys.ps1   -> 通过（appsettings×17、kv-seed×35、SensitiveConfigKeys×13）
```
