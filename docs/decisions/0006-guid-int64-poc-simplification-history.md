# ADR-0006: Guid→int64 POC 简化历史（GetHashCode）

## 状态
已接受（2026-07-19，Task 27 POC 阶段决策，已由 ADR-0007 取代生产化路径）

## 上下文
M4 gRPC 双轨方案 POC 阶段（Task 27）需要在 `.proto` 文件中承载 `Guid` 类型 ID。
Protobuf 原生不支持 `Guid` 类型，需要选择替代方案：

- 方案 A：`string` 字段承载 `Guid.ToString()`（wire 友好但 POC 阶段需新增字段，违反 ADR-0005）
- 方案 B：`bytes` 字段承载 `Guid.ToByteArray()`（wire 紧凑但可读性差，调试困难）
- 方案 C：`int64` 字段承载 `(long)guid.GetHashCode()`（POC 阶段已有 int64 字段，零改动）

POC 阶段优先验证 gRPC 通信链路（双轨 + 熔断 + 适配器），不希望被 Guid 序列化问题阻塞进度。

## 决策
POC 阶段采用方案 C（`int64` + `GetHashCode`）：

- 复用 POC 阶段已有的 `int64 xxx_id` 字段，通过 `(long)guid.GetHashCode()` 写入
- 仅用于 POC 阶段验证 gRPC 通信链路，不进入生产环境
- 生产化阶段通过 ADR-0007（新增 `string` 字段 + 标记 int64 deprecated）迁移

## 后果

**正面：**
- 快速验证：POC 阶段不被 Guid 序列化问题阻塞，专注 gRPC 通信链路
- 零改动：复用现有 int64 字段，不修改 .proto（符合 ADR-0005）
- 端到端跑通：双轨 + 熔断 + 适配器链路完整验证

**负面：**
- `GetHashCode()` 可能碰撞（不同 Guid 可能产生相同 int64），数据完整性不保证
- `int64 → Guid` 不可逆，只能用于 ID 透传，不能用于业务逻辑
- 测试需特殊处理（mock 数据时需用 `(long)guid.GetHashCode()` 计算 int64 期望值）

**风险缓解：**
- 仅 POC 阶段使用，生产化阶段通过 Task D1-D3 迁移到 `string` 字段（详见 ADR-0007）
- POC 测试用例显式声明 int64 仅用于 ID 透传，不参与业务判定
- 迁移完成后该简化策略失效，但 POC 阶段的历史决策保留追溯
