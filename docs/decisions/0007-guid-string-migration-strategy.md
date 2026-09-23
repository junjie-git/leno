# ADR-0007: Guid→string 迁移策略

## 状态
已接受（2026-07-19，工作流 D 决策；2026-07-22 P1-T27 补充 int64 字段映射策略）
**已完成（2026-09-24，C3 收口：int64 字段与其编号/名一并删除并 reserved）**

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
- **int64 字段映射策略（P1-T27，2026-07-22）**：迁移期间保留的 `int64` 字段不再使用 `(long)Guid.GetHashCode()` 映射（32 位 int 转 long，2^32 碰撞率），改用 `BitConverter.ToInt64(Guid.ToByteArray(), 0)` 取 Guid 前 8 字节作为 long。虽然 2^64 仍存在极小概率碰撞，但远低于 GetHashCode 的 2^32，且仅作为 string 字段迁移完成前的向后兼容兜底。新增客户端必须读 `string` 字段，不得依赖 int64 字段的唯一性

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

---

## 收口记录（2026-09-24，C3）

**触发条件已满足**：仓内所有 gRPC 客户端均只读 `string` 字段（`xxx_id_str`），服务随版本同发、
维护窗口可停机 —— 满足上文"所有客户端都读 string 后可删除 int64 字段"。

**执行结果**：

- 22 个 int64 ID 字段跨 7 个 proto 删除，并 `reserved` 编号与字段名（编号不得复用，
  见 ADR-0005 修订）：product 10、order 3、seller 3、review 3、promotion 1、cart 1、inventory 1。
- 删除的不只是"字段声明"，还有配套的**伪兼容代码**——这些 int64 通道本身不承载可逆信息
  （固定 0、`GetHashCode`/`BitConverter.ToInt64` 取 Guid 前 8 字节、`new Guid((int)shopId, 0, …)`），
  旧客户端拿到的是错误 Guid，兼容路径从未真正可用：
  - 服务端双写：Product / Order / SellerShop / Cart（4 个 gRPC 服务端）；
  - 客户端 int64 回退解码与"优先 string 失败则回退 int64"分支：Product / Cart / Order 防腐客户端；
  - Review 的"收到非 0 int64 即抛 InvalidArgument"分支、SellerShop 的 `new Guid((int)shopId, …)` 兜底。
- 编译期守护取代文档级提醒：删除字段后 CS0612（deprecated 字段被使用）从 67 条降为 **0 条**，
  仍有代码触碰废弃 ID 字段会直接编译失败。
- 字段名保留 `xxx_id_str` 形态（不再重命名为 `xxx_id`）：改名属于新的 breaking，且会造成 8 个
  服务与全部测试的属性名 churn；`_str` 即当前唯一权威名。

**结论**：标识契约从 `int64 + string` 双形态收敛为 **`string` 单形态**；本策略完成，不再有"回退
int64"的路径。
