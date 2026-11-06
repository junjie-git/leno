<#
.SYNOPSIS
    为本地全栈写入最小种子数据，并验证 BFF 商品详情聚合端点返回 200。

.DESCRIPTION
    复用仓库既有的「sqlcmd 执行 .sql 脚本」机制（与 scripts/migrations 的 schema 脚本同一驱动）：
    账号走 Identity 注册接口（口令为 Argon2+pepper 哈希，无法 SQL 直插），角色写入 access-control，
    商品/订单/评价由 scripts/seed/minimal-seed.sql 幂等写入。
    种子 SQL 刻意放在 scripts/seed/ 而非 scripts/migrations/：后者由 EF 生成、
    被 CI 的「迁移脚本 ↔ Helm files/migrations 同步」与「空库顺序执行」两个 job 强约束，
    数据种子混入会导致 CI 失败。脚本本身不修改任何服务代码，也不引入新的运行时 seeder。

.PARAMETER GatewayBaseUrl
    网关地址，默认 http://localhost:8080。

.PARAMETER SqlContainer
    运行 SQL Server 的容器名，默认 leno-sqlserver。

.PARAMETER SaPassword
    SQL Server sa 口令；默认取环境变量 LENO_DB_PASSWORD，缺省为本地开发口令。

.PARAMETER BuyerUsername / SellerUsername
    买家与卖家账号名。

.PARAMETER Password
    账号口令（仅本地开发环境使用）。

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/seed-minimal-data.ps1
#>
[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = 'http://localhost:8080',
    [string]$SqlContainer = 'leno-sqlserver',
    [string]$SaPassword = $(if ($env:LENO_DB_PASSWORD) { $env:LENO_DB_PASSWORD } else { 'Leno_Local_2026!x' }),
    [string]$BuyerUsername = 'seedbuyer01',
    [string]$SellerUsername = 'seedseller01',
    [string]$Password = 'Passw0rd!123'
)

$ErrorActionPreference = 'Stop'
$seedFileHost = Join-Path $PSScriptRoot 'seed\minimal-seed.sql'
$seedFileContainer = '/tmp/minimal-seed.sql'

$SQLCMD = '/opt/mssql-tools18/bin/sqlcmd'

function Invoke-SqlQuery {
    param(
        [Parameter(Mandatory)] [string]$Database,
        [Parameter(Mandatory)] [string]$Query
    )
    $output = & docker exec $SqlContainer $SQLCMD -S localhost -U sa -P $SaPassword -C -d $Database -W -h -1 -Q $Query 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd 执行失败（$Database）：`n$output"
    }
    return ($output | Where-Object { $_ -ne '' -and $_ -notmatch '^(rows affected|受影响的行)' })
}

function Invoke-DbSqlScript {
    param([Parameter(Mandatory)] [string]$ContainerPath, [Parameter(Mandatory)] [hashtable]$Variables)

    & docker cp $seedFileHost "${SqlContainer}:${ContainerPath}" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'docker cp 种子 SQL 失败' }

    $cmdArgs = @('exec', $SqlContainer, $SQLCMD, '-S', 'localhost', '-U', 'sa', '-P', $SaPassword, '-C', '-b', '-f', '65001', '-i', $ContainerPath)
    foreach ($key in $Variables.Keys) {
        $cmdArgs += '-v'
        $cmdArgs += "$key=$($Variables[$key])"
    }
    $output = & docker @cmdArgs 2>&1
    if ($LASTEXITCODE -ne 0) { throw "种子 SQL 执行失败：`n$output" }
    return $output
}

function Invoke-ApiJson {
    param(
        [Parameter(Mandatory)] [string]$Method,
        [Parameter(Mandatory)] [string]$Url,
        [Parameter(Mandatory)] [hashtable]$Body,
        [string]$BearerToken
    )
    $headers = @{}
    if ($BearerToken) { $headers['Authorization'] = "Bearer $BearerToken" }
    $json = $Body | ConvertTo-Json -Compress
    try {
        $response = Invoke-RestMethod -Method $Method -Uri $Url -ContentType 'application/json' -Headers $headers -Body $json -TimeoutSec 30
        return @{ Ok = $true; Status = 200; Data = $response }
    }
    catch {
        $status = 0
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        $payload = $null
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $payload = $_.ErrorDetails.Message }
        return @{ Ok = $false; Status = $status; Data = $payload; Message = $_.Exception.Message }
    }
}

function Ensure-Account {
    param([Parameter(Mandatory)] [string]$Username)

    $register = Invoke-ApiJson -Method Post -Url "$GatewayBaseUrl/api/auth/register" -Body @{
        username = $Username
        email    = "$Username@leno.local"
        password = $Password
        nickname = $Username
    }
    if ($register.Ok) {
        Write-Host "[seed] 注册账号成功：$Username" -ForegroundColor Green
    }
    else {
        # 已存在账号时注册返回 4xx，属预期幂等路径
        Write-Host "[seed] 账号已存在或注册被拒（$($register.Status)），继续走登录：$Username" -ForegroundColor Yellow
    }

    $login = Invoke-ApiJson -Method Post -Url "$GatewayBaseUrl/api/auth/login" -Body @{
        usernameOrEmail = $Username
        password        = $Password
    }
    if (-not $login.Ok) {
        throw "账号 $Username 登录失败（HTTP $($login.Status)）：$($login.Data)"
    }
    return $login.Data.data.accessToken
}

function Get-AccountId {
    param([Parameter(Mandatory)] [string]$Username)
    $rows = Invoke-SqlQuery -Database 'leno_identity' -Query "SET NOCOUNT ON; SELECT CAST(id AS varchar(36)) FROM users WHERE username = N'$Username';"
    if (-not $rows -or ($rows -is [array] -and $rows.Count -eq 0)) {
        throw "leno_identity 中未找到账号：$Username"
    }
    return (@($rows)[0]).Trim()
}

function Ensure-Role {
    param(
        [Parameter(Mandatory)] [string]$UserId,
        [Parameter(Mandatory)] [int]$Role
    )
    $query = @"
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
IF NOT EXISTS (SELECT 1 FROM user_role_assignments WHERE user_id = '$UserId' AND role = $Role AND is_active = 1)
BEGIN
    INSERT INTO user_role_assignments (id, user_id, role, is_active, assigned_at, revoked_at, operator_id,
                                       created_at, updated_at, created_by, updated_by)
    VALUES (NEWID(), '$UserId', $Role, 1, SYSUTCDATETIME(), NULL, NULL,
            SYSUTCDATETIME(), SYSUTCDATETIME(), N'seed', N'seed');
    PRINT 'role inserted: $Role for $UserId';
END
ELSE
BEGIN
    PRINT 'role already present: $Role for $UserId';
END
"@
    $output = & docker exec $SqlContainer $SQLCMD -S localhost -U sa -P $SaPassword -C -d leno_access_control -b -Q $query 2>&1
    if ($LASTEXITCODE -ne 0) { throw "写入 access-control 角色失败：`n$output" }
    return $output
}

function Get-TokenRoles {
    param([Parameter(Mandatory)] [string]$Token)
    $parts = $Token.Split('.')
    if ($parts.Length -lt 2) { return @() }
    $payload = $parts[1].Replace('-', '+').Replace('_', '/')
    switch ($payload.Length % 4) {
        2 { $payload += '==' }
        3 { $payload += '=' }
    }
    $json = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
    $roleClaim = $json.role
    if (-not $roleClaim) { $roleClaim = $json.roles }
    if ($null -eq $roleClaim) { return @() }
    return @($roleClaim)
}

Write-Host '[seed] 1/6 注册并登录买家/卖家账号' -ForegroundColor Cyan
$buyerToken = Ensure-Account -Username $BuyerUsername
$sellerToken = Ensure-Account -Username $SellerUsername
$buyerId = Get-AccountId -Username $BuyerUsername
$sellerId = Get-AccountId -Username $SellerUsername
Write-Host "[seed] BuyerId=$buyerId SellerId=$sellerId"

Write-Host '[seed] 2/6 写入 access-control 角色（Buyer=1, Seller=2）' -ForegroundColor Cyan
Ensure-Role -UserId $buyerId -Role 1 | ForEach-Object { Write-Host "  $_" }
Ensure-Role -UserId $sellerId -Role 2 | ForEach-Object { Write-Host "  $_" }

Write-Host '[seed] 3/6 重新登录以获取带角色的令牌' -ForegroundColor Cyan
$buyerToken = Ensure-Account -Username $BuyerUsername
$sellerToken = Ensure-Account -Username $SellerUsername
$buyerRoles = Get-TokenRoles -Token $buyerToken
$sellerRoles = Get-TokenRoles -Token $sellerToken
Write-Host "[seed] buyer token roles = $($buyerRoles -join ',')"
Write-Host "[seed] seller token roles = $($sellerRoles -join ',')"

Write-Host '[seed] 4/6 执行 scripts/seed/minimal-seed.sql' -ForegroundColor Cyan
$seedOutput = Invoke-DbSqlScript -ContainerPath $seedFileContainer -Variables @{
    SellerId = $sellerId
    BuyerId  = $buyerId
}
$seedOutput | Where-Object { $_ -match 'Msg|错误|失败' } | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }

Write-Host '[seed] 5/6 复核落库行数' -ForegroundColor Cyan
$productCounts = Invoke-SqlQuery -Database 'leno_product' -Query 'SET NOCOUNT ON; SELECT (SELECT COUNT(*) FROM spus) AS spus, (SELECT COUNT(*) FROM skus) AS skus;'
$orderCounts = Invoke-SqlQuery -Database 'LenoOrder' -Query 'SET NOCOUNT ON; SELECT (SELECT COUNT(*) FROM orders) AS orders, (SELECT COUNT(*) FROM order_items) AS items;'
$reviewCounts = Invoke-SqlQuery -Database 'leno_review' -Query 'SET NOCOUNT ON; SELECT COUNT(*) FROM reviews;'
Write-Host "[seed] leno_product: $($productCounts -join ' ')"
Write-Host "[seed] LenoOrder: $($orderCounts -join ' ')"
Write-Host "[seed] leno_review: $($reviewCounts -join ' ')"

Write-Host '[seed] 6/6 调用 BFF 聚合端点验证 200（Seller 令牌：商品详情端点类级要求 Seller 角色）' -ForegroundColor Cyan
$bffUrl = "$GatewayBaseUrl/api/bff/products/22222222-2222-2222-2222-222222222222"
try {
    $bffResponse = Invoke-WebRequest -Method Get -Uri $bffUrl -Headers @{ Authorization = "Bearer $sellerToken" } -TimeoutSec 30 -UseBasicParsing
    Write-Host "[seed] BFF HTTP $($bffResponse.StatusCode)"
    Write-Host $bffResponse.Content
}
catch {
    $status = 0
    $body = $null
    if ($_.Exception.Response) {
        $status = [int]$_.Exception.Response.StatusCode
        try { $body = $_.ErrorDetails.Message } catch { $body = $null }
    }
    Write-Host "[seed] BFF 调用失败 HTTP $status：$($_.Exception.Message)" -ForegroundColor Red
    if ($body) { Write-Host $body }
    exit 1
}

Write-Host '[seed] 完成：种子数据已就绪，BFF 聚合端点返回 200' -ForegroundColor Green
