# Consul KV 种子数据

本文档汇总 Leno 平台 Consul KV 配置中心的种子数据，便于环境初始化与密钥轮换。

## M5.2：11 个 BC 独立 InternalApiKey

每个 BC 使用独立的 32 字节 base64 编码 InternalApiKey，存放在 Consul KV 路径 `leno/security/internal-key/{bc}` 下。

生成 32 字节随机 key（base64 编码 = 44 字符）：

```bash
# 生成 11 个 BC 独立 InternalApiKey 并写入 Consul KV
for bc in userauth product cart order promotion reviewaftersales pointsmembership payment notification sellershop systemadmin; do
  key=$(openssl rand -base64 32)
  echo "leno/security/internal-key/$bc = $key"
  # 写入 Consul KV
  curl -X PUT "http://localhost:8500/v1/kv/leno/security/internal-key/$bc" -d "$key"
done
```

## 11 个 BC 清单与 Consul KV 路径

| BC 名 | Consul KV 路径 | 注入到 appsettings 的环境变量 |
|---|---|---|
| UserAuth | `leno/security/internal-key/userauth` | `LENO_INTERNAL_API_KEY_USERAUTH` |
| Product | `leno/security/internal-key/product` | `LENO_INTERNAL_API_KEY_PRODUCT` |
| Cart | `leno/security/internal-key/cart` | `LENO_INTERNAL_API_KEY_CART` |
| Order | `leno/security/internal-key/order` | `LENO_INTERNAL_API_KEY_ORDER` |
| Promotion | `leno/security/internal-key/promotion` | `LENO_INTERNAL_API_KEY_PROMOTION` |
| ReviewAfterSales | `leno/security/internal-key/reviewaftersales` | `LENO_INTERNAL_API_KEY_REVIEWAFTERSALES` |
| PointsMembership | `leno/security/internal-key/pointsmembership` | `LENO_INTERNAL_API_KEY_POINTSMEMBERSHIP` |
| Payment | `leno/security/internal-key/payment` | `LENO_INTERNAL_API_KEY_PAYMENT` |
| Notification | `leno/security/internal-key/notification` | `LENO_INTERNAL_API_KEY_NOTIFICATION` |
| SellerShop | `leno/security/internal-key/sellershop` | `LENO_INTERNAL_API_KEY_SELLERSHOP` |
| SystemAdmin | `leno/security/internal-key/systemadmin` | `LENO_INTERNAL_API_KEY_SYSTEMADMIN` |

另外保留一个 Shared 兼容期 Key（用于尚未迁移到独立 Key 的 BC）：

```bash
shared_key=$(openssl rand -base64 32)
echo "leno/security/internal-key/shared = $shared_key"
curl -X PUT "http://localhost:8500/v1/kv/leno/security/internal-key/shared" -d "$shared_key"
```

环境变量：`LENO_INTERNAL_API_KEY_SHARED`

## 调用方配置（防腐层 HttpClient）

防腐层调用方需在 `appsettings.json` 配置目标 BC 的 InternalApiKey（`AntiCorruption:TargetInternalApiKeys`）。
实际值由 Consul KV 注入对应环境变量。

### Order BC（调用 Product / Promotion / PointsMembership）

```json
// Leno.Order.Api/appsettings.json
{
  "AntiCorruption": {
    "UseGrpc": false,
    "TargetInternalApiKeys": {
      "Product": "${LENO_INTERNAL_API_KEY_PRODUCT}",
      "Promotion": "${LENO_INTERNAL_API_KEY_PROMOTION}",
      "PointsMembership": "${LENO_INTERNAL_API_KEY_POINTSMEMBERSHIP}"
    }
  }
}
```

### Notification BC（调用 UserAuth）

```json
// Leno.Notification.Api/appsettings.json
{
  "AntiCorruption": {
    "UseGrpc": false,
    "TargetInternalApiKeys": {
      "UserAuth": "${LENO_INTERNAL_API_KEY_USERAUTH}"
    }
  }
}
```

### Cart BC（调用 Product）

```json
// Leno.Cart.Api/appsettings.json
{
  "AntiCorruption": {
    "UseGrpc": false,
    "TargetInternalApiKeys": {
      "Product": "${LENO_INTERNAL_API_KEY_PRODUCT}"
    }
  }
}
```

### ReviewAfterSales BC（调用 Payment / Order）

```json
// Leno.ReviewAfterSales.Api/appsettings.json
{
  "AntiCorruption": {
    "UseGrpc": false,
    "TargetInternalApiKeys": {
      "Payment": "${LENO_INTERNAL_API_KEY_PAYMENT}",
      "Order": "${LENO_INTERNAL_API_KEY_ORDER}"
    }
  }
}
```

## 校验流程

### 启动期校验（ValidateSensitiveConfig）

各 BC 的 `Program.cs` 调用 `app.Configuration.ValidateSensitiveConfig()`，按以下顺序校验：

1. 读取 `Service:Name`（如 `Order`、`Product`）。
2. 优先校验 `Security:InternalApiKey:{BcName}` 是否存在且长度 ≥ 44 字符（32 字节 base64）。
3. 若 BC 独立 key 缺失，降级检查 `Security:InternalApiKey:Shared`，存在则打印 warning 继续启动（兼容期）。
4. 二者皆缺则返回 `false`，由 `Program.cs` 在生产环境抛异常阻止启动。
5. `Service:Name` 未配置时跳过 BC 独立校验（兼容期）。

### 调用方注入（防腐层 HttpClient）

- `InternalApiKeyMiddleware` 校验时，按 `Service:Name` 匹配 `Security:InternalApiKey:{BcName}`（本 BC 自身的 key）。
- 防腐层 HttpClient 调用时，按目标 BC 名匹配 `AntiCorruption:TargetInternalApiKeys:{TargetBc}`，注入 `X-Internal-Key` 请求头。
- 头名 `X-Internal-Key` 由 `InternalApiKeyMiddleware` 与所有防腐层服务共用常量。

## 灰度迁移步骤

1. **当前阶段（M5.2 兼容期）：** 各 BC appsettings.json 配置 `Security:InternalApiKey:Shared` 占位符；调用方配置 `AntiCorruption:TargetInternalApiKeys`。实际值由 Consul KV / 环境变量注入。`InternalApiKeyMiddleware` 仍使用 `InternalAuth:ApiKey`，与防腐层注入的 `X-Internal-Key` 比对。
2. **下一步：** 改造 `InternalApiKeyMiddleware` 读取 `Security:InternalApiKey:{BcName}` 替代 `InternalAuth:ApiKey`，实现真正按 BC 隔离。
3. **完成：** 移除 `Security:InternalApiKey:Shared` 与 `InternalAuth:ApiKey` 配置项，各 BC 仅使用独立 key。
