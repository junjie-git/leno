#!/bin/bash
# 库存 staging E2E（阶段 5）：gRPC 预占/确认/释放/查询 + SQL 台账断言。
#
# 与 staging-inventory.ps1 等价，供 macOS/Linux（无 pwsh）本地或 CI 使用。
# 默认面向本机 compose 模式（inventory-api 暴露 5265）。
#
# 前置（本地 compose 必读）：
#   1) 先建库与 schema —— Docker 环境下应用**不执行启动迁移**（Database:MigrateOnStartup 默认 false），
#      需按部署期迁移 Job 同款方式手工应用：inventory-initial.sql（库 leno_inventory）、
#      scheduler-quartz.sql（库 leno_scheduler，否则 Quartz 持久化会因缺表失败）；
#   2) sqlcmd 执行上述脚本时需加 -I（QUOTED_IDENTIFIER ON），否则 CREATE INDEX 段会报 Msg 1934 中断。
#
# 退出码：0 = 全部通过；1 = 存在失败断言；2 = 连接失败。
set -euo pipefail

GRPC_ENDPOINT="${GRPC_ENDPOINT:-http://localhost:5265}"
SQL_CONNECTION_STRING="${SQL_CONNECTION_STRING:-Server=localhost,1433;Database=leno_inventory;User Id=sa;Password=Leno_Local_2026!x;TrustServerCertificate=true}"
INTERNAL_API_KEY="${INTERNAL_API_KEY:-local-dev-internal-api-key-32bytes!!}"

dotnet run --project "$(dirname "$0")/InventoryE2E" -- \
    --grpc "$GRPC_ENDPOINT" \
    --sql "$SQL_CONNECTION_STRING" \
    --api-key "$INTERNAL_API_KEY"
exit $?