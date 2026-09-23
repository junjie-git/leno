# ADR-0010: 云依赖约束与 Azure Key Vault 例外登记

## 状态
已接受（2026-09-24，G2 收口；对应硬约束 #12）

## 上下文

2026-09-21 的双轨审计报告 G2 指出：`Leno.Infrastructure.Auth` 引入 `Azure.Identity` /
`Azure.Security.KeyVault.Keys`，且经元包传导编译进**全部服务产物**，与"不使用任何云服务、
全本地自建"的架构约束冲突。

侦察后确认两点事实：

1. **该约束从未写成文** —— `docs/handbook/01-project-overview.md` §1.5.5 的硬约束清单没有此项，
   项目 memory 为空，原始项目 brief 中也无此表述。它只作为审计报告的前提被引用过一句。
   → 所以本项工作不是"修订某条目"，而是**把约束正式写下来并同时登记例外**。
2. **项目确实存在云相关的外部集成**（短信渠道的阿里云/腾讯云 SDK 适配、可选的对象存储 OSS）。
   它们与"基础设施云托管"是两类问题，需要分别对待，否则约束会自相矛盾。

2026-09-21 已决策：**保留 AKV 实现**（可选、默认关闭），并补两件事 —— 修订约束、
在合规检查登记例外。本 ADR 完成这两件事（决策点 D-2 的口径：AKV 例外登记，
运行时基础设施仍全自建；自建 Vault 的运维复杂度与收益不成比例）。

## 决策

1. **运行时基础设施全自建**：数据存储、消息、缓存、搜索、服务发现、网关
   （SQL Server / Redis / RabbitMQ / Elasticsearch / Consul / YARP）一律自托管，
   **不引入云托管中间件**；跨 BC 通信自建（MassTransit + Outbox）。
2. **密钥托管为登记例外**：允许 **Azure Key Vault** 作为 RSA 签名密钥的托管后端（HSM），
   但**可选且默认关闭**；默认路径是 `EnvironmentKms`（环境变量 PEM，用于本地/CI）。
3. **保留自建 Vault 迁移路径**：`IKeyManagementService` 是唯一抽象，切换后端 = 换一个实现 +
   改 DI 注册，调用方（`RsaJwtSigningService` / `JwtTokenService` / JWKS 端点）无感。
4. **外部服务商 SDK 不在本约束的硬闸范围**：如短信渠道 SDK，属"调用外部商业服务"而非
   "基础设施云托管"；但仍须通过 `INotificationChannel` / `ISmsChannel` 适配隔离，
   避免云 SDK 类型渗透到 Application/Domain 层。

## 例外登记详情

### 范围（精确到包/代码/配置）

| 维度 | 内容 |
|---|---|
| NuGet 包 | `Azure.Identity` 1.14.2、`Azure.Security.KeyVault.Keys` 4.6.0（**仅** `src/BuildingBlocks/Leno.Infrastructure.Auth/Leno.Infrastructure.Auth.csproj` 声明）|
| 代码 | `Leno.Infrastructure.Auth/Security/AzureKeyVaultKms.cs`（`IKeyManagementService` 实现）；`Leno.Identity.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 中 `AddSingleton<IKeyManagementService>` 的 AKV 分支（`new DefaultAzureCredential()`）|
| 配置键 | `JwtSigning:UseAzureKeyVault`（默认 `false`）、`JwtSigning:KeyVaultUri`（默认空）、`JwtSigning:CurrentKeyId`、`JwtSigning:PreviousKeyIds`（仅 Identity 的 `appsettings.json`）|
| 激活条件 | `UseAzureKeyVault=true` **且** `KeyVaultUri` 非空 **且** `DefaultAzureCredential` 可用；当前所有环境均为 false / 空 |
| 传导范围 | Azure SDK 随全部服务产物分发（元包传导）—— **已接受状态**，不再视为缺陷 |
| 运维文档 | `deploy/docs/jwt-key-rotation-runbook.md`（AKV 密钥轮换四步法）|

### 审计

- **自动**：`scripts/check-cloud-dependencies.sh` —— 扫描 `src/**/*.csproj` 的 `PackageReference`
  云厂商前缀（`Azure.` / `Microsoft.Azure.` / `Amazon.` / `AWSSDK.` / `Google.Cloud.` /
  `Google.Apis.` / `AlibabaCloud.` / `aliyun` / `TencentCloud` / `HuaweiCloud.`），
  白名单**只含上表两个包**；由 CI job `compliance-checks` 执行，命中白名单外即失败。
- **人工**：新增任何云依赖（包 / 激活分支 / 配置键）必须同时更新本 ADR 与脚本白名单；
  **两者不一致即视为违规**（脚本的提示信息已写明这一点）。
- **已知盲区（诚实声明）**：
  1. 脚本只看 `PackageReference` 声明处，不检测运行期 HTTP 调用外部云 API；
  2. `web/` 前端 npm 依赖不在扫描范围；
  3. 外部服务商 SDK（短信/OSS）按决策 4 不设硬闸，靠 code review。

### 回退

1. **切回全自建（无需构建）**：`JwtSigning:UseAzureKeyVault=false`（当前默认）→ 立即走
   `EnvironmentKms`。
2. **彻底移除云依赖**：删除 `AzureKeyVaultKms.cs` + 2 个包 + 2 个配置键 + 本 ADR 的例外登记 +
   脚本白名单两条 —— `IKeyManagementService` 抽象不变，**无调用方改动**。
3. **迁移到自建 Vault / HSM**：新增 `IKeyManagementService` 实现 + 改 DI 注册；AKV 实现可保留待用
   （此时需重新登记本例外）。

## 后果

**正面：**
- 约束有明确文本与唯一判据，消灭"约束只存在于口头前提"的状态
- 例外范围可审计（包/代码/配置/激活条件四项可核对）、可回退（换实现即可，调用方无感）
- 后续新增云依赖会被 CI 拦住，防"云绑定"悄悄蔓延（与收尾方案"防回潮"同源）

**负面：**
- 仍保留两条云 SDK 依赖（已接受，换取生产密钥托管的生产级方案）
- 对"外部服务商 SDK"不设硬闸，需依赖 review 纪律
- 脚本白名单与本 ADR 是两份副本，存在漂移可能

**风险缓解：**
- 脚本失败信息直接指向本 ADR，形成"改一处必须改另一处"的引导
- 前端与运行期调用列为已知盲区，避免给出"已全面覆盖"的假安全感