using Leno.SharedKernel.Exceptions;

namespace Leno.SellerShop.Domain.Exceptions;

/// <summary>
/// 卖家与店铺管理域领域异常，携带业务错误码与映射 HTTP 状态码。
/// 由全局异常中间件转换为标准 <c>ApiResponse</c>。
/// </summary>
public sealed class SellerShopDomainException : DomainException
{
    public SellerShopDomainException(string message, string errorCode = "SELLER_SHOP_DOMAIN_ERROR", int httpStatusCode = 400)
        : base(message, errorCode, httpStatusCode)
    {
    }

    public SellerShopDomainException(string message, Exception innerException, string errorCode = "SELLER_SHOP_DOMAIN_ERROR", int httpStatusCode = 400)
        : base(message, innerException, errorCode, httpStatusCode)
    {
    }
}
