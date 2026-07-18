## 变更说明

<!-- 简要描述本 PR 做了什么、为什么做 -->

## 变更类型

- [ ] feat（新功能）
- [ ] fix（Bug 修复）
- [ ] refactor（重构）
- [ ] perf（性能优化）
- [ ] docs（文档）
- [ ] test（测试）
- [ ] chore（构建/工具/依赖）
- [ ] ci（CI 配置）

## 关联 Issue / Spec

<!-- 关联的 issue 号或 spec 文档路径，如 Closes #123 / Implements docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md §13 -->

## 影响范围

- **限界上下文**: <!-- 如 Product / Order / Promotion / PointsMembership / SellerShop / 网关 / BuildingBlocks -->
- **变更层**: <!-- Domain / Application / Infrastructure / Api / Contracts / Tests / Docs / Infra -->
- **向后兼容**: <!-- 是 / 否；若否则说明迁移路径 -->

## 验证清单

- [ ] 代码通过 `dotnet build Leno.slnx`（0 错误）
- [ ] 单元测试通过（`dotnet test`）
- [ ] 新增/修改的功能有对应测试
- [ ] 敏感配置未硬编码（使用环境变量或 Consul KV）
- [ ] Domain 层未引用 SharedContracts 或跨 BC Domain 项目
- [ ] 集成事件未实现 IDomainEvent，域事件未实现 IIntegrationEvent
- [ ] 错误码已映射到 HTTP 状态码（ErrorCodeMapping.cs）
- [ ] 文档已同步（如涉及 API 契约、编码规范、需求文档总览）

## 部署注意事项

<!-- 如需特别部署步骤（如 EF 迁移、Consul KV 写入、buf generate、helm upgrade），在此说明 -->

## 截图 / 日志

<!-- 如涉及 UI 变更或日志输出，附截图或日志片段 -->
