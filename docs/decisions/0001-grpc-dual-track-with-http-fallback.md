# ADR-0001: gRPC 双轨方案（保留 HttpClient fallback）

## 状态
已接受（2026-07-19）

## 上下文
M4.3 通信升级需要降低跨 BC 同步调用的延迟。gRPC 相比 HTTP/1.1 + JSON 有显著性能优势
（Protobuf 二进制 + HTTP/2 多路复用）。但直接迁移到 gRPC 存在风险：

- 服务端故障时无降级路径
- 灰度切换困难
- 运维复杂度高

已有 HttpClient 调用路径在生产环境稳定运行，无法一次性切换至 gRPC。
需要一种方案在不放弃 HttpClient 的前提下渐进引入 gRPC，并具备故障自动降级能力。

## 决策
采用 gRPC + HttpClient 双轨方案：

1. `AntiCorruptionDispatcher<TService>` 在运行时选择传输方式
2. `UseGrpc` 配置开关通过 Consul KV 热更新（1-2 秒生效），可按 BC 独立切换
3. 熔断器三状态机自动降级（3 次连续失败 Open，30 秒后 HalfOpen 探测）
   - gRPC 故障状态码（Unavailable / DeadlineExceeded / Internal / ResourceExhausted）触发降级
4. HttpClient 代码永久保留作为 fallback（不实施 Task 11 下线）

## 后果

**正面：**
- 风险可控：gRPC 故障时自动降级到 HttpClient
- 灵活灰度：按 BC 独立切换
- 性能提升：gRPC P99 延迟显著低于 HttpClient
- 可观测性统一：Dispatcher 入口统一埋点，metrics 标签区分 grpc/http

**负面：**
- 代码复杂度增加：需维护两套实现 + 适配器
- 测试覆盖成本：需覆盖双轨 + 降级场景
- 运维成本：需监控 gRPC 调用指标 + 熔断器状态

**风险缓解：**
- 适配器模式隔离复杂度（DispatcherAdapter 实现 TService 接口，详见 ADR-0003）
- 单元测试覆盖核心场景（双轨切换、降级、恢复）
- Prometheus 指标 + Grafana 仪表盘监控
- Runbook（`docs/runbooks/m4-grpc-poc-verification.md`）提供应急回滚操作
- 4 周稳定期观察窗口，期间 UseGrpc 默认 false，按 BC 灰度开启
