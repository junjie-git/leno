#!/bin/bash
# 秒杀结算单向校验（双轨下线 · 秒杀结算收口）：
#   Promotion 预占记录 ↔ Inventory 台账（stock_reservations）↔ Redis 秒杀配额。
#
# 本地 compose 模式（默认值即可）：
#   bash scripts/verify/seckill-ledger-reconcile.sh
# 真 staging：覆盖连接串参数，或只跑台账覆盖校验（省略 --redis）。
#
# 退出码：0 = 通过；1 = 存在违反；2 = 连接失败/参数错误。
set -euo pipefail

PROMO_SQL="${PROMO_SQL:-Server=localhost,1433;Database=leno_promotion;User Id=sa;Password=Leno_Local_2026!x;TrustServerCertificate=true}"
INVENTORY_SQL="${INVENTORY_SQL:-Server=localhost,1433;Database=leno_inventory;User Id=sa;Password=Leno_Local_2026!x;TrustServerCertificate=true}"
REDIS_CONN="${REDIS_CONN:-localhost:6379}"
HOURS="${HOURS:-24}"

ARGS=(--promo-sql "$PROMO_SQL" --inventory-sql "$INVENTORY_SQL" --hours "$HOURS")
if [ "${SKIP_REDIS:-0}" != "1" ]; then
    ARGS+=(--redis "$REDIS_CONN")
fi
if [ -n "${ACTIVITY_ID:-}" ]; then
    ARGS+=(--activity "$ACTIVITY_ID")
fi

dotnet run --project "$(dirname "$0")/SeckillLedgerReconcile" -- "${ARGS[@]}"
exit $LASTEXITCODE