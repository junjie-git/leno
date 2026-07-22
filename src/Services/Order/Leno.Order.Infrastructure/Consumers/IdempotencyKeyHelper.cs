using System.Security.Cryptography;
using System.Text;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 幂等键工具：将业务幂等键字符串（如 "stock-confirm-{PaymentId}"、"order-timeout-{OrderId}"）
/// 转换为稳定的 Guid，供 <see cref="Leno.Infrastructure.Abstractions.IIdempotencyStore"/>（基于 Guid 的接口）使用。
/// 同一字符串始终映射到同一 Guid，保证跨重试、跨消费者的幂等键稳定且唯一。
/// </summary>
internal static class IdempotencyKeyHelper
{
    /// <summary>
    /// 将字符串幂等键转换为确定性 Guid（基于 SHA-256 哈希前 16 字节）。
    /// 同一输入始终产生同一 Guid，不同输入产生不同 Guid，满足幂等去重存储的 Guid 键要求。
    /// </summary>
    /// <param name="key">业务幂等键字符串，需非空。</param>
    /// <returns>稳定的 Guid。</returns>
    public static Guid ToDeterministicGuid(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        return new Guid(bytes);
    }
}
