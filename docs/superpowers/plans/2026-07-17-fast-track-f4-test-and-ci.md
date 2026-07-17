# 快轨 Wave-F4 关键测试补齐 + CI 守护 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 补齐 PointsMembership.Application.Tests 单元测试至 ≥20 个方法；新建 4 个跨 BC 关键路径集成测试；CI 增加覆盖率门槛校验；为 3 个缺口 BC 补建 Infrastructure.Tests 项目骨架

**Architecture:** 单元测试沿用 xUnit + FluentAssertions + Moq + SUT 模式（参照 `AfterSalesAppServiceTests`/`PointsAppServiceTests`）；集成测试基于 `Leno.Testing/Fixtures/ContainerFixture` 启动 Testcontainers（MsSql + Redis + RabbitMq），新增 `CrossBcIntegrationTestBase` 抽象基类；CI 复用既有 `scripts/check-placeholders.sh`，新增 `scripts/check-coverage.ps1` 解析 reportgenerator JSON

**Tech Stack:** .NET 10、xUnit 2.9.0、FluentAssertions 7.0.0、Moq 4.20.72、coverlet.collector 6.0.2、Testcontainers 4.0.0、MassTransit Test Framework 8.3.6、reportgenerator、WireMock.Net

**关联 spec:** [2026-07-17-comprehensive-optimization-v2-design.md §7](../specs/2026-07-17-comprehensive-optimization-v2-design.md)

**前置依赖:** Plan 1（F1 业务流程修复）完成，4 个集成测试覆盖 F1.1/F1.2/F1.3/F1.4 修复路径

---

## 关键代码定位（实施前必读）

| 位置 | 路径 | 关键发现 |
|---|---|---|
| PointsMembership Application | `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/` | 7 个 AppService：PointsAppService（已测）、ExchangeCouponAppService、MemberAppService、MembershipPackageAppService、PointsInternalAppService、PointsOffsetAppService、TaskAppService |
| PointsMembership.Application.Tests 现状 | `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/` | 仅 `ApplicationTests.cs`（第 13 行类名 `PointsAppServiceTests`）+ `GlobalUsings.cs`；缺 Leno.Testing 引用；未引用 Leno.Infrastructure.Abstractions |
| 文件名与类名不一致 | `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/ApplicationTests.cs:13` | 类名 `PointsAppServiceTests`，文件名应为 `PointsAppServiceTests.cs`，需 `git mv` 重命名 |
| ReviewAfterSales.Application.Tests 现状 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application.Tests/` | **已有 19 个测试**：`AfterSalesAppServiceTests.cs`（9 个）+ `ReviewAppServiceTests.cs`（10 个），满足 spec F4.2 要求 ≥15 个 |
| SellerShop.Application.Tests 现状 | `src/Services/SellerShop/Leno.SellerShop.Application.Tests/` | **已有 27 个测试**：`ShopAppServiceTests.cs`（13）+`SellerDashboardAppServiceTests.cs`（7）+`SellerAppServiceTests.cs`（7），远超 spec 要求 |
| SystemAdmin.Application.Tests 现状 | `src/Services/SystemAdmin/Leno.SystemAdmin.Application.Tests/` | 已有 33 个测试（ScheduledTask/FeatureFlag/DeadLetter/AuditLog），满足 spec |
| 单元测试 SUT 模式参照 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application.Tests/AfterSalesAppServiceTests.cs:21-46` | `Mock<T>` 字段 + `_sut` 系统UnderTest + 构造函数注入 |
| 测试项目 csproj 模板 | `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application.Tests/Leno.ReviewAfterSales.Application.Tests.csproj` | 第 7 行 `<IsTestProject>true</IsTestProject>`；第 11-22 行 6 个包引用；第 26-27 行 ProjectReference 到 Application + Leno.Testing |
| Infrastructure.Tests 缺失 BC | ReviewAfterSales、SellerShop、Notification | 3 个 BC 无 `*.Infrastructure.Tests` 项目，需补建骨架（最小 csproj + GlobalUsings.cs + 1 个占位测试避免空项目） |
| IntegrationTestBase | `src/BuildingBlocks/Leno.Testing/Fixtures/IntegrationTestBase.cs:1-16` | 抽象基类，**当前无任何子类继承**（Testcontainers 基础设施已搭好未使用） |
| ContainerFixture | `src/BuildingBlocks/Leno.Testing/Fixtures/ContainerFixture.cs:11-71` | 启动 4 容器：MsSql、Redis、RabbitMq、Elasticsearch；`SqlConnectionString` 第 25 行、`RedisConnectionString` 第 26 行、`RabbitMqConnectionString` 第 27 行 |
| Leno.Testing.csproj | `src/BuildingBlocks/Leno.Testing/Leno.Testing.csproj:24` | 仅引用 `Leno.SharedKernel`，**未引用 Leno.Infrastructure**；F4.3 集成测试需各 BC 测试项目自己引用 Leno.Infrastructure |
| CI 配置 | `.github/workflows/ci.yml` | 5 个 jobs：build-solution（10-34）、integration-tests（36-46）、build-services（48-85）、docker-build（87-121）、validate-compose（123-129）；第 22-23 行已运行 check-placeholders.sh；第 25 行 `--filter "Category!=Integration"`；第 46 行 `--filter "Category=Integration"`；第 27-29 行已用 reportgenerator 生成 HTML 报告 |
| check-placeholders.sh | `scripts/check-placeholders.sh` | **已存在**，6 项检查（NotImplementedException、空断言、NewFeatureTests、TODO/FIXME、return default!/null!、空测试类），CI 已集成 |
| Category=Integration trait 使用 | 仅 `src/ApiGateway/Leno.ApiGateway.Tests/Integration/Phase6IntegrationTests.cs:86` 1 处 | CI 集成测试 job 实际只跑 1 个测试，与 job 名称承诺的能力不匹配 |
| Directory.Build.props | `Directory.Build.props:25-33` | 集中管理测试包版本变量（`$(XUnitVersion)`、`$(TestContainersVersion)` 等） |
| MassTransit Test Framework | `src/BuildingBlocks/Leno.Infrastructure/Leno.Infrastructure.csproj:26-27` | 已引用 MassTransit 8.3.6，可在测试中使用 InMemory test harness |

---

## Task 1: 重命名 PointsMembership ApplicationTests.cs 为 PointsAppServiceTests.cs

> 修复文件名与类名不一致，避免后续按文件查找测试时困惑。

**Files:**
- Rename: `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/ApplicationTests.cs` → `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsAppServiceTests.cs`

- [ ] **Step 1: 使用 git mv 重命名文件（保留历史）**

```bash
git mv src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/ApplicationTests.cs src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsAppServiceTests.cs
```

- [ ] **Step 2: 编译验证**

```bash
dotnet build src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/Leno.PointsMembership.Application.Tests.csproj
```

预期：编译成功（类名 `PointsAppServiceTests` 未变，namespace 未变）。

- [ ] **Step 3: 运行既有 PointsAppService 测试验证无回归**

```bash
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/Leno.PointsMembership.Application.Tests.csproj
```

预期：原有测试全部 PASS。

- [ ] **Step 4: 提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsAppServiceTests.cs src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/ApplicationTests.cs
git commit -m "refactor(points-membership): 重命名 ApplicationTests.cs 为 PointsAppServiceTests.cs 与类名一致"
```

---

## Task 2: 补齐 PointsMembership Application 层单元测试至 ≥20 个方法

> spec F4.1 要求覆盖 MemberAppService、PointsOffsetAppService、TaskAppService、MembershipPackageAppService 共 ≥20 个测试方法。当前仅 PointsAppService 有测试。

**Files:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/MemberAppServiceTests.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsOffsetAppServiceTests.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/TaskAppServiceTests.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/MembershipPackageAppServiceTests.cs`

- [ ] **Step 1: 阅读 4 个 AppService 接口与实现，记录公共方法签名**

读取以下文件以理解构造函数依赖与公共方法：
- `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/MemberAppService.cs`
- `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsOffsetAppService.cs`
- `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/TaskAppService.cs`
- `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/MembershipPackageAppService.cs`

记录每个 AppService 的：
- 构造函数依赖（仓储、UoW、领域服务、防腐层接口）
- 关键公共方法（写操作优先）

- [ ] **Step 2: 写失败测试 — MemberAppServiceTests（≥5 个方法）**

创建 `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/MemberAppServiceTests.cs`，参照 `PointsAppServiceTests` SUT 模式。

**示例骨架**（实际方法名/依赖需根据 Step 1 阅读结果调整）：

```csharp
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Moq;
using FluentAssertions;

namespace Leno.PointsMembership.Application.Tests;

public class MemberAppServiceTests
{
    private readonly Mock<IMemberRepository> _memberRepoMock = new();
    private readonly Mock<IMembershipLevelRepository> _levelRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly MemberAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    public MemberAppServiceTests()
    {
        _sut = new MemberAppService(_memberRepoMock.Object, _levelRepoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task GetMemberAsync_Existing_ShouldReturnDto()
    {
        // Arrange：根据 MemberAppService 实际方法签名调整
        var member = CreateMember(level: MemberLevel.Regular);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        // Act
        var result = await _sut.GetMemberAsync(UserId);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(UserId);
        result.Level.Should().Be(MemberLevel.Regular);
    }

    [Fact]
    public async Task GetMemberAsync_NotExist_ShouldThrowNotFoundException()
    {
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Member?)null);

        var act = () => _sut.GetMemberAsync(UserId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*不存在*");
    }

    [Fact]
    public async Task UpgradeLevelAsync_Valid_ShouldUpgradeAndSave()
    {
        // Arrange：构造符合条件的会员与目标等级
        var member = CreateMember(level: MemberLevel.Regular, points: 1000);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _levelRepoMock.Setup(r => r.GetByLevelAsync(MemberLevel.Gold, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLevel(MemberLevel.Gold, threshold: 800));

        // Act
        await _sut.UpgradeLevelAsync(UserId, MemberLevel.Gold);

        // Assert
        member.Level.Should().Be(MemberLevel.Gold);
        _memberRepoMock.Verify(r => r.UpdateAsync(member, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpgradeLevelAsync_InsufficientPoints_ShouldThrow()
    {
        var member = CreateMember(level: MemberLevel.Regular, points: 100);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _levelRepoMock.Setup(r => r.GetByLevelAsync(MemberLevel.Gold, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLevel(MemberLevel.Gold, threshold: 800));

        var act = () => _sut.UpgradeLevelAsync(UserId, MemberLevel.Gold);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*积分不足*");
        _memberRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Member>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DowngradeLevelAsync_Valid_ShouldDowngradeAndSave()
    {
        var member = CreateMember(level: MemberLevel.Gold, points: 200);
        _memberRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _levelRepoMock.Setup(r => r.GetByLevelAsync(MemberLevel.Regular, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateLevel(MemberLevel.Regular, threshold: 0));

        await _sut.DowngradeLevelAsync(UserId, MemberLevel.Regular);

        member.Level.Should().Be(MemberLevel.Regular);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Member CreateMember(MemberLevel level = MemberLevel.Regular, int points = 0)
    {
        // 根据 Member 聚合实际工厂方法调整
        return Member.Create(MemberId, UserId, level, points);
    }

    private static MembershipLevel CreateLevel(MemberLevel level, int threshold)
    {
        // 根据 MembershipLevel 实体实际工厂方法调整
        return MembershipLevel.Create(level, threshold);
    }
}
```

注意：上述代码基于推测的方法签名，Step 1 阅读后必须按实际签名调整。如果方法名、参数、聚合工厂方法不同，需全部对齐。

- [ ] **Step 3: 运行 MemberAppServiceTests 验证失败**

```bash
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/Leno.PointsMembership.Application.Tests.csproj --filter "FullyQualifiedName~MemberAppServiceTests"
```

预期：FAIL（如构造函数签名或方法名不匹配）或 PASS（如 Step 2 推测正确）。

- [ ] **Step 4: 根据 Step 1 阅读结果修正测试代码，使其编译通过且全部 PASS**

修正所有不匹配的方法签名、工厂方法、命名空间。每个测试方法命名遵循 `方法名_场景_期望结果` 格式。

- [ ] **Step 5: 创建 PointsOffsetAppServiceTests.cs（≥5 个方法）**

创建 `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsOffsetAppServiceTests.cs`：

```csharp
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Moq;
using FluentAssertions;

namespace Leno.PointsMembership.Application.Tests;

/// <summary>
/// 积分抵现应用服务单元测试，覆盖试算、冻结、释放、确认扣减四个核心流程。
/// </summary>
public class PointsOffsetAppServiceTests
{
    private readonly Mock<IPointsAccountRepository> _accountRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly PointsOffsetAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    public PointsOffsetAppServiceTests()
    {
        _sut = new PointsOffsetAppService(_accountRepoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task CalculateOffsetAsync_SufficientBalance_ShouldReturnOffsetAmount()
    {
        // Arrange：根据实际方法签名调整（如 CalculateOffsetAsync(userId, orderAmount, rate)）
        var account = CreateAccount(availablePoints: 1000);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _sut.CalculateOffsetAsync(UserId, orderAmount: 100m, pointsRate: 0.01m);

        // Assert
        result.OffsetPoints.Should().BeLessOrEqualTo(1000);
        result.OffsetAmount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CalculateOffsetAsync_AccountNotExist_ShouldThrow()
    {
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);

        var act = () => _sut.CalculateOffsetAsync(UserId, 100m, 0.01m);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*账户不存在*");
    }

    [Fact]
    public async Task FreezeAsync_Valid_ShouldFreezeAndSave()
    {
        var account = CreateAccount(availablePoints: 500);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.FreezeAsync(UserId, OrderId, pointsToFreeze: 200);

        account.AvailablePoints.Should().Be(300);
        account.FrozenPoints.Should().Be(200);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FreezeAsync_InsufficientBalance_ShouldThrow()
    {
        var account = CreateAccount(availablePoints: 100);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var act = () => _sut.FreezeAsync(UserId, OrderId, pointsToFreeze: 200);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*积分不足*");
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseAsync_Valid_ShouldReleaseAndSave()
    {
        var account = CreateAccount(availablePoints: 300, frozenPoints: 200);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.ReleaseAsync(UserId, OrderId);

        account.AvailablePoints.Should().Be(500);
        account.FrozenPoints.Should().Be(0);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmDeductAsync_Valid_ShouldDeductFrozenAndSave()
    {
        var account = CreateAccount(availablePoints: 300, frozenPoints: 200);
        _accountRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        await _sut.ConfirmDeductAsync(UserId, OrderId);

        account.AvailablePoints.Should().Be(300);
        account.FrozenPoints.Should().Be(0);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PointsAccount CreateAccount(int availablePoints, int frozenPoints = 0)
    {
        // 根据 PointsAccount 聚合实际工厂方法调整
        var account = PointsAccount.Create(AccountId, UserId);
        // 通过反射或内部测试 hook 设置初始积分（根据聚合实际 API）
        return account;
    }
}
```

- [ ] **Step 6: 修正 PointsOffsetAppServiceTests 直至编译通过且 6 个测试 PASS**

- [ ] **Step 7: 创建 TaskAppServiceTests.cs（≥5 个方法）**

创建 `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/TaskAppServiceTests.cs`，覆盖任务完成奖励、任务重置、防重复完成场景。

骨架参照 Step 2/5，根据 `TaskAppService` 实际公共方法（如 `CompleteTaskAsync`、`ResetTaskAsync`、`GetTaskListAsync`）编写 ≥5 个测试。

- [ ] **Step 8: 创建 MembershipPackageAppServiceTests.cs（≥5 个方法）**

创建 `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/MembershipPackageAppServiceTests.cs`，覆盖套餐订阅、续费、取消场景。

骨架参照 Step 2/5，根据 `MembershipPackageAppService` 实际公共方法编写 ≥5 个测试。

- [ ] **Step 9: 运行全部 PointsMembership.Application.Tests 验证 ≥20 个方法**

```bash
dotnet test src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/Leno.PointsMembership.Application.Tests.csproj --verbosity normal
```

预期：测试方法总数 ≥ 20（原 PointsAppService 5 个 + MemberAppService 5 个 + PointsOffsetAppService 6 个 + TaskAppService 5 个 + MembershipPackageAppService 5 个 ≈ 26 个），全部 PASS。

- [ ] **Step 10: 提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/
git commit -m "test(points-membership): 补齐 MemberAppService/PointsOffsetAppService/TaskAppService/MembershipPackageAppService 单元测试至 26 个方法"
```

---

## Task 3: 为 ReviewAfterSales/SellerShop/Notification 补建 Infrastructure.Tests 项目骨架

> 这 3 个 BC 当前无 `*.Infrastructure.Tests` 项目。补建骨架避免 CI matrix 漏跑，并防止未来 Infrastructure 层无测试守护。

**Files:**
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/Leno.ReviewAfterSales.Infrastructure.Tests.csproj`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/GlobalUsings.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/SmokeTests.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/GlobalUsings.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/SmokeTests.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure.Tests/Leno.Notification.Infrastructure.Tests.csproj`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure.Tests/GlobalUsings.cs`
- Create: `src/Services/Notification/Leno.Notification.Infrastructure.Tests/SmokeTests.cs`

- [ ] **Step 1: 读取 Infrastructure.Tests.csproj 模板**

读取 `src/Services/UserAuth/Leno.UserAuth.Infrastructure.Tests/Leno.UserAuth.Infrastructure.Tests.csproj` 作为模板，记录包引用与 ProjectReference 模式。

- [ ] **Step 2: 创建 ReviewAfterSales.Infrastructure.Tests 项目**

创建 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/Leno.ReviewAfterSales.Infrastructure.Tests.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="$(CoverletCollectorVersion)" />
    <PackageReference Include="FluentAssertions" Version="$(FluentAssertionsVersion)" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$(MicrosoftNetTestSdkVersion)" />
    <PackageReference Include="Moq" Version="$(MoqVersion)" />
    <PackageReference Include="xunit" Version="$(XUnitVersion)" />
    <PackageReference Include="xunit.runner.visualstudio" Version="$(XUnitRunnerVersion)" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\BuildingBlocks\Leno.Testing\Leno.Testing.csproj" />
    <ProjectReference Include="..\Leno.ReviewAfterSales.Infrastructure\Leno.ReviewAfterSales.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

创建 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/GlobalUsings.cs`：

```csharp
global using Xunit;
global using FluentAssertions;
global using Moq;
```

创建 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/SmokeTests.cs`：

```csharp
namespace Leno.ReviewAfterSales.Infrastructure.Tests;

/// <summary>
/// 基础冒烟测试，验证项目可加载与执行；F4.3 将在此项目内补充集成测试。
/// </summary>
public class SmokeTests
{
    [Fact]
    public void ProjectAssembly_ShouldLoadSuccessfully()
    {
        var assembly = typeof(Leno.ReviewAfterSales.Infrastructure.ReviewAfterSalesDbContext).Assembly;
        assembly.FullName.Should().NotBeNull();
        assembly.GetName().Name.Should().Be("Leno.ReviewAfterSales.Infrastructure");
    }
}
```

- [ ] **Step 3: 创建 SellerShop.Infrastructure.Tests 项目**

复制 Step 2 模式，将命名空间/类型名替换为 `Leno.SellerShop.Infrastructure`，路径替换为 `src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/`。

注意：SmokeTests 中 `typeof(Leno.SellerShop.Infrastructure.SellerShopDbContext)`。

- [ ] **Step 4: 创建 Notification.Infrastructure.Tests 项目**

复制 Step 2 模式，将命名空间/类型名替换为 `Leno.Notification.Infrastructure`，路径替换为 `src/Services/Notification/Leno.Notification.Infrastructure.Tests/`。

注意：SmokeTests 中 `typeof(Leno.Notification.Infrastructure.NotificationDbContext)`。

- [ ] **Step 5: 添加 3 个新项目到 Leno.sln**

```bash
dotnet sln Leno.sln add src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/Leno.ReviewAfterSales.Infrastructure.Tests.csproj
dotnet sln Leno.sln add src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj
dotnet sln Leno.sln add src/Services/Notification/Leno.Notification.Infrastructure.Tests/Leno.Notification.Infrastructure.Tests.csproj
```

- [ ] **Step 6: 编译与运行新项目测试**

```bash
dotnet build Leno.sln
dotnet test src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/Leno.ReviewAfterSales.Infrastructure.Tests.csproj
dotnet test src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj
dotnet test src/Services/Notification/Leno.Notification.Infrastructure.Tests/Leno.Notification.Infrastructure.Tests.csproj
```

预期：编译成功，每个项目 SmokeTests 的 1 个测试 PASS。

- [ ] **Step 7: 提交**

```bash
git add src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/ src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/ src/Services/Notification/Leno.Notification.Infrastructure.Tests/ Leno.sln
git commit -m "test: 补建 ReviewAfterSales/SellerShop/Notification 三个 BC 的 Infrastructure.Tests 项目骨架"
```

---

## Task 4: 新建 CrossBcIntegrationTestBase 抽象基类

> spec F4.3 要求 4 个跨 BC 集成测试，需要共享基类管理 Testcontainers + MassTransit 测试套件 + DbContext 迁移。

**Files:**
- Modify: `src/BuildingBlocks/Leno.Testing/Leno.Testing.csproj`（添加 MassTransit Test Framework 与 Leno.Infrastructure 引用）
- Create: `src/BuildingBlocks/Leno.Testing/Fixtures/CrossBcIntegrationTestBase.cs`

- [ ] **Step 1: 修改 Leno.Testing.csproj 添加测试框架依赖**

修改 `src/BuildingBlocks/Leno.Testing/Leno.Testing.csproj`，在 ItemGroup 中添加：

```xml
<ItemGroup>
  <ProjectReference Include="..\Leno.SharedKernel\Leno.SharedKernel.csproj" />
  <ProjectReference Include="..\Leno.Infrastructure\Leno.Infrastructure.csproj" />
</ItemGroup>

<ItemGroup>
  <PackageReference Include="MassTransit" Version="$(MassTransitVersion)" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="$(EntityFrameworkCoreVersion)" />
  <PackageReference Include="FluentAssertions" Version="$(FluentAssertionsVersion)" />
  <PackageReference Include="xunit" Version="$(XUnitVersion)" />
</ItemGroup>
```

注意：包版本变量需先在 `Directory.Build.props:25-33` 确认存在。若 `$(MassTransitVersion)` 与 `$(EntityFrameworkCoreVersion)` 未定义，需在 Directory.Build.props 中补充：

```xml
<MassTransitVersion>8.3.6</MassTransitVersion>
<EntityFrameworkCoreVersion>10.0.0</EntityFrameworkCoreVersion>
```

- [ ] **Step 2: 创建 CrossBcIntegrationTestBase 抽象基类**

创建 `src/BuildingBlocks/Leno.Testing/Fixtures/CrossBcIntegrationTestBase.cs`：

```csharp
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace Leno.Testing.Fixtures;

/// <summary>
/// 跨 BC 集成测试基类：基于 ContainerFixture 启动真实 Testcontainers（MsSql + Redis + RabbitMq），
/// 提供 MassTransit InMemoryTestHarness 或 RabbitMqTestHarness 选项，
/// 子类注册具体 DbContext 与消费者，验证跨 BC 事件流转。
/// 所有测试方法自动标记 [Trait("Category", "Integration")]（通过 Assembly 属性或基类 Trait）。
/// </summary>
[Collection(ContainerCollection.Name)]
[Trait("Category", "Integration")]
public abstract class CrossBcIntegrationTestBase<TDbContext> : IAsyncLifetime
    where TDbContext : DbContext
{
    protected readonly ContainerFixture Fixture;
    protected IServiceProvider ServiceProvider { get; private set; } = null!;
    protected ITestHarness TestHarness { get; private set; } = null!;

    protected CrossBcIntegrationTestBase(ContainerFixture fixture)
    {
        Fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(Fixture.RedisConnectionString);
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddDebug());

        // 注册 Redis 与分布式锁
        services.AddSingleton<IConnectionMultiplexer>(_ => multiplexer);
        services.AddDistributedRedisLock(_ => multiplexer);
        services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();

        // MassTransit Test Harness（连接到 Testcontainers RabbitMq）
        services.AddMassTransitTestHarness(cfg =>
        {
            ConfigureConsumers(cfg);
        });

        // 子类注册 DbContext 与其他服务
        ConfigureServices(services, Fixture.SqlConnectionString, Fixture.RabbitMqConnectionString);

        ServiceProvider = services.BuildServiceProvider();

        // 执行迁移
        await ServiceProvider.MigrateWithLockAsync<TDbContext>();

        // 启动 MassTransit Test Harness
        TestHarness = ServiceProvider.GetRequiredService<ITestHarness>();
        await TestHarness.Start();
    }

    public async Task DisposeAsync()
    {
        if (TestHarness is not null)
        {
            await TestHarness.Stop();
        }
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        await Task.CompletedTask;
    }

    protected abstract void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString);

    protected abstract void ConfigureConsumers(IBusRegistrationConfigurator configurator);
}
```

- [ ] **Step 3: 编译验证 Leno.Testing**

```bash
dotnet build src/BuildingBlocks/Leno.Testing/Leno.Testing.csproj
```

预期：编译成功。若失败，根据错误调整包引用与方法签名。

- [ ] **Step 4: 提交**

```bash
git add src/BuildingBlocks/Leno.Testing/Leno.Testing.csproj src/BuildingBlocks/Leno.Testing/Fixtures/CrossBcIntegrationTestBase.cs Directory.Build.props
git commit -m "test(infrastructure): 新增 CrossBcIntegrationTestBase 跨 BC 集成测试基类，集成 Testcontainers + MassTransit TestHarness"
```

---

## Task 5: 创建 SeckillOrderFlowIntegrationTests（秒杀下单流程集成测试）

> 覆盖 spec F4.3 第 1 个集成测试：秒杀下单 → 事件 → Order 创建 → 支付 → 秒杀确认（依赖 Plan 1 F1.1 修复完成）。

**Files:**
- Create: `src/Services/Order/Leno.Order.Infrastructure.Tests/Integration/SeckillOrderFlowIntegrationTests.cs`

- [ ] **Step 1: 修改 Order.Infrastructure.Tests.csproj 引用 Leno.Testing 与 Leno.Order.Application**

读取 `src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj`，确认是否已引用 `Leno.Testing`。如未引用，添加：

```xml
<ProjectReference Include="..\..\..\BuildingBlocks\Leno.Testing\Leno.Testing.csproj" />
<ProjectReference Include="..\Leno.Order.Application\Leno.Order.Application.csproj" />
```

- [ ] **Step 2: 创建 SeckillOrderFlowIntegrationTests**

创建 `src/Services/Order/Leno.Order.Infrastructure.Tests/Integration/SeckillOrderFlowIntegrationTests.cs`：

```csharp
using FluentAssertions;
using Leno.Order.Infrastructure;
using Leno.Order.Infrastructure.Services;
using Leno.SharedContracts.Events;
using Leno.Testing.Fixtures;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Leno.Order.Infrastructure.Tests.Integration;

/// <summary>
/// 秒杀下单全流程集成测试：覆盖 Promotion 发布 SeckillOrderCreatedEvent → Order BC 消费 → 创建订单。
/// 依赖 Plan 1 F1.1 已补建 SeckillOrderCreatedEventConsumer。
/// </summary>
public class SeckillOrderFlowIntegrationTests : CrossBcIntegrationTestBase<OrderDbContext>
{
    public SeckillOrderFlowIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(sqlConnectionString));
        services.AddScoped<SeckillOrderCreatedEventConsumer>();
    }

    protected override void ConfigureConsumers(IBusRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<SeckillOrderCreatedEventConsumer>();
    }

    [Fact]
    public async Task SeckillOrderCreatedEvent_Published_ShouldCreateOrderInOrderDbContext()
    {
        // Arrange
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var expectedOrderId = Guid.NewGuid();
        var publishTime = DateTime.UtcNow;

        // Act：发布 SeckillOrderCreatedIntegrationEvent 到 TestHarness
        await TestHarness.Bus.Publish(new SeckillOrderCreatedIntegrationEvent(
            activityId, expectedOrderId, userId, sellerId, skuId, quantity: 1,
            unitPrice: 99.9m, publishTime));

        // Assert：消费者收到事件
        var consumed = await TestHarness.Consumed.Any<SeckillOrderCreatedIntegrationEvent>(TimeSpan.FromSeconds(10));
        consumed.Should().BeTrue("SeckillOrderCreatedEventConsumer 应消费 SeckillOrderCreatedIntegrationEvent");

        // Assert：订单已创建
        await using var scope = ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == expectedOrderId);
        order.Should().NotBeNull("Order BC 应创建秒杀订单");
        order!.OrderType.Should().Be(OrderType.Seckill, "秒杀订单 OrderType 应为 Seckill");
    }
}
```

注意：`SeckillOrderCreatedEventConsumer` 与 `SeckillOrderCreatedIntegrationEvent` 的命名空间、构造函数依赖需根据 Plan 1 实际落地代码调整。`OrderType.Seckill` 假设已存在（Plan 1 Task 4 已确认 Order 聚合支持）。

- [ ] **Step 3: 运行集成测试验证**

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SeckillOrderFlowIntegrationTests"
```

预期：测试 PASS。首次运行会拉取镜像，耗时 5-10 分钟。

若 FAIL，检查：
- `SeckillOrderCreatedEventConsumer` 是否正确注册
- 消费者是否正确调用 `OrderSagaOrchestrator` 或直接创建 Order
- DbContext 迁移是否执行成功

- [ ] **Step 4: 提交**

```bash
git add src/Services/Order/Leno.Order.Infrastructure.Tests/Integration/SeckillOrderFlowIntegrationTests.cs src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj
git commit -m "test(order): 新增 SeckillOrderFlowIntegrationTests 验证秒杀下单全流程事件流转"
```

---

## Task 6: 创建 ForceCancelRefundIntegrationTests（强制取消退款集成测试）

> 覆盖 spec F4.3 第 2 个集成测试：强制取消 → Outbox 退款事件 → Payment 处理退款（依赖 Plan 1 F1.2 修复完成）。

**Files:**
- Create: `src/Services/Order/Leno.Order.Infrastructure.Tests/Integration/ForceCancelRefundIntegrationTests.cs`

- [ ] **Step 1: 创建 ForceCancelRefundIntegrationTests**

创建 `src/Services/Order/Leno.Order.Infrastructure.Tests/Integration/ForceCancelRefundIntegrationTests.cs`：

```csharp
using FluentAssertions;
using Leno.Infrastructure.Outbox;
using Leno.Order.Infrastructure;
using Leno.SharedContracts.Events;
using Leno.Testing.Fixtures;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Leno.Order.Infrastructure.Tests.Integration;

/// <summary>
/// ForceCancel 退款流程集成测试：覆盖 OrderAppService.ForceCancelAsync 通过 Outbox 发布 RefundRequestedIntegrationEvent。
/// 依赖 Plan 1 F1.2 已修复 Outbox 模式（不再直接 _eventBus.PublishAsync）。
/// </summary>
public class ForceCancelRefundIntegrationTests : CrossBcIntegrationTestBase<OrderDbContext>
{
    public ForceCancelRefundIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(sqlConnectionString));
    }

    protected override void ConfigureConsumers(IBusRegistrationConfigurator configurator)
    {
        // 本测试不注册消费者，仅验证 Outbox 表内有待发布消息
    }

    [Fact]
    public async Task ForceCancelAsync_ShouldWriteRefundEventToOutbox_NotDirectlyPublish()
    {
        // Arrange：插入一个已支付订单
        await using (var scope = ServiceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var order = CreatePaidOrder();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            var orderId = order.Id;

            // Act：调用 ForceCancel（需通过 OrderAppService，注入 ICurrentUserContext 模拟管理员）
            // 注意：实际调用需通过 OrderAppService，此处简化为直接验证 Outbox
            // 见下方 Step 2 调整为通过 AppService 调用
        }

        // Assert：OutboxMessages 表应包含 RefundRequestedIntegrationEvent 记录
        await using (var verifyScope = ServiceProvider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var outboxMessage = await db.OutboxMessages
                .FirstOrDefaultAsync(m => m.EventType == nameof(RefundRequestedIntegrationEvent));

            outboxMessage.Should().NotBeNull("ForceCancel 应通过 Outbox 发布 RefundRequestedIntegrationEvent");
            outboxMessage!.ProcessedAt.Should().BeNull("Outbox 消息初始状态应为未发布");
        }

        // Assert：TestHarness 不应直接收到事件（因为未通过 _eventBus.PublishAsync）
        var directPublished = await TestHarness.Published.Any<RefundRequestedIntegrationEvent>(TimeSpan.FromSeconds(2));
        directPublished.Should().BeFalse("ForceCancel 不应直接通过 EventBus 发布，应由 OutboxPublisher 后台处理");
    }

    private static Leno.Order.Domain.Aggregates.Order CreatePaidOrder()
    {
        // 根据 Order 聚合实际工厂方法构造已支付订单
        // 返回 Order.Create(...) 后调用 MarkAsPaid()
        throw new NotImplementedException("Step 2 实现时根据 Order 聚合实际 API 填充");
    }
}
```

- [ ] **Step 2: 完善 CreatePaidOrder 工厂方法与 AppService 调用**

阅读 `src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs` 工厂方法，替换 `throw new NotImplementedException` 为真实构造代码。

同时考虑通过 `OrderAppService.ForceCancelAsync` 调用而非直接操作 DbContext，需在 `ConfigureServices` 中注册 `OrderAppService` 及其依赖（防腐层 Mock、仓储等）。

- [ ] **Step 3: 运行集成测试验证**

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ForceCancelRefundIntegrationTests"
```

预期：测试 PASS。验证 Outbox 表内有 RefundRequestedIntegrationEvent 记录，TestHarness 未直接收到事件。

- [ ] **Step 4: 提交**

```bash
git add src/Services/Order/Leno.Order.Infrastructure.Tests/Integration/ForceCancelRefundIntegrationTests.cs
git commit -m "test(order): 新增 ForceCancelRefundIntegrationTests 验证 ForceCancel 通过 Outbox 发布退款事件"
```

---

## Task 7: 创建 CartProductSyncIntegrationTests（购物车商品同步集成测试）

> 覆盖 spec F4.3 第 3 个集成测试：商品下架 → 事件 → Cart 标记 SKU 无效 → 结算拦截（依赖 Plan 1 F1.3 修复完成）。

**Files:**
- Create: `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Integration/CartProductSyncIntegrationTests.cs`

- [ ] **Step 1: 修改 Cart.Infrastructure.Tests.csproj 引用 Leno.Testing**

读取 `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj`，确认是否已引用 `Leno.Testing`。如未引用，添加：

```xml
<ProjectReference Include="..\..\..\BuildingBlocks\Leno.Testing\Leno.Testing.csproj" />
```

- [ ] **Step 2: 创建 CartProductSyncIntegrationTests**

创建 `src/Services/Cart/Leno.Cart.Infrastructure.Tests/Integration/CartProductSyncIntegrationTests.cs`：

```csharp
using FluentAssertions;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Infrastructure;
using Leno.Cart.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.Testing.Fixtures;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Leno.Cart.Infrastructure.Tests.Integration;

/// <summary>
/// 购物车商品同步集成测试：覆盖 Product 发布 SKU 下架事件 → Cart ProductEventConsumer 消费 → Cart 标记 SKU 无效。
/// 依赖 Plan 1 F1.3 已实现 ProductEventConsumer（不再仅记日志返回）。
/// </summary>
public class CartProductSyncIntegrationTests : CrossBcIntegrationTestBase<CartDbContext>
{
    public CartProductSyncIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        services.AddDbContext<CartDbContext>(options => options.UseSqlServer(sqlConnectionString));
    }

    protected override void ConfigureConsumers(IBusRegistrationConfigurator configurator)
    {
        configurator.AddConsumer<SkuUnavailableEventConsumer>();
        configurator.AddConsumer<ProductPublishedEventConsumer>();
        configurator.AddConsumer<ProductUnpublishedEventConsumer>();
    }

    [Fact]
    public async Task SkuUnavailableEvent_Published_ShouldMarkCartItemsInvalid()
    {
        // Arrange：插入一个含目标 SKU 的购物车
        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();

        await using (var scope = ServiceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CartDbContext>();
            var cart = Cart.Create(userId);
            cart.AddItem(skuId, quantity: 2, unitPrice: 99.9m, snapshot: null);
            db.Carts.Add(cart);
            await db.SaveChangesAsync();
        }

        // Act：发布 SKU 下架事件
        await TestHarness.Bus.Publish(new SkuUnavailableIntegrationEvent(skuId, DateTime.UtcNow));

        // Assert：消费者收到事件
        var consumed = await TestHarness.Consumed.Any<SkuUnavailableIntegrationEvent>(TimeSpan.FromSeconds(10));
        consumed.Should().BeTrue("ProductEventConsumer 应消费 SkuUnavailableIntegrationEvent");

        // Assert：购物车中该 SKU 项已标记无效
        await using (var verifyScope = ServiceProvider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<CartDbContext>();
            var cart = await db.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
            cart.Should().NotBeNull();
            var item = cart!.Items.FirstOrDefault(i => i.SkuId == skuId);
            item.Should().NotBeNull();
            item!.IsValid.Should().BeFalse("SKU 下架后购物车项应标记为无效");
        }
    }
}
```

注意：`SkuUnavailableEventConsumer`、`ProductPublishedEventConsumer`、`ProductUnpublishedEventConsumer` 与 `SkuUnavailableIntegrationEvent` 的命名空间需根据 Plan 1 F1.3 实际落地代码调整。

- [ ] **Step 3: 运行集成测试验证**

```bash
dotnet test src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj --filter "FullyQualifiedName~CartProductSyncIntegrationTests"
```

预期：测试 PASS。

- [ ] **Step 4: 提交**

```bash
git add src/Services/Cart/Leno.Cart.Infrastructure.Tests/Integration/CartProductSyncIntegrationTests.cs src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj
git commit -m "test(cart): 新增 CartProductSyncIntegrationTests 验证商品下架事件同步至购物车"
```

---

## Task 8: 创建 SellerOwnershipIntegrationTests（卖家越权集成测试）

> 覆盖 spec F4.3 第 4 个集成测试：非归属卖家调用 → 403（依赖 Plan 1 F1.4 修复完成）。

**Files:**
- Create: `src/Services/Order/Leno.Order.Infrastructure.Tests/Integration/SellerOwnershipIntegrationTests.cs`
- Create: `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/Integration/SellerOwnershipIntegrationTests.cs`

- [ ] **Step 1: 创建 Order BC 的 SellerOwnershipIntegrationTests**

创建 `src/Services/Order/Leno.Order.Infrastructure.Tests/Integration/SellerOwnershipIntegrationTests.cs`：

```csharp
using FluentAssertions;
using Leno.Order.Infrastructure;
using Leno.Testing.Fixtures;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Leno.Order.Infrastructure.Tests.Integration;

/// <summary>
/// 卖家越权集成测试：覆盖非归属卖家调用 Order ShipAsync 应抛异常。
/// 依赖 Plan 1 F1.4 已修复 OrderAppService.ShipAsync 越权校验。
/// </summary>
public class SellerOwnershipIntegrationTests : CrossBcIntegrationTestBase<OrderDbContext>
{
    public SellerOwnershipIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(sqlConnectionString));
        // 注册 OrderAppService 与依赖（仓储、UoW、防腐层 Mock）
    }

    protected override void ConfigureConsumers(IBusRegistrationConfigurator configurator)
    {
        // 本测试不涉及事件消费
    }

    [Fact]
    public async Task ShipAsync_NonOwnerSeller_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange：插入一个归属卖家 A 的订单
        var sellerA = Guid.NewGuid();
        var sellerB = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await using (var scope = ServiceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var order = CreateOrder(sellerId: sellerA, orderId);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        // Act：卖家 B 尝试发货
        using var actScope = ServiceProvider.CreateScope();
        var appService = actScope.ServiceProvider.GetRequiredService<Leno.Order.Application.Services.OrderAppService>();

        var act = () => appService.ShipAsync(sellerB, orderId, "SF1234567890");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*无权操作*");
    }

    [Fact]
    public async Task ShipAsync_OwnerSeller_ShouldSucceed()
    {
        // Arrange
        var sellerA = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await using (var scope = ServiceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var order = CreateOrder(sellerId: sellerA, orderId);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        // Act
        using var actScope = ServiceProvider.CreateScope();
        var appService = actScope.ServiceProvider.GetRequiredService<Leno.Order.Application.Services.OrderAppService>();

        await appService.ShipAsync(sellerA, orderId, "SF1234567890");

        // Assert：订单状态变更为 Shipped
        await using var verifyScope = ServiceProvider.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        order!.Status.Should().Be(Leno.Order.Domain.Aggregates.OrderStatus.Shipped);
    }

    private static Leno.Order.Domain.Aggregates.Order CreateOrder(Guid sellerId, Guid orderId)
    {
        // 根据 Order 聚合实际工厂方法构造（含 SellerId 与待发货状态）
        throw new NotImplementedException("实现时根据 Order 聚合实际 API 填充");
    }
}
```

- [ ] **Step 2: 创建 ReviewAfterSales BC 的 SellerOwnershipIntegrationTests**

创建 `src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/Integration/SellerOwnershipIntegrationTests.cs`：

```csharp
using FluentAssertions;
using Leno.ReviewAfterSales.Infrastructure;
using Leno.Testing.Fixtures;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Leno.ReviewAfterSales.Infrastructure.Tests.Integration;

/// <summary>
/// 卖家越权集成测试（ReviewAfterSales BC）：覆盖非归属卖家调用 ApproveAfterSalesAsync 应抛异常。
/// 依赖 Plan 1 F1.4 已修复 AfterSalesAppService.ApproveAfterSalesAsync 越权校验。
/// </summary>
public class SellerOwnershipIntegrationTests : CrossBcIntegrationTestBase<ReviewAfterSalesDbContext>
{
    public SellerOwnershipIntegrationTests(ContainerFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureServices(IServiceCollection services, string sqlConnectionString, string rabbitMqConnectionString)
    {
        services.AddDbContext<ReviewAfterSalesDbContext>(options => options.UseSqlServer(sqlConnectionString));
    }

    protected override void ConfigureConsumers(IBusRegistrationConfigurator configurator)
    {
    }

    [Fact]
    public async Task ApproveAfterSalesAsync_NonOwnerSeller_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var sellerA = Guid.NewGuid();
        var sellerB = Guid.NewGuid();
        var afterSalesId = Guid.NewGuid();

        await using (var scope = ServiceProvider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReviewAfterSalesDbContext>();
            var afterSales = CreateAfterSales(sellerId: sellerA, afterSalesId);
            db.AfterSales.Add(afterSales);
            await db.SaveChangesAsync();
        }

        // Act
        using var actScope = ServiceProvider.CreateScope();
        var appService = actScope.ServiceProvider.GetRequiredService<Leno.ReviewAfterSales.Application.Services.AfterSalesAppService>();

        var act = () => appService.ApproveAfterSalesAsync(sellerB, afterSalesId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*无权操作*");
    }

    private static Leno.ReviewAfterSales.Domain.Aggregates.AfterSales CreateAfterSales(Guid sellerId, Guid afterSalesId)
    {
        throw new NotImplementedException("实现时根据 AfterSales 聚合实际 API 填充");
    }
}
```

- [ ] **Step 3: 完善 CreateOrder 与 CreateAfterSales 工厂方法，注册 AppService 依赖**

阅读 Order 聚合与 AfterSales 聚合的工厂方法，替换 `throw new NotImplementedException`。在 `ConfigureServices` 中注册 `OrderAppService`/`AfterSalesAppService` 及其依赖（仓储、UoW、防腐层 Mock、ICurrentUserContext）。

- [ ] **Step 4: 运行集成测试验证**

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SellerOwnershipIntegrationTests"
dotnet test src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/Leno.ReviewAfterSales.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SellerOwnershipIntegrationTests"
```

预期：测试 PASS（3 个方法）。

- [ ] **Step 5: 提交**

```bash
git add src/Services/Order/Leno.Order.Infrastructure.Tests/Integration/SellerOwnershipIntegrationTests.cs src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure.Tests/Integration/SellerOwnershipIntegrationTests.cs
git commit -m "test: 新增 SellerOwnershipIntegrationTests 验证 Order/ReviewAfterSales 卖家越权拦截"
```

---

## Task 9: 创建 check-coverage.ps1 覆盖率门槛校验脚本

> spec F4.4 要求 Domain ≥80%、Application ≥60%、Infrastructure ≥40% 门槛校验。

**Files:**
- Create: `scripts/check-coverage.ps1`
- Create: `scripts/coverage-thresholds.json`

- [ ] **Step 1: 创建 coverage-thresholds.json 配置文件**

创建 `scripts/coverage-thresholds.json`：

```json
{
  "thresholds": [
    {
      "layer": "Domain",
      "pathPattern": "src/Services/*/Leno.*.Domain/Leno.*.Domain.csproj",
      "minimumLineCoverage": 80
    },
    {
      "layer": "Application",
      "pathPattern": "src/Services/*/Leno.*.Application/Leno.*.Application.csproj",
      "minimumLineCoverage": 60
    },
    {
      "layer": "Infrastructure",
      "pathPattern": "src/Services/*/Leno.*.Infrastructure/Leno.*.Infrastructure.csproj",
      "minimumLineCoverage": 40
    }
  ],
  "temporaryExemptions": {
    "PointsMembership.Application": {
      "until": "F4.1-completed",
      "reason": "F4.1 补齐前豁免，F4 合并后转为阻止"
    },
    "ReviewAfterSales.Infrastructure": {
      "until": "F4.3-completed",
      "reason": "Infrastructure.Tests 骨架刚补建，集成测试未完整"
    },
    "SellerShop.Infrastructure": {
      "until": "F4.3-completed",
      "reason": "Infrastructure.Tests 骨架刚补建"
    },
    "Notification.Infrastructure": {
      "until": "F4.3-completed",
      "reason": "Infrastructure.Tests 骨架刚补建"
    }
  }
}
```

- [ ] **Step 2: 创建 check-coverage.ps1 脚本**

创建 `scripts/check-coverage.ps1`：

```powershell
<#
.SYNOPSIS
  解析 reportgenerator 生成的 JSON summary，按层校验覆盖率门槛。

.DESCRIPTION
  输入：coverage-results/ 目录下 reportgenerator 生成的 summary.json 文件
  规则：按 scripts/coverage-thresholds.json 配置的层与最小 line coverage 校验
  豁免：temporaryExemptions 列出的项目仅警告不阻止合并（F4 合并后转为阻止）
  退出码：0 全部通过；1 任一未豁免项目低于阈值

.EXAMPLE
  pwsh scripts/check-coverage.ps1 -CoverageResultsPath coverage-results/
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$CoverageResultsPath
)

$ErrorActionPreference = "Stop"

$configPath = Join-Path $PSScriptRoot "coverage-thresholds.json"
$config = Get-Content $configPath -Raw | ConvertFrom-Json

$hasError = $false

foreach ($threshold in $config.thresholds) {
    Write-Host "校验 $($threshold.layer) 层覆盖率（阈值 $($threshold.minimumLineCoverage)%）..."

    $summaryFiles = Get-ChildItem -Path $CoverageResultsPath -Filter "summary.json" -Recurse

    foreach ($summaryFile in $summaryFiles) {
        $summary = Get-Content $summaryFile.FullName -Raw | ConvertFrom-Json

        foreach ($assembly in $summary.summary) {
            $assemblyName = $assembly.assembly
            $lineCoverage = [double]$assembly.linecoverage

            if ($assemblyName -notmatch $threshold.pathPattern) {
                continue
            }

            $projectName = $assemblyName -replace '\.dll$', ''
            $exemption = $config.temporaryExemptions.$projectName

            if ($exemption) {
                Write-Host "::warning::$projectName 覆盖率 $lineCoverage% 低于 $($threshold.minimumLineCoverage)%（豁免中：$($exemption.reason))" -ForegroundColor Yellow
                continue
            }

            if ($lineCoverage -lt $threshold.minimumLineCoverage) {
                Write-Host "::error::$projectName 覆盖率 $lineCoverage% 低于阈值 $($threshold.minimumLineCoverage)%"
                $hasError = $true
            } else {
                Write-Host "$projectName 覆盖率 $lineCoverage% 通过阈值 $($threshold.minimumLineCoverage)%" -ForegroundColor Green
            }
        }
    }
}

if ($hasError) {
    Write-Host "覆盖率校验失败，请提升测试覆盖后重试" -ForegroundColor Red
    exit 1
}

Write-Host "全部项目覆盖率通过门槛校验" -ForegroundColor Green
exit 0
```

- [ ] **Step 3: 修改 CI ci.yml 集成覆盖率校验步骤**

修改 `.github/workflows/ci.yml` 的 `build-solution` job（第 10-34 行），在第 27-29 行已有的 reportgenerator 步骤后追加覆盖率校验：

```yaml
      - name: Generate coverage report
        run: |
          dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.4.0 || true
          reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-results -reporttypes:"Cobertura;JsonSummary"

      - name: Check coverage thresholds
        run: pwsh scripts/check-coverage.ps1 -CoverageResultsPath coverage-results/
```

注意：实际添加位置需根据现有 ci.yml 结构判断，紧跟在 "Generate coverage report" 步骤之后。`JsonSummary` reporttype 生成 `summary.json` 供脚本解析。

- [ ] **Step 4: 本地运行覆盖率校验脚本验证**

先在本地生成覆盖率报告：

```bash
dotnet test Leno.sln --filter "Category!=Integration" --collect:"XPlat Code Coverage" --results-directory ./coverage-results
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:./coverage-results -reporttypes:"JsonSummary"
pwsh scripts/check-coverage.ps1 -CoverageResultsPath ./coverage-results/
```

预期：脚本输出各项目覆盖率，未豁免项目低于阈值则退出码 1。

注意：F4.1 补齐前 PointsMembership.Application 应被豁免（仅警告）。

- [ ] **Step 5: 提交**

```bash
git add scripts/check-coverage.ps1 scripts/coverage-thresholds.json .github/workflows/ci.yml
git commit -m "ci: 新增覆盖率门槛校验脚本，Domain 80%/Application 60%/Infrastructure 40% 阈值与临时豁免"
```

---

## Task 10: 调整 CI 集成测试 job 真正运行 Testcontainers 集成测试

> 当前 CI `integration-tests` job 仅能跑 1 个测试（仅 1 个 trait 标记），需让 Task 5-8 新增的集成测试能在 CI 中运行。

**Files:**
- Modify: `.github/workflows/ci.yml`（第 36-46 行 integration-tests job）

- [ ] **Step 1: 读取现有 integration-tests job**

读取 `.github/workflows/ci.yml` 第 36-46 行，确认现有步骤。

- [ ] **Step 2: 修改 integration-tests job 增加 Docker 服务容器**

修改 `integration-tests` job 为：

```yaml
  integration-tests:
    name: 集成测试 (Testcontainers)
    needs: build-solution
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore Leno.sln

      - name: Build
        run: dotnet build Leno.sln --no-restore --configuration Release

      - name: Run integration tests (Category=Integration)
        run: dotnet test Leno.sln --no-build --configuration Release --filter "Category=Integration" --collect:"XPlat Code Coverage"
        env:
          # Testcontainers 需要访问 Docker socket
          DOCKER_HOST: unix:///var/run/docker.sock

      - name: Upload integration test results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: integration-test-results
          path: |
            **/TestResults/
            **/coverage.cobertura.xml
          retention-days: 7
```

注意：
- `runs-on: ubuntu-latest` 自带 Docker，Testcontainers 可直接使用
- 不需要显式启动 SQL Server/Redis/RabbitMq 容器，Testcontainers 会自行管理
- `DOCKER_HOST` 显式指向 socket，避免某些 runner 配置问题

- [ ] **Step 3: 验证 CI YAML 语法**

```bash
# 如安装了 yamllint
yamllint .github/workflows/ci.yml
```

或推送后观察 GitHub Actions 是否能正确解析。

- [ ] **Step 4: 提交**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: 增强 integration-tests job，运行全部 Category=Integration 测试含 Testcontainers"
```

---

## Self-Review 自检

### 1. Spec 覆盖（对照 spec §7 F4）

| Spec 要求 | 对应 Task | 覆盖 |
|---|---|---|
| F4.1 PointsMembership.Application.Tests ≥20 个测试 | Task 2（≥26 个方法）+ Task 1（重命名） | ✅ |
| F4.1 覆盖率 PointsMembership.Application ≥60% | Task 2 补齐后达标；Task 9 门槛校验 | ✅ |
| F4.2 ReviewAfterSales ≥15 个测试 | 现状已有 19 个（探索确认），无需新增 | ✅ 现状满足 |
| F4.2 SellerShop ≥15 个测试 | 现状已有 27 个（探索确认），无需新增 | ✅ 现状满足 |
| F4.2 覆盖率两 BC Application 层 ≥60% | Task 9 门槛校验 | ✅ |
| F4.3 SeckillOrderFlowIntegrationTests | Task 5 | ✅ |
| F4.3 ForceCancelRefundIntegrationTests | Task 6 | ✅ |
| F4.3 CartProductSyncIntegrationTests | Task 7 | ✅ |
| F4.3 SellerOwnershipIntegrationTests | Task 8（Order + ReviewAfterSales 两个文件） | ✅ |
| F4.3 CI staging 运行 Category=Integration | Task 10 | ✅ |
| F4.4 CI 含 Scan placeholders 步骤 | 现状 ci.yml 第 22-23 行已集成 check-placeholders.sh | ✅ 现状满足 |
| F4.4 CI 含覆盖率阈值校验 | Task 9 | ✅ |
| F4.4 覆盖率报告 artifact 上传 | Task 10 Step 2 "Upload integration test results" | ✅ |
| F4.4 临时豁免 F4.1/F4.2 补齐前警告不阻止 | Task 9 coverage-thresholds.json temporaryExemptions | ✅ |
| F4.4 F4 合并后转为阻止 | 需 F4 合并后手动删除 temporaryExemptions 条目 | ⏭️ 后续手动 |
| 额外：3 BC Infrastructure.Tests 缺失补建 | Task 3 | ✅（spec 未明确要求但探索发现缺口） |
| 额外：文件名与类名不一致修复 | Task 1 | ✅（spec 未明确要求但符合代码规范） |

### 2. 占位符扫描

- ✅ 无 "TBD"、"TODO"、"fill in details"
- ⚠️ Task 5-8 含 `throw new NotImplementedException("...实现时根据...")` — **这是合理的实施指引占位**，由实施者在 Step 2/3 替换为真实代码。已在 Task 步骤明确说明"阅读 X 文件替换 NotImplementedException"。
- ✅ 所有代码块完整可用（除上述明确标记需实施者填充的工厂方法）
- ✅ 所有命令含确切参数与预期输出
- ✅ 每个 Task 都有独立提交步骤

### 3. 类型一致性

- `CrossBcIntegrationTestBase<TDbContext>` 签名：Task 4 定义 `(ContainerFixture fixture)` 构造函数 + `ConfigureServices` + `ConfigureConsumers` 两个抽象方法 + `ServiceProvider`/`TestHarness` 属性，Task 5-8 子类均继承并实现 — 一致 ✅
- `ITestHarness`：Task 4 使用 `MassTransit.Testing.ITestHarness`，Task 5-8 通过 `TestHarness.Bus.Publish` 与 `TestHarness.Consumed.Any<T>` 验证 — 一致 ✅
- `MigrateWithLockAsync<TDbContext>`：Task 4 调用 `await ServiceProvider.MigrateWithLockAsync<TDbContext>()`，依赖 Plan 3 Task 2 已定义 — 一致 ✅
- `ContainerFixture` 属性：Task 4-8 使用 `Fixture.SqlConnectionString`/`Fixture.RedisConnectionString`/`Fixture.RabbitMqConnectionString`，与 `src/BuildingBlocks/Leno.Testing/Fixtures/ContainerFixture.cs:25-27` 一致 ✅
- `[Trait("Category", "Integration")]`：Task 4 基类标记，CI Task 10 用 `--filter "Category=Integration"` 过滤 — 一致 ✅
- 4 个集成测试类名：Task 5-8 类名与 spec F4.3 表格完全一致 — 一致 ✅
- 4 个 AppService 测试类名（MemberAppServiceTests/PointsOffsetAppServiceTests/TaskAppServiceTests/MembershipPackageAppServiceTests）：Task 2 创建，与 spec F4.1 列表一致 — 一致 ✅

### 4. 已知注意事项

1. **Task 2 测试代码含推测方法签名**：示例骨架中的方法名（如 `CalculateOffsetAsync`、`UpgradeLevelAsync`）基于 AppService 命名推测，Step 1 与 Step 4/6/8 明确要求按实际签名修正。这是合理的实施指引方式，非占位符。
2. **Task 5-8 含 `throw new NotImplementedException` 工厂方法**：这些是给实施者的明确指引，需在对应 Step 中替换。`check-placeholders.sh` 在 CI 中会检测 `NotImplementedException`，但这些代码在合并到主分支前必须由实施者完成替换，否则 CI 阻止合并（符合 F4.4 占位零容忍原则）。
3. **Task 3 SmokeTests 是"占位测试"但非"空断言"**：SmokeTests 验证程序集可加载，是合理的最小冒烟测试，符合 `check-placeholders.sh` 第 64-89 行"空测试类检测"（class 声明含 `[Fact]` 方法即通过）。
4. **Task 9 coverage-thresholds.json temporaryExemptions**：F4 合并后需手动删除豁免条目让门槛转为阻止。这是 spec F4.4 明确要求的"临时豁免"机制。
5. **依赖 Plan 1/Plan 3 前置完成**：Task 5-8 依赖 Plan 1 F1.1/F1.2/F1.3/F1.4 业务修复；Task 4-8 依赖 Plan 3 Task 2 `MigrateWithLockAsync` 扩展方法。Plan 4 必须在 Plan 1 与 Plan 3 完成后执行。
6. **spec F4.2 现状已满足**：探索发现 ReviewAfterSales（19 个）与 SellerShop（27 个）的 Application.Tests 已远超 spec 要求 ≥15 个。Plan 4 不重复补齐，仅依赖 Task 9 门槛校验保证覆盖率。
7. **Task 10 修改 ci.yml 需谨慎**：实际合并时需根据现有 ci.yml 完整结构（5 个 jobs）调整缩进与步骤顺序，避免破坏 build-services/docker-build/validate-compose 等下游 job。
