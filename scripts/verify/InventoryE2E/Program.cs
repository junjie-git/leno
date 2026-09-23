using System.Data;
using System.Security.Claims;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Leno.SharedContracts.Grpc.Inventory.V1;
using Microsoft.Data.SqlClient;

// ============================================================================
// 库存 staging E2E（双轨下线阶段 5）：gRPC 预占/确认/释放/查询 + SQL 台账断言。
// 目标：InventoryInternalService（Inventory BC，gRPC 5265，InternalApiKey 拦截器）。
// 断言采用相对值（预占前后差值），不依赖具体计数器语义。
// 退出码：0 = 全部通过；1 = 存在失败断言；2 = 连接失败。
// ============================================================================

static string? GetArg(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

var grpcEndpoint = GetArg(args, "--grpc") ?? "http://localhost:5265";
var sqlConnection = GetArg(args, "--sql") ?? "Server=localhost,1433;Database=leno_inventory;User Id=sa;Password=Leno_Local_2026!x;TrustServerCertificate=true";
var apiKey = GetArg(args, "--api-key") ?? "local-dev-internal-api-key-32bytes!!";

var failures = new List<string>();
var step = 0;

void Check(bool cond, string name)
{
    step++;
    Console.WriteLine($"{(cond ? "PASS" : "FAIL")} [{step:00}] {name}");
    if (!cond)
    {
        failures.Add(name);
    }
}

async Task<AvailableStockResponse> GetAvailableStockAsync(InventoryInternalService.InventoryInternalServiceClient client,
    string skuId, Metadata headers)
    => await client.GetAvailableStockAsync(new GetAvailableStockRequest { SkuId = skuId }, headers, cancellationToken: default);

// ---- 连接 SQL ----
await using var sql = new SqlConnection(sqlConnection);
try
{
    await sql.OpenAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL: SQL 连接失败（{sqlConnection}）：{ex.Message}");
    return 2;
}

// ---- 连接 gRPC ----
GrpcChannel channel;
InventoryInternalService.InventoryInternalServiceClient client;
try
{
    channel = GrpcChannel.ForAddress(grpcEndpoint);
    client = new InventoryInternalService.InventoryInternalServiceClient(channel);
    // 探活：查询任意 SKU（不存在的 SKU 也应返回 0 而非异常）
    var probe = await client.GetAvailableStockAsync(
        new GetAvailableStockRequest { SkuId = Guid.NewGuid().ToString() },
        new Metadata { { "X-Internal-Key", apiKey } },
        deadline: DateTime.UtcNow.AddSeconds(10));
    Console.WriteLine($"gRPC 连接成功（探测返回 available_qty={probe.AvailableQty}）");
}
catch (Exception ex)
{
    Console.WriteLine($"FATAL: gRPC 连接失败（{grpcEndpoint}）：{ex.Message}");
    return 2;
}

var headers = new Metadata { { "X-Internal-Key", apiKey } };
var orderId1 = Guid.NewGuid();
var orderId2 = Guid.NewGuid();
// 幂等键契约：① 必须是 GUID（服务端按 Guid.Parse 校验）；② 键是**操作级**去重 ——
// 一次预占、一次确认、一次释放是三个不同操作，必须用不同键；只有"同操作重放"才复用同一个键。
// （旧实现用 e2e-<hex> 非 GUID 串、且 reserve/confirm/release 共用一个键，
//   导致 confirm/release 被幂等存储直接判为重放而静默 no-op，等于这两步从未被真正验证。）
var idemKeyReserve1 = Guid.NewGuid().ToString();
var idemKeyConfirm1 = Guid.NewGuid().ToString();
var idemKeyReserve2 = Guid.NewGuid().ToString();
var idemKeyRelease2 = Guid.NewGuid().ToString();
var skuA = Guid.NewGuid();
var productId = Guid.NewGuid();

// ---- Arrange：基线 100 件（幂等：已存在则重置计数器） ----
const int baselineQty = 100;
await using (var cmd = sql.CreateCommand())
{
    cmd.CommandText = """
        IF NOT EXISTS (SELECT 1 FROM stock_baselines WHERE sku_id = @sku)
        BEGIN
            INSERT INTO stock_baselines (id, sku_id, product_id, available_qty, reserved_qty, deducted_qty, created_at, updated_at)
            VALUES (NEWID(), @sku, @product, @qty, 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
        END
        ELSE
            UPDATE stock_baselines SET available_qty = @qty, reserved_qty = 0, deducted_qty = 0, updated_at = SYSUTCDATETIME()
            WHERE sku_id = @sku;
        DELETE FROM stock_reservations WHERE order_id IN (@o1, @o2);
        """;
    cmd.Parameters.Add("@sku", SqlDbType.UniqueIdentifier).Value = skuA;
    cmd.Parameters.Add("@product", SqlDbType.UniqueIdentifier).Value = productId;
    cmd.Parameters.Add("@qty", SqlDbType.Int).Value = baselineQty;
    cmd.Parameters.Add("@o1", SqlDbType.UniqueIdentifier).Value = orderId1;
    cmd.Parameters.Add("@o2", SqlDbType.UniqueIdentifier).Value = orderId2;
    await cmd.ExecuteNonQueryAsync();
}

Console.WriteLine($"Arrange 完成：SKU={skuA} 基线={baselineQty}，订单 {orderId1}/{orderId2}");

// ---- 01 基线可查 ----
var v0 = (await GetAvailableStockAsync(client, skuA.ToString(), headers)).AvailableQty;
Check(v0 == baselineQty, $"01 基线可查：available={v0}（期望 {baselineQty}）");

// ---- 02 预占 5 件 ----
var reserve1 = await client.ReserveStockAsync(new ReserveStockRequest
{
    OrderId = orderId1.ToString(),
    IdempotencyKey = idemKeyReserve1,
    Items = { new ReserveStockItem { SkuId = skuA.ToString(), Quantity = 5 } }
}, headers, deadline: DateTime.UtcNow.AddSeconds(15));
Check(reserve1.Success && !string.IsNullOrEmpty(reserve1.ReservationId),
    $"02 预占成功：success={reserve1.Success} reservationId={reserve1.ReservationId} reason={reserve1.FailureReason}");

// ---- 03 预占后可用量 -5 ----
var v1 = (await GetAvailableStockAsync(client, skuA.ToString(), headers)).AvailableQty;
Check(v1 == v0 - 5, $"03 预占扣减可用量：{v0} → {v1}（期望 -5）");

// ---- 04 SQL 台账：order1 存在 Reserved 记录 ----
var (cnt1, reservedQty1, status1) = await QueryReservationAsync(sql, orderId1);
Check(cnt1 == 1 && reservedQty1 == 5, $"04 台账记录：count={cnt1} reservedQty={reservedQty1}（期望 1/5）");
Check(status1 == 0, $"04b 台账状态为 Reserved(0)：status={status1}");

// ---- 05 幂等：同 idempotency_key 重复预占 → 同一 reservation_id ----
var reserve1Again = await client.ReserveStockAsync(new ReserveStockRequest
{
    OrderId = orderId1.ToString(),
    IdempotencyKey = idemKeyReserve1,
    Items = { new ReserveStockItem { SkuId = skuA.ToString(), Quantity = 5 } }
}, headers, deadline: DateTime.UtcNow.AddSeconds(15));
Check(reserve1Again.Success && reserve1Again.ReservationId == reserve1.ReservationId,
    $"05 幂等重放：reservationId 一致（{reserve1Again.ReservationId == reserve1.ReservationId}）");
var v1b = (await GetAvailableStockAsync(client, skuA.ToString(), headers)).AvailableQty;
Check(v1b == v1, $"05b 幂等不重复占用：available={v1b}（期望 {v1}）");

// ---- 06 确认扣减（新幂等键：与预占是不同操作）----
var confirm1 = await client.ConfirmStockAsync(new ConfirmStockRequest
{
    OrderId = orderId1.ToString(),
    IdempotencyKey = idemKeyConfirm1
}, headers, deadline: DateTime.UtcNow.AddSeconds(15));
Check(confirm1.Success, $"06 确认扣减成功：reason={confirm1.FailureReason}");
var v2 = (await GetAvailableStockAsync(client, skuA.ToString(), headers)).AvailableQty;
Check(v2 == v1, $"07 确认后可用量不变：{v2}（期望 {v1}，已扣减进入 deducted）");

// ---- 08 SQL 台账：order1 状态迁移为终态 ----
var (cnt1b, _, status1b) = await QueryReservationAsync(sql, orderId1);
Check(cnt1b == 1, $"08 台账仍为单条（确认幂等）：count={cnt1b}");
Check(status1b == 1, $"08b 台账状态迁移为 Confirmed(1)：status={status1b}（证明确认真的执行，而非被幂等跳过）");

// ---- 09 预占 3 件后释放 ----
var reserve2 = await client.ReserveStockAsync(new ReserveStockRequest
{
    OrderId = orderId2.ToString(),
    IdempotencyKey = idemKeyReserve2,
    Items = { new ReserveStockItem { SkuId = skuA.ToString(), Quantity = 3 } }
}, headers, deadline: DateTime.UtcNow.AddSeconds(15));
Check(reserve2.Success, $"09 二次预占成功：reason={reserve2.FailureReason}");
var v3 = (await GetAvailableStockAsync(client, skuA.ToString(), headers)).AvailableQty;
Check(v3 == v2 - 3, $"09b 预占扣减：{v2} → {v3}（期望 -3）");

// ---- 10 释放预占（新幂等键）----
var release2 = await client.ReleaseStockAsync(new ReleaseStockRequest
{
    OrderId = orderId2.ToString(),
    IdempotencyKey = idemKeyRelease2,
    OperationType = 0 // Release（proto 0 = ReleaseStockOperationType.Release）
}, headers, deadline: DateTime.UtcNow.AddSeconds(15));
Check(release2.Success, $"10 释放成功：reason={release2.FailureReason}");
var v4 = (await GetAvailableStockAsync(client, skuA.ToString(), headers)).AvailableQty;
Check(v4 == v2, $"11 释放归还可用量：{v3} → {v4}（期望回到 {v2}）");
var (cnt2, _, status2) = await QueryReservationAsync(sql, orderId2);
Check(cnt2 == 1 && status2 == 2, $"11b 台账状态迁移为 Released(2)：count={cnt2} status={status2}");

// ---- 12 超卖拒绝：预占 10000 件（仅剩 95+50?）→ success=false ----
var oversell = await client.ReserveStockAsync(new ReserveStockRequest
{
    OrderId = Guid.NewGuid().ToString(),
    IdempotencyKey = Guid.NewGuid().ToString(),
    Items = { new ReserveStockItem { SkuId = skuA.ToString(), Quantity = 1_000_000 } }
}, headers, deadline: DateTime.UtcNow.AddSeconds(15));
Check(!oversell.Success, $"12 超卖拒绝：success={oversell.Success} reason={oversell.FailureReason}");

// ---- 结果 ----
if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"E2E 失败（{failures.Count} 项）：");
    failures.ForEach(f => Console.WriteLine("  ✗ " + f));
    return 1;
}

Console.WriteLine();
Console.WriteLine("E2E 全部通过 ✓（gRPC 预占/确认/释放/查询 + 幂等 + 超卖拒绝 + SQL 台账）");
return 0;

static async Task<(int Count, int ReservedQty, int Status)> QueryReservationAsync(SqlConnection sql, Guid orderId)
{
    await using var cmd = sql.CreateCommand();
    // 状态也要读出来：只断言行数无法区分"确认/释放真的执行了"与"被幂等存储跳过"（两者行数都是 1）
    cmd.CommandText = "SELECT COUNT(*), ISNULL(SUM(quantity), 0), ISNULL(MAX(status), -1) FROM stock_reservations WHERE order_id = @o";
    cmd.Parameters.Add("@o", SqlDbType.UniqueIdentifier).Value = orderId;
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        return (0, 0, -1);
    }
    return (reader.GetInt32(0), reader.IsDBNull(1) ? 0 : reader.GetInt32(1), reader.IsDBNull(2) ? -1 : reader.GetInt32(2));
}
