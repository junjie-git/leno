# ADR-0002: 熔断器三状态机（Closed/Open/HalfOpen）

## 状态
已接受（2026-07-19）

## 上下文
ADR-0001 决定保留 HttpClient 作为 fallback，但需要一个机制在 gRPC 故障时自动降级，
避免 gRPC 调用雪崩导致整个请求链路阻塞。需求：

- 跨请求累积失败计数（无状态 HTTP 请求需要共享熔断状态）
- 故障后快速切换至 HttpClient，恢复后自动探测 gRPC 是否恢复
- 不同 BC 的服务故障相互隔离，避免一个服务故障影响其他服务

## 决策
采用三状态熔断机：

- **Closed（正常）**：gRPC 调用正常，连续失败计数 < 3 时保持 Closed
- **Open（熔断）**：连续失败 3 次后切换至 Open，所有 gRPC 调用直接走 HttpClient
  - Open 状态持续 30 秒
- **HalfOpen（探测）**：30 秒后切换至 HalfOpen，仅允许 1 次 gRPC 探测
  - 探测成功 → 连续成功 2 次后回到 Closed
  - 探测失败 → 回到 Open，重新计时 30 秒

状态管理：

- 熔断器以 `Keyed Singleton per ServiceName` 注册到 DI 容器，跨请求共享状态
- `HalfOpen` 仅允许 1 次 gRPC 探测，避免恢复期并发请求冲击服务端
- 触发熔断的 gRPC 状态码：Unavailable / DeadlineExceeded / Internal / ResourceExhausted

## 后果

**正面：**
- 自动降级：gRPC 故障时无需人工介入，自动切换至 HttpClient
- 避免雪崩：Open 状态下不再发起 gRPC 调用，避免连锁故障
- 跨请求累积失败计数：避免无状态 HTTP 调用无法感知前序故障
- 服务隔离：每个 ServiceName 独立熔断状态，互不影响

**负面：**
- 30 秒 Open 窗口内强制走 HttpClient（即使 gRPC 已恢复）
- 状态机复杂度增加（Closed/Open/HalfOpen 三态转换需测试覆盖）
- HalfOpen 探测期间并发请求行为需明确（仅 1 次探测，其余走 HttpClient）

**风险缓解：**
- 熔断参数可通过 `AntiCorruptionOptions` 配置（失败阈值、Open 时长、探测次数等）
- Prometheus 暴露熔断器状态指标（`anticorruption_circuit_breaker_state{service=...}`）
- 状态转换日志记录，便于事后分析
- 单元测试覆盖所有状态转换路径
