// 秒杀结算单向校验（双轨下线 · 秒杀结算收口，2026-09-24）。
//
// 背景：秒杀链路的 Redis 配额只做「准入控制」，库存权威在 Inventory 的 SQL 台账。
// 订单真正落地时必须落台账（Order 侧 SeckillOrderCreationService 调 IInventoryGateway.ReserveBatchAsync），
// 否则 Inventory 的可用量会高估"秒杀已售"部分 → 超卖。
//
// 本工具校验两条不变量：
//   1) 台账覆盖（必须）：Promotion 每条未回退的预占记录，都能在 Inventory 台账按 (order_id, sku_id)
//      找到记录，且 quantity 一致、状态不是 Released/Returned（后者说明"台账已释放但 Promotion 未回退"，口径分叉）。
//   2) 配额上限（需 --redis）：每个活动「台账量 ≤ 已扣减配额」，已扣减配额 = total_stock - Redis 剩余。
//
// 退出码：0 = 全部通过；1 = 存在违反；2 = 连接失败。
using System.Data;
using Microsoft.Data.SqlClient;
using StackExchange.Redis;

var opts = ParseArgs(args);
if (opts is null)
{
    Console.Error.WriteLine("用法: SeckillLedgerReconcile --promo-sql <conn> --inventory-sql <conn> [--redis <conn>] [--activity <guid>] [--hours 24]");
    return 2;
}

var since = DateTime.UtcNow.AddHours(-opts.Hours);
var failures = new List<string>();
var checks = 0;

Console.WriteLine("秒杀结算单向校验（Promotion 预占记录 ↔ Inventory 台账 ↔ Redis 配额）");
Console.WriteLine($"窗口: {since:O} 起（近 {opts.Hours} 小时）" + (opts.ActivityId is null ? "" : $"，活动: {opts.ActivityId}"));
Console.WriteLine();

// ---------- 读取 Promotion 侧预占记录 ----------
List<(Guid ActivityId, Guid OrderId, Guid SkuId, int Quantity)> promoRecords;
try
{
    promoRecords = await LoadPromotionRecordsAsync(opts.PromoConnectionString, since, opts.ActivityId);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"连接/查询 Promotion 库失败: {ex.Message}");
    return 2;
}

Console.WriteLine($"Promotion 未回退预占记录: {promoRecords.Count} 条");

// ---------- 读取 Inventory 台账 ----------
Dictionary<(Guid OrderId, Guid SkuId), (int Quantity, int Status)> ledger;
try
{
    ledger = await LoadLedgerAsync(opts.InventoryConnectionString, since);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"连接/查询 Inventory 库失败: {ex.Message}");
    return 2;
}

Console.WriteLine($"Inventory 台账条目: {ledger.Count} 条");
Console.WriteLine();

// ---------- 不变量 1：台账覆盖 ----------
foreach (var r in promoRecords)
{
    checks++;
    var label = $"[覆盖] activity={r.ActivityId} order={r.OrderId} sku={r.SkuId} qty={r.Quantity}";
    if (!ledger.TryGetValue((r.OrderId, r.SkuId), out var entry))
    {
        failures.Add($"{label} → 台账缺失（配额已放行但订单未落台账）");
        continue;
    }

    if (entry.Quantity != r.Quantity)
    {
        failures.Add($"{label} → 台账数量不一致（台账={entry.Quantity}）");
    }

    if (entry.Status is 2 or 3)
    {
        failures.Add($"{label} → 台账已 {(entry.Status == 2 ? "Released" : "Returned")}，但 Promotion 记录未标记 IsRolledBack（口径分叉）");
    }
}

Console.WriteLine($"不变量 1（台账覆盖）: {promoRecords.Count} 条记录，失败 {failures.Count} 条");
Console.WriteLine();

// ---------- 不变量 2：配额上限（需 Redis）----------
if (string.IsNullOrWhiteSpace(opts.RedisConnectionString))
{
    Console.WriteLine("不变量 2（配额上限）: 未提供 --redis，跳过（仅校验台账覆盖）");
}
else
{
    ConnectionMultiplexer redis;
    try
    {
        redis = await ConnectionMultiplexer.ConnectAsync(opts.RedisConnectionString);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"连接 Redis 失败: {ex.Message}");
        return 2;
    }

    var activities = await LoadActivitiesAsync(opts.PromoConnectionString, opts.ActivityId);
    var db = redis.GetDatabase();
    var quotaFailures = 0;
    var asserted = 0;

    foreach (var a in activities)
    {
        // Redis 未初始化（活动未激活）→ 无从计算已扣减配额，显式跳过而非静默通过
        var remainingRaw = await db.HashGetAsync($"seckill:{a.ActivityId}:stock", a.SkuId.ToString());
        if (remainingRaw.IsNull)
        {
            Console.WriteLine($"  [配额] activity={a.ActivityId} 无 Redis 库存键（活动未激活？）→ 跳过");
            continue;
        }

        var remaining = (int)remainingRaw;
        var deductedQuota = a.TotalStock - remaining;

        // 台账量：该活动未回退记录中，已落台账且状态为 Reserved/Confirmed 的数量合计
        var ledgerQty = 0;
        foreach (var r in promoRecords.Where(x => x.ActivityId == a.ActivityId))
        {
            if (ledger.TryGetValue((r.OrderId, r.SkuId), out var entry) && entry.Status is 0 or 1)
            {
                ledgerQty += entry.Quantity;
            }
        }

        asserted++;
        var pass = ledgerQty <= deductedQuota;
        Console.WriteLine($"  [配额] activity={a.ActivityId} total={a.TotalStock} redis剩余={remaining} 已扣减配额={deductedQuota} 台账={ledgerQty} → {(pass ? "PASS" : "FAIL")}");

        if (!pass)
        {
            quotaFailures++;
            failures.Add($"[配额] activity={a.ActivityId} 台账 {ledgerQty} > 已扣减配额 {deductedQuota}");
        }
    }

    redis.Dispose();
    Console.WriteLine($"不变量 2（配额上限）: 断言 {asserted} 个活动，失败 {quotaFailures} 个");
}

Console.WriteLine();
if (failures.Count > 0)
{
    Console.WriteLine($"❌ 校验失败：{failures.Count} 项（断言总数 {checks} + 配额活动）");
    foreach (var f in failures.Take(50))
    {
        Console.WriteLine($"   {f}");
    }

    return 1;
}

Console.WriteLine("✅ 秒杀结算单向校验通过（台账覆盖完整、台账量未超出已扣减配额）。");
return 0;

// ---------------------------------------------------------------- helpers

static Options? ParseArgs(string[] args)
{
    string? promo = null, inventory = null, redis = null, activity = null;
    var hours = 24;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--promo-sql" when i + 1 < args.Length:
                promo = args[++i];
                break;
            case "--inventory-sql" when i + 1 < args.Length:
                inventory = args[++i];
                break;
            case "--redis" when i + 1 < args.Length:
                redis = args[++i];
                break;
            case "--activity" when i + 1 < args.Length:
                activity = args[++i];
                break;
            case "--hours" when i + 1 < args.Length:
                if (!int.TryParse(args[++i], out hours) || hours <= 0)
                {
                    return null;
                }

                break;
            default:
                return null;
        }
    }

    if (string.IsNullOrWhiteSpace(promo) || string.IsNullOrWhiteSpace(inventory))
    {
        return null;
    }

    Guid? activityId = null;
    if (!string.IsNullOrWhiteSpace(activity))
    {
        if (!Guid.TryParse(activity, out var parsed))
        {
            return null;
        }

        activityId = parsed;
    }

    return new Options(promo, inventory, redis, activityId, hours);
}

static async Task<List<(Guid ActivityId, Guid OrderId, Guid SkuId, int Quantity)>> LoadPromotionRecordsAsync(
    string connectionString, DateTime since, Guid? activityId)
{
    var result = new List<(Guid, Guid, Guid, int)>();
    await using var conn = new SqlConnection(connectionString);
    await conn.OpenAsync();

    var sql = """
        SELECT ActivityId, OrderId, SkuId, Quantity
        FROM seckill_pre_occupation_records
        WHERE IsRolledBack = 0 AND PreOccupiedAt >= @since
        """ + (activityId is null ? string.Empty : " AND ActivityId = @activity");

    await using var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.Add(new SqlParameter("@since", SqlDbType.DateTime2) { Value = since });
    if (activityId is not null)
    {
        cmd.Parameters.Add(new SqlParameter("@activity", SqlDbType.UniqueIdentifier) { Value = activityId.Value });
    }

    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        result.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetInt32(3)));
    }

    return result;
}

static async Task<List<(Guid ActivityId, Guid SkuId, int TotalStock)>> LoadActivitiesAsync(
    string connectionString, Guid? activityId)
{
    var result = new List<(Guid, Guid, int)>();
    await using var conn = new SqlConnection(connectionString);
    await conn.OpenAsync();

    var sql = "SELECT id, sku_id, total_stock FROM seckill_activities"
        + (activityId is null ? string.Empty : " WHERE id = @activity");

    await using var cmd = new SqlCommand(sql, conn);
    if (activityId is not null)
    {
        cmd.Parameters.Add(new SqlParameter("@activity", SqlDbType.UniqueIdentifier) { Value = activityId.Value });
    }

    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        result.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2)));
    }

    return result;
}

static async Task<Dictionary<(Guid OrderId, Guid SkuId), (int Quantity, int Status)>> LoadLedgerAsync(
    string connectionString, DateTime since)
{
    var result = new Dictionary<(Guid, Guid), (int, int)>();
    await using var conn = new SqlConnection(connectionString);
    await conn.OpenAsync();

    await using var cmd = new SqlCommand(
        "SELECT order_id, sku_id, quantity, status FROM stock_reservations WHERE created_at >= @since", conn);
    cmd.Parameters.Add(new SqlParameter("@since", SqlDbType.DateTime2) { Value = since });

    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        result[(reader.GetGuid(0), reader.GetGuid(1))] = (reader.GetInt32(2), reader.GetInt32(3));
    }

    return result;
}

internal sealed record Options(
    string PromoConnectionString,
    string InventoryConnectionString,
    string? RedisConnectionString,
    Guid? ActivityId,
    int Hours);