# Leno 电商平台 - Vibe Coding 环境配置指南

> **目标**: 配置环境让 AI Agent 自动执行 87 个开发任务并监控进度
> **文档版本**: V1.0
> **创建日期**: 2026-07-11

---

## 1. 前置条件

### 1.1 软件依赖

| 软件           | 版本要求  | 用途                          |
| -------------- | --------- | ----------------------------- |
| .NET SDK       | 10.0.301+ | 编译、测试、运行项目          |
| Docker Desktop | 24.0+     | 基础设施容器化运行            |
| Trae IDE       | 最新版    | Vibe Coding AI Agent 运行环境 |
| Git            | 2.40+     | 版本控制、代码提交            |

### 1.2 环境变量检查

```powershell
# 验证 .NET 版本
dotnet --version
# 预期输出: 10.0.301 或更高

# 验证 Docker 运行状态
docker info
# 预期: 无错误信息，显示 Docker 版本和资源信息

# 验证 Git 版本
git --version
# 预期输出: git version 2.40.0 或更高
```

### 1.3 项目目录结构

确保工作目录包含以下关键文件：

```
e:\Leno\
├── Leno.slnx                    # 解决方案文件
├── docker-compose.yml           # 基础设施容器配置
├── docs/
│   └── todo/
│       ├── prompt.md            # Vibe Coding Prompt 文件
│       └── tasks/
│           ├── progress.md      # 进度跟踪文档（AI Agent 读写）
│           └── *.md             # 各模块任务文档
└── src/                         # 源代码目录
```

---

## 2. 基础设施启动

### 2.1 启动核心基础设施

项目依赖以下基础设施服务：

| 服务          | 端口       | 说明                     |
| ------------- | ---------- | ------------------------ |
| SQL Server    | 1433       | 主数据库                 |
| Redis         | 6379       | 缓存、分布式锁、购物车   |
| RabbitMQ      | 5672/15672 | 消息队列（管理端 15672） |
| Elasticsearch | 9200       | 全文搜索                 |

**启动命令**（PowerShell）：

```powershell
# 进入项目目录
cd e:\Leno

# 仅启动基础设施服务（不启动微服务）
docker-compose up -d sqlserver redis rabbitmq elasticsearch
```

**验证服务状态**：

```powershell
# 查看容器状态
docker-compose ps

# 预期所有基础设施服务状态为 healthy
# leno-sqlserver       healthy
# leno-redis           healthy
# leno-rabbitmq        healthy
# leno-elasticsearch   healthy
```

### 2.2 服务健康检查

```powershell
# SQL Server
docker exec leno-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "Leno@SqlServer2019" -Q "SELECT 1"

# Redis
docker exec leno-redis redis-cli ping
# 预期输出: PONG

# RabbitMQ
docker exec leno-rabbitmq rabbitmq-diagnostics check_running
# 预期输出: ok

# Elasticsearch
curl http://localhost:9200/_cluster/health
# 预期输出: {"status":"green",...}
```

---

## 3. Trae IDE 配置

### 3.1 打开项目

1. 启动 Trae IDE
2. 通过 **File → Open Folder** 打开 `e:\Leno` 目录
3. 等待项目加载完成（可能需要几分钟）

### 3.2 配置项目规则（关键步骤）

将 `prompt.md` 配置为项目规则文件，使 AI Agent 能够读取并执行：

**方法一：通过 Trae UI 配置**

1. 点击左侧导航栏的 **Rules** 图标
2. 点击 **Add Rule**
3. 在弹出对话框中：
    - **Rule Name**: `Leno-Vibe-Coding`
    - **Rule Type**: `Project Rule`
    - **Source**: 选择 `docs/todo/prompt.md`
    - **Priority**: `High`
4. 点击 **Save**

**方法二：手动配置 .trae 目录**

```powershell
# 创建规则文件
$ruleContent = @'
{
  "name": "Leno-Vibe-Coding",
  "type": "project",
  "source": "docs/todo/prompt.md",
  "priority": "high",
  "enabled": true
}
'@

$ruleContent | Out-File -FilePath "e:\Leno\.trae\rules\leno-vibe-coding.json" -Encoding utf8
```

### 3.3 验证规则加载

```powershell
# 检查规则文件是否存在
Get-ChildItem "e:\Leno\.trae\rules\"

# 预期输出包含: leno-vibe-coding.json
```

---

## 4. 任务执行入口

### 4.1 启动 Vibe Coding Agent

在 Trae IDE 中启动 AI Agent 并输入启动指令：

```
请根据 docs/todo/prompt.md 中的指令，开始执行 Leno 电商平台的自动化开发任务。
从 SK-01 测试基础设施搭建任务开始，按优先级和依赖顺序执行所有 87 个任务。
每次完成任务后更新 docs/todo/tasks/progress.md 和对应模块的任务文件。
```

### 4.2 Agent 执行流程

AI Agent 启动后会按照以下流程自动执行：

```
1. 读取 prompt.md → 理解项目目标和约束
2. 读取 progress.md → 获取当前任务状态
3. 分析依赖关系 → 选择可执行的最高优先级任务
4. 创建子 Agent → 执行具体任务
5. 验证交付物 → 更新任务状态
6. 循环直到所有任务完成
```

### 4.3 关键配置项

确保 Agent 拥有以下权限：

- ✅ 文件读写权限（读取/更新 Markdown 文件）
- ✅ Shell 执行权限（运行 dotnet 命令）
- ✅ Git 操作权限（提交代码）
- ✅ Docker 操作权限（启动测试容器）

---

## 5. 进度监控

### 5.1 实时监控方法

**方法一：监控 progress.md 变化**

```powershell
# PowerShell 实时监控进度文件变化
Get-Content "e:\Leno\docs\todo\tasks\progress.md" -Wait -Tail 20
```

**方法二：统计完成任务数**

```powershell
# 统计已完成任务数（✅标记）
$progress = Get-Content "e:\Leno\docs\todo\tasks\progress.md" -Raw
$completed = ([regex]::Matches($progress, "✅")).Count
$total = 87
$percentage = [math]::Round(($completed / $total) * 100, 1)
Write-Host "已完成: $completed / $total ($percentage%)"
```

**方法三：创建监控脚本**

创建 `monitor-progress.ps1` 脚本：

```powershell
# 进度监控脚本
$progressPath = "e:\Leno\docs\todo\tasks\progress.md"
$totalTasks = 87

while ($true) {
    $progress = Get-Content $progressPath -Raw

    # 统计各状态任务数
    $completed = ([regex]::Matches($progress, "✅")).Count
    $inProgress = ([regex]::Matches($progress, "🔄")).Count
    $pending = ([regex]::Matches($progress, "⬜")).Count
    $paused = ([regex]::Matches($progress, "⏸️")).Count
    $cancelled = ([regex]::Matches($progress, "❌")).Count

    $percentage = [math]::Round(($completed / $totalTasks) * 100, 1)

    # 输出进度
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
```

运行脚本：

```powershell
.\monitor-progress.ps1
```

### 5.2 模块进度监控

```powershell
# 查看各模块完成情况
$modules = @(
    "共享内核", "用户与认证授权", "商品域", "购物车域", "订单与交易域",
    "促销域", "支付集成域", "积分与会员域", "评价与售后域",
    "卖家与店铺管理域", "消息通知域", "系统管理域"
)

$progress = Get-Content "e:\Leno\docs\todo\tasks\progress.md" -Raw

foreach ($module in $modules) {
    $pattern = "$module.*?模块进度:\s*(\d+)/(\d+)"
    if ($progress -match $pattern) {
        $completed = $Matches[1]
        $total = $Matches[2]
        $percentage = [math]::Round(($completed / $total) * 100, 1)
        Write-Host "$module`: $completed/$total ($percentage%)"
    }
}
```

### 5.3 测试结果监控

```powershell
# 运行所有单元测试
dotnet test Leno.slnx --configuration Release --filter "Category=Unit"

# 运行所有测试（含集成测试，需要Testcontainers）
dotnet test Leno.slnx --configuration Release
```

---

## 6. 测试环境配置

### 6.1 Testcontainers 配置

集成测试使用 Testcontainers 启动临时容器，首次运行会自动拉取 Docker 镜像：

| 镜像                                                   | 用途          |
| ------------------------------------------------------ | ------------- |
| `mcr.microsoft.com/mssql/server:2019-latest`           | SQL Server    |
| `redis:7-alpine`                                       | Redis         |
| `rabbitmq:3.12-management`                             | RabbitMQ      |
| `docker.elastic.co/elasticsearch/elasticsearch:8.13.0` | Elasticsearch |

**首次运行注意事项**：

- 首次运行集成测试会下载镜像，可能需要 5-10 分钟
- 确保 Docker Desktop 有足够的资源分配（建议至少 4GB 内存）

### 6.2 CI 流水线说明

当前 CI 流水线（`.github/workflows/ci.yml`）仅运行单元测试：

```yaml
# 当前配置
dotnet test Leno.slnx --configuration Release --no-build --filter "Category=Unit"
```

**本地完整测试命令**：

```powershell
# 运行所有测试（单元 + 集成）
dotnet test Leno.slnx --configuration Release

# 运行特定模块测试
dotnet test src/Services/Order/Leno.Order.Api/Leno.Order.Api.csproj --configuration Release
```

### 6.3 测试覆盖率检查

```powershell
# 生成测试覆盖率报告（需要 coverlet 包）
dotnet test Leno.slnx --configuration Release --collect:"XPlat Code Coverage"

# 查看覆盖率报告（在 TestResults 目录下）
Get-ChildItem "**/TestResults" -Recurse | Where-Object { $_.Name -match "coverage.cobertura.xml" }
```

---

## 7. 故障排查

### 7.1 常见问题

| 问题                 | 原因                      | 解决方案                                     |
| -------------------- | ------------------------- | -------------------------------------------- |
| 基础设施服务启动失败 | Docker 资源不足           | 增加 Docker 内存分配（至少 4GB）             |
| 测试容器启动超时     | Testcontainers 镜像拉取慢 | 确保网络通畅，耐心等待首次拉取               |
| Agent 无法更新进度   | 文件权限问题              | 确保 Trae 进程有文件写入权限                 |
| 代码编译失败         | .NET 版本不匹配           | 运行 `dotnet --version` 确认版本为 10.0.301+ |
| 数据库连接失败       | SQL Server 未就绪         | 等待 `docker-compose ps` 显示 healthy        |

### 7.2 日志查看

```powershell
# 查看基础设施日志
docker-compose logs sqlserver
docker-compose logs redis
docker-compose logs rabbitmq
docker-compose logs elasticsearch

# 查看 Trae IDE 日志（Windows）
Get-Content "$env:APPDATA\Trae CN\logs\latest.log" -Tail 50
```

### 7.3 紧急停止

```powershell
# 停止所有基础设施服务
docker-compose down

# 停止特定服务
docker-compose stop sqlserver redis rabbitmq elasticsearch

# 清理容器（谨慎使用，会删除数据）
docker-compose down -v
```

---

## 8. 启动检查清单

在启动 Vibe Coding Agent 前，确保完成以下检查：

- [ ] ✅ .NET SDK 10.0.301+ 已安装
- [ ] ✅ Docker Desktop 已启动
- [ ] ✅ Git 已配置（用户名和邮箱）
- [ ] ✅ 基础设施服务已启动（`docker-compose up -d sqlserver redis rabbitmq elasticsearch`）
- [ ] ✅ 所有基础设施服务状态为 healthy（`docker-compose ps`）
- [ ] ✅ Trae IDE 已打开项目
- [ ] ✅ `prompt.md` 已配置为项目规则
- [ ] ✅ `progress.md` 文件存在且可读写
- [ ] ✅ 执行 `dotnet restore` 确保依赖已安装
- [ ] ✅ 执行 `dotnet build` 确保项目可编译

---

## 9. 预期输出

### 9.1 进度报告示例

```
========================================
Leno 项目进度监控 - 2026-07-11 10:30:00
========================================
总任务数: 87
✅ 已完成: 15
🔄 进行中: 2
⬜ 待开始: 70
⏸️ 已暂停: 0
❌ 已取消: 0
----------------------------------------
整体完成率: 17.2%
========================================
```

### 9.2 任务完成状态

每次任务完成后，`progress.md` 会更新：

```markdown
| 任务                     | 优先级 | 状态 | 负责人   | 开始日期   | 完成日期   |
| ------------------------ | ------ | ---- | -------- | ---------- | ---------- |
| SK-01: 测试基础设施搭建  | P0     | ✅   | AI Agent | 2026-07-11 | 2026-07-11 |
| UA-01: 测试项目创建      | P0     | ✅   | AI Agent | 2026-07-11 | 2026-07-11 |
| UA-02: OAuth2 第三方登录 | P0     | 🔄   | AI Agent | 2026-07-12 | -          |
```

---

## 10. 注意事项

1. **首次运行耗时较长**：基础设施启动、Testcontainers 镜像拉取、依赖安装都需要时间
2. **网络要求**：需要稳定的网络连接下载依赖包和 Docker 镜像
3. **资源要求**：建议至少 16GB 内存、4 核 CPU 的机器运行
4. **定期保存进度**：AI Agent 会自动更新 `progress.md`，但建议定期备份
5. **异常处理**：如果任务失败，Agent 会自动重试（最多 3 次），仍失败会标记为暂停

---

> **启动提示**: 完成以上配置后，在 Trae IDE 中输入启动指令即可开始自动化开发。
