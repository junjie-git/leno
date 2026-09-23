# A-1 拆分方案：UserAuth 退役 → Identity + UserCenter + AccessControl

> 制定时间：2026-09-21
> 决策依据：用户决策「采用 A-1 彻底拆分」+「`api/admin/roles` 归到 AccessControl」
> 关联：`docs/双轨情况检测报告.md` A6 / A3 / H1；`docs/rs256-推进方案.md` 路线 A

---

## 1. 核心结论

**目标架构已在代码层完整实现，工作重心不是"写新代码"，而是「补部署 + 做切换 + 迁数据 + 退役旧单体」。**

逐项核对后，三个目标 BC 的实现完整度都足够：

| BC | 实现完整度 | 建表路径 | 部署状态 |
|----|-----------|---------|---------|
| **Identity** | 完整（含 2FA / OAuth / 密码栈 / RS256 三模式签名） | ⚠️ EF 迁移只有 2 个增量（`ExtendOAuthClientForOidc`、`AddPasswordHashVersionColumn`），**缺 InitialCreate**；建表实际靠 `identity-accesscontrol-initial.sql` | ❌ 无容器 |
| **UserCenter** | **完整垂直切片** —— Domain 聚合（Address / BrowseHistory / Favorite / NotificationPreferences）+ 仓储 + EF 配置 + DbContext + 应用服务 + 校验器 + Api.Tests | ❌ **零建表路径** —— 无 `Migrations/` 目录（仅有 `UserCenterDbContextDesignTimeFactory`），且 Helm 的 `files/migrations/` 中**无 `usercenter-*.sql`** | ❌ 无容器 |
| **AccessControl** | 完整 —— `AdminRolesController` 注释明确"**从 UserAuth BC 迁移，沿用 7 个端点契约**"，`api/admin/roles` 已就位 | ⚠️ 无 EF 迁移，建表靠 `identity-accesscontrol-initial.sql`（**与 Identity 合并在同一文件**） | ❌ 无容器 |

**关键含义**：`api/admin/roles → AccessControl` 这条决策**在代码层已经完成**，无需新开发；UserCenter 的资料类能力（4 条路由）同样已完成。剩下的是工程收尾。

---

## 2. 待补缺口（状态已更新至 2026-09-21）

| # | 缺口 | 影响 | 状态 |
|---|------|------|------|
| G1 | UserCenter 无建表路径 | 阻塞 —— 服务能部署但无表可用 | ✅ **已解决**：生成 `20260921060658_InitialCreate`（含 addresses / browse_histories / favorites / notification_preferences / notification_preference_items / outbox_messages + 索引） |
| G2 | AccessControl 无 EF 迁移，且与 Identity 挤在同一 SQL 文件 | 中等 | ✅ **已解决**：生成 `20260921060704_InitialCreate`；冲突的手写 SQL 已删除 |
| G3 | ~~Identity 缺 InitialCreate~~ | — | ⚠️ **原判断有误，已更正**：`20260723181050_ExtendOAuthClientForOidc` **本身就在 CreateTable 全部 6 张表**，它其实是事实上的 InitialCreate（只是名字起错）。Identity 的 EF 链**本身是自洽的**，无需新增迁移。真正的问题是手写 SQL 与 EF 链冲突（见下） |
| G4 | compose / Helm 无三个服务定义 | 阻塞 | ✅ **已解决（2026-09-21）**：新建 3 个 Dockerfile；compose 补 3 个服务（5162/5163/5164）；Helm `values.yaml` 补 3 个服务组（12 → 15）；5 个模板均从 `.Values.services` 派生，无需改动 |
| G5 | UserAuth 未退役 | 收尾 | ⬜ **未处理**：10 条路由已全部有归属，见第 3 节 |

### 2.1 G3 更正说明 + 由此暴露的真问题

原判断"Identity 缺 InitialCreate"**是错的**。查证：
- `20260723181050_ExtendOAuthClientForOidc` → `CreateTable` × 6（users / user_external_logins / refresh_tokens /
  two_factor_sessions / oauth_clients / outbox_messages）+ 9 个索引 → 它就是 InitialCreate
- `20260723193852_AddPasswordHashVersionColumn` → 仅 `AddColumn(password_hash_version)`
- 两者 ID 有序、链完整 → **Identity 的 EF 迁移链自洽，能独立建库**

**真问题**是手写 SQL 与 EF 链**冲突**：
- 手工 `identity-accesscontrol-initial.sql` 用 phantom ID `20260723000000_IdentityAccessControlInitial`
  建了**同一批表**（与 `20260723181050` 建的表重复）
- 由于 helm migration-job 只遍历 `.Values.services`，而 identity/accesscontrol **不在其中** →
  **这个文件从未被执行过**（它既没建过表，也没污染过任何 `__EFMigrationsHistory`）
- 但若有人把 `identity` 加进 values.yaml，Job 会先建表，随后应用启动时 EF `Migrate()`
  会再执行 `20260723181050` 重复建表 → **直接崩**
- 结论：该文件是**死代码且有害**，已删除（两个目录各一份）

---

## 3. 路由归属表（切换后）

| 原 UserAuth 路由 | 迁往 | 现状 |
|-----------------|------|------|
| `api/auth` | **Identity** | ✅ 已实现（Task A3 返工 + 9 端点） |
| `api/account` | **Identity** | ✅ 已实现 |
| `api/users/me` | **Identity** | ✅ 已实现 |
| `api/admin/users` | **Identity** | ✅ 已实现 |
| `api/admin/oauth-clients` | **Identity** | ✅ 已实现 |
| `api/internal/users` | **Identity** | ✅ 已实现 |
| `api/users/me/addresses` | **UserCenter** | ✅ 已实现 |
| `api/users/me/favorites` | **UserCenter** | ✅ 已实现 |
| `api/users/me/browse-history` | **UserCenter** | ✅ 已实现 |
| `api/users/me/notification-preferences` | **UserCenter** | ✅ 已实现 |
| `api/admin/roles` | **AccessControl** | ✅ 已实现（7 端点契约沿用） |

**结果**：UserAuth 的 10 条路由全部有归属，可实现**完全退役**。

---

## 4. 执行阶段

```
阶段 1  建表路径补齐（G1–G3）—— 决策依赖 N1
        └─ UserCenter / AccessControl / Identity 的 schema 事实来源统一

阶段 2  部署三个服务（G4）
        ├─ Dockerfile + compose 服务 + Helm 服务组 + values
        └─ 迁移 Job + ingress/健康检查对齐现有 11 个服务

阶段 3  数据迁移（决策依赖 N2）
        ├─ 账号/凭证类：LenoUserAuth → LenoIdentity
        └─ 资料类：LenoUserAuth → LenoUserCenter（地址 / 收藏 / 浏览 / 通知偏好）

阶段 4  路由切换（决策依赖 N3，**必须原子**）
        └─ 因 9 条路由冲突，不能灰度共存 —— 需一次性切换宿主

阶段 5  验收与退役（G5）
        ├─ 端到端回归（登录 / 刷新 / 2FA / 资料读写 / 角色管理）
        └─ 下线 UserAuth：容器 → 代码 → DB（保留只读快照一段时间）
```

**阶段 1 与阶段 2 可并行**（建表路径不影响容器定义）。

---

## 5. 风险清单

| 风险 | 说明 | 缓解 |
|------|------|------|
| **切换非原子 → 路由打架** | 9 条路由重叠，Identity 与 UserAuth 同时对外暴露会冲突 | 走"网关切流 + 一次性切换"，不做灰度共存 |
| **数据迁移丢数据/不一致** | 账号数据迁移涉及密码哈希（`Argon2id + pepper`），pepper 与 KMS 相关 | 迁移前确认 pepper 一致性；先做校验脚本比对行数与关键字段 |
| **密码哈希算法差异** | UserAuth 用 bcrypt，Identity 用 Argon2id + pepper，且 Identity 有 `BcryptPasswordVerifier` 兼容旧哈希 | **需确认**：迁移后旧用户能否直接登录（依赖兼容校验路径是否生效） |
| **Session/RefreshToken 失效** | 两 BC 的 refresh token **存储介质不同**：UserAuth 用 **Redis**（`RegisterRefreshTokenStore`，默认 `RefreshToken:Provider=Redis`，InMemory 仅限 Development 且有 fail-fast 保护）；Identity 用 **DB**（`EfCoreRefreshTokenRepository` → `RefreshTokens` 表） | 切换签发方时现有 refresh token 无法沿用 → **全体用户需重新登录**。要么接受一次登出，要么把 Redis 中的 token 迁到 Identity 表 |
| **UserCenter 无表 → 资料数据无处落** | G1 未解决前无法迁移资料类数据 | 阶段 1 优先级最高 |
| **RS256 与拆分叠加** | 两条工作流都动认证域，同时推进风险叠加 | 建议：**先完成拆分与切换（HS256 不变），再切 RS256** —— 避免一次变更混合两类风险 |

---

## 6. 与 RS256 的依赖关系（重要）

```
A-1 拆分 + 切换 ──► Identity 上线并接管签发 ──► RS256 阶段 2/3 才可执行
                                              （RS256 阶段 1 验签侧不受阻塞）
```

**建议顺序**：A-1（阶段 1→5，全程保持 HS256）→ 再启动 RS256 阶段 2/3。
理由：把"服务拆分+数据迁移"与"签名算法切换"两个高风险变更**解耦**，各自可独立回归。

---

## 7. 决策点状态

| # | 问题 | 状态 |
|---|------|------|
| **N1** | 建表事实来源：EF 迁移 vs 手工 SQL？ | ✅ **已决策：采用 EF 迁移统一**（执行方案见第 9 节） |
| **N2** | 数据迁移策略：停机迁 / 双写 / 按需迁？ | ✅ **已决策：停机迁**（执行方案见第 10 节） |
| **N3** | 路由切换方式：网关一次性切换 / 维护窗口 / 蓝绿？ | ⬅ **待决策**（9 条路由冲突决定不能灰度共存） |
| **N4** | 迁移的单一执行点：部署期 Job vs 启动时迁移？ | ✅ **已决策并实施**：分环境方案（见 9.4.1）——Development 走启动迁移，其余环境走 Helm Job；配置 `Database:MigrateOnStartup` 可覆盖 |

---

## 8. 需补充的信息

1. **`LenoUserAuth` 数据量级** —— 账号数、地址数、收藏数、浏览记录数（浏览记录通常量最大，可能需按期归档）
2. **前端/第三方是否强依赖 UserAuth 现有部署** —— 换宿主对客户端是否透明、网关能否无感切换
3. **旧用户密码哈希** —— UserAuth 侧是 bcrypt 还是已迁 Argon2id？迁移后能否直接登录
4. **RefreshToken 存储介质迁移** —— UserAuth 用 Redis（`RefreshToken:Provider=Redis`），Identity 用 DB 表。
   切换时现有 refresh token 能否沿用？**是否接受一次全体重新登录**？（这是切换日体验的关键决策）
5. **bcrypt → Argon2id 兼容校验是否已就绪** —— UserAuth 注册的是 `BcryptPasswordHasher`，
   Identity 用 Argon2id + pepper 并提供 `BcryptPasswordVerifier`。
   需确认迁移后**存量 bcrypt 用户能否直接登录**（兼容路径是否生效、pepper 是否与旧数据一致）

---

## 9. N1 执行方案（EF 迁移统一）

> ✅ 决策（2026-09-21）：**采用 EF 迁移统一作为 schema 的事实来源**

### 9.1 现状：迁移机制实际分成三类

| 类别 | BC | 机制 |
|------|----|------|
| **A. 已在 EF 管线内** | 11 个：userauth / product / cart / order / promotion / payment / pointsmembership / reviewaftersales / sellershop / notification / systemadmin | `scripts/generate-migration-scripts.ps1` 调 `dotnet ef migrations script --idempotent` **从 EF 迁移生成** SQL |
| **B. 手写 SQL 冒充生成物** | **identity + accesscontrol** | `identity-accesscontrol-initial.sql` 是**手工文件**，被放进生成产物目录（`scripts/migrations/` 与 helm `files/migrations/`），看起来像生成物但不是 |
| **C. 完全无建表路径** | **usercenter** | 无 EF 迁移、无 SQL（见 G1） |

### 9.2 ⚠️ 执行前必须注意的正确性风险：phantom MigrationId

`identity-accesscontrol-initial.sql:32` 手工往 `__EFMigrationsHistory` 插入了
**一条 EF 代码库里并不存在的迁移 ID**：

```sql
DECLARE @MigrationId nvarchar(150) = N'20260723000000_IdentityAccessControlInitial';
```

而 Identity 里**真实的** EF 迁移只有两个：`20260723181050_ExtendOAuthClientForOidc`、
`20260723193852_AddPasswordHashVersionColumn`（**均缺 InitialCreate**）。

后果：
- 手工 SQL 建表后，历史表里有 phantom 记录，但真实增量迁移的 ID **不在**历史里 →
  EF `Migrate()` 会把两个增量迁移当作"待应用"再跑一遍
- 也就是说**运行时行为依赖"手工 SQL 建出的结构恰好等于 InitialCreate 的产物"**这一脆弱假设；
  一旦手工 SQL 与 EF 模型漂移，增量迁移会在不匹配的结构上执行
- 这正是 H1 描述的"两条路径事实来源不同"的具体形态

### 9.3 执行步骤与状态（2026-09-21 已执行 1–4）

```
步骤 1  补齐 EF 迁移（事实来源）                                        ✅ 已完成
        ├─ UserCenter   → 20260921060658_InitialCreate
        │                  （6 表 + 索引，含 outbox_messages）
        ├─ AccessControl → 20260921060704_InitialCreate
        └─ Identity      → 无需新增（见 2.1：其 EF 链本就自洽，原判断有误）

步骤 2  把三个 BC 加入生成管线                                          ✅ 已完成
        └─ generate-migration-scripts.ps1 的 $bcProjects 由 11 → 14 个
           （同时补充了 .NOTES：执行前必须注入 LENO_DESIGNTIME_CONNECTION_STRING）

步骤 3  移除冲突的手工文件                                              ✅ 已完成
        └─ 删除 scripts/migrations/ 与 deploy/helm/leno/files/migrations/ 下的
           identity-accesscontrol-initial.sql（死代码且有害，见 2.1）

步骤 4  重新生成并同步                                                  ✅ 已完成
        └─ 生成 identity-initial.sql / accesscontrol-initial.sql / usercenter-initial.sql
           并同步到 helm files/migrations/（两目录各 16 个文件，已对齐）

步骤 5  对齐 MigrateWithLockAsync（单一执行点）                          ⬜ 待办（N4，分环境方案）
        └─ Development 保留启动迁移；已部署环境改由 Helm Job 负责
```

**顺带修复的构建质量问题**：EF 生成的迁移代码会触发 **CA1861**（内联数组误报）。
已在根 `.editorconfig` 增加 `[**/Migrations/**/*.cs]` 段，标记 `generated_code = true`
并关闭 CA1861 —— 对**所有 BC 的迁移**统一生效，而非只针对新增的两个。

**验证结果**：`UserCenter.Infrastructure` / `AccessControl.Infrastructure` 构建 **0 warning、0 error**；
生成的幂等 SQL 均带 `__EFMigrationsHistory` 守卫，且使用**真实** EF MigrationId（无 phantom）。
```

### 9.4 遗留子决策：单一执行点（H1 的剩余分支）

事实来源统一后，"**在哪执行**"仍未定 —— 现在两个执行点都在：

| 执行点 | 位置 | 现状 |
|--------|------|------|
| 部署期 | `helm/templates/migration-job.yaml`（pre-install/pre-upgrade hook） | ✅ 在用，有明确设计理由（运行时镜像无 SDK，改用 sqlcmd + 预生成 SQL） |
| 运行期 | 各 BC `Program.cs` → `MigrateWithLockAsync<TDbContext>()` | ✅ 在用，带 Redis 分布式锁 |

**选项**：
- (a) 保留 Job，移除启动迁移 → 部署期一次性完成，应用启动更快、职责清晰；代价：本地开发需另想办法
- (b) 保留启动迁移，移除 Job → 简单，但每个副本启动都尝试迁移
- (c) **分环境**：Development 用启动迁移（开发便利），已部署环境用 Job（受控）→ 推荐，兼顾两端

### 9.4.1 ✅ 决策与实施（2026-09-21，采用 (c) 分环境方案）

**改动位置**：`Leno.Infrastructure.Persistence/Persistence/DatabaseMigrationExtensions.cs` —— 单点改造，
19 个 BC 的 `Program.cs` **零改动**（签名不变）。

**判定优先级**（`ShouldMigrateOnStartup`）：

```
1. 显式配置 Database:MigrateOnStartup（可空 bool）存在 → 以其为准（运维逃生舱，可强制开/关）
2. 解析不到 IHostEnvironment（单元测试 / 非 Host 场景）→ 无法判定环境，保持既有行为（执行）
3. IHostEnvironment.IsDevelopment() → 执行
4. 其余环境（Docker / Staging / Production）→ 跳过，交由部署期 migration-job
```

**两个实现细节**：
- 环境判定放在**取分布式锁之前** —— 跳过的环境不消耗锁、不产生任何 DB 往返
- 跳过时输出 `LogInformation`，明确写出"由部署期迁移 Job 负责"及配置项名，便于排障时一眼确认

**验证**：
```
dotnet test --filter "FullyQualifiedName~DatabaseMigrationExtensions"
  Total tests: 8   Passed: 8   Failed: 0
  ├─ 既有 2 个（锁获取 / 锁占用跳过）—— 未注册 IHostEnvironment，走规则 2，行为不变 ✅
  └─ 新增 6 个：Development 执行 / Docker·Staging·Production 三环境跳过 /
               配置强制 true（Production 也执行）/ 配置强制 false（Development 也跳过）

dotnet build Leno.Order.Api + Leno.UserCenter.Api -> 0 Error（BC 侧不受影响）
```

**运维提示**：若某环境确实需要启动迁移（例如排查问题时），
设置环境变量 `Database__MigrateOnStartup=true` 即可覆盖，无需改代码或重新发版。

### 9.5 需先确认的信息

1. **生产 `LenoIdentity.__EFMigrationsHistory` 目前实际有哪些行** ——
   决定 Identity 的 phantom ID 是"需要保留"（已应用）还是"可以纠正"
2. **`LenoAccessControl` 库是否已被手工建过表** —— 若已有表，InitialCreate 需按 phantom 同法处理
3. **`LenoUserCenter` 库是否已存在** —— kv-seed 已下发连接串，但服务未部署，预计为空库

---

## 10. N2 执行方案（停机迁）

> ✅ 决策（2026-09-21）：**数据迁移采用停机迁**

**收益**：schema 迁移与数据迁移可**合并进同一个维护窗口**执行，无需双写与增量同步，
方案复杂度与出错面大幅下降。

**停机窗口内的执行顺序**：

```
1. 停止 UserAuth（及其它依赖它的服务）+ 网关摘流
2. 冻结写入（确认无在途请求）
3. 备份 LenoUserAuth（全量）
4. 建新库 schema：LenoIdentity / LenoUserCenter / LenoAccessControl（EF 迁移）
5. 数据迁移：
   ├─ 账号 / 凭证 / OAuth / 2FA  → LenoIdentity
   └─ 地址 / 收藏 / 浏览记录 / 通知偏好 → LenoUserCenter
       （AdminRoles 的角色与权限数据 → LenoAccessControl）
6. 校验：行数比对 + 关键字段抽样 + 抽查登录
7. 切换路由宿主（Identity / UserCenter / AccessControl 上线，网关指向新服务）
8. 冒烟：登录 / 刷新 / 2FA / 资料读写 / 角色管理
9. 解除冻结，恢复流量
10. UserAuth 转只读保留（观察期后再下线代码与库）
```

**注意**：第 5 步需处理**主键映射**——若 `LenoUserAuth` 与目标库的 ID 生成策略不同，
需保证外键（如地址 → 用户）在迁移后仍然一致。建议**原样保留主键**，不做重新生成。

**⚠️ 2026-09-21 更新**：用户已明确 **数据迁移按全新处理（目标环境无任何旧数据）** →
上述第 5 步数据迁移与之相关的第 6 步校验**不再需要**，主键映射/哈希兼容/refresh token 介质差异
三个风险项一并消失。停机窗口从"数据迁移窗口"缩减为**纯粹的切换窗口**（仅执行 schema 迁移 + 路由切换）。

---

## 11. G4 执行记录（2026-09-21）

### 11.1 前置核对

- 三个 Api 项目**均无 Dockerfile**（全仓仅 11 个，正好对应原 11 个已部署 BC）
- compose 主机端口 5151–5161 已满、网关占 8080 → 新增用 **5162 / 5163 / 5164**
- 5 个 Helm 模板（deployment / service / hpa / ingress / migration-job）**全部 `range .Values.services`**
  → 只需补 values 条目，**模板零改动**
- `migrations-configmap.yaml` 用 `Files.Glob "files/migrations/*.sql"` → 新增 SQL **自动纳入**

### 11.2 实际改动

| # | 文件 | 改动 |
|---|------|------|
| 1 | `src/Services/Identity/Leno.Identity.Api/Dockerfile` | 新建（照既有 11 个的模式：sdk 构建 + aspnet 运行，EXPOSE 8080） |
| 2 | `src/Services/AccessControl/Leno.AccessControl.Api/Dockerfile` | 新建 |
| 3 | `src/Services/UserCenter/Leno.UserCenter.Api/Dockerfile` | 新建 |
| 4 | `docker-compose.yml` | 补 `identity-api:5162` / `access-control-api:5163` / `user-center-api:5164`，依赖与健康检查对齐既有服务；并加注释说明与 user-auth-api 的路由冲突、二者不可同时对外暴露 |
| 5 | `deploy/helm/leno/values.yaml` | 服务数 12 → **15**；补 `identity` / `accesscontrol` / `usercenter` 三组（端口 5162-5164、grpcPort 5262-5264、`connectionStringKey` 分别 IdentityDb/AccessControlDb/UserCenterDb、`migration.runOnInit: true`）；同步更新文件头注释 |
| 6 | `.github/workflows/ci.yml` | **两个矩阵各 12 → 15**：`build-services.matrix.project` 与 `docker-build.matrix.service` 各补 3 条 |
| 7 | `.github/workflows/cd.yml` | `env.SERVICES` 12 → 15（该清单用于 "Check all service images exist" 前置校验） |

> **为什么必须改 CI/CD**：`ci.yml` 的 `docker-build` 矩阵决定**镜像是否会被构建推送**。
> 若只改 values.yaml 而不补矩阵，Helm 部署会因镜像不存在而 **ImagePullBackOff** ——
> 这正是 G4 容易漏掉的一半。

### 11.3 验证

```
PyYAML 校验
  docker-compose.yml             OK  services=24（原 21 + 3）
  deploy/helm/leno/values.yaml   OK  services=15
  .github/workflows/ci.yml       OK  build-services=15  docker-build=15
  .github/workflows/cd.yml       OK  SERVICES=15

migration-job glob 核对（文件名必须匹配 {service}-*.sql，否则 Job 报"未找到迁移脚本"）
  identity       -> identity-initial.sql        ✅
  accesscontrol  -> accesscontrol-initial.sql   ✅
  usercenter     -> usercenter-initial.sql      ✅
  （对照）userauth / order -> 既有文件名保持一致

docker compose config --quiet  ->  EXIT=0（官方 Compose 解析器通过）
```

### 11.4 尚未完成（需后续补齐）

1. **Secret 已就绪，无需改代码**：核对确认 `deploy/scripts/create-secrets.ps1:52` 的 `$BcList`
   已包含全部 19 个 BC（含 `identity:IdentityDb`、`accesscontrol:AccessControlDb`、`usercenter:UserCenterDb`），
   `deploy/consul/kv-seed.json` 同样已含三者连接串。
   → 剩余仅是**运维动作**：在目标环境执行脚本创建/更新 `leno-db-connectionstrings`
2. **镜像名大小写约定已核实**：`docker/metadata-action` 会把镜像名自动转小写，
   故 matrix 用 PascalCase（`Identity`）→ 实际镜像 `leno-identity`，与 values.yaml 的
   `image.repository` 一致 ✅
3. **`global.serviceUrls` 未新增三者**：目前无 BC 读取（Notification 仍读 `UserAuthApi`），
   切换时需把调用方指向新服务 —— 属 N3 切换范畴
4. **Dockerfile 未做实际构建验证**：本环境未跑 `docker build`，
   依赖 CI 的 `docker-build` job（PR 触发即构建，可作首次验证）
5. **`identity` → `accesscontrol` 的 gRPC 端点**：Identity 的 `AntiCorruption:GrpcEndpoints:AccessControl`
   目前指向 `http://localhost:8082`，而 Helm `serviceUrls` 未含 AccessControl。
   当前 `UseGrpc=false`（KV 全量播种 false）故不影响；**若将来启用 gRPC，需补该服务地址**
