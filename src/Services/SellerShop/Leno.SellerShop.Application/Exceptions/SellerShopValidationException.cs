using Leno.SharedKernel.Exceptions;

namespace Leno.SellerShop.Application.Exceptions;

/// <summary>
/// 应用层输入校验异常，继承领域异常基类以经全局异常中间件统一映射为 400。
/// </summary>
public sealed class SellerShopValidationException : DomainException
{
    public SellerShopValidationException(string message)
        : base(message, "SELLER_SHOP_VALIDATION_ERROR", 400)
    {
    }

    public SellerShopValidationException(IEnumerable<string> errors)
        : base(string.Join(" | ", errors), "SELLER_SHOP_VALIDATION_ERROR", 400)
    {
    }
}
