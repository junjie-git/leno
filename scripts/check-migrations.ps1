<#
.SYNOPSIS
  检查所有 BC 的 EF Core 模型与迁移同步状态。

.DESCRIPTION
  对每个 BC 执行 `dotnet ef migrations has-pending-model-changes` 检测模型与最新迁移快照的差异。
  若存在未提交到迁移的模型变更，脚本退出码 1，CI 阻止合并。

  注：`dotnet ef migrations has-pending-model-changes` 为 EF Core 10 新增命令。
  若该命令在当前 dotnet-ef 版本不存在或行为不同，脚本将捕获非零退出码并优雅标注失败，
  不会抛出未处理异常。

.EXAMPLE
  pwsh scripts/check-migrations.ps1
#>

$ErrorActionPreference = "Stop"

$bcProjects = @(
    @{ Name = "UserAuth"; Infrastructure = "src/Services/UserAuth/Leno.UserAuth.Infrastructure"; Api = "src/Services/UserAuth/Leno.UserAuth.Api" },
    @{ Name = "Product"; Infrastructure = "src/Services/Product/Leno.Product.Infrastructure"; Api = "src/Services/Product/Leno.Product.Api" },
    @{ Name = "Cart"; Infrastructure = "src/Services/Cart/Leno.Cart.Infrastructure"; Api = "src/Services/Cart/Leno.Cart.Api" },
    @{ Name = "Order"; Infrastructure = "src/Services/Order/Leno.Order.Infrastructure"; Api = "src/Services/Order/Leno.Order.Api" },
    @{ Name = "Promotion"; Infrastructure = "src/Services/Promotion/Leno.Promotion.Infrastructure"; Api = "src/Services/Promotion/Leno.Promotion.Api" },
    @{ Name = "Payment"; Infrastructure = "src/Services/Payment/Leno.Payment.Infrastructure"; Api = "src/Services/Payment/Leno.Payment.Api" },
    @{ Name = "PointsMembership"; Infrastructure = "src/Services/PointsMembership/Leno.PointsMembership.Infrastructure"; Api = "src/Services/PointsMembership/Leno.PointsMembership.Api" },
    @{ Name = "ReviewAfterSales"; Infrastructure = "src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure"; Api = "src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api" },
    @{ Name = "SellerShop"; Infrastructure = "src/Services/SellerShop/Leno.SellerShop.Infrastructure"; Api = "src/Services/SellerShop/Leno.SellerShop.Api" },
    @{ Name = "Notification"; Infrastructure = "src/Services/Notification/Leno.Notification.Infrastructure"; Api = "src/Services/Notification/Leno.Notification.Api" },
    @{ Name = "SystemAdmin"; Infrastructure = "src/Services/SystemAdmin/Leno.SystemAdmin.Infrastructure"; Api = "src/Services/SystemAdmin/Leno.SystemAdmin.Api" }
)

$hasError = $false

foreach ($bc in $bcProjects) {
    Write-Host "检查 $($bc.Name) BC 模型与迁移同步状态..."
    $output = dotnet ef migrations has-pending-model-changes `
        --project $bc.Infrastructure `
        --startup-project $bc.Api 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "::error::$($bc.Name) BC 执行 has-pending-model-changes 失败（命令可能不存在或行为不同）：$output"
        $hasError = $true
        continue
    }

    if ($output -match "True" -or $output -match "true") {
        Write-Host "::error::$($bc.Name) BC 模型存在未提交到迁移的变更，请运行 dotnet ef migrations add <Name> 生成新迁移后再合并 PR"
        $hasError = $true
    } else {
        Write-Host "$($bc.Name) BC 模型与迁移同步"
    }
}

if ($hasError) {
    exit 1
}

Write-Host "所有 BC 模型与迁移均已同步"
exit 0
