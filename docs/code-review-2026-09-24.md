# Leno 代码库全面审查报告

> **审查日期**：2026-09-24 ｜ **审查方式**：需求基线比对 + 4 路并行分区深查（后端/前端/测试/安全）+ 高危项人工复核
> **审查范围**：src/（16 个微服务 + BuildingBlocks + ApiGateway，约 2140 个 .cs）、web/（4 个 Vue3 应用）、tests/、deploy/、docker-compose.yml
> **需求基线**：docs/spec/00-12（V2.5）、docs/tasks/progress.md（120/120 任务）、USAGE.md v1.1

---

## TL;DR

- **完成度**：后端约 **92-93%**（120/120 任务属实，无造假式完成：全仓 0 处 `NotImplementedException`、0 处 TODO/FIXME、0 处 mock 假数据）；前端四端 82%–96%（buyer-app 最弱 82%）。
- **亮点**：Outbox 分片投递、Redis Lua 秒杀扣减、事件消费幂等基类、充血领域模型、支付回调先验签、固定时间比较 API Key——核心机制均为真实实现，代码重复度极低。
- **最大风险**：①3 个服务 appsettings.json 硬编码 sa 密码且已进 git 历史；②Redis 无密码且暴露宿主机；③UserCenter 跨上下文直连 Identity 并旁路 Outbox 写库；④Inventory 测试为零 + 核心交易链路无端到端测试；⑤全部容器 root 运行。
- **结论**：业务实现质量显著高于平均水平，但**部署/安全加固与测试补强是上线前必须完成的功课**。

---

## 一、功能完成度评估

### 1.1 后端（按限界上下文）

| 上下文 | 完成度 | 主要缺口 |
|---|---|---|
| Identity | 95% | — |
| Product | 95% | — |
| Cart | 95% | — |
| Order | 95% | — |
| Payment | 95% | — |
| Notification | 95% | Domain 层耦合 SharedContracts（架构小瑕疵） |
| Inventory | 92% | 秒杀回退幂等 check-then-act 非原子（#12） |
| Promotion / Points / Membership / AfterSales / AccessControl | 90% | 消费幂等需逐一确认（#13） |
| SystemAdmin | 90% | 看板数据源故障静默返回零值（#19） |
| Review | 88% | gRPC proto 未重新生成，订单行匹配半成品（#20） |
| SellerShop | 88% | 仪表盘时间范围参数未消费（#21） |
| UserCenter | 85% | 跨 BC 直连 Identity + 旁路 Outbox（#4，P0） |

### 1.2 前端（4 应用）

| 应用 | 完成度 | 主要缺口 |
|---|---|---|
| system-admin | 96% | 功能最全（2FA、权限双校验），仅缺 token 刷新 |
| operations | 94% | 5 个千行组件、shared 未抽库 |
| seller | 88% | **2FA 明确占位未启用**、店铺描述等次级功能 |
| buyer-app | 82% | 店铺详情靠 productApi 拼凑、无 refresh token、7 个测试文件对 46 个视图、9 个千行组件 |

### 1.3 对照需求尚缺失/未完善的功能点

| # | 缺失项 | 证据 | 影响端 |
|---|---|---|---|
| F1 | 卖家端两步验证（system-admin 已有完整实现，seller 端登录框 disabled"暂未启用"） | web/seller/src/modules/08-account/views/Login.vue:96 | 卖家 |
| F2 | 买家端店铺详情独立数据源（复用商品搜索结果拼凑，未消费 /api/shops/{id}） | web/buyer-app/src/modules/04-shop/views/ShopDetail.vue:24 | 买家 |
| F3 | Refresh token 机制（4 端均无，token 长期有效直至过期） | 各端 auth.store.ts | 全部 |
| F4 | Review gRPC 订单行匹配（proto 已定义 order_line_id/spu_id 字段，Generated 代码未再生，代码填 Guid.Empty 双轨兜底） | Leno.Review.Infrastructure/Services/Grpc/GrpcOrderStatusProvider.cs:104-122 | 评价域 |
| F5 | SellerShop 仪表盘时间范围过滤（接口契约有 StartDate/EndDate，实现"预留扩展点暂不消费"） | ShopDashboardQueryHandler.cs:26 | 卖家 |
| F6 | 业务微服务可观测性接入（AddLenoOpenTelemetry 已实现但 16 个服务均未启用，仅网关有完整三支柱） | USAGE.md §9.1 自述 | 运维 |
| F7 | 契约测试推广（Pact 基建就绪，实际仅 Order→Product 1 条样例契约） | tests/Contracts/ | 全局 |
| F8 | 核心交易链路端到端测试（下单→支付→扣库存→发通知，tests/Integration 仅 1 文件且 mock 掉全部业务逻辑） | tests/Integration/DomainMigrationIntegrationTests.cs:46-55 | 全局 |

---

## 二、问题清单（按严重程度与优先级）

> 严重程度：🔴 高 / 🟡 中 / 💭 低；优先级：**P0** 上线前必须修复 ｜ **P1** 下一迭代 ｜ **P2** 排期改进

### P0 —— 上线阻塞项

| # | 级别 | 类别 | 位置 | 问题 | 修复建议 |
|---|---|---|---|---|---|
| 1 | 🔴 | 安全 | `Leno.AfterSales.Api/appsettings.json:11`、`Leno.Points.Api/appsettings.json:11`、`Leno.Review.Api/appsettings.json:11` | **sa 密码明文入库**（`Password=Leno_SqlServer_2026!`），同文件 SchedulerDb 已用 `${MSSQL_SA_PASSWORD}` 占位，三服务漏改；密码已进 git 历史 | 改占位符+环境变量注入，**轮换该密码**，git 历史清理评估 |
| 2 | 🔴 | 安全 | `docker-compose.yml:21-28` | Redis 无 `--requirepass` 且 6379 映射宿主机；库内存密码重置 token（`reset:pwd:`）与秒杀库存，未授权访问即可读写 | 加密码 + 去掉宿主机端口映射（仅内网互通） |
| 3 | 🔴 | 安全 | 全部 17 个 Dockerfile（如 `Leno.ApiGateway/Dockerfile:7`） | 容器以 root 运行（无 USER 指令），逃逸后即 root | 末尾加 `USER app`（镜像自带 UID 1654） |
| 4 | 🔴 | 架构 | `Leno.UserCenter.Infrastructure.csproj:6-7`、`UserDefaultAddressStore.cs:39-46` | UserCenter 直接引用 **Identity.Domain + Identity.Infrastructure**，直接操作他域 DbContext 调 `user.SetDefaultAddress()` 并 `SaveChangesAsync` —— 跨 BC 写他域聚合 + **旁路 Outbox 丢 User 域事件** + 注释与实现不符 | 改为集成事件或 Identity 侧 internal API（防腐层抽象真正落地） |
| 5 | 🔴 | 测试 | `Leno.Inventory.Application.Tests` / `Domain.Tests`（csproj 存在，**0 个 .cs 文件**） | 库存扣减是交易核心却零测试；空壳工程交付"测试存在"的假象 | 补库存域单测；空壳工程要么补齐要么移出 slnx |

### P1 —— 下一迭代应修复

| # | 级别 | 类别 | 位置 | 问题 | 修复建议 |
|---|---|---|---|---|---|
| 6 | 🟡 | 安全 | `InternalApiKeyMiddleware.cs:53-60` | ApiKey 未配置 + Development 环境 fail-open 放行全部 `internal/` 端点；生产误设环境变量即裸奔（prod 有 fail-closed 兜底） | 启动时校验环境合法性 + fail-open 时告警审计 |
| 7 | 🟡 | 安全 | 15 个服务 appsettings.json（如 `Leno.Identity.Api/appsettings.json:43`） | `RequireHttpsMetadata: false` 无生产 gate（仅网关有校验），下游直连可被 JWKS 中间人伪造 token | `WebApplicationExtensions` 统一加生产校验 |
| 8 | 🟡 | 安全 | `GlobalExceptionMiddleware.cs:97-103` | `ArgumentException.Message` **无条件**返回客户端（可能泄漏 SQL/内部字段名）；Development 返回 exception.Message | 恒返通用文案 + 业务错误码白名单 |
| 9 | 🟡 | 安全 | `Directory.Build.props:10` | `NoWarn` 压制 NU1902/NU1903（NuGet 传递依赖漏洞告警），供应链风险静默 | 移除压制，集中升级处理 |
| 10 | 🟡 | 安全 | `docker-compose.yml:10,14,41,58,78` | MSSQL/RabbitMQ/ES 端口全量映射宿主机；健康检查命令行内嵌 sa 密码（docker inspect 可见） | 移除非必要端口；密码改 `--password-file`/secret |
| 11 | 🟡 | 安全 | web/buyer-app、seller 等 `auth.store.ts:105`（4 端同模式） | token 存 localStorage（XSS 可窃取）、无 refresh 轮换；`client.ts:37` 绕 store 直读 `'auth'` 魔法 key | 评估 httpOnly cookie 或至少补 refresh 轮换；key 常量化 |
| 12 | 🟡 | 并发 | `SeckillStockAppService.cs:92-101` | 秒杀回退 IsProcessed→Restore→Mark 三步 check-then-act 非原子（对比 StockConfirmConsumer 的正确占锁做法）；当前靠 Lua 上限兜底 | 改 `TryMarkAsProcessingAsync` 原子占锁 |
| 13 | 🟡 | 架构 | 约 10 个 IConsumer 直接实现（如 `UserEventConsumer.cs:12`、`LoginLogConsumer.cs:18`） | 未继承 `IntegrationEventConsumerBase` 幂等基类，幂等性依赖各自实现（LoginLog 重复消费=重复日志行） | 逐个确认或收敛到基类 |
| 14 | 🟡 | 架构 | `Leno.Infrastructure/BannedSymbols.txt:13-14` | DbContext.Save* 禁令仅在 BuildingBlocks 生效，**各 BC 可自由旁路 Outbox**；已存在旁路点：`EfCoreMenuRepository.cs:46,54,64`、`EfCoreLoginLogRepository.cs:45`、`QualificationExpiryReminder.cs:111` | 复制 BannedSymbols.txt 到各 BC 并升级 RS0030=error |
| 15 | 🔴 | 测试 | AccessControl/AfterSales/Review/UserCenter 各仅 1 个测试文件（覆盖 4-6%）；Membership 仅 1 文件 | 6 个服务测试覆盖 ~0-9%，远低于核心 BC 的 23-38% | 按 P0 业务权重排期补齐，Inventory 优先 |
| 16 | 🔴 | 测试 | tests/Contracts/ | 契约测试仅 Order→Product 一条（Cart→Product、Order→Payment/Inventory 等关键内部契约缺失） | 按调用图补齐 Top5 内部契约 |
| 17 | 🟡 | 前端 | buyer-app：7 spec / 46 视图，9 个 >800 行组件（最大 1426 行） | 买家主链路组件级测试为零 + 超大组件回归风险最高 | 拆分 + 补组件测试 |
| 18 | 🔴 | 重复 | `seller/client.ts` 与 `system-admin/client.ts` **md5 相同**；`format.ts` 三端 md5 相同已开始漂移 | 4 端各持一份 client/format/logger/DataTable 拷贝，修 bug 需改 4 处 | 提取 `@leno/shared` workspace 包（pnpm workspace 已就绪） |
| 19 | 🟡 | 可靠性 | `StatisticsMetricsSource.cs:47-53` | 运营看板 GMV/支付成功率等指标在下游故障时**静默返回零值**，仅日志无报警，运营易误判 | 零值占位加 staleness 标记 + 报警 |
| 20 | 🟡 | 依赖 | web/buyer-app、seller `package.json` | axios ^1.7.9 受 CVE-2025-27152 影响（修复版 ≥1.8.2） | 升级 axios ≥1.8.x，跑 pnpm audit |
| 21 | 🟡 | 依赖 | `Directory.Build.props:28-41` | xunit 2.9.0（2.x 维护模式）、FluentAssertions 7.0.0（v8 起商业授权）、YARP 版本未收编集中管理 | 规划迁移评估；YARP 版本收编 props |

### P2 —— 排期改进

| # | 级别 | 类别 | 位置 | 问题 | 建议 |
|---|---|---|---|---|---|
| 22 | 💭 | 架构 | `Leno.Notification.Domain.csproj` | 16 个 Domain 中唯一引用 SharedContracts，领域层耦合集成事件契约 | 引出所需抽象到 SharedKernel |
| 23 | 💭 | 架构 | `NotificationCallbacksController.cs:100-120` | 控制器承载仓储查询+领域调用+UoW 提交（验签与幂等本身正确） | 编排下沉 Application 层 |
| 24 | 💭 | 安全 | `DlqCleanupJob.cs:218` | RabbitMQ 密码缺失静默回退 "guest" | fail-fast 报错 |
| 25 | 💭 | 安全 | 网关 `appsettings.json:25-29` | CORS 含 localhost:3000 + AllowCredentials，建议按环境分文件 | 环境分文件 |
| 26 | 💭 | 安全 | `RedisAnonymousCartRepository.cs:358` | 匿名购物车 sessionId 无签名可枚举 | sessionId 加签名校验 |
| 27 | 💭 | 配置 | 网关 `appsettings.json:87` | AccessTokenExpiryMinutes=120 与签发方 30 分钟不一致，过期 token 可能被网关长期信任 | 对齐两处配置 |
| 28 | 💭 | 测试 | `Notification.Infrastructure.Tests/SmokeTests.cs:8-14` | 占位测试仅断言程序集可加载（注释自认待补充） | 删除或替换 |
| 29 | 💭 | 文档 | `RabbitMqEventBus.cs` 头注释 | 声称 "Topic" 交换机，实现为 fanout（USAGE.md 已勘误，代码注释未改） | 改注释 |
| 30 | 💭 | 前端 | operations/DataTable.vue:105 等 9 处 | `as any` 集中在 antd 事件回调适配 | 补类型定义 |
| 31 | 💭 | 前端 | 4 端 package.json | vue/vite/pinia/vitest 整体落后一个小版本周期（Vite 7/vitest 3/pinia 3 已发布），无已知高危 | 例行升级 |

---

## 三、技术债盘点

### 3.1 代码重复（严重度：🟡，集中在前端）
- 后端重复度**极低**（亮点）：16 个 Program.cs 仅 46-81 行/个，启动逻辑收敛到 BuildingBlocks；消费幂等模板收敛到 `IntegrationEventConsumerBase`；唯一轻度重复是各 Consumer 的占锁/释放/标记三段式。
- 前端 4 端 shared 层拷贝是主要债务：client.ts（2 端 md5 完全相同）、format.ts（3 端相同+1 端漂移）、DataTable/ChartLine/logger/auth.store 均重复 3-4 份。pnpm workspace 基建已就绪，提取成本低。

### 3.2 测试覆盖（严重度：🔴，结构性失衡）
| 层 | 成熟度 | 现状 |
|---|---|---|
| 单元 | B- | 核心 BC（Cart/Order/Payment/Product/SystemAdmin）23-38% 扎实；Inventory 0% + 6 个服务 0-9% |
| 集成 | B | Testcontainers 真实用（Cart/Order/秒杀链路），Payment/Inventory/Notification 未接入 |
| E2E | D | 核心交易链路零覆盖，唯一近似物 mock 掉全部业务逻辑 |
| 契约 | D+ | Pact 基建就绪，实际 1 条样例 |

### 3.3 坏味道（严重度：💭，后端几乎干净）
- 全仓无 async void、无 `.Result/.Wait()` sync-over-async、无 N+1、无内存大数据过滤；空 catch 均为有注释的合法降级场景；CancellationToken 传递普遍良好。
- 遗留坏味道主要是：前端 14 个 >800 行大组件（buyer-app 占 9 个）、`as any` 9 处、占位测试 1 个。

### 3.4 依赖健康（严重度：🟡）
- .NET 10 / EF Core 10.0.0 最新主版本 ✅；MassTransit 8.3.6、StackExchange.Redis 2.8.16 健康 ✅。
- 需关注：axios CVE（P1#20）、xunit 2.x 维护模式、FluentAssertions v8 商业授权墙、NU1902/1903 被压制（P1#9）、前端一个版本周期落后。

---

## 四、正面发现（值得保持）

1. **Outbox 分片投递**（`ShardedOutboxPublisher.cs`）：UPDLOCK/ROWLOCK/READPAST 行锁防多实例重复发布，两阶段标记 + 僵死恢复 + 积压报警 + SystemAdmin 运维闭环。
2. **秒杀扣减**（`RedisSeckillStockService.cs`）：Redis Hash + Lua 原子"校验+扣减+限购"，回退脚本带 TotalStock 上限防重复回退膨胀。
3. **事件幂等基类**（`IntegrationEventConsumerBase.cs`）：EventId 校验 + SET NX 原子占权 + 失败释放锁，抽查实现正确。
4. **领域模型质量**：Order 状态机每个迁移有前置校验+领域事件、金额统一 AwayFromZero；SPU/SKU 充血模型 private set + IReadOnlyList；Payment 回调先验签后业务、分渠道处理。
5. **安全细节**：ApiKey 固定时间比较防计时侧信道、JWT 全参数校验+30s ClockSkew、FromSqlRaw 参数化、日志不打印明文凭据、Swagger 生产不暴露。
6. **前端工程**：mock 双重守卫生产隔离、错误体系统一、路由守卫完整（system-admin 还有 meta.roles/permission 双校验）、console.log 零遗留。

---

## 五、建议修复顺序

| 批次 | 内容 | 理由 |
|---|---|---|
| 第 1 批（本周） | P0 #1-#5：轮换泄漏密码、Redis 加密、Dockerfile USER、UserCenter 跨域耦合改造、Inventory 测试 | 安全红线 + 架构红线 |
| 第 2 批（下迭代） | P1 安全组（#6-#11）+ 并发组（#12-#13）| 上线前安全加固 |
| 第 3 批（持续） | 测试补强（#15-#16、F8）、前端 @leno/shared 提取（#18）、buyer-app 组件拆分与测试（#17） | 长期质量基建 |
| 第 4 批（排期） | P2 全部 + 依赖升级（#20-#21、#31） | 维护性 |

---

## 六、修复进展（2026-09-24 当日更新）

### 已完成（6 个 commit）

| Commit | 内容 | 对应问题 |
|---|---|---|
| `9344e7b0` | 4 个服务（Membership 亦漏改）硬编码 sa 密码改 `${MSSQL_SA_PASSWORD}` 占位；**密码已泄漏需运维轮换** | P0#1 |
| `c0675811` | Redis requirepass（REDIS_PASSWORD，env 注入）、宿主机端口绑 127.0.0.1、redis/mssql 健康检查改运行时读 env；网关/Inventory 连接串补 password；.env.example 与 USAGE.md 同步 | P0#2 + P1#10 部分 |
| `0f89a5fa` | 17 个 Dockerfile 加 `USER $APP_UID` + 预建 /app/logs、/app/uploads 可写目录（已实际构建验证 uid=1654） | P0#3 |
| `1d6a3f54` | UserCenter→Identity 跨 BC 直连改造：Identity 新增 `PUT internal/v1/users/{id}/default-address`，UserCenter 改 HTTP 防腐层（AntiCorruptionBase 模式），移除对 Identity 两个工程的引用与 IdentityDb 连接串，kv-seed 增补配置，测试 15/15 | P0#4 |
| `7686df3a` | Inventory 空壳测试工程补 45 个单测（Domain 31 + Application 14）；SeckillStockAppService.RestoreAsync 原子占权修复 | P0#5 + P1#12 |
| `c09d4f67` | ArgumentException 响应改通用文案；NoWarn 移除 NU1902/NU1903 | P1#8、P1#9 |

### 复核后确认无需改动（报告信息滞后）

- **P1#6**：内部 Key 生产 fail-closed 已由 `EnsureInternalApiKeyConfigured()` 启动校验覆盖（`WebApplicationExtensions.cs:258` 接线全部 BC，生产缺 Key 拒绝启动）。
- **P1#7**：`RequireHttpsMetadata` 生产门禁已由 `StartupConfigurationValidationService`（`StartupConfigurationValidation.cs:186-195`）实现，仅 Production 生效。

### 遗留待办

- P0#1 关联：**运维侧轮换 SQL Server sa 密码**并评估 git 历史清理（代码侧已完成）。
- P1#11（前端 token/'auth' 魔法 key）→ 并入第 3 批与 `@leno/shared` 提取一起做。
- P1#13（约 10 个 IConsumer 幂等基类收敛）、P1#15-#21 及 P2 全部 → 按批次排期。
