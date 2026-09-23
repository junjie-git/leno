<#
.SYNOPSIS
  为所有 BC 生成幂等迁移 SQL 脚本，用于 staging 环境空库验证与生产部署。

.DESCRIPTION
  对每个 BC 执行 `dotnet ef migrations script --idempotent` 生成幂等 SQL 脚本。
  生成的脚本可重复执行，已存在的对象将被跳过（IF NOT EXISTS 语义）。

.NOTES
  前置条件：各 BC 的 DesignTimeDbContextFactory 需要环境变量 LENO_DESIGNTIME_CONNECTION_STRING
  才能构建设计期 DbContext（`migrations script` 不连库，仅需可解析的连接串）。CI 流水线会自动注入。
  本地执行示例：
    $env:LENO_DESIGNTIME_CONNECTION_STRING='Server=localhost,1433;Database=LenoDesignTime;User Id=sa;Password=<YOUR_PASSWORD>;TrustServerCertificate=True'
    pwsh scripts/generate-migration-scripts.ps1

  产物落点为 scripts/migrations/，需同步复制到 deploy/helm/leno/files/migrations/（helm migration-job 读该目录）。

.EXAMPLE
  pwsh scripts/generate-migration-scripts.ps1
#>

$ErrorActionPreference = "Stop"

$bcProjects = @(
    @{ Name = "product"; Infrastructure = "src/Services/Product/Leno.Product.Infrastructure"; Api = "src/Services/Product/Leno.Product.Api" },
    @{ Name = "cart"; Infrastructure = "src/Services/Cart/Leno.Cart.Infrastructure"; Api = "src/Services/Cart/Leno.Cart.Api" },
    @{ Name = "order"; Infrastructure = "src/Services/Order/Leno.Order.Infrastructure"; Api = "src/Services/Order/Leno.Order.Api" },
    @{ Name = "promotion"; Infrastructure = "src/Services/Promotion/Leno.Promotion.Infrastructure"; Api = "src/Services/Promotion/Leno.Promotion.Api" },
    @{ Name = "payment"; Infrastructure = "src/Services/Payment/Leno.Payment.Infrastructure"; Api = "src/Services/Payment/Leno.Payment.Api" },
    @{ Name = "sellershop"; Infrastructure = "src/Services/SellerShop/Leno.SellerShop.Infrastructure"; Api = "src/Services/SellerShop/Leno.SellerShop.Api" },
    @{ Name = "notification"; Infrastructure = "src/Services/Notification/Leno.Notification.Infrastructure"; Api = "src/Services/Notification/Leno.Notification.Api" },
    @{ Name = "systemadmin"; Infrastructure = "src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure"; Api = "src/Services/SystemAdmin/Leno.SystemAdmin.Api" },
    # 2026-09-21 补入：此前 identity / accesscontrol / usercenter 完全在生成管线之外
    # （identity+accesscontrol 靠手写 SQL，usercenter 无任何建表路径），
    # N1 决策「EF 迁移统一」后纳入统一生成。
    @{ Name = "identity"; Infrastructure = "src/Services/Identity/Leno.Identity.Infrastructure"; Api = "src/Services/Identity/Leno.Identity.Api" },
    @{ Name = "accesscontrol"; Infrastructure = "src/Services/AccessControl/Leno.AccessControl.Infrastructure"; Api = "src/Services/AccessControl/Leno.AccessControl.Api" },
    @{ Name = "usercenter"; Infrastructure = "src/Services/UserCenter/Leno.UserCenter.Infrastructure"; Api = "src/Services/UserCenter/Leno.UserCenter.Api" },
    # 2026-09-21 补入（A3：剩余 5 个 BC 全部部署，DEC-4 决策）
    @{ Name = "inventory"; Infrastructure = "src/Services/Inventory/Leno.Inventory.Infrastructure"; Api = "src/Services/Inventory/Leno.Inventory.Api" },
    @{ Name = "points"; Infrastructure = "src/Services/Points/Leno.Points.Infrastructure"; Api = "src/Services/Points/Leno.Points.Api" },
    @{ Name = "membership"; Infrastructure = "src/Services/Membership/Leno.Membership.Infrastructure"; Api = "src/Services/Membership/Leno.Membership.Api" },
    @{ Name = "review"; Infrastructure = "src/Services/Review/Leno.Review.Infrastructure"; Api = "src/Services/Review/Leno.Review.Api" },
    @{ Name = "aftersales"; Infrastructure = "src/Services/AfterSales/Leno.AfterSales.Infrastructure"; Api = "src/Services/AfterSales/Leno.AfterSales.Api" }
)

New-Item -ItemType Directory -Force -Path scripts/migrations | Out-Null

foreach ($bc in $bcProjects) {
    Write-Host "生成 $($bc.Name) BC 幂等迁移 SQL 脚本..."
    dotnet ef migrations script --idempotent `
        --project $bc.Infrastructure `
        --startup-project $bc.Api `
        --output "scripts/migrations/$($bc.Name)-initial.sql"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "::error::$($bc.Name) BC 迁移脚本生成失败"
        exit 1
    }
}

Write-Host "全部 BC 迁移 SQL 脚本已生成至 scripts/migrations/"
