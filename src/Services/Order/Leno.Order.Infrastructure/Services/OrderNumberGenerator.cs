using Leno.Order.Domain.Services;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 订单号生成器实现，基于 UTC 时间戳与随机数生成业务可读的全局唯一订单编号。
/// 格式：LN{yyyyMMddHHmmss}{6位随机数}。
/// </summary>
public sealed class OrderNumberGenerator : IOrderNumberGenerator
{
    /// <inheritdoc />
    public Task<string> GenerateAsync(CancellationToken ct = default)
    {
        return Task.FromResult($"LN{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100000, 999999)}");
    }
}
