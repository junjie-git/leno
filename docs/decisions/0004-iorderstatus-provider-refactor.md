# ADR-0004: IOrderStatusProvider 重构（分离远程查询与业务规则）

## 状态
已接受（2026-07-19，Task 23 实施时发现）

## 上下文
Task 23 实施过程中发现 `EligibilityChecker` 承担了过多职责：

- 远程调用（查询订单状态、库存、促销等 BC 服务）
- 业务规则评估（ eligibility 判定逻辑）
- 仓储查询（读取本地聚合数据）

这导致：

- 难以单独测试业务规则（必须 mock 远程调用 + 仓储）
- 无法对订单状态查询应用 gRPC 双轨方案（查询逻辑被业务规则污染）
- 职责混合使代码可读性下降，新人理解成本高

## 决策
提取 `IOrderStatusProvider` 接口分离远程查询职责：

- `IOrderStatusProvider` 仅负责远程查询订单状态（暴露只读查询方法子集）
- `EligibilityChecker` 保留业务规则评估 + 仓储查询，依赖 `IOrderStatusProvider` 获取订单状态
- `IOrderStatusProvider` 的实现走 `AntiCorruptionDispatcher` 双轨路径（gRPC / HttpClient）

## 后果

**正面：**
- 职责分离：业务规则、远程查询、仓储查询各自单一职责
- 双轨化：`IOrderStatusProvider` 可独立走 gRPC 双轨方案
- 可测试性提升：业务规则可独立测试，无需 mock 远程调用
- 可替换性：未来可替换 `IOrderStatusProvider` 实现（如缓存层）而不影响业务规则

**负面：**
- 多一层抽象（接口 + 实现类）
- 调用链增加：业务层 → EligibilityChecker → IOrderStatusProvider → Dispatcher → gRPC/HttpClient

**风险缓解：**
- 接口仅暴露只读查询方法子集（不暴露写操作，避免业务规则旁路）
- 实现类委托 Dispatcher，复用 ADR-0001 双轨能力，无额外维护成本
- 单元测试通过 mock `IOrderStatusProvider` 隔离业务规则测试
