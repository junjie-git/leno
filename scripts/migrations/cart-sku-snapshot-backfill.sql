-- ============================================================
-- 阶段三 3.11：cart_items SKU 快照回填脚本
-- 数据库：SQL Server（购物车域 LenoCart）
-- 执行时机：EF 迁移 20260723000001_AddCartItemSkuSnapshot 部署后，
--           启用 Cart:UseSkuSnapshot=true 前或灰度期间执行
-- 幂等性：只读统计，可重复执行
-- ============================================================
-- 背景：
--   迁移 20260723000001 为 cart_items 表新增 sku_snapshot_* 列（全部可空）。
--   历史购物车项的 SkuSnapshot 为 NULL，读取路径回退实时跨进程调用。
--   价格/币种/可售状态需从商品域获取，无法纯 SQL 回填（商品域为真源）。
--
-- 回填策略（运行时自动，非 SQL）：
--   本脚本不执行数据 UPDATE，因为部分回填（设置展示字段但价格 NULL）会导致
--   EF Core 物化 Price=0 的不完整快照，误导用户看到 0 元商品。
--   正确回填路径为运行时后台刷新：
--     1. 启用 Cart:UseSkuSnapshot=true 后，SnapshotCartPriceService 检测缺失快照
--     2. 回退实时调用获取价格，同时入队 SkuSnapshotRefreshQueue 后台刷新
--     3. 后台队列批量调用商品域 ACL，完整回填快照（含价格/币种/可售状态）
--     4. ProductSkuUpdatedEventConsumer 消费商品域事件持续更新快照
--
--   加速回填（可选）：通过管理接口向 IBackgroundSnapshotRefresher.EnqueueRefreshBatch
--   传入所有 distinct sku_id，触发批量后台刷新，缩短回填窗口。
-- ============================================================

-- 1. 回填状态统计：待回填的购物车项数量与进度
SELECT '回填进度' AS phase,
       COUNT(*) AS total_cart_items,
       SUM(CASE WHEN sku_snapshot_at IS NULL THEN 1 ELSE 0 END) AS items_needing_backfill,
       SUM(CASE WHEN sku_snapshot_at IS NOT NULL THEN 1 ELSE 0 END) AS items_backfilled,
       CASE
           WHEN COUNT(*) > 0 THEN ROUND(100.0 * SUM(CASE WHEN sku_snapshot_at IS NOT NULL THEN 1 ELSE 0 END) / COUNT(*), 2)
           ELSE 0
       END AS backfill_percent
FROM cart_items;

-- 2. 待回填 SKU 清单（distinct sku_id，用于手动触发批量刷新的输入）
SELECT DISTINCT sku_id
FROM cart_items
WHERE sku_snapshot_at IS NULL
ORDER BY sku_id;

-- 3. 一致性校验：检查是否存在 SkuId 不一致的异常数据（应为 0）
SELECT '一致性校验' AS check_name,
       COUNT(*) AS mismatch_count
FROM cart_items
WHERE sku_snapshot_sku_id IS NOT NULL
  AND sku_snapshot_sku_id <> sku_id;

-- ============================================================
-- 操作流程
--
-- 步骤 1：部署迁移
--   dotnet ef database update --project src/Services/Cart/Leno.Cart.Infrastructure
--   （或应用启动时 MigrateWithLockAsync 自动执行）
--
-- 步骤 2：灰度启用快照模式
--   方式 A（appsettings.json）："Cart": { "UseSkuSnapshot": true }
--   方式 B（Consul KV 热更新）：leno/cart/UseSkuSnapshot = true
--   建议先灰度 10% 流量验证，观察后台刷新队列与价格一致性
--
-- 步骤 3：监控回填进度
--   重复执行本脚本第 1 节查询，items_needing_backfill 趋近 0 即回填完成
--   预期回填速率：RefreshConcurrency(3) × RefreshBatchSize(50) = 150 SKU/批
--
-- 步骤 4（可选）：加速回填
--   通过管理 API 或脚本向 IBackgroundSnapshotRefresher 注入所有待回填 sku_id
--   触发即时批量刷新，无需等待用户访问触发
--
-- 步骤 5：回滚预案
--   设 Cart:UseSkuSnapshot=false 即恢复旧路径（实时跨进程调用）
--   cart_items 表 sku_snapshot_* 列保留不影响旧路径（CartPriceService 不读这些列）
-- ============================================================
