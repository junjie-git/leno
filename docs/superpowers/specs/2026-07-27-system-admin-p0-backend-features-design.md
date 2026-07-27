# 系统管理后台 P0 功能后端实现设计文档

**文档版本**：V1.0
**创建日期**：2026-07-27
**所属项目**：Leno 电商平台
**文档类型**：后端实现设计 spec
**关联文档**：
- [docs/superpowers/specs/2026-07-27-system-admin-p0-features-supplement-design.md](./2026-07-27-system-admin-p0-features-supplement-design.md) — 前端 Mock + 后端 API 需求清单（前端契约来源）
- [docs/spec/12-系统管理域.md](../../spec/12-系统管理域.md) — 系统管理域需求文档
- [docs/handbook/03-architecture-overview.md](../../handbook/03-architecture-overview.md) — 整体架构概览

## 0 摘要

本 spec 落实前端 spec §3.8 文档化的后端 API 需求：5 个 Controller / 19 个 Endpoint，全部归入现有 [Leno.SystemAdmin](file:///workspace/src/Services/SystemAdmin) BC，与现有 14 个聚合根模式完全对齐。修改密码（第 6 项 P0 功能）已在 [Identity BC](file:///workspace/src/Services/Identity) 实装（`PUT /api/users/me/password`），本 spec 不重复实现。

**关键决策汇总**：

| 决策项 | 选择 | 理由 |
|---|---|---|
| BC 归属 | 全部归入 SystemAdmin BC | 与现有 Health/IndexRebuild/AuditLog 等运行时运维职责一致，单 BC 单 DbContext 模式 |
| 菜单存储 | EfCore 新聚合根 Menu | 与现有 14 个聚合根模式一致，支持事务与迁移 |
| 在线会话源 | Redis 会话存储（IUserSessionStore 抽象） | 登录即可见，无集成事件延迟；放 SharedKernel 抽象层 |
| 缓存监控 | StackExchange.Redis 直连 | 复用主 Redis 连接，INFO/SCAN/TYPE 命令直调 |
| 服务器监控 | .NET 进程内 API（System.Diagnostics） | 不依赖外部 Prometheus，启动即用 |
| 登录日志存储 | 新增 LoginLog 聚合根 | 与 AuditLog 解耦，字段差异大，语义清晰 |
| 跨 BC 协调 | Identity 同步调 IUserSessionStore + 异步发 UserLoggedInEvent | 在线用户要求登录即可见；登录日志可容忍 100-500ms 延迟 |

**交付物**：SystemAdmin BC 新增 2 个聚合根（Menu / LoginLog）+ 1 个 Redis 投影（OnlineUserSession）+ 5 Controller / 19 Endpoint + EF Core 迁移 + Redis 抽象实现 + .NET 进程监控 + 后台采样服务；Identity BC 在 AuthAppService.LoginAsync 末尾追加 2 行同步调用。新增测试用例 116 个。

## 1 总体架构与改造范围

### 1.1 总体架构

5 项新增 Controller 全部归入 [Leno.SystemAdmin.Api](file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api)，复用现有 DDD 四层与 DI 管线。改动面分布在三层：

```
Leno.SystemAdmin.Domain          +2 聚合根 (Menu/LoginLog) + 1 Redis 投影 (OnlineUserSession)
                                +2 仓储接口 + 1 抽象 (IUserSessionStore)
                                +3 域服务抽象 (IRedisCacheMonitor / IDotNetProcessMonitor / IMetricHistoryStore)

Leno.SystemAdmin.Application     +5 应用服务接口 + 实现
                                +DTOs 文件（按功能拆分）

Leno.SystemAdmin.Infrastructure  +3 EF 配置 + DbSet
                                +3 仓储实现
                                +1 迁移
                                +RedisUserSessionStore（StackExchange.Redis）
                                +RedisCacheMonitorService（StackExchange.Redis）
                                +DotNetProcessServerMonitorService（System.Diagnostics）

Leno.SystemAdmin.Api             +5 Controller
                                +扩展 ICurrentUserContext 增加 SessionId 属性（从 JWT jti claim 解析）

Leno.Identity.Application        扩展 IAuthAppService.LoginAsync 在登录成功时调
                                IUserSessionStore.RecordAsync(session)
                                （新增集成事件 UserLoggedInEvent）
```

### 1.2 改造前后对比

| 维度 | 改造前 | 改造后 |
|---|---|---|
| SystemAdminDbContext DbSet | 14 | 16（+Menu / +LoginLog；OnlineUserSession 仅 Redis） |
| Controllers | 17 | 22（+5） |
| 聚合根 | 14 | 16（+2；OnlineUserSession 为 Redis 投影非聚合根） |
| Endpoints | 现有 | +19 |

### 1.3 跨 BC 协调点

仅 1 处：Identity 域登录成功时需发布会话信息到 Redis。两种实现路径：

- **路径 1（采用）**：Identity 直接注入 `IUserSessionStore`（在 `Leno.Infrastructure.Abstractions` 中定义），登录成功同步写 Redis，无集成事件延迟
- **路径 2（已否决）**：Identity 发布 `UserLoggedInEvent`，SystemAdmin 消费写入 Redis，最终一致但延迟 100-500ms

选路径 1：在线用户场景要求"登录即可见"，延迟不可接受；`IUserSessionStore` 抽象放在 SharedKernel 层，SystemAdmin 提供实现，Identity 只依赖抽象。

### 1.4 不改动范围

- 修改密码：[Identity 域已实装](file:///workspace/src/Services/Identity)，无需任何后端改动
- 现有 14 个聚合根、17 个 Controller 的逻辑与契约均不变
- SystemAdmin.Api 的 `Program.cs` 仅新增 DI 注册，整体管线不变
- 前端契约（DTO 字段）严格对齐 [前端 spec §3.3-3.7](./2026-07-27-system-admin-p0-features-supplement-design.md)，后端是契约的消费方

### 1.5 依赖矩阵

```
Menu ──> 无外部依赖
LoginLog ──> Identity 发布 UserLoggedInEvent（异步）+ 本地写
OnlineUserSession ──> Identity 登录流程注入 IUserSessionStore
CacheMonitor ──> Redis 连接（已存在）
ServerMonitor ──> System.Diagnostics / GC / DriveInfo（BCL）
```

5 项功能相互独立，可并行开发。**关键路径**为 `IUserSessionStore` 抽象定义（路径 1 必需），需在第 1 阶段完成。

## 2 聚合根、仓储与 DTO

### 2.1 Menu 聚合根（菜单管理）

**位置**：`Leno.SystemAdmin.Domain/Aggregates/Menu.cs`

```csharp
namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 菜单聚合根：树形结构，支持 Directory / Menu / Button 三类节点。
/// 排序通过同级 Sort 字段控制；删除时递归处理子节点。
/// </summary>
public sealed class Menu
{
    public Guid Id { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public MenuType Type { get; private set; }
    public string? Path { get; private set; }
    public string? Component { get; private set; }
    public string? Icon { get; private set; }
    public int Sort { get; private set; }
    public string? Permission { get; private set; }
    public List<string> Roles { get; private set; } = [];
    public bool Visible { get; private set; } = true;
    public bool Cache { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // 工厂方法：CreateRoot / CreateChild
    // 行为方法：Rename / ChangePath / ChangeSort / ToggleVisible / ToggleCache / AssignRoles / MoveTo
    // 不变量：Button 类型 Path 必须为 null；Menu 类型 Component 必填；Sort >= 0
}
```

**枚举**：`MenuType { Directory = 1, Menu = 2, Button = 3 }`

**仓储接口**（`IMenuRepository`）：
- `Task<Menu?> GetByIdAsync(Guid id, CancellationToken ct)`
- `Task<List<Menu>> GetAllAsync(CancellationToken ct)` — 一次性载入全部，应用层组装树
- `Task<List<Menu>> GetChildrenAsync(Guid parentId, CancellationToken ct)`
- `Task<Menu?> GetByPathAsync(string path, CancellationToken ct)` — 唯一性校验
- `Task AddAsync(Menu menu, CancellationToken ct)`
- `Task UpdateAsync(Menu menu, CancellationToken ct)`
- `Task DeleteAsync(Guid id, CancellationToken ct)` — 仓储内部递归删除
- `Task<int> CountChildrenAsync(Guid parentId, CancellationToken ct)` — 删除前置校验
- `Task<List<Menu>> GetByRoleAsync(string role, CancellationToken ct)` — 动态菜单查询

**不变量与校验**（在聚合根内）：
- `Name` 非空，长度 1-32
- `Type == MenuType.Button` 时 `Path` 必须为 null
- `Type == MenuType.Menu` 时 `Component` 必填（前端 componentMap 路径）
- `Type == MenuType.Directory` 时 `Path` 可空或目录前缀
- `Sort >= 0`
- 删除带子节点的菜单抛 `SystemAdminDomainException("存在子菜单，无法删除")`，由应用层先调 `CountChildrenAsync` 校验

### 2.2 LoginLog 聚合根（登录日志）

**位置**：`Leno.SystemAdmin.Domain/Aggregates/LoginLog.cs`

```csharp
namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 登录日志聚合根：仅追加（Append-Only），登录成功或失败时由消费者写入。
/// 与 AuditLog 解耦：AuditLog 记录运营操作，LoginLog 专记认证事件。
/// </summary>
public sealed class LoginLog
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public Guid? UserId { get; private set; }      // 失败登录时为 null
    public string IpAddress { get; private set; } = string.Empty;
    public string? GeoLocation { get; private set; }
    public string Browser { get; private set; } = string.Empty;
    public string Os { get; private set; } = string.Empty;
    public LoginResult Result { get; private set; }
    public string? FailureReason { get; private set; }
    public int DurationMs { get; private set; }
    public string UserAgent { get; private set; } = string.Empty;
    public string? DeviceFingerprint { get; private set; }
    public string? RefererUrl { get; private set; }
    public string TraceId { get; private set; } = string.Empty;
    public DateTime LoginAt { get; private set; }

    // 工厂方法：CreateSuccess / CreateFailed
    // 无变更方法：日志仅追加
}
```

**枚举**：`LoginResult { Success = 1, Failed = 2 }`

**仓储接口**（`ILoginLogRepository`）：
- `Task<LoginLog?> GetByIdAsync(Guid id, CancellationToken ct)`
- `Task<(List<LoginLog> Items, int Total)> QueryAsync(LoginLogQuery query, CancellationToken ct)`
- `Task AddAsync(LoginLog log, CancellationToken ct)`
- `IAsyncEnumerable<LoginLog> StreamAsync(LoginLogQuery query, int limit, CancellationToken ct)` — CSV 导出
- **无 Update/Delete**：日志仅追加

**查询对象** `LoginLogQuery`：`Username?`、`Result?`、`LoginAtFrom?`、`LoginAtTo?`、`Page`、`PageSize`

### 2.3 OnlineUserSession 投影（在线用户，非聚合根）

**位置**：`Leno.SystemAdmin.Domain/Aggregates/OnlineUserSession.cs`

```csharp
namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 在线用户会话投影：存储在 Redis，不进入 EF Core DbContext。
/// 由 Identity 登录流程通过 IUserSessionStore.RecordAsync 写入，
/// SystemAdmin 通过 IUserSessionStore 查询与强制下线。
/// </summary>
public sealed class OnlineUserSession
{
    public string SessionId { get; set; } = string.Empty;       // JWT jti
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public string IpAddress { get; set; } = string.Empty;
    public string? GeoLocation { get; set; }
    public string Browser { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string TokenPreview { get; set; } = string.Empty;    // 前 8 位
    public string? DeviceFingerprint { get; set; }
    public int RequestCount { get; set; }
    public DateTime LoginAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public bool IsAnomaly { get; set; }
}
```

**抽象**（`Leno.Infrastructure.Abstractions`）：

```csharp
namespace Leno.Infrastructure.Abstractions;

public interface IUserSessionStore
{
    Task RecordAsync(OnlineUserSession session, CancellationToken ct = default);
    Task<List<OnlineUserSession>> QueryAsync(OnlineUserQuery query, CancellationToken ct = default);
    Task<OnlineUserSession?> GetByIdAsync(string sessionId, CancellationToken ct = default);
    Task<OnlineUserStats> GetStatsAsync(CancellationToken ct = default);
    Task RemoveAsync(string sessionId, CancellationToken ct = default);   // 强制下线
    Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default);
}
```

**Redis 键设计**：
- `session:{sessionId}` → Hash（单会话详情，TTL 24h）
- `session:user:{userId}` → Set（用户的所有 sessionId，便于多设备检测）
- `session:index` → ZSet（score = loginAt timestamp，便于按时间范围查询）

### 2.4 DTO 契约

严格对齐前端 spec §3.3-3.7 字段。**字段命名采用 camelCase 序列化**（System.Text.Json 默认），与前端 axios 自动转换对齐。

**MenuDto / CreateMenuDto / UpdateMenuDto / MenuSortItemDto**：对应 [前端 spec §3.3](./2026-07-27-system-admin-p0-features-supplement-design.md)。`Id` 用 `Guid` 序列化为字符串，前端 `string` 类型可接收。

**OnlineUserDto / OnlineUserStatsDto / OnlineUserQueryDto**：对应 [前端 spec §3.4](./2026-07-27-system-admin-p0-features-supplement-design.md)。`SessionDurationMs` 为派生字段，由应用层 `DateTime.UtcNow - LoginAt` 实时计算。

**LoginLogDto / LoginLogQueryDto**：对应 [前端 spec §3.5](./2026-07-27-system-admin-p0-features-supplement-design.md)。`Result` 用枚举 `Success` / `Failed` 序列化为字符串。

**RedisInfoDto / KeyspaceDto / RedisKeyDto / RedisKeyDetailDto / CacheKeyQueryDto**：对应 [前端 spec §3.6](./2026-07-27-system-admin-p0-features-supplement-design.md)。

**ServerSnapshotDto / MetricPointDto / MetricHistoryDto**：对应 [前端 spec §3.7](./2026-07-27-system-admin-p0-features-supplement-design.md)。`MemoryTotalBytes` 等用 `long`，前端 `number` 可接收（16GB = 1.7e10，< 2^53 安全整数）。

### 2.5 DTO 文件组织

按现有 [SystemAdminDtos.cs](file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/DTOs/SystemAdminDtos.cs) 模式，新 DTO 拆为独立文件避免单文件过大：

```
DTOs/
├── SystemAdminDtos.cs          (现有，不变)
├── AlertDtos.cs                (现有，不变)
├── OutboxMonitorDtos.cs        (现有，不变)
├── MenuDtos.cs                 (新增)
├── LoginLogDtos.cs             (新增)
├── OnlineUserDtos.cs           (新增)
├── CacheMonitorDtos.cs         (新增)
└── ServerMonitorDtos.cs        (新增)
```

### 2.6 不变量与校验总结

| 聚合根 | 关键不变量 | 校验位置 |
|---|---|---|
| Menu | Button 无 Path；Menu 必填 Component；Sort ≥ 0；Name 1-32 字符 | 聚合根工厂方法 |
| LoginLog | Result=Success 时 FailureReason 必为 null；Result=Failed 时 FailureReason 必填 | 聚合根工厂方法 |
| OnlineUserSession | SessionId 非空；UserId 非空；LoginAt ≤ LastActivityAt | DTO 校验 + Redis 写入前断言 |

## 3 数据流与跨 BC 协调

### 3.1 菜单管理数据流

```
管理员 → POST /api/admin/menus
  → MenusController.Create
    → IMenuAppService.CreateAsync
      → Menu.CreateRoot / Menu.CreateChild (领域校验)
      → IMenuRepository.AddAsync (EF Core)
      → IUnitOfWork.SaveChangesAsync
      → 发布 MenuChangedEvent (Outbox)
    → 返回 MenuDto

读取：GET /api/admin/menus/tree
  → IMenuAppService.GetTreeAsync
    → IMenuRepository.GetAllAsync (一次载入全部)
    → 应用层组装树形结构（按 ParentId 分组递归）
    → 返回 List<MenuDto> (含 children)
```

**Outbox 事件**：`MenuChangedEvent` 用于缓存失效通知。当前阶段仅写 Outbox，不阻塞交付。

### 3.2 在线用户数据流（关键路径）

```
用户登录 → Identity.AuthAppService.LoginAsync
  ↓ 校验密码成功后
  ↓ 生成 JWT (jti = sessionId)
  → IUserSessionStore.RecordAsync(session)   ★ 同步写 Redis
    Redis 写入：
      - SET session:{jti} <Hash> EX 86400
      - SADD session:user:{userId} {jti}
      - ZADD session:index {loginAtTs} {jti}
  → 返回 LoginResultDto

管理员 → GET /api/admin/online-users
  → OnlineUsersController.List
    → IOnlineUserAppService.QueryAsync
      → IUserSessionStore.QueryAsync (按 ZSet 范围查 + 批量 HGETALL)
      → 派生 SessionDurationMs / IsAnomaly
      → 返回 List<OnlineUserDto>

管理员 → DELETE /api/admin/online-users/{sessionId}
  → OnlineUsersController.ForceOffline
    → IOnlineUserAppService.ForceOfflineAsync
      → 校验：sessionId != 当前操作者 sessionId (否则抛 ForbiddenException)
      → IUserSessionStore.RemoveAsync(sessionId)
        Redis 删除：
          - DEL session:{jti}
          - SREM session:user:{userId} {jti}
          - ZREM session:index {jti}
      → 发布 UserForceLoggedOutEvent (Outbox)
        → Identity 消费，将 jti 加入 JWT 黑名单（如已实现黑名单机制）
```

**异常会话检测算法**（在 `OnlineUserAppService.QueryAsync` 中派生）：

```csharp
// 多设备：同一 userId 在 session:user:{userId} 集合中存在 ≥2 个 sessionId
// 异地：同 userId 不同会话的 IpAddress 跨网段（简化为不同 /16 前缀）
session.IsAnomaly = userSessionIds.Count >= 2
                 || HasCrossSegmentIp(userSessionIds, sessions);
```

### 3.3 登录日志数据流

```
用户登录 → Identity.AuthAppService.LoginAsync
  ↓ 生成 JWT 后
  → 发布 UserLoggedInEvent (MassTransit)
    Identity 发布事件 → MassTransit → SystemAdmin.LoginLogConsumer 消费
    → ILoginLogRepository.AddAsync
```

**事件契约**（`Leno.SharedContracts`）：

```csharp
public sealed record UserLoggedInEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string Username { get; init; } = string.Empty;
    public Guid? UserId { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public string? RefererUrl { get; init; }
    public string TraceId { get; init; } = string.Empty;
    public int DurationMs { get; init; }
    public bool Success { get; init; }
    public string? FailureReason { get; init; }
}
```

**UA 解析（LoginLog 数据流）**：SystemAdmin 消费者侧用 `UAParser`（NuGet 包，约 100KB）解析 `Browser` / `Os` / `DeviceFingerprint` 派生字段。`UserLoggedInEvent` 仅携带原始 `UserAgent` 字符串，不在 Identity 端解析（保持事件契约精简）。

**UA 解析（OnlineUserSession 数据流）**：Identity 写入 Redis 投影时**会**解析 UA（用于在线用户列表直接展示 Browser/Os 字段），Identity 注入 `IUserAgentParser` 抽象（SystemAdmin 共享同一实现 `UAParserUserAgentParser`）。

**地理定位**：内网 IP（`10.0.0.0/8` / `172.16.0.0/12` / `192.168.0.0/16`）标记为「内网·本地」；公网 IP 通过 `MaxMind GeoLite2` 本地库查询（一次性下载 50MB `.mmdb`，启动时加载，无外部 API 依赖）。MaxMind 免费授权可在 `appsettings.json` 配置 license key。

### 3.4 缓存监控数据流

```
管理员 → GET /api/admin/cache/info
  → CacheController.GetInfo
    → ICacheMonitorAppService.GetRedisInfoAsync
      → IRedisCacheMonitor.GetInfoAsync
        → IConnectionMultiplexer.GetServer(endpoint).Info()
        → 解析 INFO 输出为 RedisInfoDto
      → 返回

管理员 → GET /api/admin/cache/keys?pattern=*&type=string&db=0&page=1&size=20
  → ICacheMonitorAppService.QueryKeysAsync
    → IRedisCacheMonitor.ScanKeysAsync(db, pattern, type, page, size)
      → IServer.Keys(pattern, pageSize) 流式 SCAN
      → TYPE key 过滤
      → 分页返回 RedisKeyDto[]
    → 返回
```

**抽象**（`Leno.SystemAdmin.Domain.Services`）：

```csharp
public interface IRedisCacheMonitor
{
    Task<RedisInfoDto> GetInfoAsync(CancellationToken ct = default);
    Task<List<KeyspaceDto>> GetKeyspacesAsync(CancellationToken ct = default);
    Task<PagedResult<RedisKeyDto>> ScanKeysAsync(int db, string pattern, string? type, int page, int pageSize, CancellationToken ct = default);
    Task<RedisKeyDetailDto?> GetKeyDetailAsync(string key, int db, CancellationToken ct = default);
    Task<bool> DeleteKeyAsync(string key, int db, CancellationToken ct = default);
}
```

**关键实现细节**：
- `IServer.Keys()` 默认扫描整个 keyspace，单次最多返回 `pageSize * 5` 后过滤分页（防止 KEYS 阻塞）
- `GetKeyDetailAsync` 根据 `TYPE` 调用对应命令：string → `GET`、hash → `HGETALL`、list → `LRANGE 0 -1`、set → `SMEMBERS`、zset → `ZRANGE WITHSCORES`
- 大 key 防护：value 序列化后超 1MB 截断并标记 `truncated: true`

### 3.5 服务器监控数据流

```
管理员前端 → 5s 轮询 GET /api/admin/server-monitor/snapshot
  → ServerMonitorController.GetSnapshot
    → IServerMonitorAppService.GetSnapshotAsync
      → IDotNetProcessMonitor.GetSnapshotAsync
        → Process.GetCurrentProcess() 获取 CPU/内存/进程数
        → GC.GetTotalMemory(false) / GC.CollectionCount
        → DriveInfo.GetDrives() 获取磁盘
        → Environment.MachineName / OSDescription / ProcessorCount
      → 返回 ServerSnapshotDto

管理员前端 → GET /api/admin/server-monitor/history?metric=cpu&range=5m
  → IServerMonitorAppService.GetHistoryAsync
    → IMetricHistoryStore.GetHistoryAsync(metric, range)
      → 从内存滚动窗口读取最近 300 点
      → 返回 MetricPointDto[]
```

**抽象**：

```csharp
public interface IDotNetProcessMonitor
{
    Task<ServerSnapshotDto> GetSnapshotAsync(CancellationToken ct = default);
}

public interface IMetricHistoryStore
{
    Task RecordAsync(MetricName metric, double value, CancellationToken ct = default);
    Task<List<MetricPointDto>> GetHistoryAsync(MetricName metric, TimeSpan range, CancellationToken ct = default);
}
```

**实现要点**：
- `DotNetProcessMonitor` 用 `Process.TotalProcessorTime` 增量计算 CPU 使用率（采样间隔 1s，记录上次值）
- `MetricHistoryStore` 用 `System.Threading.Channels` 维护内存滚动窗口（300 点 × 3 metric = 900 点，约 7KB）
- **后台采样服务** `ServerMetricSamplerBackgroundService`：1s 间隔调 `IDotNetProcessMonitor` 采样一次，写入 `IMetricHistoryStore`
- 历史数据不持久化（重启清空），符合"实时监控"语义

### 3.6 跨 BC 协调总结

| 协调点 | 方向 | 机制 | 时效 |
|---|---|---|---|
| 在线用户会话写入 | Identity → SystemAdmin | `IUserSessionStore` 抽象（Redis 直写） | 同步，<10ms |
| 在线用户强制下线 | SystemAdmin → Identity | `UserForceLoggedOutEvent`（Outbox） | 异步，1-5s |
| 登录日志写入 | Identity → SystemAdmin | `UserLoggedInEvent`（MassTransit） | 异步，100-500ms |
| UA 解析与地理定位 | SystemAdmin 内部 | `UAParser` + `MaxMind GeoLite2` | 同步，<5ms |
| 缓存监控 | SystemAdmin 内部 | `IConnectionMultiplexer` 直连 Redis | 同步，<50ms |
| 服务器监控 | SystemAdmin 内部 | `System.Diagnostics` 进程内 API | 同步，<5ms |

### 3.7 Identity BC 改动点

仅 1 处代码改动（[IAuthAppService](file:///workspace/src/Services/Identity/Leno.Identity.Application/IAuthAppService.cs) 实现类）：

```csharp
// Leno.Identity.Application/Services/AuthAppService.cs (现有 LoginAsync 方法末尾追加)
public async Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
{
    // ... 现有登录逻辑 ...
    var jwt = GenerateJwtToken(user, sessionId: jti);

    // ★ 新增：记录会话到 Redis
    var session = new OnlineUserSession
    {
        SessionId = jti,
        UserId = user.UserId,
        Username = user.Username,
        Roles = user.Roles,
        IpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        Browser = uaParser.ParseBrowser(userAgent),
        Os = uaParser.ParseOs(userAgent),
        TokenPreview = jwt.Token.Substring(0, 8),
        LoginAt = DateTime.UtcNow,
        LastActivityAt = DateTime.UtcNow,
    };
    await _userSessionStore.RecordAsync(session, ct);

    // ★ 新增：发布登录日志事件
    await _publishEndpoint.Publish(new UserLoggedInEvent
    {
        Username = dto.Username,
        UserId = user.UserId,
        IpAddress = session.IpAddress,
        UserAgent = userAgent,
        TraceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString(),
        DurationMs = durationMs,
        Success = true,
    }, ct);

    return jwt;
}
```

**Identity 项目需新增引用**：
- `Leno.Infrastructure.Abstractions`（已引用，复用 `IUserSessionStore`）
- `MassTransit.Abstractions`（已通过 `Leno.Infrastructure.EventBus` 间接引用）

### 3.8 数据流图汇总

```
┌─────────────┐       登录成功         ┌──────────────┐
│  Identity   │ ───────────────────→  │   Redis      │
│  AuthApp    │   IUserSessionStore   │  session:*   │
│  Service    │                       │              │
└──────┬──────┘                       └──────┬───────┘
       │                                     │ 读取
       │ UserLoggedInEvent                   │
       ↓                                     ↓
┌──────────────────────────────────────────────────────┐
│                MassTransit Bus                       │
└──────────────────────┬───────────────────────────────┘
                       ↓ 消费
┌──────────────────────────────────────────────────────┐
│              SystemAdmin BC                          │
│                                                      │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────┐   │
│  │ LoginLog    │  │ OnlineUser   │  │ Menu       │   │
│  │ Consumer    │  │ AppService   │  │ AppService │   │
│  ↓             │  ↓              │  ↓            │   │
│  LoginLog      │  IUserSession  │  IMenuRepo    │   │
│  Repository    │  Store (Redis) │  (EF Core)    │   │
│  (EF Core)     │                │                │   │
│  └─────────────┘  └──────────────┘  └────────────┘   │
│                                                      │
│  ┌─────────────────────┐  ┌──────────────────────┐   │
│  │ CacheMonitor        │  │ ServerMonitor        │   │
│  │ AppService          │  │ AppService           │   │
│  ↓                     │  ↓                      │   │
│  IRedisCacheMonitor    │  IDotNetProcessMonitor │   │
│  (StackExchange.Redis) │  + BackgroundService   │   │
│  └─────────────────────┘  └──────────────────────┘   │
└──────────────────────────────────────────────────────┘
```

## 4 Controller / Endpoint 设计与错误处理

### 4.1 MenusController（5 Endpoints）

**位置**：`Leno.SystemAdmin.Api/Controllers/MenusController.cs`

```csharp
[Authorize(Roles = "Admin")]
[ApiController]
public sealed class MenusController : SystemAdminControllerBase
{
    public MenusController(ICurrentUserContext currentUser, IMenuAppService menuAppService)
        : base(currentUser) { /* ... */ }

    [HttpGet("api/admin/menus/tree")]
    public async Task<IActionResult> GetTreeAsync(CancellationToken ct);

    [HttpPost("api/admin/menus")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateMenuDto body, CancellationToken ct);

    [HttpPut("api/admin/menus/{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateMenuDto body, CancellationToken ct);

    [HttpDelete("api/admin/menus/{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct);

    [HttpPut("api/admin/menus/sort")]
    public async Task<IActionResult> SortAsync([FromBody] List<MenuSortItemDto> items, CancellationToken ct);
}
```

**鉴权**：所有 endpoint 要求 `Admin` 角色。如未来引入细粒度权限，可通过 `menu:write` / `menu:read` 策略扩展。

**幂等性**：POST/PUT/DELETE 通过 `[IdempotencyKey]` 过滤器（项目已实装）保护，避免重复创建。

### 4.2 OnlineUsersController（4 Endpoints）

```csharp
[Authorize(Roles = "Admin")]
[ApiController]
public sealed class OnlineUsersController : SystemAdminControllerBase
{
    [HttpGet("api/admin/online-users")]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? username,
        [FromQuery] string? ipAddress,
        [FromQuery] DateTime? loginAtFrom,
        [FromQuery] DateTime? loginAtTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default);

    [HttpGet("api/admin/online-users/{sessionId}")]
    public async Task<IActionResult> GetByIdAsync(string sessionId, CancellationToken ct);

    [HttpDelete("api/admin/online-users/{sessionId}")]
    public async Task<IActionResult> ForceOfflineAsync(string sessionId, CancellationToken ct);

    [HttpGet("api/admin/online-users/stats")]
    public async Task<IActionResult> GetStatsAsync(CancellationToken ct);
}
```

**关键校验**：
- `sessionId` 必须是合法 JWT jti 格式（UUID 或 base64url），防止注入
- `ForceOfflineAsync` 内部校验 `sessionId != CurrentUser.SessionId`（当前操作者不能下线自己）

**当前用户 sessionId 获取**：扩展 `ICurrentUserContext` 增加 `SessionId` 属性（从 JWT `jti` claim 解析）。`ICurrentUserContext` 在 `Leno.Infrastructure.Auth` 中，已被 Identity 与 SystemAdmin 共享。

### 4.3 LoginLogsController（3 Endpoints）

```csharp
[Authorize(Roles = "Admin,Operator")]
[ApiController]
public sealed class LoginLogsController : SystemAdminControllerBase
{
    [HttpGet("api/admin/login-logs")]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? username,
        [FromQuery] LoginResult? result,
        [FromQuery] DateTime? loginAtFrom,
        [FromQuery] DateTime? loginAtTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default);

    [HttpGet("api/admin/login-logs/{id:guid}")]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct);

    [HttpGet("api/admin/login-logs/export")]
    public async Task<IActionResult> ExportAsync(
        [FromQuery] string? username,
        [FromQuery] LoginResult? result,
        [FromQuery] DateTime? loginAtFrom,
        [FromQuery] DateTime? loginAtTo,
        CancellationToken ct);
}
```

**鉴权**：`Admin` 与 `Operator` 均可读（与现有 [AuditLogsController](file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Api/Controllers/AuditLogsController.cs) 一致）。

**导出实现**：复用 [AuditLogAppService.ExportAuditLogsAsync](file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Application/Services/AuditLogAppService.cs) 模式，流式 `StreamAsync` 拼接 CSV，限制单次最大 10 万条。CSV 列对齐 [前端 spec §6.4](./2026-07-27-system-admin-p0-features-supplement-design.md)：

```csv
id,loginAt,username,ipAddress,geoLocation,browser,os,result,failureReason,durationMs,traceId
```

### 4.4 CacheController（5 Endpoints）

```csharp
[Authorize(Roles = "Admin")]
[ApiController]
public sealed class CacheController : SystemAdminControllerBase
{
    [HttpGet("api/admin/cache/info")]
    public async Task<IActionResult> GetInfoAsync(CancellationToken ct);

    [HttpGet("api/admin/cache/keyspaces")]
    public async Task<IActionResult> GetKeyspacesAsync(CancellationToken ct);

    [HttpGet("api/admin/cache/keys")]
    public async Task<IActionResult> QueryKeysAsync(
        [FromQuery] int db = 0,
        [FromQuery] string pattern = "*",
        [FromQuery] string? type = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default);

    [HttpGet("api/admin/cache/keys/{key}")]
    public async Task<IActionResult> GetKeyDetailAsync(string key, [FromQuery] int db = 0, CancellationToken ct = default);

    [HttpDelete("api/admin/cache/keys/{key}")]
    public async Task<IActionResult> DeleteKeyAsync(string key, [FromQuery] int db = 0, CancellationToken ct = default);
}
```

**关键校验**：
- `db` 必须在 0-15 范围
- `pattern` 防注入：Redis SCAN pattern 不支持命令注入，但仍需长度限制（≤ 256 字符）与字符白名单（仅允许 `*?[a-zA-Z0-9_:.-`）
- `key` URL 解码后需校验长度 ≤ 1024

**危险操作标记**：DELETE 操作需在响应中返回 `{deleted: true, key: "..."}`，便于审计日志记录。

### 4.5 ServerMonitorController（2 Endpoints）

```csharp
[Authorize(Roles = "Admin")]
[ApiController]
public sealed class ServerMonitorController : SystemAdminControllerBase
{
    [HttpGet("api/admin/server-monitor/snapshot")]
    public async Task<IActionResult> GetSnapshotAsync(CancellationToken ct);

    [HttpGet("api/admin/server-monitor/history")]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] string metric,        // cpu | memory | disk-io
        [FromQuery] int rangeSeconds = 300,
        CancellationToken ct = default);
}
```

**metric 参数校验**：枚举 `cpu` / `memory` / `disk-io`，其他值返回 400。

**rangeSeconds 校验**：1-3600（最大 1 小时），默认 300（5 分钟）。

### 4.6 统一响应信封

所有 endpoint 返回 `ApiResponse<T>`（[Leno.SharedContracts.Responses](file:///workspace/src/BuildingBlocks/Leno.SharedContracts)），与现有 17 个 Controller 一致：

```csharp
return Ok(ApiResponse.Success(result));        // 成功
return NotFound(ApiResponse.Fail(404, "菜单不存在"));
return BadRequest(ApiResponse.Fail(400, "参数错误"));
```

CSV 导出例外，返回 `FileResult`。

### 4.7 错误处理矩阵

| 场景 | HTTP 状态码 | ApiResponse.Code | 异常类型 |
|---|---|---|---|
| 菜单不存在 | 404 | 40400 | `NotFoundException` |
| 删除带子菜单的目录 | 400 | 40001 | `BusinessException` |
| 菜单校验失败（Name 为空等） | 400 | 40002 | `SystemAdminDomainException` |
| 强制下线自己 | 403 | 40003 | `ForbiddenException` |
| 在线用户会话不存在 | 404 | 40400 | `NotFoundException` |
| 缓存 key 不存在 | 404 | 40400 | `NotFoundException` |
| 缓存 db 越界（0-15） | 400 | 40002 | `ArgumentException` |
| 服务器监控 metric 参数非法 | 400 | 40002 | `ArgumentException` |
| Redis 连接失败 | 503 | 50300 | `ServiceUnavailableException` |
| 未认证 | 401 | 40100 | `UnauthorizedAccessException` |

**异常映射**：复用项目现有的 [全局异常中间件](file:///workspace/src/BuildingBlocks/Leno.Infrastructure)（`GlobalExceptionMiddleware`），无需新增。新增的异常类型需在中间件中注册映射：

```csharp
// Leno.Infrastructure/GlobalExceptionMiddleware.cs (扩展)
else if (exception is NotFoundException nfe)
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    return new ApiResponse { Code = 40400, Message = nfe.Message };
else if (exception is ForbiddenException fe)
    context.Response.StatusCode = StatusCodes.Status403Forbidden;
    return new ApiResponse { Code = 40003, Message = fe.Message };
else if (exception is ServiceUnavailableException sue)
    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    return new ApiResponse { Code = 50300, Message = sue.Message };
```

**新增异常类型**（`Leno.SystemAdmin.Domain.Exceptions`）：

```csharp
public sealed class NotFoundException : SystemAdminDomainException { /* ... */ }
public sealed class ForbiddenException : SystemAdminDomainException { /* ... */ }
public sealed class ServiceUnavailableException : SystemAdminDomainException { /* ... */ }
```

**Redis 连接失败的优雅降级**：
- `CacheController` 所有 endpoint 在 Redis 不可用时返回 503，不抛 500
- `OnlineUsersController` 在 Redis 不可用时返回空列表 + `total: 0`，不阻塞页面渲染
- `ServerMonitorController` 不依赖 Redis，永远可用

### 4.8 审计日志集成

所有写操作自动记录到 [AuditLog](file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Domain/Aggregates/AuditLog.cs) 聚合根：

| 操作 | Action | ResourceType |
|---|---|---|
| 创建菜单 | `menu.create` | `Menu` |
| 更新菜单 | `menu.update` | `Menu` |
| 删除菜单 | `menu.delete` | `Menu` |
| 菜单排序 | `menu.sort` | `Menu` |
| 强制下线 | `online-user.force-offline` | `OnlineUserSession` |
| 删除缓存 key | `cache.delete-key` | `RedisKey` |

通过 `[AuditLog]` Action Filter（项目已实装）自动捕获，无需业务代码显式写入。

### 4.9 OpenAPI 文档

5 个 Controller 自动出现在 Swagger UI（项目已启用 OpenAPI），无需额外配置。每个 endpoint 添加 `[ProducesResponseType]` 与 `[Summary]` XML 注释，对齐现有 Controller 风格。

## 5 基础设施实现与迁移

### 5.1 EF Core 迁移

**新增迁移文件**：`20260727100000_AddP0SystemAdminFeatures.cs`

**新增表**：

```sql
-- 菜单表（树形，邻接表模型）
CREATE TABLE [systemadmin].[Menus] (
    [Id]          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [ParentId]    UNIQUEIDENTIFIER NULL,
    [Name]        NVARCHAR(32)    NOT NULL,
    [Type]        TINYINT         NOT NULL,    -- 1=Directory 2=Menu 3=Button
    [Path]        NVARCHAR(256)   NULL,
    [Component]   NVARCHAR(256)   NULL,
    [Icon]        NVARCHAR(64)    NULL,
    [Sort]        INT             NOT NULL DEFAULT 0,
    [Permission]  NVARCHAR(64)    NULL,
    [Roles]       NVARCHAR(256)   NOT NULL,    -- JSON 数组 ["Admin","Operator"]
    [Visible]     BIT             NOT NULL DEFAULT 1,
    [Cache]       BIT             NOT NULL DEFAULT 0,
    [CreatedAt]   DATETIME2(7)    NOT NULL,
    [UpdatedAt]   DATETIME2(7)    NOT NULL,
    CONSTRAINT [FK_Menus_Parent_Menus] FOREIGN KEY ([ParentId]) REFERENCES [systemadmin].[Menus]([Id])
);
CREATE INDEX [IX_Menus_ParentId] ON [systemadmin].[Menus]([ParentId]);
CREATE INDEX [IX_Menus_Type_Visible] ON [systemadmin].[Menus]([Type], [Visible]);

-- 登录日志表（仅追加，按时间分区索引）
CREATE TABLE [systemadmin].[LoginLogs] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [Username]           NVARCHAR(64)    NOT NULL,
    [UserId]             UNIQUEIDENTIFIER NULL,
    [IpAddress]          NVARCHAR(64)    NOT NULL,
    [GeoLocation]        NVARCHAR(128)   NULL,
    [Browser]            NVARCHAR(64)    NOT NULL,
    [Os]                 NVARCHAR(64)    NOT NULL,
    [Result]             TINYINT         NOT NULL,    -- 1=Success 2=Failed
    [FailureReason]      NVARCHAR(64)    NULL,
    [DurationMs]         INT             NOT NULL,
    [UserAgent]          NVARCHAR(512)   NOT NULL,
    [DeviceFingerprint]  NVARCHAR(128)   NULL,
    [RefererUrl]         NVARCHAR(512)   NULL,
    [TraceId]            NVARCHAR(64)    NOT NULL,
    [LoginAt]            DATETIME2(7)    NOT NULL
);
CREATE INDEX [IX_LoginLogs_LoginAt] ON [systemadmin].[LoginLogs]([LoginAt] DESC);
CREATE INDEX [IX_LoginLogs_Username_LoginAt] ON [systemadmin].[LoginLogs]([Username], [LoginAt] DESC);
CREATE INDEX [IX_LoginLogs_Result_LoginAt] ON [systemadmin].[LoginLogs]([Result], [LoginAt] DESC);
```

**注意**：OnlineUserSession 不建表，仅存 Redis。

### 5.2 EF Core 配置文件

**位置**：`Leno.SystemAdmin.Infrastructure/Configurations/`

```csharp
// MenuConfiguration.cs
public sealed class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> builder)
    {
        builder.ToTable("Menus", "systemadmin");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).HasMaxLength(32).IsRequired();
        builder.Property(m => m.Type).HasConversion<byte>();
        builder.Property(m => m.Path).HasMaxLength(256);
        builder.Property(m => m.Component).HasMaxLength(256);
        builder.Property(m => m.Icon).HasMaxLength(64);
        builder.Property(m => m.Permission).HasMaxLength(64);
        builder.Property(m => m.Roles)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasMaxLength(256);
        builder.Property(m => m.Sort).HasDefaultValue(0);
        builder.Property(m => m.Visible).HasDefaultValue(true);
        builder.Property(m => m.Cache).HasDefaultValue(false);
        builder.HasIndex(m => m.ParentId);
        builder.HasIndex(m => new { m.Type, m.Visible });
    }
}

// LoginLogConfiguration.cs
public sealed class LoginLogConfiguration : IEntityTypeConfiguration<LoginLog>
{
    public void Configure(EntityTypeBuilder<LoginLog> builder)
    {
        builder.ToTable("LoginLogs", "systemadmin");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Username).HasMaxLength(64).IsRequired();
        builder.Property(l => l.IpAddress).HasMaxLength(64).IsRequired();
        builder.Property(l => l.GeoLocation).HasMaxLength(128);
        builder.Property(l => l.Browser).HasMaxLength(64).IsRequired();
        builder.Property(l => l.Os).HasMaxLength(64).IsRequired();
        builder.Property(l => l.Result).HasConversion<byte>();
        builder.Property(l => l.FailureReason).HasMaxLength(64);
        builder.Property(l => l.UserAgent).HasMaxLength(512).IsRequired();
        builder.Property(l => l.DeviceFingerprint).HasMaxLength(128);
        builder.Property(l => l.RefererUrl).HasMaxLength(512);
        builder.Property(l => l.TraceId).HasMaxLength(64).IsRequired();
        builder.HasIndex(l => l.LoginAt).IsDescending();
        builder.HasIndex(l => new { l.Username, l.LoginAt }).IsDescending();
        builder.HasIndex(l => new { l.Result, l.LoginAt }).IsDescending();
    }
}
```

### 5.3 DbContext 扩展

[SystemAdminDbContext](file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/SystemAdminDbContext.cs) 新增 2 个 DbSet：

```csharp
public DbSet<Menu> Menus => Set<Menu>();
public DbSet<LoginLog> LoginLogs => Set<LoginLog>();
```

**`OnlineUserSession` 不加入 DbContext**，避免 EF Core 跟踪 Redis 投影。

### 5.4 仓储实现

**位置**：`Leno.SystemAdmin.Infrastructure/Repositories/`

```csharp
// EfCoreMenuRepository.cs
public sealed class EfCoreMenuRepository : IMenuRepository
{
    private readonly SystemAdminDbContext _db;

    public EfCoreMenuRepository(SystemAdminDbContext db) { _db = db; }

    public Task<Menu?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Menus.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<List<Menu>> GetAllAsync(CancellationToken ct)
        => _db.Menus.AsNoTracking().OrderBy(m => m.Sort).ToListAsync(ct);

    public Task<List<Menu>> GetChildrenAsync(Guid parentId, CancellationToken ct)
        => _db.Menus.AsNoTracking().Where(m => m.ParentId == parentId).OrderBy(m => m.Sort).ToListAsync(ct);

    public Task<Menu?> GetByPathAsync(string path, CancellationToken ct)
        => _db.Menus.AsNoTracking().FirstOrDefaultAsync(m => m.Path == path, ct);

    public async Task AddAsync(Menu menu, CancellationToken ct)
    {
        await _db.Menus.AddAsync(menu, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Menu menu, CancellationToken ct)
    {
        _db.Menus.Update(menu);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        // 递归收集子节点（应用层递归）
        var toDelete = await CollectSubtreeAsync(id, ct);
        _db.Menus.RemoveRange(toDelete);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> CountChildrenAsync(Guid parentId, CancellationToken ct)
        => await _db.Menus.CountAsync(m => m.ParentId == parentId, ct);

    public async Task<List<Menu>> GetByRoleAsync(string role, CancellationToken ct)
    {
        // 菜单数量 ≤ 100，全量载入后应用层过滤，避免 SQL Server 低版本 JSON 查询子串误匹配
        // （如 LIKE '%Admin%' 会误匹配 "SuperAdmin"）
        var all = await _db.Menus.AsNoTracking().OrderBy(m => m.Sort).ToListAsync(ct);
        return all.Where(m => m.Roles.Contains(role)).ToList();
    }

    private async Task<List<Menu>> CollectSubtreeAsync(Guid rootId, CancellationToken ct)
    {
        var result = new List<Menu>();
        var queue = new Queue<Guid>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = await _db.Menus.Where(m => m.ParentId == current).ToListAsync(ct);
            foreach (var child in children)
            {
                result.Add(child);
                queue.Enqueue(child.Id);
            }
        }
        result.Add(await _db.Menus.FirstAsync(m => m.Id == rootId, ct));
        return result;
    }
}
```

**Roles JSON 查询说明**：`GetByRoleAsync` 采用应用层过滤（全量载入 + `List.Contains`），避免 SQL Server 低版本 `LIKE '%Admin%'` 子串误匹配（"Admin" 误匹配 "SuperAdmin"）。菜单总数 ≤ 100，性能可接受。

### 5.5 Redis 会话存储实现

**位置**：`Leno.SystemAdmin.Infrastructure/Services/RedisUserSessionStore.cs`

```csharp
public sealed class RedisUserSessionStore : IUserSessionStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _sessionTtl = TimeSpan.FromHours(24);

    public async Task RecordAsync(OnlineUserSession session, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var batch = db.CreateBatch();

        // 1. 会话详情 Hash
        var sessionKey = $"session:{session.SessionId}";
        var entries = new List<HashEntry>
        {
            new("userId", session.UserId.ToString()),
            new("username", session.Username),
            new("roles", JsonSerializer.Serialize(session.Roles)),
            new("ipAddress", session.IpAddress),
            new("geoLocation", session.GeoLocation ?? ""),
            new("browser", session.Browser),
            new("os", session.Os),
            new("tokenPreview", session.TokenPreview),
            new("deviceFingerprint", session.DeviceFingerprint ?? ""),
            new("requestCount", session.RequestCount.ToString()),
            new("loginAt", session.LoginAt.ToString("O")),
            new("lastActivityAt", session.LastActivityAt.ToString("O")),
            new("isAnomaly", session.IsAnomaly.ToString()),
        };
        _ = batch.HashSetAsync(sessionKey, entries.ToArray());
        _ = batch.KeyExpireAsync(sessionKey, _sessionTtl);

        // 2. 用户会话索引 Set
        _ = batch.SetAddAsync($"session:user:{session.UserId}", session.SessionId);
        _ = batch.KeyExpireAsync($"session:user:{session.UserId}", _sessionTtl);

        // 3. 全局会话时间索引 ZSet
        _ = batch.SortedSetAddAsync("session:index", session.SessionId, new DateTimeOffset(session.LoginAt).ToUnixTimeSeconds());

        batch.Execute();
    }

    public async Task<List<OnlineUserSession>> QueryAsync(OnlineUserQuery query, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        // 按 ZSet 范围查询（最近优先），再 HGETALL 批量取详情
        var fromTs = query.LoginAtFrom.HasValue ? new DateTimeOffset(query.LoginAtFrom.Value).ToUnixTimeSeconds() : 0;
        var toTs = query.LoginAtTo.HasValue ? new DateTimeOffset(query.LoginAtTo.Value).ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sessionIds = await db.SortedSetRangeByScoreAsync("session:index", fromTs, toTs);
        var skip = (query.Page - 1) * query.PageSize;
        var take = query.PageSize;
        var pagedIds = sessionIds.Skip(skip).Take(take).ToArray();

        var sessions = new List<OnlineUserSession>();
        foreach (var sid in pagedIds)
        {
            var hash = await db.HashGetAllAsync($"session:{sid}");
            if (hash.Length == 0) continue;  // 已过期
            sessions.Add(MapFromHash(sid.ToString(), hash));
        }
        // 应用层过滤 username / ipAddress
        return sessions.Where(s => string.IsNullOrEmpty(query.Username) || s.Username.Contains(query.Username))
                       .Where(s => string.IsNullOrEmpty(query.IpAddress) || s.IpAddress.Contains(query.IpAddress))
                       .ToList();
    }

    public async Task<OnlineUserStats> GetStatsAsync(CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var total = await db.SortedSetLengthAsync("session:index");
        var since24h = DateTimeOffset.UtcNow.AddHours(-24).ToUnixTimeSeconds();
        var logins24h = await db.SortedSetLengthAsync("session:index", since24h);
        // 异常会话数：遍历所有会话统计 IsAnomaly=true（会话数 ≤ 千级，可接受）
        var sessionIds = await db.SortedSetRangeByScoreAsync("session:index");
        int anomalies = 0;
        foreach (var sid in sessionIds)
        {
            var isAnomaly = (string?)await db.HashGetAsync($"session:{sid}", "isAnomaly");
            if (isAnomaly == "True") anomalies++;
        }
        return new OnlineUserStats { Total = (int)total, Logins24h = (int)logins24h, Anomalies = anomalies };
    }

    public async Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var userIdStr = (string?)await db.HashGetAsync($"session:{sessionId}", "userId");
        var batch = db.CreateBatch();
        _ = batch.KeyDeleteAsync($"session:{sessionId}");
        if (Guid.TryParse(userIdStr, out var userId))
        {
            _ = batch.SetRemoveAsync($"session:user:{userId}", sessionId);
        }
        _ = batch.SortedSetRemoveAsync("session:index", sessionId);
        batch.Execute();
    }
}
```

### 5.6 Redis 缓存监控实现

**位置**：`Leno.SystemAdmin.Infrastructure/Services/RedisCacheMonitorService.cs`

```csharp
public sealed class RedisCacheMonitorService : IRedisCacheMonitor
{
    private readonly IConnectionMultiplexer _redis;

    public async Task<RedisInfoDto> GetInfoAsync(CancellationToken ct = default)
    {
        var endpoint = _redis.GetEndPoints()[0];
        var server = _redis.GetServer(endpoint);
        var infoSections = await server.InfoAsync(ct);  // 全部分组

        var serverSection = infoSections.FirstOrDefault(s => s.Key == "Server");
        var memorySection = infoSections.FirstOrDefault(s => s.Key == "Memory");
        var clientsSection = infoSections.FirstOrDefault(s => s.Key == "Clients");
        var statsSection = infoSections.FirstOrDefault(s => s.Key == "Stats");

        return new RedisInfoDto
        {
            RedisVersion = GetInfoValue(serverSection, "redis_version"),
            RedisMode = GetInfoValue(serverSection, "redis_mode"),
            Os = GetInfoValue(serverSection, "os"),
            ArchBits = GetInfoValue(serverSection, "arch_bits"),
            TcpPort = int.Parse(GetInfoValue(serverSection, "tcp_port")),
            UptimeInDays = int.Parse(GetInfoValue(serverSection, "uptime_in_days")),
            ConnectedClients = int.Parse(GetInfoValue(clientsSection, "connected_clients")),
            UsedMemoryHuman = GetInfoValue(memorySection, "used_memory_human"),
            UsedMemoryPeakHuman = GetInfoValue(memorySection, "used_memory_peak_human"),
            MaxmemoryHuman = GetInfoValue(memorySection, "maxmemory_human"),
            MemFragmentationRatio = double.Parse(GetInfoValue(memorySection, "mem_fragmentation_ratio")),
            TotalConnectionsReceived = long.Parse(GetInfoValue(statsSection, "total_connections_received")),
            TotalCommandsProcessed = long.Parse(GetInfoValue(statsSection, "total_commands_processed")),
            KeyspaceHits = long.Parse(GetInfoValue(statsSection, "keyspace_hits")),
            KeyspaceMisses = long.Parse(GetInfoValue(statsSection, "keyspace_misses")),
            EvictedKeys = long.Parse(GetInfoValue(statsSection, "evicted_keys")),
        };
    }

    public async Task<List<KeyspaceDto>> GetKeyspacesAsync(CancellationToken ct = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints()[0]);
        var keyspaceSection = (await server.InfoAsync("keyspace", ct)).FirstOrDefault();
        var result = new List<KeyspaceDto>();
        for (int db = 0; db <= 15; db++)
        {
            var line = GetInfoValue(keyspaceSection, $"db{db}");
            if (string.IsNullOrEmpty(line))
            {
                result.Add(new KeyspaceDto { Db = db, Keys = 0, Expires = 0, AvgTtl = 0 });
                continue;
            }
            // 解析 "keys=1243,expires=120,avg_ttl=3600000"
            var parts = line.Split(',');
            result.Add(new KeyspaceDto
            {
                Db = db,
                Keys = int.Parse(parts[0].Split('=')[1]),
                Expires = int.Parse(parts[1].Split('=')[1]),
                AvgTtl = int.Parse(parts[2].Split('=')[1]),
            });
        }
        return result;
    }

    public async Task<PagedResult<RedisKeyDto>> ScanKeysAsync(int db, string pattern, string? type, int page, int pageSize, CancellationToken ct = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints()[0]);
        var redisDb = _redis.GetDatabase(db);
        var keys = new List<RedisKey>();
        // IServer.Keys 内部使用 SCAN，不阻塞
        await foreach (var key in server.KeysAsync(database: db, pattern: pattern, pageSize: pageSize * 5).WithCancellation(ct))
        {
            if (keys.Count >= pageSize * 5) break;
            keys.Add(key);
        }

        var filtered = new List<RedisKeyDto>();
        foreach (var key in keys)
        {
            var keyType = type == null ? null : await redisDb.KeyTypeAsync(key);
            if (type != null && !string.Equals(keyType.ToString(), type, StringComparison.OrdinalIgnoreCase))
                continue;
            var ttl = await redisDb.KeyTimeToLiveAsync(key);
            var size = await GetKeySizeAsync(redisDb, key, keyType?.ToString() ?? "string");
            filtered.Add(new RedisKeyDto
            {
                Key = key.ToString(),
                Type = (keyType?.ToString() ?? "string").ToLowerInvariant(),
                Size = size,
                Ttl = ttl?.Seconds ?? -1,
            });
        }
        var total = filtered.Count;
        var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<RedisKeyDto> { Items = paged, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<bool> DeleteKeyAsync(string key, int db, CancellationToken ct = default)
    {
        var redisDb = _redis.GetDatabase(db);
        return await redisDb.KeyDeleteAsync(key);
    }

    private static string GetInfoValue(IGrouping<string, KeyValuePair<string, string>>? section, string key)
        => section?.FirstOrDefault(p => p.Key == key).Value ?? string.Empty;

    private static async Task<int> GetKeySizeAsync(IDatabase db, RedisKey key, string type)
        => type switch
        {
            "string" => (await db.StringLengthAsync(key)),
            "hash" => (int)(await db.HashLengthAsync(key)),
            "list" => (int)(await db.ListLengthAsync(key)),
            "set" => (int)(await db.SetLengthAsync(key)),
            "zset" => (int)(await db.SortedSetLengthAsync(key)),
            _ => 0,
        };
}
```

### 5.7 .NET 进程监控实现

**位置**：`Leno.SystemAdmin.Infrastructure/Services/DotNetProcessMonitorService.cs`

```csharp
public sealed class DotNetProcessMonitorService : IDotNetProcessMonitor
{
    private readonly Process _currentProcess = Process.GetCurrentProcess();
    private DateTime _lastCpuSample = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;
    private readonly object _cpuLock = new();

    public Task<ServerSnapshotDto> GetSnapshotAsync(CancellationToken ct = default)
    {
        var snapshot = BuildSnapshot();
        return Task.FromResult(snapshot);
    }

    private ServerSnapshotDto BuildSnapshot()
    {
        lock (_cpuLock)
        {
            var now = DateTime.UtcNow;
            var totalProcessorTime = _currentProcess.TotalProcessorTime;
            var cpuUsagePercent = CalculateCpuUsage(now, totalProcessorTime);
            _lastCpuSample = now;
            _lastTotalProcessorTime = totalProcessorTime;

            var memUsedBytes = _currentProcess.WorkingSet64;
            var memoryTotalBytes = GetTotalPhysicalMemory();
            var memoryCachedBytes = GC.GetGCMemoryInfo().HeapSizeBytes;

            var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToArray();
            var diskTotalBytes = drives.Sum(d => d.TotalSize);
            var diskUsedBytes = drives.Sum(d => d.TotalSize - d.AvailableFreeSpace);

            var loadAvg = GetLoadAverage();

            return new ServerSnapshotDto
            {
                Hostname = Environment.MachineName,
                Os = RuntimeInformation.OSDescription,
                KernelVersion = Environment.OSVersion.Version.ToString(),
                CpuModel = GetCpuModel(),
                CpuCores = Environment.ProcessorCount,
                CpuUsagePercent = cpuUsagePercent,
                MemoryTotalBytes = memoryTotalBytes,
                MemoryUsedBytes = memUsedBytes,
                MemoryCachedBytes = memoryCachedBytes,
                DiskTotalBytes = diskTotalBytes,
                DiskUsedBytes = diskUsedBytes,
                DiskReadBytesPerSec = 0,  // 需 PerformanceCounter，Linux 上简化为 0
                DiskWriteBytesPerSec = 0,
                LoadAvg1 = loadAvg.avg1,
                LoadAvg5 = loadAvg.avg5,
                LoadAvg15 = loadAvg.avg15,
                ProcessCount = Process.GetProcesses().Length,
                UptimeSeconds = (int)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds,
                BootTime = Process.GetCurrentProcess().StartTime.ToUniversalTime().ToString("O"),
                DotnetRuntimeVersion = RuntimeInformation.FrameworkDescription,
                GcTotalCollections = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2),
                SampledAt = DateTime.UtcNow.ToString("O"),
            };
        }
    }

    private double CalculateCpuUsage(DateTime now, TimeSpan totalProcessorTime)
    {
        var elapsed = now - _lastCpuSample;
        var cpuElapsed = totalProcessorTime - _lastTotalProcessorTime;
        if (elapsed.TotalSeconds <= 0) return 0;
        // CPU 使用率 = (进程 CPU 时间增量 / (经过时间 * 核心数)) * 100
        return Math.Min(100, Math.Max(0, cpuElapsed.TotalSeconds / (elapsed.TotalSeconds * Environment.ProcessorCount) * 100));
    }

    private static long GetTotalPhysicalMemory()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return 0;
        var lines = File.ReadAllLines("/proc/meminfo");
        var memTotalLine = lines.FirstOrDefault(l => l.StartsWith("MemTotal:"));
        if (memTotalLine != null && long.TryParse(memTotalLine.Split(':')[1].Trim().Split(' ')[0], out var kb))
            return kb * 1024;
        return 0;
    }

    private static (double avg1, double avg5, double avg15) GetLoadAverage()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return (0, 0, 0);
        var lines = File.ReadAllLines("/proc/loadavg");
        var parts = lines[0].Split(' ');
        if (parts.Length >= 3
            && double.TryParse(parts[0], out var avg1)
            && double.TryParse(parts[1], out var avg5)
            && double.TryParse(parts[2], out var avg15))
        {
            return (avg1, avg5, avg15);
        }
        return (0, 0, 0);
    }

    private static string GetCpuModel()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var lines = File.ReadAllLines("/proc/cpuinfo");
            var modelLine = lines.FirstOrDefault(l => l.StartsWith("model name"));
            return modelLine?.Split(':')[1].Trim() ?? "Unknown";
        }
        return RuntimeInformation.OSArchitecture.ToString();
    }
}
```

### 5.8 后台采样服务

**位置**：`Leno.SystemAdmin.Infrastructure/BackgroundServices/ServerMetricSamplerBackgroundService.cs`

```csharp
public sealed class ServerMetricSamplerBackgroundService : BackgroundService
{
    private readonly IDotNetProcessMonitor _monitor;
    private readonly IMetricHistoryStore _historyStore;
    private readonly TimeSpan _sampleInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _monitor.GetSnapshotAsync(stoppingToken);
                await _historyStore.RecordAsync(MetricName.Cpu, snapshot.CpuUsagePercent, stoppingToken);
                await _historyStore.RecordAsync(MetricName.Memory, snapshot.MemoryTotalBytes > 0
                    ? snapshot.MemoryUsedBytes / (double)snapshot.MemoryTotalBytes * 100
                    : 0, stoppingToken);
                await _historyStore.RecordAsync(MetricName.DiskIo, snapshot.DiskReadBytesPerSec + snapshot.DiskWriteBytesPerSec, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 采样失败不阻塞后台服务，下次循环继续
            }
            await Task.Delay(_sampleInterval, stoppingToken);
        }
    }
}
```

**`IMetricHistoryStore` 实现**：`MemoryMetricHistoryStore`，内部用 `System.Threading.Channels` 维护 3 个滚动缓冲区（每个 300 点）。

### 5.9 DI 注册扩展

[ServiceCollectionExtensions](file:///workspace/src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure/Dependencies/ServiceCollectionExtensions.cs) `AddSystemAdminInfrastructure` 末尾追加：

```csharp
// P0 新增：菜单、登录日志、在线用户、缓存监控、服务器监控
services.AddScoped<IMenuRepository, EfCoreMenuRepository>();
services.AddScoped<ILoginLogRepository, EfCoreLoginLogRepository>();

services.AddScoped<IMenuAppService, MenuAppService>();
services.AddScoped<ILoginLogAppService, LoginLogAppService>();
services.AddScoped<IOnlineUserAppService, OnlineUserAppService>();
services.AddScoped<ICacheMonitorAppService, CacheMonitorAppService>();
services.AddScoped<IServerMonitorAppService, ServerMonitorAppService>();

// Redis 抽象实现：复用主 Redis 连接（已在 AddLenoApi 中注册）
services.AddSingleton<IUserSessionStore, RedisUserSessionStore>();
services.AddSingleton<IRedisCacheMonitor, RedisCacheMonitorService>();

// 进程监控
services.AddSingleton<IDotNetProcessMonitor, DotNetProcessMonitorService>();
services.AddSingleton<IMetricHistoryStore, MemoryMetricHistoryStore>();
services.AddHostedService<ServerMetricSamplerBackgroundService>();

// UA 解析与地理定位
services.AddSingleton<IUserAgentParser, UAParserUserAgentParser>();
services.AddSingleton<IGeoLocationResolver, MaxMindGeoLocationResolver>();
```

### 5.10 Identity 改动点（最小侵入）

[AuthAppService](file:///workspace/src/Services/Identity/Leno.Identity.Application) 注入三个新依赖：

```csharp
public sealed class AuthAppService : IAuthAppService
{
    private readonly IUserSessionStore _userSessionStore;        // 新增
    private readonly IUserAgentParser _uaParser;                 // 新增（共享实现）
    private readonly IPublishEndpoint _publishEndpoint;          // 已有

    public async Task<LoginResultDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        // ... 现有登录逻辑 ...
        var session = BuildSession(user, jti, httpContext, userAgent, _uaParser);
        await _userSessionStore.RecordAsync(session, ct);

        await _publishEndpoint.Publish(new UserLoggedInEvent { /* ... */ }, ct);
        return jwt;
    }
}
```

**Identity 项目需引用**：`Leno.Infrastructure.Abstractions`（已引用）。`IUserAgentParser` 抽象与 `UAParserUserAgentParser` 实现放在 `Leno.Infrastructure.Abstractions` 与 `Leno.Infrastructure` 中（共享给 Identity 与 SystemAdmin）。Identity 的 `Program.cs` 注册 `services.AddSingleton<IUserAgentParser, UAParserUserAgentParser>();`。

### 5.11 appsettings.json 新增配置

```json
{
  "ConnectionStrings": {
    "SystemAdminDb": "..."
  },
  "Redis": {
    "Configuration": "...",
    "InstanceName": "leno:"
  },
  "P0Features": {
    "UserSession": {
      "SessionTtlHours": 24,
      "MaxSessionsPerUser": 5
    },
    "ServerMonitor": {
      "SampleIntervalSeconds": 1,
      "HistoryMaxPoints": 300
    },
    "GeoLocation": {
      "MaxMindDbPath": "/var/lib/leno/GeoLite2-City.mmdb",
      "LicenseKey": ""
    }
  }
}
```

### 5.12 迁移与回滚

**应用顺序**（启动时 `MigrateWithLockAsync` 自动执行）：
1. EF Core 迁移 `AddP0SystemAdminFeatures`
2. DI 注册新服务（无破坏性）
3. Identity 改动（仅新增 2 行同步调用）

**回滚策略**：
- 关闭 P0 功能：注释 5 个 Controller 注册（不影响其他 17 个）
- 回滚 EF 迁移：`dotnet ef migrations revert AddP0SystemAdminFeatures`
- 回滚 Identity 改动：删除 `IUserSessionStore.RecordAsync` 调用（Redis 中残留数据自动过期）

## 6 测试策略

### 6.1 测试分层

| 层级 | 工具 | 范围 | 覆盖目标 |
|---|---|---|---|
| 领域单测 | xUnit | 2 个聚合根不变量（Menu/LoginLog） | 100% 路径 |
| 应用单测 | xUnit + Moq | 5 个 AppService 业务逻辑 | 关键路径 100% |
| 仓储集成测试 | xUnit + EF Core SQLite | 2 个仓储（Menu/LoginLog） | CRUD 100% |
| 基础设施集成测试 | xUnit + Testcontainers | Redis 会话/缓存监控 | 关键路径 100% |
| Controller 集成测试 | xUnit + WebApplicationFactory | 5 个 Controller / 19 Endpoint | 100% Endpoint |
| 跨域事件消费测试 | xUnit + MassTransit Test Harness | LoginLogConsumer | 100% |
| 端到端冒烟 | xUnit | 登录→会话写入→日志落库→查询 | 主链路 |

### 6.2 领域单测清单

**`Leno.SystemAdmin.Domain.Tests/MenuTests.cs`**：

| 用例 | 断言 |
|---|---|
| `CreateRoot_ValidParams_BuildsDirectory` | ParentId=null；Id 非空；Type=Directory |
| `CreateChild_WithParentId_BuildsMenuNode` | ParentId=parent.Id；Sort 默认 0 |
| `CreateMenu_WithoutComponent_ThrowsDomainException` | 异常消息含 "Component" |
| `CreateButton_WithPath_ThrowsDomainException` | Button 类型 Path 必须为 null |
| `CreateMenu_NameEmpty_ThrowsDomainException` | Name 长度 < 1 或 > 32 抛异常 |
| `CreateMenu_SortNegative_ThrowsDomainException` | Sort < 0 抛异常 |
| `Rename_ChangesName_UpdatedAtBumps` | Name 更新；UpdatedAt > 原 CreatedAt |
| `ChangeSort_UpdatesSortField` | Sort 字段同步更新 |
| `MoveTo_NewParentId_UpdatesParentId` | ParentId 变更 |
| `ToggleVisible_FalseToTrue` | Visible 字段翻转 |

**`Leno.SystemAdmin.Domain.Tests/LoginLogTests.cs`**：

| 用例 | 断言 |
|---|---|
| `CreateSuccess_FailureReasonNull_ResultSuccessAndReasonNull` | Result=Success；FailureReason=null |
| `CreateFailed_WithReason_ResultFailedAndReasonSet` | Result=Failed；FailureReason 非空 |
| `CreateSuccess_WithFailureReason_ThrowsDomainException` | 校验失败 |
| `CreateFailed_WithoutFailureReason_ThrowsDomainException` | 校验失败 |
| `CreateSuccess_UserIdSet` | UserId 非空 |
| `CreateFailed_UserIdNull` | UserId 为 null |

**`OnlineUserSession` 投影不设领域不变量**（仅 DTO 校验），无单测。

### 6.3 应用服务单测清单

**`MenuAppServiceTests.cs`**：

| 用例 | Mock | 断言 |
|---|---|---|
| `GetTreeAsync_ReturnsHierarchicalList` | repo.GetAllAsync 返回扁平 7 条 | 应用层组装为 1 根 6 子节点 |
| `CreateAsync_ValidDto_CallsRepoAddAsyncOnce` | repo 正常 | AddAsync 调用 1 次；返回 MenuDto |
| `CreateAsync_DuplicatePath_ThrowsBusinessException` | repo.GetByPathAsync 返回已有 | 抛异常 code 40001 |
| `UpdateAsync_MenuNotFound_ThrowsNotFoundException` | repo.GetByIdAsync 返回 null | 抛 NotFoundException |
| `DeleteAsync_WithChildren_ThrowsBusinessException` | repo.CountChildrenAsync 返回 2 | 抛异常 code 40001 |
| `DeleteAsync_NoChildren_CallsRepoDeleteAsync` | repo.CountChildrenAsync 返回 0 | DeleteAsync 调用 1 次 |
| `SortAsync_ReordersAllItems` | repo 正常 | UpdateAsync 调用 N 次（N=items.Count） |

**`LoginLogAppServiceTests.cs`**：

| 用例 | 断言 |
|---|---|
| `QueryAsync_WithFilters_PassesQueryToRepo` | query.Username="admin"；repo.QueryAsync 接收正确 query |
| `QueryAsync_Pagination_ReturnsCorrectPage` | page=2, pageSize=10；返回 Total 与 Page |
| `GetByIdAsync_NotFound_ReturnsNull` | repo.GetByIdAsync 返回 null |
| `ExportAsync_BuildsCsvWithHeader` | CSV 首行为表头；行数=数据条数 |
| `ExportAsync_StreamLimit_StopsAt100000` | repo.StreamAsync 返回 100001 条；CSV 行数=100000 |

**`OnlineUserAppServiceTests.cs`**：

| 用例 | Mock | 断言 |
|---|---|---|
| `QueryAsync_DerivesSessionDurationMs` | store 返回 LoginAt=1h 前 | SessionDurationMs ≈ 3600000 |
| `QueryAsync_FiltersByUsername` | store 返回 3 条 | 按 username 过滤后剩 1 条 |
| `GetStatsAsync_ReturnsThreeMetrics` | store 返回 total=5, logins24h=3, anomalies=1 | DTO 字段对齐 |
| `ForceOfflineAsync_SelfSession_ThrowsForbiddenException` | sessionId == CurrentUser.SessionId | 抛异常 code 40003 |
| `ForceOfflineAsync_OtherSession_CallsStoreRemoveAsync` | sessionId != CurrentUser.SessionId | RemoveAsync 调用 1 次 |
| `QueryAsync_RedisUnavailable_ReturnsEmptyList` | store 抛 RedisConnectionException | 返回空列表 + total=0 |

**`CacheMonitorAppServiceTests.cs`**：

| 用例 | 断言 |
|---|---|
| `GetRedisInfoAsync_MapsAllFields` | 16 个字段全部映射 |
| `GetKeyspacesAsync_Returns16Dbs` | db0-db15，缺数据返回零值 |
| `ScanKeysAsync_PatternMatch_FiltersByPattern` | pattern="user:*" 仅返回 user 前缀 key |
| `ScanKeysAsync_TypeFilter_FiltersByType` | type="hash" 仅返回 hash 类型 |
| `GetKeyDetailAsync_StringType_ReturnsValue` | value 字段为字符串 |
| `GetKeyDetailAsync_HashType_ReturnsDictionary` | value 字段为字典 |
| `GetKeyDetailAsync_KeyNotFound_ReturnsNull` | 不存在返回 null |
| `DeleteKeyAsync_ExistingKey_ReturnsTrue` | 删除成功 |
| `GetRedisInfoAsync_RedisUnavailable_ThrowsServiceUnavailableException` | 503 异常 |

**`ServerMonitorAppServiceTests.cs`**：

| 用例 | 断言 |
|---|---|
| `GetSnapshotAsync_ReturnsAllFields` | 21 个字段全部填充 |
| `GetSnapshotAsync_CpuUsageCalculation` | 两次调用间 CPU 增量计算正确 |
| `GetHistoryAsync_CpuMetric_Returns300Points` | 返回 300 个 MetricPointDto |
| `GetHistoryAsync_RangeFilter_ReturnsLast5Min` | 仅返回 5 分钟内的点 |
| `GetHistoryAsync_InvalidMetric_ThrowsArgumentException` | metric="invalid" 抛 400 |

### 6.4 仓储集成测试

**`EfCoreMenuRepositoryTests.cs`**（用 SQLite in-memory）：

```csharp
public sealed class EfCoreMenuRepositoryTests
{
    private static async Task<SystemAdminDbContext> BuildDbContextAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SystemAdminDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new SystemAdminDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    [Fact]
    public async Task AddAsync_PersistsMenu()
    {
        var db = await BuildDbContextAsync();
        var repo = new EfCoreMenuRepository(db);
        var menu = Menu.CreateRoot("用户管理", MenuType.Directory, "/user-access", icon: "TeamOutlined");

        await repo.AddAsync(menu, default);

        var loaded = await repo.GetByIdAsync(menu.Id, default);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("用户管理");
    }

    [Fact]
    public async Task DeleteAsync_WithSubtree_RemovesAllDescendants()
    {
        // 构建 root → child → grandchild 三层
        // 调 DeleteAsync(root.Id)
        // 断言 3 条记录全部删除
    }

    [Fact]
    public async Task CountChildrenAsync_ReturnsDirectChildCount()
    {
        // root 有 2 个直接子节点（不计算孙节点）
    }

    [Fact]
    public async Task GetByRoleAsync_AdminRole_ReturnsMatchedMenus()
    {
        // Roles=["Admin"] 的菜单返回；Roles=["Operator"] 的不返回
    }
}
```

**`EfCoreLoginLogRepositoryTests.cs`**：

| 用例 | 断言 |
|---|---|
| `AddAsync_PersistsLog` | Id 自动生成；LoginAt 写入 |
| `QueryAsync_ByUsername_FiltersCorrectly` | 仅返回匹配用户名 |
| `QueryAsync_ByResult_FiltersSuccessOnly` | Result=Success 过滤 |
| `QueryAsync_ByTimeRange_FiltersByLoginAt` | 时间窗口过滤 |
| `QueryAsync_Pagination_ReturnsCorrectPage` | Total/Page/PageSize |
| `StreamAsync_YieldsInOrder` | 按 LoginAt 降序 |

### 6.5 基础设施集成测试（Testcontainers）

**`RedisUserSessionStoreTests.cs`**（用 Testcontainers.Redis）：

```csharp
public sealed class RedisUserSessionStoreTests : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder()
        .WithImage("redis:7.2-alpine")
        .Build();

    [Fact]
    public async Task RecordAsync_WritesThreeKeys()
    {
        var store = new RedisUserSessionStore(_container.GetConnection());
        var session = BuildTestSession();

        await store.RecordAsync(session);

        var db = _container.GetDatabase();
        var sessionExists = await db.KeyExistsAsync($"session:{session.SessionId}");
        var userIndexExists = await db.KeyExistsAsync($"session:user:{session.UserId}");
        var globalIndexExists = await db.KeyExistsAsync("session:index");
        sessionExists.Should().BeTrue();
        userIndexExists.Should().BeTrue();
        globalIndexExists.Should().BeTrue();
    }

    [Fact]
    public async Task QueryAsync_ReturnsRecordedSessions()
    {
        // 写 3 条会话 → QueryAsync 返回 3 条
    }

    [Fact]
    public async Task QueryAsync_FiltersByLoginAtRange()
    {
        // 写 3 条不同时间会话 → 范围查询返回 1 条
    }

    [Fact]
    public async Task RemoveAsync_DeletesAllThreeKeys()
    {
        // 写入后删除 → 3 个 key 均不存在
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectCounts()
    {
        // 写 3 条 → total=3, logins24h=3, anomalies=0
    }

    [Fact]
    public async Task RecordAsync_SetsTtl_KeyExpiresIn24h()
    {
        // 验证 TTL 在 23-24 小时之间
    }

    public async Task InitializeAsync() => await _container.StartAsync();
    public async Task DisposeAsync() => await _container.DisposeAsync();
}
```

**`RedisCacheMonitorServiceTests.cs`**（同样用 Testcontainers.Redis）：

| 用例 | 断言 |
|---|---|
| `GetInfoAsync_ReturnsAllFields` | redis_version 等字段非空 |
| `GetKeyspacesAsync_ReturnsDb0ToDb15` | 16 条 KeyspaceDto |
| `ScanKeysAsync_PatternStar_ReturnsAllKeys` | 写 5 条后查询返回 5 条 |
| `ScanKeysAsync_PatternUserPrefix_FiltersCorrectly` | 仅返回 user:* 前缀 |
| `ScanKeysAsync_TypeFilter_HashOnly` | 仅返回 hash 类型 |
| `GetKeyDetailAsync_StringType_ReturnsValue` | value 为字符串 |
| `GetKeyDetailAsync_HashType_ReturnsDictionary` | value 为字典 |
| `DeleteKeyAsync_ExistingKey_ReturnsTrue` | 删除后 KeyExists=false |

### 6.6 Controller 集成测试

**`MenusControllerTests.cs`**（用 WebApplicationFactory）：

```csharp
public sealed class MenusControllerTests : IClassFixture<SystemAdminApiFactory>
{
    private readonly HttpClient _client;

    [Fact]
    public async Task GetTreeAsync_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/admin/menus/tree");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTreeAsync_AsAdmin_ReturnsMenuTree()
    {
        // 用 Admin JWT 调用 → 200 + ApiResponse 信封
    }

    [Fact]
    public async Task CreateAsync_ValidBody_Returns201()
    {
        // POST → 201 + MenuDto
    }

    [Fact]
    public async Task CreateAsync_DuplicatePath_Returns400WithCode40001()
    {
        // 重复 path → 400 + code 40001
    }

    [Fact]
    public async Task DeleteAsync_WithChildren_Returns400WithCode40001()
    {
        // 有子菜单 → 400 + code 40001
    }

    [Fact]
    public async Task SortAsync_ValidItems_Returns200()
    {
        // PUT /sort → 200
    }
}
```

**其余 4 个 Controller 测试同理**，每个 Controller 测试覆盖：
- 未认证 → 401
- 错误角色 → 403
- 参数校验失败 → 400
- 资源不存在 → 404
- 成功 → 200/201
- 业务规则违反 → 400 + 业务错误码

**完整清单**：

| Controller | 测试文件 | 用例数 |
|---|---|---|
| MenusController | MenusControllerTests.cs | 12 |
| OnlineUsersController | OnlineUsersControllerTests.cs | 9 |
| LoginLogsController | LoginLogsControllerTests.cs | 7 |
| CacheController | CacheControllerTests.cs | 11 |
| ServerMonitorController | ServerMonitorControllerTests.cs | 5 |

### 6.7 跨域事件消费测试

**`LoginLogConsumerTests.cs`**（用 MassTransit Test Harness）：

```csharp
public sealed class LoginLogConsumerTests
{
    [Fact]
    public async Task Consume_UserLoggedInEvent_PersistsLoginLog()
    {
        // 用 MassTransit Test Harness 发布 UserLoggedInEvent
        // 验证 LoginLogRepository.AddAsync 被调用
        // 验证 LoginLog.Username / Result=Success / LoginAt 字段正确
    }

    [Fact]
    public async Task Consume_FailedLoginEvent_PersistsWithFailureReason()
    {
        // Success=false + FailureReason="密码错误"
    }

    [Fact]
    public async Task Consume_DuplicateEventId_IdempotentSkip()
    {
        // 同一 EventId 发布两次 → 仅写入 1 条 LoginLog
    }

    [Fact]
    public async Task Consume_ParsesUserAgent_PopulatesBrowserAndOs()
    {
        // UA="Mozilla/5.0...Chrome 120..." → Browser="Chrome 120" / Os="Windows 11"
    }
}
```

### 6.8 端到端冒烟测试

**`P0SystemAdminFeaturesE2ETests.cs`**：

```csharp
[Fact]
public async Task LoginToOnlineUserQuery_FullFlowWorks()
{
    // 1. 用 Identity 服务登录（Testcontainers + 真实 Redis）
    // 2. 立即调 SystemAdmin GET /api/admin/online-users
    // 3. 断言返回列表包含刚才登录的用户
}

[Fact]
public async Task LoginToLoginLogQuery_FullFlowWorks()
{
    // 1. 登录
    // 2. 等待 1s（事件消费延迟）
    // 3. 调 GET /api/admin/login-logs?username=admin
    // 4. 断言返回最新 1 条记录
}

[Fact]
public async Task ForceOffline_RemovesFromOnlineList()
{
    // 1. 登录 user A
    // 2. 用 Admin 登录
    // 3. Admin 调 DELETE /api/admin/online-users/{A.sessionId}
    // 4. 调 GET /api/admin/online-users → 列表不含 A
}

[Fact]
public async Task MenuCrud_FullCycleWorks()
{
    // POST → GET → PUT → DELETE → GET 404
}
```

### 6.9 测试基础设施

**`SystemAdminApiFactory.cs`**（WebApplicationFactory）：

```csharp
public sealed class SystemAdminApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // 替换 DbContext 为 SQLite in-memory
            services.RemoveAll<DbContextOptions<SystemAdminDbContext>>();
            services.AddDbContext<SystemAdminDbContext>(opt => opt.UseSqlite("DataSource=:memory:"));

            // 替换 IConnectionMultiplexer 为 Testcontainers Redis
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(_ => TestRedisContainer.GetConnection());

            // 替换 ICurrentUserContext 为测试用户
            services.RemoveAll<ICurrentUserContext>();
            services.AddScoped(_ => new TestCurrentUserContext(role: "Admin"));
        });
    }
}
```

### 6.10 测试依赖

新增 NuGet 包（仅测试项目）：

```xml
<!-- Leno.SystemAdmin.Infrastructure.Tests.csproj -->
<PackageReference Include="Testcontainers.Redis" Version="3.9.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.11" />

<!-- Leno.SystemAdmin.Api.Tests.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.11" />
<PackageReference Include="MassTransit.TestFramework" Version="8.2.3" />
```

### 6.11 CI 集成

[现有 CI 配置](file:///workspace/.github/workflows/ci.yml) 已支持 dotnet test，无需改动。Testcontainers 在 GitHub Actions Linux runner 上原生支持 Docker。

**覆盖率门槛**（[coverage-thresholds.json](file:///workspace/scripts/coverage-thresholds.json)）：

```json
{
  "Leno.SystemAdmin.Domain": 90,
  "Leno.SystemAdmin.Application": 85,
  "Leno.SystemAdmin.Infrastructure": 75,
  "Leno.SystemAdmin.Api": 80
}
```

新增代码覆盖率不低于现有门槛，CI 失败则阻塞合并。

### 6.12 测试用例总数

| 项目 | 新增测试文件数 | 用例数 |
|---|---|---|
| Domain.Tests | 2 | 16 |
| Application.Tests | 5 | 32 |
| Infrastructure.Tests | 4 | 24 |
| Api.Tests | 5 | 44 |
| **合计** | **16** | **116** |

## 7 实施顺序建议

建议按以下顺序分阶段实施（具体拆分由 writing-plans skill 决定）：

1. **基础设施抽象层**：`IUserSessionStore` 接口定义（Leno.Infrastructure.Abstractions）+ `IUserAgentParser` 抽象 + `OnlineUserSession` 投影 + 异常类型扩展（NotFoundException / ForbiddenException / ServiceUnavailableException）+ `ICurrentUserContext.SessionId` 扩展
2. **领域层**：`Menu` / `LoginLog` 聚合根 + 仓储接口 + 域服务抽象（`IRedisCacheMonitor` / `IDotNetProcessMonitor` / `IMetricHistoryStore`）
3. **基础设施层**：EF 配置 + 迁移 + 仓储实现 + Redis 实现（UserSessionStore / CacheMonitorService）+ .NET 进程监控 + 后台采样服务
4. **应用层**：5 个 AppService 接口与实现 + DTO 文件 + UA 解析与地理定位
5. **API 层**：5 个 Controller + `[Authorize(Roles = "Admin")]` 角色鉴权 + 全局异常中间件扩展（新增 NotFoundException / ForbiddenException / ServiceUnavailableException 映射）
6. **Identity 改动**：AuthAppService 注入 `IUserSessionStore` + `IUserAgentParser`，登录成功同步写 Redis（含 UA 解析）+ 发布 `UserLoggedInEvent`（仅携带原始 UserAgent）
7. **测试**：领域单测 → 应用单测 → 仓储集成测试 → 基础设施 Testcontainers 测试 → Controller 集成测试 → 跨域事件测试 → E2E 冒烟
8. **联调与验收**：前端 `VITE_USE_MOCK=false` 切换到真实后端，端到端验证 6 项功能

## 8 风险与缓解

| 风险 | 影响 | 缓解 |
|---|---|---|
| Identity 登录流程改动导致登录失败 | 用户无法登录 | `IUserSessionStore.RecordAsync` 失败时仅记日志不抛异常（登录仍成功，仅在线用户列表缺失该会话） |
| Redis 不可用导致在线用户与缓存监控不可用 | 5 项功能中 2 项降级 | 在线用户返回空列表；缓存监控返回 503；服务器监控不依赖 Redis 永远可用 |
| EF Core 迁移与生产数据库版本不兼容 | 部署失败 | 迁移脚本在 staging 环境验证；JSON 字段查询在 SQL Server 2016+ 兼容（退化为 LIKE） |
| UserLoggedInEvent 消费失败导致登录日志缺失 | 审计不完整 | MassTransit 自动重试 3 次；失败进死信队列；监控告警 |
| MaxMind GeoLite2 数据库未下载 | 公网 IP 地理定位为空 | 启动时检查文件存在性，缺失时仅记录内网标记，不阻塞启动 |
| `IServer.Keys()` 在大 keyspace 上扫描慢 | 缓存监控页面超时 | 单次最多扫描 `pageSize * 5` 个 key 后停止；前端提示「数据量过大，请缩小 pattern」 |
| 后台采样服务异常退出 | 历史数据停止更新 | `try-catch` 包裹单次采样，失败仅记日志不退出；进程重启后历史数据清空符合预期 |
| JWT jti claim 缺失导致 sessionId 为空 | 在线用户功能完全失效 | `ICurrentUserContext.SessionId` 解析失败时返回空字符串；`OnlineUsersController` 对空 sessionId 返回 400 |

## 9 验收清单

**功能验收**：
- 19 个新 Endpoint 在 Swagger UI 中可见，参数与响应契约对齐前端 spec §3.3-3.7
- 用户登录后立即出现在在线用户列表（< 1s 延迟）
- 登录日志在登录后 1s 内可查询（事件消费延迟）
- 菜单 CRUD 完整：创建/查询树/更新/排序/递归删除
- 缓存监控：Redis INFO / Keyspace / Key 浏览 / Key 详情 / 删除 全部可用
- 服务器监控：6 统计卡片 + 3 折线图 + 系统信息全部填充真实数据
- 强制下线：当前操作者无法下线自己；下线他人后从列表移除
- Redis 不可用时：在线用户返回空列表；缓存监控返回 503；服务器监控正常

**代码验收**：
- 5 个 Controller / 5 个 AppService / 2 个聚合根 + 1 个 Redis 投影 / 2 个仓储 / 3 个 Redis 实现 全部完整实现，无占位符
- EF Core 迁移可在干净数据库与现有数据库上正向应用
- 新增 116 个测试用例全部通过
- 新增代码覆盖率不低于现有门槛（Domain 90% / Application 85% / Infrastructure 75% / Api 80%）
- Identity 改动仅 2 行同步调用，不影响现有登录流程

**文档验收**：
- spec 文档自检通过（无占位符、无歧义、无内部矛盾）
- 5 Controller / 19 Endpoint 契约与前端 spec §3.8 完全对齐
- 后端 API 文档（Swagger）自动生成，无需额外维护

**部署验收**：
- Docker 镜像构建成功（SystemAdmin.Api 与 Identity.Api）
- Helm values-dev / values-staging / values-prod 无需修改（仅环境变量）
- 启动时 EF 迁移自动执行（`MigrateWithLockAsync`）
- Consul 服务注册正常
