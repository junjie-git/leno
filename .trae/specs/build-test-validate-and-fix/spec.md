# 克隆仓库 + 全量构建测试验证并修复 Spec

## Why
当前 `/workspace` 已是 Leno 仓库的工作副本（分支 `improve-0720`，工作区干净），但尚未在该环境执行过 `dotnet build` / `dotnet test`，且 `dotnet` SDK 未安装。需要建立一条"代码就绪 → 构建 → 测试 → 修复"的闭环，确保 `Leno.slnx` 可成功编译、单元测试全部通过，使仓库恢复到绿色可交付状态。

## What Changes
- 通过 `mise` 安装 .NET SDK `10.0.301`（`mise.toml` 已锁定版本）。
- 确保仓库为最新状态（已克隆，必要时 `git pull` 同步）。
- 执行 `dotnet restore Leno.slnx` 恢复依赖。
- 执行 `dotnet build Leno.slnx --configuration Release` 全量编译。
- 修复所有编译错误（以及阻断构建的告警/分析器错误，如 `RS0030` 等）。
- 执行 `dotnet test Leno.slnx --configuration Release --filter "Category!=Integration"` 运行全量单元测试。
- 修复所有失败的单元测试。
- 运行 `bash scripts/check-placeholders.sh` 确认无占位符回退（与 CI 一致）。
- 按用户规则：修复完成后以**中文**提交说明提交变更并推送到远程仓库。
- **受限范围**：当前环境无 Docker，`Category=Integration`（Testcontainers）集成测试无法执行，将在交付说明中明确标注为"环境受限跳过"。

## Impact
- Affected specs: 无直接相关 spec（本任务为工程化验证修复，不改动业务规格）。
- Affected code: 可能涉及任意 `.cs` 项目（编译错误/测试失败处），重点是 `src/` 下各 BC 的 Application/Domain/Infrastructure/Api 层与测试工程。预期不引入新业务逻辑，仅修复使其可编译/测试通过。

## ADDED Requirements

### Requirement: 环境就绪
系统（本工作环境）SHALL 安装并启用 `mise.toml` 中锁定的 .NET SDK `10.0.301`，使得 `dotnet --version` 可用且版本匹配。

#### Scenario: SDK 安装成功
- **WHEN** 执行 `mise install` 并激活环境
- **THEN** `dotnet --version` 输出 `10.0.301`（或兼容的 10.0.x）

### Requirement: 全量构建通过
执行 `dotnet build Leno.slnx --configuration Release` SHALL 成功完成，退出码为 0，无编译错误。

#### Scenario: 构建成功
- **WHEN** 运行 `dotnet restore Leno.slnx` 后运行 `dotnet build Leno.slnx --configuration Release --no-restore`
- **THEN** 所有项目编译通过，退出码 0

#### Scenario: 构建失败需修复
- **WHEN** 初次构建出现编译错误（CS* 错误或被升级为 error 的分析器规则）
- **THEN** 逐一定位根因并修复，直至重新构建退出码为 0；修复不得引入占位符/存根逻辑

### Requirement: 全量单元测试通过
执行 `dotnet test Leno.slnx --configuration Release --filter "Category!=Integration"` SHALL 全部通过，退出码为 0。

#### Scenario: 单元测试通过
- **WHEN** 构建成功后运行上述测试命令
- **THEN** 所有非集成测试用例通过，无失败/错误

#### Scenario: 测试失败需修复
- **WHEN** 存在失败的单元测试
- **THEN** 修正被测代码或测试代码使其通过；修复必须为真实实现，禁止注释掉断言或 `Assert.Ignore` 跳过

### Requirement: 占位符检查通过
执行 `bash scripts/check-placeholders.sh` SHALL 退出码为 0（与 CI 一致），确认无违规占位符。

#### Scenario: 无占位符
- **WHEN** 运行占位符检查脚本
- **THEN** 脚本退出码 0，无违规项

### Requirement: 变更提交与推送
所有修复产生的代码变更 SHALL 以中文 Conventional Commit 提交并推送到远程仓库（遵循用户规则）。

#### Scenario: 提交并推送
- **WHEN** 构建与测试均恢复绿色
- **THEN** 将变更以中文提交说明提交，并 `git push` 到远程当前分支 `improve-0720`

## MODIFIED Requirements
（无）

## REMOVED Requirements
（无）

## 约束与边界
1. **不回滚用户既有改动**：当前工作区为干净状态，不得执行 `git reset --hard`、`checkout .`、`clean -f` 等破坏性操作。
2. **零占位容忍**：所有修复必须为真实实现，禁止 `TODO`/`throw new NotImplementedException()`/空函数体等（遵循用户代码完整性契约）。
3. **集成测试受限**：环境无 Docker，`Category=Integration` 测试无法运行，不计入本次"修复"范围，但须在最终交付说明中明确告知。
4. **最小改动原则**：仅修复使构建/测试通过所需的内容，不做额外重构或风格化改动。
