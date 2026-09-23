# ADR-0005: .proto 向后兼容约束

## 状态
已接受（2026-07-19，项目硬约束）；**2026-09-24 修订 §2 第 2/4 条**（见文末「修订」）

## 上下文
Leno 项目采用 gRPC + Protobuf 作为 BC 间同步通信协议。`.proto` 文件是跨 BC 的契约，
一旦发布并被多个客户端消费，修改会破坏 wire 兼容性：

- 已部署的客户端无法识别新增/删除的字段
- 修改字段编号会导致 wire 格式错乱
- 修改字段类型可能导致反序列化失败
- 删除字段后旧客户端仍发送该字段，服务端行为不可预测

需要约束 `.proto` 修改方式，保证已发布字段的 wire 兼容性。

## 决策
`.proto` 文件修改约束：

1. **只能新增字段**，不能修改/删除字段名、类型、编号
2. 废弃字段标记 `[deprecated = true]`，不直接删除
3. `buf breaking` 校验集成 CI，违反约束的 PR 无法合并
4. 新增字段使用新的字段编号（不占用已使用编号，不跳过中间编号复用）
5. 字段命名遵循 `snake_case`，新增字段保持命名一致性

## 后果

**正面：**
- wire 兼容：已发布字段保证向后兼容，旧客户端可持续运行
- 渐进升级：客户端可按自身节奏升级 .proto 版本
- CI 强制校验：人为失误被 buf breaking 拦截
- 团队协作：跨 BC 协作时契约清晰可追溯

**负面：**
- 字段累积：废弃字段长期保留在 .proto 中，文件膨胀
- 字段编号空间有限（1-536,870,911），需规划编号分配
- 需定期标记 deprecated，否则废弃字段无人维护

**风险缓解：**
- CI 强制校验 `buf breaking`，PR 描述需说明字段变更
- Code review 检查 deprecated 标记使用情况
- 字段编号规划文档化，避免编号冲突
- 长期废弃字段在下个大版本（major version）允许删除（如 v2.0）

---

## 修订（2026-09-24，随 C3 契约收口执行）

**背景**：ADR-0007 的 Guid→string 迁移已到终点（所有客户端只读 `string`，且本仓服务同版本发布、
维护窗口可停机），需按 ADR-0007 预告的"下版可删除 int64 字段"执行删除。原 §2 第 2 条
"废弃字段标记 `[deprecated = true]`，不直接删除"会永久禁止这一步，与 ADR-0007 的终点冲突。

**修订内容**：

1. §2 第 2 条调整为：**废弃字段先标记 `[deprecated = true]`；迁移确认完成后允许删除，但必须
   同时 `reserved <编号>;` 与 `reserved "<字段名>";`**——删除不等于编号可复用，§2 第 4 条
   "新增字段使用新编号、不复用"仍是硬约束。
2. `buf breaking` 配置随之精确化：不再启用"一律禁止删除"的 `FIELD_NO_DELETE`，改为启用
   `FIELD_NO_DELETE_UNLESS_NUMBER_RESERVED` + `FIELD_NO_DELETE_UNLESS_NAME_RESERVED`
   （见 `src/BuildingBlocks/Leno.SharedContracts/buf.yaml`）。实测行为：
   - 删除**已** reserved 编号与名的字段 → 放行；
   - 删除**未** reserved 的字段 → 拦截并指出缺哪个 reserved。
3. CI 的 breaking 基线改为 PR 目标分支（`github.base_ref`）；当基线分支尚无 `Protos/`
   契约（如契约尚未合并入 main）时显式 skip 并输出 notice，避免"无基线"被当成恒红的假失败。

**适用范围**：本修订适用于"契约客户端全部在本仓、随服务同版本发布"的前提。若将来出现仓外
独立演进的 gRPC 客户端，需回到 §2 原口径（只新增、不删除）或改走 v2 包名。

**本次执行**：22 个 int64 ID 字段跨 7 个 proto 删除并 reserved（product 10 / order 3 /
seller 3 / review 3 / promotion 1 / cart 1 / inventory 1）。
