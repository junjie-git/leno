using Leno.Order.Domain.Services;

namespace Leno.Order.Infrastructure.Services;

/// <summary>
/// 订单号生成器实现，基于 UTC 时间戳（含毫秒）+ 机器位 + 随机数生成业务可读的全局唯一订单编号。
/// 格式：LN{yyyyMMddHHmmssfff}{4位机器位 base36}{4位随机数}。
/// P2-T30：原格式 LN{yyyyMMddHHmmss}{6位随机数} 在同秒内仅 100w 随机空间，1000 单/秒碰撞概率显著；
/// 新格式引入毫秒时间戳（同毫秒内冲突概率降低 1000 倍）与机器位（多机部署无冲突），
/// 单机单毫秒内仍有 9000 个随机槽位，DB 唯一索引（ix_orders_order_no）兜底保证最终唯一。
/// </summary>
public sealed class OrderNumberGenerator : IOrderNumberGenerator
{
    /// <summary>base36 字符表，用于机器位编码（10 数字 + 26 字母 = 36 个字符）。</summary>
    private const string Base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// 机器位 4 字符 base36 编码，进程级缓存（同进程内不变）。
    /// 由 hostname 的 SHA256 哈希取低 32 位后模 36^4 得到，多机部署时不同 hostname 概率性映射到不同机器位。
    /// 36^4 = 1,679,616 个机器槽位，单集群千台规模下碰撞概率可接受。
    /// </summary>
    private readonly string _machineTag = ComputeMachineTag();

    /// <inheritdoc />
    public Task<string> GenerateAsync(CancellationToken ct = default)
    {
        // 17 位时间戳（含毫秒）+ 4 位机器位 + 4 位随机数（1000-9999）
        // 同毫秒同机器 9000 个随机槽位，DB 唯一索引兜底保证最终唯一
        var orderNo = $"LN{DateTime.UtcNow:yyyyMMddHHmmssfff}{_machineTag}{Random.Shared.Next(1000, 10000)}";
        return Task.FromResult(orderNo);
    }

    /// <summary>
    /// 计算 4 字符 base36 机器位标识，基于 hostname 的 SHA256 哈希。
    /// 进程内缓存，避免每次生成订单号时重复计算哈希。
    /// </summary>
    private static string ComputeMachineTag()
    {
        var hostName = Environment.MachineName;
        if (string.IsNullOrEmpty(hostName))
        {
            hostName = Environment.GetEnvironmentVariable("HOSTNAME") ?? "unknown";
        }

        // SHA256 哈希 hostname，取前 8 字节作为 ulong，再模 36^4 得到 0..1,679,615 的整数
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hostName));
        var hashValue = BitConverter.ToUInt64(hashBytes, 0);
        var machineNumber = (int)(hashValue % 1679616L); // 36^4 = 1,679,616

        // 转换为 4 字符 base36（低位在前，左侧补 0）
        var chars = new char[4];
        for (var i = 3; i >= 0; i--)
        {
            chars[i] = Base36Chars[machineNumber % 36];
            machineNumber /= 36;
        }
        return new string(chars);
    }
}
