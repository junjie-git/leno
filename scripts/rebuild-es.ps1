<#
.SYNOPSIS
  ES 读模型重建运维脚本（双轨下线阶段 4 / E3，2026-09-23）。
.DESCRIPTION
  经 SystemAdmin 的索引重建编排 API 触发指定索引重建，并轮询任务进度直至 Completed/Failed。
  重建机制：SystemAdmin 编排（IndexRebuildOrchestrator + ElasticsearchRebuildTrigger）
  经 ES _reindex API 复制 source 索引到 dest 索引（{index}_reindex_{taskId}）。
  事件回放兜底：数据源头损坏时使用共享内核 ReadModelRebuilder（快照 + 增量回放）按聚合重建，
  本脚本不覆盖该路径（见 ReadModelRebuilderTests 与各 BC 读模型消费者）。
.PARAMETER TargetContext
  目标上下文（BC 名，如 Product / Order / Review / SellerShop / Promotion）。
.PARAMETER IndexName
  索引名（如 leno_products / orders / reviews_v2 / leno_shop_dashboards / leno_coupons）。
.PARAMETER SystemAdminBaseUrl
  SystemAdmin API 地址（默认 http://localhost:5180）。
.PARAMETER Token
  管理端 Bearer Token（admin API 需 Operator 角色鉴权）。
.PARAMETER PollIntervalSeconds
  进度轮询间隔（默认 10 秒）。
.PARAMETER TimeoutMinutes
  超时（默认 30 分钟，超时返回退出码 3）。
.EXAMPLE
  pwsh scripts/rebuild-es.ps1 -TargetContext Product -IndexName leno_products -Token $token
#>
param(
    [Parameter(Mandatory = $true)] [string] $TargetContext,
    [Parameter(Mandatory = $true)] [string] $IndexName,
    [string] $SystemAdminBaseUrl = "http://localhost:5180",
    [string] $Token = "",
    [string] $Operator = "ops-rebuild-es",
    [int] $PollIntervalSeconds = 10,
    [int] $TimeoutMinutes = 30
)

$ErrorActionPreference = "Stop"
$headers = @{ Accept = "application/json" }
if ($Token) { $headers.Authorization = "Bearer $Token" }

function Get-ApiJson([string] $method, [string] $url, [object] $body) {
    $json = if ($body) { $body | ConvertTo-Json -Depth 5 } else { $null }
    $resp = Invoke-WebRequest -Uri $url -Method $method -Headers $headers `
        -ContentType "application/json" -Body $json -UseBasicParsing
    return $resp.Content | ConvertFrom-Json
}

try {
    # 1) 触发重建
    Write-Host "[1/2] 触发重建 TargetContext=$TargetContext IndexName=$IndexName Operator=$Operator"
    $trigger = Get-ApiJson -method Post `
        -url "$SystemAdminBaseUrl/api/admin/index-rebuild/trigger" `
        -body @{ targetContext = $TargetContext; indexName = $IndexName; triggeredBy = $Operator }
    $taskId = $trigger.data.taskId
    Write-Host "      任务已创建 TaskId=$taskId Status=$($trigger.data.status)"

    # 2) 轮询进度
    Write-Host "[2/2] 轮询任务进度（每 ${PollIntervalSeconds}s，超时 ${TimeoutMinutes}min）"
    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ($true) {
        Start-Sleep -Seconds $PollIntervalSeconds
        $task = Get-ApiJson -method Get -url "$SystemAdminBaseUrl/api/admin/index-rebuild/tasks/$taskId"
        $status = $task.data.status
        Write-Host "      Status=$status"
        if ($status -eq "Completed") {
            Write-Host "重建完成 TaskId=$taskId" -ForegroundColor Green
            exit 0
        }
        if ($status -eq "Failed") {
            Write-Host "重建失败 TaskId=$taskId（可通过 POST /api/admin/index-rebuild/tasks/$taskId/retry 重试）" -ForegroundColor Red
            exit 2
        }
        if ((Get-Date) -gt $deadline) {
            Write-Host "超时（${TimeoutMinutes}min），任务仍在运行 TaskId=$taskId（请人工跟进）" -ForegroundColor Yellow
            exit 3
        }
    }
}
catch {
    Write-Host "重建脚本异常：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
