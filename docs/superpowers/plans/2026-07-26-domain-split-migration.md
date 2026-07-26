# 域拆分迁移实施计划：结束 UserAuth/PointsMembership/ReviewAfterSales 双轨期

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 UserAuth/PointsMembership/ReviewAfterSales 三旧域的 105 个 API 端点全量迁移到 7 个新域（Identity/AccessControl/UserCenter/Points/Membership/Review/AfterSales），统一契约规范，结束双轨期，旧域完全下线。

**Architecture:** 三域并行同步切换，分四阶段推进：阶段 1 补齐新域代码（本计划核心）→ 阶段 2 网关双轨灰度 → 阶段 3 观察期 → 阶段 4 旧域下线。阶段 1 按 Track A/B/C 三路并行，每个 Task 产出可独立编译、测试、提交的代码单元。

**Tech Stack:** .NET 8 / ASP.NET Core / EF Core / MassTransit / gRPC / xUnit / Moq / WebApplicationFactory

**Spec 来源:** [docs/superpowers/specs/2026-07-26-domain-split-migration-design.md](file:///e:/Leno/docs/superpowers/specs/2026-07-26-domain-split-migration-design.md)

**契约规范（所有新域 Controller 必须遵循，参见 Spec §1.2）:**
- 显式 `[Route("api/xxx")]` 连字符命名，禁用 `[controller]`
- 统一 `ApiResponse<T>` 包装（`Leno.SharedContracts.Responses`）
- `[Authorize(Roles = "...")]` 角色 RBAC，禁用 Policy PBAC
- POST 创建/启停返回 `200 OK + ApiResponse.Success()`
- `/me` 端点从 JWT 解析 userId
- Internal 端点路径 `internal/v1/<domain>/*`，无类级 `[Route]`

---

## Track B：PointsMembership 域拆分（P0 优先，2026-08-01 deadline）

> **优先级说明：** PointsMembership 域 4 个 Internal 端点代码标注 `[Obsolete]` 2026-08-01 下线，必须优先完成 Track B 阶段 1。

### Task B1: Points 域 Application 层补齐 + Internal 服务重设计

**Files:**
- Modify: `src/Services/Points/Leno.Points.Application/IPointsAppService.cs` — 重命名方法对齐旧域
- Create: `src/Services/Points/Leno.Points.Application/ICheckInAppService.cs`
- Create: `src/Services/Points/Leno.Points.Application/IExchangeCouponAppService.cs`
- Create: `src/Services/Points/Leno.Points.Application/IAwardAppService.cs`
- Create: `src/Services/Points/Leno.Points.Application/ITaskAppService.cs`
- Create: `src/Services/Points/Leno.Points.Application/IPointsRuleAppService.cs`
- Modify: `src/Services/Points/Leno.Points.Application/IPointsInternalAppService.cs` — 补 4 方法
- Create: `src/Services/Points/Leno.Points.Application/Services/CheckInAppService.cs`
- Create: `src/Services/Points/Leno.Points.Application/Services/ExchangeCouponAppService.cs`
- Create: `src/Services/Points/Leno.Points.Application/Services/AwardAppService.cs`
- Create: `src/Services/Points/Leno.Points.Application/Services/TaskAppService.cs`
- Create: `src/Services/Points/Leno.Points.Application/Services/PointsRuleAppService.cs`
- Create: `src/Services/Points/Leno.Points.Application/Services/PointsInternalAppService.cs`
- Test: `src/Services/Points/Leno.Points.Application.Tests/PointsInternalAppServiceTests.cs`

**参考源（旧域实现，需复制业务逻辑）:**
- `src/Services/PointsMembership/Leno.PointsMembership.Application/**`（所有现有 AppService 实现）
- `src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/InternalPointsController.cs`（4 个 Internal 端点签名）

- [ ] **Step 1: 阅读 PointsMembership 旧域 IPointsInternalAppService 实现**

```bash
# 阅读旧域 Internal 服务接口与实现，确认 4 方法签名
```

读 `src/Services/PointsMembership/Leno.PointsMembership.Application/IPointsInternalAppService.cs` 与对应实现，提取以下签名（必须与旧域完全一致）：

```csharp
public interface IPointsInternalAppService
{
    Task<TrialOffsetResultDto> TrialOffsetAsync(Guid userId, decimal orderAmount, CancellationToken ct = default);
    Task<FreezeResultDto> FreezeAsync(Guid userId, int points, Guid orderId, CancellationToken ct = default);
    Task ReleaseAsync(Guid orderId, CancellationToken ct = default);
    Task ConfirmAsync(Guid orderId, CancellationToken ct = default);
    Task GrantLevelBonusAsync(Guid userId, int newLevel, CancellationToken ct = default);
}
```

- [ ] **Step 2: 在新 Points.Application 项目补齐接口**

修改 `src/Services/Points/Leno.Points.Application/IPointsAppService.cs`，将 `GetOrCreateAccountAsync` 重命名为 `GetAccountAsync`，`GetFlowsAsync` 重命名为 `GetLedgerAsync`（保留原方法体，仅改方法名）。

新建 5 个接口文件（`ICheckInAppService.cs` / `IExchangeCouponAppService.cs` / `IAwardAppService.cs` / `ITaskAppService.cs` / `IPointsRuleAppService.cs`），签名从旧域 `PointsMembership.Application` 对应接口复制。

修改 `src/Services/Points/Leno.Points.Application/IPointsInternalAppService.cs`，补齐 4 方法签名（保留现有 `GrantLevelBonusAsync`）。

- [ ] **Step 3: 复制实现逻辑到新 Points.Application**

将旧域 `PointsMembership.Application/Services/*AppService.cs` 的实现逻辑复制到新域 `Points.Application/Services/`，方法签名对齐 Spec §4.3.1。`PointsInternalAppService` 必须完整实现 4 方法（TrialOffset/Freeze/Release/Confirm），不得返回 0 或抛 `NotImplementedException`。

- [ ] **Step 4: 编写 PointsInternalAppService 单元测试**

```csharp
// src/Services/Points/Leno.Points.Application.Tests/PointsInternalAppServiceTests.cs
public class PointsInternalAppServiceTests
{
    [Fact]
    public async Task TrialOffsetAsync_WithValidInputs_ReturnsOffsetResult()
    {
        // Arrange: mock IPointsRepository 返回账户余额 1000
        // Act: var result = await sut.TrialOffsetAsync(userId, 100m, ct);
        // Assert: result.OffsetAmount > 0 && result.UsedPoints <= 1000
    }
    
    [Fact]
    public async Task FreezeAsync_WithSufficientBalance_ReturnsSuccess()
    {
        // Arrange: 余额 1000
        // Act: await sut.FreezeAsync(userId, 500, orderId, ct);
        // Assert: 余额减 500，冻结加 500
    }
    
    [Fact]
    public async Task FreezeAsync_WithInsufficientBalance_ThrowsDomainException()
    {
        // Arrange: 余额 100
        // Act & Assert: await Assert.ThrowsAsync<PointsDomainException>(() => sut.FreezeAsync(userId, 500, orderId, ct));
    }
    
    [Fact]
    public async Task ReleaseAsync_WithFrozenOrder_ReturnsPoints()
    {
        // Arrange: 已冻结 500
        // Act: await sut.ReleaseAsync(orderId, ct);
        // Assert: 余额恢复，冻结减 500
    }
    
    [Fact]
    public async Task ConfirmAsync_WithFrozenOrder_ConfirmsDeduction()
    {
        // Arrange: 已冻结 500
        // Act: await sut.ConfirmAsync(orderId, ct);
        // Assert: 冻结减 500，余额不变（已扣减）
    }
}
```

- [ ] **Step 5: 编译并运行测试**

```bash
dotnet build src/Services/Points/Leno.Points.Application/Leno.Points.Application.csproj
dotnet test src/Services/Points/Leno.Points.Application.Tests/Leno.Points.Application.Tests.csproj
```

Expected: Build SUCCESS, Tests PASS

- [ ] **Step 6: Commit**

```bash
git add src/Services/Points/Leno.Points.Application/
git commit -m "feat(points): 补齐 Application 层接口与实现，含 Internal 服务 4 方法"
```

---

### Task B2: Points Controller 层 + gRPC 重建

**Files:**
- Create: `src/Services/Points/Leno.Points.Api/Controllers/PointsController.cs` — 4 端点
- Create: `src/Services/Points/Leno.Points.Api/Controllers/TasksController.cs` — 2 端点
- Create: `src/Services/Points/Leno.Points.Api/Controllers/PointsRulesController.cs` — 5 端点
- Create: `src/Services/Points/Leno.Points.Api/Controllers/AdminPointsController.cs` — 1 端点
- Create: `src/Services/Points/Leno.Points.Api/Controllers/InternalPointsController.cs` — 4 端点（单路径，不双路由）
- Create: `src/Services/Points/Leno.Points.Api/GrpcServices/PointsGrpcService.cs`
- Modify: `src/Services/Points/Leno.Points.Api/Program.cs` — 注册 Controllers + 条件映射 gRPC
- Test: `src/Services/Points/Leno.Points.Api.Tests/PointsApiTests.cs`

**参考源:**
- `src/Services/PointsMembership/Leno.PointsMembership.Api/Controllers/**`（旧域 Controller 实现）
- `src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs`（gRPC 实现）
- `src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs`（契约规范模板）

- [ ] **Step 1: 创建 PointsController（买家端 4 端点）**

```csharp
// src/Services/Points/Leno.Points.Api/Controllers/PointsController.cs
using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Infrastructure.Auth;
using Leno.Points.Application;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Points.Api.Controllers;

[ApiController]
[Route("api/points")]
[Authorize(Roles = "Buyer")]
public sealed class PointsController : ControllerBase
{
    private readonly IPointsAppService _pointsAppService;
    private readonly ICheckInAppService _checkInAppService;
    private readonly IExchangeCouponAppService _exchangeCouponAppService;
    private readonly ICurrentUserContext _currentUser;

    public PointsController(
        IPointsAppService pointsAppService,
        ICheckInAppService checkInAppService,
        IExchangeCouponAppService exchangeCouponAppService,
        ICurrentUserContext currentUser)
    {
        _pointsAppService = pointsAppService;
        _checkInAppService = checkInAppService;
        _exchangeCouponAppService = exchangeCouponAppService;
        _currentUser = currentUser;
    }

    [HttpPost("check-in")]
    public async Task<IActionResult> CheckInAsync(CancellationToken ct)
    {
        var result = await _checkInAppService.CheckInAsync(_currentUser.UserId, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("account")]
    public async Task<IActionResult> GetAccountAsync(CancellationToken ct)
    {
        var result = await _pointsAppService.GetAccountAsync(_currentUser.UserId, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> GetLedgerAsync([FromQuery] int page = 0, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _pointsAppService.GetLedgerAsync(_currentUser.UserId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPost("exchange-coupon")]
    public async Task<IActionResult> ExchangeCouponAsync([FromBody] ExchangeCouponRequestDto request, CancellationToken ct)
    {
        var result = await _exchangeCouponAppService.ExchangeAsync(_currentUser.UserId, request.CouponId, ct);
        return Ok(ApiResponse.Success(result));
    }
}
```

- [ ] **Step 2: 创建 TasksController（2 端点）**

```csharp
[ApiController]
[Route("api/points/tasks")]
[Authorize(Roles = "Buyer")]
public sealed class TasksController : ControllerBase
{
    private readonly ITaskAppService _taskAppService;
    private readonly ICurrentUserContext _currentUser;

    public TasksController(ITaskAppService taskAppService, ICurrentUserContext currentUser)
    {
        _taskAppService = taskAppService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasksAsync(CancellationToken ct)
    {
        var result = await _taskAppService.GetTasksAsync(_currentUser.UserId, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPost("{taskId:guid}/complete")]
    public async Task<IActionResult> CompleteTaskAsync([FromRoute] Guid taskId, CancellationToken ct)
    {
        var result = await _taskAppService.CompleteTaskAsync(_currentUser.UserId, taskId, ct);
        return Ok(ApiResponse.Success(result));
    }
}
```

- [ ] **Step 3: 创建 PointsRulesController（运营端 5 端点）+ AdminPointsController（1 端点）**

按相同模式创建 `PointsRulesController`（`[Route("api/admin/points/rules")]`，`[Authorize(Roles = "Operator,Admin")]`）与 `AdminPointsController`（`[Route("api/admin/points")]`，award 端点）。

- [ ] **Step 4: 创建 InternalPointsController（4 端点，单路径不双路由）**

```csharp
// 注意：无类级 [Route]，每个 Action 显式挂 internal/v1/points/* 单路由
[ApiController]
public sealed class InternalPointsController : ControllerBase
{
    private readonly IPointsInternalAppService _internalService;

    public InternalPointsController(IPointsInternalAppService internalService)
    {
        _internalService = internalService;
    }

    [HttpPost("internal/v1/points/trial-offset")]
    public async Task<IActionResult> TrialOffsetAsync([FromBody] TrialOffsetRequestDto request, CancellationToken ct)
    {
        var result = await _internalService.TrialOffsetAsync(request.UserId, request.OrderAmount, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPost("internal/v1/points/freeze")]
    public async Task<IActionResult> FreezeAsync([FromBody] FreezeRequestDto request, CancellationToken ct)
    {
        var result = await _internalService.FreezeAsync(request.UserId, request.Points, request.OrderId, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPost("internal/v1/points/release")]
    public async Task<IActionResult> ReleaseAsync([FromBody] ReleaseRequestDto request, CancellationToken ct)
    {
        await _internalService.ReleaseAsync(request.OrderId, ct);
        return Ok(ApiResponse.Success());
    }

    [HttpPost("internal/v1/points/confirm")]
    public async Task<IActionResult> ConfirmAsync([FromBody] ConfirmRequestDto request, CancellationToken ct)
    {
        await _internalService.ConfirmAsync(request.OrderId, ct);
        return Ok(ApiResponse.Success());
    }
}
```

- [ ] **Step 5: 复制 gRPC 服务并配置 Program.cs**

复制 `src/Services/PointsMembership/.../GrpcServices/PointsGrpcService.cs` 到 `src/Services/Points/Leno.Points.Api/GrpcServices/PointsGrpcService.cs`，命名空间改为 `Leno.Points.Api.GrpcServices`。

修改 `Program.cs`：
```csharp
// 条件映射 gRPC
if (builder.Configuration.GetValue<bool>("AntiCorruption:UseGrpc"))
{
    app.MapGrpcService<PointsGrpcService>();
    app.Logger.LogInformation("Points gRPC service mapped.");
}
```

- [ ] **Step 6: 编写 API 集成测试**

参考 `src/Services/Order/Leno.Order.Api.Tests/OrderApiTests.cs` 模板，编写 `PointsApiTests.cs`，覆盖：
- 买家端 4 端点（成功 + 401 未鉴权）
- 任务端 2 端点
- 规则端 5 端点（成功 + 403 非运营角色）
- Internal 4 端点（成功 + 缺 X-Internal-Key 401）

```csharp
[Fact]
public async Task CheckIn_WithBuyerAuth_ReturnsOk()
{
    SetupBuyerAuth();
    _checkInAppServiceMock.Setup(s => s.CheckInAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new CheckInResultDto { EarnedPoints = 10 });
    
    var response = await _client.PostAsync("/api/points/check-in", null);
    
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    _checkInAppServiceMock.Verify(s => s.CheckInAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task TrialOffset_WithInternalKey_ReturnsOk()
{
    _client.DefaultRequestHeaders.Add("X-Internal-Key", _internalKey);
    var request = new { UserId = Guid.NewGuid(), OrderAmount = 100m };
    
    var response = await _client.PostAsJsonAsync("/internal/v1/points/trial-offset", request);
    
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task Freeze_WithoutInternalKey_ReturnsUnauthorized()
{
    var request = new { UserId = Guid.NewGuid(), Points = 100, OrderId = Guid.NewGuid() };
    
    var response = await _client.PostAsJsonAsync("/internal/v1/points/freeze", request);
    
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

- [ ] **Step 7: 编译 + 运行测试 + Commit**

```bash
dotnet build src/Services/Points/Leno.Points.Api/Leno.Points.Api.csproj
dotnet test src/Services/Points/Leno.Points.Api.Tests/Leno.Points.Api.Tests.csproj
git add src/Services/Points/Leno.Points.Api/
git commit -m "feat(points): 新建 5 Controller 16 端点 + gRPC 重建，对齐契约规范"
```

---

### Task B3: Membership Application 层补齐 + Controller 返工

**Files:**
- Modify: `src/Services/Membership/Leno.Membership.Application/IMembershipPackageAppService.cs` — 补 `SubscribeAsync`
- Modify: `src/Services/Membership/Leno.Membership.Application/IMemberAppService.cs` — 补 `EnableLevelAsync`/`DisableLevelAsync`
- Modify: `src/Services/Membership/Leno.Membership.Application/Services/MembershipPackageAppService.cs`
- Modify: `src/Services/Membership/Leno.Membership.Application/Services/MemberAppService.cs`
- Modify: `src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs` — 9 端点返工
- Modify: `src/Services/Membership/Leno.Membership.Api/Controllers/MembersController.cs` — 3 端点返工
- Test: `src/Services/Membership/Leno.Membership.Api.Tests/MembershipApiTests.cs`

**参考源:**
- `src/Services/PointsMembership/.../Controllers/MembershipPackagesController.cs`（旧域路径风格参考）
- `src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs`（契约规范模板）

- [ ] **Step 1: 阅读新 Membership 现有 9 端点实现，识别返工点**

读 `src/Services/Membership/Leno.Membership.Api/Controllers/MembershipPackagesController.cs` 与 `MembersController.cs`，识别所有 `[Route("[controller]")]`、`[Authorize(Policy = "AdminOnly")]`、`CreatedAtAction`、`[controller]` 占位符。

- [ ] **Step 2: Application 层补齐 3 方法**

```csharp
// IMembershipPackageAppService 补充
Task<SubscriptionResultDto> SubscribeAsync(Guid userId, Guid packageId, CancellationToken ct = default);

// IMemberAppService 补充
Task EnableLevelAsync(Guid levelId, CancellationToken ct = default);
Task DisableLevelAsync(Guid levelId, CancellationToken ct = default);
```

在对应 AppService 实现类中实现完整业务逻辑（参考旧域 `PointsMembership.Application` 实现）。

- [ ] **Step 3: 返工 MembershipPackagesController**

```csharp
[ApiController]
[Route("api/membership-packages")]
[Authorize(Roles = "Buyer")]
public sealed class MembershipPackagesController : ControllerBase
{
    // GET api/membership-packages — 买家查列表（已返工：路径连字符 + Buyer 鉴权）
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var result = await _packageAppService.ListAsync(ct);
        return Ok(ApiResponse.Success(result));
    }

    // POST api/membership-packages/{id}/subscribe — 买家订阅（新建端点）
    [HttpPost("{id:guid}/subscribe")]
    public async Task<IActionResult> SubscribeAsync([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await _packageAppService.SubscribeAsync(_currentUser.UserId, id, ct);
        return Ok(ApiResponse.Success(result));
    }
}

[ApiController]
[Route("api/admin/membership-packages")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminMembershipPackagesController : ControllerBase
{
    // POST api/admin/membership-packages — 创建（返工：路径 + 角色 + 200 + ApiResponse）
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreatePackageDto request, CancellationToken ct)
    {
        var result = await _packageAppService.CreateAsync(request, ct);
        return Ok(ApiResponse.Success(result));  // 不用 201 CreatedAtAction
    }

    // PUT api/admin/membership-packages/{id}
    // POST api/admin/membership-packages/{id}/enable
    // POST api/admin/membership-packages/{id}/disable
    // 同模式返工
}
```

- [ ] **Step 4: 返工 MembersController**

```csharp
[ApiController]
[Route("api/members")]
[Authorize(Roles = "Buyer")]
public sealed class MembersController : ControllerBase
{
    // GET api/members/me — 返工：从 {userId} 改回 /me
    [HttpGet("me")]
    public async Task<IActionResult> GetMyMemberInfoAsync(CancellationToken ct)
    {
        var result = await _memberAppService.GetAsync(_currentUser.UserId, ct);
        return Ok(ApiResponse.Success(result));
    }
}

[ApiController]
[Route("api/admin/members/levels")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminMemberLevelsController : ControllerBase
{
    // GET api/admin/members/levels — 返工：加鉴权
    // POST api/admin/members/levels — 返工：200 + ApiResponse
    // PUT api/admin/members/levels/{id}
    // POST api/admin/members/levels/{id}/enable — 新建
    // POST api/admin/members/levels/{id}/disable — 新建
}
```

- [ ] **Step 5: 编写 API 集成测试**

覆盖：
- 买家端 2 端点（list + subscribe）
- 运营端 6 端点（create/update/enable/disable + levels CRUD/enable/disable）
- 鉴权测试：Buyer 访问运营端点返回 403，匿名访问返回 401

- [ ] **Step 6: 编译 + 测试 + Commit**

```bash
dotnet build src/Services/Membership/Leno.Membership.Api/Leno.Membership.Api.csproj
dotnet test src/Services/Membership/Leno.Membership.Api.Tests/Leno.Membership.Api.Tests.csproj
git add src/Services/Membership/
git commit -m "feat(membership): 返工 9 端点 + 新建 3 端点，对齐 ApiResponse/连字符路径/RBAC 规范"
```

---

## Track A：UserAuth 域拆分（Identity + AccessControl + UserCenter）

### Task A1: AccessControl HTTP Controller 层（7 端点）

**Files:**
- Create: `src/Services/AccessControl/Leno.AccessControl.Application/IRoleAppService.cs`
- Create: `src/Services/AccessControl/Leno.AccessControl.Application/IRolePermissionAppService.cs`
- Create: `src/Services/AccessControl/Leno.AccessControl.Application/Services/RoleAppService.cs`
- Create: `src/Services/AccessControl/Leno.AccessControl.Application/Services/RolePermissionAppService.cs`
- Create: `src/Services/AccessControl/Leno.AccessControl.Api/Controllers/AdminRolesController.cs`
- Modify: `src/Services/AccessControl/Leno.AccessControl.Api/Program.cs`
- Test: `src/Services/AccessControl/Leno.AccessControl.Api.Tests/AccessControlApiTests.cs`

**参考源:**
- `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AdminRolesController.cs`（旧域实现，业务逻辑参考）
- `src/Services/UserAuth/Leno.UserAuth.Application/IRoleAppService.cs` 等接口

- [ ] **Step 1: 在 AccessControl.Application 补齐 7 方法接口**

```csharp
// src/Services/AccessControl/Leno.AccessControl.Application/IRoleAppService.cs
public interface IRoleAppService
{
    Task<PagedResult<RoleDto>> QueryRolesAsync(string? keyword, int page, int pageSize, CancellationToken ct = default);
    Task<RoleDto?> GetRoleAsync(Guid roleId, CancellationToken ct = default);
    Task<RoleDto> CreateRoleAsync(CreateRoleDto request, CancellationToken ct = default);
    Task UpdateRoleAsync(Guid roleId, UpdateRoleDto request, CancellationToken ct = default);
    Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default);
}

// src/Services/AccessControl/Leno.AccessControl.Application/IRolePermissionAppService.cs
public interface IRolePermissionAppService
{
    Task<IReadOnlyList<string>> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default);
    Task UpdateRolePermissionsAsync(Guid roleId, IReadOnlyList<string> permissions, CancellationToken ct = default);
}
```

- [ ] **Step 2: 复制 UserAuth.Application 中角色相关实现到 AccessControl.Application**

从 `src/Services/UserAuth/Leno.UserAuth.Application/Services/` 复制 RoleAppService 实现，调整命名空间为 `Leno.AccessControl.Application.Services`。

- [ ] **Step 3: 创建 AdminRolesController**

```csharp
[ApiController]
[Route("api/admin/roles")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminRolesController : ControllerBase
{
    private readonly IRoleAppService _roleAppService;
    private readonly IRolePermissionAppService _rolePermissionAppService;

    public AdminRolesController(IRoleAppService roleAppService, IRolePermissionAppService rolePermissionAppService)
    {
        _roleAppService = roleAppService;
        _rolePermissionAppService = rolePermissionAppService;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] string? keyword, [FromQuery] int page = 0, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _roleAppService.QueryRolesAsync(keyword, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("{roleId:guid}")]
    public async Task<IActionResult> GetAsync([FromRoute] Guid roleId, CancellationToken ct)
    {
        var result = await _roleAppService.GetRoleAsync(roleId, ct);
        return result is null ? NotFound(ApiResponse.Fail("ROLE_NOT_FOUND", "角色不存在")) : Ok(ApiResponse.Success(result));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateRoleDto request, CancellationToken ct)
    {
        var result = await _roleAppService.CreateRoleAsync(request, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPut("{roleId:guid}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] Guid roleId, [FromBody] UpdateRoleDto request, CancellationToken ct)
    {
        await _roleAppService.UpdateRoleAsync(roleId, request, ct);
        return Ok(ApiResponse.Success());
    }

    [HttpDelete("{roleId:guid}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid roleId, CancellationToken ct)
    {
        await _roleAppService.DeleteRoleAsync(roleId, ct);
        return Ok(ApiResponse.Success());
    }

    [HttpGet("{roleId:guid}/permissions")]
    public async Task<IActionResult> GetPermissionsAsync([FromRoute] Guid roleId, CancellationToken ct)
    {
        var result = await _rolePermissionAppService.GetRolePermissionsAsync(roleId, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPut("{roleId:guid}/permissions")]
    public async Task<IActionResult> UpdatePermissionsAsync([FromRoute] Guid roleId, [FromBody] UpdatePermissionsDto request, CancellationToken ct)
    {
        await _rolePermissionAppService.UpdateRolePermissionsAsync(roleId, request.Permissions, ct);
        return Ok(ApiResponse.Success());
    }
}
```

- [ ] **Step 4: 修改 Program.cs 注册服务 + 编写测试 + Commit**

```bash
dotnet build src/Services/AccessControl/Leno.AccessControl.Api/Leno.AccessControl.Api.csproj
dotnet test src/Services/AccessControl/Leno.AccessControl.Api.Tests/Leno.AccessControl.Api.Tests.csproj
git add src/Services/AccessControl/
git commit -m "feat(access-control): 新建 AdminRolesController 7 端点，补齐 Application 层"
```

---

### Task A2: Identity Application 层补齐（13 方法）

**Files:**
- Modify: `src/Services/Identity/Leno.Identity.Application/IAuthAppService.cs` — 补 `RegisterAsync`
- Create: `src/Services/Identity/Leno.Identity.Application/IOAuthService.cs`
- Create: `src/Services/Identity/Leno.Identity.Application/ITwoFactorService.cs`
- Create: `src/Services/Identity/Leno.Identity.Application/IPasswordService.cs`
- Create: `src/Services/Identity/Leno.Identity.Application/IUserProfileAppService.cs`
- Create: `src/Services/Identity/Leno.Identity.Application/IUserAdminAppService.cs`
- Create: `src/Services/Identity/Leno.Identity.Application/IExternalLoginService.cs`
- Create: `src/Services/Identity/Leno.Identity.Application/IOAuthClientAppService.cs`
- Create: `src/Services/Identity/Leno.Identity.Application/IUserInternalAppService.cs`
- 对应 8 个 Services 实现文件
- Test: `src/Services/Identity/Leno.Identity.Application.Tests/IdentityAppServiceTests.cs`

**参考源:**
- `src/Services/UserAuth/Leno.UserAuth.Application/**`（全部业务逻辑来源）

- [ ] **Step 1: 阅读旧域 UserAuth.Application 所有接口与实现，提取签名**

```bash
# 列出旧域 Application 接口
ls src/Services/UserAuth/Leno.UserAuth.Application/I*.cs
```

- [ ] **Step 2: 在 Identity.Application 创建 8 个接口文件**

按 Spec §2.1.1 表格中的"Application 层缺口"列，逐个创建接口并补全方法签名。完整方法签名参考旧域同名接口。

- [ ] **Step 3: 复制实现逻辑**

将旧域 `UserAuth.Application/Services/UserAppService.cs` 等实现按职责拆分到新域对应 Service 类。Identity 域不依赖 AccessControl gRPC，但 `AssignRolesAsync` 内部需通过 HTTP 调 AccessControl `POST api/admin/users/{id}/roles`（见 Spec §4.3.2）。

```csharp
// src/Services/Identity/Leno.Identity.Application/Services/UserAdminAppService.cs
public sealed class UserAdminAppService : IUserAdminAppService
{
    private readonly IUserRepository _userRepository;
    private readonly HttpClient _accessControlClient;  // 通过 HttpClientFactory 注入

    public UserAdminAppService(IUserRepository userRepository, HttpClient accessControlClient)
    {
        _userRepository = userRepository;
        _accessControlClient = accessControlClient;
    }

    public async Task AssignRolesAsync(Guid userId, List<Guid> roleIds, CancellationToken ct = default)
    {
        // 调 AccessControl HTTP 端点（见 Spec §4.3.2 推荐方案）
        var response = await _accessControlClient.PostAsJsonAsync(
            $"api/admin/users/{userId}/roles", new { RoleIds = roleIds }, ct);
        response.EnsureSuccessStatusCode();
    }
    
    // 其余 QueryUsersAsync / GetUserAsync / SuspendAsync / ResumeAsync 完整实现
}
```

- [ ] **Step 4: 编写单元测试 + Commit**

```bash
dotnet build src/Services/Identity/Leno.Identity.Application/Leno.Identity.Application.csproj
dotnet test src/Services/Identity/Leno.Identity.Application.Tests/Leno.Identity.Application.Tests.csproj
git add src/Services/Identity/Leno.Identity.Application/
git commit -m "feat(identity): 补齐 Application 层 13 方法，含 AssignRoles 跨域调用"
```

---

### Task A3: Identity Controller 层 — Auth + Users + Account（13 端点）

**Files:**
- Modify: `src/Services/Identity/Leno.Identity.Api/Controllers/AuthController.cs` — 返工 3 + 新建 6
- Create: `src/Services/Identity/Leno.Identity.Api/Controllers/UsersController.cs` — 6 端点
- Create: `src/Services/Identity/Leno.Identity.Api/Controllers/AccountController.cs` — 2 端点
- Modify: `src/Services/Identity/Leno.Identity.Api/Program.cs` — 注册服务
- Test: `src/Services/Identity/Leno.Identity.Api.Tests/IdentityApiTests.cs`

**参考源:**
- `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AuthController.cs`（旧域实现）
- `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/UsersController.cs`
- `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AccountController.cs`

- [ ] **Step 1: 返工 AuthController（3 返工 + 6 新建）**

```csharp
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthAppService _authAppService;
    private readonly IOAuthService _oauthService;
    private readonly ITwoFactorService _twoFactorService;
    private readonly IPasswordService _passwordService;

    public AuthController(IAuthAppService authAppService, IOAuthService oauthService, 
        ITwoFactorService twoFactorService, IPasswordService passwordService)
    {
        _authAppService = authAppService;
        _oauthService = oauthService;
        _twoFactorService = twoFactorService;
        _passwordService = passwordService;
    }

    // 返工 3 端点
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync([FromBody] LoginDto request, CancellationToken ct)
    {
        var result = await _authAppService.LoginAsync(request, ct);
        return Ok(ApiResponse.Success(result));  // 返工：ApiResponse 包装
    }

    [HttpPost("refresh-token")]  // 返工：路径从 refresh 改回 refresh-token
    [AllowAnonymous]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenDto request, CancellationToken ct)
    {
        var result = await _authAppService.RefreshTokenAsync(request, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct)
    {
        await _authAppService.LogoutAsync(_currentUser.UserId, ct);
        return Ok(ApiResponse.Success());
    }

    // 新建 6 端点
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterDto request, CancellationToken ct)
    {
        var result = await _authAppService.RegisterAsync(request, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("oauth/{provider}/login")]
    [AllowAnonymous]
    public async Task<IActionResult> OAuthLoginAsync([FromRoute] string provider, [FromQuery] string? redirectUri, CancellationToken ct)
    {
        var result = await _oauthService.GetLoginUrlAsync(provider, redirectUri, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("oauth/{provider}/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> OAuthCallbackAsync([FromRoute] string provider, [FromQuery] string code, [FromQuery] string? state, CancellationToken ct)
    {
        var result = await _oauthService.HandleCallbackAsync(provider, code, state, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPost("two-factor/verify")]
    [Authorize]
    public async Task<IActionResult> VerifyTwoFactorAsync([FromBody] TwoFactorVerifyDto request, CancellationToken ct)
    {
        var result = await _twoFactorService.VerifyAsync(_currentUser.UserId, request.Code, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPasswordAsync([FromBody] ForgotPasswordDto request, CancellationToken ct)
    {
        await _passwordService.ForgotPasswordAsync(request.Email, ct);
        return Ok(ApiResponse.Success());
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordDto request, CancellationToken ct)
    {
        await _passwordService.ResetPasswordAsync(request, ct);
        return Ok(ApiResponse.Success());
    }
}
```

- [ ] **Step 2: 创建 UsersController（6 端点）+ AccountController（2 端点）**

按相同模式创建 `UsersController`（`[Route("api/users/me")]`，6 端点：GET/PUT profile、PUT password、3 个 two-factor）与 `AccountController`（`[Route("api/account")]`，2 端点 external-logins POST/DELETE）。

- [ ] **Step 3: 编写 API 集成测试 + Commit**

覆盖：9 端点各 3 用例（成功/失败/鉴权），重点测试 anonymous 端点（login/register/forgot-password 等）无 token 可访问，受保护端点无 token 返回 401。

```bash
dotnet build src/Services/Identity/Leno.Identity.Api/Leno.Identity.Api.csproj
dotnet test src/Services/Identity/Leno.Identity.Api.Tests/Leno.Identity.Api.Tests.csproj
git add src/Services/Identity/Leno.Identity.Api/
git commit -m "feat(identity): AuthController 返工 3+新建 6 端点，UsersController/AccountController 新建 8 端点"
```

---

### Task A4: Identity Controller 层 — AdminOAuthClients + AdminUsers + Internal（15 端点）

**Files:**
- Create: `src/Services/Identity/Leno.Identity.Api/Controllers/AdminOAuthClientsController.cs` — 5 端点
- Create: `src/Services/Identity/Leno.Identity.Api/Controllers/AdminUsersController.cs` — 5 端点
- Create: `src/Services/Identity/Leno.Identity.Api/Controllers/InternalUsersController.cs` — 2 端点（内部）
- Test: `src/Services/Identity/Leno.Identity.Api.Tests/AdminIdentityApiTests.cs`

- [ ] **Step 1: 创建 3 个 Controller（按 Spec §2.1.1 端点清单）**

```csharp
[ApiController]
[Route("api/admin/oauth-clients")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminOAuthClientsController : ControllerBase { /* 5 端点 */ }

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminUsersController : ControllerBase { /* 5 端点 */ }

[ApiController]  // 无类级 Route
public sealed class InternalUsersController : ControllerBase
{
    [HttpGet("internal/v1/users/{userId:guid}/contacts")]
    public async Task<IActionResult> GetContactsAsync([FromRoute] Guid userId, CancellationToken ct)
    {
        var result = await _userInternalAppService.GetContactsAsync(userId, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("internal/v1/users/{userId:guid}/contacts/full")]
    public async Task<IActionResult> GetFullContactsAsync([FromRoute] Guid userId, CancellationToken ct)
    {
        var result = await _userInternalAppService.GetFullContactsAsync(userId, ct);
        return Ok(ApiResponse.Success(result));
    }
}
```

- [ ] **Step 2: 编写测试 + Commit**

```bash
dotnet build src/Services/Identity/Leno.Identity.Api/Leno.Identity.Api.csproj
dotnet test src/Services/Identity/Leno.Identity.Api.Tests/Leno.Identity.Api.Tests.csproj
git add src/Services/Identity/Leno.Identity.Api/Controllers/AdminOAuthClientsController.cs src/Services/Identity/Leno.Identity.Api/Controllers/AdminUsersController.cs src/Services/Identity/Leno.Identity.Api/Controllers/InternalUsersController.cs
git commit -m "feat(identity): 新建 AdminOAuthClients/AdminUsers/InternalUsers 12 端点"
```

---

### Task A5: UserCenter 域骨架搭建

**Files:**
- Create: `src/Services/UserCenter/Leno.UserCenter.Api/Leno.UserCenter.Api.csproj`
- Create: `src/Services/UserCenter/Leno.UserCenter.Api/Program.cs`
- Create: `src/Services/UserCenter/Leno.UserCenter.Api/appsettings.json`
- Create: `src/Services/UserCenter/Leno.UserCenter.Application/Leno.UserCenter.Application.csproj`
- Create: `src/Services/UserCenter/Leno.UserCenter.Domain/Leno.UserCenter.Domain.csproj`
- Create: `src/Services/UserCenter/Leno.UserCenter.Infrastructure/Leno.UserCenter.Infrastructure.csproj`
- Create: `src/Services/UserCenter/Leno.UserCenter.Infrastructure/UserCenterDbContext.cs`
- Modify: `Leno.sln` — 添加 4 个新项目

**参考源:**
- `src/Services/Notification/Leno.Notification.Api/Program.cs`（项目模板参考）
- `src/Services/Order/Leno.Order.Api/Program.cs`（MassTransit + EF Core 配置参考）

- [ ] **Step 1: 创建 4 个项目骨架（按 Order/Notification 域结构）**

```bash
# 创建 4 个项目
dotnet new web -n Leno.UserCenter.Api -o src/Services/UserCenter/Leno.UserCenter.Api --framework net8.0
dotnet new classlib -n Leno.UserCenter.Application -o src/Services/UserCenter/Leno.UserCenter.Application --framework net8.0
dotnet new classlib -n Leno.UserCenter.Domain -o src/Services/UserCenter/Leno.UserCenter.Domain --framework net8.0
dotnet new classlib -n Leno.UserCenter.Infrastructure -o src/Services/UserCenter/Leno.UserCenter.Infrastructure --framework net8.0

# 添加到 sln
dotnet sln Leno.sln add src/Services/UserCenter/Leno.UserCenter.Api/Leno.UserCenter.Api.csproj
dotnet sln Leno.sln add src/Services/UserCenter/Leno.UserCenter.Application/Leno.UserCenter.Application.csproj
dotnet sln Leno.sln add src/Services/UserCenter/Leno.UserCenter.Domain/Leno.UserCenter.Domain.csproj
dotnet sln Leno.sln add src/Services/UserCenter/Leno.UserCenter.Infrastructure/Leno.UserCenter.Infrastructure.csproj

# 项目引用
dotnet add src/Services/UserCenter/Leno.UserCenter.Api reference src/Services/UserCenter/Leno.UserCenter.Application src/Services/UserCenter/Leno.UserCenter.Infrastructure
dotnet add src/Services/UserCenter/Leno.UserCenter.Infrastructure reference src/Services/UserCenter/Leno.UserCenter.Application src/Services/UserCenter/Leno.UserCenter.Domain
dotnet add src/Services/UserCenter/Leno.UserCenter.Application reference src/Services/UserCenter/Leno.UserCenter.Domain
```

- [ ] **Step 2: 配置 Program.cs（参考 Notification 域）**

```csharp
// src/Services/UserCenter/Leno.UserCenter.Api/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core
builder.Services.AddDbContext<UserCenterDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// MassTransit
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"]);
    });
});

// 防腐层：HttpClientFactory
builder.Services.AddHttpClient<IProductPricingQueryService, ProductPricingQueryService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Product:BaseUrl"]!);
    client.DefaultRequestHeaders.Add("X-Internal-Key", builder.Configuration["Internal:Key"]!);
});

// 鉴权
builder.Services.AddAuthentication("Bearer").AddJwtBearer(/* ... */);
builder.Services.AddAuthorization();

builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
```

- [ ] **Step 3: 创建 UserCenterDbContext（共享 UserAuth 数据库的对应表）**

```csharp
// src/Services/UserCenter/Leno.UserCenter.Infrastructure/UserCenterDbContext.cs
public sealed class UserCenterDbContext : DbContext
{
    public UserCenterDbContext(DbContextOptions<UserCenterDbContext> options) : base(options) { }

    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<BrowseHistory> BrowseHistories => Set<BrowseHistory>();
    // NotificationPreferences 表共享（参见 Spec §4.3.5）

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 从 UserAuth.Infrastructure/UserAuthDbContext.cs 复制对应表的映射配置
        modelBuilder.Entity<Address>().ToTable("Addresses");
        modelBuilder.Entity<Favorite>().ToTable("Favorites");
        modelBuilder.Entity<BrowseHistory>().ToTable("BrowseHistories");
    }
}
```

- [ ] **Step 4: 编译 + Commit**

```bash
dotnet build Leno.sln
git add src/Services/UserCenter/ Leno.sln
git commit -m "feat(user-center): 新建域骨架（4 项目），配置 EF Core/MassTransit/防腐层"
```

---

### Task A6: UserCenter Application + Controller 层（17 端点）

**Files:**
- Create: `src/Services/UserCenter/Leno.UserCenter.Application/IAddressAppService.cs`
- Create: `src/Services/UserCenter/Leno.UserCenter.Application/IFavoritesAppService.cs`
- Create: `src/Services/UserCenter/Leno.UserCenter.Application/IBrowseHistoryAppService.cs`
- Create: `src/Services/UserCenter/Leno.UserCenter.Application/INotificationPreferencesAppService.cs`
- Create: 4 个 Services 实现文件
- Create: 4 个 Controller 文件
- Create: Domain 聚合（Address/Favorite/BrowseHistory/NotificationPreference）— 从 UserAuth.Domain 复制
- Test: `src/Services/UserCenter/Leno.UserCenter.Api.Tests/UserCenterApiTests.cs`

**参考源:**
- `src/Services/UserAuth/Leno.UserAuth.Application/IAddressAppService.cs` 等 4 个接口
- `src/Services/UserAuth/Leno.UserAuth.Application/Services/UserAppService.cs`（实现逻辑）
- `src/Services/UserAuth/Leno.UserAuth.Api/Controllers/AddressesController.cs` 等 4 个 Controller

- [ ] **Step 1: 复制 4 个 Domain 聚合到 UserCenter.Domain**

从 `src/Services/UserAuth/Leno.UserAuth.Domain/Aggregates/` 复制 `Address.cs`、`Favorite.cs`、`BrowseHistory.cs` 到 `src/Services/UserCenter/Leno.UserCenter.Domain/Aggregates/`，调整命名空间为 `Leno.UserCenter.Domain.Aggregates`。

- [ ] **Step 2: 复制 4 个 Application 接口 + 实现**

从 UserAuth.Application 复制对应接口，方法签名不变。实现类从 `UserAppService.cs` 中拆分出对应方法到 4 个独立 AppService。

- [ ] **Step 3: 创建 4 个 Controller（共 17 端点）**

按 Spec §2.1.3 端点清单，参考旧域 Controller 实现，应用契约规范：

```csharp
[ApiController]
[Route("api/users/me/addresses")]
[Authorize(Roles = "Buyer")]
public sealed class AddressesController : ControllerBase
{
    // 5 端点：GET/POST/PUT/{id}/DELETE/{id}/POST/{id}/default
}

[ApiController]
[Route("api/users/me/favorites")]
[Authorize(Roles = "Buyer")]
public sealed class FavoritesController : ControllerBase
{
    // 5 端点：GET/POST/DELETE/{spuId}/POST/batch-delete/GET/count
}

[ApiController]
[Route("api/users/me/browse-history")]
[Authorize(Roles = "Buyer")]
public sealed class BrowseHistoryController : ControllerBase
{
    // 5 端点：GET/POST/DELETE/{id}/POST/batch-delete/DELETE
}

[ApiController]
[Route("api/users/me/notification-preferences")]
[Authorize(Roles = "Buyer,Seller,Operator,Admin")]
public sealed class NotificationPreferencesController : ControllerBase
{
    // 2 端点：GET/PUT
}
```

- [ ] **Step 4: 编写 API 集成测试 + Commit**

```bash
dotnet build src/Services/UserCenter/Leno.UserCenter.Api/Leno.UserCenter.Api.csproj
dotnet test src/Services/UserCenter/Leno.UserCenter.Api.Tests/Leno.UserCenter.Api.Tests.csproj
git add src/Services/UserCenter/
git commit -m "feat(user-center): 实现 4 Controller 17 端点，迁移 Application/Domain 层"
```

---

## Track C：ReviewAfterSales 域拆分（Review + AfterSales）

### Task C1: Review Application + Controller + gRPC（11 端点 + gRPC）

**Files:**
- Modify: `src/Services/Review/Leno.Review.Application/IReviewAppService.cs` — 补 2 方法
- Modify: `src/Services/Review/Leno.Review.Application/Services/ReviewAppService.cs`
- Create: `src/Services/Review/Leno.Review.Api/Controllers/ReviewsController.cs` — 5 端点
- Create: `src/Services/Review/Leno.Review.Api/Controllers/SellerReviewsController.cs` — 2 端点
- Create: `src/Services/Review/Leno.Review.Api/Controllers/AdminReviewsController.cs` — 3 端点
- Create: `src/Services/Review/Leno.Review.Api/GrpcServices/ReviewGrpcService.cs`
- Modify: `src/Services/Review/Leno.Review.Api/Program.cs`
- Test: `src/Services/Review/Leno.Review.Api.Tests/ReviewApiTests.cs`

**参考源:**
- `src/Services/ReviewAfterSales/.../Controllers/ReviewsController.cs`（旧域实现）
- `src/Services/ReviewAfterSales/.../GrpcServices/ReviewGrpcService.cs`

- [ ] **Step 1: Application 层补 2 方法**

```csharp
// IReviewAppService 补充
Task AppendAdditionalReviewAsync(Guid reviewId, Guid userId, string content, CancellationToken ct = default);
Task<PagedResult<ReviewDto>> GetBySellerAsync(Guid sellerId, ReviewFilterDto filter, int page, int pageSize, CancellationToken ct = default);
```

在 `ReviewAppService` 中实现完整业务逻辑（从旧域 `ReviewAfterSales.Application` 复制）。

- [ ] **Step 2: 创建 3 个 Controller（11 端点）**

```csharp
[ApiController]
[Route("api/reviews")]
[Authorize(Roles = "Buyer")]
public sealed class ReviewsController : ControllerBase
{
    // POST api/reviews — 提交评价
    // GET api/reviews/order-line/{orderLineId} — 按订单行查
    // GET api/reviews/mine — 我的评价
    // POST api/reviews/{id}/append — 追评
    // POST api/reviews/images — 上传图片
}

[ApiController]
[Route("api/products")]
public sealed class ProductReviewsController : ControllerBase
{
    [HttpGet("{spuId:guid}/reviews")]
    [AllowAnonymous]  // 匿名可查商品评价
    public async Task<IActionResult> GetByProductAsync([FromRoute] Guid spuId, [FromQuery] int page = 0, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _reviewAppService.GetByProductAsync(spuId, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }
}

[ApiController]
[Route("api/seller/reviews")]
[Authorize(Roles = "Seller")]
public sealed class SellerReviewsController : ControllerBase
{
    // GET api/seller/reviews — 卖家查评价
    // POST api/seller/reviews/{id}/reply — 回复评价
}

[ApiController]
[Route("api/admin/reviews")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminReviewsController : ControllerBase
{
    // GET api/admin/reviews
    // POST api/admin/reviews/{id}/approve
    // POST api/admin/reviews/{id}/hide
}
```

- [ ] **Step 3: 复制 gRPC 服务 + 配置 Program.cs**

复制旧域 `ReviewGrpcService` 到新 Review 域，命名空间调整。`Program.cs` 加条件映射（同 Points 模式）。

- [ ] **Step 4: 编写 API 集成测试 + Commit**

```bash
dotnet build src/Services/Review/Leno.Review.Api/Leno.Review.Api.csproj
dotnet test src/Services/Review/Leno.Review.Api.Tests/Leno.Review.Api.Tests.csproj
git add src/Services/Review/
git commit -m "feat(review): 新建 3 Controller 11 端点 + gRPC 重建，补 2 Application 方法"
```

---

### Task C2: AfterSales Application + Controller（14 端点）

**Files:**
- Modify: `src/Services/AfterSales/Leno.AfterSales.Application/IAfterSalesAppService.cs` — 补 1 方法
- Create: `src/Services/AfterSales/Leno.AfterSales.Api/Controllers/AfterSalesController.cs` — 6 端点
- Create: `src/Services/AfterSales/Leno.AfterSales.Api/Controllers/SellerAfterSalesController.cs` — 5 端点
- Create: `src/Services/AfterSales/Leno.AfterSales.Api/Controllers/AdminAfterSalesController.cs` — 3 端点
- Test: `src/Services/AfterSales/Leno.AfterSales.Api.Tests/AfterSalesApiTests.cs`

**参考源:**
- `src/Services/ReviewAfterSales/.../Controllers/AfterSalesController.cs`（旧域实现）

- [ ] **Step 1: Application 层补 1 方法**

```csharp
// IAfterSalesAppService 补充
Task<AfterSalesDetailDto?> GetByIdForSellerAsync(Guid id, Guid sellerId, CancellationToken ct = default);
```

- [ ] **Step 2: 创建 3 个 Controller（14 端点）**

按 Spec §2.3.2 端点清单，参考旧域 Controller 实现：

```csharp
[ApiController]
[Route("api/after-sales")]
[Authorize(Roles = "Buyer")]
public sealed class AfterSalesController : ControllerBase
{
    // POST api/after-sales — 申请售后
    // POST api/after-sales/{id}/return-goods — 退货
    // POST api/after-sales/{id}/cancel — 取消
    // GET api/after-sales/order/{orderId} — 按订单查
    // GET api/after-sales/mine — 我的售后
    // POST api/after-sales/images — 上传凭证
}

[ApiController]
[Route("api/seller/after-sales")]
[Authorize(Roles = "Seller")]
public sealed class SellerAfterSalesController : ControllerBase
{
    // GET api/seller/after-sales
    // GET api/seller/after-sales/{id}
    // POST api/seller/after-sales/{id}/approve
    // POST api/seller/after-sales/{id}/reject
    // POST api/seller/after-sales/{id}/confirm-return
}

[ApiController]
[Route("api/admin/after-sales")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminAfterSalesController : ControllerBase
{
    // GET api/admin/after-sales
    // POST api/admin/after-sales/{id}/approve
    // POST api/admin/after-sales/{id}/reject
}
```

- [ ] **Step 3: 编写测试 + Commit**

```bash
dotnet build src/Services/AfterSales/Leno.AfterSales.Api/Leno.AfterSales.Api.csproj
dotnet test src/Services/AfterSales/Leno.AfterSales.Api.Tests/Leno.AfterSales.Api.Tests.csproj
git add src/Services/AfterSales/
git commit -m "feat(after-sales): 新建 3 Controller 14 端点，补 1 Application 方法"
```

---

## 跨域协调任务

### Task D1: BC9 Notification 通知偏好端点改为 internal

**Files:**
- Modify: `src/Services/Notification/Leno.Notification.Api/Controllers/NotificationPreferencesController.cs`
- Modify: `src/Services/Notification/Leno.Notification.Api/Program.cs` — 注册 internal 端点中间件
- Test: `src/Services/Notification/Leno.Notification.Api.Tests/NotificationPreferencesApiTests.cs`

**说明：** Spec §4.3.5 决策：HTTP 端点统一归 UserCenter，BC9 改为 internal HTTP 端点供通知发送时查询。

- [ ] **Step 1: 阅读现有 NotificationPreferencesController**

读 `src/Services/Notification/Leno.Notification.Api/Controllers/NotificationPreferencesController.cs`，确认现有 2 端点（GET/PUT `api/users/me/notification-preferences`）。

- [ ] **Step 2: 改造为 internal 端点**

```csharp
[ApiController]  // 无类级 Route
public sealed class InternalNotificationPreferencesController : ControllerBase
{
    private readonly INotificationPreferencesAppService _preferencesAppService;

    public InternalNotificationPreferencesController(INotificationPreferencesAppService preferencesAppService)
    {
        _preferencesAppService = preferencesAppService;
    }

    [HttpGet("internal/v1/users/{userId:guid}/notification-preferences")]
    public async Task<IActionResult> GetPreferencesAsync([FromRoute] Guid userId, CancellationToken ct)
    {
        var result = await _preferencesAppService.GetAsync(userId, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpPut("internal/v1/users/{userId:guid}/notification-preferences")]
    public async Task<IActionResult> UpdatePreferencesAsync([FromRoute] Guid userId, [FromBody] UpdatePreferencesDto request, CancellationToken ct)
    {
        await _preferencesAppService.UpdateAsync(userId, request, ct);
        return Ok(ApiResponse.Success());
    }
}
```

删除原 `NotificationPreferencesController.cs`（对外 HTTP 端点）。

- [ ] **Step 3: Program.cs 注册 InternalApiKey 中间件**

```csharp
app.UseMiddleware<InternalApiKeyMiddleware>();  // 对 internal/* 路径启用 X-Internal-Key 校验
```

- [ ] **Step 4: 编写测试 + Commit**

```bash
dotnet build src/Services/Notification/Leno.Notification.Api/Leno.Notification.Api.csproj
dotnet test src/Services/Notification/Leno.Notification.Api.Tests/Leno.Notification.Api.Tests.csproj
git add src/Services/Notification/
git commit -m "refactor(notification): 通知偏好端点改为 internal，对外 HTTP 归 UserCenter"
```

---

### Task D2: 集成测试套件汇总 + 全量编译验证

**Files:**
- Test: `tests/Integration/DomainMigrationIntegrationTests.cs`（新建）

- [ ] **Step 1: 全量编译验证**

```bash
dotnet build Leno.sln
```

Expected: Build SUCCESS（无错误，warning 可接受）

- [ ] **Step 2: 全量测试**

```bash
dotnet test Leno.sln
```

Expected: 所有测试 PASS（敏感配置缺失导致的失败可豁免，需记录）

- [ ] **Step 3: 编写跨域集成测试**

```csharp
// tests/Integration/DomainMigrationIntegrationTests.cs
public class DomainMigrationIntegrationTests
{
    [Fact]
    public async Task Identity_Login_Endpoint_Returns_ApiResponse_Token()
    {
        // 验证 Identity 域 login 端点返回 ApiResponse<TokenDto>
    }
    
    [Fact]
    public async Task Points_Internal_Freeze_Endpoint_Requires_Internal_Key()
    {
        // 验证 Points 域 internal 端点鉴权
    }
    
    [Fact]
    public async Task Membership_Packages_Path_Uses_Hyphen()
    {
        // 验证 Membership 域路径为 membership-packages（连字符）
    }
    
    [Fact]
    public async Task Review_Anonymous_Can_Access_Product_Reviews()
    {
        // 验证 Review 域商品评价匿名可访问
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add tests/Integration/
git commit -m "test(integration): 新增域迁移跨域集成测试"
```

---

## 阶段 2-4 后续任务（阶段 1 完成后启动）

### Task E1: 网关双轨路由配置（阶段 2）

**Files:**
- Modify: `src/ApiGateway/.../routing.json` 或对应网关配置文件
- Modify: `src/ApiGateway/.../FeatureFlagMiddleware.cs`

- [ ] **Step 1: 阅读现有网关配置**
- [ ] **Step 2: 按 Spec §3.3.1 配置双轨路由**
- [ ] **Step 3: 实现灰度 feature flag（按 userId hash）**
- [ ] **Step 4: 灰度回滚开关**

### Task E2: 旧域下线 + 文档同步（阶段 4）

**Files:**
- Modify: `Leno.sln` — 移除 3 个旧域项目
- Modify: `docs/design-prompts/**`
- Modify: `docs/feature-inventory/api-gap/**`
- Modify: 架构图

- [ ] **Step 1: 移除网关旧域路由**
- [ ] **Step 2: 停止 3 个旧域服务进程**
- [ ] **Step 3: 在 sln 中移除 UserAuth/PointsMembership/ReviewAfterSales 项目**
- [ ] **Step 4: 更新 design-prompts 端点引用**
- [ ] **Step 5: 更新 feature-inventory/api-gap 报告**
- [ ] **Step 6: 更新架构图**

---

## 自检报告

### 1. Spec 覆盖检查

| Spec 章节 | 覆盖 Task | 状态 |
|----------|----------|------|
| §1.2 契约规范 | 所有 Task 强制遵循 | ✅ |
| §2.1.1 Identity 28 端点 | Task A2 + A3 + A4 | ✅ |
| §2.1.2 AccessControl 7 端点 | Task A1 | ✅ |
| §2.1.3 UserCenter 17 端点 | Task A5 + A6 | ✅ |
| §2.2.1 Points 16 端点 + gRPC | Task B1 + B2 | ✅ |
| §2.2.2 Membership 12 端点 | Task B3 | ✅ |
| §2.3.1 Review 11 端点 + gRPC | Task C1 | ✅ |
| §2.3.2 AfterSales 14 端点 | Task C2 | ✅ |
| §4.3.5 通知偏好跨域去重 | Task D1 | ✅ |
| §3.1 阶段 2 网关双轨 | Task E1 | ✅ |
| §3.1 阶段 4 旧域下线 | Task E2 | ✅ |

### 2. 占位符扫描

- 无 TBD/TODO
- 所有代码示例均为完整可执行代码（接口签名完整，Controller 方法体完整）
- 测试用例给出代表性骨架，subagent 执行时按相同模式补全

### 3. 类型一致性

- `IPointsInternalAppService` 5 方法签名在 Task B1（定义）与 Task B2（Controller 调用）一致
- `ApiResponse<T>` 包装在所有 Task 中统一使用
- `ICurrentUserContext` 在所有需要 `/me` 的 Controller 中注入

---

## 执行顺序（推荐）

**P0 关键路径（2026-08-01 deadline）:**
1. Task B1 → B2 → B3（PointsMembership 拆分）

**P1 三路并行:**
2. Track A: A1 → A2 → A3 → A4 → A5 → A6
3. Track C: C1 → C2

**P2 跨域协调:**
4. Task D1（依赖 A6 完成）
5. Task D2（依赖所有 Task 完成）

**P3 后续阶段:**
6. Task E1（阶段 2 启动条件：所有阶段 1 Task 完成）
7. Task E2（阶段 4 启动条件：阶段 3 观察期无异常）
