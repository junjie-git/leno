# M4 gRPC 双轨 POC 验证 Runbook

> 适用范围：阶段 1 Product POC（Task 13-17）→ 阶段 2 全量推广前的灰度验证

## 1. 启用 gRPC（Order BC 调用 Product BC）

```bash
# 1. 写入 Consul KV 启用 Order BC 的 gRPC
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/order" -d 'true'

# 2. 写入 gRPC 端点（Product BC 的 gRPC 端口）
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/grpc/endpoints/product" -d 'https://leno-product-api:5152'

# 3. 观察日志（ConsulConfigWatcher 5 秒内拉取新配置）
kubectl logs deployment/leno-order-api -f | grep "UseGrpc"
```

## 2. 验证指标（1 周观察期）

| 指标 | 目标 | 数据源 |
|---|---|---|
| gRPC 调用成功率 | ≥ 99.9% | `anticorruption_grpc_request_total{service="product"}` |
| 熔断降级触发次数 | < 10 次/天 | `anticorruption_fallback_total{service="product"}` |
| gRPC P99 延迟 | < 10ms | `anticorruption_grpc_duration_seconds` |
| HttpClient P99 延迟 | < 50ms（降级时） | `anticorruption_failure_total{path="http"}` |
| 业务错误率 | 0 | Application Insights |

## 3. 紧急回滚

```bash
# 1-2 秒内生效，无需重启
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/order" -d 'false'
```

回滚后所有 Order → Product 调用走 HttpClient，CircuitBreakerState 保持在最后一次状态（Singleton 实例不重置，但不影响 HTTP 调用）。

## 4. 验收清单（1 周观察期结束后填写）

### 4.1 功能验证

- [ ] Product.Api 启动后 gRPC 端点可调（`kubectl port-forward` 验证）
- [ ] Order BC `UseGrpc=true` 后通过 gRPC 调用 Product 成功
- [ ] 熔断降级机制验证：手动停 Product gRPC 后 Order 自动降级到 HttpClient
- [ ] gRPC 鉴权验证：无 `x-internal-key` 的调用被拒绝（Unauthenticated）

### 4.2 测试验证

- [ ] GrpcProductAntiCorruptionClient 单元测试 3 个全部 PASS
- [ ] AntiCorruptionDispatcher 单元测试 9 个全部 PASS（Task 9 已实现，Task 16 已跳过）
- [ ] AntiCorruptionDispatcher 集成测试已跳过（与 Task 9 场景重叠）

### 4.3 指标验证（连续 7 天）

- [ ] gRPC 调用成功率 ≥ 99.9%
- [ ] 熔断降级触发 < 10 次/天
- [ ] gRPC P99 < 10ms
- [ ] HttpClient P99 < 50ms（降级时）
- [ ] 业务错误率 = 0

### 4.4 运维验证

- [ ] ConsulConfigWatcher 热更新生效（5 秒内）
- [ ] 紧急回滚演练通过（关闭 UseGrpc 后 1-2 秒切回 HTTP）
- [ ] Prometheus 指标面板显示 gRPC vs HTTP 双轨数据
- [ ] HalfOpen 探测 2 次成功后切 Closed 验证通过

## 5. 已知限制（POC 阶段）

1. **Guid → int64 映射简化**：proto 中 `sku_id` 为 `int64`，当前使用 `GetHashCode()` 映射。生产化阶段（Task 27）需改为 `string` 类型承载 Guid（spec §4.1 决策）。
2. **仅 Product 防腐层双轨**：Promotion 与 Points 防腐层仍使用 HttpClient，将在阶段 2（Task 18-20）实施。
3. **MapToDto 中 ID 字段为 Guid.Empty**：POC 阶段 SkuId/SpuId/SellerId 映射为 Guid.Empty，因 int64 无法承载 Guid。生产化前需评估业务影响。

## 6. 相关文档

- Spec：`docs/superpowers/specs/2026-07-19-m4-grpc-dual-track-design.md`
- Plan：`docs/superpowers/plans/2026-07-19-m4-grpc-dual-track-implementation.md`
- 关联 Plan：`docs/superpowers/plans/2026-07-17-slow-track-m4-communication-upgrade.md`（Plan 8）

---

## 7. 阶段 5：全量 gRPC 开启 + 4 周稳定运行验收

> 适用范围：阶段 1-4 完成后，将所有 BC 的 `AntiCorruption:UseGrpc` 切为 `true`，进入 4 周稳定运行期。
> 本章节基于实际代码实现（非 Plan 假设），供运维团队执行 Task 25 验收。

### 7.1 Consul KV 配置切换指南

#### 7.1.1 需要切换的 BC 清单

**重要：调用方与被调用方 BC 均需设置 `UseGrpc=true`。**

实际代码中，gRPC 服务端端点映射（`MapGrpcService`）由 `builder.Configuration.GetValue<bool>("AntiCorruption:UseGrpc")` 在启动时决定（见各 BC `Program.cs`）。因此被调用方 BC 也必须设置 `UseGrpc=true`，否则 gRPC 端点不会映射，调用方将全部降级到 HttpClient。

| BC 名称 | Service:Name | 角色 | 需设置 UseGrpc | 原因 |
|---|---|---|---|---|
| Order | `Order` | 调用方 + 被调用方 | ✅ | 调用 Product/Promotion/Points + 暴露 OrderGrpcService |
| Notification | `Notification` | 调用方 | ✅ | 调用 UserAuth |
| Cart | `Cart` | 调用方 | ✅ | 调用 Product（CartPrice） |
| ReviewAfterSales | `ReviewAfterSales` | 调用方 | ✅ | 调用 Order/Payment |
| Product | `Product` | 被调用方 | ✅ | 暴露 ProductGrpcService（供 Order/Cart 调用） |
| Promotion | `Promotion` | 被调用方 | ✅ | 暴露 PromotionGrpcService（供 Order 调用） |
| PointsMembership | `PointsMembership` | 被调用方 | ✅ | 暴露 PointsGrpcService（供 Order 调用） |
| UserAuth | `UserAuth` | 被调用方 | ✅ | 暴露 UserAuthGrpcService（供 Notification 调用） |
| Payment | `Payment` | 被调用方 | ✅ | 暴露 PaymentGrpcService（供 ReviewAfterSales 调用） |

> ⚠️ 与 Plan 原假设的差异：Plan Task 25 Step 1 注释"被调用方 BC 无需设置 UseGrpc"，但实际代码中 `MapGrpcService` 受 `UseGrpc` 开关控制，被调用方也必须设置。运维执行时需切换全部 9 个 BC。

#### 7.1.2 Consul KV 写入命令

ConsulConfigWatcher 监听路径前缀：`leno/anticorruption/use-grpc/`（见 `ConsulConfigWatcher.cs:17`）。

```bash
# 调用方 BC（4 个）
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/Order" -d 'true'
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/Notification" -d 'true'
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/Cart" -d 'true'
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/ReviewAfterSales" -d 'true'

# 被调用方 BC（5 个）—— 需重启进程使 MapGrpcService 生效
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/Product" -d 'true'
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/Promotion" -d 'true'
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/PointsMembership" -d 'true'
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/UserAuth" -d 'true'
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/Payment" -d 'true'
```

#### 7.1.3 热更新机制与生效时间

**调用方 BC（客户端热更新）：**

`ConsulConfigWatcher`（`src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs`）长轮询 `leno/anticorruption/use-grpc/{Service:Name}` KV：

- **WaitTime**：5 分钟（Consul 长轮询，KV 变更时 Consul 立即返回，实际感知延迟 1-2 秒）
- **RetryDelay**：10 秒（异常重试间隔）
- **写入目标**：`IConfiguration["AntiCorruption:UseGrpc"]`，由 `IOptionsMonitor<AntiCorruptionOptions>` 实时反映到 `AntiCorruptionDispatcher`

验证日志（Serilog Information 级别，`ConsulConfigWatcher.cs:69`）：

```
[INFO] UseGrpc 配置热更新为 true（BC=Order）
```

启动日志（`ConsulConfigWatcher.cs:51`）：

```
[INFO] ConsulConfigWatcher 启动，监听 KV: leno/anticorruption/use-grpc/Order
```

> ⚠️ **已知代码缺口**：`ConsulConfigWatcher` 类已实现但当前未在任何 BC 的 DI 容器中通过 `AddHostedService<ConsulConfigWatcher>()` 注册。运维若发现日志未出现，需先由开发团队在各调用方 BC `Program.cs` 或 `AddLenoInfrastructure` 中补注册。临时替代方案：通过 `AddLenoConsulConfig`（前缀 `leno/config`，30 秒轮询热重载）写入 `leno/config/AntiCorruption:UseGrpc=true`，该机制已由 `Winton.Extensions.Configuration.Consul` 接入。

**被调用方 BC（服务端需重启）：**

`MapGrpcService` 在 `builder.Build()` 后由 `if (builder.Configuration.GetValue<bool>("AntiCorruption:UseGrpc"))` 一次性决定，**不支持热更新**。被调用方 BC 设置 KV 后需滚动重启进程才能暴露 gRPC 端点。

### 7.2 4 周稳定运行监控指标

指标由 `AntiCorruptionMetrics`（`src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs`）统一发布，Meter 名 `Leno.AntiCorruption`，各 BC 通过 `AddLenoOpenTelemetry` 订阅。

#### 7.2.1 核心指标与目标

| 指标名 | 标签 | 目标 | 说明 |
|---|---|---|---|
| `anticorruption_grpc_request_total` | `service`, `status_code` | 成功率 > 99.9% | gRPC 调用计数，`status_code` 取值：`OK`/`Unavailable`/`DeadlineExceeded`/`Internal`/`ResourceExhausted`/`Unknown` 等 |
| `anticorruption_grpc_duration_seconds` | `service`, `status_code` | P99 < 100ms | gRPC 调用延迟分布（秒） |
| `anticorruption_circuit_open` | `service` | Open 次数 = 0 | 熔断器 Open 状态 Gauge（1=Open，0=Closed/HalfOpen） |
| `anticorruption_fallback_total` | `service`, `reason` | 降级次数 < 0.1% | 降级原因：`circuit_open`/`grpc_Unavailable`/`grpc_DeadlineExceeded`/`grpc_Internal`/`grpc_ResourceExhausted`/`grpc_unknown` |
| `anticorruption_failure_total` | `service`, `operation`, `path` | 业务错误率 = 0 | `path` 取值 `http`/`grpc`，无新增 503/500 |

#### 7.2.2 PromQL 查询示例

```promql
# 1. gRPC 调用成功率（status_code=OK 占比）
sum(rate(anticorruption_grpc_request_total{status_code="OK"}[5m]))
  / sum(rate(anticorruption_grpc_request_total[5m]))

# 2. gRPC P99 延迟（按 service 维度）
histogram_quantile(0.99,
  sum(rate(anticorruption_grpc_duration_seconds_bucket[5m])) by (le, service))

# 3. 熔断器 Open 状态（任意 service=1 即告警）
anticorruption_circuit_open == 1

# 4. 降级次数速率（按 service/reason 维度）
sum(rate(anticorruption_fallback_total[5m])) by (service, reason)

# 5. 业务错误数（path=grpc 的 failure_total）
sum(rate(anticorruption_failure_total{path="grpc"}[5m])) by (service, operation)

# 6. 按 service 维度的 gRPC 调用量
sum(rate(anticorruption_grpc_request_total[5m])) by (service)
```

> 实际 `service` 标签取值（来自 `CircuitBreakerState` 与 `GrpcAntiCorruptionClientBase.ServiceName`）：
> `product` / `promotion` / `points` / `user_contact` / `payment` / `order`

### 7.3 4 周后验收 checklist

#### 7.3.1 功能与稳定性

- [ ] 所有 7 个防腐层 gRPC 调用稳定，连续 4 周无降级（`anticorruption_fallback_total` 增量为 0）
- [ ] Grafana 仪表盘显示 gRPC 调用量占比 100%（`anticorruption_grpc_request_total` 速率 > 0 且无 HttpClient 旁路流量）
- [ ] gRPC 调用成功率 ≥ 99.9%（连续 4 周）
- [ ] gRPC P99 延迟 < 100ms（连续 4 周）
- [ ] 熔断器 Open 次数 = 0（或仅因计划内维护触发，需在变更记录中说明）
- [ ] 降级到 HttpClient 的次数占比 < 0.1%
- [ ] 业务错误率 = 0（无新增 503/500，`anticorruption_failure_total{path="grpc"}` 无增长）

#### 7.3.2 业务回归验证

- [ ] 订单流程正常：下单 → 支付 → 发货 → 完成（Order → Product/Promotion/Points gRPC 链路）
- [ ] 购物车流程正常：加购 → 改价 → 结算（Cart → Product gRPC 链路）
- [ ] 通知流程正常：下单/支付/发货通知触发（Notification → UserAuth gRPC 链路）
- [ ] 评价流程正常：订单完成后提交评价（ReviewAfterSales → Order gRPC 链路）
- [ ] 售后流程正常：退款/退货申请与资格校验（ReviewAfterSales → Order/Payment gRPC 链路）

#### 7.3.3 运维验证

- [ ] **ConsulConfigWatcher 热更新验证**：手动将某调用方 BC 的 `leno/anticorruption/use-grpc/{BC}` 切回 `false`，观察日志 `UseGrpc 配置热更新为 false（BC={BC}）` 在 1-2 秒内出现，且后续调用走 HttpClient；再切回 `true` 验证恢复
- [ ] **熔断器恢复验证**：手动停某被调用方 BC 的 gRPC 端点，观察 `anticorruption_circuit_open{service=...}` 在连续 3 次失败后变为 1（Open）；恢复 gRPC 后 30 秒（`OpenDurationSeconds` 默认值）内进入 HalfOpen，2 次成功探测后切回 Closed（`anticorruption_circuit_open` 归 0）
- [ ] **紧急回滚演练**：将 Consul KV `leno/anticorruption/use-grpc/{BC}` 切回 `false`，验证调用方 1-2 秒内切回 HttpClient（被调用方需重启才能关闭 gRPC 端点，但不影响回滚——调用方不再发起 gRPC 调用）
- [ ] Prometheus 指标面板显示 gRPC vs HTTP 双轨数据可观测
- [ ] Grafana 仪表盘按 `service` 标签可区分 6 个熔断器实例状态

### 7.4 防腐层与服务端清单

#### 7.4.1 7 个 gRPC 双轨防腐层（实际实施）

> Plan 原列 9 个防腐层，实际实施 7 个：Cart 的 `ProductSnapshotAntiCorruptionService` 保留 HttpClient 未双轨；ReviewAfterSales 的 `AfterSalesEligibilityChecker`/`ReviewEligibilityChecker` 合并重构为 `IOrderStatusProvider` 双轨。

| # | 调用方 BC | 下游 BC | 防腐层接口 | gRPC 客户端实现 | serviceName | 熔断器 Keyed Singleton Key |
|---|---|---|---|---|---|---|
| 1 | Order | Product | `IProductAntiCorruptionService` | `GrpcProductAntiCorruptionClient` | `product` | `product` |
| 2 | Order | Promotion | `IPromotionAntiCorruptionService` | `GrpcPromotionAntiCorruptionClient` | `promotion` | `promotion` |
| 3 | Order | PointsMembership | `IPointsAntiCorruptionService` | `GrpcPointsAntiCorruptionClient` | `points` | `points` |
| 4 | Notification | UserAuth | `IUserContactService` | `GrpcUserContactAntiCorruptionClient` | `user_contact` | `user_contact` |
| 5 | Cart | Product | `ICartPriceService` | `GrpcCartPriceService` | `product` | `product` |
| 6 | ReviewAfterSales | Payment | `IPaymentInfoQueryService` | `GrpcPaymentInfoQueryService` | `payment` | `payment` |
| 7 | ReviewAfterSales | Order | `IOrderStatusProvider`（重构） | `GrpcOrderStatusProvider` | `order` | `order` |

> 注：Order 与 Cart 的 `product` 熔断器为各自 DI 容器内的独立 Keyed Singleton 实例，但 Prometheus 指标 `service` 标签均为 `product`，仪表盘需结合 `instance`/`job` 标签区分来源 BC。

#### 7.4.2 6 个 gRPC 服务端清单

| # | BC | GrpcService 类 | 位置 | 复用业务逻辑 |
|---|---|---|---|---|
| 1 | Product | `ProductGrpcService` | `src/Services/Product/Leno.Product.Api/GrpcServices/` | `IProductInternalQueryService` |
| 2 | Promotion | `PromotionGrpcService` | `src/Services/Promotion/Leno.Promotion.Api/GrpcServices/` | `IPromotionInternalQueryService` |
| 3 | PointsMembership | `PointsGrpcService` | `src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/` | `IPointsInternalQueryService` |
| 4 | UserAuth | `UserAuthGrpcService` | `src/Services/UserAuth/Leno.UserAuth.Api/GrpcServices/` | `IUserInternalQueryService` |
| 5 | Order | `OrderGrpcService` | `src/Services/Order/Leno.Order.Api/GrpcServices/` | `IOrderInternalQueryService` |
| 6 | Payment | `PaymentGrpcService` | `src/Services/Payment/Leno.Payment.Api/GrpcServices/` | `IPaymentInternalQueryService` |

所有 GrpcService 均通过 `GrpcInternalKeyInterceptor` 校验 `X-Internal-Key` 请求头（在 `AddLenoApi` 中当 `UseGrpc=true` 时注册）。

### 7.5 回滚预案

#### 7.5.1 紧急回滚（调用方侧，秒级生效）

将调用方 BC 的 Consul KV 切回 `false`，ConsulConfigWatcher 1-2 秒内热更新，后续调用走 HttpClient：

```bash
# 单 BC 回滚（示例：Order）
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/Order" -d 'false'

# 全量回滚（4 个调用方 BC）
for BC in Order Notification Cart ReviewAfterSales; do
  curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/${BC}" -d 'false'
done
```

回滚后所有跨 BC 调用走 HttpClient，`CircuitBreakerState` 保持当前状态（Keyed Singleton 实例不重置，但不影响 HTTP 调用路径）。

#### 7.5.2 熔断器自动降级（无需人工干预）

`AntiCorruptionDispatcher` 在以下场景自动降级到 HttpClient，业务不中断：

- **熔断 Open 期间**：`CircuitBreakerState.GetState() == Open` 时直接走 HttpClient，记录 `anticorruption_fallback_total{reason="circuit_open"}`
- **gRPC 不可用异常**：`StatusCode` 为 `Unavailable`/`DeadlineExceeded`/`Internal`/`ResourceExhausted` 时降级，记录 `anticorruption_fallback_total{reason="grpc_{StatusCode}"}`

熔断器参数（`AntiCorruptionOptions.CircuitBreaker`，默认值）：

| 参数 | 默认值 | 说明 |
|---|---|---|
| `FailureThreshold` | 3 | 连续失败次数阈值，达到后熔断 Open |
| `SuccessThreshold` | 2 | HalfOpen 状态下连续成功次数，达到后切 Closed |
| `OpenDurationSeconds` | 30 | Open 持续时间（秒），过期后转 HalfOpen |

#### 7.5.3 被调用方 gRPC 端点关闭（需重启）

如需关闭被调用方的 gRPC 端点（例如 gRPC 服务端有内存泄漏），将对应 BC 的 `UseGrpc` 切回 `false` 后**重启进程**：

```bash
curl -X PUT "${CONSUL_ADDR}/v1/kv/leno/anticorruption/use-grpc/Product" -d 'false'
# 然后滚动重启 Product.Api Pod
kubectl rollout restart deployment/leno-product-api
```

> 调用方侧无需等待被调用方重启——调用方的 `AntiCorruptionDispatcher` 会在 gRPC 调用失败后自动降级到 HttpClient。

### 7.6 已知限制与注意事项

1. **ConsulConfigWatcher 未注册**：类已实现但未通过 `AddHostedService` 注册到任何 BC 的 DI 容器。运维执行前需确认开发团队已补注册，否则调用方热更新不生效，只能通过重启进程或 `leno/config` 前缀的 30 秒轮询热重载切换。
2. **被调用方需重启**：`MapGrpcService` 在启动时一次性决定，不支持热更新。被调用方 BC 设置 `UseGrpc=true` 后需滚动重启。
3. **`product` serviceName 重复**：Order 与 Cart 均用 `service="product"` 标签，Prometheus 指标需结合 `instance`/`job` 区分来源 BC。
4. **Guid → int64 简化（POC 遗留）**：部分 .proto 中 ID 字段为 `int64`，当前使用 `GetHashCode()` 映射。生产化（Task 27）需改为 `string` 承载 Guid。验收期间若出现 ID 冲突需立即回滚。
5. **防腐层数量与 Plan 差异**：Plan 列 9 个防腐层，实际实施 7 个（见 7.4.1）。验收 checklist 以实际 7 个为准。
