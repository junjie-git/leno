<#
.SYNOPSIS
  为所有 BC 生成幂等迁移 SQL 脚本，用于 staging 环境空库验证与生产部署。

.DESCRIPTION
  对每个 BC 执行 `dotnet ef migrations script --idempotent` 生成幂等 SQL 脚本。
  生成的脚本可重复执行，已存在的对象将被跳过（IF NOT EXISTS 语义）。

.EXAMPLE
  pwsh scripts/generate-migration-scripts.ps1
#>

$ErrorActionPreference = "Stop"

$bcProjects = @(
    @{ Name = "userauth"; Infrastructure = "src/Services/UserAuth/Leno.UserAuth.Infrastructure"; Api = "src/Services/UserAuth/Leno.UserAuth.Api" },
    @{ Name = "product"; Infrastructure = "src/Services/Product/Leno.Product.Infrastructure"; Api = "src/Services/Product/Leno.Product.Api" },
    @{ Name = "cart"; Infrastructure = "src/Services/Cart/Leno.Cart.Infrastructure"; Api = "src/Services/Cart/Leno.Cart.Api" },
    @{ Name = "order"; Infrastructure = "src/Services/Order/Leno.Order.Infrastructure"; Api = "src/Services/Order/Leno.Order.Api" },
    @{ Name = "promotion"; Infrastructure = "src/Services/Promotion/Leno.Promotion.Infrastructure"; Api = "src/Services/Promotion/Leno.Promotion.Api" },
    @{ Name = "payment"; Infrastructure = "src/Services/Payment/Leno.Payment.Infrastructure"; Api = "src/Services/Payment/Leno.Payment.Api" },
    @{ Name = "pointsmembership"; Infrastructure = "src/Services/PointsMembership/Leno.PointsMembership.Infrastructure"; Api = "src/Services/PointsMembership/Leno.PointsMembership.Api" },
    @{ Name = "reviewaftersales"; Infrastructure = "src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure"; Api = "src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api" },
    @{ Name = "sellershop"; Infrastructure = "src/Services/SellerShop/Leno.SellerShop.Infrastructure"; Api = "src/Services/SellerShop/Leno.SellerShop.Api" },
    @{ Name = "notification"; Infrastructure = "src/Services/Notification/Leno.Notification.Infrastructure"; Api = "src/Services/Notification/Leno.Notification.Api" },
    @{ Name = "systemadmin"; Infrastructure = "src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure"; Api = "src/Services/SystemAdmin/Leno.SystemAdmin.Api" }
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
