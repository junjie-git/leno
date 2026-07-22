# Tasks

- [x] Task 1: 环境就绪 — 安装 .NET SDK 10.0.301
  - [x] SubTask 1.1: 执行 `mise install` 安装 `mise.toml` 锁定的 dotnet 10.0.301（CDN 限速，改用 azureedge 镜像 8 路并行下载后手动解压至 dotnet-root，mise 复检识别为已安装）
  - [x] SubTask 1.2: 验证 `dotnet --version` 输出 10.0.301，runtimes（NETCore 10.0.9 / AspNetCore 10.0.9）就绪

- [x] Task 2: 仓库就绪与依赖恢复
  - [x] SubTask 2.1: 确认 `/workspace` 与 origin/improve-0720 同步（0/0），工作区仅含未跟踪的 spec 文档
  - [x] SubTask 2.2: `dotnet restore Leno.slnx` 修复 NU1605（Hosting.Abstractions/Logging.Abstractions 9.0.0→10.0.0 对齐解决方案）与 NU1101（移除不存在的 Microsoft.Extensions.Configuration.Memory 引用，AddInMemoryCollection 经 Leno.Testing→Microsoft.Extensions.Configuration 10.0.0 传递可用）；环境 nuget.org 被透明重定向至 nuget.azure.cn 镜像，已补充全局 dotnet-public 源；restore 退出码 0

- [ ] Task 3: 全量构建并修复编译错误
  - [ ] SubTask 3.1: 执行 `dotnet build Leno.slnx --configuration Release --no-restore`
  - [ ] SubTask 3.2: 收集所有编译错误（CS* 及被升级为 error 的分析器规则如 RS0030）
  - [ ] SubTask 3.3: 逐一修复错误，遵循零占位容忍原则（真实实现，不跳过/不注释绕过）
  - [ ] SubTask 3.4: 重新构建直至退出码为 0

- [ ] Task 4: 全量单元测试并修复失败
  - [ ] SubTask 4.1: 执行 `dotnet test Leno.slnx --configuration Release --no-build --filter "Category!=Integration"`
  - [ ] SubTask 4.2: 收集所有失败/错误用例
  - [ ] SubTask 4.3: 修复被测代码或测试代码使失败用例通过（禁止 `Assert.Ignore`/注释断言）
  - [ ] SubTask 4.4: 重新运行测试直至退出码为 0

- [ ] Task 5: 占位符检查
  - [ ] SubTask 5.1: 执行 `bash scripts/check-placeholders.sh`
  - [ ] SubTask 5.2: 若有违规则修复后复跑，直至退出码 0

- [ ] Task 6: 变更提交与推送（遵循用户规则）
  - [ ] SubTask 6.1: `git add` 相关修复文件（按文件名精确暂存，避免误提交敏感文件）
  - [ ] SubTask 6.2: 以中文 Conventional Commit 提交（如 `fix: 修复 Leno.slnx 构建与单元测试错误`）
  - [ ] SubTask 6.3: `git push` 到远程分支 `improve-0720`

# Task Dependencies
- Task 2 依赖 Task 1（需 SDK 才能 restore）
- Task 3 依赖 Task 2（需 restore 成功才能 build）
- Task 4 依赖 Task 3（需构建成功才能 test）
- Task 5 可与 Task 4 并行（仅文本扫描），但修复占位符可能影响 Task 3/4，建议在 Task 3 后执行
- Task 6 依赖 Task 3、Task 4、Task 5 全部完成
