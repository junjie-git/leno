# 遗留项清理：乐观锁 + 基础设施抽象 + 空测试补全

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 清理 4 项遗留：恢复乐观锁 shadow property、创建 Infrastructure.Abstractions 独立项目、补全 4 个空 Api.Tests、补建 3 个缺失 Infrastructure.Tests。

**Architecture:** Entity.Version 改为 BaseDbContext 中的 shadow property 统一配置；5 个基础设施抽象接口迁移到独立轻量项目；4 个 Api.Tests 补充 WebApplicationFactory 集成测试；3 个 Infrastructure.Tests 补充 Consumer 单元测试。

**Tech Stack:** .NET 10, EF Core, xUnit, Moq, FluentAssertions

---

## 遗留项 1：乐观锁 shadow property

### Task 1: 移除 Entity.Version + BaseDbContext 统一配置 shadow property

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs`
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs`
- Modify: 56 处 `*Configuration.cs`（移除 `Property(x => x.Version)...IsRowVersion()`）

- [ ] **Step 1: 从 Entity 移除 Version 字段**

在 `Entity.cs` 中删除：
```csharp
    /// <summary>
    /// 乐观锁版本号（SQL Server rowversion），由 EF Core 与数据库协同维护。
    /// </summary>
    public byte[] Version { get; set; } = Array.Empty<byte>();
```

- [ ] **Step 2: 在 BaseDbContext 添加统一 shadow property 配置**

在 `BaseDbContext.OnModelCreating` 中，在 `ApplyConfigurationsFromAssembly` 之后添加：

```csharp
    // 统一配置乐观锁 shadow property（避免领域层 Entity 携带持久化细节）
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(SharedKernel.Abstractions.IEntity).IsAssignableFrom(entityType.ClrType))
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property<byte[]>("Version")
                .HasColumnName("version")
                .IsRowVersion();
        }
    }
```

- [ ] **Step 3: 批量移除 56 处 Configuration 中的 IsRowVersion() 配置**

搜索所有 `Property(x => x.Version)` 或 `.IsRowVersion()` 行，删除这些配置行。

- [ ] **Step 4: 验证编译与测试**

- [ ] **Step 5: 提交**

---

## 遗留项 2：创建 Leno.Infrastructure.Abstractions 独立项目

### Task 2: 新建项目并迁移 5 个接口

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure.Abstractions/Leno.Infrastructure.Abstractions.csproj`
- Move: 5 个接口文件从 SharedKernel/Abstractions/ 到 Infrastructure.Abstractions/
- Modify: 所有引用这些接口的文件（更新 using 命名空间）
- Modify: `Leno.slnx`（添加新项目）

- [ ] **Step 1: 创建 Leno.Infrastructure.Abstractions.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Leno.SharedKernel\Leno.SharedKernel.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 迁移 5 个接口文件**

将以下文件从 `Leno.SharedKernel/Abstractions/` 移动到 `Leno.Infrastructure.Abstractions/`，命名空间改为 `Leno.Infrastructure.Abstractions`：
- ICacheService.cs
- IBloomFilter.cs
- IFileStorageService.cs
- IEventBus.cs
- IExternalChannelOptions.cs

- [ ] **Step 3: Leno.Infrastructure.csproj 引用新项目**

- [ ] **Step 4: 全局更新 using 命名空间**

所有使用这 5 个接口的文件，添加 `using Leno.Infrastructure.Abstractions;`。SharedKernel.Abstractions 中保留 IEntity/IRepository/IUnitOfWork/IDomainEvent 等领域抽象。

- [ ] **Step 5: Application 层 csproj 引用新项目**

所有 Application 项目添加对 Leno.Infrastructure.Abstractions 的引用。

- [ ] **Step 6: 添加到 Leno.slnx**

- [ ] **Step 7: 验证编译与测试**

- [ ] **Step 8: 提交**

---

## 遗留项 3：补全 4 个空 Api.Tests 项目

### Task 3: SellerShop.Api.Tests + ReviewAfterSales.Api.Tests

### Task 4: Notification.Api.Tests + SystemAdmin.Api.Tests

---

## 遗留项 4：补建 3 个 Infrastructure.Tests 项目

### Task 5: Cart.Infrastructure.Tests

### Task 6: PointsMembership.Infrastructure.Tests

### Task 7: SystemAdmin.Infrastructure.Tests

### Task 8: 最终验收
