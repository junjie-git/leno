# ADR-0007: Guid→string 迁移策略

## 状态
已接受（2026-07-19，工作流 D 决策）

## 上下文
ADR-0006 记录的 POC 阶段 `Guid → int64` 简化（`GetHashCode`）存在数据完整性问题，
生产化阶段需修复。但 ADR-0005 约束 `.proto` 只能新增字段，不能修改/删除字段，
无法直接将 `int64 xxx_id` 改为 `string xxx_id`。

约束：

- 不能违反 ADR-0005（wire 兼容性硬约束）
- 必须支持渐进迁移（旧客户端仍可读 int64）
- 必须修复 `GetHashCode` 碰撞风险

## 决策
采用**新增 `string` 字段 + 标记 `int64` 字段 `[deprecated = true]`** 策略：

- 对每个 `int64 xxx_id` 字段，新增 `string xxx_id_str = N;`（N 为新字段号）
- 在原 `int64 xxx_id` 字段添加 `[deprecated = true]` 选项，表达迁移意图
- 保留 `int64` 字段（永久向后兼容，旧客户端仍可读取）
- `buf breaking` 校验通过（仅新增字段 + 添加 deprecated 选项，不触发 breaking）

实现细节：

- **GrpcService（服务端）**：双写 `int64` + `string` 字段（保证旧客户端兼容）
- **GrpcClient（客户端）**：优先读 `string` 字段，回退到 `int64`（兼容旧服务端）
- **迁移路径**：客户端逐步升级到读 `string`，最终所有客户端都读 `string` 后，下版 .proto 可删除 `int64` 字段

## 后果

**正面：**
- wire 兼容：保留 int64 字段，旧客户端可持续运行
- 渐进迁移：客户端按自身节奏升级，无强制同步要求
- 数据完整性：string 字段承载 `Guid.ToString()`，无碰撞风险
- 可观测性：deprecated 标记使废弃字段在文档/工具中显式标识

**负面：**
- 字段冗余：迁移期间 `.proto` 同时保留 int64 + string，文件膨胀
- 代码复杂度增加：GrpcService 需双写，GrpcClient 需优先 string + 回退 int64 逻辑
- 迁移周期长：需所有客户端升级后才可删除 int64 字段

**风险缓解：**
- 迁移完成后下一版 `.proto` 可删除 deprecated int64 字段（符合 ADR-0005 major version 例外）
- CI 校验 deprecated 字段使用情况，监控迁移进度
- GrpcClient 回退逻辑文档化，避免新人误用 int64 字段
- 待迁移 .proto 清单（6 个文件）记录在 plan §11.2，按文件逐步推进
