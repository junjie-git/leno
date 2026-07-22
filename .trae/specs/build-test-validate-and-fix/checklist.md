# Checklist

- [ ] `dotnet --version` 输出为 10.0.301（或兼容 10.0.x），SDK 已就绪
- [ ] `dotnet restore Leno.slnx` 成功，退出码 0
- [ ] `dotnet build Leno.slnx --configuration Release` 成功，退出码 0，无编译错误
- [ ] 修复过程中未引入任何占位符（无 `TODO`/`NotImplementedException`/空函数体/`// 此处省略` 等）
- [ ] `dotnet test Leno.slnx --configuration Release --filter "Category!=Integration"` 全部通过，退出码 0
- [ ] 修复失败测试时未使用 `Assert.Ignore` / 注释断言 / 跳过用例等绕过手段
- [ ] `bash scripts/check-placeholders.sh` 退出码 0
- [ ] 未执行任何破坏性 git 操作（`reset --hard`/`checkout .`/`clean -f`/`push --force`）
- [ ] 修复变更已以中文 Conventional Commit 提交
- [ ] 变更已 `git push` 到远程分支 `improve-0720`
- [ ] 最终交付说明已明确告知：`Category=Integration` 集成测试因环境无 Docker 被跳过
