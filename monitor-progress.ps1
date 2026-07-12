$progressPath = "e:\Leno\docs\todo\tasks\progress.md"
$totalTasks = 87

while ($true) {
    $progress = Get-Content $progressPath -Raw

    $completed = ([regex]::Matches($progress, "✅")).Count - 1
    $inProgress = ([regex]::Matches($progress, "🔄")).Count - 1
    $pending = ([regex]::Matches($progress, "⬜")).Count - 1
    $paused = ([regex]::Matches($progress, "⏸️")).Count - 1
    $cancelled = ([regex]::Matches($progress, "❌")).Count - 1

    $percentage = [math]::Round(($completed / $totalTasks) * 100, 1)

    Write-Host ""
    Write-Host "========================================"
    Write-Host "Leno 项目进度监控 - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Write-Host "========================================"
    Write-Host "总任务数: $totalTasks"
    Write-Host "✅ 已完成: $completed"
    Write-Host "🔄 进行中: $inProgress"
    Write-Host "⬜ 待开始: $pending"
    Write-Host "⏸️ 已暂停: $paused"
    Write-Host "❌ 已取消: $cancelled"
    Write-Host "----------------------------------------"
    Write-Host "整体完成率: $percentage%"
    Write-Host "========================================"

    Start-Sleep -Seconds 60
}
