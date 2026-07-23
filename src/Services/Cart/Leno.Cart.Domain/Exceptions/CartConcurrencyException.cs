using Leno.SharedKernel.Exceptions;

namespace Leno.Cart.Domain.Exceptions;

/// <summary>
/// 匿名购物车 CAS 乐观并发冲突异常（P1-1 修复）。
/// <para>
/// 当 <c>RedisAnonymousCartRepository.SaveAsync</c>（无 expectedVersion 重载）检测到 Redis Hash 中的 version 字段
/// 与聚合 <c>Revision</c> 不一致时抛出，表示另一并发请求已先于本请求修改了购物车，
/// 本请求的写入被拒绝以避免覆盖写丢失更新。
/// </para>
/// <para>
/// 调用方可捕获此异常后重新 <c>GetAsync</c> 加载最新购物车、重新应用业务操作后重试保存，
/// 或将冲突向上传递由全局异常中间件映射为 HTTP 409 Conflict。
/// </para>
/// </summary>
public sealed class CartConcurrencyException : DomainException
{
    /// <summary>客户端期望的版本号（基于上次加载时的 cart.Revision）。</summary>
    public int ExpectedVersion { get; }

    /// <summary>Redis 中实际的版本号（被另一并发请求递增后的值）。</summary>
    public int ActualVersion { get; }

    public CartConcurrencyException(string message, int expectedVersion, int actualVersion)
        : base(message, "CART_CONFLICT")
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }

    public CartConcurrencyException(string message, int expectedVersion, int actualVersion, Exception innerException)
        : base(message, innerException, "CART_CONFLICT")
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}
