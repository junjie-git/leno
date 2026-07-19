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
