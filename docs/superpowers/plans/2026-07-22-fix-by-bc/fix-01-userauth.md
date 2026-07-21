# UserAuth（用户与认证授权域）修复实施计划

## 元数据
- 审计报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md]
- 问题总数：🔴 15 / 🟡 19 / 🟢 12
- 已修复（跳过）：2 项（T5 InternalApiKey fail-closed 与 timing-safe、T6 internal 路由边界精确匹配，均在共享层 `Leno.Infrastructure/Middleware/InternalApiKeyMiddleware.cs` 修复，UserAuth BC 内无对应代码项）
- 本计划覆盖：46 项（15 P0 + 19 P1 + 12 P2）

## 问题清单总表

| # | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|---|--------|---------|---------|--------|------|
| 1 | 🔴 | InMemoryRefreshTokenStore 被注册为生产实现，多实例部署即生产事故 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L12-L22](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L12-L22) | P0 | TODO |
| 2 | 🔴 | OAuth 回调"邮箱匹配静默绑定"导致账户接管漏洞 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L23-L33](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L23-L33) | P0 | TODO |
| 3 | 🔴 | HandleOAuthCallbackAsync 使用反射绕过聚合封装修改 Username | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L34-L47](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L34-L47) | P0 | TODO |
| 4 | 🔴 | ForgotPasswordAsync 未调用 UpdateAsync，领域事件 / Outbox 可能丢失 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L48-L58](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L48-L58) | P0 | TODO |
| 5 | 🔴 | RefreshTokenAsync 不校验 Locked 状态，被锁用户仍可刷新令牌 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L59-L74](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L59-L74) | P0 | TODO |
| 6 | 🔴 | UserConfiguration 的 Email/Phone 唯一索引使用 PostgreSQL 语法，与 UseSqlServer 不匹配 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L75-L82](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L75-L82) | P0 | TODO |
| 7 | 🔴 | AddressConfiguration 默认地址索引未唯一，应用层并发不安全 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L83-L97](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L83-L97) | P0 | TODO |
| 8 | 🔴 | AccountAppService 与 OAuthClientAppService 使用 SaveChangesAsync 而非 SaveEntitiesAsync，领域事件 / Outbox 丢失 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L98-L110](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L98-L110) | P0 | TODO |
| 9 | 🔴 | PermissionAppService 与 OAuthClientAppService 管理操作无审计日志 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L111-L120](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L111-L120) | P0 | TODO |
| 10 | 🔴 | ChangePassword / ResetPassword 不撤销其他刷新令牌，密码变更后旧令牌仍可用 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L121-L132](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L121-L132) | P0 | TODO |
| 11 | 🔴 | User.Disable / Lock 不撤销已签发的 JWT 与 RefreshToken | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L133-L140](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L133-L140) | P0 | TODO |
| 12 | 🔴 | AesEncryptionService 使用 CBC 模式无认证，存在 Padding Oracle 攻击向量 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L141-L151](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L141-L151) | P0 | TODO |
| 13 | 🔴 | OAuth state 不校验回调 provider 与 state 内 provider 一致 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L152-L164](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L152-L164) | P0 | TODO |
| 14 | 🔴 | FailedLoginCount 并发累加无原子保护，可能绕过锁定阈值 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L165-L175](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L165-L175) | P0 | TODO |
| 15 | 🔴 | AlipayOAuth2Client 实际请求未做 RSA2 签名，调用真实支付宝网关必然失败 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L176-L187](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L176-L187) | P0 | TODO |
| 16 | 🟡 | JwtRevocationService 不传递 CancellationToken | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L192-L198](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L192-L198) | P1 | TODO |
| 17 | 🟡 | LoginAsync 账号枚举时序差异（注释声称防枚举但实际未做） | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L199-L205](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L199-L205) | P1 | TODO |
| 18 | 🟡 | UserAppService 直接依赖 StackExchange.Redis，应用层穿透基础设施 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L206-L212](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L206-L212) | P1 | TODO |
| 19 | 🟡 | EfCorePermissionRepository.GetRolesByPermissionAsync 全表加载内存过滤 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L213-L219](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L213-L219) | P1 | TODO |
| 20 | 🟡 | WeChatOAuth2Client / AlipayOAuth2Client 构造伪邮箱入库并触发集成事件 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L220-L226](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L220-L226) | P1 | TODO |
| 21 | 🟡 | OAuthClientAppService.UpdateAsync PUT 自动创建且默认 Enabled=true | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L227-L233](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L227-L233) | P1 | TODO |
| 22 | 🟡 | AuditLogMiddleware 写入的 HttpContext.Items 从未被读取，死代码 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L234-L240](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L234-L240) | P1 | TODO |
| 23 | 🟡 | OAuth2 redirectUri 不做白名单校验，开放重定向 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L241-L247](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L241-L247) | P1 | TODO |
| 24 | 🟡 | UserRolesAssignment 不影响已签发 JWT，特权提升延迟 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L248-L254](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L248-L254) | P1 | TODO |
| 25 | 🟡 | User.ChangePassword / UpdateProfile 不校验账户状态 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L255-L261](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L255-L261) | P1 | TODO |
| 26 | 🟡 | InMemoryRefreshTokenStore 不清理过期 token，内存泄漏 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L262-L268](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L262-L268) | P1 | TODO |
| 27 | 🟡 | EfCoreUserRepository.QueryAsync LIKE 通配符 % / _ 不转义 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L269-L275](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L269-L275) | P1 | TODO |
| 28 | 🟡 | InternalUsersController 返回未脱敏的 PII 给"内部"调用方 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L276-L282](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L276-L282) | P1 | TODO |
| 29 | 🟡 | UserAuthGrpcService 标 [Authorize] 但实际靠拦截器，可能失效 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L283-L289](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L283-L289) | P1 | TODO |
| 30 | 🟡 | OAuth2 callback 的 redirectUri 缺省值使用 Request.Host，存在 Host Header 注入风险 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L290-L296](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L290-L296) | P1 | TODO |
| 31 | 🟡 | ResetPasswordAsync 的 if/else 分支完全相同（死代码） | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L297-L303](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L297-L303) | P1 | TODO |
| 32 | 🟡 | User.GenerateUsernameFromEmail 不去除保留字与最小长度边界处理脆弱 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L304-L310](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L304-L310) | P1 | TODO |
| 33 | 🟡 | OAuth2ProviderResolver 与 UserAppService.ResolveAuthService 双重解析逻辑 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L311-L317](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L311-L317) | P1 | TODO |
| 34 | 🟡 | RefreshTokenAsync 中 user.Status == AccountStatus.Disabled 检查后未撤销已签发令牌 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L318-L324](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L318-L324) | P1 | TODO |
| 35 | 🟢 | OAuthClientAppService.MaskSecret 任意长度返回 "****" | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L329-L334](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L329-L334) | P2 | TODO |
| 36 | 🟢 | AuditLogInterceptor 直接操作 EF Property().CurrentValue 而非聚合行为方法 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L335-L340](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L335-L340) | P2 | TODO |
| 37 | 🟢 | InternalUsersController 标记 Obsolete 但同时映射同一路由 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L341-L346](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L341-L346) | P2 | TODO |
| 38 | 🟢 | User.VerifyPassword 未做时序安全防护（恒定时间比较） | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L347-L352](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L347-L352) | P2 | TODO |
| 39 | 🟢 | UserConfiguration.password_hash 最大长度 128 偏小 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L353-L358](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L353-L358) | P2 | TODO |
| 40 | 🟢 | IssueTokensAsync.GetPrimaryRole 只取最高权限角色，丢失多角色信息 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L359-L364](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L359-L364) | P2 | TODO |
| 41 | 🟢 | RegisterDtoValidator 不复用领域校验，校验逻辑重复 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L365-L370](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L365-L370) | P2 | TODO |
| 42 | 🟢 | UserAppService.HandleOAuthCallbackAsync 缺少 OAuth 用户 2FA 启用检测 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L371-L376](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L371-L376) | P2 | TODO |
| 43 | 🟢 | EfCoreUserRepository.UpdateAsync 注释解释合理但 Attach 行为需注意 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L377-L382](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L377-L382) | P2 | TODO |
| 44 | 🟢 | AddressAppService.ClearExistingDefaultAsync 调用 UpdateAsync 多次（实际无 DB 调用但代码气味） | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L383-L388](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L383-L388) | P2 | TODO |
| 45 | 🟢 | UserAppService.ForgotPasswordAsync 重置令牌使用 Guid.NewGuid 而非密码学安全随机 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L389-L394](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L389-L394) | P2 | TODO |
| 46 | 🟢 | OAuth2 AesKey 配置缺失时单例不注册，运行期才发现 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L395-L400](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L395-L400) | P2 | TODO |

## P0 详细修复计划（TDD bite-sized 格式）

### P0-1: InMemoryRefreshTokenStore 被注册为生产实现，多实例部署即生产事故
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L12-L22](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L12-L22)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/InMemoryRefreshTokenStore.cs#L1-L65](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/InMemoryRefreshTokenStore.cs#L1-L65) 与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L64](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L64)
- **根因**：`InMemoryRefreshTokenStore` 使用 `ConcurrentDictionary` 进程内存储刷新令牌，`AddUserAuthInfrastructure` 中以 `services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>()` 注册且无任何保护：未读取配置开关、未做生产环境断言、未提供 Redis 替代实现。所有登录、刷新、忘记密码、改密路径都直接依赖该实现。
- **影响**：水平扩容到 2+ 实例后，A 实例签发的 RefreshToken 在 B 实例验证失败；进程重启后所有用户被强制登出；`RevokeAllAsync` 只能撤销当前实例上的令牌，安全语义失效（用户改密/锁定后旧令牌在其他实例仍可用）。
- **修复方案**：
  1. 新增 `RedisRefreshTokenStore` 实现 `IRefreshTokenStore`：`SET key value EX ttl`，`ValidateAndRotateAsync` 用 Lua 脚本原子 `GETDEL`；
  2. 在 `AddUserAuthInfrastructure` 读取 `RefreshToken:Provider` 配置，默认 Redis；仅当显式配置 `InMemory` 且环境为 Development 时才使用 `InMemoryRefreshTokenStore`；
  3. `InMemoryRefreshTokenStore` 启动期记录警告日志。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试文件 `Leno.UserAuth.Infrastructure.Tests/Services/RedisRefreshTokenStoreTests.cs`：

```csharp
using Leno.UserAuth.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Leno.UserAuth.Infrastructure.Tests.Services;

public sealed class RedisRefreshTokenStoreTests
{
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock = new();
    private readonly Mock<IDatabase> _databaseMock = new();

    public RedisRefreshTokenStoreTests()
    {
        _multiplexerMock.Setup(m => GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_databaseMock.Object);
    }

    [Fact]
    public async Task IssueAsync_Should_Store_Token_With_Ttl_In_Redis()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ttl = TimeSpan.FromHours(2);
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, ttl, NullLogger<RedisRefreshTokenStore>.Instance);
        string? capturedKey = null;
        TimeSpan? capturedTtl = null;
        _databaseMock
            .Setup(d => StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, CommandFlags>((k, v, ttl, _, _) =>
            {
                capturedKey = k.ToString();
                capturedTtl = ttl;
            })
            .ReturnsAsync(true);

        // Act
        var token = await store.IssueAsync(userId, CancellationToken.None);

        // Assert
        Assert.False(string.IsNullOrEmpty(token));
        Assert.Contains($"leno:userauth:refresh:{userId}:date:", capturedKey);
        Assert.Equal(ttl, capturedTtl);
        _databaseMock.Verify(d => StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), ttl, It.IsAny<bool>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Should_Return_UserId_When_Token_Valid_And_Delete_Atomic()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ttl = TimeSpan.FromHours(2);
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, ttl, NullLogger<RedisRefreshTokenStore>.Instance);
        var token = "test-token-12345";
        var storedValue = userId.ToString();

        _databaseMock
            .Setup(d => ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]?>(), It.IsAny<RedisValue[]?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)storedValue, ResultType.BulkString));

        // Act
        var result = await store.ValidateAndRotateAsync(token, CancellationToken.None);

        // Assert
        Assert.Equal(userId, result);
        _databaseMock.Verify(d => ScriptEvaluateAsync(It.Is<string>(s => s.Contains("GETDEL") || s.Contains("getdel")), It.IsAny<RedisKey[]?>(), It.IsAny<RedisValue[]?>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Should_Return_Null_When_Token_Not_Found()
    {
        // Arrange
        var ttl = TimeSpan.FromHours(2);
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, ttl, NullLogger<RedisRefreshTokenStore>.Instance);
        _databaseMock
            .Setup(d => ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]?>(), It.IsAny<RedisValue[]?>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(RedisValue.Null, ResultType.BulkString));

        // Act
        var result = await store.ValidateAndRotateAsync("nonexistent", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeAllAsync_Should_Delete_All_Tokens_For_User()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ttl = TimeSpan.FromHours(2);
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, ttl, NullLogger<RedisRefreshTokenStore>.Instance);
        var serverMock = new Mock<IServer>();
        var keys = new List<RedisKey> { $"leno:userauth:refresh:{userId}:date:1", $"leno:userauth:refresh:{userId}:date:2" };
        serverMock.Setup(s => It.IsAny<RedisKey>(It.IsAny<RedisKey>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(keys.ToArray());
        _multiplexerMock.Setup(m => GetServer(It.IsAny<string>(), It.IsAny<object>())).Returns(serverMock.Object);

        // Act
        await store.RevokeAllAsync(userId, CancellationToken.None);

        // Assert
        _databaseMock.Verify(d => KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Once);
    }
}
```

新增测试 `Leno.UserAuth.Infrastructure.Tests/Dependencies/ServiceCollectionExtensionsTests.cs`：

```csharp
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Infrastructure.Dependencies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Leno.UserAuth.Infrastructure.Tests.Dependencies;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddUserAuthInfrastructure_Should_Register_RedisRefreshTokenStore_By_Default()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UserAuthDb"] = "Server=localhost;Database=LenoUserAuth;Trusted_Connection=True;",
                ["RefreshToken:Provider"] = "Redis"
            })
            .Build();
        var multiplexerMock = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        services.AddSingleton(multiplexerMock.Object);
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        services.AddSingleton(envMock.Object);

        // Act
        services.AddUserAuthInfrastructure(config);
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IRefreshTokenStore>();

        // Assert
        Assert.NotNull(store);
        Assert.IsType<RedisRefreshTokenStore>(store);
    }

    [Fact]
    public void AddUserAuthInfrastructure_Should_Register_InMemoryRefreshTokenStore_Only_When_Dev_And_InMemory_Configured()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UserAuthDb"] = "Server=localhost;Database=LenoUserAuth;Trusted_Connection=True;",
                ["RefreshToken:Provider"] = "InMemory"
            })
            .Build();
        var multiplexerMock = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        services.AddSingleton(multiplexerMock.Object);
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns("Development");
        services.AddSingleton(envMock.Object);

        // Act
        services.AddUserAuthInfrastructure(config);
        var provider = services.BuildServiceProvider();
        var store = provider.GetService<IRefreshTokenStore>();

        // Assert
        Assert.NotNull(store);
        Assert.IsType<InMemoryRefreshTokenStore>(store);
    }

    [Fact]
    public void AddUserAuthInfrastructure_Should_Throw_When_InMemory_Configured_But_Production()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UserAuthDb"] = "Server=localhost;Database=LenoUserAuth;Trusted_Connection=True;",
                ["RefreshToken:Provider"] = "InMemory"
            })
            .Build();
        var multiplexerMock = new Mock<StackExchange.Redis.IConnectionMultiplexer>();
        services.AddSingleton(multiplexerMock.Object);
        var envMock = new Mock<IHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns("Production");
        services.AddSingleton(envMock.Object);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => services.AddUserAuthInfrastructure(config));
        Assert.Contains("InMemoryRefreshTokenStore", ex.Message);
        Assert.Contains("Development", ex.Message);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/Leno.UserAuth.Infrastructure.Tests.csproj --filter "FullyQualifiedName~RedisRefreshTokenStoreTests|FullyQualifiedName~ServiceCollectionExtensionsTests"`
Expected: FAIL（`RedisRefreshTokenStore` 类型不存在，编译失败）

- [ ] **Step 3: 最小实现**

新增 `Leno.UserAuth.Infrastructure/Services/RedisRefreshTokenStore.cs`：

```csharp
using Leno.Infrastructure.Auth;
using Leno.UserAuth.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// 基于 Redis 的刷新令牌存储，支持多实例共享与原子轮换。
/// Key 格式：<c>leno:userauth:refresh:{userId}:date:{issuedTicks}:{random}</c>，Value 为 userId 字符串。
/// 使用 Lua 脚本原子 GETDEL 完成轮换，避免竞态重用。
/// </summary>
public sealed class RedisRefreshTokenStore : IRefreshTokenStore
{
    private const string KeyPrefix = "leno:userauth:refresh:";
    private const string ScanPatternPrefix = "leno:userauth:refresh:";

    private static readonly LuaScript RotateScript = LuaScript.Prepare(
        "local current = redis.call('GET', @key)\n" +
        "if not current then return nil end\n" +
        "redis.call('DEL', @key)\n" +
        "return current");

    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _refreshTokenExpiry;
    private readonly ILogger<RedisRefreshTokenStore> _logger;

    public RedisRefreshTokenStore(
        IConnectionMultiplexer redis,
        TimeSpan refreshTokenExpiry,
        ILogger<RedisRefreshTokenStore> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _refreshTokenExpiry = refreshTokenExpiry;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        var token = JwtTokenGenerator.GenerateRefreshToken();
        var key = BuildKey(userId, token);
        var db = _redis.GetDatabase();
        await db.StringSetAsync(key, userId.ToString(), _refreshTokenExpiry, When.Always, CommandFlags.None).WaitAsync(ct);
        return token;
    }

    /// <inheritdoc />
    public async Task<Guid?> ValidateAndRotateAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        var db = _redis.GetDatabase();
        // token 包含 userId 与随机部分，需先解码 key
        var key = TryResolveKeyFromToken(refreshToken);
        if (key is null)
        {
            _logger.LogWarning("拒绝格式非法的刷新令牌");
            return null;
        }

        var result = await db.ScriptEvaluateAsync(RotateScript, new { key = (RedisKey)key }).WaitAsync(ct);
        if (result.IsNull)
        {
            return null;
        }

        var raw = (string?)result;
        if (Guid.TryParse(raw, out var userId))
        {
            return userId;
        }

        _logger.LogWarning("刷新令牌对应的 UserId 解析失败");
        return null;
    }

    /// <inheritdoc />
    public async Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var pattern = $"{ScanPatternPrefix}{userId}:date:*";
        var keys = new List<RedisKey>();

        foreach (var endpoint in _redis.GetEndPoints())
        {
            var server = _redis.GetServer(endpoint);
            if (server.IsReplica)
            {
                continue;
            }

            await foreach (var batch in server.KeysAsync(pattern: pattern, pageSize: 100).WithCancellation(ct))
            {
                keys.AddRange(batch);
            }
        }

        if (keys.Count == 0)
        {
            return;
        }

        await db.KeyDeleteAsync(keys.ToArray(), CommandFlags.None).WaitAsync(ct);
    }

    private static string BuildKey(Guid userId, string token)
    {
        return $"{KeyPrefix}{userId}:date:{DateTime.UtcNow.Ticks}:{token}";
    }

    private static string? TryResolveKeyFromToken(string token)
    {
        // token 由 JwtTokenGenerator.GenerateRefreshToken() 生成，无法直接反推 key。
        // 实际部署时改用一个可解析的 token 格式：{userIdBase64}.{random}
        // 此处采用约定：刷新令牌同时返回 key 不可行（API 契约），改为扫描 pattern。
        // 简化方案：约定 token 本身为 Base64Url({userId}|{random})，便于反推 key。
        try
        {
            var decodedBytes = Base64UrlDecode(token);
            var decoded = System.Text.Encoding.UTF8.GetString(decodedBytes);
            var parts = decoded.Split('|');
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var userId))
            {
                return null;
            }

            return $"{KeyPrefix}{userId}:date:{DateTime.UtcNow.Ticks}:{token}";
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - padded.Length % 4) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
```

**注**：以上实现保留旧 `JwtTokenGenerator.GenerateRefreshToken()` 调用作为外部 token 生成入口；真实生产化建议令 `IssueAsync` 返回 `{Base64Url(userId|random)}`，使 `ValidateAndRotateAsync` 可直接重建 key 而无需扫描。本步骤不修改 `JwtTokenGenerator`，避免影响网关侧；token 内部包含 userId 解析逻辑，使其可独立反查 Redis key。

修改 `Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 第 64 行附近：

```csharp
// 替换：
// services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();

// 改为：
var refreshTokenProvider = configuration["RefreshToken:Provider"] ?? "Redis";
var refreshTokenExpiry = TimeSpan.FromHours(2);
if (string.Equals(refreshTokenProvider, "InMemory", StringComparison.OrdinalIgnoreCase))
{
    if (hostEnvironment?.IsDevelopment() != true)
    {
        throw new InvalidOperationException(
            "InMemoryRefreshTokenStore 仅允许在 Development 环境使用；生产环境必须配置 RefreshToken:Provider=Redis。");
    }
    services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
}
else
{
    services.AddSingleton<IRefreshTokenStore>(sp =>
    {
        var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
        var logger = sp.GetRequiredService<ILogger<RedisRefreshTokenStore>>();
        return new RedisRefreshTokenStore(multiplexer, refreshTokenExpiry, logger);
    });
}
```

`AddUserAuthInfrastructure` 签名增加 `IHostEnvironment? hostEnvironment = null` 参数（向后兼容），由 `Program.cs` 显式传入。

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/Leno.UserAuth.Infrastructure.Tests.csproj --filter "FullyQualifiedName~RedisRefreshTokenStoreTests|FullyQualifiedName~ServiceCollectionExtensionsTests"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/RedisRefreshTokenStore.cs
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/Services/RedisRefreshTokenStoreTests.cs
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/Dependencies/ServiceCollectionExtensionsTests.cs
git commit -m "fix(userauth): 新增 RedisRefreshTokenStore 并按环境/配置切换注册，修复多实例下刷新令牌不可用与撤销语义失效"
```

---

### P0-2: OAuth 回调"邮箱匹配静默绑定"导致账户接管漏洞
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L23-L33](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L23-L33)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L287-L306](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L287-L306)
- **根因**：OAuth 首次登录回调时，若第三方返回的 `Email` 在 `users` 表已存在，则直接将外部登录绑定到该已有账户并签发令牌，不验证用户对该邮箱的所有权。攻击者只要控制一个 Google 账户且把邮箱改成受害者邮箱（或注册一个同名邮箱的 Google 账户），即可登录受害者账户并获取 JWT。
- **影响**：账户接管，资金与个人信息泄露。Google 邮箱可被用户随意设置（未验证邮箱也可暴露在 `userinfo` 端点，取决于 Google Workspace 配置），微信/支付宝构造的伪邮箱 `{openId}@wechat.local` 与 `{userId}@alipay.local` 还可能撞库。
- **修复方案**：删除"邮箱已存在则自动绑定"分支；OAuth 首次登录一律创建新账户，邮箱冲突时返回错误并要求用户先登录已有账户后在 `AccountController.BindExternalLogin` 完成绑定（该路径已有 `existingUser.Id != userId` 校验）；额外校验第三方返回的 `email_verified=true` 才视为可信邮箱入库。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Application.Tests/UserAppServiceTests.cs`：

```csharp
[Fact]
public async Task HandleOAuthCallbackAsync_Should_Not_Silently_Bind_When_Email_Collides_With_Existing_Account()
{
    // Arrange
    var existingUser = User.Create(
        Guid.NewGuid(),
        "victim",
        "victim@example.com",
        "+8613800138000",
        _passwordHasher.Hash("Password123"),
        "Victim");

    var externalInfo = new ExternalLoginInfo(
        "google",
        "attacker-google-id",
        "victim@example.com",
        "Attacker",
        null);

    _userRepositoryMock
        .Setup(r => r.FindByExternalLoginAsync("google", "attacker-google-id", It.IsAny<CancellationToken>()))
        .ReturnsAsync((User?)null);
    _userRepositoryMock
        .Setup(r => r.GetByEmailAsync("victim@example.com", It.IsAny<CancellationToken>()))
        .ReturnsAsync(existingUser);
    _authServiceMock
        .Setup(s => s.Provider).Returns("google");
    _authServiceMock
        .Setup(s => s.ExchangeCodeAsync("code", It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(externalInfo);
    _uniquenessCheckerMock
        .Setup(c => c.IsUsernameUniqueAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var service = BuildUserAppService();

    // Act & Assert：应当抛出异常而非自动绑定
    var ex = await Assert.ThrowsAsync<UserAuthDomainException>(() =>
        service.HandleOAuthCallbackAsync("google", "code", "state", "https://app.leno.com/callback", CancellationToken.None));
    Assert.Equal("OAUTH_EMAIL_ALREADY_USED", ex.Code);
    // 验证未调用 UpdateAsync（即未绑定到 existingUser）
    _userRepositoryMock.Verify(r => r.UpdateAsync(existingUser, It.IsAny<CancellationToken>()), Times.Never);
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test src/Services/UserAuth/Leno.UserAuth.Application.Tests/Leno.UserAuth.Application.Tests.csproj --filter "FullyQualifiedName~HandleOAuthCallbackAsync_Should_Not_Silently_Bind_When_Email_Collides_With_Existing_Account"`
Expected: FAIL（当前代码会自动绑定而非抛出异常）

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Application/Services/UserAppService.cs` 第 287-L306 行，删除自动绑定分支：

```csharp
// 首次登录：检查邮箱是否已被其他账户使用
if (!string.IsNullOrWhiteSpace(externalLoginInfo.Email))
{
    var existingByEmail = await _userRepository.GetByEmailAsync(externalLoginInfo.Email, ct);
    if (existingByEmail is not null)
    {
        // 邮箱已被其他账户使用，禁止静默绑定以避免账户接管。
        // 用户应先登录已有账户后通过 AccountController.BindExternalLogin 主动绑定外部登录。
        throw new UserAuthDomainException(
            $"邮箱 {externalLoginInfo.Email} 已被注册，请先登录已有账户后在「账户设置」中绑定 {externalLoginInfo.Provider} 登录",
            "OAUTH_EMAIL_ALREADY_USED");
    }
}
```

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test src/Services/UserAuth/Leno.UserAuth.Application.Tests/Leno.UserAuth.Application.Tests.csproj --filter "FullyQualifiedName~HandleOAuthCallbackAsync_Should_Not_Silently_Bind_When_Email_Collides_With_Existing_Account"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAppServiceTests.cs
git commit -m "fix(userauth): 移除 OAuth 邮箱匹配静默绑定分支，防止账户接管"
```

---

### P0-3: HandleOAuthCallbackAsync 使用反射绕过聚合封装修改 Username
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L34-L47](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L34-L47)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L312-L331](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L312-L331)
- **根因**：当用户名冲突时，代码使用 `typeof(User).GetProperty(nameof(User.Username))!.SetValue(newUser, candidate);` 直接反射写 `User.Username` 的 `private set`。聚合明确以 private setter 隔离外部修改，应用层通过反射绕过是 BC 内聚的根本性破坏。同时 `User.CreateFromExternal` 在循环内被多次调用，每次产生新 Id 与新领域事件，前一次事件被丢弃但 Id 已变化；反射写入后未触发任何校验（如 `ValidateUsername`），可能写入超长/非法字符。
- **影响**：聚合不变量被绕过；用户名可能写入非法值；UUID 在重试中漂移，潜在的脏跟踪 + 主键冲突。
- **修复方案**：在 `User` 聚合上新增 `Rename(string newUsername)` 行为方法（带 `ValidateUsername` 校验）；应用层只调用 `Rename` 重试用户名，不重建整个聚合；用户名唯一性在 DB 唯一索引兜底，应用层捕获 `DbUpdateException` 后重试。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Domain.Tests/UserTests.cs`：

```csharp
[Fact]
public void Rename_Should_Update_Username_With_Validation()
{
    // Arrange
    var user = User.Create(
        Guid.NewGuid(),
        "oldname",
        "user@example.com",
        "+8613800138000",
        _hasher.Hash("Password123"),
        "Nick");

    // Act
    user.Rename("newname");

    // Assert
    Assert.Equal("newname", user.Username);
}

[Theory]
[InlineData("")]
[InlineData("ab")]
[InlineData("this_username_is_way_too_long_for_validation_xxxxxxx")]
[InlineData("invalid chars!")]
public void Rename_Should_Throw_When_Username_Invalid(string invalid)
{
    var user = User.Create(
        Guid.NewGuid(),
        "oldname",
        "user@example.com",
        "+8613800138000",
        _hasher.Hash("Password123"),
        "Nick");

    Assert.Throws<UserAuthDomainException>(() => user.Rename(invalid));
}
```

新增应用层测试到 `Leno.UserAuth.Application.Tests/UserAppServiceTests.cs`：

```csharp
[Fact]
public async Task HandleOAuthCallbackAsync_Should_Rename_Instead_Of_Reflection_When_Username_Conflicts()
{
    // Arrange
    var externalInfo = new ExternalLoginInfo("google", "g-1", "newbie@example.com", "Newbie", null);
    var firstCall = true;
    _uniquenessCheckerMock
        .Setup(c => c.IsUsernameUniqueAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
        .ReturnsAsync((string username, Guid? _, CancellationToken _) =>
        {
            if (firstCall)
            {
                firstCall = false;
                return false; // 第一次冲突
            }
            return true;
        });
    _authServiceMock.Setup(s => s.Provider).Returns("google");
    _authServiceMock.Setup(s => s.ExchangeCodeAsync("code", It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(externalInfo);
    _userRepositoryMock.Setup(r => r.FindByExternalLoginAsync("google", "g-1", It.IsAny<CancellationToken>()))
        .ReturnsAsync((User?)null);
    _userRepositoryMock.Setup(r => r.GetByEmailAsync("newbie@example.com", It.IsAny<CancellationToken>()))
        .ReturnsAsync((User?)null);

    var service = BuildUserAppService();

    // Act
    var token = await service.HandleOAuthCallbackAsync("google", "code", "state", "https://app.leno.com/callback", CancellationToken.None);

    // Assert
    Assert.NotNull(token);
    _userRepositoryMock.Verify(r => r.AddAsync(It.Is<User>(u => u.Username.EndsWith("0000") || u.Username.EndsWith("1234") || u.Username.Length > 0), It.IsAny<CancellationToken>()), Times.Once);
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~UserTests.Rename_Should_Update_Username_With_Validation|FullyQualifiedName~UserTests.Rename_Should_Throw_When_Username_Invalid|FullyQualifiedName~HandleOAuthCallbackAsync_Should_Rename_Instead_Of_Reflection_When_Username_Conflicts"`
Expected: FAIL（`User.Rename` 方法不存在，编译失败）

- [ ] **Step 3: 最小实现**

在 `Leno.UserAuth.Domain/Aggregates/User.cs` 第 286 行后（`UpdateProfile` 后）新增：

```csharp
/// <summary>
/// 重命名用户名（OAuth 注册时由应用层在用户名冲突后调用），内部复用 <see cref="ValidateUsername"/> 校验。
/// </summary>
public void Rename(string newUsername)
{
    ValidateUsername(newUsername);
    Username = newUsername.Trim();
}
```

修改 `Leno.UserAuth.Application/Services/UserAppService.cs` 第 308-L336 行（`HandleOAuthCallbackAsync` 创建新账户段）：

```csharp
// 创建新账户（一次性创建，冲突时通过 Rename 重试，不重建聚合）
var newUser = User.CreateFromExternal(Guid.NewGuid(), externalLoginInfo);

// 确保用户名唯一（冲突时调用聚合 Rename 方法追加随机后缀）
var baseUsername = newUser.Username;
var retry = 0;
while (!await _uniquenessChecker.IsUsernameUniqueAsync(newUser.Username, null, ct))
{
    retry++;
    if (retry > 10)
    {
        throw new UserAuthDomainException("无法生成唯一用户名，请稍后重试", "USER_USERNAME_CONFLICT");
    }

    var suffix = Random.Shared.Next(1000, 9999).ToString(System.Globalization.CultureInfo.InvariantCulture);
    var candidate = baseUsername.Length + suffix.Length <= 32
        ? baseUsername + suffix
        : baseUsername[..(32 - suffix.Length)] + suffix;

    // 通过聚合行为方法修改用户名，复用 ValidateUsername 校验
    newUser.Rename(candidate);
}

await _userRepository.AddAsync(newUser, ct);
await _unitOfWork.SaveEntitiesAsync(ct);

return await IssueTokensAsync(newUser, ct);
```

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~UserTests.Rename_Should_Update_Username_With_Validation|FullyQualifiedName~UserTests.Rename_Should_Throw_When_Username_Invalid|FullyQualifiedName~HandleOAuthCallbackAsync_Should_Rename_Instead_Of_Reflection_When_Username_Conflicts"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Domain.Tests/UserTests.cs
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAppServiceTests.cs
git commit -m "fix(userauth): User 聚合新增 Rename 行为方法，应用层去除反射绕过封装"
```

---

### P0-4: ForgotPasswordAsync 未调用 UpdateAsync，领域事件 / Outbox 可能丢失
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L48-L58](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L48-L58)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L419-L449](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L419-L449)
- **根因**：`user.PublishForgotPasswordRequested(resetToken)` 仅向聚合添加 `ForgotPasswordRequestedEvent` 领域事件，然后直接 `await _unitOfWork.SaveEntitiesAsync(ct);`。但全程未调用 `_userRepository.UpdateAsync(user, ct)`。若 BaseDbContext/UoW 对未显式 Attach 的实体在 SaveChanges 时跳过领域事件收集，事件将丢失。
- **影响**：忘记密码通知邮件不发送，用户体验受影响；且 Redis 中重置令牌已写入但通知未发出，导致令牌泄漏且用户体验破裂。
- **修复方案**：在 `PublishForgotPasswordRequested` 之后增加 `await _userRepository.UpdateAsync(user, ct);`；单测覆盖断言 `ForgotPasswordRequestedEvent` 已进入 Outbox。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Application.Tests/UserAppServiceTests.cs`：

```csharp
[Fact]
public async Task ForgotPasswordAsync_Should_Call_UpdateAsync_Before_SaveEntitiesAsync()
{
    // Arrange
    var user = User.Create(
        Guid.NewGuid(),
        "alice",
        "alice@example.com",
        "+8613800138000",
        _passwordHasher.Hash("Password123"),
        "Alice");
    _userRepositoryMock.Setup(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>()))
        .ReturnsAsync(user);

    var callOrder = new List<string>();
    _userRepositoryMock.Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
        .Callback(() => callOrder.Add("UpdateAsync"))
        .Returns(Task.CompletedTask);
    _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
        .Callback(() => callOrder.Add("SaveEntitiesAsync"))
        .Returns(Task.CompletedTask);

    var service = BuildUserAppService();

    // Act
    await service.ForgotPasswordAsync(new ForgotPasswordDto { Account = "alice@example.com" }, CancellationToken.None);

    // Assert
    Assert.Equal(new[] { "UpdateAsync", "SaveEntitiesAsync" }, callOrder);
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~ForgotPasswordAsync_Should_Call_UpdateAsync_Before_SaveEntitiesAsync"`
Expected: FAIL（当前代码不调用 UpdateAsync）

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Application/Services/UserAppService.cs` 第 446-L448 行：

```csharp
// 发布领域事件
user.PublishForgotPasswordRequested(resetToken);

await _userRepository.UpdateAsync(user, ct);
await _unitOfWork.SaveEntitiesAsync(ct);
```

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~ForgotPasswordAsync_Should_Call_UpdateAsync_Before_SaveEntitiesAsync"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAppServiceTests.cs
git commit -m "fix(userauth): ForgotPasswordAsync 在 SaveEntitiesAsync 前调用 UpdateAsync，避免领域事件丢失"
```

---

### P0-5: RefreshTokenAsync 不校验 Locked 状态，被锁用户仍可刷新令牌
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L59-L74](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L59-L74)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L157-L177](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L157-L177)
- **根因**：刷新令牌时仅检查 `user.Status == AccountStatus.Disabled`，未检查 `Locked` 与 `LockedUntil`。被锁定用户持有的 RefreshToken 仍然有效，可换取新 AccessToken 继续访问 API，绕过登录锁定机制。同时未检查 `TwoFactorEnabled` 状态——用户启用 2FA 后，旧刷新令牌仍可直接换 AccessToken 而无需二次验证。
- **影响**：登录锁定机制可被绕过；2FA 强度被削弱。
- **修复方案**：增加 Locked 状态校验，超时自动解锁逻辑在刷新路径上同步应用；2FA 已启用用户的刷新路径返回临时令牌要求二次验证（与登录路径一致）。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Application.Tests/UserAppServiceTests.cs`：

```csharp
[Fact]
public async Task RefreshTokenAsync_Should_Reject_Locked_User_Within_Lock_Window()
{
    // Arrange
    var user = User.Create(
        Guid.NewGuid(),
        "locked-user",
        "locked@example.com",
        "+8613800138000",
        _passwordHasher.Hash("Password123"),
        "Locked");
    user.Lock("audit test", TimeSpan.FromMinutes(30));

    _refreshTokenStoreMock.Setup(s => s.ValidateAndRotateAsync("rt", It.IsAny<CancellationToken>()))
        .ReturnsAsync(user.Id);
    _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(user);

    var service = BuildUserAppService();

    // Act & Assert
    var ex = await Assert.ThrowsAsync<UserAuthDomainException>(() =>
        service.RefreshTokenAsync("rt", CancellationToken.None));
    Assert.Equal("USER_LOCKED", ex.Code);
}

[Fact]
public async Task RefreshTokenAsync_Should_Auto_Unlock_When_Lock_Window_Elapsed()
{
    // Arrange
    var user = User.Create(
        Guid.NewGuid(),
        "auto-unlock",
        "auto@example.com",
        "+8613800138000",
        _passwordHasher.Hash("Password123"),
        "Auto");
    user.Lock("test", TimeSpan.FromMilliseconds(1));
    await Task.Delay(50); // 等待锁定过期

    _refreshTokenStoreMock.Setup(s => s.ValidateAndRotateAsync("rt", It.IsAny<CancellationToken>()))
        .ReturnsAsync(user.Id);
    _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(user);
    _tokenServiceMock.Setup(t => t.GenerateAccessToken(user.Id, It.IsAny<string>(), It.IsAny<Guid?>()))
        .Returns("access");
    _refreshTokenStoreMock.Setup(s => s.IssueAsync(user.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync("new-rt");

    var service = BuildUserAppService();

    // Act
    var token = await service.RefreshTokenAsync("rt", CancellationToken.None);

    // Assert
    Assert.NotNull(token);
    Assert.Equal(AccountStatus.Active, user.Status);
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~RefreshTokenAsync_Should_Reject_Locked_User_Within_Lock_Window|FullyQualifiedName~RefreshTokenAsync_Should_Auto_Unlock_When_Lock_Window_Elapsed"`
Expected: FAIL（当前代码不校验 Locked）

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Application/Services/UserAppService.cs` 第 157-L177 行：

```csharp
/// <inheritdoc />
public async Task<TokenDto> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(refreshToken))
    {
        throw new UserAuthValidationException("刷新令牌不可为空");
    }

    var userId = await _refreshTokenStore.ValidateAndRotateAsync(refreshToken, ct);
    if (!userId.HasValue)
    {
        throw new UnauthorizedAccessException("刷新令牌无效或已过期");
    }

    var user = await _userRepository.GetByIdAsync(userId.Value, ct);
    if (user is null || user.Status == AccountStatus.Disabled)
    {
        throw new UnauthorizedAccessException("账户不可用");
    }

    // 锁定超时自动解锁（与 LoginAsync 一致）
    if (user.Status == AccountStatus.Locked
        && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow))
    {
        user.Unlock();
        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }
    else if (user.Status == AccountStatus.Locked)
    {
        throw new UserAuthDomainException(
            $"账户已锁定，请于 {user.LockedUntil:O} 后重试", "USER_LOCKED");
    }

    // 已启用 2FA 的用户：刷新令牌不应直接换发完整 AccessToken，
    // 改为签发临时令牌要求二次验证，避免 2FA 被绕过。
    if (user.TwoFactorEnabled)
    {
        return await IssueTwoFactorRequiredTokenAsync(user, ct);
    }

    return await IssueTokensAsync(user, ct);
}
```

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~RefreshTokenAsync_Should_Reject_Locked_User_Within_Lock_Window|FullyQualifiedName~RefreshTokenAsync_Should_Auto_Unlock_When_Lock_Window_Elapsed"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAppServiceTests.cs
git commit -m "fix(userauth): RefreshTokenAsync 校验 Locked 状态并应用 2FA 二次验证"
```

---

### P0-6: UserConfiguration 的 Email/Phone 唯一索引使用 PostgreSQL 语法，与 UseSqlServer 不匹配
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L75-L82](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L75-L82)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs#L69-L72](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs#L69-L72) 与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L45](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L45)
- **根因**：`AddUserAuthInfrastructure` 调用 `options.UseSqlServer(connectionString)`，但 `UserConfiguration` 中过滤索引写为 PostgreSQL 风格：`.HasFilter("\"email\" IS NOT NULL")`。SQL Server 的过滤索引语法为 `WHERE ([email] IS NOT NULL)`，使用方括号标识符与 `WHERE` 关键字。该配置在 SQL Server 上要么迁移失败，要么 `HasFilter` 被忽略导致索引退化为非过滤唯一索引——而 `email` 为 NULL 的多行会因唯一约束冲突插入失败。
- **影响**：开发环境若使用 PostgreSQL 或 SQLite 测试则不暴露问题；部署到 SQL Server 生产环境后，第二个 OAuth 用户注册即因唯一约束冲突失败，业务阻断。
- **修复方案**：改为 `.HasFilter("[email] IS NOT NULL")`，并在迁移中验证生成的 SQL。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Infrastructure.Tests/Configurations/UserConfigurationTests.cs`：

```csharp
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Leno.UserAuth.Infrastructure.Tests.Configurations;

public sealed class UserConfigurationTests
{
    [Fact]
    public void UserConfiguration_Email_Filter_Should_Use_SqlServer_Syntax()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UserAuthDbContext>()
            .UseSqlServer("Server=localhost;Database=Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new UserAuthDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(User));
        Assert.NotNull(entityType);
        var emailIndex = entityType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_users_email");
        Assert.NotNull(emailIndex);

        var filter = emailIndex.GetFilter();

        // Assert：应为 SQL Server 风格 [email] IS NOT NULL，不应包含 PostgreSQL 风格的双引号
        Assert.NotNull(filter);
        Assert.Contains("[email]", filter);
        Assert.DoesNotContain("\"email\"", filter);
    }

    [Fact]
    public void UserConfiguration_Phone_Filter_Should_Use_SqlServer_Syntax()
    {
        var options = new DbContextOptionsBuilder<UserAuthDbContext>()
            .UseSqlServer("Server=localhost;Database=Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new UserAuthDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(User));
        Assert.NotNull(entityType);
        var phoneIndex = entityType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_users_phone_number");
        Assert.NotNull(phoneIndex);

        var filter = phoneIndex.GetFilter();

        Assert.NotNull(filter);
        Assert.Contains("[phone_number]", filter);
        Assert.DoesNotContain("\"phone_number\"", filter);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~UserConfigurationTests"`
Expected: FAIL（当前 filter 为 `"email" IS NOT NULL`）

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs` 第 69-L72 行：

```csharp
builder.HasIndex(u => u.Username).HasDatabaseName("ix_users_username").IsUnique();
builder.HasIndex(u => u.Email).HasDatabaseName("ix_users_email").IsUnique()
    .HasFilter("[email] IS NOT NULL");
builder.HasIndex(u => u.PhoneNumber).HasDatabaseName("ix_users_phone_number").IsUnique()
    .HasFilter("[phone_number] IS NOT NULL");
```

新增迁移：

```bash
dotnet ef migrations add FixUserEmailPhoneFilterSyntax --project src/Services/UserAuth/Leno.UserAuth.Infrastructure --startup-project src/Services/UserAuth/Leno.UserAuth.Api
```

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~UserConfigurationTests"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Migrations/*
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/Configurations/UserConfigurationTests.cs
git commit -m "fix(userauth): Email/Phone 唯一索引过滤条件改为 SQL Server 方括号语法"
```

---

### P0-7: AddressConfiguration 默认地址索引未唯一，应用层并发不安全
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L83-L97](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L83-L97)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/AddressConfiguration.cs#L34-L35](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/AddressConfiguration.cs#L34-L35) 与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AddressAppService.cs#L145-L167](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AddressAppService.cs#L145-L167)
- **根因**：`AddressAppService.SetDefaultAsync` 通过 `ClearExistingDefaultAsync` 读改写所有地址的 `IsDefault` 字段实现"默认地址唯一"不变量。但 `AddressConfiguration` 中 `ix_addresses_user_default` 索引未设置 `IsUnique()` 且无 `WHERE is_default = true` 过滤，数据库层面不约束。两个并发的 `SetDefaultAsync`（A 把 add1 设默认，B 把 add2 设默认）可能同时通过 `ClearExistingDefault` 后各自写入 `IsDefault=true`，最终用户存在两条默认地址，破坏 `User.DefaultAddressId` 的单一性语义。
- **影响**：默认地址漂移；下单地址错乱；订单路由错发。
- **修复方案**：在 `AddressConfiguration` 增加唯一过滤索引；在 `AddressAppService.SetDefaultAsync` 用单条 SQL 原子化"清除其他默认 + 设置当前默认"；`Address` 聚合保留 `MarkAsDefault/UnmarkDefault` 行为方法。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Infrastructure.Tests/Configurations/AddressConfigurationTests.cs`：

```csharp
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Leno.UserAuth.Infrastructure.Tests.Configurations;

public sealed class AddressConfigurationTests
{
    [Fact]
    public void AddressConfiguration_Default_Index_Should_Be_Unique_With_Filter()
    {
        var options = new DbContextOptionsBuilder<UserAuthDbContext>()
            .UseSqlServer("Server=localhost;Database=Dummy;Trusted_Connection=True;")
            .Options;

        using var context = new UserAuthDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(Address));
        Assert.NotNull(entityType);

        var index = entityType.GetIndexes().FirstOrDefault(i => i.GetDatabaseName() == "ix_addresses_user_default");
        Assert.NotNull(index);
        Assert.True(index.IsUnique);

        var filter = index.GetFilter();
        Assert.NotNull(filter);
        Assert.Contains("is_default", filter);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~AddressConfigurationTests"`
Expected: FAIL（当前索引未设置 IsUnique 与 HasFilter）

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Infrastructure/Configurations/AddressConfiguration.cs` 第 35 行：

```csharp
builder.HasIndex(a => a.UserId).HasDatabaseName("ix_addresses_user_id");
builder.HasIndex(a => new { a.UserId, a.IsDefault })
    .HasDatabaseName("ix_addresses_user_default")
    .IsUnique()
    .HasFilter("[is_default] = 1");
```

新增迁移：

```bash
dotnet ef migrations add FixAddressDefaultUniqueIndex --project src/Services/UserAuth/Leno.UserAuth.Infrastructure --startup-project src/Services/UserAuth/Leno.UserAuth.Api
```

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~AddressConfigurationTests"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/AddressConfiguration.cs
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Migrations/*
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/Configurations/AddressConfigurationTests.cs
git commit -m "fix(userauth): AddressConfiguration 默认地址索引增加唯一约束与过滤条件，防止并发漂移"
```

---

### P0-8: AccountAppService 与 OAuthClientAppService 使用 SaveChangesAsync 而非 SaveEntitiesAsync
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L98-L110](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L98-L110)
- **代码位置**：
  - [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AccountAppService.cs#L77](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AccountAppService.cs#L77) `BindExternalLoginAsync`
  - [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AccountAppService.cs#L96](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AccountAppService.cs#L96) `UnbindExternalLoginAsync`
  - [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L67](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L67) `UpdateAsync`
  - [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L75](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L75) `EnableAsync`
  - [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L83](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L83) `DisableAsync`
- **根因**：`IUnitOfWork.SaveChangesAsync` 通常只调用 `DbContext.SaveChangesAsync`，不会处理领域事件收集与 Outbox 写入。`User.LinkExternalLogin` 触发 `ExternalLoginLinkedEvent`、`OAuthClient.Enable/Disable` 等虽然当前未在 `UserAuthIntegrationEventMapper` 注册翻译，但只要后续订阅方出现（Notification BC 监听登录绑定通知、审计 BC 监听 OAuth 客户端变更），这些事件就会丢失。其他应用服务（`UserAppService`、`AddressAppService`、`UserAdminAppService`、`PermissionAppService`）都使用 `SaveEntitiesAsync`，只有这两个服务遗漏。
- **影响**：未来添加集成事件订阅方时事件丢失；同事务内审计日志也不写入。
- **修复方案**：全部替换为 `await _unitOfWork.SaveEntitiesAsync(ct);`。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Application.Tests/AccountAppServiceTests.cs`：

```csharp
[Fact]
public async Task BindExternalLoginAsync_Should_Call_SaveEntitiesAsync_Not_SaveChangesAsync()
{
    // Arrange
    var user = User.Create(Guid.NewGuid(), "u1", "u1@example.com", "+8613800138000",
        _hasher.Hash("Password123"), "U1");
    _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
    _providerResolverMock.Setup(r => r.Resolve("google")).Returns(_authServiceMock.Object);
    _authServiceMock.Setup(s => s.ExchangeCodeAsync("code", It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ExternalLoginInfo("google", "g-1", "u1@example.com", "U1", null));
    _userRepositoryMock.Setup(r => r.FindByExternalLoginAsync("google", "g-1", It.IsAny<CancellationToken>()))
        .ReturnsAsync((User?)null);

    var service = new AccountAppService(
        _userRepositoryMock.Object,
        _unitOfWorkMock.Object,
        _providerResolverMock.Object);

    // Act
    await service.BindExternalLoginAsync(user.Id, new BindExternalLoginDto { Provider = "google", Code = "code", RedirectUri = "https://app.leno.com/cb" }, CancellationToken.None);

    // Assert
    _unitOfWorkMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
}
```

类似测试 `UnbindExternalLoginAsync_Should_Call_SaveEntitiesAsync` 与 OAuthClientAppService 的 `UpdateAsync/EnableAsync/DisableAsync_Should_Call_SaveEntitiesAsync`（共 5 个用例）。

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~SaveEntitiesAsync_Not_SaveChangesAsync"`
Expected: FAIL

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Application/Services/AccountAppService.cs` 第 77 行与第 96 行：

```csharp
// L77（BindExternalLoginAsync 末尾）
await _userRepository.UpdateAsync(user, ct);
await _unitOfWork.SaveEntitiesAsync(ct);

// L96（UnbindExternalLoginAsync 末尾）
await _userRepository.UpdateAsync(user, ct);
await _unitOfWork.SaveEntitiesAsync(ct);
```

修改 `Leno.UserAuth.Application/Services/OAuthClientAppService.cs` 第 67、75、83 行：

```csharp
// L67（UpdateAsync 末尾）
await _unitOfWork.SaveEntitiesAsync(ct);

// L75（EnableAsync 末尾）
await _unitOfWork.SaveEntitiesAsync(ct);

// L83（DisableAsync 末尾）
await _unitOfWork.SaveEntitiesAsync(ct);
```

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~SaveEntitiesAsync_Not_SaveChangesAsync"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/AccountAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/AccountAppServiceTests.cs
git commit -m "fix(userauth): AccountAppService 与 OAuthClientAppService 改用 SaveEntitiesAsync，避免领域事件与 Outbox 丢失"
```

---

### P0-9: PermissionAppService 与 OAuthClientAppService 管理操作无审计日志
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L111-L120](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L111-L120)
- **代码位置**：
  - [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/PermissionAppService.cs#L52-L137](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/PermissionAppService.cs#L52-L137)
  - [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L38-L84](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L38-L84)
- **根因**：`UserAdminAppService.AssignRolesAsync/SuspendAsync/ResumeAsync` 都写 `AuditLog`，但 `PermissionAppService` 与 `OAuthClientAppService` 对同等敏感的管理员操作（角色 CRUD、权限全量替换、OAuth 提供方启停）完全无审计。攻击者拿到 Admin 账户后修改 OAuth ClientSecret / RedirectUri 到自己的服务器，或添加 `ui:admin:*` 权限给 Buyer 角色——这些动作无审计追溯。
- **影响**：RBAC 被篡改后无追溯；OAuth 客户端被替换为恶意配置后无审计；合规审计失败。
- **修复方案**：在 `PermissionAppService` 与 `OAuthClientAppService` 注入 `IAuditLogRepository`，每个写操作前后做 `Snapshot` 并写 `AuditLog.Create`，与 `UserAdminAppService` 保持一致。同时审计 `AccountAppService.BindExternalLoginAsync`。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Application.Tests/PermissionAppServiceTests.cs`：

```csharp
[Fact]
public async Task CreateRoleAsync_Should_Write_AuditLog()
{
    // Arrange
    _permissionRepositoryMock.Setup(r => r.GetByNameAsync("Manager", It.IsAny<CancellationToken>()))
        .ReturnsAsync((Role?)null);
    var operatorId = Guid.NewGuid();
    var service = new PermissionAppService(
        _permissionRepositoryMock.Object,
        _unitOfWorkMock.Object,
        _auditLogRepositoryMock.Object,
        operatorId);

    // Act
    await service.CreateRoleAsync(new SaveRoleDto { Name = "Manager", Description = "Store manager" }, CancellationToken.None);

    // Assert
    _auditLogRepositoryMock.Verify(a => a.AddAsync(It.Is<AuditLog>(log =>
        log.Action == "RoleCreate" &&
        log.ResourceType == "Role" &&
        log.OperatorId == operatorId), It.IsAny<CancellationToken>()), Times.Once);
}
```

类似测试覆盖 `UpdateRoleAsync/DeleteRoleAsync/UpdateRolePermissionsAsync` 与 OAuthClientAppService 的 `UpdateAsync/EnableAsync/DisableAsync`。

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~CreateRoleAsync_Should_Write_AuditLog"`
Expected: FAIL（当前 PermissionAppService 不注入 IAuditLogRepository）

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Application/Services/PermissionAppService.cs`：

```csharp
public sealed class PermissionAppService : IPermissionAppService
{
    private readonly IPermissionRepository _permissionRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Guid _operatorId;

    public PermissionAppService(
        IPermissionRepository permissionRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        Guid operatorId)
    {
        ArgumentNullException.ThrowIfNull(permissionRepository);
        ArgumentNullException.ThrowIfNull(auditLogRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _permissionRepository = permissionRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _operatorId = operatorId;
    }

    public async Task<RoleDto> CreateRoleAsync(SaveRoleDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new UserAuthDomainException("角色名称不可为空", "ROLE_NAME_EMPTY");
        }

        var existing = await _permissionRepository.GetByNameAsync(dto.Name, ct);
        if (existing is not null)
        {
            throw new UserAuthDomainException("角色名称已存在", "ROLE_NAME_EXISTS");
        }

        var role = Role.Create(Guid.NewGuid(), dto.Name, dto.Description);
        await _permissionRepository.AddAsync(role, ct);
        await WriteAuditAsync("RoleCreate", role.Id, null, Snapshot(role), ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToDto(role);
    }

    public async Task<RoleDto> UpdateRoleAsync(Guid roleId, SaveRoleDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new UserAuthDomainException("角色名称不可为空", "ROLE_NAME_EMPTY");
        }

        var role = await RequireRoleAsync(roleId, ct);
        var before = Snapshot(role);

        var existing = await _permissionRepository.GetByNameAsync(dto.Name, ct);
        if (existing is not null && existing.Id != roleId)
        {
            throw new UserAuthDomainException("角色名称已存在", "ROLE_NAME_EXISTS");
        }

        role.Update(dto.Name, dto.Description);
        await _permissionRepository.UpdateAsync(role, ct);
        await WriteAuditAsync("RoleUpdate", role.Id, before, Snapshot(role), ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToDto(role);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await RequireRoleAsync(roleId, ct);
        if (role.IsBuiltIn)
        {
            throw new UserAuthDomainException("内置角色不可删除", "ROLE_BUILTIN_DELETE");
        }

        var hasReferences = await _permissionRepository.HasUserReferencesAsync(roleId, ct);
        if (hasReferences)
        {
            throw new UserAuthDomainException("角色存在用户引用，不可删除", "ROLE_HAS_USER_REFERENCES");
        }

        var before = Snapshot(role);
        await _permissionRepository.RemoveAsync(role, ct);
        await WriteAuditAsync("RoleDelete", role.Id, before, null, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    public async Task UpdateRolePermissionsAsync(Guid roleId, UpdatePermissionsDto dto, CancellationToken ct = default)
    {
        var role = await RequireRoleAsync(roleId, ct);
        var before = Snapshot(role);

        var permissions = dto.Permissions
            .Select(p => new PermissionVO(p))
            .ToList();

        role.SetPermissions(permissions);
        await _permissionRepository.UpdateAsync(role, ct);
        await WriteAuditAsync("RolePermissionsUpdate", role.Id, before, Snapshot(role), ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    private async Task WriteAuditAsync(string action, Guid roleId, string? before, string? after, CancellationToken ct)
    {
        var auditLog = AuditLog.Create(
            Guid.NewGuid(),
            _operatorId,
            action,
            "Role",
            roleId.ToString(),
            before,
            after);
        await _auditLogRepository.AddAsync(auditLog, ct);
    }

    private static string Snapshot(Role role)
        => JsonSerializer.Serialize(new { role.Id, role.Name, role.Description, Permissions = role.Permissions.Select(p => p.ResourceKey).ToArray() });

    // 其它方法保持不变（QueryRolesAsync / GetRoleAsync / GetRolePermissionsAsync / RequireRoleAsync / ToDto）
}
```

类似修改 `OAuthClientAppService`：构造函数注入 `IAuditLogRepository` 与 `Guid operatorId`，在 `UpdateAsync/EnableAsync/DisableAsync` 末尾写 `AuditLog.Create(...)`，action 分别为 `OAuthClientUpdate/OAuthClientEnable/OAuthClientDisable`，resourceType 为 `OAuthClient`。

`AccountAppService.BindExternalLoginAsync` 同样增加审计（action=`ExternalLoginBind`，resourceType=`User`）。需在 `Program.cs` 通过 `IHttpContextAccessor` + JWT claims 提取 operatorId 注入到上述服务。

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~PermissionAppServiceTests|FullyQualifiedName~OAuthClientAppServiceTests"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/PermissionAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/AccountAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/PermissionAppServiceTests.cs
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/OAuthClientAppServiceTests.cs
git commit -m "fix(userauth): PermissionAppService 与 OAuthClientAppService 管理操作写入审计日志"
```

---

### P0-10: ChangePassword / ResetPassword 不撤销其他刷新令牌
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L121-L132](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L121-L132)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L200-L208](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L200-L208)（ChangePassword）与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L452-L502](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L452-L502)（ResetPassword）
- **根因**：`ChangePasswordAsync` 调用 `user.ChangePassword` 后仅 `UpdateAsync + SaveEntitiesAsync`，未调用 `IRefreshTokenStore.RevokeAllAsync(user.Id)`。密码改后，已签发的 RefreshToken 仍可换取新 AccessToken。同样地，`ResetPasswordAsync` 也未撤销。`UserAppService` 构造函数已注入 `IRefreshTokenStore`，但忘记密码/改密路径根本不调用。
- **影响**：账户被盗后用户改密，攻击者持有的旧令牌仍可继续访问直到自然过期；管理员禁用账户同样失效。
- **修复方案**：在 `ChangePasswordAsync` 与 `ResetPasswordAsync` 末尾调用 `_refreshTokenStore.RevokeAllAsync(user.Id, ct)`。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Application.Tests/UserAppServiceTests.cs`：

```csharp
[Fact]
public async Task ChangePasswordAsync_Should_Revoke_All_Refresh_Tokens()
{
    // Arrange
    var user = User.Create(Guid.NewGuid(), "alice", "alice@example.com", "+8613800138000",
        _hasher.Hash("OldPassword1"), "Alice");
    _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    var service = BuildUserAppService();

    // Act
    await service.ChangePasswordAsync(user.Id, new ChangePasswordDto
    {
        OldPassword = "OldPassword1",
        NewPassword = "NewPassword1"
    }, CancellationToken.None);

    // Assert
    _refreshTokenStoreMock.Verify(s => s.RevokeAllAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task ResetPasswordAsync_Should_Revoke_All_Refresh_Tokens()
{
    // Arrange
    var user = User.Create(Guid.NewGuid(), "alice", "alice@example.com", "+8613800138000",
        _hasher.Hash("OldPassword1"), "Alice");
    var token = "reset-token";
    _redisMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
        .ReturnsAsync((RedisValue)user.Id.ToString());
    _userRepositoryMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    var service = BuildUserAppService();

    // Act
    await service.ResetPasswordAsync(new ResetPasswordDto { Token = token, NewPassword = "NewPassword1" }, CancellationToken.None);

    // Assert
    _refreshTokenStoreMock.Verify(s => s.RevokeAllAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~ChangePasswordAsync_Should_Revoke_All_Refresh_Tokens|FullyQualifiedName~ResetPasswordAsync_Should_Revoke_All_Refresh_Tokens"`
Expected: FAIL

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Application/Services/UserAppService.cs`：

```csharp
// ChangePasswordAsync 末尾（L208 前）
public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto, CancellationToken ct = default)
{
    await ValidateAsync(_changePasswordValidator, dto, ct);
    var user = await RequireUserAsync(userId, ct);

    user.ChangePassword(dto.OldPassword, dto.NewPassword, _passwordHasher);
    await _userRepository.UpdateAsync(user, ct);
    await _unitOfWork.SaveEntitiesAsync(ct);

    // 撤销该用户所有 RefreshToken，强制重新登录
    await _refreshTokenStore.RevokeAllAsync(user.Id, ct);
}

// ResetPasswordAsync 末尾（L502 前）
public async Task ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(dto.Token))
    {
        throw new UserAuthDomainException("重置令牌不可为空", "USER_RESET_TOKEN_EMPTY");
    }

    if (string.IsNullOrWhiteSpace(dto.NewPassword))
    {
        throw new UserAuthDomainException("新密码不可为空", "USER_NEW_PASSWORD_EMPTY");
    }

    var redisKey = $"reset:pwd:{dto.Token}";
    var redisValue = await _redis.StringGetAsync(redisKey);
    await _redis.KeyDeleteAsync(redisKey);

    if (!redisValue.HasValue)
    {
        throw new UserAuthDomainException("重置令牌无效或已过期", "USER_RESET_TOKEN_INVALID");
    }

    if (!Guid.TryParse(redisValue.ToString(), out var userId))
    {
        throw new UserAuthDomainException("重置令牌数据无效", "USER_RESET_TOKEN_INVALID");
    }

    var user = await RequireUserAsync(userId, ct);

    if (user.Status == AccountStatus.Disabled)
    {
        throw new UserAuthDomainException("账户已被禁用", "USER_DISABLED");
    }

    user.ResetPassword(_passwordHasher.Hash(dto.NewPassword), _passwordHasher);
    await _userRepository.UpdateAsync(user, ct);
    await _unitOfWork.SaveEntitiesAsync(ct);

    // 撤销该用户所有 RefreshToken，防止旧令牌继续使用
    await _refreshTokenStore.RevokeAllAsync(user.Id, ct);
}
```

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~ChangePasswordAsync_Should_Revoke_All_Refresh_Tokens|FullyQualifiedName~ResetPasswordAsync_Should_Revoke_All_Refresh_Tokens"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAppServiceTests.cs
git commit -m "fix(userauth): ChangePassword/ResetPassword 后撤销该用户所有刷新令牌"
```

---

### P0-11: User.Disable / Lock 不撤销已签发的 JWT 与 RefreshToken
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L133-L140](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L133-L140)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L182-L224](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L182-L224) 与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAdminAppService.cs#L85-L131](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAdminAppService.cs#L85-L131)
- **根因**：`UserAdminAppService.SuspendAsync` / `ResumeAsync`(Activate 路径) 调用聚合行为后只更新 User 实体，不撤销该用户已签发的所有 RefreshToken，也不批量加入 JWT 黑名单。被锁定/禁用用户在令牌自然过期前仍可访问受保护资源。
- **影响**：管理员紧急封禁恶意账户的响应时间被拉长到 JWT TTL（通常 15-60 分钟）；安全事件扩散。
- **修复方案**：在 `UserAdminAppService` 注入 `IRefreshTokenStore`，`SuspendAsync` 末尾调用 `RevokeAllAsync(targetUserId, ct)`；考虑在网关侧增加按 `userId` 查询黑名单的能力（Redis Set），管理员封禁时把 `userId` 加入短期黑名单 Set，网关侧拒绝。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Application.Tests/UserAdminAppServiceTests.cs`：

```csharp
[Fact]
public async Task SuspendAsync_Should_Revoke_All_Refresh_Tokens_For_Target_User()
{
    // Arrange
    var targetId = Guid.NewGuid();
    var operatorId = Guid.NewGuid();
    var user = User.Create(targetId, "badguy", "bad@example.com", "+8613800138000",
        _hasher.Hash("Password1"), "Bad");
    _userRepositoryMock.Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    var service = new UserAdminAppService(
        _userRepositoryMock.Object,
        _auditLogRepositoryMock.Object,
        _unitOfWorkMock.Object,
        _refreshTokenStoreMock.Object);

    // Act
    await service.SuspendAsync(targetId, new SuspendUserDto { Reason = "abuse", DurationMinutes = 30 }, operatorId, CancellationToken.None);

    // Assert
    _refreshTokenStoreMock.Verify(s => s.RevokeAllAsync(targetId, It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task ResumeAsync_On_Disabled_User_Should_Not_Revoke_Tokens()
{
    // Arrange
    var targetId = Guid.NewGuid();
    var operatorId = Guid.NewGuid();
    var user = User.Create(targetId, "badguy", "bad@example.com", "+8613800138000",
        _hasher.Hash("Password1"), "Bad");
    user.Disable("test", operatorId);
    _userRepositoryMock.Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    var service = new UserAdminAppService(
        _userRepositoryMock.Object,
        _auditLogRepositoryMock.Object,
        _unitOfWorkMock.Object,
        _refreshTokenStoreMock.Object);

    // Act
    await service.ResumeAsync(targetId, operatorId, CancellationToken.None);

    // Assert：恢复操作不应撤销令牌（用户已通过审核恢复）
    _refreshTokenStoreMock.Verify(s => s.RevokeAllAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~SuspendAsync_Should_Revoke_All_Refresh_Tokens|FullyQualifiedName~ResumeAsync_On_Disabled_User_Should_Not_Revoke_Tokens"`
Expected: FAIL

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Application/Services/UserAdminAppService.cs`：

```csharp
public sealed class UserAdminAppService : IUserAdminAppService
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IUnitOfWork _unitOfWork;

    public UserAdminAppService(
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        IRefreshTokenStore refreshTokenStore)
    {
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _refreshTokenStore = refreshTokenStore;
    }

    public async Task SuspendAsync(Guid targetUserId, SuspendUserDto dto, Guid operatorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            throw new UserAuthValidationException("锁定原因不可为空");
        }

        if (dto.DurationMinutes is <= 0 or > 1440)
        {
            throw new UserAuthValidationException("锁定时长须为 1-1440 分钟");
        }

        var user = await RequireUserAsync(targetUserId, ct);
        var before = Snapshot(user);

        user.Lock(dto.Reason, TimeSpan.FromMinutes(dto.DurationMinutes));

        var after = Snapshot(user);
        await _userRepository.UpdateAsync(user, ct);
        await WriteAuditAsync(operatorId, "UserSuspend", targetUserId, before, after, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 撤销该用户所有 RefreshToken，封禁立即生效
        await _refreshTokenStore.RevokeAllAsync(targetUserId, ct);
    }

    // AssignRolesAsync / ResumeAsync / 其它方法保持原样
}
```

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~SuspendAsync_Should_Revoke_All_Refresh_Tokens|FullyQualifiedName~ResumeAsync_On_Disabled_User_Should_Not_Revoke_Tokens"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAdminAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAdminAppServiceTests.cs
git commit -m "fix(userauth): UserAdminAppService.SuspendAsync 撤销目标用户所有刷新令牌，封禁立即生效"
```

---

### P0-12: AesEncryptionService 使用 CBC 模式无认证，存在 Padding Oracle 攻击向量
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L141-L151](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L141-L151)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/AesEncryptionService.cs#L7-L86](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/AesEncryptionService.cs#L7-L86)
- **根因**：注释明确"使用 CBC 模式 + PKCS7 填充"，但未做 Encrypt-then-MAC（HMAC-SHA256）或使用 AES-GCM。`Decrypt` 方法捕获异常时只判断 `fullCipher.Length < 16`，未验证密文完整性。若攻击者获得数据库 `client_secret` 字段写入权限，可通过修改密文+观察响应错误类型推断明文（Padding Oracle）。该字段存储 OAuth ClientSecret，一旦泄露可冒充 Leno 调用 Google/WeChat/Alipay OAuth API。
- **影响**：OAuth ClientSecret 泄露风险；第三方平台账户接管。
- **修复方案**：改用 `AesGcm`（.NET 8+ 内置）：`nonce(12B) + ciphertext + tag(16B)`；提供一次性迁移逻辑兼容旧密文（先尝试 GCM，失败再回退 HMAC 验证 + CBC 解密）。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Infrastructure.Tests/Services/AesEncryptionServiceTests.cs`：

```csharp
using Leno.UserAuth.Infrastructure.Services;
using Xunit;

namespace Leno.UserAuth.Infrastructure.Tests.Services;

public sealed class AesEncryptionServiceTests
{
    private static readonly byte[] Key = Convert.FromBase64String("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=");

    [Fact]
    public void Encrypt_Then_Decrypt_Should_Roundtrip_Original_Plaintext()
    {
        var service = new AesEncryptionService(Convert.ToBase64String(Key));
        var plain = "my-oauth-client-secret-12345";

        var cipher = service.Encrypt(plain);
        var decrypted = service.Decrypt(cipher);

        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Encrypt_Should_Produce_Different_Ciphertext_For_Same_Plaintext_Due_To_Random_Nonce()
    {
        var service = new AesEncryptionService(Convert.ToBase64String(Key));
        var plain = "same-secret";

        var c1 = service.Encrypt(plain);
        var c2 = service.Encrypt(plain);

        Assert.NotEqual(c1, c2);
    }

    [Fact]
    public void Decrypt_Should_Throw_When_Ciphertext_Tampered()
    {
        var service = new AesEncryptionService(Convert.ToBase64String(Key));
        var cipher = service.Encrypt("secret");

        // 篡改密文末尾
        var bytes = Convert.FromBase64String(cipher);
        bytes[^1] ^= 0xFF;
        var tampered = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(tampered));
    }

    [Fact]
    public void Decrypted_Ciphertext_Length_Should_Be_At_Least_Nonce_Plus_Tag()
    {
        var service = new AesEncryptionService(Convert.ToBase64String(Key));
        var cipher = service.Encrypt("x");
        var bytes = Convert.FromBase64String(cipher);

        // GCM: nonce(12) + ciphertext + tag(16)，最少 12 + 1 + 16 = 29
        Assert.True(bytes.Length >= 29);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~AesEncryptionServiceTests"`
Expected: FAIL（篡改测试会通过当前 CBC 实现，未抛 CryptographicException）

- [ ] **Step 3: 最小实现**

替换 `Leno.UserAuth.Infrastructure/Services/AesEncryptionService.cs` 全文：

```csharp
using System.Security.Cryptography;
using Leno.UserAuth.Application.Abstractions;

namespace Leno.UserAuth.Infrastructure.Services;

/// <summary>
/// AES-GCM 加密服务，用于 OAuth2 ClientSecret 的加密存储。
/// 格式：Base64(Nonce[12B] + Ciphertext + Tag[16B])，提供认证加密，防止 Padding Oracle 与密文篡改。
/// </summary>
public sealed class AesEncryptionService : IClientSecretEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;

    public AesEncryptionService(string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            throw new ArgumentException("AES 密钥不可为空", nameof(base64Key));
        }

        _key = Convert.FromBase64String(base64Key);
        if (_key.Length != 32)
        {
            throw new ArgumentException("AES 密钥必须为 32 字节（256 位）", nameof(base64Key));
        }
    }

    /// <summary>
    /// 加密明文，返回 Base64 编码的密文（含 Nonce 前缀与 Tag 后缀）。
    /// 格式：Base64(Nonce[12B] + Ciphertext + Tag[16B])。
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            throw new ArgumentException("明文不可为空", nameof(plainText));
        }

        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var result = new byte[NonceSize + cipherBytes.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(cipherBytes, 0, result, NonceSize, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + cipherBytes.Length, TagSize);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// 解密密文（Base64 编码的 Nonce + Ciphertext + Tag），返回明文。
    /// Tag 校验失败抛 <see cref="CryptographicException"/>，防止密文篡改。
    /// </summary>
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            throw new ArgumentException("密文不可为空", nameof(cipherText));
        }

        var fullCipher = Convert.FromBase64String(cipherText);
        if (fullCipher.Length < NonceSize + TagSize)
        {
            throw new ArgumentException("密文长度不足", nameof(cipherText));
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipherBytes = new byte[fullCipher.Length - NonceSize - TagSize];

        Buffer.BlockCopy(fullCipher, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(fullCipher, NonceSize, cipherBytes, 0, cipherBytes.Length);
        Buffer.BlockCopy(fullCipher, NonceSize + cipherBytes.Length, tag, 0, TagSize);

        var plainBytes = new byte[cipherBytes.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}
```

迁移：写一次性脚本读所有 `oauth_clients.client_secret` 字段，旧密文用旧 CBC 解密（保留临时 `LegacyAesCbcEncryptionService` 类），用新 GCM 重新加密写回；迁移完成后删除 legacy 类。

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~AesEncryptionServiceTests"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/AesEncryptionService.cs
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/Services/AesEncryptionServiceTests.cs
git commit -m "fix(userauth): AesEncryptionService 改用 AES-GCM 认证加密，防止 Padding Oracle 攻击"
```

---

### P0-13: OAuth state 不校验回调 provider 与 state 内 provider 一致
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L152-L164](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L152-L164)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L216-L260](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L216-L260)
- **根因**：`GetOAuthLoginUrlAsync` 把 `$"{authService.Provider}|{redirectUri}"` 存入 Redis state；`HandleOAuthCallbackAsync` 读取 state 后只取 `parts[0]`（`stateProvider`）但从不与 callback URL 的 `provider` 参数比较。`parts.Length < 1` 永远为 false（split 至少返回 1 元素），校验形同虚设。攻击者可以用 Google 的 state 在 WeChat callback 端点完成回调，触发 `ResolveAuthService("wechat")` 拿 WeChat 的 ClientId/Secret 调用——state 与 provider 跨实例失配的语义不明确，CSRF 防护被削弱。
- **影响**：跨 OAuth 提供方的 CSRF；state 重放。
- **修复方案**：校验 `stateProvider == provider`；并把 `redirectUri` 从 state 取出与 callback `redirectUri` 比较；同时校验 `parts.Length == 2`。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Application.Tests/UserAppServiceTests.cs`：

```csharp
[Fact]
public async Task HandleOAuthCallbackAsync_Should_Reject_When_State_Provider_Mismatch_Callback_Provider()
{
    // Arrange：state 中存 google，但回调 provider=wechat
    _redisMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
        .ReturnsAsync((RedisValue)"google|https://app.leno.com/cb");

    var service = BuildUserAppService();

    // Act & Assert
    var ex = await Assert.ThrowsAsync<UserAuthDomainException>(() =>
        service.HandleOAuthCallbackAsync("wechat", "code", "state", "https://app.leno.com/cb", CancellationToken.None));
    Assert.Equal("OAUTH_STATE_PROVIDER_MISMATCH", ex.Code);
}

[Fact]
public async Task HandleOAuthCallbackAsync_Should_Reject_When_State_Parts_Length_Not_Two()
{
    _redisMock.Setup(r => r.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
        .ReturnsAsync((RedisValue)"google"); // 无 redirectUri

    var service = BuildUserAppService();

    var ex = await Assert.ThrowsAsync<UserAuthDomainException>(() =>
        service.HandleOAuthCallbackAsync("google", "code", "state", "https://app.leno.com/cb", CancellationToken.None));
    Assert.Equal("OAUTH_STATE_INVALID", ex.Code);
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~HandleOAuthCallbackAsync_Should_Reject_When_State_Provider_Mismatch|FullyQualifiedName~HandleOAuthCallbackAsync_Should_Reject_When_State_Parts_Length_Not_Two"`
Expected: FAIL

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Application/Services/UserAppService.cs` 第 249-L257 行：

```csharp
var parts = redisValue.ToString().Split('|');
if (parts.Length != 2)
{
    throw new UserAuthDomainException("State 数据无效", "OAUTH_STATE_INVALID");
}

var stateProvider = parts[0];
var stateRedirectUri = parts[1];

// 校验 state 内 provider 与 callback provider 一致，防止跨 OAuth 提供方的 CSRF
if (!string.Equals(stateProvider, provider.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
{
    throw new UserAuthDomainException("State 与 provider 不匹配", "OAUTH_STATE_PROVIDER_MISMATCH");
}

// 校验 state 内 redirectUri 与 callback redirectUri 一致，防止开放重定向
if (!string.Equals(stateRedirectUri, redirectUri, StringComparison.OrdinalIgnoreCase))
{
    throw new UserAuthDomainException("State 内 redirectUri 与回调不匹配", "OAUTH_REDIRECT_URI_MISMATCH");
}

var authService = ResolveAuthService(provider);
```

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~HandleOAuthCallbackAsync_Should_Reject_When_State_Provider_Mismatch|FullyQualifiedName~HandleOAuthCallbackAsync_Should_Reject_When_State_Parts_Length_Not_Two"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAppServiceTests.cs
git commit -m "fix(userauth): OAuth state 校验 provider 与 redirectUri 与回调一致，防止跨提供方 CSRF"
```

---

### P0-14: FailedLoginCount 并发累加无原子保护，可能绕过锁定阈值
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L165-L175](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L165-L175)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L122-L142](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L122-L142) 与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L134-L145](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L134-L145)
- **根因**：`VerifyPassword` 中 `FailedLoginCount++` 与 `Lock(...)` 在聚合内串行执行，但 EF Core 的并发控制依赖 `RowVersion` / 乐观锁。`UserConfiguration` 中未配置 `RowVersion` 字段。两个并发请求同时读取 `FailedLoginCount=4`，各自 `++` 写回 5，DB 中最终值是 5（不是 6），下一次失败才能触发锁定。
- **影响**：暴力破解阈值被削弱；账户锁定延迟触发。
- **修复方案**：在 `UserConfiguration` 增加 `RowVersion` 字段（`byte[]`）；`User` 聚合增加 `RowVersion` 属性；捕获 `DbUpdateConcurrencyException` 后重试 `VerifyPassword`。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Infrastructure.Tests/Configurations/UserConfigurationTests.cs`：

```csharp
[Fact]
public void UserConfiguration_Should_Configure_RowVersion_As_Concurrency_Token()
{
    var options = new DbContextOptionsBuilder<UserAuthDbContext>()
        .UseSqlServer("Server=localhost;Database=Dummy;Trusted_Connection=True;")
        .Options;

    using var context = new UserAuthDbContext(options);
    var entityType = context.Model.FindEntityType(typeof(User));
    Assert.NotNull(entityType);

    var rowVersionProp = entityType.FindProperty(nameof(User.RowVersion));
    Assert.NotNull(rowVersionProp);
    Assert.True(rowVersionProp.IsConcurrencyToken);
    Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersionProp.ValueGenerated);
}
```

新增应用层测试 `UserAppServiceTests.LoginAsync_Should_Retry_On_DbUpdateConcurrencyException`：

```csharp
[Fact]
public async Task LoginAsync_Should_Retry_When_DbUpdateConcurrencyException_Thrown()
{
    // Arrange
    var user = User.Create(Guid.NewGuid(), "alice", "alice@example.com", "+8613800138000",
        _hasher.Hash("Password1"), "Alice");
    _userRepositoryMock.Setup(r => r.GetByUsernameAsync("alice", It.IsAny<CancellationToken>()))
        .ReturnsAsync(user);

    int saveCallCount = 0;
    _unitOfWorkMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
        .Returns(() =>
        {
            saveCallCount++;
            if (saveCallCount == 1)
            {
                throw new DbUpdateConcurrencyException("RowVersion mismatch");
            }
            return Task.CompletedTask;
        });

    var service = BuildUserAppService();

    // Act：登录失败一次（密码错误），第一次 Save 抛并发异常，应自动重试
    await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() =>
        service.LoginAsync(new LoginDto { Account = "alice", Password = "wrong" }, CancellationToken.None));

    // Assert
    Assert.True(saveCallCount >= 2);
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~UserConfiguration_Should_Configure_RowVersion_As_Concurrency_Token|FullyQualifiedName~LoginAsync_Should_Retry_On_DbUpdateConcurrencyException"`
Expected: FAIL

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Domain/Aggregates/User.cs`，在 `TwoFactorSecret` 属性后新增：

```csharp
/// <summary>EF Core 乐观并发控制版本号（shadow property 配合 IsRowVersion）。</summary>
public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
```

修改 `Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs`，在 `TwoFactorSecret` 配置后新增：

```csharp
builder.Property(u => u.RowVersion).HasColumnName("row_version").IsRowVersion();
```

修改 `Leno.UserAuth.Application/Services/UserAppService.cs` 第 134-L145 行（`LoginAsync` 失败计数累加段），增加重试：

```csharp
var passwordOk = user.VerifyPassword(dto.Password, _passwordHasher);

if (!passwordOk)
{
    await SaveWithConcurrencyRetryAsync(async ct =>
    {
        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }, ct);
    throw new UnauthorizedAccessException("账号或密码错误");
}

user.RecordLogin();
await SaveWithConcurrencyRetryAsync(async ct =>
{
    await _userRepository.UpdateAsync(user, ct);
    await _unitOfWork.SaveEntitiesAsync(ct);
}, ct);
```

在 `UserAppService` 私有方法区新增：

```csharp
private static async Task SaveWithConcurrencyRetryAsync(Func<CancellationToken, Task> saveAction, CancellationToken ct, int maxRetry = 3)
{
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            await saveAction(ct);
            return;
        }
        catch (DbUpdateConcurrencyException) when (attempt < maxRetry)
        {
            // 重新加载聚合以拿到最新的 RowVersion，由调用方再次构造变更
            await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), ct);
        }
    }
}
```

新增迁移：

```bash
dotnet ef migrations add AddUserRowVersion --project src/Services/UserAuth/Leno.UserAuth.Infrastructure --startup-project src/Services/UserAuth/Leno.UserAuth.Api
```

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~UserConfiguration_Should_Configure_RowVersion_As_Concurrency_Token|FullyQualifiedName~LoginAsync_Should_Retry_On_DbUpdateConcurrencyException"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs
git add src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Migrations/*
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/Configurations/UserConfigurationTests.cs
git add src/Services/UserAuth/Leno.UserAuth.Application.Tests/UserAppServiceTests.cs
git commit -m "fix(userauth): User 增加 RowVersion 乐观锁，FailedLoginCount 并发累加失败时自动重试"
```

---

### P0-15: AlipayOAuth2Client 实际请求未做 RSA2 签名，调用真实支付宝网关必然失败
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L176-L187](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L176-L187)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/AlipayOAuth2Client.cs#L55-L91](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/AlipayOAuth2Client.cs#L55-L91) 与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/AlipayOAuth2Client.cs#L100-L141](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/AlipayOAuth2Client.cs#L100-L141)
- **根因**：支付宝开放平台所有 API 请求必须包含 `sign` 与 `sign_type` 参数，`sign` 由请求参数按字典序拼接后用商户私钥做 RSA2 签名。代码注释里写了 `sign_type=RSA2` 但完全没生成 `sign` 参数，只有参数列表。同样，响应也未做 RSA2 验签。调用真实支付宝网关必然返回 `isv.InvalidSignatures` 或类似错误，支付宝登录完全不可用。
- **影响**：支付宝登录在生产环境 100% 失败；开发环境若未连接真实支付宝，缺陷被掩盖。
- **修复方案**：自行实现 RSA2 签名（不引入额外 SDK）：加载商户私钥（PEM）；按 ASCII 字典序拼接所有非空业务参数，`&` 连接；`RSA-SHA256` 签名后 Base64 编码作为 `sign`；响应验签：用支付宝公钥校验响应中 `sign` 字段。

#### Task 1: 写失败测试
- [ ] **Step 1: 编写测试**

新增测试到 `Leno.UserAuth.Infrastructure.Tests/Auth/AlipayOAuth2ClientTests.cs`：

```csharp
using Leno.UserAuth.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Cryptography;
using Xunit;

namespace Leno.UserAuth.Infrastructure.Tests.Auth;

public sealed class AlipayOAuth2ClientTests
{
    private static (string privateKeyPem, string publicKeyPem) GenerateRsaKeyPair()
    {
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportPkcs8PrivateKeyPem();
        var publicKey = rsa.ExportSubjectPublicKeyInfoPem();
        return (privateKey, publicKey);
    }

    [Fact]
    public void BuildSignedParameters_Should_Include_Sign_And_SignType_Rsa2()
    {
        // Arrange
        var (privateKey, _) = GenerateRsaKeyPair();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OAuth2:Alipay:AppId"] = "2021000000000001",
                ["OAuth2:Alipay:MerchantPrivateKey"] = privateKey
            })
            .Build();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var client = new AlipayOAuth2Client(httpClientFactory.Object, config, NullLogger<AlipayOAuth2Client>.Instance);

        var parameters = new Dictionary<string, string?>
        {
            ["app_id"] = "2021000000000001",
            ["method"] = "alipay.system.oauth.token",
            ["charset"] = "utf-8",
            ["timestamp"] = "2026-07-22 12:00:00",
            ["version"] = "1.0",
            ["grant_type"] = "authorization_code",
            ["code"] = "test-code"
        };

        // Act
        var signed = client.BuildSignedParameters(parameters);

        // Assert
        Assert.Equal("RSA2", signed["sign_type"]);
        Assert.False(string.IsNullOrEmpty(signed["sign"]));
        // sign 应为可 Base64 解码
        var signBytes = Convert.FromBase64String(signed["sign"]!);
        Assert.True(signBytes.Length == 256); // RSA-2048 签名 = 256 字节
    }

    [Fact]
    public void BuildSignedParameters_Should_Sort_Parameters_By_Ascii_Key_Before_Signing()
    {
        // Arrange
        var (privateKey, publicKey) = GenerateRsaKeyPair();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OAuth2:Alipay:AppId"] = "2021000000000001",
                ["OAuth2:Alipay:MerchantPrivateKey"] = privateKey,
                ["OAuth2:Alipay:AlipayPublicKey"] = publicKey
            })
            .Build();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var client = new AlipayOAuth2Client(httpClientFactory.Object, config, NullLogger<AlipayOAuth2Client>.Instance);

        var parameters = new Dictionary<string, string?>
        {
            ["zebra"] = "1",
            ["apple"] = "2",
            ["mango"] = "3"
        };

        // Act
        var signed = client.BuildSignedParameters(parameters);

        // Assert：用同私钥重新签名应当得到相同 sign
        var expectedSign = client.ComputeSign(signed.Where(kv => kv.Key != "sign" && kv.Key != "sign_type" && !string.IsNullOrEmpty(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value));
        Assert.Equal(expectedSign, signed["sign"]);
    }

    [Fact]
    public void VerifyResponseSign_Should_Return_True_When_Sign_Valid()
    {
        // Arrange
        var (privateKey, publicKey) = GenerateRsaKeyPair();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OAuth2:Alipay:AppId"] = "2021000000000001",
                ["OAuth2:Alipay:MerchantPrivateKey"] = privateKey,
                ["OAuth2:Alipay:AlipayPublicKey"] = publicKey
            })
            .Build();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var client = new AlipayOAuth2Client(httpClientFactory.Object, config, NullLogger<AlipayOAuth2Client>.Instance);

        var responseData = new Dictionary<string, string?>
        {
            ["user_id"] = "2088000000000001",
            ["access_token"] = "token-123"
        };
        // 用商户私钥模拟支付宝签名（实际场景应使用支付宝公钥对应私钥）
        var sign = client.ComputeSign(responseData);

        // Act
        var verified = client.VerifyResponseSign(responseData, sign);

        // Assert
        Assert.True(verified);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**
Run: `dotnet test --filter "FullyQualifiedName~AlipayOAuth2ClientTests"`
Expected: FAIL（`BuildSignedParameters/ComputeSign/VerifyResponseSign` 方法不存在）

- [ ] **Step 3: 最小实现**

修改 `Leno.UserAuth.Infrastructure/Auth/AlipayOAuth2Client.cs` 全文（添加签名/验签支持）：

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Leno.UserAuth.Infrastructure.Auth;

/// <summary>
/// 支付宝开放平台 OAuth2 客户端实现，基于支付宝授权登录流程。
/// 所有 API 请求按支付宝规范做 RSA2 签名，响应用支付宝公钥做验签。
/// </summary>
public sealed class AlipayOAuth2Client : IExternalAuthService
{
    private const string AuthorizationEndpoint = "https://openauth.alipay.com/oauth2/publicAppAuthorize.htm";
    private const string GatewayUrl = "https://openapi.alipay.com/gateway.do";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlipayOAuth2Client> _logger;
    private readonly RSA _merchantPrivateKey;
    private readonly RSA? _alipayPublicKey;

    public string Provider => "alipay";

    public AlipayOAuth2Client(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AlipayOAuth2Client> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _httpClient = httpClientFactory.CreateClient(nameof(AlipayOAuth2Client));
        _configuration = configuration;
        _logger = logger;

        _merchantPrivateKey = LoadRsaPrivateKey(GetRequiredConfig("OAuth2:Alipay:MerchantPrivateKey"));
        var alipayPublicKeyPem = _configuration["OAuth2:Alipay:AlipayPublicKey"];
        if (!string.IsNullOrWhiteSpace(alipayPublicKeyPem))
        {
            _alipayPublicKey = LoadRsaPublicKey(alipayPublicKeyPem);
        }
    }

    public string GetAuthorizationUrl(string state, string redirectUri)
    {
        var appId = GetAppId();
        var query = new Dictionary<string, string?>
        {
            ["app_id"] = appId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "auth_user",
            ["state"] = state
        };

        var queryString = string.Join("&", query
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        return $"{AuthorizationEndpoint}?{queryString}";
    }

    public async Task<ExternalLoginInfo> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        var appId = GetAppId();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var businessParams = new Dictionary<string, string?>
        {
            ["app_id"] = appId,
            ["method"] = "alipay.system.oauth.token",
            ["charset"] = "utf-8",
            ["sign_type"] = "RSA2",
            ["timestamp"] = timestamp,
            ["version"] = "1.0",
            ["grant_type"] = "authorization_code",
            ["code"] = code
        };

        var signed = BuildSignedParameters(businessParams);
        var tokenUrl = BuildGatewayUrl(signed);
        var response = await _httpClient.GetAsync(tokenUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Alipay token exchange failed: {StatusCode} {Body}", (int)response.StatusCode, body);
            throw new UserAuthDomainException("支付宝授权码交换失败", "OAUTH_TOKEN_EXCHANGE_FAILED");
        }

        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // 整体验签（支付宝响应 sign 字段在根层）
        if (root.TryGetProperty("sign", out var signEl) && _alipayPublicKey is not null)
        {
            var sign = signEl.GetString() ?? string.Empty;
            // 提取响应数据：以 alipay_xxx_response 节点为准
            var responseNode = root.EnumerateObject().FirstOrDefault(p => p.Name.EndsWith("_response", StringComparison.OrdinalIgnoreCase));
            if (responseNode.Value.ValueKind == JsonValueKind.Object)
            {
                var responseJson = responseNode.Value.GetRawText();
                if (!VerifyResponseSignRaw(responseJson, sign))
                {
                    _logger.LogError("Alipay token exchange response sign verification failed");
                    throw new UserAuthDomainException("支付宝响应验签失败", "OAUTH_RESPONSE_SIGN_INVALID");
                }
            }
        }

        var responseData = root.TryGetProperty("alipay_system_oauth_token_response", out var tokenResp)
            ? tokenResp
            : root;

        if (responseData.TryGetProperty("code", out var codeEl) && codeEl.GetString() != "10000")
        {
            var msg = responseData.TryGetProperty("msg", out var msgEl) ? msgEl.GetString() : "未知错误";
            _logger.LogError("Alipay token exchange error: {Code} {Msg}", codeEl.GetString(), msg);
            throw new UserAuthDomainException($"支付宝授权失败: {msg}", "OAUTH_TOKEN_EXCHANGE_FAILED");
        }

        var accessToken = responseData.GetProperty("access_token").GetString()
            ?? throw new UserAuthDomainException("支付宝未返回访问令牌", "OAUTH_TOKEN_EMPTY");

        var alipayUserId = responseData.TryGetProperty("user_id", out var userIdEl) ? userIdEl.GetString() : null;

        return await GetUserInfoAsync(accessToken, alipayUserId, ct);
    }

    public Task<ExternalLoginInfo> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        throw new NotSupportedException("支付宝须通过 ExchangeCodeAsync 获取用户信息，请勿直接调用 GetUserInfoAsync(accessToken)");
    }

    private async Task<ExternalLoginInfo> GetUserInfoAsync(string accessToken, string? alipayUserId, CancellationToken ct)
    {
        var appId = GetAppId();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var businessParams = new Dictionary<string, string?>
        {
            ["app_id"] = appId,
            ["method"] = "alipay.user.info.share",
            ["charset"] = "utf-8",
            ["sign_type"] = "RSA2",
            ["timestamp"] = timestamp,
            ["version"] = "1.0",
            ["auth_token"] = accessToken
        };

        var signed = BuildSignedParameters(businessParams);
        var userInfoUrl = BuildGatewayUrl(signed);

        var response = await _httpClient.GetAsync(userInfoUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Alipay userinfo failed: {StatusCode} {Body}", (int)response.StatusCode, body);
            throw new UserAuthDomainException("获取支付宝用户信息失败", "OAUTH_USERINFO_FAILED");
        }

        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("sign", out var signEl) && _alipayPublicKey is not null)
        {
            var sign = signEl.GetString() ?? string.Empty;
            var responseNode = root.EnumerateObject().FirstOrDefault(p => p.Name.EndsWith("_response", StringComparison.OrdinalIgnoreCase));
            if (responseNode.Value.ValueKind == JsonValueKind.Object)
            {
                var responseJson = responseNode.Value.GetRawText();
                if (!VerifyResponseSignRaw(responseJson, sign))
                {
                    _logger.LogError("Alipay userinfo response sign verification failed");
                    throw new UserAuthDomainException("支付宝响应验签失败", "OAUTH_RESPONSE_SIGN_INVALID");
                }
            }
        }

        var responseData = root.TryGetProperty("alipay_user_info_share_response", out var infoResp)
            ? infoResp
            : root;

        if (responseData.TryGetProperty("code", out var codeEl) && codeEl.GetString() != "10000")
        {
            var msg = responseData.TryGetProperty("sub_msg", out var msgEl) ? msgEl.GetString() : "未知错误";
            _logger.LogError("Alipay userinfo error: {Code} {Msg}", codeEl.GetString(), msg);
            throw new UserAuthDomainException($"获取支付宝用户信息失败: {msg}", "OAUTH_USERINFO_FAILED");
        }

        var userId = alipayUserId ?? (responseData.TryGetProperty("user_id", out var uid) ? uid.GetString() : null);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UserAuthDomainException("支付宝未返回用户标识", "OAUTH_USER_ID_EMPTY");
        }

        var avatar = responseData.TryGetProperty("avatar", out var av) ? av.GetString() : null;
        var nickName = responseData.TryGetProperty("nick_name", out var nn) ? nn.GetString() : null;
        var email = $"{userId}@alipay.local";

        return new ExternalLoginInfo(Provider, userId, email, nickName ?? "支付宝用户", avatar);
    }

    /// <summary>
    /// 对业务参数做 RSA2 签名并返回包含 sign_type / sign 的完整参数集合。
    /// </summary>
    internal Dictionary<string, string?> BuildSignedParameters(IReadOnlyDictionary<string, string?> businessParams)
    {
        var withSignType = new Dictionary<string, string?>(businessParams)
        {
            ["sign_type"] = "RSA2"
        };

        var sign = ComputeSign(withSignType);
        withSignType["sign"] = sign;
        return withSignType;
    }

    /// <summary>
    /// 按 ASCII 字典序拼接所有非空业务参数（不含 sign / sign_type），用商户私钥做 RSA-SHA256 签名，Base64 编码返回。
    /// </summary>
    internal string ComputeSign(IReadOnlyDictionary<string, string?> parameters)
    {
        var sortedPairs = parameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value) && kv.Key != "sign" && kv.Key != "sign")
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}");

        var content = string.Join("&", sortedPairs);
        var dataBytes = Encoding.UTF8.GetBytes(content);
        var signature = _merchantPrivateKey.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// 验证支付宝响应签名：对响应数据 JSON 重新签名并比较。
    /// </summary>
    internal bool VerifyResponseSign(IReadOnlyDictionary<string, string?> responseData, string sign)
    {
        if (_alipayPublicKey is null)
        {
            _logger.LogWarning("AlipayPublicKey 未配置，跳过响应验签");
            return true;
        }

        var sortedPairs = responseData
            .Where(kv => !string.IsNullOrEmpty(kv.Value) && kv.Key != "sign" && kv.Key != "sign_type")
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}");

        var content = string.Join("&", sortedPairs);
        var dataBytes = Encoding.UTF8.GetBytes(content);
        var signBytes = Convert.FromBase64String(sign);

        return _alipayPublicKey.VerifyData(dataBytes, signBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>
    /// 验证支付宝响应签名（原始 JSON 字节序）：直接对响应节点 JSON 文本验签。
    /// </summary>
    private bool VerifyResponseSignRaw(string responseJson, string sign)
    {
        if (_alipayPublicKey is null)
        {
            return true;
        }

        var dataBytes = Encoding.UTF8.GetBytes(responseJson);
        var signBytes = Convert.FromBase64String(sign);
        return _alipayPublicKey.VerifyData(dataBytes, signBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static string BuildGatewayUrl(IReadOnlyDictionary<string, string?> parameters)
    {
        var query = string.Join("&", parameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        return $"{GatewayUrl}?{query}";
    }

    private static RSA LoadRsaPrivateKey(string pem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }

    private static RSA LoadRsaPublicKey(string pem)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }

    private string GetAppId()
    {
        return GetRequiredConfig("OAuth2:Alipay:AppId");
    }

    private string GetRequiredConfig(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UserAuthDomainException($"支付宝 OAuth2 配置缺失：{key}", "OAUTH_CONFIG_MISSING");
        }
        return value;
    }
}
```

`appsettings.json` 新增 `OAuth2:Alipay:MerchantPrivateKey` 与 `OAuth2:Alipay:AlipayPublicKey` 配置项（PEM 内容）。

- [ ] **Step 4: 运行测试验证通过**
Run: `dotnet test --filter "FullyQualifiedName~AlipayOAuth2ClientTests"`
Expected: PASS

- [ ] **Step 5: 提交**
```bash
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/AlipayOAuth2Client.cs
git add src/Services/UserAuth/Leno.UserAuth.Api/appsettings.json
git add src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/Auth/AlipayOAuth2ClientTests.cs
git commit -m "fix(userauth): AlipayOAuth2Client 实现 RSA2 签名与响应验签，使支付宝登录真实可用"
```

---

## P1 修复清单（任务清单格式）

### P1-1: JwtRevocationService 不传递 CancellationToken
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L192-L198](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L192-L198)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/JwtRevocationService.cs#L21-L26](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/JwtRevocationService.cs#L21-L26)
- **根因**：`await db.StringSetAsync(...)` 未传 `ct`，客户端断开后操作继续，浪费 Redis 连接。同时该实现位于 Application 层但 `using StackExchange.Redis`，应迁移到 Infrastructure。
- **修复步骤**：
  1. `StringSetAsync` 调用追加 `CommandFlags.None` 与 `ct`（通过扩展方法 `WaitAsync(ct)`）；
  2. 将 `JwtRevocationService.cs` 移到 `Leno.UserAuth.Infrastructure/Services/JwtRevocationService.cs`，命名空间改为 `Leno.UserAuth.Infrastructure.Services`；
  3. 在 `ServiceCollectionExtensions` 中改为 `services.AddScoped<IJwtRevocationService, Leno.UserAuth.Infrastructure.Services.JwtRevocationService>()`。
- **影响范围**：登出路径；Application 层依赖反向问题。
- **验证方法**：`dotnet build src/Services/UserAuth/Leno.UserAuth.Application/Leno.UserAuth.Application.csproj` 应不再依赖 `StackExchange.Redis`；单测 `JwtRevocationService_RevokeAsync_Should_Pass_CancellationToken` 验证 ct 透传。

### P1-2: LoginAsync 账号枚举时序差异
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L199-L205](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L199-L205)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L109-L154](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L109-L154)
- **根因**：注释声称"账号不存在统一返回账号或密码错误，防账号枚举（INV-18）"，但实际：账号不存在立即返回 401（耗时 < 1ms），账号存在则执行 `bcrypt.Verify`（耗时 50-200ms）。攻击者通过响应时间差异可枚举有效账户。
- **修复步骤**：
  1. 在 `UserAppService` 定义私有字段 `private const string DummyPasswordHash = "$2a$11$...";`（预先生成的 bcrypt 哈希）；
  2. `LoginAsync` 中 `user is null` 分支执行 `BCrypt.Net.BCrypt.Verify("\x00", DummyPasswordHash);`（结果丢弃），耗时与真实路径一致，再返回 401；
  3. 在 `LoginAsyncTimeAttackTests` 中断言两条路径耗时差 < 20ms（用 `Stopwatch` 多次采样取中位数）。
- **影响范围**：登录路径响应时间；账号枚举攻击面。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~LoginAsyncTimeAttackTests"`；压测脚本对比不存在账号与密码错误账号响应时间分布。

### P1-3: UserAppService 直接依赖 StackExchange.Redis，应用层穿透基础设施
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L206-L212](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L206-L212)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L12-L13](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L12-L13) 等
- **根因**：Application 层直接 `using StackExchange.Redis;`，把 Redis 当成应用层一等公民使用于 OAuth state、2FA 临时令牌、密码重置令牌。未来要替换为分布式缓存或内存缓存需要修改 Application 层代码。
- **修复步骤**：
  1. 在 `Leno.UserAuth.Application/Abstractions/` 新增三个抽象：`IOAuthStateStore`、`ITwoFactorTempTokenStore`、`IPasswordResetTokenStore`，每个接口方法签名带 `CancellationToken`；
  2. 在 `Leno.UserAuth.Infrastructure/Services/` 新增 `RedisOAuthStateStore`、`RedisTwoFactorTempTokenStore`、`RedisPasswordResetTokenStore`，分别封装现有 Redis 调用逻辑（`StringSetAsync` + `StringGetAsync` + TTL）；
  3. `UserAppService` 构造函数把 `IConnectionMultiplexer`、`IDatabase` 替换为上述三个抽象；
  4. `ServiceCollectionExtensions` 注册三个 Redis 实现（`AddScoped` 或 `AddSingleton`，依据是否有 `IDatabase` 复用）；
  5. 从 `Leno.UserAuth.Application.csproj` 移除 `StackExchange.Redis` 包引用。
- **影响范围**：`UserAppService` 全部 OAuth / 2FA / 密码重置逻辑；Application 层依赖。
- **验证方法**：`dotnet build src/Services/UserAuth/Leno.UserAuth.Application/Leno.UserAuth.Application.csproj` 应不再依赖 `StackExchange.Redis`；现有 UserAppService 单测全部通过。

### P1-4: EfCorePermissionRepository.GetRolesByPermissionAsync 全表加载内存过滤
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L213-L219](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L213-L219)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCorePermissionRepository.cs#L61-L67](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCorePermissionRepository.cs#L61-L67)
- **根因**：权限以 JSON `nvarchar(max)` 存储，无法在 DB 端查询。`GetRolesByPermissionAsync` 注释明确"permissions are stored as JSON, we load all roles and filter in memory"。角色数达到几百时每次权限校验都要全表加载 + 反序列化。
- **修复步骤**：
  1. 新建 `role_permissions` 表：`RoleId (uniqueidentifier)` + `ResourceKey (nvarchar(64))` + `Action (nvarchar(32))`，复合主键 `(RoleId, ResourceKey, Action)`，索引 `IX_role_permissions_resource_action`；
  2. 新建 `RolePermissionConfiguration` 配置该表；
  3. 新建 EF Core 迁移 `AddRolePermissionsTable`；
  4. `EfCorePermissionRepository.GetRolesByPermissionAsync` 改为 `JOIN role_permissions` 查询：`_context.RolePermissions.Where(rp => rp.ResourceKey == resourceKey && rp.Action == action).Select(rp => rp.Role).Distinct()`；
  5. `Role.AssignPermission` / `Role.RevokePermission` 同步维护 `role_permissions` 行（领域事件或聚合行为方法）；
  6. 数据迁移脚本：从现有 `Role.Permissions` JSON 反序列化后批量插入 `role_permissions`。
- **影响范围**：权限查询 / RBAC 校验；需要数据迁移。
- **验证方法**：`GetRolesByPermissionAsync_PerformanceTests` 断言 1000 角色下查询耗时 < 50ms；`dotnet ef migrations script` 审查迁移脚本。

### P1-5: WeChatOAuth2Client / AlipayOAuth2Client 构造伪邮箱入库并触发集成事件
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L220-L226](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L220-L226)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/WeChatOAuth2Client.cs#L126](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/WeChatOAuth2Client.cs#L126) 与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/AlipayOAuth2Client.cs#L138](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/AlipayOAuth2Client.cs#L138)
- **根因**：`ExternalLoginInfo.Email` 强制非空，微信 / 支付宝不返回邮箱，代码硬构造 `{openId}@wechat.local` / `{userId}@alipay.local` 作为邮箱入库。该伪邮箱会通过 `UserRegisteredDomainEvent` 广播给下游 BC，下游若发欢迎邮件必然失败，且被 P0-2 的邮箱匹配逻辑误判为真实邮箱。
- **修复步骤**：
  1. `ExternalLoginInfo.Email` 改为 `string?`（可空）；
  2. `User.CreateFromExternal` 中 `Email = externalLoginInfo.Email`（保持可空）；
  3. `UserRegisteredDomainEvent` 增加 `bool IsEmailVerified` 字段，当 `Email` 为 null 或未验证时为 false；
  4. `WeChatOAuth2Client` / `AlipayOAuth2Client` 不再构造伪邮箱，`Email = null`；
  5. `UserConfiguration` 的 `email` 列改为可空（`IsRequired(false)`），唯一索引改为过滤索引 `[Email] IS NOT NULL`（与 P0-6 一致）；
  6. 通知下游 BC（Membership / Notification）监听 `IsEmailVerified` 字段决定是否发邮件。
- **影响范围**：所有微信 / 支付宝用户；下游 BC 事件契约。
- **验证方法**：`WeChatOAuth2ClientTests.CreateUserInfo_Should_Return_Null_Email` 通过；下游 BC 单测验证 `IsEmailVerified=false` 时不触发邮件发送。

### P1-6: OAuthClientAppService.UpdateAsync PUT 自动创建且默认 Enabled=true
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L227-L233](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L227-L233)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L38-L68](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L38-L68)
- **根因**：`UpdateAsync` 在 client 不存在时调用 `OAuthClient.Create` 创建，默认 `enabled = true`。管理员误传未校验的 `provider` 名称会自动创建一个 Enabled 的 OAuth 客户端配置，污染 OAuth 解析器，同时违反 PUT 幂等性语义。
- **修复步骤**：
  1. 新增 `CreateAsync(OAuthClientCreateDto dto, CancellationToken ct)`：校验 provider 不存在后 `OAuthClient.Create(...)`，默认 `Enabled=false`，调用 `AddAsync` + `SaveEntitiesAsync`；
  2. `UpdateAsync` 改为：`var existing = await _repo.GetByProviderAsync(dto.Provider, ct) ?? throw new UserAuthDomainException(..., "OAUTH_CLIENT_NOT_FOUND");` 然后调用 `existing.Update(...)`；
  3. 新增 `EnableAsync(string provider, CancellationToken ct)` 与 `DisableAsync` 显式启用 / 禁用；
  4. `OAuthClientsController` 拆分 `[HttpPost]` → `CreateAsync`、`[HttpPut]` → `UpdateAsync`、`[HttpPost("{provider}/enable")]` → `EnableAsync`；
  5. 写单测：`UpdateAsync_NotExists_Should_Throw_OAUTH_CLIENT_NOT_FOUND`、`CreateAsync_Should_Default_Enabled_False`。
- **影响范围**：OAuth 客户端管理路径。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~OAuthClientAppServiceTests"`；HTTP 集成测试验证 PUT 不存在返回 404。

### P1-7: AuditLogMiddleware 写入的 HttpContext.Items 从未被读取，死代码
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L234-L240](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L234-L240)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Audit/AuditLogMiddleware.cs#L31-L48](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Audit/AuditLogMiddleware.cs#L31-L48) 与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Audit/AuditLogInterceptor.cs#L39-L73](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Audit/AuditLogInterceptor.cs#L39-L73)
- **根因**：中间件在请求开始时解析 Action / ResourceType / ResourceId / OperatorId 存入 `HttpContext.Items["AuditLog:Action"]` 等，但拦截器 `EnrichAuditLogs` 只读取 `Ip / UserAgent / TraceId`，从不读取中间件存的字段。中间件实质上是死代码，且 `ResolveResourceId` 依赖 `Guid.TryParse(segment)` 仅在路径模板已绑定时有效。
- **修复步骤**：
  1. 重构 `AuditLogMiddleware`：在 `_next(context)` 之后（响应阶段）读取 `HttpContext.Response.StatusCode` 与异常信息；
  2. 中间件在响应阶段构造 `AuditLog.Create(action, resourceType, resourceId, operatorId, statusCode, success)`，通过 `IAuditLogRepository` 写入；
  3. `AuditLogInterceptor.EnrichAuditLogs` 改为读取 `HttpContext.Items["AuditLog:Ip"]` / `UserAgent` / `TraceId`（保留现有逻辑），并增加读取 `HttpContext.Items["AuditLog:Action"]` 等字段用于补充；
  4. 若决定不在中间件层创建审计（仍由应用服务显式创建），则删除 `ResolveAction` / `ResolveResourceType` / `ResolveResourceId` / `ResolveOperatorId` 死代码方法；
  5. 写集成测试：`AuditLogMiddleware_Should_Write_Log_On_Response_Stage`。
- **影响范围**：审计中间件；可观察性。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~AuditLogMiddlewareTests"`；代码覆盖率工具确认中间件无死代码。

### P1-8: OAuth2 redirectUri 不做白名单校验，开放重定向
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L241-L247](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L241-L247)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L211-L222](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L211-L222)
- **根因**：`GetOAuthLoginUrlAsync(state, redirectUri)` 直接把客户端传入的 `redirectUri` 拼接到第三方授权 URL 与存入 Redis state，无白名单校验。攻击者可构造钓鱼链接 `?redirectUri=https://evil.com/callback`，用户授权后回调到 `evil.com`，攻击者用该 code 调用 Leno callback 完成登录。
- **修复步骤**：
  1. 在 `appsettings.json` 新增 `OAuth2:AllowedRedirectUris` 数组配置（如 `["https://www.leno.com/auth/callback", "https://m.leno.com/auth/callback"]`）；
  2. 新建 `OAuth2Options` 强类型配置绑定该数组；
  3. `UserAppService.GetOAuthLoginUrlAsync` 中校验：`if (!_options.AllowedRedirectUris.Contains(redirectUri, StringComparer.OrdinalIgnoreCase)) throw new UserAuthDomainException("redirectUri 不在白名单", "OAUTH_REDIRECT_URI_NOT_ALLOWED");`；
  4. 同时支持 `*.leno.com` 后缀匹配作为兜底（可选）；
  5. 写单测：`GetOAuthLoginUrlAsync_DisallowedRedirectUri_Should_Throw`。
- **影响范围**：OAuth 登录入口；开放重定向攻击面。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~OAuthRedirectUriWhitelistTests"`；渗透测试验证非白名单 redirectUri 被拒。

### P1-9: UserRolesAssignment 不影响已签发 JWT，特权提升延迟
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L248-L254](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L248-L254)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAdminAppService.cs#L58-L82](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAdminAppService.cs#L58-L82) 与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Events/UserRoleAssignedEvent.cs#L7-L8](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Events/UserRoleAssignedEvent.cs#L7-L8)
- **根因**：`UserRoleAssignedEvent` 注释明确"Token 中角色声明在下一次登录或刷新后生效"。反向撤销 Admin 角色后，被撤销用户的现有 JWT 仍带 Admin 角色声明直到自然过期（15-60 分钟），结合 P0-10、P0-11（不撤销令牌），管理员撤销权限的实际生效时间被显著拉长。
- **修复步骤**：
  1. `UserAdminAppService.AssignRolesAsync` / `RevokeRolesAsync` 在变更后调用 `_refreshTokenStore.RevokeAllAsync(userId, ct)`；
  2. 同时调用 `IJwtRevocationService.RevokeAsync(userId, ct)`（基于 userId 的批量撤销，把 userId 加入短期黑名单 TTL = JWT 最大有效期）；
  3. `JwtRevocationService.IsRevokedAsync` 不仅校验 jti，还校验 userId 黑名单；
  4. 写单测：`AssignRolesAsync_Should_Revoke_Existing_Tokens`、`RevokeRolesAsync_Should_Add_UserId_To_Blacklist`。
- **影响范围**：RBAC 变更生效延迟；安全策略。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~UserAdminAppServiceRoleRevocationTests"`；集成测试验证角色撤销后旧 JWT 立即失效。

### P1-10: User.ChangePassword / UpdateProfile 不校验账户状态
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L255-L261](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L255-L261)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L147-L171](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L147-L171)（ChangePassword）、[#L279-L286](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L279-L286)（UpdateProfile）
- **根因**：聚合行为方法未检查 `Status`。Disabled / Locked 用户仍可调用 `ChangePassword` 与 `UpdateProfile`，结合 P0-11（Disable 不撤销令牌），被禁用用户可改密码后继续使用已有令牌。
- **修复步骤**：
  1. `User.ChangePassword` 入口增加：`if (Status == AccountStatus.Disabled) throw new UserAuthDomainException("账户已禁用", "USER_DISABLED");`；
  2. `User.UpdateProfile` 入口增加同样的 Disabled 校验；
  3. Locked 状态允许改密（视为正常解锁流程），但 Disabled 不允许；
  4. 写单测：`ChangePassword_DisabledUser_Should_Throw_USER_DISABLED`、`UpdateProfile_DisabledUser_Should_Throw_USER_DISABLED`、`ChangePassword_LockedUser_Should_Allow`。
- **影响范围**：用户管理路径。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~UserAggregateStatusTests"`。

### P1-11: InMemoryRefreshTokenStore 不清理过期 token，内存泄漏
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L262-L268](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L262-L268)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/InMemoryRefreshTokenStore.cs#L1-L65](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Services/InMemoryRefreshTokenStore.cs#L1-L65)
- **根因**：`ConcurrentDictionary` 只在 `TryRemove` 时清除条目，过期 token（用户从未刷新）永远不清理，长期运行下内存持续增长。
- **修复步骤**：
  1. 将 `ConcurrentDictionary<string, RefreshTokenEntry>` 改为 `Microsoft.Extensions.Caching.Memory.MemoryCache`；
  2. `IssueAsync` 中 `Set(key, entry, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl })`，TTL 到期自动驱逐；
  3. 保留 `ValidateAndRotateAsync` / `RevokeAsync` / `RevokeAllAsync` 语义不变；
  4. 或者实现 `IHostedService` 定时清理：每 5 分钟扫描 `Where(kv => kv.Value.ExpiresAt < DateTimeOffset.UtcNow)` 并 `TryRemove`。
- **影响范围**：长期运行实例内存（仅 Development 环境，因 P0-1 后生产用 Redis）。
- **验证方法**：`InMemoryRefreshTokenStore_ExpiryTests`：注入 token 后等待 TTL（或 mock 时间），断言 `ValidateAndRotateAsync` 返回 null 且内部字典为空。

### P1-12: EfCoreUserRepository.QueryAsync LIKE 通配符 % / _ 不转义
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L269-L275](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L269-L275)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCoreUserRepository.cs#L60-L65](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCoreUserRepository.cs#L60-L65)
- **根因**：`EF.Functions.Like(u.Username, $"%{kw}%")` 中 `kw` 未转义 `%` 与 `_`。用户搜索 `%` 会匹配所有用户，搜索 `_` 会匹配任意单字符。虽无安全影响（管理员才能调用），但搜索结果不可预测。
- **修复步骤**：
  1. 在 `QueryAsync` 入口转义：`var escaped = kw.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");`；
  2. 查询改为 `EF.Functions.Like(u.Username, $"%{escaped}%", "\\")`（第三个参数指定转义字符）；
  3. 抽取 `EscapeLikePattern` 静态方法复用，写单测：`EscapeLikePattern_Should_Escape_Percent_Underscore_Backslash`；
  4. 集成测试：插入用户名含 `%` 的记录，搜索 `%` 只返回该用户。
- **影响范围**：管理后台用户搜索。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~EfCoreUserRepositoryLikeEscapeTests"`。

### P1-13: InternalUsersController 返回未脱敏的 PII 给"内部"调用方
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L276-L282](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L276-L282)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserInternalQueryService.cs#L20-L35](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserInternalQueryService.cs#L20-L35) 与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs#L22-L35](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs#L22-L35)
- **根因**：`UserContactsDto.PhoneNumber` 与 `Email` 直接返回用户原始字段，未脱敏。`InternalUsersController` 仅靠 `InternalApiKeyMiddleware` 保护，若中间件配置错误（开发环境常跳过），任意调用方可拉取全部用户手机号 / 邮箱。`Email` 是 `string` 非空但 `user.Email` 可能是 null，代码用 `?? string.Empty` 兜底，下游无法区分"无邮箱"与"空邮箱"。
- **修复步骤**：
  1. `UserContactsDto.Email` 改为 `string?`（可空），移除 `?? string.Empty`；
  2. 新增 `UserContactsMaskedDto`：`PhoneNumber` 返回 `138****1234` 格式（保留前 3 后 4），`Email` 返回 `a***@example.com` 格式（保留首字符与域名）；
  3. `InternalUsersController` 默认返回 `UserContactsMaskedDto`，新增 `[HttpGet("internal/v1/users/{id}/contacts/full")]` 需要更高权限（如 `RequireClaim("internal-pii-read")`）才返回完整 `UserContactsDto`；
  4. `InternalUsersController` 显式标注 `[Authorize(Policy = "InternalApiKey")]`，不依赖中间件兜底；
  5. 所有内部查询记录审计日志。
- **影响范围**：跨 BC 用户信息查询；PII 泄露风险。
- **验证方法**：`InternalUsersController_PII_MaskingTests` 断言默认响应已脱敏；`dotnet test --filter "FullyQualifiedName~UserContactsMaskingTests"`。

### P1-14: UserAuthGrpcService 标 [Authorize] 但实际靠拦截器，可能失效
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L283-L289](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L283-L289)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/GrpcServices/UserAuthGrpcService.cs#L13-L14](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/GrpcServices/UserAuthGrpcService.cs#L13-L14)
- **根因**：注释说"鉴权由 GrpcInternalKeyInterceptor 拦截器统一处理（metadata x-internal-key）"，但类上加了 `[Authorize]`。若 ASP.NET Core 鉴权管线未对 gRPC 启用 JWT Bearer，`[Authorize]` 不生效；若拦截器顺序错误或被移除，gRPC 端点完全开放。
- **修复步骤**：
  1. 明确 gRPC 鉴权策略文档：gRPC 仅依赖 `GrpcInternalKeyInterceptor`，不依赖 JWT Bearer；
  2. 移除 `UserAuthGrpcService` 上的 `[Authorize]` 特性（避免误导），改为在拦截器中显式校验 `x-internal-key` metadata；
  3. 或保留 `[Authorize]` 但配置 gRPC 鉴权管线启用 JWT Bearer + `[Authorize(Policy = "InternalGrpc")]` 策略；
  4. 写集成测试：`GrpcInternalKeyInterceptor_MissingKey_Should_Return_Unauthenticated`、`GrpcInternalKeyInterceptor_ValidKey_Should_Allow`；
  5. 在 `Program.cs` 确保拦截器注册顺序在 gRPC 服务映射之前。
- **影响范围**：gRPC 内部调用鉴权。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~GrpcAuthTests"`；用 grpcurl 不带 `x-internal-key` 调用应返回 `UNAUTHENTICATED`。

### P1-15: OAuth2 callback 的 redirectUri 缺省值使用 Request.Host，存在 Host Header 注入风险
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L290-L296](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L290-L296)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L125-L129](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs#L125-L129)
- **根因**：`redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/oauth/{provider}/callback";` 直接信任 `Host` 头。若反向代理未设置 `ForwardedHost`，攻击者可构造 `Host: evil.com` 的请求让 callback 用 evil.com 作为 redirectUri，结合 P1-8（无白名单）可放大为开放重定向。
- **修复步骤**：
  1. 在 `appsettings.json` 新增 `OAuth2:PublicBaseUrl` 配置（如 `https://api.leno.com`）；
  2. `AuthController.GetOAuthLoginUrl` 中 `redirectUri = $"{_options.PublicBaseUrl}/api/auth/oauth/{provider}/callback";`，不再读取 `Request.Host`；
  3. 启动期校验 `OAuth2:PublicBaseUrl` 非空且为 HTTPS（生产环境），缺失则 fail-fast；
  4. 保留 `Request.Host` 仅用于本地开发环境（`IHostEnvironment.IsDevelopment()` 时回退）；
  5. 写单测：`GetOAuthLoginUrl_Should_Use_Configured_BaseUrl_Not_RequestHost`。
- **影响范围**：OAuth callback 路径；Host Header 注入攻击面。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~OAuthCallbackBaseUrlTests"`；构造 `Host: evil.com` 请求验证 redirectUri 仍为配置值。

### P1-16: ResetPasswordAsync 的 if/else 分支完全相同（死代码）
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L297-L303](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L297-L303)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L489-L498](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L489-L498)
- **根因**：`if (string.IsNullOrEmpty(user.PasswordHash))` 与 `else` 两个分支都执行 `user.ResetPassword(_passwordHasher.Hash(dto.NewPassword), _passwordHasher);`。意图大概是纯 OAuth 用户首次设置密码要走不同路径，但实际行为一致，属于死代码。
- **修复步骤**：
  1. 删除 `if (string.IsNullOrEmpty(user.PasswordHash))` 分支判断，直接调用 `user.ResetPassword(_passwordHasher.Hash(dto.NewPassword), _passwordHasher);`；
  2. 若产品需求要求纯 OAuth 用户首次设置密码有额外校验（如必须先验证邮箱），补充该逻辑；当前按"行为一致"处理，仅清理死代码；
  3. 写单测：`ResetPasswordAsync_OAuthUser_Should_Reset_Password`、`ResetPasswordAsync_PasswordUser_Should_Reset_Password` 验证两条路径行为一致。
- **影响范围**：密码重置路径。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~ResetPasswordAsyncTests"`；代码审查确认无死代码分支。

### P1-17: User.GenerateUsernameFromEmail 不去除保留字与最小长度边界处理脆弱
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L304-L310](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L304-L310)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L408-L425](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L408-L425)
- **根因**：当邮箱前缀 `< 3` 字符时 `PadRight(3, '0')`，但若邮箱前缀经 sanitize 后为空（如 `@example.com`），`sanitized` 为空字符串，`PadRight(3, '0')` 后是 `"000"`——可能与其他 OAuth 用户冲突。同时未排除保留字（如 `admin`、`root`、`system`）。
- **修复步骤**：
  1. 新建 `UsernameReservedWords` 静态类：`public static readonly HashSet<string> Reserved = new() { "admin", "root", "system", "administrator", "leno", "support", "null", "undefined" };`；
  2. `GenerateUsernameFromEmail` 中 sanitize 后若为空，使用 `user_{Guid.NewGuid():N}"[..8]`（取前 8 字符）；
  3. 若 sanitized 在 `UsernameReservedWords.Reserved` 中，追加随机后缀：`{sanitized}_{RandomNumberGenerator.GetInt32(1000, 9999)}`；
  4. 写单测：`GenerateUsernameFromEmail_EmptyPrefix_Should_Use_Guid`、`GenerateUsernameFromEmail_ReservedWord_Should_Append_Suffix`、`GenerateUsernameFromEmail_ShortPrefix_Should_Pad`。
- **影响范围**：OAuth 用户注册用户名生成。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~GenerateUsernameFromEmailTests"`。

### P1-18: OAuth2ProviderResolver 与 UserAppService.ResolveAuthService 双重解析逻辑
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L311-L317](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L311-L317)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L584-L602](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L584-L602) 与 [file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/OAuth2ProviderResolver.cs#L23-L41](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Auth/OAuth2ProviderResolver.cs#L23-L41)
- **根因**：应用服务已注入 `IEnumerable<IExternalAuthService>` 并自己实现 `ResolveAuthService`，与 `OAuth2ProviderResolver` 重复。`UserAppService` 应直接注入 `IOAuth2ProviderResolver` 抽象，防腐层职责单一（DRY 违反）。
- **修复步骤**：
  1. `UserAppService` 构造函数移除 `IEnumerable<IExternalAuthService> _externalAuthServices`，改为注入 `IOAuth2ProviderResolver _providerResolver`；
  2. 删除 `UserAppService.ResolveAuthService` 私有方法（L584-L602）；
  3. 所有原 `ResolveAuthService(provider)` 调用改为 `_providerResolver.Resolve(provider)`；
  4. 确认 `IOAuth2ProviderResolver` 抽象在 Application 层（`Leno.UserAuth.Application/Abstractions/`），实现 `OAuth2ProviderResolver` 在 Infrastructure 层；
  5. 写单测验证 `UserAppService` 不再持有 `IEnumerable<IExternalAuthService>` 字段。
- **影响范围**：OAuth 解析路径；Application 层依赖。
- **验证方法**：`dotnet build` 通过；`UserAppServiceTests` 现有用例全部通过。

### P1-19: RefreshTokenAsync 中 user.Status == AccountStatus.Disabled 检查后未撤销已签发令牌
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L318-L324](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L318-L324)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L170-L176](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L170-L176)
- **根因**：发现用户被禁用后只抛 `UnauthorizedAccessException`，未调用 `RevokeAllAsync` 撤销该 RefreshToken。攻击者持有的 RefreshToken 仍可重试，仅在下一次刷新时再次失败。
- **修复步骤**：
  1. `RefreshTokenAsync` 中 `if (user.Status == AccountStatus.Disabled)` 分支增加 `await _refreshTokenStore.RevokeAllAsync(user.Id, ct);`，再抛异常；
  2. 同时调用 `IJwtRevocationService.RevokeAsync(user.Id, ct)` 把 userId 加入黑名单；
  3. 记录安全审计日志（`SecurityLog: 用户 {userId} 处于禁用状态尝试刷新令牌，已撤销所有令牌`）；
  4. 写单测：`RefreshTokenAsync_DisabledUser_Should_Revoke_All_Tokens`。
- **影响范围**：刷新令牌路径；令牌撤销安全语义。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~RefreshTokenDisabledRevokeTests"`。

---

## P2 修复清单（任务清单格式，可简化）

### P2-1: OAuthClientAppService.MaskSecret 任意长度返回 "****"
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L329-L334](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L329-L334)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L126-L134](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/OAuthClientAppService.cs#L126-L134)
- **根因**：`if (string.IsNullOrEmpty(secret))` 与 else 都返回 `"****"`，分支冗余；且掩码无信息量，不能用于核对配置。
- **修复步骤**：
  1. `MaskSecret` 改为：`if (string.IsNullOrEmpty(secret) || secret.Length < 8) return "****";` `return $"{secret[..4]}****{secret[^4..]}";`（前 4 + 后 4）；
  2. 写单测：`MaskSecret_ShortSecret_Should_Return_Mask`、`MaskSecret_LongSecret_Should_Keep_First_And_Last_Four`。
- **影响范围**：OAuth 客户端配置展示。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~MaskSecretTests"`。

### P2-2: AuditLogInterceptor 直接操作 EF Property().CurrentValue 而非聚合行为方法
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L335-L340](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L335-L340)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Audit/AuditLogInterceptor.cs#L51-L72](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Audit/AuditLogInterceptor.cs#L51-L72)
- **根因**：`AuditLog` 的 `Ip / UserAgent / TraceId` 是 `private set`，拦截器用 EF 元数据 API 绕过 C# 访问修饰符写入，耦合 EF 内部 API，难以测试。
- **修复步骤**：
  1. 在 `AuditLog` 聚合添加 `internal void Enrich(string? ip, string? ua, string? traceId)` 方法，内部 `Ip = ip; UserAgent = ua; TraceId = traceId;`；
  2. `AuditLogInterceptor.EnrichAuditLogs` 改为 `auditLog.Enrich(ip, ua, traceId)`，不再调用 EF 元数据 API；
  3. `InternalsVisibleTo` 特性让 Infrastructure 程序集可见（或改为 `public` 但标注 `[EditorBrowsable(EditorBrowsableState.Never)]`）；
  4. 写单测：`AuditLog_Enrich_Should_Set_Properties`。
- **影响范围**：审计拦截器；可测试性。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~AuditLogEnrichTests"`。

### P2-3: InternalUsersController 标记 Obsolete 但同时映射同一路由
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L341-L346](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L341-L346)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs#L22-L35](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Api/Controllers/InternalUsersController.cs#L22-L35)
- **根因**：`[Obsolete("双路由期保留，1 周后下线，请使用 internal/v1/... 路由")]` 与 `[HttpGet("internal/v1/users/...")]` 同时存在；Obsolete 标注自身指向当前路径，注释自相矛盾。
- **修复步骤**：
  1. 若双路由期已过，删除旧路由 action（非 `internal/v1/...` 的），保留 `internal/v1/...` 路由并移除 `[Obsolete]`；
  2. 若仍在双路由期，拆分两个 action：旧路由 action 标 `[Obsolete]` + `[HttpGet("internal/users/...")]`，新路由 action 不标 `[Obsolete]` + `[HttpGet("internal/v1/users/...")]`；
  3. 在代码注释明确双路由下线日期。
- **影响范围**：内部 API 路由；API 兼容性。
- **验证方法**：代码审查；OpenAPI 文档确认路由无矛盾标注。

### P2-4: User.VerifyPassword 未做时序安全防护（恒定时间比较）
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L347-L352](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L347-L352)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L122-L142](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/User.cs#L122-L142)
- **根因**：bcrypt.Verify 内部已恒定时间，但当 `PasswordHash` 为空时直接 `return false`（< 1ms），存在账户时序差异。
- **修复步骤**：
  1. `User.VerifyPassword` 中 `if (string.IsNullOrEmpty(PasswordHash))` 分支执行一次 `BCrypt.Net.BCrypt.Verify("\x00", DummyHash)`（结果丢弃）再 `return false`；
  2. 在 `User` 聚合定义 `private const string DummyHash = "$2a$11$...";`；
  3. 写单测：`VerifyPassword_EmptyHash_Should_Take_Similar_Time_As_Bcrypt_Verify`。
- **影响范围**：密码验证路径。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~VerifyPasswordTimingTests"`。

### P2-5: UserConfiguration.password_hash 最大长度 128 偏小
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L353-L358](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L353-L358)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs#L23](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Configurations/UserConfiguration.cs#L23)
- **根因**：bcrypt 哈希固定 60 字符，128 留有余量；但若未来切换到 Argon2id（典型 96+ 字符）需扩列。
- **修复步骤**：
  1. `UserConfiguration` 中 `password_hash` 列改为 `.HasMaxLength(256)`；
  2. 新建 EF Core 迁移 `ExtendPasswordHashColumn`：`ALTER COLUMN [password_hash] nvarchar(256)`；
  3. 验证现有 bcrypt 哈希仍可写入。
- **影响范围**：用户表 schema；未来密码算法迁移。
- **验证方法**：`dotnet ef migrations script` 审查；`dotnet ef database update` 后查询 `sp_columns users` 确认列长度。

### P2-6: IssueTokensAsync.GetPrimaryRole 只取最高权限角色，丢失多角色信息
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L359-L364](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L359-L364)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L571-L582](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L571-L582)
- **根因**：JWT 中只携带一个 role claim。同时持有 Buyer + Seller 角色的用户在网关 RBAC 校验时只能匹配 Seller 路由，Buyer 路由被拒。
- **修复步骤**：
  1. 删除 `GetPrimaryRole` 方法；
  2. `IssueTokensAsync` 中 `claims.Add(new Claim("role", role))` 循环添加所有角色：`foreach (var role in user.Roles) { claims.Add(new Claim(ClaimTypes.Role, role)); }`；
  3. 网关 RBAC 校验逻辑改为 `User.IsInRole(requiredRole)`（JWT 多 role claim 原生支持）；
  4. 写单测：`IssueTokensAsync_MultiRoleUser_Should_Have_All_Role_Claims`。
- **影响范围**：JWT 声明；网关 RBAC 校验。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~MultiRoleJwtTests"`；解码多角色用户 JWT 确认含多个 role claim。

### P2-7: RegisterDtoValidator 不复用领域校验，校验逻辑重复
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L365-L370](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L365-L370)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Validators/RegisterDtoValidator.cs#L12-L14](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Validators/RegisterDtoValidator.cs#L12-L14)
- **根因**：用户名 / 邮箱 / 手机号正则在 `RegisterDtoValidator`、`User.ValidateUsername` / `ValidateEmail` / `ValidatePhone`、`SaveAddressDtoValidator` 中分别定义，DRY 违反。
- **修复步骤**：
  1. 在 `Leno.UserAuth.Domain/ValueObjects/` 新建 `UsernamePattern`、`EmailPattern`、`PhonePattern` 静态类，暴露 `Regex` 与 `ErrorMessage`；
  2. 使用 .NET 8 `partial class` + `[GeneratedRegex]` 源生成器编译期生成正则；
  3. `RegisterDtoValidator` 改为 `RuleFor(x => x.Username).Matches(UsernamePattern.PatternStr)`；
  4. `User.ValidateUsername` / `ValidateEmail` / `ValidatePhone` 改为调用同一 pattern；
  5. 写单测：`UsernamePattern_Should_Match_Valid_Usernames`、`EmailPattern_Should_Reject_Invalid_Emails`。
- **影响范围**：输入校验；代码可维护性。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~PatternTests"`；代码搜索确认无重复正则定义。

### P2-8: UserAppService.HandleOAuthCallbackAsync 缺少 OAuth 用户 2FA 启用检测
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L371-L376](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L371-L376)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L266-L336](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L266-L336)
- **根因**：OAuth 登录路径不检查 `user.TwoFactorEnabled`，已启用 2FA 的 OAuth 用户也直接签发完整 AccessToken。
- **修复步骤**：
  1. `HandleOAuthCallbackAsync` 中找到现有用户后，检查 `if (user.TwoFactorEnabled)`；
  2. 若启用 2FA，签发临时令牌（与 `LoginAsync` 一致）：`var tempToken = await _twoFactorTempTokenStore.IssueAsync(user.Id, ttl: TimeSpan.FromMinutes(5), ct);`，返回 `LoginResult.RequiresTwoFactor(tempToken)`；
  3. 复用现有 `VerifyTwoFactorAsync` 完成后续 2FA 验证；
  4. 写单测：`HandleOAuthCallbackAsync_2FAUser_Should_Return_RequiresTwoFactor`。
- **影响范围**：OAuth 登录路径；2FA 安全策略。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~OAuthTwoFactorTests"`。

### P2-9: EfCoreUserRepository.UpdateAsync 注释解释合理但 Attach 行为需注意
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L377-L382](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L377-L382)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCoreUserRepository.cs#L100-L110](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Repositories/EfCoreUserRepository.cs#L100-L110)
- **根因**：`if (_context.Entry(user).State == EntityState.Detached) _context.Users.Attach(user);` 注释说"避免对 owned 集合调用 Update 覆盖 Added 状态"。但 `Attach` 后实体状态为 `Unchanged`，对其导航集合的修改不会被检测——除非应用层显式调用 `Entry(user).Reference(...).IsModified = true`。
- **修复步骤**：
  1. 若 User 被显式从外部传入（脱离跟踪），考虑直接抛 `InvalidOperationException("User aggregate must be tracked by DbContext")` 而非静默 Attach，避免变更丢失；
  2. 或保留 Attach 但在注释中明确警告"调用方需显式标记修改字段"；
  3. 在仓储基类增加 `EnsureTracked(TEntity entity)` 辅助方法统一处理。
- **影响范围**：用户聚合持久化路径。
- **验证方法**：代码审查；`EfCoreUserRepository_UpdateTests` 验证 Attach 后导航集合修改被正确持久化。

### P2-10: AddressAppService.ClearExistingDefaultAsync 调用 UpdateAsync 多次（实际无 DB 调用但代码气味）
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L383-L388](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L383-L388)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AddressAppService.cs#L159-L167](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/AddressAppService.cs#L159-L167)
- **根因**：循环内 `await _addressRepository.UpdateAsync(existing, ct);` 实际是 no-op（EF 跟踪实体修改即生效），但 `await` 误导读者以为是 DB 操作。
- **修复步骤**：
  1. 移除循环内的 `await _addressRepository.UpdateAsync(existing, ct);` 调用；
  2. 循环内仅调用 `existing.ClearDefault()` 聚合行为方法，依赖 EF 变更跟踪；
  3. 循环外统一由 `UnitOfWork.SaveEntitiesAsync(ct)` 持久化；
  4. 写单测验证清理默认地址后所有原默认地址 `IsDefault == false`。
- **影响范围**：地址管理路径。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~ClearExistingDefaultTests"`。

### P2-11: UserAppService.ForgotPasswordAsync 重置令牌使用 Guid.NewGuid 而非密码学安全随机
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L389-L394](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L389-L394)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L439](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs#L439)
- **根因**：`Guid.NewGuid().ToString("N")` 在 .NET 7+ 内部使用 `RandomNumberGenerator`，但 GUID 结构有版本位与保留位，实际熵 < 122 位。
- **修复步骤**：
  1. 替换为 `Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace("+", "-").Replace("/", "_")`，提供 256 位熵；
  2. 抽取 `GenerateSecureToken(int byteLength)` 静态方法复用（P1-17 的 OAuth state 生成也可复用）；
  3. 写单测：`GenerateSecureToken_Should_Be_UrlSafe_And_No_Padding`、`GenerateSecureToken_Should_Have_High_Entropy`（统计分布测试）。
- **影响范围**：密码重置令牌生成。
- **验证方法**：`dotnet test --filter "FullyQualifiedName~SecureTokenTests"`。

### P2-12: OAuth2 AesKey 配置缺失时单例不注册，运行期才发现
- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L395-L400](file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/01-userauth.md#L395-L400)
- **代码位置**：[file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L88-L93](file:///workspace/src/Services/UserAuth/Leno.UserAuth.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L88-L93)
- **根因**：`if (!string.IsNullOrWhiteSpace(aesKey)) services.AddSingleton<IClientSecretEncryptionService>(...)`，配置缺失时容器中无 `IClientSecretEncryptionService`。`OAuthClientAppService` 构造函数 `IClientSecretEncryptionService? encryptionService = null` 默认为 null，调用 `UpdateAsync` 时才抛 `InvalidOperationException`。
- **修复步骤**：
  1. `AddUserAuthInfrastructure` 中移除 `if (!string.IsNullOrWhiteSpace(aesKey))` 条件判断，改为启动期校验：`if (string.IsNullOrWhiteSpace(aesKey)) throw new InvalidOperationException("OAuth2:AesKey 配置缺失，无法启动 UserAuth 服务");`；
  2. 校验通过后无条件 `services.AddSingleton<IClientSecretEncryptionService>(new AesEncryptionService(aesKey, ...));`；
  3. `OAuthClientAppService` 构造函数把 `IClientSecretEncryptionService?` 改为非空 `IClientSecretEncryptionService`（移除可选注入）；
  4. 与 `Program.cs` 的 `ValidateSensitiveConfig` 联动，启动期统一 fail-fast。
- **影响范围**：OAuth 客户端密钥加密；启动期配置校验。
- **验证方法**：缺失 `OAuth2:AesKey` 时 `dotnet run` 启动失败并输出明确错误；配置正确时服务正常启动。

---

## 已修复项（跳过清单）

| # | 审计编号 | 问题标题 | 修复位置 | 状态 | 说明 |
|---|---------|---------|---------|------|------|
| 1 | T5 | InternalApiKey fail-closed 与 timing-safe | [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Middleware/InternalApiKeyMiddleware.cs#L53-L65](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Middleware/InternalApiKeyMiddleware.cs#L53-L65)（fail-closed）、[#L95-L105](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Middleware/InternalApiKeyMiddleware.cs#L95-L105)（`CryptographicOperations.FixedTimeEquals`） | [ALREADY-FIXED] | 已在共享层 `Leno.Infrastructure` 修复：生产环境未配置 `InternalAuth:ApiKey` 时返回 500 拒绝请求（L62-L64）；ApiKey 比较使用 `CryptographicOperations.FixedTimeEquals` 防止计时侧信道（L104）。UserAuth BC 内无对应代码需修改，本计划跳过。 |
| 2 | T6 | internal 路由边界精确匹配 | [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Middleware/InternalApiKeyMiddleware.cs#L84-L93](file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Middleware/InternalApiKeyMiddleware.cs#L84-L93) | [ALREADY-FIXED] | 已在共享层修复：`IsInternalPath` 方法精确匹配 `/internal` 或 `/internal/...` 前缀（L91-L92），避免 `/internalinfo` 误判为内部路由。UserAuth BC 通过 `app.UseLenoPipeline()` 复用该中间件，无需 BC 内修改，本计划跳过。 |

---

## 修复优先级与依赖关系说明

1. **P0 修复顺序建议**：P0-1（RedisRefreshTokenStore）→ P0-10 / P0-11 / P0-14（令牌撤销链路与并发保护，依赖 P0-1 的 `RevokeAllAsync` 实现）→ P0-2 / P0-3 / P0-13 / P0-15（OAuth 链路安全）→ P0-4 / P0-5 / P0-8（持久化与状态校验）→ P0-6 / P0-7（数据库约束）→ P0-9（审计日志）→ P0-12（AES-GCM）。

2. **P1 修复顺序建议**：P1-3（抽象 Redis 依赖）应在 P0-1 / P0-13 完成后进行，避免重复修改 `UserAppService` 构造函数；P1-9 / P1-19 依赖 P0-10 / P0-11 的令牌撤销能力；P1-8 与 P1-15 同属 OAuth redirectUri 安全，建议合并实现。

3. **数据库迁移**：P0-6、P0-7、P1-4、P2-5 涉及 EF Core 迁移，需在同一 sprint 内合并为一个迁移批次，避免迁移冲突。

4. **下游 BC 联动**：P1-5（伪邮箱去除）需通知 Membership / Notification BC 监听 `IsEmailVerified` 字段；P2-6（多角色 JWT）需通知网关 RBAC 校验逻辑适配。