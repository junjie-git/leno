# 双轨下线收尾核实与残留收口 —— 设计文档（R2 修订版）

> 日期：2026-10-16 · 状态：**R2 已获用户批准，待实施**
> 修订记录：
> - **R1**（同日早前）：基于 `docs/双轨下线-实施方案.md` §7 过时状态编制，规划"批次 1 机械收敛 + 批次 2 四开关切流 + 交易闭环 7 项"。
> - **R2**（本版）：经全代码盘点证实 **R1 规划的实施对象已于 2026-09-23/24 全部执行完毕**。R2 将工作重心改为「文档 SSOT 对齐 + 残留收口 + 抽查补漏」。
> 前置文档：`docs/双轨下线-最优收尾方案.md`、`docs/双轨下线-实施方案.md` §7（其状态表已证实过时，本文档 W-A 任务负责回写）、`docs/todo/tasks/progress.md`（同上）

---

## 0. 决策记录

| # | 决策点 | 结论 | 状态 |
|---|--------|------|------|
| D-1 | 主方向 | 技术债收尾 + 业务功能补齐混合推进 | ✅ 继续有效 |
| D-2 | 双轨下线范围 = 批次 1+2 | ~~W4 退役另开窗口~~ | ❌ 失效：盘点证实 W4 亦已完成 |
| D-3 | 业务任务 = 交易闭环 7 项 | ~~待开发~~ | ❌ 失效：盘点证实已实现 |
| D-4 | 验证基线 V2 | 单测+契约为底，高风险项才起 compose | ✅ 继续有效（适配到新范围） |
| D-5 | C2 立即删除不设观测期 | — | ✅ 已被历史执行满足 |
| D-6 | D3 差分按促销域文档裁决 | — | ✅ 已被历史执行满足（`RuleEngineDiscountRegressionTests` 已固化期望值） |
| D-7 | 执行编排 = 风险递增串行 | 四阶段 | 🔄 演化：R2 重排为 W-A→W-B→W-C 三块 |
| **D-8** | **盘点结论**（2026-10-16） | 代码侧收尾已基本完成，`progress.md` 与实施方案 §7 全面过时，构成 SSOT 失真 | ✅ 新增 |
| **D-9** | **R2 新方向**（用户批准） | 文档 SSOT 对齐 + 残留收口 + 抽查补漏；原批次 1/2 与交易闭环开发任务全部取消 | ✅ 新增 |
| **D-10** | **AntiCorruption:UseGrpc 终局** | 按 D-9 收口：gRPC 定为唯一防腐路径，删开关、删 HTTP 降级路径；防腐端点配置缺失时 fail-fast 启动失败，不做静默降级 | ✅ 新增 |

## 0.1 盘点结论汇总（2026-10-16，代码证据）

### 双轨下线 19 项

| 组 | 项 | 状态 | 关键证据 |
|----|----|------|---------|
| 批次 1 | A5 端口清单 | ✅ 有残留缺陷 | `deploy/services.manifest.json` + `scripts/validate-service-ports.ps1` + CI `validate-service-ports` job（ci.yml:236）齐备；**残留**：Helm payment 探针 5158（values.yaml:175-176）、校验脚本不覆盖探针、serviceUrls 仅抽查 5 项 |
| 批次 1 | C2 旧路由 | ✅ | 探测器与 `/internal/*`（非 v1）端点全仓零命中 |
| 批次 1 | B3 延迟消息 | ✅ | `OrderAppService.cs:322`、`OrderSagaOrchestrator.cs:135` 走 `IMessageScheduler` |
| 批次 1 | C3 gRPC 契约 | ✅ | 各 proto "int64 双字段已删除，C3 2026-09-24"；余下 int64 均为合法金额/时间戳 |
| 批次 1 | F5 乐观锁 | ✅ | EF 全部 `IsRowVersion()`；`Cart.Revision` 为 Redis CAS 计数器（`builder.Ignore`），非别名 |
| 批次 2 | D1 Saga / D2 PM / D3 规则引擎 / D4 SKU 快照 | ✅ | 开关全删（`CartSnapshotOptions.cs:13`、`PromotionCalculateAppService.cs:9` 注释记载）；差分回归测试已固化 |
| W4 | A1/A2/A6+G5 三 BC 退役 | ✅ | src/Services 无旧 BC；compose:324 注释"已退役"；网关路由指 identity/accesscontrol/usercenter；Order 积分防腐层指 `ServiceUrls:PointsApi` |
| W5 | G1 Auth:Mode | ✅ 已删除 | `WebApplicationExtensions.cs:133` 注释记载；全仓无代码级引用 |
| W5 | E2/E3 读模型终局、其余澄清项 | ⬜ 不在本范围 | 独立澄清项，维持移交 |

**特性开关现状：6 → 1（仅剩 `AntiCorruption:UseGrpc`）**，即 W-B 的收口对象。

### 19 个 P2 业务任务（progress.md 标"未开始"的）

| 任务 | 状态 | 关键证据 |
|------|------|---------|
| PRD-06 运营全量商品列表 | ✅ | `AdminProductsController.cs:99-119` + `ProductApiTests.cs:204-237` |
| PRD-07 SKU 唯一校验 | ✅ | `IProductUniquenessChecker.cs` + `ProductUniquenessChecker.cs:20-35` + `ix_skus_sku_code` 唯一索引 |
| CART-05 全选/取消全选 | ✅ | `Cart.cs:220-235 ToggleAllSelection` + `CartsController.cs:76-78 PATCH /api/cart/selection` |
| ORD-09 强制取消 | ✅（完整性待 W-C 复核） | `OrdersController.cs:197-201` + ForceCancel 三套集成测试 |
| RAS-06 凭证/评价图片 | ✅ | `AfterSalesController.cs:21` 注入 `IFileStorageService` + API 测试 |
| PAY-05 对账 | ✅ | `ReconciliationService.cs` + `ReconciliationDiff` 全套（聚合/仓储/迁移/Controller/三层测试） |
| PAY-06 渠道配置 | ✅ | `PaymentChannelConfig` 全套 + `ChannelConfigProvider` + Controller |
| UA-06 OAuth 客户端配置 | ✅ | `OAuthClientConfiguration` + `ExtendOAuthClientForOidc` 迁移 |
| UA-07 三方绑定/解绑 | ✅ | `Bind/UnbindExternalLogin` 端点 + `IdentityApiTests.cs:663-711` |
| NTF-10~13 | ✅ | `ChannelSelector`、`NotificationCallbacksController:54-129`、`NotificationRecordAppService:32-90`、`NotificationTemplateAppService:43-174` |
| SK-03/04、PM-08 | ✅ | OpenTelemetry/Consul 热更新接入、`TaskAppService`（260 行） |
| SK-05 布隆过滤器 / SK-06 健康检查 UI | ⬜ 未核实 | W-C 核查项 |
| **UA-08 审计日志中间件** | 🟡 **架构已转向** | HTTP 中间件形态不存在于 Identity；审计采集由 SystemAdmin `AuditLogConsumer` 事件聚合承担（SYS-05）。是否补 Identity 管道中间件 → W-C 裁决项 |
| 前端落点 | ✅ 大部分存在 | operations 端 PaymentChannels/Reconciliation/OrderManagement/ProductAudit 页面齐备；buyer-app/system-admin 落点在 W-C 抽查 |

---

## 1. 新范围（三块工作，串行）

```
W-B  残留收口（代码）   B1 A5 残留修复 → B2 AntiCorruption:UseGrpc 单轨化   ← 先做，核心价值
W-C  抽查补漏（核查）   C1 SK-05/06 与 UA-08 定性 → C2 前端落点抽查 → C3 缺口补齐（如有）
W-A  文档 SSOT 对齐     A1 progress.md → A2 实施方案 §7 → A3 收尾方案状态头  ← 最后做，以最终代码状态为准
```

执行顺序理由：先收代码（W-B 的 B2 是唯一实质双轨残留）；W-C 的核查结论决定 W-A 回写内容的准确性（UA-08 定性、SK-05/06 状态直接影响 progress.md 怎么写），故 W-C 先于 W-A。全程 TDD、每块闸门确认（D-7）。

## 2. W-B 残留收口设计（代码工作）

### 2.1 B1：A5 残留修复

1. **Helm payment 探针端口修正**：`deploy/helm/leno/values.yaml:175-176` 的 readinessProbe/livenessProbe `port: 5158` → `5156`。这是真缺陷：Helm 部署的 payment 健康检查打在错误端口，Pod 将永不就绪。
2. **校验脚本增强（防回潮）**：`scripts/validate-service-ports.ps1` 增加第 4 层校验——同一服务块内 `readinessProbe.port` / `livenessProbe.port` 必须等于 manifest `httpPort`（网关跳过同现有逻辑）。
3. **serviceUrls 覆盖面扩展**：`urlMap` 从 5 项（PaymentApi/OrderApi/ProductApi/PromotionApi/IdentityApi）扩展为 manifest 全部可映射服务（对照 `deploy/docs/consul-kv-coverage-audit.md` 的映射清单逐项登记；仅校验 values.yaml 中已登记项，缺失项不报警——与现有"仅校验可映射项"口径一致）。
4. **kv-seed.json 直校验**：脚本对 `deploy/consul/kv-seed.json` 中 `ServiceUrls__*` 键值做同规则校验（此前只校验 values.yaml 的 serviceUrls 段，KV 种子本身是盲区）。

验证：人为改错一个端口/探针 → 校验脚本报错退出 1 → 恢复后通过；CI `validate-service-ports` job 绿。

### 2.2 B2：AntiCorruption:UseGrpc 单轨化（最后一个双轨开关）

**门控点全清单**（盘点已确认）与处置：

| # | 位置 | 现状 | 处置 |
|---|------|------|------|
| 1 | Product/Points/Order/Review/SellerShop/Payment/Cart/Promotion 八个 Api 的 `Program.cs`（各 ~L45-50） | `if (AntiCorruption:UseGrpc)` 才映射 gRPC 服务端 | 改为无条件 `MapGrpcService` |
| 2 | AccessControl `Program.cs:45-47` | 同上 | 同上 |
| 3 | Order `Infrastructure/ServiceCollectionExtensions.cs:159-203` | UseGrpc=true 注册 gRPC 客户端，否则 HTTP 直调 | 删分支，无条件注册 gRPC 客户端；HTTP 实现类删除 |
| 4 | Review `Infrastructure/ServiceCollectionExtensions.cs:202` + `NullProductInfoQueryService.cs` | 配置缺失时"降级 HttpClient + 警告日志" | 删降级路径；配置缺失改为启动时抛异常（fail-fast，D-10） |
| 5 | Cart 防腐层（`GrpcCartPriceService.cs`、`CartPriceDispatcherAdapter`） | gRPC/HTTP 双路径 | 删 HTTP 路径，gRPC 唯一 |
| 6 | `ConsulConfigWatcher.cs:23` 的 `UseGrpcConfigKey` 常量 + `ConsulReloadableConfigurationProvider` 相关注释 | 特化配置键 | 删常量；泛化热更新机制保留 |
| 7 | `Leno.Infrastructure.Tests` 的 `ConsulReloadableConfigurationProviderTests` / `ConsulConfigWatcherTests` | 以 UseGrpc 为测试配置键 | 改用中性键名（如 `Test:FeatureFlag`），保持测试语义 |
| 8 | `deploy/consul/kv-seed.json`、`values*.yaml` 中 `AntiCorruption__UseGrpc` 配置项 | 开关种子值 | 删除配置项 |
| 9 | 各处"双轨方案"历史注释 | 描述已删开关 | 顺手改为陈述现状（"gRPC 唯一路径"） |

**fail-fast 设计**（D-10）：防腐层注册扩展方法在启动时校验必需的 `AntiCorruption:GrpcEndpoints:*` 配置，缺失即 `InvalidOperationException` 带明确指引（"在 Consul KV 配置 GrpcEndpoints:Product 等端点后启动"），杜绝静默降级导致的数据不一致。

**验证策略（D-4 适配）**：
- 单测/集成测试全绿（HTTP 路径的既有测试改写为 gRPC 语义或删除）
- 全仓 grep `AntiCorruption:UseGrpc` 零代码命中（文档历史记载除外）
- 集成测试起一次 compose（consul + 相关服务），验证 KV 无开关值时服务正常启动、端点配置齐全时防腐调用走 gRPC
- 特性开关终态达成：**6 → 0**（收尾方案 §1 终态闭合）

## 3. W-C 抽查补漏设计（核查工作）

| # | 核查项 | 方法 | 产出 |
|---|--------|------|------|
| C1 | SK-05 布隆过滤器、SK-06 健康检查 UI | grep 实现与接线 | 定性 ✅/⬜/🟡；未实现则评估是否纳入本设计（预期：未实现则记入遗留，不临时扩scope） |
| C2 | UA-08 审计日志裁决 | 已证实：事件聚合形态存在（SYS-05），HTTP 中间件形态缺失 | **裁决点（向用户提问）**：a) 接受事件聚合为 UA-08 终态，关闭任务；b) 补 Identity 侧审计中间件 |
| C3 | 前端落点抽查 | buyer-app（CART-05 调用）、system-admin（PAY-05/06 页面）、operations（ProductAudit 是否含全量列表、OrderManagement 是否含 force-cancel 按钮） | 缺失页面列入补齐清单；补齐遵循各端既有 vitest spec 模式 |
| C4 | ORD-09 完整性复核 | 对照任务验收标准逐条核对（退款事件、释放库存/积分/优惠券、审计、双通知、Admin 授权） | 缺口列入补齐清单 |

## 4. W-A 文档 SSOT 对齐设计

按 W-B/W-C 完成后的最终状态回写（保持一次性、准确）：

| # | 文档 | 回写内容 |
|---|------|---------|
| A1 | `docs/todo/tasks/progress.md` | 19 项任务逐项改 ✅（完成日期 2026-09-23/24，证据列指向盘点表）；UA-08/SK-05/SK-06 按 C1/C2 裁决结果定性；全局统计（总数 87、完成数、完成率）重算 |
| A2 | `docs/双轨下线-实施方案.md` §7 | §7.1 已完成表并入批次 1/2、W4 全部项；§7.2 未完成表清空并注明"全部收口，除 E2/E3 移交"；§7.3 库存遗留 3/4 项维持 |
| A3 | `docs/双轨下线-最优收尾方案.md` | 文档头部加"实施完成（2026-09-23/24，核实于 2026-10-16）"状态注记 |
| A4 | 本设计文档 | R2 状态改"已完成"，附验收证据链接 |

## 5. 测试与验收策略（全程贯穿）

- **TDD**（规则 10.5）：B1 校验脚本增强先写"坏 fixture 必须报错"的失败测试再改脚本；B2 每删一条路径先让对应"gRPC 唯一路径"测试失败再删。
- **CI 守护**（防回潮）：`ci.yml` 新增一个轻量 job——grep 检查 `AntiCorruption:UseGrpc`/`UseSagaStateMachine`/`UsePaymentProcessManager`/`UseRuleEngine`/`UseSkuSnapshot`/`Auth:Mode` 六个开关名在 `src/` 代码零命中（文档目录豁免）。
- **前端**：任何补齐页面配 vitest spec；四端公共逻辑进 `@leno/shared`。
- **错误处理**：沿用既有全局异常中间件与一致错误格式（规则 8.3/7.7）。

## 6. 最终验收总表

| 块 | 交付 | 证据 |
|----|------|------|
| W-B | 探针缺陷修复 + 校验脚本四层校验；`AntiCorruption:UseGrpc` 删除，gRPC 唯一防腐路径 | CI 绿 + 人为破坏校验通过 + compose 冒烟 + 开关名零命中 |
| W-C | SK-05/06、UA-08、前端落点、ORD-09 完整性定性结论；缺口补齐（如有） | 定性表 + （如有）补齐项的测试绿 |
| W-A | progress.md / 实施方案 §7 / 收尾方案 三文档与代码现实一致 | 抽查比对记录 |
| 终态 | **特性开关 6 → 0**，双轨下线 19 项全部闭合 | 收口记录 |

## 7. 遗留移交

- E2 SellerShop 读模型、E3 读模型权威终局（W5 澄清项）
- 库存遗留第 3/4 项：台账 SellerId 语义契约、gRPC 通道加密
- UA-08 若裁决为"补中间件"（C2-b）但工期外溢，可单列移交
- W-C 若发现 SK-05/06 未实现且纳入范围会破坏本设计边界，移交后续立项
