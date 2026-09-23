# JWT RS256 密钥轮换 Runbook（四步法）

> 适用：Identity BC 的访问令牌签名密钥（RS256）。
> 目标：轮换全程**零 401**——重叠期内新旧公钥同时在 JWKS 发布，消费方按令牌头 `kid` 匹配。
> 前提阅读：`docs/配置管理与JWKS集成分析报告.md`（缓存与刷新机制）。

## 背景机制

| 组件 | 行为 |
|---|---|
| Identity `JwksController` | 发布 `CurrentKeyId` + `PreviousKeyIds`（逗号分隔的重叠钥）对应的全部公钥 |
| 消费方 JwtBearer | 公钥缓存：自动刷新 1h、拉取失败 30s 重试、`kid` 未命中即时 `RequestRefresh()` |
| 令牌 | `kid` 头 = 签发时的 `CurrentKeyId`；TTL 30 分钟 |

## 四步轮换

### 步骤 1：新私钥入 KMS（key-v2）

- **生产（AKV）**：`JwtSigning:UseAzureKeyVault=true` 下为 `jwt-signing` 密钥创建新版本，
  确认 `AzureKeyVaultKms.GetPrivateKeyAsync("key-v2")` 可路由（keyId 与其版本路由规则一致）。
- **本地/CI（EnvironmentKms）**：生成新 PEM 并按 keyId 后缀区分注入
  （当前实现按 `CurrentKeyId` 读取单一 PEM——轮换时先替换私钥再切 KeyId，见步骤 2 的原子变更说明）。

```bash
openssl genpkey -algorithm RSA -out private-key-v2.pem -pkeyopt rsa_keygen_bits:2048
openssl rsa -in private-key-v2.pem -pubout -out public-key-v2.pem
```

### 步骤 2：原子切换"当前钥 + 重叠钥"（JWKS 同时发布两把）

在 Consul KV（或部署配置）**一次变更**以下两项：

```text
JwtSigning__CurrentKeyId   = key-v2
JwtSigning__PreviousKeyIds = key-v1
```

- JWKS 立即发布 v1 + v2 两把公钥；
- 新令牌 `kid=key-v2`；存量令牌（`kid=key-v1`）仍可被 JWKS 中的 v1 验签；
- 消费方 `kid` 未命中时自动 `RequestRefresh()`，最坏情况一次 401 后自愈。

### 步骤 3：观察

- 窗口 = 令牌 TTL（30min）+ 消费方最大刷新周期（1h）；
- 观察 Identity 日志 / 网关 401 率：确认验签流量全部走 `kid=key-v2`；
- 如出现持续 401：回滚步骤 2（`CurrentKeyId=key-v1`，`PreviousKeyIds` 保留），JWKS 仍含两钥，无损。

### 步骤 4：移除旧钥

```text
JwtSigning__PreviousKeyIds = （清空）
```

- JWKS 回到单钥（key-v2）；KMS 中的 v1 私钥归档/销毁（按安全策略保留一个回滚周期）。

## 常见问题

| 现象 | 原因 | 处置 |
|---|---|---|
| 轮换后偶发 401，一次重试恢复 | 消费方缓存未刷新 | 预期行为（`kid` 未命中 → RequestRefresh）；持续出现则检查步骤 2 的 JWKS 是否真的含两把钥（`curl /.well-known/jwks.json` 看 `kid` 列表） |
| JWKS 只有一把钥 | `PreviousKeyIds` 未配置或旧钥在 KMS 不可用（看 Identity 告警日志） | 补配置；旧钥不可用且已无 v1 流量时跳过属预期 |
| 全量 401 不恢复 | JWKS 移除 v1 过早（<TTL+刷新周期） | 回滚步骤 2 重叠配置 |
| AKV 模式 keyId 路由失败 | `AzureKeyVaultKms` 的版本路由规则与 keyId 不符 | 核对 `AzureKeyVaultKms.GetPrivateKeyAsync` 的 keyId 解析；必要时补充实现 |

## 与本仓库实现的对应关系

| Runbook 步骤 | 代码支撑 |
|---|---|
| 步骤 2 的多钥发布 | `JwksController.BuildPublishedKeyIds()`（CurrentKeyId + PreviousKeyIds 去重，旧钥不可用跳过） |
| 消费方即时刷新 | `WebApplicationExtensions` / 网关 `Program.cs` 的 `OnAuthenticationFailed` → `RequestRefresh()` |
| 刷新窗口 | `AutomaticRefreshInterval=1h`、`RefreshInterval=30s`（两端一致） |
| KV 热更新 | `AddLenoConsulConfig`（ReloadOnChange）+ `JwtSigning__*` 键 |
